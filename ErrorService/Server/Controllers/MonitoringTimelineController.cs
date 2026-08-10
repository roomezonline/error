using System.Globalization;
using System.Text.Json;
using ErrorService.Server.Data;
using ErrorService.Server.Models;
using ErrorService.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErrorService.Server.Controllers;

[ApiController]
[Route("api/monitoring/chart")]
public class MonitoringTimelineController : ControllerBase
{
    private readonly ErrorServiceDbContext _db;

    public MonitoringTimelineController(ErrorServiceDbContext db)
    {
        _db = db;
    }

    [HttpGet("{monitoringId}/timeline")]
    public async Task<ActionResult<MonitoringTimelineResponse>> GetTimeline(int monitoringId)
    {
        if (monitoringId <= 0)
            return BadRequest("Invalid monitoringId");

        var connection = await _db.MonitoringReceiptConnections
            .Include(c => c.MonitoringDevice)
            .Include(c => c.CustomerReceipt).ThenInclude(cr => cr.Customer)
            .Include(c => c.Workshop)
            .FirstOrDefaultAsync(c => c.Id == monitoringId);

        if (connection == null)
            return NotFound();

        var logs = await _db.MonitoringDeviceChangeLogs
            .Where(x => x.MonitoringId == monitoringId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();

        var startTime = connection.CreatedAt.DateTime;
        var endTime = connection.EndedAt?.DateTime ?? DateTime.UtcNow;
        var totalDuration = endTime - startTime;

        var equipmentTypes = new[] { "Priz", "MotorState", "Element1", "Element2" };

        var tempSnapshots = ExtractTemperatureSnapshots(logs);

        var timelines = new List<EquipmentTimeline>();
        var summaries = new List<EquipmentSummary>();

        foreach (var eqType in equipmentTypes)
        {
            var eqLogs = logs.Where(x => x.ChangeType == eqType).ToList();
            var segments = BuildSegments(eqLogs, startTime, endTime);

            var totalOnTicks = segments
                .Where(s => s.EndTime.HasValue)
                .Sum(s => s.DurationTicks);

            var openSegment = segments.FirstOrDefault(s => !s.EndTime.HasValue);
            if (openSegment != null)
                totalOnTicks += (endTime - openSegment.StartTime).Ticks;

            summaries.Add(new EquipmentSummary
            {
                ChangeType = eqType,
                DisplayName = EquipmentColors.GetDisplayName(eqType),
                Color = EquipmentColors.GetColor(eqType),
                StartCount = segments.Count(s => s.EndTime.HasValue || s.StartTime != startTime),
                TotalDurationTicks = totalOnTicks,
                AvgDurationTicks = segments.Count > 0 ? totalOnTicks / segments.Count : 0,
                Percentage = totalDuration.Ticks > 0
                    ? Math.Round((double)totalOnTicks / totalDuration.Ticks * 100, 1)
                    : 0,
                StateChangeCount = eqLogs.Count
            });

            timelines.Add(new EquipmentTimeline
            {
                ChangeType = eqType,
                DisplayName = EquipmentColors.GetDisplayName(eqType),
                Color = EquipmentColors.GetColor(eqType),
                Segments = segments
            });
        }

        return Ok(new MonitoringTimelineResponse
        {
            DeviceTitle = connection.MonitoringDevice?.Title ?? "",
            DeviceCode = connection.MonitoringDevice?.DeviceNumber ?? "",
            CustomerName = connection.CustomerReceipt?.Customer != null
                ? $"{connection.CustomerReceipt.Customer.FirstName} {connection.CustomerReceipt.Customer.LastName}".Trim()
                : "",
            WorkshopName = connection.Workshop?.WorkshopName ?? "",
            ConnectionStartTime = startTime,
            ConnectionEndTime = connection.EndedAt?.DateTime,
            EquipmentTimelines = timelines,
            TemperatureSnapshots = tempSnapshots,
            Summary = summaries,
            TotalDurationTicks = totalDuration.Ticks,
            TotalChangeLogs = logs.Count
        });
    }

    private static List<TimeSegment> BuildSegments(
        List<MonitoringDeviceChangeLog> eqLogs,
        DateTime startTime,
        DateTime endTime)
    {
        var segments = new List<TimeSegment>();
        if (eqLogs.Count == 0)
            return segments;

        var isOn = false;
        DateTime? segStart = null;

        for (int i = 0; i < eqLogs.Count; i++)
        {
            var log = eqLogs[i];
            var newIsOn = log.NewValue?.Trim().Equals("on", StringComparison.OrdinalIgnoreCase) == true;

            if (i == 0)
            {
                var oldWasOn = log.OldValue?.Trim().Equals("on", StringComparison.OrdinalIgnoreCase) == true;

                if (newIsOn && !oldWasOn)
                {
                    segStart = log.CreatedAt;
                    isOn = true;
                }
                else if (newIsOn && oldWasOn)
                {
                    segStart = startTime;
                    isOn = true;
                }
                else if (!newIsOn && oldWasOn)
                {
                    segStart = startTime;
                    segments.Add(new TimeSegment
                    {
                        StartTime = segStart.Value,
                        EndTime = log.CreatedAt,
                        DurationTicks = (log.CreatedAt - segStart.Value).Ticks
                    });
                    segStart = null;
                    isOn = false;
                }
                continue;
            }

            if (newIsOn && !isOn)
            {
                segStart = log.CreatedAt;
            }
            else if (!newIsOn && isOn && segStart.HasValue)
            {
                segments.Add(new TimeSegment
                {
                    StartTime = segStart.Value,
                    EndTime = log.CreatedAt,
                    DurationTicks = (log.CreatedAt - segStart.Value).Ticks
                });
                segStart = null;
            }

            isOn = newIsOn;
        }

        if (isOn && segStart.HasValue)
        {
            segments.Add(new TimeSegment
            {
                StartTime = segStart.Value,
                EndTime = null,
                DurationTicks = (endTime - segStart.Value).Ticks
            });
        }

        return segments;
    }

    private static List<TempSnapshot> ExtractTemperatureSnapshots(List<MonitoringDeviceChangeLog> logs)
    {
        var snapshots = new List<TempSnapshot>();

        foreach (var log in logs)
        {
            try
            {
                using var doc = JsonDocument.Parse(log.DataSnapshot);
                var root = doc.RootElement;

                double? fridge = null;
                double? freezer = null;

                if (root.TryGetProperty("Temp1", out var t1) && t1.ValueKind == JsonValueKind.String)
                {
                    if (double.TryParse(t1.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var tr))
                        fridge = tr;
                }

                if (root.TryGetProperty("Temp2", out var t2) && t2.ValueKind == JsonValueKind.String)
                {
                    if (double.TryParse(t2.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var tf))
                        freezer = tf;
                }

                if (fridge.HasValue || freezer.HasValue)
                {
                    snapshots.Add(new TempSnapshot
                    {
                        Timestamp = log.CreatedAt,
                        FridgeTemp = fridge,
                        FreezerTemp = freezer
                    });
                }
            }
            catch
            {
            }
        }

        return snapshots;
    }
}
