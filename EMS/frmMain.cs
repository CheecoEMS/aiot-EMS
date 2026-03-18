#define DEBUG

using IEC104;
using Modbus;
using System;
using System.Threading;
using System.Windows.Forms;
using System.Text;
using System.Windows.Forms.DataVisualization.Charting;
using log4net;
using static IEC104.CIEC104Slave;
using System.Diagnostics;
using System.Threading.Tasks;
using EMS;
using System.Collections.Generic;
using System.IO;
using log4net.Util;
using System.IO.Ports;

//351200

namespace EMS
{
    public partial class frmMain : Form
    {
        public int ErrorGridFreshCount = 0;
        private int sCount = 20;
        static public frmMain Selffrm;//实例化窗口对象
        public static string UserID = "";
        public static int UserPower = -1;
        public static bool SysThreathStoped = false;
        public bool BeFoused = true;
        ///设备对象
        public AllEquipmentClass AllEquipment = new AllEquipmentClass();

        ///储能柜对象
        public EMSEquipment ems = new EMSEquipment();

        ///Modbus从站
        private ModbusSlave _modbusSlave;


        ///主从串口通信参数
        private delegate void OnReceiveCMDDelegate(int DataSourceType, byte[] aByteData);//建立事件委托
        private event OnReceiveCMDDelegate OnReceiveCMDEvent;
        /////策略相关
        //时段电价
        //static public ElectrovalenceListClass ElectrovalenceList = new ElectrovalenceListClass();
        //充放电策略时段
        static public TacticsListClass TacticsList = new TacticsListClass();
        //均衡策略时段
        static public BalaTacticsListClass BalaTacticsList = new BalaTacticsListClass();

        //故障事件
        static public WarmingListClass WarmingList = new WarmingListClass();  //局部静态对象

        //debug
        public delegate void Displaydelegate(byte[] InputBuf);
        public Displaydelegate disp_delegate;
        private delegate void UpdateChart(Chart aOneChart, bool aCleanAllSeries);

        public IEC104_delegate iec104_delegate = abc;
        public static void abc() { }

        //8.18
        //public ThreadPoolClass ThreadPool = new ThreadPoolClass();

        //8.23
        //北向通信参数

        //对接104电网
        public TCPClientClass TCPCloud = new TCPClientClass();
        public TCPServerClass TCPserver = new TCPServerClass();
        public CIEC104Slave Slave104 = new CIEC104Slave();
        //test

        //private delegate void TCPserver.OnReceiveDataEventDelegate(int DataSourceType, byte[] aByteData);//建立事件委

        //12.5



        //定时器
        private static System.Threading.Timer DeviceData_Timer;
        private static System.Threading.Timer DO_Timer; //指示灯输出
        private static System.Threading.Timer UI_timer;
        private static System.Threading.Timer BalaTacitc_Timer;
        private static System.Threading.Timer Public_Timer;
        private static System.Threading.Timer CXFN_Timer;//超限防逆log
        private static System.Threading.Timer Led_Timer;
        private static System.Threading.Timer LiquidCold_Timer;
        private static System.Threading.Timer TestSignalStrength_Timer;
        private static System.Threading.Timer TemperControl_Timer;

        private static bool isDeviceDataExecuting = false;
        private static bool isDOExecuting = false;
        private static bool isUiExecuting = false; //判断UI_timer是否正在执行
        private static bool isBalaTacticExecuting = false;
        private static bool isPublicExecuting = false;
        private static bool isCXFNExecuting = false;
        private static bool isLedLoopExecuting = false;
        private static bool isLiquidColdHeartbeatExecuting = false;
        private static bool isTestSignalStrengthExecuting = false;
        private static bool isTemperControlExecuting = false;

        //线程
        private Thread PublicThread;
        //private bool isPublicExecuting = false;

        //监控定时器线程
        private Thread MonitorTimer;

        // 日志记录器
        private static ILog log = LogManager.GetLogger("frmMain");

        /// <summary>
        /// 数据库加载重试方法
        /// </summary>
        /// <param name="loadAction">加载操作委托</param>
        /// <param name="tableName">表名（用于日志）</param>
        /// <returns>是否成功加载</returns>
        private static bool RetryLoadDatabase(Func<bool> loadAction, string tableName)
        {
            const int maxRetries = 5;
            const int retryDelayMs = 2000; // 5秒延迟

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (loadAction())
                    {
                        if (attempt > 1)
                        {
                            log.Info($"数据库表 {tableName} 加载成功（第 {attempt} 次尝试）");
                        }
                        return true;
                    }

                    if (attempt < maxRetries)
                    {
                        log.Warn($"数据库表 {tableName} 加载失败（第 {attempt} 次），{retryDelayMs}ms 后重试...");
                        Thread.Sleep(retryDelayMs);
                    }
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries)
                    {
                        log.Warn($"数据库表 {tableName} 加载异常（第 {attempt} 次）: {ex.Message}，{retryDelayMs}ms 后重试...");
                        Thread.Sleep(retryDelayMs);
                    }
                    else
                    {
                        log.Error($"数据库表 {tableName} 加载失败，已达到最大重试次数 ({maxRetries}): {ex.Message}");
                    }
                }
            }

            log.Error($"数据库表 {tableName} 加载失败，已达到最大重试次数 ({maxRetries})");
            return false;
        }

        //tcp
        //对接主从通讯
        public TCPServerClass ModbusTcpServer = new TCPServerClass();
        public TCPClientClass ModbusTcpClient = new TCPClientClass();


        public DateTime receive_time_start;
        public DateTime receive_time_end;
        public DateTime receive_time_send;



        //static public PID pid = new PID();

        public frmMain()
        {
            InitializeComponent();
            // 只在构造函数中进行基本的初始化
            this.DoubleBuffered = true;
            this.Width = 1024;
            this.Height = 768;
        }

        public bool Initialize()
        {
            try
            {
                //初始化用户等级
                SetFormPower(UserPower);
                log.Error("初始化EMS版本：EMS1.1.1");

                // TCP服务器事件
                if (!InitializationManager.InitializeComponent(InitializationManager.InitStep.TCPServerEvent, () =>
                {
                    TCPserver.OnReceiveDataEvent2 += new Modbus.TCPServerClass.OnReceiveDataEventDelegate2(OnReceive104CMD2);
                }))
                {
                    log.Error("TCPserver.OnReceiveDataEvent2绑定失败");
                    return false;
                }

                // ModbusTcp客户端事件
                if (!InitializationManager.InitializeComponent(InitializationManager.InitStep.ModbusTcpClientEvent, () =>
                {
                    ModbusTcpClient.OnReceiveDataEvent2 += new Modbus.TCPClientClass.OnReceiveDataEventDelegate2(OnReceiveModbusTcpClientCMD);
                }))
                {
                    log.Error("ModbusTcpClient.OnReceiveDataEvent2绑定失败");
                    return false;
                }

                frmFlash.AddPostion(10);

                // 初始化各个窗体
                if (!InitializationManager.InitializeComponent(InitializationManager.InitStep.FormControl, () =>
                {
                    frmControl.INIForm();
                }))
                {
                    log.Error("frmControl页面初始化失败");
                    return false;
                }

                if (!InitializationManager.InitializeComponent(InitializationManager.InitStep.FormUser, () =>
                {
                    frmoneUser.INIForm();
                }))
                {
                    log.Error("frmoneUser页面初始化失败");
                    return false;
                }

                if (!InitializationManager.InitializeComponent(InitializationManager.InitStep.FormKeyBoard, () =>
                {
                    frmKeyBoard.INIForm();
                }))
                {
                    log.Error("frmKeyBoard页面初始化失败");
                    return false;
                }

                if (!InitializationManager.InitializeComponent(InitializationManager.InitStep.FormSet, () =>
                {
                    frmSet.INIForm();
                }))
                {
                    log.Error("frmSet页面初始化失败");
                    return false;
                }

                if (!InitializationManager.InitializeComponent(InitializationManager.InitStep.FormState, () =>
                {
                    frmState.INIForm();
                }))
                {
                    log.Error("frmState页面初始化失败");
                    return false;
                }

                if (!InitializationManager.InitializeComponent(InitializationManager.InitStep.FormLogin, () =>
                {
                    frmLogin.INIForm();
                }))
                {
                    log.Error("frmLogin页面初始化失败");
                    return false;
                }

                if (!InitializationManager.InitializeComponent(InitializationManager.InitStep.FormAbout, () =>
                {
                    frmAbout.INIForm();
                }))
                {
                    log.Error("frmAbout页面初始化失败");
                    return false;
                }

                if (!InitializationManager.InitializeComponent(InitializationManager.InitStep.FormLine, () =>
                {
                    frmLine.INIForm();
                }))
                {
                    log.Error("frmLine页面初始化失败");
                    return false;
                }

                frmFlash.AddPostion(10);

                // 加载主要功能
                if (!InitializationManager.InitializeComponent(InitializationManager.InitStep.LoadForm, () =>
                {
                    return LoadForm();
                }))
                {
                    log.Error("LoadForm加载失败");
                    return false;
                }

                frmFlash.CloseFlashForm();

                return true;
            }
            catch (Exception ex)
            {
                log.Error($"frmMain构造函数失败: {ex.Message}");
                return false;
            }
        }

        #region 串口网口消息解析函数
        static public byte[] Back3Data(int aAddr, short iLen)
        {
            byte[] returnMsg = null;
            ushort aMsg;
            int index = 3;
            returnMsg = ModbusBase.BuildMSG3sTitle((byte)frmSet.config.i485Addr, 3, (ushort)iLen);
            for (int i = aAddr; i <= aAddr+iLen; ++i)
            {
                aMsg = 0;
                switch (i)
                {
                    case 0x5000://设备序列号
                        //aMsg = frmSet.SysID;
                        break;
                    case 0x5001://功率，正数为放电，负数为充电
                        aMsg = (ushort)frmMain.Selffrm.AllEquipment.PCSKVA;
                        break;
                    case 0x5002://日充电量kWh
                        aMsg = (ushort)frmMain.Selffrm.AllEquipment.E2PKWH[0];
                        break;
                    case 0x5003://日放电量kWh
                        aMsg = (ushort)frmMain.Selffrm.AllEquipment.E2OKWH[0];
                        break;
                    case 0x5004://月充电量kWh
                        aMsg = 0;
                        break;
                    case 0x5005://月放电量kWh
                        aMsg = 0;
                        break;
                    case 0x5006://总充电量kWh
                        aMsg = (ushort)frmMain.Selffrm.AllEquipment.Elemeter2.PUkwh[0];
                        break;
                    case 0x5007://总放电量kWh
                        aMsg = (ushort)frmMain.Selffrm.AllEquipment.Elemeter2.OUkwh[0];
                        break;
                    case 0x5008://总容量（%）
                        aMsg = 200;
                        break;
                    case 0x5009://soc上限
                        aMsg = 100;
                        break;
                    case 0x5010://soc下限
                        aMsg = 5;
                        break;
                    case 0x5011://最大功率充电时长（分钟）
                        aMsg = 90;
                        break;
                    case 0x5012://最大功率放电时长（分钟)
                        aMsg = 90;
                        break;
                    case 0x5013://健康度（%）
                        aMsg = 100;
                        break;
                    case 0x5014://状态1：在线，0：离线
                        aMsg = 0;
                        break;
                    case 0x5015://充放电状态0：待机，1：充电，2：放电
                        if (frmMain.Selffrm.AllEquipment.PCSKVA == 0)
                        {
                            aMsg = 0;
                        }
                        else
                        {
                            if (frmMain.Selffrm.AllEquipment.wTypeActive == "充电")
                            {
                                aMsg = 1;
                            }
                            else if (frmMain.Selffrm.AllEquipment.wTypeActive == "放电")
                            {
                                aMsg = 2;
                            }
                        }
                        break;
                    case 0x5016://BMS告警信息
                        aMsg = 0;
                        break;
                    case 0x5017://PCS告警信息
                        aMsg = 0;
                        break;
                    case 0x5018://EMS告警信息
                        aMsg = 0;
                        break;
                    case 0x5019:
                        break;
                }
                //组装报文
                ModbusBase.AddMSG3(aMsg, ref returnMsg, ref index);
            }
            ModbusBase.AddCRC(ref returnMsg);
            return returnMsg;

        }

        //连控数据中读取数据-----3读取
        static public byte[] Back3Data(int aAddr)
        {
            switch (aAddr)
            {
                case 0x6001://计划功率
                    return ModbusBase.BuildMSG3Back((byte)frmSet.config.i485Addr, 3, (ushort)(Math.Abs(frmMain.Selffrm.AllEquipment.PCSScheduleKVA)));
                case 0x6002://实际功率
                    double value = Math.Abs(frmMain.Selffrm.AllEquipment.PCSKVA);
                    return ModbusBase.BuildMSG3Back((byte)frmSet.config.i485Addr, 3, (ushort)value);
                case 0x6003://充放电
                    if (frmMain.Selffrm.AllEquipment.PCSKVA < -0.5)//充电
                        return ModbusBase.BuildMSG3Back((byte)frmSet.config.i485Addr, 3, 0);
                    else if (frmMain.Selffrm.AllEquipment.PCSKVA > 0.5)//放电
                        return ModbusBase.BuildMSG3Back((byte)frmSet.config.i485Addr, 3, 1);
                    else//待机
                        return ModbusBase.BuildMSG3Back((byte)frmSet.config.i485Addr, 3, 2);
                case 0x6004: //PCSType 恒压横流恒功率、AC恒压
                    return ModbusBase.BuildMSG3Back((byte)frmSet.config.i485Addr, 3, (ushort)Array.IndexOf(PCSClass.PCSTypes, frmMain.Selffrm.AllEquipment.PCSTypeActive));
                case 0x6005: //EMS运行状态 ： 0正常，1故障，2停机
                    return ModbusBase.BuildMSG3Back((byte)frmSet.config.i485Addr, 3, (ushort)frmMain.Selffrm.AllEquipment.runState);
                case 0x6006: //BMS是否告警
                    if (frmMain.Selffrm.AllEquipment.BMS.Error[1] + frmMain.Selffrm.AllEquipment.BMS.Error[2] + frmMain.Selffrm.AllEquipment.BMS.Error[3] > 0)
                        return ModbusBase.BuildMSG3Back((byte)frmSet.config.i485Addr, 3, 1);
                    else
                        return ModbusBase.BuildMSG3Back((byte)frmSet.config.i485Addr, 3, 0);
            }
            return null;
        }

        //连控数据中设置寄存器---执行6
        static public void Active6Data(int aAddr, int data)
        {

            switch (aAddr)
            {
                case 0x6000://开关pcs
                    if (data != 0)
                    {
                        frmMain.Selffrm.AllEquipment.PCSList[0].ExcSetPCSPower(true);
                        lock (frmMain.Selffrm.AllEquipment)
                            frmMain.Selffrm.AllEquipment.HostStart = true;
                    }
                    else
                    {
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            frmMain.Selffrm.AllEquipment.HostStart = false;
                            frmMain.Selffrm.AllEquipment.PCSScheduleKVA = 0;
                        }
                    }
                    break;
                case 0x6001://计划功率
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        frmMain.Selffrm.AllEquipment.PCSScheduleKVA = data;
                    }
                    break;
                case 0x6002://实际功率
                    //log.Error("从机接收Command执行参数:"+ frmMain.Selffrm.AllEquipment.wTypeActive + frmMain.Selffrm.AllEquipment.PCSTypeActive + data);
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        frmMain.Selffrm.AllEquipment.HostStart = true;
                        frmMain.Selffrm.AllEquipment.PCSScheduleKVA = data;
                        frmMain.Selffrm.AllEquipment.NetControl = true;
                    }
                    break;
                case 0x6003://充放电
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        if (data == 0)
                            frmMain.Selffrm.AllEquipment.wTypeActive = "充电";
                        else
                            frmMain.Selffrm.AllEquipment.wTypeActive = "放电";
                    }
                    break;
                case 0x6004://恒压横流恒功率、AC恒压
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        if (data>=0 && data < PCSClass.PCSTypes.Length)
                        {
                            frmMain.Selffrm.AllEquipment.PCSTypeActive = PCSClass.PCSTypes[data];
                        }
                    }
                    break;
            }
        }
        #endregion

        //tcp
        private void OnReceiveModbusTcpClientCMD(byte[] aByteData)
        {
            //验证消息
            //string hexString = BitConverter.ToString(aByteData);

            int SysID = 0;
            int CMDID = 0;
            short iAddr = 0;
            short iLen = 0;
            long iData = 0;
            ////判断是否为传到的命令
            //检查是否是为命令  //检查crc

            if (!ModbusBase.CheckResponse(aByteData))
                return;

            //解析命令
            iData = GetCMDFunctionID(aByteData, ref SysID, ref CMDID, ref iAddr, ref iLen);

            AllEquipment.NetCtlTime = DateTime.Now;
            AllEquipment.Clock_Watch.RestartMeasurement();
            frmSet.config.SysMode = 2;
            byte[] message = new byte[7];
            short[] sData01 = { 00, 00 };
            short[] data = { 00 };
            switch (CMDID)
            {
                case 0x03://读取
                    AllEquipment.NetConnect = true;
                    if (iLen == 1)
                    {
                        frmMain.Selffrm.ModbusTcpClient.SendMSG(Back3Data(iAddr));
                    }
                    else
                    {
                        frmMain.Selffrm.ModbusTcpClient.SendMSG(Back3Data(iAddr, iLen));
                        //frmMain.Selffrm.ModbusTcpClient.clientSocket.Send(CloudClass.Back3Data(iAddr, iLen));
                    }
                    break;
                case 0x06://设置
                    AllEquipment.NetConnect = true;
                    Active6Data(iAddr, (int)iData);
                    //frmMain.Selffrm.ModbusTcpClient.clientSocket.Send(aByteData);
                    frmMain.Selffrm.ModbusTcpClient.SendMSG(aByteData);
                    break;
                case 0x20://读取设备ID
                    AllEquipment.NetConnect = true;
                    data[0] = (short)SysID; //ilen 是主机端赋予从机的虚拟地址号，返回虚拟地址号和实际设备号
                    message = ModbusBase.BuildCloundMSG((byte)frmSet.config.i485Addr, 0x20, 1, data);
                    //string result = BitConverter.ToString(message);

                    frmMain.Selffrm.ModbusTcpClient.SendMSG(message);

                    //IPEndPoint localEndPoint = (IPEndPoint)frmMain.Selffrm.ModbusTcpClient.clientSocket.LocalEndPoint;
                    //"Local IP address: " + localEndPoint.Address
                    //"Local port: " + localEndPoint.Port

                    // Get the remote endpoint information
                    //IPEndPoint remoteEndPoint = (IPEndPoint)frmMain.Selffrm.ModbusTcpClient.clientSocket.RemoteEndPoint;
                    //"Remote IP address: " + remoteEndPoint.Address
                    //"Remote port: " + remoteEndPoint.Port
                    break;
                case 0x21:
                    /*                    sData01[0] = (short)1;
                                        message = ModbusBase.BuildCloundMSG(1, 0x22, 01, sData01);
                                        TCPCloud.SendMSG(message);*/
                    break;
                case 0x26: //闻讯间隔
                    /*                    sData01[0] = (short)1;
                                        message = ModbusBase.BuildCloundMSG(1, 0x26, 01, sData01);
                                        TCPCloud.SendMSG(message);
                                        frmSet.YunInterval = iLen;
                                        //设置云的读取间隔，判断两次无数据就会重新连接云（2B）
                                        TCPCloud.ReconnectTime = frmSet.YunInterval;//AllEquipment.AskInterval;
                                        frmSet.SaveSet2File();//保存数据 */
                    break;
                case 0x16:
                    // CloundClass.Command16(iAddr, iData);
                    break;
                case 0x18:
                //主机获取从机执行反馈

                default:
                    break;
            }
        }

        //处理接收到的104报文协议
        private void OnReceive104CMD2(byte[] msg)
        {
            //do+委托
            string hexString = BitConverter.ToString(msg);
            receive_time_start = DateTime.Now;
            //"收到TCP消息：" + hexString
            Slave104.iec104_packet_parser(msg);

        }

        //人员的权限管理
        public void SetFormPower(int aPower)
        {
            btnLine.Visible= (aPower >=0);
            btnState.Visible = (aPower >= 0);
            btnWarning.Visible = (aPower >= 1);
            btnControl.Visible = (aPower>=2);
            btnSet.Visible = (aPower >= 3);
        }


        private long GetCMDFunctionID(byte[] aByteData, ref int aID, ref int aCommID, ref short aAddr, ref short aDataLen)
        {  //012700010001a5cd
            int iResult = 0;
            try
            {
                if (aByteData.Length > 0)
                {
                    //设备ID
                    aID = (int)aByteData[0]; //还原第1字节（低位）
                    //取得ComandID
                    aCommID = (int)aByteData[1]; //还原第1字节（低位）
                    //取得Addr
                    aAddr = (short)(aByteData[2] << 8); //还原第2字节
                    aAddr += (short)aByteData[3]; //还原第1字节（低位）
                    //若为写的话就是寄存器值
                    aDataLen = (short)(aByteData[4] << 8); //还原第2字节
                    aDataLen += (short)aByteData[5]; //还原第1字节（低位）

                    iResult = 0;
                    if (aCommID == 6) //只有6 才能写入到设备
                    {
                        iResult=(Int16)aDataLen;
                    }
                }
            }
            catch { }
            return iResult;
        }

        /// <summary>
        /// 字节数组转16进制字符串：空格分隔
        /// </summary>
        /// <param name="byteDatas"></param>
        /// <returns></returns>
        public string ToHexStrFromByte(byte[] byteDatas)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < byteDatas.Length; i++)
            {
                builder.Append(string.Format("{0:X2} ", byteDatas[i]));
            }
            return builder.ToString().Trim();
        }
        /// <summary>
        /// 十六进制字符串转字节数组
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public byte[] ConvertHexStringToByteArray(string hex)
        {
            // 确保输入的十六进制字符串的长度是偶数
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have an even length");

            byte[] byteArray = new byte[hex.Length / 2];

            for (int i = 0; i < hex.Length; i += 2)
            {
                // 解析每一对字符
                string hexPair = hex.Substring(i, 2);
                byteArray[i / 2] = Convert.ToByte(hexPair, 16);
            }

            return byteArray;
        }

        /******************************串口事件处理函数************************************************/
        //收到命令函数
        public void OnReceiveCMD2(int DataSourceType, byte[] aByteData)
        {
            int SysID = 0;
            int CMDID = 0;
            short iAddr = 0;
            short iLen = 0;
            long iData = 0;
            ////判断是否为传到的命令
            //检查是否是为命令  //检查crc

            if (!ModbusBase.CheckResponse(aByteData))
                return;

            //解析命令
            iData = GetCMDFunctionID(aByteData, ref SysID, ref CMDID, ref iAddr, ref iLen);
            if (SysID != frmSet.config.i485Addr)
                return;

            AllEquipment.NetControl = true;
            AllEquipment.NetCtlTime = DateTime.Now;
            frmSet.config.SysMode = 2;
            byte[] message = new byte[7];
            short[] sData01 = { 00, 00 };
            switch (CMDID)
            {
                case 0x03://读取
                    if (Back3Data(iAddr) != null)
                    {
                        ////modbus返回:使用缓冲区中的数据将指定数量的字节写入串行端口。
                        frmMain.Selffrm.ems.m485.WriteRaw(Back3Data(iAddr), 0, 7);
                    }
                    break;
                case 0x06://设置
                    frmMain.Selffrm.ems.m485.WriteRaw(aByteData, 0, aByteData.Length);
                    Active6Data(iAddr, (int)iData);
                    break;
                default:
                    break;
            }
        }

        /*
         *InitializeEquipment 依赖 InitializeDatabase
         *InitializeDeviceData 依赖 InitializeEquipment
         */
        private bool LoadForm()
        {
            string strSysPath = AppDomain.CurrentDomain.BaseDirectory;

            if (!InitializePaths(strSysPath))
            {
                log.Error("LoadForm - InitializePaths失败");
                return false;
            }
            if (!InitializeDatabase())
            {
                log.Error("LoadForm - InitializeDatabase失败");
                return false;
            }
            if (!InitializeEquipment())
            {
                log.Error("LoadForm - InitializeEquipment失败");
                return false;
            }
            if (!InitializeDeviceData())
            {
                log.Error("LoadForm - InitializeDeviceData失败");
                return false;
            }
            if (!InitializeLoadDatabase())
            {
                log.Error("LoadForm - InitializeLoadDatabase失败");
                return false;
            }
            if (!InitializeCloudServices())
            {
                log.Error("LoadForm - InitializeCloudServices失败");
                return false;
            }
            if (!InitializeExternalInterface())
            {
                log.Error("LoadForm - InitializeExternalInterface失败");
                return false;
            }
            if (!InitializeCommunication())
            {
                log.Error("LoadForm - InitializeCommunication失败");
                //return false;
            }
            if (!InitializeTimersAndThreads())
            {
                log.Error("LoadForm - InitializeTimersAndThreads失败");
                return false;
            }

            return true;
        }

        private bool InitializePaths(string strSysPath)
        {
            frmSet.BalaPath = Path.Combine(strSysPath, "BalaCell.txt");
            AllEquipment.DofD = Path.Combine(strSysPath, "DofD.ini");

            // 验证文件是否存在
            if (!File.Exists(frmSet.BalaPath)) return false;

            return true;
        }

        private bool InitializeDatabase()
        {
            //查看数据库是否连接成功
            if (!DBConnection.CheckRec("select * from config")) return false;

            //检查数据库结构是否一致
            //if(!DBConnection.CheckTables()) return false;
            if (!frmSet.InitializeSingletonTableIds()) return false;

            // 加载数据库配置（必填）
            if (!RetryLoadDatabase(() => frmSet.LoadCloudLimitsFromMySQL(), "CloudLimits")) return false;
            if (!RetryLoadDatabase(() => frmSet.LoadConfigFromMySQL(), "Config")) return false;
            if (!RetryLoadDatabase(() => frmSet.LoadVariChargeFromMySQL(), "VariCharge")) return false;
            if (!RetryLoadDatabase(() => frmSet.LoadComponentSettingsFromMySQL(), "ComponentSettings")) return false;
            if (!RetryLoadDatabase(() => frmSet.LoadHistoryDataFromMySQL(), "HistoryData")) return false;
            if (!RetryLoadDatabase(() => frmSet.LoadPeElesticFromMySQL(), "PeElestic")) return false;

            return true;
        }

        //必须在InitializeDeviceData之后，因为LoadJFPGFromSQL依赖确定电表版本
        private bool InitializeLoadDatabase()
        {
            // 加载策略相关数据（选填）
            //if (!TacticsList.LoadFromMySQL(0)) return false;
            //if (!TacticsList.LoadJFPGFromSQL()) return false;
            //if (!BalaTacticsList.LoadFromMySQL()) return false;

            //读取数据库中的故障（选填）
            //if (!Selffrm.AllEquipment.LoadErrorState()) return false;
            Selffrm.AllEquipment.LoadErrorState();

            frmMain.Selffrm.AllEquipment.currentDate  = frmSet.peElestic.rDate.ToString("yyyy-MM-dd");

            //数据看板展示
            DBConnection.SetDBGrid(frmMain.Selffrm.dbvError);

            //配置均衡定时器
            if (frmMain.Selffrm.AllEquipment.BMS != null && frmSet.cloudLimits != null)
            {
                if (frmSet.cloudLimits.OpenBala == 1)
                {
                    frmMain.Selffrm.AllEquipment.BMS.countdownTimer.Start();
                }
                else
                {
                    frmMain.Selffrm.AllEquipment.BMS.countdownTimer.Stop();
                }
            }

            return true;
        }

        private bool InitializeEquipment()
        {
            //同步今日剩余重启次数
            AllEquipment.RebootCount = frmSet.historyDatas.RebootCount;

            //获取历史需量
            if (frmSet.config.IsMaster == 1)
            {
                AllEquipment.E1_PUMdemand_Max_old = frmSet.historyDatas.E1PUMdemandMaxOld;
                AllEquipment.Client_PUMdemand_Max_old = frmSet.historyDatas.ClientPUMdemandMaxOld;
                AllEquipment.Client_PUMdemand_Max = frmSet.historyDatas.ClientPUMdemandMax;
            }

            // 加载设备配置
            if (!AllEquipment.LoadSetFromFile())
            {
                log.Error("LoadForm - InitializeEquipment - LoadSetFromFile失败");
                return false;
            }

            //初始化端口
            if (!frmSet.InitGPIO())
            {
                log.Error("LoadForm - InitializeEquipment - InitGPIO失败");
                //return false;
            }

            //初始化灯板
            if (!AllEquipment.init_LED())
            {
                log.Error("LoadForm - InitializeEquipment - init_LED失败");
                //return false;
            }

            //初始化液冷机
            if (AllEquipment.LiquidCool != null)
            {
                AllEquipment.init_LiquidCool();
            }

            //初始化BMS功能等级,不同等级不同功能
            if (AllEquipment.BMS != null)
            {
                AllEquipment.BMS.CheckFunctionLevel();
                AllEquipment.BMS.CheckBMStype();
            }

            //连接硬件：4G通讯模块


            return true;
        }

        private bool InitializeDeviceData()
        {
            //校验储能表是否是八费率
            if (frmMain.Selffrm.AllEquipment.Elemeter2 != null) {
                frmMain.Selffrm.AllEquipment.Elemeter2.Check_Version();


                /*                if (AllEquipment.Elemeter1List != null)
                                {
                                    foreach (var tempEleMeter in AllEquipment.Elemeter1List)
                                    {
                                        //if (!tempEleMeter.GetDataFromEqipment()) return false;
                                        tempEleMeter.GetDataFromEqipment();
                                    }
                                }
                                if (frmMain.Selffrm.AllEquipment.Elemeter2 != null)
                                    frmMain.Selffrm.AllEquipment.Elemeter2.GetDataFromEqipment();
                                if (frmMain.Selffrm.AllEquipment.Elemeter3 != null)
                                    frmMain.Selffrm.AllEquipment.Elemeter3.GetDataFromEqipment();
                                if (frmMain.Selffrm.AllEquipment.Elemeter4 != null)
                                    frmMain.Selffrm.AllEquipment.Elemeter4.GetDataFromEqipment();*/


                /*            if (AllEquipment.Elemeter2?.GetDataFromEqipment() == false) return false;
                            if (AllEquipment.Elemeter3?.GetDataFromEqipment() == false) return false;
                            if (AllEquipment.Elemeter4?.GetDataFromEqipment() == false) return false;*/

                //必须在设备初始化结束后
                //if (!AllEquipment.ReadDataInoneDaySQL()) return false;
                //AllEquipment.ReadDataInoneDaySQL();

                // 初始化电表数据，需校验是否成功
                if (frmMain.Selffrm.AllEquipment.Elemeter2.InitE2Power())
                {
                    log.Error("初始化电表数据成功");
                    //校验电表数据
                    //if (!AllEquipment.Power_CRC()) return false;
                    AllEquipment.Power_CRC();

                    //初始化今日充放数据
                    //if (!AllEquipment.InitE2Power()) return false;
                    AllEquipment.InitE2Power();

                    //return true;
                }
            }

            return true;
        }

        private bool InitializeCommunication()
        {
            // 104服务初始化
            if (frmSet.config.Open104 == 1)
            {
                if (!Initialize104Service()) return false;
            }

            // TCP/IP通信初始化
            if (frmSet.config.IsMaster == 1)
            {
                if (!InitializeMasterCommunication()) return false;
            }
            else
            {
                if (!InitializeSlaveCommunication()) return false;
            }

            return true;
        }

        private bool Initialize104Service()
        {
            //恢复远动状态
            if (frmSet.historyDatas.YDstatus == 1)
            {
                AllEquipment.eState = 2; //进入网控模式
                frmSet.config.SysMode = 2;
                TacticsList.TacticsOn = false; //关闭策略

                //初始化设置
                lock (AllEquipment)
                {
                    AllEquipment.PCSScheduleKVA = 0;
                    AllEquipment.HostStart = true;
                    AllEquipment.SlaveStart = true;
                }
            }

            log.Warn("初始化EMS连接二级EMS状态：{"+ "网控模式:" + frmMain.Selffrm.AllEquipment.eState + ",远动连接标志位: " + frmSet.historyDatas.YDstatus);

            while (!AllEquipment.HostStart)
            {
                log.Error($"HostStart未置true：{AllEquipment.HostStart}");
                AllEquipment.HostStart = true;
            }

            Slave104.IEC104_Init();

            //配置主站开放2404端口
            if (!TCPserver.TCPServerIni(2404)) return false;

            //监听客户端连接
            if (!TCPserver.StartMonitor2404()) return false;

            return true;
        }

        private bool InitializeMasterCommunication()
        {
            if (frmSet.config.ConnectStatus == "tcp")
            {
                return InitializeMasterTCP();
            }
            else if (frmSet.config.ConnectStatus == "485")
            {
                return InitializeMaster485();
            }

            log.Error("未知的通信方式");
            return false;
        }

        private bool InitializeMasterTCP()
        {
            try
            {
                ModbusTcpServer.clientManager = new ClientManager();
                if (!ModbusTcpServer.TCPServerIni(502))
                {
                    log.Error("502端口已被占用");
                    return false;
                }

                if (!ModbusTcpServer.StartMonitor502()) return false;
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"初始化主站TCP失败: {ex.Message}");
                return false;
            }
        }

        private bool InitializeMaster485()
        {
            try
            {
                //打开主机串口
                ems.m485 = new modbus();
                ems.m485.OpenEMS(
                    frmSet.config.DebugComName,
                    38400,
                    8,
                    System.IO.Ports.Parity.None,
                    System.IO.Ports.StopBits.One);
                // 初始化从机列表
                for (int i = 0; i < frmSet.config.SysCount - 1; i++)
                {
                    EMSEquipment oneEMSEquipment = new EMSEquipment();
                    if (!oneEMSEquipment.LoadCommandFromFile())
                    {
                        log.Error($"加载从机{i + 2}命令文件失败");
                        return false;
                    }

                    oneEMSEquipment.ID = i + 2;
                    oneEMSEquipment.Parent = AllEquipment;
                    oneEMSEquipment.m485 = ems.m485;

                    AllEquipment.EMSList.Add(oneEMSEquipment);
                }
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"初始化主站485失败: {ex.Message}");
                return false;
            }
        }

        private bool InitializeSlaveCommunication()
        {
            if (frmSet.config.ConnectStatus == "tcp")
            {
                return InitializeSlaveTCP();
            }
            else if (frmSet.config.ConnectStatus == "485")
            {
                return InitializeSlave485();
            }

            log.Error("未知的通信方式");
            return false;
        }

        private bool InitializeSlaveTCP()
        {
            try
            {
                return ModbusTcpClient.TCPClientIni(frmSet.config.MasterIp, 502);
            }
            catch (Exception ex)
            {
                log.Error($"初始化从站TCP失败: {ex.Message}");
                return false;
            }
        }

        private bool InitializeSlave485()
        {
            try
            {
                ems.ID = frmSet.config.i485Addr;
                ems.Parent = AllEquipment;
                ems.m485 = new modbus();

                return ems.m485.OpenEMS(
                    frmSet.config.DebugComName,
                    38400,
                    8,
                    System.IO.Ports.Parity.None,
                    System.IO.Ports.StopBits.One);

                /*                bool opened = ems.m485.OpenEMS(
                                    frmSet.config.DebugComName,
                                    38400,
                                    8,
                                    System.IO.Ports.Parity.None,
                                    System.IO.Ports.StopBits.One);

                                if (opened)
                                {
                                    // 创建并启动 Modbus 从站
                                    var emsService = new EmsServiceAdapter(ems);
                                    _modbusSlave = new ModbusSlave(ems.m485, (byte)frmSet.config.i485Addr, emsService);
                                    _modbusSlave.Start();
                                }

                                return opened;*/
            }
            catch (Exception ex)
            {
                log.Error($"初始化从站485失败: {ex.Message}");
                return false;
            }
        }

        private bool InitializeCloudServices()
        {
            try
            {
                // 设置设备码
                string strID = frmSet.config.SysID;
                AllEquipment.full_iot_code = strID;

                if (strID.Length >= 7)
                {
                    strID = strID.Substring(strID.Length - 7, 7);
                }
                AllEquipment.iot_code = "ems" + strID;
                AllEquipment.Fire.iot_code = "fire" + strID;
                AllEquipment.Profit2Cloud.iot_code = "ems" + strID;

                // 初始化云服务
                /*                AllEquipment.Report2Cloud = new CloudClass
                                {
                                    Parent = AllEquipment
                                };*/

                //AllEquipment.Report2Cloud.IniClound();

                string strSysPath = Convert.ToString(System.AppDomain.CurrentDomain.BaseDirectory);
                //frmMain.Selffrm.AllEquipment.Report2Cloud.strUpPath = strSysPath + "UpData";
                //frmMain.Selffrm.AllEquipment.Report2Cloud.strDownPath = strSysPath + "DownData";

                /*                if (!AllEquipment.Report2Cloud.mqttConnect())
                                {
                                    log.Error("MQTT连接失败");
                                }*/

                // 初始化策略
                TacticsList.Parent = AllEquipment;

                //新增mqttManager
                var mqtt = new MqttManager
                {
                    BrokerIp = frmSet.config.MqttBrokerIp,
                    BrokerPort = frmSet.config.MqttBrokerPort,
                    Username = frmSet.config.MqttBrokerUser,
                    Password = frmSet.config.MqttBrokerPassword,
                    ClientId = frmSet.config.SysID
                };

                AllEquipment.cloudService = new CloudService(mqtt)
                {
                    Parent = AllEquipment
                };
                AllEquipment.cloudService.Start();


                return true;
            }
            catch (Exception ex)
            {
                log.Error($"初始化云服务失败: {ex.Message}");
                return false;
            }
        }

        private bool InitializeExternalInterface()
        {
            try
            {
                // 初始化对外接口
                BaseEquipmentClass oneEquipment = new EMSEquipment();
                oneEquipment.Parent = AllEquipment;
                oneEquipment = (EMSEquipment)oneEquipment;

                if (!oneEquipment.LoadCommandFromFile())
                {
                    log.Error("加载外部接口命令文件失败");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                log.Error($"初始化外部接口失败: {ex.Message}");
                return false;
            }
        }

        private bool InitializeTimersAndThreads()
        {
            if (!IniralizeFrmMain_Timer()) return false;
            if (!InitFrmMainClass_Threads()) return false;
            //if(!AllEquipment.Report2Cloud.InitCloudClass_Timer()) return false;
            //if (!AllEquipment.Report2Cloud.InitCloudClass_Threads()) return false;
            if (!AllEquipment.AutoReadData()) return false;

            return true;
        }

        /**********************************/
        /*                                */
        /*            监控线程            */
        /*                                */
        /*********************************/

        public void MonitorTimerAlive()
        {
            try
            {
                // 创建并启动监控线程
                MonitorTimer = new Thread(MonitorTimerCallback)
                {
                    IsBackground = true,
                    Priority = ThreadPriority.Normal,
                    Name = "MonitorThread"
                };
                MonitorTimer.Start();

                log.Info("MonitorThread has been started.");
            }
            catch (Exception ex)
            {
                log.Error("Error starting MonitorThread: " + ex.Message);
            }
        }

        private void MonitorTimerCallback()
        {

        }

        /**********************************/
        /*                                */
        /*            定时任务线程        */
        /*                                */
        /*********************************/
        public bool InitFrmMainClass_Threads()
        {
            try
            {
                if (!StartPublicThread()) return false;

                if (BalaTacticsList != null)
                {
                    if (!BalaTacticsList.AutoCheckBalaTactics()) return false;
                }

                if (TacticsList != null)
                {
                    if (!TacticsList.AutoCheckTactics()) return false;
                    if (!TacticsList.AutoCheckJFPG()) return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                log.Error("InitFrmMainClass_Threads: " + ex.Message);
                return false;
            }
        }


        private bool StartPublicThread()
        {
            try
            {
                // 创建并启动 Heartbeat 线程
                PublicThread = new Thread(PublicThreadCallback);
                PublicThread.IsBackground = true;
                PublicThread.Priority = ThreadPriority.Highest;
                PublicThread.Name = "PublicThread";
                PublicThread.Start();
                return true;
            }
            catch (Exception ex)
            {
                log.Error("Error starting PublicThread: " + ex.Message);
                return false;
            }
        }

        private void PublicThreadCallback()
        {
            while (true)
            {
                try
                {
                    //上电启动后必须完成1次电表校准
                    if (!frmMain.Selffrm.AllEquipment.MeterCalibrationSuccess)
                    {
                        if (frmMain.Selffrm.AllEquipment.MeterCalibration())
                        {
                            frmMain.Selffrm.AllEquipment.MeterCalibrationSuccess = true;
                        }

                    }

                    /************************* 日切换执行 *************************************/
                    if (!frmMain.Selffrm.AllEquipment.LoadBalaTacticsSuccess)
                    {
                        if (frmMain.BalaTacticsList.LoadFromMySQL())
                        {
                            frmMain.Selffrm.AllEquipment.LoadBalaTacticsSuccess = true;
                        }
                    }

                    if (!frmMain.Selffrm.AllEquipment.LoadTacticsSuccess)
                    {
                        if (frmMain.TacticsList.LoadMasterDailyTactics())
                        {
                            frmMain.Selffrm.AllEquipment.LoadTacticsSuccess = true;
                        }
                    }


                    if (!frmMain.Selffrm.AllEquipment.LoadJFPGSuccess)
                    {
                        if (frmMain.TacticsList.LoadJFPGFromSQL())
                        {
                            frmMain.Selffrm.AllEquipment.LoadJFPGSuccess = true;
                        }

                    }

                    if (!frmMain.Selffrm.AllEquipment.SetHistoryDataSuccess)
                    {
                        if (frmSet.Set_HistoryData())
                        {
                            frmMain.Selffrm.AllEquipment.SetHistoryDataSuccess = true;
                        }
                    }

                    if (!frmMain.Selffrm.AllEquipment.DeleOldDataSuccess)
                    {
                        if (frmSet.DeleOldData(DateTime.Now.AddDays(-180).ToString("yyyy-MM-dd")))
                        {
                            frmMain.Selffrm.AllEquipment.DeleOldDataSuccess = true;
                        }
                    }

                    if (!frmMain.Selffrm.AllEquipment.WriteDataInoneDaySuccess)
                    {
                        if (frmMain.Selffrm.AllEquipment.WriteDataInoneDaySQL(frmSet.peElestic.rDate.ToString("yyyy-MM-dd")))
                        {
                            frmSet.peElestic.rDate = DateTime.Now;
                            frmMain.Selffrm.AllEquipment.WriteDataInoneDaySuccess = true;
                        }
                    }

                    // 检查月份是否更新
                    if (frmMain.Selffrm.AllEquipment.mDate != DateTime.Now.ToString("yyyy-MM"))
                    {
                        frmSet.historyDatas.ClientPUMdemandMaxOld = (int)frmMain.Selffrm.AllEquipment.Client_PUMdemand_Max;
                        frmSet.historyDatas.E1PUMdemandMaxOld = (int)frmMain.Selffrm.AllEquipment.E1_PUMdemand_Max;
                        frmSet.historyDatas.ClientPUMdemandMax = 0;
                        frmMain.Selffrm.AllEquipment.Client_PUMdemand_Max = 0;
                        frmMain.Selffrm.AllEquipment.mDate = DateTime.Now.ToString("yyyy-MM");
                    }

                    // 检查日期是否更新
                    //if (frmMain.Selffrm.AllEquipment.rDate != DateTime.Now.ToString("yyyy-MM-dd"))
                    if (frmMain.Selffrm.AllEquipment.currentDate != DateTime.Now.ToString("yyyy-MM-dd"))
                    {
                        log.Error("日期更迭：" + "记录时间: " + frmMain.Selffrm.AllEquipment.currentDate + "当前时间： " + DateTime.Now.ToString("yyyy-MM-dd"));

                        // 更新日期
                        frmMain.Selffrm.AllEquipment.currentDate = DateTime.Now.ToString("yyyy-MM-dd");

                        // 重置EMS重启次数
                        if (frmSet.historyDatas != null && frmSet.historyDatas.RebootCount != 5)
                        {
                            frmSet.historyDatas.RebootCount = 5;
                        }

                        //每日同步今日充放电量
                        frmMain.Selffrm.AllEquipment.SetHistoryDataSuccess = false;

                        // 删除180天前的数据
                        //frmSet.DeleOldData(DateTime.Now.AddDays(-180).ToString("yyyy-MM-dd"));
                        frmMain.Selffrm.AllEquipment.DeleOldDataSuccess = false;

                        // 记录收益
                        frmMain.Selffrm.AllEquipment.WriteDataInoneDaySuccess = false;

                        // 校准电表日期
                        frmMain.Selffrm.AllEquipment.MeterCalibrationSuccess = false;
                        frmMain.Selffrm.AllEquipment.LoadJFPGSuccess = false;

                        // 加载充放策略
                        frmMain.Selffrm.AllEquipment.LoadTacticsSuccess = false;

                        // 加载均衡策略
                        frmMain.Selffrm.AllEquipment.LoadBalaTacticsSuccess = false;
                    }
           
                }
                catch (Exception ex)
                {
                    log.Error("Public_TimerCallback encountered an error: " + ex.Message);
                }

                // 等待 2分钟再进行下一次心跳
                Thread.Sleep(120000);
                //Thread.Sleep(1000);
            }
        }




        /*        private void PublicThreadCallback()
                {
                    while (true)
                    {
                        try
                        {
                            // 周期计算今日充放电量
                            //frmMain.Selffrm.AllEquipment.CalculateNowPower();

                            //上电启动后必须完成1次电表校准
                            if (!frmMain.Selffrm.AllEquipment.MeterCalibrationSuccess)
                            {
                                if (frmMain.Selffrm.AllEquipment.MeterCalibration()) {
                                    frmMain.Selffrm.AllEquipment.MeterCalibrationSuccess = true;
                                }

                            }

                            *//************************* 日切换执行 *************************************//*
                            if (!frmMain.Selffrm.AllEquipment.LoadJFPGSuccess) {
                                if (frmMain.TacticsList.LoadTodayJFPGFromSQL_CompareAndSendIfDiff()){
                                    frmMain.Selffrm.AllEquipment.LoadJFPGSuccess = true;
                                }
                            }

                            if (!frmMain.Selffrm.AllEquipment.SetHistoryDataSuccess) {
                                if (frmSet.Set_HistoryData()){
                                    frmMain.Selffrm.AllEquipment.SetHistoryDataSuccess = true;
                                }
                            }

                            if (!frmMain.Selffrm.AllEquipment.DeleOldDataSuccess) {
                                if (frmSet.DeleOldData(DateTime.Now.AddDays(-180).ToString("yyyy-MM-dd"))){
                                    frmMain.Selffrm.AllEquipment.DeleOldDataSuccess = true;
                                }
                            }

                            if (!frmMain.Selffrm.AllEquipment.WriteDataInoneDaySuccess) {
                                if (frmMain.Selffrm.AllEquipment.WriteDataInoneDaySQL(frmSet.peElestic.rDate.ToString("yyyy-MM-dd"))) {
                                    frmSet.peElestic.rDate = DateTime.Now;
                                    frmMain.Selffrm.AllEquipment.WriteDataInoneDaySuccess = true;
                                }
                            }

                            //log.Error("frmMain.Selffrm.AllEquipment.SignalAlarmActive:" + frmMain.Selffrm.AllEquipment.SignalAlarmActive);
                            // 检查月份是否更新
                            if (frmMain.Selffrm.AllEquipment.mDate != DateTime.Now.ToString("yyyy-MM"))
                            {
                                frmSet.historyDatas.ClientPUMdemandMaxOld = (int)frmMain.Selffrm.AllEquipment.Client_PUMdemand_Max;
                                frmSet.historyDatas.E1PUMdemandMaxOld = (int)frmMain.Selffrm.AllEquipment.E1_PUMdemand_Max;
                                frmSet.historyDatas.ClientPUMdemandMax = 0;
                                frmMain.Selffrm.AllEquipment.Client_PUMdemand_Max = 0;
                                frmMain.Selffrm.AllEquipment.mDate = DateTime.Now.ToString("yyyy-MM");
                            }

                            // 检查日期是否更新
                            //if (frmMain.Selffrm.AllEquipment.rDate != DateTime.Now.ToString("yyyy-MM-dd"))
                            if (frmMain.Selffrm.AllEquipment.currentDate != DateTime.Now.ToString("yyyy-MM-dd"))
                            {
                                log.Error("日期更迭：" + "记录时间: " + frmMain.Selffrm.AllEquipment.currentDate + "当前时间： " + DateTime.Now.ToString("yyyy-MM-dd"));

                                // 更新日期
                                frmMain.Selffrm.AllEquipment.currentDate = DateTime.Now.ToString("yyyy-MM-dd");

                                // 重置EMS重启次数
                                if (frmSet.historyDatas != null && frmSet.historyDatas.RebootCount != 5)
                                {
                                    frmSet.historyDatas.RebootCount = 5;
                                }

                                //每日同步今日充放电量
                                frmMain.Selffrm.AllEquipment.SetHistoryDataSuccess = false;

                                // 删除180天前的数据
                                //frmSet.DeleOldData(DateTime.Now.AddDays(-180).ToString("yyyy-MM-dd"));
                                frmMain.Selffrm.AllEquipment.DeleOldDataSuccess = false;

                                // 记录收益
                                frmMain.Selffrm.AllEquipment.WriteDataInoneDaySuccess = false;

                                *//*                        if (frmMain.Selffrm.AllEquipment.Elemeter2 != null && frmMain.Selffrm.AllEquipment.Elemeter2.Prepared)
                                                        {
                                                            string strdate = frmSet.peElestic.rDate.ToString("yyyy-MM-dd");
                                                            if (!DBConnection.CheckRec("select *  FROM profit where rTime = '" + strdate + "'")) //防止重复插入
                                                            {
                                                                // 保存当天收益到数据库
                                                                //frmMain.Selffrm.AllEquipment.SaveDataInoneDay(frmMain.Selffrm.AllEquipment.rDate);
                                                                //frmMain.Selffrm.AllEquipment.SaveDataInoneDaySQL(frmMain.Selffrm.AllEquipment.rDate);

                                                                frmMain.Selffrm.AllEquipment.SaveDataInoneDaySQL(frmSet.peElestic.rDate.ToString("yyyy-MM-dd"));
                                                                log.Error("保存当天收益到数据库");

                                                                // 当日收益发送到云
                                                                frmMain.Selffrm.AllEquipment.CalculateProfit(frmSet.peElestic.rDate.ToString("yyyy-MM-dd"));
                                                                frmMain.Selffrm.AllEquipment.WaitRecPem = 1;//等待确认消息送达
                                                                //frmMain.Selffrm.AllEquipment.Report2Cloud.SaveProfit2Cloud(frmMain.Selffrm.AllEquipment.rDate);
                                                                frmMain.Selffrm.AllEquipment.Report2Cloud.SaveProfit2Cloud(frmSet.peElestic.rDate.ToString("yyyy-MM-dd"));
                                                                log.Error("当日收益发送到云");

                                                                // 更新日期
                                                                //frmMain.Selffrm.AllEquipment.rDate = DateTime.Now.ToString("yyyy-MM-dd");
                                                                frmSet.peElestic.rDate = DateTime.Now;

                                                                // 将当天的储能表和辅表的电能数据保存到INI
                                                                //frmMain.Selffrm.AllEquipment.WriteDataInoneDayINI(frmMain.Selffrm.AllEquipment.rDate);

                                                                // 将当天的储能表和辅表的电能数据保存到SQL
                                                                frmMain.Selffrm.AllEquipment.WriteDataInoneDaySQL(frmSet.peElestic.rDate.ToString("yyyy-MM-dd"));
                                                                log.Error("将当天的储能表和辅表的电能数据保存到SQL");
                                                            }
                                                            else
                                                            {
                                                                // 更新日期
                                                                frmSet.peElestic.rDate = DateTime.Now;
                                                                log.Error("重新更新日期");
                                                            }
                                                        }*/


        /*                        frmMain.Selffrm.AllEquipment.MeterCalibration();

                                frmMain.TacticsList.LoadJFPGFromSQL();//更新电表时段*//*

        // 校准电表日期
        frmMain.Selffrm.AllEquipment.MeterCalibrationSuccess = false;
        frmMain.Selffrm.AllEquipment.LoadJFPGSuccess = false;


        // 每晚00:00更新策略
        if (frmMain.TacticsList != null && frmSet.config.IsMaster == 1)
        {
            try
            {
                if (frmMain.Selffrm.AllEquipment.SignalAlarmActive)
                {
                    log.Error("监测到4G通信异常，使用情况1来装载策略");
                    frmMain.TacticsList.LoadFromMySQL(1);//重新装载策略
                }
                else {
                    log.Error("监测到4G通信正常，使用情况0来装载策略");
                    frmMain.TacticsList.LoadFromMySQL(0);//重新装载策略
                }
            }
            catch (Exception ex)
            {
                log.Error("定时器刷新数据库失败: " + ex.Message);
            }
        }

        // 更新均衡策略
        try
        {
            frmMain.BalaTacticsList.LoadFromMySQL();
        }
        catch (Exception ex)
        {
            log.Error("00:00更新均衡策略失败: " + ex.Message);
        }

        // 检查网络是否正常: 可能出现储能表通讯不正常且4G不通导致反复重启得问题
*//*                        if (frmMain.Selffrm.AllEquipment.SignalAlarmActive)
                        {
                            log.Error("检查网络不正常");
                            Program.RestartDevice();
                        }*//*
                    }
*//*                    else
                    {
                        //昨日收益重传
                        if (frmMain.Selffrm.AllEquipment.WaitRecPem == 1)
                        {
                            log.Error("未确认接收，重发报文");
                            DateTime previousDay = frmSet.peElestic.rDate.AddDays(-1);
                            string previousDayString = previousDay.ToString("yyyy-MM-dd");
                            frmMain.Selffrm.AllEquipment.Report2Cloud.SaveProfit2Cloud(previousDayString);
                        }
                    }*//*


                }
                catch (Exception ex)
                {
                    log.Error("Public_TimerCallback encountered an error: " + ex.Message);
                }

                // 等待 2分钟再进行下一次心跳
                Thread.Sleep(120000);
                //Thread.Sleep(1000);
            }
        }*/

        /*****************************************************************************************/



        /**********************************/
        /*                                */
        /*            定时器              */
        /*                                */
        /*********************************/

        private void StopInitRetryTimer()
        {

            if (DeviceData_Timer != null)
            {
                DeviceData_Timer.Dispose();
                DeviceData_Timer = null;
            }

        }

        public bool IniralizeFrmMain_Timer()
        {
            //更新数据看板显示数据
            if(!frmMain.Selffrm.InitializeUI_timer()) return false;

            //记录EMS功率日志
            if(!frmMain.Selffrm.InitializeCXFN_Timer()) return false;

            // 控制灯板输出
            if (!frmMain.Selffrm.InitializeDO_Timer()) return false;

            return true;
        }

        private bool InitializeDO_Timer()
        {
            try
            {
                int twoMinutesMs = 2 * 60 * 1000;
                DO_Timer = new System.Threading.Timer(DO_TimerCallback, null, 0, twoMinutesMs);
                return true;
            }
            catch (Exception ex)
            {
                log.Error("InitializeDO_Timer： " + ex.Message);
                return false;
            }
        }

        private void DO_TimerCallback(Object state)
        {
            // 检查是否已有正在执行的任务，避免重叠
            if (isDOExecuting)
            {
                log.Info("GPIO_Timer_TimerCallback is still executing. Skipping this tick to avoid overlap.");
                return;
            }

            isDOExecuting = true;

            try
            {
                // EMS电源指示灯
                frmSet.PowerGPIO(1);

                // 电源指示灯
                frmSet.ePowerGPIO(1);

                // 故障指示灯
                if (frmMain.Selffrm.AllEquipment.ErrorState[2])
                {
                    frmSet.ErrorGPIO(1);
                }
            }
            catch (Exception ex)
            {
                log.Error("DO_TimerCallback encountered an error: " + ex.Message);
            }
            finally
            {
                isDOExecuting = false;
            }
        }


        private bool InitializeCXFN_Timer()
        {
            try
            {
                CXFN_Timer = new System.Threading.Timer(CXFN_TimerCallback, null, 0, 10000);
                return true;
            }
            catch (Exception ex)
            {
                log.Error("InitializeCXFN_Timer创建失败： " + ex.Message);
                return false;
            }
        }
        private void CXFN_TimerCallback(Object state)
        {
            // 检查是否已有正在执行的任务，避免重叠
            if (isCXFNExecuting)
            {
                log.Info("CXFN_TimerCallback is still executing. Skipping this tick to avoid overlap.");
                return;
            }

            isCXFNExecuting = true;

            try
            {
                // 根据 SysCount 的值调用不同的日志方法
                if (frmSet.config.IsMaster == 1)
                {
                    if (frmSet.config.SysCount > 1)
                    {
                        frmMain.Selffrm.AllEquipment.MutiReflux_Log();
                    }
                    else
                    {
                        frmMain.Selffrm.AllEquipment.SingleReflux_Log();
                    }
                }
                else
                {
                    frmMain.Selffrm.AllEquipment.Client_Log();
                }

            }
            catch (Exception ex)
            {
                log.Error("CXFN_TimerCallback encountered an error: " + ex.Message);
            }
            finally
            {
                isCXFNExecuting = false;
            }
        }

        private bool InitializeUI_timer()
        {
            // 每5秒修正 UI
            try
            {
                UI_timer = new System.Threading.Timer(UI_timerCallback, null, 0, 5000);
                return true;
            }
            catch (Exception ex)
            {
                log.Error("InitializeUI_timer创建失败:" + ex.Message);
                return false;
            }
        }

        private void UI_timerCallback(Object state)
        {
            // 检查是否已有任务在执行，避免重叠
            if (isUiExecuting)
            {
                log.Info("UI_timerCallback is still executing. Skipping this tick to avoid overlap.");
                return;
            }

            isUiExecuting = true;

            try
            {
                // 确认页面是否在焦点内
                if (!frmMain.Selffrm.BeFoused)
                    return;

                // 更新策略状态和功率显示
                UpdatePowerState();

                // 更新温度
                UpdateTemperatureDisplay();

                // 更新 SOC
                UpdateSOCDisplay();

                // 更新电表数据
                UpdateMeterDisplay();
            }
            catch (Exception ex)
            {
                log.Error("UI_timerCallback encountered an error: " + ex.Message);
            }
            finally
            {
                isUiExecuting = false;
            }
        }

        private void UpdatePowerState()
        {
            if (frmMain.Selffrm.AllEquipment.PCSList.Count > 0)
            {
                string strCap = "手动";
                if (TacticsList.TacticsOn)
                    strCap = "策略";
                else if (frmSet.config.PCSGridModel == 1)
                    strCap = "离网";
                else if (frmSet.config.SysMode == 2)
                    strCap = "网控";

                double allUkva = frmMain.Selffrm.AllEquipment.PCSList[0].allUkva;
                string stateText = strCap + (allUkva > 0.5 ? "放电" : allUkva < -0.5 ? "充电" : "待机");
                string powerText = allUkva.ToString("F1") + "kw";

                if (frmMain.Selffrm.labState.IsHandleCreated && frmMain.Selffrm.labPCSuKW.IsHandleCreated)
                {
                    frmMain.Selffrm.Invoke((Action)(() =>
                    {
                        frmMain.Selffrm.labState.Text = stateText;
                        frmMain.Selffrm.labPCSuKW.Text = powerText;
                    }));
                }
            }
        }

        private void UpdateTemperatureDisplay()
        {
            if (frmMain.Selffrm.AllEquipment.TempControl != null)
            {
                double indoorTemp = frmMain.Selffrm.AllEquipment.TempControl.indoorTemp;
                if (frmMain.Selffrm.labACState.IsHandleCreated)
                {
                    frmMain.Selffrm.Invoke((Action)(() =>
                    {
                        frmMain.Selffrm.labACState.Text = indoorTemp.ToString() + "℃";
                    }));
                }
            }
        }

        private void UpdateSOCDisplay()
        {
            double BMSSOC = frmMain.Selffrm.AllEquipment.BMSSOC;
            if (frmMain.Selffrm.labSOC.IsHandleCreated && frmMain.Selffrm.vpbSOC.IsHandleCreated)
            {
                frmMain.Selffrm.Invoke((Action)(() =>
                {
                    frmMain.Selffrm.labSOC.Text = BMSSOC.ToString() + "%";
                    frmMain.Selffrm.vpbSOC.Value = (int)BMSSOC;
                }));
            }
        }

        private void UpdateMeterDisplay()
        {
            if (frmMain.Selffrm.AllEquipment.Elemeter2 != null)
            {
                double GridKVA = frmMain.Selffrm.AllEquipment.GridKVA;
                double PCSOKWH = frmMain.Selffrm.AllEquipment.Elemeter2.PUkwh[0];
                double PCSPKWH = frmMain.Selffrm.AllEquipment.Elemeter2.OUkwh[0];
                double E2OKWH = frmMain.Selffrm.AllEquipment.E2OKWH[0];
                double E2PKWH = frmMain.Selffrm.AllEquipment.E2PKWH[0];

                if (frmMain.Selffrm.labGridkva.IsHandleCreated &&
                    frmMain.Selffrm.labPCSOKWH.IsHandleCreated &&
                    frmMain.Selffrm.labPCSPKWH.IsHandleCreated &&
                    frmMain.Selffrm.labE2PKWH.IsHandleCreated &&
                    frmMain.Selffrm.labE2OKWH.IsHandleCreated &&
                    frmMain.Selffrm.labelDelay.IsHandleCreated &&
                    frmMain.Selffrm.labelJitter.IsHandleCreated)
                {
                    frmMain.Selffrm.Invoke((Action)(() =>
                    {
                        frmMain.Selffrm.labGridkva.Text = GridKVA.ToString("F3");
                        frmMain.Selffrm.labPCSOKWH.Text = PCSOKWH.ToString("F3");
                        frmMain.Selffrm.labPCSPKWH.Text = PCSPKWH.ToString("F3");
                        frmMain.Selffrm.labE2PKWH.Text = E2PKWH.ToString("F3");
                        frmMain.Selffrm.labE2OKWH.Text = E2OKWH.ToString("F3");
                        frmMain.Selffrm.labelDelay.Text = frmMain.Selffrm.AllEquipment.SignalDelay.ToString("F3");
                        frmMain.Selffrm.labelJitter.Text = frmMain.Selffrm.AllEquipment.SignalDelayJitter.ToString("F3");
                    }));
                }
            }
        }

        /*************************************************************************************************************************/

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            SysThreathStoped = true;

            // 停止 Modbus 从站
            _modbusSlave?.Stop();
        }

        /// <summary>
        /// 在关闭其他窗体后，显示主窗体
        /// </summary>
        public static void ShowMainForm()
        {
            if (Selffrm == null)
                return;
            //增加自适应屏幕
            int iW = Screen.PrimaryScreen.Bounds.Width;
            int iH = Screen.PrimaryScreen.Bounds.Height;
            if (iW != 1024)
            {
                Selffrm.WindowState = FormWindowState.Normal;
                Selffrm.StartPosition = FormStartPosition.CenterScreen;
                Selffrm.Left = (int)Math.Round((iW - 1024) / 2.0);
                Selffrm.Top = (int)Math.Round((iH - 768) / 2.0);
            }
            else
            {
                Selffrm.StartPosition = FormStartPosition.CenterScreen;
                Selffrm.WindowState = FormWindowState.Maximized;
            }


            Selffrm.Show();

            Selffrm.BringToFront();
            //Selffrm.Activate();
            Selffrm.BeFoused = true;
        }

        public static void ShowDebugMSG(string astrError)
        {
#if DEBUG
            // MessageBox.Show(astrError);
#endif
        }


        /// <summary>
        /// 委托更新显示
        /// </summary>
        static public void ShowShedule2Char(bool aCleanAllData)
        {
            //Selffrm.Invoke(new UpdateChart(frmMain.TacticsList.ShowTactic2Char), new object[] { frmMain.Selffrm.ctMain, aCleanAllData });
        }

        //加载
        private void frmMain_Load(object sender, EventArgs e)
        {
            /*            this.DoubleBuffered = true;
                        this.Width = 1024;
                        this.Height = 768;
                        SetFormPower(UserPower);

                        //链接网络 ----非拨号网络无效
                        SysIO.Connect4G();
                        //检查是否有断网数据
                        if (!NetTime.IsConnectInternet())
                            frmMain.ShowDebugMSG("网络连接异常！");

                        frmFlash.AddPostion(10);
                        //-------打开监视操作进程或者time，在无人操作时候进入休眠并关闭屏幕和注销用户
                        frmFlash.AddPostion(10);
                        //初始化窗体，提高将来的速度
                        frmSet.INIForm();
                        frmFlash.AddPostion(10);
                        frmoneUser.INIForm();
                        frmFlash.AddPostion(10);
                        frmKeyBoard.INIForm();
                        frmFlash.AddPostion(10);
                        frmState.INIForm();
                        frmFlash.AddPostion(10);
                        frmLogin.INIForm();
                        //////////////////////////////////////
                        Thread.Sleep(500);
                        frmFlash.AddPostion(10);
                        //AllEquipment.Report2Cloud.mqttConnect();
                        frmFlash.AddPostion(10);
                        //打开debug的串口
                        try
                        {

                        }
                        catch (Exception ex)
                        {
                            ShowDebugMSG(ex.ToString());
                        }
                        frmFlash.AddPostion(10);

                        frmFlash.AddPostion(10);
                        frmLogin.INIForm();
                        //
                        frmFlash.CloseFlashForm();


                        Control.CheckForIllegalCrossThreadCalls = false;*/

        }

        static public bool CheckUserInf(string astrName, string astrPassword)
        {
            if ((astrName == "chiku") || (astrPassword == "1100"))
                return true;
            else
                return false;
        }


        private void btnLine_Click(object sender, EventArgs e)
        {
            BeFoused = false;
            frmLine.ShowForm();
        }

        private void btnState_Click(object sender, EventArgs e)
        {
            BeFoused = false;
            frmState.ShowForm();
        }

        private void btnSet_Click(object sender, EventArgs e)
        {
            BeFoused = false;
            frmSet.ShowForm();
        }


        private void btnLogin_Click(object sender, EventArgs e)
        {
            if (frmMain.UserID != "")
            {
                frmMain.UserID = "";
                frmMain.UserPower = -1;
                btnLogin.Text = "用户登录";
            }
            else
            {
                BeFoused = false;
                frmLogin.ShowForm();
                if (frmMain.UserID != "")
                    Selffrm.btnLogin.Text = "注销登录";
            }
            SetFormPower(UserPower);

        }

        private void btnWarning_Click(object sender, EventArgs e)
        {
            BeFoused = false;
            frmWarrning.ShowForm();
        }

        private void btnQuery_Click(object sender, EventArgs e)
        {
            BeFoused = false;
            frmQuery.ShowForm();
            // BeFoused = true;
            //ShowMainForm();
            //SysIO.WinExec("", (int)SysIO.ShowWindowCommands.SW_SHOW);
        }

        private void btnAbout_Click(object sender, EventArgs e)
        {
            BeFoused = false;
            frmAbout.ShowForm();
        }

        private void spDebug_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            try
            {
                //Thread.Sleep(100);  //（毫秒）等待一定时间，确保数据的完整性 int len
                //int len = spDebug.BytesToRead;

                //if (len != 0)
                //{
                //    byte[] buff = new byte[len];
                //    spDebug.Read(buff, 0, len);
                //    //receive = Encoding.Default.GetString(buff);//数据接收内容
                //    //this.Invoke(spDebugData, buff);
                //}
            }
            catch (Exception ex)
            {
                ShowDebugMSG(ex.ToString());
            }
        }



        private void button2_Click_1(object sender, EventArgs e)
        {
            ////关闭空调
            //AllEquipment.TempControl.TCPowerOn(false);
            ////关闭预充
            ////AllEquipment.BMS.PowerOn(false);
            ////远端控制关闭
            //AllEquipment.PCSList[0].SetSysData(82, 0xFF00);
            //AllEquipment.PCSList[0].ExcSetPCSPower(false);
            //清理故障
            // AllEquipment.PCSList[0].SetSysData(76, 0xFF00);
            //AllEquipment.[0].SetSysData(76, 0xFF00);
            //远程
            //AllEquipment.PCSList[0].SetSysData(82, 0xFF00);
            //离线
            //AllEquipment.PCSList[0].SetSysData(84, 1);
            //AllEquipment.PCSList[0].SetSysData(84, 0);
            ////负给电网放电，正从电网充电
            //AllEquipment.PCSList[0].SetSysData(55, -20);
            //AllEquipment.PCSList[0].SetSysData(56, -20);
            //AllEquipment.PCSList[0].SetSysData(57, -20);
            //充放电先打开空调
            //AllEquipment.TempControl.TCPowerOn(true);
            ////开始预充
            //AllEquipment.BMS.PowerOn(true);
            ////设置为远端控制
            //AllEquipment.PCSList[0].SetSysData(82, 0xFF00);
            //AllEquipment.PCSList[0].ExcSetPCSPower(true);
            //frmMain.ShowDebugMSG("error!");
            //AllEquipment.Report2Cloud.Save2CloudFile();
            //SysIO.ConvertToJson(atest, "d:\\test.json");
            //int myint32 = -1;
            //UInt32 myuint32 = (UInt32)myint32;

            //uint myuint32 = 4294967295;
            //int myint32 = (int)myuint32;

            //short myShort = -1;
            //ushort myUshort = (ushort)myShort;

            //ushort myUshort = 65535;
            //short myShort = (short)myUshort;

            //DateTime dt = DateTime.Now;
            //double n = dt.ToOADate();//时间转化为浮点数
            //DateTime origintime = DateTime.FromOADate(n);//浮点数转化为时间

            //string aTime = "";
            // AllEquipment.Elemeter2.GetSysData(63, ref aTime);
            //button2.Text = aTime;
            // AllEquipment.Elemeter2.SetTime(new byte[] { 05, 01, 18, 29, 1, 23 });
            //AllEquipment.Elemeter2.SetTime(new byte[] { 5, 07, 12, 17, 1, 23 });
            //AllEquipment.Elemeter2.SetTime(new byte[] { 5, 07, 12, 17, 1, 23 });
            //AllEquipment.Elemeter2.SetTime(new byte[] { 1, 23 });
            //AllEquipment.Report2Cloud.Save2CloudFile(DateTime.Now);

        }




        /// <summary>
        /// 屏保后需要处理的函数，1、退出所有窗口，2、退出登录系统
        /// </summary>
        public static void AutoLoadout()
        {
            //qiao
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DBConnection.RecordLOG("通讯异常", "反应超时", "无法判断具体设备");
        }


        private void button10_Click(object sender, EventArgs e)
        {
            AllEquipment.TempControl.RecodError("", "", 0, 0, "", true);
        }

        private void btnMain_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            //if (NetTime.GetandSetTime())
            //{
            //    DateTime dtTemp = DateTime.Now;
            //    byte[] aTime = { (byte)dtTemp.Second, (byte)dtTemp.Minute, (byte)dtTemp.Hour, (byte)dtTemp.Day,
            //                              (byte)dtTemp.Month, (byte)(dtTemp.Year-2000) };
            //    if (AllEquipment.Elemeter1 != null)
            //        AllEquipment.Elemeter1.SetTime(aTime);
            //    if (AllEquipment.Elemeter2 != null)
            //        AllEquipment.Elemeter2.SetTime(aTime);
            //    byte[] aTime2 = { (byte)(dtTemp.Year-2000),(byte)dtTemp.Month, (byte)dtTemp.Day,
            //                            (byte)dtTemp.Hour,(byte)dtTemp.Minute, (byte)dtTemp.Second  };
            //    if (AllEquipment.Elemeter3 != null)
            //        AllEquipment.Elemeter3.SetTime(aTime2);
            //}
            //byte[] aTime = { (byte)dtTemp.Second, (byte)dtTemp.Minute, (byte)dtTemp.Hour, (byte)dtTemp.Day,
            //(byte)dtTemp.Month, (byte)(dtTemp.Year-2000) };
            //    if (AllEquipment.Elemeter1 != null)
            //        AllEquipment.Elemeter1.SetTime(aTime);
            //    if (AllEquipment.Elemeter2 != null)
            //AllEquipment.Elemeter2.SetTime  (aTime);
        }

        private void button1_Click_3(object sender, EventArgs e)
        {
            TacticsList.AddOneStep(ctMain, DateTime.Now, -1 * AllEquipment.Elemeter2.AllUkva, AllEquipment.Elemeter2.Gridkva, AllEquipment.Elemeter2.Subkw);
        }

        private void label2_Click(object sender, EventArgs e)
        {
            //this.AllEquipment.Elemeter3.Save2DataSource(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            // Selffrm.AllEquipment.Report2Cloud.SaveProfit2Cloud(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));//qiao
        }

        private void frmMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            System.Environment.Exit(0);
        }

        private bool GetCommandID(byte[] Resoursedata, ref int aCommandID, ref int aAddr)
        {
            bool bResult = ModbusBase.CheckResponse(Resoursedata);
            if (bResult)
            {
                //qiao
                // aCommandID= Resoursedata
                return true;
            }
            else
                return false;
        }


        private void btnControl_Click(object sender, EventArgs e)
        {
            BeFoused = false;
            frmControl.ShowForm();
        }
    }

}


public class InitializationManager
{
    private static readonly ILog log = LogManager.GetLogger("InitializationManager");

    public enum InitStep
    {
        TCPServerEvent,
        ModbusTcpClientEvent,
        FormControl,
        FormUser,
        FormKeyBoard,
        FormSet,
        FormState,
        FormLogin,
        LoadForm,
        FormAbout,
        FormLine
    }

    private static Dictionary<InitStep, bool> initStatus = new Dictionary<InitStep, bool>();
    private static Dictionary<InitStep, string> stepDescriptions = new Dictionary<InitStep, string>();

    static InitializationManager()
    {
        // 初始化状态字典和描述
        foreach (InitStep step in Enum.GetValues(typeof(InitStep)))
        {
            initStatus[step] = false;
            stepDescriptions[step] = step.ToString();
        }
    }

    //无返回bool，对于无异常即为成功
    public static bool InitializeComponent(InitStep step, Action initAction)
    {
        try
        {
            log.Error($"开始初始化: {stepDescriptions[step]}");
            initAction(); // 执行初始化动作
            initStatus[step] = true; // 如果没有异常，则认为初始化成功
            log.Error($"成功初始化: {stepDescriptions[step]}");
            return true;
        }
        catch (Exception ex)
        {
            log.Error($"初始化失败 {stepDescriptions[step]}: {ex.Message}");
            return false;
        }
    }

    //有返回bool
    public static bool InitializeComponent(InitStep step, Func<bool> initAction)
    {
        try
        {
            log.Error($"开始初始化: {stepDescriptions[step]}");
            bool result = initAction(); // 调用初始化方法并获取其结果
            if (result)
            {
                initStatus[step] = true;
                log.Error($"成功初始化: {stepDescriptions[step]}");
            }
            else
            {
                log.Error($"初始化未成功完成: {stepDescriptions[step]}");
            }
            return result;
        }
        catch (Exception ex)
        {
            log.Error($"初始化失败 {stepDescriptions[step]}: {ex.Message}");
            return false;
        }
    }
}
