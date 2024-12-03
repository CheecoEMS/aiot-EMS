using M2Mqtt;
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
        public string HeartbeatTopic;
        public string UploadTopic;
        public string OtaTopic;

        public MqttClient mqttClient { get; set; }
        public bool FirstRun = true;
        
        public volatile bool receivedHeartbeatResponse = true;  //每次发送心跳，置为false，接收到心跳置为true
        public volatile bool ConnectToCloud = false;  //只有当接收到心跳返回，才置为true

        public string HeartbeatID;  //校验发送和接收得心跳uuid
        
        
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

        /********************************************************************************************
         * 
         *                              线程
         * 
         * *****************************************************************************************/


        
        public void InitCloudClass_Threads()
        {
            StartUploadDataThread();
            StartHeartbeatThread();
            StartDownloadDataThread();
        }

        /********************************DownloadDataThread*************************************/

        public void StartDownloadDataThread()
        {
            // 创建取消令牌源
            downloadDataCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = downloadDataCancellationTokenSource.Token;

            // 创建并启动 DownloadData 线程
            DownloadDataThread = new Thread(() => DownloadDataThreadCallback(cancellationToken))
            {
                IsBackground = true,
                Priority = ThreadPriority.Normal,
                Name = "DownloadDataThread"
            };
            DownloadDataThread.Start();
        }

        private void DownloadDataThreadCallback(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    DateTime tempTime = DateTime.Now;
                    // 采集数据保存在数据库中
                    Save2DataSoure(tempTime);
/*                    // 采集数据上传云端
                    Save2CloudFile(tempTime);*/
                }
                catch (Exception ex)
                {
                    log.Error("DownloadDataThread encountered an error: " + ex.Message);
                }

                // 等待 60 秒再进行下一次数据上传
                Thread.Sleep(60000);
            }
        }

        public void StopDownloadDataThread()
        {
            if (downloadDataCancellationTokenSource != null)
            {
                downloadDataCancellationTokenSource.Cancel(); // 发出取消信号
                downloadDataCancellationTokenSource.Dispose();
                downloadDataCancellationTokenSource = null;
            }

            if (DownloadDataThread != null && DownloadDataThread.IsAlive)
            {
                DownloadDataThread.Join(); // 等待线程安全结束
            }

            log.Info("DownloadDataThread has been successfully stopped.");
        }

        /********************************UploadDataThread*************************************/


        public void TryStartUploadDataThread()
        {
            WaitUploadDataThread = new Thread(TryStartUploadDataThreadCallback);
            WaitUploadDataThread.IsBackground = true;
            WaitUploadDataThread.Priority = ThreadPriority.Normal;
            WaitUploadDataThread.Name = "WaitUploadDataThread";
            WaitUploadDataThread.Start();
        }

        private void TryStartUploadDataThreadCallback()
        {
            isWaitUploadDataExecuting = true;
            while (!ConnectToCloud || isUploadDataThreadRunning)
            {
                // 如果未连接到云端或线程已运行，等待一段时间再检查
                Thread.Sleep(5000); // 每隔 5 秒检查一次条件
            }

            // 一旦条件满足，启动上传数据线程
            bool res = true;
            int retryCount = 0;
            const int maxRetries = 5; // 最大重试次数

            while (res && ConnectToCloud && retryCount < maxRetries)
            {     
                res = StartUploadDataThread();
                if (res)
                {
                    break;
                }
                retryCount++;
                log.Error($"UploadDataThread 启动失败，正在进行第 {retryCount} 次重试...");
                Thread.Sleep(5000);
            }
            isWaitUploadDataExecuting = false;

            // 回调方法执行完毕，线程会自动销毁
            log.Info("WaitUploadDataThread has finished execution and will be automatically terminated.");
        }

        private bool StartUploadDataThread()
        {
            // 创建取消令牌源
            uploadDataCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = uploadDataCancellationTokenSource.Token;

            UploadDataThread = new Thread(() => UploadDataThreadCallback(cancellationToken))
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = "UploadDataThread"
            };

            try
            {
                UploadDataThread.Start();

                // 检查线程是否成功启动
                if (UploadDataThread.IsAlive)
                {
                    isUploadDataThreadRunning = true;
                    log.Info("UploadDataThread 启动成功。");
                }
                else
                {
                    isUploadDataThreadRunning = false;
                    log.Error("UploadDataThread 启动失败。");
                }
            }
            catch (Exception ex)
            {
                log.Error($"启动 UploadDataThread 时发生异常: {ex.Message}");
                isUploadDataThreadRunning = false;
            }

            return isUploadDataThreadRunning;
        }

        private void UploadDataThreadCallback(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (ConnectToCloud)
                    {
                        frmMain.Selffrm.AllEquipment.Report2Cloud.SendmqttData();
                    }
                }
                catch (Exception ex)
                {
                    log.Error("UploadDataThread encountered an error: " + ex.Message);
                }
                Thread.Sleep(30000);
            }

            log.Error("UploadDataThread has been stopped.");
        }

        public void StopUploadDataThread()
        {
            try
            {
                if (uploadDataCancellationTokenSource != null)
                {
                    uploadDataCancellationTokenSource.Cancel(); // 发出取消信号
                    uploadDataCancellationTokenSource.Dispose();
                    uploadDataCancellationTokenSource = null;
                }

                if (UploadDataThread != null && UploadDataThread.IsAlive)
                {
                    UploadDataThread.Join(); // 等待线程安全结束
                }

                isUploadDataThreadRunning = false; // 重置线程运行标志
                log.Info("UploadDataThread has been successfully stopped.");
            }
            catch (Exception ex)
            {
                log.Error("StopUploadDataThread: " + ex.Message);
            }
        }

        /********************************tHeartbeatThread*************************************/
        private void StartHeartbeatThread()
        {
            try
            {
                // 创建并启动 Heartbeat 线程
                HeartbeatThread = new Thread(HeartbeatThreadCallback);
                HeartbeatThread.IsBackground = true;
                HeartbeatThread.Priority = ThreadPriority.Normal;
                HeartbeatThread.Name = "HeartbeatThread";
                HeartbeatThread.Start();
            }
            catch (Exception ex)
            {
                log.Error("Error starting HeartbeatThread: " + ex.Message);
            }
        }

        private void HeartbeatThreadCallback()
        {
            while (true)
            {
                try
                {
                    log.Info("触发心跳定时器");
                    if (frmMain.Selffrm.AllEquipment.Report2Cloud.mqttClient != null)
                    {
                        frmMain.Selffrm.AllEquipment.Report2Cloud.SendHeartbeat();
                    }
                    else
                    {
                        log.Error("mqttClient为空，触发重连");
                        frmMain.Selffrm.AllEquipment.Report2Cloud.mqttReconnect();
                    }
                }
                catch (Exception ex)
                {
                    log.Error("HeartbeatThreadCallback encountered an error: " + ex.Message);
                }

                // 等待 3 分钟再进行下一次心跳
                Thread.Sleep(180000);
            }
        }

        /********************************************************************************************
         * 
         *                              定时器
         * 
         * *****************************************************************************************/

        public void InitCloudClass_Timer()
        {
            InitializeDownloadData_timer();
        }


        private void InitializeDownloadData_timer()
        {
            //每60秒 数据上云  
            DownloadData_timer = new System.Threading.Timer(DownloadDataCallback, null, 0, 60000);
        }
        private void DownloadDataCallback(Object state)
        {
            if (isDownloadDataExecuting)
            {
                log.Info("DownloadDataCallback is still executing. Skipping this tick to avoid overlap.");
                return;
            }

            isDownloadDataExecuting = true;

            try
            {
                DateTime tempTime = DateTime.Now;
/*                //采集数据保存在数据库中
                Save2DataSoure(tempTime);*/
                //采集数据上传云端
                Save2CloudFile(tempTime);
            }
            catch (Exception ex)
            {
                log.Error("DownloadDataCallback encountered an error: " + ex.Message);
            }
            finally
            {
                isDownloadDataExecuting = false; // 执行完毕，重置标志位
            }
        }


        /// <summary>
        /// 查询目录里的文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public string[] GetAllFileNames(string path, string pattern = "*.*")
        {
            return Directory.GetFiles(path, pattern, SearchOption.TopDirectoryOnly);
        }


        public void SendmqttData()
        {
            log.Info("数据上云获取锁_lockTXT ");
            lock (_lockTXT)
            {
                allFiles = GetAllFileNames(DataPath, Filters);

                // 发送数据  
                if (allFiles.Length > 0)
                {
                    for (int i = 0; i < allFiles.Length; i += batchSize)
                    {
                        string[] batch = allFiles.Skip(i).Take(batchSize).ToArray();

                        // 逐个发送文件  
                        foreach (string file in batch) // 修改这里的变量名为file  
                        {
                            try
                            {
                                string fileName = Path.GetFileName(file);
                                string aFileCap = fileName.Substring(1, 3); // 确保这里的索引和长度是有效的  
                                string strData = File.ReadAllText(file);
                                Write2Topic(aFileCap, strData);
                                File.Delete(file);
                            }
                            catch (Exception ex)
                            {
                                log.Error($"处理文件 {file} 时出错: {ex.Message}");
                            }
                        }
                    }
                }

                allFiles = Array.Empty<string>();
            }
        }


        public void IniClound()
        {
            try
            {
                PriceTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/meter/price/";
                TacticTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/ems/strategy/";//request
                EMSLimitTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/ems/limit/";
                //AIOTTableTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/ctl/table/";
                string strID = frmSet.config.SysID;
                if (strID.Length >= 7)
                    strID = strID.Substring(strID.Length - 7, 7);
                AIOTTableTopic = "/rpc/ctl" + strID + "/aiot/table/";
                BalaTableTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/aiot/table/";
                BalaTacticTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/ems/BalaStrategy/";
                HeartbeatTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/Heartbeat";
                UploadTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/aiot/uploadData/";
                OtaTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/aiot/ota/";
            }
            catch (Exception ex)
            {
                log.Error("IniClound: " + ex.Message);
            }
        }

        public bool CreateClient()
        {
            bool res = false;
            INIFile ConfigINI = new INIFile();
            string strSysPath = Convert.ToString(System.AppDomain.CurrentDomain.BaseDirectory);

            String iotcode = frmSet.config.SysID;
            EMQX_CLIENT_ID = iotcode;

            log.Info("创建Client获取锁_lockMqtt");
            lock (_lockMqtt)
            {
                try
                {


                    if (mqttClient != null)
                    {
                        if (mqttClient.IsConnected)
                        {
                            mqttClient.Disconnect();
                        }
                        mqttClient = null;
                    }
                    mqttClient = new MqttClient(EMQX_BROKER_IP, EMQX_BROKER_PORT, true, null, null, MqttSslProtocols.TLSv1_2);
                    mqttClient.Connect(EMQX_CLIENT_ID,
                                                "aiot",// user,
                                                "Lab123123123",//pwd,
                                                true, // cleanSession
                                                60); // keepAlivePeriod 
                    //2.21 暂时注释
                    mqttClient.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;

                    res =  true;
                }
                catch (Exception ex)
                {
                    log.Error("CreateClient fail: " + ex.Message);
                    res = false;
                }
            }
            return res;
        }

        public void ListernAllTopic()
        {
            ListenTopic(PriceTopic + "request");
            ListenTopic(TacticTopic + "request");
            ListenTopic(EMSLimitTopic + "request");
            ListenTopic(AIOTTableTopic + "request");
            ListenTopic(BalaTableTopic + "request");
            ListenTopic(HeartbeatTopic);
            ListenTopic(UploadTopic + "request");
            ListenTopic(OtaTopic + "request");

            log.Error("Topic: " + OtaTopic + "request");
        }

        // 建立MQTT连接
        public void mqttConnect()
        {
            try
            {
                if (CreateClient())
                {
                    ListernAllTopic();
                    FirstRun = true;
                    receivedHeartbeatResponse = true;
                }
            }
            catch (Exception ex)
            {
                log.Error("mqttConnect: " + ex.Message);
            }
        }


/*        public void mqttReconnect()
        {
            try
            {
                ConnectToCloud = false;

                if (!isUploadDataStopped)
                {
                    // 先停止定时器，以确保在重连期间不会重复触发
                    UploadData_Timer?.Change(Timeout.Infinite, Timeout.Infinite);
                    isUploadDataStopped = true;
                }

                if (CreateClient())
                {
                    ListernAllTopic();
                    // 重连成功后重新启动定时器
                    UploadData_Timer?.Change(0, 30000);  // 设置定时器间隔为 30 秒
                    isUploadDataStopped = false;  // 更新状态
                    log.Info("重连成功，定时器UploadData_Timer已重新启动。");
                    receivedHeartbeatResponse = true;
                }
                
            }
            catch (Exception ex)
            {
                log.Error("mqttReconnect: " + ex.Message);
            }
        }*/

        public void mqttReconnect()
        {
            try
            {
                ConnectToCloud = false;

                // 先停止上传数据的线程，确保在重连期间不会重复触发
                StopUploadDataThread();

                if (CreateClient())
                {
                    ListernAllTopic();
                    receivedHeartbeatResponse = true;

                    // 重连成功后重新创建并启动上传数据的线程

                    //StartUploadDataThread();
                    if (!isWaitUploadDataExecuting)
                    { 
                        TryStartUploadDataThread();
                    }            
                }
            }
            catch (Exception ex)
            {
                log.Error("mqttReconnect: " + ex.Message);
            }
        }




        public void SendHeartbeat()
        {
            if (receivedHeartbeatResponse)
            {
                receivedHeartbeatResponse = false;
                HeartbeatID = Guid.NewGuid().ToString();
                string heartbeatMessage = $"{{\"HeartBeatID\":\"{HeartbeatID}\"}}";

                log.Info("发送心跳等待锁_lockMqtt");
                lock (_lockMqtt)
                {
                    if (mqttClient != null)
                    {
                        try
                        {
                            log.Info("发送心跳uuid: " + HeartbeatID);
                            mqttClient.Publish(HeartbeatTopic, System.Text.Encoding.UTF8.GetBytes(heartbeatMessage),
                                MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);

                            log.Info("发送心跳结束");
                        }
                        catch (Exception ex)
                        { 
                            log.Error("SendHeartbeat: " + ex.Message);
                        }
                    }
                }
            }
            else
            {
                log.Error("未监测到心跳返回，触发重连");
                mqttReconnect();
            }      
        }
        /// <summary>
        /// 给一个topic写数据
        /// </summary>
        /// <param name="currentTopic"></param>
        /// <param name="content"></param>
        public void Write2Topic(string currentTopic, string content)
        {
            lock (_lockMqtt)
            {
                if (mqttClient != null && !string.IsNullOrEmpty(currentTopic) && !string.IsNullOrEmpty(content))
                {
                    log.Info("数据上云获取锁_lockMqtt ");
                    try
                    {
                        mqttClient.Publish(currentTopic, System.Text.Encoding.UTF8.GetBytes(content), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);//qos
                    }
                    catch (Exception ex)
                    {
                        log.Error("Write2Topic: " + ex.Message);
                    }

                }
            }
        }


        /// <summary>
        /// 设置监听一个topic
        /// </summary>
        /// <param name="aTopic"></param>
        public void ListenTopic(string aTopic)
        {
            log.Info("监听Topic获取锁_lockMqtt");
            lock (_lockMqtt)
            {
                try
                {
                    if (mqttClient != null && !string.IsNullOrEmpty(aTopic))
                    {
                        mqttClient.Subscribe(new string[] { aTopic },
                        new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });//QOS_LEVEL_EXACTLY_ONCE
                    }
                }
                catch (Exception ex)
                {
                    log.Error("ListenTopic: " + ex.Message);
                }
            }
        }


        //这段定义了收到消息之后做什么事情
        private void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            try
            {
                bool Result = false;
                string strResponse = "{ \"jsonrpc\":\"2.0\", \"result\":true, \"id\":\"";
                string ErrorstrResponse = "{ \"jsonrpc\":\"2.0\", \"result\":false, \"id\":\"";
                string topic = e.Topic.ToString();
                string message = System.Text.Encoding.Default.GetString(e.Message);

                JObject jsonObject = JObject.Parse(message);
                string strID = "";
                if (jsonObject["id"] != null)
                {
                    strID = jsonObject["id"].ToString();
                }
                //同时订阅两个或者以上主题时，分类收集收到的信息

                if (topic == TacticTopic + "request")
                {
                    Result = GetServerTactics(message);
                    log.Info("接收TacticTopic，获取锁_lockMqtt");
                    lock (_lockMqtt)
                    {
                        if (Result)
                        {
                            try
                            {
                                mqttClient.Publish(TacticTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(strResponse + strID + "\"}"),
                                    MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
                            }
                            catch (Exception ex)
                            {
                                log.Error("Client_MqttMsgPublishReceived:" + ex.Message);
                            }
                        }
                        else
                        {
                            try
                            {
                                mqttClient.Publish(TacticTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(ErrorstrResponse + strID + "\"}"),
                                    MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
                            }
                            catch (Exception ex)
                            {
                                log.Error("Client_MqttMsgPublishReceived:" + ex.Message);
                            }
                        }
                    }
                }
                else if (topic == PriceTopic + "request")
                {
                    Result = GetServerEPrices(message);
                    log.Info("接收PriceTopic，获取锁_lockMqtt");
                    lock (_lockMqtt)
                    {
                        if (Result)
                        {
                            try
                            {
                                mqttClient.Publish(PriceTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(strResponse + strID + "\"}"),
                                    MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
                            }
                            catch (Exception ex)
                            {
                                log.Error("Client_MqttMsgPublishReceived:" + ex.Message);
                            }
                        }
                        else
                        {
                            try
                            {
                                mqttClient.Publish(PriceTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(ErrorstrResponse + strID + "\"}"),
                                    MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
                            }
                            catch (Exception ex)
                            {
                                log.Error("Client_MqttMsgPublishReceived:" + ex.Message);
                            }
                        }
                    }
                }
                else if (topic == EMSLimitTopic + "request")
                {
                    Result = GetServerEMSLimit(message);
                    log.Info("接收EMSLimitTopic，获取锁_lockMqtt");
                    lock (_lockMqtt)
                    {
                        if (Result)
                        {
                            try
                            {
                                mqttClient.Publish(EMSLimitTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(strResponse + strID + "\"}"),
                                    MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
                            }
                            catch (Exception ex)
                            {
                                log.Error("Client_MqttMsgPublishReceived:" + ex.Message);
                            }
                        }
                        else
                        {
                            try
                            {
                                mqttClient.Publish(EMSLimitTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(ErrorstrResponse + strID + "\"}"),
                                    MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
                            }
                            catch (Exception ex)
                            {
                                log.Error("Client_MqttMsgPublishReceived:" + ex.Message);
                            }
                        }
                    }
                }
                else if (topic == AIOTTableTopic + "request")
                {
                    strID = GetAiotTable(message);
                    log.Info("接收AIOTTableTopic，获取锁_lockMqtt");
                    lock (_lockMqtt)
                    {
                        if (mqttClient != null)
                        {
                            try
                            {
                                mqttClient.Publish(AIOTTableTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(strResponse + strID + "\"}"), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
                            }
                            catch (Exception ex)
                            {
                                log.Error("Client_MqttMsgPublishReceived:" + ex.Message);
                            }
                        }
                    }
                }
                else if (topic == BalaTableTopic + "request")
                {
                    strID = GetBalaTable(message);
                    log.Info("接收BalaTableTopic，获取锁_lockMqtt");
                    lock (_lockMqtt)
                    {
                        if (mqttClient != null)
                        {
                            try
                            {
                                mqttClient.Publish(BalaTableTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(strResponse + strID + "\"}"), MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
                            }
                            catch (Exception ex)
                            {
                                log.Error("Client_MqttMsgPublishReceived:" + ex.Message);
                            }

                        }
                    }
                }
                else if (topic == HeartbeatTopic)
                {
                    GetHeartbeat(message);
                }
                else if (topic == UploadTopic  + "request")
                {
                    Result = DataRetransmission(message);
                    log.Info("接收UploadTopic，获取锁_lockMqtt");
                    lock (_lockMqtt)
                    {
                        if (Result)
                        {
                            try
                            {
                                mqttClient.Publish(UploadTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(strResponse + strID + "\"}"),
                                    MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
                            }
                            catch (Exception ex)
                            {
                                log.Error("Client_MqttMsgPublishReceived:" + ex.Message);
                            }
                        }
                        else
                        {
                            try
                            {
                                mqttClient.Publish(UploadTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(ErrorstrResponse + strID + "\"}"),
                                    MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
                            }
                            catch (Exception ex)
                            {
                                log.Error("Client_MqttMsgPublishReceived:" + ex.Message);
                            }
                        }
                    }
                }
                else if (topic == OtaTopic  + "request")
                {
                    log.Info("接收到升级指令");
                    ImplOta(message);                 
                }
                /*            else if (topic == BalaTacticTopic)
                            {
                                //log.Info("接收到均衡策略");
                                strID = GetServerBalaTactics(message);
                                mqttClient.Publish(BalaTableTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(strResponse + strID + "\"}"),
                                    MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
                            }*/
            }
            catch (Exception ex)
            {
                log.Error("Client_MqttMsgPublishReceived: " + ex.Message);
            }
        }







        /// <summary>
        /// 将DateTime类型转换为long类型
        /// </summary>
        /// <param name="adtTime">时间格式的时间</param>
        /// <returns></returns>
        public static long ConvertDataTime2Long(DateTime adtTime)
        {
            //dateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000
            //DateTime dtBase = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            //TimeSpan toNow = dt.ToUniversalTime().Subtract(dtBase);
            //long timeStamp = toNow.Ticks / 10000;
            long timeStamp = (long)((adtTime.ToUniversalTime().Ticks - 621355968000000000) / 10000);
            return timeStamp;
        }

        /// <summary>
        /// 将long类型转换为DateTime类型
        /// </summary>
        /// <param name="alTime">长整型时间戳</param>
        /// <returns></returns>
        public static DateTime ConvertLong2DataTime(long alTime)
        {
            DateTime dtBase = new DateTime(1970, 1, 1, 8, 0, 0).AddMilliseconds(alTime);
            return dtBase;
        }

        /// <summary>
        /// 将一个对象转换为Json格式字符串
        /// </summary>
        /// <param name="aObj"></param>
        /// <returns></returns>
        public static string GetProperties(object aObj)//GetProperties<T>(T t)
        {
            string tStr = string.Empty;
            if (aObj == null)
            {
                return tStr;
            }
            PropertyInfo[] properties = aObj.GetType().GetProperties();// (BindingFlags.Instance | BindingFlags.Public);

            if (properties.Length <= 0)
            {
                return tStr;
            }
            tStr += "{\n";
            foreach (PropertyInfo item in properties)
            {
                string name = item.Name;
                object value = item.GetValue(aObj, null);
                if (item.PropertyType == typeof(double[]))
                {
                    //浮点数组
                    double[] fTemp = (double[])value;
                    if (fTemp.Length <= 0)
                        continue;
                    tStr += string.Format("	\"{0}\":[", name);
                    for (int i = 0; i < fTemp.Length; i++)
                    {
                        if(fTemp[i]!=Math.Round(fTemp[i]))
                            tStr += "\"" + fTemp[i].ToString() + "\",";
                        else
                            tStr += "\"" + fTemp[i].ToString("0.000") + "\",";
                    } 
                    tStr = tStr.Substring(0, tStr.Length - 1);
                    tStr += "],\n";
                }
                else if (item.PropertyType == typeof(ushort[]))
                {
                    //Int16数组
                    ushort[] fTemp = (ushort[])value;
                    if (fTemp.Length <= 0)
                        continue;
                    tStr += string.Format("	\"{0}\":[", name);
                    for (int i = 0; i < fTemp.Length; i++)
                        tStr += fTemp[i].ToString() + ",";
                    tStr = tStr.Substring(0, tStr.Length - 1);
                    tStr += "],\n";
                }
                else if (item.PropertyType.IsValueType || item.PropertyType.Name.StartsWith("String"))
                {
                    if (item.PropertyType == typeof(bool))
                        tStr += string.Format("	\"{0}\": \"{1}\",\n", name, ((bool)value).ToString().ToLower());
                    else if (item.PropertyType == typeof(string))
                        tStr += string.Format("	\"{0}\": \"{1}\",\n", name, value);
                    else if (item.PropertyType == typeof(long))
                        tStr += string.Format("	\"{0}\": {1},\n", name, value);
                    else if (item.PropertyType == typeof(int))
                        tStr += string.Format("	\"{0}\": {1},\n", name, value);
                    else if (item.PropertyType == typeof(DateTime))
                        tStr += string.Format("	\"{0}\": {1},\n", name, ConvertDataTime2Long((DateTime)value));
                    else if (item.PropertyType == typeof(double))
                    {
                        tStr += string.Format("	\"{0}\": \"{1}\",\n", name, ((double)value).ToString("0.000"));
                    }
                    else if (item.PropertyType == typeof(float))
                    {
                        tStr += string.Format("	\"{0}\": \"{1}\",\n", name, ((float)value).ToString("0.000"));
                    }
                    else
                        tStr += string.Format("	\"{0}\": {1},\n", name, value);
                }
                else //object
                {
                    tStr += GetProperties(value) + ",";
                }
            }
            tStr = tStr.Substring(0, tStr.Length - 2);
            tStr += "\n}";
            return tStr;
        }

        /// <summary>
        /// 写入json文件
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="savePath"></param>
        /// 
        public static void ConvertToJson(string json, string aDirection, string aSavePath)
        {
            try
            {
                if (!Directory.Exists(aDirection))
                    Directory.CreateDirectory(aDirection);

                log.Info("1数据写入文本获取锁_lockTXT ");
                lock (_lockTXT)
                {
                    JObject jsonObject = JObject.Parse(json);
                    string strID = jsonObject["time"].ToString();

                    using (StreamWriter sw = new StreamWriter(aDirection + "\\" + aSavePath))
                    {
                        sw.WriteLine(json);
                    }
                }
            }
            catch (Exception e)
            {
                // 向用户显示出错消息
                Console.WriteLine("The file could not be read:" + e.Message);
            }
        }

        public static void ConvertToJson(object aObj, string aDirection, string aSavePath)
        {
            try
            {
                // 创建一个 StreamReader 的实例来读取文件 
                // using (StreamReader sr = new StreamReader("c:/jamaica.txt"))  while ((line = sr.ReadLine()) != null)
                if (!Directory.Exists(aDirection))
                    Directory.CreateDirectory(aDirection);

                log.Info("2数据写入文本获取锁_lockTXT ");
                lock (_lockTXT)
                {
                    using (StreamWriter sw = new StreamWriter(aDirection + "\\" + aSavePath))
                    {
                        sw.WriteLine(GetProperties(aObj));
                        //sw.Close();
                        //sw.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
                // 向用户显示出错消息
                Console.WriteLine("The file could not be read:" + e.Message);
            }
        }

        //保存到文件
        public void Save2DataSoure(DateTime atempTime)
        {
            try
            {
                if (Parent == null)
                    return;
                string tempDate = atempTime.ToString("yyyy-MM-dd HH:mm:ss");
                int i = 0;
                //关口电表 
                if (Parent.Elemeter1List != null)
                {
                    foreach (Elemeter1Class tempEM1 in Parent.Elemeter1List)
                    {
                        tempEM1.Save2DataSource(tempDate);
                    }
                }
                //电表2---设备电表
                if (Parent.Elemeter2 != null)
                    Parent.Elemeter2.Save2DataSource(tempDate);
                //电表3---辅助电表
                if (Parent.Elemeter3 != null)
                    Parent.Elemeter3.Save2DataSource(tempDate);
                //PCS                
                for (i = 0; i < Parent.PCSList.Count; i++)
                    Parent.PCSList[i].Save2DataSource(tempDate);
                //BMS                
                if (Parent.BMS!=null)
                    Parent.BMS.Save2DataSource(tempDate);
                //空调
                if (Parent.TempControl!=null)
                    Parent.TempControl.Save2DataSource(tempDate);
                //液冷
                if (Parent.LiquidCool!=null)
                    Parent.LiquidCool.Save2DataSource(tempDate);
                //传感器
                if (Parent.Fire != null)
                    Parent.Fire.Save2DataSource(tempDate);
                //UPS
                /*                if (UPS != null)
                                    UPS.Save2DataSource(tempDate);*/
                //其他 
            }
            catch (Exception ex)
            {
                log.Error("Save2DataSoure: " + ex.Message);
            }
            finally
            {

            }
        }



        //将数据整理存入文件
        public void Save2CloudFile(DateTime tempTime)
        {
            if (Parent == null)
                return;
            string strTime = tempTime.ToString("yyyyMMddHHmmss");
            //电表1---设备电表
            for (int i = 0; i < Parent.Elemeter1List.Count; i++)
            {
                Parent.Elemeter1List[i].time = tempTime;
                ConvertToJson(Parent.Elemeter1List[i], strUpPath, "\\0met" + strTime + i.ToString() + ".json");
            }
            //if (Parent.Elemeter1 != null)
            //{
            //    Parent.Elemeter1.time = tempTime;
            //    ConvertToJson(Parent.Elemeter1, strUpPath, "\\0met" + strTime + ".json");
            //}
            //电表2---储能电表
            if (Parent.Elemeter2 != null)
            {
                Parent.Elemeter2.time = tempTime;
                ConvertToJson(Parent.Elemeter2, strUpPath, "\\1met" + strTime + ".json");
            }

            //电表3---设备电表
            if (Parent.Elemeter3 != null)
            {
                Parent.Elemeter3.time = tempTime;
                ConvertToJson(Parent.Elemeter3, strUpPath, "\\2met" + strTime + ".json");
            }

            //电表4---设备电表
            if (Parent.Elemeter4 != null)
            {
                Parent.Elemeter4.time = tempTime;
                ConvertToJson(Parent.Elemeter4, strUpPath, "\\3met" + strTime + ".json");
            }
            //汇流柜电表
            if (Parent.Elemeter2H != null)
            {
                Parent.Elemeter2H.time = tempTime;
                ConvertToJson(Parent.Elemeter2H, strUpPath, "\\4met" + strTime + ".json");
            }
            //PCS
            for (int i = 0; i < Parent.PCSList.Count; i++)
            {
                Parent.PCSList[i].time = tempTime;
                ConvertToJson(Parent.PCSList[i], strUpPath, "\\" + i.ToString() + "pcs" + strTime + ".json");
            }

            //BMS
            if (  Parent.BMS!=null)
            {
                Parent.BMS.time = tempTime;
                ConvertToJson(Parent.BMS , strUpPath, "\\"   + "0bms" + strTime + ".json");
            }

            //TempControl
            if (Parent.TempControl != null)
            {
                Parent.TempControl.time = tempTime;
                ConvertToJson(Parent.TempControl, strUpPath, "\\" +  "0air" + strTime + ".json");
            }
            //液冷
            //TempControl
            if (Parent.LiquidCool != null)
            {
                Parent.LiquidCool.time = tempTime;
                ConvertToJson(Parent.LiquidCool, strUpPath, "\\" + "0liq" + strTime + ".json");
                //log.
            }
            //除湿机
            if (Parent.Dehumidifier != null)
            {
                Parent.Dehumidifier.time = tempTime;
                ConvertToJson(Parent.Dehumidifier, strUpPath, "\\" + "0csj" + strTime + ".json");
            }
            //消防
            if (Parent.Fire != null)
            { 
                Parent.Fire.time = tempTime;
                ConvertToJson(Parent.Fire, strUpPath, "\\0fir" + strTime + ".json");
            }
            //EMS
            Parent.time = tempTime;
            ConvertToJson(Parent, strUpPath, "\\0ems" + strTime + ".json");
        }
        
        public void SaveProfit2Cloud(string astrDate)
        {
            ConvertToJson(Parent.Profit2Cloud, strUpPath, "\\0pem" + astrDate + ".json");
        }

        //模拟未连接任何设备的EMS制造数据
        public void SaveProfit2CloudTest(string strTime)
        {
            ConvertToJson(Parent.Profit2Cloud, strUpPath, "\\0pem" + strTime + ".json");
        }

        public void UploadProfit2Cloud(string json, string astrDate)
        {
            ConvertToJson(json, strUpPath, "\\0pem" + astrDate + ".json");
        }

        public void SaveFault2Cloud(string astrDate)
        {
            string id = Guid.NewGuid().ToString();
            ConvertToJson(Parent.Fault2Cloud, strUpPath, "\\0fau" + astrDate + "UUID" + id + ".json");
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ///
        //接收到的文件
        //
        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


        private async void ExecOtaFormCloud(string version)
        {
            bool isUpdate = await frmSet.CheckAndUpdateAsyncNoBox(version);
            if (isUpdate)
            {
                frmSet.PowerGPIO(0);
                frmSet.Set_Cloudlimits();
                if (frmMain.Selffrm.AllEquipment.Led != null)
                {
                    frmMain.Selffrm.AllEquipment.Led.Set_Led_ShutDown();
                }
                frmMain.Selffrm.Close();
            }

        }

        public void ImplOta(string astrData)
        {
            try
            {
                if (astrData == "")
                    return ;
                JObject jsonObject = JObject.Parse(astrData);
                string strID = "";
                strID = jsonObject["id"].ToString(); //int.Parse   bool.Parse
                string strTopic = jsonObject["method"].ToString();
                if (strTopic != "aiot/ota")
                    return ;
                //9.11
                var param = jsonObject["params"];
                string version = param["version"].ToString();
                if (version != "")
                {
                    ExecOtaFormCloud(version);
                }
            }
            catch (Exception ex)
            {
                log.Error("ImplOta: " + ex.Message);
                return ;
            }

            return ;
        }
        
        public bool DataRetransmission(string astrData)
        {
            bool result = false;
            try
            {             
                if (astrData == "")
                    return false;
                JObject jsonObject = JObject.Parse(astrData);
                string strID = "";
                strID = jsonObject["id"].ToString(); //int.Parse   bool.Parse
                string strTopic = jsonObject["method"].ToString();
                if (strTopic != "aiot/uploadData")
                    return false;
                //9.11
                var param = jsonObject["params"];
                if (param["topic"].ToString() == "pem" && param["iot_code"] != null)
                {
                    if (param["iot_code"].ToString() == frmMain.Selffrm.AllEquipment.iot_code)
                    {
                        string start = param["start"].ToString();
                        string end = param["end"].ToString();
                        //string sqlQuery2 = "SELECT * FROM profit WHERE rTime = '" + tempDate + "'"; // 你的查询语句
                        string sqlQuery = $"SELECT * FROM profit WHERE rTime BETWEEN '{start}' AND '{end}'";
                        DBConnection.UploadCloud(sqlQuery);
                        result = true;
                    }           
                }
            }
            catch (Exception ex)
            {
                log.Error("DataRetransmission: " + ex.Message);
                return false;
            }

            return result;           
        }

        public void GetHeartbeat(string astrData, bool aIsFileData = false)
        {
            try
            {
                JObject jsonObject = null;
                jsonObject = JObject.Parse(astrData);
                string ID = jsonObject["HeartBeatID"].ToString();
                log.Info("接收心跳uuid: " + ID);
                if (ID == HeartbeatID)
                {
                    if (!ConnectToCloud)
                    {
                        ConnectToCloud = true;
                    }
                    receivedHeartbeatResponse = true;
                }
            }
            catch (Exception ex)
            {
                log.Error("GetHeartbeat: " + ex.Message);
            }
        }

        //接收均衡策略数据
        /// <param name="astrTacticFile"></param>
        public string GetServerBalaTactics(string astrData, bool aIsFileData = false)
        {
            JObject jsonObject = null;
            string strDataFile = "";
            if (aIsFileData)
            {
                strDataFile = strDownPath + "\\" + astrData;
                if (!File.Exists(strDataFile))
                    return "";
                StreamReader file = File.OpenText(strDataFile);
                JsonTextReader reader = new JsonTextReader(file);
                jsonObject = (JObject)JToken.ReadFrom(reader);
            }
            else
            {
                if (astrData == "")
                    return "";
                jsonObject = JObject.Parse(astrData);
            }
            string strID = "";
            try
            {
                strID = jsonObject["id"].ToString(); //int.Parse   bool.Parse
                string date = jsonObject["params"]["date"].ToString();
                string strTopic = jsonObject["method"].ToString();
                if (strTopic != "ems/BalaStrategy")
                    return "";
                int iTacticCount = jsonObject["params"]["strategy"].Count();

                //只有设置接受云策略 且 为主机 才接收云下发的策略
                if (frmSet.config.UseBalaTactics == 0)
                    return strID;

                //清理旧数据
                DBConnection.ExecSQL("delete FROM balatactics");
                string strData = "";
                //增加新数据
                for (int i = 0; i < iTacticCount; i++)
                {
                    strData = jsonObject["params"]["strategy"][i]["start"].ToString() + "','"
                        + jsonObject["params"]["strategy"][i]["end"].ToString();

                    //从云获取策略插入数据库中
                    strData = "INSERT into balatactics (startTime,endTime)VALUES('" + strData + "')";
                    DBConnection.ExecSQL(strData);
                }
                //更新策略
                frmMain.BalaTacticsList.LoadFromMySQL();
                frmMain.ShowShedule2Char(false);
                frmMain.BalaTacticsList.ActiveIndex = -1;
                if (aIsFileData)
                    File.Delete(strDataFile);
            }
            catch
            { }
            return strID;
        }


        //接收到均衡控制命令
        public string GetBalaTable(string astrData)
        {
            string strID = "";
            try
            {
                if (astrData == "")
                    return "";
                JObject jsonObject = JObject.Parse(astrData);
                strID = jsonObject["id"].ToString(); //int.Parse   bool.Parse
                string strTopic = jsonObject["method"].ToString();
                if (strTopic != "aiot/table")
                    return "";
                //9.11
                int iBalaStart = int.Parse(jsonObject["params"]["table"]["BalaStart"].ToString());
                if (FirstRun)
                {
                    FirstRun = false;
                }
                else
                {
                   //从机器不执行网络命令(不开放离网模式)
                   frmControl.SetBala(iBalaStart);
                }
                /*
                 mode:    0手工模式,1预设策略,2网络控制
                 charge:  0待机、1恒压、2恒流、3恒功率、4AC恒压
                 pcsSet:  0充电、1放电
                 pcsSetValue：正整数
                 on: 0关机、1运行
                 */
            }
            catch(Exception ex)
            {
                log.Error("GetBalaTable: " + ex.Message);
            }
            return strID;
        }


        /// <summary>
        /// 接受到策略数据
        ///    "start":"03:00:00",
        ///    "end":"05:00:00",
        ///    "mode":3,//充放电模式 0待机 1恒流 2 恒压 3恒功率 4AC恒压（离网）5自适应需量
        ///    "charge":false,
        ///     "value":100 
        /// </summary>
        /// <param name="astrTacticFile"></param>
        public bool GetServerTactics(string astrData)
        {
            bool result = false;
            try
            {
                if (astrData == "")
                {
                    return false;
                }
                JObject jsonObject = null;
                jsonObject = JObject.Parse(astrData);
                string strID = "";
                strID = jsonObject["id"].ToString(); //int.Parse   bool.Parse
                string date = jsonObject["params"]["date"].ToString();
                string strTopic = jsonObject["method"].ToString();
                if (strTopic != "ems/strategy")
                    return false;
                int iTacticCount = jsonObject["params"]["strategy"].Count();

                //只有设置接受云策略 且 为主机 才接收云下发的策略
                if ((frmSet.config.UseYunTactics == 0)|| (frmSet.config.IsMaster == 0))
                {
                    return false;
                }

                //清理旧数据
                if (DBConnection.CheckRec("select *  FROM tactics"))
                {
                    DBConnection.ExecSQL("delete FROM tactics");
                }

                string strData = "";
                //增加新数据
                for (int i = 0; i < iTacticCount; i++)
                {
                    strData = jsonObject["params"]["strategy"][i]["start"].ToString() + "','"
                        + jsonObject["params"]["strategy"][i]["end"].ToString() + "',";

                    if (bool.Parse(jsonObject["params"]["strategy"][i]["charge"].ToString()))
                        strData += "'充电',";
                    else
                        strData += "'放电',";

                    if (int.Parse(jsonObject["params"]["strategy"][i]["mode"].ToString()) == 3)
                        strData += "'恒功率','" + jsonObject["params"]["strategy"][i]["value"].ToString();
                    else if (int.Parse(jsonObject["params"]["strategy"][i]["mode"].ToString()) == 5)
                        strData += "'自适应需量','" + jsonObject["params"]["strategy"][i]["value"].ToString();


                    //从云获取策略插入数据库中
                    strData = "INSERT into tactics (startTime, endTime,tType, PCSType, waValue)VALUES('" + strData + "')";

                    if (!DBConnection.ExecSQL(strData))
                    {
                        return false;
                    }
                }

                if (frmMain.TacticsList.LoadFromMySQL())
                {
                    frmMain.TacticsList.ActiveIndex = -1;
                    result  = true;
                }
                else
                { 
                    result = false;
                }
            }
            catch (Exception ex)
            {
                log.Error("GetServerTactics: " + ex.Message);
            }
            return result;
        }

        /// <summary>
        /// 尖峰平谷的设置
        ///    "start":"11:30:00",
        ///    "end":"13:30:00",
        ///     "price":0.8,
        ///     "range":3 //  尖：1峰：2平：3谷：4
        /// </summary>
        /// <param name="astrTacticFile"></param>
        public bool  GetServerEPrices(string astrData, bool aIsFileData = false)
        {
            bool result = false;
            try
            {
                JObject jsonObject = null;
                string strDataFile = "";
                if (aIsFileData)
                {
                    strDataFile = strDownPath + "\\" + astrData;
                    if (!File.Exists(strDataFile))
                        return false;
                    StreamReader file = File.OpenText(strDataFile);
                    JsonTextReader reader = new JsonTextReader(file);
                    jsonObject = (JObject)JToken.ReadFrom(reader);
                }
                else
                {
                    if (astrData == "")
                        return false;
                    jsonObject = JObject.Parse(astrData);
                }

                string date = jsonObject["params"]["date"].ToString();
                int iPriceCount = jsonObject["params"]["price"].Count();
                string strTopic = jsonObject["method"].ToString();
                if (strTopic != "meter/price")
                    return false;

                //清理旧数据
                DBConnection.ExecSQL("delete FROM electrovalence");
                string strData = "";
                int isection = 0;
                //增加新数据
                for (int i = 0; i < iPriceCount; i++)
                {
                    isection = int.Parse(jsonObject["params"]["price"][i]["range"].ToString());
                    frmSet.Prices[0, isection] = (int)Math.Round(double.Parse(jsonObject["params"]["price"][i]["buyPrice"].ToString()) * 100);
                    frmSet.Prices[1, isection] = (int)Math.Round(double.Parse(jsonObject["params"]["price"][i]["sellPrice"].ToString()) * 100);
                    strData = jsonObject["params"]["price"][i]["start"].ToString() + "','"
                        + isection.ToString() + "','0','"
                        + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    strData = "INSERT into electrovalence (startTime, eName,section, rTime)VALUES('" + strData + "')";
                    
                    if(DBConnection.ExecSQL(strData))
                    {
                        result = true;
                    }
                }
                //更新策略
                if (result)
                {
                    frmMain.TacticsList.LoadJFPGFromSQL();
                    if (aIsFileData)
                        File.Delete(strDataFile);
                }
            }
            catch (Exception ex)
            {
                log.Error("GetServerEPrices: " + ex.Message);
            }
            //输出返回数据
            return result;
        }

        
        //云发来的策略数据
        public string GetAiotTable(string astrData)
        {
            if (astrData == "")
                return "";
            JObject jsonObject = JObject.Parse(astrData);
            string strID = "";
            string[] ipcsSets = { "充电", "放电" };
            try
            {
                strID = jsonObject["id"].ToString(); //int.Parse   bool.Parse
                //return strID;
                //string date = jsonObject["params"]["date"].ToString();
                string strTopic = jsonObject["method"].ToString();
                if (strTopic != "aiot/table")
                    return "";
                //iMode手工、策略
                int iMode= int.Parse(jsonObject["params"]["table"]["mode"].ToString());
                //充放电 //0充电为正,1放电
                int icharge = int.Parse(jsonObject["params"]["table"]["charge"].ToString());
                //待机、恒压、恒流恒、恒功率 , AC恒压（离网） ，自适应需量
                int ipcsSet = int.Parse(jsonObject["params"]["table"]["pcsSet"].ToString());
                int ipcsSetValue=int.Parse(jsonObject["params"]["table"]["pcsSetValue"].ToString());
                int iOn =int.Parse(jsonObject["params"]["table"]["on"].ToString());
                if (FirstRun)
                {
                    FirstRun = false;
                }
                else
                {
                    //从机器不执行网络命令(不开放离网模式)
                    if ((frmSet.config.IsMaster == 0)&&(ipcsSet!=4))
                        frmControl.SetControl(iMode, PCSClass.PCSTypes[ipcsSet], ipcsSets[icharge], ipcsSetValue,iOn, true);
                } 
                /*
                 mode:    0手工模式,1预设策略,2网络控制
                 charge:  0待机、1恒压、2恒流、3恒功率、4AC恒压
                 pcsSet:  0充电、1放电
                 pcsSetValue：正整数
                 on: 0关机、1运行
                 */
            }
            catch (Exception ex)
            {
                log.Error("GetAiotTable: " + ex.Message);
            }
            return strID;
        }

        //设置窗口的几个限制值
        public bool GetServerEMSLimit(string astrData)
        {
            bool bResult = false;
            try
            {
                if (astrData == "")
                    return false;

                JObject jsonObject = JObject.Parse(astrData);
                string strTopic = jsonObject["method"].ToString();
                if (strTopic != "ems/limit")
                {
                    return false;
                }
                else
                {
                    if (jsonObject["params"] != null)
                    {
                        var parameters = jsonObject["params"];
                        if (parameters["requireLimit"] != null)
                        {
                            frmSet.cloudLimits.MaxGridKW = int.Parse(parameters["requireLimit"].ToString());
                        }
                        if (parameters["invertPower"] != null)
                        {
                            frmSet.cloudLimits.MinGridKW = int.Parse(parameters["invertPower"].ToString());
                        }
                        if (parameters["socUp"] != null)
                        {
                            frmSet.cloudLimits.MaxSOC = int.Parse(parameters["socUp"].ToString());
                        }
                        if (parameters["socDown"] != null)
                        {
                            frmSet.cloudLimits.MinSOC = int.Parse(parameters["socDown"].ToString());
                        }
                        if (parameters["WarnMaxGridKW"] != null)
                        {
                            frmSet.cloudLimits.WarnMaxGridKW = int.Parse(parameters["WarnMaxGridKW"].ToString());
                        }
                        if (parameters["WarnMinGridKW"] != null)
                        {
                            frmSet.cloudLimits.WarnMinGridKW = int.Parse(parameters["WarnMinGridKW"].ToString());
                        }
                        if (parameters["PcsKva"] != null)
                        {
                            frmSet.cloudLimits.PcsKva = int.Parse(parameters["PcsKva"].ToString());
                        }
                        if (parameters["Pre_Client_PUMdemand_Max"] != null)
                        {
                            frmSet.cloudLimits.Pre_Client_PUMdemand_Max = int.Parse(parameters["Pre_Client_PUMdemand_Max"].ToString());
                        }
                        if (parameters["EnableActiveReduce"] != null)
                        {
                            frmSet.cloudLimits.EnableActiveReduce = int.Parse(parameters["EnableActiveReduce"].ToString());
                        }
                        if (parameters["PumScale"] != null)
                        {
                            frmSet.cloudLimits.PumScale = int.Parse(parameters["PumScale"].ToString());
                        }
                        if (parameters["AllUkvaWindowSize"] != null)
                        {
                            frmSet.cloudLimits.AllUkvaWindowSize = int.Parse(parameters["AllUkvaWindowSize"].ToString());
                        }
                        if (parameters["PumTime"] != null)
                        {
                            frmSet.cloudLimits.PumTime = int.Parse(parameters["PumTime"].ToString());
                        }
                        if (parameters["BmsDerateRatio"] != null)
                        {
                            frmSet.cloudLimits.BmsDerateRatio = int.Parse(parameters["BmsDerateRatio"].ToString());
                        }
                        if (parameters["FrigOpenLower"] != null)
                        {
                            frmSet.cloudLimits.FrigOpenLower = int.Parse(parameters["FrigOpenLower"].ToString());
                        }
                        if (parameters["FrigOffLower"] != null)
                        {
                            frmSet.cloudLimits.FrigOffLower = int.Parse(parameters["FrigOffLower"].ToString());
                        }
                        if (parameters["FrigOffUpper"] != null)
                        {
                            frmSet.cloudLimits.FrigOffUpper = int.Parse(parameters["FrigOffUpper"].ToString());
                        }


                        if (frmSet.Set_Cloudlimits())
                        {
                            bResult = true;
                        }
                        else
                        {
                            bResult = false;
                        }
                    }
                    else
                    {
                        bResult = false;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("GetServerEMSLimit: " + ex.Message);
            }
            return bResult;
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
