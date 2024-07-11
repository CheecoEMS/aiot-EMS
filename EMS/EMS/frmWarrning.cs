using System;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmWarrning : Form
    {
        static public frmWarrning oneForm = null;
        public frmWarrning()
        {
            InitializeComponent();
            SqlExecutor.SetDBGrid(dbgWarning);
            SqlExecutor.ShowData2DBGrid(dbgWarning, "select id,WaringID, rTime, wClass,wLevels, Warning, memo, CheckTime, UserID, ResetTime "
                 + " from warning  where (ResetTime IS NULL )or (ResetTime>='" + DateTime.Now.ToString("yyyy-MM-dd 0:0:0")
                 + "')order by rTime DESC");
        }

        static public void ShowForm()
        {
            if (oneForm == null)
                oneForm = new frmWarrning();
            oneForm.SetFormPower(frmMain.UserPower);
            oneForm.ShowDialog();
        }

        static public void CloseForm()
        {

            if (oneForm != null)
            {
                oneForm.Close();
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
        }
        private void btnRevovery_Click(object sender, EventArgs e)
        {
            // if (frmMain.UseerID=="")
            //    return;
            if (dbgWarning.SelectedRows.Count <= 0)
                return;
            if (MessageBox.Show("确定取消核实当前警告信息了吗？", "询问信息", MessageBoxButtons.OKCancel) != DialogResult.OK)
                return;
            string DataID = dbgWarning.SelectedRows[0].Cells[0].Value.ToString();
            frmMain.WarmingList.BeChecked(Convert.ToInt32(DataID), frmMain.UserID, true);
            int iIndex = dbgWarning.SelectedRows[0].Index;
            SqlExecutor.ShowData2DBGrid(dbgWarning, "select id,WaringID, rTime, wClass,wLevels, Warning, memo, CheckTime, UserID, ResetTime "
                + " from warning   where (ResetTime IS NULL )or (ResetTime>='" + DateTime.Now.ToString("yyyy-MM-dd 0:0:0")
                + "')order by rTime DESC");
            dbgWarning.Rows[iIndex].Selected = true;
            SqlExecutor.RecordLOG("用户操作", "取消确定警告信息", "用户：" + frmMain.UserID + "，数据：" + DataID);
        }

        private void btnCheck_Click(object sender, EventArgs e)
        {
            // if (frmMain.UseerID=="")
            //    return;
            //qiao
            frmMain.UserID = "qiao";
            if (dbgWarning.SelectedRows.Count <= 0)
                return;
            if (MessageBox.Show("确定已核实何当前警告信息了吗？", "询问信息", MessageBoxButtons.OKCancel) != DialogResult.OK)
                return;
            int iIndex = dbgWarning.SelectedRows[0].Index;
            string DataID = dbgWarning.SelectedRows[0].Cells[0].Value.ToString();
            frmMain.WarmingList.BeChecked(Convert.ToInt32(DataID), frmMain.UserID);
            SqlExecutor.ShowData2DBGrid(dbgWarning, "select id,WaringID, rTime, wClass, wLevels,Warning, memo, CheckTime, UserID, ResetTime "
                + " from warning   where (ResetTime IS NULL )or (ResetTime>='" + DateTime.Now.ToString("yyyy-MM-dd 0:0:0")
                + "')order by rTime DESC");
            dbgWarning.Rows[iIndex].Selected = true;
        }

        private void btnMain_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmMain.ShowMainForm();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            SqlExecutor.ShowData2DBGrid(dbgWarning, "select id,WaringID, rTime, wClass, Warning, wLevels,memo, CheckTime, UserID, ResetTime "
                             + " from warning  where (ResetTime IS NULL )or (ResetTime>='" + DateTime.Now.ToString("yyyy-MM-dd 0:0:0")
                             + "')order by rTime DESC");

        }

        private void btnAbout_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmAbout.ShowForm();
        }

        private void btnState_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmState.ShowForm();
        }

        private void btnLine_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmLine.ShowForm();
        }
    }
}
