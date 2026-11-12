using System.Globalization;
using SongStash.Desktop.ViewModels;
using Xunit;

namespace SongStash.Desktop.Tests;

public sealed class UserDateTimeFormatterTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("ru-RU")]
    public void UsesFixedDayMonthYearAnd24HourFormatRegardlessOfUiCulture(string cultureName)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            var localDateTime = new DateTime(2026, 11, 7, 20, 26, 0, DateTimeKind.Unspecified);
            var localOffset = TimeZoneInfo.Local.GetUtcOffset(localDateTime);
            var value = new DateTimeOffset(localDateTime, localOffset);

            Assert.Equal("07/11/2026 20:26", UserDateTimeFormatter.Format(value));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}
