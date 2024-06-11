using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;


//using Microsoft.WindowsAPICodePack.ApplicationServices; 
//  PowerManager.IsMonitorOnChanged += new EventHandler(MonitorOnChanged);
//void MonitorOnChanged(object sender, EventArgs e)
//{
//    settings.MonitorOn = PowerManager.IsMonitorOn;
//    AddEventMessage(string.Format("Monitor status changed (new status: {0})", PowerManager.IsMonitorOn ? "On" : "Off"));
//}


namespace EMS
{

    class SysIO
    {
        private const string strDriveDllName = "SpesTechDriverControl.dll";
        private const string strExeDllName = "SpesTechMmioRW.dll";

        [DllImport(strExeDllName)] //uint IntPtr
        public static extern bool SetPhysLong(IntPtr hDriver, UInt32 pbPhysAddr, UInt32 dwPhysVal);
        [DllImport(strExeDllName)]
        public static extern bool GetPhysLong(IntPtr hDriver, UInt32 pbPhysAddr, out UInt32 pdwPhysVal);

        [DllImport(strDriveDllName)]
        public static extern IntPtr InitializeWinIo();
        [DllImport(strDriveDllName)]
        public static extern bool ShutdownWinIo(IntPtr hDriver);

        static private IntPtr hDriver;
        //public static UInt32[] GPOIAddr ={
        //    0xFED0E178,//消防
        //    0xFED0E278,//急停
        //    0xFED0E1C8,
        //    0xFED0E1B8,
        //    0xFED0E168,
        //    0xFED0E158,//UPS反馈：3:正常 2：故障
        //    0xFED0E188,//市电 ： 3：正常 2：故障
        //    0xFED0E198,
        //    //
        //    0xFED0E388,
        //    0xFED0E368,
        //    0xFED0E318,
        //    0xFED0E378,//蜂鸣器故障灯
        //    0xFED0E308,
        //    0xFED0E398,
        //    0xFED0E328,
        //    0xFED0E3A8,
        //};

        /// <summary>
        /// 初始化GPIO前8个为输入，后8个输出
        /// </summary>
        //public static void GPOIIni()
        //{
        //    if (hDriver == IntPtr.Zero)
        //    {
        //        IntPtr hDriver = InitializeWinIo();//打开 //初始化dll和driver
        //        //frmMain.ShowDebugMSG("GPIO初始化失败"); 
        //    }
        //    if (hDriver == IntPtr.Zero)
        //    {
        //        return;
        //    }
        //        try
        //    {
        //        //前8个为输入，后8个输出 02out,0200 0201
        //        ////01in   --0100   0102
        //        //配置成输入：BIT[2, 1, 0] = Value[0, 1, x]   = 2
        //        //配置成输出高：BIT[2, 1, 0] = Value[0, 0, 1] = 1
        //        //配置成输出低：BIT[2, 1, 0] = Value[0, 0, 0] = 0
        //        SetPhysLong(hDriver, GPOIAddr[0], 2);//写入  消防出点
        //        SetPhysLong(hDriver, GPOIAddr[1], 2);//写入  紧急停机
        //        SetPhysLong(hDriver, GPOIAddr[2], 2);//写入  预留 断路器
        //        SetPhysLong(hDriver, GPOIAddr[3], 2);//写入  预留 断路器
        //        SetPhysLong(hDriver, GPOIAddr[4], 2);//写入  预留 门禁系统
        //        SetPhysLong(hDriver, GPOIAddr[5], 3);//写入  UPS
        //        SetPhysLong(hDriver, GPOIAddr[6], 3);//写入  市电
        //        SetPhysLong(hDriver, GPOIAddr[7], 2);//写入 
        //        ////////////////////////////////////////////////////////
        //        //输出
        //        SetPhysLong(hDriver, GPOIAddr[8], 0);//写出  电源指示灯
        //        SetPhysLong(hDriver, GPOIAddr[9], 1);//写出  运行指示灯，充放电点亮 
        //        SetPhysLong(hDriver, GPOIAddr[10], 1);//写出  一般故障1、2级不影响工作
        //        SetPhysLong(hDriver, GPOIAddr[11], 1);//写出  综合控制箱风机控制 
        //        SetPhysLong(hDriver, GPOIAddr[12], 1);//输出 
        //        SetPhysLong(hDriver, GPOIAddr[13], 1);//输出  
        //        SetPhysLong(hDriver, GPOIAddr[14], 1);//输出   预留 主动消防控制
        //        SetPhysLong(hDriver, GPOIAddr[15], 1);//输出 严重故障、导致不可恢复的停机 
                
        //    }
        //    catch { }
        //    //ShutdownWinIo(hDriver);//关闭
        //}
        //public static void GPIOClose()
        //{
        //    if (hDriver != IntPtr.Zero)
        //    {
        //        ShutdownWinIo(hDriver);//关闭
        //    }
          
        //}


        /// <summary>
        /// 获取一个GPIO的输入值 ：0输出低电平，1输出高高电平，2输入低电平，3输入高电平
        /// </summary>
        /// <param name="aIndex"></param>
        /// <returns></returns>
        //public static UInt32 GetGPIOState(int aIndex)
        //{
        //    UInt32 uiBack = 0;
        //    //IntPtr hDriver = InitializeWinIo();//打开 //初始化dll和driver

        //    if (hDriver == IntPtr.Zero)
        //    {
        //        IntPtr hDriver = InitializeWinIo();//打开 //初始化dll和driver
        //        //frmMain.ShowDebugMSG("GPIO初始化失败"); 
        //    }
        //    if (hDriver == IntPtr.Zero)
        //    {
        //        return 0;
        //    }
        //    try
        //    {
        //        //if ((aIndex > 15)||(aIndex<0))
        //        //    return uiBack;
        //        //  IntPtr hDriver = InitializeWinIo();//打开 //初始化dll和driver
        //                                           // if (hDriver == IntPtr.Zero)
        //                                           //     bResult= false;

        //        GetPhysLong(hDriver, GPOIAddr[aIndex], out uiBack);//读取 //读取一个byte的值 
        //    }
        //    catch
        //    { }
        //    //ShutdownWinIo(hDriver);//关闭
        //    return uiBack;
        //}

        /// <summary>
        /// 设置gpio的状态0输出低电平，1输出高高电平，2输入低电平，3输入高电平
        /// </summary>
        /// <param name="aIndex"></param>
        /// <param name="aOn"></param>
        /// <returns></returns>
        //public static bool SetGPIOState(int aIndex, ushort aOn)
        //{
        //    bool bResult = true;
        //   // if ((aIndex > 15) || (aIndex < 0))
        //    //    return false;
        //    //IntPtr hDriver = InitializeWinIo();//打开 //初始化dll和driver

        //    if (hDriver == IntPtr.Zero)
        //    {
        //          hDriver = InitializeWinIo();//打开 //初始化dll和driver 
        //    }
        //    if (hDriver == IntPtr.Zero)
        //    {
        //        return false;
        //    }
        //    SetPhysLong(hDriver, GPOIAddr[aIndex], aOn);//设置一个byte的值 
        //  //  ShutdownWinIo(hDriver);//关闭
        //    return bResult;
        //}




        /// <summary>
        /// ////////////////////////////////////////////////////////////////////////////
        /// </summary>
        private const string strDllName = "coredll.dll";

        private const int SC_CLOSE = 0xF060;
        private const uint Sipe_ON = 0x0001; //打开输入面板
        private const uint Sipe_OFF = 0x0000;//关闭输入面板
        private const int SW_HIDE = 0;  //隐藏任务栏
        private const int SW_RESTORE = 9;//显示任务栏
        public const int EWX_LOGOFF = 0x00000000; // 注消 
        public const int EWX_SHUTDOWN = 0x00000001; // 先保存再关机 
        public const int EWX_REBOOT = 0x00000002; // 重启 
        public const int EWX_FORCE = 0x00000004;//终止没响应程序 
        public const int EWX_POWEROFF = 0x00000008;//强制关机 
        public const int EWX_FORCEIFHUNG = 0x00000010;//不保存就关机 
        internal const int SE_PRIVILEGE_ENABLED = 0x00000002;
        internal const int TOKEN_QUERY = 0x00000008;
        internal const int TOKEN_ADJUST_PRIVILEGES = 0x00000020;
        internal const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";

        public enum ShowWindowCommands : int
        {
            SW_HIDE = 0,
            SW_SHOWNORMAL = 1,    //用最近的大小和位置显示，激活
            SW_NORMAL = 2,
            SW_SHOWMINIMIZED = 3,
            SW_SHOWMAXIMIZED = 4,
            SW_MAXIMIZE = 5,
            SW_SHOWNOACTIVATE = 6,
            SW_SHOW = 7,
            SW_MINIMIZE = 8,
            SW_SHOWMINNOACTIVE = 9,
            SW_SHOWNA = 10,
            SW_RESTORE = 11,
            SW_SHOWDEFAULT = 12,
            SW_MAX = 13
        }

        [DllImport("kernel32.dll")]
        public static extern int WinExec(string programPath, int operType);

        /// <summary>
        /// 获取数据中某一位1bit值，bool
        /// </summary>
        /// <param name="aData">传入的数据类型,可换成其它数据类型,比如Int</param>
        /// <param name="aIndex">要获取的第几位的序号,从0开始</param>
        /// <returns>返回值为true表示获取值1;反之为fasle</returns>
        static public bool BoolofInt32(Int32 aData, int aIndex)
        {
            return (aData & ((uint)1 << aIndex)) > 0 ? true : false;
            //左移到最高位
            //int value = input << (sizeof(byte) - 1 - index);
            //右移到最低位
            //value = value >> (sizeof(byte) - 1); 
        }

        /// <summary>
        /// 获取数据中某几位nbit的值
        /// </summary>
        /// <param name="aData">传入的数据类型,可换成其它数据类型,比如Int</param>
        /// <param name="aIndex">要获取的第几位的序号,从0开始</param>
        /// <returns>返回值</returns>     
        static public int ByteofInt32(Int32 aData, int aIndex)
        {
            //左移到最高位
            //int value = aData << (31 - aIndex);
            //右移到最低位
            //value = value >> (31);
            int value = (aData >> aIndex) & 1;
            return value;
        }

        /// <summary>
        /// 将数组改为一个int32数据
        /// </summary>
        /// <param name="aSource"></param>
        static public Int32 Array2Int(byte[] aSource)
        {
            Int32 iResult = 0;
            int ArrayLen = aSource.Length;
            for (int i = ArrayLen - 1; i <= 0; i--)
                iResult += aSource[i] << (ArrayLen - i - 1);
            return iResult;
        }


        [DllImport(strDllName)]
        public static extern int SipShowIM(uint KType);
        public static void ShowKeyBoard()
        {
            SipShowIM(Sipe_ON);
        }

        public static void HideKeyBoard()
        {
            SipShowIM(Sipe_OFF);
        }

        public static void HideTaskBar()
        {
            IntPtr lpClassName = FindWindow("HHTaskBar", null);
            ShowWindow(lpClassName, SW_HIDE); //隐藏任务栏

        }

        public static void ShowTaskBar()
        {//Shell_TrayWnd
            ShowWindow(FindWindow("HHTaskBar", null), SW_RESTORE);
        }

        //This function initializes the DLL.
        [DllImport("SvApiLibx64.dll")] //64位dll库，目标平台需要选择x64或取消勾选首选32位。
        public static extern bool SvApiLibInitialize();

        //This function uninitialize the dll and must be called before terminating the application or in case
        //the DLL is no longer required.
        [DllImport("SvApiLibx64.dll")]
        public static extern bool SvApiLibUnInitialize();//

        //This function reads a BYTE value from the specified I/O port address.
        [DllImport("SvApiLibx64.dll")]
        public static extern bool SvReadIoPortByteEx(ushort port, ref byte value);

        //This function writes a BYTE value to the specified I/O port address
        [DllImport("SvApiLibx64.dll")]
        public static extern bool SvWriteIoPortByteEx(ushort port, byte value);

        public const uint WM_SYSCOMMAND = 0x0112;
        public const uint SC_MONITORPOWER = 0xF170;
        [DllImport("user32")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint wMsg, uint wParam, int lParam);
        [DllImport(strDllName, CharSet = CharSet.Auto)]
        public static extern int MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool BRePaint);

        [DllImport(strDllName, EntryPoint = "FindWindow")]
        public static extern IntPtr FindWindowPtr(string lpClassName, string lpWindowName);

        //[DllImport("User32.dll", EntryPoint = "FindWindowEx")]
        //public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpClassName, string lpWindowName);
        [DllImport(strDllName)]
        public static extern int ShowWindow(IntPtr hwnd, int nCmdShow);
        [DllImport(strDllName)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport(strDllName)]
        public static extern IntPtr FindWindowEx(IntPtr hWnd1, IntPtr hWnd2, string lpsz1, string lpsz2);

        /// <summary>
        /// 自定义的结构
        /// </summary>
        public struct My_lParam
        {
            public int i;
            public string s;
        }
        /// <summary>
        /// 使用COPYDATASTRUCT来传递字符串
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpData;
        }
        //消息发送API  strDllName
        [DllImport("User32.dll", EntryPoint = "SendMessage")]
        public static extern int SendMessage(
            IntPtr hWnd,        // 信息发往的窗口的句柄
           int Msg,            // 消息ID
            int wParam,         // 参数1
            int lParam          //参数2
        );


        //消息发送API
        [DllImport("User32.dll", EntryPoint = "SendMessage")]
        public static extern int SendMessage(
            IntPtr hWnd,        // 信息发往的窗口的句柄
           int Msg,            // 消息ID
            int wParam,         // 参数1
            ref My_lParam lParam //参数2
        );

        //消息发送API
        [DllImport("User32.dll", EntryPoint = "SendMessage")]
        public static extern int SendMessage(
            IntPtr hWnd,        // 信息发往的窗口的句柄
           int Msg,            // 消息ID
            int wParam,         // 参数1
            ref COPYDATASTRUCT lParam  //参数2
        );

        //消息发送API
        [DllImport("User32.dll", EntryPoint = "PostMessage")]
        public static extern int PostMessage(
            IntPtr hWnd,        // 信息发往的窗口的句柄
           int Msg,            // 消息ID
            int wParam,         // 参数1
            int lParam            // 参数2
        );



        //消息发送API
        [DllImport("User32.dll", EntryPoint = "PostMessage")]
        public static extern int PostMessage(
            IntPtr hWnd,        // 信息发往的窗口的句柄
           int Msg,            // 消息ID
            int wParam,         // 参数1
            ref My_lParam lParam //参数2
        );

        //异步消息发送API
        [DllImport("User32.dll", EntryPoint = "PostMessage")]
        public static extern int PostMessage(
            IntPtr hWnd,        // 信息发往的窗口的句柄
           int Msg,            // 消息ID
            int wParam,         // 参数1
            ref COPYDATASTRUCT lParam  // 参数2
        );
        //void CloseLCD(object sender, EventArgs e)
        //{
        //    SendMessage(this.Handle, WM_SYSCOMMAND,
        //    SC_MONITORPOWER, 2); // 2 为关闭显示器， －1则打开显示器
        //}

        /*READ 
         *   ushort address = Convert.ToUInt16(AddrText.Text,16);
            byte bit = Convert.ToByte(BitText.Text,16);
            byte value = 0;
            SvApiLibInitialize(); //初始化dll和driver
            SvReadIoPortByteEx(address, ref value); //读取一个byte的值
            SvApiLibUnInitialize();//关闭 dll 和 driver

            value = Convert.ToByte(value & (1 << bit));//计算对应GPIO的值
            value = Convert.ToByte(value >> bit); 
            ValueText.Text = Convert.ToString(value);


        //WRITE
        ushort address = Convert.ToUInt16(AddrText.Text, 16);
            byte bit = Convert.ToByte(BitText.Text, 16);
            byte value = Convert.ToByte(ValueText.Text, 16);
            byte read_value = 0x0;

            SvApiLibInitialize(); //打开
            SvReadIoPortByteEx(address, ref read_value); //读取
            read_value = Convert.ToByte(read_value & (~(1 << bit)));// clear value of addr.bit
            read_value = Convert.ToByte(read_value | (value << bit));// set value of addr.bit to val
            SvWriteIoPortByteEx(address, read_value); //写入
            SvApiLibUnInitialize();//关闭
         * */



        // 接收到服务器消息改变后触发的事件 
        public delegate void OnReceiveIODataEventDelegate(int aIOData);//建立事件委托
        public event OnReceiveIODataEventDelegate OnReceiveIODataEvent;//收到数据的事件
        //数据接收监听线程
        private Thread MonitorThread;
        public bool IsMonitoring;
        public int SleepTimems = 1000;
        public int IOData = -1;
        private bool bCanReadIOData = false;

        //开始监控
        public void StartMonitor(int aSleepTime)
        {
            IsMonitoring = true;
            SleepTimems = aSleepTime;
            //开启接收线程   
            MonitorThread = new Thread(new ThreadStart(CheckIOData));//启动新线程做接收
            MonitorThread.IsBackground = true;
            MonitorThread.Start();
        }//启动并且 监听 服务器发来的数据

        //停止监听
        public void StopMonitor()
        {
            IsMonitoring = false;
            MonitorThread.Abort();
        }

        public void ReadDataControl(bool aCanReadIOData)
        {
            IOData = -1;
            bCanReadIOData = aCanReadIOData;

        }

        //接收数据做服务 
        private void CheckIOData()
        {
            int tempIOData = 0;
            while (IsMonitoring)
            {
                if (bCanReadIOData)
                {
                    //qiao 读取IO 状态
                    // tempIOData = Win32APIs.Win32API.ReadIOdata();
                    if (tempIOData != IOData)
                    {
                        IOData = tempIOData;
                        if (OnReceiveIODataEvent != null)
                            OnReceiveIODataEvent.Invoke(IOData);
                    }
                }
                Thread.Sleep(SleepTimems);
            }
        }


        #region 设置开机自动运行
        //设置开机自动运行
        public static bool SetAutoRun(string fileName, bool isAutoRun)
        {
            bool bResult = false;
            RegistryKey reg = null;
            try
            {
                if (!System.IO.File.Exists(fileName))
                {
                    //reSet = "该文件不存在!";
                    bResult = false;
                }
                string name = fileName.Substring(fileName.LastIndexOf(@"\") + 1);
                reg = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (reg == null)
                {
                    reg = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                }
                if (isAutoRun)
                {
                    reg.SetValue(name, fileName);
                    //reSet = "设置成功。";
                    bResult = true;
                }
                else
                {
                    reg.SetValue(name, false);
                }

            }
            catch (Exception ex)
            {
                //reSet = "设置时出错。" + ex.Message;
                frmMain.ShowDebugMSG(ex.ToString());
                bResult = false;
            }
            finally
            {
                if (reg != null)
                {
                    reg.Close();
                }
            }
            return bResult;
        }
        #endregion

        #region 重启win7
        //的需要的常量 
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct TokPriv1Luid
        {
            public int Count;
            public long Luid;
            public int Attr;
        }
        [DllImport("kernel32.dll", ExactSpelling = true)]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern bool OpenProcessToken(IntPtr h, int acc, ref IntPtr phtok);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool LookupPrivilegeValue(string host, string name, ref long pluid);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern bool AdjustTokenPrivileges(IntPtr htok, bool disall, ref TokPriv1Luid newst, int len, IntPtr prev, IntPtr relen);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern bool ExitWindowsEx(int flg, int rea);

        private static void DoExitWin(int flg)
        {
            bool ok;
            TokPriv1Luid tp;
            IntPtr hproc = GetCurrentProcess();
            IntPtr htok = IntPtr.Zero;
            ok = OpenProcessToken(hproc, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, ref htok);
            tp.Count = 1;
            tp.Luid = 0;
            tp.Attr = SE_PRIVILEGE_ENABLED;
            ok = LookupPrivilegeValue(null, SE_SHUTDOWN_NAME, ref tp.Luid);
            ok = AdjustTokenPrivileges(htok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            ok = ExitWindowsEx(flg, 0);
        }

        /// 重启电脑
        public static void ReStartComputer()
        {
            Process.Start("shutdown", "/r /t 0"); // 参数 /r 的意思是要重启计算机   
        }

        public static void Reboot()
        {
            DoExitWin(EWX_FORCE | EWX_REBOOT); //重启
        }

        public static void PowerOff()
        {
            DoExitWin(EWX_FORCE | EWX_POWEROFF);    //关机
        }

        public static void LogoOff()
        {
            DoExitWin(EWX_FORCE | EWX_LOGOFF);      //注销
        }
        #endregion

        #region 设置开机背景图片（有进度条的那种）
        //设置开机自动运行
        public static bool SetBackScreen(bool aUseSysBack)
        {
            //msconfig 在常规里选择“有选择的启动
            //C:\Windows\system32\oobe\info\backgrounds\backgroundDefault.jpg  //不要超过250KB
            //C:Windows\System32\oobe\info\backgrounds，如果没有info和backgrounds这两个文件夹的时候，我们就需要手动创建
            bool bResult = true;
            RegistryKey reg = null;
            try
            {
                reg = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\Background", true);
                if (reg == null)
                {
                    reg = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\Background");
                }
                if (aUseSysBack)
                {
                    reg.SetValue("OEMBackground", 1);   //reSet = "设置成功。";
                    bResult = true;
                }
                else
                {
                    reg.SetValue("OEMBackground", 0);
                }

            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
                bResult = false;
            }
            finally
            {
                if (reg != null)
                    reg.Close();
            }
            return bResult;
        }
        #endregion

        //
        //防止重复运行
        //
        public const Int32 NATIVE_ERROR_ALREADY_EXISTS = 183;
        [DllImport(strDllName, EntryPoint = "CreateMutex", SetLastError = true)]
        public static extern IntPtr CreateMutex(
            IntPtr lpMutexAttributes,
            bool InitialOwner,
            string MutexName);

        [DllImport(strDllName, EntryPoint = "ReleaseMutex", SetLastError = true)]
        public static extern bool ReleaseMutex(IntPtr hMutex);
        public static bool IsInstanceRunning()
        {
            string strAppName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            IntPtr hMutex = CreateMutex(IntPtr.Zero, true, strAppName);
            if (hMutex == IntPtr.Zero)
            {
                throw new ApplicationException("Failure creating mutex: " + Marshal.GetLastWin32Error().ToString("X"));
            }
            if (Marshal.GetLastWin32Error() == NATIVE_ERROR_ALREADY_EXISTS)
            {
                return true;
            }
            else
            {
                return false;
            }
        }




        public struct MsgQOptions
        {
            public uint dwSize;
            public uint dwFlags;
            public uint dwMaxMessages;
            public uint cbMaxMessage;
            public bool bReadAccess;
        }
        uint ConvertByteArray(byte[] array, int offset)
        {
            uint res = 0;
            res += array[offset];
            res += array[offset + 1] * (uint)0x100;
            res += array[offset + 2] * (uint)0x10000;
            res += array[offset + 3] * (uint)0x1000000;
            return res;
        }
        IntPtr ptr = IntPtr.Zero;
        //Thread WorkThread = null;
        //bool ThreadTerminal = false;

        [DllImport("coredll.dll")]
        private static extern IntPtr RequestPowerNotifications(IntPtr hMsgQ, uint Flags);

        [DllImport("coredll.dll")]
        private static extern uint WaitForSingleObject(IntPtr hHandle, int wait);

        [DllImport("coredll.dll")]
        private static extern IntPtr CreateMsgQueue(string name, ref MsgQOptions options);

        [DllImport("coredll.dll")]
        private static extern bool ReadMsgQueue(IntPtr hMsgQ, byte[] lpBuffer, uint cbBufSize, ref uint lpNumRead, int dwTimeout, ref uint pdwFlags);

        private void DoWork()
        {
            //buf is declared with 8bytes, since 1st 4bytes are used for Powername and 2nd 4bytes are used for the actual power state type
            byte[] buf = new byte[8];
            uint nRead = 0, flags = 0, res = 0;
            try
            {
                //while (!ThreadTerminal)
                {
                    res = WaitForSingleObject(ptr, 1000);
                    if (res == 0)
                    {
                        ReadMsgQueue(ptr, buf, (uint)buf.Length, ref nRead, -1, ref flags);
                        uint flag = ConvertByteArray(buf, 4);
                        switch (flag)
                        {
                            case 0x00000000:
                                //this.BeginInvoke(_ScreenBLDelegate, new object[] { Guid.NewGuid().ToString() }); 
                                frmMain.AutoLoadout();
                                break;
                            case 0x11000000:

                                break;
                            case 0x12010000:

                                break;
                            case 1048576://Power Idle

                                //MyClass.Executemymethod()                               
                                break;
                            case 65536://Power on

                                //MyClass.Executemymethod()                               
                                break;
                            case 131072://Power off
                                //out
                                //MyClass.Executemymethod()                               
                                break;
                            case 2097152://device is in suspended state. do your opertaion here

                                //MyClass.Executemymethod()                               
                                break;

                            case 262144://Power critical

                                //MyClass.Executemymethod()                               
                                break;
                            case 524288:// Power Boot

                                //MyClass.Executemymethod()                               
                                break;
                            case 8388608:// Power reset 

                                //MyClass.Executemymethod()                               
                                break;
                        }//case
                    }//if
                }//while
                return;
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
        }//fun

        /// <summary>
        /// 宽带连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public static bool Connect4G()
        {
            Process p = new Process();//新建一个进程对象
            p.StartInfo.FileName = "Rasdial.exe";//设置要启动的进程名字
            p.StartInfo.Arguments = "宽带连接" + " " + "宽带连接的账号" + " " + "宽带连接的密码";//传递参数 格式 连接名字+空格+账号+空格+密码
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;//设置执行时的控制台为隐藏的
            p.Start();//开始执行
            p.WaitForExit();//等待连接后自动退出
            if (p.ExitCode == 0)//通过退出返回的代码判断连接是否成功
            {
                //AppendText("宽带连接成功！" + "\n");
                return true;
            }
            else
            {
                //AppendText("宽带连接失败！" + "\n");
                return false;
            }
        }
    }
}
