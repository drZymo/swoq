using Avalonia.Controls;

namespace Swoq.ReplayViewer.Views;

public partial class ReplayView : UserControl
{
    private void UserControl_DataContextChanged(object sender, EventArgs e)
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
