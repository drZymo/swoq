using Avalonia;
using Avalonia.Controls;

namespace Swoq.InfraUI.Views;

public partial class GroupBox : UserControl
{
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<GroupBox, string>(nameof(Header), defaultValue: "");

    public string Header
    {
        get { return GetValue(HeaderProperty); }
        set { SetValue(HeaderProperty, value); }
    }
}
