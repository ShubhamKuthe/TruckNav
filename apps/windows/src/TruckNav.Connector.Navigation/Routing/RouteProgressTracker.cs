using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Routing;

public sealed class RouteProgressTracker : IRouteProgressTracker
{
    private const double OffRouteThresholdMeters = 80;
    private const double TeleportThresholdMeters = 2_000;
    private static readonly TimeSpan OffRouteStableDuration = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RerouteCooldown = TimeSpan.FromSeconds(10);

    private string? _routeId;
    private int _segmentIndex;
    private double _segmentFraction;
    private int _offRouteSamples;
    private DateTimeOffset? _offRouteSince;
    private DateTimeOffset _lastRerouteUtc = DateTimeOffset.MinValue;
    private WorldPoint? _lastPosition;

    public RouteProgressSnapshot Update(RoutePlan route, TelemetrySnapshot telemetry)
    {
        if (!string.Equals(_routeId, route.RouteId, StringComparison.Ordinal))
        {
            Reset();
            _routeId = route.RouteId;
        }

        if (route.Points.Count < 2)
        {
            throw new ArgumentException("A route must contain at least two points.", nameof(route));
        }

        var teleportDetected = _lastPosition is { } previous &&
                               previous.DistanceTo(telemetry.Position) >= TeleportThresholdMeters;
        _lastPosition = telemetry.Position;

        var start = teleportDetected ? 0 : _segmentIndex;
        var end = Math.Min(route.Points.Count - 2, _segmentIndex + 32);
        var projection = FindBestForwardSegment(
            route.Points,
            telemetry.Position,
            telemetry.HeadingDegrees,
            start,
            end,
            _segmentIndex);
        if (projection.DistanceMeters > OffRouteThresholdMeters * 2)
        {
            projection = FindBestForwardSegment(
                route.Points,
                telemetry.Position,
                telemetry.HeadingDegrees,
                teleportDetected ? 0 : _segmentIndex,
                route.Points.Count - 2,
                _segmentIndex);
        }

        if (!teleportDetected && projection.SegmentIndex == _segmentIndex && projection.Fraction < _segmentFraction)
        {
            projection = ProjectAtFraction(
                telemetry.Position,
                route.Points[_segmentIndex],
                route.Points[_segmentIndex + 1],
                _segmentIndex,
                _segmentFraction);
        }

        _segmentIndex = projection.SegmentIndex;
        _segmentFraction = projection.Fraction;
        var offRoute = projection.DistanceMeters > OffRouteThresholdMeters;
        if (offRoute)
        {
            _offRouteSamples++;
            _offRouteSince ??= telemetry.TimestampUtc;
        }
        else
        {
            _offRouteSamples = 0;
            _offRouteSince = null;
        }

        var stableOffRoute = offRoute &&
                             _offRouteSamples >= 3 &&
                             telemetry.TimestampUtc - _offRouteSince >= OffRouteStableDuration;
        var shouldReroute = (teleportDetected || stableOffRoute) &&
                            telemetry.TimestampUtc - _lastRerouteUtc >= RerouteCooldown;
        if (shouldReroute)
        {
            _lastRerouteUtc = telemetry.TimestampUtc;
        }

        var remainingDistance = CalculateRemainingDistance(route, projection);
        var remainingTime = route.TotalDistanceMeters <= 0
            ? 0
            : route.EstimatedTimeSeconds * (remainingDistance / route.TotalDistanceMeters);
        var distanceFromStart = Math.Max(0, route.TotalDistanceMeters - remainingDistance);
        var nextStep = route.Steps.FirstOrDefault(step => step.DistanceFromStartMeters > distanceFromStart + 5) ??
                       route.Steps[^1];

        return new RouteProgressSnapshot(
            route.RouteId,
            projection.SegmentIndex,
            projection.Point,
            projection.DistanceMeters,
            remainingDistance,
            remainingTime,
            nextStep.Maneuver,
            nextStep.Instruction,
            Math.Max(0, nextStep.DistanceFromStartMeters - distanceFromStart),
            offRoute,
            shouldReroute,
            teleportDetected,
            telemetry.TimestampUtc);
    }

    public void Reset()
    {
        _routeId = null;
        _segmentIndex = 0;
        _segmentFraction = 0;
        _offRouteSamples = 0;
        _offRouteSince = null;
        _lastPosition = null;
    }

    private static double CalculateRemainingDistance(RoutePlan route, SegmentProjection projection)
    {
        var currentSegmentDistance = route.SegmentDistancesMeters[projection.SegmentIndex];
        var remaining = currentSegmentDistance * (1 - projection.Fraction);
        for (var index = projection.SegmentIndex + 1; index < route.SegmentDistancesMeters.Count; index++)
        {
            remaining += route.SegmentDistancesMeters[index];
        }

        return Math.Max(0, remaining);
    }

    private static SegmentProjection FindBestForwardSegment(
        IReadOnlyList<WorldPoint> points,
        WorldPoint position,
        double headingDegrees,
        int start,
        int end,
        int currentSegmentIndex)
    {
        var best = new SegmentProjection(start, points[start], double.PositiveInfinity, 0);
        var bestScore = double.PositiveInfinity;
        for (var index = start; index <= end; index++)
        {
            var candidate = Project(position, points[index], points[index + 1], index);
            var segmentBearing = Bearing(points[index], points[index + 1]);
            var headingDifference = Math.Abs(SignedAngleDifference(headingDegrees, segmentBearing));
            var headingPenalty = headingDifference <= 70 ? 0 : Math.Min(55, (headingDifference - 70) * 0.5);
            var forwardJumpPenalty = Math.Max(0, index - currentSegmentIndex) * 1.5;
            var score = candidate.DistanceMeters + headingPenalty + forwardJumpPenalty;
            if (score < bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    private static SegmentProjection Project(WorldPoint point, WorldPoint start, WorldPoint end, int segmentIndex)
    {
        var dx = end.X - start.X;
        var dz = end.Z - start.Z;
        var lengthSquared = (dx * dx) + (dz * dz);
        var fraction = lengthSquared <= double.Epsilon
            ? 0
            : Math.Clamp(((point.X - start.X) * dx + (point.Z - start.Z) * dz) / lengthSquared, 0, 1);
        var projected = new WorldPoint(start.X + (dx * fraction), start.Z + (dz * fraction));
        return new SegmentProjection(segmentIndex, projected, point.DistanceTo(projected), fraction);
    }

    private static SegmentProjection ProjectAtFraction(
        WorldPoint point,
        WorldPoint start,
        WorldPoint end,
        int segmentIndex,
        double fraction)
    {
        var projected = new WorldPoint(
            start.X + ((end.X - start.X) * fraction),
            start.Z + ((end.Z - start.Z) * fraction));
        return new SegmentProjection(segmentIndex, projected, point.DistanceTo(projected), fraction);
    }

    private static double Bearing(WorldPoint from, WorldPoint to)
    {
        var degrees = Math.Atan2(to.X - from.X, -(to.Z - from.Z)) * 180 / Math.PI;
        return degrees < 0 ? degrees + 360 : degrees;
    }

    private static double SignedAngleDifference(double from, double to) =>
        (to - from + 540) % 360 - 180;

    private sealed record SegmentProjection(
        int SegmentIndex,
        WorldPoint Point,
        double DistanceMeters,
        double Fraction);
}
