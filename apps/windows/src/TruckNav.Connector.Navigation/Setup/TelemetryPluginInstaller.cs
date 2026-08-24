using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Setup;

public sealed class TelemetryPluginInstaller : ITelemetryPluginInstaller
{
    public const string PluginFileName = "trucksim-gps-telemetry.dll";
    public const string UpstreamCommit = "c5686cb41b36d0978dabcce24f730bbc1bf0b4bf";

    private readonly IGameInstallationLocator _installationLocator;
    private readonly string _bundledPluginPath;

    public TelemetryPluginInstaller(IGameInstallationLocator installationLocator)
    {
        _installationLocator = installationLocator;
        _bundledPluginPath = Path.Combine(
            AppContext.BaseDirectory,
            "ThirdParty",
            "TruckSimGpsTelemetry",
            PluginFileName);
    }

    public TelemetryPluginStatus GetStatus(GameType game)
    {
        var installation = _installationLocator.FindInstallations().FirstOrDefault(item => item.Game == game);
        if (installation is null)
        {
            return new TelemetryPluginStatus(game, false, false, false, null, null, "Game installation not found.");
        }

        var targetPath = GetTargetPath(installation);
        if (!File.Exists(targetPath))
        {
            return new TelemetryPluginStatus(
                game,
                true,
                false,
                false,
                installation.InstallPath,
                targetPath,
                "Telemetry plugin is not installed.");
        }

        var current = File.Exists(_bundledPluginPath) && HashesMatch(targetPath, _bundledPluginPath);
        return new TelemetryPluginStatus(
            game,
            true,
            true,
            current,
            installation.InstallPath,
            targetPath,
            current ? "Telemetry plugin is installed and current." : "Telemetry plugin update is available.");
    }

    public async Task<TelemetryPluginStatus> InstallAsync(GameType game, CancellationToken cancellationToken = default)
    {
        var installation = _installationLocator.FindInstallations().FirstOrDefault(item => item.Game == game);
        if (installation is null)
        {
            return GetStatus(game);
        }

        if (!File.Exists(_bundledPluginPath))
        {
            return new TelemetryPluginStatus(
                game,
                true,
                false,
                false,
                installation.InstallPath,
                null,
                "The verified bundled telemetry plugin is missing. Reinstall TruckNav Connector.");
        }

        var processName = game == GameType.EuroTruckSimulator2 ? "eurotrucks2" : "amtrucks";
        if (Process.GetProcessesByName(processName).Length > 0)
        {
            return new TelemetryPluginStatus(
                game,
                true,
                File.Exists(GetTargetPath(installation)),
                false,
                installation.InstallPath,
                GetTargetPath(installation),
                "Close the game before installing the telemetry plugin.");
        }

        var targetPath = GetTargetPath(installation);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        await using (var source = File.OpenRead(_bundledPluginPath))
        await using (var destination = File.Create(targetPath))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        await using var installedPlugin = File.OpenRead(targetPath);
        var installedHash = await SHA256.HashDataAsync(installedPlugin, cancellationToken);
        var provenance = new
        {
            source = "TruckSim-GPS/trucksim-gps-plugin",
            commit = UpstreamCommit,
            license = "MIT",
            installedUtc = DateTimeOffset.UtcNow,
            sha256 = Convert.ToHexString(installedHash)
        };
        var provenancePath = Path.Combine(Path.GetDirectoryName(targetPath)!, "trucknav-plugin-provenance.json");
        await File.WriteAllTextAsync(
            provenancePath,
            JsonSerializer.Serialize(provenance, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        return GetStatus(game);
    }

    private static string GetTargetPath(GameInstallation installation) =>
        Path.Combine(installation.InstallPath, "bin", "win_x64", "plugins", PluginFileName);

    private static bool HashesMatch(string leftPath, string rightPath)
    {
        using var left = File.OpenRead(leftPath);
        using var right = File.OpenRead(rightPath);
        return SHA256.HashData(left).SequenceEqual(SHA256.HashData(right));
    }
}
