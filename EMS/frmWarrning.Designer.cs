
namespace EMS
{
    partial class frmWarrning
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmWarrning));
            this.dbgWarning = new System.Windows.Forms.DataGridView();
            this.id = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.rTime = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.wClass = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Memo = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.UPower = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel1 = new System.Windows.Forms.Panel();
            this.button3 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.btnCheck = new System.Windows.Forms.Button();
            this.btnRevovery = new System.Windows.Forms.Button();
            this.panel9 = new System.Windows.Forms.Panel();
            this.btnControl = new System.Windows.Forms.Button();
            this.btnMain = new System.Windows.Forms.Button();
            this.pictureBox2 = new System.Windows.Forms.PictureBox();
            this.btnSet = new System.Windows.Forms.Button();
            this.btnLine = new System.Windows.Forms.Button();
            this.btnAbout = new System.Windows.Forms.Button();
            this.btnWarning = new System.Windows.Forms.Button();
            this.btnLogin = new System.Windows.Forms.Button();
            this.btnState = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.panel2 = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.dbgWarning)).BeginInit();
            this.panel1.SuspendLayout();
            this.panel9.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // dbgWarning
            // 
            this.dbgWarning.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dbgWarning.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.id,
            this.rTime,
            this.wClass,
            this.Memo,
            this.Column4,
            this.Column5,
            this.Column3,
            this.Column2,
            this.UPower,
            this.Column1});
            this.dbgWarning.Location = new System.Drawing.Point(16, 2);
            this.dbgWarning.Margin = new System.Windows.Forms.Padding(2);
            this.dbgWarning.Name = "dbgWarning";
            this.dbgWarning.RowHeadersWidth = 62;
            this.dbgWarning.RowTemplate.Height = 30;
            this.dbgWarning.Size = new System.Drawing.Size(792, 653);
            this.dbgWarning.TabIndex = 12;
            // 
            // id
            // 
            this.id.DataPropertyName = "id";
            this.id.HeaderText = "ID";
            this.id.Name = "id";
            this.id.Width = 50;
            // 
            // rTime
            // 
            this.rTime.DataPropertyName = "rTime";
            this.rTime.HeaderText = "发生时间";
            this.rTime.MinimumWidth = 8;
            this.rTime.Name = "rTime";
            this.rTime.Width = 150;
            // 
            // wClass
            // 
            this.wClass.DataPropertyName = "wClass";
            this.wClass.HeaderText = "设备名称";
            this.wClass.MinimumWidth = 8;
            this.wClass.Name = "wClass";
            this.wClass.Width = 110;
            // 
            // Memo
            // 
            this.Memo.DataPropertyName = "Warning";
            this.Memo.HeaderText = "报警信息";
            this.Memo.MinimumWidth = 8;
            this.Memo.Name = "Memo";
            this.Memo.Width = 150;
            // 
            // Column4
            // 
            this.Column4.DataPropertyName = "wLevels";
            this.Column4.HeaderText = "报警级别";
            this.Column4.Name = "Column4";
            // 
            // Column5
            // 
            this.Column5.DataPropertyName = "warningID";
            this.Column5.HeaderText = "报警编号";
            this.Column5.Name = "Column5";
            this.Column5.Visible = false;
            // 
            // Column3
            // 
            this.Column3.DataPropertyName = "ResetTime";
            this.Column3.HeaderText = "恢复时间";
            this.Column3.Name = "Column3";
            this.Column3.Width = 150;
            // 
            // Column2
            // 
            this.Column2.DataPropertyName = "CheckTime";
            this.Column2.HeaderText = "确认时间";
            this.Column2.MinimumWidth = 8;
            this.Column2.Name = "Column2";
            this.Column2.Width = 110;
            // 
            // UPower
            // 
            this.UPower.DataPropertyName = "eID";
            this.UPower.HeaderText = "设备编号";
            this.UPower.MinimumWidth = 8;
            this.UPower.Name = "UPower";
            this.UPower.Width = 110;
            // 
            // Column1
            // 
            this.Column1.DataPropertyName = "UserID";
            this.Column1.HeaderText = "确认人";
            this.Column1.MinimumWidth = 8;
            this.Column1.Name = "Column1";
            this.Column1.Width = 90;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.button3);
            this.panel1.Controls.Add(this.button2);
            this.panel1.Controls.Add(this.btnCheck);
            this.panel1.Controls.Add(this.btnRevovery);
            this.panel1.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.panel1.Location = new System.Drawing.Point(175, 20);
            this.panel1.Margin = new System.Windows.Forms.Padding(2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(824, 52);
            this.panel1.TabIndex = 11;
            // 
            // button3
            // 
            this.button3.BackColor = System.Drawing.Color.Transparent;
            this.button3.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.button3.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.button3.FlatAppearance.MouseOverBackColor = System.Drawing.Color.SkyBlue;
            this.button3.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button3.ForeColor = System.Drawing.Color.White;
            this.button3.Location = new System.Drawing.Point(718, 2);
            this.button3.Margin = new System.Windows.Forms.Padding(1);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(88, 44);
            this.button3.TabIndex = 10;
            this.button3.Text = "刷    新";
            this.button3.UseVisualStyleBackColor = false;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // button2
            // 
            this.button2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.button2.Enabled = false;
            this.button2.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.button2.FlatAppearance.CheckedBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.button2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button2.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.button2.ForeColor = System.Drawing.Color.White;
            this.button2.Location = new System.Drawing.Point(14, 3);
            this.button2.Margin = new System.Windows.Forms.Padding(1);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(118, 44);
            this.button2.TabIndex = 9;
            this.button2.Text = "警告信息";
            this.button2.UseVisualStyleBackColor = false;
            // 
            // btnCheck
            // 
            this.btnCheck.BackColor = System.Drawing.Color.Transparent;
            this.btnCheck.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnCheck.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnCheck.FlatAppearance.MouseOverBackColor = System.Drawing.Color.SkyBlue;
            this.btnCheck.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCheck.ForeColor = System.Drawing.Color.White;
            this.btnCheck.Location = new System.Drawing.Point(538, 2);
            this.btnCheck.Margin = new System.Windows.Forms.Padding(1);
            this.btnCheck.Name = "btnCheck";
            this.btnCheck.Size = new System.Drawing.Size(88, 44);
            this.btnCheck.TabIndex = 8;
            this.btnCheck.Text = "确认警告";
            this.btnCheck.UseVisualStyleBackColor = false;
            this.btnCheck.Visible = false;
            this.btnCheck.Click += new System.EventHandler(this.btnCheck_Click);
            // 
            // btnRevovery
            // 
            this.btnRevovery.BackColor = System.Drawing.Color.Transparent;
            this.btnRevovery.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnRevovery.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnRevovery.FlatAppearance.MouseOverBackColor = System.Drawing.Color.SkyBlue;
            this.btnRevovery.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRevovery.ForeColor = System.Drawing.Color.White;
            this.btnRevovery.Location = new System.Drawing.Point(628, 2);
            this.btnRevovery.Margin = new System.Windows.Forms.Padding(1);
            this.btnRevovery.Name = "btnRevovery";
            this.btnRevovery.Size = new System.Drawing.Size(88, 44);
            this.btnRevovery.TabIndex = 7;
            this.btnRevovery.Text = "取消确认";
            this.btnRevovery.UseVisualStyleBackColor = false;
            this.btnRevovery.Visible = false;
            this.btnRevovery.Click += new System.EventHandler(this.btnRevovery_Click);
            // 
            // panel9
            // 
            this.panel9.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(24)))), ((int)(((byte)(34)))), ((int)(((byte)(41)))));
            this.panel9.Controls.Add(this.btnControl);
            this.panel9.Controls.Add(this.btnMain);
            this.panel9.Controls.Add(this.pictureBox2);
            this.panel9.Controls.Add(this.btnSet);
            this.panel9.Controls.Add(this.btnLine);
            this.panel9.Controls.Add(this.btnAbout);
            this.panel9.Controls.Add(this.btnWarning);
            this.panel9.Controls.Add(this.btnLogin);
            this.panel9.Controls.Add(this.btnState);
            this.panel9.Controls.Add(this.button1);
            this.panel9.Dock = System.Windows.Forms.DockStyle.Left;
            this.panel9.Location = new System.Drawing.Point(0, 0);
            this.panel9.Margin = new System.Windows.Forms.Padding(2);
            this.panel9.Name = "panel9";
            this.panel9.Size = new System.Drawing.Size(152, 743);
            this.panel9.TabIndex = 63;
            // 
            // btnControl
            // 
            this.btnControl.BackColor = System.Drawing.Color.Transparent;
            this.btnControl.Enabled = false;
            this.btnControl.FlatAppearance.CheckedBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnControl.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btnControl.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnControl.ForeColor = System.Drawing.Color.Gray;
            this.btnControl.Image = ((System.Drawing.Image)(resources.GetObject("btnControl.Image")));
            this.btnControl.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnControl.Location = new System.Drawing.Point(1, 410);
            this.btnControl.Margin = new System.Windows.Forms.Padding(2);
            this.btnControl.Name = "btnControl";
            this.btnControl.Size = new System.Drawing.Size(148, 52);
            this.btnControl.TabIndex = 60;
            this.btnControl.Text = "设备操作";
            this.btnControl.UseVisualStyleBackColor = false;
            // 
            // btnMain
            // 
            this.btnMain.BackColor = System.Drawing.Color.Transparent;
            this.btnMain.FlatAppearance.CheckedBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnMain.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btnMain.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnMain.ForeColor = System.Drawing.Color.White;
            this.btnMain.Image = ((System.Drawing.Image)(resources.GetObject("btnMain.Image")));
            this.btnMain.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnMain.Location = new System.Drawing.Point(4, 92);
            this.btnMain.Margin = new System.Windows.Forms.Padding(2);
            this.btnMain.Name = "btnMain";
            this.btnMain.Size = new System.Drawing.Size(148, 52);
            this.btnMain.TabIndex = 58;
            this.btnMain.Text = "数据看板";
            this.btnMain.UseVisualStyleBackColor = false;
            this.btnMain.Click += new System.EventHandler(this.btnMain_Click);
            // 
            // pictureBox2
            // 
            this.pictureBox2.Image = global::EMS.Properties.Resources.logo_2x;
            this.pictureBox2.Location = new System.Drawing.Point(17, 24);
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.Size = new System.Drawing.Size(121, 43);
            this.pictureBox2.TabIndex = 57;
            this.pictureBox2.TabStop = false;
            // 
            // btnSet
            // 
            this.btnSet.BackColor = System.Drawing.Color.Transparent;
            this.btnSet.Enabled = false;
            this.btnSet.FlatAppearance.CheckedBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnSet.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btnSet.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSet.ForeColor = System.Drawing.Color.Gray;
            this.btnSet.Image = ((System.Drawing.Image)(resources.GetObject("btnSet.Image")));
            this.btnSet.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnSet.Location = new System.Drawing.Point(2, 463);
            this.btnSet.Margin = new System.Windows.Forms.Padding(2);
            this.btnSet.Name = "btnSet";
            this.btnSet.Size = new System.Drawing.Size(148, 52);
            this.btnSet.TabIndex = 36;
            this.btnSet.Text = "系统设置";
            this.btnSet.UseVisualStyleBackColor = false;
            // 
            // btnLine
            // 
            this.btnLine.BackColor = System.Drawing.Color.Transparent;
            this.btnLine.Enabled = false;
            this.btnLine.FlatAppearance.CheckedBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnLine.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btnLine.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnLine.ForeColor = System.Drawing.Color.Gray;
            this.btnLine.Image = ((System.Drawing.Image)(resources.GetObject("btnLine.Image")));
            this.btnLine.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnLine.Location = new System.Drawing.Point(3, 251);
            this.btnLine.Margin = new System.Windows.Forms.Padding(2);
            this.btnLine.Name = "btnLine";
            this.btnLine.Size = new System.Drawing.Size(148, 52);
            this.btnLine.TabIndex = 55;
            this.btnLine.Text = "主线路图";
            this.btnLine.UseVisualStyleBackColor = false;
            this.btnLine.Click += new System.EventHandler(this.btnLine_Click);
            // 
            // btnAbout
            // 
            this.btnAbout.BackColor = System.Drawing.Color.Transparent;
            this.btnAbout.Enabled = false;
            this.btnAbout.FlatAppearance.CheckedBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnAbout.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btnAbout.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnAbout.ForeColor = System.Drawing.Color.Gray;
            this.btnAbout.Image = ((System.Drawing.Image)(resources.GetObject("btnAbout.Image")));
            this.btnAbout.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnAbout.Location = new System.Drawing.Point(2, 198);
            this.btnAbout.Margin = new System.Windows.Forms.Padding(2);
            this.btnAbout.Name = "btnAbout";
            this.btnAbout.Size = new System.Drawing.Size(148, 52);
            this.btnAbout.TabIndex = 33;
            this.btnAbout.Text = "系统信息";
            this.btnAbout.UseVisualStyleBackColor = false;
            this.btnAbout.Click += new System.EventHandler(this.btnAbout_Click);
            // 
            // btnWarning
            // 
            this.btnWarning.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnWarning.FlatAppearance.CheckedBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnWarning.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btnWarning.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnWarning.ForeColor = System.Drawing.Color.White;
            this.btnWarning.Image = ((System.Drawing.Image)(resources.GetObject("btnWarning.Image")));
            this.btnWarning.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnWarning.Location = new System.Drawing.Point(3, 356);
            this.btnWarning.Margin = new System.Windows.Forms.Padding(2);
            this.btnWarning.Name = "btnWarning";
            this.btnWarning.Size = new System.Drawing.Size(148, 52);
            this.btnWarning.TabIndex = 50;
            this.btnWarning.Text = "告警信息";
            this.btnWarning.UseVisualStyleBackColor = false;
            // 
            // btnLogin
            // 
            this.btnLogin.BackColor = System.Drawing.Color.Transparent;
            this.btnLogin.Enabled = false;
            this.btnLogin.FlatAppearance.CheckedBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnLogin.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btnLogin.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnLogin.ForeColor = System.Drawing.Color.Gray;
            this.btnLogin.Image = ((System.Drawing.Image)(resources.GetObject("btnLogin.Image")));
            this.btnLogin.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnLogin.Location = new System.Drawing.Point(2, 146);
            this.btnLogin.Margin = new System.Windows.Forms.Padding(2);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(148, 52);
            this.btnLogin.TabIndex = 38;
            this.btnLogin.Text = "用户登录";
            this.btnLogin.UseVisualStyleBackColor = false;
            // 
            // btnState
            // 
            this.btnState.BackColor = System.Drawing.Color.Transparent;
            this.btnState.Enabled = false;
            this.btnState.FlatAppearance.CheckedBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnState.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btnState.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnState.ForeColor = System.Drawing.Color.Gray;
            this.btnState.Image = ((System.Drawing.Image)(resources.GetObject("btnState.Image")));
            this.btnState.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnState.Location = new System.Drawing.Point(3, 304);
            this.btnState.Margin = new System.Windows.Forms.Padding(2);
            this.btnState.Name = "btnState";
            this.btnState.Size = new System.Drawing.Size(148, 52);
            this.btnState.TabIndex = 56;
            this.btnState.Text = "当前状态";
            this.btnState.UseVisualStyleBackColor = false;
            this.btnState.Click += new System.EventHandler(this.btnState_Click);
            // 
            // button1
            // 
            this.button1.BackColor = System.Drawing.Color.Transparent;
            this.button1.Enabled = false;
            this.button1.FlatAppearance.CheckedBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.button1.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.button1.ForeColor = System.Drawing.Color.DimGray;
            this.button1.Image = ((System.Drawing.Image)(resources.GetObject("button1.Image")));
            this.button1.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.button1.Location = new System.Drawing.Point(4, 680);
            this.button1.Margin = new System.Windows.Forms.Padding(2);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(148, 52);
            this.button1.TabIndex = 54;
            this.button1.Text = "查询统计";
            this.button1.UseVisualStyleBackColor = false;
            this.button1.Visible = false;
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(55)))), ((int)(((byte)(64)))));
            this.panel2.Controls.Add(this.dbgWarning);
            this.panel2.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.panel2.Location = new System.Drawing.Point(174, 72);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(825, 659);
            this.panel2.TabIndex = 64;
            // 
            // frmWarrning
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.ClientSize = new System.Drawing.Size(1024, 743);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel9);
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Margin = new System.Windows.Forms.Padding(1);
            this.Name = "frmWarrning";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "frmWarrning";
            ((System.ComponentModel.ISupportInitialize)(this.dbgWarning)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel9.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
            this.panel2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dbgWarning;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnCheck;
        private System.Windows.Forms.Button btnRevovery;
        private System.Windows.Forms.Panel panel9;
        private System.Windows.Forms.Button btnMain;
        private System.Windows.Forms.PictureBox pictureBox2;
        private System.Windows.Forms.Button btnAbout;
        private System.Windows.Forms.Button btnSet;
        private System.Windows.Forms.Button btnState;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.Button btnLine;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button btnWarning;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.DataGridViewTextBoxColumn id;
        private System.Windows.Forms.DataGridViewTextBoxColumn rTime;
        private System.Windows.Forms.DataGridViewTextBoxColumn wClass;
        private System.Windows.Forms.DataGridViewTextBoxColumn Memo;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column4;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column5;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column3;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column2;
        private System.Windows.Forms.DataGridViewTextBoxColumn UPower;
        private System.Windows.Forms.DataGridViewTextBoxColumn Column1;
        private System.Windows.Forms.Button btnControl;
    }
}