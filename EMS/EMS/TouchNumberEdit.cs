using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace EMS

{
    public partial class TouchNumberEdit : UserControl
    {

        public TouchNumberEdit()
        {
            InitializeComponent();
            Value = 0;
            ValueStep = 1;
            CanEdit = true;
            //Height = 32;
            //Width = 124;
            DefaultValue = 0;
            if (Parent != null)
                BackColor = Parent.BackColor;
        }

        public TouchNumberEdit(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }
        public int DefaultValue
        {
            get;
            set;
        }
        //选中事件
        public delegate void OnValueChangEventDelegate(object sender);//建立事件委托
        public event OnValueChangEventDelegate OnValueChange;//被选中的事件 

        public Label CapLab = null;
        private string strCap = "";
        public string strText
        {
            get
            {
                if (CapLab != null)
                    return CapLab.Text;
                else
                    return strCap;
            }

            set
            {
                strCap = value;
            }
        }
        //
        public bool CanEdit
        {
            get;
            set;
        }

        public bool FoceInZone
        {
            get;
            set;
        }

        public int Maximum
        {
            get;
            set;
        }
        //
        public int Minimum
        {
            get;
            set;
        }
        //
        public int ValueStep
        {
            get;
            set;
        }

        private double iValue;
        private Label btnL;
        private Label btnR;
        private double ValueOld;
        public bool Changed
        {
            get
            {
                return (!(ValueOld == Value));
            }

        }
        public int Value
        {
            get
            {
                return (int)iValue;
            }

            set
            {
                iValue = value;
                // this.Refresh();
            }
        }

        //old value=new value
        public void UpdateVale()
        {
            ValueOld = Value;
            //Refresh();
        }

        public void SetIntValue(int aIntValue)
        {
            if (Minimum != Maximum)
            {
                if (aIntValue < Minimum)
                    aIntValue = Minimum;
                if (aIntValue > Maximum)
                    aIntValue = Maximum;
            }

            if (Value != aIntValue)
            {
                Value = aIntValue;
                //DefaultValue = aIntValue;
            }
            ValueOld = aIntValue;
        }

        public void SetValue(double adValue)
        {
            if (Minimum != Maximum)
            {
                if (adValue < Minimum)
                    adValue = Minimum;
                if (adValue > Maximum)
                    adValue = Maximum;
            }

            if (Value != adValue)
            {
                iValue = adValue;
                ValueOld = adValue;
                // DefaultValue = adValue;
            }
            ValueOld = adValue;
        }



        private void TouchNumberEdit_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            // g.Clear(Color.White);//this.BackColor
            //SolidBrush GrayBrush = new SolidBrush(Color.Black);
            Pen myPen;//画笔颜色
            if (CanEdit)//
            {
                btnL.BackColor = Color.FromArgb(20, 169, 255);
                btnR.BackColor = Color.FromArgb(20, 169, 255);
                myPen = new Pen(Color.White);
            }
            else
            {
                btnL.BackColor = Color.Black;
                btnR.BackColor = Color.Black;
                myPen = new Pen(Color.Red);
            }

            myPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;//-------Dash这样类型的虚线Dot  
            //g.DrawLine(myPen, pbLeft.Width, 0, Width - Height, 0);
            //g.DrawLine(myPen, pbLeft.Width, Height - 1, Width - Height, Height - 1);

            Brush Font_BRUSH;
            if ((ValueOld == Value) || (!CanEdit))
                Font_BRUSH = new SolidBrush(Color.White);  //更新界面时候需要Pen myPen = new Pen();FromArgb(64, 64, 64)
            else
                Font_BRUSH = new SolidBrush(Color.GreenYellow);
            string strTemp = iValue.ToString();
            int tempX = (int)(Width / 2 - strTemp.Length * 4.5);
            if (tempX < Height)
                tempX = Height;
            g.DrawString(strTemp, this.Font, Font_BRUSH, tempX, (Height - 18) / 2);//Height + 4
                                                                                   //BLACK_BRUSH.Dispose();
                                                                                   //frmMessageBox.ShowMessage("value:"+iValue.ToString() + "/Y:" + ValueY.ToString() + "  / 高" + Height.ToString()); 
                                                                                   // myPen.Dispose(); 
        }


        public void UpDateState()
        {
            //this.Refresh(); 
            //pbLeft.Refresh();
            //pbRight.Refresh();
        }


        private void TouchNumberEdit_MouseUp(object sender, MouseEventArgs e)
        {
            if (!CanEdit)
                return;
            //int OldValue = Value;
            //if (CapLab == null)
            //    Value = frnNoEdit.EditData(Maximum, Minimum, DefaultValue, Value, ValueStep, "", FoceInZone);
            //else
            //    Value = frnNoEdit.EditData(Maximum, Minimum, DefaultValue, Value, ValueStep, CapLab.Text,FoceInZone );

            int NewValue = 0;
            string DefCap = "";
            if (CapLab != null)
                DefCap = CapLab.Text;


            frmNoEdit.EditData(Maximum, Minimum, DefaultValue, Value, ValueStep, DefCap, FoceInZone, ref NewValue, this);
            this.Refresh();
        }

        public void SetNewValue(int NewValue)
        {
            if (((NewValue < Minimum) || (NewValue > Maximum)) & (Minimum != Maximum))
                return;
            if (NewValue != Value)
            {
                Value = NewValue;
                if (OnValueChange != null)
                    OnValueChange(this);
                this.Refresh();
            }
        }

        private void InitializeComponent()
        {
            this.btnL = new System.Windows.Forms.Label();
            this.btnR = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnL
            // 
            this.btnL.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnL.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnL.Font = new System.Drawing.Font("微软雅黑", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnL.Location = new System.Drawing.Point(0, 0);
            this.btnL.Name = "btnL";
            this.btnL.Size = new System.Drawing.Size(32, 32);
            this.btnL.TabIndex = 1;
            this.btnL.Text = "-";
            this.btnL.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnL.Click += new System.EventHandler(this.btnL_Click);
            this.btnL.MouseDown += new System.Windows.Forms.MouseEventHandler(this.btnR_MouseDown);
            this.btnL.MouseUp += new System.Windows.Forms.MouseEventHandler(this.btnR_MouseUp);
            // 
            // btnR
            // 
            this.btnR.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnR.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnR.Font = new System.Drawing.Font("微软雅黑", 15F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnR.Location = new System.Drawing.Point(138, 0);
            this.btnR.Name = "btnR";
            this.btnR.Size = new System.Drawing.Size(32, 32);
            this.btnR.TabIndex = 2;
            this.btnR.Text = "+";
            this.btnR.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnR.Click += new System.EventHandler(this.btnR_Click);
            this.btnR.MouseDown += new System.Windows.Forms.MouseEventHandler(this.btnR_MouseDown);
            this.btnR.MouseUp += new System.Windows.Forms.MouseEventHandler(this.btnR_MouseUp);
            // 
            // TouchNumberEdit
            // 
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.Controls.Add(this.btnR);
            this.Controls.Add(this.btnL);
            this.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.ForeColor = System.Drawing.Color.White;
            this.Name = "TouchNumberEdit";
            this.Size = new System.Drawing.Size(170, 32);
            this.Load += new System.EventHandler(this.TouchNumberEdit_Load);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.TouchNumberEdit_Paint);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.TouchNumberEdit_MouseUp);
            this.Resize += new System.EventHandler(this.TouchNumberEdit_Resize_1);
            this.ResumeLayout(false);

        }

        private void TouchNumberEdit_Resize_1(object sender, EventArgs e)
        {
            btnL.Width = this.Height;
            btnR.Width = this.Height;
        }

        private void btnL_Click(object sender, EventArgs e)
        {
            SetNewValue(Value - 1);
        }

        private void btnR_Click(object sender, EventArgs e)
        {
            SetNewValue(Value + 1);
        }

        private void btnR_MouseDown(object sender, MouseEventArgs e)
        {
            ((Label)sender).BackColor = Color.Black;
        }

        private void btnR_MouseUp(object sender, MouseEventArgs e)
        {
            ((Label)sender).BackColor = Color.FromArgb(20, 169, 255);
        }

        private void TouchNumberEdit_Load(object sender, EventArgs e)
        {

        }
    }
}
