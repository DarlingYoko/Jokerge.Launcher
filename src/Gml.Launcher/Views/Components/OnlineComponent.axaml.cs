using Avalonia;
using Avalonia.Controls.Primitives;

namespace Gml.Launcher.Views.Components;

public class OnlineComponent : TemplatedControl
{
    public static readonly StyledProperty<string> OnlineProperty = AvaloniaProperty.Register<OnlineComponent, string>(
        "Online", "0");

    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<OnlineComponent, string>(
        "Label", Assets.Resources.Resources.OnServers);

    public string Online
    {
        get => GetValue(OnlineProperty);
        set => SetValue(OnlineProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }
}
