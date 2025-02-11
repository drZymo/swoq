namespace Swoq.Infra;

public static class MapVisibility
{
    public static bool IsVisible(this Map map, Position from, Position to, int maxRange)
    {
        if (from.Equals(to)) return true;
        if (to.y < 0 || to.y >= map.Height) return false;
        if (to.x < 0 || to.x >= map.Width) return false;
        if (to.DistanceTo(from) > maxRange) return false;

        if (from.x < to.x && IsVisible(map, from.x + 0.5, from.y + 0.5, to.x, to.y + 0.5)) return true;
        if (from.x > to.x && IsVisible(map, from.x + 0.5, from.y + 0.5, to.x + 1, to.y + 0.5)) return true;
        if (from.y < to.y && IsVisible(map, from.x + 0.5, from.y + 0.5, to.x + 0.5, to.y)) return true;
        if (from.y > to.y && IsVisible(map, from.x + 0.5, from.y + 0.5, to.x + 0.5, to.y + 1)) return true;

        return false;
    }

    private static bool IsVisible(Map map, double srcX, double srcY, double dstX, double dstY)
    {
        var dx = dstX - srcX;
        var dy = dstY - srcY;

        if (Math.Abs(dx) > 1e-6) // prevent division by small amount
        {
            var stepX = Math.Sign(dx);
            var x = stepX > 0 ? Math.Ceiling(srcX) : Math.Floor(srcX);
            var stepY = stepX * dy / dx;
            var y = srcY + (x - srcX) * dy / dx;

            while (!(srcX < dstX && x >= dstX) && !(srcX > dstX && x <= dstX))
            {
                var mapX = (int)(x + stepX * 0.5);
                var mapY = (int)y;
                if (mapX < 0 || mapX >= map.Width || mapY < 0 || mapY >= map.Height) break;
                if (!map[mapY, mapX].CanWalkOn()) return false; // Blocked by wall

                x += stepX;
                y += stepY;
            }
        }

        if (Math.Abs(dy) > 1e-6) // prevent division by small amount
        {
            var stepY = Math.Sign(dy);
            var y = stepY > 0 ? Math.Ceiling(srcY) : Math.Floor(srcY);
            var stepX = stepY * dx / dy;
            var x = srcX + (y - srcY) * dx / dy;

            while (!(srcY < dstY && y >= dstY) && !(srcY > dstY && y <= dstY))
            {
                var mapY = (int)(y + stepY * 0.5);
                var mapX = (int)x;
                if (mapX < 0 || mapX >= map.Width || mapY < 0 || mapY >= map.Height) break;
                if (!map[mapY, mapX].CanWalkOn()) return false; // Blocked by wall

                y += stepY;
                x += stepX;
            }
        }

        return true;
    }
}
