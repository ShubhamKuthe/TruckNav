using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Navigation.Data;

namespace TruckNav.Connector.Navigation.Map;

public sealed class VisualMapStoragePaths
{
    public VisualMapStoragePaths()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TruckNav",
            "map"))
    {
    }

    public VisualMapStoragePaths(string rootDirectory)
    {
        RootDirectory = rootDirectory;
    }

    public string RootDirectory { get; }

    public string GetGameRoot(GameType game) =>
        Path.Combine(RootDirectory, NavigationStoragePaths.GetGameCode(game));

    public string GetDatasetPath(GameType game) =>
        Path.Combine(GetGameRoot(game), "trucknav-visual-map-v1.bin");

    public string GetTileCacheRoot(GameType game) =>
        Path.Combine(GetGameRoot(game), "tiles-v1");
}
