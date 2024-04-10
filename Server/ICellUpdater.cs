namespace Swoc2024Server;

internal interface ICellUpdater
{
    void Clear(Position position);
    void SetFood(Position position, int foodValue);
    void SetSnake(Position position, Snake snake);
}
