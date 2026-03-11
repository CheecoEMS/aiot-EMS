using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;


namespace EMS
{
    public static class SafeProcessRunner
    {
        #region ===== Win32 ErrorMode =====

        [DllImport("kernel32.dll")]
        static extern uint SetErrorMode(uint uMode);

        const uint SEM_FAILCRITICALERRORS = 0x0001;
        const uint SEM_NOGPFAULTERRORBOX = 0x0002;
        const uint SEM_NOOPENFILEERRORBOX = 0x8000;

        static SafeProcessRunner()
        {
            // 保留原有模式（重要）
            uint old = SetErrorMode(0);
            SetErrorMode(old |
                SEM_FAILCRITICALERRORS |
                SEM_NOGPFAULTERRORBOX |
                SEM_NOOPENFILEERRORBOX);
        }

        #endregion

        #region ===== Win32 Window APIs =====

        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        const int WM_CLOSE = 0x0010;

        #endregion

        #region ===== Public API =====

        /// <summary>
        /// 安全运行外部进程（防弹窗 + 超时）
        /// </summary>
        public static ProcessResult Run(
            string fileName,
            string arguments,
            int timeoutMs = 30_000,
            bool enableWatchdog = true)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = psi })
            {
                process.Start();

                PopupWatchdog watchdog = null;
                if (enableWatchdog)
                {
                    watchdog = new PopupWatchdog(process.Id);
                }

                string stdout = "";
                string stderr = "";

                // 后台读取，防止缓冲区阻塞（兼容旧框架）
                var outThread = new Thread(() => stdout = process.StandardOutput.ReadToEnd());
                var errThread = new Thread(() => stderr = process.StandardError.ReadToEnd());
                outThread.Start();
                errThread.Start();

                bool exited = process.WaitForExit(timeoutMs);

                if (!exited)
                {
                    try { process.Kill(); } catch { }
                    watchdog?.Dispose();
                    throw new TimeoutException("进程执行超时");
                }

                outThread.Join(2000);
                errThread.Join(2000);

                watchdog?.Dispose();

                return new ProcessResult
                {
                    ExitCode = process.ExitCode,
                    StandardOutput = stdout,
                    StandardError = stderr
                };
            }
        }

        #endregion

        #region ===== Watchdog =====

        class PopupWatchdog : IDisposable
        {
            readonly int _pid;
            readonly Thread _thread;
            volatile bool _stop;

            public PopupWatchdog(int pid)
            {
                _pid = pid;
                _thread = new Thread(Watch)
                {
                    IsBackground = true
                };
                _thread.Start();
            }

            void Watch()
            {
                while (!_stop)
                {
                    EnumWindows((hWnd, lParam) =>
                    {
                        if (!IsWindowVisible(hWnd))
                            return true;

                        GetWindowThreadProcessId(hWnd, out uint winPid);
                        if (winPid != _pid)
                            return true;

                        var sb = new StringBuilder(256);
                        GetWindowText(hWnd, sb, sb.Capacity);
                        string title = sb.ToString();

                        int hitCount = 0;

                        if (title.Contains("cmd.exe") ||
                            title.Contains("应用程序错误") ||
                            title.Contains("unable to start correctly"))
                        {
                            hitCount++;

                            SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

                            if (hitCount >= 5) // 约 1 秒
                            {
                                try
                                {
                                    Process.GetProcessById(_pid).Kill();
                                }
                                catch { }
                            }
                        }

                        return true;
                    }, IntPtr.Zero);

                    Thread.Sleep(200);
                }
            }

            public void Dispose()
            {
                _stop = true;
                _thread.Join(500);
            }
        }

        #endregion
    }

    /// <summary>
    /// 进程执行结果
    /// </summary>
    public class ProcessResult
    {
        public int ExitCode;
        public string StandardOutput;
        public string StandardError;

        public bool Success => ExitCode == 0;
    }
}
