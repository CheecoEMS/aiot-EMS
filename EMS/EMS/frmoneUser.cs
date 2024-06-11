using System;
using System.Drawing;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmoneUser : Form
    {
        static private frmoneUser oneForm;
        bool IsEdit = false;
        string DataID;
        int iSelectIndex;
        DataGridView aDBGrid;
        public frmoneUser()
        {
            InitializeComponent();

        }
        static public void INIForm()
        {
            oneForm = new frmoneUser();
        }

        static public void CloseForm()
        {
            if (oneForm != null)
            {
                //oneForm.Close();
                //oneForm.Dispose();
                //oneForm = null;
                oneForm.Hide();
            }
        }

        public void Slef_SendKey(string aChar_key)
        {

            if ((tbPassword.Focused) || (tbPassword.BackColor == Color.Black))
            {
                if (tbPassword.Text.Length < 20)
                    tbPassword.Text += aChar_key;
                tbPassword.BackColor = Color.Black;
            }
            else
            {
                if (tbUserName.Text.Length < 20)
                    tbUserName.Text += aChar_key;
                tbUserName.BackColor = Color.Black;
            }
        }

        private bool CheckFormData()
        {
            if (tbUserName.Text == "")
                return false;
            else
                return true;
        }

        static public void EditData(DataGridView aDBGrid)
        {
            if (oneForm == null)
                oneForm = new frmoneUser();
            oneForm.aDBGrid = aDBGrid;
            oneForm.DataID = aDBGrid.SelectedRows[0].Cells[0].Value.ToString();
            oneForm.iSelectIndex = aDBGrid.SelectedRows[0].Index;
            oneForm.IsEdit = true;
            //oneForm.tbPassword
            oneForm.ShowData(aDBGrid);
            oneForm.Show();
        }

        static public void AddData(DataGridView aDBGrid)
        {
            if (oneForm == null)
                oneForm = new frmoneUser();
            oneForm.aDBGrid = aDBGrid;
            oneForm.CleanForm();
            oneForm.IsEdit = false;
            oneForm.Show();
        }


        //显示数据
        private void ShowData(DataGridView aDBGrid)
        {
            try
            {
                oneForm.tcbPower.SetSelectItemIndex(Convert.ToInt32(aDBGrid.SelectedRows[0].Cells[3].Value));
                oneForm.tbUserName.Text = aDBGrid.SelectedRows[0].Cells[1].Value.ToString();
                oneForm.tbPassword.Text = aDBGrid.SelectedRows[0].Cells[2].Value.ToString();
                tbUserName.BackColor = Color.Black;
                tbPassword.BackColor = Color.FromArgb(42, 55, 64);
                tbUserName.Focus();
            }
            catch
            { }
        }

        //清理数据
        private void CleanForm()
        {
            tcbPower.SelectItemIndex = 0;
            tbUserName.Text = "";
            tbPassword.Text = "";
            tbUserName.Focus();
            tbUserName.BackColor = Color.Black;
            tbPassword.BackColor = Color.FromArgb(42, 55, 64);
        }



        private void btnClose_Click(object sender, EventArgs e)
        {
            CloseForm();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (!CheckFormData())
                return;
            if (oneForm.IsEdit)
            {
                DBConnection.ExecSQL("update  users  SET "
                    + "UName= '" + oneForm.tbUserName.Text
                    + "',UPassword='" + oneForm.tbPassword.Text
                    + "',UPower='" + oneForm.tcbPower.SelectItemIndex.ToString()
                    + "',AddTime='" + DateTime.Now.ToString("yyyy-M-d H:m:s")
                     + "' where id='" + DataID + "'");

                DBConnection.ShowData2DBGrid(aDBGrid, "select * from users");
                //aDBGrid.Rows[0].Selected = false;
                aDBGrid.Rows[iSelectIndex].Selected = true;
                this.DialogResult = DialogResult.OK;
            }
            else
            {
                DBConnection.ExecSQL("insert into users (UName,UPassword,UPower,AddTime) "
                      + "values ('" + oneForm.tbUserName.Text + "','"
                      + oneForm.tbPassword.Text + "','"
                      + oneForm.tcbPower.SelectItemIndex.ToString() + "','"
                      + DateTime.Now.ToString("yyyy-M-d H:m:s") + "')");

                DBConnection.ShowData2DBGrid(aDBGrid, "select * from users");
                aDBGrid.Rows[aDBGrid.Rows.Count - 1].Selected = true;

            }
            this.Hide();
        }

        private void tbUserName_Click(object sender, EventArgs e)
        {
            tbUserName.Focus();
            tbUserName.BackColor = Color.Black;
            tbPassword.BackColor = Color.FromArgb(42, 55, 64);
        }

        private void tbPassword_Click(object sender, EventArgs e)
        {
            tbPassword.Focus();
            tbPassword.BackColor = Color.Black;
            tbUserName.BackColor = Color.FromArgb(42, 55, 64);
        }

        private void button9_Click(object sender, EventArgs e)
        {
            this.Slef_SendKey(((Button)sender).Text);
        }

        private void btnDel2_MouseDown(object sender, MouseEventArgs e)
        {
            PictureBox tempPBTN = (PictureBox)sender;
            Graphics g = tempPBTN.CreateGraphics();
            Pen RectPen = new Pen(Color.GreenYellow, 2);//画笔颜色 
            g.DrawRectangle(RectPen, 0, 0, tempPBTN.Width - 1, tempPBTN.Height - 1);// 黑色框  
        }

        private void pbtnClean_MouseUp(object sender, MouseEventArgs e)
        {
            PictureBox tempPBTN = (PictureBox)sender;
            Graphics g = tempPBTN.CreateGraphics();
            Pen RectPen = new Pen(Color.Black, 2);//画笔颜色
            g.DrawRectangle(RectPen, 0, 0, tempPBTN.Width - 1, tempPBTN.Height - 1);// 黑色框  

        }

        private void btnDel2_Click(object sender, EventArgs e)
        {
            if ((tbPassword.Focused) || (tbPassword.BackColor == Color.Black))
            {
                if (tbPassword.Text.Length > 0)
                    tbPassword.Text = tbPassword.Text.Substring(0, tbPassword.Text.Length - 1);
                tbPassword.BackColor = Color.Black;
            }
            else
            {
                if (tbUserName.Text.Length > 0)
                    tbUserName.Text = tbUserName.Text.Substring(0, tbUserName.Text.Length - 1);
                tbUserName.BackColor = Color.Black;
            }
        }

        private void pbtnClean_Click(object sender, EventArgs e)
        {
            if ((tbPassword.Focused) || (tbPassword.BackColor == Color.Black))
            {
                tbPassword.Text = "";
                tbPassword.BackColor = Color.Black;
            }
            else
            {
                tbUserName.Text = "";
                tbUserName.BackColor = Color.Black;
            }
        }

        private void tbPassword_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
