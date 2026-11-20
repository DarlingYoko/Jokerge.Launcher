using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Gml.Launcher.Models;

namespace Gml.Launcher.Views.Components;

public class ProfileUserControl : ItemsControl
{
    public static readonly StyledProperty<int> SpacingProperty = AvaloniaProperty.Register<StackFrameBorder, int>(
        nameof(Spacing));

    public static readonly StyledProperty<IEnumerable<ProfileInfoItem>> ProfileInfoItemsProperty =
        AvaloniaProperty.Register<ProfileUserControl, IEnumerable<ProfileInfoItem>>(
            nameof(ProfileInfoItems));

    public static readonly StyledProperty<string?> SkinUrlProperty =
        AvaloniaProperty.Register<ProfileUserControl, string?>(nameof(SkinUrl));

    public static readonly StyledProperty<string?> CapeUrlProperty =
        AvaloniaProperty.Register<ProfileUserControl, string?>(nameof(CapeUrl));

    public static readonly StyledProperty<string?> ViewerHintProperty =
        AvaloniaProperty.Register<ProfileUserControl, string?>(nameof(ViewerHint));

    public static readonly StyledProperty<string> UserNameProperty =
        AvaloniaProperty.Register<ProfileUserControl, string>(
            nameof(UserName), "GamerVII");

    public string UserName
    {
        get => GetValue(UserNameProperty);
        set => SetValue(UserNameProperty, value);
    }

    public string? SkinUrl
    {
        get => GetValue(SkinUrlProperty);
        set => SetValue(SkinUrlProperty, value);
    }

    public string? CapeUrl
    {
        get => GetValue(CapeUrlProperty);
        set => SetValue(CapeUrlProperty, value);
    }

    /// <summary>Tooltip of the 3D model, e.g. "drag to rotate".</summary>
    public string? ViewerHint
    {
        get => GetValue(ViewerHintProperty);
        set => SetValue(ViewerHintProperty, value);
    }

    public IEnumerable<ProfileInfoItem> ProfileInfoItems
    {
        get => GetValue(ProfileInfoItemsProperty);
        set => SetValue(ProfileInfoItemsProperty, value);
    }

    public int Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }
}
