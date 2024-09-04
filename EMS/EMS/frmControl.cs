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
                oneForm.ShowINIData();
                oneForm.SetFormPower(frmMain.UserPower);
                oneForm.ShowDialog();
            }
            catch { }
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
                    break;
                case 1:
                    break;
                default: 
                    break;
            }
        }

        private void btnPostProfit_Click(object sender, EventArgs e)
        {
            //当日收益发送到云
            frmMain.Selffrm.AllEquipment.Report2Cloud.SaveProfit2Cloud(frmMain.Selffrm.AllEquipment.rDate);//qiao
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
            //保存修改数据
            frmSet.Set_Config();
        }
    }
}
