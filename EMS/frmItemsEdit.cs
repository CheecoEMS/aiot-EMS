using System;
using System.Drawing;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmItemsEdit : Form
    {
        private static frmItemsEdit frmSelf = null;
        bool dragging = false;
        int VScrollValue = 0;
        public frmItemsEdit()
        {
            InitializeComponent();
        }

        public static void CloseSelf()
        {
            try
            {
                if (frmSelf != null)
                {
                    frmSelf.DialogResult = DialogResult.Cancel;
                    frmSelf.Close();
                    frmSelf = null;
                }
            }
            catch
            { }
        }

        public static int GetSelectedData(string[] AItems, int aDefaultIndex, string AstrText, string astrCaption)  //List<string> 
        {
            try
            {
                frmItemsEdit newFrom = new frmItemsEdit();
                newFrom.labCap.Text = AstrText;
                newFrom.lbDataList.Items.Clear();
                for (int i = 0; i < AItems.Length; i++)
                {
                    if (AItems[i] == "")
                        continue;
                    newFrom.lbDataList.Items.Add(AItems[i]);
                }
                newFrom.lbDataList.SelectedIndex = aDefaultIndex;
                //newFrom.Top = 0;

                newFrom.labCap.Text = astrCaption;
                //newFrom.pbtnCancel.Caption = frmMain.strCapList[53];
                //newFrom.pbtnOK.Caption = frmMain.strCapList[52];
                frmSelf = newFrom;
                newFrom.ShowDialog();
                frmSelf = null;
                if (newFrom.DialogResult == DialogResult.OK)
                {
                    aDefaultIndex = newFrom.lbDataList.SelectedIndex;
                }
                newFrom.Dispose();
                return aDefaultIndex;
            }
            catch (Exception e)
            {
                // frmMain.WriteLog(e.ToString());
                return aDefaultIndex;
            }
        }

        private void frmItemsEdit_Load(object sender, EventArgs e)
        {

        }

        private void btnCanncel_Click(object sender, EventArgs e)
        {
            this.Close();
            this.DialogResult = DialogResult.Cancel;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            this.Close();
            this.DialogResult = DialogResult.OK;
        }

        private void plTouch_MouseDown(object sender, MouseEventArgs e)
        {

            if (e.Button != MouseButtons.Left)
                return;
            VScrollValue = e.Y;
            dragging = true;
        }

        private void plTouch_MouseEnter(object sender, EventArgs e)
        {

        }

        private void plTouch_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            dragging = false;
            VScrollValue = 0;
        }

        private void pbtnUp_MouseDown(object sender, MouseEventArgs e)
        {
            PictureBox tempPBTN = (PictureBox)sender;
            Graphics g = tempPBTN.CreateGraphics();
            Pen RectPen = new Pen(Color.FromArgb(20, 169, 255), 2);//画笔颜色 
            g.DrawRectangle(RectPen, 0, 0, tempPBTN.Width - 1, tempPBTN.Height - 1);// 黑色框  
        }

        private void pbtnUp_MouseUp(object sender, MouseEventArgs e)
        {
            PictureBox tempPBTN = (PictureBox)sender;
            Graphics g = tempPBTN.CreateGraphics();
            Pen RectPen = new Pen(Color.FromArgb(75, 86, 93), 2);//画笔颜色
            g.DrawRectangle(RectPen, 0, 0, tempPBTN.Width - 1, tempPBTN.Height - 1);// 黑色框  
        }

        private void pbtnUp_Click(object sender, EventArgs e)
        {
            if (lbDataList.SelectedIndex > 0)
                lbDataList.SelectedIndex -= 1;
            //labValue.Text = lbDataList.SelectedValue.ToString();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            if (lbDataList.SelectedIndex < (lbDataList.Items.Count - 1))
                lbDataList.SelectedIndex += 1;
        }

        private void plTouch_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            if (dragging)
            {
                int tempHSValue = +lbDataList.SelectedIndex - (VScrollValue - e.Y) / 12;
                if (tempHSValue != lbDataList.SelectedIndex)
                {
                    VScrollValue = e.Y;
                    if (tempHSValue < 0)
                        lbDataList.SelectedIndex = 0;
                    else if (tempHSValue >= lbDataList.Items.Count)
                        lbDataList.SelectedIndex = lbDataList.Items.Count - 1;
                    else
                        lbDataList.SelectedIndex = tempHSValue;
                }
            }
        }
    }
}
