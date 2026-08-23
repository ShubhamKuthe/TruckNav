namespace TruckNav.Connector.Core.Models;

public sealed record GameDetectionSnapshot(IReadOnlyList<RunningGameInfo> RunningGames)
{
    public static GameDetectionSnapshot Empty { get; } = new(Array.Empty<RunningGameInfo>());

    public bool IsRunning(Enums.GameType gameType) =>
        RunningGames.Any(game => game.GameType == gameType && game.ProcessRunning);

    public RunningGameInfo? GetGame(Enums.GameType gameType) =>
        RunningGames.FirstOrDefault(game => game.GameType == gameType);

    public Enums.GameType SingleRunningGameOrNone =>
        RunningGames.Count == 1
            ? RunningGames[0].GameType
            : Enums.GameType.None;
}
