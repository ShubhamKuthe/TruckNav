using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Setup;

public sealed class SteamGameInstallationLocator : IGameInstallationLocator
{
    private static readonly GameDefinition[] Games =
    [
        new(GameType.EuroTruckSimulator2, "227300", "Euro Truck Simulator 2", "bin\\win_x64\\eurotrucks2.exe"),
        new(GameType.AmericanTruckSimulator, "270880", "American Truck Simulator", "bin\\win_x64\\amtrucks.exe")
    ];

    private readonly string _manualPathsFile;

    public SteamGameInstallationLocator()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TruckNav",
            "game-paths.json"))
    {
    }

    public SteamGameInstallationLocator(string manualPathsFile)
    {
        _manualPathsFile = manualPathsFile;
    }

    public IReadOnlyList<GameInstallation> FindInstallations()
    {
        var libraries = FindSteamLibraries();
        var manualPaths = ReadManualPaths();
        var installations = new List<GameInstallation>();

        foreach (var game in Games)
        {
            var candidates = new List<(string Path, string Library)>();
            foreach (var library in libraries)
            {
                var manifestPath = Path.Combine(library, "steamapps", $"appmanifest_{game.AppId}.acf");
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                var installDirectory = SteamLibraryParser.ParseInstallDirectory(File.ReadAllText(manifestPath));
                if (!string.IsNullOrWhiteSpace(installDirectory))
                {
                    candidates.Add((Path.Combine(library, "steamapps", "common", installDirectory), library));
                }
            }

            if (manualPaths.TryGetValue(game.Game.ToString(), out var manualPath))
            {
                candidates.Add((manualPath, "Manual"));
            }

            foreach (var candidate in candidates.DistinctBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
            {
                var executablePath = Path.Combine(candidate.Path, game.ExecutableRelativePath);
                if (!File.Exists(executablePath))
                {
                    continue;
                }

                var version = FileVersionInfo.GetVersionInfo(executablePath).ProductVersion ?? "Unknown";
                installations.Add(new GameInstallation(
                    game.Game,
                    Path.GetFullPath(candidate.Path),
                    Path.GetFullPath(executablePath),
                    version,
                    candidate.Library,
                    CalculateDlcSignature(candidate.Path)));
                break;
            }
        }

        return installations;
    }

    public bool TrySetManualPath(GameType game, string path, out string errorMessage)
    {
        var definition = Games.FirstOrDefault(value => value.Game == game);
        if (definition is null)
        {
            errorMessage = "Unsupported game.";
            return false;
        }

        var normalized = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!File.Exists(Path.Combine(normalized, definition.ExecutableRelativePath)))
        {
            errorMessage = $"This folder does not contain {definition.ExecutableRelativePath}.";
            return false;
        }

        try
        {
            var paths = ReadManualPaths();
            paths[game.ToString()] = Path.GetFullPath(normalized);
            Directory.CreateDirectory(Path.GetDirectoryName(_manualPathsFile)!);
            File.WriteAllText(
                _manualPathsFile,
                JsonSerializer.Serialize(paths, new JsonSerializerOptions { WriteIndented = true }));
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            errorMessage = exception.Message;
            return false;
        }
    }

    private static IReadOnlyList<string> FindSteamLibraries()
    {
        var steamPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (hive, view, subKey, valueName) in RegistryCandidates())
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var key = baseKey.OpenSubKey(subKey);
                if (key?.GetValue(valueName) is string value && Directory.Exists(value))
                {
                    steamPaths.Add(Path.GetFullPath(value));
                }
            }
            catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException)
            {
            }
        }

        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        if (Directory.Exists(defaultPath))
        {
            steamPaths.Add(defaultPath);
        }

        var libraries = new HashSet<string>(steamPaths, StringComparer.OrdinalIgnoreCase);
        foreach (var steamPath in steamPaths)
        {
            var manifest = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(manifest))
            {
                continue;
            }

            foreach (var path in SteamLibraryParser.ParseLibraryPaths(File.ReadAllText(manifest), steamPath))
            {
                if (Directory.Exists(path))
                {
                    libraries.Add(path);
                }
            }
        }

        return libraries.ToArray();
    }

    private Dictionary<string, string> ReadManualPaths()
    {
        try
        {
            return File.Exists(_manualPathsFile)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_manualPathsFile)) ?? []
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string CalculateDlcSignature(string gamePath)
    {
        var descriptor = string.Join(
            '\n',
            Directory.EnumerateFiles(gamePath, "dlc_*.scs", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .Select(file => $"{file.Name}:{file.Length}:{file.LastWriteTimeUtc.Ticks}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(descriptor)));
    }

    private static IEnumerable<(RegistryHive Hive, RegistryView View, string SubKey, string ValueName)> RegistryCandidates()
    {
        yield return (RegistryHive.CurrentUser, RegistryView.Default, "Software\\Valve\\Steam", "SteamPath");
        yield return (RegistryHive.LocalMachine, RegistryView.Registry64, "SOFTWARE\\Valve\\Steam", "InstallPath");
        yield return (RegistryHive.LocalMachine, RegistryView.Registry32, "SOFTWARE\\Valve\\Steam", "InstallPath");
    }

    private sealed record GameDefinition(
        GameType Game,
        string AppId,
        string DefaultInstallDirectory,
        string ExecutableRelativePath);
}
