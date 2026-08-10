using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.Script.Serialization;
using System.Text;
using System.Globalization;
using System.Web.Services;
using System.Web.Script.Services;
using System.IO;

namespace ErrorService.WebForms.admin.ardino
{
    public partial class chart1 : System.Web.UI.Page
    {
        private static readonly object _debugLock = new object();
        private static string DebugLogPath
        {
            get
            {
                try { return HttpContext.Current.Server.MapPath("~/debug_chart_log.txt"); }
                catch { return Path.Combine(Path.GetTempPath(), "debug_chart_log.txt"); }
            }
        }
        private static void LogDebug(string msg)
        {
            try
            {
                lock (_debugLock)
                {
                    string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{System.Threading.Thread.CurrentThread.ManagedThreadId}] {msg}";
                    System.IO.File.AppendAllText(DebugLogPath, line + Environment.NewLine);
                }
            }
            catch { }
        }

        protected override void InitializeCulture()
        {
            // Enforce Persian culture for formatting and UI
            var culture = new CultureInfo("fa-IR");
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
            base.InitializeCulture();
        }

        // LTTB downsampling: returns indices of selected points (including first and last)
        private static List<int> LttbSelectIndices(List<double> x, List<double> y, int threshold)
        {
            var n = (x == null || y == null) ? 0 : Math.Min(x.Count, y.Count);
            if (threshold >= n || threshold <= 0 || n == 0)
            {
                var idxAll = new List<int>(n);
                for (int i = 0; i < n; i++) idxAll.Add(i);
                return idxAll;
            }

            var sampled = new List<int>(threshold);
            int bucketSize = (n - 2) / (threshold - 2);
            if (bucketSize <= 0) bucketSize = 1;

            int a = 0; // always include the first point
            sampled.Add(a);
            int nextA = 0;

            for (int i = 0; i < threshold - 2; i++)
            {
                int start = (i + 1) * bucketSize + 1;
                int end = Math.Min((i + 2) * bucketSize + 1, n - 1);
                if (start >= end) start = Math.Max(start - 1, i + 1);

                // Calculate average for next bucket
                double avgX = 0, avgY = 0; int avgCount = 0;
                for (int j = start; j < end; j++) { avgX += x[j]; avgY += y[j]; avgCount++; }
                if (avgCount > 0) { avgX /= avgCount; avgY /= avgCount; } else { avgX = x[Math.Max(end - 1, 0)]; avgY = y[Math.Max(end - 1, 0)]; }

                // Point a
                double ax = x[a]; double ay = y[a];

                // Find point in this bucket with max triangle area relative to a and avg point
                double maxArea = -1; int maxIndex = start;
                for (int j = start; j < end; j++)
                {
                    double area = Math.Abs((ax - avgX) * (y[j] - ay) - (ay - avgY) * (x[j] - ax));
                    if (area > maxArea)
                    {
                        maxArea = area; maxIndex = j;
                    }
                }

                sampled.Add(maxIndex);
                a = maxIndex;
                nextA = a;
            }

            // Always include the last point
            sampled.Add(n - 1);
            sampled.Sort();
            return sampled;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public static string GetChartDataDecimated(string monitorId, string startJalali, string endJalali, int maxPoints)
        {
            LogDebug($"=== GetChartDataDecimated START: monitorId={monitorId}, startJalali={startJalali}, endJalali={endJalali}, maxPoints={maxPoints}");

            var chartData = new TemperatureChartData
            {
                Timestamps = new List<string>(),
                Temperatures = new List<double>(),
                FreezerTemperatures = new List<double>(),
                MotorStates = new List<int>(),
                PowerStates = new List<int>(),
                Element1States = new List<int>(),
                Element2States = new List<int>(),
                FDCStates = new List<int>(),
                FACStates = new List<int>(),
                CurrentStates = new List<int>(),
                PowerConsumptionStates = new List<int>(),
                CurrentValues = new List<double>(),
                PowerValues = new List<double>(),
                Ids = new List<int>(),
                Notes = new List<string>()
            };

            try
            {
                if (string.IsNullOrWhiteSpace(monitorId)) { LogDebug("FAIL: monitorId is null/empty"); return new JavaScriptSerializer().Serialize(chartData); }
                if (maxPoints <= 0) maxPoints = 1500;

                string sStart = NormalizeJalaliDateTime(startJalali);
                string sEnd = NormalizeJalaliDateTime(endJalali);
                LogDebug($"Normalized dates: sStart='{sStart}', sEnd='{sEnd}'");
                if (string.IsNullOrEmpty(sStart) || string.IsNullOrEmpty(sEnd)) { LogDebug("FAIL: normalized dates empty"); return new JavaScriptSerializer().Serialize(chartData); }

                var dtStart = JalaliStringToDateTime(sStart);
                var dtEnd = JalaliStringToDateTime(sEnd);
                LogDebug($"Parsed Gregorian: dtStart={dtStart?.ToString("yyyy-MM-dd HH:mm:ss")}, dtEnd={dtEnd?.ToString("yyyy-MM-dd HH:mm:ss")}");
                if (!dtStart.HasValue || !dtEnd.HasValue) { LogDebug("FAIL: date parsing failed"); return new JavaScriptSerializer().Serialize(chartData); }

                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
                LogDebug($"ConnectionString found: {(string.IsNullOrEmpty(connectionString) ? "NO!" : "yes")}");
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    LogDebug("DB connection opened OK");

                    string effectiveId = monitorId;
                    using (var countCmd = new SqlCommand("SELECT COUNT(*) FROM MonitoringDataRecords WHERE MonitoringId = @Id", connection))
                    {
                        countCmd.Parameters.AddWithValue("@Id", effectiveId);
                        int cnt0 = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);
                        LogDebug($"Direct count for Id='{effectiveId}': {cnt0}");
                        if (cnt0 == 0)
                        {
                            using (var mapCmd = new SqlCommand(@"SELECT TOP 1 CAST(Id AS NVARCHAR(100))
                                                              FROM tb_create_monitor
                                                              WHERE Request_Id = @Req OR Id = @Req
                                                              ORDER BY Id DESC", connection))
                            {
                                mapCmd.Parameters.AddWithValue("@Req", monitorId);
                                var alt = mapCmd.ExecuteScalar();
                                string altId = (alt == null || alt == DBNull.Value) ? null : alt.ToString();
                                LogDebug($"Mapped ID from tb_create_monitor: '{altId}'");
                                if (!string.IsNullOrWhiteSpace(altId)) effectiveId = altId;
                            }
                        }
                    }
                    LogDebug($"Effective MonitoringId: '{effectiveId}'");

                    string query = @"SELECT Id, Timestamp, TimestampFa, Note, TemperatureRef, TemperatureFreez,
                         CASE WHEN motorstate IS NULL THEN 0 WHEN LOWER(motorstate) = 'off' THEN 0 WHEN LOWER(motorstate) = 'on' THEN 1 WHEN ISNUMERIC(motorstate) = 1 THEN COALESCE(TRY_CONVERT(INT, motorstate), 0) ELSE 0 END AS MotorState,
                         CASE WHEN bargh IS NULL THEN 0 WHEN LOWER(bargh) = 'off' THEN 0 WHEN LOWER(bargh) = 'on' THEN 1 WHEN ISNUMERIC(bargh) = 1 THEN COALESCE(TRY_CONVERT(INT, bargh), 0) ELSE 0 END AS PowerState,
                         CASE WHEN element1 IS NULL THEN 0 WHEN LOWER(element1) = 'off' THEN 0 WHEN LOWER(element1) = 'on' THEN 1 WHEN ISNUMERIC(element1) = 1 THEN COALESCE(TRY_CONVERT(INT, element1), 0) ELSE 0 END AS Element1State,
                         CASE WHEN element2 IS NULL THEN 0 WHEN LOWER(element2) = 'off' THEN 0 WHEN LOWER(element2) = 'on' THEN 1 WHEN ISNUMERIC(element2) = 1 THEN COALESCE(TRY_CONVERT(INT, element2), 0) ELSE 0 END AS Element2State,
                         CASE WHEN fdc1 IS NULL THEN 0 WHEN LOWER(fdc1) = 'off' THEN 0 WHEN LOWER(fdc1) = 'on' THEN 1 WHEN ISNUMERIC(fdc1) = 1 THEN COALESCE(TRY_CONVERT(INT, fdc1), 0) ELSE 0 END AS FDCState,
                         CASE WHEN fac1 IS NULL THEN 0 WHEN LOWER(fac1) = 'off' THEN 0 WHEN LOWER(fac1) = 'on' THEN 1 WHEN ISNUMERIC(fac1) = 1 THEN COALESCE(TRY_CONVERT(INT, fac1), 0) ELSE 0 END AS FACState,
                         CASE WHEN jaryan IS NULL THEN 0 WHEN LOWER(jaryan) = 'off' THEN 0 WHEN LOWER(jaryan) = 'on' THEN 1 WHEN ISNUMERIC(jaryan) = 1 THEN COALESCE(TRY_CONVERT(INT, jaryan), 0) ELSE 0 END AS CurrentState,
                         Jaryan AS CurrentRaw,
                         CASE WHEN power IS NULL THEN 0 WHEN LOWER(power) = 'off' THEN 0 WHEN LOWER(power) = 'on' THEN 1 WHEN ISNUMERIC(power) = 1 THEN COALESCE(TRY_CONVERT(INT, power), 0) ELSE 0 END AS PowerConsumptionState,
                         Power AS PowerRaw
                    FROM MonitoringDataRecords
                    WHERE MonitoringId = @MonitorId
                      AND Timestamp >= @StartDate AND Timestamp <= @EndDate
                    ORDER BY Id ASC";
                    LogDebug($"SQL query prepared (len={query.Length})");

                    var ts = new List<string>();
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
                    var xs = new List<double>();
                    var ids = new List<int>();
                    var notes = new List<string>();

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@MonitorId", effectiveId);
                        cmd.Parameters.AddWithValue("@StartDate", dtStart.Value);
                        cmd.Parameters.AddWithValue("@EndDate", dtEnd.Value);
                        LogDebug($"Executing SQL: @MonitorId='{effectiveId}', @StartDate={dtStart.Value:yyyy-MM-dd HH:mm:ss}, @EndDate={dtEnd.Value:yyyy-MM-dd HH:mm:ss}");
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            DateTime? t0 = null;
                            int rowCount = 0;
                            while (reader.Read())
                            {
                                rowCount++;
                                try
                                {
                                    DateTime tsVal = Convert.ToDateTime(reader["Timestamp"]);
                                    string tsFa = reader["TimestampFa"] == DBNull.Value ? null : reader["TimestampFa"].ToString();
                                    string tsDisplay = FormatJalaliDateTime(tsFa) ?? DateTimeToJalaliString(tsVal);

                                    if (!t0.HasValue) t0 = tsVal;
                                    double x = (tsVal - t0.Value).TotalSeconds;

                                    if (rowCount <= 3)
                                    {
                                        LogDebug($"Row#{rowCount}: tsVal={tsVal:yyyy-MM-dd HH:mm:ss}, tsFa='{tsFa}', tsDisplay='{tsDisplay}', TempRef={reader["TemperatureRef"]}, MotorState={reader["MotorState"]}");
                                    }

                                    ts.Add(tsDisplay);
                                    xs.Add(x);
                                    ids.Add(Convert.ToInt32(reader["Id"]));
                                    notes.Add(reader["Note"] == DBNull.Value ? null : reader["Note"].ToString());
                                    temp.Add(ParseDoubleFlexible(reader["TemperatureRef"].ToString()));
                                    freez.Add(ParseDoubleFlexible(reader["TemperatureFreez"].ToString()));
                                    motor.Add(Convert.ToInt32(reader["MotorState"]));
                                    power.Add(Convert.ToInt32(reader["PowerState"]));
                                    e1.Add(Convert.ToInt32(reader["Element1State"]));
                                    e2.Add(Convert.ToInt32(reader["Element2State"]));
                                    fdc.Add(Convert.ToInt32(reader["FDCState"]));
                                    fac.Add(Convert.ToInt32(reader["FACState"]));
                                    curSt.Add(Convert.ToInt32(reader["CurrentState"]));
                                    powSt.Add(Convert.ToInt32(reader["PowerConsumptionState"]));
                                    curVal.Add(ParseDoubleFlexible(reader["CurrentRaw"].ToString()));
                                    powVal.Add(ParseDoubleFlexible(reader["PowerRaw"].ToString()));
                                }
                                catch (Exception ex_row)
                                {
                                    LogDebug($"Row#{rowCount} ERROR: {ex_row.Message}");
                                    continue;
                                }
                            }
                            LogDebug($"SQL returned {rowCount} rows, successfully parsed {ts.Count}");
                        }
                    }

                    int n = ts.Count;
                    LogDebug($"Total parsed rows={n}");
                    if (n == 0)
                    {
                        LogDebug("FAIL: no data rows parsed, returning empty");
                        return new JavaScriptSerializer().Serialize(chartData);
                    }

                    // 1) اندیس‌های تغییر وضعیت برای سری‌های باینری (حفظ مرزهای 0/1)
                    var transIdx = new SortedSet<int>();
                    void collectTransitions(List<int> s)
                    {
                        if (s == null || s.Count == 0) return;
                        int prev = s[0];
                        for (int i = 1; i < s.Count; i++)
                        {
                            int cur = s[i];
                            if (cur != prev)
                            {
                                transIdx.Add(i - 1);
                                transIdx.Add(i);
                            }
                            prev = cur;
                        }
                    }
                    collectTransitions(motor);
                    collectTransitions(power);
                    collectTransitions(e1);
                    collectTransitions(e2);
                    collectTransitions(fdc);
                    collectTransitions(fac);
                    collectTransitions(curSt);
                    collectTransitions(powSt);

                    // 1.5) اندیس‌های نقاط دارای نوت را همیشه نگه داریم
                    var noteIdx = new SortedSet<int>();
                    for (int i = 0; i < n; i++)
                    {
                        var noteVal = (i >= 0 && i < notes.Count) ? notes[i] : null;
                        if (!string.IsNullOrWhiteSpace(noteVal)) noteIdx.Add(i);
                    }

                    // همیشه اولین و آخرین نقطه را نگه داریم
                    transIdx.Add(0);
                    transIdx.Add(n - 1);

                    // 2) شاخص‌های LTTB برای سری دما
                    List<int> lttb;
                    try { lttb = LttbSelectIndices(xs, temp, maxPoints); }
                    catch { lttb = Enumerable.Range(0, n).ToList(); }

                    // 3) ادغام: ابتدا تمامی نقاط با اهمیت (مرزها + نوت‌ها)، سپس تا سقف از LTTB پر کن
                    var selected = new SortedSet<int>(transIdx);
                    foreach (var ni in noteIdx) selected.Add(ni);
                    if (selected.Count < maxPoints)
                    {
                        foreach (var idx in lttb)
                        {
                            if (selected.Count >= maxPoints) break;
                            selected.Add(idx);
                        }
                    }
                    else if (selected.Count > maxPoints)
                    {
                        // اگر از سقف بیشتر شد: یکنواخت رقیق کنیم ولی 0 و n-1 و تمام نقاط نوت را نگه داریم
                        var keep = new SortedSet<int>();
                        keep.Add(0);
                        keep.Add(n - 1);
                        foreach (var ni in noteIdx) keep.Add(ni);
                        int remaining = Math.Max(0, maxPoints - keep.Count);
                        if (remaining > 0)
                        {
                            var transList = selected.Where(i => i != 0 && i != (n - 1) && !noteIdx.Contains(i)).ToList();
                            if (transList.Count <= remaining)
                            {
                                foreach (var i2 in transList) keep.Add(i2);
                            }
                            else
                            {
                                // انتخاب یکنواخت از بین مرزها
                                double step = (double)transList.Count / remaining;
                                for (int k = 0; k < remaining; k++)
                                {
                                    int pick = transList[(int)Math.Floor(k * step)];
                                    keep.Add(pick);
                                }
                            }
                        }
                        selected = keep;
                    }

                    LogDebug($"Selected {selected.Count} points out of {n} total for output");

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

                string json = new JavaScriptSerializer().Serialize(chartData);
                LogDebug($"GetChartDataDecimated SUCCESS: Timestamps count={chartData.Timestamps.Count}, JSON len={json.Length}");
                return json;
            }
            catch (Exception ex)
            {
                LogDebug($"GetChartDataDecimated EXCEPTION: {ex.Message}\n{ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"GetChartDataDecimated error: {ex.Message}");
                return new JavaScriptSerializer().Serialize(chartData);
            }
        }

        public class ConnectionStatusResponse
        {
            public bool IsOnline { get; set; }
            public string LastTimestamp { get; set; }
            public int RecordCount { get; set; }
            public string FirstTimestamp { get; set; }
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public static string GetConnectionStatus(string monitorId)
        {
            LogDebug($"=== GetConnectionStatus: monitorId='{monitorId}'");

            var resp = new ConnectionStatusResponse { IsOnline = false, LastTimestamp = "-", RecordCount = 0, FirstTimestamp = "-" };
            try
            {
                if (string.IsNullOrWhiteSpace(monitorId)) { LogDebug("FAIL: monitorId empty"); return new JavaScriptSerializer().Serialize(resp); }

                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    LogDebug("DB opened OK");

                    string effectiveId = monitorId;

                    using (var countCmd = new SqlCommand("SELECT COUNT(*) FROM MonitoringDataRecords WHERE MonitoringId = @Id", connection))
                    {
                        countCmd.Parameters.AddWithValue("@Id", effectiveId);
                        object cnt = countCmd.ExecuteScalar();
                        resp.RecordCount = (cnt == null || cnt == DBNull.Value) ? 0 : Convert.ToInt32(cnt);
                        LogDebug($"Direct count for '{effectiveId}': {resp.RecordCount}");
                    }

                    if (resp.RecordCount == 0)
                    {
                        using (var mapCmd = new SqlCommand(@"SELECT TOP 1 CAST(Id AS NVARCHAR(100))
                                                              FROM tb_create_monitor
                                                              WHERE Request_Id = @Req OR Id = @Req
                                                              ORDER BY Id DESC", connection))
                        {
                            mapCmd.Parameters.AddWithValue("@Req", monitorId);
                            var alt = mapCmd.ExecuteScalar();
                            string altId = (alt == null || alt == DBNull.Value) ? null : alt.ToString();
                            LogDebug($"Mapped ID '{altId}' from tb_create_monitor");
                            if (!string.IsNullOrWhiteSpace(altId))
                            {
                                effectiveId = altId;
                                using (var countCmd2 = new SqlCommand("SELECT COUNT(*) FROM MonitoringDataRecords WHERE MonitoringId = @Id", connection))
                                {
                                    countCmd2.Parameters.AddWithValue("@Id", effectiveId);
                                    object cnt2 = countCmd2.ExecuteScalar();
                                    resp.RecordCount = (cnt2 == null || cnt2 == DBNull.Value) ? 0 : Convert.ToInt32(cnt2);
                                    LogDebug($"Count after mapping '{effectiveId}': {resp.RecordCount}");
                                }
                            }
                        }
                    }

                    LogDebug($"Final effectiveId='{effectiveId}', RecordCount={resp.RecordCount}");

                    using (var lastCmd = new SqlCommand("SELECT TOP 1 Timestamp, TimestampFa FROM MonitoringDataRecords WHERE MonitoringId = @Id ORDER BY Id DESC", connection))
                    {
                        lastCmd.Parameters.AddWithValue("@Id", effectiveId);
                        using (var lastReader = lastCmd.ExecuteReader())
                        {
                            if (lastReader.Read())
                            {
                                object rawTs = lastReader["Timestamp"];
                                object rawTsFa = lastReader["TimestampFa"];
                                LogDebug($"Last: raw Timestamp='{rawTs}' (type={rawTs?.GetType()}), TimestampFa='{rawTsFa}'");
                                DateTime ts = Convert.ToDateTime(rawTs);
                                string tsFa = rawTsFa == DBNull.Value ? null : rawTsFa.ToString();
                                resp.LastTimestamp = FormatJalaliDateTime(tsFa) ?? DateTimeToJalaliString(ts);
                                var diff = DateTime.Now - ts;
                                resp.IsOnline = diff.TotalMinutes < 2.0;
                                LogDebug($"Last: Jalali='{resp.LastTimestamp}', IsOnline={resp.IsOnline}, diffMin={diff.TotalMinutes:F1}");
                            }
                            else
                            {
                                LogDebug("Last: no rows found (empty for last)");
                            }
                        }
                    }

                    using (var firstCmd = new SqlCommand("SELECT TOP 1 Timestamp, TimestampFa FROM MonitoringDataRecords WHERE MonitoringId = @Id ORDER BY Id ASC", connection))
                    {
                        firstCmd.Parameters.AddWithValue("@Id", effectiveId);
                        using (var firstReader = firstCmd.ExecuteReader())
                        {
                            if (firstReader.Read())
                            {
                                object rawTs = firstReader["Timestamp"];
                                object rawTsFa = firstReader["TimestampFa"];
                                LogDebug($"First: raw Timestamp='{rawTs}' (type={rawTs?.GetType()}), TimestampFa='{rawTsFa}'");
                                DateTime ts = Convert.ToDateTime(rawTs);
                                string tsFa = rawTsFa == DBNull.Value ? null : rawTsFa.ToString();
                                resp.FirstTimestamp = FormatJalaliDateTime(tsFa) ?? DateTimeToJalaliString(ts);
                                LogDebug($"First: Jalali='{resp.FirstTimestamp}'");
                            }
                            else
                            {
                                LogDebug("First: no rows found (empty for first)");
                                resp.FirstTimestamp = "-";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"GetConnectionStatus EXCEPTION: {ex.Message}\n{ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"GetConnectionStatus error: {ex.Message}");
            }

            string jsonResp = new JavaScriptSerializer().Serialize(resp);
            LogDebug($"GetConnectionStatus response: {jsonResp}");
            return jsonResp;
        }

        // Convert zero-padded Jalali datetime string (YYYY/MM/DD HH:mm:ss) to Gregorian DateTime using PersianCalendar
        private static DateTime? JalaliStringToDateTime(string jalali)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jalali)) return null;
                var parts = jalali.Trim().Split(new[] { ' ', 'T' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return null;
                var date = parts[0];
                var time = (parts.Length > 1 ? parts[1] : "00:00:00");
                var d = date.Split('/', '-');
                if (d.Length < 3) return null;
                int jy = int.Parse(d[0]);
                int jm = int.Parse(d[1]);
                int jd = int.Parse(d[2]);
                var t = time.Split(':');
                int hh = t.Length > 0 ? int.Parse(t[0]) : 0;
                int mm = t.Length > 1 ? int.Parse(t[1]) : 0;
                int ss = t.Length > 2 ? int.Parse(t[2]) : 0;
                var pc = new PersianCalendar();
                var dt = new DateTime(pc.ToDateTime(jy, jm, jd, hh, mm, ss, 0).Ticks, DateTimeKind.Local);
                return dt;
            }
            catch { return null; }
        }

        public class EquipStatus
        {
            public string CurrentStatus { get; set; } // "On" / "Off"
            public string LastStart { get; set; }      // Jalali string
            public long StopBeforeLastStartSeconds { get; set; } // seconds; 0 if N/A
        }

        private static EquipStatus ComputeStatusFromSeries(List<string> timestamps, List<int> states)
        {
            var result = new EquipStatus { CurrentStatus = "-", LastStart = "-", StopBeforeLastStartSeconds = 0 };
            if (timestamps == null || states == null || timestamps.Count == 0 || states.Count != timestamps.Count)
                return result;

            int n = states.Count;
            bool currentOn = states[n - 1] == 1;
            result.CurrentStatus = currentOn ? "On" : "Off";

            int startIdx = -1;
            for (int i = n - 1; i > 0; i--)
            {
                if (states[i] == 1 && states[i - 1] == 0) { startIdx = i; break; }
            }
            if (startIdx == -1 && currentOn) startIdx = 0;

            if (startIdx >= 0)
            {
                result.LastStart = timestamps[startIdx] ?? "-";

                if (startIdx > 0)
                {
                    int offEnd = startIdx - 1; // this index OFF
                    int j = offEnd;
                    while (j > 0 && states[j - 1] == 0) j--;
                    var offStartJalali = timestamps[j];
                    var startJalali = timestamps[startIdx];
                    var t1 = JalaliStringToDateTime(offStartJalali);
                    var t2 = JalaliStringToDateTime(startJalali);
                    if (t1.HasValue && t2.HasValue && t2.Value >= t1.Value)
                    {
                        result.StopBeforeLastStartSeconds = (long)(t2.Value - t1.Value).TotalSeconds;
                    }
                }
            }

            return result;
        }

        public class EquipmentStatusResponse
        {
            public EquipStatus Motor { get; set; }
            public EquipStatus Heater1 { get; set; }
            public EquipStatus Heater2 { get; set; }
        }

        public class MonitorHeaderInfo
        {
            public string CustomerName { get; set; }
            public string CustomerMobile { get; set; }
            public string DeviceTitle { get; set; }
            public string DeviceNumber { get; set; }
            public string WorkshopName { get; set; }
            public string ProblemDescription { get; set; }
            public int ReceiptId { get; set; }
            public string CreatedAtFa { get; set; }
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public static string GetMonitorHeaderInfo(string monitorId)
        {
            var info = new MonitorHeaderInfo
            {
                CustomerName = "-",
                CustomerMobile = "-",
                DeviceTitle = "-",
                DeviceNumber = "-",
                WorkshopName = "-",
                ProblemDescription = "-",
                ReceiptId = 0,
                CreatedAtFa = "-"
            };
            try
            {
                if (string.IsNullOrWhiteSpace(monitorId)) return new JavaScriptSerializer().Serialize(info);

                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string effectiveId = monitorId;
                    bool found = false;

                    string query = @"SELECT
                        ISNULL(wc.FirstName + ' ' + wc.LastName, '') AS CustomerName,
                        ISNULL(wc.Mobile, '') AS CustomerMobile,
                        ISNULL(md.Title, '') AS DeviceTitle,
                        ISNULL(md.DeviceNumber, '') AS DeviceNumber,
                        ISNULL(w.WorkshopName, '') AS WorkshopName,
                        ISNULL(cr.ProblemDescription, '') AS ProblemDescription,
                        mrc.CustomerReceiptId,
                        mrc.CreatedAt
                    FROM MonitoringReceiptConnections mrc
                    JOIN MonitoringDevices md ON mrc.MonitoringDeviceId = md.Id
                    JOIN CustomerReceipts cr ON mrc.CustomerReceiptId = cr.Id
                    JOIN Workshops w ON mrc.WorkshopId = w.Id
                    JOIN WorkshopCustomers wc ON cr.CustomerId = wc.Id
                    WHERE mrc.Id = @Id";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Id", effectiveId);
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                found = true;
                                info.CustomerName = (reader["CustomerName"]?.ToString() ?? "-").Trim();
                                info.CustomerMobile = (reader["CustomerMobile"]?.ToString() ?? "-").Trim();
                                info.DeviceTitle = (reader["DeviceTitle"]?.ToString() ?? "-").Trim();
                                info.DeviceNumber = (reader["DeviceNumber"]?.ToString() ?? "-").Trim();
                                info.WorkshopName = (reader["WorkshopName"]?.ToString() ?? "-").Trim();
                                info.ProblemDescription = (reader["ProblemDescription"]?.ToString() ?? "-").Trim();
                                info.ReceiptId = Convert.ToInt32(reader["CustomerReceiptId"]);
                                if (reader["CreatedAt"] != DBNull.Value)
                                {
                                    var cal = new PersianCalendar();
                                    var dt = (DateTimeOffset)reader["CreatedAt"];
                                    info.CreatedAtFa = $"{cal.GetYear(dt.DateTime)}/{cal.GetMonth(dt.DateTime):00}/{cal.GetDayOfMonth(dt.DateTime):00}";
                                }
                            }
                        }
                    }

                    if (!found)
                    {
                        using (var mapCmd = new SqlCommand(@"SELECT TOP 1 CAST(Id AS NVARCHAR(100))
                                                              FROM tb_create_monitor
                                                              WHERE Request_Id = @Req OR Id = @Req
                                                              ORDER BY Id DESC", connection))
                        {
                            mapCmd.Parameters.AddWithValue("@Req", monitorId);
                            var alt = mapCmd.ExecuteScalar();
                            string altId = (alt == null || alt == DBNull.Value) ? null : alt.ToString();
                            if (!string.IsNullOrWhiteSpace(altId))
                            {
                                using (SqlCommand cmd2 = new SqlCommand(query, connection))
                                {
                                    cmd2.Parameters.AddWithValue("@Id", altId);
                                    using (SqlDataReader reader2 = cmd2.ExecuteReader())
                                    {
                                        if (reader2.Read())
                                        {
                                            info.CustomerName = (reader2["CustomerName"]?.ToString() ?? "-").Trim();
                                            info.CustomerMobile = (reader2["CustomerMobile"]?.ToString() ?? "-").Trim();
                                            info.DeviceTitle = (reader2["DeviceTitle"]?.ToString() ?? "-").Trim();
                                            info.DeviceNumber = (reader2["DeviceNumber"]?.ToString() ?? "-").Trim();
                                            info.WorkshopName = (reader2["WorkshopName"]?.ToString() ?? "-").Trim();
                                            info.ProblemDescription = (reader2["ProblemDescription"]?.ToString() ?? "-").Trim();
                                            info.ReceiptId = Convert.ToInt32(reader2["CustomerReceiptId"]);
                                            if (reader2["CreatedAt"] != DBNull.Value)
                                            {
                                                var cal = new PersianCalendar();
                                                var dt = (DateTimeOffset)reader2["CreatedAt"];
                                                info.CreatedAtFa = $"{cal.GetYear(dt.DateTime)}/{cal.GetMonth(dt.DateTime):00}/{cal.GetDayOfMonth(dt.DateTime):00}";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetMonitorHeaderInfo error: {ex.Message}");
            }
            return new JavaScriptSerializer().Serialize(info);
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public static string GetEquipmentStatus(string monitorId)
        {
            var resp = new EquipmentStatusResponse
            {
                Motor = new EquipStatus { CurrentStatus = "-", LastStart = "-", StopBeforeLastStartSeconds = 0 },
                Heater1 = new EquipStatus { CurrentStatus = "-", LastStart = "-", StopBeforeLastStartSeconds = 0 },
                Heater2 = new EquipStatus { CurrentStatus = "-", LastStart = "-", StopBeforeLastStartSeconds = 0 }
            };

            try
            {
                if (string.IsNullOrWhiteSpace(monitorId)) return new JavaScriptSerializer().Serialize(resp);

                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = @"SELECT        TOP (100) PERCENT Timestamp, CASE WHEN motorstate IS NULL THEN 0 WHEN LOWER(motorstate) = 'off' THEN 0 WHEN LOWER(motorstate) = 'on' THEN 1 WHEN ISNUMERIC(motorstate) 
                         = 1 THEN COALESCE (TRY_CONVERT(INT, motorstate), 0) ELSE 0 END AS MotorState, CASE WHEN element1 IS NULL THEN 0 WHEN LOWER(element1) = 'off' THEN 0 WHEN LOWER(element1) 
                         = 'on' THEN 1 WHEN ISNUMERIC(element1) = 1 THEN COALESCE (TRY_CONVERT(INT, element1), 0) ELSE 0 END AS Element1State, CASE WHEN element2 IS NULL THEN 0 WHEN LOWER(element2) 
                         = 'off' THEN 0 WHEN LOWER(element2) = 'on' THEN 1 WHEN ISNUMERIC(element2) = 1 THEN COALESCE (TRY_CONVERT(INT, element2), 0) ELSE 0 END AS Element2State
FROM            MonitoringDataRecords
WHERE        (MonitoringId = @MonitorId)
ORDER BY Id DESC";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MonitorId", monitorId);
                        connection.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            var timestamps = new List<string>();
                            var motor = new List<int>();
                            var e1 = new List<int>();
                            var e2 = new List<int>();
                            while (reader.Read())
                            {
                                string ts = reader["Timestamp"].ToString();
                                if (string.IsNullOrWhiteSpace(ts) || !IsValidJalaliDate(ts)) continue;
                                ts = FormatJalaliDateTime(ts);
                                timestamps.Add(ts);
                                motor.Add(Convert.ToInt32(reader["MotorState"]));
                                e1.Add(Convert.ToInt32(reader["Element1State"]));
                                e2.Add(Convert.ToInt32(reader["Element2State"]));
                            }

                            // Ensure chronological ascending order for correct transition detection
                            // If first timestamp is later than last, the list is descending -> reverse all series
                            if (timestamps.Count > 1 && string.Compare(timestamps[0], timestamps[timestamps.Count - 1], StringComparison.Ordinal) > 0)
                            {
                                timestamps.Reverse();
                                motor.Reverse();
                                e1.Reverse();
                                e2.Reverse();
                            }

                            resp.Motor = ComputeStatusFromSeries(timestamps, motor);
                            resp.Heater1 = ComputeStatusFromSeries(timestamps, e1);
                            resp.Heater2 = ComputeStatusFromSeries(timestamps, e2);
                        }
                    }
                }

                return new JavaScriptSerializer().Serialize(resp);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetEquipmentStatus error: {ex.Message}");
                return new JavaScriptSerializer().Serialize(resp);
            }
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public static string GetLatestChartData(string monitorId, int take = 1000)
        {
            var chartData = new TemperatureChartData
            {
                Timestamps = new List<string>(),
                Temperatures = new List<double>(),
                FreezerTemperatures = new List<double>(),
                MotorStates = new List<int>(),
                PowerStates = new List<int>(),
                Element1States = new List<int>(),
                Element2States = new List<int>(),
                FDCStates = new List<int>(),
                FACStates = new List<int>(),
                CurrentStates = new List<int>(),
                PowerConsumptionStates = new List<int>(),
                CurrentValues = new List<double>(),
                PowerValues = new List<double>(),
                Ids = new List<int>(),
                Notes = new List<string>()
            };

            try
            {
                if (string.IsNullOrWhiteSpace(monitorId)) return new JavaScriptSerializer().Serialize(chartData);

                if (take <= 0) take = 1000;

                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string effectiveId = monitorId;
                    using (var countCmd = new SqlCommand("SELECT COUNT(*) FROM MonitoringDataRecords WHERE MonitoringId = @Id", connection))
                    {
                        countCmd.Parameters.AddWithValue("@Id", effectiveId);
                        var cntObj = countCmd.ExecuteScalar();
                        int cnt = (cntObj == null || cntObj == DBNull.Value) ? 0 : Convert.ToInt32(cntObj);
                        if (cnt == 0)
                        {
                            using (var mapCmd = new SqlCommand(@"SELECT TOP 1 CAST(Id AS NVARCHAR(100))
                                                              FROM tb_create_monitor
                                                              WHERE Request_Id = @Req OR Id = @Req
                                                              ORDER BY Id DESC", connection))
                            {
                                mapCmd.Parameters.AddWithValue("@Req", monitorId);
                                var alt = mapCmd.ExecuteScalar();
                                string altId = (alt == null || alt == DBNull.Value) ? null : alt.ToString();
                                if (!string.IsNullOrWhiteSpace(altId))
                                {
                                    effectiveId = altId;
                                }
                            }
                        }
                    }

                    string query = @"SELECT TOP (@Take) Id, Timestamp, TimestampFa, Note, TemperatureRef, TemperatureFreez,
                         CASE WHEN motorstate IS NULL THEN 0 WHEN LOWER(motorstate) = 'off' THEN 0 WHEN LOWER(motorstate) = 'on' THEN 1 WHEN ISNUMERIC(motorstate) = 1 THEN COALESCE(TRY_CONVERT(INT, motorstate), 0) ELSE 0 END AS MotorState,
                         CASE WHEN bargh IS NULL THEN 0 WHEN LOWER(bargh) = 'off' THEN 0 WHEN LOWER(bargh) = 'on' THEN 1 WHEN ISNUMERIC(bargh) = 1 THEN COALESCE(TRY_CONVERT(INT, bargh), 0) ELSE 0 END AS PowerState,
                         CASE WHEN element1 IS NULL THEN 0 WHEN LOWER(element1) = 'off' THEN 0 WHEN LOWER(element1) = 'on' THEN 1 WHEN ISNUMERIC(element1) = 1 THEN COALESCE(TRY_CONVERT(INT, element1), 0) ELSE 0 END AS Element1State,
                         CASE WHEN element2 IS NULL THEN 0 WHEN LOWER(element2) = 'off' THEN 0 WHEN LOWER(element2) = 'on' THEN 1 WHEN ISNUMERIC(element2) = 1 THEN COALESCE(TRY_CONVERT(INT, element2), 0) ELSE 0 END AS Element2State,
                         CASE WHEN fdc1 IS NULL THEN 0 WHEN LOWER(fdc1) = 'off' THEN 0 WHEN LOWER(fdc1) = 'on' THEN 1 WHEN ISNUMERIC(fdc1) = 1 THEN COALESCE(TRY_CONVERT(INT, fdc1), 0) ELSE 0 END AS FDCState,
                         CASE WHEN fac1 IS NULL THEN 0 WHEN LOWER(fac1) = 'off' THEN 0 WHEN LOWER(fac1) = 'on' THEN 1 WHEN ISNUMERIC(fac1) = 1 THEN COALESCE(TRY_CONVERT(INT, fac1), 0) ELSE 0 END AS FACState,
                         CASE WHEN jaryan IS NULL THEN 0 WHEN LOWER(jaryan) = 'off' THEN 0 WHEN LOWER(jaryan) = 'on' THEN 1 WHEN ISNUMERIC(jaryan) = 1 THEN COALESCE(TRY_CONVERT(INT, jaryan), 0) ELSE 0 END AS CurrentState,
                         Jaryan AS CurrentRaw,
                         CASE WHEN power IS NULL THEN 0 WHEN LOWER(power) = 'off' THEN 0 WHEN LOWER(power) = 'on' THEN 1 WHEN ISNUMERIC(power) = 1 THEN COALESCE(TRY_CONVERT(INT, power), 0) ELSE 0 END AS PowerConsumptionState,
                         Power AS PowerRaw
                    FROM MonitoringDataRecords
                    WHERE MonitoringId = @MonitorId
                    ORDER BY Id DESC";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MonitorId", effectiveId);
                        command.Parameters.AddWithValue("@Take", take);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                try
                                {
                                    DateTime tsVal = Convert.ToDateTime(reader["Timestamp"]);
                                    string tsFa = reader["TimestampFa"] == DBNull.Value ? null : reader["TimestampFa"].ToString();
                                    string formattedTimestamp = FormatJalaliDateTime(tsFa) ?? DateTimeToJalaliString(tsVal);

                                    double temperature = ParseDoubleFlexible(reader["TemperatureRef"].ToString());
                                    double freezerTemp = ParseDoubleFlexible(reader["TemperatureFreez"].ToString());

                                    chartData.Timestamps.Add(formattedTimestamp);
                                    chartData.Temperatures.Add(temperature);
                                    chartData.FreezerTemperatures.Add(freezerTemp);
                                    chartData.MotorStates.Add(Convert.ToInt32(reader["MotorState"]));
                                    chartData.PowerStates.Add(Convert.ToInt32(reader["PowerState"]));
                                    chartData.Element1States.Add(Convert.ToInt32(reader["Element1State"]));
                                    chartData.Element2States.Add(Convert.ToInt32(reader["Element2State"]));
                                    chartData.FDCStates.Add(Convert.ToInt32(reader["FDCState"]));
                                    chartData.FACStates.Add(Convert.ToInt32(reader["FACState"]));
                                    chartData.CurrentStates.Add(Convert.ToInt32(reader["CurrentState"]));
                                    chartData.PowerConsumptionStates.Add(Convert.ToInt32(reader["PowerConsumptionState"]));

                                    double currentVal = ParseDoubleFlexible(reader["CurrentRaw"].ToString());
                                    double powerVal = ParseDoubleFlexible(reader["PowerRaw"].ToString());
                                    chartData.CurrentValues.Add(currentVal);
                                    chartData.PowerValues.Add(powerVal);
                                    chartData.Ids.Add(Convert.ToInt32(reader["Id"]));
                                    chartData.Notes.Add(reader["Note"] == DBNull.Value ? null : reader["Note"].ToString());
                                }
                                catch { continue; }
                            }
                        }
                    }
                }

                // چون پرس‌وجو نزولی است، برای نمایش صحیح در نمودار، لیست‌ها را برعکس می‌کنیم
                chartData.Timestamps.Reverse();
                chartData.Temperatures.Reverse();
                chartData.FreezerTemperatures.Reverse();
                chartData.MotorStates.Reverse();
                chartData.PowerStates.Reverse();
                chartData.Element1States.Reverse();
                chartData.Element2States.Reverse();
                chartData.FDCStates.Reverse();
                chartData.FACStates.Reverse();
                chartData.CurrentStates.Reverse();
                chartData.PowerConsumptionStates.Reverse();
                chartData.CurrentValues.Reverse();
                chartData.PowerValues.Reverse();

                var serializer = new JavaScriptSerializer();
                return serializer.Serialize(chartData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetLatestChartData error: {ex.Message}");
                return new JavaScriptSerializer().Serialize(chartData);
            }
        }

        // نرمال‌سازی و تبدیل رشته عددی با ارقام فارسی/عربی و جداکننده‌های متفاوت اعشار به عدد اعشاری
        private static double ParseDoubleFlexible(string input)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input)) return 0d;

                string s = input.Trim();

                // جایگزینی ارقام فارسی و عربی به لاتین
                char[] persianDigits = new[] { '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' };
                char[] arabicIndicDigits = new[] { '٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩' };
                for (int i = 0; i <= 9; i++)
                {
                    s = s.Replace(persianDigits[i], (char)('0' + i));
                    s = s.Replace(arabicIndicDigits[i], (char)('0' + i));
                }

                // حذف جداکننده هزارگان رایج و یکسان‌سازی جداکننده اعشار
                s = s.Replace("٬", "");   // Arabic thousands separator
                s = s.Replace("٫", ".");  // Arabic decimal separator to dot
                s = s.Replace("،", ".");  // Arabic comma to dot

                // اگر هم کاما و هم نقطه وجود دارد، فرض کنیم کاما هزارگان است
                if (s.Contains(",") && s.Contains("."))
                {
                    s = s.Replace(",", "");
                }
                else
                {
                    // در غیر اینصورت کاما را به نقطه تبدیل می‌کنیم تا InvariantCulture کار کند
                    s = s.Replace(",", ".");
                }

                // حذف حروف و کاراکترهای غیرعددی/غیرمرتبط (مانند A, W, آمپر, وات و ...)
                // ابتدا با Regex اولین عدد اعشاری/صحیح را استخراج می‌کنیم
                var match = System.Text.RegularExpressions.Regex.Match(s, @"[-+]?\d*(?:\.\d+)?");
                if (match.Success && !string.IsNullOrWhiteSpace(match.Value))
                {
                    string num = match.Value;
                    if (double.TryParse(num, System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowLeadingSign,
                        System.Globalization.CultureInfo.InvariantCulture, out double value1))
                    {
                        return value1;
                    }
                }

                // اگر هیچ عدد معتبری پیدا نشد، صفر برگردان
                return 0d;
            }
            catch
            {
                return 0d;
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            // Force UTF-8 output regardless of server defaults
            Response.Charset = "utf-8";
            Response.ContentEncoding = Encoding.UTF8;
            Response.ContentType = "text/html; charset=utf-8";

            if (!IsPostBack)
            {
                // Check for csvKey mode (offline chart from uploaded CSV)
                var csvKey = Request.QueryString["csvKey"];
                string csvKeyError = null;
                if (!string.IsNullOrEmpty(csvKey))
                {
                    try
                    {
                        string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
                        using (var conn = new System.Data.SqlClient.SqlConnection(connectionString))
                        {
                            conn.Open();
                            using (var cmd = new System.Data.SqlClient.SqlCommand("SELECT [Data] FROM CachedCsvData WHERE [Key] = @Key", conn))
                            {
                                cmd.Parameters.AddWithValue("@Key", csvKey);
                                var raw = cmd.ExecuteScalar() as string;
                                if (!string.IsNullOrEmpty(raw))
                                {
                                    var serializer = new JavaScriptSerializer();
                                    var entry = serializer.Deserialize<CsvCacheEntryDto>(raw);
                                    if (entry?.Records != null && entry.Records.Count > 0)
                                    {
                                        var records = entry.Records;
                                        var chartData = new TemperatureChartData
                                        {
                                            Timestamps = records.Select(r => r.Timestamp).ToList(),
                                            Temperatures = records.Select(r => (double)(r.TemperatureRef ?? 0)).ToList(),
                                            FreezerTemperatures = records.Select(r => (double)(r.TemperatureFreez ?? 0)).ToList(),
                                            MotorStates = records.Select(r => r.MotorState ? 1 : 0).ToList(),
                                            PowerStates = records.Select(r => r.Bargh ? 1 : 0).ToList(),
                                            Element1States = records.Select(r => r.Element1 ? 1 : 0).ToList(),
                                            Element2States = records.Select(r => r.Element2 ? 1 : 0).ToList(),
                                            FDCStates = records.Select(r => r.Fdc1 ? 1 : 0).ToList(),
                                            FACStates = records.Select(r => r.Fac1 ? 1 : 0).ToList(),
                                            CurrentStates = records.Select(r => (int)(r.Jaryan ?? 0)).ToList(),
                                            PowerConsumptionStates = records.Select(r => (int)(r.Power ?? 0)).ToList(),
                                            CurrentValues = records.Select(r => (double)(r.Jaryan ?? 0)).ToList(),
                                            PowerValues = records.Select(r => (double)(r.Power ?? 0)).ToList(),
                                            Ids = records.Select(r => (int)r.Id).ToList(),
                                            Notes = records.Select(r => r.Note ?? "").ToList()
                                        };
                                        SerializeChartData(chartData);

                                        var firstTs = records.First().TimestampFa ?? "";
                                        var lastTs = records.Last().TimestampFa ?? "";
                                        var headerJson = serializer.Serialize(new
                                        {
                                            entry.ConnectionId,
                                            entry.CustomerName,
                                            entry.CustomerMobile,
                                            entry.MonitoringDeviceTitle,
                                            entry.MonitoringDeviceNumber,
                                            RecordCount = records.Count,
                                            FirstTimestampFa = firstTs,
                                            LastTimestampFa = lastTs
                                        });

                                        string startupScript = "window.isCsvMode = true;\n";
                                        startupScript += "window.csvMetaData = " + headerJson + ";\n";
                                        ClientScript.RegisterStartupScript(this.GetType(), "csvModeInit", startupScript, true);
                                    }
                                    else
                                    {
                                        csvKeyError = "داده‌ای در فایل CSV یافت نشد";
                                    }
                                }
                                else
                                {
                                    csvKeyError = "داده‌ای در حافظه موقت یافت نشد. لطفاً دوباره فایل CSV را بارگذاری کنید.";
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        csvKeyError = "خطا در خواندن داده: " + ex.Message;
                    }

                    if (csvKeyError != null)
                    {
                        string errorScript = "window.csvLoadError = " + new JavaScriptSerializer().Serialize(csvKeyError) + ";\n";
                        ClientScript.RegisterStartupScript(this.GetType(), "csvModeError", errorScript, true);
                    }

                    return;
                }

                // Always start with empty data; frontend will request range via PageMethods
                var emptyData = new TemperatureChartData
                {
                    Timestamps = new List<string>(),
                    Temperatures = new List<double>(),
                    FreezerTemperatures = new List<double>(),
                    MotorStates = new List<int>(),
                    PowerStates = new List<int>(),
                    Element1States = new List<int>(),
                    Element2States = new List<int>(),
                    FDCStates = new List<int>(),
                    FACStates = new List<int>(),
                    CurrentStates = new List<int>(),
                    PowerConsumptionStates = new List<int>(),
                    CurrentValues = new List<double>(),
                    PowerValues = new List<double>()
                };
                SerializeChartData(emptyData);
            }
        }

        // Normalize Jalali datetime to zero-padded "YYYY/MM/DD HH:mm:ss"
        private static string NormalizeJalaliDateTime(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            var parts = System.Text.RegularExpressions.Regex.Split(s, "[T\\s]+");
            var datePart = parts.Length > 0 ? parts[0] : "";
            var timePart = parts.Length > 1 ? parts[1] : "00:00:00";
            var d = System.Text.RegularExpressions.Regex.Split(datePart, "[-/]");
            if (d.Length < 3) return null;
            int y = int.Parse(d[0]); int m = int.Parse(d[1]); int ddd = int.Parse(d[2]);
            var t = timePart.Split(':');
            int hh = t.Length > 0 ? int.Parse(t[0]) : 0;
            int mm = t.Length > 1 ? int.Parse(t[1]) : 0;
            int ss = t.Length > 2 ? int.Parse(t[2]) : 0;
            return string.Format(CultureInfo.InvariantCulture, "{0:0000}/{1:00}/{2:00} {3:00}:{4:00}:{5:00}", y, m, ddd, hh, mm, ss);
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public static string GetChartData(string monitorId, string startJalali, string endJalali)
        {
            // Prepare result container
            var chartData = new TemperatureChartData
            {
                Timestamps = new List<string>(),
                Temperatures = new List<double>(),
                FreezerTemperatures = new List<double>(),
                MotorStates = new List<int>(),
                PowerStates = new List<int>(),
                Element1States = new List<int>(),
                Element2States = new List<int>(),
                FDCStates = new List<int>(),
                FACStates = new List<int>(),
                CurrentStates = new List<int>(),
                PowerConsumptionStates = new List<int>(),
                CurrentValues = new List<double>(),
                PowerValues = new List<double>()
            };

            try
            {
                if (string.IsNullOrWhiteSpace(monitorId)) return new JavaScriptSerializer().Serialize(chartData);

                string sStart = NormalizeJalaliDateTime(startJalali);
                string sEnd = NormalizeJalaliDateTime(endJalali);
                if (string.IsNullOrEmpty(sStart) || string.IsNullOrEmpty(sEnd)) return new JavaScriptSerializer().Serialize(chartData);

                var dtStart = JalaliStringToDateTime(sStart);
                var dtEnd = JalaliStringToDateTime(sEnd);
                if (!dtStart.HasValue || !dtEnd.HasValue) return new JavaScriptSerializer().Serialize(chartData);

                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Resolve effective MonitoringId (same logic as GetLatestChartData)
                    string effectiveId = monitorId;
                    using (var countCmd = new SqlCommand("SELECT COUNT(*) FROM MonitoringDataRecords WHERE MonitoringId = @Id", connection))
                    {
                        countCmd.Parameters.AddWithValue("@Id", effectiveId);
                        var cntObj = countCmd.ExecuteScalar();
                        int cnt = (cntObj == null || cntObj == DBNull.Value) ? 0 : Convert.ToInt32(cntObj);
                        if (cnt == 0)
                        {
                            using (var mapCmd = new SqlCommand(@"SELECT TOP 1 CAST(Id AS NVARCHAR(100))
                                                              FROM tb_create_monitor
                                                              WHERE Request_Id = @Req OR Id = @Req
                                                              ORDER BY Id DESC", connection))
                            {
                                mapCmd.Parameters.AddWithValue("@Req", monitorId);
                                var alt = mapCmd.ExecuteScalar();
                                string altId = (alt == null || alt == DBNull.Value) ? null : alt.ToString();
                                if (!string.IsNullOrWhiteSpace(altId))
                                {
                                    effectiveId = altId;
                                }
                            }
                        }
                    }

                    string query = @"SELECT Id, Note, Timestamp, TimestampFa, TemperatureRef, TemperatureFreez,
                         CASE WHEN motorstate IS NULL THEN 0 WHEN LOWER(motorstate) = 'off' THEN 0 WHEN LOWER(motorstate) = 'on' THEN 1 WHEN ISNUMERIC(motorstate) = 1 THEN COALESCE(TRY_CONVERT(INT, motorstate), 0) ELSE 0 END AS MotorState,
                         CASE WHEN bargh IS NULL THEN 0 WHEN LOWER(bargh) = 'off' THEN 0 WHEN LOWER(bargh) = 'on' THEN 1 WHEN ISNUMERIC(bargh) = 1 THEN COALESCE(TRY_CONVERT(INT, bargh), 0) ELSE 0 END AS PowerState,
                         CASE WHEN element1 IS NULL THEN 0 WHEN LOWER(element1) = 'off' THEN 0 WHEN LOWER(element1) = 'on' THEN 1 WHEN ISNUMERIC(element1) = 1 THEN COALESCE(TRY_CONVERT(INT, element1), 0) ELSE 0 END AS Element1State,
                         CASE WHEN element2 IS NULL THEN 0 WHEN LOWER(element2) = 'off' THEN 0 WHEN LOWER(element2) = 'on' THEN 1 WHEN ISNUMERIC(element2) = 1 THEN COALESCE(TRY_CONVERT(INT, element2), 0) ELSE 0 END AS Element2State,
                         CASE WHEN fdc1 IS NULL THEN 0 WHEN LOWER(fdc1) = 'off' THEN 0 WHEN LOWER(fdc1) = 'on' THEN 1 WHEN ISNUMERIC(fdc1) = 1 THEN COALESCE(TRY_CONVERT(INT, fdc1), 0) ELSE 0 END AS FDCState,
                         CASE WHEN fac1 IS NULL THEN 0 WHEN LOWER(fac1) = 'off' THEN 0 WHEN LOWER(fac1) = 'on' THEN 1 WHEN ISNUMERIC(fac1) = 1 THEN COALESCE(TRY_CONVERT(INT, fac1), 0) ELSE 0 END AS FACState,
                         CASE WHEN jaryan IS NULL THEN 0 WHEN LOWER(jaryan) = 'off' THEN 0 WHEN LOWER(jaryan) = 'on' THEN 1 WHEN ISNUMERIC(jaryan) = 1 THEN COALESCE(TRY_CONVERT(INT, jaryan), 0) ELSE 0 END AS CurrentState,
                         Jaryan AS CurrentRaw,
                         CASE WHEN power IS NULL THEN 0 WHEN LOWER(power) = 'off' THEN 0 WHEN LOWER(power) = 'on' THEN 1 WHEN ISNUMERIC(power) = 1 THEN COALESCE(TRY_CONVERT(INT, power), 0) ELSE 0 END AS PowerConsumptionState,
                         Power AS PowerRaw
                    FROM MonitoringDataRecords
                    WHERE MonitoringId = @MonitorId
                      AND TimestampFa >= @StartDate AND TimestampFa <= @EndDate
                    ORDER BY Timestamp";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MonitorId", effectiveId);
                        command.Parameters.AddWithValue("@StartDate", dtStart.Value);
                        command.Parameters.AddWithValue("@EndDate", dtEnd.Value);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                try
                                {
                                    DateTime tsVal = Convert.ToDateTime(reader["Timestamp"]);
                                    string tsFa = reader["TimestampFa"] == DBNull.Value ? null : reader["TimestampFa"].ToString();
                                    string tsDisplay = FormatJalaliDateTime(tsFa) ?? DateTimeToJalaliString(tsVal);

                                    double temperature = ParseDoubleFlexible(reader["TemperatureRef"].ToString());
                                    double freezerTemp = ParseDoubleFlexible(reader["TemperatureFreez"].ToString());

                                    chartData.Timestamps.Add(tsDisplay);
                                    chartData.Temperatures.Add(temperature);
                                    chartData.FreezerTemperatures.Add(freezerTemp);
                                    chartData.MotorStates.Add(Convert.ToInt32(reader["MotorState"]));
                                    chartData.PowerStates.Add(Convert.ToInt32(reader["PowerState"]));
                                    chartData.Element1States.Add(Convert.ToInt32(reader["Element1State"]));
                                    chartData.Element2States.Add(Convert.ToInt32(reader["Element2State"]));
                                    chartData.FDCStates.Add(Convert.ToInt32(reader["FDCState"]));
                                    chartData.FACStates.Add(Convert.ToInt32(reader["FACState"]));
                                    chartData.CurrentStates.Add(Convert.ToInt32(reader["CurrentState"]));
                                    chartData.PowerConsumptionStates.Add(Convert.ToInt32(reader["PowerConsumptionState"]));

                                    double currentVal = ParseDoubleFlexible(reader["CurrentRaw"].ToString());
                                    double powerVal = ParseDoubleFlexible(reader["PowerRaw"].ToString());
                                    chartData.CurrentValues.Add(currentVal);
                                    chartData.PowerValues.Add(powerVal);
                                }
                                catch { continue; }
                            }
                        }
                    }
                }

                var serializer = new JavaScriptSerializer();
                return serializer.Serialize(chartData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetChartData error: {ex.Message}");
                return new JavaScriptSerializer().Serialize(chartData);
            }
        }

        private TemperatureChartData FetchMonitoringDataRecords(string monitorId)
        {
            var chartData = new TemperatureChartData
            {
                Timestamps = new List<string>(),
                Temperatures = new List<double>(),
                FreezerTemperatures = new List<double>(),
                MotorStates = new List<int>(),
                PowerStates = new List<int>(),
                Element1States = new List<int>(),
                Element2States = new List<int>(),
                FDCStates = new List<int>(),
                FACStates = new List<int>(),
                CurrentStates = new List<int>(),
                PowerConsumptionStates = new List<int>(),
                CurrentValues = new List<double>(),
                PowerValues = new List<double>()
            };

            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
                System.Diagnostics.Debug.WriteLine($"Connection String: {connectionString}");

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = @"SELECT Id, Timestamp, TimestampFa, Note,
                                    TemperatureRef, TemperatureFreez,
                                    CASE 
                                        WHEN motorstate IS NULL THEN 0 
                                        WHEN LOWER(motorstate) = 'off' THEN 0 
                                        WHEN LOWER(motorstate) = 'on' THEN 1 
                                        WHEN ISNUMERIC(motorstate) = 1 THEN COALESCE(TRY_CONVERT(INT, motorstate), 0) 
                                        ELSE 0 
                                    END AS MotorState,
                                    CASE 
                                        WHEN bargh IS NULL THEN 0 
                                        WHEN LOWER(bargh) = 'off' THEN 0 
                                        WHEN LOWER(bargh) = 'on' THEN 1 
                                        WHEN ISNUMERIC(bargh) = 1 THEN COALESCE(TRY_CONVERT(INT, bargh), 0) 
                                        ELSE 0 
                                    END AS PowerState,
                                    CASE 
                                        WHEN element1 IS NULL THEN 0 
                                        WHEN LOWER(element1) = 'off' THEN 0 
                                        WHEN LOWER(element1) = 'on' THEN 1 
                                        WHEN ISNUMERIC(element1) = 1 THEN COALESCE(TRY_CONVERT(INT, element1), 0) 
                                        ELSE 0 
                                    END AS Element1State,
                                    CASE 
                                        WHEN element2 IS NULL THEN 0 
                                        WHEN LOWER(element2) = 'off' THEN 0 
                                        WHEN LOWER(element2) = 'on' THEN 1 
                                        WHEN ISNUMERIC(element2) = 1 THEN COALESCE(TRY_CONVERT(INT, element2), 0) 
                                        ELSE 0 
                                    END AS Element2State,
                                    CASE 
                                        WHEN fdc1 IS NULL THEN 0 
                                        WHEN LOWER(fdc1) = 'off' THEN 0 
                                        WHEN LOWER(fdc1) = 'on' THEN 1 
                                        WHEN ISNUMERIC(fdc1) = 1 THEN COALESCE(TRY_CONVERT(INT, fdc1), 0) 
                                        ELSE 0 
                                    END AS FDCState,
                                    CASE 
                                        WHEN fac1 IS NULL THEN 0 
                                        WHEN LOWER(fac1) = 'off' THEN 0 
                                        WHEN LOWER(fac1) = 'on' THEN 1 
                                        WHEN ISNUMERIC(fac1) = 1 THEN COALESCE(TRY_CONVERT(INT, fac1), 0) 
                                        ELSE 0 
                                    END AS FACState,
                                    CASE 
                                        WHEN jaryan IS NULL THEN 0 
                                        WHEN LOWER(jaryan) = 'off' THEN 0 
                                        WHEN LOWER(jaryan) = 'on' THEN 1 
                                        WHEN ISNUMERIC(jaryan) = 1 THEN COALESCE(TRY_CONVERT(INT, jaryan), 0) 
                                        ELSE 0 
                                    END AS CurrentState,
                                    jaryan AS CurrentRaw,
                                    CASE 
                                        WHEN power IS NULL THEN 0 
                                        WHEN LOWER(power) = 'off' THEN 0 
                                        WHEN LOWER(power) = 'on' THEN 1 
                                        WHEN ISNUMERIC(power) = 1 THEN COALESCE(TRY_CONVERT(INT, power), 0) 
                                        ELSE 0 
                                    END AS PowerConsumptionState,
                                    power AS PowerRaw
                                    FROM MonitoringDataRecords
                                    WHERE MonitoringId = @MonitorId
                                    ORDER BY Timestamp";

                    System.Diagnostics.Debug.WriteLine($"Executing query for MonitorId: {monitorId}");

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MonitorId", monitorId);

                        try
                        {
                            connection.Open();
                            System.Diagnostics.Debug.WriteLine("Database connection opened successfully");

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                System.Diagnostics.Debug.WriteLine("Query executed successfully");

                                while (reader.Read())
                                {
                                    try
                                    {
                                        DateTime tsVal = Convert.ToDateTime(reader["Timestamp"]);
                                        string tsFa = reader["TimestampFa"] == DBNull.Value ? null : reader["TimestampFa"].ToString();
                                        string formattedTimestamp = FormatJalaliDateTime(tsFa) ?? DateTimeToJalaliString(tsVal);

                                        double temperature = ParseDoubleFlexible(reader["TemperatureRef"].ToString());
                                        double freezerTemp = ParseDoubleFlexible(reader["TemperatureFreez"].ToString());

                                        chartData.Timestamps.Add(formattedTimestamp);
                                        chartData.Temperatures.Add(temperature);
                                        chartData.FreezerTemperatures.Add(freezerTemp);
                                        chartData.MotorStates.Add(Convert.ToInt32(reader["MotorState"]));
                                        chartData.PowerStates.Add(Convert.ToInt32(reader["PowerState"]));
                                        chartData.Element1States.Add(Convert.ToInt32(reader["Element1State"]));
                                        chartData.Element2States.Add(Convert.ToInt32(reader["Element2State"]));
                                        chartData.FDCStates.Add(Convert.ToInt32(reader["FDCState"]));
                                        chartData.FACStates.Add(Convert.ToInt32(reader["FACState"]));
                                        chartData.CurrentStates.Add(Convert.ToInt32(reader["CurrentState"]));
                                        chartData.PowerConsumptionStates.Add(Convert.ToInt32(reader["PowerConsumptionState"]));

                                        double currentVal = ParseDoubleFlexible(reader["CurrentRaw"].ToString());
                                        double powerVal = ParseDoubleFlexible(reader["PowerRaw"].ToString());
                                        chartData.CurrentValues.Add(currentVal);
                                        chartData.PowerValues.Add(powerVal);
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error processing row: {ex.Message}");
                                        System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                                        continue;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Database error: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                            throw;
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Total data points collected: {chartData.Timestamps.Count}");
                if (chartData.Timestamps.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"First timestamp: {chartData.Timestamps[0]}");
                    System.Diagnostics.Debug.WriteLine($"Last timestamp: {chartData.Timestamps[chartData.Timestamps.Count - 1]}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in FetchMonitoringDataRecords: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            return chartData;
        }

        private static bool IsValidJalaliDate(string dateStr)
        {
            try
            {
                // بررسی فرمت تاریخ جلالی (YYYY/MM/DD HH:mm:ss یا YYYY/M/D H:m:s)
                if (!System.Text.RegularExpressions.Regex.IsMatch(dateStr, @"^\d{4}/\d{1,2}/\d{1,2}\s\d{1,2}:\d{1,2}:\d{1,2}$"))
                    return false;

                string[] dateParts = dateStr.Split(' ')[0].Split('/');
                if (dateParts.Length != 3)
                    return false;

                int year = int.Parse(dateParts[0]);
                int month = int.Parse(dateParts[1]);
                int day = int.Parse(dateParts[2]);

                // اعتبارسنجی اولیه برای تقویم جلالی
                if (year < 1300 || year > 1500) // محدوده منطقی برای سال‌های جلالی
                    return false;
                if (month < 1 || month > 12)
                    return false;
                if (day < 1 || day > 31)
                    return false;

                // اعتبارسنجی اضافی برای ماه‌های خاص
                if (month > 6 && day > 30)
                    return false;
                if (month == 12 && day > 29)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string FormatJalaliDateTime(string dateStr)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dateStr)) return null;
                string[] parts = dateStr.Split(' ');
                if (parts.Length != 2)
                    return dateStr;

                string[] dateParts = parts[0].Split('/');
                if (dateParts.Length != 3)
                    return dateStr;

                int year = int.Parse(dateParts[0]);
                int month = int.Parse(dateParts[1]);
                int day = int.Parse(dateParts[2]);

                string[] timeParts = parts[1].Split(':');
                if (timeParts.Length != 3)
                    return dateStr;

                int hour = int.Parse(timeParts[0]);
                int minute = int.Parse(timeParts[1]);
                int second = int.Parse(timeParts[2]);

                return $"{year:0000}/{month:00}/{day:00} {hour:00}:{minute:00}:{second:00}";
            }
            catch
            {
                return dateStr;
            }
        }

        private static string DateTimeToJalaliString(DateTime dt)
        {
            try
            {
                var pc = new PersianCalendar();
                int jy = pc.GetYear(dt);
                int jm = pc.GetMonth(dt);
                int jd = pc.GetDayOfMonth(dt);
                int hh = dt.Hour;
                int mm = dt.Minute;
                int ss = dt.Second;
                return $"{jy:0000}/{jm:00}/{jd:00} {hh:00}:{mm:00}:{ss:00}";
            }
            catch
            {
                return dt.ToString("yyyy/MM/dd HH:mm:ss");
            }
        }

        // Response DTO for chunked loading
        private class ChunkedChartResponse
        {
            public List<string> Timestamps { get; set; }
            public List<double> Temperatures { get; set; }
            public List<double> FreezerTemperatures { get; set; }
            public List<int> MotorStates { get; set; }
            public List<int> PowerStates { get; set; }
            public List<int> Element1States { get; set; }
            public List<int> Element2States { get; set; }
            public List<int> FDCStates { get; set; }
            public List<int> FACStates { get; set; }
            public List<int> CurrentStates { get; set; }
            public List<int> PowerConsumptionStates { get; set; }
            public List<double> CurrentValues { get; set; }
            public List<double> PowerValues { get; set; }
            public List<int> Ids { get; set; }
            public List<string> Notes { get; set; }
            public bool HasMore { get; set; }
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public static string GetChartDataChunk(string monitorId, string startJalali, string endJalali, int page, int pageSize)
        {
            var resp = new ChunkedChartResponse
            {
                Timestamps = new List<string>(),
                Temperatures = new List<double>(),
                FreezerTemperatures = new List<double>(),
                MotorStates = new List<int>(),
                PowerStates = new List<int>(),
                Element1States = new List<int>(),
                Element2States = new List<int>(),
                FDCStates = new List<int>(),
                FACStates = new List<int>(),
                CurrentStates = new List<int>(),
                PowerConsumptionStates = new List<int>(),
                CurrentValues = new List<double>(),
                PowerValues = new List<double>(),
                Ids = new List<int>(),
                Notes = new List<string>(),
                HasMore = false,
                TotalCount = 0,
                Page = page,
                PageSize = pageSize
            };

            try
            {
                if (string.IsNullOrWhiteSpace(monitorId)) return new JavaScriptSerializer().Serialize(resp);

                // sanitize paging
                if (page < 0) page = 0;
                if (pageSize <= 0 || pageSize > 1500) pageSize = 800; // keep payload reasonable
                resp.Page = page; resp.PageSize = pageSize;

                string sStart = NormalizeJalaliDateTime(startJalali);
                string sEnd = NormalizeJalaliDateTime(endJalali);
                if (string.IsNullOrEmpty(sStart) || string.IsNullOrEmpty(sEnd)) return new JavaScriptSerializer().Serialize(resp);

                var dtStart = JalaliStringToDateTime(sStart);
                var dtEnd = JalaliStringToDateTime(sEnd);
                if (!dtStart.HasValue || !dtEnd.HasValue) return new JavaScriptSerializer().Serialize(resp);

                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Resolve effective MonitoringId
                    string effectiveId = monitorId;
                    using (var countCmd0 = new SqlCommand("SELECT COUNT(*) FROM MonitoringDataRecords WHERE MonitoringId = @Id", connection))
                    {
                        countCmd0.Parameters.AddWithValue("@Id", effectiveId);
                        int cnt0 = Convert.ToInt32(countCmd0.ExecuteScalar() ?? 0);
                        if (cnt0 == 0)
                        {
                            using (var mapCmd = new SqlCommand(@"SELECT TOP 1 CAST(Id AS NVARCHAR(100))
                                                              FROM tb_create_monitor
                                                              WHERE Request_Id = @Req OR Id = @Req
                                                              ORDER BY Id DESC", connection))
                            {
                                mapCmd.Parameters.AddWithValue("@Req", monitorId);
                                var alt = mapCmd.ExecuteScalar();
                                string altId = (alt == null || alt == DBNull.Value) ? null : alt.ToString();
                                if (!string.IsNullOrWhiteSpace(altId)) effectiveId = altId;
                            }
                        }
                    }
                    int totalCount = 0;
                    using (var totalCmd = new SqlCommand(@"SELECT COUNT(*)
                                                            FROM MonitoringDataRecords
                                                            WHERE MonitoringId = @MonitorId
                                                              AND Timestamp >= @StartDate AND Timestamp <= @EndDate", connection))
                    {
                        totalCmd.Parameters.AddWithValue("@MonitorId", effectiveId);
                        totalCmd.Parameters.AddWithValue("@StartDate", dtStart.Value);
                        totalCmd.Parameters.AddWithValue("@EndDate", dtEnd.Value);
                        totalCount = Convert.ToInt32(totalCmd.ExecuteScalar() ?? 0);
                    }

                    int offset = page * pageSize;
                    if (offset < 0) offset = 0;

                    string query = @"WITH CTE AS (
                                        SELECT Id, Timestamp, TimestampFa,
                                               Note,
                                               TemperatureRef, TemperatureFreez,
                                               CASE WHEN motorstate IS NULL THEN 0 
                                                    WHEN LOWER(motorstate) = 'off' THEN 0 
                                                    WHEN LOWER(motorstate) = 'on' THEN 1 
                                                    WHEN ISNUMERIC(motorstate) = 1 THEN COALESCE(TRY_CONVERT(INT, motorstate), 0) 
                                                    ELSE 0 END AS MotorState,
                                               CASE WHEN bargh IS NULL THEN 0 
                                                    WHEN LOWER(bargh) = 'off' THEN 0 
                                                    WHEN LOWER(bargh) = 'on' THEN 1 
                                                    WHEN ISNUMERIC(bargh) = 1 THEN COALESCE(TRY_CONVERT(INT, bargh), 0) 
                                                    ELSE 0 END AS PowerState,
                                               CASE WHEN element1 IS NULL THEN 0 
                                                    WHEN LOWER(element1) = 'off' THEN 0 
                                                    WHEN LOWER(element1) = 'on' THEN 1 
                                                    WHEN ISNUMERIC(element1) = 1 THEN COALESCE(TRY_CONVERT(INT, element1), 0) 
                                                    ELSE 0 END AS Element1State,
                                               CASE WHEN element2 IS NULL THEN 0 
                                                    WHEN LOWER(element2) = 'off' THEN 0 
                                                    WHEN LOWER(element2) = 'on' THEN 1 
                                                    WHEN ISNUMERIC(element2) = 1 THEN COALESCE(TRY_CONVERT(INT, element2), 0) 
                                                    ELSE 0 END AS Element2State,
                                                CASE WHEN fdc1 IS NULL THEN 0 
                                                     WHEN LOWER(fdc1) = 'off' THEN 0 
                                                     WHEN LOWER(fdc1) = 'on' THEN 1 
                                                     WHEN ISNUMERIC(fdc1) = 1 THEN COALESCE(TRY_CONVERT(INT, fdc1), 0) 
                                                     ELSE 0 END AS FDCState,
                                                CASE WHEN fac1 IS NULL THEN 0 
                                                     WHEN LOWER(fac1) = 'off' THEN 0 
                                                     WHEN LOWER(fac1) = 'on' THEN 1 
                                                     WHEN ISNUMERIC(fac1) = 1 THEN COALESCE(TRY_CONVERT(INT, fac1), 0) 
                                                     ELSE 0 END AS FACState,
                                               CASE WHEN jaryan IS NULL THEN 0 
                                                    WHEN LOWER(jaryan) = 'off' THEN 0 
                                                    WHEN LOWER(jaryan) = 'on' THEN 1 
                                                    WHEN ISNUMERIC(jaryan) = 1 THEN COALESCE(TRY_CONVERT(INT, jaryan), 0) 
                                                    ELSE 0 END AS CurrentState,
                                               jaryan AS CurrentRaw,
                                               CASE WHEN power IS NULL THEN 0 
                                                    WHEN LOWER(power) = 'off' THEN 0 
                                                    WHEN LOWER(power) = 'on' THEN 1 
                                                    WHEN ISNUMERIC(power) = 1 THEN COALESCE(TRY_CONVERT(INT, power), 0) 
                                                    ELSE 0 END AS PowerConsumptionState,
                                               power AS PowerRaw
                                        FROM MonitoringDataRecords
                                        WHERE MonitoringId = @MonitorId
                                          AND Timestamp >= @StartDate AND Timestamp <= @EndDate
                                    )
                                    SELECT * FROM CTE
                                    ORDER BY Id ASC
                                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@MonitorId", effectiveId);
                        cmd.Parameters.AddWithValue("@StartDate", dtStart.Value);
                        cmd.Parameters.AddWithValue("@EndDate", dtEnd.Value);
                        cmd.Parameters.AddWithValue("@Offset", offset);
                        cmd.Parameters.AddWithValue("@PageSize", pageSize);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                try
                                {
                                    DateTime tsVal = Convert.ToDateTime(reader["Timestamp"]);
                                    string tsFa = reader["TimestampFa"] == DBNull.Value ? null : reader["TimestampFa"].ToString();
                                    string tsDisplay = FormatJalaliDateTime(tsFa) ?? DateTimeToJalaliString(tsVal);

                                    double temperature = ParseDoubleFlexible(reader["TemperatureRef"].ToString());
                                    double freezerTemp = ParseDoubleFlexible(reader["TemperatureFreez"].ToString());

                                    resp.Timestamps.Add(tsDisplay);
                                    resp.Temperatures.Add(temperature);
                                    resp.FreezerTemperatures.Add(freezerTemp);
                                    resp.MotorStates.Add(Convert.ToInt32(reader["MotorState"]));
                                    resp.PowerStates.Add(Convert.ToInt32(reader["PowerState"]));
                                    resp.Element1States.Add(Convert.ToInt32(reader["Element1State"]));
                                    resp.Element2States.Add(Convert.ToInt32(reader["Element2State"]));
                                    resp.FDCStates.Add(Convert.ToInt32(reader["FDCState"]));
                                    resp.FACStates.Add(Convert.ToInt32(reader["FACState"]));
                                    resp.CurrentStates.Add(Convert.ToInt32(reader["CurrentState"]));
                                    resp.PowerConsumptionStates.Add(Convert.ToInt32(reader["PowerConsumptionState"]));

                                    double currentVal = ParseDoubleFlexible(reader["CurrentRaw"].ToString());
                                    double powerVal = ParseDoubleFlexible(reader["PowerRaw"].ToString());
                                    resp.CurrentValues.Add(currentVal);
                                    resp.PowerValues.Add(powerVal);
                                    resp.Ids.Add(Convert.ToInt32(reader["Id"]));
                                    resp.Notes.Add(reader["Note"] == DBNull.Value ? null : reader["Note"].ToString());
                                }
                                catch { continue; }
                            }
                        }
                    }

                    resp.TotalCount = totalCount;
                    resp.HasMore = ((offset + resp.Timestamps.Count) < totalCount);
                }

                return new JavaScriptSerializer().Serialize(resp);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetChartDataChunk error: {ex.Message}");
                return new JavaScriptSerializer().Serialize(resp);
            }
        }

        // Build a WHERE fragment that matches Timestamp by day prefixes between start and end (inclusive)
        // Handles both zero-padded and non-padded Jalali formats stored as strings in DB
        private static string BuildDayLikeWhere(DateTime gStart, DateTime gEnd, out List<SqlParameter> parameters)
        {
            var pc = new PersianCalendar();
            var clauses = new List<string>();
            parameters = new List<SqlParameter>();
            int idx = 0;
            // Ensure start <= end
            if (gEnd < gStart) { var tmp = gStart; gStart = gEnd; gEnd = tmp; }
            for (var d = gStart.Date; d <= gEnd.Date; d = d.AddDays(1))
            {
                int jy = pc.GetYear(d);
                int jm = pc.GetMonth(d);
                int jd = pc.GetDayOfMonth(d);
                string pPad = string.Format(CultureInfo.InvariantCulture, "{0:0000}/{1:00}/{2:00} ", jy, jm, jd);
                string pNoPad = string.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2} ", jy, jm, jd).Replace("/0", "/");
                string parPad = "@DayPad" + idx;
                string parNoPad = "@DayNoPad" + idx;
                clauses.Add($"(Timestamp LIKE {parPad} + '%' OR Timestamp LIKE {parNoPad} + '%')");
                parameters.Add(new SqlParameter(parPad, SqlDbType.NVarChar) { Value = pPad });
                parameters.Add(new SqlParameter(parNoPad, SqlDbType.NVarChar) { Value = pNoPad });
                idx++;
            }
            if (clauses.Count == 0)
            {
                // Fallback to a clause that matches nothing
                return "1=0";
            }
            return string.Join(" OR ", clauses);
        }

        private void SerializeChartData(TemperatureChartData chartData)
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(chartData);

                // Ensure the JSON is valid
                if (string.IsNullOrEmpty(json))
                {
                    json = "{\"Timestamps\":[\"1404/02/31 08:36:07\",\"1404/02/31 08:36:11\",\"1404/02/31 08:36:51\"],\"Temperatures\":[23.14,23.14,23.11],\"FreezerTemperatures\":[-15.2,-15.1,-15.3],\"MotorStates\":[0,1,0]}";
                }

                hdnChartData.Value = json;
            }
            catch (Exception ex)
            {
                // Log the exception
                System.Diagnostics.Debug.WriteLine($"Error serializing chart data: {ex.Message}");
                // Set a valid JSON with sample data in Persian date format
                hdnChartData.Value = "{\"Timestamps\":[\"1404/02/31 08:36:07\",\"1404/02/31 08:36:11\",\"1404/02/31 08:36:51\"],\"Temperatures\":[23.14,23.14,23.11],\"FreezerTemperatures\":[-15.2,-15.1,-15.3],\"MotorStates\":[0,1,0]}";
            }
        }

        private class TemperatureChartData
        {
            public List<string> Timestamps { get; set; }
            public List<double> Temperatures { get; set; }
            public List<double> FreezerTemperatures { get; set; }
            public List<int> MotorStates { get; set; }
            public List<int> PowerStates { get; set; }
            public List<int> Element1States { get; set; }
            public List<int> Element2States { get; set; }
            public List<int> FDCStates { get; set; }
            public List<int> FACStates { get; set; }
            public List<int> CurrentStates { get; set; }
            public List<int> PowerConsumptionStates { get; set; }
            public List<double> CurrentValues { get; set; }
            public List<double> PowerValues { get; set; }
            public List<int> Ids { get; set; }
            public List<string> Notes { get; set; }
        }

        // DTOs for CSV cache deserialization (mirrors CsvCacheEntry from Server)
        private sealed class CsvCacheEntryDto
        {
            public int ConnectionId { get; set; }
            public int CustomerReceiptId { get; set; }
            public string CustomerName { get; set; } = "";
            public string CustomerMobile { get; set; } = "";
            public string MonitoringDeviceTitle { get; set; } = "";
            public string MonitoringDeviceNumber { get; set; } = "";
            public List<CsvRecordDto> Records { get; set; } = new List<CsvRecordDto>();
        }

        private sealed class CsvRecordDto
        {
            public long Id { get; set; }
            public string Timestamp { get; set; } = "";
            public string TimestampFa { get; set; } = "";
            public float? TemperatureRef { get; set; }
            public float? TemperatureFreez { get; set; }
            public bool MotorState { get; set; }
            public bool Bargh { get; set; }
            public bool Element1 { get; set; }
            public bool Element2 { get; set; }
            public bool Fdc1 { get; set; }
            public bool Fac1 { get; set; }
            public float? Jaryan { get; set; }
            public float? Power { get; set; }
            public float? Kw { get; set; }
            public float? SumKw { get; set; }
            public string State { get; set; } = "";
            public string Note { get; set; } = "";
        }

        public class SimpleResult
        {
            public bool Ok { get; set; }
            public string Message { get; set; }
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public static string UpsertNote(string monitorId, int id, string note)
        {
            var res = new SimpleResult { Ok = false, Message = "" };
            try
            {
                if (string.IsNullOrWhiteSpace(monitorId) || id <= 0)
                {
                    res.Message = "Invalid input"; return new JavaScriptSerializer().Serialize(res);
                }
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string effectiveId = monitorId;
                    using (var countCmd = new SqlCommand("SELECT COUNT(*) FROM MonitoringDataRecords WHERE MonitoringId = @Id", connection))
                    {
                        countCmd.Parameters.AddWithValue("@Id", effectiveId);
                        int cnt0 = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);
                        if (cnt0 == 0)
                        {
                            using (var mapCmd = new SqlCommand(@"SELECT TOP 1 CAST(Id AS NVARCHAR(100))
                                                              FROM tb_create_monitor
                                                              WHERE Request_Id = @Req OR Id = @Req
                                                              ORDER BY Id DESC", connection))
                            {
                                mapCmd.Parameters.AddWithValue("@Req", monitorId);
                                var alt = mapCmd.ExecuteScalar();
                                string altId = (alt == null || alt == DBNull.Value) ? null : alt.ToString();
                                if (!string.IsNullOrWhiteSpace(altId)) effectiveId = altId;
                            }
                        }
                    }
                    using (var cmd = new SqlCommand("UPDATE MonitoringDataRecords SET Note = @Note WHERE MonitoringId = @MonitorId AND Id = @Id", connection))
                    {
                        cmd.Parameters.AddWithValue("@Note", (object)note ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@MonitorId", effectiveId);
                        cmd.Parameters.AddWithValue("@Id", id);
                        int aff = cmd.ExecuteNonQuery();
                        res.Ok = (aff > 0);
                        res.Message = res.Ok ? "Saved" : "Not found";
                    }
                }
            }
            catch (Exception ex)
            {
                res.Ok = false; res.Message = ex.Message;
            }
            return new JavaScriptSerializer().Serialize(res);
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public static string DeleteNote(string monitorId, int id)
        {
            return UpsertNote(monitorId, id, null);
        }

        public class NotesRangeResponse
        {
            public List<string> Timestamps { get; set; }
            public List<int> Ids { get; set; }
            public List<string> Notes { get; set; }
        }

        public class EnergyCalculationData
        {
            public List<string> Timestamps { get; set; }
            public List<int> MotorStates { get; set; }
            public List<int> Element1States { get; set; }
            public List<int> Element2States { get; set; }
            public List<double> PowerValues { get; set; }
            public List<int> Ids { get; set; }
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public static string GetNotesInRange(string monitorId, string startJalali, string endJalali)
        {
            var resp = new NotesRangeResponse { Timestamps = new List<string>(), Ids = new List<int>(), Notes = new List<string>() };
            try
            {
                System.Diagnostics.Debug.WriteLine($"GetNotesInRange called with monitorId: {monitorId}, startJalali: {startJalali}, endJalali: {endJalali}");
                
                if (string.IsNullOrWhiteSpace(monitorId)) 
                {
                    System.Diagnostics.Debug.WriteLine("GetNotesInRange: monitorId is null or empty");
                    return new JavaScriptSerializer().Serialize(resp);
                }

                string sStart = NormalizeJalaliDateTime(startJalali);
                string sEnd = NormalizeJalaliDateTime(endJalali);
                System.Diagnostics.Debug.WriteLine($"GetNotesInRange: Normalized dates - sStart: {sStart}, sEnd: {sEnd}");
                
                var dtStart = JalaliStringToDateTime(sStart);
                var dtEnd = JalaliStringToDateTime(sEnd);
                System.Diagnostics.Debug.WriteLine($"GetNotesInRange: Parsed dates - dtStart: {dtStart}, dtEnd: {dtEnd}");
                
                if (!dtStart.HasValue || !dtEnd.HasValue) 
                {
                    System.Diagnostics.Debug.WriteLine("GetNotesInRange: Date parsing failed");
                    return new JavaScriptSerializer().Serialize(resp);
                }

                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    // Resolve effective id
                    string effectiveId = monitorId;
                    using (var countCmd = new SqlCommand("SELECT COUNT(*) FROM MonitoringDataRecords WHERE MonitoringId = @Id", connection))
                    {
                        countCmd.Parameters.AddWithValue("@Id", effectiveId);
                        int cnt0 = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0);
                        if (cnt0 == 0)
                        {
                            using (var mapCmd = new SqlCommand(@"SELECT TOP 1 CAST(Id AS NVARCHAR(100))
                                                              FROM tb_create_monitor
                                                              WHERE Request_Id = @Req OR Id = @Req
                                                              ORDER BY Id DESC", connection))
                            {
                                mapCmd.Parameters.AddWithValue("@Req", monitorId);
                                var alt = mapCmd.ExecuteScalar();
                                string altId = (alt == null || alt == DBNull.Value) ? null : alt.ToString();
                                if (!string.IsNullOrWhiteSpace(altId)) effectiveId = altId;
                            }
                        }
                    }

                    string query = @"SELECT Id, Timestamp, TimestampFa, Note
                                     FROM MonitoringDataRecords
                                     WHERE MonitoringId = @MonitorId
                                       AND Timestamp >= @StartDate AND Timestamp <= @EndDate
                                       AND Note IS NOT NULL AND LTRIM(RTRIM(Note)) <> ''
                                     ORDER BY Id ASC";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@MonitorId", effectiveId);
                        cmd.Parameters.AddWithValue("@StartDate", dtStart.Value);
                        cmd.Parameters.AddWithValue("@EndDate", dtEnd.Value);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                try
                                {
                                    DateTime tsVal = Convert.ToDateTime(reader["Timestamp"]);
                                    string tsFa = reader["TimestampFa"] == DBNull.Value ? null : reader["TimestampFa"].ToString();
                                    string fmt = FormatJalaliDateTime(tsFa) ?? DateTimeToJalaliString(tsVal);
                                    string note = reader["Note"] == DBNull.Value ? null : reader["Note"].ToString();
                                    if (string.IsNullOrWhiteSpace(note)) continue;
                                    resp.Timestamps.Add(fmt);
                                    resp.Ids.Add(Convert.ToInt32(reader["Id"]));
                                    resp.Notes.Add(note);
                                }
                                catch { continue; }
                            }
                        }
                    }
                }

                return new JavaScriptSerializer().Serialize(resp);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetNotesInRange error: {ex.Message}");
                return new JavaScriptSerializer().Serialize(resp);
            }
        }

        [WebMethod]
        public static void GetEnergyCalculationData(string monitorId, string startJalali, string endJalali)
        {
            var timestamps = new List<string>();
            var motorStates = new List<int>();
            var element1States = new List<int>();
            var element2States = new List<int>();
            var powerValues = new List<double>();
            var ids = new List<int>();

            try
            {
                System.Diagnostics.Debug.WriteLine($"GetEnergyCalculationData called with monitorId: {monitorId}, startJalali: {startJalali}, endJalali: {endJalali}");
                
                if (string.IsNullOrWhiteSpace(monitorId)) 
                {
                    System.Diagnostics.Debug.WriteLine("GetEnergyCalculationData: monitorId is null or empty");
                    System.Web.HttpContext.Current.Response.ContentType = "application/json";
                    System.Web.HttpContext.Current.Response.Write("{\"Timestamps\":[],\"MotorStates\":[],\"Element1States\":[],\"Element2States\":[],\"PowerValues\":[],\"Ids\":[]}");
                    return;
                }

                string sStart = NormalizeJalaliDateTime(startJalali);
                string sEnd = NormalizeJalaliDateTime(endJalali);
                System.Diagnostics.Debug.WriteLine($"GetEnergyCalculationData: Normalized dates - sStart: {sStart}, sEnd: {sEnd}");
                
                var dtStart = JalaliStringToDateTime(sStart);
                var dtEnd = JalaliStringToDateTime(sEnd);
                System.Diagnostics.Debug.WriteLine($"GetEnergyCalculationData: Parsed dates - dtStart: {dtStart}, dtEnd: {dtEnd}");
                
                if (!dtStart.HasValue || !dtEnd.HasValue) 
                {
                    System.Diagnostics.Debug.WriteLine("GetEnergyCalculationData: Date parsing failed");
                    System.Web.HttpContext.Current.Response.ContentType = "application/json";
                    System.Web.HttpContext.Current.Response.Write("{\"Timestamps\":[],\"MotorStates\":[],\"Element1States\":[],\"Element2States\":[],\"PowerValues\":[],\"Ids\":[]}");
                    return;
                }

                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string effectiveId = monitorId;

                    // Check direct match first
                    using (var countCmd = new SqlCommand("SELECT COUNT(*) FROM MonitoringDataRecords WHERE MonitoringId = @Id", connection))
                    {
                        countCmd.Parameters.AddWithValue("@Id", effectiveId);
                        var cntObj = countCmd.ExecuteScalar();
                        int cnt = (cntObj == null || cntObj == DBNull.Value) ? 0 : Convert.ToInt32(cntObj);
                        System.Diagnostics.Debug.WriteLine($"GetEnergyCalculationData: Direct match count for {effectiveId}: {cnt}");
                        
                        if (cnt == 0)
                        {
                            // Try mapping Request_Id/Id from tb_create_monitor
                            using (var mapCmd = new SqlCommand(@"SELECT TOP 1 CAST(Id AS NVARCHAR(100))
                                                              FROM tb_create_monitor
                                                              WHERE Request_Id = @Req OR Id = @Req
                                                              ORDER BY Id DESC", connection))
                            {
                                mapCmd.Parameters.AddWithValue("@Req", monitorId);
                                var alt = mapCmd.ExecuteScalar();
                                string altId = (alt == null || alt == DBNull.Value) ? null : alt.ToString();
                                System.Diagnostics.Debug.WriteLine($"GetEnergyCalculationData: Mapped ID from {monitorId} to {altId}");
                                
                                if (!string.IsNullOrWhiteSpace(altId))
                                {
                                    effectiveId = altId;
                                }
                            }
                        }
                    }

                    string query = @"
                        SELECT Id, Timestamp, TimestampFa,
                        CASE WHEN motorstate IS NULL THEN 0 
                             WHEN LOWER(motorstate) = 'off' THEN 0 
                             WHEN LOWER(motorstate) = 'on' THEN 1 
                             WHEN ISNUMERIC(motorstate) = 1 THEN COALESCE(TRY_CONVERT(INT, motorstate), 0) 
                             ELSE 0 END AS MotorState,
                        CASE WHEN element1 IS NULL THEN 0 
                             WHEN LOWER(element1) = 'off' THEN 0 
                             WHEN LOWER(element1) = 'on' THEN 1 
                             WHEN ISNUMERIC(element1) = 1 THEN COALESCE(TRY_CONVERT(INT, element1), 0) 
                             ELSE 0 END AS Element1State,
                        CASE WHEN element2 IS NULL THEN 0 
                             WHEN LOWER(element2) = 'off' THEN 0 
                             WHEN LOWER(element2) = 'on' THEN 1 
                             WHEN ISNUMERIC(element2) = 1 THEN COALESCE(TRY_CONVERT(INT, element2), 0) 
                             ELSE 0 END AS Element2State,
                        power AS PowerRaw
                        FROM MonitoringDataRecords
                        WHERE MonitoringId = @MonitorId
                          AND Timestamp >= @StartDate AND Timestamp <= @EndDate
                        ORDER BY Id ASC";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.CommandTimeout = 120;
                        cmd.Parameters.AddWithValue("@MonitorId", effectiveId);
                        cmd.Parameters.AddWithValue("@StartDate", dtStart.Value);
                        cmd.Parameters.AddWithValue("@EndDate", dtEnd.Value);

                        System.Diagnostics.Debug.WriteLine($"GetEnergyCalculationData: Executing query with effectiveId: {effectiveId}");

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            int rowCount = 0;
                            int batchSize = 5000;
                            
                            while (reader.Read())
                            {
                                try
                                {
                                    rowCount++;
                                    DateTime tsVal = Convert.ToDateTime(reader["Timestamp"]);
                                    string tsFa = reader["TimestampFa"] == DBNull.Value ? null : reader["TimestampFa"].ToString();
                                    string fmt = FormatJalaliDateTime(tsFa) ?? DateTimeToJalaliString(tsVal);

                                    timestamps.Add(fmt);
                                    ids.Add(Convert.ToInt32(reader["Id"]));

                                    int motorState = reader["MotorState"] == DBNull.Value ? 0 : Convert.ToInt32(reader["MotorState"]);
                                    int element1State = reader["Element1State"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Element1State"]);
                                    int element2State = reader["Element2State"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Element2State"]);
                                    
                                    motorStates.Add(motorState);
                                    element1States.Add(element1State);
                                    element2States.Add(element2State);

                                    double power = ParseDoubleFlexible(reader["PowerRaw"]?.ToString() ?? "0");
                                    powerValues.Add(power);

                                    if (rowCount % batchSize == 0)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"GetEnergyCalculationData: Processed {rowCount} rows so far...");
                                        System.GC.Collect();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"GetEnergyCalculationData: Error processing row {rowCount}: {ex.Message}");
                                    continue;
                                }
                            }
                            System.Diagnostics.Debug.WriteLine($"GetEnergyCalculationData: Processed {rowCount} rows, final result count: {timestamps.Count}");
                        }
                    }
                }

                // Build JSON manually to avoid serialization limits
                System.Diagnostics.Debug.WriteLine($"GetEnergyCalculationData: Building JSON for {timestamps.Count} records");
                
                var json = new System.Text.StringBuilder();
                json.Append("{\"Timestamps\":[");
                for (int i = 0; i < timestamps.Count; i++)
                {
                    if (i > 0) json.Append(",");
                    json.Append("\"").Append(timestamps[i]).Append("\"");
                }
                json.Append("],\"MotorStates\":[");
                for (int i = 0; i < motorStates.Count; i++)
                {
                    if (i > 0) json.Append(",");
                    json.Append(motorStates[i]);
                }
                json.Append("],\"Element1States\":[");
                for (int i = 0; i < element1States.Count; i++)
                {
                    if (i > 0) json.Append(",");
                    json.Append(element1States[i]);
                }
                json.Append("],\"Element2States\":[");
                for (int i = 0; i < element2States.Count; i++)
                {
                    if (i > 0) json.Append(",");
                    json.Append(element2States[i]);
                }
                json.Append("],\"PowerValues\":[");
                for (int i = 0; i < powerValues.Count; i++)
                {
                    if (i > 0) json.Append(",");
                    json.Append(powerValues[i].ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                }
                json.Append("],\"Ids\":[");
                for (int i = 0; i < ids.Count; i++)
                {
                    if (i > 0) json.Append(",");
                    json.Append(ids[i]);
                }
                json.Append("]}");
                
                System.Diagnostics.Debug.WriteLine($"GetEnergyCalculationData: Successfully built JSON with length: {json.Length}");
                var resp = System.Web.HttpContext.Current.Response;
                resp.ContentType = "application/json";
                resp.Write(json.ToString());
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetEnergyCalculationData error: {ex.Message}");
                var resp = System.Web.HttpContext.Current?.Response;
                if (resp != null)
                {
                    resp.ContentType = "application/json";
                    resp.StatusCode = 500;
                    resp.Write("{\"Timestamps\":[],\"MotorStates\":[],\"Element1States\":[],\"Element2States\":[],\"PowerValues\":[],\"Ids\":[]}");
                }
                return;
            }
        }
    }
}