using System;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
using Squirrel;
using log4net;


namespace EMS
{
    static class Program
    {

        [DllImport("kernel32.dll")]
        static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

        //Get the handler of current thread
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentThread();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
        // 消息函数
        [DllImport("user32.dll", EntryPoint = "PostMessageA")]
        public static extern bool PostMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string strclassName, string strWindowName);
        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("Dbghelp.dll")]
        public static extern bool MiniDumpWriteDump(IntPtr hProcess, uint ProcessId, IntPtr hFile, int DumpType, IntPtr ExceptionParam, IntPtr UserStreamParam, IntPtr CallbackParam);

        public const int WM_SYSCOMMAND = 0x0112;
        public const int SC_MAXIMIZE = 0xF030;//窗体最大化消息
        public const int SC_NOMAL = 0xF120;//窗体还原消息
        private const int MAX_RETRY = 3;
        private const string SingleInstanceMutexName = @"Global\EMS_SingleInstance_Mutex";

        private static readonly ILog log = LogManager.GetLogger("Program");
        private static Mutex singleInstanceMutex;
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static async Task Main()
        {
            /*            try
                        {
                            // 配置远程更新的 URL，指向包含 RELEASES 文件和 .nupkg 文件的服务器路径
                            using (var mgr = new UpdateManager("https://aiot-data-ems.oss-cn-shanghai.aliyuncs.com/EMS/v1.0.0"))
                            {
                                //await mgr.UpdateApp();

                                var updateInfo = await mgr.CheckForUpdate();
                                if (updateInfo.ReleasesToApply.Count > 0)
                                {
                                    // 下载和应用更新
                                    //await mgr.UpdateApp();
                                    mgr.UpdateApp().GetAwaiter().GetResult();

                                    // 应用完成后重启应用以加载新版本
                                    UpdateManager.RestartAppWhenExited();
                                    return;  // 停止当前应用，等待重启
                                }


                            }
                        }
                        catch (Exception ex)
                        {
                            log.Error("远程更新失败：" + ex.Message);
                        }*/

            bool createdNew = false;

            try
            {
                singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out createdNew);
                if (!createdNew)
                {
                    //log.Error("检测到 EMS 已在运行，当前实例不再启动。");
                    //ActivateExistingInstance();
                    return;
                }

                //测试windows异常弹窗不会阻塞主进程：超时+看门狗+窗口消灭
                //RunVerificationTest();

                    {
                        // 定义要执行的命令
                        string[] commands = new string[3];

                        commands[0] = "netsh interface ip set dns name=\"移动宽带连接\" source=static addr=223.5.5.5 register=primary";
                        commands[1] = "netsh interface ip add dns name=\"移动宽带连接\" addr=223.6.6.6 index=2";
                        //commands[2] = "netsh interface ipv6 set interface name=\"移动宽带连接\" admin=disabled";

                        for (int i = 0; i < 2; ++i)
                    {
                        try
                        {
                            // 使用 SafeProcessRunner 执行命令
                            var result = SafeProcessRunner.Run("cmd", $"/c {commands[i]}", timeoutMs: 2000);

                            if (result.Success)
                            {
                                log.Info($"命令执行完成: {commands[i]}, ExitCode: {result.ExitCode}");
                                if (!string.IsNullOrEmpty(result.StandardOutput))
                                {
                                    log.Info($"命令输出: {result.StandardOutput}");
                                }
                            }
                            else
                            {
                                log.Error($"命令执行失败: {commands[i]}, ExitCode: {result.ExitCode}");
                                if (!string.IsNullOrEmpty(result.StandardError))
                                {
                                    log.Error($"错误输出: {result.StandardError}");
                                }
                            }
                        }
                        catch (TimeoutException ex)
                        {
                            log.Error($"命令执行超时: {commands[i]}, 错误: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            log.Error($"执行命令失败: {commands[i]}, 错误: {ex.Message}");
                        }
                    }



                    //启动EMS主程序
                    Application.EnableVisualStyles();

                    frmFlash.ShowFlashForm();
                    frmFlash.AddPostion(10);
                    frmMain.Selffrm = new frmMain();
                    // 创建并初始化主窗体
                    for (int i = 0; i < MAX_RETRY; i++)
                    {
                        if (frmMain.Selffrm != null)
                        {
                            if (frmMain.Selffrm.Initialize())
                            {
                                Application.Run(frmMain.Selffrm);
                                break;
                            }
                            else
                            {
                                //Initilize初始化失败，重试
                                log.Error("Initilize初始化失败，重试");
                                RestartApplication();
                            }
                        }
                        else
                        {
                            log.Error("frmMain.Selffrm创建失败");
                            frmMain.Selffrm = new frmMain();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"应用程序的主入口点发生错误: {ex.Message}");
            }
            finally
            {
                if (createdNew && singleInstanceMutex != null)
                {
                    singleInstanceMutex.ReleaseMutex();
                    singleInstanceMutex.Dispose();
                    singleInstanceMutex = null;
                }
            }


/*            if (! CheckAppExists()) 
            { 
                frmFlash.ShowFlashForm();

*//*                //自动保存dump文件
                string crashDumpFolder = @"C:\crashdump"; // 设置 crashdump 文件夹路径
                string strSysPath = Convert.ToString(System.AppDomain.CurrentDomain.BaseDirectory);
                if (!Directory.Exists(crashDumpFolder))
                {
                    Directory.CreateDirectory(crashDumpFolder);
                }
                StartCrashMonitor(crashDumpFolder);*//*

                frmFlash.AddPostion(10);

                try
                {
                    Application.EnableVisualStyles();
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }
                frmMain.Selffrm = new frmMain();
                
                Application.Run(frmMain.Selffrm);
*//*                Application.Exit();
                Application.ExitThread(); *//*
            }*/
        }

/*        public static void RestartDevice()
        {
            try
            {
                // 先尝试正常关闭当前应用程序
                frmSet.PowerGPIO(0);

                // 使用Process启动cmd执行关机命令，/r表示重启，/t 0表示立即执行
                ProcessStartInfo psi = new ProcessStartInfo("shutdown", "/r /t 0");
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;

                Process.Start(psi);
                log.Info("设备重启命令已发送");
            }
            catch (Exception ex)
            {
                log.Error("发送设备重启命令失败: " + ex.Message);
            }
        }*/


        public static void RestartApplicationWithoutCount()
        {
            try
            {
                frmSet.PowerGPIO(0);
                string exePath = AppDomain.CurrentDomain.BaseDirectory + "\\EMS.exe";
                try
                {
                    Process.Start(exePath);
                }
                catch (Exception ex)
                {
                    log.Error("无法重启应用程序: " + ex.Message);
                }

                // 退出当前进程  
                Environment.Exit(0);

            }
            catch (Exception ex)
            {
                log.Error("RestartApplication: " + ex.Message);
            }
        }


        public static void RestartApplication()
        {
            try
            {
                if (frmSet.historyDatas != null &&  frmSet.historyDatas.RebootCount > 0)
                {
                    frmSet.historyDatas.RebootCount--;

                    frmSet.PowerGPIO(0);
                    frmSet.Set_HistoryData();

                    string exePath = AppDomain.CurrentDomain.BaseDirectory + "\\EMS.exe";
                    try
                    {
                        Process.Start(exePath);
                    }
                    catch (Exception ex)
                    {
                        log.Error("无法重启应用程序: " + ex.Message);
                    }

                    // 退出当前进程  
                    Environment.Exit(0);
                }
                else
                {
                    log.Error("重启失败，FrmSet未初始化或重启次数耗尽");
                }
            }
            catch (Exception ex)
            {
                log.Error("RestartApplication: " + ex.Message);
            }
        }


        static void StartCrashMonitor(string crashDumpFolder)
        {
            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = crashDumpFolder;
            watcher.Filter = "*.dmp";
            watcher.EnableRaisingEvents = true;
            watcher.Created += (sender, e) =>
            {
                // .dmp 文件创建时，移动到指定的文件夹
                string destinationFolder = @"C:\crashdump";
                if (!Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                string sourceFile = e.FullPath;
                string destinationFile = Path.Combine(destinationFolder, Path.GetFileName(e.FullPath));
                File.Move(sourceFile, destinationFile);
                Console.WriteLine("Moved crash dump file to: " + destinationFile);
            };
        }
        // 激活已经运行的 EMS 实例窗口
/*        public static void ActivateExistingInstance()
        {
            try
            {
                string currentProcessName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
                string currentExePath = Path.GetFullPath(Application.ExecutablePath);
                int currentProcessId = Process.GetCurrentProcess().Id;

                Process[] processes = Process.GetProcessesByName(currentProcessName);
                foreach (Process process in processes)
                {
                    try
                    {
                        if (process.Id == currentProcessId)
                        {
                            continue;
                        }

                        string processPath = Path.GetFullPath(process.MainModule.FileName);
                        if (!string.Equals(processPath, currentExePath, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        IntPtr hWnd = process.MainWindowHandle;
                        if (hWnd == IntPtr.Zero)
                        {
                            continue;
                        }

                        if (IsIconic(hWnd))
                        {
                            ShowWindowAsync(hWnd, 9);
                        }
                        else
                        {
                            SendMessage(hWnd, WM_SYSCOMMAND, SC_NOMAL, 0);
                        }

                        SetForegroundWindow(hWnd);
                        return;
                    }
                    catch (Exception ex)
                    {
                        log.Error($"激活已有进程窗口失败，进程ID: {process.Id}, 错误: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"ActivateExistingInstance 执行失败: {ex.Message}");
            }
        }*/

        /*        static ulong SetCpuID(int lpIdx)
                {
                    ulong cpuLogicalProcessorId = 0;
                    if (lpIdx < 0 || lpIdx >= System.Environment.ProcessorCount)
                    {
                        lpIdx = 0;
                    }
                    cpuLogicalProcessorId |= 1UL << lpIdx;
                    return cpuLogicalProcessorId;
                }*/

        public static void RunVerificationTest()
        {
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Crash");
            string crashTesterPath = Path.Combine(basePath, "cmd.exe");

            if (!File.Exists(crashTesterPath))
            {
                Console.WriteLine($"[错误] 找不到测试程序：{crashTesterPath}");
                Console.WriteLine("请先编译 CrashTest 项目并复制到 Crash 文件夹。");
                return;
            }

            Console.WriteLine("==========================================");
            Console.WriteLine("阶段 1: 验证旧代码 (原生 Process) 会被阻塞");
            Console.WriteLine("==========================================");
            Console.WriteLine("【警告】接下来启动的进程如果弹出窗口，主程序将卡死！");
            Console.WriteLine("【操作】请不要点击弹窗，观察主程序日志是否停止滚动。");
            Console.WriteLine("【操作】若要继续测试阶段 2，请手动关闭弹窗 或 在任务管理器杀死本主进程后重新运行（跳过阶段 1）。");
            Console.WriteLine("3 秒后开始...");
            Thread.Sleep(3000);

            // ==========================================
            // 第一部分：旧代码 (原生 Process) - 预期会卡死
            // ==========================================
            try
            {
                Console.WriteLine("\n>>> [旧代码] 启动 CrashTest.exe (无超时保护)...");

                ProcessStartInfo psi = new ProcessStartInfo(crashTesterPath)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process process = Process.Start(psi))
                {
                    Console.WriteLine(">>> [旧代码] 进程已启动，正在等待退出 (WaitForExit)...");
                    Console.WriteLine(">>> [状态] 如果此时出现弹窗，下一行日志将永远不会打印！");

                    // 【关键点】这里没有超时时间，它会永远等待，直到弹窗被手动关闭
                    process.WaitForExit();

                    Console.WriteLine(">>> [旧代码] 进程已退出。 (如果你看到了这行，说明你手动关闭了弹窗)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($">>> [旧代码] 发生异常: {ex.Message}");
            }

            Console.WriteLine("\n------------------------------------------");
            Console.WriteLine("阶段 1 结束。如果主程序没死，继续测试阶段 2。");
            Console.WriteLine("现在测试 SafeProcessRunner (带看门狗 + 超时)...");
            Console.WriteLine("------------------------------------------\n");

            // ==========================================
            // 第二部分：新代码 (SafeProcessRunner) - 预期自动恢复
            // ==========================================
            try
            {
                Console.WriteLine(">>> [新代码] 启动 CrashTest.exe (开启看门狗 + 5秒超时)...");

                var result = SafeProcessRunner.Run(
                    fileName: crashTesterPath,
                    arguments: "",
                    timeoutMs: 5000,      // 5秒超时
                    enableWatchdog: true  // 开启看门狗
                );

                Console.WriteLine(">>> [新代码] 成功捕获结果！程序未卡死。");
                Console.WriteLine($">>> [新代码] 退出码: {result.ExitCode}");
                if (result.ExitCode == 0)
                    Console.WriteLine(">>> [结论] 看门狗成功关闭了弹窗，进程正常退出。");
                else
                    Console.WriteLine(">>> [结论] 进程被超时强制杀死或异常退出。");
            }
            catch (TimeoutException)
            {
                Console.WriteLine(">>> [新代码] 捕获到超时异常。");
                Console.WriteLine(">>> [结论] 看门狗未能关闭弹窗（可能是标题不匹配），但超时机制杀死了进程，主程序未卡死。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($">>> [新代码] 发生其他异常: {ex.Message}");
            }

            Console.WriteLine("\n==========================================");
            Console.WriteLine("测试全部完成。主程序依然存活。");
            Console.WriteLine("==========================================");
        }
    }

} 
