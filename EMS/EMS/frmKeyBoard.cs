using System;
using System.Drawing;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmKeyBoard : Form
    {
        public int MaxLen = 0;
        private static frmKeyBoard KeyBoardForm = null;
        private TouchText TTextSource = null;

        static DialogResult KeyBackResult = DialogResult.None;

        private int KeyType = 0;  //0小写字母，1大写字母，2符号
        private bool IsCNImmInput = false;//是中文输入 
        private string strInput = "";
        private string WordList;  //输入的汉字
        private int WordIndex = 0;
        private bool ASCOnly = false;
        private bool DefaultCharOnly = false;
        private bool WithSymbols = true;
        private bool WithNum = true;
        private bool IsTimerFormat = false;
        public frmKeyBoard()
        {
            InitializeComponent();
        }

        static public void INIForm()
        {
            KeyBoardForm = new frmKeyBoard();
        }

        public void Slef_SendKey(string aChar_key)
        {
            string strLeftString = labStrResult.Text;
            int iPos = labStrResult.SelectionStart;
            if (iPos != strLeftString.Length)
            {
                string strRightString = strLeftString.Substring(iPos, strLeftString.Length - iPos);
                strLeftString = strLeftString.Substring(0, iPos);
                labStrResult.Text = strLeftString + aChar_key + strRightString;
            }
            else
            {
                labStrResult.Text = labStrResult.Text + aChar_key;
            }
            labStrResult.SelectionStart = iPos + aChar_key.Length;
        }

        public static void GetTouchTextString(TouchText aSend, int KeyType, bool aIsPasswordChar,
              string astrDef, int aMaxTextLength, string aStrCap)// bDefKeyTypeOnly, bWithNumberKeys, bASCOnly, bWithSymbolKeys, bIsTimerFormat)
        {
            if (KeyBoardForm == null)
                KeyBoardForm = new frmKeyBoard();
            KeyBoardForm.TTextSource = aSend;
            KeyBoardForm.labStrResult.Text = aSend.strText;
            // KeyBoardForm.KeyType = 0; 
            // KeyBoardForm.IsPasswordChar = false; 
            KeyBoardForm.IsCNImmInput = false;//是中文输入  
            KeyBoardForm.strInput = aSend.strText; ;
            KeyBoardForm.WordIndex = 0;
            KeyBoardForm.WordList = "";
            //KeyBoardForm.ShowInputWord();


            KeyBoardForm.labCap.Visible = false;
            KeyBoardForm.labCap.Text = aStrCap;
            KeyBoardForm.MaxLen = aMaxTextLength;
            //KeyBoardForm.ShowButtonCap(aDefaulChartIndex);
            KeyBoardForm.ASCOnly = true;


            if (aIsPasswordChar)
            {
                KeyBoardForm.labStrResult.PasswordChar = '*';
            }
            else
                KeyBoardForm.labStrResult.PasswordChar = Convert.ToChar(0);
            //do it
            KeyBoardForm.Show();
        }

        private void frmKeyBoard_Load(object sender, EventArgs e)
        {

        }

        private void pbtnClean_MouseUp(object sender, MouseEventArgs e)
        {
            PictureBox tempPBTN = (PictureBox)sender;
            Graphics g = tempPBTN.CreateGraphics();
            Pen RectPen = new Pen(Color.Black, 2);//画笔颜色
            g.DrawRectangle(RectPen, 0, 0, tempPBTN.Width - 1, tempPBTN.Height - 1);// 黑色框  
            labStrResult.Text = "";
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            //if (NeedClose)
            //{
            //    strResult = labStrResult.Text;
            //    this.Close();
            //    KeyBackResult = DialogResult.OK;
            //}
            //else
            {
                this.Hide();
                if (TTextSource.strText != labStrResult.Text)
                {
                    TTextSource.strText = labStrResult.Text;
                    TTextSource.ValueChanged();
                }
            }
        }

        private void btnCanncel_Click(object sender, EventArgs e)
        {
            this.Hide();
            //if (NeedClose)
            //{
            //    this.Close();
            //    KeyBackResult = DialogResult.Cancel;
            //}
        }

        private void btnDel2_Click(object sender, EventArgs e)
        {
            //if ((IsCNImmInput) && (strInput != ""))
            //{
            //    strInput = strInput.Substring(0, strInput.Length - 1);
            //    kbEnInput.Caption = strInput;
            //    WordIndex = 0;
            //    WordList = PinyinInput.py_ime(strInput);
            //    ShowInputWord();
            //}
            //else
            {
                if (labStrResult.Text == "")
                    return;
                if (labStrResult.Focused)
                {
                    if ((labStrResult.SelectionStart == 0) && (labStrResult.SelectionLength == 0))
                        return;
                    int iPos = labStrResult.SelectionStart;
                    int iSelLenght = labStrResult.SelectionLength;
                    string tempStrCap = labStrResult.Text;

                    if (labStrResult.SelectionLength == 0)
                    {
                        labStrResult.Text = tempStrCap.Substring(0, iPos - 1) + tempStrCap.Substring(iPos, tempStrCap.Length - iPos);
                        labStrResult.SelectionStart = iPos - 1;
                    }
                    else
                    {
                        labStrResult.Text = tempStrCap.Substring(0, iPos) + tempStrCap.Substring(iPos + iSelLenght, tempStrCap.Length - iSelLenght - iPos);
                        labStrResult.SelectionStart = iPos;
                    }
                    //Win32APIs.Win32API.keybd_event(Keys.Back, 0, 0, 0);
                }
                else
                {
                    if (labStrResult.Text != "")
                        labStrResult.Text = labStrResult.Text.Substring(0, labStrResult.Text.Length - 1);
                    labStrResult.Focus();
                    labStrResult.Select(labStrResult.Text.Length, 0);
                }
            }
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
            Pen RectPen = new Pen(Color.Black, 2);//画笔颜色
            g.DrawRectangle(RectPen, 0, 0, tempPBTN.Width - 1, tempPBTN.Height - 1);// 黑色框  


            //if ((IsCNImmInput) && (strInput != ""))
            //{
            //    strInput = strInput.Substring(0, strInput.Length - 1);
            //    kbEnInput.Caption = strInput;
            //    WordIndex = 0;
            //    WordList = PinyinInput.py_ime(strInput);
            //    ShowInputWord();
            //}
            //else
            {
                if (labStrResult.Text == "")
                    return;
                if (labStrResult.Focused)
                {
                    if ((labStrResult.SelectionStart == 0) && (labStrResult.SelectionLength == 0))
                        return;
                    int iPos = labStrResult.SelectionStart;
                    int iSelLenght = labStrResult.SelectionLength;
                    string tempStrCap = labStrResult.Text;

                    if (labStrResult.SelectionLength == 0)
                    {
                        labStrResult.Text = tempStrCap.Substring(0, iPos - 1) + tempStrCap.Substring(iPos, tempStrCap.Length - iPos);
                        labStrResult.SelectionStart = iPos - 1;
                    }
                    else
                    {
                        labStrResult.Text = tempStrCap.Substring(0, iPos) + tempStrCap.Substring(iPos + iSelLenght, tempStrCap.Length - iSelLenght - iPos);
                        labStrResult.SelectionStart = iPos;
                    }
                    //Win32APIs.Win32API.keybd_event(Keys.Back, 0, 0, 0);
                }
                else
                {
                    if (labStrResult.Text != "")
                        labStrResult.Text = labStrResult.Text.Substring(0, labStrResult.Text.Length - 1);
                    labStrResult.Focus();
                    labStrResult.Select(labStrResult.Text.Length, 0);
                }
            }
        }

        private void button9_Click(object sender, EventArgs e)
        {
            labStrResult.Focus();
            //labStrResult.SelectionStart = iSplashPos;
            if (labStrResult.Text.Length < (MaxLen) || (MaxLen == 0))
            {
                string strCap = ((Button)sender).Text;
                //if (strCap.Length > 0)
                //    strCap=strCap.Substring(0, 1);   
                if (strCap == "&&")
                    strCap = "&";

                if (labStrResult.Focused)
                    this.Slef_SendKey(strCap);
                else
                    labStrResult.Text += strCap;
            }
        }
    }
}
