using TruckNav.Connector.Core.Enums;

namespace TruckNav.Connector.Core.Models;

public sealed record VisualMapLevel(int Level, double TileWorldSize, string Detail);

public sealed record VisualMapManifest(
    GameType Game,
    string GameVersion,
    string MapGeneratorVersion,
    DateTimeOffset GeneratedUtc,
    string DlcSignature,
    double MinX,
    double MinZ,
    double MaxX,
    double MaxZ,
    IReadOnlyList<VisualMapLevel> Levels);

public sealed record VisualMapStatus(
    GameType Game,
    bool IsReady,
    bool IsStale,
    string RootDirectory,
    string? GameVersion,
    string MapGeneratorVersion,
    int RoadCount,
    int PrefabRoadCount,
    int MapAreaCount,
    int FerryCount,
    int CityCount,
    int PointOfInterestCount,
    long CacheBytes,
    DateTimeOffset? GeneratedUtc,
    string Message);

public sealed record VisualMapBuildProgress(
    GameType Game,
    string Stage,
    double Fraction,
    string Message);

public sealed record VisualMapTile(
    byte[] Content,
    string ContentType,
    string ContentEncoding,
    string ETag);
