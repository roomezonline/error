using System.Globalization;
using System.Text.Json;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Server.Services;
using ErrorService.Shared;
using ErrorService.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/monitoring/chart")]
public class MonitoringChartController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;
    private readonly MonitoringCacheService _monitoringCache;

    public MonitoringChartController(ErrorServiceDbContext db, MonitoringCacheService monitoringCache)
    {
        _db = db;
        _monitoringCache = monitoringCache;
    }

    [HttpGet("{monitoringId}/records")]
    public async Task<ActionResult<MonitoringChartResponse>> GetRecords(
        int monitoringId,
        [FromQuery] int count = 50,
        [FromQuery] bool all = false)
    {
        if (monitoringId <= 0)
            return BadRequest("Invalid monitoringId");

        // Load connection info (customer + device)
        var connection = await _db.MonitoringReceiptConnections
            .AsNoTracking()
            .Include(x => x.CustomerReceipt).ThenInclude(x => x.Customer)
            .Include(x => x.CustomerReceipt).ThenInclude(x => x.DeviceType)
            .Include(x => x.CustomerReceipt).ThenInclude(x => x.DeviceBrand)
            .Include(x => x.MonitoringDevice)
            .FirstOrDefaultAsync(x => x.Id == monitoringId);

        var query = _db.MonitoringDeviceChangeLogs
            .Where(x => x.MonitoringId == monitoringId);

        if (all)
        {
            query = query.OrderBy(x => x.CreatedAt);
        }
        else
        {
            query = query.OrderByDescending(x => x.CreatedAt)
                         .Take(count)
                         .OrderBy(x => x.CreatedAt);
        }

        var logs = await query.ToListAsync();

        if (logs.Count == 0)
            return Ok(new MonitoringChartResponse());

        var motor = 0;
        var heater1 = 0;
        var heater2 = 0;
        var power = 1;
        var timestamps = new List<DateTime>();

        var response = new MonitoringChartResponse();

        if (connection != null)
        {
            var receipt = connection.CustomerReceipt;
            response.ConnectionInfo = new Shared.ChartConnectionInfo
            {
                CustomerName = receipt?.Customer != null
                    ? $"{receipt.Customer.FirstName} {receipt.Customer.LastName}"
                    : "",
                CustomerMobile = receipt?.Customer?.Mobile ?? "",
                DeviceSerial = connection.MonitoringDevice?.DeviceNumber ?? "",
                DeviceName = connection.MonitoringDevice?.Title ?? "",
                DeviceTypeName = receipt?.DeviceType?.Name,
                DeviceBrandName = receipt?.DeviceBrand?.Name
            };
        }

        foreach (var log in logs)
        {
            float? tempRef = null;
            float? tempFreez = null;
            float? tavan = null;
            float? jaryan = null;
            try
            {
                using var doc = JsonDocument.Parse(log.DataSnapshot);
                var root = doc.RootElement;
                if (root.TryGetProperty("Temp1", out var t1) && t1.ValueKind == JsonValueKind.String)
                {
                    if (float.TryParse(t1.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var tr))
                        tempRef = tr;
                }
                if (root.TryGetProperty("Temp2", out var t2) && t2.ValueKind == JsonValueKind.String)
                {
                    if (float.TryParse(t2.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var tf))
                        tempFreez = tf;
                }
                if (root.TryGetProperty("Tavan", out var ta) && ta.ValueKind == JsonValueKind.String)
                {
                    if (float.TryParse(ta.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var tv))
                        tavan = tv;
                }
                if (root.TryGetProperty("Jaryan", out var ja) && ja.ValueKind == JsonValueKind.String)
                {
                    var jstr = ja.GetString();
                    if (!string.IsNullOrEmpty(jstr))
                    {
                        float maxJ = 0;
                        foreach (var part in jstr.Split('-'))
                        {
                            if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var jv) && jv > maxJ)
                                maxJ = jv;
                        }
                        jaryan = maxJ;
                    }
                }
            }
            catch { }

            var isOn = log.NewValue?.Trim().Equals("on", StringComparison.OrdinalIgnoreCase) == true;
            switch (log.ChangeType)
            {
                case "Priz": power = isOn ? 1 : 0; break;
                case "MotorState": motor = isOn ? 1 : 0; break;
                case "Element1": heater1 = isOn ? 1 : 0; break;
                case "Element2": heater2 = isOn ? 1 : 0; break;
            }

            response.Labels.Add(log.CreatedAt.ToString("HH:mm:ss"));
            response.LabelsFa.Add(log.CreatedAtFa);
            response.TemperatureRef.Add(tempRef);
            response.TemperatureFreez.Add(tempFreez);
            response.Tavan.Add(tavan);
            response.Jaryan.Add(jaryan);
            response.Motor.Add(motor);
            response.Heater1.Add(heater1);
            response.Heater2.Add(heater2);
            response.Power.Add(power);
            timestamps.Add(log.CreatedAt);
        }

        // Fill forward null values so chart lines remain continuous
        float? lastTavan = null;
        for (int i = 0; i < response.Tavan.Count; i++)
        {
            if (response.Tavan[i].HasValue) lastTavan = response.Tavan[i];
            else response.Tavan[i] = lastTavan;
        }
        float? lastJaryan = null;
        for (int i = 0; i < response.Jaryan.Count; i++)
        {
            if (response.Jaryan[i].HasValue) lastJaryan = response.Jaryan[i];
            else response.Jaryan[i] = lastJaryan;
        }

        // Downsample to ~1500 max for mobile performance, keeping all transition points
        timestamps = DownsampleChartResponse(response, timestamps, 1500);

        response.Cycles["power"] = CalculateCycles(response.Power, timestamps);
        response.Cycles["motor"] = CalculateCycles(response.Motor, timestamps);
        response.Cycles["heater1"] = CalculateCycles(response.Heater1, timestamps);
        response.Cycles["heater2"] = CalculateCycles(response.Heater2, timestamps);

        return Ok(response);
    }

    [HttpGet("{monitoringId}/detailed-records")]
    public async Task<ActionResult<MonitoringDetailedChartResponse>> GetDetailedRecords(
        int monitoringId,
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        [FromQuery] int count = 100000,
        [FromQuery] int maxPoints = 500)
    {
        if (monitoringId <= 0)
            return BadRequest("Invalid monitoringId");

        var query = _db.MonitoringDataRecords
            .Where(x => x.MonitoringId == monitoringId);

        if (!string.IsNullOrEmpty(startDate))
        {
            var dt = TryParseJalaliDate(startDate);
            if (dt.HasValue)
                query = query.Where(x => x.Timestamp >= dt.Value);
        }

        if (!string.IsNullOrEmpty(endDate))
        {
            var dt = TryParseJalaliDate(endDate);
            if (dt.HasValue)
                query = query.Where(x => x.Timestamp <= dt.Value);
        }

        var totalCount = await query.CountAsync();

        var records = await query
            .OrderBy(x => x.Timestamp)
            .Take(count)
            .ToListAsync();

        if (records.Count == 0)
            return Ok(new MonitoringDetailedChartResponse { TotalCount = totalCount });

        var response = new MonitoringDetailedChartResponse
        {
            TotalCount = totalCount
        };

        var selected = DecimateRecords(records, maxPoints);

        foreach (var i in selected)
        {
            var r = records[i];
            response.Timestamps.Add(r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            response.TimestampsFa.Add(r.TimestampFa);
            response.TemperatureRef.Add(r.TemperatureRef);
            response.TemperatureFreez.Add(r.TemperatureFreez);
            response.MotorState.Add(r.MotorState ? 1 : 0);
            response.Bargh.Add(r.Bargh ? 1 : 0);
            response.Element1.Add(r.Element1 ? 1 : 0);
            response.Element2.Add(r.Element2 ? 1 : 0);
            response.Fdc1.Add(r.Fdc1 ? 1 : 0);
            response.Fac1.Add(r.Fac1 ? 1 : 0);
            response.Jaryan.Add(r.Jaryan);
            response.Power.Add(r.Power);
            response.Kw.Add(r.Kw);
            response.SumKw.Add(r.SumKw);
            response.State.Add(r.State);
            response.Note.Add(r.Note);
            response.Ids.Add(r.Id);
        }

        return Ok(response);
    }

    private static List<int> DecimateRecords(List<MonitoringDataRecord> records, int maxPoints)
    {
        var n = records.Count;
        if (n <= maxPoints || maxPoints <= 0)
            return Enumerable.Range(0, n).ToList();

        var xs = new List<double>(n);
        var yRef = new List<double>(n);
        var yFreez = new List<double>(n);
        var t0 = records[0].Timestamp;

        for (int i = 0; i < n; i++)
        {
            xs.Add((records[i].Timestamp - t0).TotalSeconds);
            yRef.Add((double)(records[i].TemperatureRef ?? 0f));
            yFreez.Add((double)(records[i].TemperatureFreez ?? 0f));
        }

        var transIdx = new SortedSet<int>();

        void CollectTransitions(List<int> states)
        {
            if (states.Count == 0) return;
            int prev = states[0];
            for (int i = 1; i < states.Count; i++)
            {
                int cur = states[i];
                if (cur != prev)
                {
                    transIdx.Add(i - 1);
                    transIdx.Add(i);
                }
                prev = cur;
            }
        }

        CollectTransitions(records.Select(r => r.MotorState ? 1 : 0).ToList());
        CollectTransitions(records.Select(r => r.Bargh ? 1 : 0).ToList());
        CollectTransitions(records.Select(r => r.Element1 ? 1 : 0).ToList());
        CollectTransitions(records.Select(r => r.Element2 ? 1 : 0).ToList());
        CollectTransitions(records.Select(r => r.Fdc1 ? 1 : 0).ToList());
        CollectTransitions(records.Select(r => r.Fac1 ? 1 : 0).ToList());

        var noteIdx = new SortedSet<int>();
        for (int i = 0; i < n; i++)
        {
            if (!string.IsNullOrWhiteSpace(records[i].Note))
                noteIdx.Add(i);
        }

        transIdx.Add(0);
        transIdx.Add(n - 1);

        var lttbRef = LttbSelectIndices(xs, yRef, maxPoints);
        var lttbFreez = LttbSelectIndices(xs, yFreez, maxPoints);

        var selected = new SortedSet<int>(transIdx);
        foreach (var ni in noteIdx) selected.Add(ni);

        if (selected.Count < maxPoints)
        {
            foreach (var idx in lttbRef)
                if (selected.Count >= maxPoints) break;
                else selected.Add(idx);

            foreach (var idx in lttbFreez)
                if (selected.Count >= maxPoints) break;
                else selected.Add(idx);
        }

        var result = selected.ToList();
        result.Sort();
        return result;
    }

    private static List<int> LttbSelectIndices(List<double> x, List<double> y, int threshold)
    {
        var n = Math.Min(x.Count, y.Count);
        if (threshold >= n || threshold <= 0 || n == 0)
            return Enumerable.Range(0, n).ToList();

        var sampled = new List<int>(threshold);
        int bucketSize = (n - 2) / (threshold - 2);
        if (bucketSize <= 0) bucketSize = 1;

        int a = 0;
        sampled.Add(a);

        for (int i = 0; i < threshold - 2; i++)
        {
            int start = (i + 1) * bucketSize + 1;
            int end = Math.Min((i + 2) * bucketSize + 1, n - 1);
            if (start >= end) start = Math.Max(start - 1, i + 1);

            double avgX = 0, avgY = 0;
            int avgCount = 0;
            for (int j = start; j < end; j++) { avgX += x[j]; avgY += y[j]; avgCount++; }
            if (avgCount > 0) { avgX /= avgCount; avgY /= avgCount; }
            else { avgX = x[Math.Max(end - 1, 0)]; avgY = y[Math.Max(end - 1, 0)]; }

            double ax = x[a], ay = y[a];
            double maxArea = -1;
            int maxIndex = start;

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

    private static DateTime? TryParseJalaliDate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var parts = input.Split(new[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3
            && int.TryParse(parts[0], out var y)
            && int.TryParse(parts[1], out var m)
            && int.TryParse(parts[2], out var d))
        {
            if (y > 1300)
            {
                var dtOffset = PersianDateHelper.FromPersianDate(y, m, d);
                var dt = dtOffset.DateTime;
                if (parts.Length >= 4 && TimeSpan.TryParse(parts[3], out var time))
                    dt = dt.Add(time);
                return dt;
            }
        }

        if (DateTime.TryParse(input, out var parsed))
            return parsed;

        return null;
    }

    [HttpGet("{monitoringId}/connection-info")]
    public async Task<ActionResult> GetConnectionInfo(int monitoringId)
    {
        var firstRecord = await _db.MonitoringDataRecords
            .Where(r => r.MonitoringId == monitoringId)
            .OrderBy(r => r.Timestamp)
            .Select(r => new { r.Timestamp, r.TimestampFa, r.DeviceCode })
            .FirstOrDefaultAsync();

        var lastRecord = await _db.MonitoringDataRecords
            .Where(r => r.MonitoringId == monitoringId)
            .OrderByDescending(r => r.Timestamp)
            .Select(r => new { r.Timestamp, r.TimestampFa })
            .FirstOrDefaultAsync();

        var totalCount = await _db.MonitoringDataRecords
            .CountAsync(r => r.MonitoringId == monitoringId);

        string deviceCode = "";
        string deviceTitle = "";
        string customerName = "---";
        string customerMobile = "---";
        bool isOnline = false;

        if (lastRecord != null)
        {
            var diff = DateTime.UtcNow - lastRecord.Timestamp;
            isOnline = diff.TotalMinutes < 2.0;
        }

        var connection = await _db.MonitoringReceiptConnections
            .Include(c => c.MonitoringDevice)
            .Include(c => c.CustomerReceipt)
                .ThenInclude(cr => cr.Customer)
            .FirstOrDefaultAsync(c => c.Id == monitoringId);

        if (connection != null)
        {
            deviceCode = connection.MonitoringDevice.DeviceNumber;
            deviceTitle = connection.MonitoringDevice.Title;
            var customer = connection.CustomerReceipt?.Customer;
            if (customer != null)
            {
                customerName = $"{customer.FirstName} {customer.LastName}".Trim();
                customerMobile = customer.Mobile ?? "---";
            }
        }
        else if (firstRecord != null)
        {
            deviceCode = firstRecord.DeviceCode;
        }

        bool isActive = connection?.EndedAt == null;
        string? endedAtFa = connection?.EndedAt.HasValue == true
            ? PersianDateHelper.ToPersianDateTimeString(connection.EndedAt.Value, true)
            : null;
            string? endReason = connection?.EndReason;
        string createdAtFa = connection != null
            ? PersianDateHelper.ToPersianDateTimeString(connection.CreatedAt, true)
            : "";
        string createdAt = connection?.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "";

        bool isPoweredOn = _monitoringCache.Get(deviceCode)?.Priz?.Trim().ToLower() == "on";

        // Initialize grace period if connection is active and assignment has expired
        if (connection != null && isActive && connection.GracePeriodEndAt == null)
        {
            var expiredAssignments = await _db.MonitoringDeviceAssignments
                .Where(a => a.MonitoringDeviceId == connection.MonitoringDeviceId
                         && a.WorkshopId == connection.WorkshopId
                         && a.EndAt != null && a.EndAt <= DateTimeOffset.UtcNow)
                .OrderByDescending(a => a.StartAt)
                .Take(1)
                .ToListAsync();
            var expiredAssign = expiredAssignments.FirstOrDefault();
            if (expiredAssign != null)
            {
                connection.GracePeriodEndAt = DateTimeOffset.UtcNow.AddHours(24);
                await _db.SaveChangesAsync();
            }
        }

        string? gracePeriodEndAt = connection?.GracePeriodEndAt?.ToString("yyyy-MM-ddTHH:mm:ssZ");

        return Ok(new
        {
            deviceCode,
            deviceTitle,
            customerName,
            customerMobile,
            firstTimestampFa = firstRecord?.TimestampFa ?? "",
            lastTimestampFa = lastRecord?.TimestampFa ?? "",
            totalRecordCount = totalCount,
            isOnline,
            isPoweredOn,
            isActive,
            endedAtFa,
            endReason,
            createdAtFa,
            createdAt,
            gracePeriodEndAt
        });
    }

    [HttpGet("{monitoringId}/energy-report")]
    public async Task<ActionResult<EnergyReportResponse>> GetEnergyReport(
        int monitoringId,
        [FromQuery] string period = "24h")
    {
        if (monitoringId <= 0) return BadRequest("Invalid monitoringId");

        DateTime from, to;
        var now = DateTime.UtcNow;

        var baseQuery = _db.MonitoringDataRecords.Where(x => x.MonitoringId == monitoringId);

        switch (period)
        {
            case "session":
                var first = await baseQuery.OrderBy(x => x.Timestamp).FirstOrDefaultAsync();
                var last = await baseQuery.OrderByDescending(x => x.Timestamp).FirstOrDefaultAsync();
                if (first == null || last == null)
                    return Ok(new EnergyReportResponse { Period = period, FromLabel = "—", ToLabel = "—" });
                from = first.Timestamp;
                to = last.Timestamp;
                break;
            case "monthly":
                from = now.AddDays(-30);
                to = now;
                break;
            default:
                from = now.AddDays(-1);
                to = now;
                break;
        }

        var records = await _db.MonitoringDataRecords
            .Where(x => x.MonitoringId == monitoringId && x.Timestamp >= from && x.Timestamp <= to)
            .OrderBy(x => x.Timestamp)
            .ToListAsync();

        var response = new EnergyReportResponse
        {
            Period = period,
            FromLabel = from.ToString("MMM dd, HH:mm"),
            ToLabel = to.ToString("MMM dd, HH:mm")
        };

        if (records.Count > 0)
            CalculateEnergyReport(records, response);

        return Ok(response);
    }

    private static void CalculateEnergyReport(List<MonitoringDataRecord> records, EnergyReportResponse response)
    {
        var eqs = new (string Key, string Label, string Color, Func<MonitoringDataRecord, bool> State)[]
        {
            ("motor",   "موتور",  "#22c55e", r => r.MotorState),
            ("heater1", "المنت ۱","#3b82f6", r => r.Element1),
            ("heater2", "المنت ۲","#f59e0b", r => r.Element2),
        };

        var energyPerEq = new Dictionary<string, double>();
        var onTimePerEq = new Dictionary<string, double>();
        var cyclesPerEq = new Dictionary<string, int>();
        var hourlyEnergy = new Dictionary<int, double>();
        var hourlyTempSum = new Dictionary<int, double>();
        var hourlyTempCount = new Dictionary<int, int>();

        foreach (var e in eqs) { energyPerEq[e.Key] = 0; onTimePerEq[e.Key] = 0; cyclesPerEq[e.Key] = 0; }

        double totalKwh = 0;

        for (int i = 0; i < records.Count; i++)
        {
            var r = records[i];
            var hour = r.Timestamp.Hour;

            if (!hourlyEnergy.ContainsKey(hour)) { hourlyEnergy[hour] = 0; hourlyTempSum[hour] = 0; hourlyTempCount[hour] = 0; }
            if (r.TemperatureRef.HasValue) { hourlyTempSum[hour] += r.TemperatureRef.Value; hourlyTempCount[hour]++; }

            if (i > 0)
            {
                var prev = records[i - 1];
                var dtHours = (r.Timestamp - prev.Timestamp).TotalHours;
                double intervalKwh = 0;

                if (r.SumKw.HasValue && prev.SumKw.HasValue && r.SumKw.Value >= prev.SumKw.Value)
                    intervalKwh = (r.SumKw.Value - prev.SumKw.Value);
                else if (r.Kw.HasValue && prev.Kw.HasValue)
                    intervalKwh = ((r.Kw.Value + prev.Kw.Value) / 2) * dtHours;
                else if (r.Power.HasValue && prev.Power.HasValue)
                    intervalKwh = ((r.Power.Value + prev.Power.Value) / 2 / 1000) * dtHours;

                totalKwh += intervalKwh;
                hourlyEnergy[hour] += intervalKwh;

                int activeCount = 0;
                foreach (var e in eqs) if (e.State(r)) activeCount++;
                if (activeCount > 0 && intervalKwh > 0)
                {
                    double perEqKwh = intervalKwh / activeCount;
                    foreach (var e in eqs) if (e.State(r)) energyPerEq[e.Key] += perEqKwh;
                }

                foreach (var e in eqs)
                {
                    if (e.State(r))
                    {
                        if (!e.State(prev)) cyclesPerEq[e.Key]++;
                        onTimePerEq[e.Key] += dtHours;
                    }
                }
            }
        }

        response.AveragePower = Math.Round(records.Where(r => r.Power.GetValueOrDefault() > 0).Select(r => r.Power.GetValueOrDefault()).DefaultIfEmpty(0).Average(), 1);
        response.AverageCurrent = Math.Round(records.Where(r => r.Jaryan.GetValueOrDefault() > 0).Select(r => r.Jaryan.GetValueOrDefault()).DefaultIfEmpty(0).Average(), 2);

        response.TotalKwh = Math.Round(totalKwh, 2);
        response.TotalOnHours = Math.Round(onTimePerEq.Values.Sum(), 2);
        response.TotalCycles = cyclesPerEq.Values.Sum();

        double totalSpanHours = (records.Last().Timestamp - records.First().Timestamp).TotalHours;
        if (totalSpanHours < 0.01) totalSpanHours = 0.01;

        foreach (var e in eqs)
        {
            var kwh = Math.Round(energyPerEq[e.Key], 2);
            var onH = Math.Round(onTimePerEq[e.Key], 2);
            var cyc = cyclesPerEq[e.Key];
            response.Breakdown.Add(new EnergyBreakdownItem
            {
                Key = e.Key, Label = e.Label, Color = e.Color,
                Kwh = kwh,
                Percent = totalKwh > 0 ? Math.Round(kwh / totalKwh * 100, 1) : 0,
                OnHours = onH,
                OnPercent = totalSpanHours > 0 ? Math.Round(onH / totalSpanHours * 100, 1) : 0,
                CycleCount = cyc,
                AvgCycleMinutes = cyc > 0 ? Math.Round(onH * 60 / cyc, 1) : 0
            });
        }

        for (int h = 0; h < 24; h++)
        {
            response.HourlyTrend.Add(new HourlyConsumption
            {
                Label = $"{h:D2}",
                Kwh = Math.Round(hourlyEnergy.GetValueOrDefault(h, 0), 2),
                AvgTemp = hourlyTempCount.GetValueOrDefault(h, 0) > 0
                    ? Math.Round(hourlyTempSum[h] / hourlyTempCount[h], 1) : 0
            });
        }
    }

    private static List<DateTime> DownsampleChartResponse(MonitoringChartResponse response, List<DateTime> timestamps, int maxPoints)
    {
        var n = response.Labels.Count;
        if (n <= maxPoints || maxPoints <= 0) return timestamps;

        // Collect transition indices (equipment state changes)
        var keep = new HashSet<int> { 0, n - 1 };
        for (int i = 1; i < n; i++)
        {
            if (response.Motor[i] != response.Motor[i - 1] ||
                response.Heater1[i] != response.Heater1[i - 1] ||
                response.Heater2[i] != response.Heater2[i - 1] ||
                response.Power[i] != response.Power[i - 1])
            {
                keep.Add(i - 1);
                keep.Add(i);
            }
        }

        var sorted = keep.OrderBy(x => x).ToList();
        if (sorted.Count > maxPoints)
        {
            // Too many transitions — evenly decimate
            var step = (double)(sorted.Count - 1) / (maxPoints - 1);
            var reduced = new List<int>();
            for (int i = 0; i < maxPoints; i++)
                reduced.Add(sorted[(int)Math.Round(i * step)]);
            sorted = reduced.Distinct().OrderBy(x => x).ToList();
        }
        else if (sorted.Count < maxPoints && sorted.Count < n)
        {
            // Budget remaining — fill gaps evenly
            var existing = new HashSet<int>(sorted);
            var gap = (double)(n - 1) / (maxPoints - existing.Count + 1);
            for (int i = 0; i < maxPoints - existing.Count; i++)
            {
                var idx = (int)Math.Round(i * gap);
                if (idx >= 0 && idx < n && existing.Add(idx))
                    sorted.Add(idx);
            }
            sorted.Sort();
        }

        // Apply to all response lists
        var idxs = sorted;
        response.Labels = idxs.Select(i => response.Labels[i]).ToList();
        response.LabelsFa = idxs.Select(i => response.LabelsFa[i]).ToList();
        response.TemperatureRef = idxs.Select(i => response.TemperatureRef[i]).ToList();
        response.TemperatureFreez = idxs.Select(i => response.TemperatureFreez[i]).ToList();
        response.Tavan = idxs.Select(i => response.Tavan[i]).ToList();
        response.Jaryan = idxs.Select(i => response.Jaryan[i]).ToList();
        response.Motor = idxs.Select(i => response.Motor[i]).ToList();
        response.Heater1 = idxs.Select(i => response.Heater1[i]).ToList();
        response.Heater2 = idxs.Select(i => response.Heater2[i]).ToList();
        response.Power = idxs.Select(i => response.Power[i]).ToList();

        return idxs.Select(i => timestamps[i]).ToList();
    }

    private static List<CycleRecord> CalculateCycles(List<int> values, List<DateTime> timestamps)
    {
        var cycles = new List<CycleRecord>();
        int? startIdx = null;

        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == 1 && startIdx == null)
            {
                startIdx = i;
            }
            else if (values[i] == 0 && startIdx.HasValue)
            {
                var ts = timestamps[startIdx.Value];
                var te = timestamps[i];
                cycles.Add(new CycleRecord
                {
                    StartIndex = startIdx.Value,
                    EndIndex = i,
                    StartTime = ts.ToString("HH:mm:ss"),
                    EndTime = te.ToString("HH:mm:ss"),
                    Duration = (te - ts).TotalHours >= 1
                        ? $"{(int)(te - ts).TotalHours}:{(te - ts).Minutes:D2}:{(te - ts).Seconds:D2}"
                        : $"{(te - ts).Minutes:D2}:{(te - ts).Seconds:D2}",
                    DurationSeconds = (te - ts).TotalSeconds
                });
                startIdx = null;
            }
        }

        if (startIdx.HasValue && startIdx.Value < values.Count - 1)
        {
            var ts = timestamps[startIdx.Value];
            var te = timestamps[^1];
            cycles.Add(new CycleRecord
            {
                StartIndex = startIdx.Value,
                EndIndex = values.Count - 1,
                StartTime = ts.ToString("HH:mm:ss"),
                EndTime = te.ToString("HH:mm:ss"),
                Duration = (te - ts).TotalHours >= 1
                    ? $"{(int)(te - ts).TotalHours}:{(te - ts).Minutes:D2}:{(te - ts).Seconds:D2}"
                    : $"{(te - ts).Minutes:D2}:{(te - ts).Seconds:D2}",
                DurationSeconds = (te - ts).TotalSeconds,
                IsOpen = true
            });
        }

        return cycles;
    }

    [HttpGet("{monitoringId}/progressive-records")]
    public async Task<ActionResult<MonitoringChartResponse>> GetProgressiveRecords(
        int monitoringId,
        [FromQuery] DateTime? fromTime = null,
        [FromQuery] DateTime? toTime = null,
        [FromQuery] int maxPoints = 3000,
        [FromQuery] string? noteIndices = null)
    {
        if (monitoringId <= 0)
            return BadRequest("Invalid monitoringId");

        var connection = await _db.MonitoringReceiptConnections
            .AsNoTracking()
            .Include(x => x.CustomerReceipt).ThenInclude(x => x.Customer)
            .Include(x => x.CustomerReceipt).ThenInclude(x => x.DeviceType)
            .Include(x => x.CustomerReceipt).ThenInclude(x => x.DeviceBrand)
            .Include(x => x.MonitoringDevice)
            .FirstOrDefaultAsync(x => x.Id == monitoringId);

        var query = _db.MonitoringDataRecords
            .Where(x => x.MonitoringId == monitoringId);

        if (fromTime.HasValue)
            query = query.Where(x => x.Timestamp >= fromTime.Value);

        if (toTime.HasValue)
            query = query.Where(x => x.Timestamp <= toTime.Value);

        var totalCount = await query.CountAsync();
        var records = await query
            .OrderBy(x => x.Timestamp)
            .ToListAsync();

        if (records.Count == 0)
            return Ok(new MonitoringChartResponse());

        // Parse client-side note indices (indices within the full records list)
        var clientNotes = new HashSet<int>();
        if (!string.IsNullOrEmpty(noteIndices))
        {
            foreach (var part in noteIndices.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out var idx) && idx >= 0 && idx < records.Count)
                    clientNotes.Add(idx);
            }
        }

        var selectedIndices = SmartDecimate(records, maxPoints, clientNotes);

        var response = new MonitoringChartResponse();
        var timestamps = new List<DateTime>();

        if (connection != null)
        {
            var receipt = connection.CustomerReceipt;
            response.ConnectionInfo = new ChartConnectionInfo
            {
                CustomerName = receipt?.Customer != null
                    ? $"{receipt.Customer.FirstName} {receipt.Customer.LastName}"
                    : "",
                CustomerMobile = receipt?.Customer?.Mobile ?? "",
                DeviceSerial = connection.MonitoringDevice?.DeviceNumber ?? "",
                DeviceName = connection.MonitoringDevice?.Title ?? "",
                DeviceTypeName = receipt?.DeviceType?.Name,
                DeviceBrandName = receipt?.DeviceBrand?.Name
            };
        }

        // Build main data arrays and PointMetadata
        for (var si = 0; si < selectedIndices.Count; si++)
        {
            var idx = selectedIndices[si];
            var r = records[idx];

            response.Labels.Add(r.Timestamp.ToString("HH:mm:ss"));
            response.LabelsFa.Add(r.TimestampFa);
            response.Timestamps.Add(r.Timestamp.ToString("o"));
            response.TemperatureRef.Add(r.TemperatureRef);
            response.TemperatureFreez.Add(r.TemperatureFreez);
            response.Motor.Add(r.MotorState ? 1 : 0);
            response.Power.Add(r.Bargh ? 1 : 0);
            response.Heater1.Add(r.Element1 ? 1 : 0);
            response.Heater2.Add(r.Element2 ? 1 : 0);
            response.Tavan.Add(r.Power);
            response.Jaryan.Add(r.Jaryan);
            timestamps.Add(r.Timestamp);

            // Build PointMeta for this selected point
            var meta = new PointMeta { Index = si, HasNote = clientNotes.Contains(idx) || !string.IsNullOrEmpty(r.Note) };

            if (si > 0)
            {
                var prevIdx = selectedIndices[si - 1];
                var prev = records[prevIdx];

                if (r.MotorState != prev.MotorState ||
                    r.Bargh != prev.Bargh ||
                    r.Element1 != prev.Element1 ||
                    r.Element2 != prev.Element2 ||
                    r.Fdc1 != prev.Fdc1 ||
                    r.Fac1 != prev.Fac1)
                {
                    meta.IsEquipmentTransition = true;
                    // Build a short transition label
                    var parts = new List<string>();
                    if (r.MotorState != prev.MotorState) parts.Add(r.MotorState ? "Motor on" : "Motor off");
                    if (r.Bargh != prev.Bargh) parts.Add(r.Bargh ? "Power on" : "Power off");
                    if (r.Element1 != prev.Element1) parts.Add(r.Element1 ? "Heater1 on" : "Heater1 off");
                    if (r.Element2 != prev.Element2) parts.Add(r.Element2 ? "Heater2 on" : "Heater2 off");
                    if (r.Fdc1 != prev.Fdc1) parts.Add(r.Fdc1 ? "Fdc1 on" : "Fdc1 off");
                    if (r.Fac1 != prev.Fac1) parts.Add(r.Fac1 ? "Fac1 on" : "Fac1 off");
                    meta.TransitionType = string.Join(", ", parts);
                }

                if (r.TemperatureRef.HasValue && prev.TemperatureRef.HasValue)
                    meta.TempChange = r.TemperatureRef.Value - prev.TemperatureRef.Value;

                if (r.Jaryan.HasValue && prev.Jaryan.HasValue)
                    meta.CurrentChange = r.Jaryan.Value - prev.Jaryan.Value;
            }

            response.PointMetadata.Add(meta);
        }

        response.Cycles["motor"] = CalculateCycles(response.Motor, timestamps);
        response.Cycles["heater1"] = CalculateCycles(response.Heater1, timestamps);
        response.Cycles["heater2"] = CalculateCycles(response.Heater2, timestamps);
        response.Cycles["power"] = CalculateCycles(response.Power, timestamps);

        return Ok(response);
    }

    private static List<int> SmartDecimate(List<MonitoringDataRecord> records, int maxPoints, HashSet<int> clientNoteIndices)
    {
        var n = records.Count;
        if (n <= maxPoints || maxPoints <= 0)
            return Enumerable.Range(0, n).ToList();

        var mustKeep = new SortedSet<int>();

        // Always keep first and last
        mustKeep.Add(0);
        mustKeep.Add(n - 1);

        // Priority 1: State transitions (equipment on/off)
        for (int i = 1; i < n; i++)
        {
            if (records[i].MotorState != records[i - 1].MotorState ||
                records[i].Bargh != records[i - 1].Bargh ||
                records[i].Element1 != records[i - 1].Element1 ||
                records[i].Element2 != records[i - 1].Element2 ||
                records[i].Fdc1 != records[i - 1].Fdc1 ||
                records[i].Fac1 != records[i - 1].Fac1)
            {
                mustKeep.Add(i - 1);
                mustKeep.Add(i);
            }
        }

        // Priority 2: Significant Jaryan/Power changes (>10%)
        for (int i = 1; i < n; i++)
        {
            var prevJ = records[i - 1].Jaryan;
            var curJ = records[i].Jaryan;
            if (prevJ.HasValue && curJ.HasValue && prevJ.Value > 0)
            {
                if (Math.Abs((curJ.Value - prevJ.Value) / prevJ.Value) > 0.10)
                    mustKeep.Add(i);
            }

            var prevP = records[i - 1].Power;
            var curP = records[i].Power;
            if (prevP.HasValue && curP.HasValue && prevP.Value > 0)
            {
                if (Math.Abs((curP.Value - prevP.Value) / prevP.Value) > 0.10)
                    mustKeep.Add(i);
            }
        }

        // Priority 3: Temperature changes > 2°C
        float? lastSelectedRef = null, lastSelectedFreez = null;
        for (int i = 0; i < n; i++)
        {
            if (mustKeep.Contains(i))
            {
                lastSelectedRef = records[i].TemperatureRef;
                lastSelectedFreez = records[i].TemperatureFreez;
                continue;
            }

            var r = records[i];
            bool significantTemp = false;

            if (r.TemperatureRef.HasValue && lastSelectedRef.HasValue)
            {
                if (Math.Abs(r.TemperatureRef.Value - lastSelectedRef.Value) > 2f)
                    significantTemp = true;
            }
            if (!significantTemp && r.TemperatureFreez.HasValue && lastSelectedFreez.HasValue)
            {
                if (Math.Abs(r.TemperatureFreez.Value - lastSelectedFreez.Value) > 2f)
                    significantTemp = true;
            }

            if (significantTemp)
            {
                mustKeep.Add(i);
                lastSelectedRef = r.TemperatureRef;
                lastSelectedFreez = r.TemperatureFreez;
            }
        }

        // Priority 4: Records with notes (DB + client-side)
        for (int i = 0; i < n; i++)
        {
            if (!string.IsNullOrEmpty(records[i].Note) || clientNoteIndices.Contains(i))
                mustKeep.Add(i);
        }

        var sorted = mustKeep.OrderBy(x => x).ToList();

        if (sorted.Count > maxPoints)
        {
            // Too many priority points — decimate evenly
            var step = (double)(sorted.Count - 1) / (maxPoints - 1);
            var reduced = new List<int>();
            for (int i = 0; i < maxPoints; i++)
                reduced.Add(sorted[(int)Math.Round(i * step)]);
            return reduced.Distinct().OrderBy(x => x).ToList();
        }

        if (sorted.Count < maxPoints)
        {
            // Budget remaining — fill gaps evenly
            var existing = new HashSet<int>(sorted);
            var gap = (double)(n - 1) / (maxPoints - existing.Count + 1);
            for (int i = 0; i < maxPoints - existing.Count; i++)
            {
                var idx = (int)Math.Round(i * gap);
                if (idx >= 0 && idx < n && existing.Add(idx))
                    sorted.Add(idx);
            }
            sorted.Sort();
        }

        return sorted;
    }
}

public class MonitoringDetailedChartResponse
{
    public List<string> Timestamps { get; set; } = new();
    public List<string> TimestampsFa { get; set; } = new();
    public List<float?> TemperatureRef { get; set; } = new();
    public List<float?> TemperatureFreez { get; set; } = new();
    public List<int> MotorState { get; set; } = new();
    public List<int> Bargh { get; set; } = new();
    public List<int> Element1 { get; set; } = new();
    public List<int> Element2 { get; set; } = new();
    public List<int> Fdc1 { get; set; } = new();
    public List<int> Fac1 { get; set; } = new();
    public List<float?> Jaryan { get; set; } = new();
    public List<float?> Power { get; set; } = new();
    public List<float?> Kw { get; set; } = new();
    public List<float?> SumKw { get; set; } = new();
    public List<long> Ids { get; set; } = new();
    public List<string?> State { get; set; } = new();
    public List<string?> Note { get; set; } = new();
    public int TotalCount { get; set; }
}


