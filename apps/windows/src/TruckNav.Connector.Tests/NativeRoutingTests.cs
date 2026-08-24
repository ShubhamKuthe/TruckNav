using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;
using TruckNav.Connector.Navigation.Routing;

namespace TruckNav.Connector.Tests;

public sealed class NativeRoutingTests
{
    [Fact]
    public void ExactDestinationResolver_ShouldPreferExactIdsWithoutFuzzyMatching()
    {
        var graph = CreateGraph();
        var resolver = new ExactDestinationResolver();
        var exact = CreateTelemetry(new WorldPoint(0, 0), DateTimeOffset.UtcNow) with
        {
            DestinationCityId = " Berlin ",
            DestinationCompanyId = "TRANSINET"
        };
        var fuzzy = exact with { DestinationCityId = "berli", DestinationCompanyId = "transi" };

        var result = resolver.Resolve(exact, graph);

        Assert.NotNull(result);
        Assert.Equal(2, result!.NodeIndex);
        Assert.Null(resolver.Resolve(fuzzy, graph));
    }

    [Fact]
    public void ExactDestinationResolver_ShouldRouteToPickupBeforeTrailerAttachment()
    {
        var graph = CreateGraph();
        var resolver = new ExactDestinationResolver();
        var pickupTelemetry = TelemetrySnapshot.Disconnected with
        {
            Game = GameType.EuroTruckSimulator2,
            Connected = true,
            OnJob = false,
            SourceCityId = "hamburg",
            SourceCompanyId = "eurogoodies",
            DestinationCityId = "berlin",
            DestinationCompanyId = "transinet"
        };

        var pickup = resolver.Resolve(pickupTelemetry, graph);
        var delivery = resolver.Resolve(pickupTelemetry with { OnJob = true }, graph);

        Assert.True(pickupTelemetry.HasPickupNavigationTarget);
        Assert.True(pickupTelemetry.HasNavigationTarget);
        Assert.True(pickupTelemetry.IsNavigatingToPickup);
        Assert.Contains(":pickup:", pickupTelemetry.NavigationTargetKey);
        Assert.NotNull(pickup);
        Assert.Equal(0, pickup!.NodeIndex);
        Assert.Equal("Pickup: EuroGoodies, Hamburg", pickup.DisplayName);
        Assert.NotNull(delivery);
        Assert.Equal(2, delivery!.NodeIndex);
        Assert.Equal("Transinet, Berlin", delivery.DisplayName);
    }

    [Fact]
    public void AStar_ShouldFollowDirectedEdgesAndReturnDistanceAndManeuvers()
    {
        var graph = CreateGraph();
        var destination = new ResolvedDestination("berlin", "transinet", "Transinet, Berlin", 2, new WorldPoint(200, 0));
        var planner = new AStarRoutePlanner();

        var route = planner.Plan(graph, new WorldPoint(0, 0), 90, destination);
        var reverse = planner.Plan(graph, new WorldPoint(200, 0), 270, destination with
        {
            CityToken = "start",
            CompanyToken = "depot",
            DisplayName = "Depot, Start",
            NodeIndex = 0,
            Position = new WorldPoint(0, 0)
        });

        Assert.NotNull(route);
        Assert.Equal(3, route!.Points.Count);
        Assert.Equal(200, route.TotalDistanceMeters);
        Assert.Equal(20, route.EstimatedTimeSeconds);
        Assert.Equal(NavigationManeuverType.Depart, route.Steps.First().Maneuver);
        Assert.Equal(NavigationManeuverType.Arrive, route.Steps.Last().Maneuver);
        Assert.Equal(2, route.ContextRoads.Count);
        Assert.Contains(route.MapPoints, point => point.Type == NavigationPoiType.Fuel);
        Assert.Contains(route.MapPoints, point => point.IsDestination);
        Assert.Contains(route.MapLabels, label => label.Text == "Berlin" && label.IsDestination);
        Assert.Null(reverse);
    }

    [Fact]
    public void ProgressTracker_ShouldDebounceOffRouteBeforeRerouting()
    {
        var graph = CreateGraph();
        var route = new AStarRoutePlanner().Plan(
            graph,
            new WorldPoint(0, 0),
            90,
            new ResolvedDestination("berlin", "transinet", "Transinet, Berlin", 2, new WorldPoint(200, 0)))!;
        var tracker = new RouteProgressTracker();
        var start = new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

        var first = tracker.Update(route, CreateTelemetry(new WorldPoint(20, 100), start));
        var second = tracker.Update(route, CreateTelemetry(new WorldPoint(22, 100), start.AddMilliseconds(500)));
        var third = tracker.Update(route, CreateTelemetry(new WorldPoint(25, 100), start.AddMilliseconds(1_100)));

        Assert.True(first.IsOffRoute);
        Assert.False(first.ShouldReroute);
        Assert.False(second.ShouldReroute);
        Assert.True(third.ShouldReroute);
        Assert.InRange(third.RemainingDistanceMeters, 174, 176);
    }

    [Fact]
    public void ProgressTracker_ShouldNotMoveBackwardOnTheCurrentSegment()
    {
        var route = CreateRoute(
            new WorldPoint(0, 0),
            new WorldPoint(100, 0),
            new WorldPoint(200, 0),
            new WorldPoint(300, 0),
            new WorldPoint(400, 0));
        var tracker = new RouteProgressTracker();
        var now = DateTimeOffset.UtcNow;

        var forward = tracker.Update(
            route,
            CreateTelemetry(new WorldPoint(250, 0), now) with { HeadingDegrees = 90 });
        var noisyBackward = tracker.Update(
            route,
            CreateTelemetry(new WorldPoint(190, 0), now.AddMilliseconds(100)) with { HeadingDegrees = 90 });

        Assert.Equal(2, forward.SegmentIndex);
        Assert.Equal(2, noisyBackward.SegmentIndex);
        Assert.Equal(forward.RemainingDistanceMeters, noisyBackward.RemainingDistanceMeters, 6);
    }

    [Fact]
    public void ProgressTracker_ShouldPreferForwardHeadingOnNearbyParallelRouteSections()
    {
        var route = CreateRoute(
            new WorldPoint(0, 0),
            new WorldPoint(100, 0),
            new WorldPoint(200, 0),
            new WorldPoint(200, 10),
            new WorldPoint(100, 10),
            new WorldPoint(0, 10));
        var tracker = new RouteProgressTracker();

        var progress = tracker.Update(
            route,
            CreateTelemetry(new WorldPoint(150, 8), DateTimeOffset.UtcNow) with { HeadingDegrees = 90 });

        Assert.Equal(1, progress.SegmentIndex);
        Assert.InRange(progress.ProjectedPosition.X, 149.9, 150.1);
        Assert.Equal(0, progress.ProjectedPosition.Z, 6);
    }

    [Theory]
    [InlineData(0, NavigationManeuverType.Continue)]
    [InlineData(30, NavigationManeuverType.SlightRight)]
    [InlineData(-30, NavigationManeuverType.SlightLeft)]
    [InlineData(90, NavigationManeuverType.Right)]
    [InlineData(-90, NavigationManeuverType.Left)]
    [InlineData(120, NavigationManeuverType.SharpRight)]
    [InlineData(-120, NavigationManeuverType.SharpLeft)]
    [InlineData(180, NavigationManeuverType.UTurn)]
    public void ManeuverClassifier_ShouldUseClockwiseRightTurnConvention(
        double angle,
        NavigationManeuverType expected)
    {
        Assert.Equal(expected, NavigationManeuverClassifier.ClassifyTurn(angle));
    }

    [Fact]
    public void ManeuverClassifier_ShouldIdentifyNorthToEastAsRightTurn()
    {
        var maneuver = NavigationManeuverClassifier.Classify(
            new WorldPoint(0, 100),
            new WorldPoint(0, 0),
            new WorldPoint(100, 0));

        Assert.Equal(NavigationManeuverType.Right, maneuver);
    }

    private static RoadGraph CreateGraph()
    {
        var nodes = new[]
        {
            new RoadNode(0, "0", new WorldPoint(0, 0), [Edge(1)], []),
            new RoadNode(1, "1", new WorldPoint(100, 0), [Edge(2)], []),
            new RoadNode(2, "2", new WorldPoint(200, 0), [], [])
        };
        var destinations = new[]
        {
            new RoadDestination("hamburg", "eurogoodies", "Hamburg", "EuroGoodies", 0, new WorldPoint(0, 0)),
            new RoadDestination("berlin", "transinet", "Berlin", "Transinet", 2, new WorldPoint(200, 0))
        };
        return new RoadGraph(
            new RoadGraphMetadata(
                GameType.EuroTruckSimulator2,
                "1.60",
                "test",
                "test",
                1,
                DateTimeOffset.UtcNow,
                "test",
                "test"),
            nodes,
            destinations,
            [new RoadPointOfInterest(NavigationPoiType.Fuel, new WorldPoint(100, 15))]);
    }

    private static RoadEdge Edge(int target) =>
        new(target, 100, 10, RoadTravelDirection.Forward, false, false, 0);

    private static RoutePlan CreateRoute(params WorldPoint[] points)
    {
        var segmentDistances = points
            .Zip(points.Skip(1), (start, end) => start.DistanceTo(end))
            .ToArray();
        var totalDistance = segmentDistances.Sum();
        return new RoutePlan(
            "progress-test",
            GameType.EuroTruckSimulator2,
            new ResolvedDestination("test", "test", "Test destination", points.Length - 1, points[^1]),
            points,
            segmentDistances,
            [
                new NavigationStep(NavigationManeuverType.Depart, "Start driving", 0, 0),
                new NavigationStep(NavigationManeuverType.Arrive, "Arrive", points.Length - 1, totalDistance)
            ],
            totalDistance,
            totalDistance / 20,
            DateTimeOffset.UtcNow,
            false);
    }

    private static TelemetrySnapshot CreateTelemetry(WorldPoint position, DateTimeOffset timestamp) =>
        TelemetrySnapshot.Disconnected with
        {
            Game = GameType.EuroTruckSimulator2,
            Connected = true,
            Position = position,
            OnJob = true,
            DestinationCityId = "berlin",
            DestinationCompanyId = "transinet",
            TimestampUtc = timestamp,
            GameTimestamp = (ulong)Math.Max(1, timestamp.Ticks)
        };
}
