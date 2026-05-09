using log4net;
using MySql.Data.MySqlClient;
using Mysqlx.Crud;
using MySqlX.XDevAPI.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Windows.Forms.DataVisualization.Charting;
using static Mysqlx.Expect.Open.Types;
using static System.Collections.Specialized.BitVector32;

namespace EMS
{
    //策略的一个节点
    public class TacticsClass
    {
        //
        public DateTime startTime;
        public DateTime endTime;
        public string tType;
        public string PCSType;
        public int waValue;
        public DateTime strategyDate;//策略日期
    }

    //全部策列，策略类
    public class TacticsListClass
    {
        public static string[] PCSTypes = { "待机", "恒流", "恒压", "恒功率", "时段内均充均放" };
        public static string[] tTypes = { "待机", "充电", "放电" };
        //策略列表
        public volatile List<TacticsClass> TacticsList = new List<TacticsClass>();
        public DateTime WorkingDate = Convert.ToDateTime("2000-01-01 00:00:01");
        public bool TacticsOn = false;  //策略标识符
        public int ActiveIndex = -2;
        public AllEquipmentClass Parent = null;
        private Thread Thread_CheckTactics;
        private Thread Thread_CheckJFPG;


        private static ILog log = LogManager.GetLogger("TacticsClass");

        private static string BytesToHexNoSpaces(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            var sb = new System.Text.StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }

        private static string NormalizeHexString(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = s.Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);

            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c)) continue;
                if ((c >= '0' && c <= '9') ||
                    (c >= 'a' && c <= 'f') ||
                    (c >= 'A' && c <= 'F'))
                {
                    sb.Append(char.ToUpperInvariant(c));
                }
            }
            return sb.ToString();
        }

        private static bool HexStringEquals(string a, string b) =>
            string.Equals(NormalizeHexString(a), NormalizeHexString(b), StringComparison.Ordinal);


        public void TacticsClass(AllEquipmentClass aParent)
        {
            Parent = aParent;
        }


        #region 清理数据库中的过期费率设置
        public bool cleanJFPGFromMysql()
        {
            bool Result = true;
            // 使用参数化查询防止SQL注入
            string astrSQL = "DELETE from electrovalence where rTime < @strDate";

            try
            {
                // 使用DBConnect统一的参数化执行接口
                var parameters = new Dictionary<string, object> { { "@strDate", DateTime.Today } };
                int affectedRows = DBConnection.ExecSQLWithParams(astrSQL, parameters);

                if (affectedRows > 0)
                {
                    log.Error($"清除了 {affectedRows} 条过期电价策略");
                }
                else if (affectedRows == 0)
                {
                    log.Error("没有需要清除的过期电价策略");
                }
                else
                {
                    // affectedRows为-1表示执行失败
                    Result = false;
                }
            }
            catch (Exception ex)
            {
                log.Error("清除过期电价策略时发生错误：" + ex.Message);
                Result = false;
            }

            return Result;
        }

        public bool CleanJFPGFromMysqlKeepLatestExpiredDay()
        {
            bool result = true;

            try
            {
                string findLatestExpiredSql = @"
                    SELECT MAX(rTime)
                    FROM electrovalence
                    WHERE rTime < @Today";

                var findParams = new Dictionary<string, object>
                {
                    { "@Today", DateTime.Today }
                };

                var latestExpiredObj = DBConnection.QuerySingleValue(findLatestExpiredSql, findParams);
                DateTime? latestExpiredDate = null;
                if (latestExpiredObj != null && latestExpiredObj != DBNull.Value)
                {
                    latestExpiredDate = Convert.ToDateTime(latestExpiredObj).Date;
                }

                if (!latestExpiredDate.HasValue)
                {
                    //log.Warn("没有需要清理的过期电价策略");
                    return true;
                }

                string deleteSql = @"
                    DELETE FROM electrovalence
                    WHERE rTime < @Today
                      AND rTime < @KeepDate";

                var deleteParams = new Dictionary<string, object>
                {
                    { "@Today", DateTime.Today },
                    { "@KeepDate", latestExpiredDate.Value }
                };

                int affectedRows = DBConnection.ExecSQLWithParams(deleteSql, deleteParams);

                if (affectedRows >= 0)
                {
                    //log.Warn($"清理过期电价策略完成，删除了 {affectedRows} 条记录，保留了最近一天过期电价 {latestExpiredDate.Value:yyyy-MM-dd} 的所有电价设置");
                }
                else
                {
                    result = false;
                }
            }
            catch (Exception ex)
            {
                log.Error("清理过期电价策略并保留最近一天失败：" + ex.Message);
                result = false;
            }

            return result;
        }
        #endregion

        #region 从数据库加载费率设置
        public bool LoadJFPGFromSQL()
        {
            bool res = true;

            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);

            // 1. 读取今日 / 明日电价
            List<DataRow> todayRows;
            List<DataRow> tomorrowRows;

            if (!LoadTodayTomorrowRows(today, tomorrow, out todayRows, out tomorrowRows))
                return false;

            // ========== ① 调度 Elemeter2 ==========
            var esMeter = frmMain.Selffrm.AllEquipment.Elemeter2;
            if (esMeter != null)
            {
                string meterType;
                string todaySig, tomorrowSig;

                if (esMeter.Version == 8)
                {
                    meterType = "ES_8";
                    todaySig = BuildDayRateSignature(todayRows, true, 8);
                    tomorrowSig = BuildDayRateSignature(tomorrowRows, true, 8);
                }
                else
                {
                    meterType = "ES_4";
                    todaySig = BuildDayRateSignature(todayRows, false, 4);
                    tomorrowSig = BuildDayRateSignature(tomorrowRows, false, 4);
                }

                var schedule = frmSet.LoadRateTableSchedule(meterType);

                DecideSlots(
                    todaySig, tomorrowSig,
                    schedule,
                    out int todaySlot,
                    out int tomorrowSlot);

                HandleElemeter2(
                    today, tomorrow,
                    todayRows, tomorrowRows,
                    todaySlot, tomorrowSlot,
                    ref res);

                if (res)
                    frmSet.SaveRateTableSchedule(meterType, todaySlot, tomorrowSlot);
            }

            // ========== ② 调度 Elemeter3 ==========
            var auxMeter = frmMain.Selffrm.AllEquipment.Elemeter3;
            if (auxMeter != null)
            {
                string meterType = "AUX_4";

                string todaySig = BuildDayRateSignature(todayRows, false, 4);
                string tomorrowSig = BuildDayRateSignature(tomorrowRows, false, 4);

                var schedule = frmSet.LoadRateTableSchedule(meterType);

                DecideSlots(
                    todaySig, tomorrowSig,
                    schedule,
                    out int todaySlot,
                    out int tomorrowSlot);

                HandleElemeter3(
                    today, tomorrow,
                    todayRows, tomorrowRows,
                    todaySlot, tomorrowSlot,
                    ref res);

                if (res)
                    frmSet.SaveRateTableSchedule(meterType, todaySlot, tomorrowSlot);
            }

            return res;
        }

        private bool LoadEffectiveTodayTomorrowRows(
            DateTime today,
            DateTime tomorrow,
            out List<DataRow> todayRows,
            out List<DataRow> tomorrowRows)
        {
            todayRows = new List<DataRow>();
            tomorrowRows = new List<DataRow>();

            try
            {
                if (!LoadTodayTomorrowRows(today, tomorrow, out todayRows, out tomorrowRows))
                    return false;

                // 今日没有，向前找最近历史完整日
                if (todayRows == null || todayRows.Count == 0)
                {
                    if (!LoadNearestPreviousFullDayRows(today, out todayRows))
                        return false;
                }

                // 明日没有，沿用“今日有效配置”
                if ((tomorrowRows == null || tomorrowRows.Count == 0) &&
                    todayRows != null && todayRows.Count > 0)
                {
                    tomorrowRows = new List<DataRow>(todayRows);
                    //log.Warn($"LoadEffectiveTodayTomorrowRows: 明日无时段设置，沿用今日有效时段设置，tomorrow={tomorrow:yyyy-MM-dd}");
                }

                //log.Warn($"LoadEffectiveTodayTomorrowRows: today={todayRows.Count}, tomorrow={tomorrowRows.Count}");
                return true;
            }
            catch (Exception ex)
            {
                log.Error("LoadEffectiveTodayTomorrowRows Exception: " + ex);
                return false;
            }
        }

        private bool LoadTodayTomorrowRows(
            DateTime today,
            DateTime tomorrow,
            out List<DataRow> todayRows,
            out List<DataRow> tomorrowRows)
        {
            todayRows = new List<DataRow>();
            tomorrowRows = new List<DataRow>();

            string sql =
                "SELECT rTime, startTime, eName " +
                "FROM electrovalence " +
                "WHERE rTime = @today OR rTime = @tomorrow " +
                "ORDER BY rTime, startTime";

            var param = new Dictionary<string, object>
            {
                { "@today", today },
                { "@tomorrow", tomorrow }
            };

            DataTable dt;
            try
            {
                dt = DBConnection.QueryDataTableWithParams(sql, param);
                if (dt == null)
                {
                    log.Error("LoadTodayTomorrowRows: 查询结果为空");
                    return false;
                }
            }
            catch (Exception ex)
            {
                log.Error("LoadTodayTomorrowRows Exception: " + ex);
                return false;
            }

            foreach (DataRow r in dt.Rows)
            {
                DateTime d = Convert.ToDateTime(r["rTime"]).Date;
                if (d == today)
                    todayRows.Add(r);
                else if (d == tomorrow)
                    tomorrowRows.Add(r);
            }

/*            log.Info(
                $"LoadTodayTomorrowRows: today={todayRows.Count}, tomorrow={tomorrowRows.Count}");*/

            return true;
        }

        private bool LoadNearestPreviousFullDayRows(
            DateTime targetDate,
            out List<DataRow> rows)
        {
            rows = new List<DataRow>();

            try
            {
                string findDateSql =
                    "SELECT rTime, COUNT(1) AS rowCount " +
                    "FROM electrovalence " +
                    "WHERE rTime < @targetDate " +
                    "GROUP BY rTime " +
                    "HAVING COUNT(1) > 0 " +
                    "ORDER BY rTime DESC";

                var findDateParams = new Dictionary<string, object>
                {
                    { "@targetDate", targetDate }
                };

                DataTable candidateTable = DBConnection.QueryDataTableWithParams(findDateSql, findDateParams);
                if (candidateTable == null || candidateTable.Rows.Count == 0)
                {
                    log.Info($"LoadNearestPreviousFullDayRows: {targetDate:yyyy-MM-dd} 之前没有历史时段设置");
                    return true;
                }

                foreach (DataRow candidate in candidateTable.Rows)
                {
                    DateTime historyDate = Convert.ToDateTime(candidate["rTime"]).Date;

                    string loadSql =
                        "SELECT rTime, startTime, eName " +
                        "FROM electrovalence " +
                        "WHERE rTime = @historyDate " +
                        "ORDER BY startTime";

                    var loadParams = new Dictionary<string, object>
                    {
                        { "@historyDate", historyDate }
                    };

                    DataTable dt = DBConnection.QueryDataTableWithParams(loadSql, loadParams);
                    if (dt == null || dt.Rows.Count == 0)
                    {
                        continue;
                    }

                    foreach (DataRow r in dt.Rows)
                    {
                        rows.Add(r);
                    }

                    log.Info($"LoadNearestPreviousFullDayRows: {targetDate:yyyy-MM-dd} 无时段设置，沿用最近历史日期 {historyDate:yyyy-MM-dd} 的 {rows.Count} 条时段设置");
                    return true;
                }

                return true;
            }
            catch (Exception ex)
            {
                log.Error("LoadNearestPreviousFullDayRows Exception: " + ex);
                return false;
            }
        }

        private string BuildDayRateSignature(
            List<DataRow> rows,
            bool hourFirst,
            int maxRateNo)
        {
            byte[] arr = BuildRateArray(rows, hourFirst, maxRateNo);
            return BitConverter.ToString(arr);
        }

        private void DecideSlots(
            string todaySig,
            string tomorrowSig,
            Dictionary<DateTime, int> schedule,
            out int todaySlot,
            out int tomorrowSlot)
        {
            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);

            // ========= 1. 确定 todaySlot（只读） =========
            if (!schedule.TryGetValue(today, out todaySlot))
            {
                todaySlot = 1; // 默认起点
            }

            // ========= 2. 决定 tomorrowSlot =========

            // 情况 A：指纹相同 → 必须同 Slot
            if (todaySig == tomorrowSig)
            {
                tomorrowSlot = todaySlot;
                return;
            }

            // 情况 B：指纹不同 → 必须不同 Slot
            int expectedTomorrowSlot = (todaySlot == 1) ? 2 : 1;

            // 如果调度表已有 tomorrow
            if (schedule.TryGetValue(tomorrow, out int scheduledTomorrowSlot))
            {
                // ✅ 若符合预期，沿用
                if (scheduledTomorrowSlot == expectedTomorrowSlot)
                {
                    tomorrowSlot = scheduledTomorrowSlot;
                }
                else
                {
                    // ❌ 不符合 → 修正
                    tomorrowSlot = expectedTomorrowSlot;
                }
            }
            else
            {
                // 没有历史记录 → 直接用预期 Slot
                tomorrowSlot = expectedTomorrowSlot;
            }
        }

        private void HandleElemeter2(
            DateTime today, DateTime tomorrow,
            List<DataRow> todayRows, List<DataRow> tomorrowRows,
            int todaySlot, int tomorrowSlot,
            ref bool res)
        {
            var meter = frmMain.Selffrm.AllEquipment.Elemeter2;
            if (meter == null) return;

            // ========= 八费率 =========
            if (meter.Version == 8)
            {
                byte[] todayRate = BuildRateArray(todayRows, true, 8);
                byte[] tomorrowRate = BuildRateArray(tomorrowRows, true, 8);

                // Zone 表：直接使用 Slot
                byte[] zone = BuildZoneTable8(
                    todaySlot, today,
                    tomorrowSlot, tomorrow);

                if (!meter.SetZone8Rates(zone))
                {
                    res = false;
                    return;
                }

                // 写今天
                if (todaySlot == 1)
                    res &= meter.SetRates8Tier_1(todayRate);
                else
                    res &= meter.SetRates8Tier_2(todayRate);

                // 写明天
                if (tomorrowSlot == 1)
                    res &= meter.SetRates8Tier_1(tomorrowRate);
                else
                    res &= meter.SetRates8Tier_2(tomorrowRate);
            }
            // ========= 四费率 =========
            else
            {
                byte[] todayRate = BuildRateArray(todayRows, false, 4);
                byte[] tomorrowRate = BuildRateArray(tomorrowRows, false, 4);

                byte[] zone = BuildZoneTable4_ES(
                    todaySlot, today,
                    tomorrowSlot, tomorrow);

                if (!meter.SetZone4Rates(zone))
                {
                    res = false;
                    return;
                }

                // 写今天
                if (todaySlot == 1)
                    res &= meter.SetRates4Tier_3(todayRate);
                else
                    res &= meter.SetRates4Tier_4(todayRate);

                // 写明天
                if (tomorrowSlot == 1)
                    res &= meter.SetRates4Tier_3(tomorrowRate);
                else
                    res &= meter.SetRates4Tier_4(tomorrowRate);
            }
        }


        private void HandleElemeter3(
            DateTime today, DateTime tomorrow,
            List<DataRow> todayRows, List<DataRow> tomorrowRows,
            int todaySlot, int tomorrowSlot,
            ref bool res)
        {
            var meter = frmMain.Selffrm.AllEquipment.Elemeter3;
            if (meter == null) return;

            byte[] todayRate = BuildRateArray(todayRows, false, 4);
            byte[] tomorrowRate = BuildRateArray(tomorrowRows, false, 4);

            byte[] zone = BuildZoneTable4(
                todaySlot, today,
                tomorrowSlot, tomorrow);

            if (!meter.SetZone4Rates(zone))
            {
                res = false;
                return;
            }

            // 写今天
            if (todaySlot == 1)
                res &= meter.SetRates4Tier_1(todayRate);
            else
                res &= meter.SetRates4Tier_2(todayRate);

            // 写明天
            if (tomorrowSlot == 1)
                res &= meter.SetRates4Tier_1(tomorrowRate);
            else
                res &= meter.SetRates4Tier_2(tomorrowRate);
        }

        private byte[] BuildRateArray(
            List<DataRow> rows,
            bool hourFirst,
            int maxRateNo)
        {
            byte[] arr = new byte[42];
            int i = 0;

            foreach (var row in rows)
            {
                if (i >= 14) break;

                byte rate = Convert.ToByte(row["eName"]);
                DateTime t = Convert.ToDateTime("2022-01-01 " + row["startTime"]);

                if (rate > maxRateNo)
                {
                    arr[i * 3] = 0;
                    arr[i * 3 + 1] = 0;
                    arr[i * 3 + 2] = 0;
                }
                else
                {
                    arr[i * 3] = rate;
                    arr[i * 3 + 1] = hourFirst ? (byte)t.Hour : (byte)t.Minute;
                    arr[i * 3 + 2] = hourFirst ? (byte)t.Minute : (byte)t.Hour;
                }
                i++;
            }
            return arr;
        }

        private byte[] BuildZoneTable8(
            int todayTable, DateTime today,
            int tomorrowTable, DateTime tomorrow)
        {
            byte[] arr = new byte[42];

            arr[0] = (byte)todayTable;
            arr[1] = (byte)today.Month;
            arr[2] = (byte)today.Day;

            arr[3] = (byte)tomorrowTable;
            arr[4] = (byte)tomorrow.Month;
            arr[5] = (byte)tomorrow.Day;

            return arr;
        }

        private byte[] BuildZoneTable4(
            int todayTable, DateTime today,
            int tomorrowTable, DateTime tomorrow)
        {
            byte[] arr = new byte[12];

            arr[0] = (byte)todayTable;
            arr[1] = (byte)today.Day;
            arr[2] = (byte)today.Month;

            arr[3] = (byte)tomorrowTable;
            arr[4] = (byte)tomorrow.Day;
            arr[5] = (byte)tomorrow.Month;

            return arr;
        }

        private byte[] BuildZoneTable4_ES(
            int todayTable, DateTime today,
            int tomorrowTable, DateTime tomorrow)
        {
            byte[] arr = new byte[12];

            arr[0] = (byte)(todayTable == 1 ? 3 : 4);
            arr[1] = (byte)today.Day;
            arr[2] = (byte)today.Month;

            arr[3] = (byte)(tomorrowTable == 1 ? 3 : 4);
            arr[4] = (byte)tomorrow.Day;
            arr[5] = (byte)tomorrow.Month;

            return arr;
        }

        private byte[] BuildZoneTable4_AUX(
            int todayTable, DateTime today,
            int tomorrowTable, DateTime tomorrow)
        {
            byte[] arr = new byte[12];

            arr[0] = (byte)todayTable;
            arr[1] = (byte)today.Day;
            arr[2] = (byte)today.Month;

            arr[3] = (byte)tomorrowTable;
            arr[4] = (byte)tomorrow.Day;
            arr[5] = (byte)tomorrow.Month;

            return arr;
        }
        #endregion

        #region 从数据库中加载时区设置&费率设置 （修改下发）
        public bool LoadJFPGFromSQL_WithCompare()
        {
            bool res = true;

            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);

            List<DataRow> todayRows;
            List<DataRow> tomorrowRows;

            CleanJFPGFromMysqlKeepLatestExpiredDay();

            if (!LoadEffectiveTodayTomorrowRows(today, tomorrow, out todayRows, out tomorrowRows))
                return false;

            if ((todayRows == null || todayRows.Count == 0) ||
                (tomorrowRows == null || tomorrowRows.Count == 0))
            {
                // 不做任何下发：（1）不存在任何历史时段设置导致今日没有时段配置，即使有明日的时段配置也不做下发
                //               （2）如果今日有时段配置（今日配置或历史配置）且明日没有时段设置，强校验明日时段设置默认为今日相同时段设置
                //log.Warn("LoadJFPGFromSQL_NewWithCompare: 今日/明日无电价策略");    
                return true;
            }

            // Elemeter2
            var esMeter = frmMain.Selffrm.AllEquipment.Elemeter2;
            if (esMeter != null)
            {
                bool meterRes = true;
                string meterType;
                string todaySig, tomorrowSig;

                if (esMeter.Version == 8)
                {
                    meterType = "ES_8";
                    todaySig = BuildDayRateSignature(todayRows, true, 8);
                    tomorrowSig = BuildDayRateSignature(tomorrowRows, true, 8);
                }
                else
                {
                    meterType = "ES_4";
                    todaySig = BuildDayRateSignature(todayRows, false, 4);
                    tomorrowSig = BuildDayRateSignature(tomorrowRows, false, 4);
                }

                var schedule = frmSet.LoadRateTableSchedule(meterType);

                DecideSlots(todaySig, tomorrowSig, schedule, out int todaySlot, out int tomorrowSlot);

                HandleElemeter2_WithCompare(
                    today, tomorrow,
                    todayRows, tomorrowRows,
                    todaySlot, tomorrowSlot,
                    ref meterRes);

                res &= meterRes;

                if (meterRes)
                    frmSet.SaveRateTableSchedule(meterType, todaySlot, tomorrowSlot);
            }

            // Elemeter3
            var auxMeter = frmMain.Selffrm.AllEquipment.Elemeter3;
            if (auxMeter != null)
            {
                bool meterRes = true;
                string meterType = "AUX_4";

                string todaySig = BuildDayRateSignature(todayRows, false, 4);
                string tomorrowSig = BuildDayRateSignature(tomorrowRows, false, 4);

                var schedule = frmSet.LoadRateTableSchedule(meterType);

                DecideSlots(todaySig, tomorrowSig, schedule, out int todaySlot, out int tomorrowSlot);

                HandleElemeter3_WithCompare(
                    today, tomorrow,
                    todayRows, tomorrowRows,
                    todaySlot, tomorrowSlot,
                    ref meterRes);

                res &= meterRes;

                if (meterRes)
                    frmSet.SaveRateTableSchedule(meterType, todaySlot, tomorrowSlot);
            }

            return res;
        }

        private void HandleElemeter2_WithCompare(
            DateTime today, DateTime tomorrow,
            List<DataRow> todayRows, List<DataRow> tomorrowRows,
            int todaySlot, int tomorrowSlot,
            ref bool res)
        {
            try
            {
                var meter = frmMain.Selffrm.AllEquipment.Elemeter2;
                if (meter == null) return;

                string ToHex(byte[] b) =>
                    b == null ? string.Empty : BitConverter.ToString(b).Replace("-", "");

                // ========= 八费率 =========
                if (meter.Version == 8)
                {
                    byte[] todayRate = BuildRateArray(todayRows, true, 8);
                    byte[] tomorrowRate = BuildRateArray(tomorrowRows, true, 8);

                    byte[] zone = BuildZoneTable8(
                        todaySlot, today,
                        tomorrowSlot, tomorrow);

                    string zoneHex = ToHex(zone);
                    string todayHex = ToHex(todayRate);
                    string tomorrowHex = ToHex(tomorrowRate);

                    // 1. Zone 比较后下发
                    if (!string.Equals(zoneHex, meter.zone8Rates, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!meter.SetZone8Rates(zone))
                        {
                            res = false;
                            return;
                        }
                        //log.Warn($"Elemeter2[8]: Zone8 changed ={zoneHex}, write success. slot(today={todaySlot}, tomorrow={tomorrowSlot})");
                    }
                    else
                    {
                        //log.Warn($"Elemeter2[8]: Zone8 unchanged = {zoneHex}, skip write.");
                    }

                    // 2. 今日费率表比较后下发
                    if (todaySlot == 1)
                    {
                        if (!string.Equals(todayHex, meter.rates8Tier_1, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!meter.SetRates8Tier_1(todayRate))
                            {
                                res = false;
                            }
                            else
                            {
                                //log.Warn($"Elemeter2[8]: today rate -> Tier_1 changed = {todayHex}, write success.");
                            }
                        }
                        else
                        {
                            //log.Warn($"Elemeter2[8]: today rate -> Tier_1 unchanged = {todayHex}, skip write.");
                        }
                    }
                    else
                    {
                        if (!string.Equals(todayHex, meter.rates8Tier_2, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!meter.SetRates8Tier_2(todayRate))
                            {
                                res = false;
                            }
                            else
                            {
                                //log.Warn($"Elemeter2[8]: today rate -> Tier_2 changed = {todayHex}, write success.");
                            }
                        }
                        else
                        {
                            //log.Warn($"Elemeter2[8]: today rate -> Tier_2 unchanged = {todayHex}, skip write.");
                        }
                    }

                    // 3. 明日费率表比较后下发
                    if (tomorrowSlot == 1)
                    {
                        if (!string.Equals(tomorrowHex, meter.rates8Tier_1, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!meter.SetRates8Tier_1(tomorrowRate))
                            {
                                res = false;
                            }
                            else
                            {
                                //log.Warn($"Elemeter2[8]: tomorrow rate -> Tier_1 changed = {tomorrowHex}, write success.");
                            }
                        }
                        else
                        {
                            //log.Warn($"Elemeter2[8]: tomorrow rate -> Tier_1 unchanged = {tomorrowHex}, skip write.");
                        }
                    }
                    else
                    {
                        if (!string.Equals(tomorrowHex, meter.rates8Tier_2, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!meter.SetRates8Tier_2(tomorrowRate))
                            {
                                res = false;
                            }
                            else
                            {
                                //log.Warn($"Elemeter2[8]: tomorrow rate -> Tier_2 changed = {tomorrowHex}, write success.");
                            }
                        }
                        else
                        {
                            //log.Warn($"Elemeter2[8]: tomorrow rate -> Tier_2 unchanged = {tomorrowHex}, skip write.");
                        }
                    }
                }
                // ========= 四费率 =========
                else
                {
                    byte[] todayRate = BuildRateArray(todayRows, false, 4);
                    byte[] tomorrowRate = BuildRateArray(tomorrowRows, false, 4);

                    byte[] zone = BuildZoneTable4_ES(
                        todaySlot, today,
                        tomorrowSlot, tomorrow);

                    string zoneHex = ToHex(zone);
                    string todayHex = ToHex(todayRate);
                    string tomorrowHex = ToHex(tomorrowRate);

                    // 1. Zone 比较后下发
                    if (!string.Equals(zoneHex, meter.zone4Rates, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!meter.SetZone4Rates(zone))
                        {
                            res = false;
                            return;
                        }
                        //log.Warn($"Elemeter2[4]: Zone4 changed = {zoneHex}, write success. slot(today={todaySlot}, tomorrow={tomorrowSlot})");
                    }
                    else
                    {
                        //log.Warn($"Elemeter2[4]: Zone4 unchanged = {zoneHex}, skip write.");
                    }

                    // 2. 今日费率表比较后下发
                    if (todaySlot == 1)
                    {
                        if (!string.Equals(todayHex, meter.rates4Tier_3, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!meter.SetRates4Tier_3(todayRate))
                            {
                                res = false;
                            }
                            else
                            {
                                //log.Warn($"Elemeter2[4]: today rate -> Tier_1 changed = {todayHex}, write success.");
                            }
                        }
                        else
                        {
                            //log.Warn($"Elemeter2[4]: today rate -> Tier_1 unchanged  = {todayHex}, skip write.");
                        }
                    }
                    else
                    {
                        if (!string.Equals(todayHex, meter.rates4Tier_4, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!meter.SetRates4Tier_4(todayRate))
                            {
                                res = false;
                            }
                            else
                            {
                                //log.Warn($"Elemeter2[4]: today rate -> Tier_2 changed  = {todayHex}, write success.");
                            }
                        }
                        else
                        {
                            //log.Warn($"Elemeter2[4]: today rate -> Tier_2 unchanged  = {todayHex}, skip write.");
                        }
                    }

                    // 3. 明日费率表比较后下发
                    if (tomorrowSlot == 1)
                    {
                        if (!string.Equals(tomorrowHex, meter.rates4Tier_3, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!meter.SetRates4Tier_3(tomorrowRate))
                            {
                                res = false;
                            }
                            else
                            {
                                //log.Warn($"Elemeter2[4]: tomorrow rate -> Tier_1 changed = {tomorrowHex}, write success.");
                            }
                        }
                        else
                        {
                            //log.Warn($"Elemeter2[4]: tomorrow rate -> Tier_1 unchanged  = {tomorrowHex}, skip write.");
                        }
                    }
                    else
                    {
                        if (!string.Equals(tomorrowHex, meter.rates4Tier_4, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!meter.SetRates4Tier_4(tomorrowRate))
                            {
                                res = false;
                            }
                            else
                            {
                                //log.Warn($"Elemeter2[4]: tomorrow rate -> Tier_2 changed  = {tomorrowHex}, write success.");
                            }
                        }
                        else
                        {
                            //log.Warn($"Elemeter2[4]: tomorrow rate -> Tier_2 unchanged  = {tomorrowHex}, skip write.");
                        }
                    }
                }
            } catch (Exception ex)
            {
                log.Error("HandleElemeter2_WithCompare: " + ex);
            }
        }

        private void HandleElemeter3_WithCompare(
            DateTime today, DateTime tomorrow,
            List<DataRow> todayRows, List<DataRow> tomorrowRows,
            int todaySlot, int tomorrowSlot,
            ref bool res)
        {
            try
            {
                var meter = frmMain.Selffrm.AllEquipment.Elemeter3;
                if (meter == null) return;

                string ToHex(byte[] b) =>
                    b == null ? string.Empty : BitConverter.ToString(b).Replace("-", "");

                byte[] todayRate = BuildRateArray(todayRows, false, 4);
                byte[] tomorrowRate = BuildRateArray(tomorrowRows, false, 4);

                byte[] zone = BuildZoneTable4(
                    todaySlot, today,
                    tomorrowSlot, tomorrow);

                string zoneHex = ToHex(zone);
                string todayHex = ToHex(todayRate);
                string tomorrowHex = ToHex(tomorrowRate);

                // 1. Zone 比较后下发
                if (!string.Equals(zoneHex, meter.zone4Rates, StringComparison.OrdinalIgnoreCase))
                {
                    if (!meter.SetZone4Rates(zone))
                    {
                        res = false;
                        return;
                    }
                    meter.zone4Rates = zoneHex;
                    //log.Warn($"Elemeter3: Zone4 changed, write success. slot(today={todaySlot}, tomorrow={tomorrowSlot})");
                }
                else
                {
                    //log.Warn("Elemeter3: Zone4 unchanged, skip write.");
                }

                // 2. 今日费率表比较后下发
                if (todaySlot == 1)
                {
                    if (!string.Equals(todayHex, meter.rates4Tier_1, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!meter.SetRates4Tier_1(todayRate))
                        {
                            res = false;
                        }
                        else
                        {
                            meter.rates4Tier_1 = todayHex;
                            //log.Warn("Elemeter3: today rate -> Tier_1 changed, write success.");
                        }
                    }
                    else
                    {
                        //log.Warn("Elemeter3: today rate -> Tier_1 unchanged, skip write.");
                    }
                }
                else
                {
                    if (!string.Equals(todayHex, meter.rates4Tier_2, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!meter.SetRates4Tier_2(todayRate))
                        {
                            res = false;
                        }
                        else
                        {
                            meter.rates4Tier_2 = todayHex;
                            //log.Warn("Elemeter3: today rate -> Tier_2 changed, write success.");
                        }
                    }
                    else
                    {
                        //log.Warn("Elemeter3: today rate -> Tier_2 unchanged, skip write.");
                    }
                }

                // 3. 明日费率表比较后下发
                if (tomorrowSlot == 1)
                {
                    if (!string.Equals(tomorrowHex, meter.rates4Tier_1, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!meter.SetRates4Tier_1(tomorrowRate))
                        {
                            res = false;
                        }
                        else
                        {
                            meter.rates4Tier_1 = tomorrowHex;
                            //log.Warn("Elemeter3: tomorrow rate -> Tier_1 changed, write success.");
                        }
                    }
                    else
                    {
                        //log.Warn("Elemeter3: tomorrow rate -> Tier_1 unchanged, skip write.");
                    }
                }
                else
                {
                    if (!string.Equals(tomorrowHex, meter.rates4Tier_2, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!meter.SetRates4Tier_2(tomorrowRate))
                        {
                            res = false;
                        }
                        else
                        {
                            meter.rates4Tier_2 = tomorrowHex;
                            //log.Warn("Elemeter3: tomorrow rate -> Tier_2 changed, write success.");
                        }
                    }
                    else
                    {
                        //log.Warn("Elemeter3: tomorrow rate -> Tier_2 unchanged, skip write.");
                    }
                }
            }
            catch (Exception ex) {
                log.Error("HandleElemeter3_WithCompare: " + ex);
            }
        }

        #endregion

        #region 电表校时
        /// <summary>
        /// 比较电表时间与当前时间，偏差超过5分钟才执行校时
        /// </summary>
        public bool LoadTimeValue_CompareAndSendIfDiff()
        {
            bool result = true;
            const int maxTimeDiffMinutes = 5;

            try
            {
                // Elemeter2 校时（秒分时日月年顺序）
                var e2 = frmMain.Selffrm.AllEquipment.Elemeter2;
                if (e2 != null && e2.Prepared && !string.IsNullOrEmpty(e2.sysTimeSettings))
                {
                    DateTime? e2Time = ParseElemeter2Time(e2.sysTimeSettings);
                    if (e2Time.HasValue)
                    {
                        TimeSpan diff = DateTime.Now - e2Time.Value;
                        double diffMinutes = Math.Abs(diff.TotalMinutes);

                        if (diffMinutes > maxTimeDiffMinutes)
                        {
                            //log.Warn($"Elemeter2 时间偏差 {diffMinutes:F1} 分钟，执行校时。设备时间: {e2Time.Value:yyyy-MM-dd HH:mm:ss}，系统时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                            e2.timing(73);
                        }

                        //log.Warn("e2Time: " + e2Time + "diffMinutes: " + diffMinutes);
                    }

                }

                // Elemeter3 校时（年月日时分秒顺序）
                var e3 = frmMain.Selffrm.AllEquipment.Elemeter3;
                if (e3 != null && e3.Prepared && !string.IsNullOrEmpty(e3.sysTimeSettings))
                {
                    DateTime? e3Time = ParseElemeter3Time(e3.sysTimeSettings);
                    if (e3Time.HasValue)
                    {
                        TimeSpan diff = DateTime.Now - e3Time.Value;
                        double diffMinutes = Math.Abs(diff.TotalMinutes);

                        if (diffMinutes > maxTimeDiffMinutes)
                        {
                            //log.Warn($"Elemeter3 时间偏差 {diffMinutes:F1} 分钟，执行校时。设备时间: {e3Time.Value:yyyy-MM-dd HH:mm:ss}，系统时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                            e3.timing(47);
                        }

                        //log.Warn("e3Time: " + e3Time + "diffMinutes: " + diffMinutes);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                log.Error($"LoadTimeValue_CompareAndSendIfDiff 失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 解析 Elemeter2 时间字符串（格式：SSMMHHDDMMYY，秒分时日月年）
        /// </summary>
        private DateTime? ParseElemeter2Time(string hexString)
        {
            try
            {
                if (string.IsNullOrEmpty(hexString) || hexString.Length < 12)
                    return null;

                // 十六进制字符串转字节数组
                int second = Convert.ToInt32(hexString.Substring(0, 2), 16);
                int minute = Convert.ToInt32(hexString.Substring(2, 2), 16);
                int hour = Convert.ToInt32(hexString.Substring(4, 2), 16);
                int day = Convert.ToInt32(hexString.Substring(6, 2), 16);
                int month = Convert.ToInt32(hexString.Substring(8, 2), 16);
                int year = Convert.ToInt32(hexString.Substring(10, 2), 16) + 2000; // 年份需要加2000

                return new DateTime(year, month, day, hour, minute, second);
            }
            catch (Exception ex)
            {
                log.Error($"ParseElemeter2Time 解析失败: {ex.Message}, 输入: {hexString}");
                return null;
            }
        }

        /// <summary>
        /// 解析 Elemeter3 时间字符串（格式：YYMMDDHHMMSS，年月日时分秒）
        /// </summary>
        private DateTime? ParseElemeter3Time(string hexString)
        {
            try
            {
                if (string.IsNullOrEmpty(hexString) || hexString.Length < 12)
                    return null;

                // 十六进制字符串转字节数组
                int year = Convert.ToInt32(hexString.Substring(0, 2), 16) + 2000; // 年份需要加2000
                int month = Convert.ToInt32(hexString.Substring(2, 2), 16);
                int day = Convert.ToInt32(hexString.Substring(4, 2), 16);
                int hour = Convert.ToInt32(hexString.Substring(6, 2), 16);
                int minute = Convert.ToInt32(hexString.Substring(8, 2), 16);
                int second = Convert.ToInt32(hexString.Substring(10, 2), 16);

                return new DateTime(year, month, day, hour, minute, second);
            }
            catch (Exception ex)
            {
                log.Error($"ParseElemeter3Time 解析失败: {ex.Message}, 输入: {hexString}");
                return null;
            }
        }
        #endregion

        #region 费率监视线程
        public bool AutoCheckJFPG()
        {
            try
            {
                Thread_CheckJFPG = new Thread(CheckJFPG);
                Thread_CheckJFPG.IsBackground = true;
                Thread_CheckJFPG.Name = "AutoCheckJFPG";
                Thread_CheckJFPG.Priority = ThreadPriority.Highest;
                Thread_CheckJFPG.Start();
                return true;
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
                return false;
            }
        }

        private void CheckJFPG()
        {
            log.Error("启动监听费率");
            while (true)
            {
                try
                {
                    LoadJFPGFromSQL_WithCompare();

                    LoadTimeValue_CompareAndSendIfDiff();
                    Thread.Sleep(120000);
                }
                catch (Exception ex)
                {
                    log.Error("CheckJFPG: "+ ex.Message);
                }
            }
        }
        #endregion


        public bool LoadMasterDailyTactics()
        {
            bool res = false;
            if (frmMain.TacticsList != null && frmSet.config.IsMaster == 1)
            {
                try
                {
                    if (frmMain.Selffrm.AllEquipment.mqttManager != null && frmMain.Selffrm.AllEquipment.mqttManager.CurrentState != MqttState.Connected)
                    {
                        log.Error("监测到与云Broker连接中断，使用情况1来装载策略");
                        res = frmMain.TacticsList.LoadFromMySQL(1);//重新装载策略
                    }
                    else
                    {
                        log.Error("监测到与云Broker连接正常，使用情况0来装载策略");
                        res = frmMain.TacticsList.LoadFromMySQL(0);//重新装载策略
                    }

                    return res;
                }
                catch (Exception ex)
                {
                    log.Error("定时器刷新数据库失败: " + ex.Message);
                    return false;
                }
            }
            else {
                return true;
            }
        }


        #region 联网下清洗数据库中的策略
        public bool CleanTacticsFromMysql()
        {
            bool result = false;
            string currentDate = DateTime.Now.ToString("yyyy-MM-dd"); // 过期判断基准：当前日期零点前

            try
            {
                // 1. 查询过期数据中最近的rTime（最大的rTime）
                string maxExpiredTimeSql = @"
                    SELECT MAX(rTime)
                    FROM tactics
                    WHERE rTime < @CurrentDate";

                var parameters = new Dictionary<string, object> { { "@CurrentDate", currentDate } };
                var resultObj = DBConnection.QuerySingleValue(maxExpiredTimeSql, parameters: parameters);

                DateTime? maxExpiredTime = null;
                if (resultObj != null && resultObj != DBNull.Value)
                {
                    maxExpiredTime = Convert.ToDateTime(resultObj);
                }

                // 2. 如果存在过期数据，删除所有早于最近时间点的过期数据
                if (maxExpiredTime.HasValue)
                {
                    string deleteSql = @"
                        DELETE FROM tactics
                        WHERE rTime < @CurrentDate
                          AND rTime < @MaxExpiredTime";

                    var deleteParameters = new Dictionary<string, object>
                    {
                        { "@CurrentDate", currentDate },
                        { "@MaxExpiredTime", maxExpiredTime.Value }
                    };

                    int affectedRows = DBConnection.ExecSQLWithParams(deleteSql, deleteParameters);
                    result = affectedRows >= 0; // 即使没有删除行也视为成功（可能没有更早的数据）
                    //log.Error($"清理过期策略完成，删除了 {affectedRows} 条记录，保留了最近时间点 {maxExpiredTime.Value} 的策略");
                }
                else
                {
                    // 没有过期数据
                    result = true;
                    //log.Error("没有需要清理的过期策略");
                }
            }
            catch (Exception ex)
            {
                log.Error("清理过期策略失败：" + ex.Message);
                result = false;
            }

            return result;
        }
        #endregion

        #region 无网下清理数据库策略
        public bool CleanTacticsFromMysqlWhen4gFail()
        {
            bool result = false;
            string today = DateTime.Today.ToString("yyyy-MM-dd"); // 今天的日期（仅日期部分）

            try
            {
                // 步骤1：检查是否存在今天的策略（rTime日期为今天）
                string checkTodaySql = "SELECT COUNT(1) FROM tactics WHERE DATE(rTime) = @Today;";
                var checkParams = new Dictionary<string, object> { { "@Today", today } };
                var todayCountObj = DBConnection.QuerySingleValue(checkTodaySql, checkParams);
                int todayCount = Convert.ToInt32(todayCountObj);
                bool hasTodayTactics = todayCount > 0;

                if (hasTodayTactics)
                {
                    // 步骤2：存在今天的策略，直接删除过期策略（rTime早于今天）
                    string deleteExpiredSql = "DELETE FROM tactics WHERE rTime < @Today;";
                    var deleteParams = new Dictionary<string, object> { { "@Today", today } };
                    int rowsAffected = DBConnection.ExecSQLWithParams(deleteExpiredSql, deleteParams);
                    result = true; // 无论是否有删除，只要执行成功就返回true
                    log.Error($"存在今日策略，已删除过期策略 {rowsAffected} 条");
                }
                else
                {
                    // 步骤3：不存在今天的策略，处理过期策略
                    // 3.1 查找过期策略中最近的时间rTime1
                    string findLatestExpiredSql = "SELECT MAX(rTime) FROM tactics WHERE DATE(rTime) < @Today;";
                    var findParams = new Dictionary<string, object> { { "@Today", today } };
                    var rTime1Obj = DBConnection.QuerySingleValue(findLatestExpiredSql, findParams);
                    DateTime? latestExpiredTime = null;
                    if (rTime1Obj != null && rTime1Obj != DBNull.Value)
                    {
                        latestExpiredTime = Convert.ToDateTime(rTime1Obj);
                    }

                    if (latestExpiredTime.HasValue)
                    {
                        // 3.2 将所有rTime等于rTime1的策略时间改为今日
                        string updateSql = "UPDATE tactics SET rTime = @Today WHERE rTime = @RTime1;";
                        var updateParams = new Dictionary<string, object>
                        {
                            { "@Today", today },
                            { "@RTime1", latestExpiredTime.Value }
                        };
                        int updatedRows = DBConnection.ExecSQLWithParams(updateSql, updateParams);
                        log.Error($"已将 {updatedRows} 条最近过期策略（时间：{latestExpiredTime.Value:yyyy-MM-dd}）更新为今日");

                        // 3.3 删除修改后仍过期的策略（rTime < 今天）
                        string deleteAfterUpdateSql = "DELETE FROM tactics WHERE rTime < @Today;";
                        var deleteAfterUpdateParams = new Dictionary<string, object> { { "@Today", today } };
                        int deletedRows = DBConnection.ExecSQLWithParams(deleteAfterUpdateSql, deleteAfterUpdateParams);
                        log.Error($"更新后，删除过期策略 {deletedRows} 条");
                        result = true;
                    }
                    else
                    {
                        // 没有任何过期策略，无需操作
                        log.Error("不存在今日策略，且无任何过期策略，无需处理");
                        result = true; // 无操作也算成功
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("清理策略失败：" + ex.Message);
                result = false;
            }

            return result;
        }
        #endregion

        #region 数据库中装载策略数据
        public bool LoadFromMySQL(int type)
        {
            switch (type)
            {
                case 0:
                    CleanTacticsFromMysql();
                    break;
                case 1:
                    CleanTacticsFromMysqlWhen4gFail();
                    break;
                default:
                    break;
            }

            bool Result = false;
            string astrSQL = "SELECT startTime, endTime, tType, PCSType, waValue, rTime FROM tactics WHERE rTime = @Today ORDER BY startTime";

            try
            {
                // 使用参数化查询获取数据
                var parameters = new Dictionary<string, object> {
                    { "@Today", DateTime.Today }
                };
                var dataTable = DBConnection.QueryDataTableWithParams(astrSQL, parameters, commandTimeout: 30, connectionTimeout: 10);

                if (dataTable != null && dataTable.Rows.Count > 0)
                {
                    lock (TacticsList)
                    {
                        // 清空现有策略
                        TacticsList.Clear();

                        // 处理查询结果
                        foreach (DataRow row in dataTable.Rows)
                        {
                            var oneTactics = new TacticsClass();
                            oneTactics.startTime = Convert.ToDateTime("2022-01-01 " + row["startTime"].ToString());
                            oneTactics.endTime = Convert.ToDateTime("2022-01-01 " + row["endTime"].ToString());
                            oneTactics.tType = row["tType"].ToString();
                            oneTactics.PCSType = row["PCSType"].ToString();

                            if (oneTactics.PCSType == "恒流")
                                oneTactics.waValue = (int)(Convert.ToInt32(row["waValue"]) * 0.8);
                            if (oneTactics.PCSType == "恒压")
                            {
                                oneTactics.waValue = (int)((Convert.ToInt32(row["waValue"]) - 648) * 0.7);
                                if (oneTactics.waValue < 0)
                                    oneTactics.waValue = 0;
                            }

                            // 限额处理
                            oneTactics.waValue = Math.Abs(oneTactics.waValue);
                            if (oneTactics.waValue > 110)
                                oneTactics.waValue = 110;

                            // 修正充放电的正负功率
                            if (oneTactics.tType == "放电")
                                oneTactics.waValue = -Convert.ToInt32(row["waValue"]);
                            else
                                oneTactics.waValue = Convert.ToInt32(row["waValue"]);

                            // 策略日期
                            oneTactics.strategyDate = Convert.ToDateTime(row["rTime"]);

                            TacticsList.Add(oneTactics);
                        }
                    }
                }

                if (dataTable == null)
                {
                    log.Error("查询今日策略失败，保留当前内存策略不变");
                    return false;
                }

                if (dataTable.Rows.Count == 0)
                {
                    log.Error("不存在今日策略");
                    lock (TacticsList)
                    {
                        TacticsList.Clear();
                    }
                    return true;
                }

                Result = true;
            }
            catch (Exception ex)
            {
                log.Error($"LoadFromMySQL Error: {ex.Message}", ex);
            }

            return Result;
        }
        #endregion

        /// <summary>
        /// 检查昨天的数据是否存在
        /// </summary>
        private bool CheckYesterdayProfit()
        {
            //qiao
            return false;
        }

        /// <summary>
        /// 获取昨天的电表数据
        /// </summary>
        public void GetYesterdayData()
        {
            //qiao
            try
            {
                if (CheckYesterdayProfit())
                    return;
                // BeRequisitioned = true;
            }
            //设置电表为停止询问
            //头一次运行检车昨天的电表数据是否存在
            //profit
            //获取昨天的电表数据
            //计算成本
            //保存到数据库
            //如果没有就查询并保存昨天电表数据
            //2如果日期变化也读取昨日的数据
            //记录开始的电表等信息

            //设置电表为巡查模式
            catch { }
            finally
            {
                //BeRequisitioned = true;
            }
        }


        //true:不存在找到下个满足条件的策略与当前时间间隔为10分钟
        public bool FindNextMoveTime(string move)
        {
            bool res = true;
            DateTime now = DateTime.Now;
            TacticsClass oneTactics = null;
            for (int i = 0; i < TacticsList.Count; i++)
            {
                oneTactics = TacticsList[i];
                if (CheckMoveInShedule(oneTactics, now, move))
                {
                    res = false;
                    break;
                }
            }

            return res;
        }

        //true：找到下个满足条件的策略与当前时间间隔为10分钟
        private bool CheckMoveInShedule(TacticsClass aTactics, DateTime aTime, string move)
        {
            DateTime startTimeMinusTenMinutes = aTactics.startTime.AddMinutes(-10);

            if (aTactics.tType == move &&  aTime.CompareTo(startTimeMinusTenMinutes) <= 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        #region 策略监视线程
        public bool AutoCheckTactics()
        {
            try
            {
                Thread_CheckTactics = new Thread(CheckTactics);
                Thread_CheckTactics.IsBackground = true;
                Thread_CheckTactics.Name = "AutoCheckTactics";
                Thread_CheckTactics.Priority = ThreadPriority.Highest;
                Thread_CheckTactics.Start();
                return true;
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
                return false;
            }
        }



        private void CheckTactics()
        {
            log.Error("启动监听策略");
            while (true)
            {
                try
                {
                    Thread.Sleep(30000);
                    TacticsClass oneTactics = null;

                    if (!TacticsOn)//策略标识符没有开启，延长线程睡眠时间
                    {
                        // 只有在策略模式才会运行策略
                        if (frmSet.config.SysMode == 1)
                            TacticsOn = true;
                    }
                    //开启策略，若EMS无策略则重新读取数据库
                    if (TacticsList.Count == 0)
                    {
                        //LoadFromMySQL(0);
                        LoadMasterDailyTactics();
                    }

                    DateTime now = DateTime.Now;
                    //没有策略的执行策略就要停止输出
                    if (TacticsList.Count == 0)
                    {
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            frmMain.Selffrm.AllEquipment.waValueActive = 0;
                            //主从计划功率清零
                            frmMain.Selffrm.AllEquipment.PCSScheduleKVA = 0;
                            //主机停止中断PCS执行线程，中断向从机发送pcs工作指令
                            frmMain.Selffrm.AllEquipment.HostStart = false;
                            frmMain.Selffrm.AllEquipment.SlaveStart = false;
                        }
                    }
                    //判断时间所在的区间和工作内容
                    int i;
                    for (i = 0; i < TacticsList.Count; i++)
                    {
                        oneTactics = TacticsList[i];
                        if (CheckTimeInShedule(oneTactics, now))
                            break;//找到list中第一条符合条件的策略(遇到新的策略会立刻中断当前策略，执行新的策略)
                    }//for

                    //没找到就停止
                    if (i == TacticsList.Count)
                    {
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            frmMain.Selffrm.AllEquipment.eState = 1;
                            //主从计划功率清零
                            frmMain.Selffrm.AllEquipment.PCSScheduleKVA = 0;
                            //主机停止中断PCS执行线程，中断向从机发送pcs工作指令
                            frmMain.Selffrm.AllEquipment.HostStart = false;
                            frmMain.Selffrm.AllEquipment.SlaveStart= false;
                        }
                        continue;
                    }

                    //找到区段处理方法
                    //ActiveIndex 初始默认为-2 是因为防止更新TacticsList后 指针指向空的位置
                    //循环读取策略列表，只有运行第一条策略或者更新策略才会下发指令
                    if (ActiveIndex != i)
                    {
                        //更换策略点
                        if (ActiveIndex >= 0)
                        {
                            //从策略中取出PCS的执行参数，打开hostStart，在com1线程中唯一PCS执行
                            while (frmMain.Selffrm.AllEquipment.PCSTypeActive != oneTactics.PCSType || frmMain.Selffrm.AllEquipment.wTypeActive != oneTactics.tType || frmMain.Selffrm.AllEquipment.PCSScheduleKVA != oneTactics.waValue/frmSet.config.SysCount)
                            {
                                lock (frmMain.Selffrm.AllEquipment)
                                {
                                    //2.21
                                    frmMain.Selffrm.AllEquipment.PrewTypeActive = oneTactics.tType;
                                    frmMain.Selffrm.AllEquipment.PrePCSTypeActive = oneTactics.PCSType;

                                    if (frmMain.Selffrm.AllEquipment.PrePCSTypeActive == "恒功率")
                                    {
                                        frmMain.Selffrm.AllEquipment.GotoSchedule = true;
                                    }

                                    if (frmMain.Selffrm.AllEquipment.GotoSchedule)
                                    {
                                        frmMain.Selffrm.AllEquipment.dRate = 0;
                                        frmMain.Selffrm.AllEquipment.eState = 1;
                                        frmMain.Selffrm.AllEquipment.PCSTypeActive = oneTactics.PCSType;
                                        frmMain.Selffrm.AllEquipment.wTypeActive = oneTactics.tType;
                                        //下发的功率值恒为正数
                                        frmMain.Selffrm.AllEquipment.PCSScheduleKVA = oneTactics.waValue/frmSet.config.SysCount;
                                        frmMain.Selffrm.AllEquipment.AllPCSScheduleKVA = oneTactics.waValue;
                                        log.Error("更换策略点的PCS计划功率：" + frmMain.Selffrm.AllEquipment.PCSScheduleKVA+ " "+oneTactics.tType + " "+oneTactics.PCSType);
                                        frmMain.Selffrm.AllEquipment.HostStart = true;
                                        frmMain.Selffrm.AllEquipment.SlaveStart = true;

                                    }
                                }
                            }
                            ActiveIndex = i;
                        }
                        else
                        {
                            //运行策略
                            while (frmMain.Selffrm.AllEquipment.PCSTypeActive != oneTactics.PCSType || frmMain.Selffrm.AllEquipment.wTypeActive != oneTactics.tType || frmMain.Selffrm.AllEquipment.PCSScheduleKVA != oneTactics.waValue/frmSet.config.SysCount)
                            {
                                lock (frmMain.Selffrm.AllEquipment)
                                {
                                    //2.21
                                    frmMain.Selffrm.AllEquipment.PrewTypeActive = oneTactics.tType;
                                    frmMain.Selffrm.AllEquipment.PrePCSTypeActive = oneTactics.PCSType;
                                    if (frmMain.Selffrm.AllEquipment.PrePCSTypeActive == "恒功率")
                                    {
                                        frmMain.Selffrm.AllEquipment.GotoSchedule = true;
                                    }

                                    if (frmMain.Selffrm.AllEquipment.GotoSchedule)
                                    {
                                        frmMain.Selffrm.AllEquipment.eState = 1;
                                        //frmMain.Selffrm.AllEquipment.runState = 0;
                                        frmMain.Selffrm.AllEquipment.PCSTypeActive = TacticsList[i].PCSType;
                                        frmMain.Selffrm.AllEquipment.wTypeActive = TacticsList[i].tType;
                                        frmMain.Selffrm.AllEquipment.PCSScheduleKVA = oneTactics.waValue/frmSet.config.SysCount;
                                        frmMain.Selffrm.AllEquipment.AllPCSScheduleKVA = oneTactics.waValue;
                                        log.Error("运行策略点的PCS计划功率：" + frmMain.Selffrm.AllEquipment.PCSScheduleKVA+ " "+oneTactics.tType + " "+oneTactics.PCSType);

                                        frmMain.Selffrm.AllEquipment.HostStart = true;
                                        frmMain.Selffrm.AllEquipment.SlaveStart = true;
                                    }
                                }
                                ActiveIndex = i;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error("CheckTactics: "+ ex.Message);
                }
            }
        }
        #endregion

        //判断是否在时间段内
        private bool CheckTimeInShedule(TacticsClass aTactics, DateTime aTime)
        {
            //找到符合当前日期的策略
            if (aTactics.strategyDate.ToString("yyyy-MM-dd") == DateTime.Now.ToString("yyyy-MM-dd"))
            {
                string strStrtTime = aTactics.startTime.ToString("HH:mm:ss");
                string strEndTime = aTactics.endTime.ToString("HH:mm:ss");

                string strNow = aTime.ToString("HH:mm:ss");
                if (strStrtTime.CompareTo(strEndTime) < 0)
                {
                    if ((strNow.CompareTo(strStrtTime) >= 0) &&
                        (strNow.CompareTo(strEndTime) <= 0))
                    {
                        return true;
                    }
                    else
                        return false;
                }
                //今晚到明天的策略
                else if (strStrtTime.CompareTo(strEndTime) > 0)
                {
                    if ((strNow.CompareTo(strStrtTime) >= 0) ||
                            (strNow.CompareTo(strEndTime) < 0))
                    {
                        return true;
                    }
                    else
                        return false;
                }
                else //if (strStrtTime.CompareTo(strEndTime) == 0)
                    return false;
            }
            else
            {
                return false;
            }
        }




        //获取时间对应的充放电的具体数值
        private int GetTacticPower(string astrTime)
        {
            TacticsClass oneTactics;
            int iResult = 0;
            string strStrtTime;
            string strEndTime;
            for (int i = 0; i < TacticsList.Count; i++)
            {
                oneTactics = TacticsList[i];
                strStrtTime = oneTactics.startTime.ToString("HH:mm");
                strEndTime = oneTactics.endTime.ToString("HH:mm");
                if (strStrtTime.CompareTo(strEndTime) < 0)
                {
                    if ((astrTime.CompareTo(strStrtTime) >= 0) &&
                        (astrTime.CompareTo(strEndTime) <= 0))
                    {
                        //if (oneTactics.tType == "充电")
                        //    iResult = -1 * oneTactics.waValue;
                        //else
                        iResult = -oneTactics.waValue;
                        //StartIndex = i;
                        break;
                    }
                }
                else
                {
                    if ((astrTime.CompareTo(strStrtTime) >= 0) ||
                            (astrTime.CompareTo(strEndTime) <= 0))
                    {
                        //if (oneTactics.tType == "充电")
                        //    iResult = -1 * oneTactics.waValue;
                        //else
                        iResult = -oneTactics.waValue;
                        //StartIndex = i;
                        break;
                    }
                }
            }
            return iResult;
        }




        /// <summary>
        /// 将chart数组中位置换算成时间
        /// </summary>
        /// <param name="aCount"></param>
        /// <returns></returns>

        private string Count2Time(int aCount)
        {
            //Math.Round()：四舍六入五取偶      Math.Floor()：向下取整   Math.Ceiling()：向上取整
            return ((int)Math.Floor(aCount / 60.0)).ToString("D2") + ":" + (aCount % 60).ToString("D2");
        }

        /// <summary>
        /// 将时间换算成chart数组的位置
        /// </summary>
        /// <param name="aTime"></param>
        /// <returns></returns>
        private int Time2Count(DateTime aTime)
        {
            return aTime.Hour * 60 + aTime.Minute;
        }

        public void AddOneStep(Chart aOneChar, DateTime aDateTime, double aMainKw, double aGridKW, double aSubKW)
        {
            int iIndex = Time2Count(aDateTime);
            aOneChar.Series[1].Points[iIndex].SetValueY(aMainKw);
            aOneChar.Series[2].Points[iIndex].SetValueY(aGridKW);
            aOneChar.Series[3].Points[iIndex].SetValueY(aSubKW);

        }

        /// <summary>
        /// 显示计划2Chart
        /// </summary>
        /// <param name="aOneChart"></param>
        /// <param name="aCleanAllSeries"></param>
        public void ShowTactic2Char(Chart aOneChart, bool aCleanAllSeries)
        {
            //int iIndex = 0;
            string strData;
            int iData;
            //if (aOneChart.Series[0].Points.Count>0)
            aOneChart.Series[0].Points.Clear();
            if (aCleanAllSeries)
            {
                aOneChart.Series[1].Points.Clear();
                aOneChart.Series[2].Points.Clear();
                aOneChart.Series[3].Points.Clear();
            }
            for (int i = 0; i < 1440; i++)//1一天60*24=1440分钟
            {
                strData = Count2Time(i);
                iData = GetTacticPower(strData);//, ref iIndex);
                if (aCleanAllSeries)
                {
                    aOneChart.Series[1].Points.AddXY(strData, 0);
                    aOneChart.Series[2].Points.AddXY(strData, 0);
                    aOneChart.Series[3].Points.AddXY(strData, 0);
                }
                if (iData > 100)
                    aOneChart.Series[0].Points.AddXY(strData, iData);
                else
                    aOneChart.Series[0].Points.AddXY(strData, iData);

            }
            //aOneChart.ChartAreas[0].AxisX.ScaleView.Size = 1500;
            // aOneChart.ChartAreas[0].AxisX.Minimum = DateTime.Parse("00:00:00").ToOADate();
            // aOneChart.ChartAreas[0].AxisX.Maximum = DateTime.Parse("23:59:59").ToOADate();
            //aOneChart.ChartAreas[0].AxisX.IntervalType = DateTimeIntervalType.Minutes;//如果是时间类型的数据，间隔方式可以是秒、分、时
            //chart1.ChartAreas[0].AxisX.Interval = DateTime.Parse("00:05:00").Millisecond;//间隔为5分钟
            // aOneChart.ChartAreas[0].AxisX.Interval = DateTime.Parse("00:01:00").Second;//TODO 测试--间隔为5秒
            // aOneChart.ChartAreas[0].AxisX.LabelStyle.Format = "HH:mm";         //毫秒格式： hh:mm:ss.fff ，后面几个f则保留几位毫秒小数，此时要注意轴的最大值和最小值不要差太大
            aOneChart.ChartAreas[0].AxisX.LabelStyle.IntervalType = DateTimeIntervalType.Days;
            aOneChart.ChartAreas[0].AxisX.MajorGrid.IntervalType = DateTimeIntervalType.Days;
            aOneChart.ChartAreas[0].AxisX.Minimum = -30;
            aOneChart.ChartAreas[0].AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
            aOneChart.ChartAreas[0].AxisX.Interval = 120;
        }


    }
}
