using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Routing;

public static class NavigationManeuverClassifier
{
    public static NavigationManeuverType Classify(WorldPoint previous, WorldPoint current, WorldPoint next)
    {
        var incoming = Bearing(previous, current);
        var outgoing = Bearing(current, next);
        return ClassifyTurn(SignedAngleDifference(incoming, outgoing));
    }

    public static NavigationManeuverType ClassifyTurn(double angle)
    {
        var magnitude = Math.Abs(angle);
        if (magnitude < 28)
        {
            return NavigationManeuverType.Continue;
        }

        if (magnitude > 155)
        {
            return NavigationManeuverType.UTurn;
        }

        var right = angle > 0;
        return magnitude switch
        {
            < 50 => right ? NavigationManeuverType.SlightRight : NavigationManeuverType.SlightLeft,
            < 115 => right ? NavigationManeuverType.Right : NavigationManeuverType.Left,
            _ => right ? NavigationManeuverType.SharpRight : NavigationManeuverType.SharpLeft
        };
    }

    public static double Bearing(WorldPoint from, WorldPoint to)
    {
        var degrees = Math.Atan2(to.X - from.X, -(to.Z - from.Z)) * 180 / Math.PI;
        return degrees < 0 ? degrees + 360 : degrees;
    }

    public static double SignedAngleDifference(double from, double to) =>
        (to - from + 540) % 360 - 180;

    public static string InstructionFor(NavigationManeuverType maneuver) => maneuver switch
    {
        NavigationManeuverType.SlightLeft => "Keep left",
        NavigationManeuverType.Left => "Turn left",
        NavigationManeuverType.SharpLeft => "Make a sharp left",
        NavigationManeuverType.SlightRight => "Keep right",
        NavigationManeuverType.Right => "Turn right",
        NavigationManeuverType.SharpRight => "Make a sharp right",
        NavigationManeuverType.UTurn => "Make a U-turn when safe",
        _ => "Continue"
    };
}
