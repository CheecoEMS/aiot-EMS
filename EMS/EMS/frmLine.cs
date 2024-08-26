using System;
using System.Drawing;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmLine : Form
    {
        static public frmLine oneForm = null;
        public frmLine()
        {
            InitializeComponent();
            this.Width = 1024;
            this.Height = 768;
            tmInterval.Interval = 1000;
            tmInterval.Enabled = true;
            DoubleBuffered = true;
            ShowData();
        }


        static public void ShowForm()
        {
            if (oneForm == null)
                oneForm = new frmLine();
            //frmMain.Selffrm.Hide();
            oneForm.SetFormPower(frmMain.UserPower);
            oneForm.ShowDialog();
        }
        public void SetFormPower(int aPower)
        {
            btnLine.Visible = (aPower >= 0);
            btnState.Visible = (aPower >= 0);
            btnWarning.Visible = (aPower >= 1);
            btnControl.Visible = (aPower >= 2);
            btnSet.Visible = (aPower >= 3);
        }
        static public void CloseForm()
        {

            if (oneForm != null)
            {
                oneForm.Hide();
                oneForm.Close();
                oneForm.Dispose();
                oneForm = null;
            }
        }

        private void frmLine_Load(object sender, EventArgs e)
        {
        }


        private void ShowData()
        {
            //2.21
            for (int i = 0; i < 25; ++i)
            {
                if (frmMain.Selffrm.AllEquipment.BMS.BalaSwitch[i] != 0)
                {
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        //更新均衡运行状态
                        frmMain.Selffrm.AllEquipment.BalaRun = 1;
                        frmMain.BalaTacticsList.BalaHasOn = true;
                        break;
                    }
                }
                else
                {
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        //更新均衡运行状态
                        frmMain.Selffrm.AllEquipment.BalaRun = 0;
                    }
                }
            }
            //液冷
            if (frmMain.Selffrm.AllEquipment.LiquidCool != null)
            {
                if (frmMain.Selffrm.AllEquipment.LiquidCool.Prepared)
                {
                    plLiquidCool.BackColor = Color.GreenYellow;
                }
                else
                {
                    plLiquidCool.BackColor = Color.Red;
                }
                //液冷机开关状态
                if (frmMain.Selffrm.AllEquipment.LiquidCool.state == 1)
                {
                    plLiquidCoolRun.BackColor = Color.White;
                }
                else
                {
                    plLiquidCoolRun.BackColor = Color.Black;
                }
            }
            //除湿机
            if (frmMain.Selffrm.AllEquipment.Dehumidifier != null)
            {
                if (frmMain.Selffrm.AllEquipment.Dehumidifier.Prepared)
                {
                    plDehumidifie.BackColor = Color.GreenYellow;
                }
                else
                {
                    plDehumidifie.BackColor = Color.Red;
                }
            }
            if (frmMain.Selffrm.AllEquipment.BalaRun == 1)
            {
                plBalaRun.BackColor = Color.GreenYellow;
            }
            else
            {
                plBalaRun.BackColor = Color.Red;
            }

            if (frmMain.Selffrm.AllEquipment.ErrorState[2])
            {
                plEMSState3.BackColor = Color.Red;
            }
            else
            {
                plEMSState3.BackColor = Color.GreenYellow;
            }

            if ((frmMain.Selffrm.AllEquipment.PCSList.Count>0) && 
                (frmMain.Selffrm.AllEquipment.PCSList[0].State == 2))
            {
                plT.BackColor = Color.YellowGreen;
                plL.BackColor = Color.YellowGreen;
            }
            else if ((frmMain.Selffrm.AllEquipment.PCSList.Count > 0)&&
                (frmMain.Selffrm.AllEquipment.PCSList[0].State > 2))
            {
                plT.BackColor = Color.White;
                plL.BackColor = Color.White;
            }
            else if(frmMain.Selffrm.AllEquipment.PCSList.Count > 0)
            {
                plT.BackColor = Color.Gray;
                plL.BackColor = Color.Gray;
            }

            if (frmMain.Selffrm.AllEquipment.BMS!=null)
            {
                BMSClass oneBMS = frmMain.Selffrm.AllEquipment.BMS;
                tbDCV.Text = oneBMS.v.ToString();
                tbDCA.Text = oneBMS.a.ToString();
                tbSOC.Text = frmMain.Selffrm.AllEquipment.BMSSOC.ToString(); //oneBMS.soc.ToString();
                vpbSOC.Value = Convert.ToInt32(frmMain.Selffrm.AllEquipment.BMSSOC);//oneBMS.soc);
                if (oneBMS.Prepared)
                    plBMS.BackColor = Color.GreenYellow;
                else
                    plBMS.BackColor = Color.Red;
            }
            //
            if (frmMain.Selffrm.AllEquipment.PCSList.Count > 0)
            {
                PCSClass onePCS = frmMain.Selffrm.AllEquipment.PCSList[0];
                tbUkva.Text = onePCS.allUkva.ToString();
                tbHZ.Text = onePCS.hz.ToString();
                tbPCSTemp.Text = onePCS.PCSTemp.ToString();
                tbFactor.Text = onePCS.allPFactor.ToString();
                if (onePCS.Prepared)
                    plPCS.BackColor = Color.GreenYellow;
                else
                    plPCS.BackColor = Color.Red;
            }
            //关口表
            if (frmMain.Selffrm.AllEquipment.Elemeter1List != null)
            {
                double dGridKVA = 0;
                bool bPrepared = true;
                foreach (Elemeter1Class Elemeter1 in frmMain.Selffrm.AllEquipment.Elemeter1List)
                {
                    if (!Elemeter1.Prepared)
                        bPrepared = false;
                    else
                        dGridKVA += Elemeter1.AllUkva;
                }
                if ((bPrepared)&&(frmMain.Selffrm.AllEquipment.Elemeter1List.Count>0))
                    plE0.BackColor = Color.GreenYellow;
                else
                    plE0.BackColor = Color.Red;
                tbGridkva.Text= dGridKVA.ToString();
            }
            //设备表
            if (frmMain.Selffrm.AllEquipment.Elemeter2 != null)
            {
                if (frmMain.Selffrm.AllEquipment.Elemeter2.Prepared)
                    plE2.BackColor = Color.GreenYellow;
                else
                    plE2.BackColor = Color.Red;
                tbAllPUkwh.Text = frmMain.Selffrm.AllEquipment.Elemeter2.PUkwh[0].ToString();//Elemeter2.Ukwh[0].ToString();
                tbAllOUkwh.Text = frmMain.Selffrm.AllEquipment.Elemeter2.OUkwh[0].ToString();

            }
            //辅表
            if (frmMain.Selffrm.AllEquipment.Elemeter3 != null)
            {
                if (frmMain.Selffrm.AllEquipment.Elemeter3.Prepared)
                    plE3.BackColor = Color.GreenYellow;
                else
                    plE3.BackColor = Color.Red;
            }
            //传感器
            if (frmMain.Selffrm.AllEquipment.WaterLog1 != null)
            {
                tbWLog1.Text = frmMain.Selffrm.AllEquipment.WaterLog1.WaterlogData.ToString();
                if (tbWLog1.Text == "1")
                    tbWLog1.BackColor = Color.Red;
                else
                    tbWLog1.BackColor = Color.FromArgb(75, 86, 93);
                if (frmMain.Selffrm.AllEquipment.WaterLog1.Prepared)
                    plWaterLog1.BackColor = Color.GreenYellow;
                else
                    plWaterLog1.BackColor = Color.Red;
            }
            if (frmMain.Selffrm.AllEquipment.WaterLog2 != null)
            {
                tbWLog2.Text = frmMain.Selffrm.AllEquipment.WaterLog2.WaterlogData.ToString();
                if (tbWLog2.Text == "1")
                    tbWLog2.BackColor = Color.Red;
                else
                    tbWLog2.BackColor = Color.FromArgb(75, 86, 93);
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


            //tbAllPUkwh.Text = frmMain.Selffrm.AllEquipment.SE2PKWH[0].ToString();//Elemeter2.Ukwh[0].ToString();
            //tbAllOUkwh.Text = frmMain.Selffrm.AllEquipment.SE2OKWH[0].ToString(); 
            tbPCSInKWH.Text = frmMain.Selffrm.AllEquipment.E2PKWH[0].ToString();
            tbPCSOutKWH.Text = frmMain.Selffrm.AllEquipment.E2OKWH[0].ToString();


            tbe3kva.Text = frmMain.Selffrm.AllEquipment.AuxiliaryKVA.ToString();
            if(frmMain.Selffrm.AllEquipment.Elemeter3!=null)
                tbe3kwh.Text = frmMain.Selffrm.AllEquipment.Elemeter3.Akwh[0].ToString();
           
            //tbGP.Text = frmMain.Selffrm.AllEquipment.Elemeter2.Gridkva.ToString();


        }

        private void tmInterva_Tick(object sender, EventArgs e)
        {
            ShowData();
        }

        private void btnMain_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmMain.ShowMainForm();
        }

        private void btnState_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmState.ShowForm();
        }

        private void btnWarning_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmWarrning.ShowForm();
        }

        private void btnAbout_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmAbout.ShowForm();
        }

        private void btnLine_Click(object sender, EventArgs e)
        {

        }

        private void panel2_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
