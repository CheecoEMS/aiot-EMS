using System;
using System.Windows.Forms;

namespace EMS
{
    public partial class frmMessage : Form
    {
        static public frmMessage oneForm = null;
        public frmMessage()
        {
            InitializeComponent();

        }


        static public void TimeOut()
        {

            if (oneForm == null)
            {
                oneForm.Close();
                oneForm.Dispose();
                oneForm = null;
            }
        }

        static public void ShowForm()
        {
            if (oneForm == null)
                oneForm = new frmMessage();
            oneForm.ShowDialog();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            TimeOut();
        }
    }
}
