using System.Text.Json;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;
using TruckNav.Connector.Core.Utilities;

namespace TruckNav.Connector.Capture;

public sealed class JsonGpsCaptureProfileStore : IGpsCaptureProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly object _gate = new();
    private readonly string _profilePath;
    private Dictionary<GameType, GpsCaptureProfile> _profiles;

    public event EventHandler? ProfilesChanged;

    public JsonGpsCaptureProfileStore(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("A profile storage directory is required.", nameof(rootDirectory));
        }

        Directory.CreateDirectory(rootDirectory);
        _profilePath = Path.Combine(rootDirectory, "gps-capture-profiles.json");
        _profiles = LoadProfiles();
    }

    public IReadOnlyDictionary<GameType, GpsCaptureProfile> GetAll()
    {
        lock (_gate)
        {
            return new Dictionary<GameType, GpsCaptureProfile>(_profiles);
        }
    }

    public GpsCaptureProfile? Get(GameType gameType)
    {
        lock (_gate)
        {
            return _profiles.GetValueOrDefault(gameType);
        }
    }

    public void Save(GpsCaptureProfile profile)
    {
        Validate(profile);

        lock (_gate)
        {
            var updated = new Dictionary<GameType, GpsCaptureProfile>(_profiles)
            {
                [profile.GameType] = profile
            };

            var temporaryPath = $"{_profilePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(
                    temporaryPath,
                    JsonSerializer.Serialize(new ProfileDocument(updated.Values.ToArray()), JsonOptions));
                File.Move(temporaryPath, _profilePath, true);
                _profiles = updated;
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    private Dictionary<GameType, GpsCaptureProfile> LoadProfiles()
    {
        if (!File.Exists(_profilePath))
        {
            return new Dictionary<GameType, GpsCaptureProfile>();
        }

        try
        {
            var document = JsonSerializer.Deserialize<ProfileDocument>(
                File.ReadAllText(_profilePath),
                JsonOptions);

            return document?.Profiles
                .Where(IsValid)
                .GroupBy(profile => profile.GameType)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(profile => profile.UpdatedAtUtc).First())
                ?? new Dictionary<GameType, GpsCaptureProfile>();
        }
        catch (JsonException)
        {
            return new Dictionary<GameType, GpsCaptureProfile>();
        }
        catch (IOException)
        {
            return new Dictionary<GameType, GpsCaptureProfile>();
        }
    }

    private static bool IsValid(GpsCaptureProfile profile)
    {
        try
        {
            Validate(profile);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void Validate(GpsCaptureProfile profile)
    {
        if (profile.SchemaVersion != GpsCaptureProfile.CurrentSchemaVersion ||
            profile.GameType is not (GameType.EuroTruckSimulator2 or GameType.AmericanTruckSimulator) ||
            profile.SourceType == CaptureSourceType.DebugMock ||
            string.IsNullOrWhiteSpace(profile.SourceId) ||
            profile.ReferenceSourceWidth <= 0 ||
            profile.ReferenceSourceHeight <= 0)
        {
            throw new ArgumentException("The GPS capture profile metadata is invalid.", nameof(profile));
        }

        CaptureRegionMath.ValidateNormalizedRegion(profile.Region);
    }

    private sealed record ProfileDocument(IReadOnlyList<GpsCaptureProfile> Profiles);
}
