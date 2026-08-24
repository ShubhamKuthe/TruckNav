using System.Text;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Data;

public static class CompactRoadGraphCodec
{
    private const string Magic = "TNAVGR01";
    private const int MaximumReasonableNodes = 2_000_000;

    public static void Write(string path, RoadGraph graph)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + ".tmp";

        using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false))
        {
            writer.Write(Encoding.ASCII.GetBytes(Magic));
            WriteMetadata(writer, graph.Metadata);
            writer.Write(graph.Nodes.Count);
            foreach (var node in graph.Nodes)
            {
                writer.Write(node.SourceId);
                writer.Write(node.Position.X);
                writer.Write(node.Position.Z);
                WriteEdges(writer, node.ForwardEdges);
                WriteEdges(writer, node.BackwardEdges);
            }

            writer.Write(graph.Destinations.Count);
            foreach (var destination in graph.Destinations)
            {
                writer.Write(destination.CityToken);
                writer.Write(destination.CompanyToken);
                WriteNullableString(writer, destination.CityName);
                WriteNullableString(writer, destination.CompanyName);
                writer.Write(destination.NodeIndex);
                writer.Write(destination.Position.X);
                writer.Write(destination.Position.Z);
            }

            writer.Write(graph.PointsOfInterest.Count);
            foreach (var point in graph.PointsOfInterest)
            {
                writer.Write((byte)point.Type);
                writer.Write(point.Position.X);
                writer.Write(point.Position.Z);
            }
        }

        File.Move(temporaryPath, path, overwrite: true);
    }

    public static RoadGraph Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        ValidateMagic(reader);
        var metadata = ReadMetadata(reader);
        var nodeCount = ReadCount(reader, MaximumReasonableNodes, "node");
        var nodes = new RoadNode[nodeCount];

        for (var index = 0; index < nodeCount; index++)
        {
            nodes[index] = new RoadNode(
                index,
                reader.ReadString(),
                new WorldPoint(reader.ReadDouble(), reader.ReadDouble()),
                ReadEdges(reader, nodeCount),
                ReadEdges(reader, nodeCount));
        }

        var destinationCount = ReadCount(reader, 100_000, "destination");
        var destinations = new RoadDestination[destinationCount];
        for (var index = 0; index < destinationCount; index++)
        {
            var cityToken = reader.ReadString();
            var companyToken = reader.ReadString();
            var cityName = ReadNullableString(reader);
            var companyName = ReadNullableString(reader);
            var nodeIndex = reader.ReadInt32();
            if ((uint)nodeIndex >= nodeCount)
            {
                throw new InvalidDataException("Navigation cache contains an invalid destination node index.");
            }

            destinations[index] = new RoadDestination(
                cityToken,
                companyToken,
                cityName,
                companyName,
                nodeIndex,
                new WorldPoint(reader.ReadDouble(), reader.ReadDouble()));
        }

        var pointsOfInterest = Array.Empty<RoadPointOfInterest>();
        if (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var pointCount = ReadCount(reader, 200_000, "point of interest");
            pointsOfInterest = new RoadPointOfInterest[pointCount];
            for (var index = 0; index < pointCount; index++)
            {
                var type = (NavigationPoiType)reader.ReadByte();
                if (!Enum.IsDefined(type))
                {
                    throw new InvalidDataException("Navigation cache contains an invalid point-of-interest type.");
                }

                pointsOfInterest[index] = new RoadPointOfInterest(
                    type,
                    new WorldPoint(reader.ReadDouble(), reader.ReadDouble()));
            }
        }

        return new RoadGraph(metadata, nodes, destinations, pointsOfInterest);
    }

    public static NavigationDataStatus ReadStatus(string path, string rootDirectory)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
        ValidateMagic(reader);
        var metadata = ReadMetadata(reader);
        var nodeCount = ReadCount(reader, MaximumReasonableNodes, "node");
        var edgeCount = 0;

        for (var index = 0; index < nodeCount; index++)
        {
            _ = reader.ReadString();
            _ = reader.ReadDouble();
            _ = reader.ReadDouble();
            edgeCount += SkipEdges(reader, nodeCount);
            edgeCount += SkipEdges(reader, nodeCount);
        }

        return new NavigationDataStatus(
            metadata.Game,
            IsReady: true,
            rootDirectory,
            metadata.GameVersion,
            nodeCount,
            edgeCount,
            metadata.GeneratedUtc,
            "Native navigation data is ready.");
    }

    private static void WriteMetadata(BinaryWriter writer, RoadGraphMetadata metadata)
    {
        writer.Write((int)metadata.Game);
        writer.Write(metadata.GameVersion);
        writer.Write(metadata.Generator);
        writer.Write(metadata.GeneratorCommit);
        writer.Write(metadata.FormatVersion);
        writer.Write(metadata.GeneratedUtc.UtcTicks);
        writer.Write(metadata.SourceGamePath);
        writer.Write(metadata.DlcSignature);
    }

    private static RoadGraphMetadata ReadMetadata(BinaryReader reader) => new(
        (GameType)reader.ReadInt32(),
        reader.ReadString(),
        reader.ReadString(),
        reader.ReadString(),
        reader.ReadInt32(),
        new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
        reader.ReadString(),
        reader.ReadString());

    private static void WriteEdges(BinaryWriter writer, IReadOnlyList<RoadEdge> edges)
    {
        writer.Write(edges.Count);
        foreach (var edge in edges)
        {
            writer.Write(edge.TargetNodeIndex);
            writer.Write(edge.DistanceMeters);
            writer.Write(edge.DurationSeconds);
            writer.Write((byte)edge.ArrivalDirection);
            var flags = (byte)((edge.IsOneLaneRoad ? 1 : 0) | (edge.IsFerry ? 2 : 0));
            writer.Write(flags);
            writer.Write(edge.DlcGuard);
        }
    }

    private static IReadOnlyList<RoadEdge> ReadEdges(BinaryReader reader, int nodeCount)
    {
        var count = ReadCount(reader, 1_000, "edge");
        var edges = new RoadEdge[count];
        for (var index = 0; index < count; index++)
        {
            var targetNodeIndex = reader.ReadInt32();
            if ((uint)targetNodeIndex >= nodeCount)
            {
                throw new InvalidDataException("Navigation cache contains an invalid edge target.");
            }

            var distance = reader.ReadSingle();
            var duration = reader.ReadSingle();
            var direction = (RoadTravelDirection)reader.ReadByte();
            var flags = reader.ReadByte();
            var dlcGuard = reader.ReadInt32();
            edges[index] = new RoadEdge(
                targetNodeIndex,
                distance,
                duration,
                direction,
                IsOneLaneRoad: (flags & 1) != 0,
                IsFerry: (flags & 2) != 0,
                dlcGuard);
        }

        return edges;
    }

    private static int SkipEdges(BinaryReader reader, int nodeCount)
    {
        var count = ReadCount(reader, 1_000, "edge");
        const int edgeBytes = sizeof(int) + sizeof(float) + sizeof(float) + sizeof(byte) + sizeof(byte) + sizeof(int);
        reader.BaseStream.Seek((long)count * edgeBytes, SeekOrigin.Current);
        return count;
    }

    private static int ReadCount(BinaryReader reader, int maximum, string label)
    {
        var count = reader.ReadInt32();
        if (count < 0 || count > maximum)
        {
            throw new InvalidDataException($"Navigation cache contains an invalid {label} count.");
        }

        return count;
    }

    private static void ValidateMagic(BinaryReader reader)
    {
        var magic = Encoding.ASCII.GetString(reader.ReadBytes(Magic.Length));
        if (!string.Equals(magic, Magic, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unsupported TruckNav navigation cache format.");
        }
    }

    private static void WriteNullableString(BinaryWriter writer, string? value)
    {
        writer.Write(value is not null);
        if (value is not null)
        {
            writer.Write(value);
        }
    }

    private static string? ReadNullableString(BinaryReader reader) => reader.ReadBoolean() ? reader.ReadString() : null;
}
