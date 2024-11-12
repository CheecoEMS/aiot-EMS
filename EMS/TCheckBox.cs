using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
namespace EMS
{
    public partial class TCheckBox : UserControl
    {
        public delegate void OnValueChangEventDelegate(object sender);//建立事件委托
        public event OnValueChangEventDelegate OnValueChange;//被选中的事件 



        public TCheckBox()
        {
            InitializeComponent();
            BackColor = Color.Transparent;
            Checked = false;
            Caption = "CheckBox";
            Height = 32;
            if (Parent != null)
                BackColor = Parent.BackColor;
        }

        public TCheckBox(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }

        public bool Checked { get; set; }
        private bool CheckedOld;


        public void SetValue(bool aBoolValue)
        {
            if (Checked != aBoolValue)
            {
                Checked = aBoolValue;
                CheckedOld = aBoolValue;
                Refresh();
            }
        }

        public string Caption { get; set; }


        private void TCheckBox_Paint(object sender, PaintEventArgs e)
        {
            int tempCoreX = Height / 5;
            int tempCoreH = Height * 3 / 5;

            Graphics g = e.Graphics;
            //大方块的颜色
            Brush bushBack;
            if (this.Enabled)
                bushBack = new SolidBrush(Color.FromArgb(20, 169, 255));//Color.FromArgb(0, 0, 192));//填充的颜色
            else
                bushBack = new SolidBrush(Color.Red);
            g.FillRectangle(bushBack, 0, 0, Height, Height);//大的背景色  
            Pen pen = new Pen(Color.Black);//画笔颜色
            g.DrawRectangle(pen, 0, 0, Height - 1, Height - 1);// 黑色框   
            Brush bushCore = new SolidBrush(Parent.BackColor); //中心颜色
            g.FillRectangle(bushCore, tempCoreX, tempCoreX, tempCoreH, tempCoreH);//  

            if (Checked)
            {
                Pen cPen = new Pen(Color.GreenYellow, 3);//画笔颜色
                g.DrawLine(cPen, tempCoreX, Height / 2, Height / 2 - 2, Height * 4 / 5 - 2);
                g.DrawLine(cPen, Height / 2 - 2, Height * 4 / 5 - 2, Height * 4 / 5 - 2, tempCoreX + 1);
            }
            else
            {
                // g.FillRectangle(bush, tempCoreX, tempCoreX, tempCoreH, tempCoreH);//  
            }
            g.DrawRectangle(pen, tempCoreX, tempCoreX, tempCoreH, tempCoreH);// 黑色框

            //显示文件管理 
            Brush BLACK_BRUSH;
            if (CheckedOld == Checked)
                BLACK_BRUSH = new SolidBrush(Color.White);  //没有改显示为白色
            else
                BLACK_BRUSH = new SolidBrush(Color.GreenYellow);  //改变显示为红色提示保存更新界面
            g.DrawString(Caption, this.Font, BLACK_BRUSH, 38, 5);
        }

        private void TCheckBox_MouseUp(object sender, MouseEventArgs e)
        {
            Checked = !Checked;
            Refresh();
            if (OnValueChange != null)
                OnValueChange(this);
            //this.OnClick(e);
        }

        private void TCheckBox_Load(object sender, EventArgs e)
        {

        }
    }
}
