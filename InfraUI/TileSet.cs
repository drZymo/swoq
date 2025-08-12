using Avalonia;
using Avalonia.Media.Imaging;
using Swoq.Interface;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Swoq.InfraUI;

public static class TileSet
{
    private const int NrRows = 4;
    private const int NrColumns = 6;

    private static (PixelSize size, byte[] pixels) LoadBitmapPixels(Bitmap bitmap)
    {
        var pixels = new byte[bitmap.PixelSize.Height * bitmap.PixelSize.Width * 4];

        var pixelsHandle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        bitmap.CopyPixels(new PixelRect(PixelPoint.Origin, bitmap.PixelSize), pixelsHandle.AddrOfPinnedObject(), pixels.Length, bitmap.PixelSize.Width * 4);
        pixelsHandle.Free();

        return (bitmap.PixelSize, pixels);
    }

    public static (IImmutableDictionary<Tile, byte[]> tiles, int tileHeight, int tileWidth) FromImageFile(Bitmap bitmap)
    {
        var (bitmapSize, bitmapPixels) = LoadBitmapPixels(bitmap);

        var tileWidth = bitmapSize.Width / NrColumns;
        var tileHeight = bitmapSize.Height / NrRows;
        var tileStride = tileWidth * 4; // RGBA

        byte[] GetTile(int row, int column)
        {
            byte[] tilePixels = new byte[tileHeight * tileWidth * 4];
            for (var tileY = 0; tileY < tileHeight; tileY++)
            {
                var bitmapX = column * tileWidth;
                var bitmapY = row * tileHeight + tileY;
                var bitmapIndex = (bitmapY * bitmapSize.Width + bitmapX) * 4;
                Array.Copy(bitmapPixels, bitmapIndex, tilePixels, tileY * tileStride, tileStride);
            }
            return tilePixels;
        }

        var tiles = ImmutableDictionary<Tile, byte[]>.Empty;
        tiles = tiles.Add(Tile.Unknown, new byte[tileHeight * tileWidth * 4]);
        tiles = tiles.Add(Tile.Wall, GetTile(0, 0));
        tiles = tiles.Add(Tile.Empty, GetTile(0, 1));
        tiles = tiles.Add(Tile.Player, GetTile(0, 2));
        tiles = tiles.Add(Tile.Enemy, GetTile(0, 3));
        tiles = tiles.Add(Tile.Exit, GetTile(0, 4));
        tiles = tiles.Add(Tile.KeyRed, GetTile(1, 0));
        tiles = tiles.Add(Tile.KeyGreen, GetTile(1, 1));
        tiles = tiles.Add(Tile.KeyBlue, GetTile(1, 2));
        tiles = tiles.Add(Tile.Sword, GetTile(1, 4));
        tiles = tiles.Add(Tile.DoorRed, GetTile(2, 0));
        tiles = tiles.Add(Tile.DoorGreen, GetTile(2, 1));
        tiles = tiles.Add(Tile.DoorBlue, GetTile(2, 2));
        tiles = tiles.Add(Tile.Health, GetTile(2, 4));
        tiles = tiles.Add(Tile.PressurePlateRed, GetTile(3, 0));
        tiles = tiles.Add(Tile.PressurePlateGreen, GetTile(3, 1));
        tiles = tiles.Add(Tile.PressurePlateBlue, GetTile(3, 2));
        tiles = tiles.Add(Tile.Boulder, GetTile(3, 4));
        tiles = tiles.Add(Tile.Boss, GetTile(0, 5));
        tiles = tiles.Add(Tile.Treasure, GetTile(1, 5));
        return (tiles, tileHeight, tileWidth);
    }
}
