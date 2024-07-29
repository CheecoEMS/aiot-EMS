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



        /*1.由于字节1和字节3的最低位固定为0，不用于构成序号，所以在计算序号时，要先转换成十进制数值，再除以2；

        2.由于低位字节在前，高位字节在后，所以计算时要先做颠倒；*/


        public ushort RxCounter = 0x0000;   // 接收序号
        public ushort TxCounter = 0x0000;   // 发送序号

        public static int[] isYKACK = new int[10];
        public static int[] isYDACK = new int[10];


        private static ILog log = LogManager.GetLogger("IEC104");

        public static APDU app;


        private int _ErrorState_104;
        public int ErrorState_104 { get { return _ErrorState_104; } set { if (_ErrorState_104 != value) { _ErrorState_104 = value; OnPropertyChanged(); } } }
        private int _RunState_104;
        public int RunState_104 { get { return _RunState_104; } set { if (_RunState_104 != value) { _RunState_104 = value; OnPropertyChanged(); } } }
        
        private int _EState_104;
        public int EState_104 { get { return _EState_104; } set { if (_EState_104 != value) { _EState_104 = value; OnPropertyChanged(); } } }
       

        public  bool HostStart_104 { get { return _HostStart_104; } set { if (_HostStart_104 != value) {  _HostStart_104 = value; CIEC104Slave.ReturnSoleYXData(0X1E); } } }
        private  bool _HostStart_104;

        public  double  aC_104 { get { return _aC_104; } set { if (_aC_104 != value) {_aC_104 = value; CIEC104Slave.ReturnSoleYCData();  } } }
        private  double _aC_104;

        public  double PCSKVA_104 { get { return _PCSKVA_104; } set { if (_PCSKVA_104 != value) { _PCSKVA_104 = value; CIEC104Slave.ReturnSoleYCData(); } } }
        private  double _PCSKVA_104;

        public  double SOC_104 { get { return _SOC_104; } set { if (_SOC_104 != value) { _SOC_104 = value; CIEC104Slave.ReturnSoleYCData(); } } }
        private  double _SOC_104;
        public  double ChargeAmount_104 { get { return _ChargeAmount_104; } set { if (_ChargeAmount_104 != value) { _ChargeAmount_104 = value; CIEC104Slave.ReturnSoleYCData(); } } }
        private  double _ChargeAmount_104;


        public  double DisChargeAmount_104 { get { return _DisChargeAmount_104; } set { if (_DisChargeAmount_104 != value) { _DisChargeAmount_104 = value; CIEC104Slave.ReturnSoleYCData(); } } }
        private static double _DisChargeAmount_104;

        


        public  void IEC104_Init()
        {
            app.Isconnect = false;
            app.apci.start = 100;
            app.YC_rawdata = new float[25];
            app.YC_perv_rawdata = new float[25];
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

            frmMain.Selffrm.TCPserver.SendMsg_byte(message, ref frmMain.Selffrm.TCPserver.ClientSocket_104);
            UInt16 temp = ((ushort)(TX_bytes[0] | (TX_bytes[1] << 8)));
            temp += 2;
            app.apci.TX_field1 = (byte)temp;
            app.apci.TX_field2 = (byte)(temp >> 8);
            Console.WriteLine($"总召唤确认 ++{temp:x}");

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


            frmMain.Selffrm.TCPserver.SendMsg_byte(message, ref frmMain.Selffrm.TCPserver.ClientSocket_104);
            UInt16 temp = ((ushort)(TX_bytes[0] | (TX_bytes[1] << 8)));
            temp += 2;
            app.apci.TX_field1 = (byte)temp;
            app.apci.TX_field2 = (byte)(temp >> 8);
            Console.WriteLine($"总召唤结束 ++{temp:x}");

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

            frmMain.Selffrm.TCPserver.SendMsg_byte(message, ref frmMain.Selffrm.TCPserver.ClientSocket_104);
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

            frmMain.Selffrm.TCPserver.SendMsg_byte(message, ref frmMain.Selffrm.TCPserver.ClientSocket_104);
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
        public static void Build_SR_num(byte[] bytes)
        {
            //序号递增+1
            int num = 0;
            num = ((Convert.ToInt32(bytes[0]) + Convert.ToInt32(bytes[1]) * 16 * 16) / 2 + 1) * 2;
            Array.Copy(BitConverter.GetBytes(num), 0, bytes, 0, 2);

        }
        public static void Build_R_num(byte[] bytes)
        {
            //序号递增+1
            int num = 0;
            num = ((Convert.ToInt32(bytes[0]) + Convert.ToInt32(bytes[1]) * 16 * 16) / 2 + 1) * 2;
            Array.Copy(BitConverter.GetBytes(num), 0, bytes, 0, 2);
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
        /*                          遥测数据                              */
        /******************************************************************/
        static public void ReturnAllYCData(byte[] TX_bytes, byte[] RX_bytes)
        {
            frmMain.Selffrm.AllEquipment.BMS.Get104Info();
            //log.Debug("遥测数据");
            //byte[] message = new byte[47]; //15个遥测值 总共5*15+15=90字节     
            byte[] message = new byte[130];


            //byte[] message = new byte[19];
            //byte[] send_message = new byte[45];
            byte[] send_message = new byte[125];

            //测试数据
            //log.Debug("frmMain.Selffrm.AllEquipment.Elemeter2.Aa:" + frmMain.Selffrm.AllEquipment.Elemeter2.Aa);

            int count = 15;
            float PcsRun = 0;

            message[0] = 0x68;
            //message[1] = 0x2B;     //APDU长度45字节     58
            message[1] = 0x7B;   //
            //发送序号
            message[2] = TX_bytes[0];
            message[3] = TX_bytes[1];
            //接收序号
            message[4] = RX_bytes[0];
            message[5] = RX_bytes[1];
            //类型标示
            message[6] = 0x0D;   //短浮点数值0D   4字节的遥测值 + 1字节的品质描述符
            //可变限结构限定词
            message[7] = 0x96;   //22个字节连续地址的数据
            //message[7] = 0x01;
            //传输原因 
            message[8] = 0x14;   //响应总召唤
            message[9] = 0x00;
            //公共地址：装置地址
            message[10] = 0x01;
            message[11] = 0x00;
            //信息体地址 0x4001
            message[12] = 0x01;
            message[13] = 0x40;
            message[14] = 0x00;

            //message[15] = 0x01;


            if (frmMain.Selffrm.AllEquipment.PCSList[0].PcsRun == 255) PcsRun = 0;
            else if (frmMain.Selffrm.AllEquipment.wTypeActive == "放电") PcsRun = 2;
            else if (frmMain.Selffrm.AllEquipment.wTypeActive == "充电") PcsRun = 1;


            //信息元素(PCS数据) 
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].aA, ref message, ref count);          //A电流
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].bA, ref message, ref count);          //B电流
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].cA, ref message, ref count);          //C电流
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].aV, ref message, ref count);         //a对地电压
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].bV, ref message, ref count);         //b对地电压
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].cV, ref message, ref count);         //c对地电压
            if (frmSet.SysCount == 1)
                Get_One_YC_Data(-(float)frmMain.Selffrm.AllEquipment.PCSList[0].allUkva, ref message, ref count);     //总有用功率
            else
                Get_One_YC_Data(-(float)frmMain.Selffrm.AllEquipment.AllwaValue, ref message, ref count);
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].allNUkvar, ref message, ref count);    //总无功功率
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].allPFactor, ref message, ref count);  //总功率因数
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.BMS.ChargeAmount, ref message, ref count);      //可充电量
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.BMS.DisChargeAmount, ref message, ref count);   //可放电量
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.E2PKWH[0], ref message, ref count);             //当日充电电量            
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.E2OKWH[0], ref message, ref count);             //当日放电电量
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.Elemeter2.PUkwh[0], ref message, ref count);    //累计充电电量
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.Elemeter2.OUkwh[0], ref message, ref count);    //累计放电电量
            Get_One_YC_Data((float)100, ref message, ref count);    //累计充电电量
            Get_One_YC_Data((float)100, ref message, ref count);    //累计放电电量
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.BMS.soc, ref message, ref count);    //累计充电电量
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.BMS.soh, ref message, ref count);    //累计放电电量
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSScheduleKVA, ref message, ref count);    //累计放电电量
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.BMS.averageTemp, ref message, ref count);    //累计充电电量
            Get_One_YC_Data(PcsRun, ref message, ref count);    //累计放电电量



            Array.Copy(message, send_message, 125);
            //验证消息
            string hexString = BitConverter.ToString(send_message);
            //log.Debug("发送遥测数据：" + hexString);

            //log.Debug("A电流:"+frmMain.Selffrm.AllEquipment.PCSList[0].aA);
            //log.Debug("总无功功率:" + frmMain.Selffrm.AllEquipment.Elemeter2.AllNukva);
            //log.Debug("a对地电压:" + frmMain.Selffrm.AllEquipment.PCSList[0].aV);

            frmMain.Selffrm.TCPserver.SendMsg_byte(send_message, ref frmMain.Selffrm.TCPserver.ClientSocket_104);
            UInt16 temp = ((ushort)(TX_bytes[0] | (TX_bytes[1] << 8)));
            temp += 2;
            app.apci.TX_field1 = (byte)temp;
            app.apci.TX_field2 = (byte)(temp >> 8);
            Console.WriteLine($"遥测数据 ++{temp:x}");

            //return message;
        }

        /******************************************************************/
        /*                      获取遥测数据                              */
        /******************************************************************/

        static public bool Get_One_YC_Data(float data, ref byte[] message, ref int count)
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

            string hexString = BitConverter.ToString(byteArray);
            //log.Debug("数据1：" + hexString);

            Array.Copy(byteArray, 3, message, count, 1);
            count += 1;
            Array.Copy(byteArray, 2, message, count, 1);
            count += 1;
            Array.Copy(byteArray, 1, message, count, 1);
            count += 1;
            Array.Copy(byteArray, 0, message, count, 1);
            count += 1;

            List<byte> byteList = new List<byte>(message);
            // 添加新的字节 ,品质描述符
            byteList.Add(0x00);
            // 转换回 byte 数组
            message = byteList.ToArray();
            count += 1;
            return true;
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
            byte[] message = new byte[22];   // 15 + 6 =21


            message[0] = 0x68;
            message[1] = 0x13;    //APDU长度
            //发送序号
            message[2] = TX_bytes[0];
            message[3] = TX_bytes[1];
            //接收序号
            message[4] = RX_bytes[0];
            message[5] = RX_bytes[1];
            //类型标示
            message[6] = 0x01;   //单点遥信（带品质描述）
            //可变限结构限定词
            message[7] = 0x86;   //6个字节连续地址的数据
            //传输原因 
            message[8] = 0x14;  //响应总召唤
            message[9] = 0x00;
            //公共地址：装置地址
            message[10] = 0x01;
            message[11] = 0x00;
            //信息体地址
            message[12] = 0x01;
            message[13] = 0x00;
            message[14] = 0x00;
            //信息元素(储能表数据)
            //储能事故总信号  : ( 1:故障 0：正常 )
            if (frmMain.Selffrm.AllEquipment.ErrorState[2] == true)
                message[15] = 0x01;
            else
                message[15] = 0x00;
            //运行状态 ： （0正常运行，1故障）
            if (frmMain.Selffrm.AllEquipment.runState == 1)
                message[16] = 0x01;
            else if (frmMain.Selffrm.AllEquipment.runState == 0)
                message[16] = 0x00;
            //PCS充电放电状态 （1：充电 0：放电）
            if (frmMain.Selffrm.AllEquipment.BMS.Prepared == true)
                message[17] = 0x01;
            else
                message[17] = 0x00;
            //BMS通信 ： （ 1：通信 0：失联 ）
            if (frmMain.Selffrm.AllEquipment.eState == 2)
                message[18] = 0x01;
            else
                message[18] = 0x00;
            //储能需求侧相应模式投入 ( 1:进入网控 0：未进入)
            if (frmMain.Selffrm.AllEquipment.PCSList[0].Prepared == true)
                message[19] = 0x01;
            else
                message[19] = 0x00;
            //log.Debug("104内部 PcsRun" + frmMain.Selffrm.AllEquipment.PCSList[0].PcsRun);
            //PCS开关状态  0:停机 1：开机
            if (frmMain.Selffrm.AllEquipment.ErrorState[2] == true)
                message[20] = 0x01;
            else
                message[20] = 0x00;
  


            //验证消息
            string hexString = BitConverter.ToString(message);
            //log.Debug("发送遥信数据：" + hexString);

            frmMain.Selffrm.TCPserver.SendMsg_byte(message, ref frmMain.Selffrm.TCPserver.ClientSocket_104);
            UInt16 temp = ((ushort)(TX_bytes[0] | (TX_bytes[1] << 8)));
            temp += 2;
            app.apci.TX_field1 = (byte)temp;
            app.apci.TX_field2 = (byte)(temp >> 8);
            Console.WriteLine($"遥信数据 ++{temp:x}");

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
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg, ref frmMain.Selffrm.TCPserver.ClientSocket_104);
            UInt16 temp = ((ushort)(app.apci.TX_field1 | (app.apci.TX_field2 << 8)));
            temp += 2;
            app.apci.TX_field1 = (byte)temp;
            app.apci.TX_field2 = (byte)(temp >> 8);
            Console.WriteLine($"遥调返校 ++{temp:x}");
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
                        frmMain.Selffrm.AllEquipment.PCSScheduleKVA = (input / frmSet.SysCount);
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

                    }
                    //log.Debug("写入功率值：" + input + "写入PCSScheduleKVA" + frmMain.Selffrm.AllEquipment.PCSScheduleKVA);
                    break;
                //储能需求侧响应模式投入
                case 1:
                    if (msg[15] == 0x00)
                    {
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            frmMain.Selffrm.AllEquipment.eState = 1; ///手工
                            frmSet.SysMode = 1;
                            frmMain.TacticsList.TacticsOn = true; //恢复策略模式
                            frmMain.TacticsList.ActiveIndex = -2;
                        }
                    }
                    else
                    {
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            frmMain.Selffrm.AllEquipment.eState = 2; //进入网控模式
                            frmSet.SysMode = 2;
                            frmMain.TacticsList.TacticsOn = false;   //关闭策略

                            //初始化设置
                            frmMain.Selffrm.AllEquipment.PCSScheduleKVA = 0;
                            frmMain.Selffrm.AllEquipment.HostStart = false;
                            frmMain.Selffrm.AllEquipment.SlaveStart = false;
                            frmMain.Selffrm.Slave104.HostStart_104 = false;
                        }
                    }
                    break;

            }
            //send msg

            string hexString = BitConverter.ToString(msg);
            //log.Debug("发送遥调执行确认：" + hexString);

            frmMain.Selffrm.TCPserver.SendMsg_byte(msg, ref frmMain.Selffrm.TCPserver.ClientSocket_104);
            UInt16 temp = ((ushort)(app.apci.TX_field1 | (app.apci.TX_field2 << 8)));
            temp += 2;
            app.apci.TX_field1 = (byte)temp;
            app.apci.TX_field2 = (byte)(temp >> 8);
            Console.WriteLine($"遥调执行确认 ++{temp:x}");

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
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg, ref frmMain.Selffrm.TCPserver.ClientSocket_104);
            UInt16 temp = ((ushort)(TX_bytes[0] | (TX_bytes[1] << 8)));
            temp += 2;
            app.apci.TX_field1 = (byte)temp;
            app.apci.TX_field2 = (byte)(temp >> 8);
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
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg, ref frmMain.Selffrm.TCPserver.ClientSocket_104);
            UInt16 temp = ((ushort)(TX_bytes[0] | (TX_bytes[1] << 8)));
            temp += 2;
            app.apci.TX_field1 = (byte)temp;
            app.apci.TX_field2 = (byte)(temp >> 8);
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
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg, ref frmMain.Selffrm.TCPserver.ClientSocket_104);
            UInt16 temp = ((ushort)(app.apci.TX_field1 | (app.apci.TX_field2 << 8)));
            temp += 2;
            app.apci.TX_field1 = (byte)temp;
            app.apci.TX_field2 = (byte)(temp >> 8);
            Console.WriteLine($"遥控激活结束 ++{temp:x}");

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

            frmMain.Selffrm.TCPserver.SendMsg_byte(msg, ref frmMain.Selffrm.TCPserver.ClientSocket_104);
            UInt16 temp = ((ushort)(app.apci.TX_field1 | (app.apci.TX_field2 << 8)));
            temp += 2;
            app.apci.TX_field1 = (byte)temp;
            app.apci.TX_field2 = (byte)(temp >> 8);
            Console.WriteLine($"遥调激活结束 ++{temp:x}");

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
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg, ref frmMain.Selffrm.TCPserver.ClientSocket_104);
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
                            frmSet.SysMode = 1;
                            frmMain.TacticsList.TacticsOn = true; //恢复策略模式
                            frmMain.TacticsList.ActiveIndex = -2;

                            frmMain.Selffrm.AllEquipment.PCSScheduleKVA = 0;
                            frmMain.Selffrm.AllEquipment.HostStart = false;
                            frmMain.Selffrm.AllEquipment.SlaveStart = false;
                            frmMain.Selffrm.Slave104.HostStart_104 = false;

                            frmMain.Selffrm.AllEquipment.ExcPCSPowerOff();
                        }
                    }
                    else  //开启
                    {
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            frmMain.Selffrm.AllEquipment.eState = 2; //进入网控模式
                            frmSet.SysMode = 2;
                            frmMain.TacticsList.TacticsOn = false;   //关闭策略

                            //初始化设置
                            frmMain.Selffrm.AllEquipment.PCSScheduleKVA = 0;
                            frmMain.Selffrm.AllEquipment.HostStart = true;
                            frmMain.Selffrm.AllEquipment.SlaveStart = true;
                            frmMain.Selffrm.Slave104.HostStart_104 = true;
                        }
                    }
                    break;

            }
            //send msg
            string hexString = BitConverter.ToString(msg);
            //log.Debug("发送遥控执行确认：" + hexString);
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg, ref frmMain.Selffrm.TCPserver.ClientSocket_104);
            UInt16 temp = ((ushort)(app.apci.TX_field1 | (app.apci.TX_field2 << 8)));
            temp += 2;
            app.apci.TX_field1 = (byte)temp;
            app.apci.TX_field2 = (byte)(temp >> 8);
            Console.WriteLine($"(单点)遥控执行确认 ++{temp:x}");

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
                // s 帧
                //iEC104.txcheck = ((data[4]>>1)|(data[5]<<7));
                //log.Debug("是S帧");
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
            //储能事故总信号  : ( 1:故障 0：正常 )
            if (frmMain.Selffrm.AllEquipment.ErrorState[2] == true) arr[0] = 0x01;
            else arr[0] = 0x00;
            //运行状态 ： （0正常运行，1故障）
            if (frmMain.Selffrm.AllEquipment.runState == 1) arr[1] = 0x01;
            else if (frmMain.Selffrm.AllEquipment.runState == 0) arr[1] = 0x00;
            //BMS通信 ： （ 1：通信 0：失联 ）
            if (frmMain.Selffrm.AllEquipment.BMS.Prepared == true) arr[2] = 0x01;
            else arr[2] = 0x00;
            //储能需求侧相应模式投入 ( 1:进入网控 0：未进入)
            if (frmMain.Selffrm.AllEquipment.eState == 2) arr[3] = 0x01;
            else arr[3] = 0x00;
            //PCS开关状态  0:停机 1：开机
            //if (frmMain.Selffrm.AllEquipment.PCSList[0].PcsRun == 255) arr[5] = 0x00;
            //else arr[5] = 0x01;
            //包一个壳，不给他们pcs真实运行状态值 
            //PCS开关状态  0:停机 1：开机

            if (frmMain.Selffrm.AllEquipment.PCSList[0].Prepared == true) arr[4] = 0x01;
            else arr[4] = 0x00;

            if (frmMain.Selffrm.AllEquipment.LedErrorState[1] == true) arr[5] = 0x01;
            else arr[5] = 0x00;




            Get_Rawdata(Convert.ToBoolean(arr[0]), ref app.YX_rawdata, ref count);        //储能事故总信号
            Get_Rawdata(Convert.ToBoolean(arr[1]), ref app.YX_rawdata, ref count);             //运行状态
            Get_Rawdata(Convert.ToBoolean(arr[2]), ref app.YX_rawdata, ref count);         //BMS通信
            Get_Rawdata(Convert.ToBoolean(arr[3]), ref app.YX_rawdata, ref count);         //储能需求侧响应模式投入
            Get_Rawdata(Convert.ToBoolean(arr[4]), ref app.YX_rawdata, ref count);             //PCS通信
            Get_Rawdata(Convert.ToBoolean(arr[5]), ref app.YX_rawdata, ref count);             //告警总
            //Get_Rawdata(Convert.ToBoolean(app.bool_test), ref app.YX_rawdata, ref count);
            //Get_Rawdata(Convert.ToBoolean(app.bool_test), ref app.YX_rawdata, ref count);
            //Get_Rawdata(Convert.ToBoolean(app.bool_test), ref app.YX_rawdata, ref count);
            //Get_Rawdata(Convert.ToBoolean(app.bool_test), ref app.YX_rawdata, ref count);
            //Get_Rawdata(Convert.ToBoolean(app.bool_test), ref app.YX_rawdata, ref count);
            //Get_Rawdata(Convert.ToBoolean(app.bool_test), ref app.YX_rawdata, ref count);
            //Get_Rawdata(Convert.ToBoolean(app.bool_test), ref app.YX_rawdata, ref count);


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

            Array.Resize(ref message, Index);

            //Console.WriteLine(string.Join("-", message));

            if (!IEC104Send_Event.IsSet || (dif_count == 0)) return;
            IEC104Send_Event.Wait();
            if (frmMain.Selffrm.TCPserver.SendMsg_byte(message, ref frmMain.Selffrm.TCPserver.ClientSocket_104) == true)
            {
                UInt16 temp = ((ushort)(app.apci.TX_field1 | (app.apci.TX_field2 << 8)));
                temp += 2;
                app.apci.TX_field1 = (byte)temp;
                app.apci.TX_field2 = (byte)(temp >> 8);
                Console.WriteLine($"变化遥信 ++{temp:x}");

            }
            else
            {
                app.apci.TX_field1 = 0;
                app.apci.TX_field2 = 0;
                app.apci.RX_field3 = 0;
                app.apci.RX_field4 = 0;
                app.Isconnect = false;

            }

        }
        static public void ReturnSoleYCData()
        {
            /****************************************************/
            float PcsRun = 0;
            int Index = 0;
            int count = 0;      //记录数据次序
            int dif_count = 0;  //记录变化数据个数
            byte[] message = new byte[200];

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
            message[Index++] = 0x0D;   //短浮点数值0D   4字节的遥测值 + 1字节的品质描述符
            //可变限结构限定词
            message[Index++] = 0x00;   //占位无用
            //message[7] = 0x01;
            //传输原因 
            message[Index++] = 0x03;   //突发(变化遥信、变换遥测、soe等)
            message[Index++] = 0x00;
            //公共地址：装置地址
            message[Index++] = app.asdu.commom_asdu_1;
            message[Index++] = app.asdu.commom_asdu_2;

            /********/
            if (frmMain.Selffrm.AllEquipment.PCSList[0].PcsRun == 255) PcsRun = 0;
            else if (frmMain.Selffrm.AllEquipment.wTypeActive == "放电") PcsRun = 2;
            else if (frmMain.Selffrm.AllEquipment.wTypeActive == "充电") PcsRun = 1;

            frmMain.Selffrm.AllEquipment.BMS.Get104Info();

            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSList[0].aA, ref app.YC_rawdata, ref count);          //A电流
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSList[0].bA, ref app.YC_rawdata, ref count);          //B电流
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSList[0].cA, ref app.YC_rawdata, ref count);          //C电流
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSList[0].aV, ref app.YC_rawdata, ref count);         //a对地电压
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSList[0].bV, ref app.YC_rawdata, ref count);         //b对地电压
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSList[0].cV, ref app.YC_rawdata, ref count);         //c对地电压
            if (frmSet.SysCount == 1)
                Get_Rawdata(-(float)frmMain.Selffrm.AllEquipment.PCSList[0].allUkva, ref app.YC_rawdata, ref count);     //总有用功率
            else
                Get_Rawdata(-(float)frmMain.Selffrm.AllEquipment.AllwaValue, ref app.YC_rawdata, ref count);
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSList[0].allNUkvar, ref app.YC_rawdata, ref count);    //总无功功率
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSList[0].allPFactor, ref app.YC_rawdata, ref count);  //总功率因数
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.BMS.ChargeAmount, ref app.YC_rawdata, ref count);      //可充电量
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.BMS.DisChargeAmount, ref app.YC_rawdata, ref count);   //可放电量
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.E2PKWH[0], ref app.YC_rawdata, ref count);             //当日充电电量            
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.E2OKWH[0], ref app.YC_rawdata, ref count);             //当日放电电量
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.Elemeter2.PUkwh[0], ref app.YC_rawdata, ref count);    //累计充电电量
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.Elemeter2.OUkwh[0], ref app.YC_rawdata, ref count);    //累计放电电量
            Get_Rawdata((float)100, ref app.YC_rawdata, ref count);    //最大充电功率允许值
            Get_Rawdata((float)100, ref app.YC_rawdata, ref count);    //最大放电功率允许值
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.BMS.soc, ref app.YC_rawdata, ref count);    //SOC
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.BMS.soh, ref app.YC_rawdata, ref count);    //SOH
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.PCSScheduleKVA, ref app.YC_rawdata, ref count);    //有功功率设置
            Get_Rawdata((float)frmMain.Selffrm.AllEquipment.BMS.averageTemp, ref app.YC_rawdata, ref count);    //Bms温度
            Get_Rawdata(PcsRun, ref app.YC_rawdata, ref count);    //PCS运行状态



            for (int i = 0; i < app.YC_rawdata.Length; i++)
            {
                if (app.YC_rawdata[i] != app.YC_perv_rawdata[i])
                {
                    //信息体地址 0x4001
                    app.asdu.Object_Address_1 = ((i + 16385) & 0xFF).ToString("X");
                    app.asdu.Object_Address_2 = (((i + 16385) >> 8) & 0xFF).ToString("X");
                    app.asdu.Object_Address_3 = (((i + 16385) >> 16) & 0xFF).ToString("X");

                    message[Index++] = Convert.ToByte(app.asdu.Object_Address_1, 16);
                    message[Index++] = Convert.ToByte(app.asdu.Object_Address_2, 16);
                    message[Index++] = Convert.ToByte(app.asdu.Object_Address_3, 16);

                    //数据
                    Get_One_YC_Data(app.YC_rawdata[i], ref message, ref Index);
                    dif_count++;
                }

            }



            //数据修正          
            message[1] = (byte)(Index - 2);
            message[7] = (byte)(dif_count);
            Array.Resize(ref message, Index);

            Console.WriteLine(string.Join("-", message));
            if (!IEC104Send_Event.IsSet || (dif_count == 0)) return;
            IEC104Send_Event.Wait();
            if (frmMain.Selffrm.TCPserver.SendMsg_byte(message, ref frmMain.Selffrm.TCPserver.ClientSocket_104) == true)
            {
                UInt16 temp = ((ushort)(app.apci.TX_field1 | (app.apci.TX_field2 << 8)));
                temp += 2;
                app.apci.TX_field1 = (byte)temp;
                app.apci.TX_field2 = (byte)(temp >> 8);
                Console.WriteLine($"变化遥测 ++{temp:x}");

            }
            else//连接戳五清空接收序号
            {
                app.apci.TX_field1 = 0;
                app.apci.TX_field2 = 0;
                app.apci.RX_field3 = 0;
                app.apci.RX_field4 = 0;
                app.Isconnect = false;

            }

        }

        public event EventHandler PropertyChanged;

        public virtual void OnPropertyChanged()
        {
            PropertyChanged?.Invoke(this, EventArgs.Empty);
        }
        public void IEC104_PropertyChanged(object sender, EventArgs e)
        {
           
            if ((frmMain.Selffrm.TCPserver.ClientSocket_104 == null) || (!frmMain.Selffrm.TCPserver.ClientSocket_104.Connected))
            {
                app.apci.TX_field1 = 0;
                app.apci.TX_field2 = 0;
                app.apci.RX_field3 = 0;
                app.apci.RX_field4 = 0;
                app.Isconnect = false;
            }
            if (app.Isconnect == true)
            {
                ReturnSoleYCData();
                if (app.Isconnect != true) return;

                ReturnSoleYXData(0x01);
                if (app.Isconnect != true) return;

                ReturnSoleYXData(0X1E);

                Array.Copy(app.YX_rawdata, app.YX_perv_rawdata, app.YX_rawdata.Length);
                Array.Copy(app.YC_rawdata, app.YC_perv_rawdata, app.YC_rawdata.Length);
                app.bool_test = !app.bool_test;

            }
        }
        //获取最大充放电功率
        public void GetMax_Dis_ChargePower()
        {
            double dValue = 0;
            double dGridKVA = frmMain.Selffrm.AllEquipment.GridKVA; //  实时数据电网功率（关口表-视在功率）
            double dGridKW = frmMain.Selffrm.AllEquipment.GridKVA * frmMain.Selffrm.AllEquipment.PCSList[0].allPFactor;  //  实时数据电网功率（关口表-有功功率）

            double PowerCap = frmSet.MaxGridKW;       //最大需量（有功功率）


            //if ((dGridKVA >= PowerCap) && (frmMain.Selffrm.AllEquipment.wTypeActive == "充电"))//表1用于限流和防止超限 ,PCS在不工作时也有0.1，0.2的功率值
            { //超限
                dValue = dGridKW - PowerCap;  //关口表有功功率-最大需量
                //限流qiao 
                if (dValue >= Math.Abs(frmMain.Selffrm.AllEquipment.PCSKVA))  //相当于 用户侧功率 > 最大需量
                {
                    frmMain.Selffrm.AllEquipment.MaxChargePower = 0;  
                }
                else
                {
                    //if (frmMain.Selffrm.AllEquipment.PCSScheduleKVA != 0)
                    {   //最大需量 - 用户量
                        frmMain.Selffrm.AllEquipment.MaxChargePower = ((Math.Abs(frmMain.Selffrm.AllEquipment.PCSKVA) - dValue));
                    }
                    //else { frmMain.Selffrm.AllEquipment.MaxChargePower = 0; }
                }
            }
            //else if ((dGridKVA <= frmSet.MinGridKW) && (frmMain.Selffrm.AllEquipment.wTypeActive == "放电"))
            {
                //逆流
                dValue = frmSet.MinGridKW - dGridKVA;
                //限流qiao 
                if (dValue >= Math.Abs(frmMain.Selffrm.AllEquipment.PCSKVA))
                {
                    frmMain.Selffrm.AllEquipment.MaxDisChargePower = 0;
                }
                else
                {
                    //if (frmMain.Selffrm.AllEquipment.PCSScheduleKVA != 0)
                    {
                        frmMain.Selffrm.AllEquipment.MaxDisChargePower = ((Math.Abs(frmMain.Selffrm.AllEquipment.PCSKVA) - dValue));
                    }
                    //else { frmMain.Selffrm.AllEquipment.MaxDisChargePower = 0; }
                }
            }
            #region else
            //else
            //{
            //    if (frmMain.Selffrm.AllEquipment.wTypeActive == "充电")
            //    {
            //        //客户剩余的功率 大于 计划功率
            //        dValue = PowerCap - (frmMain.Selffrm.AllEquipment.GridKVA_window - Math.Abs(frmMain.Selffrm.AllEquipment.PCSKVA));
            //        if (dValue >= Math.Abs(frmMain.Selffrm.AllEquipment.PCSScheduleKVA))
            //        {
            //            dRate = 1;
            //        }
            //        else
            //        {
            //            if (PCSScheduleKVA != 0)
            //            {
            //                dRate = (dValue / Math.Abs(PCSScheduleKVA));
            //            }
            //            else { dRate = 0; }
            //        }
            //    }
            //    else if (wTypeActive == "放电")
            //    {
            //        dValue = (GridKVA_window + Math.Abs(PCSKVA)) - frmSet.MinGridKW;
            //        if (dValue >= Math.Abs(PCSScheduleKVA))
            //        {
            //            dRate = 1;
            //        }
            //        else
            //        {
            //            if (PCSScheduleKVA != 0)
            //            {
            //                dRate = (dValue / Math.Abs(PCSScheduleKVA));
            //            }
            //            else { dRate = 0; }
            //        }
            //    }
            //}
            #endregion

        }
    }
}