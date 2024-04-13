namespace Viewer.ViewModels;

class MainViewModel : ViewModelBase, IDisposable
{
    private static readonly Color EmptyColor = Colors.Black;
    private static readonly Color FoodColor = Colors.White;

    private static readonly Color[] PlayerColors = [Colors.Blue, Colors.Red, Colors.Yellow, Colors.Purple, Colors.Orange, Colors.Green];

    private GrpcChannel channel;
    private PlayerHost.PlayerHostClient client;

    public MainViewModel()
    {
        Cells = new ReadOnlyObservableCollection<CellViewModel>(cells);

        channel = GrpcChannel.ForAddress("https://localhost:7262");

        client = new PlayerHost.PlayerHostClient(channel);

        Dispatcher.CurrentDispatcher.BeginInvoke(UpdateState);
    }

    public void Dispose()
    {
        channel.Dispose();
    }

    private readonly ObservableCollection<CellViewModel> cells = new();
    public ReadOnlyObservableCollection<CellViewModel> Cells { get; private set; }

    private IImmutableDictionary<string, Color> playerColors = ImmutableDictionary<string, Color>.Empty;

    private int width = 0;
    public int Width
    {
        get => width;
        private set
        {
            if (width != value)
            {
                width = value;
                OnPropertyChanged();
            }
        }
    }
    private int height = 0;
    public int Height
    {
        get => height;
        private set
        {
            if (height != value)
            {
                height = value;
                OnPropertyChanged();
            }
        }
    }

    private void UpdateState()
    {
        try
        {
            var state = client.GetGameState(new EmptyRequest());

            var maxY = state.UpdatedCells.Max(u => u.Address[0]);
            var maxX = state.UpdatedCells.Max(u => u.Address[1]);
            Width = (maxX + 1) * 10;
            Height = (maxY + 1) * 10;

            cells.Clear();
            playerColors.Clear();

            foreach (var update in state.UpdatedCells)
            {
                var y = update.Address[0] * 10;
                var x = update.Address[1] * 10;
                var color = EmptyColor;
                if (!string.IsNullOrWhiteSpace(update.Player))
                {
                    if (!playerColors.TryGetValue(update.Player, out color))
                    {
                        var colorIndex = playerColors.Count % PlayerColors.Length;
                        color = PlayerColors[colorIndex];
                        playerColors = playerColors.Add(update.Player, color);
                    }
                }
                else if (update.FoodValue > 0)
                {
                    color = FoodColor;
                }
                var cell = new CellViewModel(update.Address.ToArray(), x, y, color);
                cells.Add(cell);
            }

            Task.Run(Update);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Update state exception: {ex}");
        }
    }

    private async void Update()
    {
        try
        {
            var cancellationToken = new CancellationToken();

            using var subscribe = client.Subscribe(new SubsribeRequest { PlayerIdentifier = Guid.NewGuid().ToString() });
            while (await subscribe.ResponseStream.MoveNext(cancellationToken))
            {
                var response = subscribe.ResponseStream.Current;

                foreach (var update in response.UpdatedCells)
                {
                    var address = update.Address.ToArray();
                    var cell = cells.FirstOrDefault(c => c.Address.SequenceEqual(address));
                    if (cell != null)
                    {
                        var color = EmptyColor;
                        if (!string.IsNullOrWhiteSpace(update.Player))
                        {
                            if (!playerColors.TryGetValue(update.Player, out color))
                            {
                                var colorIndex = playerColors.Count % PlayerColors.Length;
                                color = PlayerColors[colorIndex];
                                playerColors = playerColors.Add(update.Player, color);
                            }
                        }
                        else if (update.FoodValue > 0)
                        {
                            color = FoodColor;
                        }
                        cell.Color = color;
                        Debug.WriteLine($"{address[0]} {address[1]} changed to {color}");
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Just stop
        }
    }
}
