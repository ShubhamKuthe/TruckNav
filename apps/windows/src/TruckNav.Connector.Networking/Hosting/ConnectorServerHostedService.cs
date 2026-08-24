using System.Buffers;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Security.Cryptography;
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
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
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
    private readonly INavigationStateStore _navigationStateStore;
    private readonly IVisualMapService _visualMapService;
    private readonly IProtocolHandler _protocolHandler;
    private readonly IConnectorSessionStore _sessionStore;
    private readonly object _sessionGate = new();

    private WebApplication? _serverApplication;
    private IConnectionSession? _activeSession;
    private string? _activeMapAccessToken;
    private GameDetectionSnapshot _lastBroadcastGameDetection = GameDetectionSnapshot.Empty;
    private NavigationSnapshot? _pendingNavigationSnapshot;
    private bool _navigationBroadcastRunning;
    private long _lastBroadcastRouteRevision = -1;

    public ConnectorServerHostedService(
        IConnectorIdentityProvider identityProvider,
        IProtocolHandler protocolHandler,
        IConnectorSessionStore sessionStore,
        INavigationStateStore navigationStateStore,
        IVisualMapService visualMapService,
        IDiscoveryAdvertiser discoveryAdvertiser,
        ILogger<ConnectorServerHostedService> logger)
    {
        _identityProvider = identityProvider;
        _protocolHandler = protocolHandler;
        _sessionStore = sessionStore;
        _navigationStateStore = navigationStateStore;
        _visualMapService = visualMapService;
        _discoveryAdvertiser = discoveryAdvertiser;
        _logger = logger;

        _sessionStore.SnapshotChanged += (_, snapshot) => _ = BroadcastGameStateIfNeededAsync(snapshot);
        _navigationStateStore.SnapshotChanged += (_, snapshot) => QueueNavigationBroadcast(snapshot);
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
        application.MapGet("/map/{game}/manifest", HandleMapManifestAsync);
        application.MapGet("/map/{game}/{level:int}/{tileX:int}/{tileY:int}", HandleMapTileAsync);
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
            var mapAccessToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await session.SendAsync(_protocolHandler.CreateHelloAck(mapAccessToken), cancellationToken);
            if (!_sessionStore.TryActivateSession(reservation, session))
            {
                throw new InvalidOperationException("The pending TruckNav handshake reservation was lost before activation.");
            }

            sessionActivated = true;
            SetActiveSession(session, mapAccessToken);
            await SendGameStateAsync(session, _sessionStore.Snapshot.GameDetection, cancellationToken);
            await SendNavigationStateAsync(
                session,
                _navigationStateStore.Snapshot,
                forceRoute: true,
                cancellationToken);
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
                        const string unsupportedMessage = "Only ping and game_state_request messages are supported from mobile clients after handshake.";
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

    private void QueueNavigationBroadcast(NavigationSnapshot snapshot)
    {
        lock (_sessionGate)
        {
            if (_activeSession is null)
            {
                return;
            }

            _pendingNavigationSnapshot = snapshot;
            if (_navigationBroadcastRunning)
            {
                return;
            }

            _navigationBroadcastRunning = true;
        }

        _ = DrainNavigationBroadcastsAsync();
    }

    private async Task DrainNavigationBroadcastsAsync()
    {
        while (true)
        {
            IConnectionSession? session;
            NavigationSnapshot? snapshot;
            lock (_sessionGate)
            {
                session = _activeSession;
                snapshot = _pendingNavigationSnapshot;
                _pendingNavigationSnapshot = null;
                if (session is null || snapshot is null)
                {
                    _navigationBroadcastRunning = false;
                    return;
                }
            }

            try
            {
                await SendNavigationStateAsync(session, snapshot, forceRoute: false, CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to send native navigation state to the active mobile session.");
            }
        }
    }

    private async Task SendNavigationStateAsync(
        IConnectionSession session,
        NavigationSnapshot snapshot,
        bool forceRoute,
        CancellationToken cancellationToken)
    {
        var telemetry = snapshot.Telemetry;
        await session.SendAsync(
            new TelemetryStateMessage(
                ToProtocolGame(telemetry.Game),
                telemetry.Connected,
                telemetry.Paused,
                telemetry.Position.X,
                telemetry.Position.Z,
                telemetry.Elevation,
                telemetry.HeadingDegrees,
                telemetry.SpeedKph,
                telemetry.SpeedLimitKph,
                telemetry.OnJob,
                telemetry.DestinationCityName ?? telemetry.DestinationCityId,
                telemetry.DestinationCompanyName ?? telemetry.DestinationCompanyId,
                telemetry.GameNavigationDistanceMeters,
                telemetry.GameNavigationTimeSeconds,
                snapshot.Status.ToString(),
                snapshot.StatusMessage,
                telemetry.TimestampUtc),
            cancellationToken);

        if (forceRoute || snapshot.RouteRevision != _lastBroadcastRouteRevision)
        {
            await session.SendAsync(CreateRouteStateMessage(snapshot), cancellationToken);
            lock (_sessionGate)
            {
                if (ReferenceEquals(_activeSession, session))
                {
                    _lastBroadcastRouteRevision = snapshot.RouteRevision;
                }
            }
        }

        if (snapshot.Progress is { } progress)
        {
            await session.SendAsync(
                new RouteProgressMessage(
                    progress.RouteId,
                    progress.SegmentIndex,
                    progress.ProjectedPosition.X,
                    progress.ProjectedPosition.Z,
                    progress.DistanceFromRouteMeters,
                    progress.RemainingDistanceMeters,
                    progress.RemainingTimeSeconds,
                    progress.NextManeuver.ToString(),
                    progress.NextInstruction,
                    progress.DistanceToNextManeuverMeters,
                    progress.IsOffRoute,
                    progress.TeleportDetected,
                    progress.TimestampUtc),
                cancellationToken);
        }
    }

    private static RouteStateMessage CreateRouteStateMessage(NavigationSnapshot snapshot)
    {
        if (snapshot.Route is not { } route)
        {
            return new RouteStateMessage(
                snapshot.RouteRevision,
                Available: false,
                RouteId: null,
                Game: null,
                Destination: null,
                Points: [],
                Steps: [],
                ContextRoads: [],
                MapPoints: [],
                MapLabels: [],
                TotalDistanceMeters: 0,
                EstimatedTimeSeconds: 0,
                CalculatedUtc: null,
                IsReroute: false);
        }

        return new RouteStateMessage(
            snapshot.RouteRevision,
            Available: true,
            route.RouteId,
            ToProtocolGame(route.Game),
            route.Destination.DisplayName,
            route.Points.Select(point => new RoutePointMessage(point.X, point.Z)).ToArray(),
            route.Steps.Select(step => new RouteStepMessage(
                step.Maneuver.ToString(),
                step.Instruction,
                step.PointIndex,
                step.DistanceFromStartMeters)).ToArray(),
            route.ContextRoads.Select(road => new RouteContextRoadMessage(
                road.From.X,
                road.From.Z,
                road.To.X,
                road.To.Z,
                road.IsMajor,
                road.IsFerry)).ToArray(),
            route.MapPoints.Select(point => new RouteMapPointMessage(
                point.Type.ToString(),
                point.Position.X,
                point.Position.Z,
                point.Label,
                point.IsDestination)).ToArray(),
            route.MapLabels.Select(label => new RouteMapLabelMessage(
                label.Text,
                label.Position.X,
                label.Position.Z,
                label.IsDestination)).ToArray(),
            route.TotalDistanceMeters,
            route.EstimatedTimeSeconds,
            route.CalculatedUtc,
            route.IsReroute);
    }

    private static string ToProtocolGame(GameType game) => game switch
    {
        GameType.EuroTruckSimulator2 => "ets2",
        GameType.AmericanTruckSimulator => "ats",
        _ => "none"
    };

    private async Task<IResult> HandleMapManifestAsync(HttpContext context, string game)
    {
        if (!IsAuthorizedMapRequest(context))
        {
            return Results.Unauthorized();
        }
        if (!TryParseGame(game, out var gameType))
        {
            return Results.NotFound();
        }
        var manifest = await _visualMapService.GetManifestAsync(gameType, context.RequestAborted);
        return manifest is null
            ? Results.NotFound(new { code = "map_unavailable", message = "Map data unavailable" })
            : Results.Json(manifest);
    }

    private async Task<IResult> HandleMapTileAsync(
        HttpContext context,
        string game,
        int level,
        int tileX,
        int tileY)
    {
        if (!IsAuthorizedMapRequest(context))
        {
            return Results.Unauthorized();
        }
        if (!TryParseGame(game, out var gameType))
        {
            return Results.NotFound();
        }
        var tile = await _visualMapService.GetTileAsync(
            gameType, level, tileX, tileY, context.RequestAborted);
        if (tile is null)
        {
            return Results.NotFound(new { code = "tile_unavailable" });
        }
        context.Response.Headers.ContentEncoding = tile.ContentEncoding;
        context.Response.Headers.ETag = tile.ETag;
        context.Response.Headers.CacheControl = "private, max-age=86400";
        return Results.Bytes(tile.Content, tile.ContentType);
    }

    private bool IsAuthorizedMapRequest(HttpContext context)
    {
        var presented = context.Request.Headers["X-TruckNav-Map-Token"].ToString();
        string? expected;
        lock (_sessionGate)
        {
            expected = _activeSession is null ? null : _activeMapAccessToken;
        }
        if (expected is null || presented.Length != expected.Length)
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented),
            Encoding.UTF8.GetBytes(expected));
    }

    private static bool TryParseGame(string value, out GameType game)
    {
        game = value.ToLowerInvariant() switch
        {
            "ets2" => GameType.EuroTruckSimulator2,
            "ats" => GameType.AmericanTruckSimulator,
            _ => GameType.None
        };
        return game != GameType.None;
    }

    private void SetActiveSession(IConnectionSession session, string mapAccessToken)
    {
        lock (_sessionGate)
        {
            _activeSession = session;
            _activeMapAccessToken = mapAccessToken;
            _lastBroadcastGameDetection = _sessionStore.Snapshot.GameDetection;
            _pendingNavigationSnapshot = null;
            _lastBroadcastRouteRevision = -1;
        }
    }

    private void ClearActiveSession(IConnectionSession session)
    {
        lock (_sessionGate)
        {
            if (ReferenceEquals(_activeSession, session))
            {
                _activeSession = null;
                _activeMapAccessToken = null;
                _lastBroadcastGameDetection = GameDetectionSnapshot.Empty;
                _pendingNavigationSnapshot = null;
                _lastBroadcastRouteRevision = -1;
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
