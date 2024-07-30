using System;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;

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
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (! CheckAppExists()) 
            { 
                frmFlash.ShowFlashForm();
                //初始化IO口
                //SysIO.GPOIIni();
                //SysIO.SetGPIOState(0, 3);//急停
                //SysIO.SetGPIOState(1, 3);//消防
                //SysIO.SetGPIOState(2, 3);
                //SysIO.SetGPIOState(3, 3);
                //SysIO.SetGPIOState(4, 3);
                //SysIO.SetGPIOState(5, 3);
                //SysIO.SetGPIOState(6, 3);
                //SysIO.SetGPIOState(7, 3);
                ////
                //SysIO.SetGPIOState(8, 1);   //24V on(powerOn)
                //SysIO.SetGPIOState(9, 1);   //PCS On
                //SysIO.SetGPIOState(10, 1);  //2 error
                //SysIO.SetGPIOState(11, 1); //3 error
                ////SysIO.SetGPIOState(12, 1);
                //SysIO.SetGPIOState(15, 1);//EMS LED
                                          //隐藏工具栏 
                                          //SysIO.HideKeyBoard();
                                          //SysIO.HideTaskBar();
                                          //ScreenBackLight.SetPowerOn();

                //frmSet.LoadForm();
                //frmSet.LoadParameter();   

                string crashDumpFolder = @"C:\crashdump"; // 设置 crashdump 文件夹路径
                string strSysPath = Convert.ToString(System.AppDomain.CurrentDomain.BaseDirectory);
                if (!Directory.Exists(crashDumpFolder))
                {
                    Directory.CreateDirectory(crashDumpFolder);
                }
                StartCrashMonitor(crashDumpFolder);
                frmFlash.AddPostion(10);
                //frmMain.Selffrm.TCPserver.TCPServerIni(2404);
                //frmMain.Selffrm.TCPserver.StartMonitor();
                //frmMain printerForm = frmMain.LoadForm(); 
                try
                {
                    Application.EnableVisualStyles();
                    //Application.SetCompatibleTextRenderingDefault(false);
                    // Application.Run(printerForm);
                    //printerForm.Close();

                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }
                frmMain.Selffrm = new frmMain();

                //Thread.Sleep(1000);
                //SysIO.SetGPIOState(15, 1);//Power on LED
                //Thread.Sleep(1000);
                //SysIO.SetGPIOState(15, 0);//Power on LED
                
                Application.Run(frmMain.Selffrm);
                //SysIO.SetGPIOState(15, 1);//Power on LED
                Application.Exit();
                Application.ExitThread(); 
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
