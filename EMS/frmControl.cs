using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using log4net;
using System.IO;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Dynamic;

namespace EMS
{

    public partial class frmControl : Form
    {
        private static ILog log = LogManager.GetLogger("frmControl");

        public static frmControl oneForm=null ;
        public frmControl()
        {
            InitializeComponent();
        }

        static public void INIForm()
        {
            if (oneForm == null)
                oneForm = new frmControl();
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
                    oneForm = new frmControl();
                if (oneForm != null)
                {
                    oneForm.ShowINIData();
                    oneForm.SetFormPower(frmMain.UserPower);
                    oneForm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                log.Error("ShowForm异常：" + ex.Message);
            }
        }
        public void SetFormPower(int aPower)
        {
            btnLine.Visible = (aPower >= 0);
            btnState.Visible = (aPower >= 0);
            btnWarning.Visible = (aPower >= 1);
            btnControl.Visible = (aPower >= 2);
            btnSet.Visible = (aPower >= 3);
        }

        static public void SetBala(int iBalaStart)
        {
            if (iBalaStart == 1)
            {
                try
                {
                    if (frmMain.Selffrm.AllEquipment.balaCellID.Count != 0)
                        frmMain.Selffrm.AllEquipment.balaCellID.Clear();

                    using (StreamReader reader = new StreamReader(frmSet.BalaPath))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            frmMain.Selffrm.AllEquipment.balaCellID.Add(double.Parse(line));
                        }
                    }
         
                    if (frmMain.Selffrm.AllEquipment.balaCellID.Count != 0)
                    {
                        frmMain.Selffrm.AllEquipment.BMS.StartBmsBala();
                    }
                }
                catch { }

            }
            else
            {
                try
                {
                    frmMain.Selffrm.AllEquipment.BMS.ClearBmsBala();
                }
                catch { }
            }
        }

        static public void SetControl(int aSysMode, string aPCSType, string aPCSMode, int aPCSValue, int aPCSOn,bool SaveParam)
        { 
            frmSet.config.SysMode = aSysMode;//0手动，1策略，2网控
            frmSet.PCSType = aPCSType;//待机，恒压、恒流、恒功率、AC恒压（离网） ，自适应需量
            if (aPCSMode == "充电")//0充电为正
                frmSet.PCSwaValue = Math.Abs(aPCSValue); 
            else
                frmSet.PCSwaValue = -1 * Math.Abs(aPCSValue);

            if (SaveParam)
                frmSet.Set_Config();
            //执行
            if (aPCSOn!=0)
            {
                frmSet.PCSMRun();
            }
            else
            {
                //frmSet.PCSwaValue = 0;
                //关闭PCS
                frmSet.PCSMOff();
                //关闭空调 或液冷机
                if (frmMain.Selffrm.AllEquipment.TempControl != null)
                {
                    frmMain.Selffrm.AllEquipment.TempControl.TCPowerOn(false);
                }
                if (frmMain.Selffrm.AllEquipment.LiquidCool != null)
                { 
                    frmMain.Selffrm.AllEquipment.LiquidCool.LCPowerOn(false);
                }
                //frmMain.Selffrm.AllEquipment.runState = 2;
            }
        }

        private void btnMain_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmMain.ShowMainForm();
        }

        private void SaveUiInstall()
        {
            frmSet.config.SysMode = tcbSYSModel.SelectItemIndex;
            frmSet.PCSType = tcbPCSType.strText; //待机 恒压 恒流 恒功率 自适应需量
            if (tcbPCSMode.SelectItemIndex == 0)//0充电为正
                frmSet.PCSwaValue = (int)tnePCSwaValue.Value;
            else
                frmSet.PCSwaValue = -1 * (int)tnePCSwaValue.Value;

            frmSet.config.EMSstatus = tcbEMSstatus.SelectItemIndex; //0:测试模式 1：运行模式
            int iConnectStatus = tcbConnectStatus.SelectItemIndex;
            if (iConnectStatus == 0)
            {
                frmSet.config.ConnectStatus = "485";
            }
            else if (iConnectStatus == 1)
            {
                frmSet.config.ConnectStatus = "tcp";
            }

            //设置保存入数据库
            frmSet.Set_Config();
        }

        private void ShowINIData()
        {
            //frmSet.LoadSetInf();
            //frmSet.LoadFromGlobalSet();

            try
            {
                frmSet.LoadCloudLimitsFromMySQL();
                frmSet.LoadConfigFromMySQL();
                frmSet.LoadComponentSettingsFromMySQL();

                tcbSYSModel.SetSelectItemIndex(frmSet.config.SysMode);
                tcbPCSType.SetstrText(frmSet.PCSType);
                if (frmSet.PCSwaValue > 0)
                    tcbPCSMode.SetSelectItemIndex(0);
                else
                    tcbPCSMode.SetSelectItemIndex(1);
                tnePCSwaValue.SetIntValue(Math.Abs(frmSet.PCSwaValue));
                //tneBMSwaValue.SetIntValue((int)Math.Abs(frmSet.cloudLimits.BmsDerateRatio));//7.24
                /*            tneSetHotTemp.SetIntValue((int)(frmSet.componentSettings.SetHotTemp));
                            tneSetCoolTemp.SetIntValue((int)(frmSet.componentSettings.SetCoolTemp));
                            tneCoolTempReturn.SetIntValue((int)(frmSet.componentSettings.CoolTempReturn));
                            tneHotTempReturn.SetIntValue((int)(frmSet.componentSettings.HotTempReturn));*/

                //12.4
                tcbEMSstatus.SetSelectItemIndex(frmSet.config.EMSstatus);

                if (frmSet.config.ConnectStatus == "485")
                {
                    tcbConnectStatus.SetSelectItemIndex(0);
                }
                else if (frmSet.config.ConnectStatus == "tcp")
                {
                    tcbConnectStatus.SetSelectItemIndex(1);
                }
            }
            catch (Exception ex)
            {
                log.Error("ShowINIData: " + ex.Message);
            }
        }

        private void btnPCSErrorClean_Click(object sender, EventArgs e)
        {
            frmMain.Selffrm.AllEquipment.PCSCleanError();
        }

        private void btnPCSOff_Click(object sender, EventArgs e)
        {
            frmSet.PCSMOff();
            //关闭空调 或液冷机
            if (frmMain.Selffrm.AllEquipment.TempControl != null)
            {
                frmMain.Selffrm.AllEquipment.TempControl.TCPowerOn(false);
            }
            else if (frmMain.Selffrm.AllEquipment.LiquidCool != null)
            {
                frmMain.Selffrm.AllEquipment.LiquidCool.LCPowerOn(false);
            }
        }

        private void btnPCSRun_Click(object sender, EventArgs e)
        { 
            try
            {
                //保存设置
                BookUi();

                //执行
                frmSet.PCSMRun();
            }
            catch
            {
            }
        }

        private void btnEMSRun_Click(object sender, EventArgs e)
        {
            try
            {
                BookUi();
            }
            catch { }
        }
        private void btnConnectChoose_Click(object sender, EventArgs e)
        {
            try
            {
                BookUi();
            }
            catch { }
        }


        private void frmControl_Load(object sender, EventArgs e)
        {

        }

        private void btnCleanError_Click(object sender, EventArgs e)
        {
            frmMain.Selffrm.AllEquipment.ErrorState[2] = false;
            
            frmSet.historyDatas.ErrorState2 = 1;
            frmSet.Set_HistoryData();
            
            //触发指示灯
            frmSet.ErrorGPIO(0);
        }


        private void btnBalaStart_Click(object sender, EventArgs e)
        {
            try
            {
                if (frmMain.Selffrm.AllEquipment.balaCellID.Count != 0)
                    frmMain.Selffrm.AllEquipment.balaCellID.Clear();

                using (StreamReader reader = new StreamReader(frmSet.BalaPath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        frmMain.Selffrm.AllEquipment.balaCellID.Add(double.Parse(line));
                    }
                }
                if (frmMain.Selffrm.AllEquipment.balaCellID.Count != 0)
                {
                    frmMain.Selffrm.AllEquipment.BMS.StartBmsBala();
                }

            }
            catch { }
        }


        private void btnBalaClear_Click(object sender, EventArgs e)
        {
            frmMain.Selffrm.AllEquipment.BMS.ClearBmsBala();
        }

        //test 专用
        private void btnTest_Click(object sender, EventArgs e)
        {
            int item = tcbtest.SelectItemIndex;
            switch (item)
            { 
                case 0:
                    double[,] CellVs_ID = new double[frmMain.Selffrm.AllEquipment.BMS.CellVs.Length, 2];

                    for (int i = 0; i < frmMain.Selffrm.AllEquipment.BMS.CellVs.Length; i++)
                    {
                        CellVs_ID[i, 0] = frmMain.Selffrm.AllEquipment.BMS.CellVs[i];//单体电压
                        CellVs_ID[i, 1] = ((double)i +1); //单体ID ,根据BMS协议单体ID从1开始
                    }

                    //对单体数据进行冒泡排序
                    for (int i = 0; i < frmMain.Selffrm.AllEquipment.BMS.CellVs.Length -1; i++)
                    {
                        for (int j = 0; j < frmMain.Selffrm.AllEquipment.BMS.CellVs.Length -i -1; j++)
                        {
                            if (CellVs_ID[j, 0] > CellVs_ID[j+1, 0])
                            {
                                //使用元组交换值
                                (CellVs_ID[j+1, 0], CellVs_ID[j, 0])=(CellVs_ID[j, 0], CellVs_ID[j+1, 0]);
                                (CellVs_ID[j+1, 1], CellVs_ID[j, 1])=(CellVs_ID[j, 1], CellVs_ID[j+1, 1]);
                            }

                        }
                    }

                    // 创建用于存储电池信息的列表
                    List<Dictionary<string, double>> cellsVinfoList = new List<Dictionary<string, double>>();
                    int length = CellVs_ID.GetLength(0);
                    for (int i = 0; i < length; i++)
                    {
                        Dictionary<string, double> cellVinfo = new Dictionary<string, double>();
                        cellVinfo["ID"] = CellVs_ID[i, 1];
                        cellVinfo["CellV"] = CellVs_ID[i, 0];
                        cellsVinfoList.Add(cellVinfo);
                    }

                    List<Dictionary<string, double>> cellsTinfoList = new List<Dictionary<string, double>>();
                    for (int i = 0; i < frmMain.Selffrm.AllEquipment.BMS.CellTemps.Length; ++i)
                    {
                        Dictionary<string, double> cellTinfo = new Dictionary<string, double>();
                        cellTinfo["CellTemper"] = frmMain.Selffrm.AllEquipment.BMS.CellTemps[i];
                        cellsTinfoList.Add(cellTinfo);
                    }

                    // 创建最终的 JSON 对象
                    DateTime tempTime = DateTime.Now;
                    string strTime = tempTime.ToString("yyyyMMddHHmmss");

                    Dictionary<string, object> finalJson = new Dictionary<string, object>();
                    finalJson["cellsVinfo"] = cellsVinfoList;
                    finalJson["cellsTinfo"] = cellsTinfoList;
                    finalJson["cellMaxV"] = frmMain.Selffrm.AllEquipment.BMS.cellMaxV;
                    finalJson["cellMinV"] = frmMain.Selffrm.AllEquipment.BMS.cellMinV;
                    finalJson["cellMaxTemp"] = frmMain.Selffrm.AllEquipment.BMS.cellMaxTemp;
                    finalJson["cellMinTemp"] = frmMain.Selffrm.AllEquipment.BMS.cellMinTemp;
                    finalJson["averageV"] = frmMain.Selffrm.AllEquipment.BMS.averageV;
                    finalJson["averageTemp"] = frmMain.Selffrm.AllEquipment.BMS.averageTemp;
                    finalJson["v"] = frmMain.Selffrm.AllEquipment.BMS.v;
                    finalJson["a"] = frmMain.Selffrm.AllEquipment.BMS.a;
                    finalJson["timestamp"] = strTime;
                    finalJson["iotcode"] = frmMain.Selffrm.AllEquipment.BMS.iot_code;

                    // 将 JSON 对象序列化为字符串
                    string jsonString = JsonConvert.SerializeObject(finalJson);
                    // 将 JSON 字符串写入文件
                    string filePath = Path.Combine(frmMain.Selffrm.AllEquipment.Report2Cloud.strUpPath, "0cel.json");
                    File.WriteAllText(filePath, jsonString);
                    
                    break;
                case 1:
                    frmMain.Selffrm.AllEquipment.BMS.countdownTimer = new CountdownTimer();
                    frmMain.Selffrm.AllEquipment.BMS.countdownTimer.Start();
                    break;
                case 2:
                    frmMain.Selffrm.AllEquipment.BMS.countdownTimer.Reset();
                    break;
                default: 
                    break;
            }
        }

        private void btnTimeCalibration_Click(object sender, EventArgs e)
        {
            //校准电表日期
            frmMain.Selffrm.AllEquipment.MeterCalibration();
        }

        private void BookUi()
        {
            //桌面选定执行功率存入设置中
            SaveUiInstall();
        }

        private void btnReadDofD_Click(object sender, EventArgs e)
        {
            if (frmMain.Selffrm.AllEquipment != null)
            {
                frmMain.Selffrm.AllEquipment.ReadDataInoneDayINI();
            }
        }
    }
}
