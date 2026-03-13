using EMS;
using Google.Protobuf.WellKnownTypes;
using log4net;
using Modbus;
using System;
using System.Collections.Generic;
using System.Data;
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

        #region ===== Retry Helper (统一重试策略) =====

        private bool ExecuteWithRetry(
            Func<bool> action,
            int maxRetry = 3,
            int initialDelayMs = 50,
            int maxDelayMs = 1000)
        {
            try
            {

                int delay = initialDelayMs;

                for (int attempt = 1; attempt <= maxRetry; attempt++)
                {
                    if (action())
                        return true;

                    if (attempt < maxRetry)
                    {
                        Thread.Sleep(delay);
                        delay = Math.Min(delay * 2, maxDelayMs);
                    }
                }

                return false;
            }
            catch (Exception ex) {
                log.Error("ExecuteWithRetry: " + ex);
                return false;
            }
        }

        #endregion

        #region ===== Public Raw IO (线程安全的原始读写) =====

        /// <summary>
        /// 检查串口是否已打开
        /// </summary>
        public bool IsOpen
        {
            get
            {
                return spw != null && sp != null && sp.IsOpen;
            }
        }

        /// <summary>
        /// 获取可读取的字节数
        /// </summary>
        public int BytesToRead
        {
            get
            {
                if (spw == null || sp == null || !sp.IsOpen)
                    return 0;

                lock (spw.SyncRoot)
                {
                    try
                    {
                        return sp.BytesToRead;
                    }
                    catch
                    {
                        return 0;
                    }
                }
            }
        }

        /// <summary>
        /// 直接写入原始数据到串口（线程安全）
        /// </summary>
        /// <param name="data">要写入的数据</param>
        /// <param name="offset">起始偏移</param>
        /// <param name="count">写入字节数</param>
        /// <returns>是否成功</returns>
        public bool WriteRaw(byte[] data, int offset, int count)
        {
            if (spw == null || sp == null || !sp.IsOpen)
                return false;

            lock (spw.SyncRoot)
            {
                try
                {
                    sp.Write(data, offset, count);
                    return true;
                }
                catch (Exception ex)
                {
                    log.Error("WriteRaw异常: " + ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// 读取串口中所有可用的字节（线程安全）
        /// </summary>
        /// <returns>读取到的字节数组，如果失败返回 null</returns>
        public byte[] ReadAvailableBytes()
        {
            if (spw == null || sp == null || !sp.IsOpen)
                return null;

            lock (spw.SyncRoot)
            {
                try
                {
                    int bytesToRead = sp.BytesToRead;
                    if (bytesToRead <= 0)
                        return new byte[0];

                    byte[] buffer = new byte[bytesToRead];
                    int bytesRead = sp.Read(buffer, 0, bytesToRead);

                    if (bytesRead < bytesToRead)
                    {
                        byte[] result = new byte[bytesRead];
                        Array.Copy(buffer, result, bytesRead);
                        return result;
                    }
                    return buffer;
                }
                catch (Exception ex)
                {
                    log.Error("ReadAvailableBytes异常: " + ex.Message);
                    return null;
                }
            }
        }

        /// <summary>
        /// 读取单个字节（线程安全）
        /// </summary>
        /// <returns>读取到的字节，如果失败返回 -1</returns>
        public int ReadByte()
        {
            if (spw == null || sp == null || !sp.IsOpen)
                return -1;

            lock (spw.SyncRoot)
            {
                try
                {
                    if (sp.BytesToRead > 0)
                        return sp.ReadByte();
                    return -1;
                }
                catch
                {
                    return -1;
                }
            }
        }

        #endregion

        #region ===== Modbus Core =====

        private bool Read1Response(byte addr, byte cmd, ushort start, ushort len, ref byte[] resp)
        {
            try
            {
                byte[] req = ModbusBase.BuildMSG3(addr, cmd, start, len);
                resp = new byte[5 + (int)Math.Ceiling(len / 8.0)];

                return SendAndReceive(req, resp) && ModbusBase.CheckResponse(resp);
            }
            catch (Exception ex) {
                log.Error("Read1Response: " + ex);
                return false;
            }
        }

        private bool Read3Response(byte addr, byte cmd, ushort start, ushort len, ref byte[] resp)
        {
            try
            {
                byte[] req = ModbusBase.BuildMSG3(addr, cmd, start, len);
                resp = new byte[5 + len * 2];

                return SendAndReceive(req, resp) && ModbusBase.CheckResponse(resp);
            }
            catch (Exception ex)
            {
                log.Error("Read3Response: " + ex);
                return false;
            }
        }

        private bool Read5Response(byte addr, byte cmd, ushort reg, bool data, ref byte[] resp)
        {
            try
            {
                byte[] req = ModbusBase.BuildMSG5(addr, cmd, reg, data);
                resp = new byte[8];

                return SendAndReceive(req, resp) && ModbusBase.CheckResponse(resp);
            }
            catch (Exception ex)
            {
                log.Error("Read5Response: " + ex);
                return false;
            }
        }

        private bool Read6Response(byte addr, byte cmd, ushort reg, ushort data, ref byte[] resp)
        {
            try
            {
                byte[] req = ModbusBase.BuildMSG6(addr, cmd, reg, data);
                resp = new byte[8];

                return SendAndReceive(req, resp) && ModbusBase.CheckResponse(resp);
            }
            catch (Exception ex)
            {
                log.Error("Read6Response: " + ex);
                return false;
            }
        }

        private bool Read6Response(byte aAddress, byte CommandType, ushort aRegAddr, byte[] aData, ref byte[] aResponse)
        {
            try
            {
                byte[] message = ModbusBase.BuildMSG6(aAddress, CommandType, aRegAddr, aData);
                aResponse = new byte[8];

                return SendAndReceive(message, aResponse) && ModbusBase.CheckResponse(aResponse);
            }
            catch (Exception ex)
            {
                log.Error("Read6Response: " + ex);
                return false;
            }
        }

        /// <summary>
        /// 功能码16：写多个寄存器 Core 实现
        /// </summary>
        private bool Read16Response( byte addr, byte cmd, ushort start, ushort len, byte[] values, ref byte[] resp)
        {
            try
            {
                byte[] req = ModbusBase.BuildMSG16(addr, cmd, start, len, values);
                resp = new byte[8]; // 功能码16响应固定8字节

                return SendAndReceive(req, resp) && ModbusBase.CheckResponse(resp);
            }
            catch (Exception ex)
            {
                log.Error("Read6Response: " + ex);
                return false;
            }
        }

        private bool Read16Response( byte addr, byte cmd, ushort start, ushort len, short[] values, ref byte[] resp)
        {
            try
            {
                byte[] req = ModbusBase.BuildMSG16(addr, cmd, start, len, values);
                resp = new byte[8];

                return SendAndReceive(req, resp) && ModbusBase.CheckResponse(resp);
            }
            catch (Exception ex)
            {
                log.Error("Read6Response: " + ex);
                return false;
            }
        }
        #endregion



        #region ===== Public Modbus API (带重试) =====
        public bool Send1MSG(byte aAddress, byte CommandType, ushort aRegStart, ushort aRegLength, ref byte[] values)
        {
            byte[] resp = null;
            if (!Read1Response(aAddress, CommandType, aRegStart, aRegLength, ref resp))
                return false;

            int dataLen = resp[2];
            values = new byte[dataLen];
            Array.Copy(resp, 3, values, 0, dataLen);
            return true;
        }

        /// <summary>
        /// 返回数组类型为ushort型
        /// </summary>
        public bool Send3MSG(byte aAddress, byte CommandType, ushort aRegStart, ushort aRegLength, ref ushort[] values)
        {
            byte[] resp = null;
            if (!Read3Response(aAddress, CommandType, aRegStart, aRegLength, ref resp))
                return false;

            values = new ushort[aRegLength];
            for (int i = 0; i < aRegLength; i++)
                values[i] = (ushort)((resp[3 + i * 2] << 8) | resp[4 + i * 2]);

            return true;
        }

        /// <summary>
        /// 返回数组类型为字节型
        /// </summary>
        public bool Send3MSG(byte aAddress, byte CommandType, ushort aRegStart, ushort aRegLength, ref byte[] values)
        {
            byte[] resp = null;
            if (!Read3Response(aAddress, CommandType, aRegStart, aRegLength, ref resp))
            {
                return false;
            }
            //返回数据转换
            values = new byte[aRegLength];
            int DataLen = resp[2];
            //Return requested register values:
            Array.Copy(resp, 3, values, 0, DataLen);
            return true;
        }

        public bool Send5MSG(byte addr, byte cmd, ushort reg, bool data)
        {
            byte[] resp = null;
            return ExecuteWithRetry(
                () => Read5Response(addr, cmd, reg, data, ref resp)
            );
        }

        public bool Send6MSG(byte aAddress, byte CommandType, ushort aRegStart, ushort value)
        {
            byte[] resp = null;
            return ExecuteWithRetry(
                () => Read6Response(aAddress, CommandType, aRegStart, value, ref resp)
            );
        }

        /// <summary>
        /// 发送功能码6消息（写单个寄存器 - byte[]）
        /// </summary>
        public bool Send6MSG(byte aAddress, byte CommandType, ushort aRegStart, byte[] aData)
        {
            byte[] resp = null;
            return ExecuteWithRetry(
                () => Read6Response(aAddress, CommandType, aRegStart, aData, ref resp)
            );
        }

        /// <summary>
        /// 发送功能码16消息（写多个寄存器 - short[]）
        /// </summary>
        public bool Send16MSG(byte aAddress, byte CommandType, ushort aRegStart, ushort aRegLength, short[] values)
        {
            byte[] resp = null;
            return ExecuteWithRetry(
                () => Read16Response(aAddress, CommandType, aRegStart, aRegLength, values, ref resp)
            );
        }

        /// <summary>
        /// 发送功能码16消息（写多个寄存器 - byte[]）
        /// </summary>
        public bool Send16MSG(byte aAddress, byte CommandType, ushort aRegStart, ushort aRegLength, byte[] values)
        {
            byte[] resp = null;
            return ExecuteWithRetry(
                () => Read16Response(aAddress, CommandType, aRegStart, aRegLength, values, ref resp)
            );
        }

        #endregion


        #region ===== Modbus Send Core =====
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
        #endregion

        #region ===== 4G Module Methods =====

        /// <summary>
        /// 发送4G数据（线程安全）
        /// </summary>
        private void Get4GData(byte[] aMessage)
        {
            if (spw == null || sp == null || !sp.IsOpen)
                return;

            lock (spw.SyncRoot)
            {
                try
                {
                    sp.DiscardOutBuffer();
                    sp.DiscardInBuffer();
                    sp.Write(aMessage, 0, aMessage.Length);
                }
                catch (Exception ex)
                {
                    log.Error("Get4GData异常: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 重启4G模块
        /// </summary>
        public void Restart4G()
        {
            byte[] message = new byte[14] { 0x41, 0x54, 0x2B, 0x43, 0x46, 0x55, 0x4E, 0x3D, 0x31, 0x2C, 0x31, 0x0D, 0x0D, 0x0A };
            Get4GData(message);
        }

        #endregion

        #region ===== OpenEMS (兼容旧接口) =====

        /// <summary>
        /// 打开串口（兼容旧接口）
        /// </summary>
        public bool OpenEMS(string portName, int baudRate, int databits, Parity parity, StopBits stopBits)
        {
            return Open(portName, baudRate, databits, parity, stopBits);
        }

        #endregion

        #region ===== SendstrMSG =====

        /// <summary>
        /// 发送消息并返回16进制字符串
        /// </summary>
        public bool SendstrMSG(byte aAddress, byte bComType, ushort aRegStart, ushort aRegLength, ref string strBack)
        {
            byte[] response = null;
            if (!Read3Response(aAddress, bComType, aRegStart, aRegLength, ref response))
            {
                return false;
            }
            //返回数据转换
            strBack = "";
            for (int i = 0; i < response.Length; i++)
            {
                strBack += response[i].ToString("x2");
            }
            return true;
        }

        #endregion

    }
}
