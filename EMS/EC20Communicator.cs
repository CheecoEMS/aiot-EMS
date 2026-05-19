using System;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Threading;
using log4net;

namespace EMS
{
    public static class SerialPortCom11Lock
    {
        // 全局静态锁：控制所有线程对串口的访问
        public static readonly object GlobalLock = new object();
    }

    public class EC20Communicator : IDisposable
    {
        private SerialPort _serialPort;
        private bool _isDisposed = false;
        private readonly object _lockObj = new object();

        // 串口配置属性
        public string PortName { get; }
        public int BaudRate { get; }
        public int DataBits { get; }
        public Parity Parity { get; }
        public StopBits StopBits { get; }
        public bool IsConnected => _serialPort?.IsOpen ?? false;

        private static ILog log = LogManager.GetLogger("EC20Communicator");

        // 构造函数
        public EC20Communicator(string portName = "COM11", int baudRate = 115200,
                               int dataBits = 8, Parity parity = Parity.None,
                               StopBits stopBits = StopBits.One)
        {
            PortName = portName ?? throw new ArgumentNullException(nameof(portName));
            BaudRate = baudRate;
            DataBits = dataBits;
            Parity = parity;
            StopBits = stopBits;

            InitializeSerialPort();
        }

        // 初始化串口
        private void InitializeSerialPort()
        {
            try
            {
                _serialPort = new SerialPort(PortName, BaudRate, Parity, DataBits, StopBits)
                {
                    Handshake = Handshake.None,
                    ReadTimeout = 1000,   // 延长超时时间，确保能收到响应
                    WriteTimeout = 1000,
                    Encoding = System.Text.Encoding.ASCII  // AT指令通常使用ASCII编码
                };

                // 注册数据接收事件，可用于异步接收数据
                _serialPort.DataReceived += SerialPort_DataReceived;
            }catch (Exception ex){
                log.Error("InitializeSerialPort: " + ex.ToString());
            }
        }

        // 数据接收事件处理
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    string data = _serialPort.ReadExisting();
                    if (!string.IsNullOrEmpty(data))
                    {
                        log.Error($"异步接收数据:\n{data}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"数据接收错误: {ex.Message}");
            }
        }

        // 打开串口连接
        public bool Connect()
        {
            try
            {
                if (!_serialPort.IsOpen)
                {
                    _serialPort.Open();
                    //log.Error($"已打开串口 {PortName}");
                    return true;
                }
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"打开串口失败: {ex.Message}");
                return false;
            }
        }

        // 关闭串口连接
        public void Disconnect()
        {
            try
            {
                if (_serialPort?.IsOpen ?? false)
                {
                    _serialPort.Close();
                    //log.Error($"已关闭串口 {PortName}");
                }
            }
            catch (Exception ex) {
                log.Error("Disconnect: " + ex.ToString());
            }
        }

        // 发送AT指令并获取响应
        public string SendAtCommand(string command, int timeout = 1000)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("串口未连接，请先调用Connect()方法");
            }

            bool isEventAttached = false;
            EventInfo eventInfo = typeof(SerialPort).GetEvent("DataReceived");
            if (eventInfo != null)
            {
                // 获取事件对应的字段（通常事件会有一个对应的委托字段）
                FieldInfo fieldInfo = typeof(SerialPort).GetField(eventInfo.Name, BindingFlags.NonPublic | BindingFlags.Instance);
                if (fieldInfo != null)
                {
                    // 获取当前串口对象的DataReceived事件订阅的委托
                    Delegate del = fieldInfo.GetValue(_serialPort) as Delegate;
                    isEventAttached = del != null;
                }
            }

            // 临时移除异步接收事件，防止数据被截胡
            if (isEventAttached)
            {
                _serialPort.DataReceived -= SerialPort_DataReceived;
            }

            try
            {
                // 清空缓冲区（清除历史数据）
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();

                // 格式化指令（确保以\r\n结尾）
                string formattedCommand = command.EndsWith("\r\n") ? command : command + "\r\n";
                //log.Error($"发送指令: {formattedCommand.Trim()}");
                _serialPort.Write(formattedCommand);

                // 等待响应（根据指令类型调整超时）
                Thread.Sleep(timeout);
                // 读取响应（此时无异步事件干扰，能完整获取数据）
                string response = _serialPort.ReadExisting();

                //log.Error($"收到响应:\n{response}");
                return response;
            }
            catch (TimeoutException)
            {
                log.Error("读取响应超时");
                return null;
            }
            catch (Exception ex)
            {
                log.Error($"发送指令时出错: {ex.Message}");
                return null;
            }
            finally
            {
                // 恢复异步接收事件（即使出错也会执行）
                if (isEventAttached)
                {
                    _serialPort.DataReceived += SerialPort_DataReceived;
                }
            }
        }

        public bool SendFunctionLevelCommand(int funLevel, int timeout = 3000)
        {
            try
            {
                string command = $"AT+CFUN={funLevel}";
                log.Warn($"发送模块功能级别切换指令: {command}");

                string response = SendAtCommand(command, timeout);
                if (!string.IsNullOrEmpty(response) &&
                    response.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    log.Warn($"模块功能级别切换成功: {command}, response={response}");
                    return true;
                }

                log.Warn($"模块功能级别切换失败: {command}, response={response ?? "空"}");
                return false;
            }
            catch (Exception ex)
            {
                log.Error($"SendFunctionLevelCommand失败, funLevel={funLevel}: {ex}");
                return false;
            }
        }

        public bool SendCfun0Command(int timeout = 3000)
        {
            return SendFunctionLevelCommand(0, timeout);
        }

        public bool SendCfun1Command(int timeout = 3000)
        {
            return SendFunctionLevelCommand(1, timeout);
        }

        public string QueryQcereg(int timeout = 2000)
        {
            return SendAtCommand("AT+CEREG?", timeout);
        }

        public bool IsQceregRegistered(out string response)
        {
            response = QueryQcereg(2000);
            if (string.IsNullOrWhiteSpace(response))
                return false;

            return response.IndexOf(",1", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   response.IndexOf(",5", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 信号质量信息模型
        /// </summary>
        public class SignalQualityInfo
        {
            /// <summary>
            /// 信号强度指示(RSSI)：0-31（31为最强），99表示未知
            /// </summary>
            public int Rssi { get; set; }
            /// <summary>
            /// 参考信号接收功率(RSRP)：-140到-44（数值越大信号越强），127表示未知
            /// </summary>
            public int Rsrp { get; set; }
            /// <summary>
            /// 信号与干扰加噪声比(SINR)：-23到40（数值越大信号质量越好），127表示未知
            /// </summary>
            public int Sinr { get; set; }
            /// <summary>
            /// 参考信号接收质量(RSRQ)：-19.5到-3（数值越大质量越好），127表示未知
            /// </summary>
            public double Rsrq { get; set; }
            /// <summary>
            /// 解析是否成功
            /// </summary>
            public bool IsValid { get; set; }
            /// <summary>
            /// 错误信息（解析失败时）
            /// </summary>
            public string ErrorMessage { get; set; }
        }

        /// <summary>
        /// 发送AT+QCSQ命令获取信号质量信息
        /// </summary>
        /// <returns>包含RSSI、RSRP、SINR、RSRQ的信号质量对象</returns>
        public SignalQualityInfo GetSignalQuality()
        {
            var result = new SignalQualityInfo();

            try
            {
                if (!IsConnected)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "串口未连接，请先调用Connect()方法";
                    return result;
                }

                // 发送AT+QCSQ命令，延长超时时间确保获取完整响应
                string response = SendAtCommand("AT+QCSQ", 5000);

                if (string.IsNullOrEmpty(response))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "未收到响应";
                    return result;
                }

                // 解析响应，典型响应格式：+QCSQ: "LTE",18,-65,30,-7
                // 格式说明：+QCSQ: <sysmode>,<rssi>,<rsrp>,<sinr>,<rsrq>
                string[] responseLines = response.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                string qcsqLine = responseLines.FirstOrDefault(line => line.StartsWith("+QCSQ:"));

                //log.Error("响应报文:" + qcsqLine);

                if (string.IsNullOrEmpty(qcsqLine))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "未找到信号质量数据";
                    return result;
                }

                // 提取数值部分（去除"+QCSQ: "前缀和引号）
                string dataPart = qcsqLine.Replace("+QCSQ: ", "").Trim();
                dataPart = System.Text.RegularExpressions.Regex.Replace(dataPart, "\"[^\"]*\"", "").Trim(); // 移除模式名称（如"LTE"）
                string[] signalValues = dataPart.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                 .Select(v => v.Trim())
                                                 .ToArray();

                // 验证解析结果
                if (signalValues.Length < 4)
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"响应格式不正确，预期至少4个参数，实际：{signalValues.Length}";
                    return result;
                }

                // 解析各参数值
                if (int.TryParse(signalValues[0], out int rssi))
                    result.Rssi = -rssi;
                else
                    result.ErrorMessage += "RSSI解析失败; ";

                if (int.TryParse(signalValues[1], out int rsrp))
                    result.Rsrp = rsrp;
                else
                    result.ErrorMessage += "RSRP解析失败; ";

                if (int.TryParse(signalValues[2], out int sinr))
                    result.Sinr = Convert.ToInt32(sinr*0.1);
                else
                    result.ErrorMessage += "SINR解析失败; ";

                // RSRQ可能为小数（如-7.5）
                if (double.TryParse(signalValues[3], out double rsrq))
                    result.Rsrq = rsrq;
                else
                    result.ErrorMessage += "RSRQ解析失败; ";

                // 判断整体解析是否成功
                result.IsValid = string.IsNullOrEmpty(result.ErrorMessage);
                if (!result.IsValid)
                    result.ErrorMessage = "解析错误: " + result.ErrorMessage.TrimEnd(';', ' ');

                return result;
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"获取信号质量时出错: {ex.Message}";
                return result;
            }
        }

        // 使用示例（可添加到类中作为测试方法）
        public SignalQualityInfo TestSignalQuality()
        {
            // 初始化默认结果（即使失败也能返回包含错误信息的对象）
            var result = new SignalQualityInfo
            {
                IsValid = false,
                ErrorMessage = "未知错误"
            };

            // 标记是否成功打开连接，用于后续确保断开
            bool isConnected = false;

            try
            {
                // 尝试连接
                isConnected = Connect();
                if (!isConnected)
                {
                    result.ErrorMessage = "串口连接失败";
                    log.Error(result.ErrorMessage);
                    return result;
                }

                // 连接成功，获取信号质量
                var signalInfo = GetSignalQuality();
                if (signalInfo.IsValid)
                {
/*                    log.Error("信号质量信息:");
                    log.Error($"RSSI: {signalInfo.Rssi}");
                    log.Error($"RSRP: {signalInfo.Rsrp} dBm");
                    log.Error($"SINR: {signalInfo.Sinr} dB");
                    log.Error($"RSRP: {signalInfo.Rsrq} dB");*/
                    return signalInfo; // 返回有效结果
                }
                else
                {
                    result.ErrorMessage = $"获取信号质量失败: {signalInfo.ErrorMessage}";
                    log.Error(result.ErrorMessage);
                    return result;
                }
            }
            catch (Exception ex)
            {
                // 捕获所有异常，避免程序崩溃
                result.ErrorMessage = $"执行过程中发生异常: {ex.Message}";
                log.Error(result.ErrorMessage);
                return result;
            }
        }

        // 实现IDisposable接口
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            try
            {
                if (_isDisposed) return;

                if (disposing)
                {
                    // 释放托管资源
                    Disconnect();
                    _serialPort?.Dispose();
                }

                _isDisposed = true;
            }
            catch (Exception ex) {
                log.Error("Dispose: " + ex.ToString());
            }
        }

        ~EC20Communicator()
        {
            Dispose(false);
        }
    }
}