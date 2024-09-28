namespace Swoq.Server;

public record GameStartResult(string UserName, Guid GameId, GameState State);
