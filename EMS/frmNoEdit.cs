using System;
using System.Drawing;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmNoEdit : Form
    {
        public static frmNoEdit OneEditForm = null;
        private static frmNoEdit frmSelf = null;
        private static int iBackData = 0;
        private int iMaxData = 0;
        private int iMinData = 0;
        private int iNowData = 0;
        private int iDefaultData = 0;
        private int iStep = 0;
        private TouchNumberEdit oneNoEditer = null;

        private static string strBackData = "*";
        private string strNowData = "";
        //private bool bIsPassword = false;
        private int iMaxLen = 0;


        //string strCapTop = "Home             Settings"; 
        private string strCapbtnMax = "   ";
        private string strCapbtnMin = "   ";
        private string strCapbtnDefault = "   ";
        private bool FoceInZone;

        private bool bEdited = false;
        private int iOldData = 0;

        public frmNoEdit()
        {
            InitializeComponent();
        }

        private void frmNoEdit_Load(object sender, EventArgs e)
        {

        }

        static public void EditData(int aMaximum, int aMinimum, int aDefaultValue, int aValue,
            int aValueStep,
            string aDefCap, bool aFoceInZone, ref int aNewValue, TouchNumberEdit aOntTNE)
        {
            if (OneEditForm == null)
                OneEditForm = new frmNoEdit();
            if ((aDefaultValue > aMaximum) || (aDefaultValue < aMinimum))
                aDefaultValue = aValue;
            OneEditForm.Left = 0;
            OneEditForm.Top = 0;
            OneEditForm.labCap.Text = aDefCap;
            OneEditForm.iMaxData = aMaximum;
            OneEditForm.iMinData = aMinimum;
            OneEditForm.iDefaultData = aDefaultValue;
            OneEditForm.iNowData = aValue;
            OneEditForm.iStep = aValueStep;
            OneEditForm.oneNoEditer = aOntTNE;
            iBackData = 0;//
            OneEditForm.iOldData = aValue;
            OneEditForm.labValue.Text = aValue.ToString();
            OneEditForm.bEdited = false;
            //OneEditForm.labValue.Text = aDefaultValue.ToString();
            OneEditForm.strCapbtnDefault = "Def:" + aDefaultValue.ToString();
            OneEditForm.strCapbtnMax = "Max:" + aMaximum.ToString();
            OneEditForm.strCapbtnMin = "Min:" + aMinimum.ToString();
            OneEditForm.FoceInZone = aFoceInZone;
            frmSelf = OneEditForm;
            OneEditForm.ShowDialog();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            bEdited = true;
            iNowData = 0;
            labValue.Text = "";
        }

        private void btnCanncel_Click(object sender, EventArgs e)
        {
            strBackData = "*";
            iBackData = 0;
            this.Close();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            if (((iMaxData == iMinData) && (!FoceInZone)) || ((iNowData <= iMaxData) && (iNowData >= iMinData)))
            {
                iBackData = (int)iNowData;
                strBackData = strNowData;
                oneNoEditer.SetNewValue(iNowData);
                //this.Hide();
                //this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void pbMax_Click(object sender, EventArgs e)
        {
            bEdited = true;
            iNowData = iMaxData;
            labValue.Text = iNowData.ToString();
        }

        private void pbMax_Paint(object sender, PaintEventArgs e)
        {
            //显示文件信息
            Graphics g = e.Graphics;
            Brush BLACK_BRUSH = new SolidBrush(Color.White);  //更新界面时候需要
            g.DrawString(strCapbtnMax, pbMin.Parent.Font, BLACK_BRUSH, 10, 15);
            BLACK_BRUSH.Dispose();
        }

        private void pbMax_MouseDown(object sender, MouseEventArgs e)
        {
            PictureBox tempPBTN = (PictureBox)sender;
            Graphics g = tempPBTN.CreateGraphics();
            Pen RectPen = new Pen(Color.FromArgb(20, 169, 255), 2);//画笔颜色 
            g.DrawRectangle(RectPen, 0, 0, tempPBTN.Width - 1, tempPBTN.Height - 1);// 黑色框  
        }

        private void pictureBox26_MouseUp(object sender, MouseEventArgs e)
        {
            PictureBox tempPBTN = (PictureBox)sender;
            Graphics g = tempPBTN.CreateGraphics();
            Pen RectPen = new Pen(Color.FromArgb(42, 55, 64), 2);//画笔颜色
            g.DrawRectangle(RectPen, 0, 0, tempPBTN.Width - 1, tempPBTN.Height - 1);// 黑色框  
            int iCap;

            //if (!bIsPassword)
            {
                PictureBox temBtn = (PictureBox)sender;
                if (!bEdited)
                {
                    iCap = Convert.ToInt16(temBtn.Tag);
                }
                else
                {
                    if (labValue.Text == "-")
                    {
                        if ((int)temBtn.Tag != 0)
                            iCap = -1 * Convert.ToInt16(temBtn.Tag);
                        else
                            return;
                    }
                    else if (iNowData >= 0)
                        iCap = iNowData * 10 + Convert.ToInt16(temBtn.Tag);
                    else
                        iCap = iNowData * 10 - Convert.ToInt16(temBtn.Tag);
                }

                //if (((iCap >= iMinData) && (iCap <= iMaxData))||(iMaxData==iMinData)) 
                if ((iMaxData != iMinData) && (iCap > iMaxData))
                    iCap = iMaxData;
                if ((iMaxData == iMinData) && (iCap > 99999999))
                    iCap = 99999999;
                // if ((iMaxData == iMinData) || ((temValue <= iMaxData) && (temValue >= iMinData)))
                //     iNowData = temValue;                 
                bEdited = true;
                iNowData = iCap;
                labValue.Text = iNowData.ToString();
            }
        }

        private void pbDef_Click(object sender, EventArgs e)
        {
            bEdited = true;
            iNowData = iMinData;
            labValue.Text = iNowData.ToString();
        }

        private void pbMin_Click(object sender, EventArgs e)
        {
            bEdited = true;
            iNowData = iDefaultData;
            labValue.Text = iNowData.ToString();
        }

        private void pbDel_MouseUp(object sender, MouseEventArgs e)
        {
            PictureBox tempPBTN = (PictureBox)sender;
            Graphics g = tempPBTN.CreateGraphics();
            Pen RectPen = new Pen(Color.Black, 2);//画笔颜色
            g.DrawRectangle(RectPen, 0, 0, tempPBTN.Width - 1, tempPBTN.Height - 1);// 黑色框  


            //do del
            //if (!bIsPassword)
            {
                iNowData = iNowData / 10;
                if (iNowData == 0)
                    labValue.Text = "";
                else
                    labValue.Text = iNowData.ToString();
            }
        }
        private void pbAdd_Click(object sender, EventArgs e)
        {
            if (!bEdited)
                iNowData = iOldData;
            bEdited = true;
            if ((iNowData < iMaxData) || (iMinData == iMaxData))
                iNowData += iStep;
            labValue.Text = iNowData.ToString();
        }
        private void pbRed_Click(object sender, EventArgs e)
        {
            if (!bEdited)
                iNowData = iOldData;
            bEdited = true;
            if (labValue.Text != "")
            {
                if ((iNowData > iMinData) || (iMinData == iMaxData))
                    iNowData -= iStep;
                labValue.Text = iNowData.ToString();
            }
            else if (iMinData < 0)
            {
                iNowData = 0;
                labValue.Text = "-";
            }
        }

        private void pbMax_MouseUp(object sender, MouseEventArgs e)
        {
            PictureBox tempPBTN = (PictureBox)sender;
            Graphics g = tempPBTN.CreateGraphics();
            Pen RectPen = new Pen(Color.Black, 2);//画笔颜色
            g.DrawRectangle(RectPen, 0, 0, tempPBTN.Width - 1, tempPBTN.Height - 1);// 黑色框  
        }

        private void pbMin_Paint(object sender, PaintEventArgs e)
        {
            //显示文件信息
            Graphics g = e.Graphics;
            Brush BLACK_BRUSH = new SolidBrush(Color.White);  //更新界面时候需要
            g.DrawString(strCapbtnDefault, pbMin.Parent.Font, BLACK_BRUSH, 10, 15);
            BLACK_BRUSH.Dispose();
        }

        private void pbDef_Paint(object sender, PaintEventArgs e)
        {
            //显示文件信息
            Graphics g = e.Graphics;
            Brush BLACK_BRUSH = new SolidBrush(Color.White);  //更新界面时候需要
            g.DrawString(strCapbtnMin, pbMin.Parent.Font, BLACK_BRUSH, 10, 15);
            BLACK_BRUSH.Dispose();
        }

        private void pictureBox26_Click(object sender, EventArgs e)
        {

        }
    }
}
