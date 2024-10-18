using Swoq.Infra;
using Swoq.Interface;

namespace Swoq.InfraUI.Models;

public record PlayerObservation(
    string LastAction,
    int Health,
    Inventory Inventory,
    bool HasSword,
    TileMap Surroundings);

public record GameObservation(
    string UserName,
    int Tick,
    int Level,
    string ActionResult,
    bool HasEnemies,
    bool HasSwordPickup,
    TileMap Overview,
    PlayerObservation? Player1 = null,
    PlayerObservation? Player2 = null);
