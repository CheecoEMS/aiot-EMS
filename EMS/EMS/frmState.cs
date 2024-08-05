using System;
using System.Drawing;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmState : Form
    {
        static public frmState oneForm = null;
        public int DataIndex = 0;
        public int BoxIndex = 0;
        public frmState()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            this.listView1.View = System.Windows.Forms.View.Details;
            this.listView1.Columns.Add(" 电池ID ");
            this.listView1.Columns.Add(" 电压 ");
            this.listView1.Columns.Add(" 温度 ");
            this.listView1.View = View.Details;
            listView1.Columns[0].Width = 150;
            listView1.Columns[1].Width = 100;
            listView1.Columns[2].Width = 100;
            listView1.FullRowSelect = true;
            listView1.GridLines = true;
            listView1.LabelEdit = false;
            listView1.AllowColumnReorder = false;
            listView1.CheckBoxes = false;
            DoubleBuffered = true;
            //listView1.Sorting = SortOrder.Ascending;  
        }

        private void frmState_Load(object sender, EventArgs e)
        {

        }
        static public void INIForm()
        {
            oneForm = new frmState();
        }

        static public void CloseForm()
        {

            if (oneForm != null)
            {
                oneForm.tmInterva.Enabled = false;
                oneForm.Hide();
                //oneForm.Close();
                //oneForm.Dispose();
                //oneForm = null; 
            }
        }

        static public void ShowForm()
        {
            if (oneForm == null)
                oneForm = new frmState();

            oneForm.FreshData2Form(1);
            oneForm.FreshData2Form(2);
            oneForm.FreshData2Form(3);
            oneForm.FreshData2Form(4);
            oneForm.FreshData2Form(5);
            oneForm.FreshData2Form(6);
            oneForm.tmInterva.Interval = 1000;
            oneForm.tmInterva.Enabled = true;
            oneForm.btnE_Click(null, EventArgs.Empty);
            oneForm.SetFormPower(frmMain.UserPower);
            oneForm.Show();
            //oneForm.ShowDialog();
            oneForm.BringToFront();
        }
        public void SetFormPower(int aPower)
        {
            btnLine.Visible = (aPower >= 0);
            btnState.Visible = (aPower >= 0);
            btnWarning.Visible = (aPower >= 1);
            btnControl.Visible = (aPower >= 2);
            btnSet.Visible = (aPower >= 3);
        }
        //设置电池柜子选择框，根据设计中柜子的个数和当前查看的状态显示
        private void SetBoxSel(bool aVisble)
        {
            //rbBMS1.Visible = aVisble;
            //rbBMS2.Visible = aVisble;
            //rbBMS3.Visible = aVisble;
            //rbBMS4.Visible = aVisble;
            //rbBMS5.Visible = aVisble;
            //if (aVisble)
            //{
            //    rbBMS2.Visible = frmMain.Selffrm.AllEquipment.BMSList.Count>1;
            //    rbBMS3.Visible = frmMain.Selffrm.AllEquipment.BMSList.Count > 2;
            //    rbBMS4.Visible = frmMain.Selffrm.AllEquipment.BMSList.Count > 3;
            //    rbBMS5.Visible = frmMain.Selffrm.AllEquipment.BMSList.Count > 4;
            //}
        }

        //更新数据
        private void FreshData2Form(int aDataIndex)
        {
            //DataIndex = tbcAllPage.SelectedIndex;
            switch (aDataIndex)
            {
                case 10:
                    int iIndex = ctFreshChart.Series[0].Points.Count;
                    if ((frmMain.Selffrm.AllEquipment.Elemeter2.time != Convert.ToDateTime("2000-6-30 09:09:09")) && ((ctFreshChart.Series[0].Points.Count == 0) ||
                            (ctFreshChart.Series[0].Points[iIndex - 1].XValue.ToString() != frmMain.Selffrm.AllEquipment.Elemeter2.time.ToString("H;m:s"))))
                    {
                        ctFreshChart.Series[0].Points.AddXY(frmMain.Selffrm.AllEquipment.Elemeter2.time.ToString("H;m:s"),
                            frmMain.Selffrm.AllEquipment.Elemeter2.Totalkva.ToString());
                        ctFreshChart.Series[1].Points.AddXY(frmMain.Selffrm.AllEquipment.Elemeter2.time.ToString("H;m:s"),
                            frmMain.Selffrm.AllEquipment.Elemeter2.Gridkva.ToString());
                        if (frmMain.Selffrm.AllEquipment.Elemeter2.AllAkva > 0)
                        {
                            ctFreshChart.Series[2].Points.AddXY(frmMain.Selffrm.AllEquipment.Elemeter2.time.ToString("H;m:s"),
                                frmMain.Selffrm.AllEquipment.Elemeter2.AllAkva.ToString());
                            ctFreshChart.Series[3].Points.AddXY(frmMain.Selffrm.AllEquipment.Elemeter2.time.ToString("H;m:s"), "0");
                        }
                        else
                        {
                            ctFreshChart.Series[2].Points.AddXY(frmMain.Selffrm.AllEquipment.Elemeter2.time.ToString("H;m:s"), "0");
                            ctFreshChart.Series[3].Points.AddXY(frmMain.Selffrm.AllEquipment.Elemeter2.time.ToString("H;m:s"),
                                Math.Abs(frmMain.Selffrm.AllEquipment.Elemeter2.AllAkva).ToString());
                        }
                    }
                    break;
                case 0://表
                    if (frmMain.Selffrm.AllEquipment.Elemeter1List != null)
                    {
                        double[] dData = { 0, 0, 0, 0, 0 };
                        foreach (Elemeter1Class Elemeter1 in frmMain.Selffrm.AllEquipment.Elemeter1List)
                        {
                            dData[0] += Elemeter1.AllAAkva;
                            dData[1] += Elemeter1.AllUkva;
                            dData[2] += Elemeter1.AllNukva;
                            dData[3] += Elemeter1.Ukwh[0];
                            dData[4] += Elemeter1.Nukwh[0];
                        }
                        tbE1AAkva.Text = dData[0].ToString();
                        tbE1Ukva.Text = dData[1].ToString();
                        tbE1NUkva.Text = dData[2].ToString();
                        tbE1UKWH.Text = dData[3].ToString();
                        tbE1NUKWH.Text = dData[4].ToString();
                    }


                    if (frmMain.Selffrm.AllEquipment.Elemeter2 != null)
                    {
                        tbUkva2.Text = frmMain.Selffrm.AllEquipment.Elemeter2.AllUkva.ToString();
                        tbNUkvh2.Text = frmMain.Selffrm.AllEquipment.Elemeter2.AllNukva.ToString();
                        tbAAkva2.Text = frmMain.Selffrm.AllEquipment.Elemeter2.AllAkva.ToString();
                        tbPFoctor2.Text = frmMain.Selffrm.AllEquipment.Elemeter2.AllPFactor.ToString();
                        tbE2HZ.Text = frmMain.Selffrm.AllEquipment.Elemeter2.HZ.ToString();
                        tbE2AUkwh.Text = frmMain.Selffrm.AllEquipment.Elemeter2.Ukwh[0].ToString();
                        tbE2AOUkwh.Text = frmMain.Selffrm.AllEquipment.Elemeter2.OUkwh[0].ToString();
                        tbE2APUkwh.Text = frmMain.Selffrm.AllEquipment.Elemeter2.PUkwh[0].ToString();
                        tbE2APNukwh.Text = frmMain.Selffrm.AllEquipment.Elemeter2.ONukwh[0].ToString();
                        tbE2AONukwh.Text = frmMain.Selffrm.AllEquipment.Elemeter2.PNukwh[0].ToString();
                        tbE2AllNukwh.Text = frmMain.Selffrm.AllEquipment.Elemeter2.Nukwh[0].ToString();
                    }
                    if (frmMain.Selffrm.AllEquipment.Elemeter3 != null)
                    {
                        tbE3AKVA.Text = frmMain.Selffrm.AllEquipment.Elemeter3.AKva.ToString();
                        tbE3UKVA.Text = frmMain.Selffrm.AllEquipment.Elemeter3.UKva.ToString();
                        tbE3NUKVA.Text = frmMain.Selffrm.AllEquipment.Elemeter3.NUKva.ToString();
                        tbE3KWH.Text = frmMain.Selffrm.AllEquipment.Elemeter3.Akwh[0].ToString();
                    }
                    break;

                case 1://PCS
                    if (frmMain.Selffrm.AllEquipment.PCSList.Count <= BoxIndex)
                        return;
                    PCSClass onePCS = frmMain.Selffrm.AllEquipment.PCSList[BoxIndex];
                    tbUkva.Text = onePCS.allUkva.ToString();
                    tbNUkva.Text = onePCS.allNUkvar.ToString();
                    tbAkva.Text = onePCS.allAkva.ToString();
                    tbPFactor.Text = onePCS.allPFactor.ToString();
                    tbPCSTemp.Text = onePCS.PCSTemp.ToString();
                    tbPCSAv.Text = onePCS.aV.ToString();
                    tbPCSBv.Text = onePCS.bV.ToString();
                    tbPCSCv.Text = onePCS.cV.ToString();
                    //tbNetEnble.Text = onePCS.PCSTemp.ToString();
                    tbAa.Text = onePCS.aA.ToString();
                    tbBa.Text = onePCS.bA.ToString();
                    tbCa.Text = onePCS.cA.ToString();
                    tbDCkva.Text = onePCS.inputkva.ToString();
                    tbDCv.Text = onePCS.inputV.ToString();
                    tbDCa.Text = onePCS.inputA.ToString();
                    tbPCSState.Text = PCSClass.PCSStates[onePCS.State];
                    tbPCSHZ.Text = onePCS.hz.ToString();
                    tbPCSACInput.Text = onePCS.ACInkwh.ToString();
                    tbPCSACOutput.Text = onePCS.ACOutkwh.ToString();
                    tbInTemp.Text = onePCS.InTemp.ToString();
                    tbOutTemp.Text = onePCS.OutTemp.ToString();
                    if (frmSet.config.PCSGridModel == 0)
                        tbNetEnble.Text = "并网";
                    else
                        tbNetEnble.Text = "离网";
                    tbIGBT1.Text = onePCS.IGBTTemp1.ToString();
                    tbIGBT2.Text = onePCS.IGBTTemp2.ToString();
                    tbIGBT3.Text = onePCS.IGBTTemp3.ToString();
                    tbIGBT4.Text = onePCS.IGBTTemp4.ToString();
                    tbIGBT5.Text = onePCS.IGBTTemp5.ToString();
                    tbIGBT6.Text = onePCS.IGBTTemp6.ToString();
                    break;
                case 2: // 空调active BoxIndex 
                    TempControlClass oneTempControl = frmMain.Selffrm.AllEquipment.TempControl;
                    if (oneTempControl == null)
                        break;
                    if (oneTempControl.state == 1)
                    {
                        labTCSate.BackColor = Color.Green;
                        labTCSate.Text = "运行中..";
                    }

                    else
                    {
                        labTCSate.BackColor = Color.Gray;
                        labTCSate.Text = "待机中..";
                    }
                    labTemp.Text = oneTempControl.indoorTemp.ToString();            //温度
                    lbEnvironmentTemp.Text = oneTempControl.environmentTemp.ToString();//环境温度
                    lbEvaporation.Text = oneTempControl.evaporationTemp.ToString("F1");///蒸发温度
                    //lbCondenserTemp.Text = oneTempControl.condenserTemp.ToString();//冷凝/供液温度; 
                    lbindoorHumidity.Text = oneTempControl.indoorHumidity.ToString();
                    //lbfanControl.Text = oneTempControl.fanControl.ToString();
                    tneSetHotTemp.SetValue(0.1 * frmSet.componentSettings.SetCoolTemp);
                    tneSetCoolTemp.SetValue(0.1 * (frmSet.componentSettings.SetCoolTemp));
                    tneCoolTempReturn.SetValue(0.1 * (frmSet.componentSettings.CoolTempReturn));
                    tneHotTempReturn.SetValue(0.1 * (frmSet.componentSettings.SetCoolTemp));
                    //tneSetHumidity.SetIntValue((int)(SetHumidity));
                    //tneHumiReturn.SetIntValue((int)(HumiReturn));
                    //tcbTCRunWithSys.SetValue(TCRunWithSys);
                    ////cbTCAuto.Checked = TCAuto;
                    //tcbTCMode.SetSelectItemIndex(TCMode);
                    tneTCMaxTemp.SetValue(0.1 * (frmSet.componentSettings.SetCoolTemp));
                    tneTCMinTemp.SetValue(0.1 * (frmSet.componentSettings.SetCoolTemp));
                    //tneTCMaxHumidity.SetIntValue((int)(TCMaxHumi));
                    //tneTCMinHumidity.SetIntValue((int)(TCMinHumi));


                    break;
                case 3: //BMS
                    if (frmMain.Selffrm.AllEquipment.BMS==null)
                        return;
                    BMSClass oneBMS = frmMain.Selffrm.AllEquipment.BMS;

                    //8.3
                    tbBMScellErrPV1.Text = oneBMS.cellErrPV1.ToString();
                    tbBMScellErrUPV1.Text = oneBMS.cellErrUPV1.ToString();
                    tbBMScellErrPV2.Text = oneBMS.cellErrPV2.ToString();
                    tbBMScellErrUPV2.Text = oneBMS.cellErrUPV2.ToString();
                    tbBMScellErrPV3.Text = oneBMS.cellErrPV3.ToString();
                    tbBMScellErrUPV3.Text = oneBMS.cellErrUPV3.ToString();

                    tbBMSv.Text = oneBMS.v.ToString();
                    tbBMSa.Text = oneBMS.a.ToString();
                    tbBMSR.Text = oneBMS.insulationR.ToString();
                    tbBMSSOC.Text = oneBMS.soc.ToString();
                    tbBMSSOH.Text = oneBMS.soh.ToString();
                    //tbBR.Text = oneBMS.positiveR.ToString();
                    tbBMSCap.Text = (frmSet.config.SysSelfPower * oneBMS.soc * oneBMS.soh / 10000).ToString("0.##");
                    tbBMSAvgTemp.Text = oneBMS.averageTemp.ToString();
                    tbBMSAvgV.Text = oneBMS.averageV.ToString();
                    tbBMSMaxTemp.Text = oneBMS.cellIDMaxtemp.ToString() + "--" + oneBMS.cellMaxTemp.ToString();
                    tbBMSminTemp.Text = oneBMS.cellIDMintemp.ToString() + "--" + oneBMS.cellMinTemp.ToString();
                    tbBMSMaxV.Text = oneBMS.cellIDMaxV.ToString() + "--" + oneBMS.cellMaxV.ToString();
                    tbBMSMinV.Text = oneBMS.cellIDMinV.ToString() + "--" + oneBMS.cellMinV.ToString();
                    //SwitchState 0x0: 断开, 0x1: 闭合--->  Bit0: 主接触状态;  Bit1: 预充接触器状态;  Bit2: 主负接触状态;      Bit3: 隔离开关状态;

                    tbAllKey.ForeColor = (oneBMS.SwitchState & 8) > 0 ? Color.Green : Color.Red;
                    tbChargKey.ForeColor = (oneBMS.SwitchState & 2) > 0 ? Color.Green : Color.Red;
                    tbAllF.ForeColor = (oneBMS.SwitchState & 1) > 0 ? Color.Green : Color.Red;
                    tbAllZ.ForeColor = (oneBMS.SwitchState & 4) > 0 ? Color.Green : Color.Red;

                    //CellClass ontCell;
                    for (int i = 0; i < 240; i++)
                    {
                        if (listView1.Items.Count <= i)
                        {
                            this.listView1.Items.Add((i + 1).ToString());
                            this.listView1.Items[i].SubItems.Add(oneBMS.CellVs[i].ToString());
                            this.listView1.Items[i].SubItems.Add(oneBMS.CellTemps[i].ToString());
                        }
                        else
                        {
                            this.listView1.Items[i].SubItems[1].Text = oneBMS.CellVs[i].ToString();
                            this.listView1.Items[i].SubItems[2].Text = oneBMS.CellTemps[i].ToString();
                        }

                    }
                    break;
                case 4:
                    break;
                case 5:
                    //传感器
                    if (frmMain.Selffrm.AllEquipment.WaterLog1 != null)
                    {
                        tbWLog1.Text = frmMain.Selffrm.AllEquipment.WaterLog1.WaterlogData.ToString();
                        if (frmMain.Selffrm.AllEquipment.WaterLog1.Prepared)
                            plWaterLog1.BackColor = Color.GreenYellow;
                        else
                            plWaterLog1.BackColor = Color.Red;
                    }
                    if (frmMain.Selffrm.AllEquipment.WaterLog2 != null)
                    {
                        tbWLog2.Text = frmMain.Selffrm.AllEquipment.WaterLog2.WaterlogData.ToString();
                        if (frmMain.Selffrm.AllEquipment.WaterLog2.Prepared)
                            plWaterLog2.BackColor = Color.GreenYellow;
                        else
                            plWaterLog2.BackColor = Color.Red;
                    }
                    //co
                    if (frmMain.Selffrm.AllEquipment.co != null)
                    {
                        tbCO.Text = frmMain.Selffrm.AllEquipment.co.CoData.ToString();
                        if (frmMain.Selffrm.AllEquipment.co.Prepared)
                            plCO.BackColor = Color.GreenYellow;
                        else
                            plCO.BackColor = Color.Red;
                    }
                    //tempHum
                    if (frmMain.Selffrm.AllEquipment.TempHum != null)
                    {
                        tbTemp.Text = frmMain.Selffrm.AllEquipment.TempHum.TempData.ToString();
                        tbHum.Text = frmMain.Selffrm.AllEquipment.TempHum.HumidityData.ToString();
                        if (frmMain.Selffrm.AllEquipment.TempHum.Prepared)
                            plTemp.BackColor = Color.GreenYellow;
                        else
                            plTemp.BackColor = Color.Red;
                    }
                    //smoke
                    if (frmMain.Selffrm.AllEquipment.Smoke != null)
                    {
                        tbSmoke.Text = frmMain.Selffrm.AllEquipment.Smoke.SmokeData.ToString();
                        if (frmMain.Selffrm.AllEquipment.Smoke.Prepared)
                            plSmoke.BackColor = Color.GreenYellow;
                        else
                            plSmoke.BackColor = Color.Red;
                    }
                    break;
                case 6://LiquidCool
                    if (frmMain.Selffrm.AllEquipment.LiquidCool != null)
                    { 
                        tbLCModel.Text = frmMain.Selffrm.AllEquipment.LiquidCool.LCModel.ToString();
                        tbWaterPump.Text = frmMain.Selffrm.AllEquipment.LiquidCool.WaterPump.ToString();
                        tbTemperSelect.Text = frmMain.Selffrm.AllEquipment.LiquidCool.TemperSelect.ToString();
                        tbHotTempReturn.Text = frmMain.Selffrm.AllEquipment.LiquidCool.HotTempReturn.ToString();
                        tbCoolTempReturn.Text = frmMain.Selffrm.AllEquipment.LiquidCool.CoolTempReturn.ToString();
                        tbHotTemp.Text = frmMain.Selffrm.AllEquipment.LiquidCool.HotTemp.ToString();
                        tbCoolTemp.Text = frmMain.Selffrm.AllEquipment.LiquidCool.CoolTemp.ToString();
                        //5.06
                        tbLCrunState.Text = frmMain.Selffrm.AllEquipment.LiquidCool.state.ToString();
                        tbLCenvironmentTemp.Text = frmMain.Selffrm.AllEquipment.LiquidCool.environmentTemp.ToString();
                        tbLCOutwaterTemp.Text = frmMain.Selffrm.AllEquipment.LiquidCool.OutwaterTemp.ToString();
                        tbLCInwaterTemp.Text = frmMain.Selffrm.AllEquipment.LiquidCool.InwaterTemp.ToString();
                        tbLCInwaterPressure.Text = frmMain.Selffrm.AllEquipment.LiquidCool.InwaterPressure.ToString();
                        tbLCOutwaterPressure.Text = frmMain.Selffrm.AllEquipment.LiquidCool.OutwaterPressure.ToString();
                        tbLCExgasTemp.Text = frmMain.Selffrm.AllEquipment.LiquidCool.ExgasTemp.ToString();





                    }
                    if (frmMain.Selffrm.AllEquipment.Dehumidifier != null)
                    { 
                        tbTempData.Text = frmMain.Selffrm.AllEquipment.Dehumidifier.TempData.ToString();
                        tbHumidityData.Text = frmMain.Selffrm.AllEquipment.Dehumidifier.HumidityData.ToString();
                        tbTempData_Boot.Text = frmMain.Selffrm.AllEquipment.Dehumidifier.TempData_Boot.ToString();
                        tbTempData_Stop.Text = frmMain.Selffrm.AllEquipment.Dehumidifier.TempData_Stop.ToString();
                        tbHumidityData_Boot.Text = frmMain.Selffrm.AllEquipment.Dehumidifier.HumidityData_Boot.ToString();
                        tbHumidityData_Stop.Text = frmMain.Selffrm.AllEquipment.Dehumidifier.HumidityData_Stop.ToString();
                        tbWorkStatus.Text = frmMain.Selffrm.AllEquipment.Dehumidifier.WorkStatus.ToString();
                    }
                    break;
                case 7://EMS

                    break;
            }
        }

        private void ShowDate2Form()
        {
            switch (DataIndex)
            {
                case 10://qiao
                    SetBoxSel(false);
                    DBConnection.ShowData2ChartPower(ctFreshChart, "select rTime, Totalkva  ,Gridkva ,AllAAkva from elemeter2",
                              3, "H:mm:s");
                    break;
                case 0://E
                    SetBoxSel(false);
                    break;
                case 1:
                    SetBoxSel(true);
                    break;
                case 2://TC
                    SetBoxSel(true);
                    DBConnection.SetDBGrid(dbTCError);
                    DBConnection.ShowData2DBGrid(dbTCError, "select * from warning where wClass='空调' and ResetTime is null");
                    break;
                case 3://BMS
                    SetBoxSel(true);
                    //2.21
                    //frmMain.Selffrm.AllEquipment.BMS.GetCellErrUPVInfo();
                    //DBConnection.SetDBGrid(dbBError);
                    //DBConnection.ShowData2DBGrid(dbBError, "select max(rTime) as rTime,cellID,v,t,a  from cells");
                    break; 
                case 4:
                    SetBoxSel(false);
                    break;
                case 5:
                    SetBoxSel(false);
                    break;
                case 6:
                    SetBoxSel(false);
                    
                    break;

            }
            FreshData2Form(DataIndex);
            BringToFront();
        }


        private void tmInterva_Tick(object sender, EventArgs e)
        {
            ShowDate2Form();
        }

        private void tbcAllPage_SelectedIndexChanged(object sender, EventArgs e)
        {
            //ShowDate2Form();
        }
   
        private void button3_Click(object sender, EventArgs e)
        {
            frmMain.Selffrm.AllEquipment.TCPowerOn(false);
        }

        private void btnTCOn_Click(object sender, EventArgs e)
        {
            frmMain.Selffrm.AllEquipment.TCPowerOn(true);
        }
         
        private void btnE_Click(object sender, EventArgs e)
        {
            DataIndex = 0;
            btnBMS.BackColor = Color.Transparent;
            btnE.BackColor = Color.FromArgb(20, 169, 255);
            btnPCS.BackColor = Color.Transparent;
            btnTC.BackColor = Color.Transparent;
            btnRTC.BackColor = Color.Transparent;
            btnLiquidCool.BackColor = Color.Transparent;
            btnEMS.BackColor =  Color.Transparent;
            tpBMS.Parent = null;
            tpE.Parent = tbcAllPage;
            tpPCS.Parent = null;
            tpRTC.Parent = null;
            tpTC.Parent = null;
            tpFire.Parent = null;
            tpLiquidCool.Parent = null;
            tpEMS.Parent = null;
        }

        private void btnPCS_Click(object sender, EventArgs e)
        {
            DataIndex = 1;
            btnBMS.BackColor = Color.Transparent;
            btnE.BackColor = Color.Transparent;
            btnPCS.BackColor = Color.FromArgb(20, 169, 255);
            btnTC.BackColor = Color.Transparent;
            btnRTC.BackColor = Color.Transparent;
            btnLiquidCool.BackColor = Color.Transparent;
            btnEMS.BackColor =  Color.Transparent;
            tpBMS.Parent = null;
            tpE.Parent = null;
            tpPCS.Parent = tbcAllPage;
            tpRTC.Parent = null;
            tpTC.Parent = null;
            tpFire.Parent = null;
            tpLiquidCool.Parent = null;
            tpEMS.Parent = null;
        }
        private void btnTC_Click(object sender, EventArgs e)
        {
            DataIndex = 2;
            btnBMS.BackColor = Color.Transparent;
            btnE.BackColor = Color.Transparent;
            btnPCS.BackColor = Color.Transparent;
            btnTC.BackColor = Color.FromArgb(20, 169, 255);
            btnRTC.BackColor = Color.Transparent;
            btnLiquidCool.BackColor = Color.Transparent;
            btnEMS.BackColor =  Color.Transparent;
            tpBMS.Parent = null;
            tpE.Parent = null;
            tpPCS.Parent = null;
            tpRTC.Parent = null;
            tpFire.Parent = null;
            tpLiquidCool.Parent = null;
            tpEMS.Parent = null;
            tpTC.Parent = tbcAllPage;
            DBConnection.ShowData2DBGrid(dbTCError, "select * from warning where wClass='空调' and ResetTime IS NULL");
        }
         
        private void button9_Click(object sender, EventArgs e)
        {//bms
            DataIndex = 3;
            btnBMS.BackColor = Color.FromArgb(20, 169, 255);
            btnE.BackColor = Color.Transparent;
            btnPCS.BackColor = Color.Transparent;
            btnTC.BackColor = Color.Transparent;
            btnRTC.BackColor = Color.Transparent;
            btnLiquidCool.BackColor = Color.Transparent;
            btnEMS.BackColor =  Color.Transparent;
            tpBMS.Parent = tbcAllPage;
            tpE.Parent = null;
            tpPCS.Parent = null;
            tpRTC.Parent = null;
            tpTC.Parent = null;
            tpFire.Parent = null;
            tpLiquidCool.Parent = null;
            tpEMS.Parent = null;
        }
         
        private void btnRTC_Click(object sender, EventArgs e)
        {
            DataIndex = 4;
            btnBMS.BackColor = Color.Transparent;
            btnE.BackColor = Color.Transparent;
            btnPCS.BackColor = Color.Transparent;
            btnTC.BackColor = Color.Transparent;
            btnRTC.BackColor = Color.FromArgb(20, 169, 255);
            btnLiquidCool.BackColor = Color.Transparent;
            btnEMS.BackColor =  Color.Transparent;
            tpBMS.Parent = null;
            tpE.Parent = null;
            tpPCS.Parent = null;
            tpRTC.Parent = tbcAllPage;
            tpTC.Parent = null;
            tpFire.Parent = null;
            tpLiquidCool.Parent = null;
            tpEMS.Parent = null;
        }

        private void btnFire_Click(object sender, EventArgs e)
        {
            DataIndex = 5;
            btnBMS.BackColor = Color.Transparent;
            btnFire.BackColor   = Color.FromArgb(20, 169, 255);
            btnE.BackColor = Color.Transparent;
            btnPCS.BackColor = Color.Transparent;
            btnTC.BackColor = Color.Transparent;
            btnRTC.BackColor = Color.Transparent;
            btnLiquidCool.BackColor = Color.Transparent;
            btnEMS.BackColor =  Color.Transparent;
            tpBMS.Parent = null;
            tpE.Parent = null;
            tpPCS.Parent = null;
            tpRTC.Parent = null;
            tpTC.Parent = null;
            tpFire.Parent = tbcAllPage;
            tpLiquidCool.Parent = null;
            tpEMS.Parent = null;
        }

        private void btnUPS_Click(object sender, EventArgs e)
        {
            DataIndex = 6;
            btnBMS.BackColor = Color.Transparent;
            btnFire.BackColor   = Color.Transparent;
            btnE.BackColor = Color.Transparent;
            btnPCS.BackColor = Color.Transparent;
            btnTC.BackColor = Color.Transparent;
            btnRTC.BackColor = Color.Transparent;
            btnLiquidCool.BackColor = Color.FromArgb(20, 169, 255);
            btnEMS.BackColor =  Color.Transparent;
            tpBMS.Parent = null;
            tpE.Parent = null;
            tpPCS.Parent = null;
            tpRTC.Parent = null;
            tpTC.Parent = null;
            tpFire.Parent = null;
            tpLiquidCool.Parent = tbcAllPage;
            tpEMS.Parent = null;
        }

        private void btnEMS_Click(object sender, EventArgs e)
        {
            DataIndex = 7;
            btnBMS.BackColor = Color.Transparent;
            btnFire.BackColor  = Color.Transparent;
            btnE.BackColor = Color.Transparent;
            btnPCS.BackColor = Color.Transparent;
            btnTC.BackColor = Color.Transparent;
            btnRTC.BackColor = Color.Transparent;
            btnLiquidCool.BackColor = Color.Transparent;
            btnEMS.BackColor =   Color.FromArgb(20, 169, 255);
            tpBMS.Parent = null;
            tpE.Parent = null;
            tpPCS.Parent = null;
            tpRTC.Parent = null;
            tpTC.Parent = null;
            tpFire.Parent = null;
            tpLiquidCool.Parent = null;
            tpEMS.Parent = tbcAllPage;
        }

        private void btnMain_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmMain.ShowMainForm();
        }

        private void btnLine_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmLine.ShowForm();
        }

        private void btnAbout_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmAbout.ShowForm();
        }

        private void btnWarning_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmWarrning.ShowForm();
        }
 

        private void label146_Click(object sender, EventArgs e)
        {

        }

        private void tbWorkStatus_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
