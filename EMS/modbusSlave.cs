using log4net;
using Modbus;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace EMS
{
    /// <summary>
    /// 工业级 Modbus RTU 从站（单线程 RTU 状态机）
    /// </summary>
    public sealed class ModbusSlave
    {
        private readonly modbus _mb;
        private readonly byte _slaveAddr;

        private readonly EmsRegisterSnapshot _regs;
        private readonly EmsCommandExecutor _cmd;

        private readonly ILog _log = LogManager.GetLogger(nameof(ModbusSlave));

        private Thread _worker;
        private volatile bool _running;

        public ModbusSlave(modbus mb, byte slaveAddr, IEmsService emsService)
        {
            _mb = mb ?? throw new ArgumentNullException(nameof(mb));
            _slaveAddr = slaveAddr;

            _regs = new EmsRegisterSnapshot(emsService);
            _cmd = new EmsCommandExecutor(emsService);
        }

        #region ===== Start / Stop =====

        public void Start()
        {

            _running = true;
            _worker = new Thread(RtuLoop)
            {
                IsBackground = true,
                Name = "ModbusRTUSlave"
            };
            _worker.Start();

            _log.Info($"Modbus RTU 从站启动，地址={_slaveAddr}");
        }

        public void Stop()
        {
            _running = false;
            _worker?.Join(2000);
            _log.Info("Modbus RTU 从站停止");
        }

        #endregion

        #region ===== RTU 主循环（单线程） =====

        private void RtuLoop()
        {
            var buffer = new List<byte>(256);
            var sw = Stopwatch.StartNew();

            try
            {
                while (_running)
                {
                    int b = _mb.SlaveReadByte();   // ✅ 只依赖 modbus
                    if (b < 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    buffer.Add((byte)b);
                    sw.Restart();

                    while (_mb.SlaveBytesToRead == 0)
                    {
                        if (sw.ElapsedMilliseconds >= 4)
                        {
                            ProcessFrame(buffer.ToArray());
                            buffer.Clear();
                            break;
                        }
                        Thread.Sleep(1);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Fatal("RTU 主循环异常", ex);
            }
        }

        #endregion

        #region ===== Frame Processing =====

        private void ProcessFrame(byte[] frame)
        {
            if (frame.Length < 4)
                return;

            if (!ModbusBase.CheckResponse(frame))
            {
                _log.Warn("CRC 校验失败");
                return;
            }

            // 地址过滤（不响应广播）
            if (frame[0] != _slaveAddr)
                return;

            try
            {
                switch (frame[1])
                {
                    case 0x03:
                        Send(Handle03(frame));
                        break;

                    case 0x06:
                        Handle06(frame);
                        Send(frame); // 原样回显
                        break;

                    case 0x10:
                        Send(Handle16(frame));
                        break;

                    default:
                        SendException(frame[0], frame[1], 0x01);
                        break;
                }
            }
            catch (ModbusException mex)
            {
                SendException(frame[0], frame[1], mex.Code);
            }
            catch (Exception ex)
            {
                _log.Error("处理帧异常", ex);
            }
        }

        #endregion

        #region ===== Function Codes =====

        private byte[] Handle03(byte[] req)
        {
            ushort start = ToUShort(req[2], req[3]);
            ushort qty = ToUShort(req[4], req[5]);

            if (qty == 0 || qty > 125)
                throw new ModbusException(0x03);

            var resp = new byte[3 + qty * 2];
            resp[0] = _slaveAddr;
            resp[1] = 0x03;
            resp[2] = (byte)(qty * 2);

            for (int i = 0; i < qty; i++)
            {
                ushort v = _regs.Read((ushort)(start + i));
                resp[3 + i * 2] = (byte)(v >> 8);
                resp[4 + i * 2] = (byte)v;
            }

            ModbusBase.AddCRC(ref resp);
            return resp;
        }

        private void Handle06(byte[] req)
        {
            ushort addr = ToUShort(req[2], req[3]);
            ushort val = ToUShort(req[4], req[5]);

            DispatchWrite(addr, val);
        }

        private byte[] Handle16(byte[] req)
        {
            ushort start = ToUShort(req[2], req[3]);
            ushort qty = ToUShort(req[4], req[5]);

            int index = 7;
            for (int i = 0; i < qty; i++)
            {
                ushort v = ToUShort(req[index], req[index + 1]);
                DispatchWrite((ushort)(start + i), v);
                index += 2;
            }

            byte[] resp = new byte[6];
            Array.Copy(req, 0, resp, 0, 6);
            ModbusBase.AddCRC(ref resp);
            return resp;
        }

        #endregion

        #region ===== Write Dispatch =====

        private void DispatchWrite(ushort addr, ushort val)
        {
            switch (addr)
            {
                case 0x6000:
                    _cmd.Enqueue(val == 0 ? EmsCommand.Stop : EmsCommand.Start);
                    break;

                case 0x6001:
                    _cmd.Enqueue(EmsCommand.SetPower, val);
                    break;

                case 0x6003:
                    _cmd.Enqueue(EmsCommand.SetMode, val);
                    break;

                default:
                    throw new ModbusException(0x02);
            }
        }

        #endregion

        #region ===== Utils =====

        private static ushort ToUShort(byte hi, byte lo) =>
            (ushort)((hi << 8) | lo);

        private void Send(byte[] data)
        {
            _mb.WriteRaw(data, 0, data.Length);
        }

        private void SendException(byte addr, byte func, byte code)
        {
            byte[] resp = new byte[5];
            resp[0] = addr;
            resp[1] = (byte)(func | 0x80);
            resp[2] = code;
            ModbusBase.AddCRC(ref resp);
            Send(resp);
        }

        #endregion
    }

    #region ===== Register Snapshot =====

    public sealed class EmsRegisterSnapshot
    {
        private readonly IEmsService _ems;
        private readonly object _lock = new BlockingCollection<(EmsCommand, int)>();

        public EmsRegisterSnapshot(IEmsService ems)
        {
            _ems = ems;
        }

        public ushort Read(ushort addr)
        {
            lock (_lock)
            {
                ushort result;
                switch (addr)
                {
                    case 0x5001:
                        result = _ems.GetPcsPower();
                        break;

                    case 0x5002:
                        result = _ems.GetChargeEnergyDay();
                        break;

                    case 0x5003:
                        result = _ems.GetDischargeEnergyDay();
                        break;

                    case 0x5006:
                        result = _ems.GetTotalCharge();
                        break;

                    case 0x5007:
                        result = _ems.GetTotalDischarge();
                        break;

                    case 0x5015:
                        result = _ems.GetWorkState();
                        break;

                    default:
                        throw new ModbusException(0x02);
                }

                return result;
            }
        }
    }

    #endregion

    #region ===== Command Queue =====

    public enum EmsCommand
    {
        Start,
        Stop,
        SetPower,
        SetMode
    }

    public sealed class EmsCommandExecutor
    {
        private readonly IEmsService _ems;
        private readonly BlockingCollection<(EmsCommand, int)> _queue = new BlockingCollection<(EmsCommand, int)>();

        public EmsCommandExecutor(IEmsService ems)
        {
            _ems = ems;
            Task.Run(ProcessLoop);
        }

        public void Enqueue(EmsCommand cmd, int val = 0)
            => _queue.Add((cmd, val));

        private void ProcessLoop()
        {
            foreach (var (cmd, val) in _queue.GetConsumingEnumerable())
            {
                try
                {
                    switch (cmd)
                    {
                        case EmsCommand.Start:
                            _ems.StartPcs();
                            break;
                        case EmsCommand.Stop:
                            _ems.StopPcs();
                            break;
                        case EmsCommand.SetPower:
                            _ems.SetSchedulePower(val);
                            break;
                        case EmsCommand.SetMode:
                            _ems.SetChargeDischargeMode(val != 0);
                            break;
                    }
                }
                catch
                {
                    // 业务异常不影响 Modbus 通讯
                }
            }
        }
    }

    #endregion

    #region ===== Modbus Exception =====

    public sealed class ModbusException : Exception
    {
        public byte Code { get; }

        public ModbusException(byte code)
        {
            Code = code;
        }
    }

    #endregion
}
