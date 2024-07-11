using MySqlX.XDevAPI.Common;
using System;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmoneEquipment : Form
    {
        static public frmoneEquipment oneForm = null;
        //private string DataID = ""; 
        public frmoneEquipment()
        {
            InitializeComponent();

        }


        //新增数据
        static public void AddData(DataGridView aDBGrid)
        {
            if (oneForm == null)
                oneForm = new frmoneEquipment();
            oneForm.CleanForm();
            if (oneForm.ShowDialog() == DialogResult.OK)
            {
                string tempComType = "0";//485
                if (oneForm.trbTCPModbus.Checked)
                    tempComType = "1";//TCP
                else if (oneForm.trbUDPModbus.Checked)
                    tempComType = "2";//UDP

                string tempTCPType = "server";
                if (oneForm.trbClient.Checked)
                    tempTCPType = "client";


                string sql = "insert into equipment(eID,eName,eType,eModel,comType,comName,comRate,comBits,TCPType,serverIP,SerPort,LocPort,pc) "
                    + "values ('" + oneForm.tcbSysID.strText + "','"
                    + oneForm.tbeName.Text + "','"
                    + oneForm.tcbType.strText + "','"
                    + oneForm.ceModel.Text + "','"
                    + tempComType + "','"
                    + oneForm.tcb485Port.strText + "','"
                    + oneForm.tcbBaudRate.strText + "','"
                    + oneForm.tcbDatabits.strText + "','"
                    + tempTCPType + "','"
                    + oneForm.tbServerIP.Text + "','"
                    + oneForm.tneServerPort.Value.ToString() + "','"
                    + oneForm.tneLocalPort.Value.ToString() + "','"
                    + oneForm.tnePC.Value.ToString()
                    + "')";

                try
                {
                    bool result = SqlExecutor.ExecuteSqlTaskAsync(sql, 3);

                    if (result)
                    {
                        // 处理执行成功的逻辑
                    }
                    else
                    {
                        // 处理执行失败的逻辑
                    }
                }
                catch (Exception ex)
                {
                    // 处理异常情况
                }

                SqlExecutor.ShowData2DBGrid(aDBGrid, "select * from equipment");
                aDBGrid.Rows[aDBGrid.Rows.Count - 1].Selected = true;
                CloseForm();
            }
        }

        //编辑数据
        static public void EditData(DataGridView aDBGrid)
        {
            if (oneForm == null)
                oneForm = new frmoneEquipment();
            string DataID = aDBGrid.SelectedRows[0].Cells[0].Value.ToString();
            int iSelectIndex = aDBGrid.SelectedRows[0].Index;
            oneForm.ShowData(aDBGrid);
            if (oneForm.ShowDialog() == DialogResult.OK)
            {
                int tempComType = 0;// "485";
                if (oneForm.trbTCPModbus.Checked)
                    tempComType = 1;// "TCP";
                else if (oneForm.trbUDPModbus.Checked)
                    tempComType = 2;// "UDP";

                string tempTCPType = "server";
                if (oneForm.trbClient.Checked)
                    tempTCPType = "client";


                string sql = "update  equipment  SET  "
                    + " eID= '" + oneForm.tcbSysID.strText
                    + "', eName='" + oneForm.tbeName.Text
                    + "',eType='" + oneForm.tcbType.strText
                    + "',eModel='" + oneForm.ceModel.Text
                    + "',comType='" + tempComType
                    + "',comName='" + oneForm.tcb485Port.strText
                    + "',comRate='" + oneForm.tcbBaudRate.strText
                    + "',comBits='" + oneForm.tcbDatabits.strText
                    + "',TCPType='" + tempTCPType
                    + "',serverIP='" + oneForm.tbServerIP.Text
                    + "',SerPort='" + oneForm.tneServerPort.Value.ToString()
                    + "',LocPort='" + oneForm.tneLocalPort.Value.ToString()
                    + "',pc='" + oneForm.tnePC.Value.ToString()
                    + "' where id='" + DataID + "'";

                try
                {
                    bool result = SqlExecutor.ExecuteSqlTaskAsync(sql, 3);

                    if (result)
                    {
                        // 处理执行成功的逻辑
                    }
                    else
                    {
                        // 处理执行失败的逻辑
                    }
                }
                catch (Exception ex)
                {
                    // 处理异常情况
                }

                SqlExecutor.ShowData2DBGrid(aDBGrid, "select * from equipment");
                //aDBGrid.Rows[0].Selected = false;
                aDBGrid.Rows[iSelectIndex].Selected = true;
                CloseForm();
            }
        }

        //关闭窗体并释放
        static public void CloseForm()
        {
            if (oneForm != null)
            {
                oneForm.Close();
                oneForm.Dispose();
                oneForm = null;
            }
        }

        //显示数据
        private void ShowData(DataGridView aDBGrid)
        {
            try
            {
                //oneForm.tcbSysID.SetSelectItemIndex = tcbSysID.Items.IndexOf(aDBGrid.SelectedRows[0].Cells[4].Value.ToString());
                oneForm.tcbSysID.SetstrText(aDBGrid.SelectedRows[0].Cells[4].Value.ToString());
                oneForm.tcbType.SetstrText(aDBGrid.SelectedRows[0].Cells[2].Value.ToString());
                oneForm.ceModel.Text = aDBGrid.SelectedRows[0].Cells[3].Value.ToString();
                int tempComType = Convert.ToInt32(aDBGrid.SelectedRows[0].Cells[5].Value.ToString());
                oneForm.trb485Modbus.SetBoolValue(tempComType == 0); //485
                oneForm.trbTCPModbus.SetBoolValue(tempComType == 1); //TCP
                oneForm.trbUDPModbus.SetBoolValue(tempComType == 2);//UDP

                oneForm.tcb485Port.SetstrText(aDBGrid.SelectedRows[0].Cells[6].Value.ToString());
                oneForm.tcbBaudRate.SetstrText(aDBGrid.SelectedRows[0].Cells[7].Value.ToString());
                oneForm.tcbDatabits.SetstrText(aDBGrid.SelectedRows[0].Cells[8].Value.ToString());
                string tempTCPType = aDBGrid.SelectedRows[0].Cells[10].Value.ToString();
                if (tempTCPType == "server")
                    oneForm.trbServer.SetBoolValue(true);
                else
                    oneForm.trbClient.SetBoolValue(true);

                oneForm.tbServerIP.Text = aDBGrid.SelectedRows[0].Cells[9].Value.ToString();
                oneForm.tneServerPort.SetIntValue(Convert.ToInt32(aDBGrid.SelectedRows[0].Cells[11].Value));
                oneForm.tneLocalPort.SetIntValue(Convert.ToInt32(aDBGrid.SelectedRows[0].Cells[12].Value.ToString()));
                oneForm.tbeName.Text = aDBGrid.SelectedRows[0].Cells[1].Value.ToString();
                oneForm.tnePC.SetIntValue(Convert.ToInt32(aDBGrid.SelectedRows[0].Cells[13].Value.ToString()));
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }

        //清理数据
        private void CleanForm()
        {
            tcbSysID.SelectItemIndex = 0;
            tcbType.SelectItemIndex = 0;
            ceModel.Text = "";
            tcb485Port.SelectItemIndex = 0;
            tcbBaudRate.SelectItemIndex = 6;
            tcbDatabits.SelectItemIndex = 1;
            tbServerIP.Text = "192.168.1.100";
            tneServerPort.Value = 9999;
            tneLocalPort.Value = 9998;
            trb485Modbus.Checked = true;
            trbTCPModbus.Checked = false;
            trbUDPModbus.Checked = false;
            trbClient.Checked = true;
            trbServer.Checked = false;
            tnePC.Value = 1;
            tbeName.Text = "";
        }

        private bool CheckFormData()
        {
            return true;
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            CloseForm();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (!CheckFormData())
                return;
            this.DialogResult = DialogResult.OK;
            Close();
        }

        private void tbServerIP_TextChanged(object sender, EventArgs e)
        {

        }

        private void tcbType_Load(object sender, EventArgs e)
        {

        }

        private void trb485Modbus_Load(object sender, EventArgs e)
        {

        }
    }
}
