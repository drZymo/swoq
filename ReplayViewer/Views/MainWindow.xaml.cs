using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Swoq.ReplayViewer.ViewModels;

namespace Swoq.ReplayViewer.Views;

public partial class MainWindow : Window
{
    protected override void OnInitialized()
    {
        AddHandler(DragDrop.DropEvent, OnDrop);
        base.OnInitialized();
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel main) return;

        var files = e.Data.GetFiles();
        if (files == null) return;

        var file = files.FirstOrDefault();
        if (file == null) return;

        var localPath = file.TryGetLocalPath();
        if (localPath == null) return;

        main.LoadFile(localPath);
    }
}
