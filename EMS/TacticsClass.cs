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
        #endregion
        /*  public bool cleanJFPGFromMysql()
          {
              bool Result = true;
              string strDate = DateTime.Now.ToString("yyyy-MM-dd");
              // 使用参数化查询防止SQL注入
              string astrSQL = "DELETE from electrovalence where rTime < @strDate";

              try
              {
                  using (MySqlConnection connection = new MySqlConnection(DBConnection.connectionStr))
                  {
                      connection.Open();
                      using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                      {
                          // 添加参数
                          sqlCmd.Parameters.AddWithValue("@strDate", strDate);

                          // 执行删除并获取受影响的行数
                          int affectedRows = sqlCmd.ExecuteNonQuery();

                          if (affectedRows > 0)
                          {
                              log.Error($"清除了 {affectedRows} 条过期电价策略");
                          }
                          else
                          {
                              log.Error("没有需要清除的过期电价策略");
                          }
                      }
                  }
              }
              catch (Exception ex)
              {
                  log.Error("清除过期电价策略时发生错误：" + ex.Message);
                  Result = false;
              }

              return Result;
          }*/


        #region 从数据库加载费率设置
        public bool LoadJFPGFromSQL()
        {
            bool res = true;
            cleanJFPGFromMysql();

            // 使用参数化查询避免SQL注入
            string astrSQL = "select startTime, eName from electrovalence where rTime = @rTime";

            try
            {
                // 使用DBConnect统一的参数化查询接口
                var parameters = new Dictionary<string, object> { { "@rTime", DateTime.Today } };
                var dataTable = DBConnection.QueryDataTableWithParams(astrSQL, parameters);

                if (dataTable != null && dataTable.Rows.Count > 0)
                {
                    log.Error("存在今日电价策略");
                    byte[] tempJFPG_4 = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                        0, 0 };//14*3=42    14个时段 ： 号 分 时

                    byte[] tempJFPG_8 = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                        0, 0 };//14*3=42    14个时段 ： 号 时 分
                    int i = 0;
                    DateTime dtTemp;

                    foreach (DataRow row in dataTable.Rows)
                    {
                        if (i >= 14) break;

                        dtTemp = Convert.ToDateTime("2022-01-01 " + row["startTime"].ToString());   //获取起始时间 startTime

                        // 设置八费率
                        tempJFPG_8[i * 3 + 0] = Convert.ToByte(row["eName"]);  //获取 费率号（0：无 1：尖 2：峰 3：平 4：谷） eName
                        tempJFPG_8[i * 3 + 1] = (byte)dtTemp.Hour;
                        tempJFPG_8[i * 3 + 2] = (byte)dtTemp.Minute;

                        // 设置四费率
                        tempJFPG_4[i * 3 + 0] = Convert.ToByte(row["eName"]); //获取 费率号（0：无 1：尖 2：峰 3：平 4：谷） eName
                        tempJFPG_4[i * 3 + 1] = (byte)dtTemp.Minute;
                        tempJFPG_4[i * 3 + 2] = (byte)dtTemp.Hour;

                        i++;
                    }

                    byte[] atable1 = { 1, 1, 1, 1, 3, 1, 1, 6, 1, 1, 9, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };//储能表使用八费率的第一套表
                    byte[] atable2 = { 1, 1, 1, 1, 1, 3, 1, 1, 6, 1, 1, 9 };//辅助表使用四费率的第一套表
                    byte[] atable3 = { 3, 1, 1, 3, 1, 3, 3, 1, 6, 3, 1, 9 };//储能表使用四费率的第三套表
                    //只有储能能够设置8段费率
                    if (frmMain.Selffrm.AllEquipment.Elemeter2 != null)
                    {
                        if (frmMain.Selffrm.AllEquipment.Elemeter2.Version == 8)
                        {
                            if (!frmMain.Selffrm.AllEquipment.Elemeter2.SetJFTG_8(atable1, tempJFPG_8))
                            {
                                res = false;
                            }
                        }
                        else
                        {
                            // 检查并处理 tempJFPG 数组中每个时段的费率号
                            for (int j = 0; j < 14; j++)
                            {
                                if (tempJFPG_4[j * 3 + 0] > 4)
                                {
                                    tempJFPG_4[j * 3 + 0] = 0;
                                    tempJFPG_4[j * 3 + 1] = 0;
                                    tempJFPG_4[j * 3 + 2] = 0;
                                }
                            }
                            if (!frmMain.Selffrm.AllEquipment.Elemeter2.SetJFTG_4(atable3, tempJFPG_4))
                            {
                                res = false;
                            }
                        }
                    }

                    if (frmMain.Selffrm.AllEquipment.Elemeter3 != null)
                    {
                        // 检查并处理 tempJFPG 数组中每个时段的费率号
                        for (int j = 0; j < 14; j++)
                        {
                            if (tempJFPG_4[j * 3 + 0] > 4)
                            {
                                tempJFPG_4[j * 3 + 0] = 0;
                                tempJFPG_4[j * 3 + 1] = 0;
                                tempJFPG_4[j * 3 + 2] = 0;
                            }
                        }
                        frmMain.Selffrm.AllEquipment.Elemeter3.SetJFTG(atable2, tempJFPG_4);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("LoadJFPGFromSQL: " + ex.Message);
                return false;
            }
            return res;
        }

        /// <summary>
        /// 只从数据库获取“今日电价策略”，并与设备当前费率对比；
        /// 若不同才继续下发（八费率对比 zone8Rates/rates8Tier；四费率对比 zone4Rates/rates4Tier）。
        /// </summary>
        public bool LoadTodayJFPGFromSQL_CompareAndSendIfDiff()
        {
            bool res = true;
            cleanJFPGFromMysql();

            // 只取今日，并保证按起始时间排序，避免顺序不一致导致重复下发
            string astrSQL = "select startTime, eName from electrovalence where rTime = @rTime order by startTime asc";

            try
            {
                var parameters = new Dictionary<string, object> { { "@rTime", DateTime.Today } };
                var dataTable = DBConnection.QueryDataTableWithParams(astrSQL, parameters);

                if (dataTable == null || dataTable.Rows.Count <= 0)
                    return true;

                //log.Error("存在今日电价策略");

                byte[] tempJFPG_4 = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0 };//14*3=42    14个时段 ： 号 分 时

                byte[] tempJFPG_8 = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                    0, 0 };//14*3=42    14个时段 ： 号 时 分

                int i = 0;
                DateTime dtTemp;
                foreach (DataRow row in dataTable.Rows)
                {
                    if (i >= 14) break;
                    dtTemp = Convert.ToDateTime("2022-01-01 " + row["startTime"].ToString());

                    byte rateNo = Convert.ToByte(row["eName"]); // 0~4

                    // 八费率：号 时 分
                    tempJFPG_8[i * 3 + 0] = rateNo;
                    tempJFPG_8[i * 3 + 1] = (byte)dtTemp.Hour;
                    tempJFPG_8[i * 3 + 2] = (byte)dtTemp.Minute;

                    // 四费率：号 分 时（保持原逻辑）
                    tempJFPG_4[i * 3 + 0] = rateNo;
                    tempJFPG_4[i * 3 + 1] = (byte)dtTemp.Minute;
                    tempJFPG_4[i * 3 + 2] = (byte)dtTemp.Hour;

                    i++;
                }

                // 表参数（保持与旧逻辑一致）
                byte[] atable1 = { 1, 1, 1, 1, 3, 1, 1, 6, 1, 1, 9, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };//储能表使用八费率的第一套表
                byte[] atable2 = { 1, 1, 1, 1, 1, 3, 1, 1, 6, 1, 1, 9 };//辅助表使用四费率的第一套表
                byte[] atable3 = { 3, 1, 1, 3, 1, 3, 3, 1, 6, 3, 1, 9 };//储能表使用四费率的第三套表

                // 校验：四费率得费率号只允许 0~4，非法则清零（避免下发非法数据）
                for (int j = 0; j < 14; j++)
                {
                    if (tempJFPG_4[j * 3 + 0] > 4)
                    {
                        tempJFPG_4[j * 3 + 0] = 0;
                        tempJFPG_4[j * 3 + 1] = 0;
                        tempJFPG_4[j * 3 + 2] = 0;
                    }
                }

                // 设备2：支持8/4费率，必须对比后决定是否下发
                if (frmMain.Selffrm.AllEquipment.Elemeter2 != null)
                {
                    var e2 = frmMain.Selffrm.AllEquipment.Elemeter2;
                    if (e2.Version == 8)
                    {
                        string expectedZone8 = BytesToHexNoSpaces(atable1);
                        string expectedRates8 = BytesToHexNoSpaces(tempJFPG_8);

                        bool needSendElemeter2_8 =
                            !HexStringEquals(e2.zone8Rates, expectedZone8) ||
                            !HexStringEquals(e2.rates8Tier, expectedRates8);

/*                        log.Info("expectedZone8: " + expectedZone8 + "expectedRates8: " + expectedRates8 +
                            "zone8Rates: " + e2.zone8Rates + "rates8Tier: " + e2.rates8Tier);*/

                        if (!needSendElemeter2_8)
                        {
                            log.Error("八费率：设备费率与今日策略一致，跳过下发");
                        }
                        else if (!e2.SetJFTG_8(atable1, tempJFPG_8))
                        {
                            res = false;
                        }
                    }
                    else
                    {
                        string expectedZone4 = BytesToHexNoSpaces(atable3);
                        string expectedRates4 = BytesToHexNoSpaces(tempJFPG_4);

                        bool needSendElemeter2_4 =
                            !HexStringEquals(e2.zone4Rates, expectedZone4) ||
                            !HexStringEquals(e2.rates4Tier, expectedRates4);

/*                        log.Info("expectedZone4: " + expectedZone4 + "expectedRates4: " + expectedRates4 +
                             "zone4Rates: " + e2.zone4Rates + "rates4Tier: " + e2.rates4Tier);*/

                        if (!needSendElemeter2_4)
                        {
                            log.Error("四费率：设备费率与今日策略一致，跳过下发");
                        }
                        else if (!e2.SetJFTG_4(atable3, tempJFPG_4))
                        {
                            res = false;
                        }
                    }
                }

                // 设备3：只支持四费率
                if (frmMain.Selffrm.AllEquipment.Elemeter3 != null)
                {
                    var e3 = frmMain.Selffrm.AllEquipment.Elemeter3;
                    string expectedZone4 = BytesToHexNoSpaces(atable2);
                    string expectedRates4 = BytesToHexNoSpaces(tempJFPG_4);

                    bool needSendElemeter3_4 =
                        !HexStringEquals(e3.zone4Rates, expectedZone4) ||
                        !HexStringEquals(e3.rates4Tier, expectedRates4);

/*                    log.Info("expectedZone4: " + expectedZone4 + "expectedRates4: " + expectedRates4 +
                         "zone4Rates: " + e3.zone4Rates + "rates4Tier: " + e3.rates4Tier);*/


                    if (!needSendElemeter3_4)
                    {
                        log.Error("四费率：辅助费率与今日策略一致，跳过下发");
                    }
                    else if (!e3.SetJFTG(atable2, tempJFPG_4))
                    {
                        res = false;
                    }

                }
            }
            catch (Exception ex)
            {
                log.Error("LoadTodayJFPGFromSQL_CompareAndSendIfDiff: " + ex.Message);
                return false;
            }

            return res;
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
                            log.Warn($"Elemeter2 时间偏差 {diffMinutes:F1} 分钟，执行校时。设备时间: {e2Time.Value:yyyy-MM-dd HH:mm:ss}，系统时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                            e2.timing(73);
                        }

                        //log.Error("e2Time: " + e2Time + "diffMinutes: " + diffMinutes);
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
                            log.Warn($"Elemeter3 时间偏差 {diffMinutes:F1} 分钟，执行校时。设备时间: {e3Time.Value:yyyy-MM-dd HH:mm:ss}，系统时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                            e3.timing(47);
                        }

                        //log.Error("e3Time: " + e3Time + "diffMinutes: " + diffMinutes);
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
                    Thread.Sleep(120000);
                    LoadTodayJFPGFromSQL_CompareAndSendIfDiff();

                    LoadTimeValue_CompareAndSendIfDiff();
                }
                catch (Exception ex)
                {
                    log.Error("CheckJFPG: "+ ex.Message);
                }
            }
        }
        #endregion

        /*      public bool LoadJFPGFromSQL()
              {
                  bool res = true;
                  cleanJFPGFromMysql();
                  string strDate = DateTime.Now.ToString("yyyy-MM-dd");
                  string astrSQL = "select startTime, eName  from electrovalence where rTime = '" + strDate + "'";

                  try
                  {
                      using (MySqlConnection connection = new MySqlConnection(DBConnection.connectionStr))
                      {
                          connection.Open();
                          using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                          {
                              using (MySqlDataReader rd = sqlCmd.ExecuteReader())
                              {
                                  if (rd != null && rd.HasRows)
                                  {
                                      log.Error("存在今日电价策略");
                                      byte[] tempJFPG_4 = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                          0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                          0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                          0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                          0, 0 };//14*3=42    14个时段 ： 号 分 时

                                      byte[] tempJFPG_8 = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                          0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                          0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                          0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                          0, 0 };//14*3=42    14个时段 ： 号 时 分
                                      int i = 0;
                                      DateTime dtTemp;

      *//*                                //清空费率表
                                      if (frmMain.Selffrm.AllEquipment.Elemeter2 != null)
                                      {
                                          if (frmMain.Selffrm.AllEquipment.Elemeter2.Version == 8)
                                          {
                                              frmMain.Selffrm.AllEquipment.Elemeter2.clearJFPG_8();
                                          }
                                          else
                                          {
                                              frmMain.Selffrm.AllEquipment.Elemeter2.clearJFPG_4();
                                          }
                                      }*//*


                                      while (rd.Read() && i < 14)
                                      {
                                          dtTemp = Convert.ToDateTime("2022-01-01 " + rd.GetString(0));   //获取起始时间 startTime

                                          // 设置八费率
                                          tempJFPG_8[i * 3 + 0] = (byte)rd.GetInt32(1);  //获取 费率号（0：无 1：尖 2：峰 3：平 4：谷） eName
                                          tempJFPG_8[i * 3 + 1] = (byte)dtTemp.Hour;
                                          tempJFPG_8[i * 3 + 2] = (byte)dtTemp.Minute;

                                          // 设置四费率
                                          tempJFPG_4[i * 3 + 0] = (byte)rd.GetInt32(1); //获取 费率号（0：无 1：尖 2：峰 3：平 4：谷） eName
                                          tempJFPG_4[i * 3 + 1] = (byte)dtTemp.Minute;
                                          tempJFPG_4[i * 3 + 2] = (byte)dtTemp.Hour;

                                          i++;
                                      }

                                      byte[] atable1 = { 1, 1, 1, 1, 3, 1, 1, 6, 1, 1, 9, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };//储能表使用八费率的第一套表
                                      byte[] atable2 = { 1, 1, 1, 1, 1, 3, 1, 1, 6, 1, 1, 9 };//辅助表使用四费率的第一套表
                                      byte[] atable3 = { 3, 1, 1, 3, 1, 3, 3, 1, 6, 3, 1, 9 };//储能表使用四费率的第三套表
                                      //只有储能能够设置8段费率
                                      if (frmMain.Selffrm.AllEquipment.Elemeter2 != null)
                                      {
                                          if (frmMain.Selffrm.AllEquipment.Elemeter2.Version == 8)
                                          {
                                              if (!frmMain.Selffrm.AllEquipment.Elemeter2.SetJFTG_8(atable1, tempJFPG_8)) {
                                                  res = false;
                                              }
                                          }
                                          else
                                          {
                                              // 检查并处理 tempJFPG 数组中每个时段的费率号
                                              for (int j = 0; j < 14; j++)
                                              {
                                                  if (tempJFPG_4[j * 3 + 0] > 4)
                                                  {
                                                      tempJFPG_4[j * 3 + 0] = 0;
                                                      tempJFPG_4[j * 3 + 1] = 0;
                                                      tempJFPG_4[j * 3 + 2] = 0;
                                                  }
                                              }
                                              if (!frmMain.Selffrm.AllEquipment.Elemeter2.SetJFTG_4(atable3, tempJFPG_4)) {
                                                  res = false;
                                              }
                                          }
                                      }

                                      if (frmMain.Selffrm.AllEquipment.Elemeter3 != null)
                                      {
                                          // 检查并处理 tempJFPG 数组中每个时段的费率号
                                          for (int j = 0; j < 14; j++)
                                          {
                                              if (tempJFPG_4[j * 3 + 0] > 4)
                                              {
                                                  tempJFPG_4[j * 3 + 0] = 0;
                                                  tempJFPG_4[j * 3 + 1] = 0;
                                                  tempJFPG_4[j * 3 + 2] = 0;
                                              }
                                          }
                                          frmMain.Selffrm.AllEquipment.Elemeter3.SetJFTG(atable2, tempJFPG_4);
      *//*                                    if (!frmMain.Selffrm.AllEquipment.Elemeter3.SetJFTG(atable2, tempJFPG_4)) {
                                              res = false;
                                          }*//*
                                      }
                                  }
                              }
                          }
                      }
                      return res;
                  }
                  catch (Exception ex)
                  {
                      log.Error("LoadJFPGFromSQL: " + ex.Message);
                      return false;
                  }
              }*/


        public bool LoadMasterDailyTactics()
        {
            bool res = false;
            if (frmMain.TacticsList != null && frmSet.config.IsMaster == 1)
            {
                try
                {
                    if (frmMain.Selffrm.AllEquipment.SignalAlarmActive)
                    {
                        log.Error("监测到4G通信异常，使用情况1来装载策略");
                        res = frmMain.TacticsList.LoadFromMySQL(1);//重新装载策略
                    }
                    else
                    {
                        log.Error("监测到4G通信正常，使用情况0来装载策略");
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
                    log.Error($"清理过期策略完成，删除了 {affectedRows} 条记录，保留了最近时间点 {maxExpiredTime.Value} 的策略");
                }
                else
                {
                    // 没有过期数据
                    result = true;
                    log.Error("没有需要清理的过期策略");
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

        /*  public bool CleanTacticsFromMysql()
          {
              bool result = false;
              string currentDate = DateTime.Now.ToString("yyyy-MM-dd"); // 过期判断基准：当前日期零点前

              try
              {
                  using (MySqlConnection connection = new MySqlConnection(DBConnection.connectionStr))
                  {
                      connection.Open();

                      // 1. 查询过期数据中最近的rTime（最大的rTime）
                      string maxExpiredTimeSql = @"
                          SELECT MAX(rTime)
                          FROM tactics
                          WHERE rTime < @CurrentDate";

                      DateTime? maxExpiredTime = null;
                      using (MySqlCommand maxCmd = new MySqlCommand(maxExpiredTimeSql, connection))
                      {
                          maxCmd.Parameters.AddWithValue("@CurrentDate", currentDate);
                          var resultObj = maxCmd.ExecuteScalar();

                          if (resultObj != DBNull.Value)
                          {
                              maxExpiredTime = Convert.ToDateTime(resultObj);
                          }
                      }

                      // 2. 如果存在过期数据，删除所有早于最近时间点的过期数据
                      if (maxExpiredTime.HasValue)
                      {
                          string deleteSql = @"
                              DELETE FROM tactics
                              WHERE rTime < @CurrentDate
                                AND rTime < @MaxExpiredTime";

                          using (MySqlCommand deleteCmd = new MySqlCommand(deleteSql, connection))
                          {
                              deleteCmd.Parameters.AddWithValue("@CurrentDate", currentDate);
                              deleteCmd.Parameters.AddWithValue("@MaxExpiredTime", maxExpiredTime.Value);

                              int affectedRows = deleteCmd.ExecuteNonQuery();
                              result = affectedRows >= 0; // 即使没有删除行也视为成功（可能没有更早的数据）
                              log.Error($"清理过期策略完成，删除了 {affectedRows} 条记录，保留了最近时间点 {maxExpiredTime.Value} 的策略");
                          }
                      }
                      else
                      {
                          // 没有过期数据
                          result = true;
                          log.Error("没有需要清理的过期策略");
                      }
                  }
              }
              catch (Exception ex)
              {
                  log.Error("清理过期策略失败：" + ex.Message);
                  result = false;
              }

              return result;
          }*/

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
        /*        public bool CleanTacticsFromMysqlWhen4gFail()
                {
                    bool result = false;
                    string today = DateTime.Today.ToString("yyyy-MM-dd"); // 今天的日期（仅日期部分）
                    MySqlConnection connection = null;

                    try
                    {
                        using (connection = new MySqlConnection(DBConnection.connectionStr))
                        {
                            connection.Open();

                            // 步骤1：检查是否存在今天的策略（rTime日期为今天）
                            bool hasTodayTactics = false;
                            string checkTodaySql = "SELECT COUNT(1) FROM tactics WHERE DATE(rTime) = @Today;";
                            using (MySqlCommand checkCmd = new MySqlCommand(checkTodaySql, connection))
                            {
                                checkCmd.Parameters.AddWithValue("@Today", today);
                                int todayCount = Convert.ToInt32(checkCmd.ExecuteScalar());
                                hasTodayTactics = todayCount > 0;
                            }

                            if (hasTodayTactics)
                            {
                                // 步骤2：存在今天的策略，直接删除过期策略（rTime早于今天）
                                string deleteExpiredSql = "DELETE FROM tactics WHERE rTime < @Today;";
                                using (MySqlCommand deleteCmd = new MySqlCommand(deleteExpiredSql, connection))
                                {
                                    deleteCmd.Parameters.AddWithValue("@Today", today);
                                    int rowsAffected = deleteCmd.ExecuteNonQuery();
                                    result = true; // 无论是否有删除，只要执行成功就返回true
                                    log.Error($"存在今日策略，已删除过期策略 {rowsAffected} 条");
                                }
                            }
                            else
                            {
                                // 步骤3：不存在今天的策略，处理过期策略
                                // 3.1 查找过期策略中最近的时间rTime1
                                string findLatestExpiredSql = "SELECT MAX(rTime) FROM tactics WHERE DATE(rTime) < @Today;";
                                DateTime? latestExpiredTime = null;
                                using (MySqlCommand findCmd = new MySqlCommand(findLatestExpiredSql, connection))
                                {
                                    findCmd.Parameters.AddWithValue("@Today", today);
                                    object rTime1Obj = findCmd.ExecuteScalar();
                                    if (rTime1Obj != DBNull.Value)
                                    {
                                        latestExpiredTime = Convert.ToDateTime(rTime1Obj);
                                    }
                                }

                                if (latestExpiredTime.HasValue)
                                {
                                    // 3.2 将所有rTime等于rTime1的策略时间改为今日
                                    string updateSql = "UPDATE tactics SET rTime = @Today WHERE rTime = @RTime1;";
                                    using (MySqlCommand updateCmd = new MySqlCommand(updateSql, connection))
                                    {
                                        updateCmd.Parameters.AddWithValue("@Today", today);
                                        updateCmd.Parameters.AddWithValue("@RTime1", latestExpiredTime.Value);
                                        int updatedRows = updateCmd.ExecuteNonQuery();
                                        log.Error($"已将 {updatedRows} 条最近过期策略（时间：{latestExpiredTime.Value:yyyy-MM-dd}）更新为今日");
                                    }

                                    // 3.3 删除修改后仍过期的策略（rTime < 今天）
                                    string deleteAfterUpdateSql = "DELETE FROM tactics WHERE rTime < @Today;";
                                    using (MySqlCommand deleteCmd = new MySqlCommand(deleteAfterUpdateSql, connection))
                                    {
                                        deleteCmd.Parameters.AddWithValue("@Today", today);
                                        int deletedRows = deleteCmd.ExecuteNonQuery();
                                        log.Error($"更新后，删除过期策略 {deletedRows} 条");
                                        result = true;
                                    }
                                }
                                else
                                {
                                    // 没有任何过期策略，无需操作
                                    log.Error("不存在今日策略，且无任何过期策略，无需处理");
                                    result = true; // 无操作也算成功
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error("清理策略失败：" + ex.Message);
                        result = false;
                    }
                    finally
                    {
                        if (connection != null && connection.State == System.Data.ConnectionState.Open)
                        {
                            connection.Close();
                        }
                    }
                    return result;
                }
        */

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

                // 如果没有今日策略且type为0，查找最近的过期策略并更新为今日
                if ((dataTable == null || dataTable.Rows.Count == 0) && type == 0)
                {
                    log.Error("不存在今日策略，查找最近的过期策略");

                    // 查找过期策略中最近的时间rTime1
                    string findLatestExpiredSql = "SELECT MAX(rTime) FROM tactics WHERE DATE(rTime) < @Today";
                    var findParams = new Dictionary<string, object> { { "@Today", DateTime.Today } };
                    var latestExpiredTimeObj = DBConnection.QuerySingleValue(
                        findLatestExpiredSql,
                        commandTimeout: 15,
                        connectionTimeout: 5);

                    DateTime? latestExpiredTime = null;
                    if (latestExpiredTimeObj != null && latestExpiredTimeObj != DBNull.Value)
                    {
                        latestExpiredTime = Convert.ToDateTime(latestExpiredTimeObj);
                    }

                    if (latestExpiredTime.HasValue)
                    {
                        // 将所有rTime等于rTime1的策略时间改为今日
                        string updateSql = "UPDATE tactics SET rTime = @Today WHERE rTime = @RTime1";
                        var updateParams = new Dictionary<string, object>
                        {
                            {"@Today", DateTime.Today},
                            {"@RTime1", latestExpiredTime.Value}
                        };

                        int updatedRows = DBConnection.ExecSQLWithParams(updateSql, updateParams, 30, 10);
                        if (updatedRows > 0)
                        {
                            log.Error($"已将 {updatedRows} 条最近过期策略（时间：{latestExpiredTime.Value:yyyy-MM-dd}）更新为今日");
                        }

                        // 删除修改后仍过期的策略（rTime < 今天）
                        string deleteAfterUpdateSql = "DELETE FROM tactics WHERE rTime < @Today";
                        var deleteParams = new Dictionary<string, object> { { "@Today", DateTime.Today } };

                        int deletedRows = DBConnection.ExecSQLWithParams(deleteAfterUpdateSql, deleteParams, 30, 10);
                        if (deletedRows > 0)
                        {
                            log.Error($"更新后，删除过期策略 {deletedRows} 条");
                        }

                        // 更新策略后，重新查询今日策略
                        var newDataTable = DBConnection.QueryDataTableWithParams(astrSQL, parameters, commandTimeout: 30, connectionTimeout: 10);

                        if (newDataTable != null && newDataTable.Rows.Count > 0)
                        {
                            lock (TacticsList)
                            {
                                // 清空现有策略
                                TacticsList.Clear();

                                // 处理新的查询结果
                                foreach (DataRow row in newDataTable.Rows)
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
                    }
                    else
                    {
                        log.Error("不存在今日策略，且无任何过期策略");
                    }
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

        /*       public bool LoadFromMySQL(int type)
               {
                   //cleanTacticsFromMysql();
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
                   string strDate = DateTime.Now.ToString("yyyy-MM-dd");
                   string astrSQL = "select startTime,endTime, tType, PCSType, waValue, rTime"
                           + " from tactics where rTime = '" + strDate + "' order by startTime";

                   try
                   {
                       using (MySqlConnection connection = new MySqlConnection(DBConnection.connectionStr))
                       {
                           connection.Open();

                           // 首先检查是否有今日策略
                           bool hasTodayTactics = false;
                           using (MySqlCommand checkCmd = new MySqlCommand(astrSQL, connection))
                           {
                               using (MySqlDataReader rd = checkCmd.ExecuteReader())
                               {
                                   if (rd != null && rd.HasRows)
                                   {
                                       hasTodayTactics = true;
                                       lock (TacticsList)
                                       {
                                           while (TacticsList.Count > 0)
                                           {
                                               TacticsList.RemoveAt(0);
                                           }
                                           while (rd.Read())
                                           {
                                               TacticsClass oneTactics = new TacticsClass();
                                               oneTactics.startTime = Convert.ToDateTime("2022-01-01 " + rd.GetString(0));
                                               oneTactics.endTime = Convert.ToDateTime("2022-01-01 " + rd.GetString(1));
                                               oneTactics.tType = rd.GetString(2);
                                               oneTactics.PCSType = rd.GetString(3);
                                               if (oneTactics.PCSType == "恒流")
                                                   oneTactics.waValue = (int)(oneTactics.waValue * 0.8);
                                               if (oneTactics.PCSType == "恒压")
                                               {
                                                   oneTactics.waValue = (int)((oneTactics.waValue - 648) * 0.7);
                                                   if (oneTactics.waValue < 0)
                                                       oneTactics.waValue = 0;
                                               }

                                               //9.5 源码注释
                                               //oneTactics.PCSType = "恒功率";

                                               //限额
                                               oneTactics.waValue = Math.Abs(oneTactics.waValue);
                                               if (oneTactics.waValue > 110)
                                                   oneTactics.waValue = 110;
                                               //修正充放电的正负功率
                                               if (oneTactics.tType == "放电")
                                                   oneTactics.waValue = -rd.GetInt32(4);
                                               else
                                                   oneTactics.waValue = rd.GetInt32(4);

                                               //策略日期
                                               oneTactics.strategyDate = rd.GetDateTime(5);

                                               TacticsList.Add(oneTactics);
                                           }
                                       }
                                   }
                               }
                           }

                           // 如果没有今日策略且type为0，查找最近的过期策略并更新为今日
                           if (!hasTodayTactics && type == 0)
                           {
                               log.Error("不存在今日策略，查找最近的过期策略");

                               // 查找过期策略中最近的时间rTime1
                               string findLatestExpiredSql = "SELECT MAX(rTime) FROM tactics WHERE DATE(rTime) < @Today;";
                               DateTime? latestExpiredTime = null;
                               using (MySqlCommand findCmd = new MySqlCommand(findLatestExpiredSql, connection))
                               {
                                   findCmd.Parameters.AddWithValue("@Today", strDate);
                                   object rTime1Obj = findCmd.ExecuteScalar();
                                   if (rTime1Obj != DBNull.Value)
                                   {
                                       latestExpiredTime = Convert.ToDateTime(rTime1Obj);
                                   }
                               }

                               if (latestExpiredTime.HasValue)
                               {
                                   // 将所有rTime等于rTime1的策略时间改为今日
                                   string updateSql = "UPDATE tactics SET rTime = @Today WHERE rTime = @RTime1;";
                                   using (MySqlCommand updateCmd = new MySqlCommand(updateSql, connection))
                                   {
                                       updateCmd.Parameters.AddWithValue("@Today", strDate);
                                       updateCmd.Parameters.AddWithValue("@RTime1", latestExpiredTime.Value);
                                       int updatedRows = updateCmd.ExecuteNonQuery();
                                       log.Error($"已将 {updatedRows} 条最近过期策略（时间：{latestExpiredTime.Value:yyyy-MM-dd}）更新为今日");
                                   }

                                   // 删除修改后仍过期的策略（rTime < 今天）
                                   string deleteAfterUpdateSql = "DELETE FROM tactics WHERE rTime < @Today;";
                                   using (MySqlCommand deleteCmd = new MySqlCommand(deleteAfterUpdateSql, connection))
                                   {
                                       deleteCmd.Parameters.AddWithValue("@Today", strDate);
                                       int deletedRows = deleteCmd.ExecuteNonQuery();
                                       log.Error($"更新后，删除过期策略 {deletedRows} 条");
                                   }

                                   // 更新策略后，重新查询今日策略
                                   using (MySqlCommand newCmd = new MySqlCommand(astrSQL, connection))
                                   {
                                       using (MySqlDataReader newRd = newCmd.ExecuteReader())
                                       {
                                           if (newRd != null && newRd.HasRows)
                                           {
                                               lock (TacticsList)
                                               {
                                                   while (TacticsList.Count > 0)
                                                   {
                                                       TacticsList.RemoveAt(0);
                                                   }
                                                   while (newRd.Read())
                                                   {
                                                       TacticsClass oneTactics = new TacticsClass();
                                                       oneTactics.startTime = Convert.ToDateTime("2022-01-01 " + newRd.GetString(0));
                                                       oneTactics.endTime = Convert.ToDateTime("2022-01-01 " + newRd.GetString(1));
                                                       oneTactics.tType = newRd.GetString(2);
                                                       oneTactics.PCSType = newRd.GetString(3);
                                                       if (oneTactics.PCSType == "恒流")
                                                           oneTactics.waValue = (int)(oneTactics.waValue * 0.8);
                                                       if (oneTactics.PCSType == "恒压")
                                                       {
                                                           oneTactics.waValue = (int)((oneTactics.waValue - 648) * 0.7);
                                                           if (oneTactics.waValue < 0)
                                                               oneTactics.waValue = 0;
                                                       }

                                                       //限额
                                                       oneTactics.waValue = Math.Abs(oneTactics.waValue);
                                                       if (oneTactics.waValue > 110)
                                                           oneTactics.waValue = 110;
                                                       //修正充放电的正负功率
                                                       if (oneTactics.tType == "放电")
                                                           oneTactics.waValue = -newRd.GetInt32(4);
                                                       else
                                                           oneTactics.waValue = newRd.GetInt32(4);

                                                       //策略日期
                                                       oneTactics.strategyDate = newRd.GetDateTime(5);

                                                       TacticsList.Add(oneTactics);
                                                   }
                                               }
                                           }
                                       }
                                   }
                               }
                               else
                               {
                                   log.Error("不存在今日策略，且无任何过期策略");
                               }
                           }
                       }
                       Result = true;
                   }
                   catch (Exception ex)
                   {
                       log.Error(ex.Message);
                   }
                   return Result;
               }
       */

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
                        continue;
                    }
                    //开启策略，若EMS无策略则重新读取数据库
                    if (TacticsList.Count == 0)
                    {
                        LoadFromMySQL(0);
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
