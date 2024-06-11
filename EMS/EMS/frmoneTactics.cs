using System;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmoneTactics : Form
    {
        static public frmoneTactics oneForm = null;

        public frmoneTactics()
        {
            InitializeComponent();

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


        private bool CheckFormData()
        {
            return true;
        }

        static public void EditData(DataGridView aDBGrid)
        {
            if (oneForm == null)
                oneForm = new frmoneTactics();
            string DataID = aDBGrid.SelectedRows[0].Cells[0].Value.ToString();
            int iSelectIndex = aDBGrid.SelectedRows[0].Index;
            oneForm.ShowData(aDBGrid);
            if (oneForm.ShowDialog() == DialogResult.OK)
            {//,,,,,,
                DBConnection.ExecSQL("update  tactics  SET "
                     + " tType='" + oneForm.tcbtType.strText
                     + "',PCSType='" + oneForm.tcbPCSType.strText
                     + "', waValue='" + oneForm.tnedwaValue.Value.ToString()
                     + "', startTime= '"
                           + oneForm.tneStartH.Value.ToString("D2") + ":"
                           + oneForm.tneStartm.Value.ToString("D2") + ":"
                           + oneForm.tneStartS.Value.ToString("D2")
                       //oneForm.dtpStartTime.Value.ToString("H:m:s")
                       + "', endTime= '"
                      + oneForm.tneEndH.Value.ToString("D2") + ":"
                    + oneForm.tneEndm.Value.ToString("D2") + ":"
                    + oneForm.tneEndS.Value.ToString("D2")
                     + "' where id='" + DataID + "'");

                DBConnection.ShowData2DBGrid(aDBGrid, "select * from tactics order by startTime");
                //aDBGrid.Rows[0].Selected = false;
                aDBGrid.Rows[iSelectIndex].Selected = true;
                CloseForm();
            }
        }

        static public void AddData(DataGridView aDBGrid)
        {
            if (oneForm == null)
                oneForm = new frmoneTactics();
            oneForm.CleanForm();
            if (oneForm.ShowDialog() == DialogResult.OK)
            {
                DBConnection.ExecSQL("insert into tactics (startTime,endTime,tType,PCSType,waValue) "
                    + "values ('"
                    + oneForm.tneStartH.Value.ToString("D2") + ":"
                    + oneForm.tneStartm.Value.ToString("D2") + ":"
                    + oneForm.tneStartS.Value.ToString("D2") + "','"
                    + oneForm.tneEndH.Value.ToString("D2") + ":"
                    + oneForm.tneEndm.Value.ToString("D2") + ":"
                    + oneForm.tneEndS.Value.ToString("D2") + "','"
                    + oneForm.tcbtType.strText + "','"
                    + oneForm.tcbPCSType.strText + "','"
                    + oneForm.tnedwaValue.Value.ToString() + "') ");

                DBConnection.ShowData2DBGrid(aDBGrid, "select * from tactics order by startTime");
                aDBGrid.Rows[aDBGrid.Rows.Count - 1].Selected = true;
                CloseForm();
            }
        }


        //显示数据
        private void ShowData(DataGridView aDBGrid)
        {
            //string[] workTypes = { "充电", "放电" };
            //int i = 0;
            try
            {
                DateTime dtTemp = Convert.ToDateTime("2022-01-01 " + aDBGrid.SelectedRows[0].Cells[1].Value.ToString());
                tneStartH.SetIntValue(dtTemp.Hour);
                tneStartm.SetIntValue(dtTemp.Minute);
                tneStartS.SetIntValue(dtTemp.Second);
                dtTemp = Convert.ToDateTime("2022-01-01 " + aDBGrid.SelectedRows[0].Cells[2].Value.ToString());
                tneEndH.SetIntValue(dtTemp.Hour);
                tneEndm.SetIntValue(dtTemp.Minute);
                tneEndS.SetIntValue(dtTemp.Second);
                //dtpEndTime.Value = Convert.ToDateTime("2022-01-01 " + aDBGrid.SelectedRows[0].Cells[2].Value.ToString());
                //i = Array.IndexOf(PCSClass.PCSTypes, );
                tcbtType.SetstrText(aDBGrid.SelectedRows[0].Cells[3].Value.ToString());
                tcbPCSType.SetstrText(aDBGrid.SelectedRows[0].Cells[4].Value.ToString());
                //SetSelectItemIndex(Array.IndexOf(workTypes, aDBGrid.SelectedRows[0].Cells[4].Value.ToString()));
                tnedwaValue.SetIntValue(Convert.ToInt32(aDBGrid.SelectedRows[0].Cells[5].Value));

            }
            catch
            { }
        }

        //清理数据
        private void CleanForm()
        {
            //dtpStartTime.Value = DateTime.Now;
            tcbtType.SetSelectItemIndex(0);
            tcbPCSType.SetSelectItemIndex(3);
            tnedwaValue.SetIntValue(100);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            CloseForm();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void label5_Click(object sender, EventArgs e)
        {

        }
    }
}
