using EMS;
using log4net;
using Modbus;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Reflection;
using System.Timers;

namespace EMS
{
    /// <summary>
    /// 工业级 Modbus RTU 从站
    /// </summary>
    public class ModbusSlave
    {
        #region ===== Fields =====

        private readonly modbus _port;              
        private readonly byte _slaveAddr;          
        private readonly ILog log = LogManager.GetLogger("ModbusSlave");

        private readonly List<byte> _rxBuffer = new List<byte>();
        private readonly object _lock = new object();

        private readonly Timer _frameTimer;          // 3.5T 帧间隔定时器

        #endregion

        #region ===== Constructor =====

        public ModbusSlave(modbus port, byte slaveAddr)
        {
            _port = port ?? throw new ArgumentNullException(nameof(port));
            _slaveAddr = slaveAddr;

            double frameIntervalMs = CalcFrameInterval(port);
            _frameTimer = new Timer(frameIntervalMs);
            _frameTimer.AutoReset = false;
            _frameTimer.Elapsed += OnFrameTimeout;
        }

        #endregion

        #region ===== Start / Stop =====

        /// <summary>
        /// 启动从站监听（必须在串口 Open 后调用）
        /// </summary>
        public void Start()
        {
            if (!_port.IsOpen)
                throw new InvalidOperationException("串口未打开");

            _portBytesHook = new Action<byte[]>(OnBytesArrived);
            ModbusSerialHook.Attach(_port, _portBytesHook);

            log.Info($"ModbusSlave 启动，地址={_slaveAddr}");
        }

        /// <summary>
        /// 停止监听
        /// </summary>
        public void Stop()
        {
            ModbusSerialHook.Detach(_port, _portBytesHook);
            _frameTimer.Stop();
        }

        #endregion

        #region ===== 串口数据接收 =====

        private Action<byte[]> _portBytesHook;

        private void OnBytesArrived(byte[] bytes)
        {
            lock (_lock)
            {
                _rxBuffer.AddRange(bytes);

                _frameTimer.Stop();
                _frameTimer.Start(); // 重置 3.5T
            }
        }

        /// <summary>
        /// 3.5T 无数据，认为一帧结束
        /// </summary>
        private void OnFrameTimeout(object sender, ElapsedEventArgs e)
        {
            byte[] frame;

            lock (_lock)
            {
                if (_rxBuffer.Count < 4)
                {
                    _rxBuffer.Clear();
                    return;
                }

                frame = _rxBuffer.ToArray();
                _rxBuffer.Clear();
            }

            HandleFrame(frame);
        }

        #endregion

        #region ===== Frame Handling =====

        private void HandleFrame(byte[] frame)
        {
            try
            {
                // CRC 校验
                if (!ModbusBase.CheckResponse(frame))
                {
                    log.Warn("CRC 校验失败");
                    return;
                }

                // 地址过滤
                if (frame[0] != _slaveAddr && frame[0] != 0x00)
                    return;

                byte func = frame[1];

                switch (func)
                {
                    case 0x03:
                        Handle03(frame);
                        break;

                    case 0x06:
                        Handle06(frame);
                        break;

                    case 0x10:
                        Handle16(frame);
                        break;

                    default:
                        SendException(frame[0], func, 0x01);
                        break;
                }
            }
            catch (Exception ex)
            {
                log.Error("HandleFrame异常: " + ex);
            }
        }

        #endregion

        #region ===== 功能码实现 =====

        /// <summary>
        /// 03 读保持寄存器
        /// </summary>
        private void Handle03(byte[] req)
        {
            ushort start = (ushort)(req[2] << 8 | req[3]);
            ushort qty = (ushort)(req[4] << 8 | req[5]);

            byte[] resp = new byte[3 + qty * 2 + 2];
            resp[0] = _slaveAddr;
            resp[1] = 0x03;
            resp[2] = (byte)(qty * 2);

            for (int i = 0; i < qty; i++)
            {
                ushort val = Back3Data((ushort)(start + i));
                resp[3 + i * 2] = (byte)(val >> 8);
                resp[4 + i * 2] = (byte)(val);
            }

            _port.WriteRaw(resp, 0, resp.Length);
        }

        /// <summary>
        /// 06 写单寄存器
        /// </summary>
        private void Handle06(byte[] req)
        {
            ushort addr = (ushort)(req[2] << 8 | req[3]);
            ushort val = (ushort)(req[4] << 8 | req[5]);

            Active6Data(addr, val);

            // 原样回显
            _port.WriteRaw(req, 0, req.Length);
        }

        /// <summary>
        /// 16 写多个寄存器
        /// </summary>
        private void Handle16(byte[] req)
        {
            ushort start = (ushort)(req[2] << 8 | req[3]);
            ushort qty = (ushort)(req[4] << 8 | req[5]);

            int index = 7;
            for (int i = 0; i < qty; i++)
            {
                ushort val = (ushort)(req[index] << 8 | req[index + 1]);
                Active6Data((ushort)(start + i), val);
                index += 2;
            }

            byte[] resp = new byte[8];
            Array.Copy(req, 0, resp, 0, 6);

            _port.WriteRaw(resp, 0, resp.Length);
        }

        #endregion

        #region ===== Exception =====

        private void SendException(byte addr, byte func, byte code)
        {
            byte[] resp = new byte[5];
            resp[0] = addr;
            resp[1] = (byte)(func | 0x80);
            resp[2] = code;

            _port.WriteRaw(resp, 0, resp.Length);
        }

        #endregion

        #region ===== 业务接口 =====

        private ushort Back3Data(ushort addr)
        {
            return Back3Data(addr);
        }

        private void Active6Data(ushort addr, ushort value)
        {
          Active6Data(addr, value);
        }

        #endregion

        #region ===== Utils =====

        private double CalcFrameInterval(modbus port)
        {
            //int baud = port.IsOpen ? port.ParentEquipment.BaudRate : 9600;
            int baud = port.IsOpen ? 9600 : 9600;
            double charTimeMs = 1000.0 * 11 / baud;
            return charTimeMs * 3.5;
        }

        #endregion

        static public byte[] Back3Data(int aAddr, short iLen)
        {
            byte[] returnMsg = null;
            ushort aMsg;
            int index = 3;
            returnMsg = ModbusBase.BuildMSG3sTitle((byte)frmSet.config.i485Addr, 3, (ushort)iLen);
            for (int i = aAddr; i <= aAddr+iLen; ++i)
            {
                aMsg = 0;
                switch (i)
                {
                    case 0x5000://设备序列号
                        //aMsg = frmSet.SysID;
                        break;
                    case 0x5001://功率，正数为放电，负数为充电
                        aMsg = (ushort)frmMain.Selffrm.AllEquipment.PCSKVA;
                        break;
                    case 0x5002://日充电量kWh
                        aMsg = (ushort)frmMain.Selffrm.AllEquipment.E2PKWH[0];
                        break;
                    case 0x5003://日放电量kWh
                        aMsg = (ushort)frmMain.Selffrm.AllEquipment.E2OKWH[0];
                        break;
                    case 0x5004://月充电量kWh
                        aMsg = 0;
                        break;
                    case 0x5005://月放电量kWh
                        aMsg = 0;
                        break;
                    case 0x5006://总充电量kWh
                        aMsg = (ushort)frmMain.Selffrm.AllEquipment.Elemeter2.PUkwh[0];
                        break;
                    case 0x5007://总放电量kWh
                        aMsg = (ushort)frmMain.Selffrm.AllEquipment.Elemeter2.OUkwh[0];
                        break;
                    case 0x5008://总容量（%）
                        aMsg = 200;
                        break;
                    case 0x5009://soc上限
                        aMsg = 100;
                        break;
                    case 0x5010://soc下限
                        aMsg = 5;
                        break;
                    case 0x5011://最大功率充电时长（分钟）
                        aMsg = 90;
                        break;
                    case 0x5012://最大功率放电时长（分钟)
                        aMsg = 90;
                        break;
                    case 0x5013://健康度（%）
                        aMsg = 100;
                        break;
                    case 0x5014://状态1：在线，0：离线
                        aMsg = 0;
                        break;
                    case 0x5015://充放电状态0：待机，1：充电，2：放电
                        if (frmMain.Selffrm.AllEquipment.PCSKVA == 0)
                        {
                            aMsg = 0;
                        }
                        else
                        {
                            if (frmMain.Selffrm.AllEquipment.wTypeActive == "充电")
                            {
                                aMsg = 1;
                            }
                            else if (frmMain.Selffrm.AllEquipment.wTypeActive == "放电")
                            {
                                aMsg = 2;
                            }
                        }
                        break;
                    case 0x5016://BMS告警信息
                        aMsg = 0;
                        break;
                    case 0x5017://PCS告警信息
                        aMsg = 0;
                        break;
                    case 0x5018://EMS告警信息
                        aMsg = 0;
                        break;
                    case 0x5019:
                        break;
                }
                //组装报文
                ModbusBase.AddMSG3(aMsg, ref returnMsg, ref index);
            }
            ModbusBase.AddCRC(ref returnMsg);
            return returnMsg;

        }

        //连控数据中设置寄存器---执行6
        static public void Active6Data(int aAddr, int data)
        {

            switch (aAddr)
            {
                case 0x6000://开关pcs
                    if (data != 0)
                    {
                        frmMain.Selffrm.AllEquipment.PCSList[0].ExcSetPCSPower(true);
                        lock (frmMain.Selffrm.AllEquipment)
                            frmMain.Selffrm.AllEquipment.HostStart = true;
                    }
                    else
                    {
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            frmMain.Selffrm.AllEquipment.HostStart = false;
                            frmMain.Selffrm.AllEquipment.PCSScheduleKVA = 0;
                        }
                    }
                    break;
                case 0x6001://计划功率
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        frmMain.Selffrm.AllEquipment.PCSScheduleKVA = data;
                    }
                    break;
                case 0x6002://实际功率
                    //log.Error("从机接收Command执行参数:"+ frmMain.Selffrm.AllEquipment.wTypeActive + frmMain.Selffrm.AllEquipment.PCSTypeActive + data);
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        frmMain.Selffrm.AllEquipment.HostStart = true;
                        frmMain.Selffrm.AllEquipment.PCSScheduleKVA = data;
                        frmMain.Selffrm.AllEquipment.NetControl = true;
                    }
                    break;
                case 0x6003://充放电
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        if (data == 0)
                            frmMain.Selffrm.AllEquipment.wTypeActive = "充电";
                        else
                            frmMain.Selffrm.AllEquipment.wTypeActive = "放电";
                    }
                    break;
                case 0x6004://恒压横流恒功率、AC恒压
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        if (data>=0 && data < PCSClass.PCSTypes.Length)
                        {
                            frmMain.Selffrm.AllEquipment.PCSTypeActive = PCSClass.PCSTypes[data];
                        }
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// modbus 串口接收 Hook（不破坏 modbus 内部封装）
    /// </summary>
    internal static class ModbusSerialHook
    {
        private static readonly Dictionary<modbus, SerialPort> _portMap = new Dictionary<modbus, SerialPort>();
        private static readonly Dictionary<modbus, SerialDataReceivedEventHandler> _handlerMap = new Dictionary<modbus, SerialDataReceivedEventHandler>();

        /// <summary>
        /// 绑定串口接收回调
        /// </summary>
        public static void Attach(modbus m, Action<byte[]> onBytes)
        {
            if (m == null || onBytes == null)
                return;

            var sp = GetSerialPort(m);
            if (sp == null)
                throw new InvalidOperationException("无法从 modbus 获取 SerialPort");

            SerialDataReceivedEventHandler handler = (s, e) =>
            {
                try
                {
                    int count = sp.BytesToRead;
                    if (count <= 0) return;

                    byte[] buf = new byte[count];
                    sp.Read(buf, 0, count);

                    onBytes(buf);
                }
                catch
                {
                    // 串口异常直接忽略（工业现场必须抗异常）
                }
            };

            sp.DataReceived += handler;
            _portMap[m] = sp;
            _handlerMap[m] = handler;
        }

        /// <summary>
        /// 解除绑定
        /// </summary>
        public static void Detach(modbus m, Action<byte[]> _)
        {
            if (!_portMap.TryGetValue(m, out var sp))
                return;

            if (_handlerMap.TryGetValue(m, out var handler))
            {
                sp.DataReceived -= handler;
                _handlerMap.Remove(m);
                _portMap.Remove(m);
            }
        }

        /// <summary>
        /// 反射获取 modbus 内部 SerialPort（不修改 modbus）
        /// </summary>
        private static SerialPort GetSerialPort(modbus m)
        {
            var field = typeof(modbus)
                .GetField("spw", BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null) return null;

            var spw = field.GetValue(m);
            if (spw == null) return null;

            var portField = spw.GetType()
                .GetField("Port", BindingFlags.Public | BindingFlags.Instance);

            return portField?.GetValue(spw) as SerialPort;
        }
    }
}