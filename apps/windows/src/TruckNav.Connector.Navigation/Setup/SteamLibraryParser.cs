using System.Text.RegularExpressions;

namespace TruckNav.Connector.Navigation.Setup;

public static partial class SteamLibraryParser
{
    public static IReadOnlyList<string> ParseLibraryPaths(string contents, string? primarySteamPath = null)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(primarySteamPath))
        {
            paths.Add(Path.GetFullPath(primarySteamPath));
        }

        foreach (Match match in LibraryPathRegex().Matches(contents))
        {
            var value = match.Groups[1].Value.Replace("\\\\", "\\", StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(value))
            {
                paths.Add(Path.GetFullPath(value));
            }
        }

        return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string? ParseInstallDirectory(string contents)
    {
        var match = InstallDirectoryRegex().Match(contents);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    [GeneratedRegex("\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathRegex();

    [GeneratedRegex("\\\"installdir\\\"\\s+\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase)]
    private static partial Regex InstallDirectoryRegex();
}
