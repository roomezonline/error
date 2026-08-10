using System.Text.Json;
using System.Text.Json.Serialization;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/chart")]
public class ChartLegacyController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = null };

    public ChartLegacyController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet("{monitorId}/connection-status")]
    public async Task GetConnectionStatus(int monitorId)
    {
        var resp = new LegacyConnectionStatus();
        try
        {
            var last = await _db.MonitoringDataRecords
                .Where(r => r.MonitoringId == monitorId)
                .OrderByDescending(r => r.Timestamp)
                .Select(r => new { r.Timestamp, r.TimestampFa })
                .FirstOrDefaultAsync();

            var first = await _db.MonitoringDataRecords
                .Where(r => r.MonitoringId == monitorId)
                .OrderBy(r => r.Timestamp)
                .Select(r => new { r.TimestampFa })
                .FirstOrDefaultAsync();

            resp.RecordCount = await _db.MonitoringDataRecords.CountAsync(r => r.MonitoringId == monitorId);
            resp.FirstTimestamp = first?.TimestampFa ?? "-";
            resp.LastTimestamp = last?.TimestampFa ?? "-";

            if (last != null)
                resp.IsOnline = (DateTime.UtcNow - last.Timestamp).TotalMinutes < 2.0;
        }
        catch { }

        await Response.Body.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(resp, _json));
    }

    [HttpGet("{monitorId}/header-info")]
    public async Task GetHeaderInfo(int monitorId)
    {
        var info = new LegacyHeaderInfo();
        try
        {
            var connection = await _db.MonitoringReceiptConnections
                .Include(c => c.MonitoringDevice)
                .Include(c => c.CustomerReceipt).ThenInclude(cr => cr.Customer)
                .FirstOrDefaultAsync(c => c.Id == monitorId);

            if (connection != null)
            {
                info.DeviceCode = connection.MonitoringDevice.DeviceNumber;
                var customer = connection.CustomerReceipt?.Customer;
                if (customer != null)
                {
                    info.CustomerFullName = $"{customer.FirstName} {customer.LastName}".Trim();
                    info.Disc = customer.Mobile ?? "-";
                }
            }
            else
            {
                var first = await _db.MonitoringDataRecords
                    .Where(r => r.MonitoringId == monitorId)
                    .OrderBy(r => r.Timestamp)
                    .Select(r => r.DeviceCode)
                    .FirstOrDefaultAsync();
                if (!string.IsNullOrEmpty(first))
                    info.DeviceCode = first;
            }
        }
        catch { }

        await Response.Body.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(info, _json));
    }

    [HttpGet("{monitorId}/equipment-status")]
    public async Task GetEquipmentStatus(int monitorId)
    {
        var resp = new LegacyEquipmentStatusResponse();
        try
        {
            var records = await _db.MonitoringDataRecords
                .Where(r => r.MonitoringId == monitorId)
                .OrderByDescending(r => r.Timestamp)
                .Take(1000)
                .OrderBy(r => r.Timestamp)
                .ToListAsync();

            var ts = records.Select(r => r.TimestampFa).ToList();
            var motor = records.Select(r => r.MotorState ? 1 : 0).ToList();
            var e1 = records.Select(r => r.Element1 ? 1 : 0).ToList();
            var e2 = records.Select(r => r.Element2 ? 1 : 0).ToList();

            resp.Motor = ComputeStatus(ts, motor);
            resp.Heater1 = ComputeStatus(ts, e1);
            resp.Heater2 = ComputeStatus(ts, e2);
        }
        catch { }

        await Response.Body.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(resp, _json));
    }

    [HttpGet("{monitorId}/decimated-data")]
    public async Task GetDecimatedData(
        int monitorId,
        [FromQuery] string? startJalali = null,
        [FromQuery] string? endJalali = null,
        [FromQuery] int maxPoints = 1500)
    {
        var chartData = new LegacyChartData();
        try
        {
            var query = _db.MonitoringDataRecords.Where(r => r.MonitoringId == monitorId);

            if (!string.IsNullOrEmpty(startJalali))
            {
                var dt = TryParseJalali(startJalali);
                if (dt.HasValue) query = query.Where(r => r.Timestamp >= dt.Value);
            }
            if (!string.IsNullOrEmpty(endJalali))
            {
                var dt = TryParseJalali(endJalali);
                if (dt.HasValue) query = query.Where(r => r.Timestamp <= dt.Value);
            }

            var records = await query.OrderBy(r => r.Timestamp).Take(5000).ToListAsync();
            if (records.Count == 0) { await WriteJson(chartData); return; }

            var ts = new List<string>();
            var xs = new List<double>();
            var temp = new List<double>();
            var freez = new List<double>();
            var motor = new List<int>();
            var power = new List<int>();
            var e1 = new List<int>();
            var e2 = new List<int>();
            var fdc = new List<int>();
            var fac = new List<int>();
            var curSt = new List<int>();
            var powSt = new List<int>();
            var curVal = new List<double>();
            var powVal = new List<double>();
            var ids = new List<int>();
            var notes = new List<string>();

            DateTime? t0 = null;
            foreach (var r in records)
            {
                ts.Add(r.TimestampFa);
                if (!t0.HasValue) t0 = r.Timestamp;
                xs.Add((r.Timestamp - t0.Value).TotalSeconds);
                temp.Add((double)(r.TemperatureRef ?? 0f));
                freez.Add((double)(r.TemperatureFreez ?? 0f));
                motor.Add(r.MotorState ? 1 : 0);
                power.Add(r.Bargh ? 1 : 0);
                e1.Add(r.Element1 ? 1 : 0);
                e2.Add(r.Element2 ? 1 : 0);
                fdc.Add(r.Fdc1 ? 1 : 0);
                fac.Add(r.Fac1 ? 1 : 0);
                curSt.Add(r.Fdc1 ? 1 : 0);
                powSt.Add(r.Fac1 ? 1 : 0);
                curVal.Add((double)(r.Jaryan ?? 0f));
                powVal.Add((double)(r.Power ?? 0f));
                ids.Add((int)r.Id);
                notes.Add(r.Note ?? "");
            }

            var transIdx = new SortedSet<int>();
            void Collect(List<int> s) { if (s.Count == 0) return; int p = s[0]; for (int i = 1; i < s.Count; i++) { int c = s[i]; if (c != p) { transIdx.Add(i - 1); transIdx.Add(i); } p = c; } }
            Collect(motor); Collect(power); Collect(e1); Collect(e2); Collect(fdc); Collect(fac); Collect(curSt); Collect(powSt);

            for (int i = 0; i < notes.Count; i++) { if (!string.IsNullOrWhiteSpace(notes[i])) transIdx.Add(i); }
            transIdx.Add(0); transIdx.Add(ts.Count - 1);

            var lttb = LttbSelectIndices(xs, temp, maxPoints);
            var selected = new SortedSet<int>(transIdx);
            foreach (var idx in lttb) { if (selected.Count >= maxPoints) break; selected.Add(idx); }

            foreach (var i in selected)
            {
                chartData.Timestamps.Add(ts[i]);
                chartData.Temperatures.Add(temp[i]);
                chartData.FreezerTemperatures.Add(freez[i]);
                chartData.MotorStates.Add(motor[i]);
                chartData.PowerStates.Add(power[i]);
                chartData.Element1States.Add(e1[i]);
                chartData.Element2States.Add(e2[i]);
                chartData.FDCStates.Add(fdc[i]);
                chartData.FACStates.Add(fac[i]);
                chartData.CurrentStates.Add(curSt[i]);
                chartData.PowerConsumptionStates.Add(powSt[i]);
                chartData.CurrentValues.Add(curVal[i]);
                chartData.PowerValues.Add(powVal[i]);
                chartData.Ids.Add(ids[i]);
                chartData.Notes.Add(notes[i]);
            }
        }
        catch { }

        await WriteJson(chartData);
    }

    [HttpGet("{monitorId}/latest-data")]
    public async Task GetLatestData(int monitorId, [FromQuery] int take = 1000)
    {
        var chartData = new LegacyChartData();
        try
        {
            var records = await _db.MonitoringDataRecords
                .Where(r => r.MonitoringId == monitorId)
                .OrderByDescending(r => r.Timestamp)
                .Take(take)
                .OrderBy(r => r.Timestamp)
                .ToListAsync();

            foreach (var r in records)
            {
                chartData.Timestamps.Add(r.TimestampFa);
                chartData.Temperatures.Add((double)(r.TemperatureRef ?? 0f));
                chartData.FreezerTemperatures.Add((double)(r.TemperatureFreez ?? 0f));
                chartData.MotorStates.Add(r.MotorState ? 1 : 0);
                chartData.PowerStates.Add(r.Bargh ? 1 : 0);
                chartData.Element1States.Add(r.Element1 ? 1 : 0);
                chartData.Element2States.Add(r.Element2 ? 1 : 0);
                chartData.FDCStates.Add(r.Fdc1 ? 1 : 0);
                chartData.FACStates.Add(r.Fac1 ? 1 : 0);
                chartData.CurrentStates.Add(r.Fdc1 ? 1 : 0);
                chartData.PowerConsumptionStates.Add(r.Fac1 ? 1 : 0);
                chartData.CurrentValues.Add((double)(r.Jaryan ?? 0f));
                chartData.PowerValues.Add((double)(r.Power ?? 0f));
                chartData.Ids.Add((int)r.Id);
                chartData.Notes.Add(r.Note ?? "");
            }
        }
        catch { }

        await WriteJson(chartData);
    }

    [HttpGet("{monitorId}/chart-data-chunk")]
    public async Task GetChartDataChunk(
        int monitorId,
        [FromQuery] string? startJalali = null,
        [FromQuery] string? endJalali = null,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 800)
    {
        var resp = new LegacyChunkedData { Page = page, PageSize = pageSize };
        try
        {
            var query = _db.MonitoringDataRecords.Where(r => r.MonitoringId == monitorId);

            if (!string.IsNullOrEmpty(startJalali))
            {
                var dt = TryParseJalali(startJalali);
                if (dt.HasValue) query = query.Where(r => r.Timestamp >= dt.Value);
            }
            if (!string.IsNullOrEmpty(endJalali))
            {
                var dt = TryParseJalali(endJalali);
                if (dt.HasValue) query = query.Where(r => r.Timestamp <= dt.Value);
            }

            resp.TotalCount = await query.CountAsync();
            var records = await query.OrderBy(r => r.Timestamp).Skip(page * pageSize).Take(pageSize).ToListAsync();

            foreach (var r in records)
            {
                resp.Timestamps.Add(r.TimestampFa);
                resp.Temperatures.Add((double)(r.TemperatureRef ?? 0f));
                resp.FreezerTemperatures.Add((double)(r.TemperatureFreez ?? 0f));
                resp.MotorStates.Add(r.MotorState ? 1 : 0);
                resp.PowerStates.Add(r.Bargh ? 1 : 0);
                resp.Element1States.Add(r.Element1 ? 1 : 0);
                resp.Element2States.Add(r.Element2 ? 1 : 0);
                resp.FDCStates.Add(r.Fdc1 ? 1 : 0);
                resp.FACStates.Add(r.Fac1 ? 1 : 0);
                resp.CurrentStates.Add(r.Fdc1 ? 1 : 0);
                resp.PowerConsumptionStates.Add(r.Fac1 ? 1 : 0);
                resp.CurrentValues.Add((double)(r.Jaryan ?? 0f));
                resp.PowerValues.Add((double)(r.Power ?? 0f));
                resp.Ids.Add((int)r.Id);
                resp.Notes.Add(r.Note ?? "");
            }

            resp.HasMore = (page + 1) * pageSize < resp.TotalCount;
        }
        catch { }

        await WriteJson(resp);
    }

    [HttpPost("{monitorId}/upsert-note")]
    public async Task UpsertNote(int monitorId, [FromBody] NoteRequest req)
    {
        var res = new SimpleResult { Ok = false, Message = "" };
        try
        {
            var record = await _db.MonitoringDataRecords
                .FirstOrDefaultAsync(r => r.MonitoringId == monitorId && r.Id == req.RecordId);
            if (record != null)
            {
                record.Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note;
                await _db.SaveChangesAsync();
                res.Ok = true; res.Message = "Saved";
            }
            else { res.Message = "Not found"; }
        }
        catch (Exception ex) { res.Message = ex.Message; }

        await WriteJson(res);
    }

    private async Task WriteJson<T>(T data)
    {
        Response.ContentType = "application/json; charset=utf-8";
        await Response.Body.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(data, _json));
    }

    private static LegacyEquipStatus ComputeStatus(List<string> timestamps, List<int> states)
    {
        var result = new LegacyEquipStatus();
        if (timestamps == null || states == null || timestamps.Count == 0 || states.Count != timestamps.Count)
            return result;

        int n = states.Count;
        bool currentOn = states[n - 1] == 1;
        result.CurrentStatus = currentOn ? "On" : "Off";

        int startIdx = -1;
        for (int i = n - 1; i > 0; i--)
            if (states[i] == 1 && states[i - 1] == 0) { startIdx = i; break; }
        if (startIdx == -1 && currentOn) startIdx = 0;

        if (startIdx >= 0)
        {
            result.LastStart = timestamps[startIdx] ?? "-";
            if (startIdx > 0)
            {
                int j = startIdx - 1;
                while (j > 0 && states[j - 1] == 0) j--;
                var t1 = TryParseJalali(timestamps[j]);
                var t2 = TryParseJalali(timestamps[startIdx]);
                if (t1.HasValue && t2.HasValue && t2.Value >= t1.Value)
                    result.StopBeforeLastStartSeconds = (long)(t2.Value - t1.Value).TotalSeconds;
            }
        }
        return result;
    }

    private static List<int> LttbSelectIndices(List<double> x, List<double> y, int threshold)
    {
        int n = Math.Min(x.Count, y.Count);
        if (threshold >= n || threshold <= 0 || n == 0)
            return Enumerable.Range(0, n).ToList();

        var sampled = new List<int>(threshold);
        int bucketSize = Math.Max(1, (n - 2) / (threshold - 2));
        int a = 0;
        sampled.Add(a);

        for (int i = 0; i < threshold - 2; i++)
        {
            int start = Math.Min((i + 1) * bucketSize + 1, n - 1);
            int end = Math.Min((i + 2) * bucketSize + 1, n);
            if (start >= end) start = Math.Max(start - 1, i + 1);
            if (start >= end) break;

            double avgX = 0, avgY = 0;
            int avgCount = 0;
            for (int j = start; j < end; j++) { avgX += x[j]; avgY += y[j]; avgCount++; }
            if (avgCount > 0) { avgX /= avgCount; avgY /= avgCount; }

            double ax = x[a], ay = y[a];
            double maxArea = -1; int maxIndex = start;
            for (int j = start; j < end; j++)
            {
                double area = Math.Abs((ax - avgX) * (y[j] - ay) - (ay - avgY) * (x[j] - ax));
                if (area > maxArea) { maxArea = area; maxIndex = j; }
            }
            sampled.Add(maxIndex);
            a = maxIndex;
        }

        sampled.Add(n - 1);
        sampled.Sort();
        return sampled;
    }

    private static DateTime? TryParseJalali(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var parts = input.Split(new[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 && int.TryParse(parts[0], out var y) && int.TryParse(parts[1], out var m) && int.TryParse(parts[2], out var d) && y > 1300)
        {
            var dtOffset = PersianDateHelper.FromPersianDate(y, m, d);
            var dt = dtOffset.DateTime;
            if (parts.Length >= 4 && TimeSpan.TryParse(parts[3], out var time))
                dt = dt.Add(time);
            return dt;
        }
        return DateTime.TryParse(input, out var parsed) ? parsed : null;
    }
}

public class NoteRequest
{
    public long RecordId { get; set; }
    public string? Note { get; set; }
}

public class SimpleResult
{
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
}

public class LegacyChartData
{
    public List<string> Timestamps { get; set; } = new();
    public List<double> Temperatures { get; set; } = new();
    public List<double> FreezerTemperatures { get; set; } = new();
    public List<int> MotorStates { get; set; } = new();
    public List<int> PowerStates { get; set; } = new();
    public List<int> Element1States { get; set; } = new();
    public List<int> Element2States { get; set; } = new();
    public List<int> FDCStates { get; set; } = new();
    public List<int> FACStates { get; set; } = new();
    public List<int> CurrentStates { get; set; } = new();
    public List<int> PowerConsumptionStates { get; set; } = new();
    public List<double> CurrentValues { get; set; } = new();
    public List<double> PowerValues { get; set; } = new();
    public List<int> Ids { get; set; } = new();
    public List<string?> Notes { get; set; } = new();
}

public class LegacyChunkedData : LegacyChartData
{
    public bool HasMore { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class LegacyConnectionStatus
{
    public bool IsOnline { get; set; }
    public string LastTimestamp { get; set; } = "-";
    public int RecordCount { get; set; }
    public string FirstTimestamp { get; set; } = "-";
}

public class LegacyHeaderInfo
{
    public string DeviceCode { get; set; } = "-";
    public string CustomerFullName { get; set; } = "-";
    public string Disc { get; set; } = "-";
}

public class LegacyEquipStatus
{
    public string CurrentStatus { get; set; } = "-";
    public string LastStart { get; set; } = "-";
    public long StopBeforeLastStartSeconds { get; set; }
}

public class LegacyEquipmentStatusResponse
{
    public LegacyEquipStatus Motor { get; set; } = new();
    public LegacyEquipStatus Heater1 { get; set; } = new();
    public LegacyEquipStatus Heater2 { get; set; } = new();
}
