using System.Globalization;
using Gml.Launcher.Core.Stats;

namespace Gml.Launcher.Tests.Stats;

public class StatsFormatterTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 20, 0, 0);

    [TestCase(0, 0, "0 мин")]
    [TestCase(0, 45, "45 мин")]
    [TestCase(1, 0, "1 ч")]
    [TestCase(12, 5, "12 ч 5 мин")]
    [TestCase(130, 59, "130 ч 59 мин")]
    public void Duration_ShowsHoursAndMinutes(int hours, int minutes, string expected)
    {
        var value = new TimeSpan(hours, minutes, 42);

        Assert.That(StatsFormatter.Duration(value, "ч", "мин"), Is.EqualTo(expected));
    }

    [Test]
    public void Moment_Today_SaysToday()
    {
        Assert.That(Moment(new DateTime(2026, 11, 20, 17, 7, 0)), Is.EqualTo("today, 17:07"));
    }

    [Test]
    public void Moment_Yesterday_SaysYesterday()
    {
        Assert.That(Moment(new DateTime(2026, 11, 19, 19, 51, 0)), Is.EqualTo("yesterday, 19:51"));
    }

    [Test]
    public void Moment_ThisYear_DropsTheYear()
    {
        Assert.That(Moment(new DateTime(2026, 11, 1, 9, 5, 0)), Is.EqualTo("11/01, 09:05"));
    }

    [Test]
    public void Moment_OtherYear_KeepsTheYear()
    {
        Assert.That(Moment(new DateTime(2025, 12, 31, 23, 0, 0)), Is.EqualTo("12/31/2025 23:00"));
    }

    [Test]
    public void Moment_DayMonthPattern_FollowsTheCulture()
    {
        var russian = CultureInfo.GetCultureInfo("ru-RU");

        var text = StatsFormatter.Moment(new DateTime(2026, 11, 1, 9, 5, 0), Now, russian, "сегодня", "вчера");

        Assert.That(text, Does.StartWith("01.11, "));
    }

    private static string Moment(DateTime value) =>
        StatsFormatter.Moment(value, Now, CultureInfo.InvariantCulture, "today", "yesterday");
}
