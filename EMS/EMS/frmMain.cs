#define DEBUG 

using IEC104;
using Modbus;
using System;
using System.Threading;
using System.Windows.Forms;
using System.Text;
using System.Windows.Forms.DataVisualization.Charting;
using log4net;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;

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

        //8.18
        //public ThreadPoolClass ThreadPool = new ThreadPoolClass();

        //8.23
        //北向通信参数

        //对接104电网
        public TCPClientClass TCPCloud = new TCPClientClass();
        public TCPServerClass TCPserver = new TCPServerClass();
        public CIEC104Slave   Slave104 = new CIEC104Slave();
        //private delegate void TCPserver.OnReceiveDataEventDelegate(int DataSourceType, byte[] aByteData);//建立事件委

        //12.5
        public EMSEquipment Model4G = new EMSEquipment();

        //8.8
        private static ILog log = LogManager.GetLogger("frmMain");

        //tcp
        //对接主从通讯
        public TCPServerClass ModbusTcpServer = new TCPServerClass();
        public TCPClientClass ModbusTcpClient = new TCPClientClass();

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
        private void OnReceiveModbusTcpClientCMD(object sender, byte[] aByteData)
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
            frmSet.SysMode = 2;
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
                    data[0] = (short)SysID; //ilen 是主机端赋予从机的虚拟地址号，返回虚拟地址号和实际设备号
                    message = ModbusBase.BuildCloundMSG((byte)frmSet.i485Addr, 0x20, 1, data);
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
        private void OnReceive104CMD2(System.Net.Sockets.Socket sender, byte[] msg, string strFromIP, int iPort)
        {
            //do+委托
            string hexString = BitConverter.ToString(msg);
            //"收到TCP消息：" + hexString

            Slave104.iec104_packet_parser(msg);

        }



        //处理接收到的104报文协议
        private void OnReceive104CMD(System.Net.Sockets.Socket sender, string strData, string strFromIP, int iPort)
        {
            //do+委托
            byte[] msg = Encoding.ASCII.GetBytes(strData);

            //string hexString = BitConverter.ToString(msg);

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

        //收到命令函数
        private void OnReceiveCMD(int DataSourceType, byte[] aByteData)
        {
            int SysID =0;
            int CMDID = 0;
            short iAddr = 0;
            short iLen = 0;
            long iData = 0;
            ////判断是否为传到的命令 
            //检查是否是为命令  //检查crc 

            //string RecCommandInfo = new StackTrace().ToString();

            if (!ModbusBase.CheckResponse(aByteData)) 
                return;
            
            //解析命令
            iData = GetCMDFunctionID(aByteData, ref SysID, ref CMDID, ref iAddr, ref iLen); 
            if (SysID != frmSet.i485Addr)
                return;

            AllEquipment.NetControl = true;
            AllEquipment.NetCtlTime = DateTime.Now;
            frmSet.SysMode = 2;
            byte[] message = new byte[7];
            short[] sData01 = { 00, 00 };
            switch (CMDID)
            {
                case 0x03://读取 
                    if(CloudClass.Back3Data(iAddr) != null)
                    {
                        ////modbus返回:使用缓冲区中的数据将指定数量的字节写入串行端口。
                        spNetControl.Write(CloudClass.Back3Data(iAddr), 0, 7);
                    }
                   // spNetControl.Write(CloudClass.Back3Data(iAddr),0,7);
                    //8.5从机接受
                    //frmMain.Selffrm.AllEquipment.WriteDataPCSCommandINI(Selffrm.AllEquipment.rDate, System.Text.Encoding.UTF8.GetString(aByteData));
                    break;
                case 0x06://设置                     
                    spNetControl.Write(aByteData, 0, aByteData.Length);

                    CloudClass.Active6Data(iAddr, (int)iData);
                    //8.4 收到指令后向从机发送确认报文
                    //frmMain.Selffrm.AllEquipment.EMS.PCSCommandConfirm();
                    //8.5System.DateTime.Now.ToString("g")
                    //"hh:mm:ss"
                    //frmMain.Selffrm.AllEquipment.WriteDataPCSCommandINI(System.DateTime.Now.ToString("hh:mm:ss"), ToHexStrFromByte(aByteData),iAddr);
                    break; 
                case 0x20://读取设备SN  
                    sData01[0] = 10002; ;
                    message = ModbusBase.BuildCloundMSG((byte)frmSet.i485Addr, 0x27, 01, sData01);
                    TCPCloud.SendMSG(message); 
                    break;
                case 0x21:
                    sData01[0] = (short)1;
                    message = ModbusBase.BuildCloundMSG(1, 0x22, 01, sData01);
                    TCPCloud.SendMSG(message); 
                    break;
                case 0x26: //闻讯间隔
                    sData01[0] = (short)1;
                    message = ModbusBase.BuildCloundMSG(1, 0x26, 01, sData01);
                    TCPCloud.SendMSG(message);
                    frmSet.YunInterval = iLen;
                    //设置云的读取间隔，判断两次无数据就会重新连接云（2B） 
                    TCPCloud.ReconnectTime = frmSet.YunInterval;//AllEquipment.AskInterval;
                    frmSet.SaveSet2File();//保存数据 
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

        //串口收到数据的事件
        private void spNetControl_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            //主机不接受任何网络指令
            if (frmSet.IsMaster)
            {
                spNetControl.DiscardOutBuffer();
                spNetControl.DiscardInBuffer();
                return;
            }
            //处理网络控制信息
            try
            {
                Thread.Sleep(100);  //（毫秒）等待一定时间，确保数据的完整性 int len        
                int len = spNetControl.BytesToRead;
                if (len != 0)
                {
                    byte[] buff = new byte[len];
                    spNetControl.Read(buff, 0, len);
                    OnReceiveCMDEvent = OnReceiveCMD;
                    this.BeginInvoke(OnReceiveCMDEvent, 1, buff);
                }
            }
            catch (Exception ex)
            {
                ShowDebugMSG(ex.ToString());
            }
            spNetControl.DiscardInBuffer();
        }

        static public frmMain LoadForm()
        {
            //int[] a = { 0, 1 };
            //frmMain.Selffrm = new frmMain();
            try
            { 
                //配置Config配置文件地址，获取配置文件中的设定
                string strSysPath = Convert.ToString(System.AppDomain.CurrentDomain.BaseDirectory);
                frmSet.INIPath = strSysPath + "Config.ini";
                //配置均衡电池文件地址
                frmSet.BalaPath = strSysPath + "BalaCell.txt";

                //读取配置文件
                frmSet.LoadSetInf();
                //初始化端口
                frmSet.InitGPIO();
                //连接数据库
                DBConnection conn = new DBConnection();
                DBConnection.SetDBGrid(frmMain.Selffrm.dbvError);
                //从数据库加载
                frmSet.LoadFromGlobalSet();
                //从数据库中加载配置信息
                frmSet.LoadFromConfig();

                //从数据库中下载并实例化设备部件对象(包括 comlist)
                frmMain.Selffrm.AllEquipment.LoadSetFromFile();
                //5.15
                frmMain.Selffrm.AllEquipment.init_LED();
                //11.30 BMS区分风冷和液冷字段配置
                if (frmMain.Selffrm.AllEquipment.TempControl != null)
                {
                    frmMain.Selffrm.AllEquipment.BMS.BMStype = 1;
                }
                else if (frmMain.Selffrm.AllEquipment.LiquidCool != null)
                {
                    frmMain.Selffrm.AllEquipment.BMS.BMStype = 2;
                }

                //配置DofD电能历史文件的路径
                //UpData:从云接受JSON文件
                //DownData:向云上传JSON文件
                frmMain.Selffrm.AllEquipment.DofD = strSysPath + "DofD.ini";//当天数据记录，开始的波峰充放电数据
                frmMain.Selffrm.AllEquipment.DoPU = strSysPath + "DoPU.ini";//记录客户负载最大需量
                frmMain.Selffrm.AllEquipment.Report2Cloud.strUpPath = strSysPath + "UpData";
                frmMain.Selffrm.AllEquipment.Report2Cloud.strDownPath = strSysPath + "DownData";
               
                //配置各个部件的设备码
                string strID= frmSet.SysID;
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
                if (BalaTacticsList != null)
                {
                    BalaTacticsList.LoadFromMySQL();
                }
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
                if (!frmMain.Selffrm.AllEquipment.ReadDataInoneDayINI())//如果没有找到前一天保留的数据，就把现在电表数据记录为开始
                {
                    frmMain.Selffrm.AllEquipment.SaveDataInoneDay(Selffrm.AllEquipment.rDate);
                    //当日收益发送到云
                    Selffrm.AllEquipment.Report2Cloud.SaveProfit2Cloud(Selffrm.AllEquipment.rDate);//qiao
                    //当日表数据记录INI文件
                    Selffrm.AllEquipment.rDate = DateTime.Now.ToString("yyyy-MM-dd");
                    frmMain.Selffrm.AllEquipment.WriteDataInoneDayINI(Selffrm.AllEquipment.rDate);
                }
                if (frmSet.IsMaster)
                {
                    if (!frmMain.Selffrm.AllEquipment.ReadDoPUini())
                    {
                        //更新的月份
                        lock (Selffrm.AllEquipment)
                        {
                            Selffrm.AllEquipment.Client_PUMdemand_Max = 0;
                            Selffrm.AllEquipment.WriteDoPUini();
                        }
                    }
                }

                //8.7 每台主机初始化对外接口
                BaseEquipmentClass oneEquipment = null;
                oneEquipment = new EMSEquipment();
                oneEquipment.Parent = frmMain.Selffrm.AllEquipment;
                oneEquipment = (EMSEquipment)oneEquipment;
                oneEquipment.LoadCommandFromFile();


                //网络控制或者联机控制
                if (!frmSet.IsMaster)
                {
                    frmMain.Selffrm.spNetControl.PortName = frmSet.DebugComName;
                    frmMain.Selffrm.spNetControl.BaudRate = 38400;//38400
                    frmMain.Selffrm.spNetControl.Open();

                }
                else
                {
                    //从机的列表
                    for (int i = 0; i < frmSet.SysCount-1; i++)//主机调控
                    {
                        EMSEquipment oneEMSEquipment = new EMSEquipment();
                        oneEMSEquipment.LoadCommandFromFile();
                        oneEMSEquipment.ID = i + 2;
                        oneEMSEquipment.Parent = Selffrm.AllEquipment;
                        oneEMSEquipment.m485 = new modbus485();
                        oneEMSEquipment.m485.ParentEquipment = Selffrm.AllEquipment;
                        oneEMSEquipment.m485.Open(frmSet.DebugComName, 38400,
                          8, System.IO.Ports.Parity.None, System.IO.Ports.StopBits.One);
                        frmMain.Selffrm.AllEquipment.EMSList.Add(oneEMSEquipment);
                    }
                }
                //连接硬件：4G通讯模块
                frmMain.Selffrm.Model4G.m485 = new modbus485();
                frmMain.Selffrm.Model4G.m485.ParentEquipment = frmMain.Selffrm.AllEquipment; //必不可少
                frmMain.Selffrm.Model4G.m485.Open("Com11", 115200, 8, System.IO.Ports.Parity.None, System.IO.Ports.StopBits.One);
                //若配置接入104服务
                if (frmSet.Open104 == 1)
                {
                    frmMain.Selffrm.TCPserver.TCPServerIni(2404);//配置主站开放2404端口
                    frmMain.Selffrm.TCPserver.StartMonitor104();//监听客户端连接
                }

                //使用TCP/IP通讯方式
                if (frmSet.IsMaster && frmSet.ConnectStatus == "tcp")
                {
                    frmMain.Selffrm.ModbusTcpServer.clientManager = new ClientManager();
                    frmMain.Selffrm.ModbusTcpServer.clientMap = new Dictionary<int, (Socket, object)>();
                    frmMain.Selffrm.ModbusTcpServer.TCPServerIni(502);
                    frmMain.Selffrm.ModbusTcpServer.StartMonitor502();
                }
                else if (!frmSet.IsMaster && frmSet.ConnectStatus == "tcp")
                {
                    frmMain.Selffrm.ModbusTcpClient.TCPClientIni(frmSet.MasterIp, 502);
                    //frmMain.Selffrm.ModbusTcpClient.StartMonitor();
                }

                frmFlash.AddPostion(10);
                //开启任务多线程
                frmMain.Selffrm.AllEquipment.AutoReadData();

            }
            catch (Exception err)
            {
                frmMain.ShowDebugMSG(err.ToString());
            }
            return Selffrm;
        }

        //8.18

/*        internal class PCSInfo
        { 
            public string awType { get; internal set;}
            public string aPCSType { get; internal set; }
            public double aPCSValueRate { get; internal set; }
            public bool bAllParam { get; internal set; }
        }


        //8.18
        internal class ThreadPoolClass
        {
            public void SetAllPCSCommand(object obj)
            {

                PCSInfo pcsInfo = (PCSInfo)obj;

                if ((!frmSet.IsMaster)||(frmSet.PCSGridModel==1))
                    return;
                foreach (EMSEquipment oneEMSE in frmMain.Selffrm.AllEquipment.EMSList)
                {
                    //oneEMSE.ExcPCSCommand(awType, aPCSType, aPCSValueRate, bAllParam);

                    if (frmSet.SysCount > 1)
                    {
                        oneEMSE.ExcPCSCommand(pcsInfo.awType, pcsInfo.aPCSType, pcsInfo.aPCSValueRate, pcsInfo.bAllParam);
                    }
                    //oneEMSE.ExcPCSCommand(awType, aPCSType, aPCSValueRate, bAllParam);
                }
            }
            *//*        internal void SetAllPCSCommand(object state)
                    {
                        throw new NotImplementedException();
                    }*//*

        }*/


        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            SysThreathStoped = true;
            //关闭云链接
            TCPCloud.CloseConnect();
            //关闭gpio
            frmSet.GPIOClose();
           // System.Environment.Exit(0);
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


            //下载故障信息
            //WarmingList.LoadFromMySQL();


            //打开设备数据库的自己保存进程

            //连接云soket
            //OnReceiveTCPUDPCMD = OnReceiveCMD;
            //TCPCloud.ReconnectTime = frmSet.YunInterval;
            //TCPCloud.TCPClientIni(frmSet.CloundIP, frmSet.CloundPort);//www.t3cloud.cn 10000 
            ////TCP232.OnConectedEvent += OnCennectedEvent;
            //TCPCloud.OnDisconectEvent += OnDisCennectedEvent;
            //TCPCloud.OnReceiveDataEvent += OnReceiveData;
            //TCPCloud.OnReconnectFailed += OnReconnectFaildEvent;
            //TCPCloud.StartMonitor();
            //TCPCloud.AutoConnect();    //云连接恢复 


            //同步数据


            //--------运行策略timer或线程 
            //TacticsList.AutoCheckTactics();
            //--------运行均衡策略线程
            //BalaTacticsList.AutoCheckBalaTactics();

          

            /*            if (frmSet.SysAutoRun) //判断是否可以自动运行策略
                        {
                            if (frmSet.SysMode == 0 || frmSet.SysMode == 2)//0手工模式,1预设策略,2网络控制（104）
                            {
                                TacticsList.TacticsOn = false;
                                frmSet.PCSMRun();
                            }
                            else
                                TacticsList.TacticsOn = true;
                        }*/
/*            if (frmSet.SysMode == 1)
            {
                //log.Error("开启策略");
                TacticsList.TacticsOn = true;
            }*/

            //打开监控进程（主要是用户侧电表和温度消防）-----严防安全、逆流和超限额


            frmFlash.AddPostion(10);
            //-------打开监视操作进程或者time，在无人操作时候进入休眠并关闭屏幕和注销用户 
            tmSystime.Interval = 3000;
            tmSystime.Enabled = true;
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
            GC.Collect();
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

        //定时器 1min    12.5
/*        private void tmYuntime_Tick(object sender, EventArgs e)
        {
            //ping mqttfx 检查是否网络正常
            Ping ping = new Ping();
            PingReply reply = ping.Send("www.baidu.com");

            if (reply.Status != IPStatus.Success)
            {
                //输入重启指令
                *//*                if (!Model4G.m485.sp.IsOpen)
                                {
                                    try
                                    {
                                        Model4G.m485.sp.Open();
                                    }
                                    catch (Exception ex)
                                    {
                                        frmMain.ShowDebugMSG(ex.ToString());
                                    }
                                }
                                Model4G.m485.Restart4G();*//*
                if (frmMain.Selffrm.AllEquipment.HostStart == false)
                {
                    SysIO.Reboot();
                }

            }


        }*/

        //定时器：1s
        private void tmSystime_Tick(object sender, EventArgs e)
        {
/*          
            if ((AllEquipment.Report2Cloud.mqttClient == null)||(!AllEquipment.Report2Cloud.mqttClient.IsConnected))
            {
                //AllEquipment.Report2Cloud.CreateClient();
                SysIO.Reboot();
            }*/
            //19s的循环
/*            if (sCount == 0)
            {
                sCount = 19;
                //检查云下发命令通道是否连同
                AllEquipment.Report2Cloud.CheckConnect();


                if (AllEquipment.Elemeter2 == null)
                    return;

                DateTime tempTime = DateTime.Now;

                //12.4
                if (frmSet.EMSstatus == 1)
                {
                    //采集数据保存在数据库中
                    AllEquipment.Save2DataSoure(tempTime);
                    //采集数据上传云端
                    AllEquipment.Report2Cloud.Save2CloudFile(tempTime);
                }

                //更新图表曲线
                //TacticsList.AddOneStep(ctMain, tempTime, -1 * AllEquipment.Elemeter2.AllUkva, AllEquipment.Elemeter2.Gridkva, AllEquipment.Elemeter2.Subkw);
                //ctMain.Series[1].ChartType = SeriesChartType.Line;

                //2.21
                if (!Selffrm.AllEquipment.ReadDoPUini())
                {
                    //更新的月份
                    lock (Selffrm.AllEquipment)
                    { 
                        Selffrm.AllEquipment.Client_PUMdemand_Max = 0;
                        Selffrm.AllEquipment.WriteDoPUini();
                    }

                }

                //如果日期更新：
                //1.清理数据库的旧数据
                //2.保存当天收益到数据库
                //3.上传当天收益到云
                //4.下载策略
                if (Selffrm.AllEquipment.rDate != DateTime.Now.ToString("yyyy-MM-dd"))
                {
                    GC.Collect();// 通知托管堆强制回收垃圾   
                    //删除180天前的数据
                    frmSet.DeleOldData(DateTime.Now.AddDays(-180).ToString("yyyy-MM-dd"));
                    //保存当天收益到数据库
                    frmMain.Selffrm.AllEquipment.SaveDataInoneDay(Selffrm.AllEquipment.rDate);
                    //当日收益发送到云
                    Selffrm.AllEquipment.Report2Cloud.SaveProfit2Cloud(Selffrm.AllEquipment.rDate);//qiao
                    //更新日期
                    Selffrm.AllEquipment.rDate = DateTime.Now.ToString("yyyy-MM-dd");
                    //将当天的储能表和辅表的总尖峰平谷的累计电能数据保存到INI，包含日期和具体电能值
                    frmMain.Selffrm.AllEquipment.WriteDataInoneDayINI(Selffrm.AllEquipment.rDate);
                    //每晚00：00更新策略
                    if (TacticsList != null)
                    {
                        try
                        {
                            if (frmSet.IsMaster)
                            {
                                if (TacticsList != null)
                                {
                                    try
                                    {
                                        TacticsList.LoadFromMySQL();
                                    }
                                    catch
                                    {
                                        log.Error("定时器刷新数据库失败");
                                    }
                                }
                            }
                        }
                        catch
                        {
                            log.Error("00：00更新策略失败");
                        }
                    }
                    //更新均衡策略
                    try {
                        BalaTacticsList.LoadFromMySQL(); 
                    }
                    catch { log.Error("00：00更新均衡策略失败"); }
                    //在Chart显示计划
*//*                    if (TacticsList != null)
                    {
                        try
                        {
                            if (frmSet.IsMaster)
                            {
                                TacticsList.ShowTactic2Char(ctMain, true);
                            }
                        }
                        catch 
                        {
                            log.Error("Chart显示计划出错");
                        }
                    }*//*


                    //更新系统时间、表1--4、PCS、BMS
                    //校对时间 qiao
                    //if (NetTime.GetandSetTime())
                    //{
                    //    DateTime dtTemp=DateTime.Now;
                    //    byte[] aTime = { (byte)dtTemp.Second, (byte)dtTemp.Minute, (byte)dtTemp.Hour, (byte)dtTemp.Day, 
                    //                      (byte)dtTemp.Month, (byte)(dtTemp.Year-2000) };
                    //    if (AllEquipment.Elemeter1 != null)
                    //        AllEquipment.Elemeter1.SetTime(aTime);
                    //    if (AllEquipment.Elemeter2 != null)
                    //        AllEquipment.Elemeter2.SetTime(aTime);
                    //    byte[] aTime2 = { (byte)(dtTemp.Year-2000),(byte)dtTemp.Month, (byte)dtTemp.Day,
                    //                    (byte)dtTemp.Hour,(byte)dtTemp.Minute, (byte)dtTemp.Second  };
                    //    if (AllEquipment.Elemeter3 != null)
                    //        AllEquipment.Elemeter3.SetTime(aTime2);
                    //} 

                    //检查mqttp的连接情况，每分钟检查一次
                    try
                    {
                        AllEquipment.Report2Cloud.CheckConnect();       
                    }
                    catch { }

                    //1.29 重置重启次数
                    frmSet.RestartCounts = 5;
                    frmSet.SaveSet2File();

                }
                //7.25
                try
                {
                    if ((AllEquipment.Report2Cloud.mqttClient == null)||(!AllEquipment.Report2Cloud.mqttClient.IsConnected))
                    {
                        //log.Error("网络中断");
                        //log.Error("HostStart:" + frmMain.Selffrm.AllEquipment.HostStart + " " + "frmSet.EMSstatus:" + frmSet.EMSstatus+ " " + "SlaveStart:" + frmMain.Selffrm.AllEquipment.SlaveStart);
                        //在非策略时段 + 运行模式 
                        if (frmMain.Selffrm.AllEquipment.HostStart == false && frmSet.EMSstatus == 1 &&  frmMain.Selffrm.AllEquipment.SlaveStart == false)
                        {
                            //log.Error("RestartCounts:" + frmSet.RestartCounts);
                            if (frmSet.RestartCounts > 0)
                            {
                                //关闭PCS
                                log.Error("网络原因造成EMS重启");
                                frmMain.Selffrm.AllEquipment.PCSList[0].ExcSetPCSPower(false);
                                //重启次数减1 ， 每日限定重启5次.
                                frmSet.RestartCounts--;
                                frmSet.SaveSet2File();
                                Thread.Sleep(5000);
                                //重启
                                SysIO.Reboot();
                            }
                        }
                    }

                    
                }
                catch { }
                //空调控制
                //空调没打开状态， 电池温度处于低温或高温状态
                //AllEquipment.TempControl.PowerOn = ();

                if ((AllEquipment.TempControl!=null) && (AllEquipment.TempControl.state != 1))//(!AllEquipment.TempControl.PowerOn)
                {
                    //if((AllEquipment.BMS.cellMaxTemp>32)||(AllEquipment.BMS.cellMinTemp<5))
                    if ((AllEquipment.BMS.cellMaxTemp > 32) && (AllEquipment.TempControl.state != 1))
                        AllEquipment.TempControl.TCPowerOn(true);
                }
                else if(AllEquipment.TempControl != null)
                {
                    //pcs必须处于低功率状态，且电池常温10---30度就停止空调
                    if ((AllEquipment.PCSList[0].allUkva < 3) && (AllEquipment.BMS.cellMaxTemp < 30) && (AllEquipment.BMS.cellMinTemp > 10))
                        AllEquipment.TempControl.TCPowerOn(false);
                }

                //液冷控制
                if ((AllEquipment.LiquidCool !=null) && (AllEquipment.LiquidCool.state != 1))
                {
                    if (AllEquipment.BMS.cellMaxTemp > 25)
                        AllEquipment.LiquidCool.LCPowerOn(true);
                }
                else if ( (AllEquipment.LiquidCool != null) && (AllEquipment.LiquidCool.state != 1))
                {
                    //pcs必须处于低功率状态，且电池常温10---30度就停止液冷
                    if ((AllEquipment.PCSList[0].allUkva < 3) && (AllEquipment.BMS.cellMaxTemp < 25) && (AllEquipment.BMS.cellMinTemp > 10))
                        AllEquipment.LiquidCool.LCPowerOn(false);
                }


            }
            sCount--;*/

            //和页面按钮有关
            if (!BeFoused)
                return;


            //20次更新一次小面的曲线
/*            if (ErrorGridFreshCount == 0)
            { 
                {
                    DBConnection.ShowData2DBGrid(dbvError, "select * from warning  where (ResetTime IS NULL)");
                    ErrorGridFreshCount = 20;
                }
            }
            ErrorGridFreshCount--;*/


            //单个数据            
            if (AllEquipment.PCSList.Count > 0)
            {
                string strCap = "手动";
                if (TacticsList.TacticsOn)
                {
                    strCap = "策略";
                }
                else if (frmSet.PCSGridModel==1)
                {
                    strCap = "离网";
                }
                else if (frmSet.SysMode == 2)
                {
                    strCap = "网控";
                }
                if (AllEquipment.PCSList[0].allUkva > 0.5)
                {
                    labState.Text = strCap + "放电";
                    labPCSuKW.Text = AllEquipment.PCSList[0].allUkva.ToString("F1") + "kw";
                } 
                else if (AllEquipment.PCSList[0].allUkva < -0.5)
                {
                    labState.Text = strCap + "充电";
                    labPCSuKW.Text = AllEquipment.PCSList[0].allUkva.ToString("F1") + "kw";
                }
                else
                {
                    labState.Text = strCap + "待机";
                    labPCSuKW.Text = "0.0kw";
                }
                //当前PCS的功率 
                //labState.Text = strCap+PCSClass.PCSStates[AllEquipment.PCSList[0].State];
                labPCSuKW.Text = AllEquipment.PCSList[0].allUkva.ToString("F2") + "kw"; 
            } 
            // labPCSuKW.Text = AllEquipment.PCSKVA.ToString();
            //温度
            if (AllEquipment.TempControl!=null)
                labACState.Text = AllEquipment.TempControl.indoorTemp.ToString() + "℃";
            //SOC
            labSOC.Text = AllEquipment.BMSSOC.ToString() + "%";
            vpbSOC.Value = (int)AllEquipment.BMSSOC;

            if (AllEquipment.Elemeter2 == null)
                return;
            labGridkva.Text = AllEquipment.GridKVA.ToString("F3");
            labPCSOKWH.Text = AllEquipment.Elemeter2.PUkwh[0].ToString("F3");//AllEquipment.PCSInKWH.ToString();累计充电
            labPCSPKWH.Text = AllEquipment.Elemeter2.OUkwh[0].ToString("F3");//AllEquipment.PCSOutKWH.ToString();累计放电
            labE2PKWH.Text = AllEquipment.E2OKWH[0].ToString("F3");
            labE2OKWH.Text = AllEquipment.E2PKWH[0].ToString("F3");
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