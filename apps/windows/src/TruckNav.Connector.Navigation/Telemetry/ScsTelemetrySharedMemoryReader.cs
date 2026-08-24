using System.IO.MemoryMappedFiles;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Telemetry;

public sealed class ScsTelemetrySharedMemoryReader : ITelemetryReader
{
    public const string MapName = "Local\\TSGPSTelemetry";
    public const int MapSize = 32 * 1024;

    private readonly byte[] _buffer = new byte[ScsTelemetryBufferParser.MinimumBufferSize];
    private MemoryMappedFile? _memoryMappedFile;
    private MemoryMappedViewAccessor? _view;

    public bool TryRead(out TelemetrySnapshot snapshot)
    {
        snapshot = TelemetrySnapshot.Disconnected;

        try
        {
            if (!EnsureConnected())
            {
                return false;
            }

            var timestampBefore = _view!.ReadUInt64(8);
            _view.ReadArray(0, _buffer, 0, _buffer.Length);
            var timestampAfter = _view.ReadUInt64(8);
            if (timestampBefore != timestampAfter)
            {
                return false;
            }

            return ScsTelemetryBufferParser.TryParse(_buffer, DateTimeOffset.UtcNow, out snapshot);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            Disconnect();
            return false;
        }
    }

    public void Dispose() => Disconnect();

    private bool EnsureConnected()
    {
        if (_view is not null)
        {
            return true;
        }

        try
        {
            _memoryMappedFile = MemoryMappedFile.OpenExisting(MapName, MemoryMappedFileRights.Read);
            _view = _memoryMappedFile.CreateViewAccessor(0, MapSize, MemoryMappedFileAccess.Read);
            return true;
        }
        catch (FileNotFoundException)
        {
            Disconnect();
            return false;
        }
    }

    private void Disconnect()
    {
        _view?.Dispose();
        _memoryMappedFile?.Dispose();
        _view = null;
        _memoryMappedFile = null;
    }
}
