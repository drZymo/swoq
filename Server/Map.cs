namespace Swoq.Server;

using Position = (int y, int x);

internal class Map
{
    private readonly Cell[,] data;

    public int Height { get; }
    public int Width { get; }

    public Position InitialPlayerPosition { get; }

    public Map(int height, int width)
    {
        Height = height;
        Width = width;

        // Create empty map
        data = new Cell[height, width];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                data[y, x] = Cell.Empty;
            }
        }

        // Add edges
        for (var x = 0; x < width; x++)
        {
            data[0, x] = Cell.Wall;
            data[height - 1, x] = Cell.Wall;
        }
        for (var y = 1; y < height - 1; y++)
        {
            data[y, 0] = Cell.Wall;
            data[y, width - 1] = Cell.Wall;
        }

        // Some obstacles
        data[3, 3] = Cell.Wall;
        data[4, 3] = Cell.Wall;
        data[5, 3] = Cell.Wall;
        data[3, 4] = Cell.Wall;

        // Exit bottom right (in the wall)
        data[height - 2, width - 1] = Cell.Exit;

        // box around exit with red door
        data[height - 4, width - 2] = Cell.Wall;
        data[height - 4, width - 3] = Cell.Wall;
        data[height - 4, width - 4] = Cell.Wall;
        data[height - 4, width - 5] = Cell.Wall;
        data[height - 3, width - 5] = Cell.DoorRedClosed;
        data[height - 2, width - 5] = Cell.Wall;

        // key
        data[4, 4] = Cell.KeyRed;

        // Start top left
        InitialPlayerPosition = (1, 1);
    }

    private Map(Cell[,] data, int height, int width, Position initialPlayerPosition)
    {
        this.data = data;
        Height = height;
        Width = width;
        InitialPlayerPosition = initialPlayerPosition;
    }

    public static Map LoadFromFile(string path)
    {
        byte[] buffer;
        using (var file = File.Open(path, FileMode.Open, FileAccess.Read))
        {
            buffer = new byte[2 + 256 * 256];
            var count = file.Read(buffer, 0, buffer.Length);
        }

        var height = (int)buffer[0];
        var width = (int)buffer[1];

        Position initialPlayerPos = (1, 1);
        var data = new Cell[height, width];

        var i = 2;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                Cell cell;
                if (buffer[i] == 2)
                {
                    initialPlayerPos = (y, x);
                    cell = Cell.Empty;
                }
                else
                {
                    cell = ToCell(buffer[i]);
                }

                data[y, x] = cell;
                i++;
            }
        }

        return new Map(data, height, width, initialPlayerPos);
    }

    public Cell this[int y, int x]
    {
        get => data[y, x];
        set { data[y, x] = value; }
    }

    public Cell this[Position pos]
    {
        get => data[pos.y, pos.x];
        set { data[pos.y, pos.x] = value; }
    }

    private static Cell ToCell(byte value) => value switch
    {
        //0 => Cell.UNKNOWN,
        1 => Cell.Empty,
        //2 => Cell.PLAYER,
        3 => Cell.Wall,
        4 => Cell.Exit,
        5 => Cell.DoorRedClosed,
        6 => Cell.KeyRed,
        7 => Cell.DoorGreenClosed,
        8 => Cell.KeyGreen,
        9 => Cell.DoorBlueClosed,
        10 => Cell.KeyBlue,
        _ => throw new NotImplementedException("Unknown cell type"),
    };
}
