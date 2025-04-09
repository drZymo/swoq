import { DirectedAction, Position } from "./generated/swoq";

export function getDirection(
    fromPosition: Position,
    toPosition: Position
): DirectedAction {
    const dx = toPosition.x - fromPosition.x;
    const dy = toPosition.y - fromPosition.y;
    if (dx > 0) {
        return DirectedAction.MOVE_EAST;
    } else if (dx < 0) {
        return DirectedAction.MOVE_WEST;
    } else if (dy > 0) {
        return DirectedAction.MOVE_SOUTH;
    } else if (dy < 0) {
        return DirectedAction.MOVE_NORTH;
    } else {
        return DirectedAction.NONE;
    }
}

export function targetPosition(
    fromPosition: Position,
    action: DirectedAction
): Position {
    let delta;
    switch (action) {
        case DirectedAction.NONE:
            return fromPosition;
        case DirectedAction.MOVE_EAST:
        case DirectedAction.USE_EAST:
            delta = [1, 0];
            break;
        case DirectedAction.MOVE_WEST:
        case DirectedAction.USE_WEST:
            delta = [-1, 0];
            break;
        case DirectedAction.MOVE_SOUTH:
        case DirectedAction.USE_SOUTH:
            delta = [0, 1];
            break;
        case DirectedAction.MOVE_NORTH:
        case DirectedAction.USE_NORTH:
            delta = [0, -1];
            break;
    }
    return { x: fromPosition.x + delta[0], y: fromPosition.y + delta[1] };
}

export type MoveAction =
    | DirectedAction.MOVE_EAST
    | DirectedAction.MOVE_WEST
    | DirectedAction.MOVE_NORTH
    | DirectedAction.MOVE_SOUTH;

export type UseAction =
    | DirectedAction.USE_EAST
    | DirectedAction.USE_WEST
    | DirectedAction.USE_NORTH
    | DirectedAction.USE_SOUTH;

export function isMoveAction(
    action: DirectedAction | undefined
): action is MoveAction {
    switch (action) {
        case DirectedAction.MOVE_EAST:
        case DirectedAction.MOVE_WEST:
        case DirectedAction.MOVE_NORTH:
        case DirectedAction.MOVE_SOUTH:
            return true;
        default:
            return false;
    }
}

export function isUseAction(
    action: DirectedAction | undefined
): action is UseAction {
    switch (action) {
        case DirectedAction.USE_EAST:
        case DirectedAction.USE_WEST:
        case DirectedAction.USE_NORTH:
        case DirectedAction.USE_SOUTH:
            return true;
        default:
            return false;
    }
}

export function getPathDirection(path: Position[]): DirectedAction | undefined {
    if (path.length < 2) {
        return undefined;
    }
    return getDirection(path[0], path[1]);
}

export function moveToUse(move: DirectedAction): DirectedAction {
    switch (move) {
        case DirectedAction.MOVE_EAST:
            return DirectedAction.USE_EAST;
        case DirectedAction.MOVE_WEST:
            return DirectedAction.USE_WEST;
        case DirectedAction.MOVE_NORTH:
            return DirectedAction.USE_NORTH;
        case DirectedAction.MOVE_SOUTH:
            return DirectedAction.USE_SOUTH;
        default:
            throw new Error(
                `can't convert Action ${DirectedAction[move]} to Use`
            );
    }
}

export function posToString(pos: Position): string {
    return `[${pos.x},${pos.y}]`;
}

export function samePos(pos1: Position, pos2: Position): boolean {
    return pos1.x === pos2.x && pos1.y === pos2.y;
}

export function hasPosition(list: Position[], pos: Position): boolean {
    return list.some((p) => p.x === pos.x && p.y === pos.y);
}

export function addPosition(
    list: Position[] | undefined,
    pos: Position
): Position[] {
    if (!list) {
        return [pos];
    }
    if (hasPosition(list, pos)) {
        return list;
    }
    return [...list, pos];
}

export function removePosition(
    list: Position[] | undefined,
    pos: Position
): Position[] | undefined {
    if (!list) {
        return undefined;
    }
    const idx = list.findIndex((p) => p.x === pos.x && p.y === pos.y);
    if (idx < 0) {
        return list;
    }
    if (list.length === 1) {
        return undefined;
    }
    return [...list.slice(0, idx), ...list.slice(idx + 1)];
}
