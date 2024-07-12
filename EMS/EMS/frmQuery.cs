using System;
using System.Drawing;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmQuery : Form
    {
        public int DataIndex = 0;
        static public frmQuery oneForm = null;
        public frmQuery()
        {
            InitializeComponent();

            DoubleBuffered = true;
        }

        static public void ShowForm()
        {
            if (oneForm == null)
                oneForm = new frmQuery();
            oneForm.tneFromY.SetIntValue(DateTime.Now.AddHours(-2).Year);
            oneForm.tneFromM.SetIntValue(DateTime.Now.AddHours(-2).Month);
            oneForm.tneFromD.SetIntValue(DateTime.Now.AddHours(-2).Day);
            oneForm.tneFromH.SetIntValue((DateTime.Now.AddHours(-2)).Hour);
            oneForm.tneFrommm.SetIntValue((DateTime.Now.AddHours(-2)).Minute);

            oneForm.tneToY.SetIntValue(DateTime.Now.Year);
            oneForm.tneToM.SetIntValue(DateTime.Now.Month);
            oneForm.tneToD.SetIntValue(DateTime.Now.Day);
            oneForm.tneToH.SetIntValue(DateTime.Now.Hour);
            oneForm.tneTomm.SetIntValue(DateTime.Now.Minute);
            oneForm.btnBaseInf_Click(oneForm.btnBaseInf, EventArgs.Empty);
            oneForm.ShowDialog();
        }

        static public void CloseForm()
        {

            if (oneForm != null)
            {
                oneForm.Close();
                oneForm.Dispose();
                oneForm = null;
                GC.Collect();
            }
        }

        private void frmQuery_Load(object sender, EventArgs e)
        {
            tbAllControl.SelectedIndex = 0;
            //tabControl1.DrawMode = TabDrawMode.OwnerDrawFixed;
            // tabControl1.Alignment = TabAlignment.Left;
            //  tabControl1.SizeMode = TabSizeMode.Fixed;
            //  tabControl1.Multiline = true;
            //tabControl1.ItemSize = new Size(50, 80);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
        }

        //查询统计
        private void QueryData()
        {
            string strSQL = " where  rTime>='" + tneFromY.Value.ToString() + "-" + tneFromM.Value.ToString("D2") + "-" + tneFromD.Value.ToString("D2")
                + " " + tneFromH.Value.ToString() + ":" + tneFrommm.Value.ToString("D2")
                + ":00' and rTime <='" + tneToY.Value.ToString() + "-" + tneToM.Value.ToString("D2") + "-" + tneToD.Value.ToString("D2")
                + " " + tneToH.Value.ToString() + ":" + tneFrommm.Value.ToString("D2") + ":59' ";
            //DataIndex=tbAllControl.SelectedIndex;
            switch (DataIndex)
            {
                //case 0:
                //    DBConnection.ShowData2Chart(ctSOverall, "select  * from profit "+ strSQL,
                //        5, "yyyy-M-d");
                //    break;
                case 1:
                    // DBConnection.ShowData2ChartPower(ctPower, "select rTime, Gridkva ,AllAAkva from elemeter2 " + strSQL,
                    //   3, "yyyy-M-d  H:mm");
                    DBConnection.ShowData2Chart(ctPower, "select rTime, Gridkva,AllUkva ,Subkw  from elemeter2" + strSQL,
                       3, "M-d  H:mm");
                    break;
                case 2:
                    SqlExecutor.SetDBGrid(bdgelemeter);
                    SqlExecutor.ShowData2DBGrid(bdgelemeter, "select rTime,AllUkva,AllNukva,AllAAkva,AllPFoctor,Subkw,PlanKW from elemeter2" + strSQL);
                    break;
                case 3:
                    DBConnection.ShowData2Chart(ctPCS, "select rTime, allUkwa,allNUkwr,allAkwa   from pcs" + strSQL,
                       3, "M-d  H:mm");
                    DBConnection.ShowData2Chart(ctPCSTemp, "select rTime, PCSTemp  from pcs" + strSQL,     1, "M-d  H:mm");
                    
                    break;
                case 4:
                    SqlExecutor.SetDBGrid(dgBattery);
                    SqlExecutor.ShowData2DBGrid(dgBattery, "select * from battery" + strSQL);
                    break;
                case 5:
                    DBConnection.ShowData2Chart(ctCellV, "select rTime, averageV from battery" + strSQL, 1, "M-d H:mm");
                    DBConnection.ShowData2Chart(ctCellTemp, "select rTime, averageTemp from battery" + strSQL, 1, "M-d H:mm");
                    DBConnection.ShowData2Chart(ctCellA, "select rTime, a from battery" + strSQL, 1, "M-d H:mm");

                    break;
                case 6:
                    DBConnection.ShowData2Chart(ctAir, "select rTime,environmentTemp,indoorTemp,evaporationTemp,condenserTemp from tempcontrol" + strSQL,
                       3, "H:m");
                    break;
                case 7:
                    SqlExecutor.SetDBGrid(dbgProfit);
                    SqlExecutor.ShowData2DBGrid(dbgProfit, "select * from profit" + strSQL);
                    break;
                case 8:
                    SqlExecutor.SetDBGrid(dbgControl);
                    SqlExecutor.ShowData2DBGrid(dbgControl, "select * from pncontroler" + strSQL);
                    break;

            }
            GC.Collect();
        }

        private void btnMain_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmMain.ShowMainForm();
        }

        private void btnQuery_Click_1(object sender, EventArgs e)
        {
            QueryData();
        }

        private void btnBaseInf_Click(object sender, EventArgs e)
        {
            Button oneBTN = (Button)sender;
            DataIndex = Convert.ToInt32(oneBTN.Tag.ToString());
            btnBaseInf.BackColor = ((DataIndex == Convert.ToInt32(btnBaseInf.Tag.ToString())) ? Color.FromArgb(20, 169, 255) : Color.Transparent);
            btnAir.BackColor = ((DataIndex == Convert.ToInt32(btnAir.Tag.ToString())) ? Color.FromArgb(20, 169, 255) : Color.Transparent);
            btnBMS.BackColor = ((DataIndex == Convert.ToInt32(btnBMS.Tag.ToString())) ? Color.FromArgb(20, 169, 255) : Color.Transparent);
            btnCells.BackColor = ((DataIndex == Convert.ToInt32(btnCells.Tag.ToString())) ? Color.FromArgb(20, 169, 255) : Color.Transparent);
            btnE.BackColor = ((DataIndex == Convert.ToInt32(btnE.Tag.ToString())) ? Color.FromArgb(20, 169, 255) : Color.Transparent);
            btnPCS.BackColor = ((DataIndex == Convert.ToInt32(btnPCS.Tag.ToString())) ? Color.FromArgb(20, 169, 255) : Color.Transparent);
            btnProfit.BackColor = ((DataIndex == Convert.ToInt32(btnProfit.Tag.ToString())) ? Color.FromArgb(20, 169, 255) : Color.Transparent);
            tpAir.Parent = ((DataIndex == Convert.ToInt32(btnAir.Tag.ToString())) ? tbAllControl : null);
            tpBaseInf.Parent = ((DataIndex == Convert.ToInt32(btnBaseInf.Tag.ToString())) ? tbAllControl : null);
            tpButtery.Parent = ((DataIndex == Convert.ToInt32(btnBMS.Tag.ToString())) ? tbAllControl : null);
            tpCells.Parent = ((DataIndex == Convert.ToInt32(btnCells.Tag.ToString())) ? tbAllControl : null);
            tpE.Parent = ((DataIndex == Convert.ToInt32(btnE.Tag.ToString())) ? tbAllControl : null);
            tpPCS.Parent = ((DataIndex == Convert.ToInt32(btnPCS.Tag.ToString())) ? tbAllControl : null);
            tpProfit.Parent = ((DataIndex == Convert.ToInt32(btnProfit.Tag.ToString())) ? tbAllControl : null);
            tpGCTL.Parent = null;
            tpPower.Parent = null;
            // tbAllControl
            QueryData();
        }


    }
}
