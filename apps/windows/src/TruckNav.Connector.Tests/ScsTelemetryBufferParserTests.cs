using System.Buffers.Binary;
using System.Text;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Navigation.Telemetry;

namespace TruckNav.Connector.Tests;

public sealed class ScsTelemetryBufferParserTests
{
    [Fact]
    public void TryParse_ShouldReadRevisionTwelveTelemetryAtDocumentedOffsets()
    {
        var buffer = new byte[ScsTelemetryBufferParser.MinimumBufferSize];
        buffer[0] = 1;
        buffer[4] = 1;
        buffer[4_300] = 1;
        Write(buffer, 8, 987_654UL);
        Write(buffer, 40, 12U);
        Write(buffer, 44, 1U);
        Write(buffer, 48, 60U);
        Write(buffer, 52, 1U);
        Write(buffer, 948, 25f);
        Write(buffer, 1_060, 12_345f);
        Write(buffer, 1_064, 678f);
        Write(buffer, 1_068, 22.2222f);
        Write(buffer, 2_200, 101.25d);
        Write(buffer, 2_208, 42.5d);
        Write(buffer, 2_216, -202.75d);
        Write(buffer, 2_224, 0.75d);
        Write(buffer, 2_684, "berlin");
        Write(buffer, 2_748, "Berlin");
        Write(buffer, 2_812, "transinet");
        Write(buffer, 2_876, "Transinet");
        var observed = new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);

        var parsed = ScsTelemetryBufferParser.TryParse(buffer, observed, out var snapshot);

        Assert.True(parsed);
        Assert.True(snapshot.Connected);
        Assert.True(snapshot.Paused);
        Assert.True(snapshot.OnJob);
        Assert.Equal(GameType.EuroTruckSimulator2, snapshot.Game);
        Assert.Equal(90, snapshot.SpeedKph, 3);
        Assert.Equal(80, snapshot.SpeedLimitKph, 2);
        Assert.Equal(101.25, snapshot.Position.X);
        Assert.Equal(-202.75, snapshot.Position.Z);
        Assert.Equal(42.5, snapshot.Elevation);
        Assert.Equal(90, snapshot.HeadingDegrees);
        Assert.Equal("berlin", snapshot.DestinationCityId);
        Assert.Equal("Transinet", snapshot.DestinationCompanyName);
        Assert.Equal(987_654UL, snapshot.GameTimestamp);
        Assert.Equal("1.60", snapshot.GameVersion);
        Assert.Equal(observed, snapshot.TimestampUtc);
    }

    [Fact]
    public void TryParse_ShouldRejectUnknownRevision()
    {
        var buffer = new byte[ScsTelemetryBufferParser.MinimumBufferSize];
        Write(buffer, 40, 11U);

        Assert.False(ScsTelemetryBufferParser.TryParse(buffer, DateTimeOffset.UtcNow, out _));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.25, 270)]
    [InlineData(0.5, 180)]
    [InlineData(0.75, 90)]
    [InlineData(1, 0)]
    public void HeadingConverter_ShouldMapScsCounterClockwiseUnitsToClockwiseCompass(
        double scsHeading,
        double expectedDegrees)
    {
        Assert.Equal(expectedDegrees, ScsHeadingConverter.ToNavigationDegrees(scsHeading), 6);
    }

    private static void Write(byte[] buffer, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset), value);

    private static void Write(byte[] buffer, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(offset), value);

    private static void Write(byte[] buffer, int offset, float value) =>
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(offset), BitConverter.SingleToInt32Bits(value));

    private static void Write(byte[] buffer, int offset, double value) =>
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(offset), BitConverter.DoubleToInt64Bits(value));

    private static void Write(byte[] buffer, int offset, string value) =>
        Encoding.UTF8.GetBytes(value).CopyTo(buffer.AsSpan(offset));
}
