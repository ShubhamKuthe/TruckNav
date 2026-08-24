namespace TruckNav.Connector.Navigation.Telemetry;

public static class ScsHeadingConverter
{
    /// <summary>
    /// Converts the SCS SDK unit heading into a clockwise compass bearing.
    /// SCS uses 0=north/-Z and increases counter-clockwise; TruckNav uses
    /// 0=north/-Z, 90=east/+X, 180=south/+Z, and 270=west/-X.
    /// </summary>
    public static double ToNavigationDegrees(double scsUnitHeading) =>
        NormalizeDegrees(-scsUnitHeading * 360d);

    public static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360d;
        return normalized < 0 ? normalized + 360d : normalized;
    }
}
