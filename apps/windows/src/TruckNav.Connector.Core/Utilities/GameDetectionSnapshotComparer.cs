using TruckNav.Connector.Core.Models;

namespace TruckNav.Connector.Core.Utilities;

public static class GameDetectionSnapshotComparer
{
    public static bool AreEquivalent(
        GameDetectionSnapshot? left,
        GameDetectionSnapshot? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left.RunningGames.Count != right.RunningGames.Count)
        {
            return false;
        }

        var leftGames = left.RunningGames.OrderBy(game => game.GameType).ToArray();
        var rightGames = right.RunningGames.OrderBy(game => game.GameType).ToArray();

        for (var index = 0; index < leftGames.Length; index += 1)
        {
            var leftGame = leftGames[index];
            var rightGame = rightGames[index];

            if (leftGame.GameType != rightGame.GameType ||
                !string.Equals(leftGame.ProcessName, rightGame.ProcessName, StringComparison.OrdinalIgnoreCase) ||
                leftGame.ProcessId != rightGame.ProcessId ||
                leftGame.WindowHandle != rightGame.WindowHandle ||
                !string.Equals(leftGame.WindowTitle, rightGame.WindowTitle, StringComparison.Ordinal) ||
                leftGame.ProcessRunning != rightGame.ProcessRunning ||
                leftGame.WindowAvailable != rightGame.WindowAvailable)
            {
                return false;
            }
        }

        return true;
    }
}
