using System;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmoneElectrovalence : Form
    {
        static private frmoneElectrovalence oneForm;
        public frmoneElectrovalence()
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

        private void frmoneElectrovalence_Load(object sender, EventArgs e)
        {

        }

        private bool CheckFormData()
        {
            if (tbeName.Text == "")
            {
                return false;
            }
            else
                return true;
        }

        static public void EditData(DataGridView aDBGrid)
        {
            if (oneForm == null)
                oneForm = new frmoneElectrovalence();
            string DataID = aDBGrid.SelectedRows[0].Cells[0].Value.ToString();
            int iSelectIndex = aDBGrid.SelectedRows[0].Index;
            oneForm.ShowData(aDBGrid);
            if (oneForm.ShowDialog() == DialogResult.OK)
            {
                DateTime dtTemp = Convert.ToDateTime("2022-01-01 " + oneForm.tneH.Value.ToString() + ":"
                    + oneForm.tneM.Value.ToString() + ":0");
                DBConnection.ExecSQL("update  electrovalence  SET "
                      + " eName='" + oneForm.tbeName.SelectItemIndex.ToString()
                      + "',section='" + oneForm.tcbSection.SelectItemIndex.ToString()
                      + "', startTime= '" + dtTemp.ToString("H:m:0")
                      + "' where id='" + DataID + "'");

                DBConnection.ShowData2DBGrid(aDBGrid, "select * from electrovalence order by section");
                //aDBGrid.Rows[0].Selected = false;
                aDBGrid.Rows[iSelectIndex].Selected = true;
                CloseForm();
            }
        }


        static public void AddData(DataGridView aDBGrid)
        {
            if (oneForm == null)
                oneForm = new frmoneElectrovalence();
            oneForm.CleanForm();
            if (oneForm.ShowDialog() == DialogResult.OK)
            {
                DateTime dtTemp = Convert.ToDateTime("2022-01-01 " + oneForm.tneH.Value.ToString() + ":"
                    + oneForm.tneM.Value.ToString() + ":0");
                DBConnection.ExecSQL("insert into electrovalence (section,eName,startTime)  values ('"
                      + oneForm.tcbSection.SelectItemIndex.ToString() + "','"
                      + oneForm.tbeName.strText + "','"
                      + dtTemp.ToString("H:m:s") + "')");

                DBConnection.ShowData2DBGrid(aDBGrid, "select * from electrovalence order by section");
                aDBGrid.Rows[aDBGrid.Rows.Count - 1].Selected = true;
                CloseForm();
            }
        }


        //显示数据
        private void ShowData(DataGridView aDBGrid)
        {
            string strSection = aDBGrid.SelectedRows[0].Cells[1].Value.ToString();
            try
            {
                if (strSection == "")
                    strSection = "0";
                tcbSection.SetSelectItemIndex(Convert.ToInt32(strSection));
                DateTime dtTemp = Convert.ToDateTime("2022-01-01 " + aDBGrid.SelectedRows[0].Cells[2].Value.ToString());
                tneH.SetIntValue(dtTemp.Hour);
                tneM.SetIntValue(dtTemp.Minute);
                tbeName.SetSelectItemIndex(Convert.ToInt32(aDBGrid.SelectedRows[0].Cells[3].Value.ToString()));
                // tbeName.SetstrText(aDBGrid.SelectedRows[0].Cells[3].Value.ToString());
                // nudMaxPower.Value = Convert.ToInt32(aDBGrid.SelectedRows[0].Cells[4].Value);
                // nudPrice.Value = Convert.ToInt32(aDBGrid.SelectedRows[0].Cells[5].Value.ToString());

            }
            catch
            { }
        }

        //清理数据
        private void CleanForm()
        {
            //dtpStartTime.Value = DateTime.Now;
            //tcbSection.SetSelectItemIndex(0);
            //tbeName.SetstrText("尖");

        }


        private void btnClose_Click(object sender, EventArgs e)
        {

        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
            //CloseForm();
        }
    }
}
