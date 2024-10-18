using Avalonia;
using Avalonia.Controls;

namespace Swoq.InfraUI.Views;

public partial class PlayerObservationView : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<GroupBox, string>(nameof(Title), defaultValue: "");

    public string Title
    {
        get { return GetValue(TitleProperty); }
        set { SetValue(TitleProperty, value); }
    }
}
