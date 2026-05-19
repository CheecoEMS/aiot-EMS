using System;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using log4net;

namespace EMS
{
    /// <summary>
    /// 网络恢复协调器：负责全局互斥、业务静默、网络/模块恢复、恢复后放流。
    /// </summary>
    public class SignalRecoveryCoordinator
    {
        private static readonly ILog log = LogManager.GetLogger("RecoveryManager");

        private readonly object _syncRoot = new object();
        private readonly ReaderWriterLockSlim _signalDetectionGate = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        private bool _recoveryInProgress;
        private TaskNetListener _taskNetListener;
        private MqttManager _mqttManager;

        private const int SignalDetectionDrainTimeoutMs = 30000;    // 等待TaskNetListener服务结束的超时等待时间

        private const int QuiesceWindowMs = 10000;      // 连续稳定满足多久，才算成功。要求“业务静默”状态要连续保持 10 秒。
        private const int StagePollIntervalMs = 1000;   // 每隔多久检查一次条件
        private const int ModuleRecoverMinWaitMs = 10000;
        private const int ModuleRecoverPollIntervalMs = 5000;
        private const int ModuleRecoverTimeoutMs = 120000;
        private const int NetworkEnableTimeoutMs = 120000;  //移动宽带连接恢复超时等待时间

        public SignalRecoveryCoordinator()
        {

        }

        // ================= 公共 API =================

        public void SetTaskNetListener(TaskNetListener taskNetListener)
        {
            _taskNetListener = taskNetListener;
        }

        public void SetMqttManager(MqttManager mqttManager)
        {
            _mqttManager = mqttManager;
        }

        public bool TryBeginRecovery(string reason)
        {
            lock (_syncRoot)
            {
                if (_recoveryInProgress)
                {
                    log.Warn($"[Recovery] 已有恢复流程在执行中，忽略新的恢复请求，原因: {reason}");
                    return false;
                }

                _recoveryInProgress = true;
                log.Error($"[Recovery] 接受恢复请求，原因: {reason}");
                return true;
            }
        }

        public bool ExecuteSignalDetection(Action action)
        {
            if (action == null)
                return false;

            _signalDetectionGate.EnterReadLock();
            try
            {
                lock (_syncRoot)
                {
                    if (_recoveryInProgress)
                    {
                        log.Warn("[Recovery] 当前处于恢复流程中，跳过本轮信号检测");
                        return false;
                    }
                }

                try
                {
                    action();
                    return true;
                }
                catch (Exception ex) {
                    log.Error("ExecuteSignalDetection: " + ex.ToString());
                    return false;
                }
            }
            finally
            {
                if (_signalDetectionGate.IsReadLockHeld)
                    _signalDetectionGate.ExitReadLock();
            }
        }

        public bool ExecuteRecovery(string reason, Action onAlarmSet = null, Action onRecoveryFinished = null)
        {
            try
            {
                if (!TryBeginRecovery(reason))
                    return false;

                new Thread(() =>
                {
                    log.Warn("[Recovery] 等待进入恢复独占区，阻止新的信号检测进入");
                    _signalDetectionGate.EnterWriteLock();
                    try
                    {
                        log.Warn("[Recovery] 已进入恢复独占区，开始等待 TestSignalStrength() 的任务排空");

                        bool drained = WaitForCondition(
                            () => _taskNetListener == null || !_taskNetListener.IsTestSignalStrengthCoreRunning,
                            SignalDetectionDrainTimeoutMs,
                            StagePollIntervalMs,
                            "等待 TestSignalStrength() 任务排空");

                        if (drained)
                            log.Warn("[Recovery] TestSignalStrength() 任务已排空，开始执行恢复流程");
                        else
                            log.Warn("[Recovery] 等待 IsTestSignalStrengthCoreRunning=false 超时，继续执行恢复流程");

                        RecoverCore(onAlarmSet, onRecoveryFinished);
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"[Recovery] 异步恢复任务执行异常: {ex.Message}", ex);
                    }
                    finally
                    {
                        if (_signalDetectionGate.IsWriteLockHeld)
                            _signalDetectionGate.ExitWriteLock();
                    }
                })
                {
                    IsBackground = true,
                    Name = "SignalRecoveryThread"
                }.Start();

                return true;
            }
            catch (Exception ex) {
                log.Error("ExecuteRecovery: "+ ex.ToString());
                return false;
            }
        }

        private void RecoverCore(Action onAlarmSet, Action onRecoveryFinished)
        {
            try
            {
                onAlarmSet?.Invoke();

                log.Warn("[Recovery] Phase 1 - Quiesce begin");

                log.Warn($"[Recovery] Phase 2 - Quiesce window begin, need stable {QuiesceWindowMs / 1000}s");
                WaitForStableCondition(
                    () => true,
                    QuiesceWindowMs,
                    QuiesceWindowMs + StagePollIntervalMs,
                    StagePollIntervalMs,
                    "业务静默观察窗口");
                log.Warn("[Recovery] Quiesce window completed");

                var manager = new MobileBroadbandManager();

                log.Warn("[Recovery] Phase 3 - Check mobile broadband state begin");
                if (manager.IsNetEnabled())
                {
                    log.Warn("[Recovery] 移动宽带连接当前已连接，跳过CFUN恢复与MBN连接");
                }
                else
                {
                    log.Warn("[Recovery] 移动宽带连接当前未连接，开始执行 action.txt 恢复流程");

                    log.Warn("[Recovery] Phase 4 - Send AT+CFUN=0 begin");
                    bool cfun0Success = SendCfun0ToEc20Module();
                    if (cfun0Success)
                        log.Warn("[Recovery] AT+CFUN=0 completed");
                    else
                        log.Warn("[Recovery] AT+CFUN=0 failed, continue recovery flow");

                    log.Warn("[Recovery] Phase 5 - Send AT+CFUN=1 begin");
                    bool cfun1Success = SendCfun1ToEc20Module();
                    if (cfun1Success)
                        log.Warn("[Recovery] AT+CFUN=1 completed");
                    else
                        log.Warn("[Recovery] AT+CFUN=1 failed, continue recovery flow");

                    log.Warn("[Recovery] Phase 6 - Module recover wait begin");
                    if (WaitForModuleReady())
                        log.Warn("[Recovery] EC20 ready confirmed");
                    else
                        log.Warn("[Recovery] 等待EC20恢复超时，继续尝试MBN连接");

                    log.Warn("[Recovery] Phase 7 - netsh mbn connect begin");
                    bool connectTriggered = manager.ConnectNet();
                    if (connectTriggered)
                        log.Warn("[Recovery] netsh mbn connect command completed");
                    else
                        log.Warn("[Recovery] netsh mbn connect command failed");

                    log.Warn("[Recovery] Phase 8 - Network verification begin");
                    bool networkUp = WaitForCondition(
                        () => TryCheckNetworkReady(manager),
                        NetworkEnableTimeoutMs,
                        StagePollIntervalMs,
                        "网络上线检查");

                    if (networkUp)
                        log.Warn("[Recovery] Network up confirmed");
                    else
                        log.Warn("[Recovery] 等待网络上线超时，仍尝试恢复MQTT");

                }
                log.Warn("[Recovery] Phase 9 - MQTT resume begin");
                _mqttManager?.ResumeAfterRecovery();

                log.Warn("[Recovery] Phase 10 - Signal detection resume ready");
                log.Warn("[Recovery] Recovery completed");
            }
            catch (Exception ex)
            {
                log.Warn($"[Recovery] 恢复流程执行异常: {ex.Message}", ex);
            }
            finally
            {
                lock (_syncRoot)
                {
                    _recoveryInProgress = false;
                }

                onRecoveryFinished?.Invoke();
                log.Warn("[Recovery] 已退出恢复流程，恢复信号检测");
            }
        }

        private bool SendCfun0ToEc20Module()
        {
            try
            {
                lock (SerialPortCom11Lock.GlobalLock)
                {
                    using (var ec20 = new EC20Communicator())
                    {
                        if (!ec20.Connect())
                        {
                            log.Warn("[Recovery] EC20通信器连接失败，无法发送AT+CFUN=0");
                            return false;
                        }

                        string atResponse = ec20.SendAtCommand("AT", 2000);
                        if (string.IsNullOrEmpty(atResponse) ||
                            atResponse.IndexOf("OK", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            log.Warn($"[Recovery] 模块未响应AT指令，无法发送AT+CFUN=0，响应内容: {atResponse ?? "空"}");
                            return false;
                        }

                        return ec20.SendCfun0Command();
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("SendCfun0ToEc20Module: " + ex.ToString());
                return false;
            }
        }

        private bool SendCfun1ToEc20Module()
        {
            try
            {
                lock (SerialPortCom11Lock.GlobalLock)
                {
                    using (var ec20 = new EC20Communicator())
                    {
                        if (!ec20.Connect())
                        {
                            log.Warn("[Recovery] EC20通信器连接失败，无法发送AT+CFUN=1");
                            return false;
                        }

                        string atResponse = ec20.SendAtCommand("AT", 2000);
                        if (string.IsNullOrEmpty(atResponse) ||
                            atResponse.IndexOf("OK", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            log.Warn($"[Recovery] 模块未响应AT指令，无法发送AT+CFUN=1，响应内容: {atResponse ?? "空"}");
                            return false;
                        }

                        return ec20.SendCfun1Command();
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("SendCfun1ToEc20Module: " + ex.ToString());
                return false;
            }
        }

        private bool WaitForModuleReady()
        {
            try
            {
                log.Warn($"[Recovery] EC20恢复最小等待时间 {ModuleRecoverMinWaitMs / 1000}s");

                return WaitForCondition(
                    TryCheckModuleReady,
                    ModuleRecoverTimeoutMs,
                    ModuleRecoverPollIntervalMs,
                    "等待EC20恢复",
                    ModuleRecoverMinWaitMs);
            }
            catch (Exception ex) {
                log.Error("WaitForModuleReady: " + ex.ToString());
                return false;
            }
        }

        private bool WaitForCondition(
            Func<bool> condition,
            int timeoutMs,
            int pollIntervalMs,
            string stageName,
            int minWaitBeforeCheckMs = 0)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();

                while (stopwatch.ElapsedMilliseconds < timeoutMs)
                {
                    if (stopwatch.ElapsedMilliseconds >= minWaitBeforeCheckMs)
                    {
                        bool matched = false;
                        try
                        {
                            matched = condition();
                        }
                        catch (Exception ex)
                        {
                            log.Warn($"[Recovery] {stageName} 检查异常: {ex.Message}", ex);
                        }

                        if (matched)
                            return true;
                    }

                    int nextWait = Math.Min(pollIntervalMs, Math.Max(0, timeoutMs - (int)stopwatch.ElapsedMilliseconds));
                    if (nextWait <= 0)
                        break;

                    Thread.Sleep(nextWait);
                    log.Warn($"[Recovery] {stageName}中，已等待 {stopwatch.ElapsedMilliseconds / 1000}s");
                }

                return false;
            }
            catch (Exception ex) {
                log.Error("WaitForCondition: " + ex.ToString());
                return false;
            }
        }

        private bool WaitForStableCondition(
            Func<bool> condition,
            int stableWindowMs,
            int timeoutMs,
            int pollIntervalMs,
            string stageName)
        {
            try
            {
                var totalWatch = Stopwatch.StartNew();
                Stopwatch stableWatch = null;

                while (totalWatch.ElapsedMilliseconds < timeoutMs)
                {
                    bool matched = false;
                    try
                    {
                        matched = condition();
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"[Recovery] {stageName} 检查异常: {ex.Message}", ex);
                    }

                    if (matched)
                    {
                        if (stableWatch == null)
                        {
                            stableWatch = Stopwatch.StartNew();
                            log.Warn($"[Recovery] {stageName} 条件首次满足，开始稳定计时");
                        }
                        else if (stableWatch.ElapsedMilliseconds >= stableWindowMs)
                        {
                            return true;
                        }
                    }
                    else if (stableWatch != null)
                    {
                        log.Warn($"[Recovery] {stageName} 条件中断，重新计时");
                        stableWatch = null;
                    }

                    int stableElapsed = stableWatch == null ? 0 : (int)stableWatch.ElapsedMilliseconds;
                    log.Warn($"[Recovery] {stageName}中，总等待 {totalWatch.ElapsedMilliseconds / 1000}s，稳定持续 {stableElapsed / 1000}s");

                    int nextWait = Math.Min(pollIntervalMs, Math.Max(0, timeoutMs - (int)totalWatch.ElapsedMilliseconds));
                    if (nextWait <= 0)
                        break;

                    Thread.Sleep(nextWait);
                }

                return false;
            }
            catch (Exception ex) {
                log.Error("WaitForStableCondition: " + ex.ToString());
                return false;
            }
        }

        private bool TryCheckModuleReady()
        {
            try
            {
                lock (SerialPortCom11Lock.GlobalLock)
                {
                    using (var ec20 = new EC20Communicator())
                    {
                        if (!ec20.Connect())
                        {
                            log.Warn("[Recovery] 模块就绪检查失败：EC20通信器连接失败");
                            return false;
                        }

                        string atResponse = ec20.SendAtCommand("AT", 2000);
                        if (string.IsNullOrEmpty(atResponse) ||
                            atResponse.IndexOf("OK", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            log.Warn($"[Recovery] 模块就绪检查失败：AT未响应OK，响应内容: {atResponse ?? "空"}");
                            return false;
                        }

                        string cpinResponse = ec20.SendAtCommand("AT+CPIN?", 2000);
                        if (string.IsNullOrEmpty(cpinResponse) ||
                            cpinResponse.IndexOf("READY", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            log.Warn($"[Recovery] 模块就绪检查失败：SIM未就绪，响应内容: {cpinResponse ?? "空"}");
                            return false;
                        }

                        string qceregResponse;
                        if (!ec20.IsQceregRegistered(out qceregResponse))
                        {
                            log.Warn($"[Recovery] 模块就绪检查失败：网络尚未注册成功，AT+QCEREG?响应内容: {qceregResponse ?? "空"}");
                            return false;
                        }

                        log.Warn($"[Recovery] 模块就绪检查通过：AT正常，SIM已就绪，网络已注册，AT+QCEREG?响应内容: {qceregResponse}");
                        return true;
                    }
                }
            }
            catch (Exception ex) {
                log.Error("TryCheckModuleReady: " + ex.ToString());
                return false;
            }
        }

        private bool TryCheckNetworkReady(MobileBroadbandManager manager)
        {
            try
            {
                if (manager == null)
                {
                    log.Warn("[Recovery] 网络就绪检查失败：MobileBroadbandManager为空");
                    return false;
                }

                bool isEnabled = manager.IsNetEnabled();
                if (!isEnabled)
                {
                    log.Warn("[Recovery] 网络就绪检查失败：移动宽带连接未处于已连接状态");
                    return false;
                }

                log.Warn("[Recovery] 网络就绪检查通过：移动宽带连接已处于已连接状态");
                return true;
            }
            catch (Exception ex) {
                log.Error("TryCheckNetworkReady: " + ex.ToString());
                return false;
            }
        }
    }
}
