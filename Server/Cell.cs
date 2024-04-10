namespace Swoc2024Server;

internal abstract record Cell;

internal record EmptyCell(): Cell;
internal record FoodCell(int Value) : Cell;
internal record SnakeRefCell(Guid PlayerId, string SnakeName) : Cell;
