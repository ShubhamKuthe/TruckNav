using System.Buffers;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TruckNav.Connector.Core.Models;
using TruckNav.Connector.Core.Utilities;
using TruckNav.Connector.Networking.Discovery;
using TruckNav.Connector.Networking.Identity;
using TruckNav.Connector.Networking.Models;
using TruckNav.Connector.Networking.Protocol;
using TruckNav.Connector.Networking.Protocol.Messages;
using TruckNav.Connector.Networking.Session;

namespace TruckNav.Connector.Networking.Hosting;

public sealed class ConnectorServerHostedService : IHostedService, IConnectorServer
{
    private readonly Discovery.IDiscoveryAdvertiser _discoveryAdvertiser;
    private readonly Identity.IConnectorIdentityProvider _identityProvider;
    private readonly ILogger<ConnectorServerHostedService> _logger;
    private readonly IProtocolHandler _protocolHandler;
    private readonly IConnectorSessionStore _sessionStore;
    private readonly object _sessionGate = new();

    private WebApplication? _serverApplication;
    private IConnectionSession? _activeSession;
    private GameDetectionSnapshot _lastBroadcastGameDetection = GameDetectionSnapshot.Empty;

    public ConnectorServerHostedService(
        IConnectorIdentityProvider identityProvider,
        IProtocolHandler protocolHandler,
        IConnectorSessionStore sessionStore,
        IDiscoveryAdvertiser discoveryAdvertiser,
        ILogger<ConnectorServerHostedService> logger)
    {
        _identityProvider = identityProvider;
        _protocolHandler = protocolHandler;
        _sessionStore = sessionStore;
        _discoveryAdvertiser = discoveryAdvertiser;
        _logger = logger;

        _sessionStore.SnapshotChanged += (_, snapshot) => _ = BroadcastGameStateIfNeededAsync(snapshot);
    }

    public Uri? ServiceUri { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_serverApplication is not null)
        {
            return;
        }

        var localIdentity = _identityProvider.GetIdentity();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(0));

        var application = builder.Build();
        application.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TruckNavNetworkDefaults.WebSocketKeepAliveInterval
        });

        application.MapGet("/health", () => Results.Json(new { status = "ok" }));
        application.MapGet("/info", () => Results.Json(localIdentity));
        application.Map("/ws", HandleWebSocketRequestAsync);

        await application.StartAsync(cancellationToken);

        var listenerPort = ResolveListenerPort(application);
        ServiceUri = new UriBuilder(Uri.UriSchemeHttp, "127.0.0.1", listenerPort).Uri;
        _serverApplication = application;

        var lanAddresses = GetReachableLanAddresses();
        if (lanAddresses.Count == 0)
        {
            _logger.LogWarning(
                "TruckNav Connector started on port {Port}, but no reachable LAN addresses were detected. Local discovery or firewall validation may fail.",
                listenerPort);
        }
        else
        {
            _logger.LogInformation(
                "TruckNav Connector started. Port={Port}, BindStrategy=ListenAnyIP, LAN={LanAddresses}.",
                listenerPort,
                string.Join(", ", lanAddresses));
        }

        await _discoveryAdvertiser.StartAsync(localIdentity, listenerPort, cancellationToken);

        _logger.LogInformation("TruckNav Connector loopback diagnostics URI is {ServiceUri}.", ServiceUri);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _discoveryAdvertiser.StopAsync(cancellationToken);

        if (_serverApplication is not null)
        {
            await _serverApplication.StopAsync(cancellationToken);
            await _serverApplication.DisposeAsync();
            _serverApplication = null;
            ServiceUri = null;
        }
    }

    private async Task HandleWebSocketRequestAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        _logger.LogInformation(
            "Incoming TruckNav WebSocket request from {RemoteIpAddress}.",
            context.Connection.RemoteIpAddress);
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        await HandleSessionAsync(socket, context.RequestAborted);
    }

    private async Task HandleSessionAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        IConnectionSession? session = null;
        ConnectorSessionReservation? reservation = null;
        var sessionActivated = false;

        try
        {
            var firstMessage = await ReceiveTextMessageAsync(socket, cancellationToken);
            if (firstMessage.IsClosed)
            {
                return;
            }

            if (firstMessage.ErrorCode is not null)
            {
                await SendErrorAndCloseAsync(socket, firstMessage.ErrorCode, firstMessage.ErrorMessage!, cancellationToken);
                return;
            }

            var readResult = _protocolHandler.ReadClientMessage(firstMessage.Payload!);
            if (readResult.Error is not null)
            {
                await SendErrorAndCloseAsync(socket, readResult.Error.Code, readResult.Error.Message, cancellationToken);
                return;
            }

            if (readResult.Message is not HelloMessage helloMessage)
            {
                await SendErrorAndCloseAsync(
                    socket,
                    "invalid_payload",
                    "The first WebSocket message must be a hello payload.",
                    cancellationToken);
                return;
            }

            var remotePeer = ToPeerInfo(helloMessage);
            _logger.LogInformation(
                "TruckNav hello received from {DeviceName} ({DeviceId}).",
                remotePeer.DeviceName,
                remotePeer.DeviceId);

            var validation = _protocolHandler.ValidateHello(helloMessage);
            if (!validation.IsValid)
            {
                _sessionStore.RecordIssue(validation.Error!.Code, validation.Error.Message, remotePeer);
                _logger.LogWarning(
                    "Rejected TruckNav mobile hello from {DeviceName} ({DeviceId}): {Reason}",
                    remotePeer.DeviceName,
                    remotePeer.DeviceId,
                    validation.Error.Message);
                await SendErrorAndCloseAsync(
                    socket,
                    validation.Error.Code,
                    validation.Error.Message,
                    cancellationToken);
                return;
            }

            if (!_sessionStore.TryReserveSession(remotePeer, out reservation))
            {
                _sessionStore.RecordIssue("busy", "Another mobile client is already connected.", remotePeer);
                _logger.LogInformation(
                    "Rejected TruckNav mobile hello from {DeviceName} ({DeviceId}) because another session is active or finishing handshake.",
                    remotePeer.DeviceName,
                    remotePeer.DeviceId);
                await SendErrorAndCloseAsync(
                    socket,
                    "busy",
                    "TruckNav Connector currently supports one active mobile session.",
                    cancellationToken);
                return;
            }

            _logger.LogInformation(
                "TruckNav hello accepted from {DeviceName} ({DeviceId}).",
                remotePeer.DeviceName,
                remotePeer.DeviceId);

            session = new WebSocketConnectionSession(socket, remotePeer);
            await session.SendAsync(_protocolHandler.CreateHelloAck(), cancellationToken);
            if (!_sessionStore.TryActivateSession(reservation, session))
            {
                throw new InvalidOperationException("The pending TruckNav handshake reservation was lost before activation.");
            }

            sessionActivated = true;
            SetActiveSession(session);
            await SendGameStateAsync(session, _sessionStore.Snapshot.GameDetection, cancellationToken);
            _logger.LogInformation(
                "TruckNav mobile connected: {DeviceName} ({DeviceId}).",
                session.RemotePeer.DeviceName,
                session.RemotePeer.DeviceId);

            while (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
            {
                var message = await ReceiveTextMessageAsync(socket, cancellationToken);
                if (message.IsClosed)
                {
                    var intentional = string.Equals(
                        message.CloseDescription,
                        TruckNavNetworkDefaults.IntentionalDisconnectReason,
                        StringComparison.Ordinal);
                    _sessionStore.MarkSessionClosed(session, intentional, message.CloseDescription);
                    _logger.LogInformation(
                        intentional
                            ? "TruckNav mobile disconnected intentionally: {DeviceName} ({DeviceId})."
                            : "TruckNav mobile disconnected unexpectedly: {DeviceName} ({DeviceId}).",
                        session.RemotePeer.DeviceName,
                        session.RemotePeer.DeviceId);
                    return;
                }

                if (message.ErrorCode is not null)
                {
                    _sessionStore.RecordIssue(message.ErrorCode, message.ErrorMessage!, session.RemotePeer);
                    await session.CloseAsync(WebSocketCloseStatus.PolicyViolation, message.ErrorMessage, cancellationToken);
                    _sessionStore.MarkSessionClosed(session, intentional: false, message.ErrorMessage);
                    _logger.LogWarning(
                        "TruckNav session closed after protocol read error from {DeviceName} ({DeviceId}): {Reason}",
                        session.RemotePeer.DeviceName,
                        session.RemotePeer.DeviceId,
                        message.ErrorMessage);
                    return;
                }

                var parsed = _protocolHandler.ReadClientMessage(message.Payload!);
                if (parsed.Error is not null)
                {
                    _sessionStore.RecordIssue(parsed.Error.Code, parsed.Error.Message, session.RemotePeer);
                    await SendProtocolErrorAsync(session, parsed.Error.Code, parsed.Error.Message, cancellationToken);
                    await session.CloseAsync(WebSocketCloseStatus.PolicyViolation, parsed.Error.Message, cancellationToken);
                    _sessionStore.MarkSessionClosed(session, intentional: false, parsed.Error.Message);
                    _logger.LogWarning(
                        "TruckNav session closed after protocol validation error from {DeviceName} ({DeviceId}): {Reason}",
                        session.RemotePeer.DeviceName,
                        session.RemotePeer.DeviceId,
                        parsed.Error.Message);
                    return;
                }

                switch (parsed.Message)
                {
                    case PingMessage:
                        await session.SendAsync(_protocolHandler.CreatePong(), cancellationToken);
                        break;
                    case GameStateRequestMessage:
                        await SendGameStateAsync(session, _sessionStore.Snapshot.GameDetection, cancellationToken);
                        break;
                    default:
                        const string unsupportedMessage = "Only ping and game_state_request messages are supported after handshake in Phase 3.";
                        _sessionStore.RecordIssue("unsupported_message_type", unsupportedMessage, session.RemotePeer);
                        await SendProtocolErrorAsync(session, "unsupported_message_type", unsupportedMessage, cancellationToken);
                        await session.CloseAsync(WebSocketCloseStatus.PolicyViolation, unsupportedMessage, cancellationToken);
                        _sessionStore.MarkSessionClosed(session, intentional: false, unsupportedMessage);
                        _logger.LogWarning(
                            "TruckNav session closed after unsupported message from {DeviceName} ({DeviceId}).",
                            session.RemotePeer.DeviceName,
                            session.RemotePeer.DeviceId);
                        return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException exception)
        {
            if (session is not null)
            {
                ClearActiveSession(session);
                _sessionStore.RecordIssue("unexpected_disconnect", exception.Message, session.RemotePeer);
                _sessionStore.MarkSessionClosed(session, intentional: false, exception.Message);
            }

            _logger.LogWarning(exception, "TruckNav control socket closed unexpectedly.");
        }
        finally
        {
            if (!sessionActivated && reservation is not null)
            {
                _sessionStore.ReleaseReservation(reservation);
            }

            if (session is not null)
            {
                ClearActiveSession(session);
                await session.DisposeAsync();
            }
        }
    }

    private async Task SendProtocolErrorAsync(
        IConnectionSession session,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        await session.SendAsync(_protocolHandler.CreateError(code, message), cancellationToken);
    }

    private async Task SendErrorAndCloseAsync(
        WebSocket socket,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        if (socket.State == WebSocketState.Open)
        {
            var payload = Encoding.UTF8.GetBytes(ProtocolSerializer.Serialize(_protocolHandler.CreateError(code, message)));
            await socket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
        }

        if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
        {
            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, message, cancellationToken);
        }
    }

    private async Task BroadcastGameStateIfNeededAsync(ConnectorSessionSnapshot snapshot)
    {
        IConnectionSession? session;

        lock (_sessionGate)
        {
            if (_activeSession is null ||
                GameDetectionSnapshotComparer.AreEquivalent(_lastBroadcastGameDetection, snapshot.GameDetection))
            {
                return;
            }

            session = _activeSession;
            _lastBroadcastGameDetection = snapshot.GameDetection;
        }

        try
        {
            await SendGameStateAsync(session, snapshot.GameDetection, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to send updated game_state to the active TruckNav mobile session.");
        }
    }

    private async Task SendGameStateAsync(
        IConnectionSession session,
        GameDetectionSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        await session.SendAsync(CreateGameStateMessage(snapshot), cancellationToken);
        _logger.LogInformation(
            "game_state sent to {DeviceName} ({DeviceId}).",
            session.RemotePeer.DeviceName,
            session.RemotePeer.DeviceId);
    }

    private static GameStateMessage CreateGameStateMessage(GameDetectionSnapshot snapshot)
    {
        var games = SupportedGameCatalog.All
            .Select(definition => new GameStateEntryMessage(
                definition.ProtocolCode,
                snapshot.IsRunning(definition.GameType)))
            .ToList();

        return new GameStateMessage(games);
    }

    private void SetActiveSession(IConnectionSession session)
    {
        lock (_sessionGate)
        {
            _activeSession = session;
            _lastBroadcastGameDetection = _sessionStore.Snapshot.GameDetection;
        }
    }

    private void ClearActiveSession(IConnectionSession session)
    {
        lock (_sessionGate)
        {
            if (ReferenceEquals(_activeSession, session))
            {
                _activeSession = null;
                _lastBroadcastGameDetection = GameDetectionSnapshot.Empty;
            }
        }
    }

    private static ConnectorPeerInfo ToPeerInfo(HelloMessage helloMessage) =>
        new(
            helloMessage.ProtocolVersion,
            helloMessage.AppVersion,
            helloMessage.DeviceId,
            helloMessage.DeviceName,
            helloMessage.Role,
            helloMessage.Platform,
            helloMessage.Capabilities);

    private static int ResolveListenerPort(WebApplication application)
    {
        var addressFeature = application.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>();

        var boundAddress = addressFeature?.Addresses
            .Select(address => new Uri(address))
            .FirstOrDefault();

        if (boundAddress is null)
        {
            throw new InvalidOperationException("The Connector server did not expose a bound HTTP address.");
        }

        return boundAddress.Port;
    }

    private static IReadOnlyList<string> GetReachableLanAddresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType is not NetworkInterfaceType.Loopback and
                    not NetworkInterfaceType.Tunnel)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Select(unicastAddress => unicastAddress.Address)
            .Where(address =>
                !IPAddress.IsLoopback(address) &&
                !address.IsIPv6LinkLocal &&
                !(address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                  address.GetAddressBytes() is [169, 254, ..]))
            .Select(address => address.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<WebSocketReadResult> ReceiveTextMessageAsync(
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(4 * 1024);

        try
        {
            using var stream = new MemoryStream();

            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return WebSocketReadResult.Closed(result.CloseStatusDescription);
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    return WebSocketReadResult.Error("invalid_payload", "Binary WebSocket messages are not supported.");
                }

                stream.Write(buffer, 0, result.Count);
                if (stream.Length > _protocolHandler.MaxMessageBytes)
                {
                    return WebSocketReadResult.Error(
                        "message_too_large",
                        $"Control messages are limited to {_protocolHandler.MaxMessageBytes} bytes.");
                }

                if (result.EndOfMessage)
                {
                    return WebSocketReadResult.FromPayload(Encoding.UTF8.GetString(stream.ToArray()));
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private sealed record WebSocketReadResult(
        string? Payload,
        string? ErrorCode,
        string? ErrorMessage,
        bool IsClosed,
        string? CloseDescription)
    {
        public static WebSocketReadResult FromPayload(string payload) =>
            new(payload, null, null, false, null);

        public static WebSocketReadResult Error(string code, string message) =>
            new(null, code, message, false, null);

        public static WebSocketReadResult Closed(string? closeDescription) =>
            new(null, null, null, true, closeDescription);
    }
}
