using System;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
using Squirrel;
using System.Threading.Tasks;
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

        private static ILog log = LogManager.GetLogger("Program");
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


            try
            {
                //if (!CheckAppExists())  注意这个函数使用会导致EMS生成快捷方式无法启动程序，可能与快捷方式会生成EMS.exe同名启动程序
                {
                    // 定义要执行的命令
                    string[] commands = new string[2];

                    commands[0] = "netsh interface ip set dns name=\"移动宽带连接\" source=static addr=223.5.5.5 register=primary";
                    commands[1] = "netsh interface ip add dns name=\"移动宽带连接\" addr=223.6.6.6 index=2";

                    for (int i = 0; i < 2; ++i)
                    {
                        // 创建 ProcessStartInfo 对象，并配置其属性
                        ProcessStartInfo processStartInfo = new ProcessStartInfo("cmd", "/c " + commands[i])
                        {
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        // 创建并启动进程
                        // 启动进程
                        using (Process process = Process.Start(processStartInfo))
                        {
                            // 等待进程退出
                            process.WaitForExit();
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

        public static void RestartDevice()
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
        }


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
        //判断是否重复打开
        public static bool CheckAppExists()
        {
            string name = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            System.Diagnostics.Process[] myProcesses = System.Diagnostics.Process.GetProcessesByName(name);//获取指定的进程名
              
            if (myProcesses.Length > 1) //如果可以获取到知道的进程名则说明已经启动
            {
                //MessageBox.Show("程序已启动！");
                Process[] process = Process.GetProcessesByName(name);//在所有已启动的进程中查找需要的进程；
                if (process.Length > 0)//如果查找到
                {
                    //IntPtr handle = process[0].MainWindowHandle;
                    IntPtr hWnd = process[1].MainWindowHandle; 
                   // wWindowAsync(hWnd, 9);// 9就是SW_RESTORE标志，表示还原窗体
                    SendMessage(hWnd, WM_SYSCOMMAND, SC_NOMAL, 0);
                    SetForegroundWindow(hWnd);
                }
                Application.Exit();//关闭系统
                return true;
            }
            return false;
        }

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
    }

} 
