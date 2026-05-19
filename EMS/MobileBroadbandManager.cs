using System;
using log4net;

namespace EMS
{
    /// <summary>
    /// 移动宽带连接管理类，提供连接状态查询和 MBN 连接能力。
    /// </summary>
    public class MobileBroadbandManager
    {
        private readonly string _connectionName;
        private readonly string _profileName;

        private static ILog log = LogManager.GetLogger("MobileBroadbandManager");


        /// <summary>
        /// 初始化移动宽带管理类
        /// </summary>
        /// <param name="connectionName">移动宽带连接名称，默认为"移动宽带连接"</param>
        /// <param name="profileName">移动宽带配置名称，默认为"中国电信 4"</param>
        public MobileBroadbandManager(string connectionName = "移动宽带连接", string profileName = "中国电信 4")
        {
            if (string.IsNullOrWhiteSpace(connectionName))
                throw new ArgumentException("连接名称不能为空", nameof(connectionName));

            if (string.IsNullOrWhiteSpace(profileName))
                throw new ArgumentException("配置名称不能为空", nameof(profileName));

            _connectionName = connectionName;
            _profileName = profileName;
        }

        public bool ConnectNet()
        {
            try
            {
                var connectSuccess = ExecuteCommand($"netsh mbn connect interface=\"{_connectionName}\" connmode=name name=\"{_profileName}\"");
                return connectSuccess;
            }
            catch (Exception ex)
            {
                log.Error($"连接移动宽带时发生错误: {ex.Message}");
                return false;
            }
        }

        public bool IsNetEnabled()
        {
            var state = GetConnectionState();
            return state.Exists && state.Connected;
        }

        private (bool Exists, bool Connected) GetConnectionState()
        {
            try
            {
                string command = "netsh mbn show interfaces";
                string cmdPath = SafeProcessRunner.GetPreferredCmdPath();

                log.Warn($"GetConnectionState: Is64BitProcess={Environment.Is64BitProcess}, Is64BitOperatingSystem={Environment.Is64BitOperatingSystem}");
                log.Warn($"GetConnectionState: cmdPath={cmdPath}");
                log.Warn($"GetConnectionState: command={command}");

                var result = SafeProcessRunner.RunCmd(
                    command,
                    timeoutMs: 5000
                );

                if (result == null)
                {
                    log.Warn("GetConnectionState: SafeProcessRunner.Run 返回 result=null");
                    return (false, false);
                }

                var output = result.StandardOutput ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(output))
                {
                    log.Warn($"移动宽带连接状态查询输出: {output}");
                }
                else
                {
                    log.Warn("移动宽带连接状态查询输出为空");
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
                    log.Warn($"未找到移动宽带连接: {_connectionName}");
                    return (false, false);
                }

                if (string.IsNullOrWhiteSpace(currentState))
                {
                    log.Warn($"找到移动宽带连接但未读取到状态: {_connectionName}");
                    return (true, false);
                }

                log.Warn($"移动宽带连接状态: Name={_connectionName}, State={currentState}");

                bool connected = string.Equals(currentState, "已连接", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(currentState, "Connected", StringComparison.OrdinalIgnoreCase);

                return (true, connected);
            }
            catch (TimeoutException ex)
            {
                log.Error($"查询移动宽带连接状态超时: {_connectionName}, 错误: {ex.Message}");
                return (false, false);
            }
            catch (Exception ex)
            {
                log.Error($"查询移动宽带连接状态失败: {_connectionName}, 错误: {ex.Message}", ex);
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