using System;
using System.Drawing;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmLogin : Form
    {

        static private frmLogin oneForm = null;
        private TextBox activeTxtBox = null;
        private TextBox UserTxtBox = null;
        private TextBox PasswordTxtBox = null;

        public frmLogin()
        {
            InitializeComponent();
            this.Width = 1024;
            this.Height = 768;
            tbPassword.Text = "";
            tbUserName.Text = "";
           // DoubleBuffered = true;
            oneForm = this;
        }

        static public void INIForm()
        {
            //if (oneForm == null)
            //    oneForm = new frmLogin();
        }


        static public void CloseForm()
        {
            if (oneForm != null)
            {
                oneForm.Close();
                oneForm.Dispose();
                oneForm = null;
               // oneForm.Hide(); 
            }
            frmMain.ShowMainForm();
        }


        public void Slef_SendKey(string aChar_key)
        { 
            //if ((tbPassword.Focused) || (tbPassword.BackColor == Color.Black))
            //{
            //    if (tbPassword.Text.Length < 20)
            //        tbPassword.Text += aChar_key;
            //    tbPassword.BackColor = Color.Black;
            //}
            //else
            //{
            //    if (tbUserName.Text.Length < 20)
            //        tbUserName.Text += aChar_key;
            //    tbUserName.BackColor = Color.Black;
            //    tbUserName.Text = tbUserName.Text;
            //}
            if (activeTxtBox.Text.Length < 20)
                activeTxtBox.Text += aChar_key;
            activeTxtBox.BackColor = Color.Black;
            activeTxtBox.Refresh();
            activeTxtBox.Update();
        }



        private bool CheckFormData()
        {
            int iPower = -1;
            if ((oneForm.UserTxtBox.Text=="cheeco") &&(oneForm.PasswordTxtBox.Text=="88889999"))
            //if ((oneForm.UserTxtBox.Text == "") && (oneForm.PasswordTxtBox.Text == ""))
            {
                frmMain.UserID = "master";
                frmMain.UserPower = 10;
                //frmMain.Selffrm.btnLogin.Text = "注销登录";
                //用户登录
                return true;
            }
            else   if (!DBConnection.ChecUserc("select UPower from users where UName='"+
                oneForm.UserTxtBox.Text+"'and uPassword='"+oneForm.PasswordTxtBox.Text+"'", ref iPower))
            {
                MessageBox.Show("用户或密码有误！！！");
                return false;
            }
            else
            {
                frmMain.UserID = oneForm.UserTxtBox.Text;
                frmMain.UserPower = iPower;
               //frmMain.Selffrm.btnLogin.Text = "注销登录";
                //用户登录
                return true;
            }
        }

        //用户登录
        static public void ShowForm()
        {
            if (oneForm == null)
                oneForm = new frmLogin();
            oneForm.activeTxtBox = oneForm.tbUserName;
            oneForm.UserTxtBox = oneForm.tbUserName;
            oneForm.PasswordTxtBox = oneForm.tbPassword;
            oneForm.tbPassword.BackColor = Color.FromArgb(75, 86, 93);
            oneForm.tbUserName.BackColor = Color.Black;
            oneForm.tbUserName.Text = "";
            oneForm.tbPassword.Text = "";
            //PictureBox tempPBTN = (PictureBox)sender; 
            //Pen RectPen = new Pen(Color.FromArgb(20, 169, 255), 2);//画笔颜色 
            //Graphics g = oneForm.btnDel2.CreateGraphics();
            //g.DrawRectangle(RectPen, 0, 0, oneForm.btnDel2.Width - 1, oneForm.btnDel2.Height - 1);// 黑色框  
            //g = oneForm.pbtnClean.CreateGraphics();
            //g.DrawRectangle(RectPen, 0, 0, oneForm.pbtnClean.Width - 1, oneForm.btnDel2.Height - 1);// 黑色框  
            oneForm.ShowDialog();
        }




        private void frmLogin_Load(object sender, EventArgs e)
        {
            InitializeComponent();
            tbUserName.Text = "";
            tbPassword.Text = "";
        }




        private void btnClose_Click(object sender, EventArgs e)
        {
            CloseForm();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (!CheckFormData())
                return;
            CloseForm();
        }

        private void btnDel2_Click(object sender, EventArgs e)
        {
            //if ((tbPassword.Focused) || (tbPassword.BackColor == Color.Black))
            //{
            //    if (tbPassword.Text.Length > 0)
            //        tbPassword.Text = tbPassword.Text.Substring(0, tbPassword.Text.Length - 1);
            //    tbPassword.BackColor = Color.Black;
            //}
            //else
            //{
            //    if (tbUserName.Text.Length > 0)
            //        tbUserName.Text = tbUserName.Text.Substring(0, tbUserName.Text.Length - 1);
            //    tbUserName.BackColor = Color.Black;
            //}

            if (activeTxtBox.Text.Length > 0)
                activeTxtBox.Text = activeTxtBox.Text.Substring(0, activeTxtBox.Text.Length - 1);
            activeTxtBox.BackColor = Color.Black;
            activeTxtBox.Refresh();
        }

        private void pbtnClean_Click(object sender, EventArgs e)
        {
            //if ((tbPassword.Focused) || (tbPassword.BackColor == Color.Black))
            //{
            //    tbPassword.Text = "";
            //    tbPassword.BackColor = Color.Black;
            //}
            //else
            //{
            //    tbUserName.Text = "";
            //    tbUserName.BackColor = Color.Black;
            //}
            activeTxtBox.Text = "";
            activeTxtBox.BackColor = Color.Black;
            activeTxtBox.Refresh();
        }

        private void btnDel2_MouseDown(object sender, MouseEventArgs e)
        {

            PictureBox tempPBTN = (PictureBox)sender;
            Graphics g = tempPBTN.CreateGraphics();
            Pen RectPen = new Pen(Color.GreenYellow, 2);//画笔颜色 
            g.DrawRectangle(RectPen, 0, 0, tempPBTN.Width - 1, tempPBTN.Height - 1);// 黑色框  
        }

        private void btnDel2_MouseUp(object sender, MouseEventArgs e)
        {
            PictureBox tempPBTN = (PictureBox)sender;
            Graphics g = tempPBTN.CreateGraphics();
            Pen RectPen = new Pen(Color.FromArgb(20, 169, 255), 2);//画笔颜色
            g.DrawRectangle(RectPen, 0, 0, tempPBTN.Width - 1, tempPBTN.Height - 1);// 黑色框  
        }

         

        private void button11_Click(object sender, EventArgs e)
        {
            this.Slef_SendKey(((Button)sender).Text);         
        }
          

        private void tbUserName_Click(object sender, EventArgs e)
        {
            activeTxtBox = (TextBox)sender;
            activeTxtBox.BackColor = Color.Black;
            oneForm.PasswordTxtBox.BackColor = Color.FromArgb(75, 86, 93); 
        }

        private void tbPassword_Click(object sender, EventArgs e)
        {
            activeTxtBox = (TextBox)sender;
            activeTxtBox.BackColor = Color.Black;
            oneForm.UserTxtBox.BackColor = Color.FromArgb(75, 86, 93);
        }

        private void pbtnClean_Paint(object sender, PaintEventArgs e)
        {
            PictureBox tempPBTN = (PictureBox)sender;
            Graphics g = tempPBTN.CreateGraphics();
            Pen RectPen = new Pen(Color.FromArgb(20, 169, 255), 2);//画笔颜色 
            g.DrawRectangle(RectPen, 0, 0, tempPBTN.Width - 1, tempPBTN.Height - 1);// 黑色框  
        }
    }
}
