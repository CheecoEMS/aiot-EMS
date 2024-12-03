using System;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmConnect : Form
    {
        public frmConnect()
        {
            InitializeComponent();
        }

        private void btnCallRun_Click(object sender, EventArgs e)
        {
            int[] CallResult = new int[6] { 0,0,0,0,0,0};
            int i = 0;

            foreach (EMSEquipment oneEMSE in frmMain.Selffrm.AllEquipment.EMSList)
            {
                CallResult[i++] = oneEMSE.CallEMS(oneEMSE.ID);
            }

            //更新桌面
            tbSysCount.Text = frmSet.config.SysMode.ToString();
            tbcall1.Text = CallResult[0].ToString();
            tbcall2.Text = CallResult[1].ToString();
            tbcall3.Text = CallResult[2].ToString();
            tbcall4.Text = CallResult[3].ToString();
            tbcall5.Text = CallResult[4].ToString();
            tbcall6.Text = CallResult[5].ToString();

        }
    }
}
