using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Gml.Dto.Servers;
using Gml.Launcher.Assets.Resources;

namespace Gml.Launcher.Core.Converters;

public class ServersLabelConverter : MarkupExtension, IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is List<ServerReadDto> { Count: 1 }
            ? Resources.OnServer
            : Resources.OnServers;
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
