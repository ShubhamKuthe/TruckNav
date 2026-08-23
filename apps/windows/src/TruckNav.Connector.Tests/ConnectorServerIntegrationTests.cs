using System.Net;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Networking;
using TruckNav.Connector.Networking.Discovery;
using TruckNav.Connector.Networking.Hosting;
using TruckNav.Connector.Networking.Identity;
using TruckNav.Connector.Networking.Models;
using TruckNav.Connector.Networking.Protocol;
using TruckNav.Connector.Networking.Protocol.Messages;
using TruckNav.Connector.Networking.Session;
using TruckNav.Connector.Networking.Storage;

namespace TruckNav.Connector.Tests;

public sealed class ConnectorServerIntegrationTests
{
    [Fact]
    public async Task HealthEndpoint_ShouldReturnOk()
    {
        await using var context = await TestServerContext.StartAsync();
        using var httpClient = new HttpClient
        {
            BaseAddress = context.Server.ServiceUri
        };

        var payload = await httpClient.GetFromJsonAsync<JsonElement>("/health");

        Assert.Equal("ok", payload.GetProperty("status").GetString());
    }

    [Fact]
    public async Task InfoEndpoint_ShouldExposeConnectorIdentity()
    {
        await using var context = await TestServerContext.StartAsync();
        using var httpClient = new HttpClient
        {
            BaseAddress = context.Server.ServiceUri
        };

        var payload = await httpClient.GetFromJsonAsync<ConnectorPeerInfo>("/info");

        Assert.NotNull(payload);
        Assert.Equal("ROCK", payload!.DeviceName);
        Assert.Equal(1, payload.ProtocolVersion);
        Assert.Equal(TruckNavNetworkDefaults.ConnectorRole, payload.Role);
    }

    [Fact]
    public async Task WebSocketHandshake_ShouldReturnHelloAckAndPong()
    {
        await using var context = await TestServerContext.StartAsync();
        using var socket = await context.ConnectWebSocketAsync();

        await SendTextAsync(socket, BuildHelloJson());
        var helloAckPayload = await ReceiveTextAsync(socket);

        Assert.True(helloAckPayload.Contains("hello_ack", StringComparison.Ordinal), helloAckPayload);
        Assert.True(helloAckPayload.Contains("ROCK", StringComparison.Ordinal), helloAckPayload);

        await SendTextAsync(socket, ProtocolSerializer.Serialize(new PingMessage()));
        var pongPayload = await ReceiveTextAsync(socket);

        Assert.True(pongPayload.Contains("pong", StringComparison.Ordinal), pongPayload);
        Assert.Equal(DeviceConnectionState.Connected, context.SessionStore.Snapshot.DeviceState);
    }

    [Fact]
    public async Task WebSocket_ShouldRejectMalformedJson()
    {
        await using var context = await TestServerContext.StartAsync();
        using var socket = await context.ConnectWebSocketAsync();

        await SendTextAsync(socket, "{");
        var error = ProtocolSerializer.Deserialize<ErrorMessage>(await ReceiveTextAsync(socket));

        Assert.Equal("malformed_json", error.Code);
    }

    [Fact]
    public async Task WebSocket_ShouldRejectMissingRequiredFields()
    {
        await using var context = await TestServerContext.StartAsync();
        using var socket = await context.ConnectWebSocketAsync();

        await SendTextAsync(
            socket,
            """
            {
              "type": "hello",
              "protocolVersion": 1,
              "appVersion": "0.1.0",
              "deviceId": "",
              "deviceName": "",
              "role": "mobile",
              "platform": "android",
              "capabilities": []
            }
            """);

        var error = ProtocolSerializer.Deserialize<ErrorMessage>(await ReceiveTextAsync(socket));

        Assert.Equal("missing_required_fields", error.Code);
    }

    [Fact]
    public async Task WebSocket_ShouldRejectProtocolMismatch()
    {
        await using var context = await TestServerContext.StartAsync();
        using var socket = await context.ConnectWebSocketAsync();

        await SendTextAsync(socket, BuildHelloJson(protocolVersion: 99));
        var error = ProtocolSerializer.Deserialize<ErrorMessage>(await ReceiveTextAsync(socket));

        Assert.Equal("protocol_mismatch", error.Code);
    }

    [Fact]
    public async Task Endpoints_ShouldRespondViaLanIPv4_WhenOneIsAvailable()
    {
        var lanAddress = GetReachableLanIPv4Address();
        if (lanAddress is null)
        {
            return;
        }

        await using var context = await TestServerContext.StartAsync();
        using var httpClient = new HttpClient
        {
            BaseAddress = new UriBuilder(Uri.UriSchemeHttp, lanAddress, context.Server.ServiceUri!.Port).Uri
        };

        var health = await httpClient.GetFromJsonAsync<JsonElement>("/health");
        Assert.Equal("ok", health.GetProperty("status").GetString());

        var info = await httpClient.GetFromJsonAsync<ConnectorPeerInfo>("/info");
        Assert.NotNull(info);
        Assert.Equal("ROCK", info!.DeviceName);

        using var socket = await context.ConnectWebSocketAsync(lanAddress);
        await SendTextAsync(socket, BuildHelloJson(deviceName: "LAN-HEN"));
        var helloAckPayload = await ReceiveTextAsync(socket);

        Assert.True(helloAckPayload.Contains("hello_ack", StringComparison.Ordinal), helloAckPayload);
    }

    [Fact]
    public async Task SecondClient_ShouldReceiveBusyWhileFirstSessionIsActive()
    {
        await using var context = await TestServerContext.StartAsync();
        using var firstSocket = await context.ConnectWebSocketAsync();
        await SendTextAsync(firstSocket, BuildHelloJson(deviceName: "HEN"));
        _ = await ReceiveTextAsync(firstSocket);

        using var secondSocket = await context.ConnectWebSocketAsync();
        await SendTextAsync(secondSocket, BuildHelloJson(deviceId: Guid.NewGuid().ToString("D"), deviceName: "WREN"));
        var error = ProtocolSerializer.Deserialize<ErrorMessage>(await ReceiveTextAsync(secondSocket));

        Assert.Equal("busy", error.Code);
    }

    private static string BuildHelloJson(
        int protocolVersion = 1,
        string? deviceId = null,
        string deviceName = "HEN") =>
        JsonSerializer.Serialize(new
        {
            type = "hello",
            protocolVersion,
            appVersion = "0.1.0",
            deviceId = deviceId ?? Guid.NewGuid().ToString("D"),
            deviceName,
            role = "mobile",
            platform = "android",
            capabilities = Array.Empty<string>()
        });

    private static async Task SendTextAsync(ClientWebSocket socket, string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket socket)
    {
        var buffer = new byte[8 * 1024];
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            stream.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }

    private static string? GetReachableLanIPv4Address()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Select(unicastAddress => unicastAddress.Address)
            .Where(address =>
                address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(address) &&
                address.GetAddressBytes() is not [169, 254, ..])
            .Select(address => address.ToString())
            .FirstOrDefault();
    }

    private sealed class TestServerContext : IAsyncDisposable
    {
        private readonly string _storageRoot;
        private readonly NoOpDiscoveryAdvertiser _advertiser;

        private TestServerContext(
            ConnectorServerHostedService server,
            ConnectorSessionStore sessionStore,
            string storageRoot,
            NoOpDiscoveryAdvertiser advertiser)
        {
            Server = server;
            SessionStore = sessionStore;
            _storageRoot = storageRoot;
            _advertiser = advertiser;
        }

        public ConnectorServerHostedService Server { get; }

        public ConnectorSessionStore SessionStore { get; }

        public static async Task<TestServerContext> StartAsync()
        {
            var storageRoot = Path.Combine(Path.GetTempPath(), "TruckNavTests", Guid.NewGuid().ToString("N"));
            var identityProvider = new FileConnectorIdentityProvider(
                Microsoft.Extensions.Options.Options.Create(new ConnectorStorageOptions
                {
                    RootDirectory = storageRoot
                }),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<FileConnectorIdentityProvider>.Instance);
            var sessionStore = new ConnectorSessionStore(identityProvider);
            var protocolHandler = new ConnectorProtocolHandler(identityProvider);
            var advertiser = new NoOpDiscoveryAdvertiser();
            var server = new ConnectorServerHostedService(
                identityProvider,
                protocolHandler,
                sessionStore,
                advertiser,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ConnectorServerHostedService>.Instance);

            await server.StartAsync(CancellationToken.None);
            return new TestServerContext(server, sessionStore, storageRoot, advertiser);
        }

        public async Task<ClientWebSocket> ConnectWebSocketAsync(string? host = null)
        {
            var socket = new ClientWebSocket();
            var wsUri = new UriBuilder(Server.ServiceUri!)
            {
                Scheme = "ws",
                Host = host ?? Server.ServiceUri!.Host,
                Path = "/ws"
            }.Uri;

            await socket.ConnectAsync(wsUri, CancellationToken.None);
            return socket;
        }

        public async ValueTask DisposeAsync()
        {
            await Server.StopAsync(CancellationToken.None);
            await _advertiser.DisposeAsync();

            if (Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, recursive: true);
            }
        }
    }

    private sealed class NoOpDiscoveryAdvertiser : IDiscoveryAdvertiser
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task StartAsync(ConnectorPeerInfo identity, int port, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
