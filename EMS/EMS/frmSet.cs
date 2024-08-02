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


namespace EMS
{
    public partial class frmSet : Form
    {
        private delegate void AddoneStep();
        private bool ProgressOn = true;

        //8.8
        private static ILog log = LogManager.GetLogger("frmSet");

        public static frmSet oneForm = null;
        public static CloudLimitClass cloudLimits = new CloudLimitClass();
        public static ConfigClass config = new ConfigClass();
        public static VariChargeClass variCharge = new VariChargeClass();
        public static ComponentSettingsClass componentSettings = new ComponentSettingsClass();
        public static string INIPath = ""; //ini文件的地址和文件名称
        public static string BalaPath = "";
        //public static string SysName;
        //public static string SysID;
        //public static int SysPower;
        //public static int SysSelfPower;
        //public static string SysAddr;
        //public static string SysInstTime;
        //public static string strMemo;
        //public static int CellCount = 240;
        //
        //public static int SysInterval;
        //public static int YunInterval;
        //public static string CloundIP;
        //public static int CloundPort;
        //public static string SystemID1;
        //public static bool IsMaster;
        //public static string MasterIp; //tcp  新主从通讯
        //public static string ConnectStatus;//tcp :选择主从通讯方式
        //public static string MasterID;
        //public static int i485Addr;
        //public static int Master485Addr;
        //public static bool AutoRun;      //自动启动程序
        //public static string FreshTime;
        public static int FreshInterval;
        //public static string ControlIP;
        //public static int ControlPort;
        //public static bool SysAutoRun;
        //public static int SysMode;  //运行模式 ：0：手工模式 1：预设策略 2：网络控制
        //public static int PCSGridModel; //0并网；1离网
        public static string PCSType;
        public static int PCSwaValue;
        //public static double BMSwaValue;//7.24 添加BMS 1级告警时PCS降低的功率值
        //public static int MaxSOC;
        //public static int MinSOC;
        //public static double SetHotTemp; 
        //public static double SetCoolTemp;  
        //public static double HotTempReturn; 
        //public static double CoolTempReturn; 
        //public static double SetHumidity;
        //public static double HumiReturn;
        //public static bool TCRunWithSys;
        //public static bool TCAuto;
        //public static int TCMode;
        //public static double TCMaxTemp;
        //public static double TCMinTemp;
        //public static double TCMaxHumi;
        //public static double TCMinHumi;
        //public static string DebugComName;
        //public static int DebugRate;
        //public static int MaxGridKW;   //用于防超限
        //public static int MinGridKW;   //防止逆流
        //public static int SysCount;
        //public static int BMSVerb;  //质检院老版本为0；定制款为1
        //public static bool PCSForceRun;//强制PCS运行，如电压过低的充电和soc=100的放电

        public static string[] TimeZones = new string[4];
        public static int[] TZSetIndex = { 0, 0, 0, 0 };
        public static int[,] Prices = { { 0, 0, 0, 0, 0 }, { 0, 0, 0, 0, 0 } }; //无尖峰平谷的电价
        //public static bool UseYunTactics;
        //10.21
        //public static bool UseBalaTactics;
        //public static int iPCSfactory = 0;
        //10.25
        //public static int WarnGridkva;  //设置告警电网功率，在电网达到这个功率时充电状态下降低功率或者放电
        //public static int PUM;//需量控制比例


        private bool bTCDataChanged = false;
        private bool bEDataChanged = false;
        public bool bSheduleChanged = false;

        //11.17 液冷配置
        //public static int LCModel ;      //液冷模式设置
        //public static int LCTemperSelect ; //控制温度选择
        //public static int LCWaterPump ;   //水泵档位选择
        //public static double LCSetHotTemp;   //（液冷：水温加热点）
        //public static double LCSetCoolTemp;  //（液冷：水温制冷点）
        //public static double LCHotTempReturn; //（液冷：加热回差）
        //public static double LCCoolTempReturn; // (液冷：制冷回差)

        //11.23 空调补充点位
        //public static double FenMaxTemp ; //风机最高温度
        //public static double FenMinTemp ; //风机最低温度
        //public static int FenMode ;       //外风机工作模式

        //12.4 EMS测试模式（不上传告警信息）
        //public static int EMSstatus ;

        //1.29
        //public static int RestartCounts;//重启次数

        //05.04 除湿机配置
        //public static double DHSetRunStatus;
        //public static double DHSetTempBoot;      //（除湿：温度启动值）dehumidity
        //public static double DHSetTempStop;      //（除湿：温度停止值）
        //public static double DHSetHumidityBoot;  //（除湿：湿度启动值）
        //public static double DHSetHumidityStop;  //（除湿：湿度停止值）


        //05.10 Gpio 判断
        //public static int GPIO_Select_Mode = 0;//Gpio 选择  0：FA  、  1：LA 2:FB
        // 05.13 添加 UBmsPcsState  0BmsPcsState 
        //public static int UBmsPcsState;
        //public static int OBmsPcsState;

        //public static int Open104 = 0; //是否开启104， 1：开启 0：关闭


        private const string strDriveDllName = "SpesTechDriverControl.dll";
        private const string strExeDllName = "SpesTechMmioRW.dll";


        public frmSet()
        {
            InitializeComponent();
            //DBConnection.SetDBGrid(dbgUsers);
            //DBConnection.ShowData2DBGrid(dbgUsers, "select * from users");
            //DBConnection.SetDBGrid(dbgEquipment);
            //DBConnection.ShowData2DBGrid(dbgEquipment, "select * from equipment");
            //DBConnection.SetDBGrid(dbgElectrovalence);
            //DBConnection.ShowData2DBGrid(oneForm.dbgElectrovalence, "select id,section,eName,startTime from electrovalence order by section,startTime");
            //DBConnection.SetDBGrid(dbgTactics);
            //DBConnection.ShowData2DBGrid(oneForm.dbgTactics, "select * from tactics order by starttime");
            //DBConnection.SetDBGrid(dbgLog);
            //DBConnection.ShowData2DBGrid(dbgLog, "select * from log"); 
        }
        static public void INIForm()
        {
            if (oneForm == null)
                oneForm = new frmSet();
        }
        static public void CloseForm()
        {

            if (oneForm != null)
            {
                //oneForm.Dispose();
                //oneForm = null;
                oneForm.Hide();
                frmMain.ShowMainForm();
            }
        }

        static public void ShowForm()
        {
            try
            {
                if (oneForm == null)
                    oneForm = new frmSet();


                //LoadSetInf();
                //LoadFromGlobalSet();
                frmSet.LoadCloudLimitsFromMySQL();
                frmSet.LoadConfigFromMySQL();
                frmSet.LoadComponentSettingsFromMySQL();


                oneForm.ShowINIdata();
                //DBConnection.SetDBGrid(oneForm.dbgLog);
                //DBConnection.ShowData2DBGrid(oneForm.dbgElectrovalence, "select id,section,eName,startTime from electrovalence order by section,startTime");
                //DBConnection.ShowData2DBGrid(oneForm.dbgTactics, "select * from tactics order by starttime");
                //DBConnection.ShowData2DBGrid(oneForm.dbgLog, "select * from log");
                oneForm.btnBaseInf_Click(null, EventArgs.Empty);
                oneForm.bTCDataChanged = false;
                oneForm.bEDataChanged = false;
                oneForm.bSheduleChanged = false;
                oneForm.SetFormPower(frmMain.UserPower);
                oneForm.Show();
                oneForm.BringToFront();
                //oneForm.ShowDialog();
            }
            catch
            {
                oneForm.Dispose();
                oneForm = null;
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

        /********************************************/


        /***CloudLimits***/
        public static bool LoadCloudLimitsFromMySQL()
        {
            bool result = false;
            string astrSQL = "SELECT MaxGridKW, MinGridKW, MaxSOC, MinSOC,  WarnMaxGridKW, WarnMinGridKW, PcsKva, Client_PUMdemand_Max, EnableActiveReduce, PumScale, AllUkvaWindowSize, PumTime, "
                + "BmsDerateRatio, FrigOpenLower, FrigOffLower, FrigOffUpper FROM CloudLimits;";

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
                                cloudLimits.Client_PUMdemand_Max = rd.IsDBNull(7) ? 0 : rd.GetInt32(7);
                                cloudLimits.EnableActiveReduce = rd.IsDBNull(8) ? 0 : rd.GetInt32(8);
                                cloudLimits.PumScale = rd.IsDBNull(9) ? 0 : rd.GetInt32(9);
                                cloudLimits.AllUkvaWindowSize = rd.IsDBNull(10) ? 4 : rd.GetInt32(10);
                                cloudLimits.PumTime = rd.IsDBNull(11) ? 5 : rd.GetInt32(11);
                                cloudLimits.BmsDerateRatio = rd.IsDBNull(12) ? 50 : rd.GetInt32(12);
                                cloudLimits.FrigOpenLower = rd.IsDBNull(13) ? 30 : rd.GetInt32(13);
                                cloudLimits.FrigOffLower = rd.IsDBNull(14) ? 10 : rd.GetInt32(14);
                                cloudLimits.FrigOffUpper = rd.IsDBNull(15) ? 25 : rd.GetInt32(15);

                                result = true;
                            }
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                log.Error(ex.Message);
                result = false;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                result = false;
            }
            finally
            {

                
            }
            return result;
        }


        public static bool Set_Cloudlimits()
        {
            string astrSQL = "update  cloudlimits  SET "
                + " MaxGridKW ='" + frmSet.cloudLimits.MaxGridKW.ToString()
                + "', MinGridKW ='" + frmSet.cloudLimits.MinGridKW.ToString()
                + "',MaxSOC ='" + frmSet.cloudLimits.MaxSOC.ToString()
                + "',MinSOC ='" + frmSet.cloudLimits.MinSOC.ToString()
                + "', WarnMaxGridKW = '" + frmSet.cloudLimits.WarnMaxGridKW.ToString()
                + "', WarnMinGridKW = '" + frmSet.cloudLimits.WarnMinGridKW.ToString()
                + "', PcsKva = '" + frmSet.cloudLimits.PcsKva.ToString()
                + "', Client_PUMdemand_Max = '" + frmSet.cloudLimits.Client_PUMdemand_Max.ToString()
                + "', EnableActiveReduce = '" + frmSet.cloudLimits.EnableActiveReduce.ToString()
                + "', PumScale = '" + frmSet.cloudLimits.PumScale.ToString()
                + "', AllUkvaWindowSize = '" + frmSet.cloudLimits.AllUkvaWindowSize.ToString()
                + "', PumTime = '" + frmSet.cloudLimits.PumTime.ToString()
                + "', BmsDerateRatio = '" + frmSet.cloudLimits.BmsDerateRatio.ToString()
                + "', FrigOpenLower = '" + frmSet.cloudLimits.FrigOpenLower.ToString()
                + "', FrigOffLower = '" + frmSet.cloudLimits.FrigOffLower.ToString()
                + "', FrigOffUpper = '" + frmSet.cloudLimits.FrigOffUpper.ToString()
                + "';";

            bool result = false;

            try
            {
                if (DBConnection.ExecSQL(astrSQL))
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
        }

        /**config***/
        public static bool LoadConfigFromMySQL()
        {
            bool result = false;
            string astrSQL = "SELECT SysID, Open104, NetTick, SysName, SysPower, SysSelfPower, SysAddr, SysInstTime,"
                                + "CellCount, SysInterval, YunInterval, IsMaster, Master485Addr, i485Addr,"
                                + "AutoRun, SysMode, PCSGridModel, DebugComName,"
                                + "DebugRate, SysCount, UseYunTactics, UseBalaTactics, iPCSfactory, BMSVerb, PCSForceRun, "
                                + "EMSstatus, ErrorState2, GPIOSelect, MasterIp, ConnectStatus FROM config; ";
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
                                config.SysID = rd.IsDBNull(0) ? "j0001" : rd.GetString(0);
                                config.Open104 = rd.IsDBNull(1) ? 0 : rd.GetInt32(1);
                                config.NetTick = rd.IsDBNull(2) ? 10 : rd.GetInt32(2);
                                config.SysName = rd.IsDBNull(3) ? "浙江驰库" : rd.GetString(3);
                                config.SysPower = rd.IsDBNull(4) ? 0 : rd.GetInt32(4);
                                config.SysSelfPower = rd.IsDBNull(5) ? 0 : rd.GetInt32(5);
                                config.SysAddr = rd.IsDBNull(6) ? "浙江" : rd.GetString(6);
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
                                config.DebugComName = rd.IsDBNull(17) ? "com7" : rd.GetString(17);
                                config.DebugRate = rd.IsDBNull(18) ? 38400 : rd.GetInt32(18);
                                config.SysCount = rd.IsDBNull(19) ? 1 : rd.GetInt32(19);
                                config.UseYunTactics = rd.IsDBNull(20) ? 0 : rd.GetInt32(20);
                                config.UseBalaTactics = rd.IsDBNull(21) ? 0 : rd.GetInt32(21);
                                config.iPCSfactory = rd.IsDBNull(22) ? 1 : rd.GetInt32(22);
                                config.BMSVerb = rd.IsDBNull(23) ? 0 : rd.GetInt32(23);
                                config.PCSForceRun = rd.IsDBNull(24) ? 0 : rd.GetInt32(24);
                                config.EMSstatus = rd.IsDBNull(25) ? 0 : rd.GetInt32(25);
                                config.ErrorState2 = rd.IsDBNull(26) ? 0 : rd.GetInt32(26);
                                config.GPIOSelect = rd.IsDBNull(27) ? 0 : rd.GetInt32(27);
                                config.MasterIp = rd.IsDBNull(28) ? "192.168.186.9" : rd.GetString(28);
                                config.ConnectStatus = rd.IsDBNull(29) ? "485" : rd.GetString(29);

                                result = true;
                            }
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                log.Error(ex.Message);
                result = false;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                result = false;
            }
            finally
            {

            }

            return result;
        }

        public static bool Set_Config()
        {
            string astrSQL = "UPDATE config SET "
                        + "Open104 = '" + frmSet.config.Open104.ToString()
                        + "', NetTick = '" + frmSet.config.NetTick.ToString()
                        + "', SysName = '" + frmSet.config.SysName
                        + "', SysPower = '" + frmSet.config.SysPower.ToString()
                        + "', SysSelfPower = '" + frmSet.config.SysSelfPower.ToString()
                        + "', SysAddr = '" + frmSet.config.SysAddr
                        + "', SysInstTime = '" + frmSet.config.SysInstTime
                        + "', CellCount = '" + frmSet.config.CellCount.ToString()
                        + "', SysInterval = '" + frmSet.config.SysInterval.ToString()
                        + "', YunInterval = '" + frmSet.config.YunInterval.ToString()
                        + "', IsMaster = '" + frmSet.config.IsMaster.ToString()
                        + "', Master485Addr = '" + frmSet.config.Master485Addr.ToString()
                        + "', i485Addr = '" + frmSet.config.i485Addr.ToString()
                        + "', AutoRun = '" + frmSet.config.AutoRun.ToString()
                        + "', SysMode = '" + frmSet.config.SysMode.ToString()
                        + "', PCSGridModel = '" + frmSet.config.PCSGridModel.ToString()
                        + "', DebugComName = '" + frmSet.config.DebugComName
                        + "', DebugRate = '" + frmSet.config.DebugRate.ToString()
                        + "', SysCount = '" + frmSet.config.SysCount.ToString()
                        + "', iPCSfactory = '" + frmSet.config.iPCSfactory.ToString()
                        + "', BMSVerb = '" + frmSet.config.BMSVerb.ToString()
                        + "', PCSForceRun = '" + frmSet.config.PCSForceRun.ToString()
                        + "', GPIOSelect = '" + frmSet.config.GPIOSelect.ToString()
                        + "', MasterIp = '" + frmSet.config.MasterIp
                        + "', ConnectStatus = '" + frmSet.config.ConnectStatus
                        + "', ErrorState2 = '" + frmSet.config.ErrorState2.ToString()
                        + "', EMSstatus = '" + frmSet.config.EMSstatus.ToString()
                        + "', UseYunTactics = '" + frmSet.config.UseYunTactics.ToString()
                        + "', UseBalaTactics = '" + frmSet.config.UseBalaTactics.ToString()
                        + "' WHERE SysID = '" + frmSet.config.SysID + "';";

            bool result = false;

            try
            {
                if (DBConnection.ExecSQL(astrSQL))
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
                log.Error(ex.Message);
                result = false;
            }
            return result;
        }

        /*******VariCharge*********/
        public static bool LoadVariChargeFromMySQL()
        {
            bool result = false;
            string astrSQL = "SELECT UBmsPcsState, OBmsPcsState FROM VariCharge;";

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
                                variCharge.UBmsPcsState = rd.IsDBNull(19) ? 50 : rd.GetInt32(0);
                                variCharge.OBmsPcsState = rd.IsDBNull(19) ? 50 : rd.GetInt32(1);
                                result = true;
                            }
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                log.Error(ex.Message);
                result = false;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                result = false;
            }
            finally
            {

            }

            return result;
        }

        public static bool Set_VariCharge()
        {
            string astrSQL = "update  VariCharge  SET "
                + " UBmsPcsState ='" + frmSet.variCharge.UBmsPcsState.ToString()
                + "', OBmsPcsState ='" + frmSet.variCharge.OBmsPcsState.ToString()
                + "';";

            bool result = false;

            try
            {
                if (DBConnection.ExecSQL(astrSQL))
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
                log.Error (ex.Message);
                result = false;
            }
            return result;

        }

        /*******Component*********/
        public static bool LoadComponentSettingsFromMySQL()
        {
            bool result = false;
            string astrSQL = @"
                    SELECT SetHotTemp, SetCoolTemp, CoolTempReturn, HotTempReturn, SetHumidity, HumiReturn, 
                           TCRunWithSys, TCAuto, TCMode, TCMaxTemp, TCMinTemp, TCMaxHumi, TCMinHumi, 
                           FenMaxTemp, FenMinTemp, FenMode, LCModel, LCTemperSelect, LCWaterPump, 
                           LCSetHotTemp, LCSetCoolTemp, LCHotTempReturn, LCCoolTempReturn , DHSetRunStatus, DHSetTempBoot, DHSetTempStop, DHSetHumidityBoot, DHSetHumidityStop
                    FROM ComponentSettings;";

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

                                result = true;
                            }
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                result = false;
                log.Error(ex.Message);
            }
            catch (Exception ex)
            {
                result = false;
                log.Error(ex.Message);
            }
            finally
            {

            }

            return result;
        }


        public static bool Set_ComponentSettings()
        {
            string astrSQL = "UPDATE ComponentSettings SET "
                + "SetHotTemp = '" + componentSettings.SetHotTemp.ToString() + "', "
                + "SetCoolTemp = '" + componentSettings.SetCoolTemp.ToString() + "', "
                + "CoolTempReturn = '" + componentSettings.CoolTempReturn.ToString() + "', "
                + "HotTempReturn = '" + componentSettings.HotTempReturn.ToString() + "', "
                + "SetHumidity = '" + componentSettings.SetHumidity.ToString() + "', "
                + "HumiReturn = '" + componentSettings.HumiReturn.ToString() + "', "
                + "TCRunWithSys = '" + componentSettings.TCRunWithSys.ToString() + "', "
                + "TCAuto = '" + componentSettings.TCAuto.ToString() + "', "
                + "TCMode = '" + componentSettings.TCMode.ToString() + "', "
                + "TCMaxTemp = '" + componentSettings.TCMaxTemp.ToString() + "', "
                + "TCMinTemp = '" + componentSettings.TCMinTemp.ToString() + "', "
                + "TCMaxHumi = '" + componentSettings.TCMaxHumi.ToString() + "', "
                + "TCMinHumi = '" + componentSettings.TCMinHumi.ToString() + "', "
                + "FenMaxTemp = '" + componentSettings.FenMaxTemp.ToString() + "', "
                + "FenMinTemp = '" + componentSettings.FenMinTemp.ToString() + "', "
                + "FenMode = '" + componentSettings.FenMode.ToString() + "', "
                + "LCModel = '" + componentSettings.LCModel.ToString() + "', "
                + "LCTemperSelect = '" + componentSettings.LCTemperSelect.ToString() + "', "
                + "LCWaterPump = '" + componentSettings.LCWaterPump.ToString() + "', "
                + "LCSetHotTemp = '" + componentSettings.LCSetHotTemp.ToString() + "', "
                + "LCSetCoolTemp = '" + componentSettings.LCSetCoolTemp.ToString() + "', "
                + "LCHotTempReturn = '" + componentSettings.LCHotTempReturn.ToString() + "', "
                + "LCCoolTempReturn = '" + componentSettings.LCCoolTempReturn.ToString() + "', "
                + "DHSetRunStatus = '" + componentSettings.DHSetRunStatus.ToString() + "', "
                + "DHSetTempBoot = '" + componentSettings.DHSetTempBoot.ToString() + "', "
                + "DHSetTempStop = '" + componentSettings.DHSetTempStop.ToString() + "', "
                + "DHSetHumidityBoot = '" + componentSettings.DHSetHumidityBoot.ToString() + "', "
                + "DHSetHumidityStop = '" + componentSettings.DHSetHumidityStop.ToString()
                + "';";

            bool result = false;
            try
            {
                if (DBConnection.ExecSQL(astrSQL))
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
                log.Error(ex.Message);
                result = false;
            }
            return result;

        }

        /**************************************************************************************************************************************/


        public List<ModbusCommand> VersionList = new List<ModbusCommand>(); //从由协议转义的TXT文本获取command的相关信息，如寄存器地址，功能码，字节大小等


        public static UInt32[] GPOIAddr ={   //原版
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

        public static UInt32[] GPOIAddr2 ={  //修改版
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

        static private IntPtr hDriver;

        //GPIO初始化
        public static void InitGPIO()
        {
            switch (config.GPIOSelect)
            {
                case 0:
                    frmSet.Init0_GPIO();
                    frmSet.SetGPIOState(0, 3);  //急停
                    frmSet.SetGPIOState(1, 3);  //消防
                    frmSet.SetGPIOState(2, 3);  
                    frmSet.SetGPIOState(3, 3);
                    frmSet.SetGPIOState(4, 3);
                    frmSet.SetGPIOState(5, 3);
                    frmSet.SetGPIOState(6, 3);
                    frmSet.SetGPIOState(7, 3);
                    //
                    frmSet.SetGPIOState(8, 1);   //24V on(powerOn)
                    frmSet.SetGPIOState(9, 1);   //PCS On
                    frmSet.SetGPIOState(10, 1);  //2 error
                    frmSet.SetGPIOState(11, 1); //3 error
                  //frmSet.SetGPIOState(12, 1);
                    frmSet.SetGPIOState(15, 1);//EMS LED
                    break;
                case 1:
                    frmSet.Init1_GPIO();
                    frmSet.SetGPIOState(0, 2);//消防
                    frmSet.SetGPIOState(1, 2);//急停
                    frmSet.SetGPIOState(2, 2);//门禁
                    frmSet.SetGPIOState(3, 2);
                    frmSet.SetGPIOState(4, 2);
                    frmSet.SetGPIOState(5, 2);
                    frmSet.SetGPIOState(6, 2);
                    frmSet.SetGPIOState(7, 2);
                    //
                    frmSet.SetGPIOState(8, 0);   //24V on(powerOn)
                    frmSet.SetGPIOState(9, 0);   //PCS On
                    frmSet.SetGPIOState(10, 0);  //2 error
                    frmSet.SetGPIOState(11, 0); //3 error
                    frmSet.SetGPIOState(12, 0);
                    frmSet.SetGPIOState(13, 0);
                    frmSet.SetGPIOState(14, 1);
                    frmSet.SetGPIOState(15, 0);//EMS LED
                    break;
                case 2:
                    frmSet.Init2_GPIO();
                    frmSet.SetGPIOState(0, 2);//消防
                    frmSet.SetGPIOState(1, 2);//急停
                    frmSet.SetGPIOState(2, 2);
                    frmSet.SetGPIOState(3, 2);
                    frmSet.SetGPIOState(4, 2);
                    frmSet.SetGPIOState(5, 2);
                    frmSet.SetGPIOState(6, 2);
                    frmSet.SetGPIOState(7, 2);
                    //
                    frmSet.SetGPIOState(8, 1);   //24V on(powerOn)
                    frmSet.SetGPIOState(9, 0);   //PCS On
                    frmSet.SetGPIOState(10, 0);  //2 error
                    frmSet.SetGPIOState(11, 0); //3 error
                    frmSet.SetGPIOState(12, 0);
                    frmSet.SetGPIOState(13, 0);
                    frmSet.SetGPIOState(14, 0);
                    frmSet.SetGPIOState(15, 1);//EMS LED
                    break;
            }
        }
        public static void Init0_GPIO()  //原版未修改 FA
        {
            {
                if (hDriver == IntPtr.Zero)
                {
                    IntPtr hDriver = InitializeWinIo();//打开 //初始化dll和driver
                                                       //frmMain.ShowDebugMSG("GPIO初始化失败"); 
                }
                if (hDriver == IntPtr.Zero)
                {
                    return;
                }
                try
                {
                    //前8个为输入，后8个输出 02out,0200 0201
                    ////01in   --0100   0102
                    //配置成输入：BIT[2, 1, 0] = Value[0, 1, x]   = 2
                    //配置成输出高：BIT[2, 1, 0] = Value[0, 0, 1] = 1
                    //配置成输出低：BIT[2, 1, 0] = Value[0, 0, 0] = 0
                    SysIO.SetPhysLong(hDriver, GPOIAddr[0], 2);//写入  消防出点
                    SysIO.SetPhysLong(hDriver, GPOIAddr[1], 2);//写入  紧急停机
                    SysIO.SetPhysLong(hDriver, GPOIAddr[2], 2);//写入  预留 断路器
                    SysIO.SetPhysLong(hDriver, GPOIAddr[3], 2);//写入  预留 断路器
                    SysIO.SetPhysLong(hDriver, GPOIAddr[4], 2);//写入  预留 门禁系统
                    SysIO.SetPhysLong(hDriver, GPOIAddr[5], 3);//写入  UPS
                    SysIO. SetPhysLong(hDriver,GPOIAddr[6], 3);//写入  市电
                    SysIO.SetPhysLong(hDriver, GPOIAddr[7], 2);//写入 
                                                               ////////////////////////////////////////////////////////
                                                               //输出
                    SysIO.SetPhysLong(hDriver, GPOIAddr[8] , 0);//写出  电源指示灯
                    SysIO.SetPhysLong(hDriver, GPOIAddr[9] , 1);//写出  运行指示灯，充放电点亮 
                    SysIO.SetPhysLong(hDriver, GPOIAddr[10], 1);//写出  一般故障1、2级不影响工作
                    SysIO.SetPhysLong(hDriver, GPOIAddr[11], 1);//写出  综合控制箱风机控制 
                    SysIO.SetPhysLong(hDriver, GPOIAddr[12], 1);//输出 
                    SysIO.SetPhysLong(hDriver, GPOIAddr[13], 1);//输出  
                    SysIO.SetPhysLong(hDriver, GPOIAddr[14], 1);//输出   预留 主动消防控制
                    SysIO.SetPhysLong(hDriver, GPOIAddr[15], 1);//输出 严重故障、导致不可恢复的停机 

                }
                catch { }
                //ShutdownWinIo(hDriver);//关闭
            }
        }

        public static void Init1_GPIO()   //新版-功能取反
        {
            {
                if (hDriver == IntPtr.Zero)
                {
                    IntPtr hDriver = InitializeWinIo();//打开 //初始化dll和driver
                                                       //frmMain.ShowDebugMSG("GPIO初始化失败"); 
                }
                if (hDriver == IntPtr.Zero)
                {
                    return;
                }
                try
                {
                    //前8个为输入，后8个输出 02out,0200 0201
                    ////01in   --0100   0102
                    //配置成输入：BIT[2, 1, 0] = Value[0, 1, x]   = 2
                    //配置成输出高：BIT[2, 1, 0] = Value[0, 0, 1] = 1
                    //配置成输出低：BIT[2, 1, 0] = Value[0, 0, 0] = 0
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[0], 3);//写入  消防出点
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[1], 3);//写入  紧急停机
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[2], 3);//写入  门禁系统
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[3], 3);//写入  消防反馈2
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[4], 3);//写入  市电
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[5], 2);//写入  UPS
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[6], 2);//写入  
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[7], 3);//写入 
                                                               ////////////////////////////////////////////////////////
                                                               //输出
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[8], 0);//写出  
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[9], 0);//写出  
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[10], 0);//写出  
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[11], 0);//写出  
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[12], 0);//输出  KA5-主动消防
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[13], 0);//输出  KA6-泄爆阀
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[14], 1);//输出  电源指示灯 预留 主动消防控制
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[15], 0);//

                }
                catch { }
                //ShutdownWinIo(hDriver);//关闭
            }
        }

        public static void Init2_GPIO()   //新风冷 FB
        {
            {
                if (hDriver == IntPtr.Zero)
                {
                    IntPtr hDriver = InitializeWinIo();//打开 //初始化dll和driver
                                                       //frmMain.ShowDebugMSG("GPIO初始化失败"); 
                }
                if (hDriver == IntPtr.Zero)
                {
                    return;
                }
                try
                {
                    //前8个为输入，后8个输出 02out,0200 0201
                    ////01in   --0100   0102
                    //配置成输入：BIT[2, 1, 0] = Value[0, 1, x]   = 2
                    //配置成输出高：BIT[2, 1, 0] = Value[0, 0, 1] = 1
                    //配置成输出低：BIT[2, 1, 0] = Value[0, 0, 0] = 0
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[0], 3);//写入  消防反馈
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[1], 3);//写入  紧急停机
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[2], 3);//写入  脱扣反馈1
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[3], 3);//写入  消防反馈2
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[4], 3);//写入  市电
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[5], 3);//写入  门禁
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[6], 3);//写入  反馈
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[7], 3);//写入 

                    SysIO.SetPhysLong(hDriver, GPOIAddr2[8],  1);//输出  电源指示
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[9],  0);//输出  运行指示
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[10], 0);//输出  告警指示
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[11], 0);//输出  故障指示
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[12], 0);//输出  
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[13], 0);//输出  分励脱扣
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[14], 0);//输出  
                    SysIO.SetPhysLong(hDriver, GPOIAddr2[15], 1);//输出  电源指示

                }
                catch { }
                //ShutdownWinIo(hDriver);//关闭
            }
        }

        public static void GPIOClose()
        {
            if (hDriver != IntPtr.Zero)
            {
                ShutdownWinIo(hDriver);//关闭
            }

        }


        /// <summary>
        /// 获取一个GPIO的输入值 ：0输出低电平，1输出高高电平，2输入低电平，3输入高电平
        /// </summary>
        /// <param name="aIndex"></param>
        /// <returns></returns>
        public static UInt32 GetGPIOState(int aIndex)
        {
            UInt32 uiBack = 0;
            //IntPtr hDriver = InitializeWinIo();//打开 //初始化dll和driver

            if (hDriver == IntPtr.Zero)
            {
                IntPtr hDriver = InitializeWinIo();//打开 //初始化dll和driver
                //frmMain.ShowDebugMSG("GPIO初始化失败"); 
            }
            if (hDriver == IntPtr.Zero)
            {
                return 0;
            }
            try
            {
                //if ((aIndex > 15)||(aIndex<0))
                //    return uiBack;
                //  IntPtr hDriver = InitializeWinIo();//打开 //初始化dll和driver
                // if (hDriver == IntPtr.Zero)
                //     bResult= false;

                GetPhysLong(hDriver, GPOIAddr[aIndex], out uiBack);//读取 //读取一个byte的值 
            }
            catch
            { }
            //ShutdownWinIo(hDriver);//关闭
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
            bool bResult = true;
            // if ((aIndex > 15) || (aIndex < 0))
            //    return false;
            //IntPtr hDriver = InitializeWinIo();//打开 //初始化dll和driver

            if (hDriver == IntPtr.Zero)
            {
                hDriver = InitializeWinIo();//打开 //初始化dll和driver 
            }
            if (hDriver == IntPtr.Zero)
            {
                return false;
            }
            SetPhysLong(hDriver, GPOIAddr[aIndex], aOn);//设置一个byte的值 
                                                        //  ShutdownWinIo(hDriver);//关闭
            return bResult;
        }


        public static void BMS2warningGPIO(int option) 
        {
            if(option == 0)
            switch (config.GPIOSelect)
            {
                case 0:
                    frmSet.SetGPIOState(10, 1);
                    break;
                case 1:
                   // frmSet.SetGPIOState(10, 1);
                    break;
                case 2:
                    frmSet.SetGPIOState(10, 0);
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
        public static void ErrorGPIO(int option)
        {
            if (option == 0)
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
            else
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

        public static void PowerGPIO(int option)
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



        //读取设置文件
        public static void LoadSetInf()
        {
/*            try
            {
                INIFile ConfigINI = new INIFile();
                //SysName = ConfigINI.INIRead("System Set", "SysName", "公司测试", INIPath);
                SysID = ConfigINI.INIRead("System Set", "SysID", "CKCGP200A00001", INIPath);
                SysPower = Convert.ToInt32(ConfigINI.INIRead("System Set", "SysPower", "200", INIPath));
                SysSelfPower = Convert.ToInt32(ConfigINI.INIRead("System Set", "SysSelfPower", "200", INIPath));
                SysAddr = ConfigINI.INIRead("System Set", "SysAddr", "浙江嘉兴", INIPath);
                SysInstTime = ConfigINI.INIRead("System Set", "SysInstTime", "2022-11-22", INIPath);
                strMemo = ConfigINI.INIRead("System Set", "strMemo", "", INIPath);
                CellCount = Convert.ToInt32(ConfigINI.INIRead("System Set", "CellCount", "240", INIPath));
                //
                SysInterval = Convert.ToInt32(ConfigINI.INIRead("System Set", "SysInterval", "5", INIPath));
                YunInterval = Convert.ToInt32(ConfigINI.INIRead("System Set", "YunInterval", "30", INIPath));
                CloundIP = ConfigINI.INIRead("System Set", "YunIP", "192.168.1.100", INIPath);
                CloundPort = Convert.ToInt32(ConfigINI.INIRead("System Set", "YunPort", "10000", INIPath));
                SystemID1 = ConfigINI.INIRead("System Set", "SysID", "CKCGP200A00001", INIPath);
                IsMaster = Convert.ToBoolean(ConfigINI.INIRead("System Set", "IsMaster", "true", INIPath));
                if (IsMaster)
                {
                    Master485Addr = 1;
                    i485Addr = 1;
                }
                else
                {
                    Master485Addr = Convert.ToInt32(ConfigINI.INIRead("System Set", "Master485Addr", "1", INIPath));
                    i485Addr = Convert.ToInt32(ConfigINI.INIRead("System Set", "i485Addr", "1", INIPath));
                }
                MasterIp = ConfigINI.INIRead("System Set", "MasterIp", "1", INIPath);//tcp
                ConnectStatus = ConfigINI.INIRead("System Set", "ConnectStatus", "485", INIPath);//tcp
                MasterID = ConfigINI.INIRead("System Set", "MasterID", "1", INIPath);
                AutoRun = Convert.ToBoolean(ConfigINI.INIRead("System Set", "AutoRun", "false", INIPath));
                FreshTime = ConfigINI.INIRead("System Set", "FreshTime", "00:00:01", INIPath);
                FreshInterval = Convert.ToInt32(ConfigINI.INIRead("System Set", "FreshInterval", "24", INIPath));
                ControlIP = ConfigINI.INIRead("System Set", "ControlIP", "10.0.0.1", INIPath);
                ControlPort = Convert.ToInt32(ConfigINI.INIRead("System Set", "ControlPort", "9999", INIPath));

                SysAutoRun = false;// Convert.ToBoolean(ConfigINI.INIRead("System Set", "SysAutoRun", "false", INIPath));
                SysMode = Convert.ToInt32(ConfigINI.INIRead("System Set", "SysMode", "0", INIPath));
                PCSGridModel = Convert.ToInt32(ConfigINI.INIRead("System Set", "PCSGridModel", "0", INIPath));
                PCSType = ConfigINI.INIRead("System Set", "PCSType", "0", INIPath);
                PCSwaValue = Convert.ToInt32(ConfigINI.INIRead("System Set", "PCSwaValue", "100", INIPath));
                BMSwaValue = Convert.ToDouble(ConfigINI.INIRead("System Set", "BMSwaValue", "100", INIPath));//7.24
                MaxSOC = Convert.ToInt32(ConfigINI.INIRead("System Set", "MaxSOC", "100", INIPath));
                MinSOC = Convert.ToInt32(ConfigINI.INIRead("System Set", "MinSOC", "5", INIPath));
                SetHotTemp = Convert.ToDouble(ConfigINI.INIRead("System Set", "SetHotTemp", "15", INIPath));
                SetCoolTemp = Convert.ToDouble(ConfigINI.INIRead("System Set", "SetCoolTemp", "29", INIPath));
                CoolTempReturn = Convert.ToDouble(ConfigINI.INIRead("System Set", "CoolTempReturn", "20", INIPath));
                HotTempReturn = Convert.ToDouble(ConfigINI.INIRead("System Set", "HotTempReturn", "20", INIPath));
                SetHumidity = Convert.ToDouble(ConfigINI.INIRead("System Set", "SetHumidity", "50", INIPath));
                HumiReturn = Convert.ToDouble(ConfigINI.INIRead("System Set", "HumiReturn", "10", INIPath));
                TCRunWithSys = Convert.ToBoolean(ConfigINI.INIRead("System Set", "TCRunWithSys", "false", INIPath));
                TCAuto = Convert.ToBoolean(ConfigINI.INIRead("System Set", "TCAuto", "false", INIPath));
                TCMode = Convert.ToInt32(ConfigINI.INIRead("System Set", "TCMode", "0", INIPath));
                TCMaxTemp = Convert.ToDouble(ConfigINI.INIRead("System Set", "TCMaxTemp", "40", INIPath));
                TCMinTemp = Convert.ToDouble(ConfigINI.INIRead("System Set", "TCMinTemp", "0", INIPath));
                TCMaxHumi = Convert.ToDouble(ConfigINI.INIRead("System Set", "TCMaxHumi", "1", INIPath));
                TCMinHumi = Convert.ToDouble(ConfigINI.INIRead("System Set", "TCMinHumi", "900", INIPath));
                DebugComName = ConfigINI.INIRead("System Set", "DebugComName", "Com1", INIPath);
                DebugRate = Convert.ToInt32(ConfigINI.INIRead("System Set", "DebugRate", "115200", INIPath));
                MaxGridKW = Convert.ToInt32(ConfigINI.INIRead("System Set", "MaxGridKWH", "2700", INIPath));
                MinGridKW = Convert.ToInt32(ConfigINI.INIRead("System Set", "MinGridKWH", "300", INIPath));
                for (int i = 0; i < 4; i++)
                {
                    TimeZones[i] = ConfigINI.INIRead("System Set", "TimeZones" + i.ToString(), "01-01 ", INIPath);
                    //TimeZones[2 * i+1] = ConfigINI.INIRead("System Set", "TimeZones" + (2 * i+1).ToString(), "01-01", INIPath);
                    TZSetIndex[i] = Convert.ToInt32(ConfigINI.INIRead("System Set", "TZSetIndex" + (i).ToString(), "0", INIPath));
                    Prices[0, i] = Convert.ToInt32(ConfigINI.INIRead("System Set", "Prices0" + (i).ToString(), "0", INIPath));//无尖峰平谷 
                    Prices[1, i] = Convert.ToInt32(ConfigINI.INIRead("System Set", "Prices1" + (i).ToString(), "0", INIPath));//无尖峰平谷 
                }
                Prices[0, 4] = Convert.ToInt32(ConfigINI.INIRead("System Set", "Prices04", "0", INIPath));//无尖峰平谷 
                Prices[1, 4] = Convert.ToInt32(ConfigINI.INIRead("System Set", "Prices14", "0", INIPath));//无尖峰平谷 
                SysCount = Convert.ToInt32(ConfigINI.INIRead("System Set", "SysCount", "1", INIPath));
                UseYunTactics = Convert.ToBoolean(ConfigINI.INIRead("System Set", "UseYunTactics", "False", INIPath));
                UseBalaTactics = Convert.ToBoolean(ConfigINI.INIRead("System Set", "UseBalaTactics", "False", INIPath));
                iPCSfactory = Convert.ToInt32(ConfigINI.INIRead("System Set", "iPCSfactory", "0", INIPath));
                BMSVerb = Convert.ToInt32(ConfigINI.INIRead("System Set", "BMSVerb", "0", INIPath));
                PCSForceRun= Convert.ToBoolean(ConfigINI.INIRead("System Set", "PCSForceRun", "false", INIPath));
                //10.25
                WarnGridkva=Convert.ToInt32(ConfigINI.INIRead("System Set", "WarnGridkva", "2700", INIPath));
                //11.13
                PUM = Convert.ToInt32(ConfigINI.INIRead("System Set", "PUM", "85", INIPath));

                //液冷
                LCModel = Convert.ToInt32(ConfigINI.INIRead("System Set", "LCModel", "4", INIPath));      //全自动
                LCTemperSelect = Convert.ToInt32(ConfigINI.INIRead("System Set", "LCTemperSelect", "1", INIPath)); //出水温度
                LCWaterPump = Convert.ToInt32(ConfigINI.INIRead("System Set", "LCWaterPump", "0", INIPath));  //默认档
                LCSetHotTemp  = Convert.ToInt32(ConfigINI.INIRead("System Set", "LCSetHotTemp", "200", INIPath));  //20°C
                LCSetCoolTemp = Convert.ToInt32(ConfigINI.INIRead("System Set", "LCSetCoolTemp", "200", INIPath)); //20°C
                LCHotTempReturn = Convert.ToInt32(ConfigINI.INIRead("System Set", "LCHotTempReturn", "20", INIPath));  //2°C
                LCCoolTempReturn = Convert.ToInt32(ConfigINI.INIRead("System Set", "LCCoolTempReturn", "20", INIPath)); //2°C

                //11.23 空调补充点位
                FenMaxTemp = Convert.ToInt32(ConfigINI.INIRead("System Set", "FenMaxTemp", "200", INIPath));
                FenMinTemp = Convert.ToInt32(ConfigINI.INIRead("System Set", "FenMinTemp", "200", INIPath));
                FenMode = Convert.ToInt32(ConfigINI.INIRead("System Set", "FenMode", "2", INIPath));

                //12.4
                EMSstatus =  Convert.ToInt32(ConfigINI.INIRead("System Set", "EMSstatus", "0", INIPath)); //默认测试模式

                //1.16
                frmMain.Selffrm.AllEquipment.ErrorState[2] = Convert.ToBoolean(ConfigINI.INIRead("System Set", "ErrorState3", "false", INIPath));
                //1.29
                RestartCounts = Convert.ToInt32(ConfigINI.INIRead("System Set", "RestartCounts", "0", INIPath));

                GPIO_Select_Mode = Convert.ToInt32(ConfigINI.INIRead("System Set", "GPIOSelect", "0", INIPath));
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
*/

        }


        //保存数据到INI文件
        public static void SaveSet2File()
        {
 /*           INIFile ConfigINI = new INIFile();
            //ConfigINI.INIWrite("", "key", "value", INIPath);
            //ConfigINI.INIWrite("System Set", "SysName", SysName, INIPath);
            ConfigINI.INIWrite("System Set", "SysID", SysID, INIPath);
            ConfigINI.INIWrite("System Set", "SysPower", SysPower.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "SysSelfPower", SysSelfPower.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "SysAddr", SysAddr, INIPath);
            ConfigINI.INIWrite("System Set", "SysInstTime", SysInstTime, INIPath);
            ConfigINI.INIWrite("System Set", "strMemo", strMemo, INIPath);
            ConfigINI.INIWrite("System Set", "CellCount", CellCount.ToString(), INIPath);

            //
            ConfigINI.INIWrite("System Set", "SysInterval", SysInterval.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "YunInterval", YunInterval.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "YunIP", CloundIP, INIPath);
            ConfigINI.INIWrite("System Set", "YunPort", CloundPort.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "SysID", SysID, INIPath);
            ConfigINI.INIWrite("System Set", "IsMaster", IsMaster.ToString(), INIPath);
            if (IsMaster)
            {
                ConfigINI.INIWrite("System Set", "i485Addr", "1", INIPath);
                ConfigINI.INIWrite("System Set", "Master485Addr", "1", INIPath);
            }
            else
            {
                ConfigINI.INIWrite("System Set", "i485Addr", i485Addr.ToString(), INIPath);
                ConfigINI.INIWrite("System Set", "Master485Addr", Master485Addr.ToString(), INIPath);
            }
            ConfigINI.INIWrite("System Set", "MasterIp", MasterIp, INIPath);//tcp 
            ConfigINI.INIWrite("System Set", "ConnectStatus", ConnectStatus, INIPath);
            ConfigINI.INIWrite("System Set", "MasterID", MasterID, INIPath);
            ConfigINI.INIWrite("System Set", "AutoRun", AutoRun.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "FreshTime", FreshTime.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "FreshInterval", FreshInterval.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "ControlIP", ControlIP, INIPath);
            ConfigINI.INIWrite("System Set", "ControlPort", ControlPort.ToString(), INIPath);

            ConfigINI.INIWrite("System Set", "SysAutoRun", SysAutoRun.ToString(), INIPath); ;
            ConfigINI.INIWrite("System Set", "SysMode", SysMode.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "PCSGridModel", PCSGridModel.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "PCSType", PCSType, INIPath);
            ConfigINI.INIWrite("System Set", "PCSwaValue", PCSwaValue.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "BMSwaValue", BMSwaValue.ToString(), INIPath);//7.24
            ConfigINI.INIWrite("System Set", "MaxSOC", MaxSOC.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "MinSOC", MinSOC.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "SetHotTemp", SetHotTemp.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "SetCoolTemp", SetCoolTemp.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "CoolTempReturn", CoolTempReturn.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "HotTempReturn", HotTempReturn.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "SetHumidity", SetHumidity.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "HumiReturn", HumiReturn.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "TCRunWithSys", TCRunWithSys.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "TCAuto", TCAuto.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "TCMode", TCMode.ToString(), INIPath);


            ConfigINI.INIWrite("System Set", "TCMaxTemp", TCMaxTemp.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "TCMinTemp", TCMinTemp.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "TCMaxHumi", TCMaxHumi.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "TCMinHumi", TCMinHumi.ToString(), INIPath);

            ConfigINI.INIWrite("System Set", "DebugComName", DebugComName, INIPath);
            ConfigINI.INIWrite("System Set", "DebugRate", DebugRate.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "MaxGridKWH", MaxGridKW.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "MinGridKWH", MinGridKW.ToString(), INIPath);
            for (int i = 0; i < 4; i++)
            {
                ConfigINI.INIWrite("System Set", "TimeZones" + (i).ToString(), TimeZones[i], INIPath);
                //ConfigINI.INIWrite("System Set", "TimeZones" + (2 * i + 1).ToString(), TimeZones[2 * i+1], INIPath);
                ConfigINI.INIWrite("System Set", "TZSetIndex" + (i).ToString(), TZSetIndex[i].ToString(), INIPath);
                ConfigINI.INIWrite("System Set", "Prices0" + (i).ToString(), Prices[0, i].ToString(), INIPath);//无尖峰平谷 
                ConfigINI.INIWrite("System Set", "Prices1" + (i).ToString(), Prices[1, i].ToString(), INIPath);//无尖峰平谷 
            }
            ConfigINI.INIWrite("System Set", "Prices04", Prices[0, 4].ToString(), INIPath);//无尖峰平谷 
            ConfigINI.INIWrite("System Set", "Prices14", Prices[1, 4].ToString(), INIPath);//无尖峰平谷
            ConfigINI.INIWrite("System Set", "SysCount", SysCount.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "UseYunTactics", UseYunTactics.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "UseBalaTactics", UseBalaTactics.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "iPCSfactory", iPCSfactory.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "BMSVerb", BMSVerb.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "PCSForceRun", PCSForceRun.ToString(), INIPath);
            //10.25
            ConfigINI.INIWrite("System Set", "WarnGridkva", WarnGridkva.ToString(), INIPath);
            //11.13
            ConfigINI.INIWrite("System Set", "PUM", PUM.ToString(), INIPath);

            //液冷
            ConfigINI.INIWrite("System Set", "LCModel", LCModel.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "LCTemperSelect", LCTemperSelect.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "LCWaterPump", LCWaterPump.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "LCSetHotTemp", LCSetHotTemp.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "LCSetCoolTemp", LCSetCoolTemp.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "LCHotTempReturn", LCHotTempReturn.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "LCCoolTempReturn", LCCoolTempReturn.ToString(), INIPath);

            //11.23空调点位添加
            ConfigINI.INIWrite("System Set", "FenMaxTemp", FenMaxTemp.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "FenMinTemp", FenMinTemp.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "FenMode", FenMode.ToString(), INIPath);

            //12.4
            ConfigINI.INIWrite("System Set", "EMSstatus", EMSstatus.ToString(), INIPath);

            //1.16
            ConfigINI.INIWrite("System Set", "ErrorState3", frmMain.Selffrm.AllEquipment.ErrorState[2].ToString(), INIPath);
            //1.29
            ConfigINI.INIWrite("System Set", "RestartCounts", RestartCounts.ToString(), INIPath);

            //5.05 除湿器
            ConfigINI.INIWrite("System Set", "DHSetRunStatus", DHSetRunStatus.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "DHSetTempBoot", DHSetTempBoot.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "DHSetTempStop", DHSetTempStop.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "DHSetHumidityBoot", DHSetHumidityBoot.ToString(), INIPath);
            ConfigINI.INIWrite("System Set", "DHSetHumidityStop", DHSetHumidityStop.ToString(), INIPath);*/
        }

        public bool PutTchCheck(int input)
        {
            bool result ;
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
                //dtpFreshTime.Value = Convert.ToDateTime(FreshTime);
                //tneFreshInterval.SetIntValue(FreshInterval);
                //tcbControlIP.SetstrText(ControlIP);
                //tneControlPort.SetIntValue(ControlPort);
                //cbCloundIP.Text = CloundIP;
                //nudCloundPort.Value = CloundPort;
                //tcbSysAutoRun.SetValue( SysAutoRun);
                /*                tcbSYSModel.SetSelectItemIndex(config.SysMode);
                                tcbPCSGridModel.SetSelectItemIndex(config.PCSGridModel);
                                tcbPCSType.SetstrText(PCSType);
                                if (PCSwaValue > 0)
                                    tcbPCSMode.SetSelectItemIndex(0);
                                else
                                    tcbPCSMode.SetSelectItemIndex(1);
                                tnePCSwaValue.SetIntValue(Math.Abs(PCSwaValue));*/
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
                //DateTime dtTemp = Convert.ToDateTime("2022-" + TimeZones[0] + " 0:0:1");
                //tneFM0.SetIntValue(dtTemp.Month);
                //tneFD0.SetIntValue(dtTemp.Day);
                //tneTZIndex0.SetIntValue(TZSetIndex[0]);
                //dtTemp = Convert.ToDateTime("2022-" + TimeZones[1] + " 0:0:1");
                //tneFM1.SetIntValue(dtTemp.Month);
                //tneFD1.SetIntValue(dtTemp.Day);
                //tneTZIndex1.SetIntValue(TZSetIndex[1]);
                //dtTemp = Convert.ToDateTime("2022-" + TimeZones[2] + " 0:0:1");
                //tneFM2.SetIntValue(dtTemp.Month);
                //tneFD2.SetIntValue(dtTemp.Day);
                //tneTZIndex2.SetIntValue(TZSetIndex[2]);
                //dtTemp = Convert.ToDateTime("2022-" + TimeZones[3] + " 0:0:1");
                //tneFM3.SetIntValue(dtTemp.Month);
                //tneFD3.SetIntValue(dtTemp.Day);
                //tneTZIndex3.SetIntValue(TZSetIndex[3]);
                //
                //nudPrice4.Value = Prices[0, 0];
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

        public void GetINIData()
        {
            //SysName=tbSysName.Text  ; 
            //SysID=tbSysID.Text ;   
            //SysAddr=tbSysAddr.Text ;        
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
            //MasterID= tbMasterID.Text ; 
            config.i485Addr = (int)tne485Addr.Value;
            config.Master485Addr = (int)tneMaster485Addr.Value;
            config.AutoRun = GetTcbCheck(tcbAutoRun.Checked);
            //FreshTime= dtpFreshTime.Value.ToString() ;
            FreshInterval = 24;// (int)tneFreshInterval.Value ;
            //ControlIP = tcbControlIP.strText;
            //ControlPort = (int)tneControlPort.Value; 
            //CloundIP =  cbCloundIP.Text ;
            //CloundPort = (int)nudCloundPort.Value;
            //SysAutoRun = false;// tcbSysAutoRun.Checked;
/*            config.SysMode = tcbSYSModel.SelectItemIndex;
            PCSType = tcbPCSType.strText;
            config.PCSGridModel = tcbPCSGridModel.SelectItemIndex;
            if (tcbPCSMode.SelectItemIndex == 0)//0充电为正
                PCSwaValue = (int)tnePCSwaValue.Value;
            else
                PCSwaValue = -1 * (int)tnePCSwaValue.Value;
            cloudLimits.BmsDerateRatio = (int)tneBMSwaValue.Value;//7.24*/
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
            //frmMain.TacticsList.AutoTactics = (SysMode == 1);
            // 
            //TimeZones[0] = tneFM0.Value.ToString() + "-" + tneFD0.Value.ToString();
            //TZSetIndex[0] = tneTZIndex0.Value;
            //TimeZones[1] = tneFM1.Value.ToString() + "-" + tneFD1.Value.ToString();
            //TZSetIndex[1] = tneTZIndex1.Value;
            //TimeZones[2] = tneFM2.Value.ToString() + "-" + tneFD2.Value.ToString();
            //TZSetIndex[2] = tneTZIndex2.Value;
            //TimeZones[3] = tneFM3.Value.ToString() + "-" + tneFD3.Value.ToString();
            //TZSetIndex[3] = tneTZIndex3.Value;
            //
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
            //限制功率
            if (tempPCSwaValue > 110)
                tempPCSwaValue = 110;

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
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        if (frmSet.oneForm.bSheduleChanged)
                        {
                            frmMain.Selffrm.AllEquipment.eState = 1;//记策略模式   
                            frmMain.TacticsList.TacticsOn = false;
                            frmMain.TacticsList.LoadFromMySQL();
                            frmMain.ShowShedule2Char(false);
                            frmMain.TacticsList.ActiveIndex = -1;
                        }
                        frmMain.TacticsList.TacticsOn = true;
                    }
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

        static  public  void DeleOldData(string astrData)
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
            //"delete from log where rTime<'"+astrData+"'", 暂时注释
            "delete from pcs where rTime<'"+astrData+"'",
            "delete from pncontrolerwhere rTime<'"+astrData+"'",
            "delete from profit where rTime<'"+astrData+"'",
            "delete from tactics where rTime<'"+astrData+"'",
            "delete from tempcontrol where rTime<'"+astrData+"'",
            "delete from warningwhere rTime<'"+astrData+"'",
            "delete from chargeinform rTime<'"+astrData+"'"
            };
            foreach (string astrSQl in strSQL)
                DBConnection.ExecSQL(astrSQl);
        }


        private void btnApp_Click(object sender, EventArgs e)
        {
            //save
            GetINIData();
            SaveSet2File();

            //apply
            //空调设置  
            if (bTCDataChanged)
            {
                bTCDataChanged = false;
                frmMain.Selffrm.AllEquipment.TCIni(true);
            }

            //表的尖峰平谷时刻表
            //1、下载数据到结构
            //LoadFromMySQL();
            //2、下载数据 
            // SetJFTG(byte[] a4Zoon,byte[] aBFTGs1, byte[] aBFTGs2)
            //设置
            //SetMaxMinSOC
        }


        private void DelData(string aTableName, string aDataName, string aData, DataGridView aDataGrid)
        {
            if (aDataGrid.SelectedRows.Count <= 0)
                return;
            if (MessageBox.Show("确定要删除当前数据吗", "询问信息", MessageBoxButtons.OKCancel) != DialogResult.OK)
                return;

            DBConnection.ExecSQL("delete from " + aTableName + " where " + aDataName + "='" + aData + "'");
            aDataGrid.Rows.RemoveAt(aDataGrid.SelectedRows[0].Index);
            dbgEquipment.Update();
        }

        private void cbAutoRun_CheckedChanged(object sender, EventArgs e)
        {

        }



        private void btnTempRun_Click(object sender, EventArgs e)
        {
            if (bTCDataChanged)
            {
                GetINIData();
                SaveSet2File();
                bTCDataChanged = false;
                frmMain.Selffrm.AllEquipment.TCIni(true);
            } 
            frmMain.Selffrm.AllEquipment.TCPowerOn(true);
        }

        private void btnACErrorClean_Click(object sender, EventArgs e)
        {
            //if (bTCDataChanged)
            //{
            //    GetINIData();
            //    SaveSet2File();
            //    bTCDataChanged = false;
            //    frmMain.Selffrm.AllEquipment.TCIni();
            //}                 
            frmMain.Selffrm.AllEquipment.TCCleanError();
        }

        private void btnTCPowerOff_Click(object sender, EventArgs e)
        {
            if (bTCDataChanged)
            {
                GetINIData();
                SaveSet2File();
                bTCDataChanged = false;
                frmMain.Selffrm.AllEquipment.TCIni(true);
            }
            frmMain.Selffrm.AllEquipment.TCPowerOn(false);

        }
        
        private void btnPCSRun_Click(object sender, EventArgs e)
        {
            //ShowProgressBar();
            try
            {
               //保存当前数据
                GetINIData();
                SaveSet2File();
               //执行
                PCSMRun();
                //充放电先打开空调 (液冷机) 
                if (frmMain.Selffrm.AllEquipment.TempControl != null)
                {
                    frmMain.Selffrm.AllEquipment.TempControl.TCPowerOn(true);
                }
                if (frmMain.Selffrm.AllEquipment.LiquidCool != null)
                {
                    frmMain.Selffrm.AllEquipment.LiquidCool.LCPowerOn(true);
                }
                //开始预充
                //frmMain.Selffrm.AllEquipment.BMS.PowerOn(true);
                //设置为远端控制
                //frmMain.Selffrm.AllEquipment.PCSList[0].SetSysData(82, 0xFF00);
                //frmMain.Selffrm.AllEquipment.PCSList[0].ExcSetPCSPower(true); 
            }
            catch
            { 
            }
        }

        private void btnPCSOff_Click(object sender, EventArgs e)
        {
            //关闭PCS
            PCSMOff();
            //关闭空调
            if (frmMain.Selffrm.AllEquipment.TempControl != null)
            {
                frmMain.Selffrm.AllEquipment.TempControl.TCPowerOn(true);
            }
            if (frmMain.Selffrm.AllEquipment.LiquidCool != null)
            {
                frmMain.Selffrm.AllEquipment.LiquidCool.LCPowerOn(true);
            }
            //frmMain.Selffrm.AllEquipment.runState = 2;
        }


        private void btnMain_Click(object sender, EventArgs e)
        {
            //设置pcs模式后没有执行，将恢复
            if (frmMain.TacticsList.TacticsOn)
            {
                config.SysMode = 1;
            }
            else
            {
                config.SysMode = 0; 
            }
         
            GetINIData();
            //保存修改数据
            Set_Cloudlimits();
            Set_Config();
            Set_ComponentSettings();

            CloseForm();
            frmMain.ShowMainForm();
            //apply 
            if (bSheduleChanged)//策略需要更新
            {
                frmMain.TacticsList.LoadFromMySQL();
                frmMain.ShowShedule2Char(false);
                frmMain.TacticsList.ActiveIndex = -1;
            } 
            if (bTCDataChanged)  //updata 空调设置
                frmMain.Selffrm.AllEquipment.TCIni(true);
            if (bEDataChanged) //update 表
            {
                frmMain.TacticsList.LoadJFPGFromSQL();
            } 
        }


        private void tneSetHotTemp_OnValueChange(object sender)
        {
            bTCDataChanged = true;

        }

        private void tcbPCSMode_OnValueChange(object sender)
        {
            bTCDataChanged = true;
        }

        private void btnPCSErrorClean_Click(object sender, EventArgs e)
        {
            frmMain.Selffrm.AllEquipment.PCSCleanError();
        }

        private void btnClean_Click_1(object sender, EventArgs e)
        {
            if (dbgLog.SelectedRows.Count <= 0)
                return;
            if (MessageBox.Show("确定要删除所有log记录吗", "询问信息", MessageBoxButtons.OKCancel) != DialogResult.OK)
                return;

            DBConnection.ExecSQL("delete from log");
            DBConnection.ShowData2DBGrid(dbgLog, "select * from log");
        }

        private void btnSave2File_Click_1(object sender, EventArgs e)
        {
            //到处到文件
            DBConnection.SaveGrid2File(dbgLog);
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
                bEDataChanged = true;
            }
        }
        private void btnAdd3_Click(object sender, EventArgs e)
        {
            if (tcbUseYunTactics.Checked)
                return;
            frmoneTactics.AddData(dbgTactics);
            bSheduleChanged = true;

        }

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
                bEDataChanged = true;
            } 
        }

        private void btnEdit3_Click(object sender, EventArgs e)
        {
            if (tcbUseYunTactics.Checked)
                return;
            if (dbgTactics.SelectedRows.Count > 0)
            {
                frmoneTactics.EditData(dbgTactics);
                bSheduleChanged = true;
            }
        }

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

        private void btnDel3_Click(object sender, EventArgs e)
        {
            if (tcbUseYunTactics.Checked)
                return;
            if (dbgTactics.RowCount > 0)
            {
                DelData("tactics", "id", dbgTactics.SelectedRows[0].Cells[0].Value.ToString(), dbgTactics);
                bSheduleChanged = true;
            }
        }

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

            DBConnection.SetDBGrid(oneForm.dbgElectrovalence);
            DBConnection.ShowData2DBGrid(oneForm.dbgElectrovalence, "select id,section,eName,startTime from electrovalence order by section,startTime");

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
            DBConnection.ShowData2DBGrid(oneForm.dbgTactics, "select * from tactics order by starttime");

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

            DBConnection.SetDBGrid(oneForm.dbgLog);
            DBConnection.ShowData2DBGrid(oneForm.dbgLog, "select * from log");

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

        private void tneFM0_OnValueChange(object sender)
        {
            bEDataChanged = true;
        }

        private void tcbAutoRun_OnValueChange(object sender)
        {
            SysIO.SetAutoRun("", tcbAutoRun.Checked);
        }

        private void tcbPCSMode_OnValueChange_1(object sender)
        {

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

        private void btnUpL_Click(object sender, EventArgs e)
        {
            SetDbgridUp(dbgLog);
        }

        private void btnDownL_Click(object sender, EventArgs e)
        {
            SetDbgridDown(dbgLog);
        }

        private void btnSet_Click(object sender, EventArgs e)
        {

        }
        private void ShowProgressBar()
        {
            //实例化等待连接的线程
            Thread oneThread = new Thread(AddSetp);
            oneThread.IsBackground = true;
            pbTimer.Refresh();
            ProgressOn = true;
            oneThread.Start();
            oneThread.Name = "";
        }
        private void AddSetp()
        {
            pbTimer.Value = 0;
            pbTimer.Visible = true;
            pbTimer.Update();
            while (ProgressOn)
            {
                //  Selffrm.Invoke(new UpdateChart(frmMain.TacticsList.ShowTactic2Char), new object[] {frmMain.Selffrm.ctMain , aCleanAllData }); 
                this.Invoke(new AddoneStep(ShowOneStep), new object[] { });
                Thread.Sleep(100);
            }
            pbTimer.Visible = false;
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
            "delete from log; ",
            "delete from pcs; ",
            "delete from pncontroler; ",
            "delete from profit; ",
            // "delete from tactics; ",
            "delete from tempcontrol; ",
            "delete from warning; "};
            foreach (string astrSQl in strSQL)
            {
                DBConnection.ExecSQL(astrSQl);
                Thread.Sleep(100);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult aDlgResult = MessageBox.Show("确定要重启系统吗？", "询问", MessageBoxButtons.YesNo);
            if (aDlgResult != DialogResult.Yes)
                return;
            SysIO.Reboot();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            PowerGPIO(0);
            //Set_GlobalSet_State();
            Set_Cloudlimits();
            if (frmMain.Selffrm.AllEquipment.Led != null)
            {
                frmMain.Selffrm.AllEquipment.Led.SetButteryPercentOff();
                frmMain.Selffrm.AllEquipment.Led.Set_Led_ShutDown();

            }
            this.Close();
            frmMain.Selffrm.Close();
        }

        private void btnUpdata_Click(object sender, EventArgs e)
        {
            frmMain.TacticsList.LoadJFPGFromSQL();
        }

        private void btnATAppy_Click(object sender, EventArgs e)
        {
            GetINIData();
            SaveSet2File();
            bTCDataChanged = false;
            frmMain.Selffrm.AllEquipment.TCIni(true);
        }

        private void btnLCRun_Click(object sender, EventArgs e)
        {
            try
            {
                GetINIData();
                SaveSet2File();
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
                }
            }
            catch { }
        }

        private void btnDHRun_Click(object sender, EventArgs e)
        {
            try
            {
                GetINIData();
                SaveSet2File();
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
            frmMain.Selffrm.AllEquipment.Dehumidifier.GetDataFromEqipment();
        }

        //读取数据库，刷新策略时段
        private void btnFlash3_Click(object sender, EventArgs e)
        {
            DBConnection.ShowData2DBGrid(oneForm.dbgTactics, "select * from tactics order by starttime");
        }


        public class CloudLimitClass
        {
            public volatile int MaxGridKW;
            public volatile int MinGridKW;
            public volatile int MaxSOC;
            public volatile int MinSOC;
            public volatile int WarnMaxGridKW;
            public volatile int WarnMinGridKW;
            public volatile int PcsKva ;
            public volatile int Client_PUMdemand_Max;
            public volatile int EnableActiveReduce;
            public volatile int PumScale;
            public volatile int AllUkvaWindowSize;
            public volatile int PumTime;
            public volatile int BmsDerateRatio; // double
            public volatile int FrigOpenLower;
            public volatile int FrigOffLower;
            public volatile int FrigOffUpper;
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
            public int IsMaster { get; set; } // bool
            public int Master485Addr { get; set; } // int
            public int i485Addr { get; set; } // int
            public int AutoRun { get; set; } // bool
            public int SysMode { get; set; } // int
            public int PCSGridModel { get; set; } // int
            public string DebugComName { get; set; } // varchar(255)
            public int DebugRate { get; set; } // int
            public int SysCount { get; set; } // int
            public int UseYunTactics { get; set; } // bool
            public int UseBalaTactics { get; set; } // bool
            public int iPCSfactory { get; set; } // int
            public int BMSVerb { get; set; } // int
            public int PCSForceRun { get; set; } // bool
            public int ErrorState2 { get; set; } // bool
            public int EMSstatus { get; set; } // bool
            public int GPIOSelect { get; set; }
            public string MasterIp { get; set; }
            public string ConnectStatus { get; set; }

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
                GetINIData();
                //frmSet.SaveSet2File();
                Set_Cloudlimits();

                //8.3
                frmMain.Selffrm.AllEquipment.BMS.SetBmsPV1(tneBMScellPV1.Value);//BMS1级单体过压报警阈值
                frmMain.Selffrm.AllEquipment.BMS.SetBmsUPV1(tneBMScellUPV1.Value);// BMS1级单体过压恢复阈值
                frmMain.Selffrm.AllEquipment.BMS.SetBmsPV2(tneBMScellPV2.Value);//BMS2级单体过压报警阈值
                frmMain.Selffrm.AllEquipment.BMS.SetBmsUPV2(tneBMScellUPV2.Value);// BMS2级单体过压恢复阈值
                frmMain.Selffrm.AllEquipment.BMS.SetBmsPV3(tneBMScellPV3.Value);//BMS3级单体过压报警阈值
                frmMain.Selffrm.AllEquipment.BMS.SetBmsUPV3(tneBMScellUPV3.Value);// BMS3级单体过压恢复阈值*/
            }
            catch { }
        }

        private void btnBMSOn_Click(object sender, EventArgs e)
        {
            //开始预充
            frmMain.Selffrm.AllEquipment.BMS.PowerOn(true);
        }

        private void btnBMSClose_Click(object sender, EventArgs e)
        {
            //关闭预充
            frmMain.Selffrm.AllEquipment.BMS.PowerOn(false);
        }

    }
}
