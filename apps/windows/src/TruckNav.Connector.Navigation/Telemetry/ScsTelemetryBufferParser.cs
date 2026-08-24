using System.Buffers.Binary;
using System.Text;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Telemetry;

public static class ScsTelemetryBufferParser
{
    public const int MinimumBufferSize = 4_400;
    public const uint SupportedPluginRevision = 12;

    public static bool TryParse(ReadOnlySpan<byte> buffer, DateTimeOffset observedUtc, out TelemetrySnapshot snapshot)
    {
        snapshot = TelemetrySnapshot.Disconnected;
        if (buffer.Length < MinimumBufferSize)
        {
            return false;
        }

        var pluginRevision = ReadUInt32(buffer, 40);
        if (pluginRevision != SupportedPluginRevision)
        {
            return false;
        }

        var game = ReadUInt32(buffer, 52) switch
        {
            1 => GameType.EuroTruckSimulator2,
            2 => GameType.AmericanTruckSimulator,
            _ => GameType.None
        };
        var sdkActive = buffer[0] != 0;
        var version = $"{ReadUInt32(buffer, 44)}.{ReadUInt32(buffer, 48)}";

        snapshot = new TelemetrySnapshot(
            game,
            Connected: sdkActive && game != GameType.None,
            Paused: buffer[4] != 0,
            Position: new WorldPoint(ReadDouble(buffer, 2_200), ReadDouble(buffer, 2_216)),
            Elevation: ReadDouble(buffer, 2_208),
            HeadingDegrees: ScsHeadingConverter.ToNavigationDegrees(ReadDouble(buffer, 2_224)),
            SpeedKph: Math.Max(0, ReadSingle(buffer, 948) * 3.6d),
            SpeedLimitKph: Math.Max(0, ReadSingle(buffer, 1_068) * 3.6d),
            OnJob: buffer[4_300] != 0,
            SourceCityId: ReadString(buffer, 2_940, 64),
            SourceCityName: ReadString(buffer, 3_004, 64),
            SourceCompanyId: ReadString(buffer, 3_068, 64),
            SourceCompanyName: ReadString(buffer, 3_132, 64),
            DestinationCityId: ReadString(buffer, 2_684, 64),
            DestinationCityName: ReadString(buffer, 2_748, 64),
            DestinationCompanyId: ReadString(buffer, 2_812, 64),
            DestinationCompanyName: ReadString(buffer, 2_876, 64),
            GameNavigationDistanceMeters: Math.Max(0, ReadSingle(buffer, 1_060)),
            GameNavigationTimeSeconds: Math.Max(0, ReadSingle(buffer, 1_064)),
            GameTimestamp: ReadUInt64(buffer, 8),
            TimestampUtc: observedUtc,
            PluginRevision: pluginRevision,
            GameVersion: version);
        return true;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> buffer, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(offset, sizeof(uint)));

    private static ulong ReadUInt64(ReadOnlySpan<byte> buffer, int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(offset, sizeof(ulong)));

    private static float ReadSingle(ReadOnlySpan<byte> buffer, int offset) =>
        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(offset, sizeof(float))));

    private static double ReadDouble(ReadOnlySpan<byte> buffer, int offset) =>
        BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(buffer.Slice(offset, sizeof(double))));

    private static string? ReadString(ReadOnlySpan<byte> buffer, int offset, int length)
    {
        var field = buffer.Slice(offset, length);
        var terminator = field.IndexOf((byte)0);
        if (terminator >= 0)
        {
            field = field[..terminator];
        }

        var value = Encoding.UTF8.GetString(field).Trim();
        return value.Length == 0 ? null : value;
    }

}
