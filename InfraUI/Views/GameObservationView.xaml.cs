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

    public static readonly StyledProperty<bool> ShowSidePanelsProperty =
        AvaloniaProperty.Register<GroupBox, bool>(nameof(ShowSidePanels), defaultValue: true);

    public bool ShowSidePanels
    {
        get { return GetValue(ShowSidePanelsProperty); }
        set { SetValue(ShowSidePanelsProperty, value); }
    }
}
