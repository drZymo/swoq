using Swoq.Interface;

namespace Swoq.InfraUI.Models;

public abstract class LockedMapImage : IDisposable
{
    public abstract void Dispose();
    public abstract void Set(int y, int x, Tile tile, bool isVisible);
}
