
namespace EMS
{
    partial class frmoneEquipment
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
            this.plTop = new System.Windows.Forms.Panel();
            this.button1 = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.panel2 = new System.Windows.Forms.Panel();
            this.tneLocalPort = new EMS.TouchNumberEdit(this.components);
            this.tneServerPort = new EMS.TouchNumberEdit(this.components);
            this.trbClient = new EMS.TRadioButton(this.components);
            this.trbServer = new EMS.TRadioButton(this.components);
            this.tbServerIP = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label25 = new System.Windows.Forms.Label();
            this.label26 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label49 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.ceModel = new System.Windows.Forms.ComboBox();
            this.tbeName = new System.Windows.Forms.TextBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.trbUDPModbus = new EMS.TRadioButton(this.components);
            this.trbTCPModbus = new EMS.TRadioButton(this.components);
            this.trb485Modbus = new EMS.TRadioButton(this.components);
            this.plData = new System.Windows.Forms.Panel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.label7 = new System.Windows.Forms.Label();
            this.tnePC = new EMS.TouchNumberEdit(this.components);
            this.tcbBaudRate = new EMS.TouchCombox(this.components);
            this.tcbDatabits = new EMS.TouchCombox(this.components);
            this.tcbSysID = new EMS.TouchCombox(this.components);
            this.tcb485Port = new EMS.TouchCombox(this.components);
            this.tcbType = new EMS.TouchCombox(this.components);
            this.plTop.SuspendLayout();
            this.panel2.SuspendLayout();
            this.panel1.SuspendLayout();
            this.plData.SuspendLayout();
            this.panel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // plTop
            // 
            this.plTop.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.plTop.Controls.Add(this.button1);
            this.plTop.Controls.Add(this.btnOK);
            this.plTop.Controls.Add(this.btnClose);
            this.plTop.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.plTop.Location = new System.Drawing.Point(24, 24);
            this.plTop.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.plTop.Name = "plTop";
            this.plTop.Size = new System.Drawing.Size(925, 59);
            this.plTop.TabIndex = 13;
            // 
            // button1
            // 
            this.button1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.button1.Enabled = false;
            this.button1.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.button1.FlatAppearance.CheckedBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button1.ForeColor = System.Drawing.Color.White;
            this.button1.Location = new System.Drawing.Point(29, 5);
            this.button1.Margin = new System.Windows.Forms.Padding(1, 1, 1, 1);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(118, 44);
            this.button1.TabIndex = 35;
            this.button1.Text = "设备信息";
            this.button1.UseVisualStyleBackColor = false;
            // 
            // btnOK
            // 
            this.btnOK.BackColor = System.Drawing.Color.Transparent;
            this.btnOK.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnOK.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(169)))), ((int)(((byte)(255)))));
            this.btnOK.FlatAppearance.MouseOverBackColor = System.Drawing.Color.SkyBlue;
            this.btnOK.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOK.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnOK.ForeColor = System.Drawing.Color.White;
            this.btnOK.Location = new System.Drawing.Point(725, 5);
            this.btnOK.Margin = new System.Windows.Forms.Padding(1, 1, 1, 1);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(86, 45);
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
            this.btnClose.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnClose.ForeColor = System.Drawing.Color.White;
            this.btnClose.Location = new System.Drawing.Point(828, 6);
            this.btnClose.Margin = new System.Windows.Forms.Padding(1, 1, 1, 1);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(88, 44);
            this.btnClose.TabIndex = 5;
            this.btnClose.Text = "取消";
            this.btnClose.UseVisualStyleBackColor = false;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.tneLocalPort);
            this.panel2.Controls.Add(this.tneServerPort);
            this.panel2.Controls.Add(this.trbClient);
            this.panel2.Controls.Add(this.trbServer);
            this.panel2.Controls.Add(this.tbServerIP);
            this.panel2.Controls.Add(this.label1);
            this.panel2.Controls.Add(this.label2);
            this.panel2.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.panel2.Location = new System.Drawing.Point(108, 279);
            this.panel2.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(756, 134);
            this.panel2.TabIndex = 13;
            // 
            // tneLocalPort
            // 
            this.tneLocalPort.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tneLocalPort.CanEdit = true;
            this.tneLocalPort.DefaultValue = 0;
            this.tneLocalPort.FoceInZone = false;
            this.tneLocalPort.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tneLocalPort.ForeColor = System.Drawing.Color.White;
            this.tneLocalPort.Location = new System.Drawing.Point(520, 69);
            this.tneLocalPort.Maximum = 999999;
            this.tneLocalPort.Minimum = 1;
            this.tneLocalPort.Name = "tneLocalPort";
            this.tneLocalPort.Size = new System.Drawing.Size(206, 32);
            this.tneLocalPort.strText = "";
            this.tneLocalPort.TabIndex = 34;
            this.tneLocalPort.Value = 0;
            this.tneLocalPort.ValueStep = 1;
            // 
            // tneServerPort
            // 
            this.tneServerPort.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tneServerPort.CanEdit = true;
            this.tneServerPort.DefaultValue = 0;
            this.tneServerPort.FoceInZone = false;
            this.tneServerPort.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tneServerPort.ForeColor = System.Drawing.Color.White;
            this.tneServerPort.Location = new System.Drawing.Point(520, 20);
            this.tneServerPort.Maximum = 999999;
            this.tneServerPort.Minimum = 1;
            this.tneServerPort.Name = "tneServerPort";
            this.tneServerPort.Size = new System.Drawing.Size(208, 32);
            this.tneServerPort.strText = "";
            this.tneServerPort.TabIndex = 33;
            this.tneServerPort.Value = 0;
            this.tneServerPort.ValueStep = 1;
            // 
            // trbClient
            // 
            this.trbClient.BackColor = System.Drawing.Color.Transparent;
            this.trbClient.Caption = "客户端";
            this.trbClient.Checked = false;
            this.trbClient.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.trbClient.ForeColor = System.Drawing.Color.White;
            this.trbClient.Location = new System.Drawing.Point(21, 84);
            this.trbClient.Name = "trbClient";
            this.trbClient.Size = new System.Drawing.Size(235, 32);
            this.trbClient.TabIndex = 32;
            // 
            // trbServer
            // 
            this.trbServer.BackColor = System.Drawing.Color.Transparent;
            this.trbServer.Caption = "服务器";
            this.trbServer.Checked = false;
            this.trbServer.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.trbServer.ForeColor = System.Drawing.Color.White;
            this.trbServer.Location = new System.Drawing.Point(21, 23);
            this.trbServer.Name = "trbServer";
            this.trbServer.Size = new System.Drawing.Size(101, 32);
            this.trbServer.TabIndex = 31;
            // 
            // tbServerIP
            // 
            this.tbServerIP.Location = new System.Drawing.Point(127, 23);
            this.tbServerIP.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tbServerIP.Name = "tbServerIP";
            this.tbServerIP.Size = new System.Drawing.Size(165, 29);
            this.tbServerIP.TabIndex = 9;
            this.tbServerIP.Text = "192.192.192.192";
            this.tbServerIP.TextChanged += new System.EventHandler(this.tbServerIP_TextChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(408, 80);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(106, 21);
            this.label1.TabIndex = 13;
            this.label1.Text = "本地端口号：";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.ForeColor = System.Drawing.Color.White;
            this.label2.Location = new System.Drawing.Point(430, 28);
            this.label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(74, 21);
            this.label2.TabIndex = 10;
            this.label2.Text = "端口号：";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label3.ForeColor = System.Drawing.Color.White;
            this.label3.Location = new System.Drawing.Point(112, 49);
            this.label3.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(52, 21);
            this.label3.TabIndex = 16;
            this.label3.Text = "类  型";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label5.ForeColor = System.Drawing.Color.White;
            this.label5.Location = new System.Drawing.Point(549, 146);
            this.label5.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(74, 21);
            this.label5.TabIndex = 13;
            this.label5.Text = "端口号：";
            // 
            // label25
            // 
            this.label25.AutoSize = true;
            this.label25.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label25.ForeColor = System.Drawing.Color.White;
            this.label25.Location = new System.Drawing.Point(551, 244);
            this.label25.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label25.Name = "label25";
            this.label25.Size = new System.Drawing.Size(68, 21);
            this.label25.TabIndex = 29;
            this.label25.Text = "速  率：";
            // 
            // label26
            // 
            this.label26.AutoSize = true;
            this.label26.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label26.ForeColor = System.Drawing.Color.White;
            this.label26.Location = new System.Drawing.Point(549, 192);
            this.label26.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label26.Name = "label26";
            this.label26.Size = new System.Drawing.Size(74, 21);
            this.label26.TabIndex = 31;
            this.label26.Text = "数据位：";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label6.ForeColor = System.Drawing.Color.White;
            this.label6.Location = new System.Drawing.Point(562, 49);
            this.label6.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(56, 21);
            this.label6.TabIndex = 35;
            this.label6.Text = "型  号:";
            // 
            // label49
            // 
            this.label49.AutoSize = true;
            this.label49.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label49.ForeColor = System.Drawing.Color.White;
            this.label49.Location = new System.Drawing.Point(551, 102);
            this.label49.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label49.Name = "label49";
            this.label49.Size = new System.Drawing.Size(63, 21);
            this.label49.TabIndex = 33;
            this.label49.Text = "通讯ID:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label4.ForeColor = System.Drawing.Color.White;
            this.label4.Location = new System.Drawing.Point(112, 102);
            this.label4.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(59, 21);
            this.label4.TabIndex = 37;
            this.label4.Text = "部件ID";
            // 
            // ceModel
            // 
            this.ceModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.Simple;
            this.ceModel.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.ceModel.FormattingEnabled = true;
            this.ceModel.Items.AddRange(new object[] {
            "电表",
            "PCS逆变器",
            "BMS ",
            "空调系统",
            "其他"});
            this.ceModel.Location = new System.Drawing.Point(626, 37);
            this.ceModel.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.ceModel.Name = "ceModel";
            this.ceModel.Size = new System.Drawing.Size(210, 41);
            this.ceModel.TabIndex = 36;
            // 
            // tbeName
            // 
            this.tbeName.Location = new System.Drawing.Point(187, 94);
            this.tbeName.Name = "tbeName";
            this.tbeName.Size = new System.Drawing.Size(210, 29);
            this.tbeName.TabIndex = 38;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.trbUDPModbus);
            this.panel1.Controls.Add(this.trbTCPModbus);
            this.panel1.Controls.Add(this.trb485Modbus);
            this.panel1.Location = new System.Drawing.Point(108, 137);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(292, 137);
            this.panel1.TabIndex = 47;
            // 
            // trbUDPModbus
            // 
            this.trbUDPModbus.BackColor = System.Drawing.Color.Transparent;
            this.trbUDPModbus.Caption = "UDP Modbus";
            this.trbUDPModbus.Checked = false;
            this.trbUDPModbus.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.trbUDPModbus.ForeColor = System.Drawing.Color.White;
            this.trbUDPModbus.Location = new System.Drawing.Point(21, 102);
            this.trbUDPModbus.Name = "trbUDPModbus";
            this.trbUDPModbus.Size = new System.Drawing.Size(235, 32);
            this.trbUDPModbus.TabIndex = 2;
            // 
            // trbTCPModbus
            // 
            this.trbTCPModbus.BackColor = System.Drawing.Color.Transparent;
            this.trbTCPModbus.Caption = "TCP Modbus";
            this.trbTCPModbus.Checked = false;
            this.trbTCPModbus.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.trbTCPModbus.ForeColor = System.Drawing.Color.White;
            this.trbTCPModbus.Location = new System.Drawing.Point(21, 53);
            this.trbTCPModbus.Name = "trbTCPModbus";
            this.trbTCPModbus.Size = new System.Drawing.Size(235, 32);
            this.trbTCPModbus.TabIndex = 1;
            // 
            // trb485Modbus
            // 
            this.trb485Modbus.BackColor = System.Drawing.Color.Transparent;
            this.trb485Modbus.Caption = "485 Modbus";
            this.trb485Modbus.Checked = false;
            this.trb485Modbus.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.trb485Modbus.ForeColor = System.Drawing.Color.White;
            this.trb485Modbus.Location = new System.Drawing.Point(21, 9);
            this.trb485Modbus.Name = "trb485Modbus";
            this.trb485Modbus.Size = new System.Drawing.Size(235, 32);
            this.trb485Modbus.TabIndex = 0;
            this.trb485Modbus.Load += new System.EventHandler(this.trb485Modbus_Load);
            // 
            // plData
            // 
            this.plData.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(55)))), ((int)(((byte)(64)))));
            this.plData.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.plData.Controls.Add(this.panel3);
            this.plData.Controls.Add(this.panel1);
            this.plData.Controls.Add(this.tcbBaudRate);
            this.plData.Controls.Add(this.tcbDatabits);
            this.plData.Controls.Add(this.tcbSysID);
            this.plData.Controls.Add(this.tcb485Port);
            this.plData.Controls.Add(this.tcbType);
            this.plData.Controls.Add(this.tbeName);
            this.plData.Controls.Add(this.ceModel);
            this.plData.Controls.Add(this.label4);
            this.plData.Controls.Add(this.label49);
            this.plData.Controls.Add(this.label6);
            this.plData.Controls.Add(this.label26);
            this.plData.Controls.Add(this.label25);
            this.plData.Controls.Add(this.label5);
            this.plData.Controls.Add(this.label3);
            this.plData.Controls.Add(this.panel2);
            this.plData.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.plData.Location = new System.Drawing.Point(24, 82);
            this.plData.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.plData.Name = "plData";
            this.plData.Size = new System.Drawing.Size(925, 680);
            this.plData.TabIndex = 15;
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.label7);
            this.panel3.Controls.Add(this.tnePC);
            this.panel3.Location = new System.Drawing.Point(108, 433);
            this.panel3.Name = "panel3";
            this.panel3.Size = new System.Drawing.Size(756, 100);
            this.panel3.TabIndex = 50;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.ForeColor = System.Drawing.Color.White;
            this.label7.Location = new System.Drawing.Point(17, 20);
            this.label7.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(74, 21);
            this.label7.TabIndex = 48;
            this.label7.Text = "数据系数";
            // 
            // tnePC
            // 
            this.tnePC.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tnePC.CanEdit = true;
            this.tnePC.DefaultValue = 1;
            this.tnePC.FoceInZone = false;
            this.tnePC.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tnePC.ForeColor = System.Drawing.Color.White;
            this.tnePC.Location = new System.Drawing.Point(96, 13);
            this.tnePC.Maximum = 999999;
            this.tnePC.Minimum = 1;
            this.tnePC.Name = "tnePC";
            this.tnePC.Size = new System.Drawing.Size(193, 32);
            this.tnePC.strText = "";
            this.tnePC.TabIndex = 49;
            this.tnePC.Value = 1;
            this.tnePC.ValueStep = 1;
            // 
            // tcbBaudRate
            // 
            this.tcbBaudRate.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tcbBaudRate.CenterShow = true;
            this.tcbBaudRate.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tcbBaudRate.ForeColor = System.Drawing.Color.White;
            this.tcbBaudRate.Items = new string[] {
        "115200",
        "57600",
        "38400",
        "28800",
        "19200",
        "14400",
        "9600",
        "4800",
        "2400",
        "1800",
        "1200"};
            this.tcbBaudRate.Location = new System.Drawing.Point(630, 233);
            this.tcbBaudRate.Name = "tcbBaudRate";
            this.tcbBaudRate.SelectItemIndex = 0;
            this.tcbBaudRate.Size = new System.Drawing.Size(208, 32);
            this.tcbBaudRate.strText = "115200";
            this.tcbBaudRate.TabIndex = 43;
            this.tcbBaudRate.Value = 0;
            // 
            // tcbDatabits
            // 
            this.tcbDatabits.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tcbDatabits.CenterShow = true;
            this.tcbDatabits.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tcbDatabits.ForeColor = System.Drawing.Color.White;
            this.tcbDatabits.Items = new string[] {
        "7",
        "8"};
            this.tcbDatabits.Location = new System.Drawing.Point(629, 181);
            this.tcbDatabits.Name = "tcbDatabits";
            this.tcbDatabits.SelectItemIndex = 0;
            this.tcbDatabits.Size = new System.Drawing.Size(208, 32);
            this.tcbDatabits.strText = "7";
            this.tcbDatabits.TabIndex = 42;
            this.tcbDatabits.Value = 7;
            // 
            // tcbSysID
            // 
            this.tcbSysID.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tcbSysID.CenterShow = true;
            this.tcbSysID.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tcbSysID.ForeColor = System.Drawing.Color.White;
            this.tcbSysID.Items = new string[] {
        "1",
        "2",
        "3",
        "4",
        "5",
        "6",
        "7",
        "8",
        "9",
        "10",
        "11",
        "12",
        "13",
        "14",
        "15",
        "16",
        "17",
        "18",
        "19",
        "20",
        "170"};
            this.tcbSysID.Location = new System.Drawing.Point(628, 91);
            this.tcbSysID.Name = "tcbSysID";
            this.tcbSysID.SelectItemIndex = 0;
            this.tcbSysID.Size = new System.Drawing.Size(208, 32);
            this.tcbSysID.strText = "1";
            this.tcbSysID.TabIndex = 41;
            this.tcbSysID.Value = 1;
            // 
            // tcb485Port
            // 
            this.tcb485Port.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tcb485Port.CenterShow = true;
            this.tcb485Port.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tcb485Port.ForeColor = System.Drawing.Color.White;
            this.tcb485Port.Items = new string[] {
        "Com1",
        "Com2",
        "Com3",
        "Com4",
        "Com5",
        "Com6",
        "Com7",
        "Com8",
        "Com9",
        "Com10",
        "Com11",
        "Com12",
        "Com13",
        "Com14",
        "Com15",
        "Com16",
        "Com17",
        "Com18",
        "Com19"};
            this.tcb485Port.Location = new System.Drawing.Point(628, 137);
            this.tcb485Port.Name = "tcb485Port";
            this.tcb485Port.SelectItemIndex = 0;
            this.tcb485Port.Size = new System.Drawing.Size(210, 32);
            this.tcb485Port.strText = "Com1";
            this.tcb485Port.TabIndex = 40;
            this.tcb485Port.Value = 0;
            // 
            // tcbType
            // 
            this.tcbType.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(75)))), ((int)(((byte)(86)))), ((int)(((byte)(93)))));
            this.tcbType.CenterShow = true;
            this.tcbType.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tcbType.ForeColor = System.Drawing.Color.White;
            this.tcbType.Items = new string[] {
        "用户侧电表",
        "设备电表",
        "辅组电表",
        "PCS逆变器",
        "BMS ",
        "空调系统",
        "消防",
        "计量电表",
        "水浸传感器",
        "一氧化碳传感器",
        "温湿度传感器",
        "烟雾传感器",
        "UPS",
        "液冷机",
        "DSP2",
        "汇流柜电表",
        "储能电站总表",
        "灯板",
        "除湿机"};
            this.tcbType.Location = new System.Drawing.Point(187, 38);
            this.tcbType.Name = "tcbType";
            this.tcbType.SelectItemIndex = 0;
            this.tcbType.Size = new System.Drawing.Size(210, 32);
            this.tcbType.strText = "用户侧电表";
            this.tcbType.TabIndex = 39;
            this.tcbType.Value = 0;
            this.tcbType.Load += new System.EventHandler(this.tcbType_Load);
            // 
            // frmoneEquipment
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.ClientSize = new System.Drawing.Size(1009, 635);
            this.Controls.Add(this.plData);
            this.Controls.Add(this.plTop);
            this.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "frmoneEquipment";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "frmoneEquipment";
            this.plTop.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.plData.ResumeLayout(false);
            this.plData.PerformLayout();
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel plTop;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Panel panel2;
        private TRadioButton trbClient;
        private TRadioButton trbServer;
        private System.Windows.Forms.TextBox tbServerIP;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label25;
        private System.Windows.Forms.Label label26;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label49;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox ceModel;
        private System.Windows.Forms.TextBox tbeName;
        private TouchCombox tcbType;
        private TouchCombox tcb485Port;
        private TouchCombox tcbSysID;
        private TouchCombox tcbDatabits;
        private TouchCombox tcbBaudRate;
        private System.Windows.Forms.Panel plData;
        private TouchNumberEdit tneLocalPort;
        private TouchNumberEdit tneServerPort;
        private System.Windows.Forms.Panel panel1;
        private TRadioButton trbUDPModbus;
        private TRadioButton trbTCPModbus;
        private TRadioButton trb485Modbus;
        private TouchNumberEdit tnePC;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Panel panel3;
    }
}