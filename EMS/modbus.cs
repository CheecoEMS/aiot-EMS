using EMS;
using log4net;
using Modbus;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace EMS
{
    public class modbus
    {
        #region ===== Static SerialPort Pool (核心) =====

        private class SerialPortWrapper
        {
            public SerialPort Port;
            public readonly object SyncRoot = new object();
        }

        private static readonly Dictionary<string, SerialPortWrapper> PortPool =
            new Dictionary<string, SerialPortWrapper>();

        private static readonly object PortPoolLock = new object();

        private static SerialPortWrapper GetWrapper(string portName)
        {
            lock (PortPoolLock)
            {
                if (!PortPool.TryGetValue(portName, out var w))
                {
                    w = new SerialPortWrapper
                    {
                        Port = new SerialPort()
                    };
                    PortPool[portName] = w;
                }
                return w;
            }
        }

        #endregion

        #region ===== Fields =====

        private SerialPortWrapper spw;
        private SerialPort sp => spw?.Port;

        public string modbusStatus;
        public AllEquipmentClass ParentEquipment;

        private static readonly ILog log = LogManager.GetLogger("modbus485");

        #endregion

        #region ===== Constructor / Destructor =====

        public modbus() { }

        ~modbus()
        {
            // ❌ 禁止析构中 Close 串口
        }

        #endregion

        #region ===== Open / Close =====

        public bool Open(string portName, int baudRate, int databits, Parity parity, StopBits stopBits)
        {
            try
            {
                spw = GetWrapper(portName);

                lock (spw.SyncRoot)
                {
                    if (!sp.IsOpen)
                    {
                        sp.PortName = portName;
                        sp.BaudRate = baudRate;
                        sp.DataBits = databits;
                        sp.Parity = parity;
                        sp.StopBits = stopBits;
                        sp.ReadTimeout = 1000;
                        sp.WriteTimeout = 1000;
                        sp.Open();
                    }
                }

                modbusStatus = portName + " opened";
                return true;
            }
            catch (Exception ex)
            {
                log.Error("打开串口失败: " + ex);
                return false;
            }
        }

        public bool Close()
        {
            if (spw == null || sp == null)
                return true;

            lock (spw.SyncRoot)
            {
                try
                {
                    if (sp.IsOpen)
                        sp.Close();
                }
                catch (Exception ex)
                {
                    log.Error("关闭串口失败: " + ex);
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region ===== Low Level IO (绝对线程安全) =====

        private bool ReadResponse(byte[] buffer)
        {
            int index = 0;
            try
            {
                while (index < buffer.Length)
                {
                    int b = sp.ReadByte();
                    if (b < 0)
                        break;
                    buffer[index++] = (byte)b;
                }
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        private bool SendAndReceive(byte[] request, byte[] response)
        {
            if (spw == null || sp == null || !sp.IsOpen)
                return false;

            lock (spw.SyncRoot)
            {
                try
                {
                    sp.DiscardInBuffer();
                    sp.DiscardOutBuffer();

                    sp.Write(request, 0, request.Length);

                    return ReadResponse(response);
                }
                catch (Exception ex)
                {
                    log.Error("串口通信异常: " + ex.Message);
                    return false;
                }
            }
        }

        #endregion

        #region ===== Modbus Core =====

        private bool Read1Response(byte addr, byte cmd, ushort start, ushort len, ref byte[] resp)
        {
            byte[] req = ModbusBase.BuildMSG3(addr, cmd, start, len);
            resp = new byte[5 + (int)Math.Ceiling(len / 8.0)];

            return SendAndReceive(req, resp) && ModbusBase.CheckResponse(resp);
        }

        private bool Read3Response(byte addr, byte cmd, ushort start, ushort len, ref byte[] resp)
        {
            byte[] req = ModbusBase.BuildMSG3(addr, cmd, start, len);
            resp = new byte[5 + len * 2];

            return SendAndReceive(req, resp) && ModbusBase.CheckResponse(resp);
        }

        private bool Read5Response(byte addr, byte cmd, ushort reg, bool data, ref byte[] resp)
        {
            byte[] req = ModbusBase.BuildMSG5(addr, cmd, reg, data);
            resp = new byte[8];

            return SendAndReceive(req, resp) && ModbusBase.CheckResponse(resp);
        }

        private bool Read6Response(byte addr, byte cmd, ushort reg, ushort data, ref byte[] resp)
        {
            byte[] req = ModbusBase.BuildMSG6(addr, cmd, reg, data);
            resp = new byte[8];

            return SendAndReceive(req, resp) && ModbusBase.CheckResponse(resp);
        }

        #endregion

        #region ===== Public API (与你原来一致) =====

        public bool Send3MSG(byte addr, byte cmd, ushort start, ushort len, ref ushort[] values)
        {
            byte[] resp = null;
            if (!Read3Response(addr, cmd, start, len, ref resp))
                return false;

            values = new ushort[len];
            for (int i = 0; i < len; i++)
                values[i] = (ushort)((resp[3 + i * 2] << 8) | resp[4 + i * 2]);

            return true;
        }

        public bool Send1MSG(byte addr, byte cmd, ushort start, ushort len, ref byte[] values)
        {
            byte[] resp = null;
            if (!Read1Response(addr, cmd, start, len, ref resp))
                return false;

            int dataLen = resp[2];
            values = new byte[dataLen];
            Array.Copy(resp, 3, values, 0, dataLen);
            return true;
        }

        public bool Send5MSG(byte addr, byte cmd, ushort reg, bool data)
        {
            byte[] resp = null;
            return Read5Response(addr, cmd, reg, data, ref resp);
        }

        public bool Send6MSG(byte addr, byte cmd, ushort reg, ushort data)
        {
            byte[] resp = null;
            return Read6Response(addr, cmd, reg, data, ref resp);
        }

        public bool Send16MSG(byte addr, byte cmd, ushort start, ushort len, short[] values)
        {
            byte[] req = ModbusBase.BuildMSG16(addr, cmd, start, len, values);
            byte[] resp = new byte[8];

            return SendAndReceive(req, resp) && ModbusBase.CheckResponse(resp);
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

    }
}
