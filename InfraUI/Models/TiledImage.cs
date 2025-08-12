using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Swoq.Interface;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Swoq.InfraUI.Models;

internal class TiledImage(int height, int width) : IDisposable
{
    private static readonly IImmutableDictionary<Tile, byte[]> tileSet;
    private static readonly int tileHeight;
    private static readonly int tileWidth;

    static TiledImage()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Swoq.InfraUI.tiles.png")
                           ?? throw new InvalidOperationException("Resource 'tiles.png' not found.");
        var bitmap = new Bitmap(stream);

        (tileSet, tileHeight, tileWidth) = TileSet.FromImageFile(bitmap);
    }

    private readonly Tile[,] tiles = new Tile[height, width];
    private readonly bool[,] visibility = new bool[height, width];
    private readonly WriteableBitmap image = new(new PixelSize(width * tileWidth, height * tileHeight), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Opaque);

    public int Height { get; } = height;
    public int Width { get; } = width;
    public IImage Image => image;

    public void Dispose()
    {
        image.Dispose();
    }

    public LockedMapImage Lock()
    {
        return new LockedMapImageImpl(this);
    }

    private class LockedMapImageImpl(TiledImage parent) : LockedMapImage
    {
        private readonly TiledImage parent = parent;
        private readonly ILockedFramebuffer framebuffer = parent.image.Lock();

        public override void Dispose()
        {
            framebuffer.Dispose();
        }

        public override void SetTile(int row, int col, Tile tile, bool isVisible)
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
            var bitmapX = col * tileWidth;
            var bitmapY = row * tileHeight;
            var bitmapOffset = (bitmapY * bitmapSize.Width + bitmapX) * 4;
            var bitmapStride = bitmapSize.Width * 4;

            var tileIndex = 0;
            var tileStride = tileWidth * 4;
            for (var tileY = 0; tileY < tileHeight; tileY++)
            {
                Marshal.Copy(tile, tileIndex, bitmapFrameBuffer.Address + bitmapOffset, tileStride);
                bitmapOffset += bitmapStride;
                tileIndex += tileStride;
            }
        }
    }

}
