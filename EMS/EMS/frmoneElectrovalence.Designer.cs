
namespace EMS
{
    partial class frmoneElectrovalence
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
            this.tneM = new EMS.TouchNumberEdit(this.components);
            this.tneH = new EMS.TouchNumberEdit(this.components);
            this.tcbSection = new EMS.TouchCombox(this.components);
            this.tbeName = new EMS.TouchCombox(this.components);
            this.label4 = new System.Windows.Forms.Label();
            this.label49 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.button1 = new System.Windows.Forms.Button();
            this.btnCanncel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.plData.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // plData
            // 
            this.plData.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(55)))), ((int)(((byte)(64)))));
            this.plData.Controls.Add(this.label2);
            this.plData.Controls.Add(this.label1);
            this.plData.Controls.Add(this.tneM);
            this.plData.Controls.Add(this.tneH);
            this.plData.Controls.Add(this.tcbSection);
            this.plData.Controls.Add(this.tbeName);
            this.plData.Controls.Add(this.label4);
            this.plData.Controls.Add(this.label49);
            this.plData.Controls.Add(this.label5);
            this.plData.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.plData.Location = new System.Drawing.Point(24, 82);
            this.plData.Margin = new System.Windows.Forms.Padding(2);
            this.plData.Name = "plData";
            this.plData.Size = new System.Drawing.Size(976, 665);
            this.plData.TabIndex = 19;
            // 
            // tneM
            // 
            this.tneM.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tneM.CanEdit = true;
            this.tneM.DefaultValue = 0;
            this.tneM.FoceInZone = false;
            this.tneM.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tneM.ForeColor = System.Drawing.Color.White;
            this.tneM.Location = new System.Drawing.Point(553, 131);
            this.tneM.Maximum = 0;
            this.tneM.Minimum = 0;
            this.tneM.Name = "tneM";
            this.tneM.Size = new System.Drawing.Size(102, 32);
            this.tneM.strText = "";
            this.tneM.TabIndex = 57;
            this.tneM.Value = 0;
            this.tneM.ValueStep = 1;
            // 
            // tneH
            // 
            this.tneH.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tneH.CanEdit = true;
            this.tneH.DefaultValue = 0;
            this.tneH.FoceInZone = false;
            this.tneH.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tneH.ForeColor = System.Drawing.Color.White;
            this.tneH.Location = new System.Drawing.Point(407, 131);
            this.tneH.Maximum = 0;
            this.tneH.Minimum = 0;
            this.tneH.Name = "tneH";
            this.tneH.Size = new System.Drawing.Size(102, 32);
            this.tneH.strText = "";
            this.tneH.TabIndex = 56;
            this.tneH.Value = 0;
            this.tneH.ValueStep = 1;
            // 
            // tcbSection
            // 
            this.tcbSection.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tcbSection.CenterShow = true;
            this.tcbSection.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tcbSection.ForeColor = System.Drawing.Color.White;
            this.tcbSection.Items = new string[] {
        "第一套时段表",
        "第二套时段表"};
            this.tcbSection.Location = new System.Drawing.Point(408, 67);
            this.tcbSection.Name = "tcbSection";
            this.tcbSection.SelectItemIndex = 0;
            this.tcbSection.Size = new System.Drawing.Size(247, 32);
            this.tcbSection.strText = "第一套时段表";
            this.tcbSection.TabIndex = 55;
            this.tcbSection.Value = 0;
            // 
            // tbeName
            // 
            this.tbeName.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tbeName.CenterShow = true;
            this.tbeName.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tbeName.ForeColor = System.Drawing.Color.White;
            this.tbeName.Items = new string[] {
        "无",
        "尖",
        "峰",
        "平",
        "谷"};
            this.tbeName.Location = new System.Drawing.Point(408, 196);
            this.tbeName.Name = "tbeName";
            this.tbeName.SelectItemIndex = 0;
            this.tbeName.Size = new System.Drawing.Size(247, 32);
            this.tbeName.strText = "无";
            this.tbeName.TabIndex = 53;
            this.tbeName.Value = 0;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("微软雅黑 Light", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label4.ForeColor = System.Drawing.Color.White;
            this.label4.Location = new System.Drawing.Point(328, 67);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(74, 21);
            this.label4.TabIndex = 50;
            this.label4.Text = "时段表号";
            // 
            // label49
            // 
            this.label49.AutoSize = true;
            this.label49.Font = new System.Drawing.Font("微软雅黑 Light", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label49.ForeColor = System.Drawing.Color.White;
            this.label49.Location = new System.Drawing.Point(328, 131);
            this.label49.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label49.Name = "label49";
            this.label49.Size = new System.Drawing.Size(74, 21);
            this.label49.TabIndex = 39;
            this.label49.Text = "开始时间";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("微软雅黑 Light", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label5.ForeColor = System.Drawing.Color.White;
            this.label5.Location = new System.Drawing.Point(329, 196);
            this.label5.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(74, 21);
            this.label5.TabIndex = 13;
            this.label5.Text = "峰谷类型";
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.button1);
            this.panel1.Controls.Add(this.btnCanncel);
            this.panel1.Controls.Add(this.btnOk);
            this.panel1.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.panel1.Location = new System.Drawing.Point(24, 24);
            this.panel1.Margin = new System.Windows.Forms.Padding(2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(976, 54);
            this.panel1.TabIndex = 53;
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
            this.button1.Text = "时段信息";
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
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("微软雅黑 Light", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(514, 142);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(26, 21);
            this.label1.TabIndex = 58;
            this.label1.Text = "时";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("微软雅黑 Light", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label2.ForeColor = System.Drawing.Color.White;
            this.label2.Location = new System.Drawing.Point(660, 142);
            this.label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(26, 21);
            this.label2.TabIndex = 59;
            this.label2.Text = "分";
            // 
            // frmoneElectrovalence
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.ClientSize = new System.Drawing.Size(1024, 768);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.plData);
            this.ForeColor = System.Drawing.Color.White;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "frmoneElectrovalence";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "阶梯电价";
            this.Load += new System.EventHandler(this.frmoneElectrovalence_Load);
            this.plData.ResumeLayout(false);
            this.plData.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel plData;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label49;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button btnCanncel;
        private System.Windows.Forms.Button btnOk;
        private TouchNumberEdit tneM;
        private TouchNumberEdit tneH;
        private TouchCombox tcbSection;
        private TouchCombox tbeName;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
    }
}