using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace EMS
{
    public partial class TouchText : UserControl
    {
        public delegate void OnValueChangEventDelegate(object sender);//建立事件委托
        public event OnValueChangEventDelegate OnValueChange;//被选中的事件 
        public TouchText()
        {
            InitializeComponent();
            IsPassWord = false;
            Height = 32;
            //bWithNumberKeys = true;
        }

        public TouchText(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }



        private bool bASCOnly = false;
        public bool ASCOnly
        {
            get { return bASCOnly; }
            set { bASCOnly = value; }
        }

        private int iKeyType = 0;
        public int KeyType
        {
            get { return iKeyType; }
            set { iKeyType = value; }
        }

        private bool bDefKeyTypeOnly = false;
        public bool DefKeyTypeOnly
        {
            get { return bDefKeyTypeOnly; }
            set { bDefKeyTypeOnly = value; }
        }

        private bool bWithNumberKeys = true;
        public bool WithNumberKeys
        {
            get { return bWithNumberKeys; }
            set { bWithNumberKeys = value; }
        }


        private bool bWithSymbolKeys = true;
        public bool WithSymbolKeys
        {
            get { return bWithSymbolKeys; }
            set { bWithSymbolKeys = value; }
        }

        private bool bIsTimerFormat = false;
        public bool IsTimerFormat
        {
            get { return bIsTimerFormat; }
            set { bIsTimerFormat = value; }
        }

        public int DefaultValue
        {
            get;
            set;
        }

        public bool IsPassWord
        {
            get;
            set;
        }

        int iMaxLength = 0;
        public int MaxTextLength
        {
            get { return iMaxLength; }
            set { iMaxLength = value; }
        }

        public bool CanControl = true;
        private string aStrText = "";
        private string aStrTextOld;
        //内容
        public string strText
        {
            get
            {
                return aStrText;
            }
            set
            {
                aStrText = value;
                Refresh();
            }
        }
        //old value=new value
        public void UpdateVale()
        {
            aStrTextOld = aStrText;
            Refresh();
        }


        public void SetstrText(string aNewStrText)
        {
            aStrText = aNewStrText;
            aStrTextOld = aNewStrText;
            //this.Refresh();
        }

        private void TouchText_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(this.BackColor);
            Pen myPen;
            if (CanControl)
                myPen = new Pen(Color.FromArgb(20, 169, 255));
            else
                myPen = new Pen(Color.Red);
            myPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
            g.DrawRectangle(myPen, 0, 0, Width - 1, Height - 1);

            Brush Font_BRUSH;
            if (aStrTextOld == aStrText)
                Font_BRUSH = new SolidBrush(Color.White);  //更新界面时候需要Pen myPen = new Pen();FromArgb(64, 64, 64)
            else
                Font_BRUSH = new SolidBrush(Color.LightGreen);

            //aStrText
            g.DrawString(aStrText, this.Font, Font_BRUSH, 5, (Height - 20) / 2);//Height + 4
            //BLACK_BRUSH.Dispose(); 
            //myPen.Dispose(); 
        }

        private void TouchText_MouseUp(object sender, MouseEventArgs e)
        {
            //if (frmKeyBoard.GetStringInput(ref aStrText, KeyType, IsPassWord, "", MaxTextLength, bDefKeyTypeOnly, bWithNumberKey))
            frmKeyBoard.GetTouchTextString(this, KeyType, IsPassWord, strText, MaxTextLength, "");//, bDefKeyTypeOnly, bWithNumberKeys, bASCOnly, bWithSymbolKeys, bIsTimerFormat);//
            this.Refresh();
        }

        public void ValueChanged()
        {
            if (OnValueChange != null)
                OnValueChange(this);
        }

        private void TouchText_Load(object sender, EventArgs e)
        {

        }
    }
}
