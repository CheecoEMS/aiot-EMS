using Google.Protobuf.WellKnownTypes;
using log4net;
using log4net.Util;
using Modbus;
using MySql.Data.MySqlClient;
using Mysqlx;
using Mysqlx.Crud;
using Mysqlx.Prepare;
using Org.BouncyCastle.Asn1.Crmf;
using Org.BouncyCastle.Bcpg;
using System;

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Transactions;
using System.Windows.Forms;
using static Mysqlx.Expect.Open.Types.Condition.Types;
using static System.Collections.Specialized.BitVector32;
using System.Net.Sockets;
using Org.BouncyCastle.Crypto;

namespace EMS
{
    //收益类，只用于向云汇报一天的收益
    public class ProfitClass
    {
        public DateTime time { get; set; }
        public string iot_code { get; set; } = "ems2023888888";
        public double[] DaliyAuxiliaryKWH { get; set; } = { 0, 0, 0, 0, 0 };     //当天总辅助电量 （辅助电表）
        public double[] DaliyE2PKWH { get; set; } = { 0, 0, 0, 0, 0 };  //当天总充电量（positive 正向）
        public double[] DaliyE2OKWH { get; set; } = { 0, 0, 0, 0, 0 };   //当天总放电量（opposite反向，逆向） 
        public double[] DaliyPrice { get; set; } = { 0, 0, 0, 0, 0 };    //电价
        public double DaliyProfit { get; set; }           //当天收益 
    }

    public class FaultClass
    {
        public DateTime timestamp { get; set; }
        public string iotCode { get; set; }
        public string faultCode { get; set; }
        public int faultLevel { get; set; }
        public string faultName { get; set; }
        public bool faultBack { get; set; } //是否恢复 
    }

    public class ModbusCommand
    {
        public string strMemo = "";
        public string strCOmmand = "";
        public int ComType = 3;
        public int DataAddr = 0;
        public int DataLongth = 1;
        public int DataType = 0;       //读写一体类型 0 short;   1:2小数     ;2,4小数    3:byte    4 long;5string
        public double Coefficient = 0;         //系数，返回数*系数=实际数字
        public string strData = "";    //返沪数据
        public string strResult = "";    //返回数据的结果     
        public bool IsSmallEnd = true;    //12 34-->12 34大段，12 34-->34 12小段，用于4字节浮点或者整形
        public int PCIndex = 0;          //0或者没有都是不用PC参数（PT*CT）；1PC参数（PT*CT）；2PC；3TC

    }

    /// <summary>
    /// 通讯类型枚举
    /// </summary>
    public enum ComType { M485, MUDP, MTCPClient, MTCPServer, M232, Can, SPI, IIC };
    public enum WorkTypes { 充电, 放电 };
    public enum PCSTypes { 待机, 恒压, 恒流, 恒功率,AC恒压};
    /// <summary>
    /// 通讯数据类型
    /// </summary>


    //视在功率S=1.732UI
    //有功功率P=1.732UIcosΦ
    //无功功率Q = 1.732UIsinΦ
    //功率因数cosΦ = P / S
    //sinΦ=Q/S
    //所有部件的共性
    public class BaseEquipmentClass
    {
        public string version;
        public string strCap = "";
        public string strCommandFile = "";
        public string iot_code { get; set; } = "";
        public int pc = 1;
        public DateTime time { get; set; } = Convert.ToDateTime("2000-6-30 09:09:09");

        public string TCPType = "clint";//0 clint;1:Server
        public string serverIP = "192.168.1.100";
        public int SerPort = 9001;
        public int LocPort = 9001;
        public bool bUsed = false;
        public int comRate = 9600;//
        public int comBits = 8;
        public int eID = 1;

        public int eType = 0;  //硬件类型：0表；1pcs逆变；2BMU；3空调系统
        public int comType = 0;// ComType.M485;  //0:modbus485;1udp;2TCP Cleint;3TCP Server
        public string eModel = "";  // 型号
        public string comName = "Com1";//com口默认为1


        public modbus485 m485; //每个部件关联一个硬件com口的485通信SP
        //public modbusTCPServer mTCPServer;
        //public modbusTCPClient mTCPClient;
        //public modbusUDP mUDP;
        public AllEquipmentClass Parent = null; //所有部件的父类都是AllEquipmentClass类的实例对象
        public List<ModbusCommand> ComList = new List<ModbusCommand>(); //从由协议转义的TXT文本获取command的相关信息，如寄存器地址，功能码，字节大小等
        public bool Prepared = false;  //硬件是否通讯连接

        //11.16 记录PCS告警报文
        public byte[] WarnMessage = new byte[20];
        public int PreparedCount = 0;//记录通信失联次数
        //8.8
        public  static ILog log = LogManager.GetLogger("BaseEquipmentClass");

        public BaseEquipmentClass()
        {
            //ComList = new List<ModbusCommand>();
        }


        /*从文本中提取command，构建ComList*/


        /*strSource :数据源即一行command信息
         * astrDef  :默认值
         */
        //获取一个数据
        private string GetoneData(ref string strSource, string astrDef)
        {
            string strData = astrDef;
            //
            if (strSource.IndexOf(";") >= 0)
            {
                strData = strSource.Substring(0, strSource.IndexOf(";"));
                strSource = strSource.Substring(strSource.IndexOf(";") + 1, strSource.Length - strSource.IndexOf(";") - 1);
            }
            else
            {
                if (strSource.Length > 0)
                    strData = strSource;
                strSource = "";
            }
            return strData.Trim();
        }

        //获取一个modbus协议地址指令
        private bool strData2Park(string astrData, ref ModbusCommand aoneCommand,int aPC)
        {
            try
            {
                aoneCommand.strMemo = GetoneData(ref astrData, "string Command");
                aoneCommand.strCOmmand = GetoneData(ref astrData, "");
                //aoneCommand.SysID = Convert.ToInt16(GetoneData(ref astrData, "1"));
                aoneCommand.ComType = Convert.ToInt16(GetoneData(ref astrData, "3"), 16);
                aoneCommand.DataAddr = Convert.ToInt32(GetoneData(ref astrData, "0"), 16);
                aoneCommand.DataLongth = Convert.ToInt32(GetoneData(ref astrData, "1"), 16);
                aoneCommand.DataType = Convert.ToInt32(GetoneData(ref astrData, "0"));//读写一体类型 0 short;1:小数.;2byte;3long;4string
                aoneCommand.Coefficient = (double)Convert.ToDouble(GetoneData(ref astrData, "1"));
                aoneCommand.IsSmallEnd = (GetoneData(ref astrData, "1") == "1");
                aoneCommand.PCIndex = Convert.ToInt32(GetoneData(ref astrData, "0"));
                if (aoneCommand.PCIndex > 0)
                    aoneCommand.Coefficient = aoneCommand.Coefficient * aPC;
                // FloatLen=0;       //浮点苏时有效；0任意长度；其他是小数点后长度/（n*10）
                aoneCommand.strData = "";         //返沪数据
                aoneCommand.strResult = "";  //返回数据的结果  
                return true;
            }
            catch//(Exception ex)
            {
                // MessageBox.Show(ex.ToString());
            }
            return true;

        }

        //下载command文件
        public void LoadCommandFromFile()
        {
            string version;
            if (!File.Exists(strCommandFile))
                return;
            //读取数据
            StreamReader srFile = File.OpenText(strCommandFile);
            try
            {
                string strData = srFile.ReadLine();
                ComList.Clear();
                while (strData != null)
                {
                    strData = strData.Trim();//去掉首尾空格字符
                    //if ((strData.Substring(0, 1) == "*"))//遇到“*”开头的解释字符读取版本的号
                    //{
                    //    version = strData.Substring(1);
                    //}

                    if ((strData == "") || (strData.Substring(0, 1) == "#")|| (strData.Substring(0, 1) == "*"))//遇到空白行或者 “#”开头的解释字符跳过
                    {
                        strData = srFile.ReadLine();
                        continue;
                    }

                    ModbusCommand oneCommand = new ModbusCommand();
                    if (strData2Park(strData, ref oneCommand,this.pc))
                        ComList.Add(oneCommand);
                    strData = srFile.ReadLine();
                }

            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            finally
            {
                srFile.Close();
            }
        }

        public string LoadVersionFromFile()
        {
            string version = "";
            if (!File.Exists(strCommandFile))
                return "";

            //读取数据
            StreamReader srFile = File.OpenText(strCommandFile);
            try
            {
                string strData = srFile.ReadLine();
                ComList.Clear();
                while (strData != null)
                {
                    strData = strData.Trim();//去掉首尾空格字符
                    if ((strData.Substring(0, 1) == "*"))//遇到“*”开头的解释字符读取版本的号
                    {
                        version = strData.Substring(1);
                        return version;
                    }

                    if ((strData == "") || (strData.Substring(0, 1) == "#"))//遇到空白行或者 “#”开头的解释字符跳过
                    {
                        strData = srFile.ReadLine();
                        continue;
                    }
                    strData = srFile.ReadLine();
                }
                return "";


            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
                return "";

            }
            finally
            {
                srFile.Close();
            }
        }

        //把类的当时数据保存到数据库
        public virtual void Save2DataSource(string arDate)//虚方法，由继承类重写（每个部件相应的插入SQL语句）
        { } 

        //从设备上读取数据
        public virtual void GetDataFromEqipment()//虚方法，由继承类重写（子类根据对应的Comlist，获取数据）
        { }

        public virtual void GetDataFromEqipment2(int CID)
        { }
        /// <summary>
        /// 读取一个批量读取来的数据：对连续地址的一批数据，如果采用逐个modbus命令轮询，消息刷新慢。所以采用一次读去多个寄存器的值，在对读取的数据进行解析拆分，响应速度更快
        /// </summary>
        /// <param name="aDataIndex"命令ID></param>
        /// <param name="astrData"集中闻讯的字符串数据></param>
        /// <param name="aBack"返回的结果></param>
        /// <returns></returns>
        public bool GetDataFromstr(int aDataIndex, ref string astrData, ref string aBack)
        {
            if (aDataIndex >= ComList.Count)
                return false;
            ModbusCommand tempComand = ComList[aDataIndex];


            switch (tempComand.ComType) //1输出圈，2输入圈
            {
                case 1: // 功能码“01”：读1路或多路开关量输出状态
                    aBack = (System.Convert.ToInt32("0x" + astrData, 16)).ToString();
                    break;
                case 2:// 功能码“02”：读1路或多路开关量状态输入
                    aBack = (System.Convert.ToInt32("0x" + astrData, 16)).ToString();
                    break;
                case 3://功能码“03”：读多路寄存器输入
                case 4://功能码“04”：读多路非保存寄存器输入
                    if (astrData.Length < tempComand.DataLongth * 2)
                    {
                        astrData = "";
                        aBack = "";
                        return false;
                    }
                    string strData = astrData.Substring(0, tempComand.DataLongth * 2);
                    astrData = astrData.Substring(tempComand.DataLongth * 2, astrData.Length - tempComand.DataLongth * 2);
                    ////0 Ushort; 1:UFloat小数.; 2byte; 3Uint32; 4string ; 5  short; 6float; 7int32
                    switch (tempComand.DataType)
                    {
                        case 0: //Ushort
                            aBack = (System.Convert.ToUInt16("0x" + astrData, 16)).ToString();
                            break;
                        case 1://UFloat
                            if (astrData.Length == 2)
                                aBack = (System.Convert.ToUInt16("0x" + astrData, 16) * tempComand.Coefficient).ToString();
                            else
                                aBack = (System.Convert.ToUInt32("0x" + astrData, 16) * tempComand.Coefficient).ToString();
                            break;
                        case 2:
                            break;
                        case 3://3Uint32
                            aBack = (System.Convert.ToUInt32("0x" + astrData, 16)).ToString();
                            break;
                        case 4://4string
                            aBack = astrData.Trim();
                            break;
                        case 5://short
                            aBack = (System.Convert.ToInt16("0x" + astrData, 16)).ToString();
                            break;
                        case 6://6float
                            if (astrData.Length == 2)
                                aBack = (System.Convert.ToInt16("0x" + astrData, 16) * tempComand.Coefficient).ToString();
                            else
                                aBack = (System.Convert.ToInt32("0x" + astrData, 16) * tempComand.Coefficient).ToString();
                            break;
                        case 7://7int32
                            aBack = (System.Convert.ToInt32("0x" + astrData, 16)).ToString();
                            break;
                        default:
                            aBack = "";
                            break;
                    }
                    break;
            }
            return true;
        }

        public bool Get3Data(int aDataIndex, ref string aBack)
        {
            if (aDataIndex >= ComList.Count)
                return false;
            ModbusCommand tempComand = ComList[aDataIndex];
            bool bResult = m485.GetString((byte)eID, (byte)3, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth, ref aBack);
            return false;
        }


        // 读取一个批量读取来的数据：对连续地址的一批数据，如果采用逐个modbus命令轮询，消息刷新慢。所以采用一次读去多个寄存器的值，在对读取的数据进行解析拆分，响应速度更快
        // 3代表功能码 ，strData:数据读取过来时是按string类型
        public bool Get3strData(int aDataIndex, ref string aSource, ref string aBack)
        {
            int DataLen = 2;
            string strData;
            if (aDataIndex >= ComList.Count)
                return false;
            ModbusCommand tempComand = ComList[aDataIndex];
            DataLen = tempComand.DataLongth * 4;  // 4: Modbus指令中字节数的单位是short，所以返回的数据的字节长度*2 ，又返回的数据是按照1个字节按两个字节读 ，所以字节长度*4 ，见GetSysData的case3的 Get1UInt32
            if (aSource.Length < DataLen)
                return false;
            strData = aSource.Substring(0, DataLen);
            aSource = aSource.Substring(DataLen, aSource.Length - DataLen);
            ////0 Ushort; 1:UFloat小数.; 2byte; 3Uint32; 4string ; 5  short; 6float; 7int32
            switch (tempComand.DataType)
            {
                case 0:
                    aBack = ((UInt16)Convert.ToInt16("0x" + strData, 16)).ToString();
                    break;
                case 1:
                    if (tempComand.DataLongth == 1)
                        aBack = (((UInt16)Convert.ToInt16("0x" + strData, 16)) * tempComand.Coefficient).ToString();
                    else
                    {
                        if (tempComand.IsSmallEnd)
                            strData = "0X" + strData.Substring(4, 4) + strData.Substring(0, 4);
                        else
                            strData = "0X" + strData;//.Substring(0, 4) + strData.Substring(4, 4);
                        aBack = (((UInt32)Convert.ToInt32(strData, 16)) * tempComand.Coefficient).ToString();
                    }
                    //   aBack = (((UInt32)Convert.ToInt32("0x" + strData, 16)) * tempComand.Coefficient).ToString();

                    break;
                case 2:
                    //bResult = m485.Get1Float((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth,
                    //    ref tempFloat, tempComand.Coefficient, false);
                    //if (bResult)
                    //     aBack = tempShort.ToString();
                    break;
                case 3:
                    if (tempComand.DataLongth > 1)
                    {
                        if (tempComand.IsSmallEnd)
                            aBack = "0X" + strData.Substring(4, 4) + strData.Substring(0, 4);
                        else
                            aBack = "0X" + strData;
                        aBack = ((UInt32)Convert.ToInt32(aBack, 16)).ToString();
                    }
                    if (tempComand.DataLongth == 1)
                        aBack = ((UInt32)Convert.ToInt32("0x" + strData, 16)).ToString();
                    break;
                case 4://string  
                    aBack = strData;// 
                    break;
                case 5://int16  
                    aBack = Convert.ToInt16("0x" + strData, 16).ToString();
                    break;
                case 6:
                    ;
                    if (tempComand.DataLongth == 1)
                        aBack = ((Convert.ToInt16("0x" + strData, 16)) * tempComand.Coefficient).ToString();
                    else
                    {
                        if (tempComand.IsSmallEnd)
                            strData = "0X" + strData.Substring(4, 4) + strData.Substring(0, 4);
                        else
                            strData = "0X" + strData;
                        aBack = ((Convert.ToInt32(strData, 16)) * tempComand.Coefficient).ToString();
                    }
                    //   aBack = ((Convert.ToInt32("0x" + strData, 16)) * tempComand.Coefficient).ToString();
                    break;
                case 7:
                    if (tempComand.DataLongth > 1)
                    {
                        if (tempComand.IsSmallEnd)
                            aBack = "0X" + strData.Substring(4, 4) + strData.Substring(0, 4);
                        else
                            aBack = "0X" + strData;
                        aBack = (Convert.ToInt32(aBack, 16)).ToString();
                    }
                    if (tempComand.DataLongth == 1)
                        aBack = (Convert.ToInt32("0x" + strData, 16)).ToString();
                    break;
                default:
                    aBack = "";
                    break;
            }
            return true;
        }

        /// <summary>
        /// 真实的问讯读取一个数据
        /// </summary>
        /// <param name="aDataIndex"命令的ID></param>
        /// <param name="aBack"返回的字符串数据></param>
        /// <returns></returns>
        public bool GetSysData(int aDataIndex, ref string aBack)
        {
            bool bResult = false;
            short tempShort = 0;
            ushort tempUShort = 0;
            Int32 tempInt32 = 0;
            UInt32 tempUInt32 = 0;
            double tempFloat = 0;
            int iTemp;
            string tempStr = "";
            if (aDataIndex >= ComList.Count)
                return false;
            ModbusCommand tempComand = ComList[aDataIndex];
            //error
            byte[] BackData = null;
            switch (comType)////0:modbus485;1udp;2TCP Clint;3TCP Server
            {
                case 0:
                    switch (tempComand.ComType) //1输出圈，2输入圈
                    {
                        case 1:

                            bResult = m485.Send1MSG((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth, ref BackData);
                            if (bResult)
                            {
                                for (int i = 0; i < BackData.Length; i++)
                                {
                                    iTemp = BackData[i] << (8 * i);
                                    tempInt32 = tempInt32 + iTemp;
                                }

                            }
                            aBack = tempInt32.ToString();
                            break;
                        case 2:
                            //byte[] BackData=null;
                            bResult = m485.Send1MSG((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth, ref BackData);
                            if (bResult)
                            {
                                for (int i = 0; i < BackData.Length; i++)
                                {
                                    iTemp = BackData[i] << (8 * i);
                                    tempInt32 = tempInt32 + iTemp;
                                }
                                //     tempInt32 = (tempInt32 << 8) + BackData[i];
                            }
                            aBack = tempInt32.ToString();
                            break;
                        case 3://寄存器
                        case 4:
                            ////0 Ushort; 1:UFloat小数.; 2byte; 3Uint32; 4string ; 5  short; 6float; 7int32
                            switch (tempComand.DataType)
                            {
                                case 0:
                                    bResult = m485.GetUShort((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth, ref tempUShort);
                                    if (bResult)
                                        aBack = tempUShort.ToString();
                                    break;
                                case 1:
                                    bResult = m485.GetUFloat((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth,
                                        ref tempFloat, tempComand.Coefficient, tempComand.IsSmallEnd);
                                    if (bResult)
                                        aBack = tempFloat.ToString();
                                    break;
                                case 2:
                                    //bResult = m485.Get1Float((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth,
                                    //    ref tempFloat, tempComand.Coefficient, );
                                    //if (bResult)
                                    //     aBack = tempShort.ToString();
                                    break;
                                case 3:
                                    bResult = m485.Get1UInt32((byte)eID, (byte)tempComand.ComType,
                                        (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth,
                                        ref tempUInt32, tempComand.IsSmallEnd);
                                    if (bResult)
                                        aBack = tempUInt32.ToString();//返回数据按照string类型保存，字节数是tempComand.DataLongth*4
                                    break;
                                case 4:
                                    bResult = m485.GetString((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth, ref tempStr);
                                    if (bResult)
                                        aBack = tempStr.Trim();
                                    break;
                                case 5:
                                    bResult = m485.GetShort((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth, ref tempShort);
                                    if (bResult)
                                        aBack = tempShort.ToString();
                                    break;
                                case 6:
                                    bResult = m485.GetFloat((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth,
                                       ref tempFloat, tempComand.Coefficient, tempComand.IsSmallEnd);
                                    if (bResult)
                                        aBack = tempFloat.ToString();
                                    break;
                                case 7:
                                    bResult = m485.Get1Int32((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth,
                                        ref tempInt32, tempComand.IsSmallEnd);
                                    if (bResult)
                                        aBack = tempInt32.ToString();
                                    break;
                                default:
                                    aBack = "";
                                    break;
                            }
                            break;
                    }
                    //aOnetexbox.Text = (tempShort * tempComand.Coefficient).ToString();
                    break;
                case 1://tcp
                    break;
                default:
                    break;
            }
            return bResult;
        }

        //与GetSysData 的区别 ： 入参新增CID，CID代表从机ems的地址
        public bool GetSysData2(int aDataIndex, int CID, ref string aBack)
        {
            bool bResult = false;
            short tempShort = 0;
            ushort tempUShort = 0;
            Int32 tempInt32 = 0;
            UInt32 tempUInt32 = 0;
            double tempFloat = 0;
            int iTemp;
            string tempStr = "";
            if (aDataIndex >= ComList.Count)
                return false;
            ModbusCommand tempComand = ComList[aDataIndex];
            //error
            byte[] BackData = null;
            switch (comType)////0:modbus485;1udp;2TCP Clint;3TCP Server
            {
                case 0:
                    switch (tempComand.ComType) //1输出圈，2输入圈
                    {
                        case 1:

                            bResult = m485.Send1MSG((byte)CID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth, ref BackData);
                            if (bResult)
                            {
                                for (int i = 0; i < BackData.Length; i++)
                                {
                                    iTemp = BackData[i] << (8 * i);
                                    tempInt32 = tempInt32 + iTemp;
                                }

                            }
                            aBack = tempInt32.ToString();
                            break;
                        case 2:
                            //byte[] BackData=null;
                            bResult = m485.Send1MSG((byte)CID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth, ref BackData);
                            if (bResult)
                            {
                                for (int i = 0; i < BackData.Length; i++)
                                {
                                    iTemp = BackData[i] << (8 * i);
                                    tempInt32 = tempInt32 + iTemp;
                                }
                                //     tempInt32 = (tempInt32 << 8) + BackData[i];
                            }
                            aBack = tempInt32.ToString();
                            break;
                        case 3://寄存器
                        case 4:
                            ////0 Ushort; 1:UFloat小数.; 2byte; 3Uint32; 4string ; 5  short; 6float; 7int32
                            switch (tempComand.DataType)
                            {
                                case 0:
                                    bResult = m485.GetUShort((byte)CID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth, ref tempUShort);
                                    if (bResult)
                                        aBack = tempUShort.ToString();
                                    break;
                                case 1:
                                    bResult = m485.GetUFloat((byte)CID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth,
                                        ref tempFloat, tempComand.Coefficient, tempComand.IsSmallEnd);
                                    if (bResult)
                                        aBack = tempFloat.ToString();
                                    break;
                                case 2:
                                    //bResult = m485.Get1Float((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth,
                                    //    ref tempFloat, tempComand.Coefficient, );
                                    //if (bResult)
                                    //     aBack = tempShort.ToString();
                                    break;
                                case 3:
                                    bResult = m485.Get1UInt32((byte)CID, (byte)tempComand.ComType,
                                        (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth,
                                        ref tempUInt32, tempComand.IsSmallEnd);
                                    if (bResult)
                                        aBack = tempUInt32.ToString();//返回数据按照string类型保存，字节数是tempComand.DataLongth*4
                                    break;
                                case 4:
                                    bResult = m485.GetString((byte)CID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth, ref tempStr);
                                    if (bResult)
                                        aBack = tempStr.Trim();
                                    break;
                                case 5:
                                    bResult = m485.GetShort((byte)CID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth, ref tempShort);
                                    if (bResult)
                                        aBack = tempShort.ToString();
                                    break;
                                case 6:
                                    bResult = m485.GetFloat((byte)CID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth,
                                       ref tempFloat, tempComand.Coefficient, tempComand.IsSmallEnd);
                                    if (bResult)
                                        aBack = tempFloat.ToString();
                                    break;
                                case 7:
                                    bResult = m485.Get1Int32((byte)CID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)tempComand.DataLongth,
                                        ref tempInt32, tempComand.IsSmallEnd);
                                    if (bResult)
                                        aBack = tempInt32.ToString();
                                    break;
                                default:
                                    aBack = "";
                                    break;
                            }
                            break;
                    }
                    //aOnetexbox.Text = (tempShort * tempComand.Coefficient).ToString();
                    break;
                case 1://tcp
                    break;
                default:
                    break;
            }
            return bResult;
        }

            //设置一个Short数据
            public bool SetSysData(int aDataIndex, ushort aData,bool bLocksp)
        {
            bool bResult = false;
            ModbusCommand tempComand = ComList[aDataIndex];
            switch (comType)////0:modbus485;1udp;2TCP Clint;3TCP Server
            {
                case 0:
                    switch (tempComand.ComType)
                    {
                        case 5:
                            bResult = m485.Send5MSG((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, aData != 0, bLocksp);
                            break;
                        case 6:
                            bResult = m485.Send6MSG((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, aData, bLocksp);
                            break;
                    }
                    break;
                case 1:
                    break;
                default:
                    break;
            }
            return bResult;
        }

        //设置一个Short数据
        public bool SetSysData(int aDataIndex, short aData,bool bLocksp)
        {
            bool bResult = false;
            ModbusCommand tempComand = ComList[aDataIndex];
            short[] Datas = { aData };
            switch (comType)////0:modbus485;1udp;2TCP Clint;3TCP Server
            {
                case 0:
                    if (tempComand.ComType == 5)
                        bResult = m485.Send5MSG((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, aData > 0, bLocksp);
                    else if (tempComand.ComType == 6)
                        bResult = m485.Send6MSG((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr, (ushort)aData, bLocksp);
                    else
                        bResult = m485.Send16MSG((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr,
                          (ushort)tempComand.DataLongth, Datas, bLocksp);
                    break;
                case 1:
                    break;
                default:
                    break;
            }
            return bResult;
        }

        //设置一个Int32数据
        public bool SetSysData(int aDataIndex, Int32 aData,bool bLocksp)
        {
            bool bResult = false;
            if (aDataIndex > ComList.Count)
                return bResult;
            ModbusCommand tempComand = ComList[aDataIndex];
            short[] Datas = new short[2];
            Datas[0] = (short)(aData >> 16);
            Datas[1] = (short)(aData);

            //error
            switch (comType)////0:modbus485;1udp;2TCP Clint;3TCP Server
            {
                case 0:
                    switch (tempComand.ComType)//功能码
                    {
                        case 5:
                            bResult = m485.Send5MSG((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr,
                                   (aData > 0), bLocksp);
                            break;
                        case 6:
                            bResult = m485.Send6MSG((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr,
                                  (ushort)aData, bLocksp) ;
                            break;
                        case 16:
                            bResult = m485.Send16MSG((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr,
                                 (ushort)tempComand.DataLongth, Datas, bLocksp);
                            break; 
                    } 
                    break;
                case 1:
                    break;
                default:
                    break;
            }
            return bResult;
        }

        //设置一个1个Short的浮点数
        public bool SetSysData(int aDataIndex, float aData,bool bLockcp)
        {
            bool bResult = false;
            ModbusCommand tempComand = ComList[aDataIndex];
            short sData = (short)(aData / tempComand.Coefficient);
            //if(tempComand.DataLongth==1)
            short[] Datas = { sData };

            switch (comType)////0:modbus485;1udp;2TCP Clint;3TCP Server
            {
                case 0:
                    bResult = m485.Send16MSG((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr,
                                 (ushort)tempComand.DataLongth, Datas, bLockcp);
                    break;
                case 1:
                    break;
                default:
                    break;
            }
            return bResult;
        }

        //设置一个2个Short的浮点数
        public bool SetSysData(int aDataIndex, double aData,bool bLocksp)
        {
            bool bResult = false;
            ModbusCommand tempComand = ComList[aDataIndex];
            Int32 dData = (Int32)(aData / tempComand.Coefficient);
            short[] Datas = new short[2];
            Datas[0] = (short)(dData >> 16);
            Datas[1] = (short)(dData);

            switch (comType)////0:modbus485;1udp;2TCP Clint;3TCP Server
            {
                case 0:
                    bResult = m485.Send16MSG((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr,
                                 (ushort)tempComand.DataLongth, Datas, bLocksp);
                    break;
                case 1:
                    break;
                default:
                    break;
            }
            return bResult;
        }

        //设置电表尖峰平谷数据
        //设置一个bytes数组
        public bool SetSysBytes(int aDataIndex, byte[] aData,bool bLocksp)
        {
            bool bResult = false;
            ModbusCommand tempComand = ComList[aDataIndex];

            switch (comType)////0:modbus485;1udp;2TCP Clint;3TCP Server
            {
                case 0:
                    //if(tempComand.ComType==5)
                    //    bResult = m485.Send5MSG((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr,
                    //                                     aData);
                    //else
                    if (tempComand.ComType == 6)
                        bResult = m485.Send6MSG((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr,
                                 aData, bLocksp);
                    else
                        bResult = m485.Send16MSG((byte)eID, (byte)tempComand.ComType, (ushort)tempComand.DataAddr,
                                 (ushort)tempComand.DataLongth, aData, bLocksp);

                    break;
                case 1:
                    break;
                default:
                    break;
            }
            return bResult;
        }
        public void RecodChargeinform(int level)
        {
            DateTime dtTemp = DateTime.Now;
            string msg = "";
            string sql = "";
            switch (level)
            {
                case 1:
                    msg = "充电1级告警";
                    break;
                case 2:
                    msg = "充电2级告警";
                    break;
                case 3:
                    msg = "充电3级告警";
                    break;
                case 4:
                    msg = "放电1级告警";
                    break;
                case 5:
                    msg = "放电2级告警";
                    break;
                case 6:
                    msg = "放电3级告警";
                    break;
            }
            if (msg != "")
            {
                if (level < 4)
                {
                    sql = "insert into chargeinform (cellIDMaxtemp, cellMaxTemp, cellIDMaxV, cellMaxV, BMSa, Time, Warning) "
                                    + "values ('" + frmMain.Selffrm.AllEquipment.BMS.cellIDMaxtemp.ToString() + "','" + frmMain.Selffrm.AllEquipment.BMS.cellMaxTemp.ToString() + "','"
                                    + frmMain.Selffrm.AllEquipment.BMS.cellIDMaxV.ToString() + "','" + frmMain.Selffrm.AllEquipment.BMS.cellMaxV.ToString() + "','" + frmMain.Selffrm.AllEquipment.BMS.a.ToString()
                                    + "','" + dtTemp.ToString("yyyy-MM-dd HH:mm:ss") + "','" + msg + "')";
                }
                else
                {
                    sql = "insert into chargeinform (cellIDMaxtemp, cellMaxTemp, cellIDMinV, cellMinV, BMSa, Time, Warning) "
                                   + "values ('" + frmMain.Selffrm.AllEquipment.BMS.cellIDMaxtemp.ToString() + "','" + frmMain.Selffrm.AllEquipment.BMS.cellMaxTemp.ToString() + "','"
                                   + frmMain.Selffrm.AllEquipment.BMS.cellIDMinV.ToString() + "','" + frmMain.Selffrm.AllEquipment.BMS.cellMinV.ToString() + "','" + frmMain.Selffrm.AllEquipment.BMS.a.ToString()
                                   + "','" + dtTemp.ToString("yyyy-MM-dd HH:mm:ss") + "','" + msg + "')";
                }

                if (sql != "")
                {
                    DBConnection.ExecSQL(sql);
                }

            }
        }
        //处理一个故障
        public void RecodError(string awClass, string aeID, int aWaringID, int awLevels, string aWarning, bool aError)
        {
            DateTime dtTemp = DateTime.Now;
            //删除清理数据库
            string strSQL = "delete   from errorstate";
           DBConnection.ExecSQL(strSQL);
            //
            frmMain.Selffrm.ErrorGridFreshCount = 0;
            //qiao 保存所有的故障设备的值
            strSQL = "INSERT INTO errorstate(rTime,LCError1,LCError2,TCError,PCSError1,PCSError2,PCSError3,PCSError4,PCSError5,"
                + "PCSError6,PCSError7,PCSError8,BMSError1,BMSError2,BMSError3,BMSError4,BMSError5,EMSError1,EMSError2,EMSError3,EMSError4) VALUES('"
                + dtTemp.ToString("yyyy-MM-dd HH:mm:ss") + "','";
            if (frmMain.Selffrm.AllEquipment.LiquidCool != null)
                strSQL += frmMain.Selffrm.AllEquipment.LiquidCool.Error[0].ToString() + "','" + frmMain.Selffrm.AllEquipment.LiquidCool.Error[1].ToString() + "','";
            else
                strSQL += "0','0','";
            if (frmMain.Selffrm.AllEquipment.TempControl!=null)
                strSQL += frmMain.Selffrm.AllEquipment.TempControl.error.ToString() + "','";
            else
                strSQL += "0','";
            //PCS  精石只有4个Error位
            if (frmMain.Selffrm.AllEquipment.PCSList.Count > 0)
                strSQL += frmMain.Selffrm.AllEquipment.PCSList[0].Error[0].ToString() + "','"
                    + frmMain.Selffrm.AllEquipment.PCSList[0].Error[1].ToString() + "','"
                    + frmMain.Selffrm.AllEquipment.PCSList[0].Error[2].ToString() + "','"
                    + frmMain.Selffrm.AllEquipment.PCSList[0].Error[3].ToString() + "','"
                    + frmMain.Selffrm.AllEquipment.PCSList[0].Error[4].ToString() + "','"
                    + frmMain.Selffrm.AllEquipment.PCSList[0].Error[5].ToString() + "','"
                    + frmMain.Selffrm.AllEquipment.PCSList[0].Error[6].ToString() + "','"
                    + frmMain.Selffrm.AllEquipment.PCSList[0].Error[7].ToString() + "','";
            else
                strSQL += "0','0','0','0','0','0','0','0','";
            //BMS
            if (frmMain.Selffrm.AllEquipment.BMS!=null)
                strSQL += frmMain.Selffrm.AllEquipment.BMS.Error[0].ToString() + "','"
                    + frmMain.Selffrm.AllEquipment.BMS.Error[1].ToString() + "','"
                    + frmMain.Selffrm.AllEquipment.BMS.Error[2].ToString() + "','"
                    + frmMain.Selffrm.AllEquipment.BMS.Error[3].ToString() + "','"
                    + frmMain.Selffrm.AllEquipment.BMS.Error[4].ToString() + "','";
            else
                strSQL += "0','0','0','0','0','";

            strSQL += frmMain.Selffrm.AllEquipment.EMSError[0].ToString() + "','"
                   + frmMain.Selffrm.AllEquipment.EMSError[1].ToString() + "','"
                   + frmMain.Selffrm.AllEquipment.EMSError[2].ToString() + "','"
                   + frmMain.Selffrm.AllEquipment.EMSError[3].ToString()   + "')";
            DBConnection.ExecSQL(strSQL);

            Parent.Fault2Cloud.timestamp = dtTemp;
            Parent.Fault2Cloud.iotCode = aeID;
            Parent.Fault2Cloud.faultCode = "E00021";// "E" + aWaringID.ToString();
            Parent.Fault2Cloud.faultLevel = awLevels;
            Parent.Fault2Cloud.faultName = awClass+ aWarning+"("+aWaringID.ToString()+")";

            //保存当前的故障信息
            if (!aError)//aError = 0 : 故障恢复
            {
                Parent.Fault2Cloud.faultName += "恢复";
                DBConnection.ExecSQL("UPDATE warning SET  ResetTime='" + dtTemp.ToString("yyyy-MM-dd HH:mm:ss") + "' " +
                   " where wClass='" + awClass + "' and eID='" + aeID
                    + "'and WaringID='" + aWaringID.ToString() + "' and  wLevels='" + awLevels.ToString()
                    + "'and Warning='" + aWarning + "' and ResetTime IS NULL");
                Parent.Fault2Cloud.faultBack = true;
                if (awLevels == 2)
                {
                    Parent.LedErrorState[1] = false;
                }
            }
            else
            {
                if (!DBConnection.CheckRec("select ID from warning where wClass='" + awClass + "' and eID='" + aeID
                    + "'and WaringID='" + aWaringID.ToString() + "' and  wLevels='" + awLevels.ToString() + "'and Warning='" + aWarning
                    + "' and ResetTime IS NULL"))
                {
                    DBConnection.ExecSQL("INSERT INTO warning(rTime,wClass,eID,WaringID,wLevels,Warning) VALUES('" +
                        dtTemp.ToString("yyyy-MM-dd HH:mm:ss") + "','" + awClass + "','" + aeID + "','" +
                         aWaringID.ToString() + "','" + awLevels.ToString() + "','" + aWarning + "')");
                    Parent.Fault2Cloud.faultBack = false;
                    //设置故障指示灯 qiao
                    if (awLevels == 3)
                    {
                        lock (Parent.ErrorState)
                        {
                            Parent.ErrorState[2] = true;
                            //Parent.LedErrorState[2] = true;

                            //powerOff
                            //frmSet.PCSMOff();

                            //取消蜂鸣器
                            //SysIO.SetGPIOState(11, 0);
                        }
                    }
                    if (awLevels == 2)
                    {
                        Parent.LedErrorState[1] = true;
                    }

                }
            }
            if (frmSet.EMSstatus == 1)
                Parent.Report2Cloud.SaveFault2Cloud(dtTemp.ToString("yyMMddHHmmss"));
        }
    }


    /// <summary>
    /// ///////////////////////////////////////////////////////////////////////////////////////////////  
    /// 
    /// 
    /// 
    /// 
    /// </summary>
    /// 

    //新增计时器6.22
    public class TimeMeasurement
    {
        private Stopwatch stopwatch;

        public TimeMeasurement()
        {
            stopwatch = new Stopwatch();
        }

        public void RestartMeasurement()
        {
            // 重新启动计时器  
            stopwatch.Restart();
        }

        public double MeasureIntervalInSeconds()
        {
            // 返回从计时器启动到现在的时间间隔，单位秒  
            return stopwatch.Elapsed.TotalSeconds;
        }
    }

    //5.05 新增除湿机
    public class DehumidifierClass : BaseEquipmentClass
    {
        public double TempData;                  //温度-40---80，浮点型
        public double HumidityData;              //湿度0-100RH，浮点型
        public double WorkStatus;                //工作状态           
        public double TempData_Boot;             //温度启动值 LCSetHotTemp
        public double TempData_Stop;             //温度停止值	
        public double HumidityData_Boot;         //湿度启动值
        public double HumidityData_Stop;         //湿度停止值 
        public DehumidifierClass()
        {
            strCommandFile = "Dehumidifier.txt";
        }
        //导入配置
        public bool ExecCommand()
        {
            try
            {
                lock (this.m485.sp)
                {
                    SetSysData(7, (short)frmSet.DHSetTempBoot, false);
                    SetSysData(8, (short)frmSet.DHSetTempStop, false);
                    SetSysData(9, (short)frmSet.DHSetHumidityBoot, false);
                    SetSysData(10, (short)frmSet.DHSetHumidityStop, false);
                    if ((short)frmSet.DHSetRunStatus == 0)
                    {
                        SetSysData(11, (short)frmSet.DHSetRunStatus, false);
                    }
                    else {
                        SetSysData(11, 0X00FF, false);
                    }
                }
                return true;
            }
            catch
            { return false; }

        }

        public void GetSetDataFromEquipment()
        { 
            
        }


        override public void GetDataFromEqipment()
        {
            string strData = "";
            bool bPrepared = false;
            bool tempError = false;
            string strTemp = "";

            if (GetSysData(12, ref strData))
            {
                bPrepared = true;
                if (Get3strData(0, ref strData, ref strTemp))
                {
                    TempData = Convert.ToInt32(strTemp);            //温度
                }
                if (Get3strData(1, ref strData, ref strTemp))
                {
                    HumidityData = Convert.ToInt32(strTemp);            //湿度
                }
                if (Get3strData(2, ref strData, ref strTemp))
                {
                    WorkStatus = Convert.ToInt32(strTemp);            //工作状态   
                }
                if (Get3strData(3, ref strData, ref strTemp))
                {
                    TempData_Boot = Convert.ToInt32(strTemp);            //温度启动值
                }
                if (Get3strData(4, ref strData, ref strTemp))
                {
                    TempData_Stop = Convert.ToInt32(strTemp);            //温度停止值
                }
                if (Get3strData(5, ref strData, ref strTemp))
                {
                    HumidityData_Boot = Convert.ToInt32(strTemp);        //湿度启动值
                }
                if (Get3strData(6, ref strData, ref strTemp))
                {
                    HumidityData_Stop = Convert.ToInt32(strTemp);        //湿度停止值
                }
            }
            Prepared = bPrepared;
        }
        public bool SetSysData()
        {
            try
            {
                {
                    SetSysData(7, (short)frmSet.DHSetTempBoot, true);
                    SetSysData(8, (short)frmSet.DHSetTempStop, true);
                    SetSysData(9, (short)frmSet.DHSetHumidityBoot, true);
                    SetSysData(10, (short)frmSet.DHSetHumidityStop, true);
                }
                return true;
            }
            catch
            { return false; }
        }

    }
    //5.05 新增led
    public class LEDClass : BaseEquipmentClass
    {
        public int Led_on  = 0xFFFF;
        public int Led_blink = 0xCCCC;
        public int Led_off  = 0x0000;
        public LEDClass()
        {
            strCommandFile = "LED.txt";
        }
        public void SetLEDRun(int Op)
        {
            SetSysData(0, Op, true);
        }
        public void SetLEDWarn(int Op)
        {
            SetSysData(1, Op, true);
        }
        public void SetLEDError(int Op)
        {
            SetSysData(2, Op, true);
        }
        public void Set_LED(int num, int Op)
        {
            SetSysData(num, Op, true);
        }
        public void SetLEDE1(int Op)
        {
            SetSysData(3, Op, true);
        }
        public void SetLEDE2(int Op)
        {
            SetSysData(4, Op, true);
        }
        public void SetLEDE3(int Op)
        {
            SetSysData(5, Op, true);
        }
        public void SetLEDE4(int Op)
        {
            SetSysData(6, Op, true);
        }
        public void SetLEDE5(int Op)
        {
            SetSysData(7, Op, true);
        }
        public void SetBuzzer(int Op)
        {
            SetSysData(8, Op, true);
        }
        public void SetButteryPercentOff()      //灯板电量指示灯全部关闭
        {
            SetLEDE1(Led_off);
            SetLEDE2(Led_off);
            SetLEDE3(Led_off);
            SetLEDE4(Led_off);
            SetLEDE5(Led_off);
        }
        public void SetButteryPercentBlink()   //灯板电量指示灯全部闪烁
        {
            SetLEDE1(Led_blink);
            SetLEDE2(Led_blink);
            SetLEDE3(Led_blink);
            SetLEDE4(Led_blink);
            SetLEDE5(Led_blink);
        }

        /**************************设置灯板电量显示*******************************/
        public void SetButteryPercent(int Op)       //灯板显示待机电量
        {
            //for (int i = 0; i < 5; i++)
            //{
            //    Set_LED(i + 3, i <= (Op+20) / 20 ? Led_on : Led_off);
            //}
            switch(Op)
            {
                case 0:
                    SetLEDE5(Led_off);
                    SetLEDE4(Led_off);
                    SetLEDE3(Led_off);
                    SetLEDE2(Led_off);
                    SetLEDE1(Led_off);
                    break;
                case 1:
                    SetLEDE5(Led_off);
                    SetLEDE4(Led_off); 
                    SetLEDE3(Led_off);
                    SetLEDE2(Led_off);
                    SetLEDE1(Led_on);
                    break;
                case 2:
                    SetLEDE5(Led_off);
                    SetLEDE4(Led_off);
                    SetLEDE3(Led_off);
                    SetLEDE2(Led_on);
                    SetLEDE1(Led_on);
                    break;
                case 3:
                    SetLEDE5(Led_off);
                    SetLEDE4(Led_off);
                    SetLEDE3(Led_on);
                    SetLEDE2(Led_on);
                    SetLEDE1(Led_on);
                    break;
                case 4:
                    SetLEDE5(Led_off);
                    SetLEDE4(Led_on);
                    SetLEDE3(Led_on);
                    SetLEDE2(Led_on);
                    SetLEDE1(Led_on);
                    break;
                case 5:
                    SetLEDE5(Led_on);
                    SetLEDE4(Led_on);
                    SetLEDE3(Led_on);
                    SetLEDE2(Led_on);
                    SetLEDE1(Led_on);
                    break;

            }



        }
        public void SetChargeButteryPercent(int Op)  //灯板显示充电电量
        {
            switch (Op)
            {
                case 0:
                    SetLEDE5(Led_off);
                    SetLEDE4(Led_off);
                    SetLEDE3(Led_off);
                    SetLEDE2(Led_off);
                    SetLEDE1(Led_off);
                    break;
                case 1:
                    SetLEDE5(Led_off);
                    SetLEDE4(Led_off);
                    SetLEDE3(Led_off);
                    SetLEDE2(Led_off);
                    SetLEDE1(Led_blink);
                    break;
                case 2:
                    SetLEDE5(Led_off);
                    SetLEDE4(Led_off);
                    SetLEDE3(Led_off);
                    SetLEDE2(Led_blink);
                    SetLEDE1(Led_on);
                    break;
                case 3:
                    SetLEDE5(Led_off);
                    SetLEDE4(Led_off);
                    SetLEDE3(Led_blink);
                    SetLEDE2(Led_on);
                    SetLEDE1(Led_on);
                    break;
                case 4:
                    SetLEDE5(Led_off);
                    SetLEDE4(Led_blink);
                    SetLEDE3(Led_on);
                    SetLEDE2(Led_on);
                    SetLEDE1(Led_on);
                    break;
                case 5:
                    SetLEDE5(Led_blink);
                    SetLEDE4(Led_on);
                    SetLEDE3(Led_on);
                    SetLEDE2(Led_on);
                    SetLEDE1(Led_on);
                    break;

            }




        }

        /**************************设置灯板休眠*******************************/
        public void Set_Led_Sleep()
        {
            SetLEDRun(0xFF00);
        }
        /**************************设置灯板关机*******************************/
        public void Set_Led_ShutDown()
        {
            SetButteryPercentOff();
            SetLEDRun(Led_off);
            SetLEDWarn(Led_off);
            SetLEDError(Led_off);
        }


        /*********************设置灯板待机 ***** 告警**************************/
        public void Set_Led_Standby_N( )
        {
            SetLEDRun(Led_on);
            SetLEDWarn(Led_off);
            SetLEDError(Led_off);
        }
        public void Set_Led_Standby_W()
        {
            SetLEDRun(Led_off);
            SetLEDWarn(Led_on);
            SetLEDError(Led_off);
        }
        public void Set_Led_Standby_E( )
        {
            SetLEDRun(Led_off);
            SetLEDWarn(Led_off);
            SetLEDError(Led_on);
        }


        /*********************设置灯板充电 ***** 告警*************/
        public void Set_Led_Charge_N()
        {
            SetLEDRun(Led_blink);
            SetLEDWarn(Led_off);
            SetLEDError(Led_off);
        }
        public void Set_Led_Charge_W()
        {
            SetLEDRun(Led_off);
            SetLEDWarn(Led_on);
            SetLEDError(Led_off);
        }
        public void Set_Led_Charge_E()
        {
            SetLEDRun(Led_off);
            SetLEDWarn(Led_off);
            SetLEDError(Led_on);
        }
        /*********************设置灯板告警/故障*************************/
        public void Set_Led_Error_Poweron()
        {
            SetLEDRun(Led_off);
            SetLEDWarn(Led_blink);
            SetLEDError(Led_blink);
            SetButteryPercentOff();
        }
        public void Set_Led_Error_Other()
        {
            SetLEDRun(Led_off);
            SetLEDWarn(Led_on);
            SetLEDError(Led_on);
            SetButteryPercentOff();
        }
        public void Set_Led_Error_check()
        {
            SetLEDRun(Led_off);
            SetLEDWarn(Led_blink);
            SetLEDError(Led_blink);
            SetButteryPercentBlink();
        }



        public void Led_Control_Loop()
        {
            {
                //LED获取当前告警级别
                if (frmMain.Selffrm.AllEquipment.ErrorState[2] == true) frmMain.Selffrm.AllEquipment.Led_ShowError = 2; //三级告警
                else frmMain.Selffrm.AllEquipment.Led_ShowError = 0;

                //LED获取当前状态
                if (Math.Abs(frmMain.Selffrm.AllEquipment.PCSList[0].allUkva) > 0.5) frmMain.Selffrm.AllEquipment.Led_Show_status = 1; //0 待机 1 运行 
                else frmMain.Selffrm.AllEquipment.Led_Show_status = 0;

                //LED获取当前电量等级
                frmMain.Selffrm.AllEquipment.Led_ShowPowerLevel = (((int)frmMain.Selffrm.AllEquipment.BMSSOC + 19) / 20);

                if (frmMain.Selffrm.AllEquipment.Prev_Led_Show_status != frmMain.Selffrm.AllEquipment.Led_Show_status)//运行状态改变
                {
                    if (frmMain.Selffrm.AllEquipment.Led_Show_status == 0)   //获取待机状态
                    {
                        switch (frmMain.Selffrm.AllEquipment.Led_ShowError)
                        {
                            case 0:
                                frmMain.Selffrm.AllEquipment.Led.Set_Led_Standby_N();
                                break;
                            case 1:
                                frmMain.Selffrm.AllEquipment.Led.Set_Led_Standby_W();
                                break;
                            case 2:
                                frmMain.Selffrm.AllEquipment.Led.Set_Led_Standby_E();
                                break;
                        }
                    }
                    if (frmMain.Selffrm.AllEquipment.Led_Show_status == 1)   //获取运行状态 
                    {
                        switch (frmMain.Selffrm.AllEquipment.Led_ShowError)
                        {
                            case 0:
                                frmMain.Selffrm.AllEquipment.Led.Set_Led_Charge_N();
                                break;
                            case 1:
                                frmMain.Selffrm.AllEquipment.Led.Set_Led_Charge_W();
                                break;
                            case 2:
                                frmMain.Selffrm.AllEquipment.Led.Set_Led_Charge_E();
                                break;
                        }
                    }
                    switch (frmMain.Selffrm.AllEquipment.Led_Show_status)   //显示电量 
                    {
                        case 0:
                            frmMain.Selffrm.AllEquipment.Led.SetButteryPercent(frmMain.Selffrm.AllEquipment.Led_ShowPowerLevel);
                            break;
                        case 1:
                            frmMain.Selffrm.AllEquipment.Led.SetChargeButteryPercent(frmMain.Selffrm.AllEquipment.Led_ShowPowerLevel);
                            break;
                    }
                }
                else//运行状态不变
                {
                    if (frmMain.Selffrm.AllEquipment.Prev_Led_ShowError != frmMain.Selffrm.AllEquipment.Led_ShowError)
                    {
                        if (frmMain.Selffrm.AllEquipment.Led_Show_status == 0)
                        {
                            switch (frmMain.Selffrm.AllEquipment.Led_ShowError)
                            {
                                case 0:
                                    frmMain.Selffrm.AllEquipment.Led.Set_Led_Standby_N();
                                    frmMain.Selffrm.AllEquipment.Led.SetButteryPercent(frmMain.Selffrm.AllEquipment.Led_ShowPowerLevel);
                                    break;
                                case 1:
                                    frmMain.Selffrm.AllEquipment.Led.Set_Led_Standby_W();
                                    frmMain.Selffrm.AllEquipment.Led.SetButteryPercent(frmMain.Selffrm.AllEquipment.Led_ShowPowerLevel);
                                    break;
                                case 2:
                                    frmMain.Selffrm.AllEquipment.Led.Set_Led_Standby_E();
                                    frmMain.Selffrm.AllEquipment.Led.SetButteryPercent(frmMain.Selffrm.AllEquipment.Led_ShowPowerLevel);
                                    break;
                            }
                        }
                        if (frmMain.Selffrm.AllEquipment.Led_Show_status == 1)
                        {
                            switch (frmMain.Selffrm.AllEquipment.Led_ShowError)
                            {
                                case 0:
                                    frmMain.Selffrm.AllEquipment.Led.Set_Led_Charge_N();
                                    frmMain.Selffrm.AllEquipment.Led.SetChargeButteryPercent(frmMain.Selffrm.AllEquipment.Led_ShowPowerLevel);
                                    break;
                                case 1:
                                    frmMain.Selffrm.AllEquipment.Led.Set_Led_Charge_W();
                                    frmMain.Selffrm.AllEquipment.Led.SetButteryPercentOff();
                                    break;
                                case 2:
                                    frmMain.Selffrm.AllEquipment.Led.Set_Led_Charge_E();
                                    frmMain.Selffrm.AllEquipment.Led.SetButteryPercentOff();
                                    break;
                            }
                        }

                    }
                    if (frmMain.Selffrm.AllEquipment.Prev_Led_ShowPowerLevel != frmMain.Selffrm.AllEquipment.Led_ShowPowerLevel)
                    {
                        switch (frmMain.Selffrm.AllEquipment.Led_Show_status)
                        {
                            case 0:
                                frmMain.Selffrm.AllEquipment.Led.SetButteryPercent(frmMain.Selffrm.AllEquipment.Led_ShowPowerLevel);
                                break;
                            case 1:
                                frmMain.Selffrm.AllEquipment.Led.SetChargeButteryPercent(frmMain.Selffrm.AllEquipment.Led_ShowPowerLevel);
                                break;
                        }
                    }
                }

                //状态同步
                frmMain.Selffrm.AllEquipment.Prev_Led_ShowError = frmMain.Selffrm.AllEquipment.Led_ShowError;
                frmMain.Selffrm.AllEquipment.Prev_Led_Show_status = frmMain.Selffrm.AllEquipment.Led_Show_status;
                frmMain.Selffrm.AllEquipment.Prev_Led_ShowPowerLevel = frmMain.Selffrm.AllEquipment.Led_ShowPowerLevel;
            }
        }
    }

    //pcs新增DSP2  11.27
    public class DSP2Class : BaseEquipmentClass
    {
        public int DSP2 = 1;//1：晶石
/*        public static string[] PCSStates = { "待机", "停机", "充电", "放电" };
        public static string[] PCSTypes = { "待机", "恒流", "恒压", "恒功率", "AC恒压", "自适应需量" };
        public static string[] PCSNetState = { "停机", "待机", "运行", "总故障状态", "总警告状态", "1远程/0就地状态", "急停输入状态", "并网", "离网", "过载降容" };*/
        public int State = 0;
        public double aV { get; set; }
        public double bV { get; set; }
        public double cV { get; set; }
        public double aA { get; set; }
        public double bA { get; set; }
        public double cA { get; set; }
        public double hz { get; set; }            //频率
        public double aUkva { get; set; }         //a有功功率
        public double bUkva { get; set; }
        public double cUkva { get; set; }
        public double allUkva { get; set; }        //总有功功率
        public double aNUkvar { get; set; }        //A无功功率
        public double bNUkvar { get; set; }
        public double cNUkvar { get; set; }
        public double allNUkvar { get; set; }
        public double aAkva { get; set; }              //A视在功率   
        public double bAkva { get; set; }
        public double cAkva { get; set; }
        public double allAkva { get; set; }             //总视在功率
        public double aPFactor { get; set; }          //功率因数
        public double bPFactor { get; set; }
        public double cPFactor { get; set; }
        public double allPFactor { get; set; }
        public double inputkva { get; set; }         //输入功率
        public double inputV { get; set; }            //输入电压
        public double inputA { get; set; }            //输入电流
        public double PCSTemp { get; set; }           //PCS温度
        public double ACInkwh { get; set; }           //交流充电量
        public double ACOutkwh { get; set; }          //交流放电量
        public double DCInkwh { get; set; }           //直流充电量
        public double DCOutkwh { get; set; }          //直流放电量
        public double DCInputV { get; set; }
        public double DCOutputV { get; set; }
        public double DCInputA { get; set; }
        public double DCOutputA { get; set; }
        public double DCDCInkw;           //DC输入功率 
        public double DCTemp;             //DC部分温度
        public double InTemp { get; set; }           //入口温度
        public double OutTemp { get; set; }             //出口温度
        public ushort[] Error { get; set; } = { 0, 0, 0, 0, 0, 0, 0, 0 };
        public ushort[] OldError = { 0, 0, 0, 0, 0, 0, 0, 0 };

        //11.16：PCS添加设备状态字
        public ushort PcsStatus = 0;

        public double IGBTTemp1 { get; set; }
        public double IGBTTemp2 { get; set; }
        public double IGBTTemp3 { get; set; }
        public double IGBTTemp4 { get; set; }
        public double IGBTTemp5 { get; set; }
        public double IGBTTemp6 { get; set; }
        public DSP2Class(int aDSP)
        {
            DSP2 = aDSP;
            switch (DSP2)
            {
                case 0:
                    strCommandFile = "PCS1.txt";
                    break;
                case 1:
                    strCommandFile = "DSP2.txt"; //对应精石PCS
                    break;
            }
        }



        /// <summary>
        /// 读取
        /// </summary>
        override public void GetDataFromEqipment()
        {//pcs
            //Thread.Sleep(10);
            switch (DSP2)
            {
                case 1:
                    GetDataFromEqipment2();
                    break;
            }
        }

        /// <summary>
        /// 晶石pcs的DSP2
        /// </summary>
        public void GetDataFromEqipment2()
        {//DSP2

            string strTemp = "";
            string strData = "";
            bool bPrepared = false;


            if (GetSysData(37, ref strTemp))
            {
                bPrepared = true;
                if (Get3strData(0, ref strTemp, ref strData))
                {
                    aV = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2aV = aV;
                }
                if (Get3strData(1, ref strTemp, ref strData))
                {
                    bV = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2bV = bV;
                }
                if (Get3strData(2, ref strTemp, ref strData))
                {
                    cV = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2cV = cV;
                }
                if (Get3strData(3, ref strTemp, ref strData))
                {
                    aA = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2aA = aA;
                }
                if (Get3strData(4, ref strTemp, ref strData))
                {
                    bA = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2bA = bA;
                }
                if (Get3strData(5, ref strTemp, ref strData))
                {
                    cA = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2cA = cA;
                }
                if (Get3strData(6, ref strTemp, ref strData))
                {
                    allUkva = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2allUkva = allUkva;
                }
                if (Get3strData(7, ref strTemp, ref strData))
                {
                    allNUkvar = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2allNUkvar = allNUkvar;
                }
                if (Get3strData(8, ref strTemp, ref strData))
                {
                    allAkva = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2allAkva = allAkva;
                }
                if (Get3strData(9, ref strTemp, ref strData))
                {
                    hz = Math.Round(float.Parse(strData), 2);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2hz = hz;
                }
                if (Get3strData(10, ref strTemp, ref strData))
                {
                    allPFactor = Math.Round(float.Parse(strData), 3);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2allPFactor = allPFactor;
                }
                if (Get3strData(11, ref strTemp, ref strData))
                {
                    inputV = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2inputV = inputV;
                }
                if (Get3strData(12, ref strTemp, ref strData))
                {
                    inputA = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2inputA = inputA;
                }
                if (Get3strData(13, ref strTemp, ref strData))
                {
                    inputkva = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2inputkva = inputkva;
                }
            }
            if (GetSysData(38, ref strTemp))
            {
                bPrepared = true;
                double dTemp = 0;
/*                //1
                if (Get3strData(14, ref strTemp, ref strData))
                {
                    PCSTemp = Math.Round(float.Parse(strData), 1);
                    if (PCSTemp > 150)
                        PCSTemp = 0;
                    IGBTTemp1 = PCSTemp;
                }
                //2
                if (Get3strData(15, ref strTemp, ref strData))
                {
                    dTemp = Math.Round(float.Parse(strData), 1);
                    if ((dTemp > PCSTemp) && (PCSTemp < 150))
                        PCSTemp = dTemp;
                    IGBTTemp2 = PCSTemp;
                }
                //3
                if (Get3strData(16, ref strTemp, ref strData))
                {
                    dTemp = Math.Round(float.Parse(strData), 1);
                    if ((dTemp > PCSTemp) && (PCSTemp < 150))
                        PCSTemp = dTemp;
                    IGBTTemp3 = PCSTemp;
                }*/
                //4
                if (Get3strData(17, ref strTemp, ref strData))
                {
                    dTemp = Math.Round(float.Parse(strData), 1);
                    if ((dTemp > PCSTemp) && (PCSTemp < 150))
                        PCSTemp = dTemp;
                    IGBTTemp4 = PCSTemp;
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2IGBTTemp4 = IGBTTemp4;
                }
                //5
                if (Get3strData(18, ref strTemp, ref strData))
                {
                    dTemp = Math.Round(float.Parse(strData), 1);
                    if ((dTemp > PCSTemp) && (PCSTemp < 150))
                        PCSTemp = dTemp;
                    IGBTTemp5 = PCSTemp;
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2IGBTTemp5 = IGBTTemp5;
                }
                //6
                if (Get3strData(19, ref strTemp, ref strData))
                {
                    dTemp = Math.Round(float.Parse(strData), 1);
                    if ((dTemp > PCSTemp) && (PCSTemp < 150))
                        PCSTemp = dTemp;
                    IGBTTemp6 = PCSTemp;
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2IGBTTemp6 = IGBTTemp6;
                }

                if (Get3strData(20, ref strTemp, ref strData))
                {
                    InTemp = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2InTemp = InTemp;
                }
                if (Get3strData(21, ref strTemp, ref strData))
                {
                    OutTemp = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2OutTemp = OutTemp;
                }
            }
            //
            if (GetSysData(58, ref strTemp))
            {
                bPrepared = true;
                if (Get3strData(22, ref strTemp, ref strData))
                {
                    aUkva = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2aUkva = aUkva;
                }
                if (Get3strData(23, ref strTemp, ref strData))
                {
                    bUkva = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2bUkva = bUkva;
                }
                if (Get3strData(24, ref strTemp, ref strData))
                {
                    cUkva = Math.Round(float.Parse(strData), 1);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2cUkva = cUkva;
                }
                if (Get3strData(25, ref strTemp, ref strData))
                {
                    DCInputV = Math.Round(float.Parse(strData), 3);
                    frmMain.Selffrm.AllEquipment.PCSList[0].DSP2DCInputV = DCInputV;
                }
            }

            /*            if (GetSysData(39, ref strTemp))
                        {
                            bPrepared = true;
                            //状态
                            //Get3strData(26, ref strTemp, ref strData);
                            if (Get3strData(27, ref strTemp, ref strData))
                                Error[0] = Convert.ToUInt16(strData);
                            if (Get3strData(28, ref strTemp, ref strData))
                                Error[1] = Convert.ToUInt16(strData);
                            if (Get3strData(29, ref strTemp, ref strData))
                                Error[2] = Convert.ToUInt16(strData);
                            if (Get3strData(30, ref strTemp, ref strData))
                                Error[3] = Convert.ToUInt16(strData);
                        }*/

            //11.16 : DSP2添加设备状态字
            /*            if (GetSysData(26, ref strTemp))
                        {
                            bPrepared = true;
                            PcsStatus = Convert.ToUInt16(strTemp);
                        }*/



            //Parent.waValue=valu
            /*            Prepared = bPrepared;
                        if (!Prepared)//PCS发生通讯异常
                        {
                            lock (Parent.EMSError)
                            {
                                Parent.EMSError[0] &= 0xFFEF;
                                Parent.EMSError[0] |= 0x10;
                            }
                        }
                        else
                        {
                            Parent.EMSError[0] &= 0xFFEF;
                        }
                        *//*
                        if (allAkva == 0)
                        {
                            State = 0;
                        }
                        else*//*
                        //if (inputA == 0)"待机", "停机", "充电", "放电" 4bit5bit

                        if (inputA > 0.5)
                            State = 3;
                        else if (inputA < -0.5)
                            State = 2;
                        else
                            State = 0;

                        time = DateTime.Now;
                        //设置运行指示灯
                        if (State > 0)
                        {
                            SysIO.SetGPIOState(9, 0);
                        }
                        else
                        {
                            SysIO.SetGPIOState(9, 1);
                        }


                        //处理故障
                        ushort sData = 0;
                        ushort sOldData = 0;
                        ushort sKey = 0;
                        int iData;

                        //检查故障，并对比过去的故障 
                        for (int i = 0; i < 4; i++)
                        {
                            sData = Error[i];
                            sOldData = OldError[i];
                            if (sData != sOldData)
                            {
                                sOldData = (ushort)(sOldData ^ sData);
                                for (int j = 0; j < 16; j++)
                                {
                                    sKey = (ushort)(1 << (15-j));
                                    iData = sOldData & sKey;
                                    if ((iData > 0) && (ErrorClass.PCSErrorsPower2[16 * i + j] > 0))
                                    {
                                        RecodError("DSP2", iot_code, 16 * i + j, ErrorClass.PCSErrorsPower2[16 * i + j], ErrorClass.PCSErrors2[16 * i + j], (sData & sKey) > 0);
                                        //11.16 PCS告警捕捉
                                        string info = BitConverter.ToString(frmMain.Selffrm.AllEquipment.PCSList[0].WarnMessage);
                                        log.Error("DSP2告警发生或告警恢复报文：" + info);
                                        log.Error("DSP2设备状态字：" + frmMain.Selffrm.AllEquipment.PCSList[0].PcsStatus);
                                    }
                                }
                            }
                            OldError[i] = Error[i];
                        }*/
        } //GetDataFromEqipment2()


        override public void Save2DataSource(string arDate)
        {
            //基本信息
/*            DBConnection.ExecSQL("insert DSP2 (rTime, State,aV, bV, cV, aA ,bA , cA , hz ,"
                + " aUkwa,  bUkwa,  cUkwa,  allUkwa,   aNUkwr, bNUkwr,   cNUkwr, allNUkwr, "
                + " aAkwa,  bAkwa,  cAkwa,  allAkwa, aPFactor, bPFactor,   cPFactor,  allPFactor,"
                + " inputPower, inputV,  inputA, PCSTemp, "
                + " ACInkwh,ACOutkwh,DCInkwh,DCOutkwh,"
                + "Error1,Error2,Error3,Error4,Error7 )value('"
                 + arDate + "','" + State.ToString() + "','"  // rTime.ToString("yyyy-M-d H:m:s")
                 + aV.ToString() + "','" + bV.ToString() + "','" + cV.ToString() + "','"
                 + aA.ToString() + "','" + bA.ToString() + "','" + cA.ToString() + "','" + hz.ToString() + "','"
                 + aUkva.ToString() + "','" + bUkva.ToString() + "','" + cUkva.ToString() + "','" + allUkva.ToString() + "','"
                 + aNUkvar.ToString() + "','" + bNUkvar.ToString() + "','" + cNUkvar.ToString() + "','" + allNUkvar.ToString() + "','"
                 + aAkva.ToString() + "','" + bAkva.ToString() + "','" + cAkva.ToString() + "','" + allAkva.ToString() + "','"
                 + aPFactor.ToString() + "','" + bPFactor.ToString() + "','" + cPFactor.ToString() + "','" + allPFactor.ToString() + "','"
                 + inputkva.ToString() + "','" + inputV.ToString() + "','" + inputA.ToString() + "','" + PCSTemp.ToString() + "','"
                 + ACInkwh.ToString() + "','" + ACOutkwh.ToString() + "','" + DCInkwh.ToString() + "','" + DCOutkwh.ToString() + "','"
                + Error[0].ToString() + "','" + Error[1].ToString() + "','" + Error[2].ToString() + "','"
                + Error[3].ToString() + "','" + Error[7].ToString() + "')");*/
        }

    }
    //液冷机
    public class LiquidCoolClass : BaseEquipmentClass
    {
        //运行数据
        public double environmentTemp { get; set; }  //环境温度
        public double OutwaterTemp { get; set; }     //出水温度
        public double InwaterTemp { get; set; }     //回水温度
        public double ExgasTemp { get; set; }       //排气温度
        public double InwaterPressure { get; set; } //进水压力
        public double OutwaterPressure { get; set; } //出水压力

        public string ProtocolVersion { get; set; } //设备协议版本

        //设置数据
        public int LCModel { get; set; } //运行模式
        public ushort[] Error { get; set; } = { 0, 0 };
        public ushort[] OldError = { 0, 0  };
        ushort errorTemp = 0;
        public int state { get; set; }  //开关状态   开机1 关机0
        public bool PowerOn = false;
        public int TemperSelect; //控制温度选择
       public int WaterPump = 0;   //水泵选择

        private static ILog log = LogManager.GetLogger("LiquidCoolClass");

        DateTime oldTemp;
        DateTime newTemp;
        int count = 0;
        public double CoolTemp; //水温制冷点
        public double HotTemp;  //水温制热点
        public double CoolTempReturn;   //制冷回差
        public double HotTempReturn;    //制热回差
        public LiquidCoolClass()
        {
            strCommandFile = "LiquidCool.txt";
        }

        //导入配置
        public bool ExecCommand()
        {
            try
            {
                lock (this.m485.sp)
                {
                    SetSysData(1, frmSet.LCModel, false);
                    SetSysData(2, frmSet.LCTemperSelect, false);
                    SetSysData(3, (short)frmSet.LCSetCoolTemp, false);
                    SetSysData(4, (short)frmSet.LCSetHotTemp, false);
                    SetSysData(5, (short)frmSet.LCCoolTempReturn, false);
                    SetSysData(6, (short)frmSet.LCHotTempReturn, false);
                    SetSysData(7, frmSet.LCWaterPump, false);
                }
                return true;
            }
            catch
            { return false; }

        }

        /// <summary>
        /// 开关液冷机  1：开机 0：关机  （默认1）
        /// </summary>
        /// <param name="aPowerOn"></param>
        public void LCPowerOn(bool aACOn)
        {
            PowerOn = aACOn;
            try
            {
                if (aACOn)
                {
                    SetSysData(0, 0x0001, true);
                }
                else
                {
                    SetSysData(0, 0x0000, true);
                }
            }
            finally
            {
            }
        }


        /// <summary>
        /// 复位故障码
        /// </summary>
        /*        public void LCCleanError()
                {
                    SetSysData(25, 0xff00, true);//01/05 
                }*/


        public void GetSetDataFromEquipment()
        {
            string strData = "";
            string strTemp = "";
            bool bPrepared = false;
            //读取设备信息
            if (GetSysData(36, ref strData))
            {
                bPrepared = true;
                if (Get3strData(28, ref strData, ref strTemp))
                {
                    state = Convert.ToInt32(strTemp);
                }
                if (Get3strData(29, ref strData, ref strTemp))
                {
                    LCModel = Convert.ToInt32(strTemp);
                }
                if (Get3strData(30, ref strData, ref strTemp))
                {
                    TemperSelect =  Convert.ToInt32(strTemp);
                }

            }
            //读取设备信息
            if (GetSysData(37, ref strData))
            {
                bPrepared = true;
                if (Get3strData(31, ref strData, ref strTemp))
                {
                    CoolTemp = Math.Round(float.Parse(strTemp), 1);
                }
                if (Get3strData(32, ref strData, ref strTemp))
                {
                    HotTemp = Math.Round(float.Parse(strTemp), 1);
                }
                if (Get3strData(33, ref strData, ref strTemp))
                {
                    CoolTempReturn = Math.Round(float.Parse(strTemp), 1);
                }
                if (Get3strData(34, ref strData, ref strTemp))
                {
                    HotTempReturn = Math.Round(float.Parse(strTemp), 1);
                }
                if (Get3strData(35, ref strData, ref strTemp))
                {
                    WaterPump =  Convert.ToInt32(strTemp);
                }

            }

            Prepared = bPrepared;
            if (!Prepared)
            {
                if (count < 10)
                {
                    count++;
                }
                if (count > 8)
                {
                    lock (Parent.EMSError)
                    {
                        Parent.EMSError[0] &= 0xDFFF;
                        Parent.EMSError[0] |= 0x2000;
                    }
                }
            }
            else
            {
                Parent.EMSError[0] &= 0xDFFF;
                count = 0;
            }
        }

        override public void GetDataFromEqipment()
        {
            string strData = "";
            string strTemp = "";
            bool bPrepared = false;
            //读取设备信息
            if (GetSysData(25, ref strData))
            {
                bPrepared = true;
                if (Get3strData(8, ref strData, ref strTemp))
                    OutwaterTemp = Math.Round(float.Parse(strTemp), 1);//出水温度 
                if (Get3strData(9, ref strData, ref strTemp))
                    InwaterTemp = Math.Round(float.Parse(strTemp), 1);    //回水温度
                if (Get3strData(10, ref strData, ref strTemp))
                    ExgasTemp = Math.Round(float.Parse(strTemp), 1);  //排气温度
                if (Get3strData(11, ref strData, ref strTemp))
                    environmentTemp = Math.Round(float.Parse(strTemp), 1);//环境温度
                if (Get3strData(12, ref strData, ref strTemp))
                    InwaterPressure = Math.Round(float.Parse(strTemp), 1);  //进水压力
                if (Get3strData(13, ref strData, ref strTemp))
                    OutwaterPressure = Convert.ToInt32(strTemp);        //出水压力
            }
            //读取故障
            if (GetSysData(26, ref strTemp))
            {
                bPrepared = true;
                //状态
                //Get3strData(26, ref strTemp, ref strData);
                if (Get3strData(14, ref strTemp, ref strData))  //出水高温
                {
                    errorTemp = Convert.ToUInt16(strData);
                    if (errorTemp == 0) //0正常 ，1告警
                    {
                        Error[0] &= 0xFFFE;
                    }
                    else
                    {
                        Error[0] |= 0x0001;
                    }
                }
                if (Get3strData(15, ref strTemp, ref strData))//出水低温 
                {
                    errorTemp = Convert.ToUInt16(strData);
                    if (errorTemp == 0) //0正常 ，1告警
                    {
                        Error[0] &= 0xFFFD;
                    }
                    else
                    {
                        Error[0] |= 0x0002;
                    }
                }
                if (Get3strData(16, ref strTemp, ref strData))//出水温感故障
                {
                    errorTemp = Convert.ToUInt16(strData);
                    if (errorTemp == 0) //0正常 ，1告警
                    {
                        Error[0] &= 0xFFFC;
                    }
                    else
                    {
                        Error[0] |= 0x0004;
                    }
                }
                if (Get3strData(17, ref strTemp, ref strData))//回水温感故障
                {
                    errorTemp = Convert.ToUInt16(strData);
                    if (errorTemp == 0) //0正常 ，1告警
                    {
                        Error[0] &= 0xFFFB;
                    }
                    else
                    {
                        Error[0] |= 0x0008;
                    }
                }
                if (Get3strData(18, ref strTemp, ref strData)) //保留
                {
                    errorTemp = Convert.ToUInt16(strData);
                    if (errorTemp == 0) //0正常 ，1告警
                    {
                        Error[0] &= 0xFFFA;
                    }
                    else
                    {
                        Error[0] |= 0x0010;
                    }
                }
                if (Get3strData(19, ref strTemp, ref strData)) //变频器通讯故障
                {
                    errorTemp = Convert.ToUInt16(strData);
                    if (errorTemp == 0) //0正常 ，1告警
                    {
                        Error[0] &= 0xFFF9;
                    }
                    else
                    {
                        Error[0] |= 0x0020;
                    }
                }
                if (Get3strData(20, ref strTemp, ref strData))
                {
                    errorTemp = Convert.ToUInt16(strData);
                    if (errorTemp == 0) //0正常 ，1告警
                    {
                        Error[0] &= 0xFFF8;
                    }
                    else
                    {
                        Error[0] |= 0x0040;
                    }
                }
                if (Get3strData(21, ref strTemp, ref strData))
                {
                    errorTemp = Convert.ToUInt16(strData);
                    if (errorTemp == 0) //0正常 ，1告警
                    {
                        Error[0] &= 0xFFF7;
                    }
                    else
                    {
                        Error[0] |= 0x0080;
                    }
                }
                if (Get3strData(22, ref strTemp, ref strData))
                {
                    errorTemp = Convert.ToUInt16(strData);
                    if (errorTemp == 0) //0正常 ，1告警
                    {
                        Error[0] &= 0xFFF6;
                    }
                    else
                    {
                        Error[0] |= 0x0100;
                    }
                }
                if (Get3strData(23, ref strTemp, ref strData))
                {
                    errorTemp = Convert.ToUInt16(strData);
                    if (errorTemp == 0) //0正常 ，1告警
                    {
                        Error[0] &= 0xFFF5;
                    }
                    else
                    {
                        Error[0] |= 0x0200;
                    }
                }
                if (Get3strData(24, ref strTemp, ref strData))
                {
                    errorTemp = Convert.ToUInt16(strData);
                    if (errorTemp == 0) //0正常 ，1告警
                    {
                        Error[0] &= 0xFFF4;
                    }
                    else
                    {
                        Error[0] |= 0x0400;
                    }
                }

            }

            //读取设备开关状态
            if (GetSysData(27, ref strTemp))
            {
                state = Convert.ToInt32(strTemp);
                bPrepared = true;
            }

            //考虑到问询是稳定性，一个返回信息代表通讯成功
            Prepared = bPrepared;
            if (!Prepared)
            {
                lock (Parent.EMSError)
                {
                    Parent.EMSError[0] |= 0x2000;
                }
            }
            else
            {
                lock (Parent.EMSError)
                {
                    Parent.EMSError[0] &= 0xDFFF;
                }
            }

            //处理故障
            ushort sData = 0;
            ushort sOldData = 0;
            ushort sKey = 0;
            int iData;

            //检查故障，并对比过去的故障 
            int i = 0;
            {
                sData = Error[i];
                sOldData = OldError[i];
                if (sData != sOldData)
                {
                    sOldData = (ushort)(sOldData ^ sData);
                    for (int j = 0; j < 16; j++)
                    {
                        sKey = (ushort)(1 << (15-j));
                        iData = sOldData & sKey;
                        if ((iData > 0) && (ErrorClass.LCErrorPower[16 * i + j] > 0))
                            RecodError("LiquidCool", iot_code, 16 * i + j, ErrorClass.LCErrorPower[16 * i + j], ErrorClass.LCErrors[16 * i + j], (sData & sKey) > 0);
                    }
                }
                OldError[i] = Error[i];
            }
        }


        /// <summary>
        /// 保存液冷机的实时数据
        /// </summary>
        /// <param name="arDate"></param>
        override public void Save2DataSource(string arDate)
        {
            //基本信息
            DBConnection.ExecSQL("insert LiquidCool(rTime,state,OutwaterTemp,InwaterTemp,environmentTemp,ExgasTemp,"
                + "InwaterPressure,OutwaterPressure,Error1，Error2)value('"
                + time.ToString("yyyy-M-d H:m:s") + "','"
                + state.ToString() + "','"
                + OutwaterTemp.ToString() + "','"
                + InwaterTemp.ToString() + "','"
                + environmentTemp.ToString() + "','"
                + ExgasTemp.ToString() + "','"
                + InwaterPressure.ToString() + "','"
                + OutwaterPressure.ToString() + "','"
                + Error[0].ToString() + "','" 
                + Error[1].ToString() + "')");
        }

    }
    //UPS
    public class UPSClass : BaseEquipmentClass
    {
        public double v { get; set; } //电池电压
        public double a { get; set; } //电池电流

        public UPSClass() {
            strCommandFile = "ups.txt";
            Prepared = false;
        }

        override public void GetDataFromEqipment()
        {
            string strTemp = "";
            bool bPrepared = false;
            
            //UPS电池电压
            if (GetSysData(0, ref strTemp))
            {
                v = int.Parse(strTemp);
                bPrepared = true;
            }
            //UPS电池电流
            if (GetSysData(1, ref strTemp))
            {
                a = int.Parse(strTemp);
                bPrepared = true;
            }
            Prepared = bPrepared;

        }

        override public void Save2DataSource(string arDate)
        {
            //基本信息  
            DBConnection.ExecSQL("insert ups (rTime,V,A )"
                + "value('" + arDate + "','"// time.ToString("yyyy-M-d H:m:s") 
                + v.ToString() + "','"
                + a.ToString() + "','"
                +  "')");
            // + iot_code +//"','"
        }
    }
    //消防 
    public class FireClass : BaseEquipmentClass
    {
        public int FireState { get; set; }              //消防状态，0正常1已引爆
        public double Temp { get; set; }                //温度-40---80，浮点型
        public double Humidity { get; set; }            //湿度0-100RH，浮点型
        public int Waterlogging1 { get; set; }          //水浸0无水1有水，整数
        public int Waterlogging2 { get; set; }          //水浸0无水1有水，整数
        public int Smoke { get; set; }                   //烟感100-10000ppm
        public double Co { get; set; }                  //一氧化碳浓度  0.001精度 ppm
        public FireClass()
        {
            strCommandFile = "Fire.txt";
        }

        //
        override public void GetDataFromEqipment()
        {  
            if (Parent.WaterLog1 != null)
               Waterlogging1= Parent.WaterLog1.WaterlogData;
            if (Parent.WaterLog2 != null)
                Waterlogging2 = Parent.WaterLog2.WaterlogData;
            if (Parent.co != null)
                Co = Parent.co.CoData;
            if (Parent.Smoke != null)
                Smoke = Parent.Smoke.SmokeData;
            if (Parent.TempHum != null)
            {
                Temp = Parent.TempHum.TempData;
                Humidity = Parent.TempHum.TempData;
            } 
        }

        //
        override public void Save2DataSource(string arDate)
        {
            //基本信息  
            DBConnection.ExecSQL("insert fire (rTime,firestate,temp, humidity, waterlogging1,"
                + "waterlogging2,smoke,CO )"
                + "value('" + arDate + "','"// time.ToString("yyyy-M-d H:m:s") 
                + FireState.ToString() + "','"
                 + Temp.ToString() + "','"
                  + Humidity.ToString() + "','"
                + Waterlogging1.ToString() + "','"
                + Waterlogging2.ToString() + "','"
                + Smoke.ToString() + "','"
                + Co.ToString() +  "')");
               // + iot_code +//"','"
        }
    }
 

    //水浸
    public class WaterloggingClass : BaseEquipmentClass
    {
        public int WaterlogData;
        public bool IsError = false;

        
        public WaterloggingClass()
        {
            strCommandFile = "Fire.txt";
        }

        //
        override public void GetDataFromEqipment()
        { 
            string strTemp = "";
            bool tempError = false;
            if (GetSysData(0, ref strTemp))
            {
                WaterlogData = int.Parse(strTemp);
                Prepared = true; 
                if (WaterlogData != 0)
                {
                    if (GetSysData(0, ref strTemp))
                    {
                        WaterlogData = int.Parse(strTemp);
                        if (WaterlogData != 0)
                        {
                            tempError=true; 
                            //DBConnection.RecordLOG("系统", "水浸传感器", "水浸触发");
                        } 
                    }
                }
                if (tempError != IsError)
                {
                    if (tempError)
                    {
                        //SysIO.SetGPIOState(11, 0);
                        //frmSet.PCSMOff(); 
                        frmSet.Err3off();
                        lock (Parent.ErrorState)
                        {
                            Parent.ErrorState[2] = true;
                        }
                    }                  
                    IsError = tempError;
                } 
            } 
            else
                Prepared = false;
        }
    }

    //温湿度
    public class TempHumClass: BaseEquipmentClass   
    {
        public double TempData;                //温度-40---80，浮点型
        public double HumidityData;           //湿度0-100RH，浮点型
        public bool[]   IsError = { false,false,false, false , false, false };

        //private static ILog log = LogManager.GetLogger("TempHumClass");
        public TempHumClass()
        {
            strCommandFile = "Fire.txt";
        }

        //
        override public void GetDataFromEqipment()
        {
            bool bPrepared = false;
            string strTemp = "";
            bool tempError = false;
            if (GetSysData(1, ref strTemp))
            {
                TempData = Math.Round(float.Parse(strTemp), 3); //温度
                bPrepared = true;
                if (TempData>40)
                {
                    GetSysData(1, ref strTemp);
                    TempData = Math.Round(float.Parse(strTemp), 3); //温度
                    if (TempData > 40)
                    {
                        //9.11 添加注释:取消温度传感器
                        //tempError = true; 


                        //DBConnection.RecordLOG("系统", "温湿度传感器", "温度过高");
                    }
                    if (tempError != IsError[0])
                    {
                        if (tempError)
                        {
                            //frmSet.PCSMOff();
                            frmSet.Err3off();
                            //SysIO.SetGPIOState(11, 0);
                            lock (Parent.ErrorState)
                            {
                                Parent.ErrorState[2] = true;
                            }
                            lock (Parent.EMSError)
                            {
                                Parent.EMSError[1] &= 0xFFDF;
                                Parent.EMSError[1] |= 0x20;
                            }
                        }
                        else
                            Parent.EMSError[1] &= 0xFFDF;

                        // RecodError("温湿度传感器", iot_code, 17, ErrorClass.EMSErrorsPower[17],
                        //     ErrorClass.EMSErrors[17], tempError);
                        IsError[0] = tempError;
                    }
                }
                else if (TempData <0 )
                {
                    GetSysData(1, ref strTemp);
                    TempData = Math.Round(float.Parse(strTemp), 3); //温度
                    if (TempData <0)
                    {
                        tempError = true;
                        //DBConnection.RecordLOG("系统", "温湿度传感器", "温度过低");
                    }
                    if (tempError != IsError[1])
                    {
                        if (tempError)
                        {
                            //frmSet.PCSMOff();
                            frmSet.Err3off();
                            //SysIO.SetGPIOState(11, 0);
                            lock (Parent.ErrorState)
                            {
                                Parent.ErrorState[2] = true;
                            }
                            lock (Parent.EMSError)
                            {
                                Parent.EMSError[1] &= 0xFEFF;
                                Parent.EMSError[1] |= 0x100;
                            }
                        }
                        else
                            Parent.EMSError[1] &= 0xFEFF;

                        // RecodError("温湿度传感器", iot_code, 17, ErrorClass.EMSErrorsPower[17],
                        //     ErrorClass.EMSErrors[17], tempError);
                        IsError[1] = tempError;
                    }
                }

            }
            if (GetSysData(2, ref strTemp))
            {
                HumidityData = Math.Round(float.Parse(strTemp), 3); //湿度
                bPrepared = true;
            }
            Prepared = bPrepared;
        }
    }

    //烟雾
    public class SmokeClass : BaseEquipmentClass   
    {
        public int SmokeData;                  //烟感100-10000ppm 
        public bool IsError = false;
        public SmokeClass()
        {
            strCommandFile = "Fire.txt";
        }
        //
        override public void GetDataFromEqipment()
        {
            string strTemp = "";
            bool tempError = false;
            if (GetSysData(3, ref strTemp))
            {
                SmokeData = int.Parse(strTemp); //烟感100 - 10000ppm
                  Prepared = true;
                if (SmokeData > 3000)
                {
                    if (GetSysData(3, ref strTemp))
                    {
                        SmokeData = int.Parse(strTemp); //烟感100 - 10000ppm
                        if ( (SmokeData > 500) && (frmMain.Selffrm.AllEquipment.TempHum.TempData > 40) )
                        {
                            tempError = true;
                        }
                    }
                    //DBConnection.RecordLOG("系统", "烟感过高", );
                }
                if(tempError!=IsError)
                {
                    if (tempError)
                    {
                        //frmSet.PCSMOff();
                        frmSet.Err3off();
                        //SysIO.SetGPIOState(11, 0);
                        lock (Parent.ErrorState)
                        {
                            Parent.ErrorState[2] = true;
                        }
                        lock (Parent.EMSError)
                        {
                            Parent.EMSError[1] &= 0xFFFB;
                            Parent.EMSError[1] |= 0x4;
                        }
                    }
                    else
                        Parent.EMSError[1] &= 0xFFFB; 

                      RecodError("烟感传感器", iot_code, 16, ErrorClass.EMSErrorsPower[16], 
                          ErrorClass.EMSErrors[16] , tempError);

                    IsError = tempError; 
                } 
            }
            else
                Prepared = false;
        }
    } 
   
    //一氧化碳
    public class CoClass : BaseEquipmentClass    
    {
        public double CoData;//一氧化碳浓度  0.001精度 ppm
        public bool IsError = false;
        public CoClass() {
            strCommandFile = "Fire.txt";
        }
        //
        override public void GetDataFromEqipment()
        {
            string strTemp = "";
            bool tempError = false;
            if (GetSysData(4, ref strTemp))
            {
                CoData =  Math.Round(float.Parse(strTemp), 3);  //一氧化碳浓度
                Prepared = true;
                if (CoData > 500)//100-300-500
                {
                    if (GetSysData(4, ref strTemp))
                    {
                        CoData = Math.Round(float.Parse(strTemp), 3);  //一氧化碳浓度
                        if (CoData > 500)
                        {
                            tempError = true;
                            //Parent.ExcPCSPowerOff();
                            //SysIO.SetGPIOState(11,0); 
                            //Parent.ErrorState[2] = true;
                        }
                    } 
                }
                if (tempError != IsError)
                {
                    if (tempError)
                    {
                        if (tempError)
                        {
                            //frmSet.PCSMOff();
                            frmSet.Err3off();
                            //SysIO.SetGPIOState(11, 0);
                            lock (Parent.ErrorState)
                            {
                                Parent.ErrorState[2] = true;
                            }
                            lock (Parent.EMSError)
                            {
                                Parent.EMSError[1] &= 0xBFFF;
                                Parent.EMSError[1] |= 0x4000;
                            }
                        }
                        else
                            Parent.EMSError[1] &= 0xBFFF;
                        //DBConnection.RecordLOG("系统", "一氧化碳传感器", "一氧化碳过高");
                    }
                       // RecodError("一氧化碳传感器", iot_code, 18, ErrorClass.EMSErrorsPower[18],
                       //     ErrorClass.EMSErrors[18], tempError);
                       IsError = tempError;
                    }
                }
            else
                Prepared = false;
        }
    }


    //电表1
    public class Elemeter1Class : BaseEquipmentClass
    {
        public double AllAAkva { get; set; }              //电网功率---可视功率
        public double AllNukva { get; set; }             //无功功率
        public double AllUkva { get; set; }               //有功功率
                                                          //public double Ukwh { get; set; }                  //有功电能
                                                          //public double Nukwh { get; set; }                 //无功电能

        public double[] Ukwh { get; set; } = { 0, 0, 0, 0, 0 };   //有功 
        public double[] Nukwh { get; set; } = { 0, 0, 0, 0, 0 };  //无功   

        public double PUMdemand_Max { get; set; }         //当月正向有功最大需量
        //2.21
        public double PUMdemand_now { get; set; }   //当前正向有功最大需量

       
        private static ILog log = LogManager.GetLogger("Elemeter1Class");
        public Elemeter1Class()
        {
            strCommandFile = "Biao1.txt";
            Prepared = false;
        }

        //设置电压变比 PT008DH，电流变比 CT008EH
        public void SetPTandCT(short aPT, short aCT)
        {
            lock (m485.sp)
            {
                SetSysData(53, aPT,false);
                SetSysData(54, aCT,false);
            }
        }

        public void timing(int index)
        {
            DateTime dt = DateTime.Now;
            byte[] result = new byte[6]; // 6个字节的数组，依次为秒、分、时、日、月、年

            // 秒、分、时、日、月、年依次存储
            result[0] = (byte)dt.Second;
            result[1] = (byte)dt.Minute;
            result[2] = (byte)dt.Hour;
            result[3] = (byte)dt.Day;
            result[4] = (byte)dt.Month;
            result[5] = (byte)(dt.Year % 100);
            byte[] atime = result;
            SetSysBytes(index, atime, true);

        }

        /// <summary>
        /// 设置表的时间
        /// </summary>
        /// <param name="aTime"秒分时日月年></param>
        public void SetTime(byte[] aTime)
        {
            SetSysBytes(55, aTime,true);
            //SetSysBytes(56, aTime);
            //SetSysBytes(57, aTime);
            // SetSysBytes(58, aTime);
        }
        //

        //设置波峰评估的时间段
        public void SetJFTG(byte[] a4Zoon, byte[] aBFTGs1, byte[] aBFTGs2)
        {
            lock (this.m485.sp)
            {
                SetSysBytes(59, a4Zoon,false);//12字节的一年四区间的尖峰平谷设计
                SetSysBytes(61, aBFTGs1, false);//42字节的1尖峰平谷设计
                SetSysBytes(62, aBFTGs2, false);//42字节的2尖峰平谷设计
            }
        }

        //获取电网功率
        public void GetAllUkva()
        {
            bool bPrepared = false;
            string strTemp = "";
            if (GetSysData(33, ref strTemp))
            {
                AllUkva = Math.Round(float.Parse(strTemp), 3);
                bPrepared = true;
            }
            Prepared = bPrepared;
        }

        //获取需量
        public void GetPUMdemand_now()
        {
            bool bPrepared = false;
            string strTemp = "";
            if (GetSysData(68, ref strTemp))
            {
                PUMdemand_now = Math.Round(float.Parse(strTemp), 3);
                //log.Debug("读取关口表当前正向有功最大需量:" + PUMdemand_now);
                PUMdemand_now = PUMdemand_now * 0.001 * pc;
                //log.Debug("读取关口表当前正向有功最大需量换算值:" + PUMdemand_now + " " + "pc:" + pc);
                bPrepared = true;
            }
            Prepared = bPrepared;
        }

        //
        override public void GetDataFromEqipment()
        {  
            bool bPrepared = false;
            string strTemp = "";
            if (GetSysData(41, ref strTemp))
            {
                AllAAkva = Math.Round(float.Parse(strTemp), 3); //可视功率
                bPrepared = true;
            }

            if (GetSysData(33, ref strTemp))
            {
                AllUkva = Math.Round(float.Parse(strTemp), 3);
                bPrepared = true;
            }
            if (GetSysData(37, ref strTemp))
            {
                AllNukva = Math.Round(float.Parse(strTemp), 3);
                bPrepared = true;
            }
            if (GetSysData(0, ref strTemp))
            {
                Ukwh[0] = Math.Round(float.Parse(strTemp), 2);
                bPrepared = true;
            }
            if (GetSysData(15, ref strTemp))
            {
                Nukwh[0] = Math.Round(float.Parse(strTemp), 2);
                bPrepared = true;
            }

            //9.4 读取当月正向有功最大需量
            if (GetSysData(67, ref strTemp))
            {
                PUMdemand_Max = Math.Round(float.Parse(strTemp), 3) ;
                PUMdemand_Max = PUMdemand_Max * 0.001 * pc;
                bPrepared = true;
            }

            //2.21
            if (GetSysData(68, ref strTemp))
            {
                PUMdemand_now = Math.Round(float.Parse(strTemp), 3);
                PUMdemand_now = PUMdemand_now * 0.001 * pc;
                bPrepared = true;
            }
          
            Prepared = bPrepared;
            if (!Prepared)
            {
                Parent.EMSError[0] &= 0xFFFE;
                Parent.EMSError[0] |= 0x1;
            }
            else
            {
                Parent.EMSError[0] &= 0xFFFE; 
            }
        }

        //
        override public void Save2DataSource(string arDate)
        {
            //return;
            //基本信息Gridkva,
            DBConnection.ExecSQL("insert elemeter1 (rTime,Ukwh,Nukwh, AllUkva, AllNukva,AllAAkva,iot_code)"
                + "value('" + arDate + "','"// time.ToString("yyyy-M-d H:m:s") 
                + Ukwh[0].ToString() + "','"
                + Nukwh[0].ToString() + "','"
                + AllUkva.ToString() + "','"
                + AllNukva.ToString() + "','"
                + AllAAkva.ToString() + "','"
                + iot_code + "')"); //+"')");  
        }
    }

    //电表2
    public class Elemeter2Class : BaseEquipmentClass
    {
        public double[] Ukwh { get; set; } = { 0, 0, 0, 0, 0 };            //综合有功尖峰平谷电表;   总尖峰平谷
        public double[] PUkwh { get; set; } = { 0, 0, 0, 0, 0 };           //正向有功尖峰平谷电表;累计充电
        public double[] OUkwh { get; set; } = { 0, 0, 0, 0, 0 };           //反向有功尖峰平谷电表;累计放电
        public double[] Nukwh { get; set; } = { 0, 0, 0, 0, 0 };           //综合无功尖峰平谷电表;
        public double[] PNukwh { get; set; } = { 0, 0, 0, 0, 0 };          //正向无功尖峰平谷电表;
        public double[] ONukwh { get; set; } = { 0, 0, 0, 0, 0 };          //反向无功尖峰平谷电表;
        public double AllUkva { get; set; } = 0;     //总有用功率                           
        public double AUkva { get; set; } = 0;
        public double BUkva { get; set; } = 0;
        public double CUkva { get; set; } = 0;
        public double AllNukva { get; set; } = 0;     //总无用功率
        public double ANukva { get; set; } = 0;
        public double BNukva { get; set; } = 0;
        public double CNukva { get; set; } = 0;
        public double AllAkva { get; set; } = 0;       //总可视功率
        public double AAkva { get; set; } = 0;
        public double BAkva { get; set; } = 0;
        public double CAkva { get; set; } = 0;
        public double Aa { get; set; } = 0;               //A电流
        public double Ba { get; set; } = 0;  //;
        public double Ca { get; set; } = 0;  //;
        public double Akv { get; set; } = 0;  //;            //a对地电压
        public double Bkv { get; set; } = 0;  //;
        public double Ckv { get; set; } = 0;  //;
        public double ABkv { get; set; } = 0;  //;           //AB电压
        public double BCkv { get; set; } = 0;  //;
        public double CAkv { get; set; } = 0;  //;
        public double AllPFactor { get; set; } = 0;  //;     //总功率因数
        public double APFactor { get; set; } = 0;  //;
        public double BPFactor { get; set; } = 0;  //;
        public double CPFactor { get; set; } = 0;  //;
        public double HZ { get; set; } = 0;  //;             //频率
        public double Gridkva = 0;          //电网功率
        public double Totalkva = 0  ;       //总共功率
        public double Subkw = 0;            //辅表功率 
        public double Subkwh = 0;          //辅表电能

        //不是直接读取的数据 
        private float[] YPData = { 0, 0, 0, 0, 0 };          //昨日证向有功尖峰平谷电表
        private float[] YOData = { 0, 0, 0, 0, 0 };          //昨日反向有功尖峰平谷电表 
        private float[] PDataInoneDay = { 0, 0, 0, 0, 0 };      //当日起始正向有功尖峰平谷电表
        private float[] ODataInoneDay = { 0, 0, 0, 0, 0 };   //当日起始反向有功尖峰平谷电表

        //2.21
        public double PUMdemand_Max { get; set; }         //当月正向有功最大需量
        //2.21
        public double PUMdemand_now { get; set; }   //当前正向有功最大需量


        private static ILog log = LogManager.GetLogger("Elemeter2Class");
        public Elemeter2Class()
        {
            strCommandFile = "biao2.txt";
        }
        public void timing(int index)
        {
            DateTime dt = DateTime.Now;
            byte[] result = new byte[6]; // 6个字节的数组，依次为秒、分、时、日、月、年

            // 秒、分、时、日、月、年依次存储
            result[0] = (byte)dt.Second;
            result[1] = (byte)dt.Minute;
            result[2] = (byte)dt.Hour;
            result[3] = (byte)dt.Day;
            result[4] = (byte)dt.Month;
            result[5] = (byte)(dt.Year % 100);
            byte[] atime = result;
            SetSysBytes(index, atime, true);

        }

        //设置电压变比 PT008DH，电流变比 CT008EH
        public void SetPTandCT(short aPT, short aCT)
        {
            SetSysData(53, aPT,true);
            SetSysData(54, aCT,true);
        }


        /// <summary>
        /// 设置表的时间
        /// </summary>
        /// <param name="aTime"秒分时日月年></param>
        public void SetTime(byte[] aTime)
        {
            //string tempSTR = "";
            //frmMain.Selffrm.Invoke(new Action(() =>  {
            //GetSysData(63, ref tempSTR);
            //MessageBox.Show(tempSTR);
            SetSysBytes(55, aTime,false);
            //SetSysBytes(56, new byte[] { aTime[0], aTime[1] });
            //SetSysBytes(57, new byte[] { aTime[2], aTime[3] });
            //SetSysBytes(58, new byte[] { aTime[4], aTime[5] });

            // }));
            //SetSysBytes(56, aTime);
        }
        //

        //设置波峰评估的时间段
        public void SetJFTG(byte[] a4Zoon, byte[] aBFTGs)
        {
            if(m485 ==null)
                return ;
            lock (m485.sp)
            {
                SetSysBytes(59, a4Zoon,false);//12字节的一年四区间的尖峰平谷设计  
                SetSysBytes(60, aBFTGs, false);//42字节的1尖峰平谷设计
               // SetSysBytes(62, aBFTGs2, false);//42字节的2尖峰平谷设计
            }
        }
        //
        override public void GetDataFromEqipment()
        {//biao2 
            bool bPrepared = false;
            string strTemp = "";
            string strData = "";
            if (GetSysData(64, ref strTemp))//一次读取批量数据，再对数据进行拆分
            {
                bPrepared = true;
                if (Get3strData(0, ref strTemp, ref strData))
                    Ukwh[0] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(1, ref strTemp, ref strData))
                    Ukwh[1] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(2, ref strTemp, ref strData))
                    Ukwh[2] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(3, ref strTemp, ref strData))
                    Ukwh[3] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(4, ref strTemp, ref strData))
                    Ukwh[4] = Math.Round(float.Parse(strData), 2);
                //正向有功5
                if (Get3strData(5, ref strTemp, ref strData))
                    PUkwh[0] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(6, ref strTemp, ref strData))
                    PUkwh[1] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(7, ref strTemp, ref strData))
                    PUkwh[2] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(8, ref strTemp, ref strData))
                    PUkwh[3] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(9, ref strTemp, ref strData))
                    PUkwh[4] = Math.Round(float.Parse(strData), 2);
                //反向有功10
                if (Get3strData(10, ref strTemp, ref strData))
                    OUkwh[0] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(11, ref strTemp, ref strData))
                    OUkwh[1] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(12, ref strTemp, ref strData))
                    OUkwh[2] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(13, ref strTemp, ref strData))
                    OUkwh[3] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(14, ref strTemp, ref strData))
                    OUkwh[4] = Math.Round(float.Parse(strData), 2);
                //无功15
                if (Get3strData(15, ref strTemp, ref strData))
                    Nukwh[0] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(16, ref strTemp, ref strData))
                    Nukwh[1] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(17, ref strTemp, ref strData))
                    Nukwh[2] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(18, ref strTemp, ref strData))
                    Nukwh[3] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(19, ref strTemp, ref strData))
                    Nukwh[4] = Math.Round(float.Parse(strData), 2);

                //正向无功
                if (Get3strData(20, ref strTemp, ref strData))
                    PNukwh[0] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(21, ref strTemp, ref strData))
                    PNukwh[1] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(22, ref strTemp, ref strData))
                    PNukwh[2] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(23, ref strTemp, ref strData))
                    PNukwh[3] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(24, ref strTemp, ref strData))
                    PNukwh[4] = Math.Round(float.Parse(strData), 2);
                //反向无功25
                if (Get3strData(25, ref strTemp, ref strData))
                    ONukwh[0] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(26, ref strTemp, ref strData))
                    ONukwh[1] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(27, ref strTemp, ref strData))
                    ONukwh[2] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(28, ref strTemp, ref strData))
                    ONukwh[3] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(29, ref strTemp, ref strData))
                    ONukwh[4] = Math.Round(float.Parse(strData), 2);
            }

            //
            if (GetSysData(65, ref strTemp))
            {

                bPrepared = true;
                if (Get3strData(30, ref strTemp, ref strData))
                    AUkva = Math.Round(float.Parse(strData), 3);
                if (Get3strData(31, ref strTemp, ref strData))
                    BUkva = Math.Round(float.Parse(strData), 3);
                if (Get3strData(32, ref strTemp, ref strData))
                    CUkva = Math.Round(float.Parse(strData), 3);
                if (Get3strData(33, ref strTemp, ref strData))
                    AllUkva = Math.Round(float.Parse(strData), 3);


                if (Get3strData(34, ref strTemp, ref strData))
                    ANukva = Math.Round(float.Parse(strData), 3);
                if (Get3strData(35, ref strTemp, ref strData))
                    BNukva = Math.Round(float.Parse(strData), 3);
                if (Get3strData(36, ref strTemp, ref strData))
                    CNukva = Math.Round(float.Parse(strData), 3);
                if (Get3strData(37, ref strTemp, ref strData))
                    AllNukva = Math.Round(float.Parse(strData), 3);


                if (Get3strData(38, ref strTemp, ref strData))
                    AAkva = Math.Round(float.Parse(strData), 3);
                if (Get3strData(39, ref strTemp, ref strData))
                    BAkva = Math.Round(float.Parse(strData), 3);
                if (Get3strData(40, ref strTemp, ref strData))
                    CAkva = Math.Round(float.Parse(strData), 3);
                if (Get3strData(41, ref strTemp, ref strData))
                    AllAkva = Math.Round(float.Parse(strData), 3);

                //功率因数
                if (Get3strData(42, ref strTemp, ref strData))
                    APFactor = Math.Round(float.Parse(strData), 3);
                if (Get3strData(43, ref strTemp, ref strData))
                    BPFactor = Math.Round(float.Parse(strData), 3);
                if (Get3strData(44, ref strTemp, ref strData))
                    CPFactor = Math.Round(float.Parse(strData), 3);
                if (Get3strData(45, ref strTemp, ref strData))
                    AllPFactor = Math.Round(float.Parse(strData), 3);
            }

            if (GetSysData(66, ref strTemp))
            {
                bPrepared = true;
                if (Get3strData(46, ref strTemp, ref strData))
                    Akv = Math.Round(float.Parse(strData), 1);
                if (Get3strData(47, ref strTemp, ref strData))
                    Bkv = Math.Round(float.Parse(strData), 1);
                if (Get3strData(48, ref strTemp, ref strData))
                    Ckv = Math.Round(float.Parse(strData), 1);

                if (Get3strData(49, ref strTemp, ref strData))
                    Aa = Math.Round(float.Parse(strData), 2);
                if (Get3strData(50, ref strTemp, ref strData))
                    Ba = Math.Round(float.Parse(strData), 2);
                if (Get3strData(51, ref strTemp, ref strData))
                    Ca = Math.Round(float.Parse(strData), 2);
            }

            //2.21 读取当月正向有功最大需量
            if (GetSysData(67, ref strTemp))
            {
                PUMdemand_Max = Math.Round(float.Parse(strTemp), 3);
                PUMdemand_Max = PUMdemand_Max * 0.001 * pc;
                bPrepared = true;
            }

            if (GetSysData(68, ref strTemp))
            {
                PUMdemand_now = Math.Round(float.Parse(strTemp), 3);
                PUMdemand_now = PUMdemand_now * 0.001 * pc;
                bPrepared = true;
            }

            if (GetSysData(52, ref strTemp))
                HZ = Math.Round(float.Parse(strTemp), 2);

            time = DateTime.Now;
            Totalkva = Gridkva + AllAkva;

          
            Prepared = bPrepared;
            if (!Prepared)
            {
                lock (Parent.EMSError)
                {
                    Parent.EMSError[0] &= 0xFFFD;
                    Parent.EMSError[0] |= 0x2;
                }
                /*
                 Parent.EMSError[0] |= 0x2; //这条就够了
                 */
            }
            else
            {
                Parent.EMSError[0] &= 0xFFFD;
            }
        }

        //
        override public void Save2DataSource(string arDate)
        {
            //基本信息
            DBConnection.ExecSQL("insert elemeter2 (rTime, "
                 + "Ukwh,UkwhJ,UkwhF,UkwhP,UkwhG,"
                 + "PUkwh,PUkwhJ,PUkwhF,PUkwhP,PUkwhG,"
                 + "OUkwh,OUkwhJ,OUkwhF,OUkwhP,OUkwhG,"
                 + "Nukwh, NukwhJ,NukwhF,NukwhP,NukwhG,"
                 + "PNukwh,PNukwhJ,PNukwhF,PNukwhP,PNukwhG,"
                 + "ONukwh, ONukwhJ,ONukwhF,ONukwhP,ONukwhG,"
                + "AllUkva, AUkva, BUkva, CUkva,   "
                + "AllNukva,  ANukva, BNukva,  CNukva, "
                + " AllAAkva, AAkva, BAkva, CAkva," +
                " Aa,Ba,Ca, Akv, Bkv,Ckv, ABkv,  BCkv,  CAkv, AllPFoctor, APFoctor,  BPFoctor, CPFoctor, HZ, "
                + "Gridkva,Totalkva,Subkw,Subkwh ,PlanKW)value( '"
                + arDate + "','"//rTime.ToString("yyyy-M-d H:m:s")
                + Ukwh[0].ToString() + "','" + Ukwh[1].ToString() + "','" + Ukwh[2].ToString() + "','" + Ukwh[3].ToString() + "','" + Ukwh[4].ToString() + "','"
                + PUkwh[0].ToString() + "','" + PUkwh[1].ToString() + "','" + PUkwh[2].ToString() + "','" + PUkwh[3].ToString() + "','" + PUkwh[4].ToString() + "','"
                + OUkwh[0].ToString() + "','" + OUkwh[1].ToString() + "','" + OUkwh[2].ToString() + "','" + OUkwh[3].ToString() + "','" + OUkwh[4].ToString() + "','"
                + Nukwh[0].ToString() + "','" + Nukwh[1].ToString() + "','" + Nukwh[2].ToString() + "','" + Nukwh[3].ToString() + "','" + Nukwh[4].ToString() + "','"
                + PNukwh[0].ToString() + "','" + PNukwh[1].ToString() + "','" + PNukwh[2].ToString() + "','" + PNukwh[3].ToString() + "','" + PNukwh[4].ToString() + "','"
                + ONukwh[0].ToString() + "','" + ONukwh[1].ToString() + "','" + ONukwh[2].ToString() + "','" + ONukwh[3].ToString() + "','" + ONukwh[4].ToString() + "','"

                + AllUkva.ToString() + "','" + AUkva.ToString() + "','" + BUkva.ToString() + "','" + CUkva.ToString() + "','"
                + AllNukva.ToString() + "','" + ANukva.ToString() + "','" + BNukva.ToString() + "','" + CNukva.ToString() + "','"
                + AllAkva.ToString() + "','" + AAkva.ToString() + "','" + BAkva.ToString() + "','" + CAkva.ToString() + "','"
                + Aa.ToString() + "','" + Ba.ToString() + "','" + Ca.ToString() + "','"
                + Akv.ToString() + "','" + Bkv.ToString() + "','" + Ckv.ToString() + "','"
                + ABkv.ToString() + "','" + BCkv.ToString() + "','" + CAkv.ToString() + "','"
                + AllPFactor.ToString() + "','" + APFactor.ToString() + "','" + BPFactor.ToString() + "','" + CPFactor.ToString() + "','"
                + HZ.ToString() + "','" + Gridkva.ToString() + "','" + Totalkva.ToString() + "','" + Subkw.ToString() + "','" + Subkwh.ToString() + "','"
                + Parent.PCSScheduleKVA.ToString() + "')");
        }

    }

    //电表3
    public class Elemeter3Class : BaseEquipmentClass
    {
        public double[] Akwh { get; set; } = { 0, 0, 0, 0, 0 };  //组合电能  
        public double Lv { get; set; }//电压
        public double La { get; set; } //电流  
        public double UKva { get; set; } //有功功率
        public double NUKva { get; set; } //无功功率
        public double AKva { get; set; } //视在功率
        public Elemeter3Class()
        {
            strCommandFile = "biao3.txt";
        }

        /// <summary>
        /// 设置表的时间
        /// </summary>
        /// <param name="aTime"></param>
        public void SetTime(byte[] aTime)
        {
            SetSysBytes(37, aTime,false);
        }
        //

        public void timing(int index)
        {
            DateTime dt = DateTime.Now;
            byte[] result = new byte[6]; // 6个字节的数组，依次为秒、分、时、日、月、年

            // 秒、分、时、日、月、年依次存储
            result[0] = (byte)(dt.Year % 100);
            result[1] = (byte)dt.Month;
            result[2] = (byte)dt.Hour;
            result[3] = (byte)dt.Day;
            result[4] = (byte)dt.Minute;
            result[5] = (byte)dt.Second;
            byte[] atime = result;
            SetSysBytes(index, atime, true);

        }

        //设置波峰评估的时间段
        public void SetJFTG(byte[] a4Zoon, byte[] aBFTGs)
        {
            lock (m485.sp)
            {
                SetSysBytes(41, a4Zoon,false);//12字节的一年四区间的尖峰平谷设计
                SetSysBytes(43, aBFTGs, false);//42字节的一年四区间的尖峰平谷设计
                //SetSysBytes(43, aBFTGs2, false);
            }
        }

        //
        override public void GetDataFromEqipment()
        {

            bool bPrepared = false;
            string strTemp = "";
            string strData = "";
            if (GetSysData(46, ref strTemp))
            {

                bPrepared = true;
                if (Get3strData(30, ref strTemp, ref strData))
                    Lv = Math.Round(float.Parse(strData), 2);
                if (Get3strData(31, ref strTemp, ref strData))
                    La = Math.Round(float.Parse(strData), 2);
                if (Get3strData(32, ref strTemp, ref strData))
                    UKva = Math.Round(float.Parse(strData), 2);
                if (Get3strData(33, ref strTemp, ref strData))
                    NUKva = Math.Round(float.Parse(strData), 2);
                if (Get3strData(34, ref strTemp, ref strData))
                    AKva = Math.Round(float.Parse(strData), 2);

            }
            //if (GetSysData(36, ref strTemp))
            // HZ = Math.Round(float.Parse(strTemp), 2);
            //综合有功电表 
            if (GetSysData(45, ref strTemp))
            { 
                bPrepared = true;
                if (Get3strData(0, ref strTemp, ref strData))
                    Akwh[0] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(1, ref strTemp, ref strData))
                    Akwh[1] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(2, ref strTemp, ref strData))
                    Akwh[2] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(3, ref strTemp, ref strData))
                    Akwh[3] = Math.Round(float.Parse(strData), 2);
                if (Get3strData(4, ref strTemp, ref strData))
                    Akwh[4] = Math.Round(float.Parse(strData), 2);
            }
            //if (GetSysData(0, ref strTemp))
            //    Akwh[0] = Math.Round(float.Parse(strTemp), 2);
            //if (GetSysData(1, ref strTemp))
            //    Akwh[1] = Math.Round(float.Parse(strTemp), 2);
            //if (GetSysData(2, ref strTemp))
            //    Akwh[2] = Math.Round(float.Parse(strTemp), 2);
            //if (GetSysData(3, ref strTemp))
            //    Akwh[3] = Math.Round(float.Parse(strTemp), 2);
            //if (GetSysData(4, ref strTemp))
            //    Akwh[4] = Math.Round(float.Parse(strTemp), 2);
            time = DateTime.Now;
            Parent.Elemeter2.Subkw = Math.Round(AKva, 2);
            Parent.Elemeter2.Subkwh = Math.Round(Akwh[0], 2);
            Parent.AuxiliaryKVA = Math.Round(AKva, 2);
            Parent.AuxiliaryKWH[0] = Math.Round(Akwh[0], 2);
            Parent.AuxiliaryKWH[1] = Math.Round(Akwh[1], 2);
            Parent.AuxiliaryKWH[2] = Math.Round(Akwh[2], 2);
            Parent.AuxiliaryKWH[3] = Math.Round(Akwh[3], 2);
            Parent.AuxiliaryKWH[4] = Math.Round(Akwh[4], 2);
            Prepared = bPrepared;
            if (!Prepared)
            {
                lock (Parent.EMSError)
                {
                    Parent.EMSError[0] &= 0xFFF7;
                    Parent.EMSError[0] |= 0x8;
                }
            }
            else
            {
                Parent.EMSError[0] &= 0xFFF7;
            }
        }

        //
        override public void Save2DataSource(string arDate)
        {
            //基本信息
            DBConnection.ExecSQL("insert elemeter3 (rTime, Akwh, AkwhJ, AkwhF, AkwhP, AkwhG, "
                + "Ukva,Nukva,Akva,V,A)value('" + arDate + "','"// rTime.ToString("yyyy-M-d H:m:s")
                 + Akwh[0].ToString() + "','" + Akwh[1].ToString() + "','" + Akwh[2].ToString() + "','" + Akwh[3].ToString() + "','"
                + Akwh[4].ToString() + "','" + UKva.ToString() + "','" + NUKva.ToString() + "','" + AKva.ToString() + "','"
                + Lv.ToString() + "','" + La.ToString() + "')");
        }
    }

    //PCSQiao
    public class PCSClass : BaseEquipmentClass
    {
        public int PCS = 1;//0:英博；1：晶石
        public static string[] PCSStates = { "待机", "停机", "充电", "放电" };
        public static string[] PCSTypes = { "待机", "恒流", "恒压", "恒功率", "AC恒压", "自适应需量" };
        public static string[] PCSNetState = { "停机","待机", "运行", "总故障状态", "总警告状态" , "1远程/0就地状态","急停输入状态","并网","离网","过载降容"};
        public int State = 0;
        public double aV { get; set; }
        public double bV { get; set; }
        public double cV { get; set; }
        public double aA { get; set; }
        public double bA { get; set; }
        public double cA { get; set; }
        public double hz { get; set; }            //频率
        public double aUkva { get; set; }         //a有功功率
        public double bUkva { get; set; }
        public double cUkva { get; set; }
        public double allUkva { get; set; }        //总有功功率
        public double aNUkvar { get; set; }        //A无功功率
        public double bNUkvar { get; set; }
        public double cNUkvar { get; set; }
        public double allNUkvar { get; set; }
        public double aAkva { get; set; }              //A视在功率   
        public double bAkva { get; set; }
        public double cAkva { get; set; }
        public double allAkva { get; set; }             //总视在功率
        public double aPFactor { get; set; }          //功率因数
        public double bPFactor { get; set; }
        public double cPFactor { get; set; }
        public double allPFactor { get; set; }
        public double inputkva { get; set; }         //输入功率
        public double inputV { get; set; }            //输入电压
        public double inputA { get; set; }            //输入电流
        public double PCSTemp { get; set; }           //PCS温度
        public double ACInkwh { get; set; }           //交流充电量
        public double ACOutkwh { get; set; }          //交流放电量
        public double DCInkwh { get; set; }           //直流充电量
        public double DCOutkwh { get; set; }          //直流放电量
        public double DCInputV { get; set; }          //直流母线电压
        public double DCOutputV { get; set; }
        public double DCInputA { get; set; }
        public double DCOutputA { get; set; }
        public double DCDCInkw;           //DC输入功率 
        public double DCTemp;             //DC部分温度
        public double InTemp { get; set; }           //入口温度
        public double OutTemp { get; set; }             //出口温度
        public ushort[] Error { get; set; } = { 0, 0, 0, 0, 0, 0, 0, 0 };
        public int[] Error_2  = { 0, 0, 0, 0, 0, 0, 0, 0 };

        public ushort[] OldError = { 0, 0, 0, 0, 0, 0, 0, 0 };

        //11.16：PCS添加设备状态字
        public ushort PcsStatus = 0;
        //12.15 PCS启动/停机状态
        public double PcsRun = 255;  //255：停机
        public ushort PCSwaType = 0;

        public double IGBTTemp1 { get; set; }
        public double IGBTTemp2{ get; set; }
        public double IGBTTemp3 { get; set; }
        public double IGBTTemp4 { get; set; }
        public double IGBTTemp5 { get; set; }
        public double IGBTTemp6 { get; set; }


        //PCS新增DSP2参数 11.27
        public double DSP2aV { get; set; }
        public double DSP2bV { get; set; }
        public double DSP2cV { get; set; }
        public double DSP2aA { get; set; }
        public double DSP2bA { get; set; }
        public double DSP2cA { get; set; }
        public double DSP2hz { get; set; }            //频率
        public double DSP2aUkva { get; set; }         //a有功功率
        public double DSP2bUkva { get; set; }
        public double DSP2cUkva { get; set; }
        public double DSP2allUkva { get; set; }        //总有功功率
        //public double DSP2aNUkvar { get; set; }        //A无功功率
        //public double DSP2bNUkvar { get; set; }
        //public double DSP2cNUkvar { get; set; }
        public double DSP2allNUkvar { get; set; }
       // public double DSP2aAkva { get; set; }              //A视在功率   
        //public double DSP2bAkva { get; set; }
        //public double DSP2cAkva { get; set; }
        public double DSP2allAkva { get; set; }             //总视在功率
        //public double DSP2aPFactor { get; set; }          //功率因数
        //public double DSP2bPFactor { get; set; }
        //public double DSP2cPFactor { get; set; }
        public double DSP2allPFactor { get; set; }
        public double DSP2inputkva { get; set; }         //输入功率
        public double DSP2inputV { get; set; }            //输入电压
        public double DSP2inputA { get; set; }            //输入电流
        //public double DSP2PCSTemp { get; set; }           //PCS温度
        //public double DSP2ACInkwh { get; set; }           //交流充电量
        //public double DSP2ACOutkwh { get; set; }          //交流放电量
        //public double DSP2DCInkwh { get; set; }           //直流充电量
        //public double DSP2DCOutkwh { get; set; }          //直流放电量
        public double DSP2DCInputV { get; set; }
        //public double DSP2DCOutputV { get; set; }
        //public double DSP2DCInputA { get; set; }
        //public double DSP2DCOutputA { get; set; }
        //public double DSP2DCDCInkw;           //DC输入功率 
        //public double DSP2DCTemp;             //DC部分温度
        public double DSP2InTemp { get; set; }           //入口温度
        public double DSP2OutTemp { get; set; }             //出口温度

        public double DSP2IGBTTemp4 { get; set; }
        public double DSP2IGBTTemp5 { get; set; }
        public double DSP2IGBTTemp6 { get; set; }


        //11.28 新增PCS软硬件版本号
        public string SoftwareVersion { get; set; }
        public string HardwareVersion { get; set; }



        public PCSClass(int aPCS)
        {
            PCS = aPCS;
            switch (PCS)
            {
                case 0:
                    strCommandFile = "PCS1.txt";
                    break;
                case 1:
                    strCommandFile = "PCS2.txt"; //对应精石PCS
                    break;
            }
        }
        public bool IsError()
        {
            return (Error[0] + Error[0] + Error[0] + Error[0] + Error[0] + Error[0] + Error[0] + Error[0]) > 0;
        }

        public void ExcSetPCSPower(bool aPowerOn)
        {
            switch (PCS)
            {
                case 0:
                    ExcSetPCSPower1(aPowerOn);
                    break;
                case 1:
                    ExcSetPCSPower2(aPowerOn);
                    break;
            } 
        }

            public void ExcSetPCSPower1(bool aPowerOn)
        {
            //判断SOC师傅可以充放电
            //判断允许充放电的电流是否满足要求
            //设置PCS为待机状态 
            lock (m485.sp)
            {
                //SetSysData(84, frmSet.PCSGridModel, false);//0并网,1离网
                //SetSysData(82, 0xFF00,false);//1远程、0本地
                if (aPowerOn)
                    SetSysData(77, 0xff00, false);
                else
                    SetSysData(78, 0xff00, false);//qiao 
                                           //SetSysData(79, 0xff00);//qiao急停 
                                           //获取BMS返回状态
                                           //修正状态
            }
        }

       //晶石
        public void ExcSetPCSPower2(bool aPowerOn)
        {
            if (aPowerOn)
            {
                SetSysData(40, 0xFF00, true);
            }
            else
            {
                SetSysData(40, 0x00FF, true);
            }
        }


        public bool ExecCommand( string aPCSType, int aData, double aBMSSOC)
        {
            bool bResult = false;
            switch (PCS)
            {
                case 0:
                    bResult= ExecCommand1(aPCSType, aData, aBMSSOC);
                    break;
                case 1:
                    bResult= ExecCommand2(aPCSType, aData, aBMSSOC);
                    break;  
            }
            return bResult;
        }


        /// <summary>
        /// 英博PCS
        /// </summary>
        /// <param name="aWorkType"></param>
        /// <param name="aPCSType"></param>
        /// <param name="aData"></param>
        /// <param name="aBMSSOC"></param>
        /// <param name="aError"></param>
        /// <returns></returns>
        public bool ExecCommand1(  string aPCSType, int aData, double aBMSSOC)
        {
            bool bResult = false;

            try
            {
                //读取当前状态
                //if (GetSysData(0, ref strTemp))
                //    State = Convert.ToInt32(strTemp);//00301 0待机1恒流2、恒压、3恒功率 
                //如果故障记录跳出自动策略模式
                //if (aError > 0)
                //{
                //    DBConnection.RecordLOG("系统", "策略执行失败", "PCS故障导致策略执行失败");
                //    return false;
                //}
                //回测   

                //设置PCS
                int iPower;
                int iPCSTypes = Array.IndexOf(PCSTypes, aPCSType);

                lock (m485.sp)
                {
                    if (aData * Parent.waValueActive == 0)//qiao
                        SetSysData(47, 0, false);
                   if(!frmSet. PCSForceRun)
                   {
                        if ((aData < 0) && (Parent.BMS.MaxChargeA == 0))
                            aData = 0;
                        if ((aData > 0) && (Parent.BMS.MaxDischargeA == 0))
                            aData = 0;
                    }
                    //SetSysData(47, iPCSTypes); //设置PCS的为//0待机1恒流2、恒压、3恒功率
                    switch (iPCSTypes)//0待机1恒流2、恒压、3恒功率
                    {
                        case 0://待机  
                             SetSysData(84, frmSet.PCSGridModel, false);//0并网,1离网
                             SetSysData(82, 0xFF00,false);//1远程、0本地
                            iPower = ((aData * 10) / 3);////负给电网放电，正从电网充电
                            SetSysData(55, iPower, false);//三相四线
                            SetSysData(56, iPower, false);
                            SetSysData(57, iPower, false);
                            //SetSysData(50, aData);//三项三线
                            SetSysData(47, iPCSTypes, false); //设置PCS的为//0待机1恒流2、恒压、3恒功率 
                            //ExcSetPCSPower1(false);
                            break;
                        case 1://1恒流  
                            SetSysData(48, aData, false);
                            SetSysData(47, iPCSTypes, false); //设置PCS的为//0待机1恒流2、恒压、3恒功率 
                            ExcSetPCSPower1(true);
                            break;
                        case 2://恒压   
                            SetSysData(49, aData, false);
                            SetSysData(47, iPCSTypes, false); //设置PCS的为//0待机1恒流2、恒压、3恒功率 
                            ExcSetPCSPower1(true);
                            break;
                        case 3://恒功率  
                            if((aData<0) && (Parent.BMS.MaxChargeA == 0))
                                aData = 0;
                            if ((aData > 0) && (Parent.BMS.MaxDischargeA == 0))
                                aData = 0;
                            iPower = ((aData * 10) / 3);////负给电网放电，正从电网充电 
                            SetSysData(55, iPower, false);//三相四线
                            SetSysData(56, iPower, false);
                            SetSysData(57, iPower, false);
                            SetSysData(47, iPCSTypes, false); //设置PCS的为//0待机1恒流2、恒压、3恒功率
                                                              //SetSysData(50, aData);//三项三线 
                            ExcSetPCSPower1(true);
                            break;
                    }
                }
                bResult = true;
            }
            catch (Exception ex)
            { frmMain.ShowDebugMSG(ex.ToString()); }
            finally
            {
            }
            return bResult;
        }

        /// <summary>
        /// 晶石
        /// </summary>
        /// <param name="aWorkType"></param>
        /// <param name="aPCSType"></param>
        /// <param name="aData"></param>
        /// <param name="aBMSSOC"></param>
        /// <param name="aError"></param>
        /// <returns></returns>
        public bool ExecCommand2( string aPCSWorkType, int aData, double aBMSSOC)
        {
            bool bResult = false;
            aData = -aData;
            try
            { 
                //设置PCS 
                int iPCSTypes = Array.IndexOf(PCSTypes, aPCSWorkType);
                //如果不是强制开机，判断BMS属性是否满足PCS运行条件
                if (! frmSet.PCSForceRun)
                {
                    //1-DC恒压 2 - DC恒流3 - DC恒功率4 - AC恒压5 - AC恒流6 - AC恒功率 
                    if ((aData < 0) && (Parent.BMS.MaxChargeA == 0)) //BMS最大充电电流为0，不能充
                        aData = 0;
                    if ((aData > 0) && (Parent.BMS.MaxDischargeA == 0))//BMS最大放电电流为0，不能放
                        aData = 0;
                }
                lock (m485.sp)
                {
                    switch (iPCSTypes)
                    {
                        case 0://待机 
                            ExcSetPCSPower2(false);
                            SetSysData(41, 6, false); //设置PCS的为6 - AC恒功率
                            break;
                        case 1://1恒流 ---交流
                            SetSysData(53, aData * 10,false);
                            SetSysData(41, 5, false); //设置PCS的为//0待机1恒流2、恒压、3恒功率
                            break;
                        case 2://恒压----DC  
                            SetSysData(53, aData * 10, false);
                            SetSysData(41, 1, false); //设置PCS的为//0待机1恒流2、恒压、3恒功率
                            break;
                        case 3://恒功率  
                            SetSysData(42, aData * 10, false);
                            if (PCSwaType != 6)
                            {
                                SetSysData(41, 6, false); //设置PCS的为//0待机1恒流2、恒压、3恒功率 
                            }
                            break;
                        case 4://恒压---AC 
                               //73 50000
                            SetSysData(54, 5000, false); //50hz
                            SetSysData(41, 4, false); //设置PCS的为//0待机1恒流2、恒压、3恒功率 
                            break;                      
                        case 5://自适应需量并非PCS原有功率，执行上等同于恒功率
                            SetSysData(42, aData * 10, false);//有功设定
                            SetSysData(41, 6, false); //设置PCS的为//0待机1恒流2、恒压、3恒功率 
                            break;
                    }
                }
                bResult = true;
            }
            catch (Exception ex)
            { frmMain.ShowDebugMSG(ex.ToString()); }
            finally
            {
            }
            return bResult;
        }
        /// <summary>
        /// 读取
        /// </summary>
        override public void GetDataFromEqipment()
        {//pcs
            //Thread.Sleep(10);
            switch (PCS)
            {
                case 0:
                    //GetDataFromEqipment1();
                    break;
                case 1:
                    if (frmMain.Selffrm.AllEquipment.ReduceReadPCS)
                    {
                        GetLowDataFromEquipmentJS();
                    }
                    else
                    {
                        GetDataFromEqipment2();
                    }
                    break;
            } 
        }

        /// <summary>
        /// 英博
        /// </summary>
        public void GetDataFromEqipment1()
        {//pcs 
             
            string strTemp = "";
            string strData = "";
            bool bPrepared = false;
 
            //
            //if (GetSysData(85, ref strTemp))//41984
            {//10100100
             //if (Get3strData(84, ref strTemp, ref strData))
             //{ }
             // PCSNetState[] =  (strData=="1");
            }

            //
            if (GetSysData(0, ref strTemp))
            {
                bPrepared = true;
                if (Get3strData(2, ref strTemp, ref strData))
                    aV = Math.Round(float.Parse(strData), 1);
                if (Get3strData(3, ref strTemp, ref strData))
                    bV = Math.Round(float.Parse(strData), 1);
                if (Get3strData(4, ref strTemp, ref strData))
                    cV = Math.Round(float.Parse(strData), 1);
                if (Get3strData(5, ref strTemp, ref strData))
                    aA = Math.Round(float.Parse(strData), 1);
                if (Get3strData(6, ref strTemp, ref strData))
                    bA = Math.Round(float.Parse(strData), 1);
                if (Get3strData(7, ref strTemp, ref strData))
                    cA = Math.Round(float.Parse(strData), 1);
                if (Get3strData(8, ref strTemp, ref strData))
                    hz = Math.Round(float.Parse(strData), 2);
                if (Get3strData(9, ref strTemp, ref strData))
                    aUkva = Math.Round(float.Parse(strData), 1);
                if (Get3strData(10, ref strTemp, ref strData))
                    bUkva = Math.Round(float.Parse(strData), 1);
                if (Get3strData(11, ref strTemp, ref strData))
                    cUkva = Math.Round(float.Parse(strData), 1);
                if (Get3strData(12, ref strTemp, ref strData))
                    allUkva = Math.Round(float.Parse(strData), 1);
                if (Get3strData(13, ref strTemp, ref strData))
                    aNUkvar = Math.Round(float.Parse(strData), 1);
                if (Get3strData(14, ref strTemp, ref strData))
                    bNUkvar = Math.Round(float.Parse(strData), 1);
                if (Get3strData(15, ref strTemp, ref strData))
                    cNUkvar = Math.Round(float.Parse(strData), 1);
                if (Get3strData(16, ref strTemp, ref strData))
                    allNUkvar = Math.Round(float.Parse(strData), 1);
                if (Get3strData(17, ref strTemp, ref strData))
                    aAkva = Math.Round(float.Parse(strData), 1);
                if (Get3strData(18, ref strTemp, ref strData))
                    bAkva = Math.Round(float.Parse(strData), 1);
                if (Get3strData(19, ref strTemp, ref strData))
                    cAkva = Math.Round(float.Parse(strData), 1);
                if (Get3strData(20, ref strTemp, ref strData))
                    allAkva = Math.Round(float.Parse(strData), 1);
                if (Get3strData(21, ref strTemp, ref strData))
                    aPFactor = Math.Round(float.Parse(strData), 3);
                if (Get3strData(22, ref strTemp, ref strData))
                    bPFactor = Math.Round(float.Parse(strData), 3);
                if (Get3strData(23, ref strTemp, ref strData))
                    cPFactor = Math.Round(float.Parse(strData), 3);
                if (Get3strData(24, ref strTemp, ref strData))
                    allPFactor = Math.Round(float.Parse(strData), 3);
                if (Get3strData(25, ref strTemp, ref strData))
                    inputkva = Math.Round(float.Parse(strData), 1);
                if (Get3strData(26, ref strTemp, ref strData))
                    inputV = Math.Round(float.Parse(strData), 1);
                if (Get3strData(27, ref strTemp, ref strData))
                    inputA = Math.Round(float.Parse(strData), 1);
                if (Get3strData(28, ref strTemp, ref strData)) //(GetSysData(28, ref strTemp))
                    PCSTemp = Math.Round(float.Parse(strData), 0);
            }
            //if (GetSysData(0, ref strTemp))
            //    State = Convert.ToInt32(strTemp);
            //if (GetSysData(1, ref strTemp))
            //    NetEnble = Convert.ToBoolean(strTemp);

            //if (GetSysData(2, ref strTemp))
            //    aV = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(3, ref strTemp))
            //    bV = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(4, ref strTemp))
            //    cV = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(5, ref strTemp))
            //    aA = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(6, ref strTemp))
            //    bA = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(7, ref strTemp))
            //    cA = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(8, ref strTemp))
            //    hz = Math.Round(float.Parse(strTemp), 2); 
            //if (GetSysData(9, ref strTemp))
            //    aUkva = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(10, ref strTemp))
            //    bUkva = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(11, ref strTemp))
            //    cUkva = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(12, ref strTemp))
            //    allUkva = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(13, ref strTemp))
            //    aNUkvar = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(14, ref strTemp))
            //    bNUkvar = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(15, ref strTemp))
            //    cNUkvar = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(16, ref strTemp))
            //    allNUkvar = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(17, ref strTemp))
            //    aAkva = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(18, ref strTemp))
            //    bAkva = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(19, ref strTemp))
            //    cAkva = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(20, ref strTemp))
            //    allAkva = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(21, ref strTemp))
            //    aPFactor = Math.Round(float.Parse(strTemp), 3);
            //if (GetSysData(22, ref strTemp))
            //    bPFactor = Math.Round(float.Parse(strTemp), 3);
            //if (GetSysData(23, ref strTemp))
            //    cPFactor = Math.Round(float.Parse(strTemp), 3);
            //if (GetSysData(24, ref strTemp))
            //    allPFactor = Math.Round(float.Parse(strTemp), 3);
            //if (GetSysData(25, ref strTemp))
            //    inputkva = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(26, ref strTemp))
            //   inputV = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(27, ref strTemp))
            //    inputA = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(28, ref strTemp))
            //    PCSTemp = Math.Round(float.Parse(strTemp), 0);

            if (GetSysData(1, ref strTemp))
            {

                bPrepared = true;
                if (Get3strData(29, ref strTemp, ref strData))
                    ACInkwh = Math.Round(float.Parse(strData), 3);
                if (Get3strData(30, ref strTemp, ref strData))
                    ACOutkwh = Math.Round(float.Parse(strData), 3);
                if (Get3strData(31, ref strTemp, ref strData))
                    DCInkwh = Math.Round(float.Parse(strData), 3);
                if (Get3strData(32, ref strTemp, ref strData))
                    DCOutkwh = Math.Round(float.Parse(strData), 3);
            }

            // if (GetSysData(29, ref strTemp))
            //    ACInkwh = Math.Round(float.Parse(strTemp), 3);
            //if (GetSysData(30, ref strTemp))
            //    ACOutkwh = Math.Round(float.Parse(strTemp), 3);
            //if (GetSysData(31, ref strTemp))
            //    DCInkwh = Math.Round(float.Parse(strTemp), 3);
            //if (GetSysData(32, ref strTemp))
            //    DCOutkwh = Math.Round(float.Parse(strTemp), 3);
            ////不用DCDC
            //if (GetSysData(33, ref strTemp))
            //    DCInputV = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(34, ref strTemp))
            //    DCOutputV = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(35, ref strTemp))
            //    DCInputA = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(36, ref strTemp))
            //    DCOutputA = Math.Round(float.Parse(strTemp), 1);
            //if (GetSysData(37, ref strTemp))
            //    DCDCInkw = Math.Round(float.Parse(strTemp),1); 
            // if (GetSysData(38, ref strTemp))
            //    DCTemp = Math.Round(float.Parse(strTemp), 1);

            if (GetSysData(39, ref strTemp))
            {
                bPrepared = true;
                Error[0] = Convert.ToUInt16(strTemp); 
            } 
            if (GetSysData(40, ref strTemp))
            {
                bPrepared = true; 
                Error[1] = Convert.ToUInt16(strTemp);
            } 
            if (GetSysData(41, ref strTemp))
            {
                bPrepared = true;
                Error[2] = Convert.ToUInt16(strTemp);
            } 
            if (GetSysData(42, ref strTemp))
            {
                bPrepared = true; 
                Error[3] = Convert.ToUInt16(strTemp);
            } 
            if (GetSysData(86, ref strTemp))
            {
                bPrepared = true;
                Error[4] = Convert.ToUInt16(strTemp);
                Error[4] = (ushort)( Error[4] & 63);
            }
               
            //if (GetSysData(43, ref strTemp))
            //    Error[5] = Convert.ToUInt16(strTemp);
            //if (GetSysData(44, ref strTemp))
            //    Error[6] = Convert.ToUInt16(strTemp);
            //if (GetSysData(45, ref strTemp))
            //    Error[7] = Convert.ToUInt16(strTemp);
            //if (GetSysData(46, ref strTemp))
            //    Error[8] = Convert.ToUInt16(strTemp);

            //Parent.waValue=valu
            Prepared = bPrepared;
            if (!Prepared)//硬件失联
            {
                //置1
                lock (Parent.EMSError)
                {
                    Parent.EMSError[0] &= 0xFFEF;
                    Parent.EMSError[0] |= 0x10;
                }
            }
            else
            {
                Parent.EMSError[0] &= 0xFFEF;//硬件连接成功,置0
            }

            if (aAkva == 0)
            {
                State = 0;
            }
            else
            {
                if (inputA == 0)//"待机", "停机", "充电", "放电" 
                    State = 0;
                else if (inputA > 0)
                    State = 3;
                else
                    State = 2;
            }
            time = DateTime.Now;
            //设置运行指示灯
            if (State > 0)
            {
                frmSet.RunStateGPIO(1);

            }
            else
            {
                frmSet.RunStateGPIO(0);
            }

            //处理故障
            ushort sData = 0;
            ushort sOldData = 0;
            ushort sKey = 0;
            int iData;

            //检查故障，并对比过去的故障
            //1:有故障 0：无故障
            for (int i = 0; i < 8; i++)
            {
                sData = Error[i];
                sOldData = OldError[i];

                if (sData != sOldData)//sOldData ！= sData :说明Error更新 
                {
                    sOldData = (ushort)(sOldData ^ sData); 
                    for (int j = 0; j < 16; j++)
                    {
                        sKey = (ushort)(1 << j);
                        iData = sOldData & sKey;
                        if ((iData > 0) && (ErrorClass.PCSErrorsPower1[16 * i + j] > 0))   //英博
                            RecodError("PCS", iot_code, 16 * i + j, ErrorClass.PCSErrorsPower1[16 * i + j], ErrorClass.PCSErrors1[16 * i + j], (sData & sKey) > 0);
                    }
                }
                OldError[i] = Error[i];
            }
        }


        /// <summary>
        /// 晶石pcs
        /// </summary>
        /// 
        public void GetallUkva()
        {
            string strTemp = "";
            string strData = "";
            bool bPrepared = false;
            if (GetSysData(6, ref strTemp))
            {
                bPrepared = true;
                allUkva = Convert.ToDouble(strTemp);
            }
            Prepared = bPrepared; 
        }

        /// <summary>
        /// 获取pcs部分数据
        /// </summary>
        public void GetLowDataFromEquipmentJS()
        {
            string strTemp = "";
            string strData = "";
            bool bPrepared = false;

            Thread.Sleep(1000);
            if (GetSysData(64, ref strTemp))
            {
                bPrepared = true;
                PcsRun = Convert.ToDouble(strTemp);
            }

            if (GetSysData(65, ref strTemp))
            {
                bPrepared = true;
                PCSwaType = Convert.ToUInt16(strTemp);
            }

            if (GetSysData(37, ref strTemp))
            {
                bPrepared = true;
                if (Get3strData(0, ref strTemp, ref strData))
                    aV = Math.Round(float.Parse(strData), 1);
                if (Get3strData(1, ref strTemp, ref strData))
                    bV = Math.Round(float.Parse(strData), 1);
                if (Get3strData(2, ref strTemp, ref strData))
                    cV = Math.Round(float.Parse(strData), 1);
                if (Get3strData(3, ref strTemp, ref strData))
                    aA = Math.Round(float.Parse(strData), 1);
                if (Get3strData(4, ref strTemp, ref strData))
                    bA = Math.Round(float.Parse(strData), 1);
                if (Get3strData(5, ref strTemp, ref strData))
                    cA = Math.Round(float.Parse(strData), 1);
                if (Get3strData(6, ref strTemp, ref strData))
                    allUkva = Math.Round(float.Parse(strData), 1);
                if (Get3strData(7, ref strTemp, ref strData))
                    allNUkvar = Math.Round(float.Parse(strData), 1);
                if (Get3strData(8, ref strTemp, ref strData))
                    allAkva = Math.Round(float.Parse(strData), 1);
                if (Get3strData(9, ref strTemp, ref strData))
                    hz = Math.Round(float.Parse(strData), 2);
                if (Get3strData(10, ref strTemp, ref strData))
                    allPFactor = Math.Round(float.Parse(strData), 3);
                if (Get3strData(11, ref strTemp, ref strData))
                    inputV = Math.Round(float.Parse(strData), 1);
                if (Get3strData(12, ref strTemp, ref strData))
                    inputA = Math.Round(float.Parse(strData), 1);
                if (Get3strData(13, ref strTemp, ref strData))
                    inputkva = Math.Round(float.Parse(strData), 1);
            }

            if (GetSysData(39, ref strTemp))
            {
                bPrepared = true;
                //状态  
                //Get3strData(26, ref strTemp, ref strData);
                if (Get3strData(27, ref strTemp, ref strData))
                    Error[0] = Convert.ToUInt16(strData);                
                if (Get3strData(28, ref strTemp, ref strData))
                    Error[1] = Convert.ToUInt16(strData);
                if (Get3strData(29, ref strTemp, ref strData))
                    Error[2] = Convert.ToUInt16(strData);
                if (Get3strData(30, ref strTemp, ref strData))
                    Error[3] = Convert.ToUInt16(strData);
            }

            Prepared = bPrepared;

            if (!Prepared)
            {
                if (PreparedCount < 10)
                {
                    PreparedCount++;
                }

                //如果连续通讯故障次数超过8次，则认为通讯故障
                if (PreparedCount > 8)
                {
                    lock (Parent.EMSError)
                    {
                        Parent.EMSError[0] &= 0xFFEF;
                        Parent.EMSError[0] |= 0x10;
                    }
                }

            }
            else
            {
                Parent.EMSError[0] &= 0xFFEF;
                //通讯成功，清除通讯故障记录
                PreparedCount = 0;
            }

            if (inputA > 0.5)
                State = 3;
            else if (inputA < -0.5)
                State = 2;
            else
                State = 0;

            time = DateTime.Now;
            //设置运行指示灯
            if (State > 0)
            {
                frmSet.RunStateGPIO(1);
            }
            else
            {
                frmSet.RunStateGPIO(0);
            }

            //同步云，储能柜的运行状态
            if (frmMain.Selffrm.AllEquipment.PCSList[0].PcsRun == 255)//pcs为关机状态
            {
                if (!frmMain.Selffrm.AllEquipment.ErrorState[2])//未发生3级故障
                {
                    frmMain.Selffrm.AllEquipment.runState = 2;//设置运行状态为停机
                }
                else
                {
                    frmMain.Selffrm.AllEquipment.runState = 1;//设置运行状态为故障
                }
            }
            else//pcs为开机状态
            {
                if (!frmMain.Selffrm.AllEquipment.ErrorState[2])
                {
                    frmMain.Selffrm.AllEquipment.runState = 0;//设置运行状态为运行
                }
                else
                {
                    frmMain.Selffrm.AllEquipment.runState = 1;//设置运行状态为故障
                }
            }


            //处理故障
            ushort sData = 0;
            ushort sOldData = 0;
            ushort sKey = 0;
            int iData;

            //检查故障，并对比过去的故障 
            for (int i = 0; i < 4; i++)
            {
                sData = Error[i];
                sOldData = OldError[i];
                if (sData != sOldData)
                {
                    sOldData = (ushort)(sOldData ^ sData);
                    for (int j = 0; j < 16; j++)
                    {
                        sKey = (ushort)(1 << (15-j));
                        iData = sOldData & sKey;
                        if ((iData > 0) && (ErrorClass.PCSErrorsPower2[16 * i + j] > 0))
                        {
                            RecodError("PCS", iot_code, 16 * i + j, ErrorClass.PCSErrorsPower2[16 * i + j], ErrorClass.PCSErrors2[16 * i + j], (sData & sKey) > 0);
                        }
                    }
                }
                OldError[i] = Error[i];
            }
        }

        //pcs 
        public void GetDataFromEqipment2()
        {
            string strTemp = "";
            string strData = "";
            bool bPrepared = false;

            if (GetSysData(63, ref strTemp))
            {
                bPrepared = true;
                if (Get3strData(59, ref strTemp, ref strData))
                    SoftwareVersion = strData;
                if (Get3strData(60, ref strTemp, ref strData))
                    SoftwareVersion += strData;
                if (Get3strData(61, ref strTemp, ref strData))
                    HardwareVersion = strData;
                if (Get3strData(62, ref strTemp, ref strData))
                    HardwareVersion += strData;
            }

            if (GetSysData(37, ref strTemp))
            {
                bPrepared = true;
                if (Get3strData(0, ref strTemp, ref strData))
                    aV = Math.Round(float.Parse(strData), 1);
                if (Get3strData(1, ref strTemp, ref strData))
                    bV = Math.Round(float.Parse(strData), 1);
                if (Get3strData(2, ref strTemp, ref strData))
                    cV = Math.Round(float.Parse(strData), 1);
                if (Get3strData(3, ref strTemp, ref strData))
                    aA = Math.Round(float.Parse(strData), 1);
                if (Get3strData(4, ref strTemp, ref strData))
                    bA = Math.Round(float.Parse(strData), 1);
                if (Get3strData(5, ref strTemp, ref strData))
                    cA = Math.Round(float.Parse(strData), 1);
                if (Get3strData(6, ref strTemp, ref strData))
                    allUkva = Math.Round(float.Parse(strData), 1);
                if (Get3strData(7, ref strTemp, ref strData))
                    allNUkvar = Math.Round(float.Parse(strData), 1);
                if (Get3strData(8, ref strTemp, ref strData))
                    allAkva = Math.Round(float.Parse(strData), 1);
                if (Get3strData(9, ref strTemp, ref strData))
                    hz = Math.Round(float.Parse(strData), 2);
                if (Get3strData(10, ref strTemp, ref strData))
                    allPFactor = Math.Round(float.Parse(strData), 3);
                if (Get3strData(11, ref strTemp, ref strData))
                    inputV = Math.Round(float.Parse(strData), 1);
                if (Get3strData(12, ref strTemp, ref strData))
                    inputA = Math.Round(float.Parse(strData), 1);
                if (Get3strData(13, ref strTemp, ref strData))
                    inputkva = Math.Round(float.Parse(strData), 1);
            }
            if (GetSysData(38, ref strTemp))
            {
                bPrepared = true;
                double dTemp = 0;
                //1
                if (Get3strData(14, ref strTemp, ref strData))
                { 
                    PCSTemp = Math.Round(float.Parse(strData), 1);
                    if (PCSTemp > 150) 
                        PCSTemp = 0;
                    IGBTTemp1 = PCSTemp;
                }
                //2
                if (Get3strData(15, ref strTemp, ref strData))
                {
                    dTemp = Math.Round(float.Parse(strData), 1);
                    if ((dTemp > PCSTemp) && (PCSTemp < 150))
                        PCSTemp = dTemp;
                    IGBTTemp2 = PCSTemp;
                }
                //3
                if (Get3strData(16, ref strTemp, ref strData))
                {
                    dTemp = Math.Round(float.Parse(strData), 1);
                    if ((dTemp > PCSTemp) && (PCSTemp < 150))
                        PCSTemp = dTemp;
                    IGBTTemp3 = PCSTemp;
                }
                //4
                if (Get3strData(17, ref strTemp, ref strData))
                {
                    dTemp = Math.Round(float.Parse(strData), 1);
                    if ((dTemp > PCSTemp) && (PCSTemp < 150))
                        PCSTemp = dTemp;
                    IGBTTemp4 = PCSTemp;
                }
                //5
                if (Get3strData(18, ref strTemp, ref strData))
                {
                    dTemp = Math.Round(float.Parse(strData), 1);
                    if ((dTemp > PCSTemp) && (PCSTemp < 150))
                        PCSTemp = dTemp;
                    IGBTTemp5 = PCSTemp;
                }
                //6
                if (Get3strData(19, ref strTemp, ref strData))
                {
                    dTemp = Math.Round(float.Parse(strData), 1);
                    if ((dTemp > PCSTemp) && (PCSTemp < 150))
                        PCSTemp = dTemp;
                    IGBTTemp6 = PCSTemp;
                }

                if (Get3strData(20, ref strTemp, ref strData))
                    InTemp = Math.Round(float.Parse(strData), 1); 
                if (Get3strData(21, ref strTemp, ref strData))
                    OutTemp = Math.Round(float.Parse(strData), 1); 
            }
            //
            if (GetSysData(58, ref strTemp))
            {
                bPrepared = true;
                if (Get3strData(22, ref strTemp, ref strData))
                    aUkva = Math.Round(float.Parse(strData), 1);
                if (Get3strData(23, ref strTemp, ref strData))
                    bUkva = Math.Round(float.Parse(strData), 1);
                if (Get3strData(24, ref strTemp, ref strData))
                    cUkva = Math.Round(float.Parse(strData), 1);
                if (Get3strData(25, ref strTemp, ref strData))
                    DCInputV = Math.Round(float.Parse(strData), 3);
            }

            if (GetSysData(39, ref strTemp))
            {
                bPrepared = true;
                //状态
                //Get3strData(26, ref strTemp, ref strData);
                if (Get3strData(27, ref strTemp, ref strData))
                    Error[0] = Convert.ToUInt16(strData); 
                if (Get3strData(28, ref strTemp, ref strData))
                    Error[1] = Convert.ToUInt16(strData);
                if (Get3strData(29, ref strTemp, ref strData))
                    Error[2] = Convert.ToUInt16(strData);
                if (Get3strData(30, ref strTemp, ref strData))
                    Error[3] = Convert.ToUInt16(strData);
            }

            //11.16 : PCS添加设备状态字
            if (GetSysData(26, ref strTemp))
            {
                bPrepared = true;
                PcsStatus = Convert.ToUInt16(strTemp);
            }

            //12.15 PCS添加启动/停机状态
            if (GetSysData(64, ref strTemp))
            {
                bPrepared = true;
                PcsRun = Convert.ToDouble(strTemp);
            }
            if (GetSysData(65, ref strTemp))
            {
                bPrepared = true;
                PCSwaType = Convert.ToUInt16(strTemp);
            }
            
            //判断PCS的通讯状态
            Prepared = bPrepared;
            if (!Prepared)
            {
                if (PreparedCount < 10)
                {
                    PreparedCount++;
                }

                //如果连续通讯故障次数超过8次，则认为空调通讯故障
                if (PreparedCount > 6)
                {
                    lock (Parent.EMSError)
                    {
                        Parent.EMSError[0] &= 0xFFEF;
                        Parent.EMSError[0] |= 0x10;
                    }
                }

            }
            else
            {
                Parent.EMSError[0] &= 0xFFEF;
                //通讯成功，清除通讯故障记录
                PreparedCount = 0;
            }

            if (inputA > 0.5)
                State = 3;
            else if (inputA < -0.5)
                State = 2;
            else
                State = 0;
             
            time = DateTime.Now;
            //设置运行指示灯
            if (State > 0)
            {
                frmSet.RunStateGPIO(1);
            }  
            else
            {
                frmSet.RunStateGPIO(0);
            }

            //同步云，储能柜的运行状态
            if (frmMain.Selffrm.AllEquipment.PCSList[0].PcsRun == 255)//pcs为关机状态
            {
                if (!frmMain.Selffrm.AllEquipment.ErrorState[2])//未发生3级故障
                {
                    frmMain.Selffrm.AllEquipment.runState = 2;//设置运行状态为停机
                }
                else
                {
                    frmMain.Selffrm.AllEquipment.runState = 1;//设置运行状态为故障
                }
            }
            else//pcs为开机状态
            {
                if (!frmMain.Selffrm.AllEquipment.ErrorState[2])
                {
                    frmMain.Selffrm.AllEquipment.runState = 0;//设置运行状态为运行
                }
                else
                {
                    frmMain.Selffrm.AllEquipment.runState = 1;//设置运行状态为故障
                }
            }

            //处理故障
            ushort sData = 0;
            ushort sOldData = 0;
            ushort sKey = 0;
            int iData;

            //检查故障，并对比过去的故障 
            for (int i = 0; i < 4; i++)
            {
                sData = Error[i];
                sOldData = OldError[i];

                if (sData != sOldData)
                {
                    sOldData = (ushort)(sOldData ^ sData);
                    for (int j = 0; j < 16; j++)
                    {
                        sKey = (ushort)(1 << (15-j));
                        iData = sOldData & sKey;
                        if ((iData > 0) && (ErrorClass.PCSErrorsPower2[16 * i + j] > 0))  //晶石
                        {
                            RecodError("PCS", iot_code, 16 * i + j, ErrorClass.PCSErrorsPower2[16 * i + j], ErrorClass.PCSErrors2[16 * i + j], (sData & sKey) > 0);
                        }
                    }
                }
                OldError[i] = Error[i];
            }
        }


        //兼容了英博和精石的协议
        override public void Save2DataSource(string arDate)
        {
            //基本信息
            DBConnection.ExecSQL("insert pcs (rTime, State,aV, bV, cV, aA ,bA , cA , hz ,"
                + " aUkwa,  bUkwa,  cUkwa,  allUkwa,   aNUkwr, bNUkwr,   cNUkwr, allNUkwr, "
                + " aAkwa,  bAkwa,  cAkwa,  allAkwa, aPFactor, bPFactor,   cPFactor,  allPFactor,"
                + " inputPower, inputV,  inputA, PCSTemp, "
                + " ACInkwh,ACOutkwh,DCInkwh,DCOutkwh,"
                + "Error1,Error2,Error3,Error4,Error7 )value('"
                 + arDate + "','" + State.ToString() + "','"  // rTime.ToString("yyyy-M-d H:m:s")
                 + aV.ToString() + "','" + bV.ToString() + "','" + cV.ToString() + "','"
                 + aA.ToString() + "','" + bA.ToString() + "','" + cA.ToString() + "','" + hz.ToString() + "','"
                 + aUkva.ToString() + "','" + bUkva.ToString() + "','" + cUkva.ToString() + "','" + allUkva.ToString() + "','"
                 + aNUkvar.ToString() + "','" + bNUkvar.ToString() + "','" + cNUkvar.ToString() + "','" + allNUkvar.ToString() + "','"
                 + aAkva.ToString() + "','" + bAkva.ToString() + "','" + cAkva.ToString() + "','" + allAkva.ToString() + "','"
                 + aPFactor.ToString() + "','" + bPFactor.ToString() + "','" + cPFactor.ToString() + "','" + allPFactor.ToString() + "','"
                 + inputkva.ToString() + "','" + inputV.ToString() + "','" + inputA.ToString() + "','" + PCSTemp.ToString() + "','"
                 + ACInkwh.ToString() + "','" + ACOutkwh.ToString() + "','" + DCInkwh.ToString() + "','" + DCOutkwh.ToString() + "','"
                + Error[0].ToString() + "','" + Error[1].ToString() + "','" + Error[2].ToString() + "','"
                + Error[3].ToString() + "','" + Error[7].ToString() + "')");
        }

    }

    //BMS Qiao
    public class BMSClass : BaseEquipmentClass
    {
        static public string[] strRunStates = { "正常", "禁充", "禁放", "待机", "停机" };
        static public string[] strProchargStates = { "断网", "启动并网", "并网中", "并网成功", "并网失败", "" };
        static public string[] strSwitchs = { "主接触状态", "预充接触器状态", "主负接触状态", "隔离开关状态" };
        static public string[] strRCutFailures = { "总压差超限", "允许切入超时" };
        static public string[] strRNotCutIns = {"总压压差超限","Type1报警未全部解除","绝缘报警未解除","不满足单体电压过高切出后的切入条件",
            "不满足单体过低切出后的切入条件","CriticalAlarm未全解除" };

        public int AllKey { get; set; } = 0;//隔离开关
        public int AllZ { get; set; } = 0;//总正接触器
        public int AllF { get; set; } = 0;//总负接触器
        public int ChargKey { get; set; } = 0;//预充接触器

        public int batteryID;
       
        public short prochargState { get; set; } //预充电状态
        public short SwitchState { get; set; }   //接触器状态
        public short chargState { get; set; }    //充放电指示-静置、放电、充电
        public ushort RCutFailures; //切入失败原因
        public ushort RNotCutIn;    //未能切入原因
        public double prochargV;    //预充电电压
        public double v { get; set; }
        public double a { get; set; }
        public double soc { get; set; }
        public double soh { get; set; }
        public double insulationR { get; set; }   //绝缘电阻
        public double positiveR { get; set; }     //接线柱电阻
        public double negativeR { get; set; }     // 接线柱电阻

        public double MaxChargeA { get; set; }     //允许最大充电电流   单相：I=P/220； 三相：I=P/（1.73×380）
        public double MaxDischargeA { get; set; }  //允许放电最大电流   三相：P=I×1.732×U               

        public double cellMaxV { get; set; }
        public short cellIDMaxV { get; set; }
        public double cellMinV { get; set; }
        public short cellIDMinV { get; set; }
        public double cellMaxTemp { get; set; }
        public short cellIDMaxtemp { get; set; }
        public double cellMinTemp { get; set; }
        public short cellIDMintemp { get; set; }
        public double averageV { get; set; }
        public double averageTemp { get; set; }
        public  int runState { get; set; }      //运行状态
        public ushort[] Error { get; set; } = { 0, 0, 0, 0, 0 };//故障，警告123，BMU通讯故障
        public ushort[] OldError = { 0, 0, 0, 0, 0 };

        public double[] HVBoxTemps { get; set; } = new double[4];//高压箱的温度
        public double[] CellTemps { get; set; } = new double[240];//
        public double[] CellVs { get; set; } = new double[240];//
        public double[,] PackTemp = new double[12, 6];//每个pack有正极板，正极总线，负极板，负极接线柱，过桥板1，2

        //8.3
        public double cellErrPV1; //单体过压一级报警门限
        public double cellErrUPV1; //单体过压一级恢复门限
        public double cellErrPV2; //单体过压二级报警门限
        public double cellErrUPV2; //单体过压二级恢复门限
        public double cellErrPV3; //单体欠压三级报警门限
        public double cellErrUPV3; //单体过压三级报警门限

        //9.6
        public ushort[] BalaSwitch = new ushort[25];

        public float ChargeAmount = 0;    //可充电量 
        public float DisChargeAmount = 0; //可放电量

        //11.30 新增字段 ， 云平台判断水冷和液冷
        public int BMStype { get; set; } = 0; //默认未0  1：风冷 2：液冷

        public double BMSCap { get; set; } // BMS当前容量 

        private static ILog log = LogManager.GetLogger("BMSClass");


        //单电池的信息列表
        //public List<CellClass> CellList = new List<CellClass>();

        public BMSClass()
        {
            strCommandFile = "BMS.txt";
        }

        public void ClearBmsBala()
        {
            for (int i = 0; i < 25; i++)
            {
                BalaSwitch[i] = 0;
                SetSysData(i + 60, BalaSwitch[i], false);                    
            }
        }


        public void StartBmsBala()
        {
            //获取均衡开关状态
            GetBalaInfo();
            //获取均衡开关

            double index = 0;
            double num = 0; //被动均衡开关序号
            double bit = 0; //某一开关下需要被动均衡单体的序号
            ushort bala;
            ushort[] NewBalaSwitch = new ushort[25];


                foreach (double i in frmMain.Selffrm.AllEquipment.balaCellID)
                {
                    index = i;

                    bit = index % 16;

                    //被动均衡开关序号
                    if (bit ==0)
                    {
                        num = index / 16 - 1;
                    }
                    else
                        num = index / 16;

                    //电池序号 1-16

                    if (bit == 0)
                    {
                        bit = 15;
                    }
                    else
                        bit -= 1;
                    
                    //均衡开关地址置位
                    bala = (ushort)(1<<(int)bit);
                    if (NewBalaSwitch[(int)num] == 0)
                    {
                        bala |= BalaSwitch[(int)num];
                        NewBalaSwitch[(int)num] = bala;
                    }
                    else
                    {
                        NewBalaSwitch[(int)num] |= bala;
                    }
                       
                    
                }

                //开启或关闭均衡开关
                for (int i = 0; i < 25; i++)
                {
                    if (NewBalaSwitch[i] != 0)
                    {
                        SetSysData(i + 60, NewBalaSwitch[i], false);
                    }
                }

                

            
        }



        //9.8 设置BMS均衡开关
        //aData : 开关  ; index : 电池序号（1~240）
        public void SetBmsBala(int aData, double index)
        {
            double num = 0; //被动均衡开关序号
            double bit = 0; //某一开关下需要被动均衡单体的序号

            bit = index % 16;

            //被动均衡开关序号
            if (bit ==0 )
            { 
                num = index / 16 - 1;
            }
            else
                num = index / 16;

            //电池序号 1-16

            if (bit == 0)
            {
                bit = 15;
            }
            else
                bit -= 1;

            ushort bala;

            if (aData == 1)
            {
                bala = (ushort)(1<<(int)bit);
                bala |= BalaSwitch[(int)num]; 
                SetSysData((int)num + 60 , bala, false);
            }
            else if (aData == 0)
            {
                bala = (ushort)((1<<(int)bit)-1);
                bala &= BalaSwitch[(int)num];
                SetSysData((int)num + 60 , bala, false);
            }

        }

        //8.2设置BMS过压告警和恢复
        public void SetBmsPV1(int aData)
        {
            if (aData != 0)
            {
                SetSysData(54, aData, false);
            }
        }
        public void SetBmsUPV1(int aData)
        {
            if (aData != 0)
            {
                SetSysData(55, aData, false);
            }
        }
        public void SetBmsPV2(int aData)
        {
            if (aData != 0)
            {
                SetSysData(56, aData, false);
            }
        }
        public void SetBmsUPV2(int aData)
        {
            if (aData != 0)
            {
                SetSysData(57, aData, false);
            }
        }
        public void SetBmsPV3(int aData)
        {
            if (aData != 0)
            {
                SetSysData(58, aData, false);
            }
        }
        public void SetBmsUPV3(int aData)
        {
            if (aData != 0)
            {
                SetSysData(59, aData, false);
            }
        }


        /// <summary>
        /// 打开BMS
        /// </summary>
        /// <param name="aOnData"></param>
        /// <returns></returns>
        public void PowerOn(bool aOnData)
        {
            if (aOnData)
                SetSysData(37, 0x001,true);// 启动
            else
                SetSysData(37, 0x000,true);// 关闭
        }


        private void UpdateCellTemp(double[] aCellTemps, int aStart, string aData)
        {

            switch (frmSet.BMSVerb)
            {
                //case 0:
                //     UpdateCellTemp0(aCellTemps, aStart, aData);
                //    break;
                //case 1:
                default: 
                    UpdateCellTemp1(aCellTemps, aStart, aData);
                    break;
            }
        }

            /// <summary>
            /// 内嵌式电芯温度，质检院BMs和以前3台设备的BMS
            /// </summary>
            /// <param name="aCellTemps"></param>
            /// <param name="aData"></param>
        private void UpdateCellTemp0(double[] aCellTemps, int aStart, string aData)
        {
            int iStartIndex = 0;
            double dTemp;
            if (aStart > 0)
                iStartIndex = 6;

            for (int i = 0; i < 6; i++)
            {
                //1-10
                for (int j = 0; j < 5; j++)
                {
                    dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                    aData = aData.Substring(4, aData.Length - 4);
                    aCellTemps[aStart + i * 20 + 2 * j] = Math.Round(dTemp, 1);
                    aCellTemps[aStart + i * 20 + 2 * j + 1] = aCellTemps[aStart + i * 20 + 2 * j];
                }
                //6负基板
                dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                PackTemp[iStartIndex + i, 2] = Math.Round(dTemp, 1);
                aData = aData.Substring(4, aData.Length - 4);
                //7过桥
                dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                PackTemp[iStartIndex + i, 4] = Math.Round(dTemp, 1);
                aData = aData.Substring(4, aData.Length - 4);
                //8负总接线柱
                dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                PackTemp[iStartIndex + i, 3] = Math.Round(dTemp, 1);
                aData = aData.Substring(4, aData.Length - 4);
                //11-20
                for (int j = 0; j < 5; j++)
                {
                    dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                    aData = aData.Substring(4, aData.Length - 4);
                    // aCellTemps[aStart + i * 20 + 2 * j + 10] = Math.Round(dTemp, 1);
                    // aCellTemps[aStart + i * 20 + 2 * j + 11] = aCellTemps[aStart + i * 20 + 2 * j + 10];
                    aCellTemps[aStart + i * 20 - 2 * j + 18] = Math.Round(dTemp, 1);
                    aCellTemps[aStart + i * 20 - 2 * j + 19] = Math.Round(dTemp, 1);
                }
                //14 正基板
                dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                PackTemp[iStartIndex + i, 0] = Math.Round(dTemp, 1);
                aData = aData.Substring(4, aData.Length - 4);
                //15 过桥
                dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                PackTemp[iStartIndex + i, 5] = Math.Round(dTemp, 1);
                aData = aData.Substring(4, aData.Length - 4);
                //16 正总接线柱
                dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                PackTemp[iStartIndex + i, 1] = Math.Round(dTemp, 1);
                aData = aData.Substring(4, aData.Length - 4);

            }
            //int aStart
            //for (int i = 0; i < 120; i++)//aCellTemps.Length
            //{
            //    if (aData.Length >= 4)
            //    {
            //        aCellTemps[i+ aStart] = (float)(Convert.ToInt32("0X"+aData.Substring(0, 4),16) * 0.1);
            //        aData = aData.Substring(4, aData.Length - 4);
            //    }
            //    else
            //    {
            //        aCellTemps[i] = 0;
            //    }
            //} 
        }

        /// <summary>
        /// 电芯温度，定制的BMS温度部分
        /// </summary>
        /// <param name="aCellTemps"></param>
        /// <param name="aData"></param>
        private void UpdateCellTemp1(double[] aCellTemps, int aStart, string aData)
        { 
            double dTemp;  
            for (int i = 0; i < 6; i++)
            {
                //1-10
                for (int j = 0; j < 14; j++)
                {
                    dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                    aData = aData.Substring(4, aData.Length - 4);
                    aCellTemps[aStart + i * 20 + j] = Math.Round(dTemp, 1); 
                }
              
            } 
        }
        //正接线柱的温度
        private void UpdatePTemp(double[] aCellTemps, string aData)
        {
            double dTemp;
            for (int i = 0; i < 12; i++)
            {
                dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                aData = aData.Substring(4, aData.Length - 4);
                aCellTemps[i * 20 + 14] = Math.Round(dTemp, 1);
            }
        }
        //负接线柱的温度
        private void UpdateOTemp(double[] aCellTemps, string aData)
        {
            double dTemp;
            for (int i = 0; i < 12; i++)
            {
                dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                aData = aData.Substring(4, aData.Length - 4);
                aCellTemps[i * 20 + 15] = Math.Round(dTemp, 1);
            }
        }

        /******************************** 液冷 ********************************/
        //正接线柱的温度_liquidcool
        private void UpdatePTemp_liquidcool(double[] aCellTemps, string aData)
        {
            double dTemp;
            for (int i = 0; i < 5; i++)
            {
                dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                aData = aData.Substring(4, aData.Length - 4);
                aCellTemps[i * 48 + 28] = Math.Round(dTemp, 1);
                dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                aData = aData.Substring(4, aData.Length - 4);
                aCellTemps[i * 48 + 30] = Math.Round(dTemp, 1);
            }
        }
        //负接线柱的温度_liquidcool
        private void UpdateOTemp_liquidcool(double[] aCellTemps, string aData)
        {
            double dTemp;
            for (int i = 0; i < 5; i++)
            {
                dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                aData = aData.Substring(4, aData.Length - 4);
                aCellTemps[i * 48 + 29] = Math.Round(dTemp, 1);
                dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                aData = aData.Substring(4, aData.Length - 4);
                aCellTemps[i * 48 + 31] = Math.Round(dTemp, 1);
            }
        }
        private void UpdateCellTemp_liquidcool(double[] aCellTemps, int aStart, string aData)
        {
            double dTemp;
            for (int i = 0; i < 3; i++)
            {
                //1-10
                for (int j = 0; j < 28; j++)
                {
                    dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                    aData = aData.Substring(4, aData.Length - 4);
                    aCellTemps[aStart + i * 48 + j] = Math.Round(dTemp, 1);
                }

            }
        }
        private void UpdateCellTemp2_liquidcool(double[] aCellTemps, int aStart, string aData)
        {
            double dTemp;
            for (int i = 0; i < 2; i++)
            {
                //1-10
                for (int j = 0; j < 28; j++)
                {
                    dTemp = (double)(Convert.ToInt32("0X" + aData.Substring(0, 4), 16) * 0.1);
                    aData = aData.Substring(4, aData.Length - 4);
                    aCellTemps[aStart + i * 48 + j] = Math.Round(dTemp, 1);
                }

            }
        }
        /******************************** 液冷 ********************************/

        /// <summary>
        /// 电芯的电压
        /// </summary>
        /// <param name="aCellList"></param>
        /// <param name="aStart"></param>
        /// <param name="aData"></param>
        private void UpdateCellV(double[] aCellV, int aStart, string aData)//List<CellClass> aCellList
        {
            string strData = "";
            double dTemp;
            for (int i = 0; i < 120; i++)//aCellTemps.Length
            {
                if (aData.Length >= 4)
                {
                    strData = "0x" + aData.Substring(0, 4);
                    aData = aData.Substring(4, aData.Length - 4);
                    dTemp = (float)(Convert.ToInt32(strData, 16) * 0.001);
                    aCellV[i + aStart] = Math.Round(dTemp, 3);
                }
                else
                {
                    aCellV[i] = 0;
                }
            }
        }

        public void GetCellErrUPVInfo()
        {
            string strTemp = "";
            string strData = "";
            bool bPrepared = false;
            //8.3读取BMS 1，2，3级单体过压告警和恢复门限
            if (GetSysData(48, ref strTemp))
            {
                bPrepared = true;
                cellErrPV1 =  Math.Round(float.Parse(strTemp), 3);
            }
            if (GetSysData(49, ref strTemp))
            {
                bPrepared = true;
                cellErrUPV1 =  Math.Round(float.Parse(strTemp), 3);
            }
            if (GetSysData(50, ref strTemp))
            {
                bPrepared = true;
                cellErrPV2 =  Math.Round(float.Parse(strTemp), 3);
            }
            if (GetSysData(51, ref strTemp))
            {
                bPrepared = true;
                cellErrUPV2 =  Math.Round(float.Parse(strTemp), 3);
            }
            if (GetSysData(52, ref strTemp))
            {
                bPrepared = true;
                cellErrPV3 =  Math.Round(float.Parse(strTemp), 3);
            }
            if (GetSysData(53, ref strTemp))
            {
                bPrepared = true;
                cellErrUPV3 =  Math.Round(float.Parse(strTemp), 3);
            }

        }

        public void Get104Info()
        {
            string strTemp = "";
            string strData = "";
            bool bPrepared = false;

            //可充电量 可放电量
            if (GetSysData(111, ref strTemp))
            {
                bPrepared = true;
                ChargeAmount = float.Parse(strTemp);
            }
            if (GetSysData(112, ref strTemp))
            {
                bPrepared = true;
                DisChargeAmount = float.Parse(strTemp);
            }

            //读取BMS当前容量
            BMSCap = (frmSet.SysSelfPower * frmMain.Selffrm.AllEquipment.BMS.soc * frmMain.Selffrm.AllEquipment.BMS.soh / 10000);
        }

        public void GetBalaInfo()
        {
            string strTemp = "";
            string strData = "";
            bool bPrepared = false;

            //读取被动均衡开关状态
            if (GetSysData(110, ref strTemp))
            {
                bPrepared = true;
                if (Get3strData(85, ref strTemp, ref strData))
                    BalaSwitch[0] = Convert.ToUInt16(strData);
                if (Get3strData(86, ref strTemp, ref strData))
                    BalaSwitch[1] = Convert.ToUInt16(strData);
                if (Get3strData(87, ref strTemp, ref strData))
                    BalaSwitch[2] = Convert.ToUInt16(strData);
                if (Get3strData(88, ref strTemp, ref strData))
                    BalaSwitch[3] = Convert.ToUInt16(strData);
                if (Get3strData(89, ref strTemp, ref strData))
                    BalaSwitch[4] = Convert.ToUInt16(strData);
                if (Get3strData(90, ref strTemp, ref strData))
                    BalaSwitch[5] = Convert.ToUInt16(strData);
                if (Get3strData(91, ref strTemp, ref strData))
                    BalaSwitch[6] = Convert.ToUInt16(strData);
                if (Get3strData(92, ref strTemp, ref strData))
                    BalaSwitch[7] = Convert.ToUInt16(strData);
                if (Get3strData(93, ref strTemp, ref strData))
                    BalaSwitch[8] = Convert.ToUInt16(strData);
                if (Get3strData(94, ref strTemp, ref strData))
                    BalaSwitch[9] = Convert.ToUInt16(strData);
                if (Get3strData(95, ref strTemp, ref strData))
                    BalaSwitch[10] = Convert.ToUInt16(strData);
                if (Get3strData(96, ref strTemp, ref strData))
                    BalaSwitch[11] = Convert.ToUInt16(strData);
                if (Get3strData(97, ref strTemp, ref strData))
                    BalaSwitch[12] = Convert.ToUInt16(strData);
                if (Get3strData(98, ref strTemp, ref strData))
                    BalaSwitch[13] = Convert.ToUInt16(strData);
                if (Get3strData(99, ref strTemp, ref strData))
                    BalaSwitch[14] = Convert.ToUInt16(strData);
                if (Get3strData(100, ref strTemp, ref strData))
                    BalaSwitch[15] = Convert.ToUInt16(strData);
                if (Get3strData(101, ref strTemp, ref strData))
                    BalaSwitch[16] = Convert.ToUInt16(strData);
                if (Get3strData(102, ref strTemp, ref strData))
                    BalaSwitch[17] = Convert.ToUInt16(strData);
                if (Get3strData(103, ref strTemp, ref strData))
                    BalaSwitch[18] = Convert.ToUInt16(strData);
                if (Get3strData(104, ref strTemp, ref strData))
                    BalaSwitch[19] = Convert.ToUInt16(strData);
                if (Get3strData(105, ref strTemp, ref strData))
                    BalaSwitch[20] = Convert.ToUInt16(strData);
                if (Get3strData(106, ref strTemp, ref strData))
                    BalaSwitch[21] = Convert.ToUInt16(strData);
                if (Get3strData(107, ref strTemp, ref strData))
                    BalaSwitch[22] = Convert.ToUInt16(strData);
                if (Get3strData(108, ref strTemp, ref strData))
                    BalaSwitch[23] = Convert.ToUInt16(strData);
                if (Get3strData(109, ref strTemp, ref strData))
                    BalaSwitch[24] = Convert.ToUInt16(strData);

            }
            //10.16判断均衡运行状态
            for (int i = 0; i < 25; ++i)
            {
                if (BalaSwitch[i] != 0)
                {
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        //更新均衡运行状态
                        frmMain.Selffrm.AllEquipment.BalaRun = 1;
                        frmMain.BalaTacticsList.BalaHasOn = true;
                        break;
                    }
                }
                else
                {
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        //更新均衡运行状态
                        frmMain.Selffrm.AllEquipment.BalaRun = 0;
                    }
                }
            }
        }


        public void GetBaseInfo()
        {
            string strTemp = "";
            string strData = "";
            bool bPrepared = false;

            if (GetSysData(32, ref strTemp))
            {
                bPrepared = true;
                Error[4] = Convert.ToUInt16(strTemp);
            }

            if (GetSysData(45, ref strTemp))
            {
                bPrepared = true;
                if (Get3strData(41, ref strTemp, ref strData))
                    HVBoxTemps[0] = Math.Round(float.Parse(strData), 1);
                if (Get3strData(42, ref strTemp, ref strData))
                    HVBoxTemps[1] = Math.Round(float.Parse(strData), 1);
                if (Get3strData(43, ref strTemp, ref strData))
                    HVBoxTemps[2] = Math.Round(float.Parse(strData), 1);
                if (Get3strData(44, ref strTemp, ref strData))
                    HVBoxTemps[3] = Math.Round(float.Parse(strData), 1);
            }
            //正接线柱温度
            if (GetSysData(46, ref strTemp))
            {
                bPrepared = true;
                switch (BMStype)
                {
                    case 1:
                        UpdatePTemp(CellTemps, strTemp);
                        break;
                    case 2:
                        UpdatePTemp_liquidcool(CellTemps, strTemp);
                        break;
                }
            }
            //负接线柱温度
            if (GetSysData(47, ref strTemp))
            {
                bPrepared = true;
                switch (BMStype) 
                {
                    case 1:
                        UpdateOTemp(CellTemps, strTemp);
                        break;
                    case 2:
                        UpdateOTemp_liquidcool(CellTemps, strTemp);
                        break;
                }                

            }

            //电池信息读取
            if (GetSysData(33, ref strTemp))
            {
                bPrepared = true;
                UpdateCellV(CellVs, 0, strTemp);//CellList
            }
            if (GetSysData(34, ref strTemp))
            {
                bPrepared = true;
                UpdateCellV(CellVs, 120, strTemp);
            }
            //温度
            if (GetSysData(35, ref strTemp))
            {
                switch (BMStype)
                {
                    case 1:
                        UpdateCellTemp(CellTemps, 0, strTemp);
                        break;
                    case 2:
                        UpdateCellTemp_liquidcool(CellTemps, 0, strTemp);
                        break;
                }
            }
            if (GetSysData(36, ref strTemp))
            {
                switch (BMStype)
                {
                    case 1:
                        UpdateCellTemp(CellTemps, 120, strTemp);
                        break;
                    case 2:
                        UpdateCellTemp2_liquidcool(CellTemps, 144, strTemp);
                        break;
                }
            }

            //单独区分接触器状态

            //隔离开关
            if ((SwitchState & 8) > 0)   AllKey = 1;
            else                         AllKey = 0;

            //预充接触器状态
            if ((SwitchState & 2) > 0)  ChargKey = 1;
            else                        ChargKey = 0;

            //总负接触器
            if ((SwitchState & 4) > 0)
            {
                AllF = 1;
                Error[0] &= 0xEFFF;
            }
            else
            {
                AllF = 0;
                Error[0] |= 0x1000;//10.20修正 0x0400
            }
            //总正接触器
            if ((SwitchState & 1) > 0)
            {
                AllZ = 1;
                Error[0] &= 0xF7FF;
            }
            else
            {
                AllZ = 0;
                Error[0] |= 0x0800;//10.20修正 0x0200
            }

            //判断BMS的通信状态
            Prepared = bPrepared;
            if (!Prepared)
            {
                lock (Parent.EMSError)
                {
                    Parent.EMSError[0] &= 0xFFDF;
                    Parent.EMSError[0] |= 0x20;//只要这个
                }
            }
            else
            {
                Parent.EMSError[0] &= 0xFFDF;
            }

            Parent.BMSKVA = Math.Round(v * a / 1000, 2);
            Parent.BMSSOC = soc;
            Parent.BMSSOH = soh;
            time = DateTime.Now;

            //检查故障，并对比过去的故障
            ushort sData = 0;
            ushort sOldData = 0;
            ushort sKey = 0;
            int iData;
            //检查故障，并对比过去的故障
            for (int i = 0; i < 5; i++)
            {
                sData = Error[i];
                sOldData = OldError[i];
                if (sData != sOldData)
                {
                    sOldData = (ushort)(sOldData ^ sData);
                    for (int j = 0; j < 16; j++)
                    {
                        sKey = (ushort)(1 << j);
                        iData = sOldData & sKey;
                        if ((iData > 0) && (ErrorClass.BMSErrorsPower[16 * i + j] > 0))//更新故障信息
                            RecodError("BMS", iot_code, 16 * i + j, ErrorClass.BMSErrorsPower[16 * i + j], ErrorClass.BMSErrors[16 * i + j], (sData & sKey) > 1);
                    }
                }
                OldError[i] = Error[i];
            }

        }

        public static void Delay(int mm)
        {
            DateTime current = DateTime.Now;
            while (current.AddMilliseconds(mm) > DateTime.Now)
            {
                Application.DoEvents();
            }
            return;
        }

        public void GetErrorFromEquipment()
        {
            string strTemp = "";
            string strData = "";
            if (GetSysData(113, ref strTemp))
            {
                if (Get3strData(5, ref strTemp, ref strData))
                    Error[0] = Convert.ToUInt16(strData);
                if (Get3strData(6, ref strTemp, ref strData))//40968 soc过低 1010 0000 0000 1000
                    Error[1] = Convert.ToUInt16(strData);   //32776  soc过低单体电压过低1000 0000 0000 1000 
                if (Get3strData(7, ref strTemp, ref strData))//8200  单体压差过大 0010 0000 0000 1000
                    Error[2] = Convert.ToUInt16(strData);
                if (Get3strData(8, ref strTemp, ref strData))
                    Error[3] = Convert.ToUInt16(strData);
            }
        }

        override public void GetDataFromEqipment()
        {//bms

            string strTemp = "";
            string strData = "";
            bool bPrepared = false;

            if (GetSysData(1, ref strTemp))
            { 
                bPrepared = true;
                if (Get3strData(2, ref strTemp, ref strData))
                    runState = Convert.ToInt16(strData);
                if (Get3strData(3, ref strTemp, ref strData))
                    prochargState = Convert.ToInt16(strData);
                if (Get3strData(4, ref strTemp, ref strData))
                    SwitchState = Convert.ToInt16(strData);


                if (Get3strData(5, ref strTemp, ref strData))
                    Error[0] = (ushort)(Convert.ToUInt16(strData) | (6144 & Error[0]));
                if (Get3strData(6, ref strTemp, ref strData))//40968 soc过低 1010 0000 0000 1000
                    Error[1] = Convert.ToUInt16(strData);   //32776  soc过低单体电压过低1000 0000 0000 1000 
                if (Get3strData(7, ref strTemp, ref strData))//8200  单体压差过大 0010 0000 0000 1000
                    Error[2] = Convert.ToUInt16(strData);
                if (Get3strData(8, ref strTemp, ref strData))
                    Error[3] = Convert.ToUInt16(strData);  //8 单体电压过低 1000
                                                           //if (GetSysData(9, ref strTemp))
                                                           //   RCutFailures = (ushort)Convert.ToInt16(strTemp);
                                                           //if (GetSysData(10, ref strTemp))
                                                           //    RNotCutIn = Convert.ToInt16(strTemp); FFFF
                Get3strData(9, ref strTemp, ref strData);
                Get3strData(10, ref strTemp, ref strData);
                if (Get3strData(11, ref strTemp, ref strData))
                    prochargV = Math.Round(float.Parse(strData), 1);
                if (Get3strData(12, ref strTemp, ref strData))
                    v = Math.Round(float.Parse(strData), 1);
                if (Get3strData(13, ref strTemp, ref strData))
                    a = Math.Round(float.Parse(strData), 1);
                if (Get3strData(14, ref strTemp, ref strData))
                    chargState = Convert.ToInt16(strData);
                if (Get3strData(15, ref strTemp, ref strData))
                    soc = Math.Round(float.Parse(strData), 1);
                if (Get3strData(16, ref strTemp, ref strData))
                    soh = Math.Round(float.Parse(strData), 1);
                if (Get3strData(17, ref strTemp, ref strData))
                    insulationR = Math.Round(float.Parse(strData), 1);
                if (Get3strData(18, ref strTemp, ref strData))
                    positiveR = Math.Round(float.Parse(strData), 1);
                if (Get3strData(19, ref strTemp, ref strData))
                    negativeR = Math.Round(float.Parse(strData), 1);
                if (Get3strData(20, ref strTemp, ref strData))
                    MaxChargeA = Math.Round(float.Parse(strData), 1);
                if (Get3strData(21, ref strTemp, ref strData))
                    MaxDischargeA = Math.Round(float.Parse(strData), 1);
                
                if (Get3strData(22, ref strTemp, ref strData))
                    cellIDMaxV = Convert.ToInt16(strData);
                if (Get3strData(23, ref strTemp, ref strData))
                    cellMaxV = Math.Round(float.Parse(strData), 3);
                if (Get3strData(24, ref strTemp, ref strData))
                    cellIDMinV = Convert.ToInt16(strData);
                if (Get3strData(25, ref strTemp, ref strData))
                    cellMinV = Math.Round(float.Parse(strData), 3);
                if (Get3strData(26, ref strTemp, ref strData))
                    cellIDMaxtemp = Convert.ToInt16(strData);
                if (Get3strData(27, ref strTemp, ref strData))
                    cellMaxTemp = Math.Round(float.Parse(strData), 1);
                if (Get3strData(28, ref strTemp, ref strData))
                    cellIDMintemp = Convert.ToInt16(strData);
                if (Get3strData(29, ref strTemp, ref strData))
                    cellMinTemp = Math.Round(float.Parse(strData), 1);
                
                if (Get3strData(30, ref strTemp, ref strData))
                    averageV = Math.Round(float.Parse(strData), 3);
                if (Get3strData(31, ref strTemp, ref strData))
                    averageTemp = Math.Round(float.Parse(strData), 1);
            }
            Prepared = bPrepared;
        }

        override public void Save2DataSource(string arDate)
        {
            //基本信息
            DBConnection.ExecSQL("insert battery (rTime,batteryID,v,a,soc, soh,insulationR, positiveR, negativeR,"
                + " cellMaxV,   cellIDMaxV, cellMinV,  cellIDMinV,   cellMaxTemp,  cellIDMaxtemp,  averageV,averageTemp  "
                + " )value('" + arDate + "','" //rTime.ToString("yyyy-M-d H:m:s") 
                + batteryID.ToString() + "','" + v.ToString() + "','" + a.ToString() + "','" + soc.ToString() + "','"
                + soh.ToString() + "','" + insulationR.ToString() + "','" + positiveR.ToString() + "','"
                + negativeR.ToString() + "','" + cellMaxV.ToString() + "','" + cellIDMaxV.ToString() + "','"
                + cellMinV.ToString() + "','" + cellIDMinV.ToString() + "','" + cellMaxTemp.ToString() + "','"
                + cellIDMaxtemp.ToString() + "','" + averageV.ToString() + "','" + averageTemp.ToString() + "')");
            //cell
            string strCap = "", strVData = "", strTData = "";
            for (int i = 0; i < 240; i++)
            {
                strCap += "v" + (i + 1).ToString() + ",";
                strVData += CellVs[i].ToString() + "','";
                strTData += CellTemps[i].ToString() + "','";
            }
            strCap = strCap.Substring(0, strCap.Length - 1);
            strVData = strVData.Substring(0, strVData.Length - 2);
            strTData = strTData.Substring(0, strTData.Length - 2);
            //基本信息
            DBConnection.ExecSQL("insert cellsv (rTime," + strCap + ") value('" + arDate + "','" + strVData + ")");
            DBConnection.ExecSQL("insert cellstemp (rTime," + strCap + ") value('" + arDate + "','" + strTData + ")");
        }
    }


    //空调系统
    public class TempControlClass : BaseEquipmentClass
    {
        //所有字段上传云
        public double environmentTemp { get; set; }//送风/户外温度
        public double indoorTemp { get; set; }     //室内温度
        public double indoorHumidity { get; set; } //室内湿度
        public double condenserTemp { get; set; }  //冷凝/供液温度; 
        public double evaporationTemp { get; set; }//蒸发/出风温度
        public double fanControl { get; set; } //冷凝风机输出/加湿器输出 (协议取消)
        public UInt32 error { get; set; }
        public UInt32 errorOld = 0;
        public int state { get; set; }  //开机 1 关机2

        public bool PowerOn = false;

        private static ILog log = LogManager.GetLogger("TempControlClass");

        DateTime oldTemp;
        DateTime newTemp;
        int count = 0;
        //public double SetTemp;
        //public double SetHumidity;
        //public double SetTempReturn;
        //public double SetHumiReturn;
        public TempControlClass()
        {
            strCommandFile = "aircontrol.txt"; 
        }

        public bool ExecCommand(bool aWithAllSet)
        {
            try
            {
                lock (this.m485.sp)
                {
                    //11.23添加740,741 外风机工作模式，风机最高温度， 风机最低温度
 /*                   SetSysData(35,(short)frmSet.FenMode,false);
                    SetSysData(36, (short)frmSet.FenMaxTemp, false);
                    SetSysData(37, (short)frmSet.FenMinTemp, false);*/

                    SetSysData(11, (short)frmSet.SetCoolTemp,false);
                    SetSysData(12, (short)frmSet.CoolTempReturn, false);
                    SetSysData(13, (short)frmSet.SetHotTemp, false);
                    SetSysData(14, (short)frmSet.HotTempReturn, false);
                    if (aWithAllSet)
                    {
                        SetSysData(16, (short)frmSet.SetHumidity, false);
                        SetSysData(17, (short)frmSet.HumiReturn, false);
                        SetSysData(18, (short)frmSet.TCMaxTemp, false);
                        SetSysData(19, (short)frmSet.TCMinTemp, false);
                        SetSysData(20, (short)frmSet.TCMaxHumi, false);
                        SetSysData(21, (short)frmSet.TCMinTemp, false);
                        SetSysData(22, 0, false);//设置强制自动模式 803 强制模式
                                                 //设置强制自动模式
                        SetSysData(23, Convert.ToInt16(frmSet.TCMode), false);
                        if (frmSet.TCRunWithSys)
                            SetSysData(24, 0, false);//来电运行 自动
                        else
                            SetSysData(24, 1, false);//来电运行 禁止
                    }
                }

                //if (frmSet.TCAuto) 
                //    SetSysData(27, 0xff00);//178 手动 / 自动 
                //else 
                //    SetSysData(27, 0);//178 手动 / 自动  
                return true;
            }
            catch
            { return false; }

        }

        /// <summary>
        /// 开关空调
        /// </summary>
        /// <param name="aPowerOn"></param>
        public void TCPowerOn(bool aACOn)
        { 
            PowerOn = aACOn;
            try
            {
                if (aACOn)
                {
                    SetSysData(28, 0xff00,true);//01/05 //176 开机  
                    // SetSysData(30, 1);     // 遥控 开机  
                }
                else
                {
                    SetSysData(29, 0x0000,true);//01/05 //176 关机
                    //SetSysData(31, 2);     //遥控 开机  
                }
            }
            finally
            {
            }
        }

        /// <summary>
        /// 复位故障码
        /// </summary>
        public void TCCleanError()
        { 
            SetSysData(25, 0xff00,true);//01/05 
        }


        override public void GetDataFromEqipment()
        {//tc

/*            DateTime oldTemp = new DateTime(1970, 1, 1, 8, 0, 0);
            DateTime newTemp;
            int count = 0;*/

            string strData = "";
            string strTemp = "";
            bool bPrepared = false;
            if (GetSysData(32, ref strData))
            { 
                bPrepared = true;
                if (Get3strData(1, ref strData, ref strTemp))
                    indoorHumidity = Math.Round(float.Parse(strTemp), 1);//室内湿度 
                if (Get3strData(2, ref strData, ref strTemp))
                    indoorTemp = Math.Round(float.Parse(strTemp), 1);    //室内温度
                if (Get3strData(3, ref strData, ref strTemp))
                    condenserTemp = Math.Round(float.Parse(strTemp), 1);  //冷凝/供液温度
                if (Get3strData(4, ref strData, ref strTemp))
                    evaporationTemp = Math.Round(float.Parse(strTemp), 1);//蒸发/出风温度
/*                if (Get3strData(5, ref strData, ref strTemp))
                    condenserTemp = Math.Round(float.Parse(strTemp), 1);  //送风/户温度
                if (Get3strData(6, ref strData, ref strTemp))
                    fanControl = Convert.ToInt32(strTemp);//风机状态/加湿器*/
            }
            //11.16
            if (Get3strData(5, ref strData, ref strTemp))
                environmentTemp = Math.Round(float.Parse(strTemp), 1);//送风/户外温度 
            /*

            if (GetSysData(1, ref strTemp))
                indoorHumidity = Convert.ToSingle(strTemp);//湿度 
            if (GetSysData(2, ref strTemp))
                indoorTemp = Convert.ToSingle(strTemp)  ;    //温度
            if (GetSysData(3, ref strTemp))
                environmentTemp = Convert.ToSingle(strTemp) ;//送风/户外温度    
            if (GetSysData(4, ref strTemp))
                evaporationTemp = Convert.ToSingle(strTemp)  ;//蒸发温度
            if (GetSysData(5, ref strTemp))
                condenserTemp = Convert.ToSingle(strTemp) ;  //冷凝器  冷凝/供液温度           

            if (GetSysData(6, ref strTemp))//户外冷凝器温度
                fanControl = Convert.ToInt32(strTemp);//风机状态
            */
            //6-01
            //if (GetSysData(0, ref strTemp))
            //    Actived = (Convert.ToUInt32(strTemp) > 0);
            //读取状态
            if (GetSysData(33, ref strTemp))
            {
                state = Convert.ToInt32(strTemp);
                bPrepared = true;
            }
            //读取故障
            if (GetSysData(7, ref strTemp))
            {
                error = Convert.ToUInt32(strTemp);
                bPrepared = true;
            }
               
            time = DateTime.Now;

            //考虑到问询是稳定性，一个返回信息代表通讯成功
            Prepared = bPrepared;
            if (!Prepared)
            {
                if(count < 10)
                {
                    count++;
                }

                //TimeSpan ts = newTemp - oldTemp;
                //如果故障时间超过5分钟，则认为空调通讯故障
                if ( count > 8 )
                {
                    lock (Parent.EMSError)
                    {
                        Parent.EMSError[0] &= 0xFFBF;
                        Parent.EMSError[0] |= 0x40;
                    }
                }

            }
            else
            {
                Parent.EMSError[0] &= 0xFFBF;
                count = 0;          
            }


            //检查故障，并对比过去的故障
            if (error != errorOld)
            {
                UInt32 iData = 0;
                UInt32 itempData = 0;
                UInt32 sKey = 0;
                //检查故障，并对比过去的故障 //找到不一样的故障33554432
                iData = (UInt32)(error ^ errorOld);
                for (int j = 0; j < 32; j++)
                {
                    sKey = (UInt32)(1 << j);
                    itempData = iData & sKey;//找到更新的数据位
                    if ((itempData > 0) && (ErrorClass.TCErrorPower[j] > 0))//恢复故障、数据库中找到同类故障记录恢复时间
                    {
                        RecodError("空调", iot_code, j, ErrorClass.TCErrorPower[j], ErrorClass.TCErrors[j], (error & sKey) > 0);
                    }
                }
                errorOld = error;
            }
        }


        /// <summary>
        /// 保存空调的实时数据
        /// </summary>
        /// <param name="arDate"></param>
        override public void Save2DataSource(string arDate)
        {
            //基本信息
            DBConnection.ExecSQL("insert tempcontrol(rTime,state,indoorTemp,indoorHumidity,environmentTemp,condenserTemp,"
                + "evaporationTemp,fanControl,error)value('"
                + time.ToString("yyyy-M-d H:m:s") + "','"
                + state.ToString() + "','"
                + indoorTemp.ToString() + "','"
                + indoorHumidity.ToString() + "','"
                + environmentTemp.ToString() + "','"
                + condenserTemp.ToString() + "','"
                + evaporationTemp.ToString() + "','"
                + fanControl.ToString() + "','"
                + error.ToString() + "')");
        }
    }


    /// <summary>
    /// 通信类，含初始化串口和设备的查询
    /// </summary>
    public class CommClass
    {
        //Thread ClientRecThread;
        List<BaseEquipmentClass> EquipList = new List<BaseEquipmentClass>();
        public CommClass()
        {
            //ClientRecThread = new Thread();
        }

        public void IniCom(string aComName, int aRate)
        {

        }

        public void DoWork()
        {
            if (EquipList == null)
                return;
        }
    }

    public class EMSEquipment : BaseEquipmentClass
    {
        public int ID = 2;
        //public modbus485 m485 = null;
        public double ShedulePCSKVA = -1;
        //public double ActivePCSKVA = 0;//从机PCS实际运行功率
        public int PCSType { get; set; } 
        public double  WorkType { get; set; }//0充电1放电2待机
        //public bool Prepared = true;
        public double waValueActive { get; set; }
        //public AllEquipmentClass Parent = null;

        //8.7
        //public bool onLine = false;//从机是否在线
        public int runState { get; set; } //从机运行状态：0正常，1故障，2停机
        public int BMSErrorSate { get; set; }// 0:没有警告 1：有警告

        //8.8
        private static ILog log = LogManager.GetLogger("BaseEquipmentClass");
        /// <summary>
        // public string strCommandFile = "";
        /// </summary>

        public EMSEquipment()
        {
            strCommandFile = "ems.txt";
        }

        //12.4 通讯质量测试
        public int CallEMS(int CID)
        {
            string strTemp = "";
            int count = 100;//发送100条数据
            int rec = 0 ; //成功通信次数

            while (count > 0)
            {
                if (GetSysData2(0, CID, ref strTemp))
                {
                    rec++;
                }
            }

            return rec;
        }

        //8.7
        override public void GetDataFromEqipment2(int CID)
        {
            bool bPrepared = false;
            string strTemp = "";
            string strData = "";


            if (GetSysData2(1, CID, ref strTemp))
            {
                waValueActive = Math.Round(double.Parse(strTemp), 3);
                bPrepared = true;
            }

            if (GetSysData2(2, CID, ref strTemp))
            {
                WorkType = Math.Round(double.Parse(strTemp), 3);
                bPrepared = true;
            }

            /*            if (GetSysData2(5, CID, ref strTemp))
                        {
                            BMSErrorSate = Convert.ToUInt16(strTemp);
                            bPrepared = true;
                        }*/

            Prepared = bPrepared;
        }


        public void ExcPCSOn(bool aOn)//0off,1off
        { 
            //打开PCS
            if (aOn)
                m485.Send6MSG((byte)ID, 6, 0x6000, (ushort)1, true);
            else
                m485.Send6MSG((byte)ID, 6, 0x6000, 0, true);
        }



        public void ExcPCSCommand(string aWorkType, string aPCSType, double aPCSValueRate,bool bAllParam )
        {
            bool bPrepared = false;
            string[] wTpyes = { "充电","放电"};
            int itemp = Array.IndexOf(wTpyes, aWorkType);

            if (m485.Send6MSG((byte)ID, 6, 0x6003, (ushort)itemp, true))
            {
                bPrepared = true;
            }

            itemp = Array.IndexOf(PCSClass.PCSTypes, aPCSType);
            if (m485.Send6MSG((byte)ID, 6, 0x6004, (ushort)itemp, true))
            {
                bPrepared = true;
            }

            double dtemp = (Parent.PCSScheduleKVA * aPCSValueRate);
            if (m485.Send6MSG((byte)ID, 6, 0x6002, (ushort)dtemp, true))
            {
                bPrepared = true;
            }

            Prepared = bPrepared;
        }

        public void SetPCSScheduleKVA(double aPCSScheduleKVA)
        { 
            if (m485.Send6MSG((byte)ID, 6, 0x6001, (ushort)aPCSScheduleKVA, true))
            {
                Prepared = true; 
                ShedulePCSKVA = aPCSScheduleKVA;
            } 
        }
    }


    //所有部件类都是实例作为整体设备类的属性
    //整体设备类
    public class AllEquipmentClass
    {
        //SetThreadAffinityMask: Set hThread run on logical processer(LP:) dwThreadAffinityMask
        [DllImport("kernel32.dll")]
        static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

        //Get the handler of current thread
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentThread();

        //初始化完成标志
        public bool Prepared = false;
        //状态
        public short SysState = 1;
        //询问间隔
        public short AskInterval = 60;
        //计时器
        public TimeMeasurement Clock_Watch = new TimeMeasurement();
        //消防传感器
        public FireClass Fire;
        public WaterloggingClass WaterLog1;
        public WaterloggingClass WaterLog2;
        public TempHumClass TempHum;
        public SmokeClass Smoke;
        public CoClass co;
        //5.05_swp
        public LEDClass Led;
        public DehumidifierClass Dehumidifier;
        //用户侧电表（关口表）
        public List<Elemeter1Class> Elemeter1List = new List<Elemeter1Class>();

        //2.21
        public Elemeter2Class Elemeter1Z;   //主从储能柜整体功率

        //设备电表
        public Elemeter2Class Elemeter2;
        //内部辅组电表
        public Elemeter3Class Elemeter3;
        //计量电表（一般不用）
        public Elemeter2Class Elemeter4;
        //考虑到多PCS
        public List<PCSClass> PCSList = new List<PCSClass>();
        //BMS
        public  BMSClass  BMS;
        //空调可能有多个
        public  TempControlClass TempControl;
        //UPS
        public UPSClass UPS;
        //主机EMS 8.7
        public EMSEquipment EMS;
        //液冷控制部分11.17
        public LiquidCoolClass LiquidCool;
        //PCS的DPS2 11.27
        public DSP2Class DSP2;
        //485的列表
        public List<SerialPort> modbus485List = new List<SerialPort>();
        //从机的列表
        public List<EMSEquipment> EMSList = new List<EMSEquipment>();

        //云
        public CloudClass Report2Cloud = null;
        public ProfitClass Profit2Cloud = new ProfitClass();
        public FaultClass Fault2Cloud = new FaultClass();

        //汇流柜新增DTSD1352
        public Elemeter2Class Elemeter2H;

        public string DofD = "";

        //2.21
        public string DoPU = "";
        //8.5
        //public string PCSfD = "";

        //当前策略的功率情况
        public int eState;    //手工、策略、网控 
        //public string wType;  //充电、放电                 /应该状态
       // public string PCSType;//待机、恒流、恒压、恒功率--设置项目/策略状态 
        public int    MaxBMXValue=110;   //BMS限流的功率 kw

        //2.21
        public volatile string PrewTypeActive;//策略预备执行动作（准备充电/放电）
        public volatile string PrePCSTypeActive;//策略预备动作 （恒功率/自适应需量）
        public volatile bool GotoSchedule = true; //许可策略按照计划下发
        //当前执行的策略功率 
        public volatile string wTypeActive;  //充电、放电
        public volatile string PCSTypeActive;//待机、恒压、恒流恒、恒功率 , AC恒压（离网） ，自适应需量
        public double waValueActive; //对应的 电压(恒压模式) /电流 (恒流模式)/ 功率（恒功率模式）

        public int  ConversionRate = 1; //实际转换率

        public string rDate = "";
        public double[] SAuxiliaryKWH = { 0, 0, 0, 0, 0 };   //记录当天开始辅助用电量
        public double[] SE2PKWH = { 0, 0, 0, 0, 0 };         //记录当天开始充电电量（positive 正向）
        public double[] SE2OKWH = { 0, 0, 0, 0, 0 };         //记录当天开始放电电量（opposite反向，逆向）
        public DateTime time { get; set; }
        public string iot_code { get; set; } = "ems208800001";
        public int runState { get; set; } = 0;  //运行状态 0正常，1故障，2停机
        public bool[] ErrorState = { false, false, false };//1.2.3级别


        public ushort[] OldEMSError = {0,0,0,0,0 };
        public ushort[] EMSError = { 0, 0, 0, 0, 0 }; //问题？为什么有5个 和errorclass不同 

        //5.07 新增led显示故障
        public int Led_ShowError = 0; // 0 正常 ，1 警告 ，2 故障
        public int Prev_Led_ShowError = 0; // 0 正常 ，1 警告 ，2 故障

        public bool[] Led_Error = { false, false, false };//1.2.3级别
        public bool[] Prev_Led_Error = { false, false, false };//1.2.3级别

        //public int Led_ShowWarn = 0; // 0 正常 ，1 警告 ，2 故障
        //public int Prev_Led_ShowWarn = 0;
        /*0 待机 1 运行*/
        public int Led_Show_status = 0;      //0 待机 1 运行 
        public int Prev_Led_Show_status = 0; //0 待机 1 运行 
        public int Led_ShowPowerLevel = 0;
        public int Prev_Led_ShowPowerLevel = 0;
        // 6-4
        public bool[] LedErrorState = { false, false, false };//错误标志位 1.2.3级别



        //整体设备类的字段
        public UInt16 Error { get; set; }    //EMS故障  -----》EMSErrorssss
        public double[] AuxiliaryKWH { get; set; } = { 0, 0, 0, 0, 0 };     //当天总辅助电量 （辅助电表）
        public double[] E2PKWH { get; set; } = { 0, 0, 0, 0, 0 };  //当天总充电量（positive 正向）
        public double[] E2OKWH { get; set; } = { 0, 0, 0, 0, 0 };   //当天总放电量（opposite反向，逆向）


        //public double SPCSInKWH;    //记录PCS 开始的总充电量
        // public double SPCSOutKWH;   //记录PCS 开始的总放电量 
        //public double PCSInKWH { get; set; }          //当前实时数据总充电
        // public double PCSOutKWH { get; set; }         //当前实时数据总放电量
        //public double TPCSInKWH { get; set; } = 0;      //当天充电量
        //public double TPCSOutKWH { get; set; } = 0;     //当前天放电量

        public double Profit { get; set; }           //当天收益 
        public double GridKVA { get; set; }   //实时数据电网功率（关口表视为功率）

        //2.21
        public double E1_PUMdemand_Max { get; set; } = 0;   //实时数据当月正向有功最大需量
        public double E2_PUMdemand_Max { get; set; } = 0;   //实时数据当月正向有功最大需量
        public double Client_PUMdemand_Max { get; set; } = 0;   //实时数据当月正向有功最大需量
        public double E1_PUMdemand_now { get; set; } = 0;   //实时数据当前正向有功需量
        public double E1_PUMdemand_old { get; set; } = 0;   //实时数据上一次当前正向有功需量
        public double E2_PUMdemand_now { get; set; } = 0;   //实时数据当前正向有功需量

        public double Client_PUMdemand_now { get; set; } = 0;   //实时数据当前正向有功需量

        public DateTime start_Time = DateTime.Now;
        public bool recoverSchedule = true;

        //2.21
        public double GridKVA_window;
        public Queue<double> AllUkvaWindow = new Queue<double>();
        public double AllUkvaSum = 0;
        public int AllUkvaWindowSize = 4; // 1分钟的窗口大小，每秒一个值

        public double WorkKVA { get; set; } = 0;    //实时数据负载功率==电网+pcs功率（放电）、、、、电网+充电功率
        public  double PCSScheduleKVA { get; set; } = 0;     //实施的功率（手工设置或策略功率）
        public double AllPCSScheduleKVA = 0;   //主从结构的全部计划功率
        public double AllwaValue = 0;            //主从结构的全部实际功率
        public double PCSKVA { get; set; } = 0;     //实时数据pcs功率 
        public double AuxiliaryKVA { get; set; } = 0;  //辅电表功率
        public double BMSSOC { get; set; }   //实时数据SOC
        public double BMSSOH { get; set; }   //实时数据SOH
        public double BMSKVA { get; set; }   //实时电池的功率

        //7.25 BMS 均衡
        double CellV_Gap = 0.03 ;//定义最低单体电压和理想最高单体电压的差值30mv
        public List<double> balaCellV = new List<double>(); //单体电压数据
        public List<double> balaCellID = new List<double>(); //单体电压数据
        public double O_sigma { get; set; } = 0;            //上次的电压方差
        public  int BalaRun { get; set; } = 0;         //是否运行均衡标识位
        public double Cell_Diff { get; set; } = 0;                           //最大单体电压差


        public int[] ReSendClock = { 0, 0, 0, 0, 0 };
        // public int[] BMSErrorState = { 0, 0 };
        public bool NetControl = false; //记录当前状态是否为网络控制
        public bool NetConnect = false; //记录当前状态是否连接主机
        public DateTime NetCtlTime =DateTime.Now; //最后通讯时间

        //2.21
        public static string[] EquipNameList = { "用户侧电表", "设备电表", "辅组电表", "PCS逆变器", "BMS", "空调系统" 
                   , "消防","计量电表" ,"水浸传感器","一氧化碳传感器","温湿度传感器","烟雾传感器","UPS","液冷机" ,"DSP2" ,"汇流柜电表","储能电站总表","灯板","除湿机"};
        public  bool ChechPower;// = (frmMain.Selffrm.AllEquipment.Elemeter1 != null);


        //8.8
        private static ILog log = LogManager.GetLogger("AllEquipmentClass");

        //8.11
        public double UBmsPcsState = 1; //充电state
        public double OBmsPcsState = 1; //放电state

        //8.13多级防逆超限
        public double dRate = 0;
        public double dValue = 0;
        public bool FineToCharge = false;//自适应需量中，记录是否下发充电指令


        public volatile bool HostStart = false;
        public volatile bool SlaveStart = false;

        public volatile bool ReduceReadPCS = false;
        public volatile bool SlowReadBMS = false;
        public double totalcpu { get; set; }
        public double emscpu { get; set; }

        //上传版本号
        public string EMSVersion { get; set; } = "EMS240718Devlop1.0";
        public string Elemeter1_Version { get; set; } = "";
        public string Elemeter1Z_Version { get; set; } = "";
        public string Elemeter2_Version { get; set; } = "";
        public string Elemeter2H_Version { get; set; } = "";
        public string Elemeter3_Version { get; set; } = "";
        public string PCS_Version { get; set; } = "";
        public string BMS_Version { get; set; } = "";
        public string TempControl_Version { get; set; } = "";
        public string Waterlogging_Version { get; set; } = "";
        public string Co_Version { get; set; } = "";
        public string TempHum_Version { get; set; } = "";
        public string Smoke_Version { get; set; } = "";
        public string UPS_Version { get; set; } = "";
        public string LiquidCool_Version { get; set; } = "";
        public string DSP2_Version { get; set; } = "";
        public string LED_Version { get; set; } = "";
        public string Dehumidifier_Version { get; set; } = "";

        //
    


        public AllEquipmentClass()
        {
            Fire = new FireClass(); 
        }

        //析构函数
        ~AllEquipmentClass()
        {
            //ParkClass onePark;
            //ModbusCommand oneCommand;
            //while (ParkList.Count > 0)
            //{ 
            //    onePark=ParkList[0];
            //    ParkList.Remove(onePark);
            //    while(onePark.ComList.Count>0)
            //    {
            //        oneCommand= onePark.ComList[0];
            //        //onePark.ComList.
            //       //free  oneCommand

            //    }
            //  //free onePark;
            //}
        }

        public void AddValue(double value)
        {
            AllUkvaWindow.Enqueue(value);
            AllUkvaSum += value;

            if (AllUkvaWindow.Count > AllUkvaWindowSize)
            {
                AllUkvaSum -= AllUkvaWindow.Dequeue();
            }
        }

        public void clearAllUkvaWindow()
        {
            while (AllUkvaWindow.Count > 0)
            {
                AllUkvaWindow.Dequeue();
            }
        }

        public double GetAverage()
        {
            return AllUkvaSum / AllUkvaWindow.Count;
        }


        public static ulong SetCpuID(int lpIdx)
        {
            ulong cpuLogicalProcessorId = 0;
            if (lpIdx < 0 || lpIdx >= System.Environment.ProcessorCount)
            {
                lpIdx = 0;
            }
            cpuLogicalProcessorId |= 1UL << lpIdx;
            return cpuLogicalProcessorId;
        }

        //液冷设置
        public void LCIni()
        {
            if (frmMain.Selffrm.AllEquipment.LiquidCool == null)
                return;
            frmMain.Selffrm.AllEquipment.LiquidCool.ExecCommand();
        }



        //空调设置
        public void TCIni(bool aWithAllSet)
        {
            if (frmMain.Selffrm.AllEquipment.TempControl == null)
                return;
            frmMain.Selffrm.AllEquipment.TempControl.ExecCommand(aWithAllSet); 
        }
        //除湿器设置 5.05 swp
        public void DHIni()
        {
            if (frmMain.Selffrm.AllEquipment.Dehumidifier == null)
                return;
            frmMain.Selffrm.AllEquipment.Dehumidifier.ExecCommand();
        }


        //复位空调故障
        public void TCPowerOn(bool aACOn)
        {
            if (frmMain.Selffrm.AllEquipment.TempControl == null)
                return;
            frmMain.Selffrm.AllEquipment.TempControl.TCPowerOn(aACOn); 
        }

        //复位空调故障
        public void TCCleanError()
        {
            if (frmMain.Selffrm.AllEquipment.TempControl == null)
                return;
            frmMain.Selffrm.AllEquipment.TempControl.TCCleanError(); 
        }

        /// <summary>
        /// 清理PCS故障
        /// </summary>
        public void PCSCleanError()
        {
            //执行 策略
            for (int j = 0; j < PCSList.Count; j++)
            {
                PCSList[j].SetSysData(76, 0xff00,true);
            }
        }

        /// <summary>
        /// PCS执行策略的动作
        /// </summary>
        /// <param name="aWorkType"充放电类型></param>
        /// <param name="aPCSType"恒压、恒流、恒功率></param>
        /// <param name="aData"></param>
        /// <returns></returns>
        public bool ExcPCSCommand(string aWorkType, string aPCSType, int aData)
        {
            bool bResult = false;
            double fPower = frmMain.Selffrm.AllEquipment.GridKVA;

            if (HostStart)
            {
                if (ErrorState[2])
                {
                    aData = 0;
                }
                if (aPCSType == null)
                    aPCSType = "恒功率";//默认恒功率模式
                if (aWorkType == null)
                {
                    if (aData > 0)
                        aWorkType = "充电";
                    else
                        aWorkType = "放电";
                }
                //限额 
                aData = Math.Abs(aData);
                if (aData > 110)
                    aData = 110;

                if (frmMain.Selffrm.AllEquipment.BMS !=null)
                {
                    switch (aWorkType)
                    {
                        case "待机":
                            aData = 0;
                            break;
                        case "充电":
                            aData = (int)(Math.Abs(aData) * UBmsPcsState);
                            if ((BMSSOC > frmSet.MaxSOC) && (aData != 0))
                            {
                                DBConnection.RecordLOG("系统", "充电失败", "SOC过高");
                                aData = 0;
                            }
                            else if ((BMS.MaxChargeA == 0) && (aData != 0))
                            {
                                DBConnection.RecordLOG("系统", "充电失败", "BMS禁止充电");
                                aData = 0;
                            }
                            break;
                        case "放电":
                            aData = -1 * (int)(Math.Abs(aData) * OBmsPcsState);
                            if ((BMSSOC < frmSet.MinSOC) && (aData != 0))
                            {
                                DBConnection.RecordLOG("系统", "放电失败", "SOC过低");
                                aData = 0;
                            }
                            else if ((BMS.MaxDischargeA == 0) && (aData != 0))
                            {
                                DBConnection.RecordLOG("系统", "放电失败", "BMS禁止放电");
                                aData = 0;
                            }
                            break;
                        case "时间段内均充均放":
                            break;
                    }
                }

                if (PCSList.Count > 0)
                {
                    //执行 策略
                    for (int j = 0; j < PCSList.Count; j++)
                    {
                        //设置PCS停止查询恒流、恒压、恒功率 
                        if (aPCSType != "待机")
                        {

                            if ((aData != 0)&&(frmMain.Selffrm.AllEquipment.TempControl!=null))
                            {
                                if (frmMain.Selffrm.AllEquipment.TempControl.state != 1)
                                {
                                    frmMain.Selffrm.AllEquipment.TempControl.TCPowerOn(true);//PCS工作前启动空调
                                }
                            }

                            if ((aData != 0)&&(frmMain.Selffrm.AllEquipment.LiquidCool != null))
                            {
                                if (frmMain.Selffrm.AllEquipment.LiquidCool.state != 1)
                                {
                                    frmMain.Selffrm.AllEquipment.LiquidCool.LCPowerOn(true);//PCS工作前启动液冷机
                                }
                            }

                            if (aData != 0)
                            {
                                if (PCSList[j].ExecCommand(aPCSType, aData, BMSSOC)) //检查是否满足开启PCS条件，设置PCS的功率（不满足条件，设为0）
                                {
                                    if (frmMain.Selffrm.AllEquipment.PCSList[j].PcsRun == 255)
                                    {
                                        PCSList[j].ExcSetPCSPower(true);
                                    }
                                }
                            }
                            else
                            {
                                if (frmMain.Selffrm.AllEquipment.PCSList[j].PcsRun != 255)
                                {

                                    PCSList[j].ExcSetPCSPower(false);
                                }
                            }

                        }
                    }
                }
            }
            else
            {
                frmMain.Selffrm.AllEquipment.ExcPCSPowerOff();
            }
            return bResult;
        }

        //关闭PCS
        public bool ExcPCSPowerOff()
        {
            bool bResult = true;
            if (PCSList.Count > 0)
            {
                //执行 策略
                for (int j = 0; j < PCSList.Count; j++)
                {
                    //设置PCS停止运行
                    if (frmMain.Selffrm.AllEquipment.PCSList[j].PcsRun != 255)
                    {
                        frmMain.Selffrm.AllEquipment.PCSList[j].ExcSetPCSPower(false);
                    }
                }
            }
            //上位机显示参数调为0
            // frmMain.Selffrm.AllEquipment.wTypeActive = "";
            //frmMain.Selffrm.AllEquipment.PCSScheduleKVA = 0;
            frmMain.Selffrm.AllEquipment.waValueActive = 0;
            frmMain.Selffrm.AllEquipment.PCSTypeActive = "恒功率";
            return bResult;
        }


        //读取一个数据
        private string ReadoneData(StreamReader asrFile, string aDefData)
        {
            string strData = asrFile.ReadLine();
            if (strData != null)
                return strData;
            else
                return aDefData;
        }

        ////从文件中读取故障信息
        public void LoadErrorState()
        {
            MySqlConnection ctTemp = null;
            MySqlDataReader sdr = DBConnection.GetData("select id, TCError,PCSError1,PCSError2,PCSError3,PCSError4,PCSError5,"
                + "PCSError6,PCSError7,PCSError8,BMSError1,BMSError2,BMSError3,BMSError4,BMSError5,EMSError1,EMSError2,EMSError3,EMSError4 "
                + "  from errorstate order by id desc limit 1", ref ctTemp);
            //string[] eTpyeList = { "用户侧电表", "设备电表", "辅组电表","PCS逆变器", "BMS", "空调系统","消防","其他", "计量电表","" };
            try
            {
                if (sdr != null)
                {
                    if (sdr.HasRows)
                    {
                        while (sdr.Read())
                        {
                            if (TempControl!=null)
                                TempControl.errorOld = sdr.GetUInt32(1);
                            if (PCSList.Count > 0)
                            {
                                PCSList[0].OldError[0] = (ushort)sdr.GetUInt32(2);
                                PCSList[0].OldError[1] = (ushort)sdr.GetUInt32(3);
                                PCSList[0].OldError[2] = (ushort)sdr.GetUInt32(4);
                                PCSList[0].OldError[3] = (ushort)sdr.GetUInt32(5);
                                PCSList[0].OldError[4] = (ushort)sdr.GetUInt32(6);
                                PCSList[0].OldError[5] = (ushort)sdr.GetUInt32(7);
                                PCSList[0].OldError[6] = (ushort)sdr.GetUInt32(8);
                                PCSList[0].OldError[7] = (ushort)sdr.GetUInt32(9);
                            }
                            if (BMS!=null)
                            {
                                BMS.OldError[0] = (ushort)sdr.GetUInt32(10);
                                BMS.OldError[1] = (ushort)sdr.GetUInt32(11);
                                BMS.OldError[2] = (ushort)sdr.GetUInt32(12);
                                BMS.OldError[3] = (ushort)sdr.GetUInt32(13);
                                BMS.OldError[4] = (ushort)sdr.GetUInt32(14);
                            }
                            //OldError = (ushort)sdr.GetUInt32(15);
                            OldEMSError[0]= (ushort)sdr.GetUInt32(15);
                            OldEMSError[1] = (ushort)sdr.GetUInt32(16);
                            OldEMSError[2] = (ushort)sdr.GetUInt32(17);
                            OldEMSError[3] = (ushort)sdr.GetUInt32(18);
                        }
                    }
                }             
            }
            catch
            { }
            finally
            {
                if (sdr != null)
                {
                    if (!sdr.IsClosed)
                        sdr.Close();
                    sdr.Dispose();
                }

                if (ctTemp != null)
                {
                    ctTemp.Close();
                    DBConnection._connectionPool.ReturnConnection(ctTemp);
                }
            }

        }

        //从文件中读取设置信息
        //实例化整体设备类下的各个部件对象
        public void LoadSetFromFile()
        {
            Fire.Parent = this;
            int eType = 0;
            BaseEquipmentClass oneEquipment = null; //oneEquipment当作指针用
            //creat reader 
            MySqlConnection ctTemp = null;
            MySqlDataReader sdr = DBConnection.GetData("select eID,eType,eModel,comType,comName,comRate,comBits,"
                + "TCPType,serverIP,SerPort,LocPort,eName,pc from equipment", ref ctTemp);
            //string[] eTpyeList = { "用户侧电表", "设备电表", "辅组电表","PCS逆变器", "BMS", "空调系统","消防","其他", "计量电表","" };
            try
            {
                //0用户侧电表，1设备电表，2辅组电表，3PCS逆变器，4BMS，5空调系统，6消防，7计量电表，
                //8水浸传感器，9一氧化碳传感器，10温湿度传感器，11烟雾传感器,12UPS,13其他
                if (sdr != null)
                {
                    if (sdr.HasRows)
                    {
                        while (sdr.Read())//调用 Read 方法读取 SqlDataReader
                        {
                            string adata = sdr.GetString(1).Trim();
                            eType = Array.IndexOf(AllEquipmentClass.EquipNameList, adata);// sdr.GetDateTime(0).ToString(aTimeFormat),
                            switch (eType)
                            {
                                case 0:
                                    oneEquipment = new Elemeter1Class();
                                    Elemeter1List.Add((Elemeter1Class)oneEquipment);
                                    Elemeter1_Version = oneEquipment.LoadVersionFromFile();
                                    break;
                                case 1:
                                    Elemeter2 = new Elemeter2Class();
                                    oneEquipment = Elemeter2;  //父类指针指向子类对象
                                    Elemeter2_Version = oneEquipment.LoadVersionFromFile();
                                    break;
                                case 2:
                                    Elemeter3 = new Elemeter3Class();
                                    oneEquipment = Elemeter3;
                                    Elemeter3_Version = oneEquipment.LoadVersionFromFile();
                                    break;
                                case 3://pcs
                                    oneEquipment = new PCSClass(frmSet.iPCSfactory);
                                    PCSList.Add((PCSClass)oneEquipment);
                                    PCS_Version = oneEquipment.LoadVersionFromFile();
                                    break;
                                case 4://BMSClass
                                    oneEquipment = new BMSClass();
                                    BMS = (BMSClass)oneEquipment;
                                    BMS_Version = oneEquipment.LoadVersionFromFile();
                                    break;
                                case 5://TempControlList  8u
                                    oneEquipment = new TempControlClass();
                                    TempControl = (TempControlClass)oneEquipment;
                                    TempControl_Version = oneEquipment.LoadVersionFromFile();
                                    break;
                                case 8://水浸传感器
                                    oneEquipment = new WaterloggingClass();
                                    if (WaterLog1 == null)
                                    {
                                        WaterLog1 = (WaterloggingClass)oneEquipment;
                                        Waterlogging_Version = oneEquipment.LoadVersionFromFile();
                                    }
                                    else
                                    {
                                        WaterLog2 = (WaterloggingClass)oneEquipment;
                                        Waterlogging_Version = oneEquipment.LoadVersionFromFile();
                                    }
                                    break;
                                case 9://一氧化碳传感器
                                    co = new CoClass();
                                    oneEquipment = co;
                                    Co_Version = oneEquipment.LoadVersionFromFile();
                                    break;
                                case 10://温湿度传感器
                                    TempHum = new TempHumClass();
                                    oneEquipment = TempHum;
                                    TempHum_Version = oneEquipment.LoadVersionFromFile();
                                    break;
                                case 11://11烟雾传感器
                                    Smoke = new SmokeClass();
                                    oneEquipment = Smoke;
                                    Smoke_Version = oneEquipment.LoadVersionFromFile();
                                    break;
                                case 12:
                                    UPS = new UPSClass();
                                    oneEquipment = UPS;
                                    UPS_Version = oneEquipment.LoadVersionFromFile();
                                    break;
                                case 13://液冷机
                                    LiquidCool = new LiquidCoolClass();
                                    oneEquipment = LiquidCool;
                                    LiquidCool_Version = oneEquipment.LoadVersionFromFile();
                                    break;
                                case 14:
                                    DSP2 = new DSP2Class(1);
                                    oneEquipment = DSP2;
                                    DSP2_Version = oneEquipment.LoadVersionFromFile();
                                    break;
                                case 15:
                                    Elemeter2H = new Elemeter2Class();
                                    oneEquipment = Elemeter2H;
                                    Elemeter2H_Version = oneEquipment.LoadVersionFromFile();
                                    break;
                                case 16://2.21
                                    Elemeter1Z = new Elemeter2Class();
                                    oneEquipment = Elemeter1Z;
                                    Elemeter1Z_Version = oneEquipment.LoadVersionFromFile();
                                    break;
                                case 17://5.05
                                    Led = new LEDClass();
                                    oneEquipment = Led;
                                    LED_Version = oneEquipment.LoadVersionFromFile();
                                    break;
                                case 18://5.05
                                    Dehumidifier = new DehumidifierClass();
                                    oneEquipment = Dehumidifier;
                                    Dehumidifier_Version = oneEquipment.LoadVersionFromFile();
                                    break;

                            }

                            oneEquipment.Parent = this;
                            oneEquipment.eID = sdr.GetInt32(0);
                            oneEquipment.eType = eType;
                            oneEquipment.eModel = sdr.GetString(2);
                            oneEquipment.comType = sdr.GetInt32(3);
                            oneEquipment.comName = sdr.GetString(4);
                            oneEquipment.comRate = sdr.GetInt32(5);
                            oneEquipment.comBits = sdr.GetInt32(6);
                            oneEquipment.TCPType = sdr.GetString(7);
                            oneEquipment.serverIP = sdr.GetString(8);
                            oneEquipment.SerPort = sdr.GetInt32(9);
                            oneEquipment.LocPort = sdr.GetInt32(10);
                            oneEquipment.iot_code = sdr.GetString(11);
                            oneEquipment.pc = sdr.GetInt32(12);
                            //oneEquipment.version = oneEquipment.LoadVersionFromFile();
                            oneEquipment.LoadCommandFromFile(); //下载comlist
                                                                //frmMain.Selffrm.AllEquipment.LiquidCool.ProtocolVersion = oneEquipment.LoadVersionFromFile();
                                                                //oneEquipment.Parent = this;
                            switch (oneEquipment.comType)
                            {
                                case 0:
                                    oneEquipment.m485 = new modbus485();
                                    oneEquipment.m485.ParentEquipment = this;
                                    oneEquipment.m485.Open(oneEquipment.comName, oneEquipment.comRate, oneEquipment.comBits,
                                                           System.IO.Ports.Parity.None, System.IO.Ports.StopBits.One);//打开串口
                                    break;
                                case 1:
                                    break;
                                case 2:
                                    break;
                            }
                        }
                    }
                }
                ChechPower = (frmMain.Selffrm.AllEquipment.Elemeter1List != null);
                if (ChechPower)
                {
                    ChechPower = (frmMain.Selffrm.AllEquipment.Elemeter1List.Count > 0);
                }
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            finally
            {
                if (sdr != null)
                {
                    if (!sdr.IsClosed)
                        sdr.Close();
                    sdr.Dispose();
                }

                if (ctTemp != null)
                {
                    ctTemp.Close();
                    DBConnection._connectionPool.ReturnConnection(ctTemp);
                }
            }
            Report2Cloud = new CloudClass();
            Report2Cloud.Parent = this;
        }





        public void init_LED() //LED初始化
        {
            if (frmMain.Selffrm.AllEquipment.Led != null)
            {
                if (frmMain.Selffrm.AllEquipment.ErrorState[2] == true) frmMain.Selffrm.AllEquipment.Led_ShowError = 2; //三级告警
                else frmMain.Selffrm.AllEquipment.Led_ShowError = 0;
                if (Math.Abs(frmMain.Selffrm.AllEquipment.PCSList[0].allUkva) > 0.5) frmMain.Selffrm.AllEquipment.Led_Show_status = 1; //0 待机 1 运行 
                else frmMain.Selffrm.AllEquipment.Led_Show_status = 0;

                frmMain.Selffrm.AllEquipment.Led_ShowPowerLevel = (((int)frmMain.Selffrm.AllEquipment.BMSSOC + 19) / 20); //电量

                //运行状态变化
                if (frmMain.Selffrm.AllEquipment.Led_Show_status == 0)   //运行待机状态
                {
                    switch (frmMain.Selffrm.AllEquipment.Led_ShowError)
                    {
                        case 0:
                            frmMain.Selffrm.AllEquipment.Led.Set_Led_Standby_N();
                            break;
                        case 1:
                            frmMain.Selffrm.AllEquipment.Led.Set_Led_Standby_W();
                            break;
                        case 2:
                            frmMain.Selffrm.AllEquipment.Led.Set_Led_Standby_E();
                            break;
                    }
                }
                if (frmMain.Selffrm.AllEquipment.Led_Show_status == 1)   //运行运行状态 
                {
                    switch (frmMain.Selffrm.AllEquipment.Led_ShowError)
                    {
                        case 0:
                            frmMain.Selffrm.AllEquipment.Led.Set_Led_Charge_N();
                            break;
                        case 1:
                            frmMain.Selffrm.AllEquipment.Led.Set_Led_Charge_W();
                            break;
                        case 2:
                            frmMain.Selffrm.AllEquipment.Led.Set_Led_Charge_E();
                            break;
                    }
                }
                switch (frmMain.Selffrm.AllEquipment.Led_Show_status)   //显示电量 
                {
                    case 0:
                        frmMain.Selffrm.AllEquipment.Led.SetButteryPercent(frmMain.Selffrm.AllEquipment.Led_ShowPowerLevel);
                        break;
                    case 1:
                        frmMain.Selffrm.AllEquipment.Led.SetChargeButteryPercent(frmMain.Selffrm.AllEquipment.Led_ShowPowerLevel);
                        break;
                }

            }
        }
        //保存到文件
        public void Save2DataSoure(DateTime atempTime)
        {
            try
            {
                string tempDate = atempTime.ToString("yyyy-MM-dd HH:mm:ss");
                int i = 0;
                //关口电表 
                if (Elemeter1List != null)
                {
                    foreach (Elemeter1Class tempEM1 in Elemeter1List)
                    {
                        tempEM1.Save2DataSource(tempDate);
                    }
                }
                //电表2---设备电表
                if (Elemeter2 != null)
                    Elemeter2.Save2DataSource(tempDate);
                //电表3---辅助电表
                if (Elemeter3 != null)
                    Elemeter3.Save2DataSource(tempDate);
                //PCS                
                for (i = 0; i < PCSList.Count; i++)
                    PCSList[i].Save2DataSource(tempDate);
                //BMS                
               if(BMS!=null)
                    BMS.Save2DataSource(tempDate);
                //空调
               if(TempControl!=null)
                    TempControl.Save2DataSource(tempDate);
                //液冷
                if (LiquidCool!=null)
                    LiquidCool.Save2DataSource(tempDate);
                //传感器
                if (Fire != null)
                    Fire.Save2DataSource(tempDate);
                //UPS
                if (UPS != null)
                    UPS.Save2DataSource(tempDate);
                //其他 
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            finally
            {

            }
        }

        /// <summary>
        /// 自动读取进程
        /// </summary>
        public void AutoReadData()
        {
            try
            {

                /***************Highest*************/

                //采集
                AutoReadDataCom1(); //超限防逆控制
                AutoReadDataCom3();//BMS关键数据采集
                AutoReadPointPower();
                if (ChechPower)
                    AutoReadPointGrid();

                //控制与接收
                if (frmSet.IsMaster)
                {
                    if (frmSet.ConnectStatus == "485")
                    {
                        if (frmSet.SysCount > 1)
                        {
                            frmMain.Selffrm.AllEquipment.AutoControlEMS();
                        }
                    }
                    else if (frmSet.ConnectStatus == "tcp")
                    {
                        if (frmSet.SysCount > 1)
                        {
                            frmMain.Selffrm.AllEquipment.AutoControlEMSTCP();
                        }
                    }

                }
                else
                {
                    if (frmSet.ConnectStatus == "485")
                    {
                        frmMain.Selffrm.AllEquipment.Auto_Read_Serial();
                    }
                }


                /***************Normal*************/
                AutoReadDataCom2();//表2、3、4，空调,液冷机
                AutoReadDataCom4(); //PCS 
                AutoReadE1();//表1

            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }

        /// <summary>
        /// //////////////////////////////////////////////////////////////////////////////////////
        /// //
        /// 数据读取线程，原则上每个串口一个线程
        /// //
        /// /////////////////////////////////////////////////////////////////////////////////////
        /// </summary>
        /// 

        public void Read_Serial()
        {
            List<byte> receivedData = new List<byte>();
            bool GetMsg = false;
            while (true)
            {
                Thread.Sleep(1000);
                if (frmMain.Selffrm.ems.m485.sp != null)
                {
                    while (frmMain.Selffrm.ems.m485.sp.BytesToRead > 0)
                    {
                        int info = frmMain.Selffrm.ems.m485.sp.ReadByte();
                        receivedData.Add((byte)info);
                        GetMsg = true;
                    }
                    if (GetMsg)
                    {
                        frmMain.Selffrm.OnReceiveCMD2(1, receivedData.ToArray());
                        GetMsg = false;
                        receivedData.Clear();
                    }
                }
            }
        }

        public void Auto_Read_Serial()
        {
            try
            {
                //实例化等待连接的线程
                Thread ClientRecThread = new Thread(Read_Serial);
                ClientRecThread.IsBackground = true;
                ClientRecThread.Priority = ThreadPriority.Highest;
                ClientRecThread.Start();
                ClientRecThread.Name = "";
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }


        public void Systime_Tick()
        {
            while (true)
            {
                if (frmSet.EMSstatus == 1)
                {
                    DateTime tempTime = DateTime.Now;
                    //采集数据保存在数据库中
                    frmMain.Selffrm.AllEquipment.Save2DataSoure(tempTime);
                    //采集数据上传云端
                    frmMain.Selffrm.AllEquipment.Report2Cloud.Save2CloudFile(tempTime);
                }

                //更新图表曲线
                //TacticsList.AddOneStep(ctMain, tempTime, -1 * AllEquipment.Elemeter2.AllUkva, AllEquipment.Elemeter2.Gridkva, AllEquipment.Elemeter2.Subkw);
                //ctMain.Series[1].ChartType = SeriesChartType.Line;

                if (!frmMain.Selffrm.AllEquipment.ReadDoPUini())
                {
                    //更新的月份

                    frmMain.Selffrm.AllEquipment.Client_PUMdemand_Max = 0;
                    frmMain.Selffrm.AllEquipment.WriteDoPUini();
                   
                }

                //如果日期更新：
                //1.清理数据库的旧数据
                //2.保存当天收益到数据库
                //3.上传当天收益到云
                //4.下载策略
                if (frmMain.Selffrm.AllEquipment.rDate != DateTime.Now.ToString("yyyy-MM-dd"))
                { 
                     //删除180天前的数据
                    frmSet.DeleOldData(DateTime.Now.AddDays(-180).ToString("yyyy-MM-dd"));
                    //保存当天收益到数据库
                    frmMain.Selffrm.AllEquipment.SaveDataInoneDay(frmMain.Selffrm.AllEquipment.rDate);
                    //当日收益发送到云
                    frmMain.Selffrm.AllEquipment.Report2Cloud.SaveProfit2Cloud(frmMain.Selffrm.AllEquipment.rDate);//qiao
                                                                                                                   //更新日期
                    frmMain.Selffrm.AllEquipment.rDate = DateTime.Now.ToString("yyyy-MM-dd");
                    //将当天的储能表和辅表的总尖峰平谷的累计电能数据保存到INI，包含日期和具体电能值
                    frmMain.Selffrm.AllEquipment.WriteDataInoneDayINI(frmMain.Selffrm.AllEquipment.rDate);
                    //每晚00：00更新策略
                    if (frmMain.TacticsList != null)
                    {
                        try
                        {
                            if (frmSet.IsMaster)
                            {
                                if (frmMain.TacticsList != null)
                                {
                                    try
                                    {
                                        frmMain.TacticsList.LoadFromMySQL();
                                    }
                                    catch
                                    {
                                        log.Error("定时器刷新数据库失败");
                                    }
                                }
                            }
                        }
                        catch
                        {
                            log.Error("00：00更新策略失败");
                        }
                    }
                    //更新均衡策略
                    try
                    {
                        frmMain.BalaTacticsList.LoadFromMySQL();
                    }
                    catch { log.Error("00：00更新均衡策略失败"); }
                    //在Chart显示计划
                    /*                    if (TacticsList != null)
                                        {
                                            try
                                            {
                                                if (frmSet.IsMaster)
                                                {
                                                    TacticsList.ShowTactic2Char(ctMain, true);
                                                }
                                            }
                                            catch 
                                            {
                                                log.Error("Chart显示计划出错");
                                            }
                                        }*/


                    //更新系统时间、表1--4、PCS、BMS
                    //校对时间 qiao
                    //if (NetTime.GetandSetTime())
                    //{
                    //    DateTime dtTemp=DateTime.Now;
                    //    byte[] aTime = { (byte)dtTemp.Second, (byte)dtTemp.Minute, (byte)dtTemp.Hour, (byte)dtTemp.Day, 
                    //                      (byte)dtTemp.Month, (byte)(dtTemp.Year-2000) };
                    //    if (AllEquipment.Elemeter1 != null)
                    //        AllEquipment.Elemeter1.SetTime(aTime);
                    //    if (AllEquipment.Elemeter2 != null)
                    //        AllEquipment.Elemeter2.SetTime(aTime);
                    //    byte[] aTime2 = { (byte)(dtTemp.Year-2000),(byte)dtTemp.Month, (byte)dtTemp.Day,
                    //                    (byte)dtTemp.Hour,(byte)dtTemp.Minute, (byte)dtTemp.Second  };
                    //    if (AllEquipment.Elemeter3 != null)
                    //        AllEquipment.Elemeter3.SetTime(aTime2);
                    //} 



                    //1.29 重置重启次数
                    frmSet.RestartCounts = 5;
                    frmSet.SaveSet2File();

                }
                //检查mqttp的连接情况，每分钟检查一次
/*                try
                {
                    frmMain.Selffrm.AllEquipment.Report2Cloud.CheckConnect();
                }
                catch { }*/
                //7.25
/*                if (frmMain.Selffrm.AllEquipment.Report2Cloud.mqttClient != null)
                {
                    if (!frmMain.Selffrm.AllEquipment.Report2Cloud.mqttClient.IsConnected)
                    {
                        //在非策略时段 + 运行模式 
                        if (frmSet.IsMaster)
                        {
                            if (frmMain.Selffrm.AllEquipment.HostStart == false && frmSet.EMSstatus == 1 &&  frmMain.Selffrm.AllEquipment.SlaveStart == false)
                            {
                                //log.Error("RestartCounts:" + frmSet.RestartCounts);
                                if (frmSet.RestartCounts > 0)
                                {
                                    //关闭PCS
                                    log.Error("网络原因造成EMS重启");
                                    frmMain.Selffrm.AllEquipment.PCSList[0].ExcSetPCSPower(false);
                                    //重启次数减1 ， 每日限定重启5次.
                                    frmSet.RestartCounts--;
                                    frmSet.SaveSet2File();
                                    Thread.Sleep(5000);
                                    //重启
                                    SysIO.Reboot();
                                }
                            }
                        }
                        else
                        {
                            if (frmMain.Selffrm.AllEquipment.HostStart == false && frmSet.EMSstatus == 1)
                            {
                                //log.Error("RestartCounts:" + frmSet.RestartCounts);
                                if (frmSet.RestartCounts > 0)
                                {
                                    //关闭PCS
                                    log.Error("网络原因造成EMS重启");
                                    frmMain.Selffrm.AllEquipment.PCSList[0].ExcSetPCSPower(false);
                                    //重启次数减1 ， 每日限定重启5次.
                                    frmSet.RestartCounts--;
                                    frmSet.SaveSet2File();
                                    Thread.Sleep(5000);
                                    //重启
                                    SysIO.Reboot();
                                }
                            }
                        }
                    }

                }*/

                //空调控制
                if(frmSet.EMSstatus == 1)  
                {
                    if (frmMain.Selffrm.AllEquipment.TempControl != null)//(!AllEquipment.TempControl.PowerOn)
                    {
                        if (frmMain.Selffrm.AllEquipment.BMS.cellMaxTemp > 30 && frmMain.Selffrm.AllEquipment.TempControl.state != 1)
                        {
                            frmMain.Selffrm.AllEquipment.TempControl.TCPowerOn(true);//PCS工作前启动空调
                        }                    //pcs必须处于低功率状态，且电池常温10---30度就停止空调
                        else if ((frmMain.Selffrm.AllEquipment.PCSList[0].PcsRun == 255) && (frmMain.Selffrm.AllEquipment.BMS.cellMaxTemp < 25) && (frmMain.Selffrm.AllEquipment.BMS.cellMinTemp > 10))
                        {
                            if (frmMain.Selffrm.AllEquipment.TempControl.state == 1)
                            {
                                frmMain.Selffrm.AllEquipment.TempControl.TCPowerOn(false);//PCS工作前启动空调
                            }
                        }
                    }

                    //液冷控制
                    if (frmMain.Selffrm.AllEquipment.LiquidCool != null)
                    {     
                        if (frmMain.Selffrm.AllEquipment.BMS.cellMaxTemp > 30 && frmMain.Selffrm.AllEquipment.LiquidCool.state != 1)
                        {
                            frmMain.Selffrm.AllEquipment.LiquidCool.LCPowerOn(true);//PCS工作前启动液冷机
                        }                    //pcs必须处于低功率状态，且电池常温10---30度就停止液冷
                        else if ((frmMain.Selffrm.AllEquipment.PCSList[0].PcsRun == 255) && (frmMain.Selffrm.AllEquipment.BMS.cellMaxTemp < 25) && (frmMain.Selffrm.AllEquipment.BMS.cellMinTemp > 10))
                        {
                            if (frmMain.Selffrm.AllEquipment.LiquidCool.state == 1)
                            {
                                frmMain.Selffrm.AllEquipment.LiquidCool.LCPowerOn(false);//PCS工作前启动液冷机
                            }
                        }
                    }
                }

                //LED控制
                if (frmMain.Selffrm.AllEquipment.Led != null) 
                {
                    frmMain.Selffrm.AllEquipment.Led.Led_Control_Loop();
                }

                Thread.Sleep(30000);
            }
        }



        public void AutoSystime_Tick()
        {
            try
            {
                //实例化等待连接的线程
                Thread ClientRecThread = new Thread(Systime_Tick);
                ClientRecThread.IsBackground = true;
                ClientRecThread.Priority = ThreadPriority.Lowest;
                ClientRecThread.Start();
                ClientRecThread.Name = "";
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }

        /******************************tcp control*****************************************/
        public void ControlEMSTCP()
        {
            while (true)
            {
                Thread.Sleep(1000);
/*                //问询从机功率
                ReadAllEmsTCP();*/
                //发送充放电模式
                //if (frmMain.Selffrm.AllEquipment.wTypeActive != null && frmMain.Selffrm.AllEquipment.PCSTypeActive != null)
                //{
                    if (!ChechPower)
                    {
                        SetAllPCSCommandTCP(frmMain.Selffrm.AllEquipment.wTypeActive, frmMain.Selffrm.AllEquipment.PCSTypeActive, 1);
                        SetAllPCSKVATCP(frmMain.Selffrm.AllEquipment.wTypeActive, frmMain.Selffrm.AllEquipment.PCSTypeActive, 1);
                    }
                    else
                    {
                        SetAllPCSCommandTCP(frmMain.Selffrm.AllEquipment.wTypeActive, frmMain.Selffrm.AllEquipment.PCSTypeActive, frmMain.Selffrm.AllEquipment.dRate);
                        SetAllPCSKVATCP(frmMain.Selffrm.AllEquipment.wTypeActive, frmMain.Selffrm.AllEquipment.PCSTypeActive, frmMain.Selffrm.AllEquipment.dRate);
                    }
                //}

                //发送功率大小
/*                if (frmMain.Selffrm.AllEquipment.wTypeActive != null && frmMain.Selffrm.AllEquipment.PCSTypeActive != null)
                {
                    if (!ChechPower)
                    {
                        SetAllPCSCommandTCP(frmMain.Selffrm.AllEquipment.wTypeActive, frmMain.Selffrm.AllEquipment.PCSTypeActive, 1);
                    }
                    else
                    {
                        SetAllPCSCommandTCP(frmMain.Selffrm.AllEquipment.wTypeActive, frmMain.Selffrm.AllEquipment.PCSTypeActive, frmMain.Selffrm.AllEquipment.dRate);
                    }
                }*/
            }
        }

        public void AutoControlEMSTCP()
        {
            try
            {
                //实例化等待连接的线程
                Thread ClientRecThread = new Thread(ControlEMSTCP);
                ClientRecThread.IsBackground = true;
                ClientRecThread.Priority = ThreadPriority.Highest;
                ClientRecThread.Start();
                ClientRecThread.Name = "";
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }

        /******************************tcp control*****************************************/


        /******************************485 control*************************************/
        public void ControlEMS() {
            while (true)
            {
                Thread.Sleep(1000);
                if (frmMain.Selffrm.AllEquipment.wTypeActive != null && frmMain.Selffrm.AllEquipment.PCSTypeActive != null)
                {
                    if (!ChechPower)
                    {
                        SetAllPCSCommand(frmMain.Selffrm.AllEquipment.wTypeActive, frmMain.Selffrm.AllEquipment.PCSTypeActive, 1, true);
                    }
                    else
                    {
                        SetAllPCSCommand(frmMain.Selffrm.AllEquipment.wTypeActive, frmMain.Selffrm.AllEquipment.PCSTypeActive, frmMain.Selffrm.AllEquipment.dRate, true);
                    }
                }              
            }
        }

        public void AutoControlEMS() {
            try
            {
                //实例化等待连接的线程
                Thread ClientRecThread = new Thread(ControlEMS);
                ClientRecThread.IsBackground = true;
                ClientRecThread.Priority = ThreadPriority.Highest;
                ClientRecThread.Start();
                ClientRecThread.Name = "";
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }
        /******************************485 control*************************************/
        public void ReadPointGrid()
        {
            while (true)
            {
                Thread.Sleep(1000);
                //有关口表
                if (ChechPower)
                {
                    //获取电网功率
                    double tempGridKVA = 0;
                    double tempPUMdemand_max = 0;
                    double tempPUMdemand_now = 0;
                    //电表1---关口电表，用于防逆流
                    foreach (Elemeter1Class Elemeter1 in Elemeter1List)
                    {
                        Elemeter1.GetAllUkva();
                        tempGridKVA += Elemeter1.AllUkva;

                        Elemeter1.GetPUMdemand_now();
                        tempPUMdemand_max += Elemeter1.PUMdemand_Max;
                        tempPUMdemand_now += Elemeter1.PUMdemand_now;
                    }

                    E1_PUMdemand_Max = tempPUMdemand_max;
                    E1_PUMdemand_now = tempPUMdemand_now;

                    if (Elemeter1List[0].Prepared)
                    {
                        GridKVA = tempGridKVA;
                        AddValue(GridKVA);
                        GridKVA_window = GetAverage();
                    }
                    else
                    {
                        GridKVA = 0;
                        clearAllUkvaWindow();
                        GridKVA_window = 0;
                    }
                }
            }

        }



        public void AutoReadPointGrid()
        {
            try
            {
                //实例化等待连接的线程
                Thread ClientRecThread = new Thread(ReadPointGrid);
                ClientRecThread.IsBackground = true;
                ClientRecThread.Priority = ThreadPriority.Highest;
                ClientRecThread.Start();
                ClientRecThread.Name = "";
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }



        public void ReadPointPower()
        {
            //获取主机pcs功率
            while (true)
            {
                Thread.Sleep(1000);
                double PCSPower = 0;
                for (int i = 0; i < PCSList.Count; i++)
                {
                    PCSList[i].GetallUkva();
                    PCSPower += PCSList[i].allUkva;//主从模式设备整体PCS的功率
                }


                //获取主从整体pcs功率
                if (frmSet.SysCount > 1 && frmSet.IsMaster)
                {
                    if (frmSet.ConnectStatus == "tcp")
                    {
                        ReadAllEmsTCP();
                    }
                    else if (frmSet.ConnectStatus == "485")
                    {
                        ReadAllEmsRTU();
                    }

                    if (Elemeter1Z != null)
                    {
                        AllwaValue = Elemeter1Z.AllUkva;
                    }
                }
            }

        }



        public void AutoReadPointPower()
        {
            try
            {
                //实例化等待连接的线程
                Thread ClientRecThread = new Thread(ReadPointPower);
                ClientRecThread.IsBackground = true;
                ClientRecThread.Priority = ThreadPriority.Highest;
                ClientRecThread.Start();
                ClientRecThread.Name = "";
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }

        public void ReadDataE1()
        {
            //double tempGridKVA;
            double tempPUMdemand_max;
            double tempPUMdemand_now;
            while (true)
            {
                Thread.Sleep(2000);

                //1 关口表
                //tempGridKVA = 0;
                tempPUMdemand_max = 0;
                tempPUMdemand_now = 0;

                //2.21 获取整体功率
                if (Elemeter1Z != null)
                {
                    Elemeter1Z.GetDataFromEqipment();
                }


                if (ChechPower)
                {
                    //电表1---关口电表，用于防逆流
                    foreach (Elemeter1Class Elemeter1 in Elemeter1List)
                    {
                        Elemeter1.GetDataFromEqipment();
                        //tempGridKVA += Elemeter1.AllUkva;

                        //2.21
                        tempPUMdemand_max += Elemeter1.PUMdemand_Max;
                        tempPUMdemand_now += Elemeter1.PUMdemand_now;
                    }

/*                    if (Elemeter1List[0].Prepared)
                    {
                        GridKVA = tempGridKVA;
                        AddValue(GridKVA);
                        GridKVA_window = GetAverage();
                    }
                    else
                    {
                        GridKVA = 0;
                        clearAllUkvaWindow();
                        GridKVA_window = 0;
                    }*/
                }

                //2.21
                E1_PUMdemand_Max = tempPUMdemand_max;
                E1_PUMdemand_now = tempPUMdemand_now;


                if (Elemeter1Z!=null)
                {
                    E2_PUMdemand_Max = Elemeter1Z.PUMdemand_Max;
                    E2_PUMdemand_now = Elemeter1Z.PUMdemand_now;
                }

            }
        }
        public void AutoReadE1()
        {
            try
            {
                //实例化等待连接的线程
                Thread ClientRecThread = new Thread(ReadDataE1);
                ClientRecThread.IsBackground = true;
                ClientRecThread.Priority = ThreadPriority.Normal;
                ClientRecThread.Start();
                ClientRecThread.Name = "";
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }


        //8.7
        public void AutoReadEMSCom7()
        {
            try
            {
                if (frmSet.SysCount > 1)
                {
                    //实例化等待连接的线程
                    Thread ClientRecThread = new Thread(ReadCom7Data);
                    ClientRecThread.IsBackground = true;
                    ClientRecThread.Priority = ThreadPriority.Lowest;
                    ClientRecThread.Start();
                    ClientRecThread.Name = "";
                }
                
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }

        private void ReadCom7Data()
        {
            while (true)
            {
                try
                {
                    GetDataFromEMS();
                }
                catch (Exception ex)
                {
                    frmMain.ShowDebugMSG("读取线程故障" + ex.ToString());
                }
            }
        }

        public void GetDataFromEMS()
        {
            foreach (EMSEquipment oneEMSE in EMSList)
            {
                oneEMSE.GetDataFromEqipment2(oneEMSE.ID);
            }
          
        }

        //Com1 readThread1
        //实例化等待连接的线程
        public void AutoReadDataCom1()
        {
            try
            {
                //实例化等待连接的线程
                Thread ClientRecThread = new Thread(ReadCom1Data);
                ClientRecThread.IsBackground = true;
                ClientRecThread.Priority = ThreadPriority.Highest;
                ClientRecThread.Start();
                ClientRecThread.Name = "";
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }

        ///Com1 读取函数 
        //单机的防逆流
        public void SingleReflux_Log()
        {
            log.Debug("超限防逆需量上限：" + frmSet.MaxGridKW + " "
                +"超限防逆电网功率下限：" +  frmSet.MinGridKW + " "
                +"超限防逆电网功率：" +  GridKVA + " "
                +"超限防逆计算差值: " + dValue + " "
                +"超限防逆计算修正比: " + dRate + " "
                +"超限防逆PCS充放电模式：" + wTypeActive + " "
                +"超限防逆PCS工作状态：" + PCSTypeActive + " "
                +"超限防逆PCS单机实际功率：" + PCSKVA + " "
                +"超限防逆PCS单机计划功率：" + PCSScheduleKVA + " "
                +"超限防逆PCS功率：" + PCSScheduleKVA * dRate + " "
                +"超限防逆PCS本机当前功率：" + PCSKVA);
        }


        private void SingleReflux()
        {
            dValue = 0;
            //加限流和防逆--------------单机状态
            //double dGridKW = Math.Abs(GridKVA); //当前电网功率   bug 1.26
            double dGridKW = GridKVA;

            //9.4   区分自适应需量和恒功率
            double PowerCap = 0;  //电网功率上限

            //策略模式: frmMain.Selffrm.AllEquipment.PCSTypeActive)

            if (frmMain.Selffrm.AllEquipment.PCSTypeActive == "自适应需量")
            {
                DateTime dateTime = DateTime.Now;

                //新策略时段修改充放电状态为放电，若未开发策略修改权限。则恢复策略线程权限，重新读取策略
                if (frmMain.Selffrm.AllEquipment.PrewTypeActive == "放电" && frmMain.Selffrm.AllEquipment.GotoSchedule ==false)
                {
                    //计划策略切换放电
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        frmMain.Selffrm.AllEquipment.GotoSchedule = true;
                    }
                    lock (frmMain.TacticsList)
                    {
                        frmMain.TacticsList.ActiveIndex = -2;
                    }
                }
                //强制放电5分钟后
                if ((frmMain.Selffrm.AllEquipment.GotoSchedule == false) && DateTime.Compare(dateTime, start_Time.AddMinutes(5)) > 0)
                {
                    //判断操作为恢复策略还是等待
                    //若电网平均功率低于需量的上限阈值，则恢复策略
                    if (GridKVA_window < (Client_PUMdemand_Max*frmSet.PUM)/100)
                    {
                        //重新下发策略
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            frmMain.Selffrm.AllEquipment.GotoSchedule = true;
                        }
                        lock (frmMain.TacticsList)
                        {
                            frmMain.TacticsList.ActiveIndex = -2;
                        }
                    }
                    else
                    {
                        //结束放电，静置等待
                        //客户正在拉升需量，继续等待
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            dRate = 0;
                            PCSScheduleKVA = 0;
                        }
                    }
                }// if ((GotoSchedule == false) && DateTime.Compare(dateTime,start_Time.AddMinutes(5)) > 0)
                if (frmMain.TacticsList.ActiveIndex >= 0 && frmMain.Selffrm.AllEquipment.GotoSchedule)
                {
                    //"恢复策略：" + "当前电网功率:" + GridKVA_window + " " + "客户最大需量的x倍：" + (Client_PUMdemand_Max*frmSet.PUM)/100
                    if (wTypeActive == "充电")
                    {
                        //当前需量 超过 最大需量的X倍 ：强制放电
                        if (GridKVA_window > (Client_PUMdemand_Max*frmSet.PUM)/100 && FineToCharge == true)
                        {
                            //修改成强制放电，设置充放电模式，pcs功率，开始强制放电时间，禁止策略线程更新工作状态（充放电模式，pcs功率，pcs模式）
                            lock (frmMain.Selffrm.AllEquipment)
                            {
                                //"强制放电"
                                wTypeActive = "放电";
                                PCSScheduleKVA = 20;
                                dRate = 1;
                                start_Time = DateTime.Now;
                            }
                            lock (frmMain.Selffrm.AllEquipment)
                            {
                                frmMain.Selffrm.AllEquipment.GotoSchedule = false;
                            }
                            FineToCharge = false;
                        }
                        else
                        {
                            //判断电网功率是否超过需量上限
                            if (GridKVA >=  Client_PUMdemand_Max*frmSet.PUM/100)
                            {
                                dValue = GridKVA - Client_PUMdemand_Max*frmSet.PUM/100;
                                if (dValue > Math.Abs(PCSKVA))
                                {
                                    dRate = 0;
                                }
                                else
                                {
                                    if (Math.Abs(PCSScheduleKVA) != 0)
                                    {
                                        dRate = (Math.Abs(PCSKVA) - dValue) / Math.Abs(PCSScheduleKVA);
                                        FineToCharge = true;
                                    }
                                    else
                                    {
                                        dRate = 0;
                                    }
                                }
                            }
                            else
                            {
                                dValue = (Client_PUMdemand_Max*frmSet.PUM)/100 - (GridKVA - Math.Abs(PCSKVA));
                                if (dValue >= Math.Abs(PCSScheduleKVA))
                                {
                                    dRate = 1;
                                    FineToCharge = true;
                                }
                                else
                                {
                                    if (Math.Abs(PCSScheduleKVA) != 0)
                                    {
                                        dRate =  (dValue / Math.Abs(PCSScheduleKVA));
                                        FineToCharge = true;
                                    }
                                    else { dRate = 0; }
                                }
                            }
                        }
                    }
                }

                //在非静置状态下放电行为: 强制放电 + 策略放电
                if ((frmMain.TacticsList.ActiveIndex>=0) || (frmMain.Selffrm.AllEquipment.GotoSchedule == false))
                {
                    if (wTypeActive == "放电")
                    {
                        if (GridKVA <= frmSet.MinGridKW)//逆流处理
                        {
                            //逆流
                            dValue = frmSet.MinGridKW - GridKVA;
                            //限流qiao 
                            if (dValue >= Math.Abs(PCSKVA))
                            {
                                dRate= 0;
                            }
                            else
                            {
                                if (PCSScheduleKVA != 0)
                                {
                                    dRate = ((Math.Abs(PCSKVA) - dValue) / Math.Abs(PCSScheduleKVA));
                                }
                                else
                                {
                                    dRate = 0;
                                }
                            }
                        }
                        else//放电功率调整
                        {
                            dValue = (GridKVA + Math.Abs(PCSKVA)) - frmSet.MinGridKW;
                            if (dValue >= Math.Abs(PCSScheduleKVA))
                            {
                                dRate = 1;
                            }
                            else
                            {
                                if (PCSScheduleKVA != 0)
                                {
                                    dRate =  (dValue / Math.Abs(PCSScheduleKVA));
                                }
                                else { dRate = 0; }
                            }

                        }
                    }
                }
            }
            else
            {
                PowerCap = frmSet.MaxGridKW;

                if (!frmMain.Selffrm.AllEquipment.GotoSchedule)
                {
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        frmMain.Selffrm.AllEquipment.GotoSchedule = true;
                    }
                    lock (frmMain.TacticsList)
                    {
                        frmMain.TacticsList.ActiveIndex = -2;
                    }
                }

                if ((GridKVA >= PowerCap)  && (wTypeActive == "充电"))//表1用于限流和防止超限 ,PCS在不工作时也有0.1，0.2的功率值
                { //超限
                    dValue = dGridKW - PowerCap;
                    //限流qiao 
                    if (dValue >= Math.Abs(PCSKVA))
                    {
                        dRate = 0;
                    }
                    else
                    {
                        if (PCSScheduleKVA != 0)
                        {
                            dRate = ((Math.Abs(PCSKVA) - dValue) / Math.Abs(PCSScheduleKVA));
                        }
                        else { dRate = 0; }
                    }
                }
                else if ((GridKVA <= frmSet.MinGridKW) && (wTypeActive == "放电"))
                {
                    //逆流
                    dValue = frmSet.MinGridKW - GridKVA;
                    //限流qiao 
                    if (dValue >= Math.Abs(PCSKVA))
                    {
                        dRate= 0;
                    }
                    else
                    {
                        if (PCSScheduleKVA != 0)
                        {
                            dRate = ((Math.Abs(PCSKVA) - dValue) / Math.Abs(PCSScheduleKVA));
                        }
                        else { dRate = 0; }
                    }
                }
                else
                {
                    if (wTypeActive == "充电")
                    {
                        dValue = PowerCap - (GridKVA_window - Math.Abs(PCSKVA));
                        if (dValue > 0)
                        {
                            if (dValue >= Math.Abs(PCSScheduleKVA))
                            {
                                dRate = 1;
                            }
                            else
                            {
                                if (PCSScheduleKVA != 0)
                                {
                                    dRate = (dValue / Math.Abs(PCSScheduleKVA));
                                }
                                else { dRate = 0; }
                            }
                        }
                        else
                        {
                            dRate = 0;
                        }
                    }
                    else if (wTypeActive == "放电")
                    {
                        dValue = (GridKVA_window + Math.Abs(PCSKVA)) - frmSet.MinGridKW;
                        if (dValue > 0)
                        {
                            if (dValue >= Math.Abs(PCSScheduleKVA))
                            {
                                dRate = 1;
                            }
                            else
                            {
                                if (PCSScheduleKVA != 0)
                                {
                                    dRate =  (dValue / Math.Abs(PCSScheduleKVA));
                                }
                                else { dRate = 0; }
                            }
                        }
                        else
                        {
                            dRate = 0;
                        }
                    }
                }
            }
            ExcPCSCommand(wTypeActive, PCSTypeActive, (int)(PCSScheduleKVA * dRate));
        }

        //485
        public void ReadAllEmsRTU()
        {
            double TempWaValue = PCSKVA;
            foreach (EMSEquipment oneEMSE in EMSList)
            {
                oneEMSE.GetDataFromEqipment2(oneEMSE.ID);
                if (oneEMSE.WorkType == 0)
                {
                    TempWaValue -= oneEMSE.waValueActive;
                }
                else if (oneEMSE.WorkType == 1)
                {
                    TempWaValue += oneEMSE.waValueActive;
                }
            }
            AllwaValue = TempWaValue;
        }

        //502
        public void ReadAllEmsTCP()
        {
            double TempWaValue = PCSKVA;
            for (int i = 0; i < 10; ++i)
            {
                int ID = frmMain.Selffrm.ModbusTcpServer.clientManager.IDss[i];
                if (ID == -1)
                {
                    continue;
                }
                //问询第"+ ID +"台机"
                ushort tempKVA = 0;
                ushort tempType = 0;
                if (frmMain.Selffrm.ModbusTcpServer.clientMap.ContainsKey(ID))
                {
                    try
                    {
                        SocketWrapper client = frmMain.Selffrm.ModbusTcpServer.clientManager.GetClient(ID, ref frmMain.Selffrm.ModbusTcpServer.clientMap);
                        object socketLock = frmMain.Selffrm.ModbusTcpServer.clientManager.GetsocketLock(ID, ref frmMain.Selffrm.ModbusTcpServer.clientMap);
                        if (client != null)
                        {
                            if (!frmMain.Selffrm.ModbusTcpServer.GetUShort(ID, ref client, ref socketLock, 3, 0x6002, 1, ref tempKVA))
                            {                     
                                continue;
                            }
                            if (!frmMain.Selffrm.ModbusTcpServer.GetUShort(ID, ref client, ref socketLock, 3, 0x6003, 1, ref tempType))
                            {
                                continue;
                            }
                            if (tempType == 0)
                            {
                                TempWaValue -= tempKVA;
                            }
                            else if (tempType == 1)
                            {
                                TempWaValue += tempKVA;
                            }
                        }
                    }
                    catch (ArgumentException ex)
                    {
                        log.Error("捕获入参ex: " + ex.Message);
                    }

                    //读取多个寄存器值
                    //string result = "";
                    //frmMain.Selffrm.ModbusTcpServer.GetString(ID, client, ref buffer, 3, 0x5001, 18, ref result, true);

                }
            }
            AllwaValue = TempWaValue;
        }

        public void SetAllPCSKVATCP(string awType, string aPCSType, double aPCSValueRate)
        {
            bool bPrepared = false;
            string[] wTpyes = { "充电", "放电" };
            int itemp = 0;

            if (SlaveStart)
            {
                if ((!frmSet.IsMaster)||(frmSet.PCSGridModel==1))
                    return;

                for (int i = 0; i < 10; ++i)
                {
                    int ID = frmMain.Selffrm.ModbusTcpServer.clientManager.IDss[i];
                    if (ID == -1)
                    {
                        continue;
                    }
                    if (frmMain.Selffrm.ModbusTcpServer.clientMap.ContainsKey(ID))
                    {
                        //byte[] buffer = frmMain.Selffrm.ModbusTcpServer.clientManager.GetBuffer(ID, ref frmMain.Selffrm.ModbusTcpServer.clientMap);
                        //byte[] buffer1 = new byte[1024];
                        SocketWrapper client = frmMain.Selffrm.ModbusTcpServer.clientManager.GetClient(ID, ref frmMain.Selffrm.ModbusTcpServer.clientMap);

                        /*                        itemp = Array.IndexOf(wTpyes, awType);
                                                if (frmMain.Selffrm.ModbusTcpServer.SendAskMSG(ID, client, ref buffer1, 6, 0x6003, (ushort)itemp) == -1)
                                                {
                                                    continue;
                                                }
                                                byte[] buffer2 = new byte[1024];
                                                itemp = Array.IndexOf(PCSClass.PCSTypes, aPCSType);
                                                if (frmMain.Selffrm.ModbusTcpServer.SendAskMSG(ID, ref client, ref buffer2, 6, 0x6004, (ushort)itemp) == -1)
                                                {
                                                    continue;
                                                }*/

                        object socketLock = frmMain.Selffrm.ModbusTcpServer.clientManager.GetsocketLock(ID, ref frmMain.Selffrm.ModbusTcpServer.clientMap);
                        if (socketLock != null && client != null)
                        {
                            double dtemp = (frmMain.Selffrm.AllEquipment.PCSScheduleKVA * aPCSValueRate);
                            if (frmMain.Selffrm.ModbusTcpServer.Send6MSG(ID, ref client, ref socketLock, 6, 0x6002, (ushort)dtemp) == -1)
                            {
                                continue;
                            }
                        }
                    }
                }
            }
        }


        public void SetAllPCSCommandTCP(string awType, string aPCSType, double aPCSValueRate)
        {
            bool bPrepared = false;
            string[] wTpyes = { "充电", "放电" };
            int itemp = 0;

            if (SlaveStart)
            {
                if ((!frmSet.IsMaster)||(frmSet.PCSGridModel==1))
                    return;

                for (int i = 0; i < 10; ++i)
                {
                    int ID = frmMain.Selffrm.ModbusTcpServer.clientManager.IDss[i];
                    if (ID == -1)
                    {
                        continue;
                    }
                    if (frmMain.Selffrm.ModbusTcpServer.clientMap.ContainsKey(ID))
                    {
                        object socketLock = frmMain.Selffrm.ModbusTcpServer.clientManager.GetsocketLock(ID, ref frmMain.Selffrm.ModbusTcpServer.clientMap);
                        SocketWrapper client = frmMain.Selffrm.ModbusTcpServer.clientManager.GetClient(ID, ref frmMain.Selffrm.ModbusTcpServer.clientMap);

                        if (socketLock != null && client != null)
                        {
                            itemp = Array.IndexOf(wTpyes, awType);
                            if (frmMain.Selffrm.ModbusTcpServer.Send6MSG(ID, ref client, ref socketLock, 6, 0x6003, (ushort)itemp) == -1)
                            {
                                continue;
                            }
                        }
/*                        byte[] buffer2 = new byte[1024];
                        itemp = Array.IndexOf(PCSClass.PCSTypes, aPCSType);
                        log.Error("发送第二条");
                        if (frmMain.Selffrm.ModbusTcpServer.SendAskMSG(ID, client, ref buffer2, 6, 0x6004, (ushort)itemp) == -1)
                        {
                            continue;
                        }
                        log.Error("发送第二条成功返回");
                        byte[] buffer3 = new byte[1024];
                        double dtemp = (frmMain.Selffrm.AllEquipment.PCSScheduleKVA * aPCSValueRate);
                        log.Error("发送第"+ ID +"台机:"+"计划功率："+ PCSScheduleKVA+"发送功率：" + dtemp);
                        if (frmMain.Selffrm.ModbusTcpServer.SendAskMSG(ID, client, ref buffer3, 6, 0x6002, (ushort)dtemp) == -1)
                        {
                            continue;
                        }
                        log.Error("发送第三条成功返回");*/
                    }
                }
            }
            else
            {
                //待补充关机命令下发
                for (int i = 0; i < 10; ++i)
                {
                    int ID = frmMain.Selffrm.ModbusTcpServer.clientManager.IDss[i];
                    if (ID == -1)
                    {
                        continue;
                    }
                    //log.Error("发送第"+ ID +"台机关机");
                    if (frmMain.Selffrm.ModbusTcpServer.clientMap.ContainsKey(ID))
                    {
                        //byte[] buffer = frmMain.Selffrm.ModbusTcpServer.clientManager.GetBuffer(ID, ref frmMain.Selffrm.ModbusTcpServer.clientMap);
                        SocketWrapper client = frmMain.Selffrm.ModbusTcpServer.clientManager.GetClient(ID, ref frmMain.Selffrm.ModbusTcpServer.clientMap);
                        object socketLock = frmMain.Selffrm.ModbusTcpServer.clientManager.GetsocketLock(ID, ref frmMain.Selffrm.ModbusTcpServer.clientMap);
                        if (frmMain.Selffrm.ModbusTcpServer.Send6MSG(ID, ref client, ref socketLock, 6, 0x6000, 0) == -1)
                        {
                            continue;
                        }
                    }
                }
            }
        }







        //主从模式下，主机控制从机
        public void SetAllPCSCommand(string awType,string  aPCSType, double aPCSValueRate, bool bAllParam)
        {
            if (SlaveStart)
            {
                if ((!frmSet.IsMaster)||(frmSet.PCSGridModel==1))
                    return;
                foreach (EMSEquipment oneEMSE in EMSList)
                {
                    //oneEMSE.ExcPCSCommand(awType, aPCSType, aPCSValueRate, bAllParam);

                    if (frmSet.SysCount > 1)
                    {
                        oneEMSE.ExcPCSCommand(awType, aPCSType, aPCSValueRate, bAllParam);
                    }
                    //oneEMSE.ExcPCSCommand(awType, aPCSType, aPCSValueRate, bAllParam);
                }
            }
            else
            {
                SetAllPCSOn(false);
            }
        }

        public void SetAllPCSSheduleKVA(double aPCSScheduleKVA)
        {
            if ((!frmSet.IsMaster) || (frmSet.PCSGridModel == 1))
                return;
            foreach (EMSEquipment oneEMSE in EMSList)
            {
                //oneEMSE.GetEquipmendData();
                oneEMSE.ShedulePCSKVA = aPCSScheduleKVA;
                // if (oneEMSE.Prepared)
                {
                    oneEMSE.SetPCSScheduleKVA(aPCSScheduleKVA);
                }
            }
        }

        //8.6
        public void SetAllPCSOn(bool aOn)
        {
            if ((!frmSet.IsMaster)|| (frmSet.PCSGridModel == 1))
                return;
            foreach (EMSEquipment oneEMSE in EMSList)
            {
                //oneEMSE.GetEquipmendData();
                //if (oneEMSE.Prepared)
                {
                    oneEMSE.ExcPCSOn(aOn);
                }
            }
        }

        //多机的防逆流
        public void MutiReflux_Log()
        {
            log.Debug("超限防逆上限：" + frmSet.MaxGridKW + " "
                +"超限防逆下限：" + frmSet.MinGridKW + " "
                +"超限防逆电网功率：" +  GridKVA + " "
                +"超限防逆计算差值: " + dValue + " "
                +"超限防逆计算修正比: " + dRate + " "
                +"超限防逆PCS充放电模式：" + wTypeActive + " "
                +"超限防逆PCS工作状态：" + PCSTypeActive + " "
                +"超限防逆PCS主从实际功率：" + Math.Abs(AllwaValue) + " "
                +"超限防逆整体计划功率：" +  Math.Abs(AllPCSScheduleKVA) + " "
                +"超限防逆PCS本机实际计划功率：" + PCSScheduleKVA * dRate + " "
                +"超限防逆PCS本机当前功率：" + PCSKVA);
        }

        public void ClientControl_Log()
        {
            if (ChechPower)
            {
                log.Info("发送功率: " + frmMain.Selffrm.AllEquipment.PCSScheduleKVA *  frmMain.Selffrm.AllEquipment.dRate);
            }
            else 
            {
                log.Info("发送功率: " + frmMain.Selffrm.AllEquipment.PCSScheduleKVA *  1);
            }
        }

        private void MutiReflux()
        {
            dValue = 0;
            double PowerCap = 0;  //电网功率上限
            double dGridKW = GridKVA;

            //2.21
            if (frmMain.Selffrm.AllEquipment.PCSTypeActive == "自适应需量")
            {
                DateTime dateTime = DateTime.Now;

                if (frmMain.Selffrm.AllEquipment.PrewTypeActive == "放电" && frmMain.Selffrm.AllEquipment.GotoSchedule ==false)
                {
                    //计划策略切换放电
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        frmMain.Selffrm.AllEquipment.GotoSchedule = true;
                    }
                    lock (frmMain.TacticsList)
                    {
                        frmMain.TacticsList.ActiveIndex = -2;
                    }
                    recoverSchedule = true;
                }
                //持续放电5分钟
                if ((recoverSchedule == false) && DateTime.Compare(dateTime, start_Time.AddMinutes(1)) > 0)
                {
                    //判断操作为恢复策略还是等待
                    //if (E1_PUMdemand_now < (Client_PUMdemand_Max*frmSet.PUM)/100)
                    if (GridKVA_window < (Client_PUMdemand_Max*frmSet.PUM)/100)
                    {
                        //重新读取策略
                        recoverSchedule = true;
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            frmMain.Selffrm.AllEquipment.GotoSchedule = true;
                        }
                        lock (frmMain.TacticsList)
                        {
                            frmMain.TacticsList.ActiveIndex = -2;
                        }
                    }
                    else
                    {
                        //客户正在拉升需量，继续等待
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            dRate = 0;
                            PCSScheduleKVA = 0;
                        }
                    }
                }
                if (frmMain.TacticsList.ActiveIndex>=0)
                {
                    if (recoverSchedule)
                    {
                        if (wTypeActive == "充电")
                        {
                            //当前需量 超过 最大需量的X倍 ：强制放电
                            if (GridKVA_window > (Client_PUMdemand_Max*frmSet.PUM)/100 && FineToCharge == true)
                            {
                                //修改成放电
                                lock (frmMain.Selffrm.AllEquipment)
                                {
                                    //强制放电
                                    wTypeActive = "放电";
                                    PCSScheduleKVA = 20;
                                    AllPCSScheduleKVA = PCSScheduleKVA* (EMSList.Count+1);
                                    dRate = 1;
                                    start_Time = DateTime.Now;
                                    recoverSchedule = false;
                                }
                                lock (frmMain.Selffrm.AllEquipment)
                                {
                                    frmMain.Selffrm.AllEquipment.GotoSchedule = false;
                                }
                                FineToCharge = false;                         
                            }
                            else
                            {
                                if (GridKVA >=  Client_PUMdemand_Max*frmSet.PUM/100)
                                { 
                                    dValue = GridKVA - Client_PUMdemand_Max*frmSet.PUM/100;
                                    if (dValue > Math.Abs(AllwaValue))
                                    {
                                        dRate = 0;
                                    }
                                    else
                                    {
                                        if (Math.Abs(AllPCSScheduleKVA) != 0)
                                        {
                                            dRate = (Math.Abs(AllwaValue) - dValue) / Math.Abs(AllPCSScheduleKVA);
                                            FineToCharge = true;
                                        }
                                        else
                                        {
                                            dRate = 0;
                                        }
                                    }
                                }
                                else
                                {
                                    dValue = (Client_PUMdemand_Max*frmSet.PUM)/100 - (GridKVA - Math.Abs(AllwaValue));
                                    if (dValue >= Math.Abs(AllPCSScheduleKVA))
                                    {
                                        dRate = 1;
                                        FineToCharge = true;
                                    }
                                    else
                                    {
                                        if (Math.Abs(AllPCSScheduleKVA) != 0)
                                        {
                                            dRate =  (dValue / Math.Abs(AllPCSScheduleKVA));
                                            FineToCharge = true;
                                        }
                                        else { dRate = 0; }
                                    }
                                }
                            }
                        }
                    }
                }


                //在非静置状态下放电行为
                if ((frmMain.TacticsList.ActiveIndex>=0) || (recoverSchedule == false))
                {
                    if (wTypeActive == "放电")
                    {
                        if (GridKVA <= frmSet.MinGridKW)//逆流处理
                        {
                            //逆流
                            dValue = frmSet.MinGridKW - GridKVA;
                            //限流qiao 
                            if (dValue >= Math.Abs(AllwaValue))
                            {
                                dRate= 0;
                            }
                            else
                            {
                                if (AllPCSScheduleKVA != 0)
                                {
                                    dRate = (Math.Abs(AllwaValue) - dValue / Math.Abs(AllPCSScheduleKVA));
                                }
                                else
                                {
                                    dRate= 0;
                                }
                            }
                        }
                        else//放电功率调整
                        {
                            dValue = (GridKVA + Math.Abs(AllwaValue)) - frmSet.MinGridKW;
                            if (dValue >=  Math.Abs(AllPCSScheduleKVA))
                            {
                                dRate = 1;
                            }
                            else
                            {
                                if (Math.Abs(AllPCSScheduleKVA) != 0)
                                {
                                    dRate =  (dValue / Math.Abs(AllPCSScheduleKVA));
                                }
                                else
                                {
                                    dRate = 0;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                PowerCap = frmSet.MaxGridKW;
                if (!frmMain.Selffrm.AllEquipment.GotoSchedule)
                {
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        frmMain.Selffrm.AllEquipment.GotoSchedule = true;
                    }
                    lock (frmMain.TacticsList)
                    {
                        frmMain.TacticsList.ActiveIndex = -2;
                    }
                }

                if ((GridKVA >= PowerCap) && (wTypeActive == "充电"))//表1用于限流和防止超限
                { //超限
                    dValue = dGridKW - PowerCap;
                    //限流qiao 
                    //直接全部关闭
                    if (dValue >=  Math.Abs(AllwaValue))
                    {
                        dRate = 0;
                    }
                    //降低功率
                    else
                    {
                        if (AllPCSScheduleKVA != 0)
                            dRate = ((Math.Abs(AllwaValue) - dValue) / Math.Abs(AllPCSScheduleKVA));
                        else
                            dRate = 0;
                    }
                }
                else if ((GridKVA <= frmSet.MinGridKW)  && (wTypeActive == "放电") &&(frmSet.PCSGridModel == 0))//0并网，1离网 模式需要不控制
                { //逆流
                    dValue = frmSet.MinGridKW - GridKVA;
                    //限流qiao 
                    if (dValue >= Math.Abs(AllwaValue))
                    {
                        dRate= 0;
                    }
                    else
                    {
                        if (AllPCSScheduleKVA != 0)
                            dRate = ((Math.Abs(AllwaValue) - dValue) / Math.Abs(AllPCSScheduleKVA));
                        else
                            dRate = 0;
                    }
                }
                else
                { //查看是否需要恢复
                    if (wTypeActive == "充电")
                    {
                        dValue = PowerCap - (GridKVA_window - Math.Abs(AllwaValue));
                        if (dValue > 0)
                        {
                            if (dValue >=  Math.Abs(AllPCSScheduleKVA))
                            {
                                dRate = 1;
                            }
                            else
                            {
                                if (AllPCSScheduleKVA != 0)
                                {
                                    dRate = (dValue /  Math.Abs(AllPCSScheduleKVA));
                                }
                                else { dRate = 0; }
                            }
                        }
                        else
                        {
                            dRate = 0;
                        }
                    }
                    else if (wTypeActive == "放电")
                    {
                        dValue = (GridKVA_window + Math.Abs(AllwaValue)) - frmSet.MinGridKW;
                        if (dValue > 0)
                        {
                            if ((dValue > Math.Abs(AllPCSScheduleKVA)) || (frmSet.PCSGridModel == 1))
                            {
                                dRate = 1;

                            }
                            else
                            {
                                if (AllPCSScheduleKVA != 0)
                                {
                                    dRate = (dValue /  Math.Abs(AllPCSScheduleKVA));
                                }
                                else { dRate = 0; }
                            }
                        }
                        else
                        {
                            dRate = 0;
                        }
                    }
                }
            }
            ExcPCSCommand(wTypeActive, PCSTypeActive, (int)(PCSScheduleKVA * dRate));
        }


        //表一队列数据
        private void ReadCom1Data()
        {
            while (true)
            {
                Thread.Sleep(1000);
                try
                {
                    //如果是从机
                    //如果是从机
                    if (!frmSet.IsMaster)
                    {
                        //获取pcs功率
                        double PCSPower = 0;
                        for (int i = 0; i < PCSList.Count; i++)
                        {
                            PCSList[i].GetallUkva();
                            PCSPower += PCSList[i].allUkva;
                        }
                        PCSKVA = Math.Round(PCSPower, 2);

                        //判断是否超时控制，如果超时就停机等待
                        if (frmSet.ConnectStatus == "tcp")
                        {
                            if (frmMain.Selffrm.ModbusTcpClient.Connected)
                            {
                                //连接情况
                                if (NetConnect)
                                {
                                    //超时未收到控制
                                    //if (NetCtlTime.AddSeconds(30)<DateTime.Now)
                                    if (Clock_Watch.MeasureIntervalInSeconds() > 30)
                                    {
                                        //关闭PCS
                                        frmSet.PCSMOff();
                                        if (PCSList.Count > 0)
                                        {
                                            if (frmMain.Selffrm.AllEquipment.PCSList[0].PcsRun != 255)
                                            {
                                                log.Error("主从脱钩,关闭pcs");
                                                PCSList[0].ExcSetPCSPower(false);
                                            }
                                        }
                                        //关闭空调（液冷机）
                                        if (frmMain.Selffrm.AllEquipment.TempControl != null)
                                        {
                                            if (frmMain.Selffrm.AllEquipment.TempControl.state == 1)
                                            {
                                                frmMain.Selffrm.AllEquipment.TempControl.TCPowerOn(false);
                                            }
                                        }
                                        if (frmMain.Selffrm.AllEquipment.LiquidCool != null)
                                        {
                                            if (frmMain.Selffrm.AllEquipment.LiquidCool.state == 1)
                                            {
                                                frmMain.Selffrm.AllEquipment.LiquidCool.LCPowerOn(false);
                                            }
                                        }
                                        NetControl = false;
                                        NetConnect = false;
                                        frmMain.Selffrm.ModbusTcpClient.Connected = false;
                                        //SqlExecutor.RecordLOG("网控", "网控超时停止服务", "进入待机状态");
                                        continue;
                                    }
                                    else
                                    {
                                        //已接收控制指令
                                        if (NetControl)
                                        {
                                            frmMain.Selffrm.AllEquipment.PCSTypeActive = "恒功率";
                                            ExcPCSCommand(wTypeActive, PCSTypeActive, (int)Math.Round(PCSScheduleKVA));
                                        }
                                    }
                                }
                                else
                                {
                                    //10s未接收到主机消息，则判断主从通讯断开
                                    if (NetCtlTime.AddSeconds(10)<DateTime.Now)
                                    {
                                        frmMain.Selffrm.ModbusTcpClient.Connected = false;
                                    }
                                }
                            }
                            else//客户端发送报文失败，重连
                            {
                                log.Debug("重连");
                                //若刚开启EMS，pcs已经在工作，则必须立即停止
                                if (PCSList.Count > 0)
                                {
                                    if (frmMain.Selffrm.AllEquipment.PCSList[0].PcsRun != 255)//关闭pcs
                                    {
                                        log.Error("主从脱钩,关闭pcs");
                                        PCSList[0].ExcSetPCSPower(false);
                                        frmSet.PCSMOff();
                                    }
                                }

                                //从机发起重连
                                frmMain.Selffrm.ModbusTcpClient.ConnectTCP();
                                continue;
                            }
                        }
                        else
                        {
                            if (NetControl)
                            {
                                //超时
                                if (NetCtlTime.AddMinutes(5)<DateTime.Now)//最近一次通讯时间和现在时间间隔超过1min
                                {
                                    //9.16新增注释语句
                                    //frmMain.Selffrm.AllEquipment.PCSScheduleKVA = 0;
                                    //关闭PCS
                                    frmSet.PCSMOff();
                                    if (frmMain.Selffrm.AllEquipment.PCSList[0].PcsRun != 255)
                                    {
                                        log.Error("主从脱钩,关闭pcs");
                                        PCSList[0].ExcSetPCSPower(false);
                                    }
                                    //关闭空调（液冷机）
                                    if (frmMain.Selffrm.AllEquipment.TempControl != null)
                                    {
                                        if (frmMain.Selffrm.AllEquipment.TempControl.state == 1)
                                        {
                                            frmMain.Selffrm.AllEquipment.TempControl.TCPowerOn(false);
                                        }
                                    }
                                    if (frmMain.Selffrm.AllEquipment.LiquidCool != null)
                                    {
                                        if (frmMain.Selffrm.AllEquipment.LiquidCool.state == 1)
                                        {
                                            frmMain.Selffrm.AllEquipment.LiquidCool.LCPowerOn(false);
                                        }
                                    }
                                    NetControl = false;                                
                                }
                                else
                                {
                                    ExcPCSCommand(wTypeActive, PCSTypeActive, (int)Math.Round(PCSScheduleKVA));
                                }
                            }
                            else //结束网控
                            {
                                continue;
                            }
                        }
                    }
                    //如果是主机
                    else
                    {
                        //没有关口表
                        if (!ChechPower)
                        {
                            ExcPCSCommand(wTypeActive, PCSTypeActive, (int)Math.Round(PCSScheduleKVA));
                            continue;
                        }

                        //如果主机是单机  
                        if (frmSet.SysCount == 1)
                        {
                            //2.21
                            //Client_PUMdemand_Max = E1_PUMdemand_Max - E2_PUMdemand_Max;
                            Client_PUMdemand_now = E1_PUMdemand_now - E2_PUMdemand_now;
                            if (Client_PUMdemand_now > Client_PUMdemand_Max)
                            {
                                Client_PUMdemand_Max = Client_PUMdemand_now;
                                WriteDoPUini();
                            }

                            if (Client_PUMdemand_Max < 100 && frmMain.Selffrm.AllEquipment.PrePCSTypeActive == "自适应需量")
                            {
                                continue;
                            }

                            //单机防逆流
                            SingleReflux();
                        }
                        else
                        {
                            //冗余判断
                            if (EMSList.Count == 0)
                            {
                                //2.21
                                //Client_PUMdemand_Max = E1_PUMdemand_Max - E2_PUMdemand_Max;
                                Client_PUMdemand_now = E1_PUMdemand_now - E2_PUMdemand_now;
                                if (Client_PUMdemand_now > Client_PUMdemand_Max)
                                {
                                    Client_PUMdemand_Max = Client_PUMdemand_now;
                                    WriteDoPUini();
                                }
                                if (Client_PUMdemand_Max < 100 && frmMain.Selffrm.AllEquipment.PrePCSTypeActive == "自适应需量")
                                {
                                    continue;
                                }
                                SingleReflux();
                                continue;
                            }
                            AllPCSScheduleKVA = PCSScheduleKVA* (EMSList.Count+1);

                            if (Elemeter1Z != null)
                            {
                                AllwaValue = Elemeter1Z.AllUkva;
                            }


                            //多机器的主机                          
                            Client_PUMdemand_now = E1_PUMdemand_now - E2_PUMdemand_now*(EMSList.Count+1);
                            if (Client_PUMdemand_now > Client_PUMdemand_Max)
                            {
                                Client_PUMdemand_Max = Client_PUMdemand_now;
                                WriteDoPUini();
                            }
                            if (Client_PUMdemand_Max < 100 && frmMain.Selffrm.AllEquipment.PrePCSTypeActive == "自适应需量")
                            {
                                continue;
                            }
                            MutiReflux();
                        }
                    }//else
                }
                catch (Exception ex)
                {
                    frmMain.ShowDebugMSG("读取线程故障" + ex.ToString());
                }
            }
        }
        //Com2 readThread2

        public void AutoReadDataCom2()
        {
            try
            {
                //实例化等待连接的线程
                Thread ClientRecThread = new Thread(ReadCOM2Data);
                ClientRecThread.IsBackground = true;
                ClientRecThread.Priority = ThreadPriority.Normal;
                ClientRecThread.Start();
                ClientRecThread.Name = "";
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }

        private void ReadCOM2Data()
        {
            while (true)
            {
                Thread.Sleep(2000);
                try
                {
                    GetDataFromEqipment();
                }
                catch (Exception ex)
                {
                    frmMain.ShowDebugMSG("读取线程故障" + ex.ToString());
                }
            }
        }

        //电表2和空调数据更为重要，所以反复问询
        private void GetBaseEquipment()
        {
            try
            {
                if (TempControl != null)
                    TempControl.GetDataFromEqipment();

                if (Elemeter2 != null)
                {
                    Elemeter2.GetDataFromEqipment();
                    for (int i = 0; i < 5; i++)
                    {
                        E2OKWH[i] = Elemeter2.OUkwh[i] - SE2OKWH[i];
                        E2PKWH[i] = Elemeter2.PUkwh[i] - SE2PKWH[i];
                    }
                }
                //汇流柜电表
                if (Elemeter2H != null)
                {
                    Elemeter2H.GetDataFromEqipment();
                }

            }
            catch { }
        }
        //从设备上读取数据表2\3\4,5个传感器和空调
        public void GetDataFromEqipment()
        {
            try
            {
                GetBaseEquipment();
                //传感器 
                if (WaterLog1 != null)
                {
                    WaterLog1.GetDataFromEqipment();
                    if (WaterLog1.WaterlogData != 0)
                    {
                        lock (EMSError)
                        {
                            EMSError[1] &= 0x7FFF;
                            EMSError[1] |= 0x8000;
                        }
                    }
                    else
                        EMSError[1] &= 0x7FFF;
                    Fire.Waterlogging1 = WaterLog1.WaterlogData;
                }
                //GetBaseEquipment();
                if (WaterLog2 != null)
                {
                    WaterLog2.GetDataFromEqipment();
                    if (WaterLog2.WaterlogData != 0)
                    {
                        lock (EMSError)
                        {
                            EMSError[2] &= 0xFFFE;
                            EMSError[2] |= 0x0001;
                        }
                    }
                    else
                        EMSError[2] &= 0xFFFE;
                    Fire.Waterlogging2 = WaterLog2.WaterlogData;
                }
                GetBaseEquipment();
                if (co != null)
                {
                    co.GetDataFromEqipment();
                    Fire.Co = co.CoData;
                }
                GetBaseEquipment();
                if (Smoke != null)
                {
                    Smoke.GetDataFromEqipment();
                    Fire.Smoke = Smoke.SmokeData;
                }
                GetBaseEquipment();
                if (TempHum != null)
                {
                    TempHum.GetDataFromEqipment();
                    Fire.Temp = TempHum.TempData;
                    Fire.Humidity = TempHum.HumidityData;
                }
                GetBaseEquipment();
                //qiao 检验数值的边界值
                //电表3---辅助电表，计量设备本身耗电的电表
                if (Elemeter3 != null)
                {
                    Elemeter3.GetDataFromEqipment();
                    AuxiliaryKVA = Elemeter3.AKva;
                    AuxiliaryKWH[0] = Elemeter3.Akwh[0] - SAuxiliaryKWH[0];   //当天总辅助电量 =组合电能- 当天开始辅助用电量
                    AuxiliaryKWH[1] = Elemeter3.Akwh[1] - SAuxiliaryKWH[01];
                    AuxiliaryKWH[2] = Elemeter3.Akwh[2] - SAuxiliaryKWH[02];
                    AuxiliaryKWH[3] = Elemeter3.Akwh[03] - SAuxiliaryKWH[03];
                    AuxiliaryKWH[4] = Elemeter3.Akwh[04] - SAuxiliaryKWH[04];
                }
                //电表4---设备电表
                if (Elemeter4 != null)
                    Elemeter4.GetDataFromEqipment();               
                GetBaseEquipment();
                //UPS
                if (UPS != null)
                {
                    UPS.GetDataFromEqipment();
                }
                //液冷机
                if (LiquidCool != null)
                    LiquidCool.GetDataFromEqipment();
                //除湿机
                if(Dehumidifier != null)
                    Dehumidifier.GetDataFromEqipment();

            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }


        /// 读取表3信息 
        //Com3 readThread3 
        public void AutoReadDataCom3()
        {
            try
            {
                //实例化等待连接的线程
                Thread ClientRecThread = new Thread(ReadEquipmentDataBMS);
                ClientRecThread.IsBackground = true;
                ClientRecThread.Priority = ThreadPriority.Highest;
                ClientRecThread.Start();
                ClientRecThread.Name = "";
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());


            }
        }



        private void ReadEquipmentDataBMS()
        {
            while (true)
            {
                Thread.Sleep(1000);
                try
                {
                    if (BMS == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                    GetDataFromBMS();

                    //均衡操作
                    //StartStopBMSBala();
                    //Thread.Sleep(10);

                    //处理EMS故障
                    ushort sData = 0;
                    ushort sOldData = 0;
                    ushort sKey = 0;
                    int iData;

                    lock (EMSError)
                    {
                        //检查故障，并对比过去的故障 
                        for (int i = 0; i < 4; i++)
                        {
                            sData = EMSError[i];
                            sOldData = OldEMSError[i];
                            
                            if (sData != sOldData)
                            {
                                sOldData = (ushort)(sOldData ^ sData);
                                for (int j = 0; j < 16; j++)
                                {
                                    sKey = (ushort)(1 << j);//  1的二进制数左移J位
                                    iData = sOldData & sKey; //一次处理1个变化的数据位 ： iData > 0 说明第J位数据发生变化
                                    if ((iData > 0) && (ErrorClass.EMSErrorsPower[16 * i + j] > 0))
                                    {                                    
                                        BMS.RecodError("EMS", iot_code, 16 * i + j, ErrorClass.EMSErrorsPower[16 * i + j], ErrorClass.EMSErrors[16 * i + j], (sData & sKey) > 0);
                                    }
                                }
                            }
                            OldEMSError[i] = EMSError[i];
                        }
                    }
                }
                catch (Exception ex)
                {
                    frmMain.ShowDebugMSG("读取线程故障" + ex.ToString());
                }
            } 
        }

        //Com4 readThread4

        public void AutoReadDataCom4()
        {
            try
            {
                //实例化等待连接的线程
                Thread ClientRecThread = new Thread(ReadEquipmentDataPCS);
                ClientRecThread.IsBackground = true;
                ClientRecThread.Priority = ThreadPriority.Normal;
                ClientRecThread.Start();
                ClientRecThread.Name = "";
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }
        /// <summary>
        /// 读取pcs信息
        /// </summary>
        private void ReadEquipmentDataPCS()
        {
            int SelectVersion = 0;
            double PCSPower = 0;
            while (true)
            {
                try
                {
                    PCSPower = 0;
                    Thread.Sleep(2000);
                    //PCS 
                    if (PCSList.Count > 0)
                    {
                        for (int i = 0; i < PCSList.Count; i++)
                        {
                            PCSList[i].GetDataFromEqipment();
                            PCSPower += PCSList[i].allUkva;//主从模式设备整体PCS的功率
                        }
                        PCSKVA = Math.Round(PCSPower, 2);
                    }

                    //bms
                    try
                    {
                        if (BMS == null)
                        {
                            Thread.Sleep(1000);
                            continue;
                        }
                        Thread.Sleep(2000);
                        frmMain.Selffrm.AllEquipment.BMS.GetBaseInfo();
                    }
                    catch (Exception ex)
                    {
                        frmMain.ShowDebugMSG("读取线程故障" + ex.ToString());
                    }

                    //PCS的DSP2 11.27
                    //if (DSP2 != null)
                    //{
                        //DSP2.GetDataFromEqipment();
                    //}

                    //消防
                    FireFBGPIO();
                    //急停
                    EmergencyStopFBGPIO();

                }
                catch (Exception ex)
                {
                    frmMain.ShowDebugMSG("读取线程故障" + ex.ToString());
                }
            }
        }

        //Com5 readThread4
        //232->can
        //Com6 readThread4

        //Com7 readThread4
        //HDMI显示屏        

        //Com8 readThread4
        //级联
        public  void FireFBGPIO()
        {
            switch (frmSet.GPIO_Select_Mode)
            {
                case 0:
                    if (frmSet.GetGPIOState(0) == 2)
                    {
                        if (frmSet.GetGPIOState(0) == 2)
                        {
                            if (Fire.FireState == 0)
                            {
                                Fire.FireState = 1;
                                ExcPCSPowerOff();
                                //SysIO.SetGPIOState(11, 0);//黄色告警灯
                                lock (ErrorState)
                                {
                                    ErrorState[2] = true;
                                }
                                lock (EMSError)
                                {
                                    //EMSError[2] &= 0xFF7F;
                                    EMSError[2] |= 0x80;
                                }
                                //DBConnection.RecordLOG("系统", "消防系统", "消防触发");
                            }
                        }
                    }
                    else if (Fire.FireState == 1)
                    {
                        EMSError[2] &= 0xFF7F;
                        Fire.FireState = 0;
                    }
                    break;
                case 1:
                    if (frmSet.GetGPIOState(0) == 2)
                    {
                        if (frmSet.GetGPIOState(0) == 2)
                        {
                            if (Fire.FireState == 0)
                            {
                                Fire.FireState = 1;
                                ExcPCSPowerOff();
                                //SysIO.SetGPIOState(11, 0);//黄色告警灯
                                lock (ErrorState)
                                {
                                    ErrorState[2] = true;
                                }
                                lock (EMSError)
                                {
                                    //EMSError[2] &= 0xFF7F;
                                    EMSError[2] |= 0x80;
                                }
                                //DBConnection.RecordLOG("系统", "消防系统", "消防触发");
                            }
                        }
                    }
                    else if (Fire.FireState == 1)
                    {
                        EMSError[2] &= 0xFF7F;
                        Fire.FireState = 0;
                    }
                    break;
                case 2:
                    if (frmSet.GetGPIOState(0) == 2)
                    {
                        if (frmSet.GetGPIOState(0) == 2)
                        {
                            if (Fire.FireState == 0)
                            {
                                Fire.FireState = 1;
                                ExcPCSPowerOff();
                                //SysIO.SetGPIOState(11, 0);//黄色告警灯
                                lock (ErrorState)
                                {
                                    ErrorState[2] = true;
                                }
                                lock (EMSError)
                                {
                                    //EMSError[2] &= 0xFF7F;
                                    EMSError[2] |= 0x80;
                                }
                                //DBConnection.RecordLOG("系统", "消防系统", "消防触发");
                            }
                        }
                    }
                    else if (Fire.FireState == 1)
                    {
                        EMSError[2] &= 0xFF7F;
                        Fire.FireState = 0;
                    }
                    break;
            }
        }

        public void EmergencyStopFBGPIO() {

            switch (frmSet.GPIO_Select_Mode)
            { 
            case 0:                    
                if (frmSet.GetGPIOState(1) == 2)
                {
                    ExcPCSPowerOff();
                    lock (ErrorState)
                    {
                        ErrorState[2] = true;
                    }
                    lock (EMSError)
                    {
                        EMSError[2] &= 0xF7FF;
                        EMSError[2] |= 0x0800;
                    }
                }
                else
                {
                    EMSError[2] &= 0xF7FF;
                }                    
                break ;
            case 1:
                if (frmSet.GetGPIOState(1) == 2)
                {
                    ExcPCSPowerOff();
                    lock (ErrorState)
                    {
                        ErrorState[2] = true;
                    }
                    lock (EMSError)
                    {
                        EMSError[2] &= 0xF7FF;
                        EMSError[2] |= 0x0800;
                    }
                }
                else
                {
                    EMSError[2] &= 0xF7FF;
                }
                break;
            case 2:
                if (frmSet.GetGPIOState(1) == 2)
                {
                    ExcPCSPowerOff();
                    lock (ErrorState)
                    {
                        ErrorState[2] = true;
                    }
                    lock (EMSError)
                    {
                        EMSError[2] &= 0xF7FF;
                        EMSError[2] |= 0x0800;
                    }
                }
                else
                {
                    EMSError[2] &= 0xF7FF;
                }
                break;
            }


        }

        /// <summary>
        /// 检查BMS的故障信息并进行限流处理
        /// </summary>
        /// <param name="Errors"></param>
        public void CheckBMSWrror(ushort[] aErrors)
        {
            //BMS发生二级告警，如果ErrorState没有2级告警，修改ErrorState的2级标志。设置告警指示灯
            if ((aErrors[2]>0) && (!ErrorState[1]))
            {
                frmSet.BMS2warningGPIO(1);
                ErrorState[1] = true;
            }
            else if ((aErrors[2]==0) && (ErrorState[1]))
            {
                frmSet.BMS2warningGPIO(0);
                ErrorState[1] = false;
            }

            if (aErrors[0] + aErrors[3]+ aErrors[2] > 0)//发生二级告警
            {
                if (BMS.MaxChargeA == 0 && BMS.soc > 90) //双重确认为充电2级故障，修改充放阈值,记录需要进行均衡策略的单体ID
                {
                    if (frmMain.Selffrm.AllEquipment.UBmsPcsState != 0)
                    {
                        frmMain.Selffrm.AllEquipment.ExcPCSPowerOff();
                        lock (frmMain.Selffrm.AllEquipment)
                            frmMain.Selffrm.AllEquipment.UBmsPcsState = 0;

                        //记录单体电压 温度 电流
                        frmMain.Selffrm.AllEquipment.BMS.RecodChargeinform(2);
                        //7.25 BMS均衡策略提供排序
                        double[,] CellVs_ID = new double[frmMain.Selffrm.AllEquipment.BMS.CellVs.Length, 2];

                        for (int i = 0; i < frmMain.Selffrm.AllEquipment.BMS.CellVs.Length; i++)
                        {
                            CellVs_ID[i, 0] = frmMain.Selffrm.AllEquipment.BMS.CellVs[i];//单体电压
                            CellVs_ID[i, 1] = ((double)i +1); //单体ID ,根据BMS协议单体ID从1开始
                        }

                        //对单体数据进行冒泡排序
                        for (int i = 0; i < frmMain.Selffrm.AllEquipment.BMS.CellVs.Length -1; i++)
                        {
                            for (int j = 0; j < frmMain.Selffrm.AllEquipment.BMS.CellVs.Length -i -1; j++)
                            {
                                if (CellVs_ID[j, 0] > CellVs_ID[j+1, 0])
                                {
                                    //使用元组交换值
                                    (CellVs_ID[j+1, 0], CellVs_ID[j, 0])=(CellVs_ID[j, 0], CellVs_ID[j+1, 0]);
                                    (CellVs_ID[j+1, 1], CellVs_ID[j, 1])=(CellVs_ID[j, 1], CellVs_ID[j+1, 1]);
                                }

                            }
                        }

                        //抓取二级告警时最高最低单体电压差
                        frmMain.Selffrm.AllEquipment.Cell_Diff = CellVs_ID[frmMain.Selffrm.AllEquipment.BMS.CellVs.Length -1, 0] - CellVs_ID[0, 0];

                        //清空文件
                        if (frmMain.Selffrm.AllEquipment.balaCellID.Count != 0)
                            frmMain.Selffrm.AllEquipment.balaCellID.Clear();

                        //配置均衡电池文件地址
                        for (int k = 1; k < CellVs_ID.Length/2; k++)
                        {
                            if (CellVs_ID[k, 0] - CellVs_ID[0, 0] > CellV_Gap)
                            {
                                frmMain.Selffrm.AllEquipment.balaCellID.Add(CellVs_ID[k, 1]);
                            }
                        }
                        File.WriteAllText(frmSet.BalaPath, string.Empty);
                        using (StreamWriter writer = new StreamWriter(frmSet.BalaPath))
                        {
                            for (int m = 0; m < frmMain.Selffrm.AllEquipment.balaCellID.Count; m++)
                            {
                                writer.WriteLine(frmMain.Selffrm.AllEquipment.balaCellID[m]);
                            }
                        }
                        //判断均衡的效果 

                        for (int y = 0; y < CellVs_ID.Length/2; y++)
                        {
                            frmMain.Selffrm.AllEquipment.balaCellV.Add(CellVs_ID[y, 0]);
                        }
                        var u = frmMain.Selffrm.AllEquipment.balaCellV.Average();
                        var sum = frmMain.Selffrm.AllEquipment.balaCellV.Sum(p => Math.Pow(p - u, 2));
                        frmMain.Selffrm.AllEquipment.O_sigma = Math.Sqrt(sum / (frmMain.Selffrm.AllEquipment.balaCellV.Count-1)) * 1000;//标准差 * 1000倍展示
                    }
                }
                else if (BMS.MaxDischargeA == 0 && BMS.soc <10)
                {
                    if (frmMain.Selffrm.AllEquipment.OBmsPcsState != 0)
                    {
                        frmMain.Selffrm.AllEquipment.ExcPCSPowerOff();
                        lock (frmMain.Selffrm.AllEquipment)
                            frmMain.Selffrm.AllEquipment.OBmsPcsState = 0;

                        //记录单体电压 温度 电流
                        frmMain.Selffrm.AllEquipment.BMS.RecodChargeinform(5);
                    }
                }
            }
            else if (aErrors[1] > 0) //发生1级告警
            {
                //触发1级告警则减少pcs读取指令数量
                if (!frmMain.Selffrm.AllEquipment.ReduceReadPCS)
                {
                    frmMain.Selffrm.AllEquipment.ReduceReadPCS = true;
                }

                if (BMS.soc >= 50 && BMS.MaxChargeA < 140)//取消1级告警中soc告警的影响
                {
                    if (frmMain.Selffrm.AllEquipment.UBmsPcsState != frmSet.BMSwaValue/100)
                    {
                        lock (frmMain.Selffrm.AllEquipment)
                            frmMain.Selffrm.AllEquipment.UBmsPcsState = frmSet.BMSwaValue/100;
                        frmMain.Selffrm.AllEquipment.BMS.RecodChargeinform(1);
                    }
                }
                else if (BMS.soc < 50 && BMS.MaxDischargeA < 140)//取消1级告警中soc告警的影响
                {
                    if (frmMain.Selffrm.AllEquipment.OBmsPcsState != frmSet.BMSwaValue/100)
                    {
                        lock (frmMain.Selffrm.AllEquipment)
                            frmMain.Selffrm.AllEquipment.OBmsPcsState = frmSet.BMSwaValue/100;
                        frmMain.Selffrm.AllEquipment.BMS.RecodChargeinform(4);
                    }
                }
            }
        }

        //从BMS设备上读取数据
        public void GetDataFromBMS()
        {
            if (BMS==null)
                return;
            ushort[] Errors = { 0, 0, 0, 0, 0 };
            double BMSPower = 0;
            try
            {
                //string tempDate = DateTime.Now.ToString("yyyy-MM-d H:m:s");
                //BMS 
                BMS.GetDataFromEqipment();
                BMSSOC = BMS.soc;//显示的实时数据
                //BMS.GetErrorFromEquipment();
                if (20 < BMS.soc &&  BMS.soc < 80)
                {
                    frmMain.Selffrm.AllEquipment.SlowReadBMS = true;
                }
                else
                {
                    frmMain.Selffrm.AllEquipment.SlowReadBMS = false;
                }
                Errors[0] += BMS.Error[0];//故障
                Errors[1] += BMS.Error[1];//警告1
                Errors[2] += BMS.Error[2];//警告2
                Errors[3] += BMS.Error[3];//警告3
                Errors[4] += BMS.Error[4]; //通讯
                BMSPower += BMS.v * BMS.a / 1000;
                BMSKVA = Math.Round(BMSPower, 2);
                //检查BMS故障限流
                CheckBMSWrror(Errors);
                if (( Errors[1] + Errors[2] + Errors[3]) == 0)
                {
                    frmMain.Selffrm.AllEquipment.UBmsPcsState = 1;
                    frmMain.Selffrm.AllEquipment.OBmsPcsState = 1;
                    frmMain.Selffrm.AllEquipment.ReduceReadPCS = false;
                }
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }


        //2.21
        public bool ReadDoPUini() 
        {
            INIFile ConfigINI = new INIFile();
            try
            {
                //记录当天开始充电电量（positive 正向）
                //检查最大需量是否是本月的
                string NowMonth = ConfigINI.INIRead("Recode Date", "NowMonth", "0", DoPU);
                if (DateTime.Now.ToString("yyyy-MM") == NowMonth)
                {
                    Client_PUMdemand_Max = (double)Convert.ToDouble(ConfigINI.INIRead("Recode Date", "Client_PUMdemand_Max", "0", DoPU));
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());    
                return false;
            }
            finally
            {
                // ConfigINI.

            }
        }



        /// <summary>
        /// 读取当天记录数据，起始的尖峰平谷电能值
        /// </summary>
        public bool ReadDataInoneDayINI()
        {
            INIFile ConfigINI = new INIFile();
            try
            {
                //如果日期不符返回false，并赋值当前的值为起始数据
                rDate = ConfigINI.INIRead("Recode Date", "rDate", "", DofD);
                if (rDate == DateTime.Now.ToString("yyyy-MM-dd"))
                {
                    for (int i = 0; i < 5; i++)//总、尖峰=平谷
                    {
                        if (Elemeter2 == null)
                        {
                            //记录当天开始充电电量（positive 正向）
                            SE2PKWH[i] = (double)Convert.ToDouble(ConfigINI.INIRead("Recode Date", "SE2PKWH" + i.ToString()
                                , "0", DofD));
                            //记录当天开始放电电量（opposite反向，逆向）
                            SE2OKWH[i] = (double)Convert.ToDouble(ConfigINI.INIRead("Recode Date", "SE2OKWH" + i.ToString()
                                , "0", DofD));
                        }
                        else
                        {
                            //记录当天开始充电电量（positive 正向）
                            SE2PKWH[i] = (double)Convert.ToDouble(ConfigINI.INIRead("Recode Date", "SE2PKWH" + i.ToString()
                                , Elemeter2.PUkwh[i].ToString(), DofD));
                            //记录当天开始放电电量（opposite反向，逆向）
                            SE2OKWH[i] = (double)Convert.ToDouble(ConfigINI.INIRead("Recode Date", "SE2OKWH" + i.ToString()
                                , Elemeter2.OUkwh[i].ToString(), DofD));
                        }
                        if (Elemeter3 == null)
                        {
                            //记录当天开始辅助用电量
                            SAuxiliaryKWH[i] = (double)Convert.ToDouble(ConfigINI.INIRead("Recode Date", "SAuxiliaryKWH" + i.ToString()
                                , "0", DofD));
                        }
                        else
                        {
                            //记录当天开始辅助用电量
                            SAuxiliaryKWH[i] = (double)Convert.ToDouble(ConfigINI.INIRead("Recode Date", "SAuxiliaryKWH" + i.ToString()
                                , Elemeter3.Akwh[i].ToString(), DofD));
                        }
                    }
                    //记录PCS 开始的总充电量
                    // SPCSInKWH=(double) Convert.ToDouble(ConfigINI.INIRead("Recode Date", "SPCSInKWH", "0", DofD));
                    //记录PCS 开始的总放电量
                    // SPCSOutKWH=(double) Convert.ToDouble(ConfigINI.INIRead("Recode Date", "SPCSOutKWH", "0", DofD)); 
                    return true;
                }
                else
                {
                    if (Elemeter2 != null)
                    {
                        Elemeter2.GetDataFromEqipment();
                    }

                    if (Elemeter3 != null)
                    {
                        Elemeter3.GetDataFromEqipment();
                    }

                    WriteDataInoneDayINI(DateTime.Now.ToString("yyyy-MM-dd"));
                    return true;
                }
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
                return false;
            }
            finally
            {
                // ConfigINI.

            }
        }

        /// <summary>
        /// 将从机接受主机指令数据保存到INI
        /// </summary>
/*        public void WriteDataPCSCommandINI(string arDate , string PCS485, int aAddr)
        {
            INIFile ConfigINI = new INIFile();
            ConfigINI.INIWrite("Recode Date", "rDate", arDate, PCSfD);

            //ConfigINI.INIWrite("Recode Date", "PCS485" , PCS485, PCSfD);
            switch (aAddr)
            {
                case 0x6000://开关pcs
                    ConfigINI.INIWrite("Recode Date", "PCS开关", PCS485, PCSfD);
                    break;
                case 0x6001://计划功率 
                    ConfigINI.INIWrite("Recode Date", "PCS计划功率", PCS485, PCSfD);
                    break;
                case 0x6002://实际功率 
                    ConfigINI.INIWrite("Recode Date", "PCS实际功率", PCS485, PCSfD);
                    break;
                case 0x6003://充放电  
                    ConfigINI.INIWrite("Recode Date", "PCS充放电模式", PCS485, PCSfD);
                    break;
                case 0x6004://恒压横流恒功率、AC恒压
                    ConfigINI.INIWrite("Recode Date", "PCS状态", PCS485, PCSfD);
                    break;
                case 0x6005:
                    ConfigINI.INIWrite("Recode Date", "PCS执行主机指令", PCS485, PCSfD);
                    break;
                case 0x6006:
                    ConfigINI.INIWrite("Recode Date", "PCS充放电状态", PCS485, PCSfD);
                    break;
                case 0x6007:
                    ConfigINI.INIWrite("Recode Date", "PCS模式", PCS485, PCSfD);
                    break;
                case 0x6008:
                    ConfigINI.INIWrite("Recode Date", "PCS运行功率", PCS485, PCSfD);
                    break;
            }
        }*/

        //2.21
        public void WriteDoPUini()
        {
            INIFile ConfigINI = new INIFile();
            DateTime dateTime = DateTime.Now;
            //"Recode Date"=配置节点名称，"rDate"=键名，arDate=返回键值，DofD=路径
            
            //记录客户负载最大值
            ConfigINI.INIWrite("Recode Date", "Client_PUMdemand_Max" , frmMain.Selffrm.AllEquipment.Client_PUMdemand_Max.ToString(), DoPU);
            //记录时间
            ConfigINI.INIWrite("Recode Date", "rDate", dateTime.ToString("yyyy-MM-d H:m:s"), DoPU);
            //记录当月
            ConfigINI.INIWrite("Recode Date", "NowMonth", dateTime.ToString("yyyy-MM"), DoPU);

        }


        /// <summary>
        /// 将当天的起始尖峰平谷数据保存到INI，包含日期和具体电能值
        /// </summary>
        public void WriteDataInoneDayINI(string arDate)
        {
            INIFile ConfigINI = new INIFile();
            ConfigINI.INIWrite("Recode Date", "rDate", arDate, DofD);//"Recode Date"=配置节点名称，"rDate"=键名，arDate=返回键值，DofD=路径
            {
                for (int i = 0; i < 5; i++)//总\尖\峰\平\谷
                {
                    if (Elemeter2 != null)
                    {
                        SE2PKWH[i] = Elemeter2.PUkwh[i];
                        SE2OKWH[i] = Elemeter2.OUkwh[i];
                    }
                    if (Elemeter3 != null)
                    {
                        SAuxiliaryKWH[i] = Elemeter3.Akwh[i];
                    }

                     //记录当天开始（隔天时）充电电量（positive 正向）//隔天时，过去累计电量
                    ConfigINI.INIWrite("Recode Date", "SE2PKWH" + i.ToString(), SE2PKWH[i].ToString(), DofD);
                    //记录当天开始放电电量（opposite反向，逆向）
                    ConfigINI.INIWrite("Recode Date", "SE2OKWH" + i.ToString(), SE2OKWH[i].ToString(), DofD);
                    //记录当天开始辅助用电量
                    ConfigINI.INIWrite("Recode Date", "SAuxiliaryKWH" + i.ToString(), SAuxiliaryKWH[i].ToString(), DofD);
                }
                //记录PCS 开始的总充电量
                // ConfigINI.INIWrite("Recode Date", "SPCSInKWH"  , SPCSInKWH.ToString(), DofD); 
                //记录PCS 开始的总放电量
                // ConfigINI.INIWrite("Recode Date", "SPCSOutKWH" , SPCSOutKWH.ToString(), DofD); 
            }
        }

        public bool CalculatePower()
        {
            if (Elemeter2 == null)
                return false;

            double dProfit = 0;
            //计算尖峰平谷数据的当天充放电量---电表2为计量表
            for (int i = 0; i < 5; i++)
            {
                E2PKWH[i] = Elemeter2.PUkwh[i] - SE2PKWH[i]; //当前表值--当天开始的值
                E2OKWH[i] = Elemeter2.OUkwh[i] - SE2OKWH[i];
                Profit2Cloud.DaliyE2PKWH[i] = E2PKWH[i];
                Profit2Cloud.DaliyE2OKWH[i] = E2OKWH[i];
                if (Elemeter3 != null)
                {
                    AuxiliaryKWH[i] = Elemeter3.Akwh[i] - SAuxiliaryKWH[i]; //辅助电表当天用电量  
                    Profit2Cloud.DaliyAuxiliaryKWH[i] = AuxiliaryKWH[i];
                }
                //计算成本和价格
                dProfit += E2OKWH[i] * frmSet.Prices[1, i] - E2PKWH[i] * frmSet.Prices[0, i];//qiao 辅电接入计量表内 - AuxiliaryKWH[i] * frmSet.Prices[0, i];
                Profit2Cloud.DaliyPrice[i] = frmSet.Prices[0, i];
            }
            //返回今日省的钱数
            Profit = dProfit / 100;//按分
            Profit2Cloud.DaliyProfit = Profit;
            return true;
        }

        //日期更换时候保存当天数据
        public void SaveDataInoneDay(string astrDate)
        {
            if (Elemeter2 == null)
                return;
            lock (Elemeter2)
            {
                if (Elemeter3 == null)
                    return;

                lock (Elemeter3)
                {
                    //计算尖峰平谷数据的当天充放电量
                    if (astrDate != "")
                    {
                        Profit2Cloud.time = Convert.ToDateTime(astrDate + " 23:59:59");
                        CalculatePower();
                    }
                    //更新当天的其实电表电能值
                    for (int i = 0; i < 5; i++)
                    {
                        SE2PKWH[i] = Elemeter2.PUkwh[i]; //当前表值--当天开始的值
                        SE2OKWH[i] = Elemeter2.OUkwh[i];
                        if (Elemeter3 != null)
                            SAuxiliaryKWH[i] = Elemeter3.Akwh[i]; //辅助电表当天用电量
                    }
                }
            }
            //
            if (astrDate == "")
                return;

            try
            {
                string strData = "";
                for (int i = 1; i < 5; i++)
                {
                    strData += "','" + E2OKWH[i].ToString() + "','" + frmSet.Prices[1, i].ToString() + "','"
                            + E2PKWH[i].ToString() + "','" + AuxiliaryKWH[i].ToString()
                            + "','" + frmSet.Prices[0, i].ToString();
                } 
                //保存到数据库   
                DBConnection.ExecSQL("insert profit (rTime, profit,inPower,outPower,auxkwhAll,"
                + "out1kwh,out1Price,in1kwh,auxkwh1,in1Price,out2kwh,out2Price,in2kwh,auxkwh2,in2Price,"
                + "out3kwh,out3Price,in3kwh,auxkwh3,in3Price,out4kwh,out4Price,in4kwh,auxkwh4,in4Price"
               + ")value('" + astrDate + "','" + Profit.ToString() + "','"
               + E2OKWH[0].ToString() + "','" + E2PKWH[0].ToString() + "','" + AuxiliaryKWH[0].ToString() + strData + "')");
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }

    }



}
