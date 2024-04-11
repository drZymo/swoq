//using System.Collections.Immutable;

//namespace Swoc2024Server;

//internal class Snake
//{
//    private readonly ICellUpdater cellUpdater;

//    private IImmutableList<Position> positions = ImmutableList<Position>.Empty;

//    public Snake(string name, Guid playerId, Position initialPosition, ICellUpdater cellUpdater)
//    {
//        Name = name;
//        PlayerId = playerId;
//        this.cellUpdater = cellUpdater;

//        positions = positions.Add(initialPosition);
//    }

//    public string Name { get; }

//    public Guid PlayerId { get; }

//    public Position HeadPosition => positions.Last();

//    public int Length => positions.Count;

//    public void Move(int axis, int dir)
//    {
//        Move(axis, dir, true);
//    }

//    public void Grow(int axis, int dir)
//    {
//        Move(axis, dir, false);
//    }

//    private void Move(int axis, int dir, bool clear)
//    {
//        var head = positions.Last();
//        var tail = positions.First();

//        if (axis < 0 && axis >= head.Values.Count) throw new ArgumentOutOfRangeException(nameof(axis));
//        if (dir != 1 && dir != -1) throw new ArgumentOutOfRangeException(nameof(dir));

//        var newValues = head.Values.SetItem(axis, head.Values[axis] + dir);
//        var newHead = new Position(newValues);

//        positions = positions.Add(newHead);
//        cellUpdater.SetSnake(newHead, this);
//        if (clear)
//        {
//            positions = positions.RemoveAt(0);
//            cellUpdater.Clear(tail);
//        }
//    }
//}
