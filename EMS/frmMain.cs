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


        ///主从串口通信参数
        private delegate void OnReceiveCMDDelegate(int DataSourceType, byte[] aByteData);//建立事件委托  
        private event OnReceiveCMDDelegate OnReceiveCMDEvent;
        /////策略相关
        //时段电价
        static public ElectrovalenceListClass ElectrovalenceList = new ElectrovalenceListClass();
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
        public CIEC104Slave   Slave104 = new CIEC104Slave();
        //test 

        //private delegate void TCPserver.OnReceiveDataEventDelegate(int DataSourceType, byte[] aByteData);//建立事件委

        //12.5
        public EMSEquipment Model4G = new EMSEquipment();

        //定时器
        private static System.Threading.Timer UI_timer;
        private static System.Threading.Timer BalaTacitc_Timer;
        private static System.Threading.Timer Public_Timer;
        private static System.Threading.Timer CXFN_Timer;//超限防逆log
        private static System.Threading.Timer Led_Timer;
        private static System.Threading.Timer LiquidCold_Timer;
        private static System.Threading.Timer TestSignalStrength_Timer;
        private static System.Threading.Timer TemperControl_Timer;

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

        //8.8
        private static ILog log = LogManager.GetLogger("frmMain");

        //tcp
        //对接主从通讯
        public TCPServerClass ModbusTcpServer = new TCPServerClass();
        public TCPClientClass ModbusTcpClient = new TCPClientClass();


        public DateTime receive_time_start ;
        public DateTime receive_time_end ;
        public DateTime receive_time_send;


        static public PID pid = new PID();

        public frmMain()
        { 
            InitializeComponent();
            Selffrm = this;
            Text = "EMS system";

            //委托与事件挂钩，当事件发生时将委托给函数OnReceive104CMD
            TCPserver.OnReceiveDataEvent2 +=new Modbus.TCPServerClass.OnReceiveDataEventDelegate2(OnReceive104CMD2);

            //tcp
            ModbusTcpClient.OnReceiveDataEvent2 += new Modbus.TCPClientClass.OnReceiveDataEventDelegate2(OnReceiveModbusTcpClientCMD);//从机接收消息触发事件

            LoadForm();
        }


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
                        frmMain.Selffrm.ModbusTcpClient.SendMSG(CloudClass.Back3Data(iAddr));
                    }
                    else
                    {
                        frmMain.Selffrm.ModbusTcpClient.SendMSG(CloudClass.Back3Data(iAddr, iLen));
                        //frmMain.Selffrm.ModbusTcpClient.clientSocket.Send(CloudClass.Back3Data(iAddr, iLen));
                    }
                    break;
                case 0x06://设置
                    AllEquipment.NetConnect = true;
                    CloudClass.Active6Data(iAddr, (int)iData);
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


        private long GetCMDFunctionID(byte[] aByteData,ref int aID, ref int aCommID, ref short aAddr, ref short aDataLen)
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
                        iResult=(Int16) aDataLen;
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
        public string ToHexStrFromByte( byte[] byteDatas)
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
                    if (CloudClass.Back3Data(iAddr) != null)
                    {
                        ////modbus返回:使用缓冲区中的数据将指定数量的字节写入串行端口。
                        frmMain.Selffrm.ems.m485.sp.Write(CloudClass.Back3Data(iAddr), 0, 7);
                    }
                    break;
                case 0x06://设置                     
                    frmMain.Selffrm.ems.m485.sp.Write(aByteData, 0, aByteData.Length);
                    CloudClass.Active6Data(iAddr, (int)iData);
                    break;
                default:
                    break;
            }
        }

        static public frmMain LoadForm()
        {
            //int[] a = { 0, 1 };
            //frmMain.Selffrm = new frmMain();
            try
            {
                //延迟等待2min
                //Thread.Sleep(120000);

                //配置Config配置文件地址，获取配置文件中的设定
                string strSysPath = Convert.ToString(System.AppDomain.CurrentDomain.BaseDirectory);
                frmSet.INIPath = strSysPath + "Config.ini";
                //配置均衡电池文件地址
                frmSet.BalaPath = strSysPath + "BalaCell.txt";         
                log.Warn("CHEECO-START");

                ////连接数据库
                DBConnection conn = new DBConnection();
                DBConnection.SetDBGrid(frmMain.Selffrm.dbvError);
                DBConnection.CheckTables();
                frmSet.LoadCloudLimitsFromMySQL();
                frmSet.LoadConfigFromMySQL();
                frmSet.LoadVariChargeFromMySQL();
                frmSet.LoadComponentSettingsFromMySQL();

                //同步今日剩余重启次数
                 frmMain.Selffrm.AllEquipment.RebootCount = frmSet.historyDatas.RebootCount;
                
               
                //获取历史需量
                if (frmSet.config.IsMaster == 1)
                {
                    if (frmSet.LoadHistoryDataFromMySQL())
                    {
                        frmMain.Selffrm.AllEquipment.E1_PUMdemand_Max_old = frmSet.historyDatas.E1PUMdemandMaxOld;
                        frmMain.Selffrm.AllEquipment.Client_PUMdemand_Max_old = frmSet.historyDatas.ClientPUMdemandMaxOld;
                        frmMain.Selffrm.AllEquipment.Client_PUMdemand_Max = frmSet.historyDatas.ClientPUMdemandMax;
                    }
                }

                //从数据库中下载并实例化设备部件对象(包括 comlist)
                if (!frmMain.Selffrm.AllEquipment.LoadSetFromFile())
                {
                    //加载数据库或者协议文件失败，则EMS重启
                    frmSet.RestartApplicationNoCount();
                }


                //初始化端口
                frmSet.InitGPIO();
                //初始化灯板
                frmMain.Selffrm.AllEquipment.init_LED();
                //初始化液冷机
                if (frmMain.Selffrm.AllEquipment.LiquidCool != null)
                {
                    frmMain.Selffrm.AllEquipment.init_LiquidCool();
                }
                //初始化BMS功能等级
                if (frmMain.Selffrm.AllEquipment.BMS != null)
                {
                    frmMain.Selffrm.AllEquipment.BMS.CheckFunctionLevel();
                }
                
                //配置DofD电能历史文件的路径
                //UpData:从云接受JSON文件
                //DownData:向云上传JSON文件
                frmMain.Selffrm.AllEquipment.DofD = strSysPath + "DofD.ini";//当天数据记录，开始的波峰充放电数据
                frmMain.Selffrm.AllEquipment.DoPU = strSysPath + "DoPU.ini";//记录客户负载最大需量
                frmMain.Selffrm.AllEquipment.Report2Cloud.strUpPath = strSysPath + "UpData";
                frmMain.Selffrm.AllEquipment.Report2Cloud.strDownPath = strSysPath + "DownData";
                //frmMain.Selffrm.AllEquipment.rDate = DateTime.Now.ToString("yyyy-MM-dd");

                //配置各个部件的设备码
                string strID = frmSet.config.SysID;
                if (strID.Length >= 7)
                    strID = strID.Substring(strID.Length - 7, 7);//截取SysID的最后7位
                frmMain.Selffrm.AllEquipment.iot_code = "ems" + strID;
                frmMain.Selffrm.AllEquipment.Fire.iot_code ="fire"+ strID;
                frmMain.Selffrm.AllEquipment.Profit2Cloud.iot_code = "ems" + strID;

                frmMain.Selffrm.AllEquipment.Report2Cloud.IniClound();//配置topic
                                                                      //连接mqtt
                frmMain.Selffrm.AllEquipment.Report2Cloud.mqttConnect();
                frmMain.Selffrm.AllEquipment.LoadErrorState();//读取数据库中的故障信息  
                frmFlash.AddPostion(10);
                //
                TacticsList.Parent = frmMain.Selffrm.AllEquipment;
                //下载电价信息
                ElectrovalenceList.LoadFromMySQL();
                //下载策略
                TacticsList.LoadFromMySQL();
                //策略曲线图展示
                ShowShedule2Char(true);
                //下载均衡策略
                 BalaTacticsList.LoadFromMySQL();
                
                try
                {
                    //先下载电表数据
                    for (int i = 0; i < 1; i++)
                    {
                        //
                        if (frmMain.Selffrm.AllEquipment.Elemeter1List != null)
                        {
                            foreach (Elemeter1Class tempEleMeter in frmMain.Selffrm.AllEquipment.Elemeter1List)
                            {
                                tempEleMeter.GetDataFromEqipment();
                            }
                        }
                        //
                        if (frmMain.Selffrm.AllEquipment.Elemeter2 != null)
                            frmMain.Selffrm.AllEquipment.Elemeter2.GetDataFromEqipment();
                        if (frmMain.Selffrm.AllEquipment.Elemeter3 != null)
                            frmMain.Selffrm.AllEquipment.Elemeter3.GetDataFromEqipment();
                        if (frmMain.Selffrm.AllEquipment.Elemeter4 != null)
                            frmMain.Selffrm.AllEquipment.Elemeter4.GetDataFromEqipment();
                    }
                }
                catch
                { }
                frmFlash.AddPostion(10);

/*                if (!frmMain.Selffrm.AllEquipment.ReadDataInoneDayINI())//如果没有找到前一天保留的数据，就把现在电表数据记录为开始
                {
                    frmMain.Selffrm.AllEquipment.SaveDataInoneDay(Selffrm.AllEquipment.rDate);
                    //当日收益发送到云
                    Selffrm.AllEquipment.Report2Cloud.SaveProfit2Cloud(Selffrm.AllEquipment.rDate);//qiao
                    //当日表数据记录INI文件
                    Selffrm.AllEquipment.rDate = DateTime.Now.ToString("yyyy-MM-dd");
                    frmMain.Selffrm.AllEquipment.WriteDataInoneDayINI(Selffrm.AllEquipment.rDate);
                }*/

                frmMain.Selffrm.AllEquipment.ReadDataInoneDaySQL();//必须在设备初始化结束后

                //校验电表数据
                Selffrm.AllEquipment.Power_CRC();

                //初始化今日充放数据
                Selffrm.AllEquipment.InitE2Power();

                //校准电表日期
                frmMain.Selffrm.AllEquipment.MeterCalibration();


                //8.7 每台主机初始化对外接口
                BaseEquipmentClass oneEquipment = null;
                oneEquipment = new EMSEquipment();
                oneEquipment.Parent = frmMain.Selffrm.AllEquipment;
                oneEquipment = (EMSEquipment)oneEquipment;
                oneEquipment.LoadCommandFromFile();


                //网络控制或者联机控制

                //连接硬件：4G通讯模块
                frmMain.Selffrm.Model4G.m485 = new modbus485();
                frmMain.Selffrm.Model4G.m485.ParentEquipment = frmMain.Selffrm.AllEquipment; //必不可少
                frmMain.Selffrm.Model4G.m485.Open("Com11", 115200, 8, System.IO.Ports.Parity.None, System.IO.Ports.StopBits.One);

                //若配置接入104服务
                if (frmSet.config.Open104 == 1)
                {
                    frmMain.Selffrm.Slave104.IEC104_Init();

                    if (frmMain.Selffrm.TCPserver.TCPServerIni(2404))//配置主站开放2404端口
                    {
                        frmMain.Selffrm.TCPserver.StartMonitor2404();//监听客户端连接  
                    }                    
                }

                //使用TCP/IP通讯方式
                if (frmSet.config.IsMaster == 1)
                {
                    if (frmSet.config.ConnectStatus == "tcp")
                    {
                        frmMain.Selffrm.ModbusTcpServer.clientManager = new ClientManager();
                        if (frmMain.Selffrm.ModbusTcpServer.TCPServerIni(502))
                        {
                            frmMain.Selffrm.ModbusTcpServer.StartMonitor502();
                        }
                        else
                        {
                            frmSet.RestartWindows();
                        }
                    }
                    else if (frmSet.config.ConnectStatus == "485")
                    {
                        //从机的列表
                        for (int i = 0; i < frmSet.config.SysCount-1; i++)//主机调控
                        {
                            EMSEquipment oneEMSEquipment = new EMSEquipment();
                            oneEMSEquipment.LoadCommandFromFile();
                            oneEMSEquipment.ID = i + 2;
                            oneEMSEquipment.Parent = Selffrm.AllEquipment;
                            oneEMSEquipment.m485 = new modbus485();
                            oneEMSEquipment.m485.ParentEquipment = Selffrm.AllEquipment;
                            oneEMSEquipment.m485.Open(frmSet.config.DebugComName, 38400,
                              8, System.IO.Ports.Parity.None, System.IO.Ports.StopBits.One);
                            frmMain.Selffrm.AllEquipment.EMSList.Add(oneEMSEquipment);
                        }
                    }
                }
                else
                {
                    if (frmSet.config.ConnectStatus == "tcp")
                    {
                        frmMain.Selffrm.ModbusTcpClient.TCPClientIni(frmSet.config.MasterIp, 502);
                    }
                    else if (frmSet.config.ConnectStatus == "485")
                    {
                        frmMain.Selffrm.ems.ID = frmSet.config.i485Addr;
                        frmMain.Selffrm.ems.Parent = Selffrm.AllEquipment;
                        frmMain.Selffrm.ems.m485 = new modbus485();
                        frmMain.Selffrm.ems.m485.OpenEMS(frmSet.config.DebugComName, 38400, 8, System.IO.Ports.Parity.None, System.IO.Ports.StopBits.One);
                    }
                }

                //开启定时器
                frmMain.Selffrm.IniralizeFrmMain_Timer();
                frmMain.Selffrm.InitFrmMainClass_Threads();

                frmMain.Selffrm.AllEquipment.Report2Cloud.InitCloudClass_Timer(); 
                frmMain.Selffrm.AllEquipment.Report2Cloud.InitCloudClass_Threads();
            
                frmFlash.AddPostion(10);
                //开启任务多线程
                frmMain.Selffrm.AllEquipment.AutoReadData();

                //线程监控程序

                //定时器监控程序
                //frmMain.Selffrm.MonitorTimerAlive();
            }
            catch (Exception err)
            {
                frmMain.ShowDebugMSG(err.ToString());
            }
            return Selffrm;
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
        public void InitFrmMainClass_Threads()
        {
            try
            {
                StartPublicThread();
                if (BalaTacticsList != null)
                {
                    BalaTacticsList.AutoCheckBalaTactics();
                }

                if (TacticsList != null)
                {
                    TacticsList.AutoCheckTactics();
                }

            }
            catch (Exception ex)
            {
                log.Error("InitFrmMainClass_Threads: " + ex.Message);
            }
        }


        private void StartPublicThread()
        {
            try
            {
                // 创建并启动 Heartbeat 线程
                PublicThread = new Thread(PublicThreadCallback);
                PublicThread.IsBackground = true;
                PublicThread.Priority = ThreadPriority.Highest;
                PublicThread.Name = "PublicThread";
                PublicThread.Start();
            }
            catch (Exception ex)
            {
                log.Error("Error starting PublicThread: " + ex.Message);
            }
        }

        private void PublicThreadCallback()
        {
            while (true)
            {
                try
                {
                    // 检查月份是否更新
                    if (frmMain.Selffrm.AllEquipment.mDate != DateTime.Now.ToString("yyyy-MM"))
                    {
                        frmSet.historyDatas.ClientPUMdemandMaxOld = (int)frmMain.Selffrm.AllEquipment.Client_PUMdemand_Max;
                        frmSet.historyDatas.E1PUMdemandMaxOld = (int)frmMain.Selffrm.AllEquipment.E1_PUMdemand_Max;
                        frmSet.historyDatas.ClientPUMdemandMax = 0;
                        frmMain.Selffrm.AllEquipment.Client_PUMdemand_Max = 0;

                        frmSet.Set_HistoryData();
                        frmMain.Selffrm.AllEquipment.mDate = DateTime.Now.ToString("yyyy-MM");
                    }

                    // 检查日期是否更新
                    //if (frmMain.Selffrm.AllEquipment.rDate != DateTime.Now.ToString("yyyy-MM-dd"))
                    if (frmSet.peElestic.rDate.ToString("yyyy-MM-dd") != DateTime.Now.ToString("yyyy-MM-dd"))
                    {
                        // 重置EMS重启次数
                        if (frmSet.historyDatas != null && frmSet.historyDatas.RebootCount != 5)
                        {
                            frmSet.historyDatas.RebootCount = 5;
                            frmSet.Set_HistoryData();
                        }

                        // 删除180天前的数据
                        frmSet.DeleOldData(DateTime.Now.AddDays(-180).ToString("yyyy-MM-dd"));

                        if (frmMain.Selffrm.AllEquipment.Elemeter2 != null && frmMain.Selffrm.AllEquipment.Elemeter2.Prepared)
                        {
                            string strdate = frmSet.peElestic.rDate.ToString("yyyy-MM-dd");
                            if (!DBConnection.CheckRec("select *  FROM profit where rTime = " + strdate)) //防止重复插入
                            {
                                // 保存当天收益到数据库
                                //frmMain.Selffrm.AllEquipment.SaveDataInoneDay(frmMain.Selffrm.AllEquipment.rDate);
                                //frmMain.Selffrm.AllEquipment.SaveDataInoneDaySQL(frmMain.Selffrm.AllEquipment.rDate);
                                frmMain.Selffrm.AllEquipment.SaveDataInoneDaySQL(frmSet.peElestic.rDate.ToString("yyyy-MM-dd"));


                                // 当日收益发送到云
                                frmMain.Selffrm.AllEquipment.CalculateProfit();
                                //frmMain.Selffrm.AllEquipment.Report2Cloud.SaveProfit2Cloud(frmMain.Selffrm.AllEquipment.rDate);
                                frmMain.Selffrm.AllEquipment.Report2Cloud.SaveProfit2Cloud(frmSet.peElestic.rDate.ToString("yyyy-MM-dd"));

                                // 更新日期
                                //frmMain.Selffrm.AllEquipment.rDate = DateTime.Now.ToString("yyyy-MM-dd");
                                frmSet.peElestic.rDate = DateTime.Now;

                                // 将当天的储能表和辅表的电能数据保存到INI
                                //frmMain.Selffrm.AllEquipment.WriteDataInoneDayINI(frmMain.Selffrm.AllEquipment.rDate);

                                // 将当天的储能表和辅表的电能数据保存到SQL
                                frmMain.Selffrm.AllEquipment.WriteDataInoneDaySQL(frmSet.peElestic.rDate.ToString("yyyy-MM-dd"));
                            }
                        }

                        // 校准电表日期
                        frmMain.Selffrm.AllEquipment.MeterCalibration();

                        // 每晚00:00更新策略
                        if (frmMain.TacticsList != null && frmSet.config.IsMaster == 1)
                        {
                            try
                            {
                                frmMain.TacticsList.LoadFromMySQL();//重新装载策略
                                frmMain.TacticsList.LoadJFPGFromSQL();//更新电表时段
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
                    }

                    // 保存今日充放电量
                    frmMain.Selffrm.AllEquipment.CalculateNowPower();

                }
                catch (Exception ex)
                {
                    log.Error("Public_TimerCallback encountered an error: " + ex.Message);
                }

                // 等待 2分钟再进行下一次心跳
                Thread.Sleep(120000);
            }
        }

        /*****************************************************************************************/



        /**********************************/
        /*                                */
        /*            定时器              */
        /*                                */
        /*********************************/
        public void IniralizeFrmMain_Timer()
        {
            //更新数据看板显示数据
            frmMain.Selffrm.InitializeUI_timer();

            //记录EMS功率日志
            frmMain.Selffrm.InitializeCXFN_Timer();
        }


        private void InitializeCXFN_Timer()
        {
            CXFN_Timer = new System.Threading.Timer(CXFN_TimerCallback, null, 0, 10000);
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

        private void InitializeUI_timer()
        {
            // 每5秒修正 UI 
            UI_timer = new System.Threading.Timer(UI_timerCallback, null, 0, 5000);
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
            //关闭gpio
            frmSet.GPIOClose();
        }

        /// <summary>
        /// 在关闭其他窗体后，显示主窗体
        /// </summary>
        public static void ShowMainForm()
        {
            if (Selffrm == null)
                return; 
            //增加自适应屏幕
           int iW= Screen.PrimaryScreen.Bounds.Width;
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
            this.DoubleBuffered = true;
            this.Width = 1024;
            this.Height = 768;
            SetFormPower(UserPower);
           
/*            //策略曲线图展示
            ShowShedule2Char(true);*/

            //TacticsList.ShowTactic2Char(ctMain,true);
            //TacticsList.LoadHistay(ctMain);
             
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
                // spDebug.PortName = frmSet.DebugComName;
                //spDebug.BaudRate = frmSet.DebugRate;
                // spDebug.Open();
            }
            catch (Exception ex)
            {
                ShowDebugMSG(ex.ToString());
            }
            frmFlash.AddPostion(10);
            ///////////////////////////////////////////////////
            ///打开显示曲线和故障图线
            //DBConnection.SetDBGrid(dbvError);
            frmFlash.AddPostion(10);
            frmLogin.INIForm();
            //
            frmFlash.CloseFlashForm();

            //tneMax.SetIntValue(  frmSet.MaxGridKW );
            //tneMin.SetIntValue(frmSet.MinGridKW);
            Control.CheckForIllegalCrossThreadCalls = false;

            //if (frmSet.GPIO_Select_Mode == 0) frmSet.SetGPIOState(11, 1);
            //else frmSet.SetGPIOState(11, 0);
            //frmSet.SetGPIOState(15, 0);//Power on LED
            //Thread.Sleep(1000);
            //frmSet.SetGPIOState(15, 1);//Power on LED
            //Thread.Sleep(1000);
            //frmSet.SetGPIOState(15, 0);//Power on LED
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

        private void button2_Click(object sender, EventArgs e)
        {
            TacticsList.LoadHistay(ctMain);
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

        private bool GetCommandID(byte[] Resoursedata,ref int aCommandID,ref int aAddr)
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

        private void TmNetLink_Tick(object sender, EventArgs e)
        {
            //ping mqttfx 检查是否网络正常
            /*            Ping ping = new Ping();
                        PingReply reply;
                        try
                        {
                            reply = ping.Send("www.baidu.com");
                        }
                        catch (Exception)
                        {
                            if (frmMain.Selffrm.AllEquipment.HostStart == false)
                            {
                                SysIO.Reboot();
                            }
                        };*/

            if ((AllEquipment.Report2Cloud.mqttClient == null)||(!AllEquipment.Report2Cloud.mqttClient.IsConnected))
            {
                //AllEquipment.Report2Cloud.CreateClient();
                SysIO.Reboot();
            }
        }
    }

}