using System.Text;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Map;

internal static class CompactVisualMapCodec
{
    private const string Magic = "TNAVMP01";
    private const int MaximumFeatureCount = 5_000_000;

    public static void Write(string path, VisualMapDataset dataset)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + ".tmp";
        using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false))
        {
            writer.Write(Encoding.ASCII.GetBytes(Magic));
            WriteHeader(writer, dataset);

            writer.Write(dataset.Lines.Count);
            foreach (var line in dataset.Lines)
            {
                writer.Write(line.Id);
                writer.Write((byte)line.Kind);
                writer.Write((byte)line.RoadClass);
                writer.Write(line.WidthMeters);
                WritePoints(writer, line.Points);
            }

            writer.Write(dataset.Polygons.Count);
            foreach (var polygon in dataset.Polygons)
            {
                writer.Write(polygon.Id);
                writer.Write(polygon.Color);
                writer.Write(polygon.DrawOver);
                WritePoints(writer, polygon.Points);
            }

            writer.Write(dataset.Labels.Count);
            foreach (var label in dataset.Labels)
            {
                writer.Write(label.Id);
                writer.Write(label.Text);
                writer.Write((float)label.Position.X);
                writer.Write((float)label.Position.Z);
                writer.Write(label.Priority);
            }

            writer.Write(dataset.PointsOfInterest.Count);
            foreach (var point in dataset.PointsOfInterest)
            {
                writer.Write(point.Id);
                writer.Write(point.Type);
                writer.Write((float)point.Position.X);
                writer.Write((float)point.Position.Z);
                writer.Write(point.Label is not null);
                if (point.Label is not null)
                {
                    writer.Write(point.Label);
                }
            }
        }

        File.Move(temporaryPath, path, overwrite: true);
    }

    public static VisualMapDataset Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        ValidateMagic(reader);
        var header = ReadHeader(reader);

        var lines = new VisualLineFeature[ReadCount(reader, "line")];
        for (var index = 0; index < lines.Length; index++)
        {
            lines[index] = new VisualLineFeature(
                reader.ReadInt32(),
                (VisualLineKind)reader.ReadByte(),
                (VisualRoadClass)reader.ReadByte(),
                reader.ReadSingle(),
                ReadPoints(reader));
        }

        var polygons = new VisualPolygonFeature[ReadCount(reader, "polygon")];
        for (var index = 0; index < polygons.Length; index++)
        {
            polygons[index] = new VisualPolygonFeature(
                reader.ReadInt32(),
                reader.ReadByte(),
                reader.ReadBoolean(),
                ReadPoints(reader));
        }

        var labels = new VisualMapLabel[ReadCount(reader, "label")];
        for (var index = 0; index < labels.Length; index++)
        {
            labels[index] = new VisualMapLabel(
                reader.ReadInt32(),
                reader.ReadString(),
                new WorldPoint(reader.ReadSingle(), reader.ReadSingle()),
                reader.ReadByte());
        }

        var points = new VisualMapPoi[ReadCount(reader, "point of interest")];
        for (var index = 0; index < points.Length; index++)
        {
            var id = reader.ReadInt32();
            var type = reader.ReadString();
            var position = new WorldPoint(reader.ReadSingle(), reader.ReadSingle());
            var label = reader.ReadBoolean() ? reader.ReadString() : null;
            points[index] = new VisualMapPoi(id, type, position, label);
        }

        return new VisualMapDataset(
            header.Manifest,
            lines,
            polygons,
            labels,
            points,
            header.RoadCount,
            header.PrefabRoadCount,
            header.MapAreaCount,
            header.FerryCount);
    }

    public static VisualMapStatus ReadStatus(string path, string rootDirectory, string? currentVersion, string? currentDlcSignature)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        ValidateMagic(reader);
        var header = ReadHeader(reader);
        var stale = currentVersion is not null &&
            (!string.Equals(header.Manifest.GameVersion, currentVersion, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(header.Manifest.DlcSignature, currentDlcSignature, StringComparison.Ordinal));
        return new VisualMapStatus(
            header.Manifest.Game,
            IsReady: !stale,
            IsStale: stale,
            rootDirectory,
            header.Manifest.GameVersion,
            header.Manifest.MapGeneratorVersion,
            header.RoadCount,
            header.PrefabRoadCount,
            header.MapAreaCount,
            header.FerryCount,
            header.CityCount,
            header.PoiCount,
            stream.Length,
            header.Manifest.GeneratedUtc,
            stale ? "Visual map data is stale. Rebuild it for the installed game version or DLC set." : "Detailed visual map is ready.");
    }

    private static void WriteHeader(BinaryWriter writer, VisualMapDataset dataset)
    {
        var manifest = dataset.Manifest;
        writer.Write((int)manifest.Game);
        writer.Write(manifest.GameVersion);
        writer.Write(manifest.MapGeneratorVersion);
        writer.Write(manifest.GeneratedUtc.UtcTicks);
        writer.Write(manifest.DlcSignature);
        writer.Write(manifest.MinX);
        writer.Write(manifest.MinZ);
        writer.Write(manifest.MaxX);
        writer.Write(manifest.MaxZ);
        writer.Write(dataset.RoadCount);
        writer.Write(dataset.PrefabRoadCount);
        writer.Write(dataset.MapAreaCount);
        writer.Write(dataset.FerryCount);
        writer.Write(dataset.Labels.Count);
        writer.Write(dataset.PointsOfInterest.Count);
    }

    private static VisualMapHeader ReadHeader(BinaryReader reader)
    {
        var game = (GameType)reader.ReadInt32();
        var gameVersion = reader.ReadString();
        var generatorVersion = reader.ReadString();
        var generatedUtc = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
        var dlcSignature = reader.ReadString();
        var minX = reader.ReadDouble();
        var minZ = reader.ReadDouble();
        var maxX = reader.ReadDouble();
        var maxZ = reader.ReadDouble();
        var roadCount = reader.ReadInt32();
        var prefabRoadCount = reader.ReadInt32();
        var mapAreaCount = reader.ReadInt32();
        var ferryCount = reader.ReadInt32();
        var cityCount = reader.ReadInt32();
        var poiCount = reader.ReadInt32();
        var manifest = new VisualMapManifest(
            game,
            gameVersion,
            generatorVersion,
            generatedUtc,
            dlcSignature,
            minX,
            minZ,
            maxX,
            maxZ,
            VisualMapService.Levels);
        return new VisualMapHeader(manifest, roadCount, prefabRoadCount, mapAreaCount, ferryCount, cityCount, poiCount);
    }

    private static void WritePoints(BinaryWriter writer, IReadOnlyList<WorldPoint> points)
    {
        writer.Write(points.Count);
        foreach (var point in points)
        {
            writer.Write((float)point.X);
            writer.Write((float)point.Z);
        }
    }

    private static IReadOnlyList<WorldPoint> ReadPoints(BinaryReader reader)
    {
        var count = ReadCount(reader, "point");
        var points = new WorldPoint[count];
        for (var index = 0; index < count; index++)
        {
            points[index] = new WorldPoint(reader.ReadSingle(), reader.ReadSingle());
        }

        return points;
    }

    private static int ReadCount(BinaryReader reader, string label)
    {
        var count = reader.ReadInt32();
        if (count < 0 || count > MaximumFeatureCount)
        {
            throw new InvalidDataException($"Visual map cache contains an invalid {label} count.");
        }

        return count;
    }

    private static void ValidateMagic(BinaryReader reader)
    {
        var magic = Encoding.ASCII.GetString(reader.ReadBytes(Magic.Length));
        if (!string.Equals(magic, Magic, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unsupported TruckNav visual map cache format.");
        }
    }

    private sealed record VisualMapHeader(
        VisualMapManifest Manifest,
        int RoadCount,
        int PrefabRoadCount,
        int MapAreaCount,
        int FerryCount,
        int CityCount,
        int PoiCount);
}
