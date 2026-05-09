using log4net;
using System;
using System.Net.NetworkInformation;

namespace EMS
{
        /// <summary>
    /// 网络信号监听器：仅负责状态观测、日志记录与告警展示，不直接发起恢复流程。
    /// </summary>
    public class TaskNetListener
    {
        private static readonly ILog log = LogManager.GetLogger("TaskNetListener");

        public AllEquipmentClass Parent = null;
        private SignalRecoveryCoordinator _recoveryCoordinator;
        private volatile bool _isTestSignalStrengthCoreRunning;

        public bool IsTestSignalStrengthCoreRunning => _isTestSignalStrengthCoreRunning;

        private const int RecoveryThreshold = 6;
        private int consecutivePingErrorCount;

        private bool isProcessingAlarm = false;

        public TaskNetListener()
        {
        }

        // ================= 公共 API =================

        public void SetRecoveryCoordinator(SignalRecoveryCoordinator recoveryCoordinator)
        {
            _recoveryCoordinator = recoveryCoordinator;
        }


        public void TestSignalStrength()
        {
            if (_recoveryCoordinator != null)
            {
                _recoveryCoordinator.ExecuteSignalDetection(TestSignalStrengthCore);
                return;
            }

            TestSignalStrengthCore();
        }

        private void TestSignalStrengthCore()
        {
            _isTestSignalStrengthCoreRunning = true;
            try
            {
                bool isSlbPingSuccess = CheckSlbPing();
                bool isService = CheckModuleService();
                bool isSignalAbnormal = !isSlbPingSuccess || !isService;

                //bool isSignalAbnormal = !isSlbPingSuccess;

                if (!isSignalAbnormal)
                {
                    GetIccid();
                    GetSignalStrength();
                    HandleSignalNormal();
                    return;
                }

                consecutivePingErrorCount++;
                log.Warn($"[Recovery] 信号异常，连续异常次数：{consecutivePingErrorCount}");

                if (consecutivePingErrorCount < RecoveryThreshold)
                    return;

                if (!isProcessingAlarm) {
                    log.Warn($"[Recovery] 连续{RecoveryThreshold}次异常，触发信号告警展示，但不直接发起恢复流程");
                    SetAlarmState();
                }
            }
            catch (Exception ex)
            {
                log.Error($"[Recovery] 信号强度检测整体异常：{ex.Message}", ex);
                consecutivePingErrorCount++;

                if (consecutivePingErrorCount < RecoveryThreshold)
                    return;

                if (!isProcessingAlarm)
                {
                    log.Warn($"[Recovery] 连续{RecoveryThreshold}次异常，触发信号告警展示，但不直接发起恢复流程");
                    SetAlarmState();
                }
            }
            finally
            {
                _isTestSignalStrengthCoreRunning = false;
            }
        }

        private bool CheckSlbPing()
        {
            using (Ping pingSender = new Ping())
            {
                PingOptions options = new PingOptions { DontFragment = true };
                byte[] buffer = new byte[32];
                int timeout = 5000;

                try
                {
                    PingReply reply = pingSender.Send("cheeco.eaiot.cloud", timeout, buffer, options);
                    bool isSuccess = reply.Status == IPStatus.Success;

                    if (!isSuccess)
                        log.Warn($"[Recovery] SLB Ping失败，状态：{reply.Status}");

                    return isSuccess;
                }
                catch (Exception ex)
                {
                    log.Error($"[Recovery] SLB Ping异常：{ex.Message}");
                    return false;
                }
            }
        }

        private bool CheckModuleService()
        {
            lock (SerialPortCom11Lock.GlobalLock)
            {
                using (var ec20 = new EC20Communicator())
                {
                    if (!ec20.Connect())
                    {
                        log.Warn("[Recovery] EC20通信器连接失败");
                        return false;
                    }

                    string atResponse = ec20.SendAtCommand("AT", 2000);
                    if (string.IsNullOrEmpty(atResponse) || atResponse.IndexOf("OK", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        log.Warn($"[Recovery] 模块未响应AT指令，响应内容: {atResponse ?? "空"}");
                        return false;
                    }

                    string qcsqResponse = ec20.SendAtCommand("AT+QCSQ", 2000);
                    if (!string.IsNullOrEmpty(qcsqResponse) &&
                        qcsqResponse.IndexOf("NOSERVICE", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        log.Warn("[Recovery] AT+QCSQ返回NOSERVICE，模块无服务");
                        return false;
                    }

                    return true;
                }
            }
        }

        private void HandleSignalNormal()
        {
            consecutivePingErrorCount = 0;

            isProcessingAlarm = false;

            if (Parent != null)
            {
                lock (Parent.EMSError)
                {
                    Parent.EMSError[0] &= 0xBFFF;
                }
            }

            log.Error("[Recovery] 信号恢复正常，解除告警");
        }

        private void SetAlarmState()
        {
            isProcessingAlarm = true;

            if (Parent == null)
            {
                log.Warn("[Recovery] Parent为空，无法写入EMSError告警位");
                return;
            }

            lock (Parent.EMSError)
            {
                Parent.EMSError[0] |= 0x4000;
            }
        }

        public void GetIccid()
        {
            lock (SerialPortCom11Lock.GlobalLock)
            {
                try
                {
                    using (var ec20 = new EC20Communicator())
                    {
                        if (ec20.Connect())
                        {
                            // 基本连通性检测
                            string atResponse = ec20.SendAtCommand("AT", 2000);
                            if (!string.IsNullOrEmpty(atResponse) &&
                                atResponse.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                // 获取 ICCID
                                string iccidResponse = ec20.SendAtCommand("AT+QCCID", 2000);

                                if (!string.IsNullOrEmpty(iccidResponse) &&
                                    iccidResponse.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    /*
                                     * 返回示例：
                                     * +QCCID: 898600xxxxxxxxxxxx
                                     * OK
                                     */

                                    string iccid = ParseIccid(iccidResponse);
                                    /*
                                                                        if (!string.IsNullOrEmpty(iccid))
                                                                        {
                                                                            frmMain.Selffrm.AllEquipment.Iccid = iccid;
                                                                            return;
                                                                        }*/

                                    // 【关键修改】如果解析成功且长度大于1，去掉最后一位校验位
                                    if (!string.IsNullOrEmpty(iccid) && iccid.Length > 1)
                                    {
                                        // 截取掉最后一位
                                        iccid = iccid.Substring(0, iccid.Length - 1);

                                        frmMain.Selffrm.AllEquipment.Iccid = iccid;
                                        return;
                                    }
                                }
                            }

                            // 失败兜底
                            frmMain.Selffrm.AllEquipment.Iccid = string.Empty;
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error("GetIccid: " + ex);
                }

            }
        }

        private string ParseIccid(string atResponse)
        {
            // 按行拆分
            var lines = atResponse.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // EC20 返回格式：+QCCID: 898600xxxxxxxxxxxx
                if (line.StartsWith("+QCCID", StringComparison.OrdinalIgnoreCase))
                {
                    int index = line.IndexOf(":");
                    if (index >= 0)
                    {
                        return line.Substring(index + 1).Trim();
                    }
                }
            }

            return string.Empty;
        }



        public void GetSignalStrength()
        {
            try
            {
                // 创建EC20通信器实例
                lock (SerialPortCom11Lock.GlobalLock)
                {
                    using (var ec20 = new EC20Communicator())
                    {
                        if (ec20.Connect())
                        {
                            string atResponse = ec20.SendAtCommand("AT", 2000); // 延长超时到2000ms，确保响应完整
                            if (!string.IsNullOrEmpty(atResponse) && atResponse.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                //log.Error("测试信号质量");
                                var result = ec20.TestSignalQuality();

                                if (result.IsValid)
                                {
                                    frmMain.Selffrm.AllEquipment.Rssi = result.Rssi;
                                    frmMain.Selffrm.AllEquipment.Rsrp = result.Rsrp;
                                    frmMain.Selffrm.AllEquipment.Sinr = result.Sinr;
                                    frmMain.Selffrm.AllEquipment.Rsrq = result.Rsrq;
                                }
                                else
                                {
                                    frmMain.Selffrm.AllEquipment.Rssi = -120;
                                    frmMain.Selffrm.AllEquipment.Rsrp = -200;
                                    frmMain.Selffrm.AllEquipment.Sinr = -10;
                                    frmMain.Selffrm.AllEquipment.Rsrq =-20;
                                }
                            }
                            else
                            {
                                frmMain.Selffrm.AllEquipment.Rssi = -120;
                                frmMain.Selffrm.AllEquipment.Rsrp = -200;
                                frmMain.Selffrm.AllEquipment.Sinr = -10;
                                frmMain.Selffrm.AllEquipment.Rsrq =-20;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("GetSignalStrength: " + ex.ToString());
            }
        }
    }
}