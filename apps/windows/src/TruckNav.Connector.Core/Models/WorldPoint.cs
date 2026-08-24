namespace TruckNav.Connector.Core.Models;

public readonly record struct WorldPoint(double X, double Z)
{
    public double DistanceTo(WorldPoint other)
    {
        var dx = other.X - X;
        var dz = other.Z - Z;
        return Math.Sqrt((dx * dx) + (dz * dz));
    }
}
