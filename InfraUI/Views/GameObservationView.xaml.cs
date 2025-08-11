using Avalonia;
using Avalonia.Controls;

namespace Swoq.InfraUI.Views;

public partial class GameObservationView : UserControl
{
    public static readonly StyledProperty<bool> ShowHeaderProperty =
        AvaloniaProperty.Register<GroupBox, bool>(nameof(ShowHeader), defaultValue: true);

    public bool ShowHeader
    {
        get { return GetValue(ShowHeaderProperty); }
        set { SetValue(ShowHeaderProperty, value); }
    }
}
