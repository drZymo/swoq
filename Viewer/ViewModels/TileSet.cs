using Swoq.Infra;
using System.Collections.Immutable;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MapGeneratorTester.ViewModels;

internal record TileSet(IImmutableDictionary<Cell, byte[]> Cells, byte[] Player, byte[] Enemy)
{
    public static TileSet FromImageFile(string path)
    {
        BitmapFrame tileset;
        using (var file = File.OpenRead(path))
        {
            var decoder = PngBitmapDecoder.Create(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            tileset = decoder.Frames[0];
            tileset.Freeze();
        }

        byte[] GetTile(int row, int column)
        {
            byte[] pixels = new byte[16 * 16 * 4];
            tileset.CopyPixels(new Int32Rect(column * 16, row * 16, 16, 16), pixels, 16 * 4, 0);
            return pixels;
        }

        var cells = ImmutableDictionary<Cell, byte[]>.Empty;
        cells = cells.Add(Cell.Wall, GetTile(0, 0));
        cells = cells.Add(Cell.Empty, GetTile(0, 1));
        var player = GetTile(0, 2);
        var enemy = GetTile(0, 3);
        cells = cells.Add(Cell.Exit, GetTile(0, 4));
        cells = cells.Add(Cell.KeyRed, GetTile(1, 0));
        cells = cells.Add(Cell.KeyGreen, GetTile(1, 1));
        cells = cells.Add(Cell.KeyBlue, GetTile(1, 2));
        cells = cells.Add(Cell.PressurePlate, GetTile(1, 3));
        cells = cells.Add(Cell.Sword, GetTile(1, 4));
        cells = cells.Add(Cell.DoorRedClosed, GetTile(2, 0));
        cells = cells.Add(Cell.DoorGreenClosed, GetTile(2, 1));
        cells = cells.Add(Cell.DoorBlueClosed, GetTile(2, 2));
        cells = cells.Add(Cell.DoorBlackClosed, GetTile(2, 3));
        cells = cells.Add(Cell.Health, GetTile(2, 4));
        return new TileSet(cells, player, enemy);
    }
}
