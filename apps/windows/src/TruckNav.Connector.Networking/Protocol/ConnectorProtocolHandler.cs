using System.Text.Json;
using TruckNav.Connector.Networking.Identity;
using TruckNav.Connector.Networking.Models;
using TruckNav.Connector.Networking.Protocol.Messages;

namespace TruckNav.Connector.Networking.Protocol;

public sealed class ConnectorProtocolHandler : IProtocolHandler
{
    private readonly ConnectorPeerInfo _localIdentity;

    public ConnectorProtocolHandler(IConnectorIdentityProvider identityProvider)
    {
        _localIdentity = identityProvider.GetIdentity();
    }

    public int MaxMessageBytes => 16 * 1024;

    public ProtocolReadResult ReadClientMessage(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("type", out var typeProperty) ||
                typeProperty.ValueKind != JsonValueKind.String)
            {
                return ProtocolReadResult.FromError(
                    "invalid_payload",
                    "Protocol messages must contain a string 'type' property.");
            }

            return typeProperty.GetString() switch
            {
                ProtocolMessageType.Hello =>
                    ProtocolReadResult.FromMessage(ProtocolSerializer.Deserialize<HelloMessage>(json)),
                ProtocolMessageType.Ping =>
                    ProtocolReadResult.FromMessage(ProtocolSerializer.Deserialize<PingMessage>(json)),
                _ => ProtocolReadResult.FromError(
                    "unsupported_message_type",
                    $"Unsupported protocol message type '{typeProperty.GetString()}'.")
            };
        }
        catch (JsonException)
        {
            return ProtocolReadResult.FromError("malformed_json", "Malformed JSON was received over the control channel.");
        }
        catch (InvalidOperationException exception)
        {
            return ProtocolReadResult.FromError("invalid_payload", exception.Message);
        }
    }

    public ProtocolValidationResult ValidateHello(HelloMessage message)
    {
        if (message.ProtocolVersion <= 0)
        {
            return ProtocolValidationResult.Failure("invalid_payload", "protocolVersion must be a positive integer.");
        }

        if (!ProtocolCompatibility.IsCompatible(message.ProtocolVersion))
        {
            return ProtocolValidationResult.Failure(
                "protocol_mismatch",
                ProtocolCompatibility.GetMismatchMessage(message.ProtocolVersion));
        }

        if (string.IsNullOrWhiteSpace(message.AppVersion) ||
            string.IsNullOrWhiteSpace(message.DeviceId) ||
            string.IsNullOrWhiteSpace(message.DeviceName) ||
            string.IsNullOrWhiteSpace(message.Role) ||
            string.IsNullOrWhiteSpace(message.Platform))
        {
            return ProtocolValidationResult.Failure(
                "missing_required_fields",
                "hello must include appVersion, deviceId, deviceName, role, and platform.");
        }

        if (!Guid.TryParse(message.DeviceId, out _))
        {
            return ProtocolValidationResult.Failure("invalid_payload", "deviceId must be a valid UUID.");
        }

        if (!string.Equals(message.Role, TruckNavNetworkDefaults.MobileRole, StringComparison.Ordinal))
        {
            return ProtocolValidationResult.Failure("invalid_payload", "hello.role must be 'mobile'.");
        }

        if (!string.Equals(message.Platform, TruckNavNetworkDefaults.AndroidPlatform, StringComparison.Ordinal))
        {
            return ProtocolValidationResult.Failure("invalid_payload", "hello.platform must be 'android'.");
        }

        if (message.DeviceName.Length > 64 || message.AppVersion.Length > 32)
        {
            return ProtocolValidationResult.Failure("invalid_payload", "hello contains a field that exceeds its supported length.");
        }

        if (message.Capabilities.Count > 16 || message.Capabilities.Any(capability => capability.Length > 32))
        {
            return ProtocolValidationResult.Failure("invalid_payload", "hello capabilities exceed the supported limits.");
        }

        return ProtocolValidationResult.Success();
    }

    public HelloAckMessage CreateHelloAck() =>
        new(
            _localIdentity.ProtocolVersion,
            _localIdentity.AppVersion,
            _localIdentity.DeviceId,
            _localIdentity.DeviceName,
            _localIdentity.Role,
            _localIdentity.Platform,
            _localIdentity.Capabilities);

    public ErrorMessage CreateError(string code, string message) => new(code, message);

    public PongMessage CreatePong() => new();
}
