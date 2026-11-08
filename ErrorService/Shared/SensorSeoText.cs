using System.Text;

namespace ErrorService.Shared;

public readonly record struct SensorSeoInfo(
    string Name,
    string CoreName,
    string FullName,
    string Title,
    string Description,
    string Keywords);

public static class SensorSeoText
{
    private const string SiteName = "ارورسرویس";
    private const int MaxTitleWithoutSite = 58;
    private const int MaxDescription = 165;

    public static SensorSeoInfo Build(
        string? sensorName,
        string? sensorType,
        string? sizeText,
        string? deviceName,
        string? notes)
    {
        var name = NormalizeName(sensorName);
        var type = Clean(sensorType);
        var size = Clean(sizeText);
        var device = Clean(deviceName);
        // اگر مقدار/نوع از قبل داخل نام آمده، دوباره تکرار نشود.
        var hasSize = size != null && !name.Contains(size, StringComparison.OrdinalIgnoreCase);
        var hasType = type != null && !name.Contains(type, StringComparison.OrdinalIgnoreCase);
        var spec = hasSize ? size : hasType ? type : null;

        var coreName = spec == null ? name : $"{name} {spec}";
        var fullName = device == null ? coreName : $"{coreName} - {device}";
        // نام دستگاه همان چیزی است که صفحه را از ده‌ها صفحهٔ مشابه («سنسور دما» برای
        // دستگاه‌های مختلف) متمایز می‌کند؛ اگر با مشخصهٔ فنی در تیتر جا نشد، مشخصه
        // حذف می‌شود (در توضیح می‌ماند) ولی دستگاه هرگز از تیتر حذف نمی‌شود.
        var titleBase = fullName;
        if (device != null && fullName.Length > MaxTitleWithoutSite)
        {
            var nameAndDevice = $"{name} - {device}";
            if (nameAndDevice.Length <= MaxTitleWithoutSite) titleBase = nameAndDevice;
        }

        return new SensorSeoInfo(
            name,
            coreName,
            fullName,
            $"{titleBase} | {SiteName}",
            BuildDescription(name, hasType ? type : null, hasSize ? size : null, device, notes),
            BuildKeywords(name, coreName, type, device));
    }

    public static string NormalizeName(string? sensorName)
    {
        var name = Clean(sensorName);
        if (name == null) return "سنسور لوازم خانگی";
        return name.StartsWith("سنسور", StringComparison.Ordinal) ? name : "سنسور " + name;
    }

    private static string BuildDescription(string name, string? type, string? size, string? device, string? notes)
    {
        var details = new List<string>();
        if (type != null) details.Add($"نوع {type}");
        if (size != null) details.Add($"مقدار {size}");

        var sb = new StringBuilder("مشخصات فنی ");
        sb.Append(name);
        if (details.Count > 0) sb.Append($" ({string.Join("، ", details)})");
        if (device != null) sb.Append($" برای {device}");
        sb.Append(". ");

        var note = CleanNotes(notes);
        if (note != null) sb.Append(note);

        var text = sb.ToString().Trim();
        if (text.Length <= MaxDescription) return text;

        var cut = text.LastIndexOf(' ', MaxDescription - 3);
        if (cut < MaxDescription / 2) cut = MaxDescription - 3;
        return text[..cut].TrimEnd('.', '،', ' ') + "...";
    }

    private static string BuildKeywords(string name, string coreName, string? type, string? device)
    {
        var candidates = new List<string> { name, coreName, "مشخصات سنسور", "سنسور لوازم خانگی" };
        if (type != null) candidates.Add(type);
        if (device != null) candidates.Add(device);

        var parts = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            var value = Clean(candidate);
            if (value != null && seen.Add(value)) parts.Add(value);
        }
        return string.Join(", ", parts);
    }

    private static string? CleanNotes(string? notes)
    {
        var note = Clean(notes);
        if (note == null) return null;
        var collapsed = string.Join(" ",
            note.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0));
        return collapsed.Length == 0 ? null : collapsed;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
