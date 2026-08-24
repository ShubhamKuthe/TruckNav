using System.Runtime.CompilerServices;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Routing;

public sealed class AStarRoutePlanner : IRoutePlanner
{
    private const double MaximumSnapDistance = 2_000;
    private const double MaximumHeuristicSpeedMetersPerSecond = 60;
    private readonly ConditionalWeakTable<RoadGraph, SpatialRoadIndex> _spatialIndexes = new();

    public RoutePlan? Plan(
        RoadGraph graph,
        WorldPoint start,
        double headingDegrees,
        ResolvedDestination destination,
        bool isReroute = false,
        CancellationToken cancellationToken = default)
    {
        if ((uint)destination.NodeIndex >= graph.Nodes.Count)
        {
            return null;
        }

        var startNode = _spatialIndexes
            .GetValue(graph, static value => new SpatialRoadIndex(value))
            .FindNearest(start, MaximumSnapDistance);
        if (startNode is null)
        {
            return null;
        }

        var stateCount = graph.Nodes.Count * 2;
        var costs = new double[stateCount];
        var previous = new int[stateCount];
        var previousDistance = new float[stateCount];
        var previousDuration = new float[stateCount];
        var previousFerry = new bool[stateCount];
        Array.Fill(costs, double.PositiveInfinity);
        Array.Fill(previous, -1);

        var open = new PriorityQueue<int, double>();
        Seed(RoadTravelDirection.Forward);
        Seed(RoadTravelDirection.Backward);
        var destinationState = -1;
        var iterations = 0;

        while (open.TryDequeue(out var currentState, out var queuedScore))
        {
            if ((iterations++ & 1023) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var currentNodeIndex = currentState / 2;
            var heuristic = Heuristic(graph.Nodes[currentNodeIndex].Position, graph.Nodes[destination.NodeIndex].Position);
            if (queuedScore > costs[currentState] + heuristic + 0.001)
            {
                continue;
            }

            if (currentNodeIndex == destination.NodeIndex)
            {
                destinationState = currentState;
                break;
            }

            var currentNode = graph.Nodes[currentNodeIndex];
            var direction = (RoadTravelDirection)(currentState & 1);
            var edges = direction == RoadTravelDirection.Forward
                ? currentNode.ForwardEdges
                : currentNode.BackwardEdges;
            foreach (var edge in edges)
            {
                var nextState = ToState(edge.TargetNodeIndex, edge.ArrivalDirection);
                var duration = edge.DurationSeconds > 0
                    ? edge.DurationSeconds
                    : Math.Max(1, edge.DistanceMeters / 22.22f);
                var tentative = costs[currentState] + duration;
                if (tentative >= costs[nextState])
                {
                    continue;
                }

                costs[nextState] = tentative;
                previous[nextState] = currentState;
                previousDistance[nextState] = Math.Max(0, edge.DistanceMeters);
                previousDuration[nextState] = duration;
                previousFerry[nextState] = edge.IsFerry;
                var estimate = tentative + Heuristic(
                    graph.Nodes[edge.TargetNodeIndex].Position,
                    graph.Nodes[destination.NodeIndex].Position);
                open.Enqueue(nextState, estimate);
            }
        }

        if (destinationState < 0)
        {
            return null;
        }

        var states = new List<int>();
        for (var state = destinationState; state >= 0; state = previous[state])
        {
            states.Add(state);
        }

        states.Reverse();
        var points = states.Select(state => graph.Nodes[state / 2].Position).ToArray();
        var segmentDistances = new List<double>(Math.Max(0, states.Count - 1));
        var segmentDurations = new List<double>(Math.Max(0, states.Count - 1));
        var ferrySegments = new List<bool>(Math.Max(0, states.Count - 1));
        for (var index = 1; index < states.Count; index++)
        {
            segmentDistances.Add(previousDistance[states[index]]);
            segmentDurations.Add(previousDuration[states[index]]);
            ferrySegments.Add(previousFerry[states[index]]);
        }

        var totalDistance = segmentDistances.Sum();
        var totalDuration = segmentDurations.Sum();
        var steps = CreateSteps(points, segmentDistances, ferrySegments);
        var routeNodeIndexes = states.Select(state => state / 2).ToArray();
        return new RoutePlan(
            Guid.NewGuid().ToString("N"),
            graph.Metadata.Game,
            destination,
            points,
            segmentDistances,
            steps,
            totalDistance,
            totalDuration,
            DateTimeOffset.UtcNow,
            isReroute)
        {
            ContextRoads = CreateContextRoads(graph, routeNodeIndexes),
            MapPoints = CreateMapPoints(graph, points, destination),
            MapLabels = CreateMapLabels(graph, points, destination)
        };

        void Seed(RoadTravelDirection direction)
        {
            var state = ToState(startNode.Index, direction);
            var penalty = GetHeadingPenalty(graph, startNode, direction, headingDegrees);
            costs[state] = penalty;
            open.Enqueue(state, penalty + Heuristic(startNode.Position, graph.Nodes[destination.NodeIndex].Position));
        }
    }

    private static int ToState(int nodeIndex, RoadTravelDirection direction) => (nodeIndex * 2) + (int)direction;

    private static double Heuristic(WorldPoint from, WorldPoint to) =>
        from.DistanceTo(to) / MaximumHeuristicSpeedMetersPerSecond;

    private static double GetHeadingPenalty(
        RoadGraph graph,
        RoadNode node,
        RoadTravelDirection direction,
        double headingDegrees)
    {
        var edges = direction == RoadTravelDirection.Forward ? node.ForwardEdges : node.BackwardEdges;
        if (edges.Count == 0)
        {
            return 120;
        }

        var bestDifference = edges
            .Select(edge => NavigationManeuverClassifier.Bearing(
                node.Position,
                graph.Nodes[edge.TargetNodeIndex].Position))
            .Select(bearing => Math.Abs(NavigationManeuverClassifier.SignedAngleDifference(
                headingDegrees,
                bearing)))
            .Min();
        return bestDifference switch
        {
            > 135 => 90,
            > 80 => 25,
            > 45 => 8,
            _ => 0
        };
    }

    private static IReadOnlyList<NavigationStep> CreateSteps(
        IReadOnlyList<WorldPoint> points,
        IReadOnlyList<double> segmentDistances,
        IReadOnlyList<bool> ferrySegments)
    {
        var steps = new List<NavigationStep>
        {
            new(NavigationManeuverType.Depart, "Start driving", 0, 0)
        };
        var distanceFromStart = 0d;

        for (var index = 1; index < points.Count - 1; index++)
        {
            distanceFromStart += segmentDistances[index - 1];
            if (ferrySegments[index - 1])
            {
                steps.Add(new NavigationStep(
                    NavigationManeuverType.Ferry,
                    "Take the ferry or train connection",
                    index,
                    distanceFromStart));
                continue;
            }

            var maneuver = NavigationManeuverClassifier.Classify(
                points[index - 1],
                points[index],
                points[index + 1]);
            if (maneuver == NavigationManeuverType.Continue)
            {
                continue;
            }

            steps.Add(new NavigationStep(
                maneuver,
                NavigationManeuverClassifier.InstructionFor(maneuver),
                index,
                distanceFromStart));
        }

        steps.Add(new NavigationStep(
            NavigationManeuverType.Arrive,
            "Arrive at destination",
            Math.Max(0, points.Count - 1),
            segmentDistances.Sum()));
        return steps;
    }

    private static IReadOnlyList<RouteContextRoad> CreateContextRoads(
        RoadGraph graph,
        IReadOnlyList<int> routeNodeIndexes)
    {
        const int maximumRoads = 6_000;
        var roads = new List<RouteContextRoad>();
        var seen = new HashSet<long>();
        var neighboringNodes = new HashSet<int>();

        foreach (var nodeIndex in routeNodeIndexes)
        {
            if (roads.Count >= maximumRoads)
            {
                break;
            }

            AddNodeEdges(nodeIndex, collectNeighbors: true);
        }

        foreach (var nodeIndex in neighboringNodes)
        {
            if (roads.Count >= maximumRoads)
            {
                break;
            }

            AddNodeEdges(nodeIndex, collectNeighbors: false);
        }

        return roads;

        void AddNodeEdges(int nodeIndex, bool collectNeighbors)
        {
            if ((uint)nodeIndex >= graph.Nodes.Count)
            {
                return;
            }

            var node = graph.Nodes[nodeIndex];
            AddEdges(node.ForwardEdges);
            AddEdges(node.BackwardEdges);

            void AddEdges(IReadOnlyList<RoadEdge> edges)
            {
                foreach (var edge in edges)
                {
                    if (roads.Count >= maximumRoads || (uint)edge.TargetNodeIndex >= graph.Nodes.Count)
                    {
                        return;
                    }

                    var low = Math.Min(nodeIndex, edge.TargetNodeIndex);
                    var high = Math.Max(nodeIndex, edge.TargetNodeIndex);
                    var key = ((long)low << 32) | (uint)high;
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    var target = graph.Nodes[edge.TargetNodeIndex];
                    if (node.Position.DistanceTo(target.Position) < 0.5)
                    {
                        continue;
                    }

                    roads.Add(new RouteContextRoad(
                        node.Position,
                        target.Position,
                        IsMajor: !edge.IsOneLaneRoad,
                        edge.IsFerry));
                    if (collectNeighbors)
                    {
                        neighboringNodes.Add(edge.TargetNodeIndex);
                    }
                }
            }
        }
    }

    private static IReadOnlyList<RouteMapPoint> CreateMapPoints(
        RoadGraph graph,
        IReadOnlyList<WorldPoint> routePoints,
        ResolvedDestination destination)
    {
        const double maximumDistanceSquared = 2_500 * 2_500;
        var samples = SampleRoute(routePoints);
        var candidates = graph.PointsOfInterest
            .Select(point => (Point: point, Distance: DistanceToSamplesSquared(point.Position, samples)))
            .Where(candidate => candidate.Distance <= maximumDistanceSquared)
            .OrderBy(candidate => PoiPriority(candidate.Point.Type))
            .ThenBy(candidate => candidate.Distance)
            .Take(280)
            .Select(candidate => new RouteMapPoint(
                candidate.Point.Type,
                candidate.Point.Position,
                Label: null,
                IsDestination: false))
            .ToList();

        var companies = graph.Destinations
            .Select(point => (Point: point, Distance: DistanceToSamplesSquared(point.Position, samples)))
            .Where(candidate => candidate.Distance <= maximumDistanceSquared)
            .OrderBy(candidate => candidate.Distance)
            .Take(80)
            .Select(candidate => new RouteMapPoint(
                NavigationPoiType.Company,
                candidate.Point.Position,
                candidate.Point.CompanyName,
                IsDestination: candidate.Point.NodeIndex == destination.NodeIndex))
            .ToList();
        candidates.AddRange(companies);

        if (!candidates.Any(point => point.IsDestination))
        {
            candidates.Add(new RouteMapPoint(
                NavigationPoiType.Company,
                destination.Position,
                destination.DisplayName,
                IsDestination: true));
        }

        return candidates;
    }

    private static IReadOnlyList<RouteMapLabel> CreateMapLabels(
        RoadGraph graph,
        IReadOnlyList<WorldPoint> routePoints,
        ResolvedDestination destination)
    {
        const double maximumDistanceSquared = 7_500 * 7_500;
        var samples = SampleRoute(routePoints);
        var labels = graph.Destinations
            .Where(item => !string.IsNullOrWhiteSpace(item.CityName))
            .GroupBy(item => item.CityToken, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(item => (Item: item, Distance: DistanceToSamplesSquared(item.Position, samples)))
            .Where(candidate => candidate.Distance <= maximumDistanceSquared)
            .OrderBy(candidate => candidate.Distance)
            .Take(36)
            .Select(candidate => new RouteMapLabel(
                candidate.Item.CityName!,
                candidate.Item.Position,
                IsDestination: candidate.Item.CityToken.Equals(
                    destination.CityToken,
                    StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (!labels.Any(label => label.IsDestination))
        {
            var destinationCity = graph.Destinations.FirstOrDefault(item =>
                item.CityToken.Equals(destination.CityToken, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(destinationCity?.CityName))
            {
                labels.Add(new RouteMapLabel(
                    destinationCity.CityName,
                    destinationCity.Position,
                    IsDestination: true));
            }
        }

        return labels;
    }

    private static IReadOnlyList<WorldPoint> SampleRoute(IReadOnlyList<WorldPoint> points)
    {
        const int maximumSamples = 320;
        if (points.Count <= maximumSamples)
        {
            return points;
        }

        var stride = (int)Math.Ceiling(points.Count / (double)maximumSamples);
        var result = new List<WorldPoint>(maximumSamples + 1);
        for (var index = 0; index < points.Count; index += stride)
        {
            result.Add(points[index]);
        }

        if (result[^1] != points[^1])
        {
            result.Add(points[^1]);
        }

        return result;
    }

    private static double DistanceToSamplesSquared(WorldPoint point, IReadOnlyList<WorldPoint> samples)
    {
        var nearest = double.PositiveInfinity;
        foreach (var sample in samples)
        {
            var dx = point.X - sample.X;
            var dz = point.Z - sample.Z;
            nearest = Math.Min(nearest, (dx * dx) + (dz * dz));
        }

        return nearest;
    }

    private static int PoiPriority(NavigationPoiType type) => type switch
    {
        NavigationPoiType.Fuel => 0,
        NavigationPoiType.Service => 1,
        NavigationPoiType.Ferry => 2,
        NavigationPoiType.Rest => 3,
        NavigationPoiType.Garage => 4,
        NavigationPoiType.Dealer => 5,
        NavigationPoiType.WeighStation => 6,
        _ => 7
    };

}
