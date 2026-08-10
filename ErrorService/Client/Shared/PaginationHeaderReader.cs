namespace ErrorService.Client.Shared;

public static class PaginationHeaderReader
{
    public static int ReadTotalCount(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-Total-Count", out var values))
        {
            var first = values.FirstOrDefault();
            if (int.TryParse(first, out var total))
                return total;
        }

        return 0;
    }
}
