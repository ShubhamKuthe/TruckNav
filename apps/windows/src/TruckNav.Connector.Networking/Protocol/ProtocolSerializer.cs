using System.Text.Json;
using TruckNav.Connector.Networking.Protocol.Messages;

namespace TruckNav.Connector.Networking.Protocol;

public static class ProtocolSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string Serialize(MessageBase message) =>
        JsonSerializer.Serialize(message, message.GetType(), JsonOptions);

    public static string Serialize<TMessage>(TMessage message) where TMessage : MessageBase =>
        JsonSerializer.Serialize(message, JsonOptions);

    public static TMessage Deserialize<TMessage>(string json) where TMessage : MessageBase
    {
        var message = JsonSerializer.Deserialize<TMessage>(json, JsonOptions);
        return message ?? throw new InvalidOperationException("Failed to deserialize protocol message.");
    }
}
