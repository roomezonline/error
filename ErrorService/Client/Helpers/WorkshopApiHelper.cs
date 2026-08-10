namespace ErrorService.Client.Helpers;

public static class WorkshopApiHelper
{
    public static int? ResolveWorkshopId(bool isSuperAdmin, int? selectedWorkshopId, int? receiptWorkshopId = null)
    {
        if (!isSuperAdmin)
            return null;

        if (receiptWorkshopId is > 0)
            return receiptWorkshopId;

        if (selectedWorkshopId is > 0)
            return selectedWorkshopId;

        return null;
    }

    public static string BuildWorkshopQuery(bool isSuperAdmin, int? selectedWorkshopId, int? receiptWorkshopId = null, string? additionalQuery = null)
    {
        if (!isSuperAdmin)
            return string.IsNullOrWhiteSpace(additionalQuery) ? string.Empty : "?" + additionalQuery.TrimStart('?', '&');

        var workshopId = ResolveWorkshopId(isSuperAdmin, selectedWorkshopId, receiptWorkshopId);
        if (workshopId is not > 0)
            return string.IsNullOrWhiteSpace(additionalQuery) ? string.Empty : "?" + additionalQuery.TrimStart('?', '&');

        if (string.IsNullOrWhiteSpace(additionalQuery))
            return $"?workshopId={workshopId}";

        return $"?workshopId={workshopId}&{additionalQuery.TrimStart('?', '&')}";
    }

    public static string AppendWorkshopQuery(string url, int? workshopId)
    {
        if (workshopId is not > 0 || string.IsNullOrWhiteSpace(url))
            return url;

        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{url}{separator}workshopId={workshopId.Value}";
    }
}
