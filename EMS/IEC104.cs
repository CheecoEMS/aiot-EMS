using EMS;
using System;
using Modbus;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Google.Protobuf.WellKnownTypes;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Utilities;
using static Mysqlx.Datatypes.Scalar.Types;
using System.Diagnostics;
using Org.BouncyCastle.Utilities.Net;
using DotNetty.Codecs;
using Newtonsoft.Json.Linq;
using M2Mqtt.Internal;
using log4net;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using Org.BouncyCastle.Asn1.Pkcs;
using System.Windows.Forms.DataVisualization.Charting;
using System.Reflection;



public struct APCI
{    // U-Format
    public byte start;    // 起始字节
    public byte len;      // 帧长度
    public byte TX_field1;   // 控制域1-4 发送序号
    public byte TX_field2;
    public byte RX_field3;   //接收序号
    public byte RX_field4;


    /// <summary>
    /// 重置发送序号和接收序号为 0
    /// </summary>
    public void Reset()
    {
        TX_field1 = 0;
        TX_field2 = 0;
        RX_field3 = 0;
        RX_field4 = 0;
    }
};



public struct ASDU
{

    public byte function;          // 类型标识
    public byte qual;              // 可变结构限定词
    public byte tx_cause_1;        // 传送原因
    public byte tx_cause_2;
    public byte commom_asdu_1;     // 公共地址
    public byte commom_asdu_2;

    public string Object_Address_1;  // 信息对象地址
    public string Object_Address_2;
    public string Object_Address_3;
    public byte[] data;            // 信息体
};

public struct APDU
{
    public APCI apci;
    public ASDU asdu;
    public bool Isconnect;            // 104通信连接标志

    //public APCI perv_apci;
    //public ASDU perv_asdu;
    public bool[] YX_rawdata;            // 遥信 数据
    public float[] YC_rawdata;            // 遥测 原数据
    public bool[] YX_perv_rawdata;            // 原数据
    public float[] YC_perv_rawdata;            // 原数据

    public int count_test;            // 测试值
    public bool bool_test;            // 测试值

};

namespace IEC104
{
    //public enum tranmission



    class BaseCommand
    {
        // 激活命令
        public byte CMD_STARTV =   0x04;
        public byte CMD_STOPV =    0x10;
        public byte CMD_TESTV =    0x40;

        // 确认命令
        public byte CMD_STARTC =   0x08;
        public byte CMD_STOPC  =   0x20;
        public byte CMD_TESTC  =   0x80;

    }

    public class CIEC104Slave
    {

        public delegate void OnReceive104DataEvent(object sender, PropertyChangedEventArgs e);//建立事件委托
        public event OnReceive104DataEvent Receive104DataEvent;//收到数据的事件

        static ManualResetEventSlim IEC104Send_Event = new ManualResetEventSlim(true);

        //8-22
        public delegate void IEC104_delegate();
        public IEC104_delegate iec104_delegate ;
        /*1.由于字节1和字节3的最低位固定为0，不用于构成序号，所以在计算序号时，要先转换成十进制数值，再除以2；

        2.由于低位字节在前，高位字节在后，所以计算时要先做颠倒；*/


        public ushort RxCounter = 0x0000;   // 接收序号
        public ushort TxCounter = 0x0000;   // 发送序号

        public static int[] isYKACK = new int[10];
        public static int[] isYDACK = new int[10];


        private static ILog log = LogManager.GetLogger("IEC104");

        public static APDU app;

        /* 
         * 值变化触发
         */
        private int _ErrorState_104;
        public int ErrorState_104 { get { return _ErrorState_104; } set { if (_ErrorState_104 != value) { _ErrorState_104 = value; } } }
        
        private int _RunState_104;
        public int RunState_104 { get { return _RunState_104; } set { if (_RunState_104 != value) { _RunState_104 = value;  } } }
        
        private int _EState_104;
        public int EState_104 { get { return _EState_104; } set { if (_EState_104 != value) { _EState_104 = value;  } } }
       

        public  bool HostStart_104 { get { return _HostStart_104; } set { if (_HostStart_104 != value) {  _HostStart_104 = value; CIEC104Slave.ReturnSoleYXData(0X1E); } } }//遥控0点位变化
        private  bool _HostStart_104;

/*        public  double  aC_104 { get { return _aC_104; } set { if (_aC_104 != value) {_aC_104 = value; CIEC104Slave.ReturnSoleYCData();  } } }
        private  double _aC_104;

        public  double PCSKVA_104 { get { return _PCSKVA_104; } set { if (_PCSKVA_104 != value) { _PCSKVA_104 = value; CIEC104Slave.ReturnSoleYCData(); } } }
        private  double _PCSKVA_104;

        public  double SOC_104 { get { return _SOC_104; } set { if (_SOC_104 != value) { _SOC_104 = value; CIEC104Slave.ReturnSoleYCData(); } } }
        private  double _SOC_104;
        public  double ChargeAmount_104 { get { return _ChargeAmount_104; } set { if (_ChargeAmount_104 != value) { _ChargeAmount_104 = value; CIEC104Slave.ReturnSoleYCData(); } } }
        private  double _ChargeAmount_104;


        public  double DisChargeAmount_104 { get { return _DisChargeAmount_104; } set { if (_DisChargeAmount_104 != value) { _DisChargeAmount_104 = value; CIEC104Slave.ReturnSoleYCData(); } } }
        private static double _DisChargeAmount_104;*/

        


        public  void IEC104_Init()
        {
            app.Isconnect = false;//顺序标识位：优先响应从站的指令，回复结束后，可以进行变化上送
            app.apci.start = 100;
            app.YC_rawdata = new float[100];
            app.YC_perv_rawdata = new float[100];
            app.YX_rawdata = new bool[25];
            app.YX_perv_rawdata = new bool[25];
            app.asdu.commom_asdu_1 = 0xFF;
            app.asdu.commom_asdu_2 = 0xFF;

        }

        /********************总召唤全部流程*******************************/
        public static void NAIec104InterrogationAll(byte[] TX_bytes, byte[] RX_bytes)
        {
            //传入参数： TX_bytes：从站序号  RX_bytes：主站序号
            //更新主站

            Build_R_num(RX_bytes);
            InterrogationConfirm(TX_bytes, RX_bytes); //发送帧的镜像，除传送原因不同

            Build_T_num(TX_bytes);
            ReturnAllYCData(TX_bytes, RX_bytes);

            Build_T_num(TX_bytes);
            ReturnAllYXData(TX_bytes, RX_bytes);

            Build_T_num(TX_bytes);
            InterrogationComplete(TX_bytes, RX_bytes);
        }



        /*****************总召唤确认*************************/
        static public void InterrogationConfirm(byte[] TX_bytes, byte[] RX_bytes)
        {
            byte[] message = new byte[16];

            //byte[] 
            // message=new byte[100];
            message[0] = 0x68;
            message[1] = 0x0E;
            //发送序号
            message[2] = TX_bytes[0];
            message[3] = TX_bytes[1];
            //接收序号
            message[4] = RX_bytes[0];
            message[5] = RX_bytes[1];
            message[6] = 0x64;
            message[7] = 0x01;
            message[8] = 0x07;
            message[9] = 0x00;
            message[10] = 0x01;
            message[11] = 0x00;
            message[12] = 0x00;
            message[13] = 0x00;
            message[14] = 0x00;
            message[15] = 0x14;

            //验证消息
            //string hexString = BitConverter.ToString(message);

            log.Warn("");
            frmMain.Selffrm.TCPserver.SendMsg_byte(message); log.Warn(" OK ");
            

            Record_Order(TX_bytes[0], TX_bytes[1]);


            //return message;
        }

        /*****************总召唤结束*************************/
        static public byte[] InterrogationComplete(byte[] TX_bytes, byte[] RX_bytes)
        {
            byte[] message = new byte[16];

            //byte[] 
            // message=new byte[100];
            message[0] = 0x68;
            message[1] = 0x0E;
            //发送序号
            message[2] = TX_bytes[0];
            message[3] = TX_bytes[1];
            //接收序号
            message[4] = RX_bytes[0];
            message[5] = RX_bytes[1];
            //类型标识
            message[6] = 0x64;
            //可变结构限定词
            message[7] = 0x01;
            //传输原因
            message[8] = 0x0A;
            message[9] = 0x00;
            //公共地址
            message[10] = 0x01;
            message[11] = 0x00;
            //信息体地址
            message[12] = 0x00;
            message[13] = 0x00;
            message[14] = 0x00;
            //限定词
            message[15] = 0x20;

            //验证消息
            string hexString = BitConverter.ToString(message);
            //log.Debug("发送总召唤结束：" + hexString);

            log.Warn("  ");
            frmMain.Selffrm.TCPserver.SendMsg_byte(message); log.Warn(" OK ");

            Record_Order(TX_bytes[0], TX_bytes[1]);
            return message;


        }
        /*        unsafe public int build_S_Msg()
                {
                    APCI header;
                    header.start  = 0x68;
                    header.len    = 0x04;
                    header.field1 = 0x01;                        // S-Format
                    header.field2 = 0x00;
                    header.field3 =  (byte)(RxCounter & 0xFE);
                    header.field4 =  (byte)((RxCounter>>8) & 0xFF);

                    return 0;
                }*/

        /*****************************************************/
        //S帧 : 记录接收到的长帧，双方可以按频率发送，比如接收8帧I帧回答一帧S帧，也可以要求接收1帧I帧就应答1帧S帧。                                    
        /*****************************************************/
        static public byte[] build_S_Msg()
        {
            byte[] message = new byte[6];

            message[0] = 0x68;
            message[1] = 0x04;
            message[2] = 0x01;
            message[3] = 0x00;
            message[4] = 0x02;
            message[5] = 0x00;

            frmMain.Selffrm.TCPserver.SendMsg_byte(message); log.Warn(" OK ");
            return message;

        }

        /*****************************************************/
        //U帧                                
        /*****************************************************/
        static public void Send_U_Msg(byte cmd)
        {
            byte[] message = new byte[6];

            message[0] = 0x68;
            message[1] = 0x04;
            message[2] = (byte)(0x03 | cmd);
            message[3] = 0x00;
            message[4] = 0x00;
            message[5] = 0x00;

            //验证消息
            string hexString = BitConverter.ToString(message);
            //log.Debug("U帧："+ hexString);

            frmMain.Selffrm.TCPserver.SendMsg_byte(message); log.Warn(" OK ");

            app.Isconnect = true;
        }
        /**************************获取发送序号*******************/
        public static byte[] Get_S_num(byte[] TX_bytes, byte[] msg)
        {
            //Array.Reverse(bytes);
            Array.Copy(msg, 2, TX_bytes, 0, 2);
            return TX_bytes;

        }
        /**************************获取接收序号*******************/
        public static byte[] Get_R_num(byte[] RX_bytes, byte[] msg)
        {
            Array.Copy(msg, 4, RX_bytes, 0, 2);
            return RX_bytes;
        }

        /**************************生成发送序号和接收序号*******************/
        public static void Build_R_num(byte[] bytes)
        {
            //序号递增+1
            int num = 0;
            num = ((Convert.ToInt32(bytes[0]) + Convert.ToInt32(bytes[1]) * 16 * 16) / 2 + 1) * 2;//接收序号 和 发送序号 最后一位都是默认0 所以值都左移1位
            Array.Copy(BitConverter.GetBytes(num), 0, bytes, 0, 2);
            
            //保存接收序列号，在主站主动发送时，此序列号+1 作为接收序列号
            app.apci.RX_field3 = bytes[0];
            app.apci.RX_field4 = bytes[1];
        }
        public static void Build_T_num(byte[] bytes)
        {
            //序号递增+1
            int num = 0;
            num = ((Convert.ToInt32(bytes[0]) + Convert.ToInt32(bytes[1]) * 16 * 16) / 2 + 1) * 2;
            Array.Copy(BitConverter.GetBytes(num), 0, bytes, 0, 2);


        }
        /******************************************************************/
        /*                          解析I帧                               */
        /******************************************************************/
        public static void ProcessFormatI(byte[] msg)
        {
            //获取主站发送报文中的发送序号和接收序号
            byte[] TX_bytes = new byte[2];    //主站序号（国网调度中心）
            byte[] RX_bytes = new byte[2];    //从站信号（EMS）

            TX_bytes = Get_S_num(TX_bytes, msg);
            RX_bytes = Get_R_num(RX_bytes, msg);

            switch (msg[6])
            {
                //单点遥信
                case 1:
                    /*传输原因*/
                    if (msg[8] == 5 && msg[9] == 0)
                    {
                        //更新主站的序号
                        Build_T_num(TX_bytes);
                        ReturnAllYXData(RX_bytes, TX_bytes);
                    }
                    break;
                //短浮点数遥测
                case 13:
                    /*传输原因*/
                    if (msg[8] == 5 && msg[9] == 0) //(遥信被请求，遥测被请求)
                    {
                        //更新主站的序号
                        Build_T_num(TX_bytes);
                        ReturnAllYCData(RX_bytes, TX_bytes);
                    }
                    break;
                //总召唤
                case 0x64:
                    //log.Debug("接收总召唤");
                    NAIec104InterrogationAll(RX_bytes, TX_bytes);
                    break;
                //单命令遥控
                case 0x2D:
                    //if (frmSet.Listen104 == 1)
                    {
                        //接收遥控预置
                        //int YKnum = Get_YKD_Num(msg, true);
                        ////log.Debug("YKnum:" + YKnum);
                        //if (msg[8] == 6 && msg[9] == 0 && isYKACK[YKnum] == 0)
                        //{
                        //    //遥控返校
                        //    //log.Debug("接收遥控预置");
                        //    Build_T_num(TX_bytes);
                        //    NAIec104YKACK(msg, RX_bytes, TX_bytes);
                        //}
                        //接收遥控执行
                         if (msg[8] == 6 && msg[9] == 0)
                        {
                            //执行确认
                            //log.Debug("接收遥控执行确认");
                            Build_T_num(TX_bytes);
                            NAIec104YKEXEACK(msg, RX_bytes, TX_bytes);
                            //激活结束
                            Build_R_num(RX_bytes);
                            NAIec104YKFinishACK(msg, RX_bytes, TX_bytes);
                        }
                        //遥控撤销
                        else if (msg[8] == 8 && msg[9] == 0)
                        {
                            //撤销确认
                            //log.Debug("接收遥控撤销确认");
                            Build_T_num(TX_bytes);
                            NAIec104YKDeactACK(msg, RX_bytes, TX_bytes);
                            //激活结束
                            Build_R_num(RX_bytes);
                            NAIec104YKFinishACK(msg, RX_bytes, TX_bytes);
                        }
                    }
                    break;
                //遥调(设定浮点数值命令)
                case 50:
                    int YDnum = Get_YKD_Num(msg, false);
                    //log.Debug("YDnum:" + YDnum);
                    //接收遥调预置
                    //if (msg[8] == 6 && msg[9] == 0 && isYDACK[YDnum] == 0)
                    //{
                    //    //遥调返校
                    //    //log.Debug("接收遥调预置");
                    //    Build_T_num(TX_bytes);
                    //    NAIec104YDACK(msg, RX_bytes, TX_bytes);
                    //}
                    //接收遥调执行
                    if (msg[8] == 6 && msg[9] == 0)
                    {
                        //执行确认
                        //log.Debug("接收遥调执行确认");
                        Build_T_num(TX_bytes);
                        NAIec104YDEXEACK(msg, RX_bytes, TX_bytes);
                        //激活结束
                        Build_R_num(RX_bytes);
                        NAIec104YDFinishACK(msg, RX_bytes, TX_bytes);
                    }
                    //遥调撤销
                    else if (msg[8] == 8 && msg[9] == 0)
                    {
                        //撤销确认
                        //log.Debug("接收遥调撤销确认");
                        Build_T_num(TX_bytes);
                        NAIec104YDDeactACK(msg, RX_bytes, TX_bytes);
                        //激活结束
                        Build_R_num(RX_bytes);
                        NAIec104YDFinishACK(msg, RX_bytes, TX_bytes);
                    }
                    break;
            }


        }




        /******************************************************************/
        /*                          解析U帧                               */
        /******************************************************************/
        static public void ProcessFormatU(byte[] msg)
        {
            BaseCommand baseCommand = new BaseCommand();

            if (msg[2] == 0x07)  // U启动
            {
                Send_U_Msg(baseCommand.CMD_STARTC);
            }
            else if (msg[2] == 0x13) // U停止
            {
                Send_U_Msg(baseCommand.CMD_STOPC);
            }
            else if (msg[2] == 0x43) // U测试
            {
                Send_U_Msg(baseCommand.CMD_TESTC);
            }
        }

        /******************************************************************/
        /*                      获取遥测数据                              */
        /******************************************************************/
        public static bool Get_One_YC_Data(float data, List<byte> messageList)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                byte[] bytes = BitConverter.GetBytes(data);
                foreach (var item in bytes)
                {
                    sb.Insert(0, item.ToString("X2"));
                }
                string dataString = sb.ToString();  //将 sb 中的十六进制字符串转换为 byteArray 字节数组

                byte[] byteArray = new byte[dataString.Length / 2];
                for (int i = 0; i < dataString.Length; i += 2)
                {
                    byteArray[i / 2] = Convert.ToByte(dataString.Substring(i, 2), 16);
                }

                //  按大端序顺序添加字节（高字节到低字节）
                messageList.Add(byteArray[3]); // 最高字节
                messageList.Add(byteArray[2]);
                messageList.Add(byteArray[1]);
                messageList.Add(byteArray[0]); // 最低字节

                // 添加品质描述符（固定为0x01，可修改）
                messageList.Add((byte)0x00);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"数据转换错误：{ex.Message}");
                return false;
            }
        }

        /******************************************************************/
        /*                          遥测数据                              */
        /******************************************************************/

        /// <summary>
        /// 将浮点数转换为 IEC104 遥测数据格式（4字节浮点值 + 1字节品质描述符），并添加到动态列表中
        /// </summary>
        /// <param name="data">待转换的浮点数</param>
        /// <param name="messageList">动态数据列表（用于安全添加数据）</param>
        /// <returns>是否成功添加数据</returns>


        static public void ReturnAllYCData(byte[] TX_bytes, byte[] RX_bytes)
        {
            // 获取设备信息（保留原始逻辑）
            frmMain.Selffrm.AllEquipment.BMS.Get104Info();

            // 使用动态列表管理数据，避免固定数组越界
            List<byte> messageList = new List<byte>();
            int dataCount = 0; // 记录数据项个数
            float pcsRun = 0;

            // ----------------------- 构造消息头 -----------------------
            messageList.AddRange(new byte[] { 0x68, 0x00 }); // 长度字段暂填0x00，后续计算
            messageList.AddRange(new byte[] { TX_bytes[0], TX_bytes[1] });         // 发送序号（2字节）
            messageList.AddRange(new byte[] { RX_bytes[0], RX_bytes[1] });         // 接收序号（2字节）
            messageList.AddRange(new byte[] { 0x0D, 0x00 }); // 类型标识（短浮点）+ 可变结构限定词（0x00表示连续数据）
            messageList.AddRange(new byte[] { 0x14, 0x00 }); // 传输原因（响应总召唤）
            messageList.AddRange(new byte[] { 0x01, 0x00 }); // 公共地址（装置地址）
            messageList.AddRange(new byte[] { 0x01, 0x40, 0x00 }); // 信息体地址（0x4001）

            // ----------------------- 安全添加遥测数据 -----------------------
            Action<float> safeAddData = value =>
            {
                if (Get_One_YC_Data(value, messageList))
                    dataCount++;
            };

            // ----------------------- 添加遥控返回点 -----------------------
            safeAddData((float)frmMain.Selffrm.AllEquipment.PCSScheduleKVA);    // 有功功率设置

            // ----------------------- 添加 PCS 相关数据 -----------------------
            if (frmMain.Selffrm.AllEquipment.PCSList.Count > 0)
            {
                var pcs = frmMain.Selffrm.AllEquipment.PCSList[0];

                // ----------------------- 计算 PCS 运行状态 -----------------------
                if (pcs.PcsRun == 255)
                    pcsRun = 0;
                else if (frmMain.Selffrm.AllEquipment.wTypeActive == "放电")
                    pcsRun = 2;
                else if (frmMain.Selffrm.AllEquipment.wTypeActive == "充电")
                    pcsRun = 1;

                safeAddData((float)pcs.aA);          // A电流
                safeAddData((float)pcs.bA);          // B电流
                safeAddData((float)pcs.cA);          // C电流
                safeAddData((float)pcs.aV);         // a对地电压
                safeAddData((float)pcs.bV);         // b对地电压
                safeAddData((float)pcs.cV);         // c对地电压

                if (frmSet.config.SysCount == 1)
                    safeAddData((float)-pcs.allUkva);     // 总有用功率
                else
                    safeAddData((float)-frmMain.Selffrm.AllEquipment.AllwaValue);

                safeAddData((float)pcs.allNUkvar);    // 总无功功率
                safeAddData((float)pcs.allPFactor);  // 总功率因数
                safeAddData(pcsRun);    // PCS充放电状态
                safeAddData(100f);    // 最大充电功率允许值
                safeAddData(100f);    // 最大放电功率允许值
            }
            else 
            {

                safeAddData(0);       // A电流
                safeAddData(0);       // B电流
                safeAddData(0);       // C电流
                safeAddData(0);       // a对地电压
                safeAddData(0);       // b对地电压
                safeAddData(0);       // c对地电压
                safeAddData(0);       // 总有用功率
                safeAddData(0);       // 总无功功率
                safeAddData(0);       // 总功率因数
                safeAddData(0);       // PCS充放电状态
                safeAddData(100f);    // 最大充电功率允许值
                safeAddData(100f);    // 最大放电功率允许值
            }

            // ----------------------- 添加 BMS 相关数据 -----------------------
            if (frmMain.Selffrm.AllEquipment.BMS != null)
            {
                safeAddData(frmMain.Selffrm.AllEquipment.BMS.ChargeAmount);      // 可充电量
                safeAddData(frmMain.Selffrm.AllEquipment.BMS.DisChargeAmount);   // 可放电量
                safeAddData((float)frmMain.Selffrm.AllEquipment.E2PKWH[0]);             // 当日充电电量
                safeAddData((float)frmMain.Selffrm.AllEquipment.E2OKWH[0]);             // 当日放电电量
                safeAddData((float)frmMain.Selffrm.AllEquipment.Elemeter2.PUkwh[0]);    // 累计充电电量
                safeAddData((float)frmMain.Selffrm.AllEquipment.Elemeter2.OUkwh[0]);    // 累计放电电量
                safeAddData((float)frmMain.Selffrm.AllEquipment.BMS.soc);    // SOC
                safeAddData((float)frmMain.Selffrm.AllEquipment.BMS.soh);    // SOH
                safeAddData((float)frmMain.Selffrm.AllEquipment.BMS.averageTemp);    // 电池温度
            }
            else
            {
                safeAddData(0);      // 可充电量
                safeAddData(0);   // 可放电量
                safeAddData(0);             // 当日充电电量
                safeAddData(0);             // 当日放电电量
                safeAddData(0);    // 累计充电电量
                safeAddData(0);    // 累计放电电量
                safeAddData(0);    // SOC
                safeAddData(0);    // SOH
                safeAddData(0);    // 电池温度
            }

            // ----------------------- 添加关口电表数据 -----------------------
            if (frmMain.Selffrm.AllEquipment.Elemeter1List.Count > 0)
            {
                var elemeter = frmMain.Selffrm.AllEquipment.Elemeter1List[0];
                safeAddData((float)elemeter.AllUkva);    // 关口电表_总有功功率
            }
            else
            {
                safeAddData(0); // 关口电表_总有功功率
            }

            // ----------------------- 添加汇流柜电表数据 -----------------------
            if (frmMain.Selffrm.AllEquipment.Elemeter2H != null)
            {
                safeAddData((float)frmMain.Selffrm.AllEquipment.Elemeter2H.AllUkva);    // 并网点电表_总有功功率
                safeAddData((float)frmMain.Selffrm.AllEquipment.Elemeter2H.HZ);         // 并网点电表_电网频率
                safeAddData((float)frmMain.Selffrm.AllEquipment.Elemeter2H.PUkwh[0]);    // 并网点电表_正向有功电能
                safeAddData((float)frmMain.Selffrm.AllEquipment.Elemeter2H.PUkwh[1]);   // 并网点电表_正向有功尖电能
                safeAddData((float)frmMain.Selffrm.AllEquipment.Elemeter2H.PUkwh[2]);   // 并网点电表_正向有功峰电能
                safeAddData((float)frmMain.Selffrm.AllEquipment.Elemeter2H.PUkwh[3]);   // 并网点电表_正向有功平电能
                safeAddData((float)frmMain.Selffrm.AllEquipment.Elemeter2H.PUkwh[4]);   // 并网点电表_正向有功谷电能
                safeAddData((float)frmMain.Selffrm.AllEquipment.Elemeter2H.OUkwh[0]);   // 并网点电表_反向有功电能
                safeAddData((float)frmMain.Selffrm.AllEquipment.Elemeter2H.OUkwh[1]);   // 并网点电表_反向有功尖电能
                safeAddData((float)frmMain.Selffrm.AllEquipment.Elemeter2H.OUkwh[2]);   // 并网点电表_反向有功峰电能
                safeAddData((float)frmMain.Selffrm.AllEquipment.Elemeter2H.OUkwh[3]);   // 并网点电表_反向有功平电能
                safeAddData((float)frmMain.Selffrm.AllEquipment.Elemeter2H.OUkwh[4]);   // 并网点电表_反向有功谷电能
            }
            else
            {
                safeAddData(0);
                safeAddData(0);
                safeAddData(0);
                safeAddData(0);
                safeAddData(0);
                safeAddData(0);
                safeAddData(0);
                safeAddData(0);
                safeAddData(0);
                safeAddData(0);
                safeAddData(0);
                safeAddData(0);
            }

            // ----------------------- 数据修正（填充长度和数据项个数）-----------------------
            if (messageList.Count >= 2)
            {
                messageList[1] = (byte)(messageList.Count - 2); // 消息长度 = 总字节数 - 2（去掉起始符和长度字段本身）
            }

            if (messageList.Count >= 8)
            {
                messageList[7] = (byte)(dataCount | 0x80); // 最高位为1表示后续有更多数据，0表示单帧（根据协议调整）
            }

            // ----------------------- 发送数据 -----------------------
            try
            {
                byte[] message = messageList.ToArray();
                frmMain.Selffrm.TCPserver.SendMsg_byte(message);
                Record_Order(TX_bytes[0], TX_bytes[1]); // 记录序号
            }
            catch (Exception ex)
            {
                // 记录发送错误日志
                Console.WriteLine($"发送 IEC104 数据失败：{ex.Message}");
            }
        }


        static public bool Get_Rawdata(float data, ref float[] rawdata, ref int count)
        {
            rawdata[count] = data;
            count += 1;
            return true;
        }
        static public bool Get_Rawdata(bool data, ref bool[] rawdata, ref int count)
        {
            rawdata[count] = data;
            count += 1;
            return true;
        }

        /******************************************************************/
        /*                          遥信数据                              */
        /******************************************************************/

        public static byte[] ReturnAllYXData(byte[] TX_bytes, byte[] RX_bytes)
        {

            int Index = 0;
            int data_count = 0;  //记录数据个数
            byte[] message = new byte[200];


            // 添加固定部分
            message[Index++] = 0x68;
            message[Index++] = 0x00;  //APUD长度，后续调整

            // 发送序号
            message[Index++] = TX_bytes[0];
            message[Index++] = TX_bytes[1];
            //接收序号
            message[Index++] = RX_bytes[0];
            message[Index++] = RX_bytes[1];

            //类型标示
            message[Index++] = 0x01;   //单点遥信（带品质描述）

            //可变限结构限定词
            message[Index++] = 0x00;   //后续调整

            //传输原因 
            message[Index++] = 0x14;  //响应总召唤
            message[Index++] = 0x00;

            //公共地址：装置地址
            message[Index++] = 0x01;
            message[Index++] = 0x00;

            //信息体地址
            message[Index++] = 0x01;
            message[Index++] = 0x00;
            message[Index++] = 0x00;

            //信息元素(储能表数据)

            //1 储能需求侧相应模式投入 ( 1:进入网控 0：未进入)
            if (frmMain.Selffrm.AllEquipment.eState == 2)
                message[Index++] = 0x01;
            else
                message[Index++] = 0x00;
            data_count++;

            //2 储能事故总信号  : ( 1:故障 0：正常 )
            if (frmMain.Selffrm.AllEquipment.ErrorState[2] == true)
                message[Index++] = 0x01;
            else
                message[Index++] = 0x00;
            data_count++;

            //3 运行状态 ： （0正常运行，1故障）
            if (frmMain.Selffrm.AllEquipment.runState == 1)
                message[Index++] = 0x00;
            else
                message[Index++] = 0x01;
            data_count++;

            //4 BMS通信 ： （ 1：通信 0：失联 ）
            if (frmMain.Selffrm.AllEquipment.BMS.Prepared == true)
                message[Index++] = 0x01;
            else
                message[Index++] = 0x00;
            data_count++;

            //5 PCS开关状态  0:停机 1：开机
            if (frmMain.Selffrm.AllEquipment.PCSList[0].Prepared == true)
                message[Index++] = 0x01;
            else
                message[Index++] = 0x00;
            data_count++;

            //数据修正          
            message[1] = (byte)(Index - 2);
            message[7] = (byte)(data_count | 0x80);
            Array.Resize(ref message, Index);

            frmMain.Selffrm.TCPserver.SendMsg_byte(message);

            Record_Order(TX_bytes[0], TX_bytes[1]);
            return message;
        }


        /*********************获取遥调地址******************************/
        public static void Get_YD_Addr(byte[] msg)
        {
            //do something
            //switch(msg[])

        }


        /*********************遥调返校******************************/
        public static void NAIec104YDACK(byte[] msg, byte[] TX_bytes, byte[] RX_bytes)
        {
            //发送序号
            msg[2] = TX_bytes[0];
            msg[3] = TX_bytes[1];
            //接收序号
            msg[4] = RX_bytes[0];
            msg[5] = RX_bytes[1];
            //传输原因
            msg[8] = 0x07;

            int num = Get_YKD_Num(msg, false);
            isYDACK[num] = 1;

            string hexString = BitConverter.ToString(msg);
            //log.Debug("发送遥调返校：" + hexString);

            //send msg
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg); log.Warn(" OK ");

            Record_Order(TX_bytes[0], TX_bytes[1]);
        }

        /**********************遥调获取参数值********************/
        public static float Get_YD_Input(byte[] msg)
        {

            byte[] bytes = new byte[4];
            Array.Copy(msg, 15, bytes, 0, 4);
            Array.Reverse(bytes);


            string hexStr = BitConverter.ToString(bytes).Replace("-", ""); ;
            //log.Debug("遥调：" + hexStr );

            if (hexStr.Length != 8)
            {
                //log.Debug(false); ;
            }
            byte[] byteArray = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                byteArray[i] = Convert.ToByte(hexStr.Substring((3 - i) * 2, 2), 16);
            }
            float floatValue = BitConverter.ToSingle(byteArray, 0);
            //log.Debug("Input:" + floatValue);

            return floatValue;
        }



        /**********************(单点)遥调执行确认********************/
        //参数设置是4个字节
        public static void NAIec104YDEXEACK(byte[] msg, byte[] TX_bytes, byte[] RX_bytes)
        {

            //发送序号
            msg[2] = TX_bytes[0];
            msg[3] = TX_bytes[1];
            //接收序号
            msg[4] = RX_bytes[0];
            msg[5] = RX_bytes[1];
            //传输原因
            msg[8] = 0x07;

            int num = Get_YKD_Num(msg, false);
            //do something
            switch (num)
            {
                //设置PCS功率值
                //写入PCS的功率 ： 充电为正 放电为负
                
                case 0:
                    float input = Get_YD_Input(msg);
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        frmMain.Selffrm.AllEquipment.PCSScheduleKVA = (input / frmSet.config.SysCount);
                        if(input == 0)
                        {
                            frmMain.Selffrm.AllEquipment.ExcPCSPowerOff();
                        }
                        else if (input > 0)
                        {
                            frmMain.Selffrm.AllEquipment.wTypeActive = "充电";
                            frmMain.Selffrm.AllEquipment.PCSTypeActive = "恒功率";
                        }
                        else 
                        {
                            frmMain.Selffrm.AllEquipment.wTypeActive = "放电";
                            frmMain.Selffrm.AllEquipment.PCSTypeActive = "恒功率";
                        }
                        log.Warn(" *****************  功率下发   *****************");
                        log.Warn($"计划功率值  {input} ----策略预备执行动作 -{frmMain.Selffrm.AllEquipment.wTypeActive}--{frmMain.Selffrm.AllEquipment.PCSTypeActive}");
                        log.Warn(" *****************  功率下发   *****************");
                    }
                    //log.Debug("写入功率值：" + input + "写入PCSScheduleKVA" + frmMain.Selffrm.AllEquipment.PCSScheduleKVA);
                    break;
/*                //储能需求侧响应模式投入
                case 1:
                    if (msg[15] == 0x00)
                    {
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            frmMain.Selffrm.AllEquipment.eState = 1; ///手工
                            frmSet.config.SysMode = 1;
                            frmMain.TacticsList.TacticsOn = true; //恢复策略模式
                            frmMain.TacticsList.ActiveIndex = -2;
                        }
                    }
                    else
                    {
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            frmMain.Selffrm.AllEquipment.eState = 2; //进入网控模式
                            frmSet.config.SysMode = 2;
                            frmMain.TacticsList.TacticsOn = false;   //关闭策略

                            //初始化设置
                            frmMain.Selffrm.AllEquipment.PCSScheduleKVA = 0;
                            frmMain.Selffrm.AllEquipment.HostStart = false;
                            frmMain.Selffrm.AllEquipment.SlaveStart = false;
                            frmMain.Selffrm.Slave104.HostStart_104 = false;
                        }
                    }
                    break;*/

            }
            //send msg

            string hexString = BitConverter.ToString(msg);
            //log.Debug("发送遥调执行确认：" + hexString);

            frmMain.Selffrm.TCPserver.SendMsg_byte(msg); log.Warn(" OK ");

            Record_Order(TX_bytes[0], TX_bytes[1]);
            isYDACK[num] = 0;

        }

        /*********************遥调撤销确认******************************/
        public static void NAIec104YDDeactACK(byte[] msg, byte[] TX_bytes, byte[] RX_bytes)
        {
            //发送序号
            msg[2] = TX_bytes[0];
            msg[3] = TX_bytes[1];
            //接收序号
            msg[4] = RX_bytes[0];
            msg[5] = RX_bytes[1];
            //传输原因
            msg[8] = 0x09;

            int num = Get_YKD_Num(msg, false);
            isYDACK[num] = 0;

            //send msg
            string hexString = BitConverter.ToString(msg);
            //log.Debug("发送遥调撤销确认：" + hexString);
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg); log.Warn(" OK ");
            Record_Order(TX_bytes[0], TX_bytes[1]);
        }


        /*********************遥控撤销确认******************************/
        public static void NAIec104YKDeactACK(byte[] msg, byte[] TX_bytes, byte[] RX_bytes)
        {
            //发送序号
            msg[2] = TX_bytes[0];
            msg[3] = TX_bytes[1];
            //接收序号
            msg[4] = RX_bytes[0];
            msg[5] = RX_bytes[1];
            //传输原因
            msg[8] = 0x09;

            int num = Get_YKD_Num(msg, true);
            isYKACK[num] = 0;

            //send msg
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg); log.Warn(" OK ");
            Record_Order(TX_bytes[0], TX_bytes[1]);
        }

        /*********************遥控激活结束******************************/
        public static void NAIec104YKFinishACK(byte[] msg, byte[] TX_bytes, byte[] RX_bytes)
        {
            //传输原因
            msg[8] = 0x0a;

            //发送序号
            msg[2] = TX_bytes[0];
            msg[3] = TX_bytes[1];
            //接收序号
            msg[4] = RX_bytes[0];
            msg[5] = RX_bytes[1];

            //send msg
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg); log.Warn(" OK ");
            Record_Order(TX_bytes[0], TX_bytes[1]);
        }

        /*********************遥调激活结束******************************/
        public static void NAIec104YDFinishACK(byte[] msg, byte[] TX_bytes, byte[] RX_bytes)
        {
            //传输原因
            msg[8] = 0x0a;

            //发送序号
            msg[2] = TX_bytes[0];
            msg[3] = TX_bytes[1];
            //接收序号
            msg[4] = RX_bytes[0];
            msg[5] = RX_bytes[1];

            //send msg
            string hexString = BitConverter.ToString(msg);
            //log.Debug("发送遥调激活结束：" + hexString);

            frmMain.Selffrm.TCPserver.SendMsg_byte(msg); log.Warn(" OK ");
            Record_Order(TX_bytes[0], TX_bytes[1]);
        }


        /**********************遥控返校***************************/
        public static void NAIec104YKACK(byte[] msg, byte[] TX_bytes, byte[] RX_bytes)
        {
            //发送序号
            msg[2] = TX_bytes[0];
            msg[3] = TX_bytes[1];
            //接收序号
            msg[4] = RX_bytes[0];
            msg[5] = RX_bytes[1];
            //传输原因
            msg[8] = 0x07;

            //获取遥控号
            int num = Get_YKD_Num(msg, true);


            //81: 遥控选择命令 开关合
            //80：遥控选择命令 开关分 
            if ((msg[15] == 0x81) || (msg[15] == 0x80))
            {
                isYKACK[num] = 1;
            }

            string hexString = BitConverter.ToString(msg);
            //log.Debug("发送遥控返校：" + hexString);

            //send msg
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg); log.Warn(" OK ");
        }


        /**********************(单点)遥控执行确认********************/
        public static void NAIec104YKEXEACK(byte[] msg, byte[] TX_bytes, byte[] RX_bytes)
        {

            //发送序号
            msg[2] = TX_bytes[0];
            msg[3] = TX_bytes[1];
            //接收序号
            msg[4] = RX_bytes[0];
            msg[5] = RX_bytes[1];
            //传输原因
            msg[8] = 0x07;

            int num = Get_YKD_Num(msg, true);
            //do something
            switch (num)
            {
                //进入网控模式
                case 0:
                    if (msg[15] == 0x00)   //关闭
                    {
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            frmMain.Selffrm.AllEquipment.eState = 1;
                            frmSet.config.SysMode = 1;
                            frmMain.TacticsList.TacticsOn = true; //恢复策略模式
                            frmMain.TacticsList.ActiveIndex = -2;

                            frmMain.Selffrm.AllEquipment.PCSScheduleKVA = 0;
                            frmMain.Selffrm.AllEquipment.HostStart = false;
                            frmMain.Selffrm.AllEquipment.SlaveStart = false;
                            frmMain.Selffrm.Slave104.HostStart_104 = false;

                            frmMain.Selffrm.AllEquipment.ExcPCSPowerOff();
                        }

                        //记录远动连接标志位
                        frmSet.historyDatas.YDstatus = 0;
                        frmSet.Set_HistoryData();
                    }
                    else  //开启
                    {
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            frmMain.Selffrm.AllEquipment.eState = 2; //进入网控模式
                            frmSet.config.SysMode = 2;
                            frmMain.TacticsList.TacticsOn = false;   //关闭策略

                            //初始化设置
                            frmMain.Selffrm.AllEquipment.PCSScheduleKVA = 0;
                            frmMain.Selffrm.AllEquipment.HostStart = true;
                            frmMain.Selffrm.AllEquipment.SlaveStart = true;
                            frmMain.Selffrm.Slave104.HostStart_104 = true;
                        }

                        //记录远动连接标志位
                        frmSet.historyDatas.YDstatus = 1;
                        frmSet.Set_HistoryData();
                    }
                    break;

            }
            //send msg
            string hexString = BitConverter.ToString(msg);
            //log.Debug("发送遥控执行确认：" + hexString);
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg); log.Warn(" OK ");
            Record_Order(TX_bytes[0], TX_bytes[1]);
            isYKACK[num] = 0;
            //log.Debug("eState:" + frmMain.Selffrm.AllEquipment.eState);
            //log.Debug("HostStart:"+ frmMain.Selffrm.AllEquipment.HostStart);

        }

        /**********************获取遥控号/遥调地址********************/
        //isYGK ： ture（遥控） false（遥调）
        public static int Get_YKD_Num(byte[] msg, bool isYGK)
        {
            int num;

            byte[] bytes = new byte[5];
            byte[] YKbytes = { 0x60, 0x01 };
            byte[] YDbytes = { 0x62, 0x01 };
            Array.Copy(msg, 12, bytes, 0, 3);
            Array.Reverse(bytes);
            if (isYGK)
            {
                num = Convert.ToInt32(BitConverter.ToString(bytes).Replace("-", ""), 16) - Convert.ToInt32(BitConverter.ToString(YKbytes).Replace("-", ""), 16); //获取遥控地址
            }
            else
                num = Convert.ToInt32(BitConverter.ToString(bytes).Replace("-", ""), 16) - Convert.ToInt32(BitConverter.ToString(YDbytes).Replace("-", ""), 16); //获取遥调地址

            return num;

        }
        public void iec104_packet_parser(byte[] data)
        {
            IEC104Send_Event.Reset();
            app.Isconnect = false;

            if ((data[2] & 0x03) == 0x03)
            {
                // u 帧
                //log.Debug("是U帧");
                ProcessFormatU(data);
                app.Isconnect = true;
                IEC104Send_Event.Set();


            }
            else if ((data[2] & 0x01) == 0x01)
            {
                //解决一包消息含多帧：找到I帧或U帧则裁剪出来，进行处理，对于S帧，直接丢弃
                if (data.Length > 6)
                {
                    byte[] _data = new byte[data.Length - 6];
                    for (int i = 0; i < data.Length / 6; i++)
                    {
                        if ((data[6 * i + 2] & 0x01) == 0x01)
                        {
                            Array.Copy(data, 6 * (i + 1), _data, 0, data.Length - 6 * (i + 1));
                        }
                        else { break; }
                    }

                    //首次找到 非S帧，进行裁切处理。丢弃此帧后的所有数据
                    if ((_data[2] & 0x01) != 0x01)
                    {
                        Console.WriteLine("************** S+I ***************** S+I **************** S+I *************");
                        Array.Resize(ref _data, _data[1] + 1);
                        ProcessFormatI(_data);
                    }
                }
                app.Isconnect = true;
                IEC104Send_Event.Set();

            }
            else
            {
                //log.Debug("是I帧");
                ProcessFormatI(data);
                app.Isconnect = true;
                IEC104Send_Event.Set();

            }
        }

        static public void ReturnSoleYXData(byte function)
        {
           // if (app.Isconnect == false) return;

            int Index = 0;
            int count = 0;
            int dif_count = 0;  //记录变化数据个数
            app.asdu.function = function;
            byte[] message = new byte[100];
            byte[] arr = new byte[10];

            //***********************拼装数据************************//
            message[Index++] = 0x68;
            message[Index++] = 0x00; //占位无用
            //发送序号
            message[Index++] = app.apci.TX_field1;
            message[Index++] = app.apci.TX_field2;
            //接收序号
            message[Index++] = app.apci.RX_field3;
            message[Index++] = app.apci.RX_field4;
            //类型标示
            message[Index++] = app.asdu.function;   //单点信息（遥信）
            //可变限结构限定词
            message[Index++] = 0x00;   //占位无用
            //message[7] = 0x01;
            //传输原因 
            message[Index++] = 0x03;   //突发
            message[Index++] = 0x00;
            //公共地址：装置地址
            message[Index++] = app.asdu.commom_asdu_1;
            message[Index++] = app.asdu.commom_asdu_2;


            //信息元素(储能表数据)

            //储能需求侧相应模式投入 ( 1:进入网控 0：未进入)
            if (frmMain.Selffrm.AllEquipment.eState == 2) arr[0] = 0x01;
            else arr[0] = 0x00;

            //储能事故总信号  : ( 1:故障 0：正常 )
            if (frmMain.Selffrm.AllEquipment.ErrorState[2] == true) arr[1] = 0x01;
            else arr[1] = 0x00;
            
            //运行状态 ： （0正常运行，1故障）
            if (frmMain.Selffrm.AllEquipment.runState == 1) arr[2] = 0x01;
            else if (frmMain.Selffrm.AllEquipment.runState == 0) arr[2] = 0x00;
            
            //BMS通信 ： （ 1：通信 0：失联 ）
            if (frmMain.Selffrm.AllEquipment.BMS.Prepared == true) arr[3] = 0x01;
            else arr[3] = 0x00;
           
            //PCS开关状态  0:停机 1：开机
            if (frmMain.Selffrm.AllEquipment.PCSList[0].Prepared == true) arr[4] = 0x01;
            else arr[4] = 0x00;

            Get_Rawdata(Convert.ToBoolean(arr[0]), ref app.YX_rawdata, ref count);        //储能需求侧响应模式投入
            Get_Rawdata(Convert.ToBoolean(arr[1]), ref app.YX_rawdata, ref count);        //储能事故总信号
            Get_Rawdata(Convert.ToBoolean(arr[2]), ref app.YX_rawdata, ref count);        //运行状态
            Get_Rawdata(Convert.ToBoolean(arr[3]), ref app.YX_rawdata, ref count);        //BMS通信
            Get_Rawdata(Convert.ToBoolean(arr[4]), ref app.YX_rawdata, ref count);        //PCS通信

            //通过对比当前遥信全量数据和过去遥信全量数据，捕获变化的遥信点位
            for (int i = 0; i < app.YX_rawdata.Length; i++)
            {
                if (app.YX_rawdata[i] != app.YX_perv_rawdata[i])
                {
                    // apdu.asdu.data[i] = message[i];
                    app.asdu.Object_Address_1 = ((i + 1) & 0xFF).ToString("X");
                    app.asdu.Object_Address_2 = (((i + 1) >> 8) & 0xFF).ToString("X");
                    app.asdu.Object_Address_3 = (((i + 1) >> 16) & 0xFF).ToString("X");
                    //信息体地址 0x4001
                    message[Index++] = Convert.ToByte(app.asdu.Object_Address_1, 16);
                    message[Index++] = Convert.ToByte(app.asdu.Object_Address_2, 16);
                    message[Index++] = Convert.ToByte(app.asdu.Object_Address_3, 16);

                    if (app.YX_rawdata[i] == true) message[Index++] = 0x01;
                    else message[Index++] = 0x00;
                    if (app.asdu.function == 0X1E)
                    {
                        //时标
                        int second = DateTime.Now.Millisecond + DateTime.Now.Second * 1000;
                        message[Index++] = (byte)second;
                        message[Index++] = (byte)(second >> 8);
                        message[Index++] = (byte)DateTime.Now.Minute;
                        message[Index++] = (byte)DateTime.Now.Hour;
                        message[Index++] = (byte)DateTime.Now.Day;
                        message[Index++] = (byte)DateTime.Now.Month;
                        message[Index++] = (byte)(DateTime.Now.Year - 2000);
                    }
                    dif_count++;
                }
            }

            //数据修正
            message[1] = (byte)(Index - 2);
            message[7] = (byte)(dif_count);

            //裁切：去掉末尾无含义的字符0
            Array.Resize(ref message, Index);


            if (dif_count == 0) return;
            //IEC104Send_Event.Wait();
            log.Warn(" 变换遥信  -- START ");
            if (frmMain.Selffrm.TCPserver.SendMsg_byte(message) == true)
            {
                log.Warn(" 变换遥信 -- OK ");
                Record_Order(app.apci.TX_field1, app.apci.TX_field2);
                Console.WriteLine($"变化遥tiao  ++   :");
                Console.WriteLine(string.Join("-", message));
            }
            else
            {
                log.Warn(" 变换遥信 -- ERROR ");
                app.apci.TX_field1 = 0;
                app.apci.TX_field2 = 0;
                app.apci.RX_field3 = 0;
                app.apci.RX_field4 = 0;
                app.Isconnect = false;
            }

        }

        static public void ReturnSoleYCData()
        {
            log.Warn("        &&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&& start");

            if (!app.Isconnect) return;

            float pcsRun = 0;
            int index = 0; // 统一使用小写驼峰命名
            int count = 0; // 记录数据次序
            int difCount = 0; // 记录变化数据个数
            var messageList = new List<byte>(); // 使用动态列表替代固定数组

            //*********************** 拼装数据头部 ************************//
            messageList.AddRange(new byte[] { 0x68, 0x00 }); // 起始符和长度占位
            messageList.AddRange(new[] { app.apci.TX_field1, app.apci.TX_field2 }); // 发送序号
            messageList.AddRange(new[] { app.apci.RX_field3, app.apci.RX_field4 }); // 接收序号
            messageList.AddRange(new byte[] { 0x0D, 0x00 }); // 类型标识和可变结构限定词
            messageList.AddRange(new byte[] { 0x03, 0x00 }); // 传输原因（突发）
            messageList.AddRange(new[] { app.asdu.commom_asdu_1, app.asdu.commom_asdu_2 }); // 公共地址

            // 获取原始数据存入 Rawdata (增加点位需要修改IEC104_Init)
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSScheduleKVA, ref app.YC_rawdata, ref count);    //有功功率设置

            // ----------------------- 添加 PCS 相关数据 -----------------------
            if (frmMain.Selffrm.AllEquipment.PCSList.Count > 0)
            {
                var pcs = frmMain.Selffrm.AllEquipment.PCSList[0];

                // ----------------------- 计算 PCS 运行状态 -----------------------
                if (pcs.PcsRun == 255)
                    pcsRun = 0;
                else if (frmMain.Selffrm.AllEquipment.wTypeActive == "放电")
                    pcsRun = 2;
                else if (frmMain.Selffrm.AllEquipment.wTypeActive == "充电")
                    pcsRun = 1;

                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSList[0].aA, ref app.YC_rawdata, ref count);          //A电流
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSList[0].bA, ref app.YC_rawdata, ref count);          //B电流
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSList[0].cA, ref app.YC_rawdata, ref count);          //C电流
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSList[0].aV, ref app.YC_rawdata, ref count);         //a对地电压
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSList[0].bV, ref app.YC_rawdata, ref count);         //b对地电压
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSList[0].cV, ref app.YC_rawdata, ref count);         //c对地电压
                if (frmSet.config.SysCount == 1)
                    Get_Rawdata(-(float)frmMain.Selffrm.AllEquipment.PCSList[0].allUkva, ref app.YC_rawdata, ref count);     //总有用功率
                else
                    Get_Rawdata(-(float)frmMain.Selffrm.AllEquipment.AllwaValue, ref app.YC_rawdata, ref count);
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSList[0].allNUkvar, ref app.YC_rawdata, ref count);    //总无功功率
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSList[0].allPFactor, ref app.YC_rawdata, ref count);  //总功率因数
                Get_Rawdata(pcsRun, ref app.YC_rawdata, ref count);                                                     //PCS运行状态
                Get_Rawdata((float)100, ref app.YC_rawdata, ref count);    //最大充电功率允许值
                Get_Rawdata((float)100, ref app.YC_rawdata, ref count);    //最大放电功率允许值
            }
            else 
            {
                Get_Rawdata(0, ref app.YC_rawdata, ref count);          //A电流
                Get_Rawdata(0, ref app.YC_rawdata, ref count);          //B电流
                Get_Rawdata(0, ref app.YC_rawdata, ref count);          //C电流
                Get_Rawdata(0, ref app.YC_rawdata, ref count);          //a对地电压
                Get_Rawdata(0, ref app.YC_rawdata, ref count);          //b对地电压
                Get_Rawdata(0, ref app.YC_rawdata, ref count);          //c对地电压
                Get_Rawdata(0, ref app.YC_rawdata, ref count);          //总有用功率
                Get_Rawdata(0, ref app.YC_rawdata, ref count);          //总无功功率
                Get_Rawdata(0, ref app.YC_rawdata, ref count);          //总功率因数
                Get_Rawdata(0, ref app.YC_rawdata, ref count);          //PCS运行状态
                Get_Rawdata((float)100, ref app.YC_rawdata, ref count);    //最大充电功率允许值
                Get_Rawdata((float)100, ref app.YC_rawdata, ref count);    //最大放电功率允许值
            }

            // ----------------------- 添加 BMS 相关数据 -----------------------
            if (frmMain.Selffrm.AllEquipment.BMS != null)
            {
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.BMS.ChargeAmount, ref app.YC_rawdata, ref count);      //可充电量
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.BMS.DisChargeAmount, ref app.YC_rawdata, ref count);   //可放电量
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.E2PKWH[0], ref app.YC_rawdata, ref count);             //当日充电电量            
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.E2OKWH[0], ref app.YC_rawdata, ref count);             //当日放电电量
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.Elemeter2.PUkwh[0], ref app.YC_rawdata, ref count);    //累计充电电量
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.Elemeter2.OUkwh[0], ref app.YC_rawdata, ref count);    //累计放电电量
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.BMS.soc, ref app.YC_rawdata, ref count);    //SOC
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.BMS.soh, ref app.YC_rawdata, ref count);    //SOH
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.BMS.averageTemp, ref app.YC_rawdata, ref count);    //Bms温度
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.BMS.averageTemp, ref app.YC_rawdata, ref count);    //Bms温度
            }
            else
            {
                Get_Rawdata(0, ref app.YC_rawdata, ref count);      //可充电量
                Get_Rawdata(0, ref app.YC_rawdata, ref count);   //可放电量
                Get_Rawdata(0, ref app.YC_rawdata, ref count);             //当日充电电量            
                Get_Rawdata(0, ref app.YC_rawdata, ref count);             //当日放电电量
                Get_Rawdata(0, ref app.YC_rawdata, ref count);    //累计充电电量
                Get_Rawdata(0, ref app.YC_rawdata, ref count);    //累计放电电量
                Get_Rawdata(0, ref app.YC_rawdata, ref count);    //SOC
                Get_Rawdata(0, ref app.YC_rawdata, ref count);    //SOH
                Get_Rawdata(0, ref app.YC_rawdata, ref count);    //Bms温度
                Get_Rawdata(0, ref app.YC_rawdata, ref count);    //Bms温度
            }

            // ----------------------- 添加关口电表数据 -----------------------
            if (frmMain.Selffrm.AllEquipment.Elemeter1List.Count > 0)
            {
                var elemeter = frmMain.Selffrm.AllEquipment.Elemeter1List[0];
                Get_Rawdata((float)elemeter.AllUkva, ref app.YC_rawdata, ref count);
            }
            else
            {
                Get_Rawdata(0, ref app.YC_rawdata, ref count); // 关口电表_总有功功率
            }

            // // ----------------------- 添加汇流柜电表数据 -----------------------
            if (frmMain.Selffrm.AllEquipment.Elemeter2H != null)
            {
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.Elemeter2H.AllUkva, ref app.YC_rawdata, ref count);  // 并网点电表_总有功功率  
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.Elemeter2H.HZ, ref app.YC_rawdata, ref count);       // 并网点电表_电网频率
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.Elemeter2H.PUkwh[0], ref app.YC_rawdata, ref count);    // 并网点电表_正向有功电能
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.Elemeter2H.PUkwh[1], ref app.YC_rawdata, ref count);    // 并网点电表_正向有功峰电能
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.Elemeter2H.PUkwh[2], ref app.YC_rawdata, ref count);    // 并网点电表_正向有功平电能
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.Elemeter2H.PUkwh[3], ref app.YC_rawdata, ref count);    // 并网点电表_正向有功平电能
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.Elemeter2H.PUkwh[4], ref app.YC_rawdata, ref count);    // 并网点电表_正向有功谷电能
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.Elemeter2H.OUkwh[0], ref app.YC_rawdata, ref count);    // 并网点电表_反向有功电能
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.Elemeter2H.OUkwh[1], ref app.YC_rawdata, ref count);    // 并网点电表_反向有功尖电能
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.Elemeter2H.OUkwh[2], ref app.YC_rawdata, ref count);    // 并网点电表_反向有功峰电能
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.Elemeter2H.OUkwh[3], ref app.YC_rawdata, ref count);    // 并网点电表_反向有功平电能
                Get_Rawdata((float)frmMain.Selffrm.AllEquipment.Elemeter2H.OUkwh[4], ref app.YC_rawdata, ref count);    // 并网点电表_反向有功谷电能
            }
            else
            {
                Get_Rawdata(0, ref app.YC_rawdata, ref count);
                Get_Rawdata(0, ref app.YC_rawdata, ref count);
                Get_Rawdata(0, ref app.YC_rawdata, ref count);
                Get_Rawdata(0, ref app.YC_rawdata, ref count);
                Get_Rawdata(0, ref app.YC_rawdata, ref count);
                Get_Rawdata(0, ref app.YC_rawdata, ref count);
                Get_Rawdata(0, ref app.YC_rawdata, ref count);
                Get_Rawdata(0, ref app.YC_rawdata, ref count);
                Get_Rawdata(0, ref app.YC_rawdata, ref count);
                Get_Rawdata(0, ref app.YC_rawdata, ref count);
                Get_Rawdata(0, ref app.YC_rawdata, ref count);
                Get_Rawdata(0, ref app.YC_rawdata, ref count);
            }

            // 对比当前数据与历史数据，处理变化项
            for (int i = 0; i < app.YC_rawdata.Length; i++)
            {
                if (i >= app.YC_perv_rawdata.Length || app.YC_rawdata[i] != app.YC_perv_rawdata[i])
                {
                    // 计算信息体地址（从 16385 开始）
                    int objectAddress = 16385 + i;
                    byte[] addressBytes = new byte[3];
                    addressBytes[0] = (byte)(objectAddress & 0xFF); // 低字节
                    addressBytes[1] = (byte)((objectAddress >> 8) & 0xFF); // 中字节
                    addressBytes[2] = (byte)((objectAddress >> 16) & 0xFF); // 高字节

                    messageList.AddRange(addressBytes); // 添加地址字节（低→高，符合 IEC 104 规范）

                    // 使用 Get_One_YC_Data 转换数据并添加到列表
                    if (Get_One_YC_Data(app.YC_rawdata[i], messageList))
                    {
                        difCount++;
                    }
                }
            }

            // 判断是否有变化数据，无变化则直接返回
            if (difCount == 0)
            {
                log.Warn("        无变化遥测数据，跳过发送");
                return;
            }

            // 处理补发数据（示例：强制补发第1个数据项）
            int forcedIndex = 0; // 假设索引从0开始，对应第20个数据项
            if (forcedIndex < app.YC_rawdata.Length)
            {
                int objectAddress = 16385 + forcedIndex;
                byte[] addressBytes = {
                    (byte)(objectAddress & 0xFF),
                    (byte)((objectAddress >> 8) & 0xFF),
                    (byte)((objectAddress >> 16) & 0xFF)
                };
                messageList.AddRange(addressBytes);

                if (Get_One_YC_Data(app.YC_rawdata[forcedIndex], messageList))
                {
                    difCount++;
                }                
            }

            // 数据修正：填充长度和数据项个数
            if (messageList.Count >= 2)
            {
                messageList[1] = (byte)(messageList.Count - 2); // 计算实际长度
            }

            if (messageList.Count >= 8)
            {
                messageList[7] = (byte)difCount; // 可变结构限定词：数据项个数
            }

            // 发送数据
            try
            {
                byte[] message = messageList.ToArray();
                if (frmMain.Selffrm.TCPserver.SendMsg_byte(message))
                {
                    frmMain.Selffrm.receive_time_send = DateTime.Now;
                    log.Warn($"        接收-发送时间：{(frmMain.Selffrm.receive_time_send - frmMain.Selffrm.receive_time_start).TotalSeconds}  变化个数：{difCount}");
                    log.Warn("        变换遥调 -- end ");
                    Record_Order(app.apci.TX_field1, app.apci.TX_field2);
                    Console.WriteLine("发送字节流：" + string.Join("-", message));
                }
                else
                {
                    log.Warn("        发送失败，重置连接状态");
                    app.apci.Reset(); // 假设存在重置方法，清空序号
                    app.Isconnect = false;
                }
            }
            catch (Exception ex)
            {
                log.Warn($"        发送异常：{ex.Message}");
                app.Isconnect = false;
            }

            log.Warn("        &&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&& end");
        }

        //为主站主动发送，记录发送序号
        public static void Record_Order(byte TX_field1, byte TX_field2)
        {
            UInt16 temp = (ushort)(TX_field1 | (TX_field2 << 8));
            temp += 2;
            app.apci.TX_field1 = (byte)temp;
            app.apci.TX_field2 = (byte)(temp >> 8);
            //Console.WriteLine($"变化遥测 ++{temp:x}");

        }

        public void IEC104_PropertyChanged(IAsyncResult ar_104)
        {
            DateTime startTime = DateTime.Now;
            DateTime endTime;
            if ((frmMain.Selffrm.TCPserver.GetConnectStatus() == false))
            {
                app.apci.TX_field1 = 0;
                app.apci.TX_field2 = 0;
                app.apci.RX_field3 = 0;
                app.apci.RX_field4 = 0;
                app.Isconnect = false;
            }
            endTime = DateTime.Now;
            Console.WriteLine("$");
            Console.WriteLine("$********* 变化 0 **********$" + (endTime - startTime).TotalSeconds);
            if (app.Isconnect == true)
            {
                ReturnSoleYCData();
               if (app.Isconnect != true) return;
                endTime = DateTime.Now;
                Console.WriteLine("$");
                Console.WriteLine("$********* 变化 1 **********$" + (endTime - startTime).TotalSeconds);
                ReturnSoleYXData(0x01);//不带时标
               if (app.Isconnect != true) return;
                endTime = DateTime.Now;
                Console.WriteLine("$");
                Console.WriteLine("$********* 变化  2 **********$" + (endTime - startTime).TotalSeconds);
                ReturnSoleYXData(0X1E);//带时标
                endTime = DateTime.Now;
                Console.WriteLine("$");
                Console.WriteLine("$********* 变化 3 **********$" + (endTime - startTime).TotalSeconds);
                Array.Copy(app.YX_rawdata, app.YX_perv_rawdata, app.YX_rawdata.Length);
                Array.Copy(app.YC_rawdata, app.YC_perv_rawdata, app.YC_rawdata.Length);
            }
        }

        //public void IEC104_PropertyChanged(object sender, EventArgs e)
        //{
           
        //    if ((frmMain.Selffrm.TCPserver.GetConnectStatus() == false))
        //    {
        //        app.apci.TX_field1 = 0;
        //        app.apci.TX_field2 = 0;
        //        app.apci.RX_field3 = 0;
        //        app.apci.RX_field4 = 0;
        //        app.Isconnect = false;
        //    }
        //    if (app.Isconnect == true)
        //    {
        //        ReturnSoleYCData();
        //        if (app.Isconnect != true) return;

        //        ReturnSoleYXData(0x01);
        //        if (app.Isconnect != true) return;

        //        ReturnSoleYXData(0X1E);

        //        Array.Copy(app.YX_rawdata, app.YX_perv_rawdata, app.YX_rawdata.Length);
        //        Array.Copy(app.YC_rawdata, app.YC_perv_rawdata, app.YC_rawdata.Length);

        //    }
        //}
        //获取最大充放电功率

    }
}