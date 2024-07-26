using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Swoq.InfraUI.ViewModels;

internal class MapImage(int mapHeight, int mapWidth)
{
    private static readonly IImmutableDictionary<Tile, byte[]> tileSet;

    static MapImage()
    {
        var currentDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)
            ?? AppDomain.CurrentDomain.BaseDirectory;
        tileSet =TileSet.FromImageFile(Path.Combine(currentDir, "tiles.png"));
    }

    private readonly Tile[,] tiles = new Tile[mapHeight, mapWidth];
    private readonly bool[,] visibility = new bool[mapHeight, mapWidth];
    private readonly WriteableBitmap image = new(new PixelSize(mapWidth * TileSet.TileWidth, mapHeight * TileSet.TileHeight), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Opaque);

    public int MapHeight { get; } = mapHeight;
    public int MapWidth { get; } = mapWidth;
    public IImage Image => image;

    public LockedMapImage Lock()
    {
        return new LockedMapImageImpl(this);
    }

    private class LockedMapImageImpl(MapImage parent) : LockedMapImage
    {
        private readonly MapImage parent = parent;
        private readonly ILockedFramebuffer framebuffer = parent.image.Lock();

        public override void Dispose()
        {
            framebuffer.Dispose();
        }

        public override void Set(int row, int col, Tile tile, bool isVisible)
        {
            if (tile != parent.tiles[row, col] || isVisible != parent.visibility[row, col])
            {
                byte[] tileImage = tileSet[tile];
                if (!isVisible)
                {
                    var newTileImage = new byte[tileImage.Length];
                    for (var i = 0; i < tileImage.Length; i++)
                    {
                        newTileImage[i] = (byte)(tileImage[i] * 2 / 3);
                    }
                    tileImage = newTileImage;
                }

                CopyTileToBitmap(tileImage, row, col, framebuffer, parent.image.PixelSize);

                parent.tiles[row, col] = tile;
                parent.visibility[row, col] = isVisible;
            }
        }

        private static void CopyTileToBitmap(byte[] tile, int row, int col, ILockedFramebuffer bitmapFrameBuffer, PixelSize bitmapSize)
        {
            var bitmapX = col * TileSet.TileWidth;
            var bitmapY = row * TileSet.TileHeight;
            var bitmapOffset = (bitmapY * bitmapSize.Width + bitmapX) * 4;
            var bitmapStride = bitmapSize.Width * 4;

            var tileIndex = 0;
            var tileStride = TileSet.TileWidth * 4;
            for (var tileY = 0; tileY < TileSet.TileHeight; tileY++)
            {
                Marshal.Copy(tile, tileIndex, bitmapFrameBuffer.Address + bitmapOffset, tileStride);
                bitmapOffset += bitmapStride;
                tileIndex += tileStride;
            }
        }
    }

}
