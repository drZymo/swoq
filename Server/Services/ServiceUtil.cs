using Swoq.Interface;

namespace Swoq.Server.Services;

internal static class ServiceUtil
{
    public static State Convert(this GameState gameState)
    {
        return new State
        {
            Tick = gameState.Tick,
            Level = gameState.Level,
            Status = gameState.Status,
            PlayerState = gameState.Player1?.Convert(),
            Player2State = gameState.Player2?.Convert(),
        };
    }

    public static Interface.PlayerState Convert(this PlayerState playerState)
    {
        var state = new Interface.PlayerState
        {
            Position = new Position { X = playerState.Position.x, Y = playerState.Position.y },
            Health = playerState.Health,
            Inventory = playerState.Inventory,
            HasSword = playerState.HasSword,
        };


        state.Surroundings.AddRange(playerState.Surroundings);
        return state;
    }
}
