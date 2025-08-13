using Avalonia.Controls;
using Swoq.ReplayViewer.ViewModels;

namespace Swoq.ReplayViewer.Views;

public partial class ReplayView : UserControl
{
    private void UserControl_DataContextChanged(object sender, EventArgs e)
    {
        if (DataContext is ReplayViewModel vm)
        {
            vm.Loaded -= OnReplayLoaded;
            vm.Loaded += OnReplayLoaded;
        }
    }

    private void OnReplayLoaded()
    {
        try
        {
            var slider = this.GetControl<Slider>("slider");
            slider.Focus();
        }
        catch (ArgumentException)
        { }
    }
}
