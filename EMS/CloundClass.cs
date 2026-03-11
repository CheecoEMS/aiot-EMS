/*using M2Mqtt;
using M2Mqtt.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Modbus;
using System.Threading;
using log4net;
using System.Runtime.InteropServices;
using M2Mqtt.Exceptions;
using System.Threading.Tasks;
using System.Web.UI;
using static System.Windows.Forms.AxHost;
using Google.Protobuf.WellKnownTypes;
using Mono.Cecil.Cil;
using System.Windows.Forms.DataVisualization.Charting;

namespace EMS
{

    public class CloudClass
    {
        //SetThreadAffinityMask: Set hThread run on logical processer(LP:) dwThreadAffinityMask
        [DllImport("kernel32.dll")]
        static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

        //Get the handler of current thread
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentThread();

        public int connectflag = 0;
        public string EMQX_CLIENT_ID ="";
        public string strUpPath = "";      //云上传数据目录
        public string strDownPath = "";    //云下传数据目录
        public AllEquipmentClass Parent = null;
        private static string EMQX_BROKER_IP = "mqtt.eaiot.cloud";
        private static int EMQX_BROKER_PORT = 8883 ;//1883
        public string PriceTopic;
        public string TacticTopic;
        public string EMSLimitTopic;
        public string AIOTTableTopic;
        public string BalaTableTopic;
        public string BalaTacticTopic;
        //public string HeartbeatTopic;
        //public string UploadTopic;
        public string OtaTopic;
        //public string checkPemTopic;

        //新版本topic
        public string PriceTopic_new;
        public string TacticTopic_new;
        public string EMSLimitTopic_new;
        public string AIOTTableTopic_new;
        public string BalaTableTopic_new;

        public MqttClient mqttClient { get; set; }
        public bool FirstRun = true;
        
        //public volatile bool receivedHeartbeatResponse = true;  //每次发送心跳，置为false，接收到心跳置为true
        //private int _missedHeartbeatCount = 0;
        //private const int MAX_MISSED_HEARTBEATS = 10;

        public volatile bool ConnectToCloud = false;  //只有当接收到心跳返回，才置为true

       // public string HeartbeatID;  //校验发送和接收得心跳uuid
        
        
        private static System.Threading.Timer DownloadData_timer;   //数据本地存储定时器
        private static System.Threading.Timer UploadData_Timer;     //数据本地上云定时器
        private static System.Threading.Timer Heartbeat_Timer;      //心跳连接定时器

        //数据上云
        private string DataPath = "c:\\SendData"; //数据保存地址
        private string Filters = "*.json"; //数据格式
        string[] allFiles;
        private static int batchSize = 10;   //限制每次据本地上云周期内上传数据量大小

        private static ILog log = LogManager.GetLogger("CloudClass");

        private static readonly object _lockMqtt = new object();
        private static readonly object _lockTXT = new object();
        
        //定时器标志位
        private static bool isUploadDataStopped = false;//判断Publish_Timer是否已被暂停
        
        private static bool isHeartbeatExecuting = false; //判断Heartbeat_Timer是否正在执行
        //private static bool isDownloadDataExecuting = false; //判断Heartbeat_Timer是否正在执行
        //private static bool isUploadDataExecuting = false; //判断Heartbeat_Timer是否正在执行

        //线程
        private Thread UploadDataThread;
        private CancellationTokenSource uploadDataCancellationTokenSource;
        private bool isUploadDataExecuting = false;
        private bool isUploadDataThreadRunning = false; // 用于标记线程是否已启动

        private Thread DownloadDataThread;
        private static bool isDownloadDataExecuting = false; //判断Heartbeat_Timer是否正在执行
        private CancellationTokenSource downloadDataCancellationTokenSource;
        
        private Thread HeartbeatThread;

        private Thread WaitUploadDataThread;
        private bool isWaitUploadDataExecuting = false;

        public CloudClass()
        {
            string strSysPath = Convert.ToString(System.AppDomain.CurrentDomain.BaseDirectory);
            DataPath = strSysPath + "UpData";
            if (!Directory.Exists(DataPath))
            {
                Directory.CreateDirectory(DataPath);
            }
            //mqttConnect(); 
        }             

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
        static public byte[] Back3Data(int aAddr ) 
        { 
            switch (aAddr)
            {
                case 0x6001://计划功率
                    return ModbusBase.BuildMSG3Back((byte)frmSet.config.i485Addr, 3, (ushort)(Math.Abs(frmMain.Selffrm.AllEquipment.PCSScheduleKVA)));
                case 0x6002://实际功率
                    double value = Math.Abs(frmMain.Selffrm.AllEquipment.PCSKVA);
                    return ModbusBase.BuildMSG3Back((byte)frmSet.config.i485Addr, 3,  (ushort)value);
                case 0x6003://充放电 
                    if (frmMain.Selffrm.AllEquipment.PCSKVA < -0.5)//充电            
                        return ModbusBase.BuildMSG3Back((byte)frmSet.config.i485Addr, 3, 0);
                    else if (frmMain.Selffrm.AllEquipment.PCSKVA > 0.5)//放电
                        return ModbusBase.BuildMSG3Back((byte)frmSet.config.i485Addr, 3, 1);
                    else//待机
                        return ModbusBase.BuildMSG3Back((byte)frmSet.config.i485Addr, 3, 2);
                case 0x6004: //PCSType 恒压横流恒功率、AC恒压
                    return ModbusBase.BuildMSG3Back((byte)frmSet.config.i485Addr, 3,(ushort)Array.IndexOf(PCSClass.PCSTypes, frmMain.Selffrm.AllEquipment.PCSTypeActive));
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

    }




}
*/