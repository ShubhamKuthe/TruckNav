using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Map;

internal enum VisualLineKind : byte
{
    Road = 0,
    PrefabRoad = 1,
    Ferry = 2,
    Train = 3,
    Divider = 4
}

internal enum VisualRoadClass : byte
{
    Minor = 0,
    Local = 1,
    Major = 2,
    Motorway = 3
}

internal sealed record VisualLineFeature(
    int Id,
    VisualLineKind Kind,
    VisualRoadClass RoadClass,
    float WidthMeters,
    IReadOnlyList<WorldPoint> Points)
{
    public (double MinX, double MinZ, double MaxX, double MaxZ) Bounds =>
        VisualMapGeometry.GetBounds(Points);
}

internal sealed record VisualPolygonFeature(
    int Id,
    byte Color,
    bool DrawOver,
    IReadOnlyList<WorldPoint> Points)
{
    public (double MinX, double MinZ, double MaxX, double MaxZ) Bounds =>
        VisualMapGeometry.GetBounds(Points);
}

internal sealed record VisualMapLabel(
    int Id,
    string Text,
    WorldPoint Position,
    byte Priority);

internal sealed record VisualMapPoi(
    int Id,
    string Type,
    WorldPoint Position,
    string? Label);

internal sealed record VisualMapDataset(
    VisualMapManifest Manifest,
    IReadOnlyList<VisualLineFeature> Lines,
    IReadOnlyList<VisualPolygonFeature> Polygons,
    IReadOnlyList<VisualMapLabel> Labels,
    IReadOnlyList<VisualMapPoi> PointsOfInterest,
    int RoadCount,
    int PrefabRoadCount,
    int MapAreaCount,
    int FerryCount);

internal static class VisualMapGeometry
{
    public static (double MinX, double MinZ, double MaxX, double MaxZ) GetBounds(
        IReadOnlyList<WorldPoint> points)
    {
        var minX = double.PositiveInfinity;
        var minZ = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxZ = double.NegativeInfinity;
        foreach (var point in points)
        {
            minX = Math.Min(minX, point.X);
            minZ = Math.Min(minZ, point.Z);
            maxX = Math.Max(maxX, point.X);
            maxZ = Math.Max(maxZ, point.Z);
        }

        return (minX, minZ, maxX, maxZ);
    }

    public static bool Intersects(
        (double MinX, double MinZ, double MaxX, double MaxZ) bounds,
        double minX,
        double minZ,
        double maxX,
        double maxZ) =>
        bounds.MaxX >= minX && bounds.MinX <= maxX &&
        bounds.MaxZ >= minZ && bounds.MinZ <= maxZ;
}
