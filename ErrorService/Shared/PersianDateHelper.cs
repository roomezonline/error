using System.Globalization;

namespace ErrorService.Shared;

public static class PersianDateHelper
{
    private static readonly PersianCalendar Calendar = new();

    public static string ToPersianDateTimeString(DateTimeOffset value, bool includeTime = true)
    {
        return ToPersianDateTimeString(value, includeTime, includeSeconds: true);
    }

    public static string ToPersianDateTimeString(DateTimeOffset value, bool includeTime, bool includeSeconds)
    {
        if (value.Ticks < 123600000000) return "-";
        try
        {
            var dt = value.ToLocalTime().DateTime;
            var y = Calendar.GetYear(dt);
            var m = Calendar.GetMonth(dt);
            var d = Calendar.GetDayOfMonth(dt);

            if (!includeTime)
                return $"{y:0000}/{m:00}/{d:00}";

            var timeFmt = includeSeconds ? $"{dt:HH:mm:ss}" : $"{dt:HH:mm}";
            return $"{y:0000}/{m:00}/{d:00} {timeFmt}";
        }
        catch (ArgumentOutOfRangeException)
        {
            return "-";
        }
    }

    public static DateTimeOffset FromPersianDate(int year, int month, int day)
    {
        var dt = Calendar.ToDateTime(year, month, day, 0, 0, 0, 0);
        var offset = TimeZoneInfo.Local.GetUtcOffset(dt);
        return new DateTimeOffset(dt, offset);
    }

    public static (int year, int month, int day) ToPersianYmd(DateTimeOffset value)
    {
        if (value.Ticks < 123600000000) return (1, 1, 1);
        try
        {
            var dt = value.ToLocalTime().DateTime;
            return (Calendar.GetYear(dt), Calendar.GetMonth(dt), Calendar.GetDayOfMonth(dt));
        }
        catch (ArgumentOutOfRangeException)
        {
            return (1, 1, 1);
        }
    }
}
