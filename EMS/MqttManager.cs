using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using M2Mqtt;
using M2Mqtt.Messages;
using log4net;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;
using static System.Net.Mime.MediaTypeNames;

namespace EMS
{
    public class MqttManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MqttManager));
        private readonly object _syncRoot = new object();

        private MqttClient _client;
        private CancellationTokenSource _cts;

        private volatile MqttState _state = MqttState.Disconnected;
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

        public void Start()
        {
            lock (_syncRoot)
            {
                if (_state != MqttState.Disconnected)
                    return;

                log.Info("MQTT Start()");
                _state = MqttState.Connecting;
                _intentionalDisconnect = false;
                _cts = new CancellationTokenSource();

                Task.Run(() => ConnectLoop(_cts.Token));
            }
        }

        public void Stop()
        {
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
                StateChanged?.Invoke(_state);
            }
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

        private async Task ConnectLoop(CancellationToken token)
        {
            int retry = 0;

            while (!token.IsCancellationRequested)
            {
                lock (_syncRoot)
                {
                    if (_state != MqttState.Connecting)
                        return;
                }

                retry++;
                int delay = Math.Min(30000, retry * 2000);
                log.Warn($"MQTT connect attempt #{retry}");

                try
                {
                    await Task.Delay(delay, token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (TryConnect())
                {
                    lock (_syncRoot)
                    {
                        if (_state == MqttState.Connecting)
                        {
                            _state = MqttState.Connected;
                            StateChanged?.Invoke(_state);
                            log.Info("MQTT Connected");
                        }
                    }
                    return;
                }
            }
        }

        private bool TryConnect()
        {
            lock (_syncRoot)
            {
                try
                {
                    // ✅ 每次连接前都彻底清理
                    CleanupClient();
                    _intentionalDisconnect = false;

                    log.Warn($"Creating MQTT client {BrokerIp}:{BrokerPort}");

                    _client = new MqttClient(
                        BrokerIp,
                        BrokerPort,
                        true,
                        null,
                        null,
                        MqttSslProtocols.TLSv1_2);

                    _client.MqttMsgPublishReceived += OnMessage;
                    _client.ConnectionClosed += OnConnectionClosed;

                    _client.Connect(
                        ClientId,
                        Username,
                        Password,
                        true,   // ✅ cleanSession = true（更稳定） 原为true，暂时改为false，防止重连后重新订阅到历史消息
                        60);    // ✅ keepAlive 较小 原为30，暂时改为60

                    return _client.IsConnected;
                }
                catch (Exception ex)
                {
                    log.Error("Connect failed", ex);
                    return false;
                }
            }
        }

        private void CleanupClient()
        {
            if (_client == null)
                return;

            try
            {
                _client.MqttMsgPublishReceived -= OnMessage;
                _client.ConnectionClosed -= OnConnectionClosed;

                if (_client.IsConnected)
                {
                    _intentionalDisconnect = true;
                    _client.Disconnect();
                }
            }
            catch (Exception ex)
            {
                log.Warn("Cleanup error", ex);
            }
            finally
            {
                _client = null;
            }
        }

        // ================= 事件 =================

        private void OnConnectionClosed(object sender, EventArgs e)
        {
            lock (_syncRoot)
            {
                log.Warn($"ConnectionClosed in state {_state}");

                if (_intentionalDisconnect || _state == MqttState.Stopping)
                    return;

                if (_state == MqttState.Connected)
                {
                    _state = MqttState.Connecting;
                    StateChanged?.Invoke(_state);
                    Task.Run(() => ConnectLoop(_cts.Token));
                }
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

// 无注释版本
/*using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using M2Mqtt;
using M2Mqtt.Messages;

namespace EMS
{
    public class MqttManager
    {
        private MqttClient _client;
        private readonly object _lock = new object();

        private Task _reconnectTask;
        private bool _needReconnect;
        private bool _stopping;

        public event EventHandler<MqttMsgPublishEventArgs> MessageReceived;
        public event Action<bool> ConnectionStateChanged;

        public string BrokerIp;
        public int BrokerPort;
        public string Username;
        public string Password;
        public string ClientId;

        public void Start()
        {
            _stopping = false;
            StartReconnectLoop();
        }

        public void Stop()
        {
            _stopping = true;
            _needReconnect = false;

            lock (_lock)
            {
                try
                {
                    if (_client != null)
                    {
                        _client.MqttMsgPublishReceived -= OnMessage;
                        _client.ConnectionClosed -= OnClosed;

                        if (_client.IsConnected)
                            _client.Disconnect();

                        _client = null;
                    }
                }
                catch { }
            }

            ConnectionStateChanged?.Invoke(false);
        }

        public bool Publish(string topic, string payload)
        {
            lock (_lock)
            {
                if (_client == null || !_client.IsConnected)
                    return false;

                _client.Publish(
                    topic,
                    Encoding.UTF8.GetBytes(payload),
                    MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE,
                    true);

                return true;
            }
        }

        public void Subscribe(string topic)
        {
            lock (_lock)
            {
                _client?.Subscribe(
                    new[] { topic },
                    new[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
            }
        }

        // ================= 内部 =================

        private void StartReconnectLoop()
        {
            if (_reconnectTask != null && !_reconnectTask.IsCompleted)
                return;

            _needReconnect = true;
            _reconnectTask = Task.Run(ReconnectLoop);
        }

        private async Task ReconnectLoop()
        {
            int retry = 0;

            while (_needReconnect && !_stopping)
            {
                retry++;
                await Task.Delay(Math.Min(30000, retry * 2000));

                try
                {
                    if (Connect())
                    {
                        _needReconnect = false;
                        ConnectionStateChanged?.Invoke(true);
                        return;
                    }
                }
                catch { }
            }
        }

        private bool Connect()
        {
            lock (_lock)
            {
                Cleanup();

                _client = new MqttClient(
                    BrokerIp,
                    BrokerPort,
                    true,
                    null,
                    null,
                    MqttSslProtocols.TLSv1_2);

                _client.MqttMsgPublishReceived += OnMessage;
                _client.ConnectionClosed += OnClosed;

                _client.Connect(
                    ClientId,
                    Username,
                    Password,
                    true,
                    60);

                return _client.IsConnected;
            }
        }

        private void Cleanup()
        {
            try
            {
                if (_client != null)
                {
                    _client.MqttMsgPublishReceived -= OnMessage;
                    _client.ConnectionClosed -= OnClosed;

                    if (_client.IsConnected)
                        _client.Disconnect();
                }
            }
            catch { }

            _client = null;
        }

        private void OnClosed(object sender, EventArgs e)
        {
            if (_stopping)
                return;

            _needReconnect = true;
            ConnectionStateChanged?.Invoke(false);
            StartReconnectLoop();
        }

        private void OnMessage(object sender, MqttMsgPublishEventArgs e)
        {
            MessageReceived?.Invoke(this, e);
        }
    }
}
*/