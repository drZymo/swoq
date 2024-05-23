using Avalonia;
using Avalonia.Media.Imaging;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Swoq.InfraUI.ViewModels;

public enum Tile
{
    Unknown,
    Wall,
    Empty,
    Player,
    Enemy,
    Exit,
    KeyRed,
    KeyGreen,
    KeyBlue,
    PressurePlate,
    Sword,
    DoorRed,
    DoorGreen,
    DoorBlue,
    DoorBlack,
    Health,
}

public static class TileSet
{
    // TODO: make tile size configurable, e.g. from bitmap size?
    public const int TileWidth = 16;
    public const int TileHeight = 16;

    private static (PixelSize size, byte[] pixels) LoadBitmapPixels(string path)
    {
        Bitmap bitmap = new(path);

        var pixels = new byte[bitmap.PixelSize.Height * bitmap.PixelSize.Width * 4];

        var pixelsHandle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        bitmap.CopyPixels(new PixelRect(PixelPoint.Origin, bitmap.PixelSize), pixelsHandle.AddrOfPinnedObject(), pixels.Length, bitmap.PixelSize.Width * 4);
        pixelsHandle.Free();

        return (bitmap.PixelSize, pixels);
    }

    public static IImmutableDictionary<Tile, byte[]> FromImageFile(string path)
    {
        var (bitmapSize, bitmapPixels) = LoadBitmapPixels(path);

        var tileStride = TileWidth * 4;

        byte[] GetTile(int row, int column)
        {
            byte[] tilePixels = new byte[TileHeight * TileWidth * 4];
            for (var tileY = 0; tileY < TileHeight; tileY++)
            {
                var bitmapX = column * TileWidth;
                var bitmapY = row * TileHeight + tileY;
                var bitmapIndex = (bitmapY * bitmapSize.Width + bitmapX) * 4;
                Array.Copy(bitmapPixels, bitmapIndex, tilePixels, tileY * tileStride, tileStride);
            }
            return tilePixels;
        }

        var tiles = ImmutableDictionary<Tile, byte[]>.Empty;
        tiles = tiles.Add(Tile.Unknown, new byte[16 * 16 * 4]);
        tiles = tiles.Add(Tile.Wall, GetTile(0, 0));
        tiles = tiles.Add(Tile.Empty, GetTile(0, 1));
        tiles = tiles.Add(Tile.Player, GetTile(0, 2));
        tiles = tiles.Add(Tile.Enemy, GetTile(0, 3));
        tiles = tiles.Add(Tile.Exit, GetTile(0, 4));
        tiles = tiles.Add(Tile.KeyRed, GetTile(1, 0));
        tiles = tiles.Add(Tile.KeyGreen, GetTile(1, 1));
        tiles = tiles.Add(Tile.KeyBlue, GetTile(1, 2));
        tiles = tiles.Add(Tile.PressurePlate, GetTile(1, 3));
        tiles = tiles.Add(Tile.Sword, GetTile(1, 4));
        tiles = tiles.Add(Tile.DoorRed, GetTile(2, 0));
        tiles = tiles.Add(Tile.DoorGreen, GetTile(2, 1));
        tiles = tiles.Add(Tile.DoorBlue, GetTile(2, 2));
        tiles = tiles.Add(Tile.DoorBlack, GetTile(2, 3));
        tiles = tiles.Add(Tile.Health, GetTile(2, 4));
        return tiles;
    }
}
