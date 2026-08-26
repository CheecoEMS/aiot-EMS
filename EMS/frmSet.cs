using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Diagnostics;
using EMS;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Google.Protobuf.WellKnownTypes;
using MySqlX.XDevAPI.Common;
using Mysqlx.Session;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;
using System.IO;
using Squirrel;
using System.Reflection;
using System.Web.UI;
using Org.BouncyCastle.Utilities.Collections;
using static System.Windows.Forms.AxHost;
using System.Data;

namespace EMS
{
    public partial class frmSet : Form
    {
        private delegate void AddoneStep();
        private bool ProgressOn = true;

        //8.8
        private static ILog log = LogManager.GetLogger("frmSet");

        public static frmSet oneForm = null;

        public volatile static CloudLimitClass cloudLimits = new CloudLimitClass();
        public volatile static ConfigClass config = new ConfigClass();
        public volatile static VariChargeClass variCharge = new VariChargeClass();
        public volatile static ComponentSettingsClass componentSettings = new ComponentSettingsClass();
        public volatile static RateTableScheduleItem rateTableScheduleItem = new RateTableScheduleItem();
        public volatile static HistoryDataClass historyDatas = new HistoryDataClass();
        public volatile static PeElesticClass peElestic = new PeElesticClass();

        //public static string INIPath = ""; //ini文件的地址和文件名称
        public static string BalaPath = "";
        public static int FreshInterval;
        public static string PCSType;
        public static int PCSwaValue;
        public static string[] TimeZones = new string[4];
        public static int[] TZSetIndex = { 0, 0, 0, 0 };
        public static int[,] Prices = { { 0, 0, 0, 0, 0, 0, 0, 0, 0 }, { 0, 0, 0, 0, 0, 0, 0, 0, 0 } }; //无尖峰平谷的电价
        private bool bTCDataChanged = false;
        private bool bEDataChanged = false;
        public bool bSheduleChanged = false;
        private const string strDriveDllName = "SpesTechDriverControl.dll";
        private const string strExeDllName = "SpesTechMmioRW.dll";
        private static readonly object gpioDriverLock = new object();
        private static IntPtr gpioDriverHandle = IntPtr.Zero;
        private static bool gpioDriverStopping = false;

        private static int PeElesticId = 1;
        private static String ConfigId = "";
        private static int HistoricalDataId = 1;
        private static int VariChargeId = 1;
        private static int CloudLimitsId = 1;
        private static int ComponentSettingsId = 1;


        public frmSet()
        {
            InitializeComponent();
        }
        static public void INIForm()
        {
            if (oneForm == null)
                oneForm = new frmSet();
        }
        static public void CloseForm()
        {
            try
            {
                if (oneForm != null)
                {
                    oneForm.Hide();
                    frmMain.ShowMainForm();

                    //oneForm.Close();
                    //oneForm.Dispose();
                    //oneForm = null;
                }
            }
            catch (Exception ex)
            {
                log.Error("CloseForm异常：" + ex.Message);
            }
        }

        static public void ShowForm()
        {
            try
            {
                if (oneForm == null)
                    oneForm = new frmSet();

                if (oneForm != null)
                {
                    frmSet.LoadCloudLimitsFromMySQL();
                    frmSet.LoadConfigFromMySQL();
                    frmSet.LoadComponentSettingsFromMySQL();

                    oneForm.ShowVersion();
                    oneForm.ShowINIdata();
                    oneForm.btnBaseInf_Click(null, EventArgs.Empty);
                    oneForm.bTCDataChanged = false;
                    oneForm.bEDataChanged = false;
                    oneForm.bSheduleChanged = false;
                    oneForm.SetFormPower(frmMain.UserPower);
                    oneForm.Show();
                    oneForm.BringToFront();
                }
                //oneForm.ShowDialog();
            }
            catch (Exception ex)
            {
                log.Error("ShowForm: " + ex.Message);
            }
        }

        public void SetFormPower(int aPower)
        {
            btnLine.Visible = (aPower >= 0);
            btnState.Visible = (aPower >= 0);
            btnWarning.Visible = (aPower >= 1);
            btnControl.Visible = (aPower >= 2);
            btnSet.Visible = (aPower >= 3);
            btnLC.Visible = (aPower >= 0);
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        //系统重启
        public static void RestartWindows()
        {
/*            if (frmSet.historyDatas != null &&  frmSet.historyDatas.RebootCount > 0)
            {
                historyDatas.RebootCount--;

                PowerGPIO(0);
                Set_Cloudlimits();
                Set_HistoryData();

                if (frmMain.Selffrm.AllEquipment.Led != null)
                {
                    frmMain.Selffrm.AllEquipment.Led.Set_Led_ShutDown();
                }
                if (frmMain.Selffrm.AllEquipment != null)
                {
                    for (int j = 0; j < frmMain.Selffrm.AllEquipment.PCSList.Count; j++)
                    {
                        frmMain.Selffrm.AllEquipment.PCSList[j].ExcSetPCSPower(false);
                    }
                }

                Thread.Sleep(120000);
                SysIO.Reboot();
            }*/
        }

        //EMS重启
        public static void RestartApplicationNoCount()
        {
            try
            {
                    PowerGPIO(0);
                    Set_Cloudlimits();
                    Set_HistoryData();

                    if (frmMain.Selffrm.AllEquipment.Led != null)
                    {
                        frmMain.Selffrm.AllEquipment.Led.Set_Led_ShutDown();
                    }
                    if (frmMain.Selffrm.AllEquipment.PCSList != null)
                    {
                        for (int j = 0; j < frmMain.Selffrm.AllEquipment.PCSList.Count; j++)
                        {
                            frmMain.Selffrm.AllEquipment.PCSList[j].ExcSetPCSPower(false);
                        }
                    }

                    ShutdownGPIODriver();

                    string exePath = AppDomain.CurrentDomain.BaseDirectory + "\\EMS.exe";
                    try
                    {
                        Process.Start(exePath);
                    }
                    catch (Exception ex)
                    {
                        log.Error("无法重启应用程序: " + ex.Message);
                    }

                    // 退出当前进程
                    Environment.Exit(0);

            }
            catch (Exception ex)
            {
                log.Error("RestartApplication: " + ex.Message);
            }
        }

        public static void RestartApplication()
        {
            try
            {
                if (historyDatas != null &&  historyDatas.RebootCount > 0)
                {
                    historyDatas.RebootCount--;

                    PowerGPIO(0);
                    Set_Cloudlimits();
                    Set_HistoryData();

                    if (frmMain.Selffrm.AllEquipment.Led != null)
                    {
                        frmMain.Selffrm.AllEquipment.Led.Set_Led_ShutDown();
                    }
                    if (frmMain.Selffrm.AllEquipment.PCSList != null)
                    {
                        for (int j = 0; j < frmMain.Selffrm.AllEquipment.PCSList.Count; j++)
                        {
                            frmMain.Selffrm.AllEquipment.PCSList[j].ExcSetPCSPower(false);
                        }
                    }

                    ShutdownGPIODriver();

                    string exePath = AppDomain.CurrentDomain.BaseDirectory + "\\EMS.exe";
                    try
                    {
                        Process.Start(exePath);
                    }
                    catch (Exception ex)
                    {
                        log.Error("无法重启应用程序: " + ex.Message);
                    }

                    // 退出当前进程
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                log.Error("RestartApplication: " + ex.Message);
            }
        }



        /***********************************************************************************************************************/

        #region 辅助方法
        private static double GetDoubleValueFromReader(MySqlDataReader reader, string columnName, double defaultValue)
        {
            try
            {
                var value = reader[columnName];
                if (value == DBNull.Value)
                    return defaultValue;
                return Convert.ToDouble(value);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static int GetIntValueFromReader(MySqlDataReader reader, string columnName, int defaultValue)
        {
            try
            {
                var value = reader[columnName];
                if (value == DBNull.Value)
                    return defaultValue;
                return Convert.ToInt32(value);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static DateTime GetDateTimeValueFromReader(MySqlDataReader reader, string columnName, DateTime defaultValue)
        {
            try
            {
                var value = reader[columnName];
                if (value == DBNull.Value)
                    return defaultValue;
                return Convert.ToDateTime(value);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 安全获取字符串值，自动处理 DBNull 和 空白字符串
        /// </summary>
        private static string GetStringValueFromReader(MySqlDataReader reader, string columnName, string defaultValue)
        {
            try
            {
                var value = reader[columnName];

                // 处理 DBNull
                if (value == DBNull.Value || value == null)
                {
                    return defaultValue;
                }

                string strValue = value.ToString();

                // 处理空字符串或纯空格
                if (string.IsNullOrWhiteSpace(strValue))
                {
                    return defaultValue;
                }

                return strValue;
            }
            catch (Exception ex)
            {
                // 记录特定字段的转换错误，但不中断整个流程
                log.Warn($"字段 {columnName} 读取失败，使用默认值 '{defaultValue}'. 错误: {ex.Message}");
                return defaultValue;
            }
        }
        #endregion

        public static Dictionary<DateTime, int> LoadRateTableSchedule(string meterType)
        {
            var result = new Dictionary<DateTime, int>();

            if (string.IsNullOrEmpty(meterType))
                return result;

            const string sql =
                "SELECT rDate, SlotNo " +
                "FROM RateTableSchedule " +
                "WHERE rDate >= @today AND MeterType = @meterType";

            var param = new Dictionary<string, object>
            {
                { "@today", DateTime.Today },
                { "@meterType", meterType }
            };

            DataTable dt = DBConnection.QueryDataTableWithParams(sql, param);
            if (dt == null)
                return result;

            foreach (DataRow row in dt.Rows)
            {
                DateTime date = Convert.ToDateTime(row["rDate"]).Date;
                int slotNo = Convert.ToInt32(row["SlotNo"]);

                // ✅ Slot 校验仍然只认 1 / 2（逻辑槽）
                if (slotNo == 1 || slotNo == 2)
                {
                    result[date] = slotNo;
                }
            }

            return result;
        }

        public static void SaveRateTableSchedule(
            string meterType,
            int todaySlot,
            int tomorrowSlot)
        {
            if (string.IsNullOrEmpty(meterType))
                return;

            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);

            // 1. 删除非今天 / 明天的数据（仅限当前 MeterType）
            const string deleteSql =
                "DELETE FROM RateTableSchedule " +
                "WHERE MeterType = @meterType AND rDate NOT IN (@today, @tomorrow)";

            DBConnection.ExecSQLWithParams(deleteSql, new Dictionary<string, object>
            {
                { "@meterType", meterType },
                { "@today", today },
                { "@tomorrow", tomorrow }
            });

            // 2. Upsert 今天
            UpsertSchedule(meterType, today, todaySlot);

            // 3. Upsert 明天
            UpsertSchedule(meterType, tomorrow, tomorrowSlot);
        }


        private static void UpsertSchedule(
            string meterType,
            DateTime date,
            int slotNo)
        {
            // ✅ 防御性校验
            if (string.IsNullOrEmpty(meterType))
                return;

            // ✅ 逻辑 Slot 只允许 1 / 2
            if (slotNo != 1 && slotNo != 2)
                return;

            const string checkSql =
                "SELECT COUNT(*) " +
                "FROM RateTableSchedule " +
                "WHERE rDate = @rDate AND MeterType = @meterType";

            var checkParams = new Dictionary<string, object>
            {
                { "@rDate", date.Date },
                { "@meterType", meterType }
            };

            object obj = DBConnection.QuerySingleValue(checkSql, checkParams);
            int count = 0;

            if (obj != null && obj != DBNull.Value)
            {
                int.TryParse(obj.ToString(), out count);
            }

            if (count > 0)
            {
                // UPDATE
                const string updateSql =
                    "UPDATE RateTableSchedule " +
                    "SET SlotNo = @SlotNo " +
                    "WHERE rDate = @rDate AND MeterType = @meterType";

                DBConnection.ExecSQLWithParams(updateSql, new Dictionary<string, object>
                {
                    { "@SlotNo", slotNo },
                    { "@rDate", date.Date },
                    { "@meterType", meterType }
                });
            }
            else
            {
                // INSERT
                const string insertSql =
                    "INSERT INTO RateTableSchedule (rDate, MeterType, SlotNo) " +
                    "VALUES (@rDate, @meterType, @SlotNo)";

                DBConnection.ExecSQLWithParams(insertSql, new Dictionary<string, object>
                {
                    { "@rDate", date.Date },
                    { "@meterType", meterType },
                    { "@SlotNo", slotNo }
                });
            }
        }


        /*********************************************
         *
         *          peElestic
         *
         ********************************************/
        public static bool CheckPeElestic()
        {
            string astrSQL = "SELECT COUNT(*) FROM PeElestic;";
            try
            {
                using (MySqlConnection connection = new MySqlConnection(DBConnection.connectionStr))
                {
                    connection.Open();
                    using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                    {
                        object result = sqlCmd.ExecuteScalar(); // 使用ExecuteScalar获取计数
                        if (result != null && Convert.ToInt32(result) > 0)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                log.Error($"MySqlException in CheckPeElestic: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                log.Error($"Exception in CheckPeElestic: {ex.Message}");
                return false;
            }
            return false;
        }


        public static bool LoadPeElesticFromMySQL()
        {
            string sql = "SELECT rDate, SE2PKWH0, SE2OKWH0, SAuxiliaryKWH0, SE2PKWH1, SE2OKWH1, SAuxiliaryKWH1, SE2PKWH2, SE2OKWH2, SAuxiliaryKWH2, SE2PKWH3, SE2OKWH3, SAuxiliaryKWH3, SE2PKWH4, SE2OKWH4, "
                       + "SAuxiliaryKWH4, SE2PKWH5, SE2OKWH5, SE2PKWH6, SE2OKWH6, SE2PKWH7, SE2OKWH7, SE2PKWH8, SE2OKWH8 FROM PeElestic WHERE id = @id";

            try
            {
                var parameters = new Dictionary<string, object> { { "@id", PeElesticId } };
                using (var reader = DBConnection.QueryDataReader(sql, parameters))
                {
                    if (reader != null && reader.Read())
                    {
                        if (peElestic != null)
                        {
                            peElestic.rDate = GetDateTimeValueFromReader(reader, "rDate", DateTime.MinValue);
                            peElestic.SE2PKWH[0] = GetDoubleValueFromReader(reader, "SE2PKWH0", 0);
                            peElestic.SE2OKWH[0] = GetDoubleValueFromReader(reader, "SE2OKWH0", 0);
                            peElestic.SAuxiliaryKWH[0] = GetDoubleValueFromReader(reader, "SAuxiliaryKWH0", 0);
                            peElestic.SE2PKWH[1] = GetDoubleValueFromReader(reader, "SE2PKWH1", 0);
                            peElestic.SE2OKWH[1] = GetDoubleValueFromReader(reader, "SE2OKWH1", 0);
                            peElestic.SAuxiliaryKWH[1] = GetDoubleValueFromReader(reader, "SAuxiliaryKWH1", 0);
                            peElestic.SE2PKWH[2] = GetDoubleValueFromReader(reader, "SE2PKWH2", 0);
                            peElestic.SE2OKWH[2] = GetDoubleValueFromReader(reader, "SE2OKWH2", 0);
                            peElestic.SAuxiliaryKWH[2] = GetDoubleValueFromReader(reader, "SAuxiliaryKWH2", 0);
                            peElestic.SE2PKWH[3] = GetDoubleValueFromReader(reader, "SE2PKWH3", 0);
                            peElestic.SE2OKWH[3] = GetDoubleValueFromReader(reader, "SE2OKWH3", 0);
                            peElestic.SAuxiliaryKWH[3] = GetDoubleValueFromReader(reader, "SAuxiliaryKWH3", 0);
                            peElestic.SE2PKWH[4] = GetDoubleValueFromReader(reader, "SE2PKWH4", 0);
                            peElestic.SE2OKWH[4] = GetDoubleValueFromReader(reader, "SE2OKWH4", 0);
                            peElestic.SAuxiliaryKWH[4] = GetDoubleValueFromReader(reader, "SAuxiliaryKWH4", 0);
                            peElestic.SE2PKWH[5] = GetDoubleValueFromReader(reader, "SE2PKWH5", 0);
                            peElestic.SE2OKWH[5] = GetDoubleValueFromReader(reader, "SE2OKWH5", 0);
                            peElestic.SE2PKWH[6] = GetDoubleValueFromReader(reader, "SE2PKWH6", 0);
                            peElestic.SE2OKWH[6] = GetDoubleValueFromReader(reader, "SE2OKWH6", 0);
                            peElestic.SE2PKWH[7] = GetDoubleValueFromReader(reader, "SE2PKWH7", 0);
                            peElestic.SE2OKWH[7] = GetDoubleValueFromReader(reader, "SE2OKWH7", 0);
                            peElestic.SE2PKWH[8] = GetDoubleValueFromReader(reader, "SE2PKWH8", 0);
                            peElestic.SE2OKWH[8] = GetDoubleValueFromReader(reader, "SE2OKWH8", 0);
                        }
                        return true;
                    }
                }

                log.Warn($"未找到 PeElestic 配置，ID: {PeElesticId}");
                return false;
            }
            catch (Exception ex)
            {
                log.Error($"LoadPeElesticFromMySQL 失败: {ex.Message}", ex);
                return false;
            }
        }

        public static bool Set_PeElesticData(string tempDate)
        {
            bool result = false;

            try
            {
                string sql =
                    "UPDATE PeElestic SET " +
                    "rDate=@rDate, " +
                    "SE2PKWH0=@SE2PKWH0, SE2OKWH0=@SE2OKWH0, SAuxiliaryKWH0=@SAuxiliaryKWH0, " +
                    "SE2PKWH1=@SE2PKWH1, SE2OKWH1=@SE2OKWH1, SAuxiliaryKWH1=@SAuxiliaryKWH1, " +
                    "SE2PKWH2=@SE2PKWH2, SE2OKWH2=@SE2OKWH2, SAuxiliaryKWH2=@SAuxiliaryKWH2, " +
                    "SE2PKWH3=@SE2PKWH3, SE2OKWH3=@SE2OKWH3, SAuxiliaryKWH3=@SAuxiliaryKWH3, " +
                    "SE2PKWH4=@SE2PKWH4, SE2OKWH4=@SE2OKWH4, SAuxiliaryKWH4=@SAuxiliaryKWH4, " +
                    "SE2PKWH5=@SE2PKWH5, SE2OKWH5=@SE2OKWH5, " +
                    "SE2PKWH6=@SE2PKWH6, SE2OKWH6=@SE2OKWH6, " +
                    "SE2PKWH7=@SE2PKWH7, SE2OKWH7=@SE2OKWH7, " +
                    "SE2PKWH8=@SE2PKWH8, SE2OKWH8=@SE2OKWH8 " +
                    "WHERE id=@id;";

                var parameters = new Dictionary<string, object>
                {
                    { "@rDate", tempDate ?? string.Empty },
                    { "@SE2PKWH0", peElestic.SE2PKWH[0] }, { "@SE2OKWH0", peElestic.SE2OKWH[0] }, { "@SAuxiliaryKWH0", peElestic.SAuxiliaryKWH[0] },
                    { "@SE2PKWH1", peElestic.SE2PKWH[1] }, { "@SE2OKWH1", peElestic.SE2OKWH[1] }, { "@SAuxiliaryKWH1", peElestic.SAuxiliaryKWH[1] },
                    { "@SE2PKWH2", peElestic.SE2PKWH[2] }, { "@SE2OKWH2", peElestic.SE2OKWH[2] }, { "@SAuxiliaryKWH2", peElestic.SAuxiliaryKWH[2] },
                    { "@SE2PKWH3", peElestic.SE2PKWH[3] }, { "@SE2OKWH3", peElestic.SE2OKWH[3] }, { "@SAuxiliaryKWH3", peElestic.SAuxiliaryKWH[3] },
                    { "@SE2PKWH4", peElestic.SE2PKWH[4] }, { "@SE2OKWH4", peElestic.SE2OKWH[4] }, { "@SAuxiliaryKWH4", peElestic.SAuxiliaryKWH[4] },
                    { "@SE2PKWH5", peElestic.SE2PKWH[5] }, { "@SE2OKWH5", peElestic.SE2OKWH[5] },
                    { "@SE2PKWH6", peElestic.SE2PKWH[6] }, { "@SE2OKWH6", peElestic.SE2OKWH[6] },
                    { "@SE2PKWH7", peElestic.SE2PKWH[7] }, { "@SE2OKWH7", peElestic.SE2OKWH[7] },
                    { "@SE2PKWH8", peElestic.SE2PKWH[8] }, { "@SE2OKWH8", peElestic.SE2OKWH[8] },
                    { "@id", PeElesticId }
                };

                result = DBConnection.ExecSQLWithParams(sql, parameters) >= 0;
            }
            catch (Exception ex)
            {
                // 处理异常情况
                result = false;
                log.Error("Set_PeElesticData: " + ex.Message);
            }
            return result;
        }

        /*        public static bool Insert_PeElesticData(string tempDate)
                {
                    // 假设 PeElestic 表有一个自增的主键或其他唯一标识符，这里不显式插入
                    string astrSQL = "INSERT INTO PeElestic (rDate, SE2PKWH0, SE2OKWH0, SAuxiliaryKWH0, SE2PKWH1, SE2OKWH1, SAuxiliaryKWH1, " +
                                     "SE2PKWH2, SE2OKWH2, SAuxiliaryKWH2, SE2PKWH3, SE2OKWH3, SAuxiliaryKWH3, SE2PKWH4, SE2OKWH4, SAuxiliaryKWH4, " +
                                     "SE2PKWH5, SE2OKWH5, SE2PKWH6, SE2OKWH6, SE2PKWH7, SE2OKWH7, SE2PKWH8, SE2OKWH8) " +
                                     "VALUES ('" + tempDate + "', '" + frmSet.peElestic.SE2PKWH[0].ToString() + "', '" + frmSet.peElestic.SE2OKWH[0].ToString() + "', '" +
                                     frmSet.peElestic.SAuxiliaryKWH[0].ToString() + "', '" + frmSet.peElestic.SE2PKWH[1].ToString() + "', '" +
                                     frmSet.peElestic.SE2OKWH[1].ToString() + "', '" + frmSet.peElestic.SAuxiliaryKWH[1].ToString() + "', '" +
                                     frmSet.peElestic.SE2PKWH[2].ToString() + "', '" + frmSet.peElestic.SE2OKWH[2].ToString() + "', '" +
                                     frmSet.peElestic.SAuxiliaryKWH[2].ToString() + "', '" + frmSet.peElestic.SE2PKWH[3].ToString() + "', '" +
                                     frmSet.peElestic.SE2OKWH[3].ToString() + "', '" + frmSet.peElestic.SAuxiliaryKWH[3].ToString() + "', '" +
                                     frmSet.peElestic.SE2PKWH[4].ToString() + "', '" + frmSet.peElestic.SE2OKWH[4].ToString() + "', '" +
                                     frmSet.peElestic.SAuxiliaryKWH[4].ToString() + "', '" + frmSet.peElestic.SE2PKWH[5].ToString() + "', '" +
                                     frmSet.peElestic.SE2OKWH[5].ToString() + "', '" + frmSet.peElestic.SE2PKWH[6].ToString() + "', '" +
                                     frmSet.peElestic.SE2OKWH[6].ToString() + "', '" + frmSet.peElestic.SE2PKWH[7].ToString() + "', '" +
                                     frmSet.peElestic.SE2OKWH[7].ToString() + "', '" + frmSet.peElestic.SE2PKWH[8].ToString() + "', '" +
                                     frmSet.peElestic.SE2OKWH[8].ToString() + "')";

                    bool result = false;

                    try
                    {
                        if (DBConnection.ExecSQLWithParams(astrSQL, null) >= 0)
                        {
                            result = true;
                        }
                        else
                        {
                            // 处理执行失败的逻辑
                            result = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 处理异常情况
                        result = false;
                        log.Error(ex.Message);
                    }
                    return result;
                }*/

        /*********************************************
         *
         *          HistoryData
         *
         ********************************************/

        #region 加载HistoryData
        public static bool LoadHistoryDataFromMySQL()
        {
            string sql = "SELECT E1PUMdemandMaxOld, ClientPUMdemandMaxOld, ClientPUMdemandMax, ErrorState2, RebootCount, YDstatus FROM HistoricalData WHERE id = @id";

            try
            {
                var parameters = new Dictionary<string, object> { { "@id", HistoricalDataId } };
                using (var reader = DBConnection.QueryDataReader(sql, parameters))
                {
                    if (reader != null && reader.Read())
                    {
                        historyDatas.E1PUMdemandMaxOld = GetIntValueFromReader(reader, "E1PUMdemandMaxOld", 0);
                        historyDatas.ClientPUMdemandMaxOld = GetIntValueFromReader(reader, "ClientPUMdemandMaxOld", 0);
                        historyDatas.ClientPUMdemandMax = GetIntValueFromReader(reader, "ClientPUMdemandMax", 0);
                        historyDatas.ErrorState2 = GetIntValueFromReader(reader, "ErrorState2", 0);
                        historyDatas.RebootCount = GetIntValueFromReader(reader, "RebootCount", 5);
                        historyDatas.YDstatus = GetIntValueFromReader(reader, "YDstatus", 0);

                        return true;
                    }
                }

                log.Warn($"未找到 HistoricalData 配置，ID: {HistoricalDataId}");
                return false;
            }
            catch (Exception ex)
            {
                log.Error($"LoadHistoryDataFromMySQL 失败: {ex.Message}", ex);
                return false;
            }
        }
        #endregion

        /* public static bool LoadHistoryDataFromMySQL()
         {
             string astrSQL = "SELECT E1PUMdemandMaxOld, ClientPUMdemandMaxOld, ClientPUMdemandMax, ErrorState2, RebootCount, YDstatus FROM HistoricalData WHERE id = " + HistoricalDataId + ";";

             try
             {
                 using (MySqlConnection connection = new MySqlConnection(DBConnection.connectionStr))
                 {
                     connection.Open();
                     using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                     {
                         using (MySqlDataReader rd = sqlCmd.ExecuteReader())
                         {
                             if (rd != null && rd.HasRows && rd.Read())
                             {
                                 historyDatas.E1PUMdemandMaxOld     = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                                 historyDatas.ClientPUMdemandMaxOld = rd.IsDBNull(1) ? 0 : rd.GetInt32(1);
                                 historyDatas.ClientPUMdemandMax    = rd.IsDBNull(2) ? 0 : rd.GetInt32(2);
                                 historyDatas.ErrorState2           = rd.IsDBNull(3) ? 0 : rd.GetInt32(3);
                                 historyDatas.RebootCount = rd.IsDBNull(4) ? 5 : rd.GetInt32(4);
                                 historyDatas.YDstatus = rd.IsDBNull(5) ? 0 : rd.GetInt32(5);

                                 return true;
                             }
                         }
                     }
                 }
             }
             catch (MySqlException ex)
             {
                 log.Error(ex.Message);
                 return false;
             }
             catch (Exception ex)
             {
                 log.Error(ex.Message);
                 return false;
             }

             log.Error("HistoryData加载失败");
             return false;
         }*/

        public static bool Set_HistoryData()
        {
            bool result = false;

            try
            {
                string sql =
                    "UPDATE HistoricalData SET " +
                    "E1PUMdemandMaxOld=@E1PUMdemandMaxOld, " +
                    "ClientPUMdemandMaxOld=@ClientPUMdemandMaxOld, " +
                    "ClientPUMdemandMax=@ClientPUMdemandMax, " +
                    "ErrorState2=@ErrorState2, " +
                    "RebootCount=@RebootCount, " +
                    "YDstatus=@YDstatus " +
                    "WHERE id=@id;";

                var parameters = new Dictionary<string, object>
                {
                    { "@E1PUMdemandMaxOld", historyDatas.E1PUMdemandMaxOld },
                    { "@ClientPUMdemandMaxOld", historyDatas.ClientPUMdemandMaxOld },
                    { "@ClientPUMdemandMax", historyDatas.ClientPUMdemandMax },
                    { "@ErrorState2", historyDatas.ErrorState2 },
                    { "@RebootCount", historyDatas.RebootCount },
                    { "@YDstatus", historyDatas.YDstatus },
                    { "@id", HistoricalDataId }
                };

                result = DBConnection.ExecSQLWithParams(sql, parameters) >= 0;
            }
            catch (Exception ex)
            {
                // 处理异常情况
                result = false;
                log.Error("Set_HistoryData: " + ex.Message);
            }
            return result;
        }

        /*        public static bool LoadHistoryDataFromMySQL()
                {
                    string astrSQL = "SELECT E1PUMdemandMaxOld, ClientPUMdemandMaxOld, ClientPUMdemandMax, ErrorState2 ,DaliyE2PKWH_Z, DaliyE2PKWH_J, DaliyE2PKWH_F, DaliyE2PKWH_P, DaliyE2PKWH_G, DaliyE2PKWH_5, DaliyE2PKWH_6, DaliyE2PKWH_7, DaliyE2PKWH_8, "
                                    + " DaliyE2OKWH_Z, DaliyE2OKWH_J, DaliyE2OKWH_F, DaliyE2OKWH_P, DaliyE2OKWH_G, DaliyE2OKWH_5, DaliyE2OKWH_6, DaliyE2OKWH_7, DaliyE2OKWH_8, RebootCount, YDstatus FROM HistoricalData WHERE id = " + HistoricalDataId + ";";

                    try
                    {
                        using (MySqlConnection connection = new MySqlConnection(DBConnection.connectionStr))
                        {
                            connection.Open();
                            using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                            {
                                using (MySqlDataReader rd = sqlCmd.ExecuteReader())
                                {
                                    if (rd != null && rd.HasRows && rd.Read())
                                    {
                                        historyDatas.E1PUMdemandMaxOld     = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                                        historyDatas.ClientPUMdemandMaxOld = rd.IsDBNull(1) ? 0 : rd.GetInt32(1);
                                        historyDatas.ClientPUMdemandMax    = rd.IsDBNull(2) ? 0 : rd.GetInt32(2);
                                        historyDatas.ErrorState2           = rd.IsDBNull(3) ? 0 : rd.GetInt32(3);
                                        historyDatas.DaliyE2PKWH_Z = rd.IsDBNull(4) ? 0 : rd.GetInt32(4);
                                        historyDatas.DaliyE2PKWH_J = rd.IsDBNull(5) ? 0 : rd.GetInt32(5);
                                        historyDatas.DaliyE2PKWH_F = rd.IsDBNull(6) ? 0 : rd.GetInt32(6);
                                        historyDatas.DaliyE2PKWH_P = rd.IsDBNull(7) ? 0 : rd.GetInt32(7);
                                        historyDatas.DaliyE2PKWH_G = rd.IsDBNull(8) ? 0 : rd.GetInt32(8);
                                        historyDatas.DaliyE2PKWH_5 = rd.IsDBNull(9) ? 0 : rd.GetInt32(9);
                                        historyDatas.DaliyE2PKWH_6 = rd.IsDBNull(10) ? 0 : rd.GetInt32(10);
                                        historyDatas.DaliyE2PKWH_7 = rd.IsDBNull(11) ? 0 : rd.GetInt32(11);
                                        historyDatas.DaliyE2PKWH_8 = rd.IsDBNull(12) ? 0 : rd.GetInt32(12);
                                        historyDatas.DaliyE2OKWH_Z = rd.IsDBNull(13) ? 0 : rd.GetInt32(13);
                                        historyDatas.DaliyE2OKWH_J = rd.IsDBNull(14) ? 0 : rd.GetInt32(14);
                                        historyDatas.DaliyE2OKWH_F = rd.IsDBNull(15) ? 0 : rd.GetInt32(15);
                                        historyDatas.DaliyE2OKWH_P = rd.IsDBNull(16) ? 0 : rd.GetInt32(16);
                                        historyDatas.DaliyE2OKWH_G = rd.IsDBNull(17) ? 0 : rd.GetInt32(17);
                                        historyDatas.DaliyE2OKWH_5 = rd.IsDBNull(18) ? 0 : rd.GetInt32(18);
                                        historyDatas.DaliyE2OKWH_6 = rd.IsDBNull(19) ? 0 : rd.GetInt32(19);
                                        historyDatas.DaliyE2OKWH_7 = rd.IsDBNull(20) ? 0 : rd.GetInt32(20);
                                        historyDatas.DaliyE2OKWH_8 = rd.IsDBNull(21) ? 0 : rd.GetInt32(21);
                                        historyDatas.RebootCount = rd.IsDBNull(22) ? 5 : rd.GetInt32(22);
                                        historyDatas.YDstatus = rd.IsDBNull(23) ? 0 : rd.GetInt32(23);

                                        return  true;
                                    }
                                }
                            }
                        }
                    }
                    catch (MySqlException ex)
                    {
                        log.Error(ex.Message);
                        return false;
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message);
                        return false;
                    }

                    log.Error("HistoryData加载失败");
                    return false;
                }

                public static bool Set_HistoryData()
                {
                    string astrSQL = "update  HistoricalData  SET "
                        + " E1PUMdemandMaxOld ='" + frmSet.historyDatas.E1PUMdemandMaxOld.ToString()
                        + "', ClientPUMdemandMaxOld ='" + frmSet.historyDatas.ClientPUMdemandMaxOld.ToString()
                        + "', ClientPUMdemandMax ='" + frmSet.historyDatas.ClientPUMdemandMax.ToString()
                        + "', ErrorState2 ='" + frmSet.historyDatas.ErrorState2.ToString()
                        + "', DaliyE2PKWH_Z ='" + frmSet.historyDatas.DaliyE2PKWH_Z.ToString()
                        + "', DaliyE2PKWH_J ='" + frmSet.historyDatas.DaliyE2PKWH_J.ToString()
                        + "', DaliyE2PKWH_F ='" + frmSet.historyDatas.DaliyE2PKWH_F.ToString()
                        + "', DaliyE2PKWH_P ='" + frmSet.historyDatas.DaliyE2PKWH_P.ToString()
                        + "', DaliyE2PKWH_G ='" + frmSet.historyDatas.DaliyE2PKWH_G.ToString()
                        + "', DaliyE2PKWH_5 ='" + frmSet.historyDatas.DaliyE2PKWH_5.ToString()
                        + "', DaliyE2PKWH_6 ='" + frmSet.historyDatas.DaliyE2PKWH_6.ToString()
                        + "', DaliyE2PKWH_7 ='" + frmSet.historyDatas.DaliyE2PKWH_7.ToString()
                        + "', DaliyE2PKWH_8 ='" + frmSet.historyDatas.DaliyE2PKWH_8.ToString()
                        + "', DaliyE2OKWH_Z ='" + frmSet.historyDatas.DaliyE2OKWH_Z.ToString()
                        + "', DaliyE2OKWH_J ='" + frmSet.historyDatas.DaliyE2OKWH_J.ToString()
                        + "', DaliyE2OKWH_F ='" + frmSet.historyDatas.DaliyE2OKWH_F.ToString()
                        + "', DaliyE2OKWH_P ='" + frmSet.historyDatas.DaliyE2OKWH_P.ToString()
                        + "', DaliyE2OKWH_G ='" + frmSet.historyDatas.DaliyE2OKWH_G.ToString()
                        + "', DaliyE2PKWH_5 ='" + frmSet.historyDatas.DaliyE2PKWH_5.ToString()
                        + "', DaliyE2PKWH_6 ='" + frmSet.historyDatas.DaliyE2PKWH_6.ToString()
                        + "', DaliyE2PKWH_7 ='" + frmSet.historyDatas.DaliyE2PKWH_7.ToString()
                        + "', DaliyE2PKWH_8 ='" + frmSet.historyDatas.DaliyE2PKWH_8.ToString()
                        + "', RebootCount ='" + frmSet.historyDatas.RebootCount.ToString()
                         + "', YDstatus ='" + frmSet.historyDatas.YDstatus.ToString()
                        + "' WHERE id = " + HistoricalDataId + ";";

                    bool result = false;

                    try
                    {
                        if (DBConnection.ExecSQLWithParams(astrSQL, null) >= 0)
                        {

                            result = true;
                        }
                        else
                        {
                            // 处理执行失败的逻辑
                            result = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 处理异常情况
                        result = false;
                        log.Error("Set_HistoryData: " + ex.Message);
                    }
                    return result;
                }*/

        /*********************************************
         *
         *          Cloudlimits
         *
         ********************************************/

        #region 加载CloudLimits
        public static bool LoadCloudLimitsFromMySQL()
        {
            string sql = @"
                SELECT MaxGridKW, MinGridKW, MaxSOC, MinSOC, WarnMaxGridKW, WarnMinGridKW,
                       PcsKva, Pre_Client_PUMdemand_Max, EnableActiveReduce, PumScale,
                       AllUkvaWindowSize, PumTime, BmsDerateRatio, FrigOpenLower, FrigOffLower,
                       FrigOffUpper, BoxHTemperAlarm, BoxLTemperAlarm, SignalDelayAlarm,
                       SignalDelayCount, CellV_Gap, OpenBala, OpenWarning
                FROM CloudLimits
                WHERE id = @id";

            try
            {
                var parameters = new Dictionary<string, object> { { "@id", CloudLimitsId } };
                using (var reader = DBConnection.QueryDataReader(sql, parameters))
                {
                    if (reader != null && reader.Read())
                    {
                        cloudLimits.MaxGridKW = GetIntValueFromReader(reader, "MaxGridKW", 0);
                        cloudLimits.MinGridKW = GetIntValueFromReader(reader, "MinGridKW", 0);
                        cloudLimits.MaxSOC = GetIntValueFromReader(reader, "MaxSOC", 100);
                        cloudLimits.MinSOC = GetIntValueFromReader(reader, "MinSOC", 0);
                        cloudLimits.WarnMaxGridKW = GetIntValueFromReader(reader, "WarnMaxGridKW", 0);
                        cloudLimits.WarnMinGridKW = GetIntValueFromReader(reader, "WarnMinGridKW", 0);
                        cloudLimits.PcsKva = GetIntValueFromReader(reader, "PcsKva", 10);
                        cloudLimits.Pre_Client_PUMdemand_Max = GetIntValueFromReader(reader, "Pre_Client_PUMdemand_Max", 0);
                        cloudLimits.EnableActiveReduce = GetIntValueFromReader(reader, "EnableActiveReduce", 0);
                        cloudLimits.PumScale = GetIntValueFromReader(reader, "PumScale", 0);
                        cloudLimits.AllUkvaWindowSize = GetIntValueFromReader(reader, "AllUkvaWindowSize", 4);
                        cloudLimits.PumTime = GetIntValueFromReader(reader, "PumTime", 5);
                        cloudLimits.BmsDerateRatio = GetIntValueFromReader(reader, "BmsDerateRatio", 50);
                        cloudLimits.FrigOpenLower = GetIntValueFromReader(reader, "FrigOpenLower", 30);
                        cloudLimits.FrigOffLower = GetIntValueFromReader(reader, "FrigOffLower", 10);
                        cloudLimits.FrigOffUpper = GetIntValueFromReader(reader, "FrigOffUpper", 25);
                        cloudLimits.BoxHTemperAlarm = GetIntValueFromReader(reader, "BoxHTemperAlarm", 40);
                        cloudLimits.BoxLTemperAlarm = GetIntValueFromReader(reader, "BoxLTemperAlarm", 0);
                        cloudLimits.SignalDelayAlarm = GetIntValueFromReader(reader, "SignalDelayAlarm", 80);
                        cloudLimits.SignalDelayCount = GetIntValueFromReader(reader, "SignalDelayCount", 10);
                        cloudLimits.CellV_Gap = GetIntValueFromReader(reader, "CellV_Gap", 30);
                        cloudLimits.OpenBala = GetIntValueFromReader(reader, "OpenBala", 0);
                        cloudLimits.OpenWarning = GetIntValueFromReader(reader, "OpenWarning", 1);

                        return true;
                    }
                }

                log.Warn($"未找到 CloudLimits 配置，ID: {CloudLimitsId}");
                return false;
            }
            catch (Exception ex)
            {
                // 记录完整堆栈，不仅仅是 Message
                log.Error($"LoadCloudLimitsFromMySQL 失败: {ex.Message}", ex);
                return false;
            }
        }
        #endregion


        /*        public static bool LoadCloudLimitsFromMySQL()
                {
                    string astrSQL = "SELECT MaxGridKW, MinGridKW, MaxSOC, MinSOC,  WarnMaxGridKW, WarnMinGridKW, PcsKva, Pre_Client_PUMdemand_Max, EnableActiveReduce, PumScale, AllUkvaWindowSize, PumTime, "
                        + "BmsDerateRatio, FrigOpenLower, FrigOffLower, FrigOffUpper, BoxHTemperAlarm, BoxLTemperAlarm, SignalDelayAlarm, SignalDelayCount, CellV_Gap, OpenBala FROM CloudLimits WHERE id = " + CloudLimitsId + ";";

                    try
                    {
                        using (MySqlConnection connection = new MySqlConnection(DBConnection.connectionStr))
                        {
                            connection.Open();
                            using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                            {
                                using (MySqlDataReader rd = sqlCmd.ExecuteReader())
                                {
                                    if (rd != null && rd.HasRows && rd.Read())
                                    {
                                        cloudLimits.MaxGridKW = rd.IsDBNull(0) ? 0 : rd.GetInt32(0);
                                        cloudLimits.MinGridKW = rd.IsDBNull(1) ? 0 : rd.GetInt32(1);
                                        cloudLimits.MaxSOC = rd.IsDBNull(2) ? 100 : rd.GetInt32(2);
                                        cloudLimits.MinSOC = rd.IsDBNull(3) ? 0 : rd.GetInt32(3);
                                        cloudLimits.WarnMaxGridKW = rd.IsDBNull(4) ? 0 : rd.GetInt32(4);
                                        cloudLimits.WarnMinGridKW = rd.IsDBNull(5) ? 0 : rd.GetInt32(5);
                                        cloudLimits.PcsKva = rd.IsDBNull(6) ? 10 : rd.GetInt32(6);
                                        cloudLimits.Pre_Client_PUMdemand_Max = rd.IsDBNull(7) ? 0 : rd.GetInt32(7);
                                        cloudLimits.EnableActiveReduce = rd.IsDBNull(8) ? 0 : rd.GetInt32(8);
                                        cloudLimits.PumScale = rd.IsDBNull(9) ? 0 : rd.GetInt32(9);
                                        cloudLimits.AllUkvaWindowSize = rd.IsDBNull(10) ? 4 : rd.GetInt32(10);
                                        cloudLimits.PumTime = rd.IsDBNull(11) ? 5 : rd.GetInt32(11);
                                        cloudLimits.BmsDerateRatio = rd.IsDBNull(12) ? 50 : rd.GetInt32(12);
                                        cloudLimits.FrigOpenLower = rd.IsDBNull(13) ? 30 : rd.GetInt32(13);
                                        cloudLimits.FrigOffLower = rd.IsDBNull(14) ? 10 : rd.GetInt32(14);
                                        cloudLimits.FrigOffUpper = rd.IsDBNull(15) ? 25 : rd.GetInt32(15);
                                        cloudLimits.BoxHTemperAlarm =  rd.IsDBNull(16) ? 40 : rd.GetInt32(16);
                                        cloudLimits.BoxLTemperAlarm = rd.IsDBNull(17) ? 0 : rd.GetInt32(17);
                                        cloudLimits.SignalDelayAlarm = rd.IsDBNull(18) ? 80 : rd.GetInt32(18);
                                        cloudLimits.SignalDelayCount = rd.IsDBNull(19) ? 10 : rd.GetInt32(19);
                                        cloudLimits.CellV_Gap = rd.IsDBNull(20) ? 30 : rd.GetInt32(20);
                                        cloudLimits.OpenBala = rd.IsDBNull(21) ? 0 : rd.GetInt32(21);

                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    catch (MySqlException ex)
                    {
                        log.Error(ex.Message);
                        return false;
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message);
                        return false;
                    }

                    log.Error("CloudLimits加载失败");
                    return false;
                }*/


        public static bool Set_Cloudlimits_OnlyChange()
        {
            // 只更新被修改过的字段
            var modifiedFields = frmSet.cloudLimits.ModifiedFields;
            if (modifiedFields.Count == 0)
            {
                return true;
            }

            // 构建只包含修改字段的SQL语句和参数
            List<string> updateClauses = new List<string>();
            var parameters = new Dictionary<string, object>();

            foreach (var field in modifiedFields)
            {
                switch (field)
                {
                    case "MaxGridKW":
                        updateClauses.Add("MaxGridKW = @MaxGridKW");
                        parameters.Add("@MaxGridKW", frmSet.cloudLimits.MaxGridKW);
                        break;
                    case "MinGridKW":
                        updateClauses.Add("MinGridKW = @MinGridKW");
                        parameters.Add("@MinGridKW", frmSet.cloudLimits.MinGridKW);
                        break;
                    case "MaxSOC":
                        updateClauses.Add("MaxSOC = @MaxSOC");
                        parameters.Add("@MaxSOC", frmSet.cloudLimits.MaxSOC);
                        break;
                    case "MinSOC":
                        updateClauses.Add("MinSOC = @MinSOC");
                        parameters.Add("@MinSOC", frmSet.cloudLimits.MinSOC);
                        break;
                    case "WarnMaxGridKW":
                        updateClauses.Add("WarnMaxGridKW = @WarnMaxGridKW");
                        parameters.Add("@WarnMaxGridKW", frmSet.cloudLimits.WarnMaxGridKW);
                        break;
                    case "WarnMinGridKW":
                        updateClauses.Add("WarnMinGridKW = @WarnMinGridKW");
                        parameters.Add("@WarnMinGridKW", frmSet.cloudLimits.WarnMinGridKW);
                        break;
                    case "PcsKva":
                        updateClauses.Add("PcsKva = @PcsKva");
                        parameters.Add("@PcsKva", frmSet.cloudLimits.PcsKva);
                        break;
                    case "Pre_Client_PUMdemand_Max":
                        updateClauses.Add("Pre_Client_PUMdemand_Max = @Pre_Client_PUMdemand_Max");
                        parameters.Add("@Pre_Client_PUMdemand_Max", frmSet.cloudLimits.Pre_Client_PUMdemand_Max);
                        break;
                    case "EnableActiveReduce":
                        updateClauses.Add("EnableActiveReduce = @EnableActiveReduce");
                        parameters.Add("@EnableActiveReduce", frmSet.cloudLimits.EnableActiveReduce);
                        break;
                    case "PumScale":
                        updateClauses.Add("PumScale = @PumScale");
                        parameters.Add("@PumScale", frmSet.cloudLimits.PumScale);
                        break;
                    case "AllUkvaWindowSize":
                        updateClauses.Add("AllUkvaWindowSize = @AllUkvaWindowSize");
                        parameters.Add("@AllUkvaWindowSize", frmSet.cloudLimits.AllUkvaWindowSize);
                        break;
                    case "BmsDerateRatio":
                        updateClauses.Add("BmsDerateRatio = @BmsDerateRatio");
                        parameters.Add("@BmsDerateRatio", frmSet.cloudLimits.BmsDerateRatio);
                        break;
                    case "FrigOpenLower":
                        updateClauses.Add("FrigOpenLower = @FrigOpenLower");
                        parameters.Add("@FrigOpenLower", frmSet.cloudLimits.FrigOpenLower);
                        break;
                    case "FrigOffLower":
                        updateClauses.Add("FrigOffLower = @FrigOffLower");
                        parameters.Add("@FrigOffLower", frmSet.cloudLimits.FrigOffLower);
                        break;
                    case "FrigOffUpper":
                        updateClauses.Add("FrigOffUpper = @FrigOffUpper");
                        parameters.Add("@FrigOffUpper", frmSet.cloudLimits.FrigOffUpper);
                        break;
                    case "CellV_Gap":
                        updateClauses.Add("CellV_Gap = @CellV_Gap");
                        parameters.Add("@CellV_Gap", frmSet.cloudLimits.CellV_Gap);
                        break;
                    case "OpenWarning":
                        updateClauses.Add("OpenWarning = @OpenWarning");
                        parameters.Add("@OpenWarning", frmSet.cloudLimits.OpenWarning);
                        break;
                }
            }

            parameters.Add("@id", CloudLimitsId);
            string sql = $"UPDATE cloudlimits SET {string.Join(", ", updateClauses)} WHERE id = @id";

            bool result = false;

            try
            {
                result = DBConnection.ExecSQLWithParams(sql, parameters) >= 0;
            }
            catch (Exception ex)
            {
                result = false;
                log.Error(ex.Message);
            }
            return result;
        }

        public static bool Set_Cloudlimits()
        {
            string sql = "UPDATE cloudlimits SET MaxGridKW = @MaxGridKW, MinGridKW = @MinGridKW, MaxSOC = @MaxSOC, MinSOC = @MinSOC, "
                + "WarnMaxGridKW = @WarnMaxGridKW, WarnMinGridKW = @WarnMinGridKW, PcsKva = @PcsKva, "
                + "Pre_Client_PUMdemand_Max = @Pre_Client_PUMdemand_Max, EnableActiveReduce = @EnableActiveReduce, "
                + "PumScale = @PumScale, AllUkvaWindowSize = @AllUkvaWindowSize, PumTime = @PumTime, "
                + "BmsDerateRatio = @BmsDerateRatio, FrigOpenLower = @FrigOpenLower, FrigOffLower = @FrigOffLower, "
                + "FrigOffUpper = @FrigOffUpper, BoxHTemperAlarm = @BoxHTemperAlarm, BoxLTemperAlarm = @BoxLTemperAlarm, "
                + "SignalDelayAlarm = @SignalDelayAlarm, SignalDelayCount = @SignalDelayCount, "
                + "CellV_Gap = @CellV_Gap, OpenBala = @OpenBala, OpenWarning = @OpenWarning WHERE id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@MaxGridKW", frmSet.cloudLimits.MaxGridKW },
                { "@MinGridKW", frmSet.cloudLimits.MinGridKW },
                { "@MaxSOC", frmSet.cloudLimits.MaxSOC },
                { "@MinSOC", frmSet.cloudLimits.MinSOC },
                { "@WarnMaxGridKW", frmSet.cloudLimits.WarnMaxGridKW },
                { "@WarnMinGridKW", frmSet.cloudLimits.WarnMinGridKW },
                { "@PcsKva", frmSet.cloudLimits.PcsKva },
                { "@Pre_Client_PUMdemand_Max", frmSet.cloudLimits.Pre_Client_PUMdemand_Max },
                { "@EnableActiveReduce", frmSet.cloudLimits.EnableActiveReduce },
                { "@PumScale", frmSet.cloudLimits.PumScale },
                { "@AllUkvaWindowSize", frmSet.cloudLimits.AllUkvaWindowSize },
                { "@PumTime", frmSet.cloudLimits.PumTime },
                { "@BmsDerateRatio", frmSet.cloudLimits.BmsDerateRatio },
                { "@FrigOpenLower", frmSet.cloudLimits.FrigOpenLower },
                { "@FrigOffLower", frmSet.cloudLimits.FrigOffLower },
                { "@FrigOffUpper", frmSet.cloudLimits.FrigOffUpper },
                { "@BoxHTemperAlarm", frmSet.cloudLimits.BoxHTemperAlarm },
                { "@BoxLTemperAlarm", frmSet.cloudLimits.BoxLTemperAlarm },
                { "@SignalDelayAlarm", frmSet.cloudLimits.SignalDelayAlarm },
                { "@SignalDelayCount", frmSet.cloudLimits.SignalDelayCount },
                { "@CellV_Gap", frmSet.cloudLimits.CellV_Gap },
                { "@OpenBala", frmSet.cloudLimits.OpenBala },
                { "@OpenWarning", frmSet.cloudLimits.OpenWarning },
                { "@id", CloudLimitsId }
            };

            bool result = false;

            try
            {
                result = DBConnection.ExecSQLWithParams(sql, parameters) >= 0;
            }
            catch (Exception ex)
            {
                // 处理异常情况
                result = false;
                log.Error(ex.Message);
            }
            return result;
        }

        /*********************************************
        *
        *          config
        *
        ********************************************/

        #region 加载Config
        public static bool LoadConfigFromMySQL()
        {
            string sql = @"SELECT SysID, Open104, NetTick, SysName, SysPower, SysSelfPower, SysAddr, SysInstTime,
                          CellCount, SysInterval, YunInterval, IsMaster, Master485Addr, i485Addr,
                          AutoRun, SysMode, PCSGridModel, DebugComName, DebugRate, SysCount,
                          UseYunTactics, UseBalaTactics, iPCSfactory, BMSVerb, PCSForceRun, EMSstatus,
                          GPIOSelect, MasterIp, ConnectStatus, CellVNum, CellTNum, BMStype, PcsLimit,
                          MqttBrokerIp, MqttBrokerPort, MqttBrokerUser, MqttBrokerPassword
                   FROM config WHERE SysID = @sysId";

            try
            {
                var parameters = new Dictionary<string, object> { { "@sysId", ConfigId } };

                using (var reader = DBConnection.QueryDataReader(sql, parameters))
                {
                    if (reader != null && reader.Read())
                    {
                        config.SysID = GetStringValueFromReader(reader, "SysID", "j00000000000001F");
                        config.SysName = GetStringValueFromReader(reader, "SysName", "浙江驰库");
                        config.SysAddr = GetStringValueFromReader(reader, "SysAddr", "浙江");
                        config.SysInstTime = GetStringValueFromReader(reader, "SysInstTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        config.DebugComName = GetStringValueFromReader(reader, "DebugComName", "Com7");
                        config.MasterIp = GetStringValueFromReader(reader, "MasterIp", "192.168.186.9");
                        config.ConnectStatus = GetStringValueFromReader(reader, "ConnectStatus", "485");
                        config.MqttBrokerIp = GetStringValueFromReader(reader, "MqttBrokerIp", "mqtt.eaiot.cloud");
                        config.MqttBrokerUser = GetStringValueFromReader(reader, "MqttBrokerUser", "aiot");
                        config.MqttBrokerPassword = GetStringValueFromReader(reader, "MqttBrokerPassword", "Lab123123123");
                        config.Open104 = GetIntValueFromReader(reader, "Open104", 0);
                        config.NetTick = GetIntValueFromReader(reader, "NetTick", 10);
                        config.SysPower = GetIntValueFromReader(reader, "SysPower", 0);
                        config.SysSelfPower = GetIntValueFromReader(reader, "SysSelfPower", 0);
                        config.CellCount = GetIntValueFromReader(reader, "CellCount", 240);
                        config.SysInterval = GetIntValueFromReader(reader, "SysInterval", 0);
                        config.YunInterval = GetIntValueFromReader(reader, "YunInterval", 0);
                        config.IsMaster = GetIntValueFromReader(reader, "IsMaster", 1);
                        config.Master485Addr = GetIntValueFromReader(reader, "Master485Addr", 1);
                        config.i485Addr = GetIntValueFromReader(reader, "i485Addr", 1);
                        config.AutoRun = GetIntValueFromReader(reader, "AutoRun", 1);
                        config.SysMode = GetIntValueFromReader(reader, "SysMode", 0);
                        config.PCSGridModel = GetIntValueFromReader(reader, "PCSGridModel", 0);
                        config.DebugRate = GetIntValueFromReader(reader, "DebugRate", 38400);
                        config.SysCount = GetIntValueFromReader(reader, "SysCount", 1);
                        config.UseYunTactics = GetIntValueFromReader(reader, "UseYunTactics", 0);
                        config.UseBalaTactics = GetIntValueFromReader(reader, "UseBalaTactics", 0);
                        config.iPCSfactory = GetIntValueFromReader(reader, "iPCSfactory", 1);
                        config.BMSVerb = GetIntValueFromReader(reader, "BMSVerb", 0);
                        config.PCSForceRun = GetIntValueFromReader(reader, "PCSForceRun", 0);
                        config.EMSstatus = GetIntValueFromReader(reader, "EMSstatus", 0);
                        config.GPIOSelect = GetIntValueFromReader(reader, "GPIOSelect", 0);
                        config.CellVNum = GetIntValueFromReader(reader, "CellVNum", 240);
                        config.CellTNum = GetIntValueFromReader(reader, "CellTNum", 168);
                        config.BMStype = GetIntValueFromReader(reader, "BMStype", 1);
                        config.PcsLimit = GetIntValueFromReader(reader, "PcsLimit", 110);
                        config.MqttBrokerPort = GetIntValueFromReader(reader, "MqttBrokerPort", 8883);

                        return true;
                    }
                }

                log.Warn($"未找到 Config 配置，SysID: {ConfigId}");
                return false;
            }
            catch (Exception ex)
            {
                log.Error($"LoadConfigFromMySQL 失败: {ex.Message}", ex);
                return false;
            }
        }
        #endregion

        /*        public static bool LoadConfigFromMySQL()
                {
                    string astrSQL = "SELECT SysID, Open104, NetTick, SysName, SysPower, SysSelfPower, SysAddr, SysInstTime,"
                                        + "CellCount, SysInterval, YunInterval, IsMaster, Master485Addr, i485Addr,"
                                        + "AutoRun, SysMode, PCSGridModel, DebugComName,"
                                        + "DebugRate, SysCount, UseYunTactics, UseBalaTactics, iPCSfactory, BMSVerb, PCSForceRun, "
                                        + "EMSstatus, GPIOSelect, MasterIp, ConnectStatus, CellVNum, CellTNum, BMStype, PcsLimit, MqttBrokerIp,"
                                        + "MqttBrokerPort, MqttBrokerUser, MqttBrokerPassword FROM config WHERE SysID = '" + ConfigId + "'; ";
                    try
                    {

                        using (MySqlConnection connection = new MySqlConnection(DBConnection.connectionStr))
                        {
                            connection.Open();
                            using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                            {
                                using (MySqlDataReader rd = sqlCmd.ExecuteReader())
                                {
                                    if (rd != null && rd.HasRows && rd.Read())
                                    {
                                        string sysIDRaw = rd.IsDBNull(0) ? "j00000000000001F" : rd.GetString(0);
                                        config.SysID = string.IsNullOrWhiteSpace(sysIDRaw)
                                            ? "j00000000000001F"
                                            : sysIDRaw;

                                        config.Open104 = rd.IsDBNull(1) ? 0 : rd.GetInt32(1);
                                        config.NetTick = rd.IsDBNull(2) ? 10 : rd.GetInt32(2);

                                        string sysNameRaw = rd.IsDBNull(3) ? "浙江驰库" : rd.GetString(3);
                                        config.SysName = string.IsNullOrWhiteSpace(sysNameRaw)
                                            ? "浙江驰库"
                                            : sysNameRaw;

                                        config.SysPower = rd.IsDBNull(4) ? 0 : rd.GetInt32(4);
                                        config.SysSelfPower = rd.IsDBNull(5) ? 0 : rd.GetInt32(5);

                                        string sysAddrRaw = rd.IsDBNull(6) ? "浙江" : rd.GetString(6);
                                        config.SysAddr = string.IsNullOrWhiteSpace(sysAddrRaw)
                                            ? "浙江"
                                            : sysAddrRaw;

                                        config.SysInstTime = rd.IsDBNull(7) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : rd.GetString(7);
                                        config.CellCount = rd.IsDBNull(8) ? 240 : rd.GetInt32(8);
                                        config.SysInterval = rd.IsDBNull(9) ? 0 : rd.GetInt32(9);
                                        config.YunInterval = rd.IsDBNull(10) ? 0 : rd.GetInt32(10);
                                        config.IsMaster = rd.IsDBNull(11) ? 1 : rd.GetInt32(11);
                                        config.Master485Addr = rd.IsDBNull(12) ? 1 : rd.GetInt32(12);
                                        config.i485Addr = rd.IsDBNull(13) ? 1 : rd.GetInt32(13);
                                        config.AutoRun = rd.IsDBNull(14) ? 1 : rd.GetInt32(14);
                                        config.SysMode = rd.IsDBNull(15) ? 0 : rd.GetInt32(15);
                                        config.PCSGridModel = rd.IsDBNull(16) ? 0 : rd.GetInt32(16);

                                        string debugComNameRaw = rd.IsDBNull(17) ? "Com7" : rd.GetString(17);
                                        config.DebugComName = string.IsNullOrWhiteSpace(debugComNameRaw)
                                            ? "Com7"
                                            : debugComNameRaw;

                                        config.DebugRate = rd.IsDBNull(18) ? 38400 : rd.GetInt32(18);
                                        config.SysCount = rd.IsDBNull(19) ? 1 : rd.GetInt32(19);
                                        config.UseYunTactics = rd.IsDBNull(20) ? 0 : rd.GetInt32(20);
                                        config.UseBalaTactics = rd.IsDBNull(21) ? 0 : rd.GetInt32(21);
                                        config.iPCSfactory = rd.IsDBNull(22) ? 1 : rd.GetInt32(22);
                                        config.BMSVerb = rd.IsDBNull(23) ? 0 : rd.GetInt32(23);
                                        config.PCSForceRun = rd.IsDBNull(24) ? 0 : rd.GetInt32(24);
                                        config.EMSstatus = rd.IsDBNull(25) ? 0 : rd.GetInt32(25);
                                        config.GPIOSelect = rd.IsDBNull(26) ? 0 : rd.GetInt32(26);

                                        string masterIpRaw = rd.IsDBNull(27) ? "192.168.186.9" : rd.GetString(27);
                                        config.MasterIp = string.IsNullOrWhiteSpace(masterIpRaw)
                                            ? "192.168.186.9"
                                            : masterIpRaw;

                                        string connectStatusRaw = rd.IsDBNull(28) ? "485" : rd.GetString(28);
                                        config.ConnectStatus = string.IsNullOrWhiteSpace(connectStatusRaw)
                                            ? "485"
                                            : connectStatusRaw;

                                        config.CellVNum = rd.IsDBNull(29) ? 240 : rd.GetInt32(29);
                                        config.CellTNum = rd.IsDBNull(30) ? 168 : rd.GetInt32(30);
                                        config.BMStype = rd.IsDBNull(31) ? 1 : rd.GetInt32(31);
                                        config.PcsLimit = rd.IsDBNull(32) ? 110 : rd.GetInt32(32);

                                        string brokerIpRaw = rd.IsDBNull(33) ? "mqtt.eaiot.cloud" : rd.GetString(33);
                                        config.MqttBrokerIp = string.IsNullOrWhiteSpace(brokerIpRaw)
                                            ? "mqtt.eaiot.cloud"
                                            : brokerIpRaw;

                                        config.MqttBrokerPort = rd.IsDBNull(34) ? 8883 : rd.GetInt32(34);

                                        string brokerUserRaw = rd.IsDBNull(35) ? "aiot" : rd.GetString(35);
                                        config.MqttBrokerUser = string.IsNullOrWhiteSpace(brokerUserRaw)
                                            ? "aiot"
                                            : brokerUserRaw;

                                        string brokerPasswordRaw = rd.IsDBNull(36) ? "Lab123123123" : rd.GetString(36);
                                        config.MqttBrokerPassword = string.IsNullOrWhiteSpace(brokerPasswordRaw)
                                            ? "Lab123123123"
                                            : brokerPasswordRaw;

                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    catch (MySqlException ex)
                    {
                        log.Error(ex.Message);
                        return false;
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message);
                        return false;
                    }

                    log.Error("config加载失败");
                    return false;
                }*/

        public static bool Set_Config()
        {
            string sql = "UPDATE config SET SysID = @SysID, Open104 = @Open104, NetTick = @NetTick, SysName = @SysName, "
                        + "SysPower = @SysPower, SysSelfPower = @SysSelfPower, SysAddr = @SysAddr, SysInstTime = @SysInstTime, "
                        + "CellCount = @CellCount, SysInterval = @SysInterval, YunInterval = @YunInterval, "
                        + "IsMaster = @IsMaster, Master485Addr = @Master485Addr, i485Addr = @i485Addr, "
                        + "AutoRun = @AutoRun, SysMode = @SysMode, PCSGridModel = @PCSGridModel, "
                        + "DebugComName = @DebugComName, DebugRate = @DebugRate, SysCount = @SysCount, "
                        + "iPCSfactory = @iPCSfactory, BMSVerb = @BMSVerb, PCSForceRun = @PCSForceRun, "
                        + "GPIOSelect = @GPIOSelect, MasterIp = @MasterIp, ConnectStatus = @ConnectStatus, "
                        + "EMSstatus = @EMSstatus, UseYunTactics = @UseYunTactics, UseBalaTactics = @UseBalaTactics, "
                        + "CellVNum = @CellVNum, CellTNum = @CellTNum, BMStype = @BMStype, PcsLimit = @PcsLimit "
                        + "WHERE SysID = @ConfigId";

            var parameters = new Dictionary<string, object>
            {
                { "@SysID", frmSet.config.SysID },
                { "@Open104", frmSet.config.Open104 },
                { "@NetTick", frmSet.config.NetTick },
                { "@SysName", frmSet.config.SysName },
                { "@SysPower", frmSet.config.SysPower },
                { "@SysSelfPower", frmSet.config.SysSelfPower },
                { "@SysAddr", frmSet.config.SysAddr },
                { "@SysInstTime", frmSet.config.SysInstTime },
                { "@CellCount", frmSet.config.CellCount },
                { "@SysInterval", frmSet.config.SysInterval },
                { "@YunInterval", frmSet.config.YunInterval },
                { "@IsMaster", frmSet.config.IsMaster },
                { "@Master485Addr", frmSet.config.Master485Addr },
                { "@i485Addr", frmSet.config.i485Addr },
                { "@AutoRun", frmSet.config.AutoRun },
                { "@SysMode", frmSet.config.SysMode },
                { "@PCSGridModel", frmSet.config.PCSGridModel },
                { "@DebugComName", frmSet.config.DebugComName },
                { "@DebugRate", frmSet.config.DebugRate },
                { "@SysCount", frmSet.config.SysCount },
                { "@iPCSfactory", frmSet.config.iPCSfactory },
                { "@BMSVerb", frmSet.config.BMSVerb },
                { "@PCSForceRun", frmSet.config.PCSForceRun },
                { "@GPIOSelect", frmSet.config.GPIOSelect },
                { "@MasterIp", frmSet.config.MasterIp },
                { "@ConnectStatus", frmSet.config.ConnectStatus },
                { "@EMSstatus", frmSet.config.EMSstatus },
                { "@UseYunTactics", frmSet.config.UseYunTactics },
                { "@UseBalaTactics", frmSet.config.UseBalaTactics },
                { "@CellVNum", frmSet.config.CellVNum },
                { "@CellTNum", frmSet.config.CellTNum },
                { "@BMStype", frmSet.config.BMStype },
                { "@PcsLimit", frmSet.config.PcsLimit },
                { "@ConfigId", ConfigId }
            };

            bool result = false;

            try
            {
                result = DBConnection.ExecSQLWithParams(sql, parameters) >= 0;
            }
            catch (Exception ex)
            {
                // 处理异常情况
                log.Error(ex.Message);
                result = false;
            }
            return result;
        }


        /*********************************************
        *
        *          VariCharge
        *
        ********************************************/
        #region 加载VariCharge
        public static bool LoadVariChargeFromMySQL()
        {
            string sql = "SELECT UBmsPcsState, OBmsPcsState FROM VariCharge WHERE id = @id";

            try
            {
                var parameters = new Dictionary<string, object> { { "@id", VariChargeId } };
                using (var reader = DBConnection.QueryDataReader(sql, parameters))
                {
                    if (reader != null && reader.Read())
                    {
                        variCharge.UBmsPcsState = GetIntValueFromReader(reader, "UBmsPcsState", 50);
                        variCharge.OBmsPcsState = GetIntValueFromReader(reader, "OBmsPcsState", 50);
                        return true;
                    }
                }

                log.Warn($"未找到 VariCharge 配置，ID: {VariChargeId}");
                return false;
            }
            catch (Exception ex)
            {
                log.Error($"LoadVariChargeFromMySQL 失败: {ex.Message}", ex);
                return false;
            }
        }
        #endregion

        /*        public static bool LoadVariChargeFromMySQL()
                {
                    string astrSQL = "SELECT UBmsPcsState, OBmsPcsState FROM VariCharge WHERE id = " + VariChargeId + ";";

                    try
                    {
                        using (MySqlConnection connection = new MySqlConnection(DBConnection.connectionStr))
                        {
                            connection.Open();
                            using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                            {
                                using (MySqlDataReader rd = sqlCmd.ExecuteReader())
                                {
                                    if (rd != null && rd.HasRows && rd.Read())
                                    {
                                        variCharge.UBmsPcsState = rd.IsDBNull(0) ? 50 : rd.GetInt32(0);
                                        variCharge.OBmsPcsState = rd.IsDBNull(1) ? 50 : rd.GetInt32(1);
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    catch (MySqlException ex)
                    {
                        log.Error(ex.Message);
                        return false;
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message);
                        return false;
                    }

                    log.Error("VariCharge加载失败");
                    return false;
                }*/

        public static bool Set_VariCharge()
        {
            string sql = "UPDATE VariCharge SET UBmsPcsState = @UBmsPcsState, OBmsPcsState = @OBmsPcsState WHERE id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@UBmsPcsState", frmSet.variCharge.UBmsPcsState },
                { "@OBmsPcsState", frmSet.variCharge.OBmsPcsState },
                { "@id", VariChargeId }
            };

            bool result = false;

            try
            {
                result = DBConnection.ExecSQLWithParams(sql, parameters) >= 0;
            }
            catch (Exception ex)
            {
                // 处理异常情况
                log.Error(ex.Message);
                result = false;
            }
            return result;
        }

        /*********************************************
        *
        *          Component
        *
        ********************************************/

        #region 加载LoadComponentSettings
        public static bool LoadComponentSettingsFromMySQL()
        {
            string sql = @"
                SELECT SetHotTemp, SetCoolTemp, CoolTempReturn, HotTempReturn, SetHumidity, HumiReturn,
                       TCRunWithSys, TCAuto, TCMode, TCMaxTemp, TCMinTemp, TCMaxHumi, TCMinHumi,
                       FenMaxTemp, FenMinTemp, FenMode, LCModel, LCTemperSelect, LCWaterPump,
                       LCSetHotTemp, LCSetCoolTemp, LCHotTempReturn, LCCoolTempReturn , DHSetRunStatus, DHSetTempBoot, DHSetTempStop, DHSetHumidityBoot, DHSetHumidityStop
                FROM ComponentSettings WHERE id = @id";

            try
            {
                var parameters = new Dictionary<string, object> { { "@id", ComponentSettingsId } };
                using (var reader = DBConnection.QueryDataReader(sql, parameters))
                {
                    if (reader != null && reader.Read())
                    {
                        componentSettings.SetHotTemp = GetDoubleValueFromReader(reader, "SetHotTemp", 0);
                        componentSettings.SetCoolTemp = GetDoubleValueFromReader(reader, "SetCoolTemp", 280);
                        componentSettings.CoolTempReturn = GetDoubleValueFromReader(reader, "CoolTempReturn", 20);
                        componentSettings.HotTempReturn = GetDoubleValueFromReader(reader, "HotTempReturn", 20);
                        componentSettings.SetHumidity = GetDoubleValueFromReader(reader, "SetHumidity", 200);
                        componentSettings.HumiReturn = GetDoubleValueFromReader(reader, "HumiReturn", 100);
                        componentSettings.TCRunWithSys = GetIntValueFromReader(reader, "TCRunWithSys", 0);
                        componentSettings.TCAuto = GetIntValueFromReader(reader, "TCAuto", 0);
                        componentSettings.TCMode = GetIntValueFromReader(reader, "TCMode", 0);
                        componentSettings.TCMaxTemp = GetDoubleValueFromReader(reader, "TCMaxTemp", 450);
                        componentSettings.TCMinTemp = GetDoubleValueFromReader(reader, "TCMinTemp", 0);
                        componentSettings.TCMaxHumi = GetDoubleValueFromReader(reader, "TCMaxHumi", 950);
                        componentSettings.TCMinHumi = GetDoubleValueFromReader(reader, "TCMinHumi", 10);
                        componentSettings.FenMaxTemp = GetDoubleValueFromReader(reader, "FenMaxTemp", 200);
                        componentSettings.FenMinTemp = GetDoubleValueFromReader(reader, "FenMinTemp", 160);
                        componentSettings.FenMode = GetIntValueFromReader(reader, "FenMode", 1);
                        componentSettings.LCModel = GetIntValueFromReader(reader, "LCModel", 1);
                        componentSettings.LCTemperSelect = GetIntValueFromReader(reader, "LCTemperSelect", 1);
                        componentSettings.LCWaterPump = GetIntValueFromReader(reader, "LCWaterPump", 1);
                        componentSettings.LCSetHotTemp = GetDoubleValueFromReader(reader, "LCSetHotTemp", 50);
                        componentSettings.LCSetCoolTemp = GetDoubleValueFromReader(reader, "LCSetCoolTemp", 50);
                        componentSettings.LCHotTempReturn = GetDoubleValueFromReader(reader, "LCHotTempReturn", 10);
                        componentSettings.LCCoolTempReturn = GetDoubleValueFromReader(reader, "LCCoolTempReturn", 10);
                        componentSettings.DHSetRunStatus = GetIntValueFromReader(reader, "DHSetRunStatus", 1);
                        componentSettings.DHSetTempBoot = GetIntValueFromReader(reader, "DHSetTempBoot", 1);
                        componentSettings.DHSetTempStop = GetIntValueFromReader(reader, "DHSetTempStop", 1);
                        componentSettings.DHSetHumidityBoot = GetIntValueFromReader(reader, "DHSetHumidityBoot", 20);
                        componentSettings.DHSetHumidityStop = GetIntValueFromReader(reader, "DHSetHumidityStop", 20);

                        return true;
                    }
                }

                log.Warn($"未找到 ComponentSettings 配置，ID: {ComponentSettingsId}");
                return false;
            }
            catch (Exception ex)
            {
                log.Error($"LoadComponentSettingsFromMySQL 失败: {ex.Message}", ex);
                return false;
            }
        }
        #endregion


        /*        public static bool LoadComponentSettingsFromMySQL()
                {
                    string astrSQL = @"
                            SELECT SetHotTemp, SetCoolTemp, CoolTempReturn, HotTempReturn, SetHumidity, HumiReturn,
                                   TCRunWithSys, TCAuto, TCMode, TCMaxTemp, TCMinTemp, TCMaxHumi, TCMinHumi,
                                   FenMaxTemp, FenMinTemp, FenMode, LCModel, LCTemperSelect, LCWaterPump,
                                   LCSetHotTemp, LCSetCoolTemp, LCHotTempReturn, LCCoolTempReturn , DHSetRunStatus, DHSetTempBoot, DHSetTempStop, DHSetHumidityBoot, DHSetHumidityStop
                            FROM ComponentSettings WHERE id = " + ComponentSettingsId + ";";

                    try
                    {
                        using (MySqlConnection connection = new MySqlConnection(DBConnection.connectionStr))
                        {
                            connection.Open();
                            using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                            {
                                using (MySqlDataReader rd = sqlCmd.ExecuteReader())
                                {
                                    if (rd != null && rd.HasRows && rd.Read())
                                    {
                                        componentSettings.SetHotTemp = rd.IsDBNull(0) ? 1 : rd.GetDouble(0);
                                        componentSettings.SetCoolTemp = rd.IsDBNull(1) ? 1 : rd.GetDouble(1);
                                        componentSettings.CoolTempReturn = rd.IsDBNull(2) ? 1 : rd.GetDouble(2);
                                        componentSettings.HotTempReturn = rd.IsDBNull(3) ? 1 : rd.GetDouble(3);
                                        componentSettings.SetHumidity = rd.IsDBNull(4) ? 1 : rd.GetDouble(4);
                                        componentSettings.HumiReturn = rd.IsDBNull(5) ? 1 : rd.GetDouble(5);
                                        componentSettings.TCRunWithSys = rd.IsDBNull(6) ? 0 : rd.GetInt32(6);
                                        componentSettings.TCAuto = rd.IsDBNull(7) ? 0 : rd.GetInt32(7);
                                        componentSettings.TCMode = rd.IsDBNull(8) ? 1 : rd.GetInt32(8);
                                        componentSettings.TCMaxTemp = rd.IsDBNull(9) ? 1 : rd.GetDouble(9);
                                        componentSettings.TCMinTemp = rd.IsDBNull(10) ? 1 : rd.GetDouble(10);
                                        componentSettings.TCMaxHumi = rd.IsDBNull(11) ? 1 : rd.GetDouble(11);
                                        componentSettings.TCMinHumi = rd.IsDBNull(12) ? 1 : rd.GetDouble(12);
                                        componentSettings.FenMaxTemp = rd.IsDBNull(13) ? 1 : rd.GetDouble(13);
                                        componentSettings.FenMinTemp = rd.IsDBNull(14) ? 1 : rd.GetDouble(14);
                                        componentSettings.FenMode = rd.IsDBNull(15) ? 1 : rd.GetInt32(15);
                                        componentSettings.LCModel = rd.IsDBNull(16) ? 1 : rd.GetInt32(16);
                                        componentSettings.LCTemperSelect = rd.IsDBNull(17) ? 1 : rd.GetInt32(17);
                                        componentSettings.LCWaterPump = rd.IsDBNull(18) ? 1 : rd.GetInt32(18);
                                        componentSettings.LCSetHotTemp = rd.IsDBNull(19) ? 1 : rd.GetDouble(19);
                                        componentSettings.LCSetCoolTemp = rd.IsDBNull(20) ? 1 : rd.GetDouble(20);
                                        componentSettings.LCHotTempReturn = rd.IsDBNull(21) ? 1 : rd.GetDouble(21);
                                        componentSettings.LCCoolTempReturn = rd.IsDBNull(22) ? 1 : rd.GetDouble(22);
                                        componentSettings.DHSetRunStatus = rd.IsDBNull(23) ? 1 : rd.GetInt32(23);
                                        componentSettings.DHSetTempBoot = rd.IsDBNull(24) ? 1 : rd.GetInt32(24);
                                        componentSettings.DHSetTempStop = rd.IsDBNull(25) ? 1 : rd.GetInt32(25);
                                        componentSettings.DHSetHumidityBoot = rd.IsDBNull(26) ? 1 : rd.GetInt32(26);
                                        componentSettings.DHSetHumidityStop = rd.IsDBNull(27) ? 1 : rd.GetInt32(27);

                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    catch (MySqlException ex)
                    {
                        log.Error(ex.Message);
                        return false;
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message);
                        return false;
                    }

                    log.Error("Component加载失败");
                    return false;
                }*/


        public static bool Set_ComponentSettings()
        {
            string sql = "UPDATE ComponentSettings SET SetHotTemp = @SetHotTemp, SetCoolTemp = @SetCoolTemp, "
                + "CoolTempReturn = @CoolTempReturn, HotTempReturn = @HotTempReturn, "
                + "SetHumidity = @SetHumidity, HumiReturn = @HumiReturn, "
                + "TCRunWithSys = @TCRunWithSys, TCAuto = @TCAuto, TCMode = @TCMode, "
                + "TCMaxTemp = @TCMaxTemp, TCMinTemp = @TCMinTemp, TCMaxHumi = @TCMaxHumi, TCMinHumi = @TCMinHumi, "
                + "FenMaxTemp = @FenMaxTemp, FenMinTemp = @FenMinTemp, FenMode = @FenMode, "
                + "LCModel = @LCModel, LCTemperSelect = @LCTemperSelect, LCWaterPump = @LCWaterPump, "
                + "LCSetHotTemp = @LCSetHotTemp, LCSetCoolTemp = @LCSetCoolTemp, "
                + "LCHotTempReturn = @LCHotTempReturn, LCCoolTempReturn = @LCCoolTempReturn, "
                + "DHSetRunStatus = @DHSetRunStatus, DHSetTempBoot = @DHSetTempBoot, DHSetTempStop = @DHSetTempStop, "
                + "DHSetHumidityBoot = @DHSetHumidityBoot, DHSetHumidityStop = @DHSetHumidityStop WHERE id = @id";

            var parameters = new Dictionary<string, object>
            {
                { "@SetHotTemp", componentSettings.SetHotTemp },
                { "@SetCoolTemp", componentSettings.SetCoolTemp },
                { "@CoolTempReturn", componentSettings.CoolTempReturn },
                { "@HotTempReturn", componentSettings.HotTempReturn },
                { "@SetHumidity", componentSettings.SetHumidity },
                { "@HumiReturn", componentSettings.HumiReturn },
                { "@TCRunWithSys", componentSettings.TCRunWithSys },
                { "@TCAuto", componentSettings.TCAuto },
                { "@TCMode", componentSettings.TCMode },
                { "@TCMaxTemp", componentSettings.TCMaxTemp },
                { "@TCMinTemp", componentSettings.TCMinTemp },
                { "@TCMaxHumi", componentSettings.TCMaxHumi },
                { "@TCMinHumi", componentSettings.TCMinHumi },
                { "@FenMaxTemp", componentSettings.FenMaxTemp },
                { "@FenMinTemp", componentSettings.FenMinTemp },
                { "@FenMode", componentSettings.FenMode },
                { "@LCModel", componentSettings.LCModel },
                { "@LCTemperSelect", componentSettings.LCTemperSelect },
                { "@LCWaterPump", componentSettings.LCWaterPump },
                { "@LCSetHotTemp", componentSettings.LCSetHotTemp },
                { "@LCSetCoolTemp", componentSettings.LCSetCoolTemp },
                { "@LCHotTempReturn", componentSettings.LCHotTempReturn },
                { "@LCCoolTempReturn", componentSettings.LCCoolTempReturn },
                { "@DHSetRunStatus", componentSettings.DHSetRunStatus },
                { "@DHSetTempBoot", componentSettings.DHSetTempBoot },
                { "@DHSetTempStop", componentSettings.DHSetTempStop },
                { "@DHSetHumidityBoot", componentSettings.DHSetHumidityBoot },
                { "@DHSetHumidityStop", componentSettings.DHSetHumidityStop },
                { "@id", ComponentSettingsId }
            };

            bool result = false;
            try
            {
                result = DBConnection.ExecSQLWithParams(sql, parameters) >= 0;
            }
            catch (Exception ex)
            {
                // 处理异常情况
                log.Error(ex.Message);
                result = false;
            }
            return result;
        }

        /**************************************************************************************************************************************/


        public List<ModbusCommand> VersionList = new List<ModbusCommand>(); //从由协议转义的TXT文本获取command的相关信息，如寄存器地址，功能码，字节大小等

        /*Fix:地址待可以归一*/
        public static UInt32[] GPOIAddr ={
            0xFED0E178,//消防
            0xFED0E278,//急停
            0xFED0E1C8,
            0xFED0E1B8,
            0xFED0E168,
            0xFED0E158,//UPS反馈：3:正常 2：故障
            0xFED0E188,//市电 ： 3：正常 2：故障
            0xFED0E198,
            //
            0xFED0E388,
            0xFED0E368,
            0xFED0E318,
            0xFED0E378,//蜂鸣器故障灯
            0xFED0E308,
            0xFED0E398,
            0xFED0E328,
            0xFED0E3A8,
        };

        public static UInt32[] GPOIAddr2 ={
            0xFED0E178,//0 消防
            0xFED0E278,//1 急停
            0xFED0E1C8,//2 门禁
            0xFED0E1B8,//3 消防反馈2
            0xFED0E168,//4 市电反馈
            0xFED0E158,//5 UPS反馈：3:正常 2：故障
            0xFED0E188,//6
            0xFED0E198,//7
            //
            0xFED0E388,// 8
            0xFED0E368,// 9
            0xFED0E318,//10
            0xFED0E378,//11 蜂鸣器故障灯
            0xFED0E308,//12 KA5-主动消防
            0xFED0E398,//13 KA6-泄爆阀
            0xFED0E328,//14 EMS电源指示
            0xFED0E3A8,//15
        };



        [DllImport(strExeDllName)] //uint IntPtr
        public static extern bool SetPhysLong(IntPtr hDriver, UInt32 pbPhysAddr, UInt32 dwPhysVal);
        [DllImport(strExeDllName)]
        public static extern bool GetPhysLong(IntPtr hDriver, UInt32 pbPhysAddr, out UInt32 pdwPhysVal);

        [DllImport(strDriveDllName)]
        public static extern IntPtr InitializeWinIo();
        [DllImport(strDriveDllName)]
        public static extern bool ShutdownWinIo(IntPtr hDriver);

        //GPIO初始化
        /// <summary>
        /// EMS启动时初始化一次GPIO驱动。驱动文件仍由SpesTechDriverControl.dll
        /// 按当前应用目录下的相对路径查找和注册，不在业务代码中拼接版本目录。
        /// </summary>
        public static bool InitializeGPIODriver()
        {
            lock (gpioDriverLock)
            {
                if (gpioDriverHandle != IntPtr.Zero)
                {
                    return true;
                }

                gpioDriverStopping = false;

                try
                {
                    gpioDriverHandle = InitializeWinIo();
                    if (gpioDriverHandle == IntPtr.Zero)
                    {
                        log.Error("InitializeGPIODriver失败：InitializeWinIo返回空句柄");
                        return false;
                    }

                    log.Info("GPIO驱动已初始化，运行期间将复用同一驱动句柄");
                    return true;
                }
                catch (Exception ex)
                {
                    gpioDriverHandle = IntPtr.Zero;
                    log.Error("InitializeGPIODriver异常: " + ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// EMS退出时统一关闭驱动句柄并停止驱动。可重复调用。
        /// </summary>
        public static void ShutdownGPIODriver()
        {
            lock (gpioDriverLock)
            {
                gpioDriverStopping = true;

                if (gpioDriverHandle == IntPtr.Zero)
                {
                    return;
                }

                IntPtr driverHandle = gpioDriverHandle;
                gpioDriverHandle = IntPtr.Zero;

                try
                {
                    if (!ShutdownWinIo(driverHandle))
                    {
                        log.Error("ShutdownGPIODriver失败：ShutdownWinIo返回false");
                    }
                    else
                    {
                        log.Info("GPIO驱动句柄已关闭，驱动已停止");
                    }
                }
                catch (Exception ex)
                {
                    log.Error("ShutdownGPIODriver异常: " + ex.Message);
                }
            }
        }

        public static bool InitGPIO()
        {
            try
            {
                if (!InitializeGPIODriver())
                {
                    return false;
                }

                switch (config.GPIOSelect)
                {
                    case 0://FA,FB 无RTC: 初始化：输入、输出 电平置高
                        if(!frmSet.Init0_GPIO()) return false;

                        break;
                    case 1://液冷 初始化：输入、输出 电平置低
                        if (!frmSet.Init1_GPIO()) return false;

                        break;
                    case 2://FB +RTC
                        if (!frmSet.Init2_GPIO()) return false;

                        break;
                }

                return true;
            }
            catch (Exception ex)
            {
                log.Error("InitGPIOL " + ex.Message);
                return false;
            }
        }
        public static bool Init0_GPIO()  //FA
        {
            /*            frmSet.SetGPIOState(0, 3);  //急停
                        frmSet.SetGPIOState(1, 3);  //消防
                        frmSet.SetGPIOState(2, 3);
                        frmSet.SetGPIOState(3, 3);
                        frmSet.SetGPIOState(4, 3);
                        frmSet.SetGPIOState(5, 3);
                        frmSet.SetGPIOState(6, 3);
                        frmSet.SetGPIOState(7, 3);*/
            //
            /*            frmSet.SetGPIOState(8, 0);   //24V on(powerOn)
                        frmSet.SetGPIOState(9, 1);   //PCS On
                        frmSet.SetGPIOState(10, 1);  //2 error
                        frmSet.SetGPIOState(11, 1); //3 error
                        frmSet.SetGPIOState(12, 1);
                        frmSet.SetGPIOState(13, 1);
                        frmSet.SetGPIOState(14, 1);
                        frmSet.SetGPIOState(15, 0);//EMS电源指示（特殊：初始化置低开启灯）*/

            if (!frmSet.SetGPIOState(8, 1)) return false;   //24V on(powerOn)
            if (!frmSet.SetGPIOState(9, 1)) return false;   //PCS On
            if (!frmSet.SetGPIOState(10, 1)) return false;  //2 error
            if (!frmSet.SetGPIOState(11, 1)) return false; //3 error
            if (!frmSet.SetGPIOState(12, 1)) return false;
            if (!frmSet.SetGPIOState(13, 1)) return false;
            if (!frmSet.SetGPIOState(14, 1)) return false;
            if (!frmSet.SetGPIOState(15, 0)) return false;//EMS电源指示（特殊：初始化置低开启灯）

            return true;
        }

        public static bool Init1_GPIO()   //液冷
        {
            /*Fix:冗余*/
            /*            frmSet.SetGPIOState(0, 2);//消防
                        frmSet.SetGPIOState(1, 2);//急停
                        frmSet.SetGPIOState(2, 2);//门禁
                        frmSet.SetGPIOState(3, 2);
                        frmSet.SetGPIOState(4, 2);
                        frmSet.SetGPIOState(5, 2);
                        frmSet.SetGPIOState(6, 2);
                        frmSet.SetGPIOState(7, 2);*/
            //
            /*            frmSet.SetGPIOState(8, 1);   //24V on(powerOn)
                        frmSet.SetGPIOState(9, 0);   //PCS On
                        frmSet.SetGPIOState(10, 0);  //2 error
                        frmSet.SetGPIOState(11, 0); //3 error
                        frmSet.SetGPIOState(12, 0);
                        frmSet.SetGPIOState(13, 0);
                        frmSet.SetGPIOState(14, 1);//EMS LED （特殊：初始化置高开启灯）
                        frmSet.SetGPIOState(15, 0);*/

            if (!frmSet.SetGPIOState(8, 1)) return false;   //24V on(powerOn)
            if (!frmSet.SetGPIOState(9, 0)) return false;   //PCS On
            if (!frmSet.SetGPIOState(10, 0)) return false;  //2 error
            if (!frmSet.SetGPIOState(11, 0)) return false; //3 error
            if (!frmSet.SetGPIOState(12, 0)) return false;
            if (!frmSet.SetGPIOState(13, 0)) return false;
            if (!frmSet.SetGPIOState(14, 1)) return false;//EMS电源指示（特殊：初始化置低开启灯）
            if (!frmSet.SetGPIOState(15, 0)) return false;

            return true;
        }

        public static bool Init2_GPIO()   //新风冷 FB
        {
            /*Fix:冗余*/
            /*            frmSet.SetGPIOState(0, 2);//消防
                        frmSet.SetGPIOState(1, 2);//急停
                        frmSet.SetGPIOState(2, 2);
                        frmSet.SetGPIOState(3, 2);
                        frmSet.SetGPIOState(4, 2);
                        frmSet.SetGPIOState(5, 2);
                        frmSet.SetGPIOState(6, 2);
                        frmSet.SetGPIOState(7, 2);*/
            //
            /*            frmSet.SetGPIOState(8, 1);   //24V on(powerOn)
                        frmSet.SetGPIOState(9, 0);   //PCS On
                        frmSet.SetGPIOState(10, 0);  //2 error
                        frmSet.SetGPIOState(11, 0); //3 error
                        frmSet.SetGPIOState(12, 0);
                        frmSet.SetGPIOState(13, 0);
                        frmSet.SetGPIOState(14, 0);
                        frmSet.SetGPIOState(15, 1);//EMS LED （特殊：初始化置高开启灯）*/

            if (!frmSet.SetGPIOState(8, 1)) return false;   //24V on(powerOn)
            if (!frmSet.SetGPIOState(9, 0)) return false;   //PCS On
            if (!frmSet.SetGPIOState(10, 0)) return false;  //2 error
            if (!frmSet.SetGPIOState(11, 0)) return false; //3 error
            if (!frmSet.SetGPIOState(12, 0)) return false;
            if (!frmSet.SetGPIOState(13, 0)) return false;
            if (!frmSet.SetGPIOState(14, 0)) return false;
            if (!frmSet.SetGPIOState(15, 1)) return false;//EMS电源指示（特殊：初始化置低开启灯）

            return true;
        }


        /// <summary>
        /// 获取一个GPIO的输入值 ：0输出低电平，1输出高高电平，2输入低电平，3输入高电平
        /// </summary>
        /// <param name="aIndex"></param>
        /// <returns></returns>
        public static UInt32 GetGPIOState(int aIndex)
        {
            UInt32 uiBack = 0;

            if (aIndex < 0 || aIndex >= GPOIAddr.Length)
            {
                log.Error("GetGPIOState失败：GPIO索引越界 " + aIndex);
                return 4;
            }

            lock (gpioDriverLock)
            {
                if (gpioDriverStopping || gpioDriverHandle == IntPtr.Zero)
                {
                    return 4;
                }

                try
                {
                    if (!GetPhysLong(gpioDriverHandle, GPOIAddr[aIndex], out uiBack))
                    {
                        log.Error("GetGPIOState失败：GetPhysLong返回false，GPIO索引 " + aIndex);
                        return 4;
                    }
                }
                catch (Exception ex)
                {
                    log.Error("GetGPIOState异常，GPIO索引 " + aIndex + ": " + ex.Message);
                    return 4;
                }
            }

            return uiBack;
        }

        /// <summary>
        /// 设置gpio的状态0输出低电平，1输出高高电平，2输入低电平，3输入高电平
        /// </summary>
        /// <param name="aIndex"></param>
        /// <param name="aOn"></param>
        /// <returns></returns>
        public static bool SetGPIOState(int aIndex, ushort aOn)
        {
            if (aIndex < 0 || aIndex >= GPOIAddr.Length)
            {
                log.Error("SetGPIOState失败：GPIO索引越界 " + aIndex);
                return false;
            }

            lock (gpioDriverLock)
            {
                if (gpioDriverStopping || gpioDriverHandle == IntPtr.Zero)
                {
                    return false;
                }

                try
                {
                    return SetPhysLong(gpioDriverHandle, GPOIAddr[aIndex], aOn);
                }
                catch (Exception ex)
                {
                    log.Error("SetGPIOState异常，GPIO索引 " + aIndex + ": " + ex.Message);
                    return false;
                }
            }
        }

        //监测触发BMS发生二级告警， 控制告警指示灯：（0：关闭 1：开启）
        public static void BMS2warningGPIO(int option)
        {
            if (option == 0)
                switch (config.GPIOSelect)
                {
                    case 0:
                        frmSet.SetGPIOState(10, 1);//FA 无RTC
                        break;
                    case 1:
                        // frmSet.SetGPIOState(10, 1);
                        break;
                    case 2:
                        frmSet.SetGPIOState(10, 0);//FB + RTC
                        break;
                }
            else
                switch (config.GPIOSelect)
                {
                    case 0:
                        frmSet.SetGPIOState(10, 0);
                        break;
                    case 1:
                        //frmSet.SetGPIOState(10, 0);
                        break;
                    case 2:
                        frmSet.SetGPIOState(10, 1);
                        break;
                }
        }

        //控制故障指示灯 ：（0：关闭 ， 1：开启）
        public static void ErrorGPIO(int option)
        {
            if (option == 0) {
                switch (config.GPIOSelect)
                {
                    case 0:
                        frmSet.SetGPIOState(11, 1);
                        break;
                    case 1:
                        // frmSet.SetGPIOState(11, 1);
                        break;
                    case 2:
                        frmSet.SetGPIOState(11, 0);
                        break;
                }
            }
            else if (option == 1 && frmSet.cloudLimits.OpenWarning == 1) {
                switch (config.GPIOSelect)
                {
                    case 0:
                        frmSet.SetGPIOState(11, 0);
                        break;
                    case 1:
                        // frmSet.SetGPIOState(11, 0);
                        break;
                    case 2:
                        frmSet.SetGPIOState(11, 1);
                        break;
                }
            }
        }

        //控制运行指示灯： （0：关闭 ， 1：开启）
        public static void RunStateGPIO(int option)
        {
            if (option == 0)
                switch (config.GPIOSelect)
                {
                    case 0:
                        frmSet.SetGPIOState(9, 1);
                        break;
                    case 1:
                        // frmSet.SetGPIOState(9, 1);
                        break;
                    case 2:
                        frmSet.SetGPIOState(9, 0);
                        break;
                }
            else
                switch (config.GPIOSelect)
                {
                    case 0:
                        frmSet.SetGPIOState(9, 0);
                        break;
                    case 1:
                        //frmSet.SetGPIOState(9, 0);
                        break;
                    case 2:
                        frmSet.SetGPIOState(9, 1);
                        break;
                }
        }

        //控制EMS电源指示灯
        public static void ePowerGPIO(int option)
        {
            if (option == 0)
                switch (config.GPIOSelect)
                {
                    case 0:
                        frmSet.SetGPIOState(15, 1);
                        break;
                    case 1:
                        frmSet.SetGPIOState(14, 0);
                        break;
                    case 2:
                        frmSet.SetGPIOState(15, 0);
                        break;
                }
            else
                switch (config.GPIOSelect)
                {
                    case 0:
                        frmSet.SetGPIOState(15, 0);
                        break;
                    case 1:
                        frmSet.SetGPIOState(14, 1);
                        break;
                    case 2:
                        frmSet.SetGPIOState(15, 1);
                        break;
                }
        }

        public static void PowerGPIO(int option)
        {
            if (option == 0)
                switch (config.GPIOSelect)
                {
                    case 0:
                        frmSet.SetGPIOState(8, 0);
                        break;
                    case 1:
                        frmSet.SetGPIOState(8, 0);
                        break;
                    case 2:
                        frmSet.SetGPIOState(8, 0);
                        break;
                }
            else
                switch (config.GPIOSelect)
                {
                    case 0:
                        frmSet.SetGPIOState(8, 1);
                        break;
                    case 1:
                        frmSet.SetGPIOState(8, 1);
                        break;
                    case 2:
                        frmSet.SetGPIOState(8, 1);
                        break;
                }
        }

        public bool PutTchCheck(int input)
        {
            bool result;
            if (input == 1)
            {
                result =  true;
            }
            else
            {
                result = false;
            }
            return result;
        }


        //显示设置文件
        public void ShowINIdata()
        {
            try
            {
                //tbSysName.Text = SysName;
                //tbSysID.Text = SysID;
                //tbSysAddr.Text = SysAddr;
                tneSysPower.SetIntValue(config.SysPower);
                tneSysSelfPower.SetIntValue(config.SysSelfPower);
                tneCellCount.SetIntValue(config.CellCount);

                DateTime dtIS = Convert.ToDateTime(config.SysInstTime);
                tneISYear.SetIntValue(dtIS.Year);
                tneISMonth.SetIntValue(dtIS.Month);
                tneISDay.SetIntValue(dtIS.Day);
                //rtbMemo.Text = strMemo;
                tneSysInterval.SetIntValue(config.SysInterval);
                tneUnInterval.SetIntValue(config.YunInterval);
                ttbSystemID.SetstrText(config.SysID);
                tcbIsMaster.SetValue(PutTchCheck(config.IsMaster));
                //tbMasterID.Text = MasterID;
                tne485Addr.SetIntValue(config.i485Addr);
                tneMaster485Addr.SetIntValue(config.Master485Addr);
                tcbAutoRun.SetValue(PutTchCheck(config.AutoRun));
                tneBMSwaValue.SetIntValue(Math.Abs(cloudLimits.BmsDerateRatio));//7.24
                tneMaxSOC.SetIntValue(cloudLimits.MaxSOC);
                tneMinSOC.SetIntValue(cloudLimits.MinSOC);
                tneSetHotTemp.SetIntValue((int)(componentSettings.SetHotTemp));
                tneSetCoolTemp.SetIntValue((int)(componentSettings.SetCoolTemp));
                tneCoolTempReturn.SetIntValue((int)(componentSettings.CoolTempReturn));
                tneHotTempReturn.SetIntValue((int)(componentSettings.HotTempReturn));

                tneSetHumidity.SetIntValue((int)(componentSettings.SetHumidity));
                tneHumiReturn.SetIntValue((int)(componentSettings.HumiReturn));
                tcbTCRunWithSys.SetValue(PutTchCheck(componentSettings.TCRunWithSys));
                //cbTCAuto.Checked = TCAuto;
                tcbTCMode.SetSelectItemIndex(componentSettings.TCMode);
                tneTCMaxTemp.SetIntValue((int)(componentSettings.TCMaxTemp));
                tneTCMinTemp.SetIntValue((int)(componentSettings.TCMinTemp));
                tneTCMaxHumidity.SetIntValue((int)(componentSettings.TCMaxHumi));
                tneTCMinHumidity.SetIntValue((int)(componentSettings.TCMinHumi));
                tcbDebugComName.SetstrText(config.DebugComName);
                labDebugRate.Text = config.DebugRate.ToString();
                tneMaxGridKWH.SetIntValue(cloudLimits.MaxGridKW);
                tneMinGridKWH.SetIntValue(cloudLimits.MinGridKW);
                tnePrice1.SetIntValue(Prices[0, 1]);
                tnePrice2.SetIntValue(Prices[0, 2]);
                tnePrice3.SetIntValue(Prices[0, 3]);
                tnePrice4.SetIntValue(Prices[0, 4]);

                tnePrice6.SetIntValue(Prices[1, 1]);
                tnePrice7.SetIntValue(Prices[1, 2]);
                tnePrice8.SetIntValue(Prices[1, 3]);
                tnePrice9.SetIntValue(Prices[1, 4]);
                tneSysCount.SetIntValue(config.SysCount);
                tcbUseYunTactics.SetValue(PutTchCheck(config.UseYunTactics));
                tcbUseBalaTactics.SetValue(PutTchCheck(config.UseBalaTactics));
                tcbiPCSfactory.SetSelectItemIndex(config.iPCSfactory);
                tcbPCSGridModel_OnValueChange(null);
                tcbGPIO.SetSelectItemIndex((config.GPIOSelect==1) ? 1 : 0);// 0、2：风冷 1：液冷   注：只展示不做UI修改
                tcbBMSVer.SetSelectItemIndex(config.BMSVerb);
                tcbPCSForceRun.SetValue(PutTchCheck(config.PCSForceRun));
                //10.25
                tneWarnGridkva.SetIntValue(cloudLimits.WarnMaxGridKW);
                //11.13
                tnePUM.SetValue(cloudLimits.PumScale);

                //液冷
                tcbLCModel.SetSelectItemIndex(componentSettings.LCModel);
                tcbLCTemperSelect.SetSelectItemIndex(componentSettings.LCTemperSelect);
                tcbLCWaterPump.SetSelectItemIndex(componentSettings.LCWaterPump);

                tneLCHotTempReturn.SetIntValue((int)(componentSettings.LCHotTempReturn));
                tneLCCoolTempReturn.SetIntValue((int)componentSettings.LCCoolTempReturn);
                tneLCSetHotTemp.SetIntValue((int)componentSettings.LCSetHotTemp);
                tneLCSetCoolTemp.SetIntValue((int)componentSettings.LCSetCoolTemp);

                //11.23 空调点位添加
                tneFenMaxTemp.SetIntValue((int)(componentSettings.FenMaxTemp));
                tneFenMinTemp.SetIntValue((int)(componentSettings.FenMinTemp));
                tcbFenMode.SetSelectItemIndex(componentSettings.FenMode);

                //除湿机
                tneDHSetHumidityStop.SetIntValue(componentSettings.DHSetHumidityStop);
                tneDHSetHumidityBoot.SetIntValue(componentSettings.DHSetHumidityBoot);
                tneDHSetTempBoot.SetIntValue(componentSettings.DHSetTempBoot);
                tneDHSetTempStop.SetIntValue(componentSettings.DHSetTempStop);
                tcbDHSetRunStatus.SetSelectItemIndex(componentSettings.DHSetRunStatus);

            }
            catch { }

        }

        //获取checkbox数据
        public int GetTcbCheck(bool input)
        {
            if (input)
            {
                return 1;
            }
            else
            { return 0; }
        }

        //采集窗口数据

        public void SaveUiInstall()
        {

            config.SysPower = (int)tneSysPower.Value;
            config.SysSelfPower = (int)tneSysSelfPower.Value;

            int MaxDay = DateTime.DaysInMonth(tneISYear.Value, tneISMonth.Value);
            if (MaxDay < tneISDay.Value)
                tneISDay.Value = MaxDay;
            config.SysInstTime = tneISYear.Value.ToString() + "-" + tneISMonth.Value.ToString() + "-" + tneISDay.Value.ToString();
            //strMemo= rtbMemo.Text;
            config.CellCount = (int)tneCellCount.Value;
            //
            config.SysInterval = (int)tneSysInterval.Value;
            config.YunInterval = (int)tneUnInterval.Value;
            config.SysID = ttbSystemID.strText;
            config.IsMaster = GetTcbCheck(tcbIsMaster.Checked);
            config.i485Addr = (int)tne485Addr.Value;
            config.Master485Addr = (int)tneMaster485Addr.Value;
            config.AutoRun = GetTcbCheck(tcbAutoRun.Checked);
            FreshInterval = 24;// (int)tneFreshInterval.Value ;
            cloudLimits.MaxSOC = (int)tneMaxSOC.Value;
            cloudLimits.MinSOC  = (int)tneMinSOC.Value;
            componentSettings.SetHotTemp = (int)tneSetHotTemp.Value;
            componentSettings.SetCoolTemp = (int)tneSetCoolTemp.Value;
            componentSettings.CoolTempReturn = (int)tneCoolTempReturn.Value;
            componentSettings.HotTempReturn = (int)tneHotTempReturn.Value;
            componentSettings.SetHumidity = (int)tneSetHumidity.Value;
            componentSettings.HumiReturn = (int)tneHumiReturn.Value;
            componentSettings.TCRunWithSys = GetTcbCheck(tcbTCRunWithSys.Checked);
            //TCAuto = tcbTCAuto;
            componentSettings.TCMode = tcbTCMode.SelectItemIndex;
            componentSettings.TCMaxTemp = (int)tneTCMaxTemp.Value;
            componentSettings.TCMinTemp = (int)tneTCMinTemp.Value;
            componentSettings.TCMaxHumi = (int)tneTCMaxHumidity.Value;
            componentSettings.TCMinHumi = (int)tneTCMinHumidity.Value;
            config.DebugComName = tcbDebugComName.strText;
            config.DebugRate = 9600;
            cloudLimits.MaxGridKW = (int)tneMaxGridKWH.Value;
            cloudLimits.MinGridKW = (int)tneMinGridKWH.Value;
            Prices[0, 0] = 0;// (int)nudPrice4.Value;
            Prices[0, 1] = (int)tnePrice1.Value;
            Prices[0, 2] = (int)tnePrice2.Value;
            Prices[0, 3] = (int)tnePrice3.Value;
            Prices[0, 4] = (int)tnePrice4.Value;
            Prices[1, 0] = 0;// (int)nudPrice4.Value;
            Prices[1, 1] = (int)tnePrice6.Value;
            Prices[1, 2] = (int)tnePrice7.Value;
            Prices[1, 3] = (int)tnePrice8.Value;
            Prices[1, 4] = (int)tnePrice9.Value;
            config.SysCount = (int)tneSysCount.Value;
            config.UseYunTactics = GetTcbCheck(tcbUseYunTactics.Checked);
            config.UseBalaTactics = GetTcbCheck(tcbUseBalaTactics.Checked);
            config.iPCSfactory = tcbiPCSfactory.SelectItemIndex;
            config.BMSVerb = tcbBMSVer.SelectItemIndex;
            config.PCSForceRun = GetTcbCheck(tcbPCSForceRun.Checked);
            //10.25
            cloudLimits.WarnMaxGridKW = (int)tneWarnGridkva.Value;
            //11.13
            cloudLimits.PumScale = (int)tnePUM.Value;
            cloudLimits.BmsDerateRatio = (int)tneBMSwaValue.Value;

            //液冷
            componentSettings.LCModel = tcbLCModel.SelectItemIndex;      //全自动
            componentSettings.LCTemperSelect = tcbLCTemperSelect.SelectItemIndex; //出水温度
            componentSettings.LCWaterPump = tcbLCWaterPump.SelectItemIndex;  //默认档
            componentSettings.LCSetHotTemp  = (int)tneLCSetHotTemp.Value;  //20°C
            componentSettings.LCSetCoolTemp = (int)tneLCSetCoolTemp.Value; //20°C
            componentSettings.LCHotTempReturn = (int)tneLCHotTempReturn.Value;  //2°C
            componentSettings.LCCoolTempReturn = (int)tneLCCoolTempReturn.Value; //2°C

            //11.23
            componentSettings.FenMaxTemp = (int)tneFenMaxTemp.Value;
            componentSettings.FenMinTemp = (int)tneFenMinTemp.Value;
            componentSettings.FenMode = tcbFenMode.SelectItemIndex;
            //5.04 除湿
            componentSettings.DHSetRunStatus = tcbDHSetRunStatus.SelectItemIndex;
            componentSettings.DHSetTempBoot = (int)tneDHSetTempBoot.Value;
            componentSettings.DHSetTempStop = (int)tneDHSetTempStop.Value;
            componentSettings.DHSetHumidityBoot = (int)tneDHSetHumidityBoot.Value;
            componentSettings.DHSetHumidityStop = (int)tneDHSetHumidityStop.Value;
        }

        private void SaveDehumidifierSettings()
        {
            componentSettings.DHSetRunStatus = tcbDHSetRunStatus.SelectItemIndex;
            componentSettings.DHSetTempBoot = (int)tneDHSetTempBoot.Value;
            componentSettings.DHSetTempStop = (int)tneDHSetTempStop.Value;
            componentSettings.DHSetHumidityBoot = (int)tneDHSetHumidityBoot.Value;
            componentSettings.DHSetHumidityStop = (int)tneDHSetHumidityStop.Value;
        }

        private void SaveTempControlSettings()
        {
            componentSettings.SetHotTemp = (int)tneSetHotTemp.Value;
            componentSettings.SetCoolTemp = (int)tneSetCoolTemp.Value;
            componentSettings.CoolTempReturn = (int)tneCoolTempReturn.Value;
            componentSettings.HotTempReturn = (int)tneHotTempReturn.Value;
            componentSettings.SetHumidity = (int)tneSetHumidity.Value;
            componentSettings.HumiReturn = (int)tneHumiReturn.Value;
            componentSettings.TCRunWithSys = GetTcbCheck(tcbTCRunWithSys.Checked);
            componentSettings.TCMode = tcbTCMode.SelectItemIndex;
            componentSettings.TCMaxTemp = (int)tneTCMaxTemp.Value;
            componentSettings.TCMinTemp = (int)tneTCMinTemp.Value;
            componentSettings.TCMaxHumi = (int)tneTCMaxHumidity.Value;
            componentSettings.TCMinHumi = (int)tneTCMinHumidity.Value;
            componentSettings.FenMaxTemp = (int)tneFenMaxTemp.Value;
            componentSettings.FenMinTemp = (int)tneFenMinTemp.Value;
            componentSettings.FenMode = tcbFenMode.SelectItemIndex;
        }

        private void SaveLiquidCoolingSettings()
        {
            componentSettings.LCModel = tcbLCModel.SelectItemIndex;
            componentSettings.LCTemperSelect = tcbLCTemperSelect.SelectItemIndex;
            componentSettings.LCWaterPump = tcbLCWaterPump.SelectItemIndex;
            componentSettings.LCSetHotTemp = (int)tneLCSetHotTemp.Value;
            componentSettings.LCSetCoolTemp = (int)tneLCSetCoolTemp.Value;
            componentSettings.LCHotTempReturn = (int)tneLCHotTempReturn.Value;
            componentSettings.LCCoolTempReturn = (int)tneLCCoolTempReturn.Value;
        }


        static public void PCSMRun()
        {
            string strWorkType = "待机";
            if (PCSType == "待机")
                strWorkType = "待机";
            else if (PCSwaValue > 0)
                strWorkType = "充电";
            else
                strWorkType = "放电";
            string tempPCSType;
            int tempPCSwaValue = Math.Abs(PCSwaValue);


            //将其他两种改编为恒功率
            if (PCSType == "恒流")
                tempPCSwaValue = (int)(tempPCSwaValue * 0.8);
            if (PCSType == "恒压")
            {
                tempPCSwaValue = (int)((tempPCSwaValue - 648) * 0.7);
                if (tempPCSwaValue < 0)
                    tempPCSwaValue = 0;
            }

            //对上位机页面显示：充电为负 放电为正  对写入PCS执行功率：充电为正，放电为负
            if (strWorkType == "放电")
                tempPCSwaValue = -tempPCSwaValue;
            //调整充放电的符号
            //tempPCSType = "恒功率";
            /*            if (PCSType != "AC恒压")
                            tempPCSType = "恒功率";
                        else
                            tempPCSType = "AC恒压";*/

            //9.4 加入自适应需量
            /*            if ((PCSType != "AC恒压") || (PCSType != "自适应需量"))
                            tempPCSType = "恒功率";
                        else
                        {
                            if (PCSType == "AC恒压")
                                tempPCSType = "AC恒压";
                            else
                                tempPCSType = "自适应需量";
                        }*/
            tempPCSType = PCSType;


            frmMain.TacticsList.ActiveIndex = -1;
            switch (config.SysMode)
            {
                case 0://手动模式
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        frmMain.Selffrm.AllEquipment.eState = 0;//记录手动开启
                        frmMain.TacticsList.TacticsOn = false;
                        frmMain.TacticsList.ActiveIndex = -2;
                        frmMain.Selffrm.AllEquipment.PCSTypeActive = tempPCSType;
                        frmMain.Selffrm.AllEquipment.wTypeActive = strWorkType;
                        frmMain.Selffrm.AllEquipment.PCSScheduleKVA = tempPCSwaValue;
                        frmMain.Selffrm.AllEquipment.HostStart = true;
                        frmMain.Selffrm.AllEquipment.SlaveStart = true;
                    }
                    break;
                case 1://策略模式
                    frmMain.Selffrm.AllEquipment.eState = 1;//记策略模式
                    frmMain.TacticsList.TacticsOn = false;
                    frmMain.TacticsList.LoadFromMySQL(0);
                    frmMain.TacticsList.ActiveIndex = -1;
                    frmMain.TacticsList.TacticsOn = true;

                    break;
                case 2://网控模式
                    frmMain.Selffrm.AllEquipment.eState = 2;//网控开启
                    frmMain.TacticsList.TacticsOn = false;
                    break;
            }
        }

        static public void Err3off()
        {
            while (frmMain.Selffrm.AllEquipment.PCSKVA != 0)
            {
                //关闭PCS充电放电
                frmMain.Selffrm.AllEquipment.HostStart = false;
                frmMain.Selffrm.AllEquipment.ExcPCSPowerOff();
                frmMain.Selffrm.AllEquipment.waValueActive = 0;
            }
        }

        static public void PCSMOff()
        {
            lock (frmMain.Selffrm.AllEquipment)
            {
                frmMain.TacticsList.TacticsOn = false;
                frmMain.Selffrm.AllEquipment.eState = 0;//记录手动开启
                frmMain.TacticsList.ActiveIndex = -2;
                //关闭PCS充电放电
                frmMain.Selffrm.AllEquipment.HostStart = false;
                frmMain.Selffrm.AllEquipment.SlaveStart= false;

                frmMain.Selffrm.AllEquipment.PCSScheduleKVA = 0;
                frmMain.Selffrm.AllEquipment.waValueActive = 0;
            }
        }

/*        static public void DeleOldData(string astrData)
        {
            //删除清理数据库
            string[] strSQL = {"delete from cellstemp where rTime<'"+astrData+"'",
            "delete from battery where rTime<'"+astrData+"'",
            "delete from cellsv where rTime<'"+astrData+"'",
            "delete from electrovalence where rTime<'"+astrData+"'",
            "delete from elemeter1 where rTime<'"+astrData+"'",
            "delete from elemeter2 where rTime<'"+astrData+"'",
            "delete from elemeter3 where rTime<'"+astrData+"'",
            "delete from elemeter4 where rTime<'"+astrData+"'",
            "delete from errorstate where rTime<'"+astrData+"'",
            "delete from fire where rTime<'"+astrData+"'",
            "delete from pcs where rTime<'"+astrData+"'",
            "delete from pncontroler where rTime<'"+astrData+"'",
            "delete from profit where rTime<'"+astrData+"'",
            "delete from tactics where rTime<'"+astrData+"'",
            "delete from tempcontrol where rTime<'"+astrData+"'",
            "delete from warning where rTime<'"+astrData+"'",
            "delete from liquidcool where rTime<'"+astrData+"'"
            //,"delete from chargeinform rTime<'"+astrData+"'"
            };
            foreach (string astrSQl in strSQL)
                DBConnection.ExecSQLWithParams(astrSQl, null);
        }*/

        static public bool DeleOldData(string astrData)
        {
            try
            {

                // 防御性检查：确保日期格式合法（可选）
                if (string.IsNullOrWhiteSpace(astrData))
                {
                    log.Error("DeleOldData: 传入的日期为空");
                    return false;
                }

                string[] strSQL = {
                "DELETE FROM cellstemp WHERE rTime < '" + astrData + "'",
                "DELETE FROM battery WHERE rTime < '" + astrData + "'",
                "DELETE FROM cellsv WHERE rTime < '" + astrData + "'",
                "DELETE FROM elemeter1 WHERE rTime < '" + astrData + "'",
                "DELETE FROM elemeter2 WHERE rTime < '" + astrData + "'",
                "DELETE FROM elemeter3 WHERE rTime < '" + astrData + "'",
                "DELETE FROM elemeter4 WHERE rTime < '" + astrData + "'",
                "DELETE FROM errorstate WHERE rTime < '" + astrData + "'",
                "DELETE FROM fire WHERE rTime < '" + astrData + "'",
                "DELETE FROM pcs WHERE rTime < '" + astrData + "'",
                "DELETE FROM pncontroler WHERE rTime < '" + astrData + "'",
                "DELETE FROM profit WHERE rTime < '" + astrData + "'",
                "DELETE FROM tempcontrol WHERE rTime < '" + astrData + "'",
                "DELETE FROM warning WHERE rTime < '" + astrData + "'",
                "DELETE FROM liquidcool WHERE rTime < '" + astrData + "'"
            };

                foreach (string sql in strSQL)
                {
                    if (DBConnection.ExecSQLWithParams(sql, null) < 0)
                    {
                        log.Error($"DeleOldData 失败于 SQL: {sql.Substring(0, Math.Min(80, sql.Length))}...");
                        return false; // 任一失败即整体失败
                    }
                }

                log.Info($"DeleOldData 成功清理 {astrData} 之前的数据");
                return true;
            }
            catch (Exception ex) {
                log.Error("DeleOldData: " + ex);
                return false;
            }
        }

        private void DelData(string aTableName, string aDataName, string aData, DataGridView aDataGrid)
        {
            if (aDataGrid.SelectedRows.Count <= 0)
                return;
            if (MessageBox.Show("确定要删除当前数据吗", "询问信息", MessageBoxButtons.OKCancel) != DialogResult.OK)
                return;

            string sql = $"delete from {aTableName} where {aDataName} = @val";
            var parameters = new Dictionary<string, object> { { "@val", aData ?? string.Empty } };
            DBConnection.ExecSQLWithParams(sql, parameters);
            aDataGrid.Rows.RemoveAt(aDataGrid.SelectedRows[0].Index);
            dbgEquipment.Update();
        }

        private void btnTempRun_Click(object sender, EventArgs e)
        {
            frmMain.Selffrm.AllEquipment.TCPowerOn(true);
        }

        private void btnACErrorClean_Click(object sender, EventArgs e)
        {
            frmMain.Selffrm.AllEquipment.TCCleanError();
        }

        private void btnTCPowerOff_Click(object sender, EventArgs e)
        {
            frmMain.Selffrm.AllEquipment.TCPowerOn(false);
        }


        private void btnMain_Click(object sender, EventArgs e)
        {
/*            //统一记录修改数据信息
            SaveUiInstall();

            //统一保存所有配置信息
            Set_Cloudlimits();
            Set_Config();
            Set_ComponentSettings();*/

            CloseForm();
            frmMain.ShowMainForm();
        }


        private void tneSetHotTemp_OnValueChange(object sender)
        {
            bTCDataChanged = true;
        }


        private void btnAdd1_Click(object sender, EventArgs e)
        {
            frmoneEquipment.AddData(dbgEquipment);
        }

        private void btnAdd2_Click(object sender, EventArgs e)
        {
            if (dbgElectrovalence.RowCount < 28)
            {
                frmoneElectrovalence.AddData(dbgElectrovalence);
            }
        }

        //实施策略->新增
        private void btnAdd3_Click(object sender, EventArgs e)
        {
            if (tcbUseYunTactics.Checked)
                return;
            frmoneTactics.AddData(dbgTactics);
        }

        //人员设置->新增
        private void btnAdd4_Click(object sender, EventArgs e)
        {
            frmoneUser.AddData(dbgUsers);
        }

        private void button8_Click(object sender, EventArgs e)
        {
            if (dbgEquipment.SelectedRows.Count > 0)
                frmoneEquipment.EditData(dbgEquipment);
        }

        private void btnEdit2_Click(object sender, EventArgs e)
        {
            if (dbgElectrovalence.SelectedRows.Count > 0)
            {
                frmoneElectrovalence.EditData(dbgElectrovalence);
            }
        }

        private void btnEdit3_Click(object sender, EventArgs e)
        {
            if (tcbUseYunTactics.Checked)
                return;
            if (dbgTactics.SelectedRows.Count > 0)
            {
                frmoneTactics.EditData(dbgTactics);
            }
        }

        //人员管理->编辑
        private void button5_Click(object sender, EventArgs e)
        {
            if (dbgUsers.SelectedRows.Count > 0)
                frmoneUser.EditData(dbgUsers);
        }

        private void btnDel1_Click(object sender, EventArgs e)
        {

            DelData("equipment", "id", dbgEquipment.SelectedRows[0].Cells[0].Value.ToString(), dbgEquipment);
        }

        private void btnDel2_Click(object sender, EventArgs e)
        {
            DelData("electrovalence", "id", dbgElectrovalence.SelectedRows[0].Cells[0].Value.ToString(), dbgElectrovalence);
        }

        //实施策略->删除
        private void btnDel3_Click(object sender, EventArgs e)
        {
            if (tcbUseYunTactics.Checked)
                return;
            if (dbgTactics.RowCount > 0)
            {
                DelData("tactics", "id", dbgTactics.SelectedRows[0].Cells[0].Value.ToString(), dbgTactics);
            }

        }

        //人员管理->删除
        private void btnDel4_Click(object sender, EventArgs e)
        {
            DelData("users", "id", dbgUsers.SelectedRows[0].Cells[0].Value.ToString(), dbgUsers);
        }

        private void btnBaseInf_Click(object sender, EventArgs e)
        {
            btnBaseInf.BackColor = Color.FromArgb(20, 169, 255);
            btnEqipments.BackColor = Color.Transparent;
            btnE.BackColor = Color.Transparent;
            btnShedule.BackColor = Color.Transparent;
            btnUser.BackColor = Color.Transparent;
            btnCom.BackColor = Color.Transparent;
            btnLog.BackColor = Color.Transparent;
            btnCTL.BackColor = Color.Transparent;
            btnLC.BackColor = Color.Transparent;
            plSetMain.Parent = tbAll;
            tpE.Parent = null;
            tpUser.Parent = null;
            tbCTL.Parent = null;
            tbShedule.Parent = null;
            tpEquipments.Parent = null;
            tpCom.Parent = null;
            tpLog.Parent = null;
            tpLC.Parent = null;


        }

        private void btnEqipments_Click(object sender, EventArgs e)
        {

            DBConnection.SetDBGrid(oneForm.dbgEquipment);
            DBConnection.ShowData2DBGrid(oneForm.dbgEquipment, "select * from equipment");

            btnBaseInf.BackColor = Color.Transparent;
            btnEqipments.BackColor = Color.FromArgb(20, 169, 255);
            btnE.BackColor = Color.Transparent;
            btnShedule.BackColor = Color.Transparent;
            btnUser.BackColor = Color.Transparent;
            btnCom.BackColor = Color.Transparent;
            btnLog.BackColor = Color.Transparent;
            btnCTL.BackColor = Color.Transparent;
            btnLC.BackColor = Color.Transparent;
            plSetMain.Parent = null;
            tpE.Parent = null;
            tpUser.Parent = null;
            tbCTL.Parent = null;
            tbShedule.Parent = null;
            tpEquipments.Parent = tbAll;
            tpCom.Parent = null;
            tpLog.Parent = null;
            tpLC.Parent = null;
            // DBConnection.SetDBGrid(dbgEquipment);
            // DBConnection.ShowData2DBGrid(dbgEquipment, "select * from equipment");
        }

        private void btnE_Click(object sender, EventArgs e)
        {
            string strDate = DateTime.Now.ToString("yyyy-MM-dd");
            DBConnection.SetDBGrid(oneForm.dbgElectrovalence);
            //DBConnection.ShowData2DBGrid(oneForm.dbgElectrovalence, "select id,section,eName,startTime from electrovalence order by section,startTime");
            DBConnection.ShowData2DBGrid(oneForm.dbgElectrovalence,
                "select id,section,eName,startTime from electrovalence " +
                "where rTime = '" + strDate + "' " +  // 添加时间约束条件
                "order by startTime");       // 按时间优先排序

            btnBaseInf.BackColor = Color.Transparent;
            btnEqipments.BackColor = Color.Transparent;
            btnE.BackColor = Color.FromArgb(20, 169, 255);
            btnShedule.BackColor = Color.Transparent;
            btnUser.BackColor = Color.Transparent;
            btnCom.BackColor = Color.Transparent;
            btnLog.BackColor = Color.Transparent;
            btnCTL.BackColor = Color.Transparent;
            btnLC.BackColor = Color.Transparent;
            plSetMain.Parent = null;
            tpE.Parent = tbAll;
            tpUser.Parent = null;
            tbCTL.Parent = null;
            tbShedule.Parent = null;
            tpEquipments.Parent = null;
            tpCom.Parent = null;
            tpLog.Parent = null;
            tpLC.Parent = null;
        }

        private void btnShedule_Click(object sender, EventArgs e)
        {

            DBConnection.SetDBGrid(oneForm.dbgTactics);
            /*            string strDate = DateTime.Now.ToString("yyyy-MM-dd");
                        DBConnection.ShowData2DBGrid(oneForm.dbgTactics, "select * from tactics where rTime = '"+ strDate +"'order by starttime");*/
            // strDate = DateTime.Now.ToString("yyyy-MM-dd");

            try {
                string sql = "SELECT * FROM tactics WHERE rTime = @Date ORDER BY startTime";

                var parameters = new Dictionary<string, object>
                {
                    { "@Date", DateTime.Today }
                };

                var dataTable = DBConnection.QueryDataTableWithParams(sql, parameters);
                if (dataTable != null)
                {
                    oneForm.dbgTactics.DataSource = dataTable;
                }
                else
                {
                    log.Error("查询数据失败");
                    oneForm.dbgTactics.DataSource = null;
                }
            }catch(Exception ex) {
                log.Error("btnShedule_Click:" +ex.ToString());
                oneForm.dbgTactics.DataSource = null;
            }


            btnBaseInf.BackColor = Color.Transparent;
            btnEqipments.BackColor = Color.Transparent;
            btnE.BackColor = Color.Transparent;
            btnShedule.BackColor = Color.FromArgb(20, 169, 255);
            btnUser.BackColor = Color.Transparent;
            btnCom.BackColor = Color.Transparent;
            btnLog.BackColor = Color.Transparent;
            btnCTL.BackColor = Color.Transparent;
            btnLC.BackColor = Color.Transparent;
            plSetMain.Parent = null;
            tpE.Parent = null;
            tpUser.Parent = null;
            tbCTL.Parent = null;
            tbShedule.Parent = tbAll;
            tpEquipments.Parent = null;
            tpCom.Parent = null;
            tpLog.Parent = null;
            tpLC.Parent = null;

        }

        private void btnUser_Click(object sender, EventArgs e)
        {

            DBConnection.SetDBGrid(oneForm.dbgUsers);
            DBConnection.ShowData2DBGrid(dbgUsers, "select * from users");

            btnBaseInf.BackColor = Color.Transparent;
            btnEqipments.BackColor = Color.Transparent;
            btnE.BackColor = Color.Transparent;
            btnShedule.BackColor = Color.Transparent;
            btnUser.BackColor = Color.FromArgb(20, 169, 255);
            btnCom.BackColor = Color.Transparent;
            btnLog.BackColor = Color.Transparent;
            btnCTL.BackColor = Color.Transparent;
            btnLC.BackColor = Color.Transparent;
            plSetMain.Parent = null;
            tpE.Parent = null;
            tpUser.Parent = tbAll;
            tbCTL.Parent = null;
            tbShedule.Parent = null;
            tpEquipments.Parent = null;
            tpCom.Parent = null;
            tpLog.Parent = null;
            tpLC.Parent = null;
        }

        private void btnCom_Click(object sender, EventArgs e)
        {
            btnBaseInf.BackColor = Color.Transparent;
            btnEqipments.BackColor = Color.Transparent;
            btnE.BackColor = Color.Transparent;
            btnShedule.BackColor = Color.Transparent;
            btnUser.BackColor = Color.Transparent;
            btnCom.BackColor = Color.Transparent;
            btnLog.BackColor = Color.Transparent;
            btnCTL.BackColor = Color.Transparent;
            btnLC.BackColor = Color.Transparent;
            plSetMain.Parent = null;
            tpE.Parent = null;
            tpUser.Parent = null;
            tbCTL.Parent = null;
            tbShedule.Parent = null;
            tpEquipments.Parent = null;
            tpCom.Parent = tbAll;
            tpLog.Parent = null;
            tpLC.Parent = null;
        }

        private void btnLog_Click(object sender, EventArgs e)
        {

            /*            DBConnection.SetDBGrid(oneForm.dbgLog);
                        DBConnection.ShowData2DBGrid(oneForm.dbgLog, "select * from log");*/

            btnBaseInf.BackColor = Color.Transparent;
            btnEqipments.BackColor = Color.Transparent;
            btnE.BackColor = Color.Transparent;
            btnShedule.BackColor = Color.Transparent;
            btnUser.BackColor = Color.Transparent;
            btnCom.BackColor = Color.Transparent;
            btnLog.BackColor = Color.FromArgb(20, 169, 255);
            btnCTL.BackColor = Color.Transparent;
            btnLC.BackColor = Color.Transparent;
            plSetMain.Parent = null;
            tpE.Parent = null;
            tpUser.Parent = null;
            tbCTL.Parent = null;
            tbShedule.Parent = null;
            tpEquipments.Parent = null;
            tpCom.Parent = null;
            tpLog.Parent = tbAll;
            tpLC.Parent = null;

        }

        private void btnCTL_Click(object sender, EventArgs e)
        {
            btnBaseInf.BackColor = Color.Transparent;
            btnEqipments.BackColor = Color.Transparent;
            btnE.BackColor = Color.Transparent;
            btnShedule.BackColor = Color.Transparent;
            btnUser.BackColor = Color.Transparent;
            btnCom.BackColor = Color.Transparent;
            btnLog.BackColor = Color.Transparent;
            btnCTL.BackColor = Color.FromArgb(20, 169, 255);
            btnLC.BackColor = Color.Transparent;
            plSetMain.Parent = null;
            tpE.Parent = null;
            tpUser.Parent = null;
            tbCTL.Parent = tbAll;
            tbShedule.Parent = null;
            tpEquipments.Parent = null;
            tpCom.Parent = null;
            tpLog.Parent = null;
            tpLC.Parent = null;

        }

        private void btnLC_Click(object sender, EventArgs e)
        {
            btnBaseInf.BackColor = Color.Transparent;
            btnEqipments.BackColor = Color.Transparent;
            btnE.BackColor = Color.Transparent;
            btnShedule.BackColor = Color.Transparent;
            btnUser.BackColor = Color.Transparent;
            btnCom.BackColor = Color.Transparent;
            btnLog.BackColor = Color.Transparent;
            btnCTL.BackColor = Color.Transparent;
            btnLC.BackColor = Color.FromArgb(20, 169, 255);
            plSetMain.Parent = null;
            tpE.Parent = null;
            tpUser.Parent = null;
            tbCTL.Parent = null;
            tbShedule.Parent = null;
            tpEquipments.Parent = null;
            tpCom.Parent = null;
            tpLog.Parent = null;
            tpLC.Parent = tbAll;
        }


        private void tcbAutoRun_OnValueChange(object sender)
        {
            SysIO.SetAutoRun("", tcbAutoRun.Checked);
        }

        /// <summary>
        /// 数据库显示的DBGrid上下选择
        /// </summary>
        /// <param name="aGrid"></param>
        private void SetDbgridUp(DataGridView aGrid)
        {
            if (aGrid.RowCount <= 0)
                return;
            if (aGrid.SelectedRows.Count == 0)
                aGrid.Rows[0].Selected = true;
            int iIndex = aGrid.SelectedRows[0].Index;
            if (iIndex > 0)
                aGrid.Rows[--iIndex].Selected = true;
        }

        /// <summary>
        /// 数据库显示的DBGrid上下选择
        /// </summary>
        /// <param name="aGrid"></param>
        private void SetDbgridDown(DataGridView aGrid)
        {
            if (aGrid.RowCount <= 0)
                return;
            if (aGrid.SelectedRows.Count == 0)
                aGrid.Rows[0].Selected = true;
            int iIndex = aGrid.SelectedRows[0].Index;

            if (iIndex < aGrid.Rows.Count - 1)
                aGrid.Rows[++iIndex].Selected = true;
        }


        private void btnUpE_Click(object sender, EventArgs e)
        {
            SetDbgridUp(dbgEquipment);
        }

        private void btnDownE_Click(object sender, EventArgs e)
        {
            SetDbgridDown(dbgEquipment);
        }

        private void btnUpS_Click(object sender, EventArgs e)
        {
            SetDbgridUp(dbgElectrovalence);
        }

        private void btnDownS_Click(object sender, EventArgs e)
        {
            SetDbgridDown(dbgElectrovalence);
        }

        private void btnUpT_Click(object sender, EventArgs e)
        {
            SetDbgridUp(dbgTactics);
        }

        private void btnDownT_Click(object sender, EventArgs e)
        {
            SetDbgridDown(dbgTactics);
        }

        private void btnUpU_Click(object sender, EventArgs e)
        {
            SetDbgridUp(dbgUsers);
        }

        private void btnDownU_Click(object sender, EventArgs e)
        {
            SetDbgridDown(dbgUsers);
        }

        private void btnSet_Click(object sender, EventArgs e)
        {

        }

        private void ShowOneStep()
        {
            if (pbTimer.Value < 95)
                pbTimer.Value += 5;
        }



        private void tcbIsMaster_OnValueChange(object sender)
        {
            if (tcbIsMaster.Checked)
            {
                tne485Addr.SetIntValue(1);
                tne485Addr.Enabled = false;
                //tne485Addr.CanEdit = false;
            }
            else
            {
                tne485Addr.Enabled = true;
                tne485Addr.CanEdit = true;
            }
        }

        private void tcbPCSGridModel_OnValueChange(object sender)
        {
            /*            switch (tcbPCSGridModel.SelectItemIndex)
                        {
                            case 0://并网
                                tcbPCSType.SetSelectItemIndex(3);
                                tcbPCSMode.SetSelectItemIndex(1);
                                tcbPCSMode.Enabled = true;
                                tnePCSwaValue.Visible = true;
                                labPCSwaValue.Visible = true;
                                lablPCSwaValue2.Visible = true;
                                break;
                            case 1://离网
                                tcbPCSType.SetSelectItemIndex(4);
                                tcbPCSMode.SetSelectItemIndex(1);
                                tcbPCSMode.Enabled = false;
                                tnePCSwaValue.Visible = false;
                                labPCSwaValue.Visible = false;
                                lablPCSwaValue2.Visible = false;
                                break;
                        }*/
        }

        private void btnCleanDataBase_Click(object sender, EventArgs e)
        {
            DialogResult aDlgResult = MessageBox.Show("确定要清理数据库吗？", "询问", MessageBoxButtons.YesNo);
            if (aDlgResult!= DialogResult.Yes)
                return;
            //删除清理数据库
            string[] strSQL = {"delete   from cellstemp;",
            "delete from battery; ",
            "delete from cellsv; ",
            "delete from electrovalence; ",
            "delete from elemeter1; ",
            "delete from elemeter2; ",
            "delete from elemeter3; ",
            "delete from elemeter4; ",
            "delete from errorstate; ",
            "delete from fire; ",
            "delete from pcs; ",
            "delete from pncontroler; ",
            "delete from profit; ",
            // "delete from tactics; ",
            "delete from tempcontrol; ",
            "delete from warning; ",
            "delete from liquidcool; "
            };
            foreach (string astrSQl in strSQL)
            {
                DBConnection.ExecSQLWithParams(astrSQl, null);
                Thread.Sleep(100);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult aDlgResult = MessageBox.Show("确定要重启系统吗？", "询问", MessageBoxButtons.YesNo);
            if (aDlgResult != DialogResult.Yes)
                return;
            //SysIO.Reboot();

            //RestartWindows();
        }

        public void btnClose_Click(object sender, EventArgs e)
        {
            try
            {
                PowerGPIO(0);
                Set_Cloudlimits();
                if (frmMain.Selffrm.AllEquipment.Led != null)
                {
                    frmMain.Selffrm.AllEquipment.Led.Set_Led_ShutDown();
                }
                this.Close();
                frmMain.Selffrm.Close();
            }
            catch (Exception ex)
            {
                log.Error("btnClose_Click: " + ex.Message);
            }
        }

        private void btnUpdata_Click(object sender, EventArgs e)
        {
            string strDate = DateTime.Now.ToString("yyyy-MM-dd");

            frmMain.TacticsList.LoadJFPGFromSQL_WithCompare();

            //DBConnection.ShowData2DBGrid(oneForm.dbgElectrovalence, "select * from electrovalence where rTime = '"+ strDate +"' order by section");
            DBConnection.ShowData2DBGrid(oneForm.dbgElectrovalence,
                "select id,section,eName,startTime from electrovalence " +
                "where rTime = '" + strDate + "' " +  // 添加时间约束条件
                "order by startTime");       // 按时间优先排序
        }

        //控制模式->空调设置->应用
        private void btnATAppy_Click(object sender, EventArgs e)
        {
            //获取UI设置
            SaveUiInstall();
            //设置下发
            frmMain.Selffrm.AllEquipment.TCIni(true);
        }

        //控制模式->空调设置->读取
        private void btnAirRead_Click(object sender, EventArgs e)
        {
            if (frmMain.Selffrm.AllEquipment.TempControl == null)
                return;
            frmMain.Selffrm.AllEquipment.TempControl.ReadTCparams();
        }

        //液冷设置->液冷设置->应用
        private void btnLCRun_Click(object sender, EventArgs e)
        {
            try
            {
                //获取UI设置
                SaveUiInstall();
                //设置下发
                frmMain.Selffrm.AllEquipment.LCIni();
            }
            catch { }
        }

        private void btnLClose_Click(object sender, EventArgs e)
        {
            try
            {
                if (frmMain.Selffrm.AllEquipment.LiquidCool !=null)
                    frmMain.Selffrm.AllEquipment.LiquidCool.LCPowerOn(false);
            }
            catch { }
        }

        private void btnLCOpen_Click(object sender, EventArgs e)
        {
            try
            {
                if (frmMain.Selffrm.AllEquipment.LiquidCool !=null)
                {
                    frmMain.Selffrm.AllEquipment.LiquidCool.LCPowerOn(true);
                    frmMain.Selffrm.AllEquipment.LiquidCool.ExecCommand();
                }
            }
            catch { }
        }

        private void btnDHRun_Click(object sender, EventArgs e)
        {
            try
            {
                SaveUiInstall();
                frmMain.Selffrm.AllEquipment.DHIni();
            }
            catch { }
        }

        private void btnLCRead_Click(object sender, EventArgs e)
        {
            frmMain.Selffrm.AllEquipment.LiquidCool.GetSetDataFromEquipment();
        }

        private void btnDHRead_Click(object sender, EventArgs e)
        {
            if (frmMain.Selffrm.AllEquipment.Dehumidifier != null)
                frmMain.Selffrm.AllEquipment.Dehumidifier.GetDataFromEqipment();
        }

        //读取数据库，刷新策略时段
        private void btnFlash3_Click(object sender, EventArgs e)
        {
            RenewTactics();
            //RenewBalaTactics();
            string strDate = DateTime.Now.ToString("yyyy-MM-dd");
            DBConnection.ShowData2DBGrid(oneForm.dbgTactics, "select * from tactics where rTime = '"+ strDate +"'order by starttime");
        }



        /************************* DB Class *********************************/
        public class PeElesticClass
        {
            public DateTime rDate;
            public double[] SE2PKWH = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };         //记录当天开始充电电量（positive 正向）  new double[9]
            public double[] SE2OKWH = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };         //记录当天开始放电电量（opposite反向，逆向）
            public double[] SAuxiliaryKWH = { 0, 0, 0, 0, 0 };
/*            public double SE2PKWH;
            public double SE2PKWH0;
            public double SE2OKWH0;
            public double SAuxiliaryKWH0;
            public double SE2PKWH1;
            public double SE2OKWH1;
            public double SAuxiliaryKWH1;
            public double SE2PKWH2;
            public double SE2OKWH2;
            public double SAuxiliaryKWH2;
            public double SE2PKWH3;
            public double SE2OKWH3;
            public double SAuxiliaryKWH3;
            public double SE2PKWH4;
            public double SE2OKWH4;
            public double SAuxiliaryKWH4;
            public double SE2PKWH5;
            public double SE2OKWH5;
            public double SE2PKWH6;
            public double SE2OKWH6;
            public double SE2PKWH7;
            public double SE2OKWH7;
            public double SE2PKWH8;
            public double SE2OKWH8;*/
        }


        public class HistoryDataClass
        {
            public volatile int E1PUMdemandMaxOld;
            public volatile int ClientPUMdemandMaxOld;
            public volatile int ClientPUMdemandMax;
            public volatile int ErrorState2;
/*            public volatile int DaliyE2PKWH_Z;
            public volatile int DaliyE2PKWH_J;
            public volatile int DaliyE2PKWH_F;
            public volatile int DaliyE2PKWH_P;
            public volatile int DaliyE2PKWH_G;
            public volatile int DaliyE2PKWH_5;
            public volatile int DaliyE2PKWH_6;
            public volatile int DaliyE2PKWH_7;
            public volatile int DaliyE2PKWH_8;
            public volatile int DaliyE2OKWH_Z;
            public volatile int DaliyE2OKWH_J;
            public volatile int DaliyE2OKWH_F;
            public volatile int DaliyE2OKWH_P;
            public volatile int DaliyE2OKWH_G;
            public volatile int DaliyE2OKWH_5;
            public volatile int DaliyE2OKWH_6;
            public volatile int DaliyE2OKWH_7;
            public volatile int DaliyE2OKWH_8;*/
            public volatile int RebootCount;
            public volatile int YDstatus;
        }

/*        public class CloudLimitClass
        {
            public volatile int MaxGridKW;
            public volatile int MinGridKW;
            public volatile int MaxSOC;
            public volatile int MinSOC;
            public volatile int WarnMaxGridKW;
            public volatile int WarnMinGridKW;
            public volatile int PcsKva;
            public volatile int Pre_Client_PUMdemand_Max;
            public volatile int EnableActiveReduce;
            public volatile int PumScale;
            public volatile int AllUkvaWindowSize;
            public volatile int PumTime;
            public volatile int BmsDerateRatio;
            public volatile int FrigOpenLower;
            public volatile int FrigOffLower;
            public volatile int FrigOffUpper;
            public volatile int BoxHTemperAlarm;
            public volatile int BoxLTemperAlarm;
            public volatile int SignalDelayAlarm;
            public volatile int SignalDelayCount;
            public volatile int CellV_Gap;
            public volatile int OpenBala;
        }*/

        public class CloudLimitClass
        {
            public int MaxGridKW { get; set; }
            public int MinGridKW { get; set; }
            public int MaxSOC { get; set; }
            public int MinSOC { get; set; }
            public int WarnMaxGridKW { get; set; }
            public int WarnMinGridKW { get; set; }
            public int PcsKva { get; set; }
            public int Pre_Client_PUMdemand_Max { get; set; }
            public int EnableActiveReduce { get; set; }
            public int PumScale { get; set; }
            public int AllUkvaWindowSize { get; set; }
            public int PumTime { get; set; }
            public int BmsDerateRatio { get; set; }
            public int FrigOpenLower { get; set; }
            public int FrigOffLower { get; set; }
            public int FrigOffUpper { get; set; }
            public int BoxHTemperAlarm { get; set; }
            public int BoxLTemperAlarm { get; set; }

            public int SignalDelayAlarm { get; set; }
            public int SignalDelayCount { get; set; }
            public int CellV_Gap { get; set; }
            public int OpenBala { get; set; }

            public int OpenWarning { get; set; }
            public HashSet<string> ModifiedFields { get; set; } = new HashSet<string>();
        }


        //运行时参数变化参数
        public class VariChargeClass
        {
            public volatile int UBmsPcsState;
            public volatile int OBmsPcsState;
        }

        //初始化不变更参数
        public class ConfigClass
        {
            public string SysID { get; set; } // varchar(255) PRIMARY KEY
            public int Open104 { get; set; } // int 是否开启104服务 0关1开
            public int NetTick { get; set; } // int 判断超时的时间间隔
            public string SysName { get; set; } // varchar(255)
            public int SysPower { get; set; } // int 储能柜容量规格
            public int SysSelfPower { get; set; } // int
            public string SysAddr { get; set; } // varchar(255)
            public string SysInstTime { get; set; } // datetime
            public int CellCount { get; set; } // int
            public int SysInterval { get; set; } // int
            public int YunInterval { get; set; } // int
            public int IsMaster { get; set; } // 1:主机 0：从机
            public int Master485Addr { get; set; } // int
            public int i485Addr { get; set; } // int
            public int AutoRun { get; set; } // bool
            public int SysMode { get; set; } // int  0手动，1策略，2网控
            public int PCSGridModel { get; set; } // int
            public string DebugComName { get; set; } // varchar(255)
            public int DebugRate { get; set; } // int
            public int SysCount { get; set; } // int
            public int UseYunTactics { get; set; } // bool
            public int UseBalaTactics { get; set; } // bool
            public int iPCSfactory { get; set; } // int
            public int BMSVerb { get; set; } // int   1：协能
            public int PCSForceRun { get; set; } // bool
            public int EMSstatus { get; set; } // bool
            public int GPIOSelect { get; set; }
            public string MasterIp { get; set; }
            public string ConnectStatus { get; set; }

            public int CellVNum { get; set; }
            public int CellTNum { get; set; } //168, 160
            public int BMStype { get; set; }
            public int PcsLimit { get; set; }

            public string MqttBrokerIp { get; set; }
            public int MqttBrokerPort { get; set; } //168, 160
            public string MqttBrokerUser { get; set; }
            public string MqttBrokerPassword { get; set; }
        }

        public class ComponentSettingsClass
        {
            // 空调
            public double SetHotTemp { get; set; }
            public double SetCoolTemp { get; set; }
            public double CoolTempReturn { get; set; }
            public double HotTempReturn { get; set; }
            public double SetHumidity { get; set; }
            public double HumiReturn { get; set; }
            public int TCRunWithSys { get; set; }
            public int TCAuto { get; set; }
            public int TCMode { get; set; }
            public double TCMaxTemp { get; set; }
            public double TCMinTemp { get; set; }
            public double TCMaxHumi { get; set; }
            public double TCMinHumi { get; set; }
            public double FenMaxTemp { get; set; }
            public double FenMinTemp { get; set; }
            public int FenMode { get; set; }

            // 液冷
            public int LCModel { get; set; }
            public int LCTemperSelect { get; set; }
            public int LCWaterPump { get; set; }
            public double LCSetHotTemp { get; set; }
            public double LCSetCoolTemp { get; set; }
            public double LCHotTempReturn { get; set; }
            public double LCCoolTempReturn { get; set; }


            //除湿机
            public int DHSetRunStatus { get; set; }
            public int DHSetTempBoot { get; set; }      //（除湿：温度启动值）dehumidity
            public int DHSetTempStop { get; set; }      //（除湿：温度停止值）
            public int DHSetHumidityBoot { get; set; }  //（除湿：湿度启动值）
            public int DHSetHumidityStop { get; set; }  //（除湿：湿度停止值）
        }

        public class RateTableScheduleItem
        {
            public DateTime RDate { get; set; }
            public int SlotNo { get; set; }   // 1 or 2
        }

        /***********************************
         *
         *  UI  BMS
         *
         * *******************************/

        private void btnBMSRead_Click(object sender, EventArgs e)
        {
            try
            {
                frmMain.Selffrm.AllEquipment.BMS.GetCellErrUPVInfo();
            }
            catch { }
        }

        private void btnBMSRun_Click(object sender, EventArgs e)
        {
            try
            {
                //充电
                frmMain.Selffrm.AllEquipment.BMS.SetBmsPV1(tneBMScellPV1.Value);//BMS1级单体过压报警阈值
                frmMain.Selffrm.AllEquipment.BMS.SetBmsUPV1(tneBMScellUPV1.Value);// BMS1级单体过压恢复阈值
                frmMain.Selffrm.AllEquipment.BMS.SetBmsPV2(tneBMScellPV2.Value);//BMS2级单体过压报警阈值
                frmMain.Selffrm.AllEquipment.BMS.SetBmsUPV2(tneBMScellUPV2.Value);// BMS2级单体过压恢复阈值
                frmMain.Selffrm.AllEquipment.BMS.SetBmsPV3(tneBMScellPV3.Value);//BMS3级单体过压报警阈值
                frmMain.Selffrm.AllEquipment.BMS.SetBmsUPV3(tneBMScellUPV3.Value);// BMS3级单体过压恢复阈值*/

                //放电
                frmMain.Selffrm.AllEquipment.BMS.SetBmsOV1(tneBMScellOV1.Value);//BMS1级单体欠压报警阈值
                frmMain.Selffrm.AllEquipment.BMS.SetBmsUOV1(tneBMScellUOV1.Value);// BMS1级单体欠压恢复阈值
                frmMain.Selffrm.AllEquipment.BMS.SetBmsOV2(tneBMScellOV2.Value);//BMS2级单体欠压报警阈值
                frmMain.Selffrm.AllEquipment.BMS.SetBmsUOV2(tneBMScellUOV2.Value);// BMS2级单体欠压恢复阈值
                frmMain.Selffrm.AllEquipment.BMS.SetBmsOV3(tneBMScellOV3.Value);//BMS3级单体欠压报警阈值
                frmMain.Selffrm.AllEquipment.BMS.SetBmsUOV3(tneBMScellUOV3.Value);// BMS3级单体欠压恢复阈值*/

                //其他参数
                frmSet.cloudLimits.BmsDerateRatio = tneBMSwaValue.Value;
                frmSet.cloudLimits.CellV_Gap = tneBmsBalaDiff.Value;

                frmSet.Set_Cloudlimits();

            }
            catch { }
        }

        private void btnBMSOn_Click(object sender, EventArgs e)
        {
            //开始预充
            frmMain.Selffrm.AllEquipment.BMS.PowerOn(true);
        }


        /*********** 策略更新处理函数  ****************/
        private void RenewTactics()
        {
            if (frmMain.TacticsList.LoadFromMySQL(0))
            {
                frmMain.TacticsList.ActiveIndex = -1;
            }
        }

/*        private void RenewBalaTactics()
        {
            if (frmMain.BalaTacticsList.LoadFromMySQL())
            {
                frmMain.BalaTacticsList.ActiveIndex = -1;
            }

        }*/

        private void RebootEms_Click(object sender, EventArgs e)
        {
            RestartApplication();
        }

        private void tbUpdateEMS_Click(object sender, EventArgs e)
        {
            //frmUpdateEms.ShowForm();
        }

        private void ShowVersion()
        {
            // 获取当前程序集
            Assembly assembly = Assembly.GetExecutingAssembly();

            // 获取版本信息
            Version version = assembly.GetName().Version;
            tbNowVersion.Text = version.ToString();
        }

        private async void btnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbVersion.Text.Trim()))
            {
                MessageBox.Show("版本信息不能为空！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                string versionInput = tbVersion.Text.Trim();
                btnOK.Enabled = false; // 禁用按钮，防止重复点击

                try
                {
                    DialogResult result = MessageBox.Show($"确认更新到版本：{versionInput} 吗？", "确认更新", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        bool isUpdate = await CheckAndUpdateAsync(versionInput);
                        if (isUpdate)
                        {
                            ReStartEMS();
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error("btnOK_ClickAsync: " + ex.Message);
                }
                finally
                {
                    btnOK.Enabled = true; // 恢复按钮
                }
            }
        }

        public static async Task<bool> CheckAndUpdateAsync(string version)
        {
            try
            {
                //判断是否处于策略时段
                string updateUrl = $"https://aiot-data-ems.oss-cn-shanghai.aliyuncs.com/EMS/{version}";
                log.Error("更新文件版本: " + version);

                // 配置远程更新的 URL，指向包含 RELEASES 文件和 .nupkg 文件的服务器路径
                //using (var mgr = new UpdateManager("https://aiot-data-ems.oss-cn-shanghai.aliyuncs.com/EMS/v1.0.0"))
                using (var mgr = new UpdateManager(updateUrl))
                {
                    var updateInfo = await mgr.CheckForUpdate();
                    if (updateInfo.ReleasesToApply.Count > 0)
                    {
                        // 下载和应用更新
                        //mgr.UpdateApp().GetAwaiter().GetResult();
                        await mgr.UpdateApp();
                        log.Error("更新完成，准备重启应用。");

                        MessageBox.Show("更新完成，应用将重启以加载新版本。", "更新成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        // 调用重启逻辑
                        await UpdateManager.RestartAppWhenExited();
                        return true;
                    }
                    else
                    {
                        // 已是最新版本
                        MessageBox.Show("当前已是最新版本，无需更新。", "版本已同步", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return false;
                    }
                }

            }
            catch (Exception ex)
            {
                log.Error("CheckAndUpdateAsync: " + ex.Message);
                MessageBox.Show($"更新失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public static async Task<bool> CheckAndUpdateAsyncNoBox(string version)
        {
            try
            {
                //判断
                frmMain.Selffrm.AllEquipment.EnsureKeepStill();

                if (frmMain.Selffrm.AllEquipment.KeepStill)
                {
                    string updateUrl = $"https://aiot-data-ems.oss-cn-shanghai.aliyuncs.com/EMS/{version}";
                    log.Error("更新文件版本: " + version);

                    // 配置远程更新的 URL，指向包含 RELEASES 文件和 .nupkg 文件的服务器路径
                    //using (var mgr = new UpdateManager("https://aiot-data-ems.oss-cn-shanghai.aliyuncs.com/EMS/v1.0.0"))
                    using (var mgr = new UpdateManager(updateUrl))
                    {
                        var updateInfo = await mgr.CheckForUpdate();
                        if (updateInfo.ReleasesToApply.Count > 0)
                        {
                            // 下载和应用更新
                            //mgr.UpdateApp().GetAwaiter().GetResult();
                            await mgr.UpdateApp();
                            log.Error("更新完成，准备重启应用。");

                            //MessageBox.Show("更新完成，应用将重启以加载新版本。", "更新成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            // 调用重启逻辑
                            await UpdateManager.RestartAppWhenExited();
                            return true;
                        }
                        else
                        {
                            // 已是最新版本
                            //MessageBox.Show("当前已是最新版本，无需更新。", "版本已同步", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return false;
                        }
                    }
                }
                else { return false; }
            }
            catch (Exception ex)
            {
                log.Error("CheckAndUpdateAsync: " + ex.Message);
                //MessageBox.Show($"更新失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void ReStartEMS()
        {
            PowerGPIO(0);
            Set_Cloudlimits();
            if (frmMain.Selffrm.AllEquipment.Led != null)
            {
                frmMain.Selffrm.AllEquipment.Led.Set_Led_ShutDown();
            }
            this.Close();
            frmMain.Selffrm.Close();
        }



        /// <summary>
        /// 初始化所有单行配置表的主键ID（确保每张表至少有一条记录）
        /// 应在程序启动时调用（例如在 frmMain.Load 或 Program.Main 中）
        /// </summary>
        public static bool InitializeSingletonTableIds()
        {
            try
            {
                PeElesticId = EnsureSingleRowExists("PeElestic");
                HistoricalDataId = EnsureSingleRowExists("HistoricalData");
                VariChargeId = EnsureSingleRowExists("VariCharge");
                CloudLimitsId = EnsureSingleRowExists("CloudLimits");
                ComponentSettingsId = EnsureSingleRowExists("ComponentSettings");

                // Config 表特殊：主键是字符串 SysID，不是自增 int id
                // 所以单独处理（见下方 InitializeConfig）
                InitializeConfig();

                return true;
            }
            catch (Exception ex)
            {
                log.Error("InitializeSingletonTableIds failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 确保指定表存在且至少有一条记录，返回其 id（自增主键）
        /// </summary>
        private static int EnsureSingleRowExists(string tableName)
        {
            // 首先检查表中记录数量，确保只有一条数据
            string sqlCount = $"SELECT COUNT(*) FROM {tableName}";
            object countResult = DBConnection.QuerySingleValue(sqlCount);
            int recordCount = Convert.ToInt32(countResult ?? 0);

            if (recordCount == 0)
            {
                // 表为空，记录日志并抛出异常，不自动插入默认数据
                log.Error($"数据缺失：表 {tableName} 中没有找到任何记录，请手动插入初始数据");
                throw new InvalidOperationException($"表 {tableName} 为空，需要手动初始化数据");
            }
            else if (recordCount > 1)
            {
                // 表中数据超过一条，违反单例表约束
                log.Error($"数据异常：表 {tableName} 中存在 {recordCount} 条记录，单例表只能包含一条记录");
                throw new InvalidOperationException($"表 {tableName} 违反单例约束：包含 {recordCount} 条记录，应只有一条");
            }

            // 确认只有一条记录，获取其ID
            string sqlGetId = $"SELECT id FROM {tableName} LIMIT 1";
            object result = DBConnection.QuerySingleValue(sqlGetId);

            if (result != null)
            {
                return Convert.ToInt32(result);
            }

            // 理论上不应该到达这里，但为了安全起见
            log.Error($"数据异常：表 {tableName} 查询ID失败");
            throw new InvalidOperationException($"表 {tableName} ID查询失败");
        }

        /// <summary>
        /// 单独处理 Config 表（主键是 SysID 字符串，非自增 int）
        /// </summary>
        private static void InitializeConfig()
        {
            string sqlCheck = "SELECT COUNT(*) FROM Config;";
            object countObj = DBConnection.QuerySingleValue(sqlCheck);
            int count = Convert.ToInt32(countObj ?? 0);

            if (count == 0)
            {
                // 数据为空，记录日志并抛出异常，不自动插入默认数据
                log.Error("数据缺失：Config 表中没有找到任何记录，请手动插入初始配置数据");
                throw new InvalidOperationException("Config 表为空，需要手动初始化配置数据");
            }
            else if (count > 1)
            {
                // Config表数据超过一条，违反单例表约束
                log.Error($"数据异常：Config 表中存在 {count} 条记录，单例表只能包含一条记录");
                throw new InvalidOperationException($"Config 表违反单例约束：包含 {count} 条记录，应只有一条");
            }

            // 读取 SysID 作为 ConfigId
            object sysIdObj = DBConnection.QuerySingleValue("SELECT SysID FROM Config LIMIT 1;");
            ConfigId = sysIdObj?.ToString() ?? "j0001";
        }

        private void btnSaveUiUpdate_Click(object sender, EventArgs e)
        {
            //统一记录修改数据信息
            SaveUiInstall();

            //统一保存所有配置信息
            Set_Cloudlimits();
            Set_Config();
            Set_ComponentSettings();
        }

        // 关闭系统闭锁
        public static void CloseSystemLock() {
            frmMain.Selffrm.AllEquipment.ErrorState[2] = false;

            frmSet.historyDatas.ErrorState2 = 1;
            frmSet.Set_HistoryData();

            //关闭故障指示灯以及蜂鸣器
            frmSet.ErrorGPIO(0);
        }
    }


}
