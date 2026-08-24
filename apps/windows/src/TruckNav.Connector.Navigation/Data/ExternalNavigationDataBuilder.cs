using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TruckNav.Connector.Core.Enums;
using TruckNav.Connector.Core.Interfaces;
using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Navigation.Data;

public sealed class ExternalNavigationDataBuilder : INavigationDataBuilder
{
    private readonly IGameInstallationLocator _installationLocator;
    private readonly IRoadGraphProvider _graphProvider;
    private readonly IRoadGraphRepository _repository;
    private readonly NavigationStoragePaths _paths;
    private readonly ILogger<ExternalNavigationDataBuilder> _logger;

    public ExternalNavigationDataBuilder(
        IGameInstallationLocator installationLocator,
        IRoadGraphProvider graphProvider,
        IRoadGraphRepository repository,
        NavigationStoragePaths paths,
        ILogger<ExternalNavigationDataBuilder> logger)
    {
        _installationLocator = installationLocator;
        _graphProvider = graphProvider;
        _repository = repository;
        _paths = paths;
        _logger = logger;
    }

    public async Task<NavigationDataStatus> BuildAsync(GameType game, CancellationToken cancellationToken = default)
    {
        var installation = _installationLocator.FindInstallations().FirstOrDefault(item => item.Game == game);
        if (installation is null)
        {
            return Missing(game, "Game installation not found. Set a manual game path before building navigation data.");
        }

        var processName = game == GameType.EuroTruckSimulator2 ? "eurotrucks2" : "amtrucks";
        if (Process.GetProcessesByName(processName).Length > 0)
        {
            return Missing(game, "Close the game before reading its installed navigation data.");
        }

        var toolRoot = FindExternalToolRoot();
        if (toolRoot is null)
        {
            return Missing(
                game,
                "The external truckermudgeon/maps generator is not configured. Set TRUCKNAV_MAPS_TOOL to its local clone.");
        }

        var parserDirectory = _paths.GetParserDirectory(game);
        var generatedDirectory = _paths.GetGeneratedDirectory(game);
        Directory.CreateDirectory(parserDirectory);
        Directory.CreateDirectory(generatedDirectory);
        Directory.CreateDirectory(_paths.GetGameRoot(game));
        var logPath = Path.Combine(_paths.GetGameRoot(game), "generation.log");

        var npm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "npm.cmd");
        var npx = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "npx.cmd");
        if (!File.Exists(npm) || !File.Exists(npx))
        {
            return Missing(game, "Node.js and npm are required to run the external local map generator.");
        }

        try
        {
            if (!Directory.Exists(Path.Combine(toolRoot, "node_modules")))
            {
                await RunAsync(npm, "install", toolRoot, logPath, cancellationToken);
            }

            var nativeParser = Path.Combine(toolRoot, "packages", "clis", "parser", "build", "Release", "gdeflate.node");
            if (!File.Exists(nativeParser))
            {
                await RunAsync(npm, "run build -w packages/clis/parser", toolRoot, logPath, cancellationToken);
            }

            await RunAsync(
                npx,
                $"tsx packages/clis/parser/index.ts -i \"{installation.InstallPath}\" -o \"{parserDirectory}\"",
                toolRoot,
                logPath,
                cancellationToken);
            await RunAsync(
                npx,
                $"tsx packages/clis/generator/index.ts graph -m {NavigationStoragePaths.GetMapCode(game)} -i \"{parserDirectory}\" -o \"{generatedDirectory}\"",
                toolRoot,
                logPath,
                cancellationToken);

            _graphProvider.Invalidate(game);
            var graph = await _graphProvider.GetGraphAsync(game, cancellationToken);
            return graph is null
                ? Missing(game, "Generation completed but TruckNav could not import the resulting graph.")
                : await _repository.GetStatusAsync(game, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            _logger.LogError(exception, "Navigation data generation failed for {Game}.", game);
            return Missing(game, $"Navigation generation failed. See {logPath}. {exception.Message}");
        }
    }

    public Task<NavigationDataStatus> GetStatusAsync(GameType game, CancellationToken cancellationToken = default) =>
        _repository.GetStatusAsync(game, cancellationToken);

    private NavigationDataStatus Missing(GameType game, string message) => new(
        game,
        false,
        _paths.GetGameRoot(game),
        null,
        0,
        0,
        null,
        message);

    private static string? FindExternalToolRoot()
    {
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("TRUCKNAV_MAPS_TOOL"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "trucknav",
                "research",
                "maps")
        };

        return candidates.FirstOrDefault(path =>
            !string.IsNullOrWhiteSpace(path) &&
            File.Exists(Path.Combine(path, "packages", "clis", "parser", "index.ts")) &&
            File.Exists(Path.Combine(path, "packages", "clis", "generator", "index.ts")));
    }

    private static async Task RunAsync(
        string executable,
        string arguments,
        string workingDirectory,
        string logPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(executable, arguments)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.Environment["NODE_OPTIONS"] = "--max-old-space-size=8192";

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException($"Unable to start {Path.GetFileName(executable)}.");
        await using var log = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(log) { AutoFlush = true };
        var stdout = PumpAsync(process.StandardOutput, writer, cancellationToken);
        var stderr = PumpAsync(process.StandardError, writer, cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(stdout, stderr);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{Path.GetFileName(executable)} exited with code {process.ExitCode}.");
        }
    }

    private static async Task PumpAsync(
        StreamReader reader,
        StreamWriter writer,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
    }
}
