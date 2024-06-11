
namespace EMS
{
    partial class frmItemsEdit
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmItemsEdit));
            this.panel1 = new System.Windows.Forms.Panel();
            this.button1 = new System.Windows.Forms.Button();
            this.btnCanncel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.plData = new System.Windows.Forms.Panel();
            this.plTouch = new System.Windows.Forms.Panel();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.pbtnUp = new System.Windows.Forms.PictureBox();
            this.lbDataList = new System.Windows.Forms.ListBox();
            this.labCap = new System.Windows.Forms.Label();
            this.panel1.SuspendLayout();
            this.plData.SuspendLayout();
            this.plTouch.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbtnUp)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.button1);
            this.panel1.Controls.Add(this.btnCanncel);
            this.panel1.Controls.Add(this.btnOk);
            this.panel1.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.panel1.Location = new System.Drawing.Point(16, 3);
            this.panel1.Margin = new System.Windows.Forms.Padding(2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(997, 54);
            this.panel1.TabIndex = 57;
            // 
            // button1
            // 
            this.button1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.button1.Enabled = false;
            this.button1.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.button1.FlatAppearance.CheckedBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button1.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.button1.ForeColor = System.Drawing.Color.White;
            this.button1.Location = new System.Drawing.Point(35, 4);
            this.button1.Margin = new System.Windows.Forms.Padding(1);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(118, 44);
            this.button1.TabIndex = 10;
            this.button1.Text = "信息选择";
            this.button1.UseVisualStyleBackColor = false;
            // 
            // btnCanncel
            // 
            this.btnCanncel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCanncel.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnCanncel.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnCanncel.FlatAppearance.MouseOverBackColor = System.Drawing.Color.SkyBlue;
            this.btnCanncel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCanncel.ForeColor = System.Drawing.Color.White;
            this.btnCanncel.Location = new System.Drawing.Point(855, 4);
            this.btnCanncel.Margin = new System.Windows.Forms.Padding(2);
            this.btnCanncel.Name = "btnCanncel";
            this.btnCanncel.Size = new System.Drawing.Size(88, 44);
            this.btnCanncel.TabIndex = 5;
            this.btnCanncel.Text = "取消";
            this.btnCanncel.UseVisualStyleBackColor = true;
            this.btnCanncel.Click += new System.EventHandler(this.btnCanncel_Click);
            // 
            // btnOk
            // 
            this.btnOk.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnOk.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnOk.FlatAppearance.MouseOverBackColor = System.Drawing.Color.SkyBlue;
            this.btnOk.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOk.ForeColor = System.Drawing.Color.White;
            this.btnOk.Location = new System.Drawing.Point(752, 4);
            this.btnOk.Margin = new System.Windows.Forms.Padding(2);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(88, 44);
            this.btnOk.TabIndex = 7;
            this.btnOk.Text = "确定";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // plData
            // 
            this.plData.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(55)))), ((int)(((byte)(64)))));
            this.plData.Controls.Add(this.labCap);
            this.plData.Controls.Add(this.plTouch);
            this.plData.Controls.Add(this.lbDataList);
            this.plData.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.plData.Location = new System.Drawing.Point(16, 61);
            this.plData.Margin = new System.Windows.Forms.Padding(2);
            this.plData.Name = "plData";
            this.plData.Size = new System.Drawing.Size(997, 696);
            this.plData.TabIndex = 56;
            // 
            // plTouch
            // 
            this.plTouch.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.plTouch.Controls.Add(this.pictureBox1);
            this.plTouch.Controls.Add(this.pbtnUp);
            this.plTouch.Location = new System.Drawing.Point(675, 68);
            this.plTouch.Name = "plTouch";
            this.plTouch.Size = new System.Drawing.Size(54, 529);
            this.plTouch.TabIndex = 28;
            this.plTouch.MouseDown += new System.Windows.Forms.MouseEventHandler(this.plTouch_MouseDown);
            this.plTouch.MouseEnter += new System.EventHandler(this.plTouch_MouseEnter);
            this.plTouch.MouseMove += new System.Windows.Forms.MouseEventHandler(this.plTouch_MouseMove);
            this.plTouch.MouseUp += new System.Windows.Forms.MouseEventHandler(this.plTouch_MouseUp);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox1.Image")));
            this.pictureBox1.Location = new System.Drawing.Point(3, 478);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(48, 48);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            this.pictureBox1.Click += new System.EventHandler(this.pictureBox1_Click);
            this.pictureBox1.MouseDown += new System.Windows.Forms.MouseEventHandler(this.pbtnUp_MouseDown);
            this.pictureBox1.MouseUp += new System.Windows.Forms.MouseEventHandler(this.pbtnUp_MouseUp);
            // 
            // pbtnUp
            // 
            this.pbtnUp.Image = ((System.Drawing.Image)(resources.GetObject("pbtnUp.Image")));
            this.pbtnUp.Location = new System.Drawing.Point(3, 3);
            this.pbtnUp.Name = "pbtnUp";
            this.pbtnUp.Size = new System.Drawing.Size(48, 48);
            this.pbtnUp.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pbtnUp.TabIndex = 1;
            this.pbtnUp.TabStop = false;
            this.pbtnUp.Click += new System.EventHandler(this.pbtnUp_Click);
            this.pbtnUp.MouseDown += new System.Windows.Forms.MouseEventHandler(this.pbtnUp_MouseDown);
            this.pbtnUp.MouseUp += new System.Windows.Forms.MouseEventHandler(this.pbtnUp_MouseUp);
            // 
            // lbDataList
            // 
            this.lbDataList.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(55)))), ((int)(((byte)(64)))));
            this.lbDataList.Font = new System.Drawing.Font("微软雅黑", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbDataList.ForeColor = System.Drawing.Color.White;
            this.lbDataList.ItemHeight = 25;
            this.lbDataList.Location = new System.Drawing.Point(247, 68);
            this.lbDataList.Name = "lbDataList";
            this.lbDataList.Size = new System.Drawing.Size(422, 529);
            this.lbDataList.TabIndex = 29;
            // 
            // labCap
            // 
            this.labCap.AutoSize = true;
            this.labCap.Font = new System.Drawing.Font("微软雅黑", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labCap.ForeColor = System.Drawing.Color.White;
            this.labCap.Location = new System.Drawing.Point(252, 22);
            this.labCap.Name = "labCap";
            this.labCap.Size = new System.Drawing.Size(79, 26);
            this.labCap.TabIndex = 30;
            this.labCap.Text = "labCap";
            // 
            // frmItemsEdit
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.ClientSize = new System.Drawing.Size(1024, 768);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.plData);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "frmItemsEdit";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "frmItemsEdit";
            this.Load += new System.EventHandler(this.frmItemsEdit_Load);
            this.panel1.ResumeLayout(false);
            this.plData.ResumeLayout(false);
            this.plData.PerformLayout();
            this.plTouch.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pbtnUp)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button btnCanncel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Panel plData;
        private System.Windows.Forms.Panel plTouch;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.PictureBox pbtnUp;
        private System.Windows.Forms.ListBox lbDataList;
        private System.Windows.Forms.Label labCap;
    }
}