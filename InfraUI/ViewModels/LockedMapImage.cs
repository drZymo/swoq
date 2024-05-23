namespace Swoq.InfraUI.ViewModels;

public abstract class LockedMapImage : IDisposable
{
    public abstract void Dispose();
    public abstract void Set(int y, int x, Tile tile, bool isVisible);
}
