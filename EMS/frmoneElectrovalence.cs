using log4net;
using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmoneElectrovalence : Form
    {
        static private frmoneElectrovalence oneForm;
        private static ILog log = LogManager.GetLogger("frmoneElectrovalence");

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
                // string strDate = DateTime.Now.ToString("yyyy-MM-dd");

                // DateTime dtTemp = Convert.ToDateTime("2022-01-01 " + oneForm.tneH.Value.ToString() + ":"
                //     + oneForm.tneM.Value.ToString() + ":0");
                // DBConnection.ExecSQL("update  electrovalence  SET "
                //       + " eName='" + oneForm.tbeName.SelectItemIndex.ToString()
                //       + "',section='" + oneForm.tcbSection.SelectItemIndex.ToString()
                //       + "', startTime= '" + dtTemp.ToString("H:m:0")
                // + "' where id='" + DataID + "'");
                try
                {
                    string strDate = DateTime.Now.ToString("yyyy-MM-dd");

                    DateTime dtTemp = Convert.ToDateTime("2022-01-01 " + oneForm.tneH.Value.ToString() + ":"
                        + oneForm.tneM.Value.ToString() + ":0");
                        
                    string strSQL = "UPDATE electrovalence SET eName = @eName, section = @section, startTime = @startTime WHERE id = @id";
                    var updateParameters  = new Dictionary<string, object>
                    {
                        { "@eName", oneForm.tbeName.SelectItemIndex.ToString() },
                        { "@section", oneForm.tcbSection.SelectItemIndex.ToString() },
                        { "@startTime", dtTemp.ToString("H:m:0") },
                        { "@id", DataID }
                    };
                    DBConnection.ExecSQLWithParams(strSQL, updateParameters);

                    string sql = "SELECT * FROM electrovalence WHERE rTime = @DateParam ORDER BY section";
                    var parameters = new Dictionary<string, object>
                    {
                        { "@DateParam", DateTime.Today }
                    };

                    var dataTable = DBConnection.QueryDataTableWithParams(sql, parameters);
                    if (dataTable != null)
                    {
                        aDBGrid.DataSource = dataTable;
                    }
                    else
                    {
                        aDBGrid.DataSource = null;
                    }

                }catch (Exception ex)
                {
                    log.Error("EditData:" + ex);
                    aDBGrid.DataSource = null;
                }


                //DBConnection.ShowData2DBGrid(aDBGrid, "select * from electrovalence where rTime = '"+ strDate +"' order by section");
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
                // string strDate = DateTime.Now.ToString("yyyy-MM-dd");

                // DateTime dtTemp = Convert.ToDateTime("2022-01-01 " + oneForm.tneH.Value.ToString() + ":"
                //     + oneForm.tneM.Value.ToString() + ":0");
                // DBConnection.ExecSQL("insert into electrovalence (section,eName,startTime,rTime)  values ('"
                //       + oneForm.tcbSection.SelectItemIndex.ToString() + "','"
                //       + oneForm.tbeName.SelectItemIndex.ToString() + "','"
                //       + dtTemp.ToString("H:m:s") + "','"
                //       + strDate + "')");

                // DBConnection.ShowData2DBGrid(aDBGrid, "select * from electrovalence where rTime = '"+ strDate +"' order by section");
                
                try
                {
                    DateTime dtTemp = Convert.ToDateTime("2022-01-01 " + oneForm.tneH.Value.ToString() + ":"
                        + oneForm.tneM.Value.ToString() + ":0");
                        
                    string insertSQL = "INSERT INTO electrovalence (section, eName, startTime, rTime) VALUES (@section, @eName, @startTime, @rTime)";
                    var insertParameters = new Dictionary<string, object>
                    {
                        { "@section", oneForm.tcbSection.SelectItemIndex.ToString() },
                        { "@eName", oneForm.tbeName.SelectItemIndex.ToString() },
                        { "@startTime", dtTemp.ToString("H:m:s") },
                        { "@rTime", DateTime.Today }
                    };
                    DBConnection.ExecSQLWithParams(insertSQL, insertParameters);

                    string selectSQL = "SELECT * FROM electrovalence WHERE rTime = @date ORDER BY section";
                    var selectParameters = new Dictionary<string, object> { { "@date", DateTime.Today } };
                    var dataTable = DBConnection.QueryDataTableWithParams(selectSQL, selectParameters);
                    if (dataTable == null)
                    {
                        aDBGrid.DataSource = null;
                    }
                    aDBGrid.DataSource = dataTable;
                }
                catch (Exception ex)
                {
                    log.Error($"插入或查询电价值失败: {ex.Message}", ex);
                    aDBGrid.DataSource = null;
                }
                
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
