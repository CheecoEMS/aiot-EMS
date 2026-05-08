using System;
using log4net;

namespace EMS
{
    /// <summary>
    /// 移动宽带连接管理类，提供重启移动宽带连接的功能
    /// </summary>
    public class MobileBroadbandManager
    {
        private readonly string _connectionName;
        private readonly int _waitMilliseconds;

        private static ILog log = LogManager.GetLogger("MobileBroadbandManager");

        /// <summary>
        /// 初始化移动宽带管理类
        /// </summary>
        /// <param name="connectionName">移动宽带连接名称，默认为"移动宽带连接"</param>
        /// <param name="waitMilliseconds">禁用和启用之间的等待时间(毫秒)，默认为10000</param>
        public MobileBroadbandManager(string connectionName = "移动宽带连接", int waitMilliseconds = 10000)
        {
            if (string.IsNullOrWhiteSpace(connectionName))
                throw new ArgumentException("连接名称不能为空", nameof(connectionName));

            if (waitMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(waitMilliseconds), "等待时间不能为负数");

            _connectionName = connectionName;
            _waitMilliseconds = waitMilliseconds;
        }

        public bool DisableNet() {
            try
            {
                // 禁用连接
                var disableSuccess = ExecuteCommand($"netsh interface set interface name=\"{_connectionName}\" admin=disable");
                return disableSuccess;
            }
            catch (Exception ex)
            {
                // 可以在这里添加日志记录
                log.Error($"重启移动宽带时发生错误: {ex.Message}");
                return false;
            }

        }

        public bool EnableNet() {
            try
            {
                // 启用连接
                var enableSuccess = ExecuteCommand($"netsh interface set interface name=\"{_connectionName}\" admin=enable");
                return enableSuccess;
            }
            catch (Exception ex)
            {
                // 可以在这里添加日志记录
                log.Error($"重启移动宽带时发生错误: {ex.Message}");
                return false;
            }
        }

        public bool IsNetEnabled()
        {
            var state = GetInterfaceDisabledState();
            return state.Exists && !state.Disabled;
        }

        public bool IsNetDisabled()
        {
            var state = GetInterfaceDisabledState();
            return state.Exists && state.Disabled;
        }

        private (bool Exists, bool Disabled) GetInterfaceDisabledState()
        {
            try
            {
                string command = "netsh mbn show interfaces";
                string cmdPath = SafeProcessRunner.GetPreferredCmdPath();

                log.Warn($"GetInterfaceDisabledState: Is64BitProcess={Environment.Is64BitProcess}, Is64BitOperatingSystem={Environment.Is64BitOperatingSystem}");
                log.Warn($"GetInterfaceDisabledState: cmdPath={cmdPath}");
                log.Warn($"GetInterfaceDisabledState: command={command}");

                var result = SafeProcessRunner.RunCmd(
                    command,
                    timeoutMs: 5000
                );

                if (result == null)
                {
                    log.Warn("GetInterfaceDisabledState: SafeProcessRunner.Run 返回 result=null");
                    return (false, false);
                }

                var output = result.StandardOutput ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(output))
                {
                    log.Warn($"移动宽带接口状态查询输出: {output}");
                }
                else
                {
                    log.Warn("移动宽带接口状态查询输出为空");
                    return (false, false);
                }

                string currentState = null;
                bool foundTargetName = false;

                foreach (var rawLine in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var line = rawLine.Trim();
                    if (line.Length == 0)
                        continue;

                    if (!foundTargetName)
                    {
                        if (line.StartsWith("名称", StringComparison.OrdinalIgnoreCase) ||
                            line.StartsWith("Name", StringComparison.OrdinalIgnoreCase))
                        {
                            int colonIndex = line.IndexOf(':');
                            int chineseColonIndex = line.IndexOf('：');
                            int splitIndex = colonIndex >= 0 ? colonIndex : chineseColonIndex;
                            if (splitIndex < 0)
                                continue;

                            string value = line.Substring(splitIndex + 1).Trim();
                            foundTargetName = string.Equals(value, _connectionName, StringComparison.OrdinalIgnoreCase);
                        }

                        continue;
                    }

                    if (line.StartsWith("状态", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("State", StringComparison.OrdinalIgnoreCase))
                    {
                        int colonIndex = line.IndexOf(':');
                        int chineseColonIndex = line.IndexOf('：');
                        int splitIndex = colonIndex >= 0 ? colonIndex : chineseColonIndex;
                        if (splitIndex < 0)
                            continue;

                        currentState = line.Substring(splitIndex + 1).Trim();
                        break;
                    }
                }

                if (!foundTargetName)
                {
                    log.Warn($"未找到移动宽带接口: {_connectionName}");
                    return (false, false);
                }

                if (string.IsNullOrWhiteSpace(currentState))
                {
                    log.Warn($"找到移动宽带接口但未读取到状态: {_connectionName}");
                    return (true, false);
                }

                log.Warn($"移动宽带接口状态: Name={_connectionName}, State={currentState}");

                bool disabled = !string.Equals(currentState, "已连接", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(currentState, "Connected", StringComparison.OrdinalIgnoreCase);

                return (true, disabled);
            }
            catch (TimeoutException ex)
            {
                log.Error($"查询移动宽带接口状态超时: {_connectionName}, 错误: {ex.Message}");
                return (false, false);
            }
            catch (Exception ex)
            {
                log.Error($"查询移动宽带接口状态失败: {_connectionName}, 错误: {ex.Message}", ex);
                return (false, false);
            }
        }

        /// <summary>
        /// 执行命令并返回执行结果
        /// </summary>
        /// <param name="command">要执行的命令</param>
        /// <returns>命令是否执行成功</returns>
        ///
        private bool ExecuteCommand(string command)
        {
            try
            {
                var result = SafeProcessRunner.RunCmd(
                    command,
                    timeoutMs: 2000
                );

                if (result.Success)
                {
                    log.Warn($"命令执行完成: {command}, ExitCode: {result.ExitCode}");

                    if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                    {
                        log.Warn($"命令输出: {result.StandardOutput}");
                    }

                    return true;
                }
                else
                {
                    log.Warn($"命令执行失败: {command}, ExitCode: {result.ExitCode}");

                    if (!string.IsNullOrWhiteSpace(result.StandardError))
                    {
                        log.Warn($"错误输出: {result.StandardError}");
                    }

                    return false;
                }
            }
            catch (TimeoutException ex)
            {
                log.Error($"命令执行超时: {command}, 错误: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                log.Error($"执行命令失败: {command}, 错误: {ex.Message}", ex);
                return false;
            }
        }
    }
}