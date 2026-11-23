using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Gml.Dto.Servers;

namespace Gml.Launcher.Core.Converters;

/// <summary>
///     Players online on the profile. A profile lists one server per route (direct domain, bridges) and they all
///     report the same players, so this is the highest count, not the sum.
/// </summary>
public class SumServersOnlineConverter : MarkupExtension, IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is List<ServerReadDto> server)
            return server
                .Where(c => c.IsOnline)
                .Max(c => c.Online)?
                .ToString() ?? "0";

        return "0";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return "0";
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}
