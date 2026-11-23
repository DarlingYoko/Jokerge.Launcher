using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Gml.Dto.Servers;
using Gml.Launcher.Assets.Resources;

namespace Gml.Launcher.Core.Converters;

/// <summary>
///     Always «На сервере»: several servers of a profile are routes to the same game server (see
///     <see cref="SumServersOnlineConverter" />).
/// </summary>
public class ServersLabelConverter : MarkupExtension, IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Resources.OnServer;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}
