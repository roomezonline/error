using System.Net.Http.Json;
using ErrorService.Shared;

namespace ErrorService.Client.Services;

public sealed class CustomerSearchCacheService
{
    private readonly HttpClient _http;
    private readonly Dictionary<int, List<WorkshopCustomerDto>> _cache = new();
    private readonly HashSet<int> _loading = new();
    private readonly object _gate = new();

    public CustomerSearchCacheService(HttpClient http)
    {
        _http = http;
    }

    private static int Key(int? workshopId) => workshopId is > 0 ? workshopId.Value : 0;

    public bool TryGet(int? workshopId, out List<WorkshopCustomerDto> customers)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(Key(workshopId), out var list))
            {
                customers = list;
                return true;
            }

            customers = new List<WorkshopCustomerDto>();
            return false;
        }
    }

    public async Task<List<WorkshopCustomerDto>> GetOrLoadAsync(int? workshopId, CancellationToken ct = default)
    {
        var key = Key(workshopId);

        while (true)
        {
            lock (_gate)
            {
                if (_cache.TryGetValue(key, out var hit))
                    return hit;

                if (!_loading.Contains(key))
                {
                    _loading.Add(key);
                    break;
                }
            }

            await Task.Delay(40, ct);
        }

        try
        {
            var url = workshopId is > 0
                ? $"api/workshop-customers/search?q=&workshopId={workshopId.Value}&limit=0"
                : "api/workshop-customers/search?q=&limit=0";

            var list = await _http.GetFromJsonAsync<List<WorkshopCustomerDto>>(url, ct)
                ?? new List<WorkshopCustomerDto>();

            lock (_gate)
            {
                _cache[key] = list;
            }

            return list;
        }
        catch
        {
            // Do not cache failures so next open can retry.
            throw;
        }
        finally
        {
            lock (_gate)
            {
                _loading.Remove(key);
            }
        }
    }

    public void Upsert(int? workshopId, WorkshopCustomerDto customer)
    {
        var key = Key(workshopId);
        lock (_gate)
        {
            if (!_cache.TryGetValue(key, out var list))
            {
                list = new List<WorkshopCustomerDto>();
                _cache[key] = list;
            }

            var existing = list.FindIndex(x => x.Id == customer.Id);
            if (existing >= 0)
                list[existing] = customer;
            else
                list.Insert(0, customer);
        }
    }

    public void Invalidate(int? workshopId)
    {
        lock (_gate)
        {
            _cache.Remove(Key(workshopId));
        }
    }

    public void InvalidateAll()
    {
        lock (_gate)
        {
            _cache.Clear();
        }
    }
}
