using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using TheArtOfDev.HtmlRenderer.Avalonia;

namespace Gml.Launcher.Views.Components;

public class NewsComponent : TemplatedControl
{
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<NewsComponent, string>(
        nameof(Title), "Заголовок");

    public static readonly StyledProperty<string> DescriptionProperty = AvaloniaProperty.Register<NewsComponent, string>(
        nameof(Description), "<i>Описание</i>");

    public static readonly StyledProperty<DateTimeOffset?> DateProperty = AvaloniaProperty.Register<NewsComponent, DateTimeOffset?>(
        nameof(Date), DateTimeOffset.Now);

    private HtmlPanel? _htmlPanel;

    public DateTimeOffset? Date
    {
        get => GetValue(DateProperty);
        set => SetValue(DateProperty, value);
    }

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    // The text sits directly on the profile background, so it is light instead of the gray that
    // was meant for a dark card. The HTML renderer has no access to the app styles, hence inline.
    private static string Styled(string? html) =>
        $"<div style='color: #D9D9D9; font-size: 13px; line-height: 1.45;'>{html}</div>";

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _htmlPanel = e.NameScope.Find<HtmlPanel>("HtmlPanel");
        UpdateDescription();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DescriptionProperty)
            UpdateDescription();
    }

    private void UpdateDescription()
    {
        if (_htmlPanel is not null)
            _htmlPanel.Text = Styled(Description);
    }
}
