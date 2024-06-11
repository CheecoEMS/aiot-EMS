
namespace EMS
{
    partial class frmoneTactics
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
            this.components = new System.ComponentModel.Container();
            this.plData = new System.Windows.Forms.Panel();
            this.label4 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.tneEndS = new EMS.TouchNumberEdit(this.components);
            this.tneEndm = new EMS.TouchNumberEdit(this.components);
            this.tneEndH = new EMS.TouchNumberEdit(this.components);
            this.label9 = new System.Windows.Forms.Label();
            this.tcbtType = new EMS.TouchCombox(this.components);
            this.tnedwaValue = new EMS.TouchNumberEdit(this.components);
            this.tcbPCSType = new EMS.TouchCombox(this.components);
            this.label5 = new System.Windows.Forms.Label();
            this.labm = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.tneStartS = new EMS.TouchNumberEdit(this.components);
            this.tneStartm = new EMS.TouchNumberEdit(this.components);
            this.tneStartH = new EMS.TouchNumberEdit(this.components);
            this.label2 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label49 = new System.Windows.Forms.Label();
            this.label26 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.plData.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // plData
            // 
            this.plData.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(55)))), ((int)(((byte)(64)))));
            this.plData.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.plData.Controls.Add(this.label4);
            this.plData.Controls.Add(this.label7);
            this.plData.Controls.Add(this.label8);
            this.plData.Controls.Add(this.tneEndS);
            this.plData.Controls.Add(this.tneEndm);
            this.plData.Controls.Add(this.tneEndH);
            this.plData.Controls.Add(this.label9);
            this.plData.Controls.Add(this.tcbtType);
            this.plData.Controls.Add(this.tnedwaValue);
            this.plData.Controls.Add(this.tcbPCSType);
            this.plData.Controls.Add(this.label5);
            this.plData.Controls.Add(this.labm);
            this.plData.Controls.Add(this.label1);
            this.plData.Controls.Add(this.tneStartS);
            this.plData.Controls.Add(this.tneStartm);
            this.plData.Controls.Add(this.tneStartH);
            this.plData.Controls.Add(this.label2);
            this.plData.Controls.Add(this.label6);
            this.plData.Controls.Add(this.label49);
            this.plData.Controls.Add(this.label26);
            this.plData.Controls.Add(this.label3);
            this.plData.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.plData.ForeColor = System.Drawing.Color.White;
            this.plData.Location = new System.Drawing.Point(11, 64);
            this.plData.Margin = new System.Windows.Forms.Padding(2);
            this.plData.Name = "plData";
            this.plData.Size = new System.Drawing.Size(1002, 693);
            this.plData.TabIndex = 17;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label4.Location = new System.Drawing.Point(827, 206);
            this.label4.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(26, 21);
            this.label4.TabIndex = 61;
            this.label4.Text = "秒";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label7.Location = new System.Drawing.Point(580, 206);
            this.label7.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(26, 21);
            this.label7.TabIndex = 60;
            this.label7.Text = "分";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label8.Location = new System.Drawing.Point(318, 206);
            this.label8.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(26, 21);
            this.label8.TabIndex = 59;
            this.label8.Text = "时";
            // 
            // tneEndS
            // 
            this.tneEndS.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tneEndS.CanEdit = true;
            this.tneEndS.DefaultValue = 0;
            this.tneEndS.FoceInZone = false;
            this.tneEndS.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tneEndS.ForeColor = System.Drawing.Color.White;
            this.tneEndS.Location = new System.Drawing.Point(684, 195);
            this.tneEndS.Maximum = 59;
            this.tneEndS.Minimum = 0;
            this.tneEndS.Name = "tneEndS";
            this.tneEndS.Size = new System.Drawing.Size(138, 32);
            this.tneEndS.strText = "";
            this.tneEndS.TabIndex = 58;
            this.tneEndS.Value = 0;
            this.tneEndS.ValueStep = 1;
            // 
            // tneEndm
            // 
            this.tneEndm.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tneEndm.CanEdit = true;
            this.tneEndm.DefaultValue = 0;
            this.tneEndm.FoceInZone = false;
            this.tneEndm.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tneEndm.ForeColor = System.Drawing.Color.White;
            this.tneEndm.Location = new System.Drawing.Point(437, 195);
            this.tneEndm.Maximum = 59;
            this.tneEndm.Minimum = 0;
            this.tneEndm.Name = "tneEndm";
            this.tneEndm.Size = new System.Drawing.Size(138, 32);
            this.tneEndm.strText = "";
            this.tneEndm.TabIndex = 57;
            this.tneEndm.Value = 0;
            this.tneEndm.ValueStep = 1;
            // 
            // tneEndH
            // 
            this.tneEndH.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tneEndH.CanEdit = true;
            this.tneEndH.DefaultValue = 0;
            this.tneEndH.FoceInZone = false;
            this.tneEndH.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tneEndH.ForeColor = System.Drawing.Color.White;
            this.tneEndH.Location = new System.Drawing.Point(175, 195);
            this.tneEndH.Maximum = 23;
            this.tneEndH.Minimum = 0;
            this.tneEndH.Name = "tneEndH";
            this.tneEndH.Size = new System.Drawing.Size(138, 32);
            this.tneEndH.strText = "";
            this.tneEndH.TabIndex = 56;
            this.tneEndH.Value = 0;
            this.tneEndH.ValueStep = 1;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label9.Location = new System.Drawing.Point(94, 206);
            this.label9.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(74, 21);
            this.label9.TabIndex = 55;
            this.label9.Text = "结束时间";
            // 
            // tcbtType
            // 
            this.tcbtType.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tcbtType.CenterShow = true;
            this.tcbtType.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tcbtType.ForeColor = System.Drawing.Color.White;
            this.tcbtType.Items = new string[] {
        "充电",
        "放电"};
            this.tcbtType.Location = new System.Drawing.Point(437, 274);
            this.tcbtType.Name = "tcbtType";
            this.tcbtType.SelectItemIndex = 0;
            this.tcbtType.Size = new System.Drawing.Size(169, 32);
            this.tcbtType.strText = "充电";
            this.tcbtType.TabIndex = 54;
            this.tcbtType.Value = 0;
            // 
            // tnedwaValue
            // 
            this.tnedwaValue.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tnedwaValue.CanEdit = true;
            this.tnedwaValue.DefaultValue = 0;
            this.tnedwaValue.FoceInZone = false;
            this.tnedwaValue.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tnedwaValue.ForeColor = System.Drawing.Color.White;
            this.tnedwaValue.Location = new System.Drawing.Point(684, 274);
            this.tnedwaValue.Maximum = 840;
            this.tnedwaValue.Minimum = 0;
            this.tnedwaValue.Name = "tnedwaValue";
            this.tnedwaValue.Size = new System.Drawing.Size(170, 32);
            this.tnedwaValue.strText = "";
            this.tnedwaValue.TabIndex = 53;
            this.tnedwaValue.Value = 0;
            this.tnedwaValue.ValueStep = 1;
            // 
            // tcbPCSType
            // 
            this.tcbPCSType.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tcbPCSType.CenterShow = true;
            this.tcbPCSType.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tcbPCSType.ForeColor = System.Drawing.Color.White;
            this.tcbPCSType.Items = new string[] {
        "待机",
        "恒流",
        "恒压",
        "恒功率",
        "AC恒压",
        "自适应需量"};
            this.tcbPCSType.Location = new System.Drawing.Point(171, 274);
            this.tcbPCSType.Name = "tcbPCSType";
            this.tcbPCSType.SelectItemIndex = 0;
            this.tcbPCSType.Size = new System.Drawing.Size(169, 32);
            this.tcbPCSType.strText = "待机";
            this.tcbPCSType.TabIndex = 51;
            this.tcbPCSType.Value = 0;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label5.Location = new System.Drawing.Point(827, 133);
            this.label5.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(26, 21);
            this.label5.TabIndex = 50;
            this.label5.Text = "秒";
            this.label5.Click += new System.EventHandler(this.label5_Click);
            // 
            // labm
            // 
            this.labm.AutoSize = true;
            this.labm.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labm.Location = new System.Drawing.Point(580, 133);
            this.labm.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labm.Name = "labm";
            this.labm.Size = new System.Drawing.Size(26, 21);
            this.labm.TabIndex = 49;
            this.labm.Text = "分";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label1.Location = new System.Drawing.Point(318, 133);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(26, 21);
            this.label1.TabIndex = 48;
            this.label1.Text = "时";
            // 
            // tneStartS
            // 
            this.tneStartS.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tneStartS.CanEdit = true;
            this.tneStartS.DefaultValue = 0;
            this.tneStartS.FoceInZone = false;
            this.tneStartS.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tneStartS.ForeColor = System.Drawing.Color.White;
            this.tneStartS.Location = new System.Drawing.Point(684, 122);
            this.tneStartS.Maximum = 59;
            this.tneStartS.Minimum = 0;
            this.tneStartS.Name = "tneStartS";
            this.tneStartS.Size = new System.Drawing.Size(138, 32);
            this.tneStartS.strText = "";
            this.tneStartS.TabIndex = 47;
            this.tneStartS.Value = 0;
            this.tneStartS.ValueStep = 1;
            // 
            // tneStartm
            // 
            this.tneStartm.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tneStartm.CanEdit = true;
            this.tneStartm.DefaultValue = 0;
            this.tneStartm.FoceInZone = false;
            this.tneStartm.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tneStartm.ForeColor = System.Drawing.Color.White;
            this.tneStartm.Location = new System.Drawing.Point(437, 122);
            this.tneStartm.Maximum = 59;
            this.tneStartm.Minimum = 0;
            this.tneStartm.Name = "tneStartm";
            this.tneStartm.Size = new System.Drawing.Size(138, 32);
            this.tneStartm.strText = "";
            this.tneStartm.TabIndex = 46;
            this.tneStartm.Value = 0;
            this.tneStartm.ValueStep = 1;
            // 
            // tneStartH
            // 
            this.tneStartH.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tneStartH.CanEdit = true;
            this.tneStartH.DefaultValue = 0;
            this.tneStartH.FoceInZone = false;
            this.tneStartH.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tneStartH.ForeColor = System.Drawing.Color.White;
            this.tneStartH.Location = new System.Drawing.Point(175, 122);
            this.tneStartH.Maximum = 23;
            this.tneStartH.Minimum = 0;
            this.tneStartH.Name = "tneStartH";
            this.tneStartH.Size = new System.Drawing.Size(138, 32);
            this.tneStartH.strText = "";
            this.tneStartH.TabIndex = 45;
            this.tneStartH.Value = 0;
            this.tneStartH.ValueStep = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(860, 274);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(69, 21);
            this.label2.TabIndex = 44;
            this.label2.Text = "A/V/Kw";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label6.Location = new System.Drawing.Point(94, 274);
            this.label6.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(72, 21);
            this.label6.TabIndex = 35;
            this.label6.Text = "PCS设置";
            // 
            // label49
            // 
            this.label49.AutoSize = true;
            this.label49.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label49.Location = new System.Drawing.Point(94, 133);
            this.label49.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label49.Name = "label49";
            this.label49.Size = new System.Drawing.Size(74, 21);
            this.label49.TabIndex = 33;
            this.label49.Text = "开始时间";
            // 
            // label26
            // 
            this.label26.AutoSize = true;
            this.label26.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label26.Location = new System.Drawing.Point(624, 274);
            this.label26.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label26.Name = "label26";
            this.label26.Size = new System.Drawing.Size(58, 21);
            this.label26.TabIndex = 31;
            this.label26.Text = "设置值";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label3.Location = new System.Drawing.Point(363, 285);
            this.label3.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(58, 21);
            this.label3.TabIndex = 16;
            this.label3.Text = "充分电";
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.btnOK);
            this.panel1.Controls.Add(this.btnClose);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Margin = new System.Windows.Forms.Padding(2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1024, 60);
            this.panel1.TabIndex = 16;
            // 
            // btnOK
            // 
            this.btnOK.BackColor = System.Drawing.Color.Transparent;
            this.btnOK.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnOK.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnOK.FlatAppearance.MouseOverBackColor = System.Drawing.Color.SkyBlue;
            this.btnOK.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOK.ForeColor = System.Drawing.Color.White;
            this.btnOK.Location = new System.Drawing.Point(771, 7);
            this.btnOK.Margin = new System.Windows.Forms.Padding(1);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(88, 44);
            this.btnOK.TabIndex = 6;
            this.btnOK.Text = "确定";
            this.btnOK.UseVisualStyleBackColor = false;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnClose
            // 
            this.btnClose.BackColor = System.Drawing.Color.Transparent;
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnClose.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnClose.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnClose.FlatAppearance.MouseOverBackColor = System.Drawing.Color.SkyBlue;
            this.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClose.ForeColor = System.Drawing.Color.White;
            this.btnClose.Location = new System.Drawing.Point(870, 6);
            this.btnClose.Margin = new System.Windows.Forms.Padding(1);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(88, 44);
            this.btnClose.TabIndex = 5;
            this.btnClose.Text = "取消";
            this.btnClose.UseVisualStyleBackColor = false;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // frmoneTactics
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.ClientSize = new System.Drawing.Size(1024, 768);
            this.Controls.Add(this.plData);
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Margin = new System.Windows.Forms.Padding(1);
            this.Name = "frmoneTactics";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "结束时间";
            this.plData.ResumeLayout(false);
            this.plData.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel plData;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label49;
        private System.Windows.Forms.Label label26;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label labm;
        private System.Windows.Forms.Label label1;
        private TouchNumberEdit tneStartS;
        private TouchNumberEdit tneStartm;
        private TouchNumberEdit tneStartH;
        private TouchCombox tcbtType;
        private TouchNumberEdit tnedwaValue;
        private TouchCombox tcbPCSType;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private TouchNumberEdit tneEndS;
        private TouchNumberEdit tneEndm;
        private TouchNumberEdit tneEndH;
        private System.Windows.Forms.Label label9;
    }
}