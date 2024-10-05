using Swoq.Infra;

namespace Swoq.InfraUI.Models;

public record PlayerObservation(string LastAction, int Health, string Inventory, bool HasSword, TileMap Surroundings);

public record GameObservation(string UserName, int Tick, int Level, string Status, string ActionResult, TileMap Overview, PlayerObservation? Player1 = null, PlayerObservation? Player2 = null);
