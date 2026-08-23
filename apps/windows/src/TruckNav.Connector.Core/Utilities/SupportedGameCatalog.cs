using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Core.Utilities;

public static class SupportedGameCatalog
{
    private static readonly IReadOnlyList<SupportedGameDefinition> Definitions =
    [
        new(
            GameType.EuroTruckSimulator2,
            "Euro Truck Simulator 2",
            "eurotrucks2.exe",
            "ets2"),
        new(
            GameType.AmericanTruckSimulator,
            "American Truck Simulator",
            "amtrucks.exe",
            "ats")
    ];

    public static IReadOnlyList<SupportedGameDefinition> All => Definitions;

    public static SupportedGameDefinition Get(GameType gameType) =>
        Definitions.First(definition => definition.GameType == gameType);

    public static string GetDisplayName(GameType gameType) =>
        gameType == GameType.None
            ? "No supported game detected"
            : Get(gameType).DisplayName;

    public static bool TryMatchProcessName(
        string? processName,
        out SupportedGameDefinition definition)
    {
        var normalizedProcessName = NormalizeProcessName(processName);
        if (string.IsNullOrEmpty(normalizedProcessName))
        {
            definition = default!;
            return false;
        }

        var matchedDefinition = Definitions.FirstOrDefault(candidate =>
            NormalizeProcessName(candidate.ProcessName) == normalizedProcessName)!;

        if (matchedDefinition is null)
        {
            definition = default!;
            return false;
        }

        definition = matchedDefinition;
        return true;
    }

    public static string NormalizeProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return string.Empty;
        }

        return Path.GetFileNameWithoutExtension(processName.Trim()).ToLowerInvariant();
    }
}
