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

public struct APCI
{    // U-Format
    public byte start;    // 起始字节
    public byte len;      // 帧长度
    public byte field1;   // 控制域1-4
    public byte field2;
    public byte field3;
    public byte field4;
};

public struct ASDU_header
{                 // 数据单元标识
    byte type;             // 类型标识
    byte qual;             // 可变结构限定词
    byte tx_cause_1;       // 传送原因
    byte tx_cause_2;
    byte commom_asdu_1;    // 公共地址
    byte commom_asdu_2;
};

public struct ASDU
{
    ASDU_header  header;      // 数据单元标识
	byte[] data ;   // 信息体
};

public struct APDU
{
     APCI apci;
	 ASDU asdu;
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
        public byte CMD_STOPC	=   0x20;
        public byte CMD_TESTC	=   0x80;

    }

    public class CIEC104Slave
    {
        /*1.由于字节1和字节3的最低位固定为0，不用于构成序号，所以在计算序号时，要先转换成十进制数值，再除以2；

        2.由于低位字节在前，高位字节在后，所以计算时要先做颠倒；*/


        public ushort RxCounter = 0x0000;   // 接收序号
        public ushort TxCounter = 0x0000;   // 发送序号

        public static int[] isYKACK = new int[10]; 
        public static int[] isYDACK = new int[10];


        private static ILog log = LogManager.GetLogger("IEC104");


        /********************总召唤全部流程*******************************/
        public static void NAIec104InterrogationAll(byte[] TX_bytes, byte[] RX_bytes)
        {
            //传入参数： TX_bytes：从站序号  RX_bytes：主站序号
            //更新主站

            Build_SR_num(RX_bytes);
            InterrogationConfirm(TX_bytes, RX_bytes); //发送帧的镜像，除传送原因不同

            Build_SR_num(TX_bytes);
            ReturnAllYCData(TX_bytes, RX_bytes);

            Build_SR_num(TX_bytes);
            ReturnAllYXData(TX_bytes, RX_bytes);

            Build_SR_num(TX_bytes);
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
            
            frmMain.Selffrm.TCPserver.SendMsg_byte(message, frmMain.Selffrm.TCPserver.ClientSocket);

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
            //string hexString = BitConverter.ToString(message);


            frmMain.Selffrm.TCPserver.SendMsg_byte(message, frmMain.Selffrm.TCPserver.ClientSocket);

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

            frmMain.Selffrm.TCPserver.SendMsg_byte(message, frmMain.Selffrm.TCPserver.ClientSocket);

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
            //string hexString = BitConverter.ToString(message);


            frmMain.Selffrm.TCPserver.SendMsg_byte(message, frmMain.Selffrm.TCPserver.ClientSocket);


        }
        /**************************获取发送序号*******************/
        public static byte[] Get_S_num(byte[] TX_bytes, byte[] msg)
        {
            //Array.Reverse(bytes);
            Array.Copy(msg, 2, TX_bytes, 0, 2);
            return TX_bytes;

        }
        /**************************获取接收序号*******************/
        public static byte[] Get_R_num( byte[] RX_bytes, byte[] msg)
        {
            Array.Copy(msg, 4, RX_bytes, 0, 2);
            return RX_bytes;
        }

        /**************************生成发送序号和接收序号*******************/
        public static void Build_SR_num(byte[] bytes)
        {
            //序号递增+1
            int num = 0;
            num = ( (Convert.ToInt32(bytes[0])+Convert.ToInt32(bytes[1])*16*16 )/2 + 1)*2;
            Array.Copy(BitConverter.GetBytes(num),0,bytes,0,2);

        }
        /******************************************************************/
        /*                          解析I帧                               */
        /******************************************************************/
        public static void ProcessFormatI(byte[] msg)
        {
            //获取主站发送报文中的发送序号和接收序号
            byte[] TX_bytes = new byte[2];    //主站序号（国网调度中心）
            byte[] RX_bytes = new byte[2];    //从站信号（EMS）

            TX_bytes = Get_S_num( TX_bytes, msg);
            RX_bytes = Get_R_num( RX_bytes, msg);

            switch (msg[6])
            {
                //单点遥信
                case 1:
                    /*传输原因*/
                    if (msg[8] == 5 && msg[9] == 0)
                    {
                        //更新主站的序号
                        Build_SR_num(TX_bytes);
                        ReturnAllYXData(RX_bytes, TX_bytes);
                    }
                    break;
                //短浮点数遥测
                case 13:
                    /*传输原因*/
                    if (msg[8] == 5 && msg[9] == 0) //(遥信被请求，遥测被请求)
                    {
                        //更新主站的序号
                        Build_SR_num(TX_bytes);
                        ReturnAllYCData(RX_bytes, TX_bytes);
                    }
                    break;
                //总召唤
                case 0x64:
                    //接收总召唤
                    NAIec104InterrogationAll(RX_bytes, TX_bytes);
                    break;
                //单命令遥控
                case 0x2D:
                    if (frmSet.Listen104 == 1)
                    {
                        //接收遥控预置
                        int YKnum = Get_YKD_Num(msg, true);
                        if (msg[8] == 6 && msg[9] == 0 && isYKACK[YKnum] == 0)
                        {
                            //遥控返校
                            //接收遥控预置
                            Build_SR_num(TX_bytes);
                            NAIec104YKACK(msg, RX_bytes, TX_bytes);
                        }
                        //接收遥控执行
                        else if (msg[8] == 6 && msg[9] == 0)
                        {
                            //执行确认
                            //接收遥控执行确认
                            Build_SR_num(TX_bytes);
                            NAIec104YKEXEACK(msg, RX_bytes, TX_bytes);
                            //激活结束
                            Build_SR_num(RX_bytes);
                            NAIec104YKFinishACK(msg, RX_bytes, TX_bytes);
                        }
                        //遥控撤销
                        else if (msg[8] == 8 && msg[9] == 0)
                        {
                            //撤销确认
                            //接收遥控撤销确认
                            Build_SR_num(TX_bytes);
                            NAIec104YKDeactACK(msg, RX_bytes, TX_bytes);
                            //激活结束
                            Build_SR_num(RX_bytes);
                            NAIec104YKFinishACK(msg, RX_bytes, TX_bytes);
                        }
                    }
                    break;
                //遥调(设定浮点数值命令)
                case 50:
                    int YDnum = Get_YKD_Num(msg, false);
                    //接收遥调预置
                    if (msg[8] == 6 && msg[9] == 0 && isYDACK[YDnum] == 0)
                    {
                        //遥调返校
                        //接收遥调预置
                        Build_SR_num(TX_bytes);
                        NAIec104YDACK(msg, RX_bytes, TX_bytes);
                    }
                    //接收遥调执行
                    else if (msg[8] == 6 && msg[9] == 0 )
                    {
                        //执行确认
                        //接收遥调执行确认
                        Build_SR_num(TX_bytes);
                        NAIec104YDEXEACK(msg, RX_bytes, TX_bytes);
                        //激活结束
                        Build_SR_num(RX_bytes);
                        NAIec104YDFinishACK(msg, RX_bytes, TX_bytes);
                    }
                    //遥调撤销
                    else if (msg[8] == 8 && msg[9] == 0)
                    {
                        //撤销确认
                        //接收遥调撤销确认
                        Build_SR_num(TX_bytes);
                        NAIec104YDDeactACK(msg, RX_bytes, TX_bytes);
                        //激活结束
                        Build_SR_num(RX_bytes);
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

            if ( msg[2] == 0x07)  // U启动
            {
                Send_U_Msg(baseCommand.CMD_STARTC);
            }
            else if (msg[2]  == 0x13) // U停止
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
        static  public void ReturnAllYCData(byte[] TX_bytes, byte[] RX_bytes)
        {
            frmMain.Selffrm.AllEquipment.BMS.Get104Info();
            //byte[] message = new byte[47]; //15个遥测值 总共5*15+15=90字节     
            byte[] message = new byte[92];


            //byte[] message = new byte[19];
            //byte[] send_message = new byte[45];
            byte[] send_message = new byte[90];

            //测试数据

            int count = 15;
           

            message[0] = 0x68;
            //message[1] = 0x2B;     //APDU长度45字节     58
            message[1] = 0x58;   //
            //发送序号
            message[2] = TX_bytes[0];
            message[3] = TX_bytes[1];
            //接收序号
            message[4] = RX_bytes[0];
            message[5] = RX_bytes[1];
            //类型标示
            message[6] = 0x0D;   //短浮点数值0D   4字节的遥测值 + 1字节的品质描述符
            //可变限结构限定词
            message[7] = 0x8f;   //15个字节连续地址的数据
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


            //信息元素(PCS数据) 
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].aA, ref message,ref count);          //A电流
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].bA, ref message, ref count);          //B电流
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].cA, ref message, ref count);          //C电流
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].aV, ref message, ref count);         //a对地电压
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].bV, ref message, ref count);         //b对地电压
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].cV, ref message, ref count);         //c对地电压
            if (frmSet.SysCount == 1)
                Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].allUkva, ref message, ref count);     //总有用功率
            else
                Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.AllwaValue, ref message, ref count);
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].allNUkvar, ref message, ref count);    //总无功功率
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.PCSList[0].allPFactor, ref message, ref count);  //总功率因数
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.BMS.ChargeAmount, ref message, ref count);      //可充电量
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.BMS.DisChargeAmount, ref message, ref count);   //可放电量
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.E2PKWH[0], ref message, ref count);             //当日充电电量            
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.E2OKWH[0], ref message, ref count);             //当日放电电量
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.Elemeter2.PUkwh[0], ref message, ref count);    //累计充电电量
            Get_One_YC_Data((float)frmMain.Selffrm.AllEquipment.Elemeter2.OUkwh[0], ref message, ref count);    //累计放电电量

            Array.Copy(message,send_message, 90);
            //验证消息
            //string hexString = BitConverter.ToString(send_message);


            frmMain.Selffrm.TCPserver.SendMsg_byte(send_message, frmMain.Selffrm.TCPserver.ClientSocket);

            //return message;
        }

        /******************************************************************/
        /*                      获取遥测数据                              */
        /******************************************************************/

        static public bool Get_One_YC_Data(float data, ref byte[] message, ref int count)
        {

/*            if (data <= 255)
            {
                if (data < 0)
                {
                    data = -data;  //总无功功率为负数 ，值取正
                }
                byte[] bytes = new byte[4];
                bytes =  BitConverter.GetBytes((int)data);
                Array.Copy(bytes, 0, message, count, bytes.Length);
                count += 4;

                List<byte> byteList = new List<byte>(message);

                // 添加新的字节 ,品质描述符
                byteList.Add(0x00);
                // 转换回 byte 数组
                message = byteList.ToArray();
                count += 1;

            }
            else if (255 < data && data < 4294967295)
            {*/

                StringBuilder sb = new StringBuilder();
                byte[] bytes = BitConverter.GetBytes(data);

                foreach (var item in bytes)
                {
                    sb.Insert(0, item.ToString("X2"));
                }

                string dataString = sb.ToString();

                byte[] byteArray = new byte[dataString.Length / 2];
                for (int i = 0; i < dataString.Length; i += 2)
                {
                    byteArray[i / 2] = Convert.ToByte(dataString.Substring(i, 2), 16);
                }

                string hexString = BitConverter.ToString(byteArray);

                Array.Copy(byteArray, 3, message, count, 1);
                count +=1;
                Array.Copy(byteArray, 2, message, count, 1);
                count +=1;
                Array.Copy(byteArray, 1, message, count, 1);
                count +=1;
                Array.Copy(byteArray, 0, message, count, 1);
                count +=1;

                List<byte> byteList = new List<byte>(message);
                // 添加新的字节 ,品质描述符
                byteList.Add(0x00);
                // 转换回 byte 数组
                message = byteList.ToArray();
                count += 1;

/*            }
            else  //超出范围data置为0
            {
                data = 0;
                byte[] bytes = new byte[4];
                bytes =  BitConverter.GetBytes((int)data);
                Array.Copy(bytes, 0, message, count, bytes.Length);
                count += 4;

                List<byte> byteList = new List<byte>(message);
                // 添加新的字节 品质描述符
                byteList.Add(0x00);
                // 转换回 byte 数组
                message = byteList.ToArray();
                count += 1;
            }*/
            return true;
        }

        /******************************************************************/
        /*                          遥信数据                              */
        /******************************************************************/
        public static  byte[] ReturnAllYXData(byte[] TX_bytes, byte[] RX_bytes)
        {
            byte[] message = new byte[21];   // 15 + 6 =21


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
            if ( frmMain.Selffrm.AllEquipment.runState == 1 )  
                message[16] = 0x01;
            else if(frmMain.Selffrm.AllEquipment.runState == 0 )
                message[16] = 0x00;
            //PCS充电放电状态 （1：充电 0：放电）
            if(frmMain.Selffrm.AllEquipment.wTypeActive ==  "充电")
                message[17] = 0x01;
            else
                message[17] = 0x00;
            //BMS通信 ： （ 1：通信 0：失联 ）
            if (frmMain.Selffrm.AllEquipment.BMS.Prepared == true )
                message[18] = 0x01;
            else
                message[18] = 0x00;
            //储能需求侧相应模式投入 ( 1:进入网控 0：未进入)
            if (frmMain.Selffrm.AllEquipment.eState == 2)
                message[19] = 0x01;
            else
                message[19] = 0x00;
            //PCS开关状态  0:停机 1：开机
            if (frmMain.Selffrm.AllEquipment.PCSList[0].PcsRun == 255)
                message[20] = 0x00;
            else
                message[20] = 0x01;


            //验证消息
            //string hexString = BitConverter.ToString(message);


            frmMain.Selffrm.TCPserver.SendMsg_byte(message , frmMain.Selffrm.TCPserver.ClientSocket);

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

            //string hexString = BitConverter.ToString(msg);


            //send msg
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg, frmMain.Selffrm.TCPserver.ClientSocket);

          
        }

        /**********************遥调获取参数值********************/
        public static float Get_YD_Input(byte[] msg)
        {
        
            byte[] bytes = new byte[4];
            Array.Copy(msg, 15, bytes, 0, 4);
            Array.Reverse(bytes);


            string hexStr = BitConverter.ToString(bytes).Replace("-", ""); ;

            if (hexStr.Length != 8)
            {

            }
            byte[] byteArray = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                byteArray[i] = Convert.ToByte(hexStr.Substring((3 - i) * 2, 2), 16);
            }
            float floatValue = BitConverter.ToSingle(byteArray, 0);

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
                            if (input >= 0)
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
                        break;
                }
                //send msg

                //string hexString = BitConverter.ToString(msg);


                frmMain.Selffrm.TCPserver.SendMsg_byte(msg, frmMain.Selffrm.TCPserver.ClientSocket);
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
            //string hexString = BitConverter.ToString(msg);

            frmMain.Selffrm.TCPserver.SendMsg_byte(msg, frmMain.Selffrm.TCPserver.ClientSocket);
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
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg, frmMain.Selffrm.TCPserver.ClientSocket);
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
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg, frmMain.Selffrm.TCPserver.ClientSocket);
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
            //string hexString = BitConverter.ToString(msg);


            frmMain.Selffrm.TCPserver.SendMsg_byte(msg, frmMain.Selffrm.TCPserver.ClientSocket);
        }
        

        /**********************遥控返校***************************/
         public static void NAIec104YKACK(byte[] msg,byte[] TX_bytes, byte[] RX_bytes)
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
            if ( (msg[15] == 0x81) || (msg[15] == 0x80) )
            {
                isYKACK[num] = 1;
            }

            //string hexString = BitConverter.ToString(msg);


            //send msg
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg , frmMain.Selffrm.TCPserver.ClientSocket);
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
                    if (msg[15] == 0x00)
                    {
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            frmMain.Selffrm.AllEquipment.eState = 1;
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
                        }
                    }
                    break;
                //打开PCS
                case 1:
                    if (frmMain.Selffrm.AllEquipment.eState == 2)
                    {
                        //PCS运行开关
                        if (msg[15] == 0x00) //pcs关闭
                        {
                            lock (frmMain.Selffrm.AllEquipment)
                            {
                                frmMain.Selffrm.AllEquipment.PCSScheduleKVA = 0;
                                frmMain.Selffrm.AllEquipment.HostStart = false;
                                frmMain.Selffrm.AllEquipment.SlaveStart = false;
                            }

                            frmMain.Selffrm.AllEquipment.ExcPCSPowerOff();

                        }
                        else //pcs打开
                        {
                            lock (frmMain.Selffrm.AllEquipment)
                            {
                                frmMain.Selffrm.AllEquipment.HostStart = true;
                                frmMain.Selffrm.AllEquipment.SlaveStart = true;
                            }
                        }
                    }
                    break;
            }
            //send msg
            //string hexString = BitConverter.ToString(msg);
            //"发送遥控执行确认：" + hexString
            frmMain.Selffrm.TCPserver.SendMsg_byte(msg, frmMain.Selffrm.TCPserver.ClientSocket);
            isYKACK[num] = 0;


        }

        /**********************获取遥控号/遥调地址********************/
        //isYGK ： ture（遥控） false（遥调）
        public static int Get_YKD_Num(byte[] msg , bool isYGK)
        { 
            int num ;

            byte[] bytes = new byte[5];
            byte[] YKbytes = { 0x60, 0x01 };
            byte[] YDbytes = { 0x62, 0x01 };
            Array.Copy(msg, 12 , bytes , 0 ,3);
            Array.Reverse(bytes);
            if (isYGK)
            {
                num = Convert.ToInt32(BitConverter.ToString(bytes).Replace("-", ""), 16)  -  Convert.ToInt32(BitConverter.ToString(YKbytes).Replace("-", ""), 16); //获取遥控地址
            }
            else
                num = Convert.ToInt32(BitConverter.ToString(bytes).Replace("-", ""), 16)  -  Convert.ToInt32(BitConverter.ToString(YDbytes).Replace("-", ""), 16); //获取遥调地址

            return num;

        }
        public void iec104_packet_parser(byte[] data)
        {
            if ((data[2] & 0x03) == 0x03)
            {
                // u 帧
                ProcessFormatU(data);
               
            }
            else if ((data[2] & 0x01) == 0x01)
            {
                // s 帧
                //iEC104.txcheck = ((data[4]>>1)|(data[5]<<7));
            }
            else
            {
                ProcessFormatI(data);
            }
        }

    }
}