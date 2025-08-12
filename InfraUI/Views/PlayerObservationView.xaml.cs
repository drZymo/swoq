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

    protected override Size MeasureOverride(Size availableSize)
    {
        // Make sure the size is square
        var minSize = Math.Min(availableSize.Width, availableSize.Height);
        var size = new Size(minSize, minSize);
        return base.MeasureOverride(size);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Make sure the size is square
        var minSize = Math.Min(finalSize.Width, finalSize.Height);
        var size = new Size(minSize, minSize);
        return base.ArrangeOverride(size);
    }
}
