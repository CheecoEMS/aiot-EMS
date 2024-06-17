using System;
using System.Windows.Forms;

namespace EMS
{/*
    1A200EMS2305180001b:
    启用co、水浸、烟雾、温湿度传感器
    修正收益iotcode跟随机器号变化
    修正策略运行，手动停机问题
    重新设计串口读取顺序
    1A200EMS2305250001b:
    更新GPIO地址表和输入输出控制

    1A200EMS2306010001：
    1\co、水浸、烟雾、温湿度传感器出现超限为故障记录 
    2\增加功率输出x%，用于防逆流和需量控制
    3\增加一个485，可读写几个寄存器
       控制模式
       开关机
       输入输出
       功率
       输入输出率x%，同上描述
    4\增加支持电表互感器pt，ct的设置支持，可以不同电表设置不同互感器

    1A200EMS2306220001A
    增加主从机控制
        取消功率输出x%，改为从机服从主机连控的策略和功率限制
        连控防逆流和需量控制
    1A200EMS2306290001A
        增加pcs进口出风口温度
        修正故障发生后策略二次开机问题
        将pcslist、空调、BMS改为单个配置 
     1A200EMS2307030001A
        修正BMS温度错位问题
       将led分为 gpio11为2级别故障,gpio12为本停机故障
    1A200EMS2307070001
       Power LED改为长闭
       修正无初始化数据启动报错问题
       增加重启软件功能
       禁止离网连控，只允许单机运行
       增加强制PCS运行模式
       跟进BMS温度变化的新协议
       修改消防数据为0的bug
       修正电表设置时间区段的bug
       add a abutton(apply air controler seting) 
  */
    public partial class frmAbout : Form
    {
        static private frmAbout oneForm = null;
        public frmAbout()
        {
            InitializeComponent();
            DoubleBuffered = true;
            labSN.Text = "设备SN：" + frmSet.SysID.Trim();
            labSoftVerb.Text = "软件版本：EMS240525Master3.1.2";
        }

        static public void CloseForm()
        {
            if (oneForm != null)
            { 
                oneForm.Close();
                oneForm.Dispose();
                oneForm = null;
            }
        }

        static public void ShowForm()
        {
            if (oneForm == null)
                oneForm = new frmAbout();
            oneForm.SetFormPower(frmMain.UserPower);
            oneForm.ShowDialog();
        }

        public void SetFormPower(int aPower)
        {
            btnLine.Visible = (aPower >= 0);
            btnState.Visible = (aPower >= 0);
            btnWarning.Visible = (aPower >= 1);
            btnControl.Visible = (aPower >= 2);
            btnSet.Visible = (aPower >= 3);
        }

        private void btnMain_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmMain.ShowMainForm();
        }

        private void btnLine_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmLine.ShowForm();

        }

        private void btnState_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmState.ShowForm();
        }

        private void btnWarning_Click(object sender, EventArgs e)
        {
            CloseForm();
            frmWarrning.ShowForm();
        }

        private void btnUpdate_Click(object sender, EventArgs e)
        {

        }

        private void btnAbout_Click(object sender, EventArgs e)
        {

        }
    }
}
