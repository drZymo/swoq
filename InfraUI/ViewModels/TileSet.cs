using System.Collections.Immutable;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

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
    public static IImmutableDictionary<Tile, byte[]> FromImageFile(string path)
    {
        BitmapFrame atlasImage;
        using (var file = File.OpenRead(path))
        {
            var decoder = PngBitmapDecoder.Create(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            atlasImage = decoder.Frames[0];
            atlasImage.Freeze();
        }

        byte[] GetTile(int row, int column)
        {
            byte[] pixels = new byte[16 * 16 * 4];
            atlasImage.CopyPixels(new Int32Rect(column * 16, row * 16, 16, 16), pixels, 16 * 4, 0);
            return pixels;
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
