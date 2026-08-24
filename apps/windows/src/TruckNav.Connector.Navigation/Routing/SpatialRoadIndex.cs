using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Routing;

public sealed class SpatialRoadIndex : ISpatialRoadIndex
{
    private const double CellSize = 750;

    private readonly RoadGraph _graph;
    private readonly Dictionary<(int X, int Z), List<int>> _cells = [];

    public SpatialRoadIndex(RoadGraph graph)
    {
        _graph = graph;
        foreach (var node in graph.Nodes)
        {
            var key = GetCell(node.Position);
            if (!_cells.TryGetValue(key, out var entries))
            {
                entries = [];
                _cells.Add(key, entries);
            }

            entries.Add(node.Index);
        }
    }

    public RoadNode? FindNearest(WorldPoint point, double maximumDistanceMeters = 500)
    {
        var center = GetCell(point);
        var radius = Math.Max(1, (int)Math.Ceiling(maximumDistanceMeters / CellSize));
        RoadNode? nearest = null;
        var nearestDistanceSquared = maximumDistanceMeters * maximumDistanceMeters;

        for (var x = center.X - radius; x <= center.X + radius; x++)
        {
            for (var z = center.Z - radius; z <= center.Z + radius; z++)
            {
                if (!_cells.TryGetValue((x, z), out var nodeIndexes))
                {
                    continue;
                }

                foreach (var nodeIndex in nodeIndexes)
                {
                    var node = _graph.Nodes[nodeIndex];
                    var dx = node.Position.X - point.X;
                    var dz = node.Position.Z - point.Z;
                    var distanceSquared = (dx * dx) + (dz * dz);
                    if (distanceSquared < nearestDistanceSquared)
                    {
                        nearestDistanceSquared = distanceSquared;
                        nearest = node;
                    }
                }
            }
        }

        return nearest;
    }

    private static (int X, int Z) GetCell(WorldPoint point) =>
        ((int)Math.Floor(point.X / CellSize), (int)Math.Floor(point.Z / CellSize));
}
