using System.Globalization;

namespace ErrorService.Client.Utils;

public static class PersianDate
{
    private static readonly PersianCalendar _pc = new();

    public static string ToJalaliDateTime(DateTimeOffset value)
    {
        var local = value.ToLocalTime().DateTime;
        var y = _pc.GetYear(local);
        var m = _pc.GetMonth(local);
        var d = _pc.GetDayOfMonth(local);
        var hh = local.Hour;
        var mm = local.Minute;

        return $"{hh:00}:{mm:00} {y:0000}/{m:00}/{d:00}";
    }

    public static string ToJalaliDate(DateTimeOffset value)
    {
        var local = value.ToLocalTime().DateTime;
        var y = _pc.GetYear(local);
        var m = _pc.GetMonth(local);
        var d = _pc.GetDayOfMonth(local);

        return $"{y:0000}/{m:00}/{d:00}";
    }

    public static string ToLongPersianDate(DateTimeOffset value)
    {
        var local = value.ToLocalTime().DateTime;
        var y = _pc.GetYear(local);
        var m = _pc.GetMonth(local);
        var d = _pc.GetDayOfMonth(local);
        var dayOfWeek = _pc.GetDayOfWeek(local);

        string dayName = dayOfWeek switch
        {
            DayOfWeek.Saturday => "شنبه",
            DayOfWeek.Sunday => "یکشنبه",
            DayOfWeek.Monday => "دوشنبه",
            DayOfWeek.Tuesday => "سه‌شنبه",
            DayOfWeek.Wednesday => "چهارشنبه",
            DayOfWeek.Thursday => "پنجشنبه",
            DayOfWeek.Friday => "جمعه",
            _ => ""
        };

        string monthName = m switch
        {
            1 => "فروردین",
            2 => "اردیبهشت",
            3 => "خرداد",
            4 => "تیر",
            5 => "مرداد",
            6 => "شهریور",
            7 => "مهر",
            8 => "آبان",
            9 => "آذر",
            10 => "دی",
            11 => "بهمن",
            12 => "اسفند",
            _ => ""
        };

        return $"{dayName} {d} {monthName} ماه {y}";
    }

    public static string ToTime(DateTimeOffset value)
    {
        var local = value.ToLocalTime().DateTime;
        return $"{local.Hour:00}:{local.Minute:00}";
    }

    public static bool TryParseJalaliDateTime(string? input, out DateTimeOffset result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(input)) return false;

        input = input.Trim();
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        string datePart, timePart;
        if (parts.Length == 1)
        {
            datePart = parts[0];
            timePart = "00:00";
        }
        else if (parts.Length == 2)
        {
            // Accept both "yyyy/MM/dd HH:mm" and legacy "HH:mm yyyy/MM/dd" orders
            if (parts[0].Contains('/')) { datePart = parts[0]; timePart = parts[1]; }
            else { datePart = parts[1]; timePart = parts[0]; }
        }
        else
        {
            return false;
        }

        var dateBits = datePart.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (dateBits.Length != 3) return false;

        if (!int.TryParse(dateBits[0], out var jy)) return false;
        if (!int.TryParse(dateBits[1], out var jm)) return false;
        if (!int.TryParse(dateBits[2], out var jd)) return false;

        var timeBits = timePart.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (timeBits.Length < 2) return false;

        if (!int.TryParse(timeBits[0], out var hh)) return false;
        if (!int.TryParse(timeBits[1], out var mm)) return false;

        if (hh < 0 || hh > 23) return false;
        if (mm < 0 || mm > 59) return false;

        try
        {
            var dt = _pc.ToDateTime(jy, jm, jd, hh, mm, 0, 0);
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);
            result = new DateTimeOffset(dt);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
