using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace EMS
{
    // public partial
    public partial class TouchCombox : UserControl
    {
        Label CapLab = null;
        public bool CanEdit = false;
        private Label btnR;
        private Label btnL;
        public bool CanControl = true;
        public delegate void OnValueChangEventDelegate(object sender);//建立事件委托
        public event OnValueChangEventDelegate OnValueChange;//被选中的事件 

        public TouchCombox()
        {
            InitializeComponent();
            SelectItemIndex = -1;
            CenterShow = true;
            //Height = 32;
            //Width = 124;
            if (Parent != null)
                BackColor = Parent.BackColor;
        }
        public TouchCombox(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }


        //选择的
        public int SelectItemIndex
        {
            get;
            set;
        }
        //列表
        public List<string> ItemList = new List<string>();
        public void UpDataList()
        {
            Items = ItemList.ToArray();
            GC.Collect();
        }

        public void GetList()
        {
            ItemList.Clear();
            ItemList = new List<string>(Items);
            //GC.Collect();
        }

        public string[] Items//ListViewItemCollectionObjectCollection 
        {
            get;
            set;
        }

        public bool CenterShow//ListViewItemCollectionObjectCollection 
        {
            get;
            set;
        }

        //System.String[] str = { }; 
        public string strTextOld;
        //显示的文字
        public string strText
        {
            get
            {
                if (CanEdit)
                {
                    return strText;
                }
                else
                {
                    if (Items == null)
                        return "";
                    else if ((SelectItemIndex < Items.Length) && (SelectItemIndex > -1))
                        return (string)Items[SelectItemIndex];
                    else
                        return "";
                }
            }
            set
            {


            }
        }

        //old value=new value
        public void UpdateVale()
        {
            strTextOld = strText;
            //Refresh();
        }

        public void SetstrText(string aSetStrText)
        {
            SelectItemIndex = 0;
            strText = aSetStrText;

            if (Items == null)
                return;

            for (int i = 0; i < Items.Length; i++)
            {
                if (aSetStrText == (string)Items[i])
                {
                    SelectItemIndex = i;
                    break;
                }
            }
            strTextOld = strText;

        }

        public void SetSelectItemIndex(int aSelectIndex)
        {
            if ((aSelectIndex < 0) || (aSelectIndex >= Items.Length))
                return;
            if (SelectItemIndex != aSelectIndex)
            {
                SelectItemIndex = aSelectIndex;
                strText = Items[aSelectIndex];
                this.Refresh();
            }
            strTextOld = strText;
            //this.Update();
        }

        public void SetIntValue(int aIntValue)
        {
            Value = aIntValue;
            strTextOld = strText;
            //this.Refresh();
        }

        //
        public int Value
        {
            get
            {
                try
                {
                    string strData = (string)Items[SelectItemIndex];
                    if (strData == "")
                        return 0;
                    else
                        return Convert.ToInt16(strData);
                }
                catch
                {
                    return 0;
                }
            }

            set
            {//qiao 可优化
                string strValue = value.ToString();
                SelectItemIndex = 0;
                if (Items == null)
                    return;
                for (int i = 0; i < Items.Length; i++)
                {
                    if (strValue == (string)Items[i])
                    {
                        SelectItemIndex = i;
                        strText = strValue;
                        this.Refresh();
                        break;
                    }
                }

            }

        }


        private void TouchCombox_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Pen myPen;//画笔颜色
            if (CanControl)
                myPen = new Pen(Color.White);
            else
                myPen = new Pen(Color.Red);
            myPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;//-------Dash这样类型的虚线Dot 
                                                                       //qiao
                                                                       // g.DrawLine(myPen, pbLeft.Width, 0, Width - Height, 0);
                                                                       // g.DrawLine(myPen, pbLeft.Width, Height - 1, Width - Height, Height - 1);

            Brush Font_BRUSH;
            if (strTextOld == strText)
                Font_BRUSH = new SolidBrush(Color.White);  //更新界面时候需要Pen myPen = new Pen();FromArgb(64, 64, 64)
            else
                Font_BRUSH = new SolidBrush(Color.GreenYellow);

            int LabX = Height + 3;
            try
            {

                if (CenterShow)
                    LabX = (int)((Width - System.Text.Encoding.Default.GetBytes(strText).Length * 9) / 2) + 2;//7.5

                if (LabX < 1)
                    LabX = 1;
            }
            catch { }
            g.DrawString(strText, this.Font, Font_BRUSH, LabX, (Height - 18) / 2);//
            //BLACK_BRUSH.Dispose();
            //frmMessageBox.ShowMessage("value:"+iValue.ToString() + "/Y:" + ValueY.ToString() + "  / 高" + Height.ToString()); 
            //myPen.Dispose(); 
        }

        private void TouchCombox_Resize(object sender, EventArgs e)
        {
            btnL.Width = this.Height;
            btnL.Height = this.Height;
            btnR.Width = this.Height;
            btnR.Height = this.Height;
        }

        private void pbRight_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Pen myPen;//画笔颜色
            Brush BLACK_BRUSH;
            if (CanControl)
            {
                myPen = new Pen(Color.FromArgb(20, 169, 255));
                BLACK_BRUSH = new SolidBrush(Color.White);  //更新界面时候需要
            }
            else
            {
                myPen = new Pen(Color.Red);
                BLACK_BRUSH = new SolidBrush(Color.Red);  //更新界面时候需要
            }

            g.DrawRectangle(myPen, 0, 0, Height - 1, Height - 1);// 黑色框  
            //g.Clear(Color.White);//this.BackColor   
            g.DrawString(">", this.Font, BLACK_BRUSH, 8, 6);
        }

        private void pbLeft_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Pen myPen;//画笔颜色
            Brush BLACK_BRUSH;
            if (CanControl)
            {
                myPen = new Pen(Color.White);
                BLACK_BRUSH = new SolidBrush(Color.White);  //更新界面时候需要Color.FromArgb(20, 169, 255)
            }
            else
            {
                myPen = new Pen(Color.Red);
                BLACK_BRUSH = new SolidBrush(Color.Red);  //更新界面时候需要
            }
            g.DrawRectangle(myPen, 0, 0, Height - 1, Height - 1);// 黑色框    
            g.DrawString("<", this.Font, BLACK_BRUSH, 8, 6);
            //BLACK_BRUSH.Dispose(); 
        }



        private void TouchCombox_Click(object sender, EventArgs e)
        {
            //两边区域
            // ;

            //中间区域
            if (CanEdit)
            {
                //TextBox
            }
            else
            {
                if (Items == null)
                    return;
                int NewIndex = 0;
                if (CapLab == null)
                    NewIndex = frmItemsEdit.GetSelectedData(Items, SelectItemIndex, "Items", "");
                else
                    NewIndex = frmItemsEdit.GetSelectedData(Items, SelectItemIndex, CapLab.Text, "");

                if (NewIndex != SelectItemIndex)
                {
                    SelectItemIndex = NewIndex;
                    if (OnValueChange != null)
                        OnValueChange(this);
                    this.Refresh();
                }
                //else
                //    SelectItemIndex = NewIndex; 
            }
        }

        private void pbLeft_MouseDown(object sender, MouseEventArgs e)
        {
            PictureBox tempPBTN = (PictureBox)sender;
            //tempBTN.BackColor = Color.FromArgb(32, 32, 32); 
            Graphics g = tempPBTN.CreateGraphics();
            Pen RectPen = new Pen(Color.GreenYellow, 2);//画笔颜色 
            g.DrawRectangle(RectPen, 0, 0, tempPBTN.Width - 1, tempPBTN.Height - 1);// 黑色框  
        }


        private void InitializeComponent()
        {
            //CanEdit = false;
            this.btnR = new System.Windows.Forms.Label();
            this.btnL = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnR
            // 
            this.btnR.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnR.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnR.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnR.Location = new System.Drawing.Point(121, 0);
            this.btnR.Name = "btnR";
            this.btnR.Size = new System.Drawing.Size(32, 32);
            this.btnR.TabIndex = 4;
            this.btnR.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnR.Click += new System.EventHandler(this.btnR_Click);
            this.btnR.Paint += new System.Windows.Forms.PaintEventHandler(this.pbRight_Paint);
            this.btnR.MouseDown += new System.Windows.Forms.MouseEventHandler(this.btnR_MouseDown);
            this.btnR.MouseUp += new System.Windows.Forms.MouseEventHandler(this.btnR_MouseUp);
            // 
            // btnL
            // 
            this.btnL.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnL.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnL.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnL.Location = new System.Drawing.Point(0, 0);
            this.btnL.Name = "btnL";
            this.btnL.Size = new System.Drawing.Size(32, 32);
            this.btnL.TabIndex = 3;
            this.btnL.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.btnL.Click += new System.EventHandler(this.btnL_Click);
            this.btnL.Paint += new System.Windows.Forms.PaintEventHandler(this.pbLeft_Paint);
            this.btnL.MouseDown += new System.Windows.Forms.MouseEventHandler(this.btnR_MouseDown);
            this.btnL.MouseUp += new System.Windows.Forms.MouseEventHandler(this.btnR_MouseUp);
            // 
            // TouchCombox
            // 
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.Controls.Add(this.btnR);
            this.Controls.Add(this.btnL);
            this.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.ForeColor = System.Drawing.Color.White;
            this.Name = "TouchCombox";
            this.Size = new System.Drawing.Size(153, 32);
            this.Load += new System.EventHandler(this.TouchCombox_Load);
            this.Click += new System.EventHandler(this.TouchCombox_Click);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.TouchCombox_Paint);
            this.Resize += new System.EventHandler(this.TouchCombox_Resize);
            this.ResumeLayout(false);
            // CanEdit = true;

        }


        private void btnR_Click(object sender, EventArgs e)
        {
            //tempBTN.BackColor = Color.FromArgb(64, 64, 64);
            Pen RectPen;
            if (CanControl)
                RectPen = new Pen(Color.Black, 2);//画笔颜色 
            else
                RectPen = new Pen(Color.Red, 2);//画笔颜色  
            if (Items == null)
                return;

            if (SelectItemIndex < (Items.Length - 1))
                SelectItemIndex += 1;
            else
                SelectItemIndex = 0;

            if (OnValueChange != null)
                OnValueChange(this);
            this.Refresh();
        }

        private void btnL_Click(object sender, EventArgs e)
        {

            //PictureBox tempPBTN = (PictureBox)sender;
            // Graphics g = tempPBTN.CreateGraphics();
            Pen RectPen;
            if (CanControl)
                RectPen = new Pen(Color.Black, 2);//画笔颜色 
            else
                RectPen = new Pen(Color.Red, 2);//画笔颜色 
                                                // g.DrawRectangle(RectPen, 0, 0, tempPBTN.Width - 1, tempPBTN.Height - 1);// 黑色框  
                                                //tempBTN.BackColor = Color.FromArgb(64, 64, 64);
            if (Items == null)
                return;

            if (SelectItemIndex > 0)
                SelectItemIndex -= 1;
            else
                SelectItemIndex = Items.Length - 1;

            if (OnValueChange != null)
                OnValueChange(this);
            this.Refresh();
        }

        private void TouchCombox_Load(object sender, EventArgs e)
        {

        }

        private void btnR_MouseDown(object sender, MouseEventArgs e)
        {
            ((Label)sender).BackColor = Color.Black;
        }

        private void btnR_MouseUp(object sender, MouseEventArgs e)
        {
            ((Label)sender).BackColor = Color.FromArgb(20, 169, 255);
        }
    }
}
