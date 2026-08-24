using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Navigation.Data;

public sealed class NavigationStoragePaths
{
    public NavigationStoragePaths()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TruckNav",
            "navigation"))
    {
    }

    public NavigationStoragePaths(string rootDirectory)
    {
        RootDirectory = rootDirectory;
    }

    public string RootDirectory { get; }

    public string GetGameRoot(GameType game) => Path.Combine(RootDirectory, GetGameCode(game));

    public string GetParserDirectory(GameType game) => Path.Combine(GetGameRoot(game), "parser");

    public string GetGeneratedDirectory(GameType game) => Path.Combine(GetGameRoot(game), "generated");

    public string GetCachePath(GameType game) => Path.Combine(GetGameRoot(game), "trucknav-road-graph-v1.bin");

    public static string GetGameCode(GameType game) => game switch
    {
        GameType.EuroTruckSimulator2 => "ets2",
        GameType.AmericanTruckSimulator => "ats",
        _ => throw new ArgumentOutOfRangeException(nameof(game), game, "A supported game is required.")
    };

    public static string GetMapCode(GameType game) => game switch
    {
        GameType.EuroTruckSimulator2 => "europe",
        GameType.AmericanTruckSimulator => "usa",
        _ => throw new ArgumentOutOfRangeException(nameof(game), game, "A supported game is required.")
    };
}
