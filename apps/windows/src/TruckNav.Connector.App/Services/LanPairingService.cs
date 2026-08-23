using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TruckNav.Connector.Networking.Hosting;
using TruckNav.Connector.Networking.Session;

namespace TruckNav.Connector.App.Services;

public sealed class LanPairingService : IHostedService, ILanPairingService, IDisposable
{
    private const string MulticastGroupAddress = "239.255.18.18";
    private const int MulticastPort = 43112;
    private static readonly TimeSpan PresenceInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DeviceTtl = TimeSpan.FromSeconds(8);

    private readonly ILogger<LanPairingService> _logger;
    private readonly IConnectorServer _connectorServer;
    private readonly IConnectorSessionStore _sessionStore;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly object _gate = new();
    private readonly Dictionary<string, LanMobileDevice> _mobileDevices = new(StringComparer.OrdinalIgnoreCase);
    private readonly IPAddress _multicastGroup = IPAddress.Parse(MulticastGroupAddress);

    private UdpClient? _udpClient;
    private CancellationTokenSource? _cancellationSource;
    private Task? _receiveLoopTask;
    private Timer? _presenceTimer;
    private bool _started;

    public LanPairingService(
        IConnectorServer connectorServer,
        IConnectorSessionStore sessionStore,
        ILogger<LanPairingService> logger)
    {
        _connectorServer = connectorServer;
        _sessionStore = sessionStore;
        _logger = logger;

        _sessionStore.SnapshotChanged += (_, _) => _ = SendPresenceAsync();
    }

    public IReadOnlyList<LanMobileDevice> MobileDevices
    {
        get
        {
            lock (_gate)
            {
                return GetVisibleMobileDevicesUnsafe();
            }
        }
    }

    public event EventHandler<IReadOnlyList<LanMobileDevice>>? MobileDevicesChanged;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_started)
        {
            return;
        }

        var udpClient = CreateUdpClient();
        _udpClient = udpClient;
        _cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(udpClient, _cancellationSource.Token), CancellationToken.None);
        _presenceTimer = new Timer(_ => _ = SendPresenceAsync(), null, TimeSpan.Zero, PresenceInterval);
        _started = true;

        _logger.LogInformation(
            "TruckNav LAN pairing started on multicast group {GroupAddress}:{Port}.",
            MulticastGroupAddress,
            MulticastPort);

        await SendPresenceAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        _presenceTimer?.Dispose();
        _presenceTimer = null;

        _cancellationSource?.Cancel();
        _udpClient?.Dispose();
        _udpClient = null;

        if (_receiveLoopTask is not null)
        {
            try
            {
                await _receiveLoopTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        _cancellationSource?.Dispose();
        _cancellationSource = null;
        _receiveLoopTask = null;

        lock (_gate)
        {
            _mobileDevices.Clear();
        }

        MobileDevicesChanged?.Invoke(this, Array.Empty<LanMobileDevice>());
    }

    public async Task RequestPairAsync(string mobileDeviceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mobileDeviceId))
        {
            return;
        }

        var serviceUri = _connectorServer.ServiceUri;
        if (serviceUri is null)
        {
            return;
        }

        var connector = _sessionStore.Snapshot.LocalConnector;
        await SendMessageAsync(new
        {
            kind = "pair_request",
            targetDeviceId = mobileDeviceId,
            connector = new
            {
                deviceId = connector.DeviceId,
                deviceName = connector.DeviceName,
                platform = connector.Platform,
                appVersion = connector.AppVersion,
                protocolVersion = connector.ProtocolVersion,
                wsPort = serviceUri.Port
            }
        }, cancellationToken);
    }

    public void Dispose()
    {
        _presenceTimer?.Dispose();
        _udpClient?.Dispose();
        _cancellationSource?.Dispose();
        _sendLock.Dispose();
    }

    private UdpClient CreateUdpClient()
    {
        var udpClient = new UdpClient(AddressFamily.InterNetwork)
        {
            MulticastLoopback = false,
            ExclusiveAddressUse = false,
            Ttl = 1
        };

        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, MulticastPort));
        udpClient.JoinMulticastGroup(_multicastGroup);
        return udpClient;
    }

    private async Task ReceiveLoopAsync(UdpClient udpClient, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await udpClient.ReceiveAsync(cancellationToken);
                HandleInboundMessage(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (SocketException exception) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug(exception, "TruckNav LAN pairing receive loop stopped.");
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "TruckNav LAN pairing receive loop ignored an unexpected datagram.");
            }
        }
    }

    private void HandleInboundMessage(byte[] payload, IPEndPoint remoteEndPoint)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("kind", out var kindProperty) ||
                kindProperty.ValueKind != JsonValueKind.String)
            {
                return;
            }

            switch (kindProperty.GetString())
            {
                case "presence":
                    HandlePresenceMessage(document.RootElement);
                    break;
                case "probe":
                    _ = SendPresenceAsync();
                    break;
            }
        }
        catch (JsonException exception)
        {
            _logger.LogDebug(
                exception,
                "Ignored malformed TruckNav LAN pairing message from {RemoteEndPoint}.",
                remoteEndPoint);
        }
    }

    private void HandlePresenceMessage(JsonElement payload)
    {
        if (!TryGetString(payload, "role", out var role) ||
            !string.Equals(role, "mobile", StringComparison.Ordinal))
        {
            return;
        }

        if (!TryGetString(payload, "deviceId", out var deviceId) ||
            !TryGetString(payload, "deviceName", out var deviceName))
        {
            return;
        }

        var localConnectorId = _sessionStore.Snapshot.LocalConnector.DeviceId;
        if (string.Equals(deviceId, localConnectorId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var platform = TryGetString(payload, "platform", out var platformValue)
            ? platformValue
            : "android";

        IReadOnlyList<LanMobileDevice> snapshot;
        lock (_gate)
        {
            _mobileDevices[deviceId] = new LanMobileDevice(
                deviceId,
                deviceName,
                platform,
                DateTimeOffset.UtcNow);
            snapshot = GetVisibleMobileDevicesUnsafe();
        }

        MobileDevicesChanged?.Invoke(this, snapshot);
    }

    private async Task SendPresenceAsync(CancellationToken cancellationToken = default)
    {
        var serviceUri = _connectorServer.ServiceUri;
        if (serviceUri is null)
        {
            return;
        }

        var connector = _sessionStore.Snapshot.LocalConnector;
        await SendMessageAsync(new
        {
            kind = "presence",
            role = "connector",
            deviceId = connector.DeviceId,
            deviceName = connector.DeviceName,
            platform = connector.Platform,
            appVersion = connector.AppVersion,
            protocolVersion = connector.ProtocolVersion,
            wsPort = serviceUri.Port
        }, cancellationToken);

        PublishPrunedSnapshot();
    }

    private async Task SendMessageAsync(object payload, CancellationToken cancellationToken)
    {
        var udpClient = _udpClient;
        if (udpClient is null)
        {
            return;
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await udpClient.SendAsync(
                bytes,
                bytes.Length,
                new IPEndPoint(_multicastGroup, MulticastPort));
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private void PublishPrunedSnapshot()
    {
        IReadOnlyList<LanMobileDevice> snapshot;
        lock (_gate)
        {
            snapshot = GetVisibleMobileDevicesUnsafe();
        }

        MobileDevicesChanged?.Invoke(this, snapshot);
    }

    private IReadOnlyList<LanMobileDevice> GetVisibleMobileDevicesUnsafe()
    {
        var cutoff = DateTimeOffset.UtcNow - DeviceTtl;
        var expiredIds = _mobileDevices
            .Where(entry => entry.Value.LastSeenUtc < cutoff)
            .Select(entry => entry.Key)
            .ToList();

        foreach (var expiredId in expiredIds)
        {
            _mobileDevices.Remove(expiredId);
        }

        return _mobileDevices.Values
            .OrderBy(device => device.DeviceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryGetString(JsonElement payload, string propertyName, out string value)
    {
        value = string.Empty;

        if (!payload.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }
}
