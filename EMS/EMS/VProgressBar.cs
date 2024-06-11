using System.ComponentModel;
using System.Windows.Forms;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks; 
//using System.Collections.Generic;
////using System;
//using System.Collections.Generic;


namespace EMS
{
    public partial class VProgressBar : ProgressBar
    {
        public VProgressBar()
        {
            InitializeComponent();
        }

        public VProgressBar(IContainer container)
        {
            container.Add(this);
            InitializeComponent();
        }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x04;
                return cp;
            }
        }
    }
}
