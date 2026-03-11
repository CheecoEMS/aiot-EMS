using log4net;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmoneTactics : Form
    {
        static public frmoneTactics oneForm = null;
        private static ILog log = LogManager.GetLogger("frmoneTactics");

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
                //string strDate = DateTime.Now.ToString("yyyy-MM-dd");
                // DBConnection.ExecSQL("update  tactics  SET "
                //      + " tType='" + oneForm.tcbtType.strText
                //      + "',PCSType='" + oneForm.tcbPCSType.strText
                //      + "', waValue='" + oneForm.tnedwaValue.Value.ToString()
                //      + "', startTime= '"
                //            + oneForm.tneStartH.Value.ToString("D2") + ":"
                //            + oneForm.tneStartm.Value.ToString("D2") + ":"
                //            + oneForm.tneStartS.Value.ToString("D2")
                //        //oneForm.dtpStartTime.Value.ToString("H:m:s")
                //        + "', endTime= '"
                //       + oneForm.tneEndH.Value.ToString("D2") + ":"
                //     + oneForm.tneEndm.Value.ToString("D2") + ":"
                //     + oneForm.tneEndS.Value.ToString("D2")
                //      + "' where id='" + DataID + "'");

                // DBConnection.ShowData2DBGrid(aDBGrid, "select * from tactics where rTime = '"+ strDate +"'order by starttime");
                
                try
                {
                    string updateSQL = "UPDATE tactics SET tType = @tType, PCSType = @PCSType, waValue = @waValue, " +
                        "startTime = @startTime, endTime = @endTime WHERE id = @id";
                    
                    string startTime = $"{oneForm.tneStartH.Value:D2}:{oneForm.tneStartm.Value:D2}:{oneForm.tneStartS.Value:D2}";
                    string endTime = $"{oneForm.tneEndH.Value:D2}:{oneForm.tneEndm.Value:D2}:{oneForm.tneEndS.Value:D2}";
                    
                    var updateParameters = new Dictionary<string, object>
                    {
                        { "@tType", oneForm.tcbtType.strText },
                        { "@PCSType", oneForm.tcbPCSType.strText },
                        { "@waValue", oneForm.tnedwaValue.Value },
                        { "@startTime", startTime },
                        { "@endTime", endTime },
                        { "@id", DataID }
                    };
                    
                    DBConnection.ExecSQLWithParams(updateSQL, updateParameters);

                    string selectSQL = "SELECT * FROM tactics WHERE rTime = @date ORDER BY starttime";
                    var selectParameters = new Dictionary<string, object> { { "@date", DateTime.Today } };
                    var dataTable = DBConnection.QueryDataTableWithParams(selectSQL, selectParameters);
                    aDBGrid.DataSource = dataTable;
                }
                catch (Exception ex)
                {
                    log.Error($"更新或查询策略数据失败: {ex.Message}", ex);
                    aDBGrid.DataSource = null;
                }
                
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
                // string strDate = DateTime.Now.ToString("yyyy-MM-dd");
                // DBConnection.ExecSQL("insert into tactics (startTime,endTime,tType,PCSType,waValue,rTime) "
                //     + "values ('"
                //     + oneForm.tneStartH.Value.ToString("D2") + ":"
                //     + oneForm.tneStartm.Value.ToString("D2") + ":"
                //     + oneForm.tneStartS.Value.ToString("D2") + "','"
                //     + oneForm.tneEndH.Value.ToString("D2") + ":"
                //     + oneForm.tneEndm.Value.ToString("D2") + ":"
                //     + oneForm.tneEndS.Value.ToString("D2") + "','"
                //     + oneForm.tcbtType.strText + "','"
                //     + oneForm.tcbPCSType.strText + "','"
                //     + oneForm.tnedwaValue.Value.ToString() + "','"
                //     + strDate  + "') ");

                // DBConnection.ShowData2DBGrid(aDBGrid, "select * from tactics where rTime = '"+ strDate +"'order by starttime");
                
                try
                {
                    string insertSQL = "INSERT INTO tactics (startTime, endTime, tType, PCSType, waValue, rTime) " +
                        "VALUES (@startTime, @endTime, @tType, @PCSType, @waValue, @rTime)";
                    
                    string startTime = $"{oneForm.tneStartH.Value:D2}:{oneForm.tneStartm.Value:D2}:{oneForm.tneStartS.Value:D2}";
                    string endTime = $"{oneForm.tneEndH.Value:D2}:{oneForm.tneEndm.Value:D2}:{oneForm.tneEndS.Value:D2}";
                    
                    var insertParameters = new Dictionary<string, object>
                    {
                        { "@startTime", startTime },
                        { "@endTime", endTime },
                        { "@tType", oneForm.tcbtType.strText },
                        { "@PCSType", oneForm.tcbPCSType.strText },
                        { "@waValue", oneForm.tnedwaValue.Value },
                        { "@rTime", DateTime.Today }
                    };
                    
                    DBConnection.ExecSQLWithParams(insertSQL, insertParameters);

                    string selectSQL = "SELECT * FROM tactics WHERE rTime = @date ORDER BY starttime";
                    var selectParameters = new Dictionary<string, object> { { "@date", DateTime.Today } };
                    var dataTable = DBConnection.QueryDataTableWithParams(selectSQL, selectParameters);
                    aDBGrid.DataSource = dataTable;
                }
                catch (Exception ex)
                {
                    log.Error($"插入或查询策略数据失败: {ex.Message}", ex);
                    aDBGrid.DataSource = null;
                }
                
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
