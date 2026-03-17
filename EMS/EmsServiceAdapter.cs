using log4net;
using System;

namespace EMS
{
    /// <summary>
    /// EMS 业务服务接口 - 用于 Modbus 从站与业务逻辑解耦
    /// </summary>
    public interface IEmsService
    {
        // 数据读取
        ushort GetPcsPower();
        ushort GetChargeEnergyDay();
        ushort GetDischargeEnergyDay();
        ushort GetTotalCharge();
        ushort GetTotalDischarge();
        ushort GetSoc();
        ushort GetWorkState();

        // 控制命令
        void StartPcs();
        void StopPcs();
        void SetSchedulePower(int kva);
        void SetChargeDischargeMode(bool discharge);
    }

    /// <summary>
    /// EMS 设备服务适配器 - 将 EMSEquipment 适配到 IEmsService 接口
    /// </summary>
    public sealed class EmsServiceAdapter : IEmsService
    {
        private static readonly ILog _log = LogManager.GetLogger(nameof(EmsServiceAdapter));
        private readonly EMSEquipment _emsEquipment;

        public EmsServiceAdapter(EMSEquipment emsEquipment)
        {
            _emsEquipment = emsEquipment ?? throw new ArgumentNullException(nameof(emsEquipment));
        }

        #region ===== 数据读取 =====

        public ushort GetPcsPower()
        {
            try
            {
                // 从 EMSEquipment 获取实际功率数据
                return (ushort)Math.Round(_emsEquipment.waValueActive);
            }
            catch (Exception ex)
            {
                _log.Error("获取 PCS 功率失败", ex);
                return 0;
            }
        }

        public ushort GetChargeEnergyDay()
        {
            try
            {
                // TODO: 从数据库或缓存获取当日充电电量
                // 当前返回 0 作为占位符
                return 0;
            }
            catch (Exception ex)
            {
                _log.Error("获取当日充电电量失败", ex);
                return 0;
            }
        }

        public ushort GetDischargeEnergyDay()
        {
            try
            {
                // TODO: 从数据库或缓存获取当日放电电量
                return 0;
            }
            catch (Exception ex)
            {
                _log.Error("获取当日放电电量失败", ex);
                return 0;
            }
        }

        public ushort GetTotalCharge()
        {
            try
            {
                // TODO: 从数据库获取累计充电电量
                return 0;
            }
            catch (Exception ex)
            {
                _log.Error("获取累计充电电量失败", ex);
                return 0;
            }
        }

        public ushort GetTotalDischarge()
        {
            try
            {
                // TODO: 从数据库获取累计放电电量
                return 0;
            }
            catch (Exception ex)
            {
                _log.Error("获取累计放电电量失败", ex);
                return 0;
            }
        }

        public ushort GetSoc()
        {
            try
            {
                // TODO: 从 BMS 获取 SOC 数据
                return 0;
            }
            catch (Exception ex)
            {
                _log.Error("获取 SOC 失败", ex);
                return 0;
            }
        }

        public ushort GetWorkState()
        {
            try
            {
                // 从 EMSEquipment 获取工作状态
                // 0:充电，1:放电，2:待机
                return (ushort)_emsEquipment.WorkType;
            }
            catch (Exception ex)
            {
                _log.Error("获取工作状态失败", ex);
                return 2; // 默认返回待机状态
            }
        }

        #endregion

        #region ===== 控制命令 =====

        public void StartPcs()
        {
            try
            {
                _emsEquipment.ExcPCSOn(true);
                _log.Info("PCS 启动命令已发送");
            }
            catch (Exception ex)
            {
                _log.Error("启动 PCS 失败", ex);
                throw;
            }
        }

        public void StopPcs()
        {
            try
            {
                _emsEquipment.ExcPCSOn(false);
                _log.Info("PCS 停机命令已发送");
            }
            catch (Exception ex)
            {
                _log.Error("停止 PCS 失败", ex);
                throw;
            }
        }

        public void SetSchedulePower(int kva)
        {
            try
            {
                _emsEquipment.SetPCSScheduleKVA(kva);
                _log.Info($"PCS 设定功率：{kva} kVA");
            }
            catch (Exception ex)
            {
                _log.Error($"设置 PCS 功率失败：{kva} kVA", ex);
                throw;
            }
        }

        public void SetChargeDischargeMode(bool discharge)
        {
            try
            {
                // 0:充电，1:放电
                _emsEquipment.WorkType = discharge ? 1 : 0;
                _log.Info($"PCS 模式设置：{(discharge ? "放电" : "充电")}");
            }
            catch (Exception ex)
            {
                _log.Error($"设置 PCS 模式失败：{(discharge ? "放电" : "充电")}", ex);
                throw;
            }
        }

        #endregion
    }
}
