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
