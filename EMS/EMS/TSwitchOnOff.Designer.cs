namespace EMS
{
    partial class TSwitchOnOff
    {
        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.pbKey = new System.Windows.Forms.PictureBox();
            this.SuspendLayout();
            // 
            // pbKey
            // 
            this.pbKey.BackColor = System.Drawing.Color.Gray;
            this.pbKey.Location = new System.Drawing.Point(30, 3);
            this.pbKey.Name = "pbKey";
            this.pbKey.Size = new System.Drawing.Size(31, 26);
            this.pbKey.DoubleClick += new System.EventHandler(this.pbKey_DoubleClick);
            this.pbKey.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pbKey_MouseMove);
            this.pbKey.Click += new System.EventHandler(this.pbKey_Click);
            this.pbKey.MouseDown += new System.Windows.Forms.MouseEventHandler(this.pbKey_MouseDown);
            this.pbKey.MouseUp += new System.Windows.Forms.MouseEventHandler(this.pbKey_MouseUp);
            // 
            // TSwitchOnOff
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(192)))));
            this.Controls.Add(this.pbKey);
            this.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular);
            this.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.Name = "TSwitchOnOff";
            this.Size = new System.Drawing.Size(64, 32);
            this.Click += new System.EventHandler(this.TSwitchOnOff_Click);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.TSwitchOnOff_MouseUp);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox pbKey;
    }
}
