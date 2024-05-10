using Swoq.Infra;

namespace Swoq.InfraUI.Models;

public record PlayerState(string LastAction, int Health, string Inventory, bool HasSword);

public record GameState(string PlayerName, int Tick, int Level, string Status, Map Map, PlayerState? Player1 = null, PlayerState? Player2 = null);
