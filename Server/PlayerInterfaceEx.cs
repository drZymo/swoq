namespace PlayerInterface;

public static class PlayerInterfaceEx
{
    //public static UpdatedCell ToUpdatedCell(this Swoc2024Server.CellUpdate update)
    //{
    //    var updatedCell = new UpdatedCell();
    //    updatedCell.Address.AddRange(update.Position.Values);
    //    if (!string.IsNullOrEmpty(update.PlayerName)) updatedCell.Player = update.PlayerName;
    //    if (update.FoodValue.HasValue) updatedCell.FoodValue = update.FoodValue.Value;
    //    return updatedCell;
    //}

    //public static GameUpdateMessage ToGameUpdateMessage(this Swoc2024Server.GameUpdate update)
    //{
    //    var updatedCells = update.CellUpdates.Select(u => u.ToUpdatedCell());
    //    var playerScores = update.PlayerScores.Select(p => p.ToPlayerScore());

    //    var message = new GameUpdateMessage();
    //    // TODO: removed snakes
    //    message.UpdatedCells.Add(updatedCells);
    //    message.PlayerScores.Add(playerScores);
    //    return message;
    //}

    //public static GameSettings ToGameSettings(this Swoc2024Server.GameSettings settings)
    //{
    //    var gameSettings = new GameSettings();
    //    gameSettings.PlayerIdentifier = settings.PlayerId.ToString();
    //    gameSettings.Dimensions.AddRange(settings.Dimensions);
    //    gameSettings.StartAddress.AddRange(settings.StartPosition.Values);
    //    gameSettings.GameStarted = true; // TODO
    //    return gameSettings;
    //}

    //public static PlayerScore ToPlayerScore(this Swoc2024Server.PlayerScore score)
    //{
    //    var playerScore = new PlayerScore();
    //    playerScore.PlayerName = score.PlayerName;
    //    playerScore.Snakes = score.NrSnakes;
    //    playerScore.Score = score.Score;
    //    return playerScore;
    //}
}
