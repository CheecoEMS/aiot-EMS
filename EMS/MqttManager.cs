using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using M2Mqtt;
using M2Mqtt.Messages;
using log4net;

namespace EMS
{
    public class MqttManager
    {
        private static readonly ILog log = LogManager.GetLogger("MqttManager");
        private readonly object _syncRoot = new object();

        private MqttClient _client;
        private CancellationTokenSource _cts;
        private Task _connectLoopTask;
        private bool _connectLoopRunning;
        private SignalRecoveryCoordinator _recoveryCoordinator;


        private const int MaxConsecutiveConnectFailuresBeforeRecovery = 12; // 测试用4，生产用12


        private volatile MqttState _state = MqttState.Disconnected;
        public MqttState CurrentState => _state;

        private volatile bool _intentionalDisconnect;

        // ================= 事件 =================

        public MqttClient.MqttMsgPublishEventHandler MessageReceived;
        public event Action<MqttState> StateChanged;

        // ================= 配置 =================

        public string BrokerIp;
        public int BrokerPort = 8883;
        public string Username;
        public string Password;
        public string ClientId;

        // ================= QOS =================
        public enum PublishQos
        {
            AtMostOnce = 0,
            AtLeastOnce = 1
        }


        // ================= 公共 API =================

        public void SetRecoveryCoordinator(SignalRecoveryCoordinator recoveryCoordinator)
        {
            lock (_syncRoot)
            {
                _recoveryCoordinator = recoveryCoordinator;
            }
        }

        public void Start()
        {
            Action<MqttState> stateChangedHandler = null;

            lock (_syncRoot)
            {
                if (_state != MqttState.Disconnected)
                    return;

                log.Info("MQTT Start()");
                _intentionalDisconnect = false;

                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                _state = MqttState.Connecting;
                stateChangedHandler = StateChanged;
            }

            stateChangedHandler?.Invoke(MqttState.Connecting);
            EnsureConnectLoopRunning();
        }

        public void Stop()
        {
            Action<MqttState> stateChangedHandler = null;

            lock (_syncRoot)
            {
                if (_state == MqttState.Stopping || _state == MqttState.Disconnected)
                    return;

                log.Info("MQTT Stop()");
                _state = MqttState.Stopping;
                _intentionalDisconnect = true;


                _cts?.Cancel();
                CleanupClient();

                _state = MqttState.Disconnected;
                stateChangedHandler = StateChanged;
            }

            stateChangedHandler?.Invoke(MqttState.Disconnected);
        }

        public bool Publish(string topic, string payload, bool retain, PublishQos qos = PublishQos.AtLeastOnce)
        {
            lock (_syncRoot)
            {
                if (_state != MqttState.Connected || _client == null)
                    return false;

                try
                {
                    byte qosLevel = qos == PublishQos.AtMostOnce
                        ? MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE
                        : MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE;

                    _client.Publish(
                        topic,
                        Encoding.UTF8.GetBytes(payload),
                        qosLevel,
                        retain);

                    return true;
                }
                catch (Exception ex)
                {
                    log.Warn("Publish failed", ex);
                    return false;
                }
            }
        }

        public void Subscribe(string topic)
        {
            lock (_syncRoot)
            {
                if (_state != MqttState.Connected || _client == null)
                    return;

                try
                {
                    _client.Subscribe(
                        new[] { topic },
                        new[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
                }
                catch (Exception ex)
                {
                    log.Warn("Subscribe failed", ex);
                }
            }
        }

        // ================= 状态机核心 =================

        private void EnsureConnectLoopRunning()
        {
            lock (_syncRoot)
            {
                if (_state != MqttState.Connecting)
                    return;

                if (_cts == null)
                    _cts = new CancellationTokenSource();

                if (_connectLoopRunning && _connectLoopTask != null && !_connectLoopTask.IsCompleted)
                    return;

                _connectLoopRunning = true;
                _connectLoopTask = Task.Run(() => ConnectLoop(_cts.Token));
            }
        }

        public void ResumeAfterRecovery()
        {
            Action<MqttState> stateChangedHandler = null;
            bool shouldRestartLoop = false;

            lock (_syncRoot)
            {
                _intentionalDisconnect = false;

                log.Warn($"MQTT ResumeAfterRecovery(), current state: {_state}");
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                if (_state != MqttState.Stopping)
                {
                    _state = MqttState.Connecting;
                    stateChangedHandler = StateChanged;
                    shouldRestartLoop = true;
                }
            }

            stateChangedHandler?.Invoke(MqttState.Connecting);

            if (shouldRestartLoop)
                EnsureConnectLoopRunning();
        }



        private async Task ConnectLoop(CancellationToken token)
        {
            int retry = 0;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    lock (_syncRoot)
                    {
                        if (_state != MqttState.Connecting)
                            return;
                    }

                    retry++;

                    int delay;
                    if (retry == 1)
                        delay = 5 * 1000;
                    else if (retry == 2)
                        delay = 25 * 1000;
                    else if (retry == 3)
                        delay = 30 * 1000;
                    else if (retry == 4)
                        delay = 1 * 60 * 1000;
                    else if (retry == 5)
                        delay = 2 * 60 * 1000;
                    else if (retry == 6)
                        delay = 6 * 60 * 1000;
                    else
                        delay = 10 * 60 * 1000;

                    log.Warn($"MQTT connect attempt #{retry}, delay {delay / 1000}s before connect");

                    try
                    {
                        await Task.Delay(delay, token);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }

                    lock (_syncRoot)
                    {
                        if (_state != MqttState.Connecting)
                            return;
                    }

                    if (TryConnect())
                    {
                        Action<MqttState> stateChangedHandler = null;

                        lock (_syncRoot)
                        {
                            if (_state != MqttState.Connecting)
                                return;

                            _state = MqttState.Connected;
                            stateChangedHandler = StateChanged;
                        }

                        stateChangedHandler?.Invoke(MqttState.Connected);
                        log.Warn("MQTT Connected");
                        return;
                    }

                    if (retry >= MaxConsecutiveConnectFailuresBeforeRecovery)
                    {
                        log.Warn($"MQTT连续重试{retry}次仍失败，开始发起网络服务和物联网模块恢复请求");

                        bool recoveryRequested = TriggerRecovery($"MQTT连续重试{retry}次失败");

                        if (recoveryRequested)
                            log.Warn("MQTT恢复请求已提交，ConnectLoop退出，等待恢复完成后由 ResumeAfterRecovery() 重新拉起");
                        else
                            log.Warn("MQTT恢复请求提交失败，ConnectLoop仍退出，等待外部再次触发连接");

                        return;
                    }
                }
            }
            finally
            {
                lock (_syncRoot)
                {
                    _connectLoopRunning = false;
                }
            }
        }


        private bool TriggerRecovery(string reason)
        {
            SignalRecoveryCoordinator recoveryCoordinator;

            lock (_syncRoot)
            {
                recoveryCoordinator = _recoveryCoordinator;
            }

            if (recoveryCoordinator == null)
            {
                log.Warn($"未配置SignalRecoveryCoordinator，无法执行恢复流程，原因: {reason}");
                return false;
            }

            try
            {
                return recoveryCoordinator.ExecuteRecovery(reason);
            }
            catch (Exception ex)
            {
                log.Error($"发起恢复流程异常，原因: {reason}", ex);
                return false;
            }
        }

        private bool TryConnect()
        {
            MqttClient clientToConnect;

            lock (_syncRoot)
            {
                if (_state != MqttState.Connecting)
                    return false;

                try
                {
                    CleanupClient();
                    _intentionalDisconnect = false;

                    log.Warn($"Creating MQTT client {BrokerIp}:{BrokerPort}");

                    clientToConnect = new MqttClient(
                        BrokerIp,
                        BrokerPort,
                        true,
                        null,
                        null,
                        MqttSslProtocols.TLSv1_2);

                    clientToConnect.MqttMsgPublishReceived += OnMessage;
                    clientToConnect.ConnectionClosed += OnConnectionClosed;
                    _client = clientToConnect;
                }
                catch (Exception ex)
                {
                    log.Error("Create MQTT client failed", ex);
                    return false;
                }
            }

            try
            {
                clientToConnect.Connect(
                    ClientId,
                    Username,
                    Password,
                    true,
                    60);
            }
            catch (Exception ex)
            {
                log.Error("Connect failed", ex);

                lock (_syncRoot)
                {
                    CleanupClientIfCurrent(clientToConnect);
                }

                return false;
            }

            lock (_syncRoot)
            {
                if (_state != MqttState.Connecting || _client != clientToConnect)
                {
                    CleanupClientIfCurrent(clientToConnect);
                    return false;
                }

                if (!clientToConnect.IsConnected)
                {
                    CleanupClientIfCurrent(clientToConnect);
                    return false;
                }

                return true;
            }
        }

        private void CleanupClient()
        {
            CleanupClientInternal(_client, true);
            _client = null;
        }

        private void CleanupClientIfCurrent(MqttClient client)
        {
            if (_client != client)
            {
                CleanupClientInternal(client, false);
                return;
            }

            CleanupClientInternal(client, true);
            _client = null;
        }

        private void CleanupClientInternal(MqttClient client, bool markCurrentAsNull)
        {
            if (client == null)
                return;

            try
            {
                client.MqttMsgPublishReceived -= OnMessage;
                client.ConnectionClosed -= OnConnectionClosed;

                if (client.IsConnected)
                {
                    _intentionalDisconnect = true;
                    client.Disconnect();
                }
            }
            catch (Exception ex)
            {
                log.Warn("Cleanup error", ex);
            }
            finally
            {
                if (markCurrentAsNull && ReferenceEquals(_client, client))
                    _client = null;
            }
        }

        // ================= 事件 =================

        private void OnConnectionClosed(object sender, EventArgs e)
        {
            Action<MqttState> stateChangedHandler = null;
            bool shouldRestartLoop = false;

            lock (_syncRoot)
            {
                log.Warn($"ConnectionClosed in state {_state}");

                if (_intentionalDisconnect || _state == MqttState.Stopping)

                    return;

                if (_state == MqttState.Connected)
                {
                    _state = MqttState.Connecting;
                    stateChangedHandler = StateChanged;
                    shouldRestartLoop = true;
                }
            }

            if (stateChangedHandler != null)
                stateChangedHandler(MqttState.Connecting);

            if (shouldRestartLoop)
                EnsureConnectLoopRunning();
        }

        public MqttState GetState()
        {
            lock (_syncRoot)
            {
                return _state;
            }
        }



        private void OnMessage(object sender, MqttMsgPublishEventArgs e)
        {
            try
            {
                MessageReceived?.Invoke(sender, e);
            }
            catch (Exception ex)
            {
                log.Error("MessageReceived handler error", ex);
            }
        }
    }

    public enum MqttState
    {
        Disconnected,
        Connecting,
        Connected,
        Stopping
    }
}

