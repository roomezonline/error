using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.Services;
using System.Data;
using System.Data.SqlClient;
using Newtonsoft.Json;
using System.Globalization;

namespace ErrorService.WebForms.admin.ardino
{
    public partial class chart : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {

                //string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
                //int createMonitorId = 270;
                //string state = "true";
                //DateTime startTime = DateTime.Now; // از زمان فعلی شروع می‌کنیم

                //Random random = new Random();

                //using (SqlConnection connection = new SqlConnection(connectionString))
                //{
                //    connection.Open();

                //    for (int i = 0; i < 600; i++)
                //    {
                //        DateTime timestamp = startTime.AddMinutes(i * 5);
                //        string persianDate = MiladiToShamsi(timestamp);
                //        string formattedTimestamp = $"{persianDate} {timestamp.ToString("HH:mm:ss")}";

                //        double temperatureRef = random.Next(1, 31);
                //        double temperatureFreez = random.Next(-20, 31);

                //        string query = @"INSERT INTO TemperatureData 
                //        (create_monitor_id, Temperature_ref, Temperature_freez, Timestamp, state) 
                //        VALUES 
                //        (@MonitorId, @TempRef, @TempFreez, @Timestamp, @State)";

                //        using (SqlCommand command = new SqlCommand(query, connection))
                //        {
                //            command.Parameters.AddWithValue("@MonitorId", createMonitorId);
                //            command.Parameters.AddWithValue("@TempRef", temperatureRef);
                //            command.Parameters.AddWithValue("@TempFreez", temperatureFreez);
                //            command.Parameters.AddWithValue("@Timestamp", formattedTimestamp);
                //            command.Parameters.AddWithValue("@State", state);

                //            command.ExecuteNonQuery();
                //        }

                //        if (i % 100 == 0)
                //        {
                //            Console.WriteLine($"Inserted {i} records");
                //        }
                //    }
                //}




                if (Request.QueryString["Id"] != null)
                {
                    LoadConnectionInfo();
                    try
                    {
                        LoadAllChartData();
                    }
                    catch (Exception ex)
                    {
                        lblLastUpdate.Text = "DB Error: " + ex.Message;
                    }
                }

            }

        }

        private void LoadConnectionInfo()
        {
            string id = Request.QueryString["Id"];
            if (string.IsNullOrEmpty(id)) return;
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = $@"SELECT
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
                    WHERE mrc.Id = {id}";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string customerName = reader["CustomerName"] as string ?? "";
                                string customerMobile = reader["CustomerMobile"] as string ?? "";
                                string deviceTitle = reader["DeviceTitle"] as string ?? "";
                                string deviceNumber = reader["DeviceNumber"] as string ?? "";
                                string workshopName = reader["WorkshopName"] as string ?? "";
                                string problem = reader["ProblemDescription"] as string ?? "";
                                int receiptId = Convert.ToInt32(reader["CustomerReceiptId"]);
                                string createdAtFa = "";
                                if (reader["CreatedAt"] != DBNull.Value)
                                {
                                    var cal = new System.Globalization.PersianCalendar();
                                    var dt = (DateTimeOffset)reader["CreatedAt"];
                                    createdAtFa = $"{cal.GetYear(dt.DateTime)}/{cal.GetMonth(dt.DateTime):00}/{cal.GetDayOfMonth(dt.DateTime):00}";
                                }

                                litInfo.Text = $@"
                                <style>
                                    .info-card{{background:#fff;border-radius:10px;margin-bottom:12px;box-shadow:0 1px 4px rgba(0,0,0,0.08);font-family:Vazirmatn,Tahoma,sans-serif;font-size:13px;line-height:1.7;direction:rtl;text-align:right;overflow:hidden;}}
                                    .info-card-head{{background:linear-gradient(135deg,#00A8A8,#007a7a);color:#fff;padding:10px 14px;font-size:14px;font-weight:700;display:flex;align-items:center;gap:8px;}}
                                    .info-card-head svg{{flex-shrink:0;}}
                                    .info-card-body{{padding:10px 14px;}}
                                    .info-grid{{display:grid;grid-template-columns:1fr 1fr;gap:8px;}}
                                    .info-item{{display:flex;flex-direction:column;padding:6px 8px;background:#f8fafc;border-radius:6px;min-width:0;}}
                                    .info-item.full{{grid-column:1/-1;}}
                                    .info-item-label{{font-size:11px;color:#8896a6;font-weight:600;margin-bottom:2px;}}
                                    .info-item-value{{font-size:13px;color:#1e2a3a;font-weight:500;}}
                                    .info-item-value.ltr{{direction:ltr;text-align:left;unicode-bidi:embed;}}
                                    @media(max-width:480px){{.info-grid{{grid-template-columns:1fr;}}}}
                                </style>
                                <div class='info-card'>
                                    <div class='info-card-head'>
                                        <svg viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' width='16' height='16'>
                                            <rect x='3' y='3' width='18' height='18' rx='2'/><path d='M9 3v18M15 3v18M3 9h18M3 15h18'/>
                                        </svg>
                                        مشخصات مشتری و رسید
                                    </div>
                                    <div class='info-card-body'>
                                        <div class='info-grid'>
                                            <div class='info-item'><span class='info-item-label'>مشتری</span><span class='info-item-value'>{customerName}</span></div>
                                            <div class='info-item'><span class='info-item-label'>موبایل</span><span class='info-item-value ltr'>{customerMobile}</span></div>
                                            <div class='info-item'><span class='info-item-label'>دستگاه</span><span class='info-item-value'>{deviceTitle} ({deviceNumber})</span></div>
                                            <div class='info-item'><span class='info-item-label'>تعمیرگاه</span><span class='info-item-value'>{workshopName}</span></div>
                                            <div class='info-item'><span class='info-item-label'>شماره رسید</span><span class='info-item-value'>{receiptId}</span></div>
                                            <div class='info-item'><span class='info-item-label'>تاریخ شروع</span><span class='info-item-value'>{createdAtFa}</span></div>
                                            <div class='info-item full'><span class='info-item-label'>شرح مشکل</span><span class='info-item-value'>{problem}</span></div>
                                        </div>
                                    </div>
                                </div>";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                litInfo.Text = "<div style='color:red;'>Error loading info: " + ex.Message + "</div>";
            }
        }

        private void LoadAllChartData()
        {
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = $@"SELECT ChangeType, NewValue, DataSnapshot, CreatedAt
                    FROM MonitoringDeviceChangeLogs
                    WHERE MonitoringId = {Request.QueryString["Id"]}
                    ORDER BY Id";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);

                        List<double> temperatureValues = new List<double>();
                        List<string> timeLabels = new List<string>();
                        List<int> statusValues = new List<int>();
                        List<double> freezerValues = new List<double>();
                        List<int> heater1Values = new List<int>();
                        List<int> heater2Values = new List<int>();
                        List<int> powerValues = new List<int>();

                        int currentMotor = 0;
                        int currentHeater1 = 0;
                        int currentHeater2 = 0;
                        int currentPower = 1;
                        var jcal = new System.Globalization.PersianCalendar();

                        foreach (DataRow row in dt.Rows)
                        {
                            string changeType = row["ChangeType"] as string ?? "";
                            string newValue = row["NewValue"] as string ?? "";
                            string snapshot = row["DataSnapshot"] as string ?? "";
                            DateTime timestamp = Convert.ToDateTime(row["CreatedAt"]);

                            bool isOn = newValue.Trim().Equals("on", StringComparison.OrdinalIgnoreCase);
                            switch (changeType)
                            {
                                case "MotorState": currentMotor = isOn ? 1 : 0; break;
                                case "Element1": currentHeater1 = isOn ? 1 : 0; break;
                                case "Element2": currentHeater2 = isOn ? 1 : 0; break;
                                case "Priz": currentPower = isOn ? 1 : 0; break;
                            }

                            double? tempRef = null;
                            double? tempFreez = null;
                            if (!string.IsNullOrEmpty(snapshot))
                            {
                                try
                                {
                                    var json = Newtonsoft.Json.Linq.JObject.Parse(snapshot);
                                    var t1 = json["Temp1"];
                                    var t2 = json["Temp2"];
                                    if (t1 != null && double.TryParse(t1.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double tr))
                                        tempRef = tr;
                                    if (t2 != null && double.TryParse(t2.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double tf))
                                        tempFreez = tf;
                                }
                                catch { }
                            }

                            temperatureValues.Add(tempRef ?? 0);
                            freezerValues.Add(tempFreez ?? 0);
                            statusValues.Add(currentMotor);
                            heater1Values.Add(currentHeater1);
                            heater2Values.Add(currentHeater2);
                            powerValues.Add(currentPower);
                            timeLabels.Add($"{jcal.GetYear(timestamp)}/{jcal.GetMonth(timestamp):00}/{jcal.GetDayOfMonth(timestamp):00} {timestamp:HH:mm:ss}");
                        }

                        hdnTemperatureData.Value = JsonConvert.SerializeObject(temperatureValues);
                        hdnFreezerData.Value = JsonConvert.SerializeObject(freezerValues);
                        hdnStatusData.Value = JsonConvert.SerializeObject(statusValues);
                        hdnHeater1Data.Value = JsonConvert.SerializeObject(heater1Values);
                        hdnHeater2Data.Value = JsonConvert.SerializeObject(heater2Values);
                        hdnPowerData.Value = JsonConvert.SerializeObject(powerValues);
                        hdnTimeLabels.Value = JsonConvert.SerializeObject(timeLabels);

                        if (dt.Rows.Count > 0)
                        {
                            DateTime lastUpdate = Convert.ToDateTime(dt.Rows[dt.Rows.Count - 1]["CreatedAt"]);
                            lblLastUpdate.Text = "آخرین به‌روزرسانی: " + lastUpdate.ToString("yyyy/MM/dd HH:mm:ss");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lblLastUpdate.Text = "DB Error: " + ex.Message;
            }
        }

        private void LoadTemperatureData()
        {
            try
            {
                // دریافت داده‌های دما از دیتابیس
                DataTable dt = GetTemperatureData(50); // دریافت 50 داده آخر
                
                List<double> temperatureValues = new List<double>();
                List<string> timeLabels = new List<string>();
                
                foreach (DataRow row in dt.Rows)
                {
                    if (row["Temperature_ref"] == DBNull.Value) continue;
                    temperatureValues.Add(Convert.ToDouble(row["Temperature_ref"]));
                    DateTime timestamp = Convert.ToDateTime(row["Timestamp"]);
                    var jcal = new System.Globalization.PersianCalendar();
                    timeLabels.Add($"{jcal.GetYear(timestamp)}/{jcal.GetMonth(timestamp):00}/{jcal.GetDayOfMonth(timestamp):00} {timestamp:HH:mm:ss}");
                }
                
                // ذخیره داده‌ها در فیلدهای مخفی برای استفاده در جاوااسکریپت
                hdnTemperatureData.Value = JsonConvert.SerializeObject(temperatureValues);
                hdnTimeLabels.Value = JsonConvert.SerializeObject(timeLabels);
                
                if (dt.Rows.Count > 0 && dt.Rows[dt.Rows.Count - 1]["Timestamp"] != DBNull.Value)
                {
                    DateTime lastUpdate = Convert.ToDateTime(dt.Rows[dt.Rows.Count - 1]["Timestamp"]);
                    lblLastUpdate.Text = "آخرین به‌روزرسانی: " + lastUpdate.ToString("yyyy/MM/dd HH:mm:ss");
                }
            }
            catch (Exception ex)
            {
                // ثبت خطا
                System.Diagnostics.Debug.WriteLine("خطا در بارگذاری داده‌های دما: " + ex.Message);
            }
        }

        private void LoadStatusData()
        {
            try
            {
                // دریافت داده‌های وضعیت از دیتابیس
                DataTable dt = GetStatusData(50);
                
                List<int> statusValues = new List<int>();
                
                foreach (DataRow row in dt.Rows)
                {
                    if (row["Status"] == DBNull.Value) continue;
                    statusValues.Add(Convert.ToInt32(row["Status"]));
                }
                
                // ذخیره داده‌ها در فیلد مخفی برای استفاده در جاوااسکریپت
                hdnStatusData.Value = JsonConvert.SerializeObject(statusValues);
            }
            catch (Exception ex)
            {
                // ثبت خطا
                System.Diagnostics.Debug.WriteLine("خطا در بارگذاری داده‌های وضعیت: " + ex.Message);
            }
        }

        private void LoadFreezerData()
        {
            try
            {
                // دریافت داده‌های دمای فریزر از دیتابیس
                DataTable dt = GetFreezerData(50);
                
                List<double> freezerValues = new List<double>();
                
                foreach (DataRow row in dt.Rows)
                {
                    if (row["Temperature_freez"] == DBNull.Value) continue;
                    freezerValues.Add(Convert.ToDouble(row["Temperature_freez"]));
                }
                
                // ذخیره داده‌ها در فیلد مخفی برای استفاده در جاوااسکریپت
                hdnFreezerData.Value = JsonConvert.SerializeObject(freezerValues);
            }
            catch (Exception ex)
            {
                // ثبت خطا
                System.Diagnostics.Debug.WriteLine("خطا در بارگذاری داده‌های دمای فریزر: " + ex.Message);
            }
        }

        private void LoadHeater1Data()
        {
            try
            {
                // دریافت داده‌های هیتر ۱ از دیتابیس
                DataTable dt = GetHeater1Data(50);
                
                List<int> heater1Values = new List<int>();
                
                foreach (DataRow row in dt.Rows)
                {
                    if (row["Heater1Status"] == DBNull.Value) continue;
                    heater1Values.Add(Convert.ToInt32(row["Heater1Status"]));
                }
                
                // ذخیره داده‌ها در فیلد مخفی برای استفاده در جاوااسکریپت
                hdnHeater1Data.Value = JsonConvert.SerializeObject(heater1Values);
            }
            catch (Exception ex)
            {
                // ثبت خطا
                System.Diagnostics.Debug.WriteLine("خطا در بارگذاری داده‌های هیتر ۱: " + ex.Message);
            }
        }

        private void LoadHeater2Data()
        {
            try
            {
                // دریافت داده‌های هیتر ۲ از دیتابیس
                DataTable dt = GetHeater2Data(50);
                
                List<int> heater2Values = new List<int>();
                
                foreach (DataRow row in dt.Rows)
                {
                    if (row["Heater2Status"] == DBNull.Value) continue;
                    heater2Values.Add(Convert.ToInt32(row["Heater2Status"]));
                }
                
                // ذخیره داده‌ها در فیلد مخفی برای استفاده در جاوااسکریپت
                hdnHeater2Data.Value = JsonConvert.SerializeObject(heater2Values);
            }
            catch (Exception ex)
            {
                // ثبت خطا
                System.Diagnostics.Debug.WriteLine("خطا در بارگذاری داده‌های هیتر ۲: " + ex.Message);
            }
        }


        private void LoadPowerData()
        {
            try
            {
                // دریافت داده‌های هیتر ۲ از دیتابیس
                DataTable dt = GetPowerData(50);

                List<int> powerValues = new List<int>();

                foreach (DataRow row in dt.Rows)
                {
                    if (row["PowerStatus"] == DBNull.Value) continue;
                    powerValues.Add(Convert.ToInt32(row["PowerStatus"]));
                }

                // ذخیره داده‌ها در فیلد مخفی برای استفاده در جاوااسکریپت
                hdnPowerData.Value = JsonConvert.SerializeObject(powerValues);
            }
            catch (Exception ex)
            {
                // ثبت خطا
                System.Diagnostics.Debug.WriteLine("خطا در بارگذاری داده‌های هیتر ۲: " + ex.Message);
            }
        }

        private DataTable GetTemperatureData(int dataPoints)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Temperature_ref", typeof(double));
            dt.Columns.Add("Timestamp", typeof(DateTime));
            
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
            
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = $@"SELECT DataSnapshot, CreatedAt AS Timestamp
                    FROM MonitoringDeviceChangeLogs
                    WHERE MonitoringId = " + Request.QueryString["Id"] + " ORDER BY Id";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            DataTable raw = new DataTable();
                            adapter.Fill(raw);
                            foreach (DataRow row in raw.Rows)
                            {
                                string snapshot = row["DataSnapshot"] as string;
                                if (string.IsNullOrEmpty(snapshot)) continue;
                                try
                                {
                                    var json = Newtonsoft.Json.Linq.JObject.Parse(snapshot);
                                    var temp1Token = json["Temp1"];
                                    if (temp1Token != null && double.TryParse(temp1Token.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double temp1))
                                    {
                                        DataRow newRow = dt.NewRow();
                                        newRow["Temperature_ref"] = temp1;
                                        newRow["Timestamp"] = row["Timestamp"];
                                        dt.Rows.Add(newRow);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "debug_temp.txt"), ex.ToString());
            }
            
            return dt;
        }
        
        private DataTable GetStatusData(int dataPoints)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Status", typeof(int));
            dt.Columns.Add("Timestamp", typeof(DateTime));
            
            // اتصال به دیتابیس و دریافت داده‌ها
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
            
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    
                    string query = $@"SELECT
                        CASE WHEN LOWER(NewValue) = 'on' THEN 1 ELSE 0 END AS Status,
                        CreatedAt AS Timestamp
                    FROM MonitoringDeviceChangeLogs
                    WHERE MonitoringId = " + Request.QueryString["Id"] + " AND ChangeType = 'MotorState' ORDER BY Id";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            
            return dt;
        }
        
        private DataTable GetFreezerData(int dataPoints)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Temperature_freez", typeof(double));
            dt.Columns.Add("Timestamp", typeof(DateTime));
            
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
            
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = $@"SELECT DataSnapshot, CreatedAt AS Timestamp
                    FROM MonitoringDeviceChangeLogs
                    WHERE MonitoringId = " + Request.QueryString["Id"] + " ORDER BY Id";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            DataTable raw = new DataTable();
                            adapter.Fill(raw);
                            foreach (DataRow row in raw.Rows)
                            {
                                string snapshot = row["DataSnapshot"] as string;
                                if (string.IsNullOrEmpty(snapshot)) continue;
                                try
                                {
                                    var json = Newtonsoft.Json.Linq.JObject.Parse(snapshot);
                                    var temp2Token = json["Temp2"];
                                    if (temp2Token != null && double.TryParse(temp2Token.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double temp2))
                                    {
                                        DataRow newRow = dt.NewRow();
                                        newRow["Temperature_freez"] = temp2;
                                        newRow["Timestamp"] = row["Timestamp"];
                                        dt.Rows.Add(newRow);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            
            return dt;
        }
        
        private DataTable GetHeater1Data(int dataPoints)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Heater1Status", typeof(int));
            dt.Columns.Add("Timestamp", typeof(DateTime));
            
            // اتصال به دیتابیس و دریافت داده‌ها
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
            
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = $@"SELECT
                        CASE WHEN LOWER(NewValue) = 'on' THEN 1 ELSE 0 END AS Heater1Status,
                        CreatedAt AS Timestamp
                    FROM MonitoringDeviceChangeLogs
                    WHERE MonitoringId = " + Request.QueryString["Id"] + " AND ChangeType = 'Element1' ORDER BY Id";


                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            
            return dt;
        }
        
        private DataTable GetHeater2Data(int dataPoints)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Heater2Status", typeof(int));
            dt.Columns.Add("Timestamp", typeof(DateTime));
            
            // اتصال به دیتابیس و دریافت داده‌ها
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;
            
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();


                    string query = $@"SELECT
                        CASE WHEN LOWER(NewValue) = 'on' THEN 1 ELSE 0 END AS Heater2Status,
                        CreatedAt AS Timestamp
                    FROM MonitoringDeviceChangeLogs
                    WHERE MonitoringId = " + Request.QueryString["Id"] + " AND ChangeType = 'Element2' ORDER BY Id";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            
            return dt;
        }
        
        private DataTable GetPowerData(int dataPoints)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("PowerStatus", typeof(int));
            dt.Columns.Add("Timestamp", typeof(DateTime));

            // اتصال به دیتابیس و دریافت داده‌ها
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["errorservicechart"].ConnectionString;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();


                    string query = $@"SELECT
                        CASE WHEN LOWER(NewValue) = 'on' THEN 1 ELSE 0 END AS PowerStatus,
                        CreatedAt AS Timestamp
                    FROM MonitoringDeviceChangeLogs
                    WHERE MonitoringId = " + Request.QueryString["Id"] + " AND ChangeType = 'Priz' ORDER BY Id";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            
            return dt;
        }
    }
}