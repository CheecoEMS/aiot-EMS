using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace EMS
{
    public partial class TRadioButton : UserControl
    {
        public TRadioButton()
        {
            InitializeComponent();
            Checked = false;
            Caption = "RadioButton";
            BackColor = Color.Transparent;
            Height = 32;
        }

        public TRadioButton(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }

        private bool bChecked = false;
        private bool OldChecked = false;
        public bool Checked
        {
            get
            { return bChecked; }
            set
            {
                bChecked = value;
                if (bChecked)
                    SetCheck(true);
                else
                    Refresh();
            }
        }



        public string Caption { get; set; }

        private void TRadioButton_Resize(object sender, EventArgs e)
        {

        }

        private void TRadioButton_Paint(object sender, PaintEventArgs e)
        {
            int tempCoreX = Height / 4;
            int tempCoreH = Height / 2 - 1;

            Graphics g = e.Graphics;

            //大的背景色
            Brush bushBack;
            if (this.Enabled)
                bushBack = new SolidBrush(Color.FromArgb(20, 169, 255));//填充的颜色RoyalBlue
            else
                bushBack = new SolidBrush(Color.Red);
            g.FillEllipse(bushBack, 0, 0, Height, Height);

            // 黑色框  
            Pen pen = new Pen(Color.Black);//画笔颜色 
            g.DrawEllipse(pen, 0, 0, Height - 1, Height - 1);
            //
            if (Checked)
            {
                Brush bush = new SolidBrush(Color.GreenYellow);
                g.FillEllipse(bush, tempCoreX, tempCoreX, tempCoreH, tempCoreH);
                //DrawRectangle
            }
            else
            {
                Brush bush = new SolidBrush(Parent.BackColor);
                g.FillEllipse(bush, tempCoreX, tempCoreX, tempCoreH, tempCoreH);// 
            }
            g.DrawEllipse(pen, tempCoreX, tempCoreX, tempCoreH, tempCoreH);// 黑色框

            //显示文件管理 
            Brush BLACK_BRUSH;
            if (bChecked == OldChecked)
                BLACK_BRUSH = new SolidBrush(Color.White);
            else
                BLACK_BRUSH = new SolidBrush(Color.GreenYellow);
            g.DrawString(Caption, this.Font, BLACK_BRUSH, 38, 5);
        }


        public void SetBoolValue(bool aChecked)
        {
            if (Checked != aChecked)
            {
                Checked = aChecked;
                this.Refresh();
            }
            OldChecked = aChecked;
        }


        private void SetCheck(bool aChecked)
        {
            if (!aChecked)
                return;
            TRadioButton tempTRTemp;
            foreach (Control tempCtl in this.Parent.Controls)
            {
                if ((tempCtl == this) || (!(tempCtl is TRadioButton)))
                    continue;
                tempTRTemp = (TRadioButton)tempCtl;
                tempTRTemp.Checked = false;
                tempTRTemp.Refresh();
            }
            Refresh();
        }


        private void TRadioButton_MouseUp(object sender, MouseEventArgs e)
        {
            Checked = true;
            SetCheck(true);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // TRadioButton
            //  
            this.BackColor = System.Drawing.Color.Transparent;
            this.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.ForeColor = System.Drawing.Color.White;
            this.Name = "TRadioButton";
            this.Size = new System.Drawing.Size(235, 32);
            this.Load += new System.EventHandler(this.TRadioButton_Load);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.TRadioButton_Paint);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.TRadioButton_MouseUp);
            this.ResumeLayout(false);

        }

        private void TRadioButton_Load(object sender, EventArgs e)
        {

        }
    }
}
