using System.Text.Json;

namespace TruckNav.Connector.Navigation.Map;

internal sealed class ParserNode
{
    public string Uid { get; init; } = string.Empty;
    public double X { get; init; }
    public double Y { get; init; }
    public double Rotation { get; init; }
}

internal sealed class ParserRoad
{
    public string Uid { get; init; } = string.Empty;
    public string RoadLookToken { get; init; } = string.Empty;
    public string StartNodeUid { get; init; } = string.Empty;
    public string EndNodeUid { get; init; } = string.Empty;
    public bool Hidden { get; init; }
    public bool MaybeDivided { get; init; }
}

internal sealed class ParserRoadLook
{
    public string Token { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> LanesLeft { get; init; } = [];
    public IReadOnlyList<string> LanesRight { get; init; } = [];
    public double? Offset { get; init; }
    public double? LaneOffset { get; init; }
    public double ShoulderSpaceLeft { get; init; }
    public double ShoulderSpaceRight { get; init; }
}

internal sealed class ParserPrefab
{
    public string Uid { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public IReadOnlyList<string> NodeUids { get; init; } = [];
    public int OriginNodeIndex { get; init; }
    public bool Hidden { get; init; }
}

internal sealed class ParserPrefabDescription
{
    public string Token { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public IReadOnlyList<ParserPrefabNode> Nodes { get; init; } = [];
    public IReadOnlyList<ParserMapPoint> MapPoints { get; init; } = [];
}

internal sealed class ParserPrefabNode
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Rotation { get; init; }
}

internal sealed class ParserMapPoint
{
    public double X { get; init; }
    public double Y { get; init; }
    public string Type { get; init; } = string.Empty;
    public IReadOnlyList<int> Neighbors { get; init; } = [];
    public byte Color { get; init; }
    public bool RoadOver { get; init; }
    public bool Hidden { get; init; }
    public double Offset { get; init; }
    public JsonElement LanesLeft { get; init; }
    public JsonElement LanesRight { get; init; }
}

internal sealed class ParserMapArea
{
    public string Uid { get; init; } = string.Empty;
    public IReadOnlyList<string> NodeUids { get; init; } = [];
    public byte Color { get; init; }
    public bool DrawOver { get; init; }
}

internal sealed class ParserDivider
{
    public string Uid { get; init; } = string.Empty;
    public string StartNodeUid { get; init; } = string.Empty;
    public string EndNodeUid { get; init; } = string.Empty;
}

internal sealed class ParserCity
{
    public string Token { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Population { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
}

internal sealed class ParserPoi
{
    public string Type { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public string? Label { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
}

internal sealed class ParserFerry
{
    public string Token { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool Train { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public IReadOnlyList<ParserFerryConnection> Connections { get; init; } = [];
}

internal sealed class ParserFerryConnection
{
    public string Token { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public double X { get; init; }
    public double Y { get; init; }
    public IReadOnlyList<ParserPosition> IntermediatePoints { get; init; } = [];
}

internal sealed class ParserPosition
{
    public double X { get; init; }
    public double Y { get; init; }
}
