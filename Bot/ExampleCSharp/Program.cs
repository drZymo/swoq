using Swoq.Interface;

var userId = "<insert your user id here>";
int? level = 0; // null for quest, integer for train

using var player = new GamePlayer(userId);

var state = player.Start(level);
var moveEast = true;
while (!state.Finished)
{
    state = player.Act(moveEast ? DirectedAction.MoveEast : DirectedAction.MoveSouth);
    moveEast = !moveEast;
}
