using EMS;
using log4net;
using System;
using System.Diagnostics;
using System.IO.Ports;

namespace Modbus
{
    public class modbus485
    {
        public SerialPort sp = null;//= new SerialPort();
        public string modbusStatus;
        public AllEquipmentClass ParentEquipment;

        private static ILog log = LogManager.GetLogger("modbus485");

        private void Get4GData(byte[] aMessage)
        {
            sp.DiscardOutBuffer();
            sp.DiscardInBuffer();
            sp.Write(aMessage, 0, aMessage.Length);

        }
        public void Restart4G()
        {
            byte[] message = new byte[14] { 0x41, 0x54, 0x2B, 0x43, 0x46, 0x55, 0x4E, 0x3D, 0x31, 0x2C, 0x31, 0x0D, 0x0D, 0x0A };
            Get4GData(message);
        }




        #region Constructor / Deconstructor
        public modbus485()
        {
            // byte[] response = new byte[100];
        }
        ~modbus485()
        {
        }
        #endregion

        //支持同485上多个设备，查找同485地址使用情况
        private SerialPort Checksp(string portName)
        {
            if (sp != null)
                return sp;
            SerialPort tempSP;
            if (ParentEquipment.modbus485List.Count <= 0)//((ParentEquipment.modbus485List == null) ||)
            {
                tempSP = new SerialPort();
                ParentEquipment.modbus485List.Add(tempSP);
                return tempSP;
            }
            else
            {
                for (int i = 0; i < ParentEquipment.modbus485List.Count; i++)
                {
                    if (ParentEquipment.modbus485List[i] == null)
                        continue;
                    if (ParentEquipment.modbus485List[i].PortName == portName)
                    {
                        return ParentEquipment.modbus485List[i];
                    }

                }
                tempSP = new SerialPort();
                ParentEquipment.modbus485List.Add(tempSP);
                return tempSP;
            }
        }

        /// <summary>
        /// 打开串口
        /// </summary>
        /// <param name="portName"></param>
        /// <param name="baudRate"></param>
        /// <param name="databits"></param>
        /// <param name="parity"></param>
        /// <param name="stopBits"></param>
        /// <returns></returns>
        #region //Open / Close Procedures      
        public bool OpenEMS(string portName, int baudRate, int databits, Parity parity, StopBits stopBits)
        {
            //Ensure port isn't already opened:
            sp = new SerialPort();
            if (!sp.IsOpen)
            {
                //Assign desired settings to the serial port:
                sp.PortName = portName;
                sp.BaudRate = baudRate;
                sp.DataBits = databits;
                sp.Parity = parity;
                sp.StopBits = stopBits;
                //These timeouts are default and cannot be editted through the class at this point:
                sp.ReadTimeout = 1000;
                sp.WriteTimeout = 1000;

                try
                {
                    sp.Open();
                }
                catch (Exception ex)
                {
                    frmMain.ShowDebugMSG(ex.ToString());
                    return false;
                }
                modbusStatus = portName + " opened successfully";
                return true;
            }
            else
            {
                modbusStatus = portName + " already opened";
                return false;
            }
        }
        public bool Open(string portName, int baudRate, int databits, Parity parity, StopBits stopBits)
        {
            sp = Checksp(portName);

            //Ensure port isn't already opened:
            if (!sp.IsOpen)
            {
                //Assign desired settings to the serial port:
                sp.PortName = portName;
                sp.BaudRate = baudRate;
                sp.DataBits = databits;
                sp.Parity = parity;
                sp.StopBits = stopBits;
                //These timeouts are default and cannot be editted through the class at this point:
                sp.ReadTimeout = 1000;
                sp.WriteTimeout = 1000;

                try
                {
                    sp.Open();
                }
                catch (Exception ex)
                {
                    frmMain.ShowDebugMSG(ex.ToString());
                    return false;
                }
                modbusStatus = portName + " opened successfully";
                return true;
            }
            else
            {
                modbusStatus = portName + " already opened";
                return false;
            }
        }

        /// <summary>
        /// 关闭串口
        /// </summary>
        /// <returns></returns>
        public bool Close()
        {
            //Ensure port is opened before attempting to close:
            if (sp.IsOpen)
            {
                try
                {
                    sp.Close();
                }
                catch (Exception err)
                {
                    modbusStatus = "Error closing " + sp.PortName + ": " + err.Message;
                    return false;
                }
                modbusStatus = sp.PortName + " closed successfully";
                return true;
            }
            else
            {
                modbusStatus = sp.PortName + " is not open";
                return false;
            }
        }
        #endregion


        /// <summary>
        /// 读取返回数据
        /// </summary>
        /// <param name="response"></param>
        #region //Get Response
        private bool GetResponse(ref byte[] response)
        {
            bool bResult = false;
            try
            {
                int i = 0;
                while (sp.BytesToRead >= 0)
                {
                    response[i] = (byte)(sp.ReadByte());
                    i++;
                    if (i >= response.Length)
                        break;
                }
                //sp.Read(response, 0, response.Length);
                bResult = true;
            }
            catch //(Exception ex)
            {
                bResult = false;
                //frmMain.ShowDebugMSG(ex.ToString());
            }
            return bResult;
        }
        #endregion

        private bool GetComFreeData(byte[] aMessage, ref byte[] aResponse)
        {
            bool bResult = false;
            for (int i = 0; i < 10; i++) //qiao
            {
                //Clear in/out buffers:
                sp.DiscardOutBuffer();
                sp.DiscardInBuffer();
                sp.Write(aMessage, 0, aMessage.Length);
                if (GetResponse(ref aResponse))
                {
                    bResult = true;
                    break;
                }
                else
                {
                    bResult = false;
                }
            }
            return bResult;
        }


        private bool GetComDada(byte[] aMessage, ref byte[] aResponse, bool bLocksp = true)
        {
            if ((sp == null) || (!sp.IsOpen))
                return false;
            bool bResult = false;
            if (bLocksp)
            {
                lock (sp)
                {
                    bResult= GetComFreeData(aMessage, ref aResponse);
                }
            }
            else
            {
                bResult = GetComFreeData(aMessage, ref aResponse);
            }
            if (bResult)
                return bResult;
            else
            {
                // DBConnection.RecordLOG("通讯异常", "反应超时", "无法判断具体设备");
                return bResult;
            }

        }


        /// <summary>
        /// 1\2读取数据bit
        /// </summary>
        /// <param name="aAddress"></param>
        /// <param name="CommandType"></param>
        /// <param name="aRegStart"></param>
        /// <param name="aRegLength"></param>
        /// <param name="aResponse"></param>
        /// <returns></returns>
        #region //read 1 read 1 byte 
        private bool Read1Response(byte aAddress, byte CommandType, ushort aRegStart, ushort aRegLength, ref byte[] aResponse)
        {
            if ((sp == null) || (!sp.IsOpen))
            {
                modbusStatus = "Serial port not open";
                return false;
            }
            //[11][01][00][13][00][25][CRC低][CRC高]
            //Function 1/2 request is always 8 bytes:     // byte[] message = new byte[8];//8
            //Build outgoing modbus message:
            byte[] message = ModbusBase.BuildMSG3(aAddress, CommandType, aRegStart, aRegLength);


            //Function 3 response buffer:
            //[11][01][05][CD][6B][B2][0E][1B] [CRC高] [CRC低]
            byte[] response = new byte[5 + (int)Math.Ceiling(aRegLength / 8.0)];

            //Send modbus message to Serial Port:                
            if (!GetComDada(message, ref response))
                return false;
            //Evaluate message:
            if (ModbusBase.CheckResponse(response))
            {
                aResponse = response;
                modbusStatus = "Read successful";
                return true;
            }
            else
            {
                modbusStatus = "CRC error";
                return false;
            }
        }
        #endregion


        #region //read 3 Multiple Registers
        private bool Read3Response(byte aAddress, byte CommandType, ushort aRegStart, ushort aRegLength, ref byte[] aResponse)
        {
            if ((sp == null) || (!sp.IsOpen))
            {
                modbusStatus = "Serial port not open";
                return false;
            }

            //[11][03][00][6B][00][03] [CRC高][CRC低]
            //Function 3 request is always 8 bytes:
            // byte[] message = new byte[8];//8
            //Function 3 response buffer:
            byte[] response = new byte[5 + 2 * aRegLength]; //5: 地址+功能码+字节数+crc
            //Back 3 //[11][03][06][02][2B][00][00][00][64] [CRC高] [CRC低]
            //Build outgoing modbus message:
            byte[] message = ModbusBase.BuildMSG3(aAddress, CommandType, aRegStart, aRegLength);

            //Send modbus message to Serial Port:
            if (!GetComDada(message, ref response))
                return false;

            //11.16捕捉PCS故障特加test
            if (aAddress == 1 && aRegStart == Convert.ToInt32("001B", 16) && aRegLength == Convert.ToInt32("0004", 16))
            {
                frmMain.Selffrm.AllEquipment.PCSList[0].WarnMessage = response;
            }

            //Evaluate message:
            if (ModbusBase.CheckResponse(response))
            {
                aResponse = response;
                modbusStatus = "Read successful";
                return true;
            }
            else
            {
                modbusStatus = "CRC error";
                return false;
            }
        }
        #endregion


        #region //read 5 read 1 byte data
        private bool Read5Response(byte aAddress, byte CommandType, ushort aRegAddr, bool aData, ref byte[] aResponse, bool bLocksp)
        {
            if ((sp == null) || (!sp.IsOpen))
            {
                modbusStatus = "Serial port not open";
                return false;
            }

            //[11][05][00][13][FF][00][CRC低][CRC高]
            //Function 5 request is always 8 bytes:     // byte[] message = new byte[8];//8
            //Build outgoing modbus message:
            byte[] message = ModbusBase.BuildMSG5(aAddress, CommandType, aRegAddr, aData);

            //Function 5 response buffer:
            //[11][05][00][13][FF][00][CRC低][CRC高]
            byte[] response = new byte[8];

            //Send modbus message to Serial Port
            if (!GetComDada(message, ref response, bLocksp))
                return false;
            //Evaluate message:
            if (ModbusBase.CheckResponse(response))
            {
                aResponse = response;
                modbusStatus = "Read successful";
                return true;
            }
            else
            {
                modbusStatus = "CRC error";
                return false;
            }
        }

        #endregion

        /// <summary>
        /// 6写一个寄存器
        /// </summary>
        /// <param name="aAddress"></param>
        /// <param name="CommandType"></param>
        /// <param name="aRegAddr"></param>
        /// <param name="aData"></param>
        /// <param name="aResponse"></param>
        /// <returns></returns>
        #region //write 6 write 1 short(for read 3 ) data
        private bool Read6Response(byte aAddress, byte CommandType, ushort aRegAddr, byte[] aData, ref byte[] aResponse, bool bLocksp)
        {
            if ((sp == null) || (!sp.IsOpen))
            {
                modbusStatus = "Serial port not open";
                return false;
            }
            //[11][06][00][01][00][03] [CRC高] [CRC低]
            //Function 6 request is always 8 bytes:     // byte[] message = new byte[8]; 
            //Build outgoing modbus message:
            //设备响应：如果成功把计算机发送的命令原样返回，否则不响应。
            byte[] message = ModbusBase.BuildMSG6(aAddress, CommandType, aRegAddr, aData);
            //Function 6 response buffer:
            //[11][06][00][01][00][03] [CRC高] [CRC低]
            byte[] response = new byte[8];

            //Send modbus message to Serial Port: 
            if (!GetComDada(message, ref response, bLocksp))
                return false;
            //Evaluate message:
            if (ModbusBase.CheckResponse(response))
            {
                aResponse = response;
                modbusStatus = "Read successful";
                return true;
            }
            else
            {
                modbusStatus = "CRC error";
                return false;
            }
        }
        #endregion

        //
        #region //write 6 write 1 short(for read 3 ) data
        /// <summary>
        /// 
        /// </summary>
        /// <param name="aAddress"></param>
        /// <param name="CommandType"></param>
        /// <param name="aRegAddr"></param>
        /// <param name="aData"></param>
        /// <param name="aResponse"></param>
        /// <returns></returns>
        private bool Read6Response(byte aAddress, byte CommandType, ushort aRegAddr, ushort aData, ref byte[] aResponse, bool bLocksp)
        {

            /*            string Info = new StackTrace().ToString();
                        log.Info(Info);*/


            if ((sp == null) || (!sp.IsOpen))
            {
                modbusStatus = "Serial port not open";
                return false;
            }

            //Clear in/out buffers:

            //[11][06][00][01][00][03] [CRC高] [CRC低]
            //Function 6 request is always 8 bytes:     // byte[] message = new byte[8]; 
            //Build outgoing modbus message:
            //设备响应：如果成功把计算机发送的命令原样返回，否则不响应。
            byte[] message = ModbusBase.BuildMSG6(aAddress, CommandType, aRegAddr, aData);

            //验证消息
            /*            string hexString = BitConverter.ToString(message);
                        log.Info("发送Read6Response消息：" + hexString);*/
            //Function 6 response buffer:
            //[11][06][00][01][00][03] [CRC高] [CRC低]
            byte[] response = new byte[8];

            //Send modbus message to Serial Port:
            if (!GetComDada(message, ref response))
                return false;

            //log.Info("接收返回报文：" + response);
            //Evaluate message:
            if (ModbusBase.CheckResponse(response))
            {
                aResponse = response;
                modbusStatus = "Read successful";
                return true;
            }
            else
            {
                modbusStatus = "CRC error";
                return false;
            }
        }
        #endregion

        #region Function 读取数据，返回数据转换为16进制- Read Registers 
        public bool SendstrMSG(byte aAddress, byte bComType, ushort aRegStart, ushort aRegLebgth, ref string strBack)//short[] values
        {
            byte[] response = null;
            if (Read3Response(aAddress, bComType, aRegStart, aRegLebgth, ref response))
            {
                modbusStatus = "CRC error";
                return false;
            }
            //返回数据转换
            strBack = "";
            for (int i = 0; i < response.Length; i++)
            {
                strBack += response[i].ToString("x2");
            }
            modbusStatus = "Read successful";
            return true;
        }
        #endregion

        //#region Function 读取数据，返回数据转换为16进制- Read Registers
        ////
        //public bool SendstrMSG(byte aAddress, byte bComType, ushort aRegStart, ushort aRegLebgth, ref string strBack)//short[] values
        //{
        //    byte[] response = null; 
        //    strBack = "";
        //    if (Read3Response(aAddress, bComType, aRegStart, aRegLebgth, ref response))
        //    {
        //        modbusStatus = "CRC error";

        //        return false;
        //    }
        //    //返回数据转换 
        //    for (int i = 0; i < response.Length; i++)
        //    {
        //        strBack += response[i].ToString("x2");
        //    }
        //    modbusStatus = "Read successful";
        //    return true;
        //}
        //#endregion

        #region Function sendMSG ,func3 
        /// <summary>
        /// 返回的数据
        /// </summary>
        /// <param name="aAddress"></param>
        /// <param name="CommandType"></param>
        /// <param name="aRegStart"></param>
        /// <param name="aRegLength"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public bool Send3MSG(byte aAddress, byte CommandType, ushort aRegStart, ushort aRegLength, ref ushort[] values)
        {
            byte[] response = null;
            if (!Read3Response(aAddress, CommandType, aRegStart, aRegLength, ref response))
            {
                modbusStatus = "CRC error";
                return false;
            }
            //返回数据转换
            values = new ushort[aRegLength];
            //Return requested register values:
            for (int i = 0; i < (response.Length - 5) / 2; i++) //5 ：设备地址1字节+功能码1字节+字节数1字节+CRC2字节 = 5字节 , /2 :2个字节作为1个ushort类型的vlaue
            {
                values[i] = response[2 * i + 3];//modbus response从第4个字节开始是寄存器值
                values[i] <<= 8;
                values[i] += response[2 * i + 4];
            }
            modbusStatus = "Read successful";
            return true;
        }
        #endregion

        #region Function sendMSG ,func3 
        /// <summary>
        /// 返回数组类型为字节型
        /// </summary>
        /// <param name="aAddress"></param>
        /// <param name="CommandType"></param>
        /// <param name="aRegStart"></param>
        /// <param name="aRegLength"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public bool Send3MSG(byte aAddress, byte CommandType, ushort aRegStart, ushort aRegLength, ref byte[] values)
        {
            byte[] response = null;
            if (!Read3Response(aAddress, CommandType, aRegStart, aRegLength, ref response))
            {
                modbusStatus = "CRC error";
                return false;
            }
            //返回数据转换
            values = new byte[aRegLength];
            int DataLen = response[2];
            //Return requested register values:
            Array.Copy(response, 3, values, 0, DataLen);
            modbusStatus = "Read successful";
            return true;
        }
        #endregion


        /// <summary>
        /// modbus获取数据ushort值
        /// </summary>
        /// <param name="aID">设备ID</param>
        /// <param name="CommandType">命令类型，如03</param>
        /// <param name="aRegStart">开始地址</param>
        /// <param name="aRegLength">长度，1是一个short，其他无效</param>
        /// <param name="aResult">返回的数据，short</param>  
        /// <returns>返回值为true表示获取值1;反之为fasle</returns>     
        /// 
        public bool GetUShort(byte aID, byte CommandType, ushort aRegStart, ushort aRegLength, ref ushort aResult)
        {
            ushort[] ResultData = null;//=new byte[100];
            if (Send3MSG(aID, CommandType, aRegStart, aRegLength, ref ResultData))
            {
                if (ResultData.Length > 0)
                    aResult = (UInt16)ResultData[0];
                return true;
            }
            else
                return false;
        }

        public bool GetShort(byte aID, byte CommandType, ushort aRegStart, ushort aRegLength, ref short aResult)
        {
            ushort[] ResultData = null;//=new byte[100];
            if (Send3MSG(aID, CommandType, aRegStart, aRegLength, ref ResultData))
            {
                if (ResultData.Length > 0)
                    aResult = (Int16)ResultData[0];
                return true;
            }
            else
                return false;
        }

        //获取一个无符号浮点
        public bool GetUFloat(byte aID, byte CommandType, ushort aRegStart, ushort aRegLength, ref double aResult,
                       double Coefficient, bool aSmallEnd)
        {
            string itemp;
            ushort[] ResultData = null;//=new byte[100];
            if (Send3MSG(aID, CommandType, aRegStart, aRegLength, ref ResultData))
            {
                if (ResultData.Length > 1)
                {
                    if (aSmallEnd)
                        itemp = "0X" + ResultData[1].ToString("X4") + ResultData[0].ToString("X4");
                    else
                        itemp = "0X" + ResultData[0].ToString("X4") + ResultData[1].ToString("X4");
                    aResult = Convert.ToUInt32(itemp, 16) * Coefficient;
                }
                else if (ResultData.Length == 1)
                    aResult = (UInt16)ResultData[0] * Coefficient;

                return true;
            }
            else
                return false;
        }

        //获取一个有浮点
        public bool GetFloat(byte aID, byte CommandType, ushort aRegStart, ushort aRegLength, ref double aResult,
                       double Coefficient, bool aSmallEnd)
        {
            Int32 iTemp = 0;
            ushort[] ResultData = null;//=new byte[100];
            if (Send3MSG(aID, CommandType, aRegStart, aRegLength, ref ResultData))
            {
                if (ResultData.Length > 1)
                {
                    if (aSmallEnd)
                        iTemp = Convert.ToInt32("0x" + ResultData[1].ToString("x4") + ResultData[0].ToString("x4"), 16);
                    else
                        iTemp = Convert.ToInt32("0x" + ResultData[0].ToString("x4") + ResultData[1].ToString("x4"), 16);
                }
                else if (ResultData.Length == 1)
                    iTemp = ((Int16)(ResultData[0]));
                aResult = iTemp * Coefficient;
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// modbus获取数据long值
        /// </summary>
        /// <param name="aID">设备ID</param>
        /// <param name="CommandType">命令类型，如03</param>
        /// <param name="aRegStart">开始地址</param>
        /// <param name="aRegLength">长度，1是一个short，2为int32</param>
        /// <param name="aResult">返回的数据，short</param>  
        /// <returns>返回值为true表示获取值1;反之为fasle</returns>     
        public bool Get1Int32(byte aID, byte CommandType, ushort aRegStart, ushort aRegLength,
            ref Int32 aResult, bool aSmallEnd)
        {
            ushort[] ResultData = null;//=new byte[100];
            if (Send3MSG(aID, CommandType, aRegStart, aRegLength, ref ResultData))
            {
                if (ResultData.Length > 1)
                {
                    if (aSmallEnd)
                        aResult = Convert.ToInt32("0x" + ResultData[1].ToString("x4") + ResultData[0].ToString("x4"), 16);
                    else
                        aResult = Convert.ToInt32("0x" + ResultData[0].ToString("x4") + ResultData[1].ToString("x4"), 16);
                }
                else if (ResultData.Length > 0)
                    aResult = (Int16)ResultData[0];
                return true;
            }
            else
                return false;
        }
        public bool Get1UInt32(byte aID, byte CommandType, ushort aRegStart, ushort aRegLength,
            ref UInt32 aResult, bool aSmallEnd)
        {
            ushort[] ResultData = null;//=new byte[100];
            if (Send3MSG(aID, CommandType, aRegStart, aRegLength, ref ResultData))
            {
                if (ResultData.Length > 1)
                {
                    if (aSmallEnd)
                        aResult = Convert.ToUInt32("0x" + ResultData[1].ToString("x4") + ResultData[0].ToString("x4"), 16);//ToString("X4"):10进制转16进制时进行默认补0来凑够位数,  X：代表16进制  4：代表每次的数据位数，当位数不足时自动补0:为了short数据拼接Float时固定4位数据位
                    else
                        aResult = Convert.ToUInt32("0x" + ResultData[0].ToString("x4") + ResultData[1].ToString("x4"), 16);
                }
                else if (ResultData.Length > 0)
                    aResult = (UInt16)ResultData[0];
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// modbus获取数据string值
        /// </summary>
        /// <param name="aID">设备ID</param>
        /// <param name="CommandType">命令类型，如03</param>
        /// <param name="aRegStart">开始地址</param>
        /// <param name="aRegLength">长度</param>
        /// <param name="aResult">返回的数据，string</param>  
        /// <returns>返回值为true表示获取值1;反之为fasle</returns>     
        public bool GetString(byte aID, byte CommandType, ushort aRegStart, ushort aRegLength, ref string aResult, bool aIxX2 = true)
        {
            ushort[] ResultData = null;//=new byte[100];
            aResult = "";
            if (Send3MSG(aID, CommandType, aRegStart, aRegLength, ref ResultData))
            {
                byte[] tembytes = new byte[ResultData.Length * 2];
                for (int i = 0; i < ResultData.Length; i++)
                {
                    if (aIxX2)
                        aResult += ((byte)(ResultData[i] >> 8)).ToString("X2") + ((byte)(ResultData[i])).ToString("X2");
                    else
                        aResult += (char)(ResultData[i] >> 8) + (byte)(ResultData[i]);
                }
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// modbus获取数据byte数组
        /// </summary>
        /// <param name="aID">设备ID</param>
        /// <param name="CommandType">命令类型，如01</param>
        /// <param name="aRegStart">开始地址</param>
        /// <param name="aRegLength">长度</param>
        /// <param name="aResult">返回的数据，byte数组</param>  
        /// <returns>返回值为true表示获取值1;反之为fasle</returns>     
        public bool GetBytes(byte aID, byte CommandType, ushort aRegStart, ushort aRegLength, ref byte[] aResult)
        {
            ushort[] ResultData = null;//=new byte[100];

            if (Send3MSG(aID, CommandType, aRegStart, aRegLength, ref ResultData))
            {
                byte[] tembytes = new byte[ResultData.Length * 2];
                for (int i = 0; i < ResultData.Length; i++)
                {
                    tembytes[i] = (byte)(ResultData[i] >> 8);
                    tembytes[i + 1] = (byte)(ResultData[i]);
                }
                aResult = tembytes;
                return true;
            }
            else
                return false;
        }


        //1\2
        //发送[11][01][00][13][00][25][CRC低][CRC高]
        //回复[11][01][05][CD][6B][B2][0E][1B] [CRC高] [CRC低]          
        #region Function send1MSG ,func1\2
        public bool Send1MSG(byte aAddress, byte CommandType, ushort aRegStart, ushort aRegLength,
            ref byte[] values)
        {
            byte[] response = null;
            if (!Read1Response(aAddress, CommandType, aRegStart, aRegLength, ref response))
            {
                modbusStatus = "W1 error";
                return false;
            }
            //返回数据转换
            int BackDataLen = response[2];
            values = new byte[BackDataLen];
            //Return requested register values:
            Array.Copy(response, 3, values, 0, BackDataLen);
            modbusStatus = "Read successful";
            return true;
        }
        #endregion

        //5设置[11][05][00][AC][FF][00][CRC高][CRC低]
        #region Function send5 
        public bool Send5MSG(byte aAddress, byte CommandType, ushort aRegStart, bool aData, bool bLocksp)//, ref byte[] values)
        {
            byte[] response = null;
            if (!Read5Response(aAddress, CommandType, aRegStart, aData, ref response, bLocksp))
            {
                modbusStatus = "w5 error";
                return false;
            }
            //返回数据转换，成功元数据返回，失败将不反悔
            int BackDataLen = response[2];
            //values = new byte[BackDataLen];
            //Return requested register values:
            //Array.Copy(response, 3, values, 0, BackDataLen);//2
            modbusStatus = "Read successful";
            return true;
        }
        #endregion

        //[11][06][00][01][00][03] [CRC高] [CRC低]
        #region Function send6MSG 
        public bool Send6MSG(byte aAddress, byte CommandType, ushort aRegStart, ushort aData, bool bLocksp)//, ref byte[] values)
        {
            int count = 3;
            byte[] response = null;
            /*            if (!Read6Response(aAddress, CommandType, aRegStart, aData, ref response, bLocksp))
                        {
                            modbusStatus = "w6 error";
                            return false;
                        }*/

            while (!Read6Response(aAddress, CommandType, aRegStart, aData, ref response, bLocksp))
            {
                if (count == 0)
                {
                    modbusStatus = "w6 error";
                    return false;
                }
                count--;
            }
            //[11][05][00][AC][FF][00][CRC高][CRC低]
            //返回数据转换，成功元数据返回，失败将不反悔
            int BackDataLen = response[2];
            //values = new byte[BackDataLen];
            //Return requested register values:
            // Array.Copy(response, 4, values, 0, 2);


            modbusStatus = "write successful";
            return true;
        }
        #endregion

        //[11][06][00][01][00][03] [CRC高] [CRC低]
        #region Function send6MSG 
        public bool Send6MSG(byte aAddress, byte CommandType, ushort aRegStart, byte[] aData, bool bLocksp)//, ref byte[] values)
        {
            byte[] response = null;
            if (!Read6Response(aAddress, CommandType, aRegStart, aData, ref response, bLocksp))
            {
                modbusStatus = "w6 error";
                return false;
            }
            //[11][05][00][AC][FF][00][CRC高][CRC低]
            //返回数据转换，成功元数据返回，失败将不反悔
            int BackDataLen = response[2];
            //values = new byte[BackDataLen];
            //Return requested register values:
            // Array.Copy(response, 4, values, 0, 2);
            modbusStatus = "write successful";
            return true;
        }
        #endregion

        #region Function 16 - Write Multiple Registers
        public bool Send16MSG(byte aAddress, byte aCommandType, ushort aRegStart, ushort aRegLength, short[] values, bool bLocksp)
        {
            //CommandType=16;
            //Ensure port is open:
            if ((sp == null) || (!sp.IsOpen))
            {
                modbusStatus = "Serial port not open";
                return false;
            }


            //Function 16 response is fixed at 8 bytes
            byte[] response = new byte[8];
            //Build outgoing message:
            byte[] message = ModbusBase.BuildMSG16(aAddress, aCommandType, aRegStart, aRegLength, values);

            //Send Modbus message to Serial Port:
            if (!GetComDada(message, ref response, bLocksp))
            {
                return false;
            }

            //Evaluate message:
            if (ModbusBase.CheckResponse(response))
            {
                modbusStatus = "Write successful";
                return true;
            }
            else
            {
                modbusStatus = "CRC error";
                return false;
            }
        }
        #endregion

        #region Function 16 - Write Multiple Registers
        public bool Send16MSG(byte aAddress, byte aCommandType, ushort aRegStart, ushort aRegLength, byte[] values, bool bLocksp)
        {
            if ((sp == null) || (!sp.IsOpen))
            {
                modbusStatus = "Serial port not open";
                return false;
            }

            //Function 16 response is fixed at 8 bytes
            byte[] response = new byte[8];
            //Build outgoing message:
            byte[] message = ModbusBase.BuildMSG16(aAddress, aCommandType, aRegStart, aRegLength, values);

            if (!GetComDada(message, ref response, bLocksp))
                return false;
            //Evaluate message:
            if (ModbusBase.CheckResponse(response))
            {
                modbusStatus = "Write successful";
                return true;
            }
            else
            {
                modbusStatus = "CRC error";
                return false;
            }
        }
        #endregion






    }
}
