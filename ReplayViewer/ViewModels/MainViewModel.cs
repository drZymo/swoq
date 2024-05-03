using Swoq.Interface;
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

        int i = 0;
        while (file.Position < file.Length)
        {
            var request = ActionRequest.Parser.ParseDelimitedFrom(file);
            var response = ActionResponse.Parser.ParseDelimitedFrom(file);

            mapBuilder.Hide();
            mapBuilder.AddPlayerState(response.State.Player1, 1);
            mapBuilder.AddPlayerState(response.State.Player2, 2);

            i++;
            if (i == 664) break;
        }

        var map = mapBuilder.CreateMap();

        Map = new MapViewModel(map);
    }

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
}
