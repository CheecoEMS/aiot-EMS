using System;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmFlash : Form
    {
        static public frmFlash FlashForm;
        static private int iPos = 0;
        public frmFlash()
        {
            InitializeComponent();
            Text = "EMS system";
        }


        static public void AddPostion(int aAddpos)
        {
            iPos += aAddpos;
            if (iPos > 100)
                iPos = 100;
            FlashForm.pbLoadInf.Value = iPos;
            FlashForm.pbLoadInf.Update();
            FlashForm.Update();
        }

        public static void ShowFlashForm()
        {
            FlashForm = new frmFlash();
            FlashForm.Show(); 
            FlashForm.BringToFront();
            AddPostion(10);
        }

        public static void CloseFlashForm()
        {
            AddPostion(10);
            FlashForm.Hide();
            FlashForm.Close();
            FlashForm.Dispose();
        }


        private void frmFlash_Load(object sender, EventArgs e)
        {

        }
    }
}
