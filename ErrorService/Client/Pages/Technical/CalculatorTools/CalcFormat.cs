using System.Globalization;

namespace ErrorService.Client.Pages.Technical.CalculatorTools;

public static class CalcFormat
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static readonly double[] E24 =
    {
        1.0, 1.1, 1.2, 1.3, 1.5, 1.6, 1.8, 2.0, 2.2, 2.4, 2.7, 3.0,
        3.3, 3.6, 3.9, 4.3, 4.7, 5.1, 5.6, 6.2, 6.8, 7.5, 8.2, 9.1
    };

    public static readonly double[] StandardCapacitors =
    {
        2.2, 3.3, 4.7, 6.8, 10, 15, 22, 33, 47, 68, 100, 120, 150, 220, 330, 470
    };

    public static string Num(double value, string format = "0.###")
        => value.ToString(format, Inv);

    public static string Num(double? value, string format = "0.###")
        => value.HasValue ? Num(value.Value, format) : "—";

    public static (string Value, string Unit) ResistanceParts(double ohms)
    {
        var abs = Math.Abs(ohms);
        if (abs >= 1e9) return (Num(ohms / 1e9), "GΩ");
        if (abs >= 1e6) return (Num(ohms / 1e6), "MΩ");
        if (abs >= 1e3) return (Num(ohms / 1e3), "kΩ");
        if (abs >= 1) return (Num(ohms), "Ω");
        if (abs >= 1e-3) return (Num(ohms * 1e3), "mΩ");
        return (Num(ohms * 1e6), "µΩ");
    }

    public static string Resistance(double ohms)
    {
        var p = ResistanceParts(ohms);
        return p.Value + " " + p.Unit;
    }

    public static (string Value, string Unit) PowerParts(double watts)
    {
        var abs = Math.Abs(watts);
        if (abs >= 1e6) return (Num(watts / 1e6), "MW");
        if (abs >= 1e3) return (Num(watts / 1e3), "kW");
        if (abs < 1 && abs > 0) return (Num(watts * 1e3), "mW");
        return (Num(watts), "W");
    }

    public static string Power(double watts)
    {
        var p = PowerParts(watts);
        return p.Value + " " + p.Unit;
    }

    public static (string Value, string Unit) CurrentParts(double amps)
    {
        var abs = Math.Abs(amps);
        if (abs < 1 && abs > 0) return (Num(amps * 1e3), "mA");
        return (Num(amps), "A");
    }

    public static (string Value, string Unit) VoltageParts(double volts)
    {
        var abs = Math.Abs(volts);
        if (abs < 1 && abs > 0) return (Num(volts * 1e3), "mV");
        return (Num(volts), "V");
    }

    public static (string Value, string Unit) CapacitanceParts(double farads)
    {
        var abs = Math.Abs(farads);
        if (abs >= 1) return (Num(farads), "F");
        if (abs >= 1e-3) return (Num(farads * 1e3), "mF");
        if (abs >= 1e-6) return (Num(farads * 1e6), "µF");
        if (abs >= 1e-9) return (Num(farads * 1e9), "nF");
        return (Num(farads * 1e12), "pF");
    }

    public static (string Value, string Unit) FrequencyParts(double hz)
    {
        var abs = Math.Abs(hz);
        if (abs >= 1e9) return (Num(hz / 1e9), "GHz");
        if (abs >= 1e6) return (Num(hz / 1e6), "MHz");
        if (abs >= 1e3) return (Num(hz / 1e3), "kHz");
        return (Num(hz), "Hz");
    }

    public static string Frequency(double hz)
    {
        var p = FrequencyParts(hz);
        return p.Value + " " + p.Unit;
    }

    public static (string Value, string Unit) TimeParts(double seconds)
    {
        var abs = Math.Abs(seconds);
        if (abs >= 3600) return (Num(seconds / 3600), "ساعت");
        if (abs >= 60) return (Num(seconds / 60), "دقیقه");
        if (abs >= 1) return (Num(seconds), "s");
        if (abs >= 1e-3) return (Num(seconds * 1e3), "ms");
        if (abs >= 1e-6) return (Num(seconds * 1e6), "µs");
        return (Num(seconds * 1e9), "ns");
    }

    public static string Time(double seconds)
    {
        var p = TimeParts(seconds);
        return p.Value + " " + p.Unit;
    }

    public static string Money(double amount) => amount.ToString("N0", Inv);

    public static string Duration(double hours)
    {
        if (double.IsNaN(hours) || double.IsInfinity(hours) || hours <= 0) return "—";

        if (hours >= 48)
        {
            var days = Math.Floor(hours / 24);
            var rest = Math.Round(hours - days * 24);
            return rest > 0
                ? $"{Num(days, "0")} روز و {Num(rest, "0")} ساعت"
                : $"{Num(days, "0")} روز";
        }

        var wholeHours = Math.Floor(hours);
        var minutes = Math.Round((hours - wholeHours) * 60);
        if (minutes >= 60) { wholeHours += 1; minutes = 0; }

        if (wholeHours <= 0) return $"{Num(minutes, "0")} دقیقه";
        return minutes > 0
            ? $"{Num(wholeHours, "0")} ساعت و {Num(minutes, "0")} دقیقه"
            : $"{Num(wholeHours, "0")} ساعت";
    }

    public static double NextE24(double value)
    {
        if (value <= 0) return 0;

        var exp = Math.Floor(Math.Log10(value));
        var scale = Math.Pow(10, exp);
        var mantissa = value / scale;

        foreach (var e in E24)
        {
            if (mantissa <= e + 1e-9) return e * scale;
        }

        return 10 * scale;
    }

    public static double NearestE24(double value)
    {
        if (value <= 0) return 0;

        var exp = Math.Floor(Math.Log10(value));
        var scale = Math.Pow(10, exp);
        var mantissa = value / scale;

        double lower = E24[0] * scale;
        double upper = 10 * scale;

        foreach (var e in E24)
        {
            if (e * scale <= value) lower = e * scale;
            else { upper = e * scale; break; }
        }

        return (value - lower) <= (upper - value) ? lower : upper;
    }

    public static string Percent(double value) => Num(value, "0.##") + "٪";
}
