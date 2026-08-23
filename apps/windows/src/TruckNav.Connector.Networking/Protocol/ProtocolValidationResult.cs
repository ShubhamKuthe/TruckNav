namespace TruckNav.Connector.Networking.Protocol;

public sealed record ProtocolValidationResult(bool IsValid, ProtocolError? Error)
{
    public static ProtocolValidationResult Success() => new(true, null);

    public static ProtocolValidationResult Failure(string code, string message) =>
        new(false, new ProtocolError(code, message));
}
