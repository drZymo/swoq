using Swoq.Infra;
using Swoq.Interface;
using System.Collections.Immutable;
using System.IO;

namespace ReplayViewer.ViewModels;

class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        using var file = File.OpenRead(@"D:\Projects\swoq-stuff\Replays\Ralph\ff0eaad9-2e05-49fa-8bea-47cf7f602a9f.bin");

        var startRequest = StartTrainingRequest.Parser.ParseDelimitedFrom(file);
        var startResponse = StartResponse.Parser.ParseDelimitedFrom(file);

        var height = startResponse.Height;
        var width = startResponse.Width;
        var visibilityRange = startResponse.VisibilityRange;

        var mapBuilder = new MapBuilder(height, width, visibilityRange);

        var state = startResponse.State;
        mapBuilder.AddPlayerState(state.Player1, 1);
        mapBuilder.AddPlayerState(state.Player2, 2);

        var startMap = mapBuilder.CreateMap();
        maps = maps.Add(startMap);

        while (file.Position < file.Length)
        {
            var request = ActionRequest.Parser.ParseDelimitedFrom(file);
            var response = ActionResponse.Parser.ParseDelimitedFrom(file);

            mapBuilder.Hide();
            mapBuilder.AddPlayerState(response.State.Player1, 1);
            mapBuilder.AddPlayerState(response.State.Player2, 2);

            var map = mapBuilder.CreateMap();
            maps = maps.Add(map);
        }

        UpdateMap();
    }

    private readonly IImmutableList<Map> maps = ImmutableList<Map>.Empty;

    private int tick = 0;
    public int Tick
    {
        get => tick;
        set
        {
            if (tick != value)
            {
                tick = Math.Clamp(value, 0, MaxTick);
                OnPropertyChanged();
                UpdateMap();
            }
        }
    }

    public int MaxTick => maps.Count - 1;

    private MapViewModel map = new();
    public MapViewModel Map
    {
        get => map;
        private set
        {
            map = value;
            OnPropertyChanged();
        }
    }

    private void UpdateMap()
    {
        var map = maps[tick];
        var vm = new MapViewModel(map);
        Map = vm;
    }
}
