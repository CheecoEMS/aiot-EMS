using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using Org.BouncyCastle.Tsp;
using log4net;
using System.Runtime.InteropServices;
using System.IO;
using Mysqlx.Notice;
using System.Diagnostics;
using Org.BouncyCastle.Utilities;
using System.Collections;
using System.Web.UI.WebControls;
using MySqlX.XDevAPI.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using Modbus;

namespace EMS
{

    public partial class frmControl : Form
    {
        //SetThreadAffinityMask: Set hThread run on logical processer(LP:) dwThreadAffinityMask
        [DllImport("kernel32.dll")]
        static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

        //Get the handler of current thread
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentThread();

        static ulong SetCpuID(int lpIdx)
        {
            ulong cpuLogicalProcessorId = 0;
            if (lpIdx < 0 || lpIdx >= System.Environment.ProcessorCount)
            {
                lpIdx = 0;
            }
            cpuLogicalProcessorId |= 1UL << lpIdx;
            return cpuLogicalProcessorId;
        }


        //8.8
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
            frmSet.SysMode = aSysMode;//0手动，1策略，2网控
            frmSet.PCSType = aPCSType;//待机，恒压、恒流、恒功率、AC恒压（离网） ，自适应需量
            if (aPCSMode == "充电")//0充电为正
                frmSet.PCSwaValue = Math.Abs(aPCSValue); 
            else
                frmSet.PCSwaValue = -1 * Math.Abs(aPCSValue);

            if (SaveParam)
                frmSet.SaveSet2File();
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
           // ShowINIData();
        }



        private void GetINIData()
        {
            
            frmSet.SysMode = tcbSYSModel.SelectItemIndex;
            frmSet.PCSType = tcbPCSType.strText; //待机 恒压 恒流 恒功率 自适应需量
            if (tcbPCSMode.SelectItemIndex == 0)//0充电为正
                frmSet.PCSwaValue = (int)tnePCSwaValue.Value;
            else
                frmSet.PCSwaValue = -1 * (int)tnePCSwaValue.Value;
            frmSet.BMSwaValue = tneBMSwaValue.Value;//7.24 添加BMS 1级告警时PCS降低的功率比例
            frmSet.SetHotTemp = (int)tneSetHotTemp.Value;
            frmSet.SetCoolTemp = (int)tneSetCoolTemp.Value;
            frmSet.CoolTempReturn = (int)tneCoolTempReturn.Value;
            frmSet.HotTempReturn = (int)tneHotTempReturn.Value;
            
            //12.4
            frmSet.EMSstatus = tcbEMSstatus.SelectItemIndex; //0:测试模式 1：运行模式
            int iConnectStatus = tcbConnectStatus.SelectItemIndex;
            if (iConnectStatus == 0)
            {
                frmSet.ConnectStatus = "485";
            }
            else if (iConnectStatus == 1)
            {
                frmSet.ConnectStatus = "tcp";
            }

        }
        private void ShowINIData()
        {
            frmSet.LoadSetInf();
            frmSet.LoadFromGlobalSet();
            tcbSYSModel.SetSelectItemIndex(frmSet.SysMode); 
            tcbPCSType.SetstrText(frmSet.PCSType);
            if (frmSet.PCSwaValue > 0)
                tcbPCSMode.SetSelectItemIndex(0);
            else
                tcbPCSMode.SetSelectItemIndex(1);
            tnePCSwaValue.SetIntValue(Math.Abs(frmSet.PCSwaValue));
            tneBMSwaValue.SetIntValue((int)Math.Abs(frmSet.BMSwaValue));//7.24
            tneSetHotTemp.SetIntValue((int)(frmSet.SetHotTemp));
            tneSetCoolTemp.SetIntValue((int)(frmSet.SetCoolTemp));
            tneCoolTempReturn.SetIntValue((int)(frmSet.CoolTempReturn));
            tneHotTempReturn.SetIntValue((int)(frmSet.HotTempReturn));

            //12.4
            tcbEMSstatus.SetSelectItemIndex(frmSet.EMSstatus);

            if (frmSet.ConnectStatus == "485")
            {
                tcbConnectStatus.SetSelectItemIndex(0);
            }
            else if (frmSet.ConnectStatus == "tcp")
            {
                tcbConnectStatus.SetSelectItemIndex(1);
            }
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

        private void btnBMSErrorClean_Click(object sender, EventArgs e)
        {
            //
        }

        private void btnTempRun_Click(object sender, EventArgs e)
        {
            GetINIData();
            frmSet.SaveSet2File(); 
            frmMain.Selffrm.AllEquipment.TCIni(false);
            frmMain.Selffrm.AllEquipment.TCPowerOn(true);
        }

        private void btnTCPowerOff_Click(object sender, EventArgs e)
        {
            frmMain.Selffrm.AllEquipment.TCPowerOn(false);
        }

        private void btnACErrorClean_Click(object sender, EventArgs e)
        {
            frmMain.Selffrm.AllEquipment.TCCleanError();
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
                //保存当前数据重新赋值给frmset的属性
                GetINIData(); //桌面选定执行功率存入设置中
                frmSet.SaveSet2File();
                //执行
                frmSet.PCSMRun();
            }
            catch
            {
            }
        }

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
                frmSet.SaveSet2File();

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

        private void btnEMSRun_Click(object sender, EventArgs e)
        {
            try
            {
                GetINIData();
                frmSet.SaveSet2File();

            }
            catch { }
        }
        private void btnConnectChoose_Click(object sender, EventArgs e)
        {
            try
            {
                GetINIData();
                frmSet.SaveSet2File();

            }
            catch { }
        }


        private void frmControl_Load(object sender, EventArgs e)
        {

        }

        private void btnCleanError_Click(object sender, EventArgs e)
        {
            frmMain.Selffrm.AllEquipment.ErrorState[2] = false;
            frmSet.SaveSet2File();
            frmSet.ErrorGPIO(1);
            //frmMain.Selffrm.AllEquipment.runState = 0;
        }

        private void btnBalaRun_Click(object sender, EventArgs e)
        {
           try
           {
                frmMain.Selffrm.AllEquipment.BMS.SetBmsBala(1, tneBMScellNum.Value);
           }
           catch { }
        }

        private void btnBalaClose_Click(object sender, EventArgs e)
        {
            try
            {
                frmMain.Selffrm.AllEquipment.BMS.SetBmsBala(0, tneBMScellNum.Value);
            }
            catch { }
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

        }

        //test 的选择框
        private void tnetest_Load(object sender, EventArgs e)
        {

        }
    }
}
