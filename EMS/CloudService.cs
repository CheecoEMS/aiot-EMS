using M2Mqtt.Messages;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using static EMS.MqttManager;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;

namespace EMS
{
    public class CloudService
    {
        private readonly MqttManager _mqtt;
        public AllEquipmentClass Parent = null;

        private static readonly ILog log = LogManager.GetLogger("CloudService");

        private readonly object _lockMqtt = new object();

        #region ===== 上传线程控制 =====

        private Thread _uploadThread;
        private volatile bool _uploadThreadStarted = false;
        private volatile bool _uploadThreadRunning = false;

        #endregion

        #region ===== 下载线程控制 =====

        private Thread _downloadThread;
        private volatile bool _downloadThreadStarted = false;
        private volatile bool _downloadThreadRunning = false;

        #endregion

        #region ===== 状态 =====

        public volatile bool ConnectToCloud = false;
        public bool FirstRun = true;

        #endregion

        #region ===== Topics =====

        public string PriceTopic;
        public string TacticTopic;
        public string EMSLimitTopic;
        public string AIOTTableTopic;
        public string BalaTableTopic;
        public string BalaTacticTopic;
        public string OtaTopic;

        public string PriceTopic_new;
        public string TacticTopic_new;
        public string EMSLimitTopic_new;
        public string AIOTTableTopic_new;
        public string BalaTableTopic_new;

        #endregion

        #region ===== 文件上传 =====

        public string DataPath;
        public string Filters = "*.json";
        public int ReadyBatchSize = 20;
        public int FailBatchSize = 10;
        private FileQueue _fileQueue;

        #endregion

        #region ===== ctor =====

        public CloudService(MqttManager mqtt)
        {
            _mqtt = mqtt;
            _mqtt.MessageReceived += OnCloudMessage;
            _mqtt.StateChanged += OnMqttStateChanged;

            DataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UpData");
            _fileQueue = new FileQueue(DataPath);
        }

        #endregion

        #region ===== 生命周期 =====

        public void Start()
        {
            try {
                InitCloud();

                _uploadThreadRunning = true;
                _downloadThreadRunning = true;
                StartUploadThreadOnce();
                StartDownloadThreadOnce();

                _mqtt.Start();


                log.Info("CloudService Start.");
            } catch (Exception ex){
                log.Error("CloudService Start Failed!", ex);
            }
        }

        public void Stop()
        {
            try
            {
                _uploadThreadRunning = false;
                _downloadThreadRunning = false;
                _mqtt.Stop();

                _uploadThread?.Join(2000);
                _downloadThread?.Join(2000);

                log.Info("CloudService stopped.");
            }
            catch (Exception ex)
            {
                log.Error("CloudService Stop Failed!", ex);
            }
        }

        #endregion

        #region ===== MQTT 事件 =====
        private void OnMqttStateChanged(MqttState state)
        {
            bool connected = (state == MqttState.Connected);

            // ✅ 统一入口，复用你原有逻辑
            OnConnectionStateChanged(connected);
        }


        private void OnConnectionStateChanged(bool connected)
        {
            ConnectToCloud = connected;

            if (connected)
            {
                SubscribeAllTopics();
            }
        }

        private void OnCloudMessage(object sender, MqttMsgPublishEventArgs e)
        {
            // ✅ MQTT 回调线程立刻释放
            Task.Run(() => HandleCloudMessage(e));
        }

        #endregion

        #region ===== 消息处理 =====

        private void HandleCloudMessage(MqttMsgPublishEventArgs e)
        {
            try
            {
                string topic = e.Topic;
                string message = System.Text.Encoding.Default.GetString(e.Message);

                JObject jsonObject = JObject.Parse(message);
                string strID = jsonObject["id"]?.ToString() ?? "";

                bool result;

                if (topic == TacticTopic + "request" || topic == TacticTopic_new + "request")
                {
                    result = GetServerTactics(message);
                    PublishResult(TacticTopic, strID, result);
                }
                else if (topic == PriceTopic + "request" || topic == PriceTopic_new + "request")
                {
                    result = GetServerEPrices(message);
                    PublishResult(PriceTopic, strID, result);
                }
                else if (topic == EMSLimitTopic + "request" || topic == EMSLimitTopic_new + "request")
                {
                    result = GetServerEMSLimit(message);
                    PublishResult(EMSLimitTopic, strID, result);
                }
                else if (topic == AIOTTableTopic + "request" || topic == AIOTTableTopic_new + "request")
                {
                    strID = GetAiotTable(message);
                    PublishResult(AIOTTableTopic, strID, true);
                }
                else if (topic == BalaTableTopic + "request" || topic == BalaTableTopic_new + "request")
                {
                    strID = GetBalaTable(message);
                    PublishResult(BalaTableTopic, strID, true);
                }
/*                else if (topic == OtaTopic + "request")
                {
                    ImplOta(message);
                }*/
            }
            catch (Exception ex)
            {
                log.Error("HandleCloudMessage error", ex);
            }
        }

        private void PublishResult(string baseTopic, string id, bool success)
        {
            string payload = success
                ? $"{{\"jsonrpc\":\"2.0\",\"result\":true,\"id\":\"{id}\"}}"
                : $"{{\"jsonrpc\":\"2.0\",\"result\":false,\"id\":\"{id}\"}}";

            lock (_lockMqtt)
            {
                _mqtt.Publish(baseTopic + "response/" + id, payload, PublishQos.AtLeastOnce);
            }
        }

        #endregion

        #region ===== 上传线程 =====

        private void StartUploadThreadOnce()
        {
            if (_uploadThreadStarted)
                return;

            _uploadThreadStarted = true;

            _uploadThread = new Thread(UploadDataLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = "UploadDataThread"
            };

            _uploadThread.Start();
        }

        private void UploadDataLoop()
        {
            while (_uploadThreadRunning)
            {
                try
                {
                    if (ConnectToCloud)
                    {
                        SendMqttData();
                    }
                }
                catch (Exception ex)
                {
                    log.Error("UploadDataLoop error", ex);
                }

                Thread.Sleep(30000);
            }

            log.Info("UploadDataThread exited");
        }

        #endregion

        #region ===== 文件发送 =====

        private void SendMqttData()
        {
            // 1️ ready
            SendBatch(_fileQueue.DequeueReadyBatch(ReadyBatchSize));
        }

        private void SendBatch(string[] files)
        {
            if (files == null || files.Length == 0)
            {
                return;
            }

            foreach (var file in files)
            {
                try
                {
                    string content = File.ReadAllText(file);
                    string topic = Path.GetFileName(file).Substring(1, 3);

                    lock (_lockMqtt)
                    {
                        _mqtt.Publish(topic, content, PublishQos.AtLeastOnce);
                    }

                    _fileQueue.MarkSuccess(file);
                }
                catch (Exception ex)
                {
                    log.Error($"Send failed: {file}", ex);
                    _fileQueue.MarkFailed(file);
                }
            }
        }

        #endregion

        #region ===== 订阅 =====

        private void SubscribeAllTopics()
        {
            _mqtt.Subscribe(PriceTopic + "request");
            _mqtt.Subscribe(TacticTopic + "request");
            _mqtt.Subscribe(EMSLimitTopic + "request");
            _mqtt.Subscribe(AIOTTableTopic + "request");
            _mqtt.Subscribe(BalaTableTopic + "request");
            _mqtt.Subscribe(OtaTopic + "request");

            _mqtt.Subscribe(PriceTopic_new + "request");
            _mqtt.Subscribe(TacticTopic_new + "request");
            _mqtt.Subscribe(EMSLimitTopic_new + "request");
            _mqtt.Subscribe(AIOTTableTopic_new + "request");
            _mqtt.Subscribe(BalaTableTopic_new + "request");
        }

        #endregion

        #region ===== 初始化 =====

        private void InitCloud()
        {
            try
            {
                PriceTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/meter/price/";
                TacticTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/ems/strategy/";
                EMSLimitTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/ems/limit/";

                //AIOTTableTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/ctl/table/";
                string strID = frmSet.config.SysID;
                if (strID.Length >= 7)
                    strID = strID.Substring(strID.Length - 7, 7);
                AIOTTableTopic = "/rpc/ctl" + strID + "/aiot/table/";

                BalaTableTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/aiot/table/";
                BalaTacticTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/ems/BalaStrategy/";
                //OtaTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/aiot/ota/";

                //新版topic
                PriceTopic_new = "/rpc/" + frmMain.Selffrm.AllEquipment.full_iot_code + "/meter/price/";
                TacticTopic_new = "/rpc/" + frmMain.Selffrm.AllEquipment.full_iot_code + "/ems/strategy/";
                EMSLimitTopic_new = "/rpc/" + frmMain.Selffrm.AllEquipment.full_iot_code + "/ems/limit/";
                AIOTTableTopic_new = "/rpc/ctl" + frmMain.Selffrm.AllEquipment.full_iot_code + "/aiot/table/";
                BalaTableTopic_new = "/rpc/" + frmMain.Selffrm.AllEquipment.full_iot_code + "/aiot/table/";
            }
            catch (Exception ex)
            {
                log.Error("IniClound: " + ex.Message);
            }
        }

        #endregion

        #region ===== 订阅到消息后的回调函数实现方法 =====

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
                frmSet.cloudLimits.OpenBala = iBalaStart;
                frmSet.Set_Cloudlimits();
            }
            catch (Exception ex)
            {
                log.Error("GetBalaTable: " + ex.Message);
            }
            return strID;
        }

        public bool GetServerTactics(string astrData)
        {
            bool result = false;
            try
            {
                //只有设置接受云策略 且 为主机 才接收云下发的策略
                if (frmSet.config != null)
                {
                    if ((frmSet.config.UseYunTactics == 0)|| (frmSet.config.IsMaster == 0))
                    {
                        return false;
                    }
                }
                else
                {
                    log.Error("frmSet.config 未初始化成功，GetServerTactics无法判断");
                    return false;
                }

                //判断内容是否为空
                if (astrData == "")
                {
                    return false;
                }
                JObject jsonObject = null;
                jsonObject = JObject.Parse(astrData);

                if (jsonObject["method"] != null)
                {
                    string strTopic = jsonObject["method"].ToString();
                    if (strTopic != "ems/strategy")
                        return false;

                    if (jsonObject["params"]["date"] != null && jsonObject["params"]["strategy"] != null)
                    {
                        string strDate = jsonObject["params"]["date"].ToString();
                        int iTacticCount = jsonObject["params"]["strategy"].Count();

                        if (iTacticCount > 0)
                        {
                            // 用于跟踪已经删除过的日期，避免重复删除
                            System.Collections.Generic.HashSet<string> deletedDates = new System.Collections.Generic.HashSet<string>();

                            //增加新数据
                            for (int i = 0; i < iTacticCount; i++)
                            {
                                string strInsert = "";
                                string start = "";
                                string end = "";
                                string charge = "";
                                string mode = "";
                                string value = "";
                                string strategyDate = "";

                                if (jsonObject["params"]["strategy"][i]["start"] != null)
                                {
                                    start = jsonObject["params"]["strategy"][i]["start"].ToString();
                                }

                                if (jsonObject["params"]["strategy"][i]["end"] != null)
                                {
                                    end = jsonObject["params"]["strategy"][i]["end"].ToString();
                                }

                                if (jsonObject["params"]["strategy"][i]["charge"] != null)
                                {
                                    if (bool.Parse(jsonObject["params"]["strategy"][i]["charge"].ToString()))
                                        charge = "充电";
                                    else
                                        charge = "放电";
                                }

                                if (jsonObject["params"]["strategy"][i]["mode"] != null)
                                {
                                    if (int.Parse(jsonObject["params"]["strategy"][i]["mode"].ToString()) == 3)
                                    {
                                        mode = "恒功率";
                                    }
                                    else if (int.Parse(jsonObject["params"]["strategy"][i]["mode"].ToString()) == 5)
                                    {
                                        mode = "自适应需量";
                                    }
                                }

                                if (jsonObject["params"]["strategy"][i]["value"] != null)
                                {
                                    value = jsonObject["params"]["strategy"][i]["value"].ToString();
                                }

                                // 获取策略日期，如果不存在则使用默认日期
                                if (jsonObject["params"]["strategy"][i]["strategyDate"] != null)
                                {
                                    strategyDate = jsonObject["params"]["strategy"][i]["strategyDate"].ToString();
                                }
                                else
                                {
                                    strategyDate = strDate;
                                }

                                // 如果该日期的策略还没有被删除，则执行删除操作
                                if (!deletedDates.Contains(strategyDate))
                                {
                                    //删除同日期的策略
                                    // string strDelete = "delete from tactics where rTime = '" + strategyDate + "';";
                                    // DBConnection.ExecSQL(strDelete);
                                    string strDelete = "delete from tactics where rTime = @strategyDate";
                                    var parameters = new Dictionary<string, object> { { "@strategyDate", strategyDate } };
                                    if (DBConnection.ExecSQLWithParams(strDelete, parameters) >= 0)
                                    {
                                        // 只有在删除成功后才添加到已删除列表
                                        deletedDates.Add(strategyDate);
                                    }
                                    else
                                    {
                                        log.Error($"删除策略数据失败，日期：{strategyDate}，数据库操作返回-1");
                                    }
                                }

                                if (start != null && end != null && charge != null && mode != null && value != null)
                                {
                                    // strInsert = "INSERT INTO tactics (startTime, endTime, tType, PCSType, waValue, rTime) " +
                                    //         "VALUES ('" + start + "', '" + end + "', '" + charge + "', '" + mode + "', '" + value + "', '" + strategyDate + "')";

                                    // //插入
                                    // if (DBConnection.ExecSQL(strInsert))
                                    // {
                                    //     result = true;
                                    // }

                                    strInsert = "INSERT INTO tactics (startTime, endTime, tType, PCSType, waValue, rTime) " +
                                            "VALUES (@start, @end, @charge, @mode, @value, @strategyDate)";

                                    var parameters = new Dictionary<string, object>
                                    {
                                        { "@start", start },
                                        { "@end", end },
                                        { "@charge", charge },
                                        { "@mode", mode },
                                        { "@value", value },
                                        { "@strategyDate", strategyDate }
                                    };

                                    //插入
                                    if (DBConnection.ExecSQLWithParams(strInsert, parameters) >= 0)
                                    {
                                        result = true;
                                    }
                                }
                            }

                            if (frmMain.TacticsList.LoadFromMySQL(0))
                            {
                                frmMain.TacticsList.ActiveIndex = -1;
                                result = true;
                            }
                            else
                            {
                                result = false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("GetServerTactics: " + ex.Message);
            }
            return result;
        }

        public bool GetServerEPrices(string astrData, bool aIsFileData = false)
        {
            bool result = true;
            try
            {
                if (astrData == "")
                {
                    return false;
                }
                JObject jsonObject = null;
                jsonObject = JObject.Parse(astrData);
                if (jsonObject["method"] != null)
                {
                    string strTopic = jsonObject["method"].ToString();
                    if (strTopic != "meter/price")
                        return false;

                    if (jsonObject["params"]["date"] != null && jsonObject["params"]["price"] != null)
                    {
                        //获取策略日期
                        string strDate = jsonObject["params"]["date"].ToString();
                        //获取数量
                        int iPriceCount = jsonObject["params"]["price"].Count();

                        if (iPriceCount > 0)
                        {
                            // 用于跟踪已经删除过的日期，避免重复删除
                            System.Collections.Generic.HashSet<string> deletedDates = new System.Collections.Generic.HashSet<string>();

                            //增加新数据
                            for (int i = 0; i < iPriceCount; i++)
                            {
                                string strInsert = "";
                                int isection = -1;
                                string start = "";
                                string pricDate = "";

                                if (jsonObject["params"]["price"][i]["range"] != null)
                                {
                                    isection = int.Parse(jsonObject["params"]["price"][i]["range"].ToString());

                                    if (jsonObject["params"]["price"][i]["buyPrice"] != null)
                                    {
                                        frmSet.Prices[0, isection] = (int)Math.Round(double.Parse(jsonObject["params"]["price"][i]["buyPrice"].ToString()) * 100);
                                    }

                                    if (jsonObject["params"]["price"][i]["sellPrice"] != null)
                                    {
                                        frmSet.Prices[1, isection] = (int)Math.Round(double.Parse(jsonObject["params"]["price"][i]["sellPrice"].ToString()) * 100);
                                    }
                                }

                                if (jsonObject["params"]["price"][i]["start"] != null)
                                {
                                    start = jsonObject["params"]["price"][i]["start"].ToString();
                                }

                                // 获取电价日期，如果不存在则使用默认日期
                                if (jsonObject["params"]["price"][i]["pricDate"] != null)
                                {
                                    pricDate = jsonObject["params"]["price"][i]["pricDate"].ToString();
                                }
                                else
                                {
                                    pricDate = strDate;
                                }

                                // 如果该日期的电价还没有被删除，则执行删除操作
                                if (!deletedDates.Contains(pricDate))
                                {
                                    //删除同日期的电价
                                    // string strDelete = "delete from electrovalence where rTime = '" + pricDate + "';";
                                    // DBConnection.ExecSQL(strDelete);
                                    string strDelete = "delete from electrovalence where rTime = @pricDate";
                                    var parameters = new Dictionary<string, object> { { "@pricDate", pricDate } };
                                    if (DBConnection.ExecSQLWithParams(strDelete, parameters) > 0)
                                    {
                                        deletedDates.Add(pricDate);
                                    }
                                    else {
                                        log.Error($"删除费率数据失败，日期：{pricDate}，数据库操作返回-1");
                                    }

                                }

                                if (start != null && isection != -1)
                                {
                                    // strInsert = "INSERT INTO electrovalence (startTime, eName,section, rTime) " +
                                    //         "VALUES ('" + start + "', '" + isection.ToString() + "', '" + "0" + "', '" + pricDate + "')";

                                    // //插入
                                    // if (!DBConnection.ExecSQL(strInsert))
                                    // {
                                    //     result = false;
                                    // }

                                    strInsert = "INSERT INTO electrovalence (startTime, eName, section, rTime) " +
                                            "VALUES (@start, @eName, @section, @pricDate)";

                                    var parameters = new Dictionary<string, object>
                                    {
                                        { "@start", start },
                                        { "@eName", isection.ToString() },
                                        { "@section", "0" },
                                        { "@pricDate", pricDate }
                                    };

                                    //插入
                                    if (DBConnection.ExecSQLWithParams(strInsert, parameters) < 0)
                                    {
                                        result = false;
                                    }
                                }
                            }

                            // 写入电表不影响返回结果
                            frmMain.Selffrm.AllEquipment.LoadJFPGSuccess = false;
                        }
                    }
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
                int iMode = int.Parse(jsonObject["params"]["table"]["mode"].ToString());
                //充放电 //0充电为正,1放电
                int icharge = int.Parse(jsonObject["params"]["table"]["charge"].ToString());
                //待机、恒压、恒流恒、恒功率 , AC恒压（离网） ，自适应需量
                int ipcsSet = int.Parse(jsonObject["params"]["table"]["pcsSet"].ToString());
                int ipcsSetValue = int.Parse(jsonObject["params"]["table"]["pcsSetValue"].ToString());
                int iOn = int.Parse(jsonObject["params"]["table"]["on"].ToString());
                if (FirstRun)
                {
                    FirstRun = false;
                }
                else
                {
                    //从机器不执行网络命令(不开放离网模式)
                    if ((frmSet.config.IsMaster == 0)&&(ipcsSet!=4))
                        frmControl.SetControl(iMode, PCSClass.PCSTypes[ipcsSet], ipcsSets[icharge], ipcsSetValue, iOn, true);
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
                    // 清空之前的修改标记
                    frmSet.cloudLimits.ModifiedFields.Clear();

                    if (jsonObject["params"] != null)
                    {
                        var parameters = jsonObject["params"];
                        if (parameters["requireLimit"] != null)
                        {
                            frmSet.cloudLimits.MaxGridKW = int.Parse(parameters["requireLimit"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("MaxGridKW");
                        }
                        if (parameters["invertPower"] != null)
                        {
                            frmSet.cloudLimits.MinGridKW = int.Parse(parameters["invertPower"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("MinGridKW");
                        }
                        if (parameters["socUp"] != null)
                        {
                            frmSet.cloudLimits.MaxSOC = int.Parse(parameters["socUp"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("MaxSOC");
                        }
                        if (parameters["socDown"] != null)
                        {
                            frmSet.cloudLimits.MinSOC = int.Parse(parameters["socDown"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("MinSOC");
                        }
                        if (parameters["WarnMaxGridKW"] != null)
                        {
                            frmSet.cloudLimits.WarnMaxGridKW = int.Parse(parameters["WarnMaxGridKW"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("WarnMaxGridKW");
                        }
                        if (parameters["WarnMinGridKW"] != null)
                        {
                            frmSet.cloudLimits.WarnMinGridKW = int.Parse(parameters["WarnMinGridKW"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("WarnMinGridKW");
                        }
                        if (parameters["PcsKva"] != null)
                        {
                            frmSet.cloudLimits.PcsKva = int.Parse(parameters["PcsKva"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("PcsKva");
                        }
                        if (parameters["Pre_Client_PUMdemand_Max"] != null)
                        {
                            frmSet.cloudLimits.Pre_Client_PUMdemand_Max = int.Parse(parameters["Pre_Client_PUMdemand_Max"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("Pre_Client_PUMdemand_Max");
                        }
                        if (parameters["EnableActiveReduce"] != null)
                        {
                            frmSet.cloudLimits.EnableActiveReduce = int.Parse(parameters["EnableActiveReduce"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("EnableActiveReduce");
                        }
                        if (parameters["PumScale"] != null)
                        {
                            frmSet.cloudLimits.PumScale = int.Parse(parameters["PumScale"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("PumScale");
                        }
                        if (parameters["AllUkvaWindowSize"] != null)
                        {
                            frmSet.cloudLimits.AllUkvaWindowSize = int.Parse(parameters["AllUkvaWindowSize"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("AllUkvaWindowSize");
                        }
                        if (parameters["PumTime"] != null)
                        {
                            frmSet.cloudLimits.PumTime = int.Parse(parameters["PumTime"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("PumTime");
                        }
                        if (parameters["BmsDerateRatio"] != null)
                        {
                            frmSet.cloudLimits.BmsDerateRatio = int.Parse(parameters["BmsDerateRatio"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("BmsDerateRatio");
                        }
                        if (parameters["FrigOpenLower"] != null)
                        {
                            frmSet.cloudLimits.FrigOpenLower = int.Parse(parameters["FrigOpenLower"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("FrigOpenLower");
                        }
                        if (parameters["FrigOffLower"] != null)
                        {
                            frmSet.cloudLimits.FrigOffLower = int.Parse(parameters["FrigOffLower"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("FrigOffLower");
                        }
                        if (parameters["FrigOffUpper"] != null)
                        {
                            frmSet.cloudLimits.FrigOffUpper = int.Parse(parameters["FrigOffUpper"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("FrigOffUpper");
                        }
                        if (parameters["CellV_Gap"] != null)
                        {
                            frmSet.cloudLimits.CellV_Gap = int.Parse(parameters["CellV_Gap"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("CellV_Gap");
                        }

                        if (frmSet.Set_Cloudlimits_OnlyChange())
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
                    return;
                JObject jsonObject = JObject.Parse(astrData);
                string strID = "";
                strID = jsonObject["id"].ToString(); //int.Parse   bool.Parse
                string strTopic = jsonObject["method"].ToString();
                if (strTopic != "aiot/ota")
                    return;
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
                return;
            }

            return;
        }

        #endregion

        #region ===== 采集数据下载线程 =====

        private void StartDownloadThreadOnce()
        {
            if (_downloadThreadStarted)
                return;

            _downloadThreadStarted = true;

            _downloadThread = new Thread(DownloadDataLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = "DownloadDataThread"
            };

            _downloadThread.Start();
        }

        private void DownloadDataLoop()
        {
            while (_downloadThreadRunning)
            {
                try
                {
                    Save2CloudFile();
                }
                catch (Exception ex)
                {
                    log.Error("DownloadDataLoop error", ex);
                }

                Thread.Sleep(60000);
            }

            log.Info("DownloadDataThread exited");
        }
        #endregion

        #region ===== 将采集数据整理存入文件 =====
        public void Save2CloudFile()
        {
            DateTime tempTime = DateTime.Now;
            if (Parent == null)
                return;
            string strTime = tempTime.ToString("yyyyMMddHHmmss");

            //电表1---设备电表
            for (int i = 0; i < Parent.Elemeter1List.Count; i++)
            {
                Parent.Elemeter1List[i].time = tempTime;
                ConvertToJsonQueue(
                    Parent.Elemeter1List[i],
                    $"0met{strTime}{i}.json"
                );
            }

            //电表2---储能电表
            if (Parent.Elemeter2 != null)
            {
                Parent.Elemeter2.time = tempTime;
                ConvertToJsonQueue(Parent.Elemeter2, $"1met{strTime}.json");
            }

            //电表3---设备电表
            if (Parent.Elemeter3 != null)
            {
                Parent.Elemeter3.time = tempTime;
                ConvertToJsonQueue(Parent.Elemeter3, $"2met{strTime}.json");
            }

            //电表4---设备电表
            if (Parent.Elemeter4 != null)
            {
                Parent.Elemeter4.time = tempTime;
                ConvertToJsonQueue(Parent.Elemeter4, $"3met{strTime}.json");
            }

            //汇流柜电表
            if (Parent.Elemeter2H != null)
            {
                Parent.Elemeter2H.time = tempTime;
                ConvertToJsonQueue(Parent.Elemeter2H, $"4met{strTime}.json");
            }

            //PCS
            for (int i = 0; i < Parent.PCSList.Count; i++)
            {
                Parent.PCSList[i].time = tempTime;
                ConvertToJsonQueue(
                    Parent.PCSList[i],
                    $"0pcs{strTime}{i}.json"
                );
            }

            //BMS
            if (Parent.BMS != null)
            {
                Parent.BMS.time = tempTime;
                ConvertToJsonQueue(Parent.BMS, $"0bms{strTime}.json");
            }

            //TempControl
            if (Parent.TempControl != null)
            {
                Parent.TempControl.time = tempTime;
                ConvertToJsonQueue(Parent.TempControl, $"0air{strTime}.json");
            }

            //液冷
            if (Parent.LiquidCool != null)
            {
                Parent.LiquidCool.time = tempTime;
                ConvertToJsonQueue(Parent.LiquidCool, $"0liq{strTime}.json");
            }

            //除湿机
            if (Parent.Dehumidifier != null)
            {
                Parent.Dehumidifier.time = tempTime;
                ConvertToJsonQueue(Parent.Dehumidifier, $"0csj{strTime}.json");
            }

            //消防
            if (Parent.Fire != null)
            {
                Parent.Fire.time = tempTime;
                ConvertToJsonQueue(Parent.Fire, $"0fir{strTime}.json");
            }

            //EMS
            Parent.time = tempTime;
            ConvertToJsonQueue(Parent, $"0ems{strTime}.json");

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
                        if (fTemp[i]!=Math.Round(fTemp[i]))
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
        #endregion

        #region ===== 数据入列同步到本地目录下 =====
        private void ConvertToJsonQueue(object obj, string fileName)
        {
            string json = GetProperties(obj);
            _fileQueue.Enqueue(fileName, json);
        }

        private void ConvertToJsonQueue(string json, string fileName)
        {
            _fileQueue.Enqueue(fileName, json);
        }
        #endregion

        #region ===== 将故障数据整理存入文件 =====
        public void SaveFault2Cloud()
        {
            string strTime = DateTime.Now.ToString("yyyyMMddHHmmss"); ;
            ConvertToJsonQueue(Parent.Fault2Cloud, $"0fau{strTime}.json");
        }
        #endregion

        #region ===== 将充放默契电池数据整理存入文件 =====
        public void SaveCel2Cloud(string json)
        {
            string strTime = DateTime.Now.ToString("yyyyMMddHHmmss"); ;
            ConvertToJsonQueue(json, $"0cel{strTime}.json");
        }
        #endregion

    }

    public class FileQueue
    {
        public readonly string ReadyPath;
        public readonly string SendingPath;
        public readonly string FailedPath; // 仅保留目录，不再使用
        public readonly string TmpPath;
        public readonly string LogPath;

        // ===== 配额 =====
        public long ReadyMaxBytes = 1024L * 1024 * 1024; // 1GB

        // ===== rolling.log 配置 =====
        private const long MaxLogBytes = 10L * 1024 * 1024; // 10MB
        private const int MaxLogFiles = 3;

        private readonly object _lock = new object();
        private readonly object _logLock = new object();

        // key = yyyyMMdd|filename（字符串排序 = 时间排序）
        private readonly SortedSet<string> _readyIndex =
            new SortedSet<string>(StringComparer.Ordinal);

        private long _readyBytes;

        // =========================================================
        // ctor
        // =========================================================
        public FileQueue(string basePath)
        {
            ReadyPath   = Path.Combine(basePath, "ready");
            SendingPath = Path.Combine(basePath, "sending");
            TmpPath     = Path.Combine(basePath, "tmp");
            LogPath     = Path.Combine(basePath, "log");

            Directory.CreateDirectory(ReadyPath);
            Directory.CreateDirectory(SendingPath);
            Directory.CreateDirectory(TmpPath);
            Directory.CreateDirectory(LogPath);

            RecoverSendingFiles();
            BuildIndexOnce();
        }

        // =========================================================
        // 启动时一次性索引（允许）
        // =========================================================
        private void BuildIndexOnce()
        {
            foreach (var dir in Directory.EnumerateDirectories(ReadyPath))
            {
                var bucket = Path.GetFileName(dir);

                foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
                {
                    var name = Path.GetFileName(file);
                    var len = new FileInfo(file).Length;

                    _readyIndex.Add(bucket + "|" + name);
                    _readyBytes += len;
                }
            }
        }

        // =========================================================
        // 入队（原子写）
        // =========================================================
        public void Enqueue(string fileName, string json)
        {
            lock (_lock)
            {
                var bucket = DateTime.Now.ToString("yyyyMMdd");
                var readyBucket = Path.Combine(ReadyPath, bucket);
                Directory.CreateDirectory(readyBucket);

                var tmp = Path.Combine(TmpPath, fileName + ".tmp");
                var ready = Path.Combine(readyBucket, fileName);

                File.WriteAllText(tmp, json);
                File.Move(tmp, ready);

                var len = new FileInfo(ready).Length;

                _readyIndex.Add(bucket + "|" + fileName);
                _readyBytes += len;

                EnforceReadyQuota();
            }
        }

        // =========================================================
        // Ready → Sending（最新优先）
        // =========================================================
        public string[] DequeueReadyBatch(int batchSize)
        {
            lock (_lock)
            {
                var result = new List<string>(batchSize);

                while (result.Count < batchSize && _readyIndex.Count > 0)
                {
                    var key = _readyIndex.Max;
                    var (bucket, file) = SplitKey(key);

                    var src = Path.Combine(ReadyPath, bucket, file);
                    var dstBucket = Path.Combine(SendingPath, bucket);
                    var dst = Path.Combine(dstBucket, file);

                    Directory.CreateDirectory(dstBucket);

                    try
                    {
                        File.Move(src, dst);

                        var len = new FileInfo(dst).Length;
                        _readyIndex.Remove(key);
                        _readyBytes -= len;

                        result.Add(dst);
                    }
                    catch
                    {
                        // 异常文件直接丢弃索引，避免死循环
                        _readyIndex.Remove(key);
                    }
                }

                return result.ToArray();
            }
        }

        // =========================================================
        // 发送成功 → 删除
        // =========================================================
        public void MarkSuccess(string sendingFile)
        {
            try
            {
                if (File.Exists(sendingFile))
                    File.Delete(sendingFile);
            }
            catch
            {
                // 成功路径不做补救
            }
        }

        // =========================================================
        // 发送失败 → 写 rolling.log → 删除
        // =========================================================
        public void MarkFailed(string sendingFile, Exception ex = null)
        {
            try
            {
                WriteRollingLog(sendingFile, ex);
            }
            finally
            {
                try
                {
                    if (File.Exists(sendingFile))
                        File.Delete(sendingFile);
                }
                catch
                {
                    // 丢弃即可
                }
            }
        }

        // =========================================================
        // Ready 配额（最旧先删）
        // =========================================================
        private void EnforceReadyQuota()
        {
            while (_readyBytes > ReadyMaxBytes && _readyIndex.Count > 0)
            {
                DeleteOldestReady();
            }
        }

        private void DeleteOldestReady()
        {
            var key = _readyIndex.Min;
            var (bucket, file) = SplitKey(key);
            var path = Path.Combine(ReadyPath, bucket, file);

            try
            {
                var len = new FileInfo(path).Length;
                File.Delete(path);
                _readyBytes -= len;
            }
            catch { }

            _readyIndex.Remove(key);
        }

        // =========================================================
        // rolling.log（线程安全 + 自动滚动）
        // =========================================================
        private void WriteRollingLog(string file, Exception ex)
        {
            lock (_logLock)
            {
                var logFile = Path.Combine(LogPath, "send_fail.log");

                RotateLogIfNeeded(logFile);

                var sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]");
                sb.AppendLine($"File={file}");

                if (ex != null)
                {
                    sb.AppendLine($"Error={ex.GetType().Name}: {ex.Message}");
                }
                else
                {
                    sb.AppendLine("Error=Unknown");
                }

                sb.AppendLine();

                File.AppendAllText(logFile, sb.ToString(), Encoding.UTF8);
            }
        }

        private void RotateLogIfNeeded(string logFile)
        {
            if (File.Exists(logFile) && new FileInfo(logFile).Length < MaxLogBytes)
                return;

            // send_fail.log.2 ← send_fail.log.1 ← send_fail.log
            for (int i = MaxLogFiles - 1; i >= 1; i--)
            {
                var src = $"{logFile}.{i}";
                var dst = $"{logFile}.{i + 1}";

                if (File.Exists(dst))
                    File.Delete(dst);

                if (File.Exists(src))
                    File.Move(src, dst);
            }

            if (File.Exists(logFile))
                File.Move(logFile, $"{logFile}.1");
        }

        // =========================================================
        // 启动恢复：sending → ready
        // =========================================================
        private void RecoverSendingFiles()
        {
            foreach (var dir in Directory.EnumerateDirectories(SendingPath))
            {
                var bucket = Path.GetFileName(dir);
                var readyBucket = Path.Combine(ReadyPath, bucket);
                Directory.CreateDirectory(readyBucket);

                foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
                {
                    var name = Path.GetFileName(file);
                    File.Move(file, Path.Combine(readyBucket, name));
                }
            }
        }

        // =========================================================
        // utils
        // =========================================================
        private static (string bucket, string file) SplitKey(string key)
        {
            var i = key.IndexOf('|');
            return (key.Substring(0, i), key.Substring(i + 1));
        }
    }

    /*    public class FileQueue
        {
            public readonly string BasePath;
            public readonly string ReadyPath;
            public readonly string SendingPath;
            public readonly string FailedPath;
            public readonly string TmpPath;

            public long ReadyMaxBytes = 1024L * 1024 * 1024; // 1GB
            public long FailMaxBytes = 1024L * 1024 * 1024; // 1GB

            private static readonly ILog log = LogManager.GetLogger("FileQueue");

            private readonly object _lock = new object();

            public FileQueue(string basePath)
            {
                BasePath    = basePath;
                ReadyPath   = Path.Combine(basePath, "ready");
                SendingPath = Path.Combine(basePath, "sending");
                FailedPath  = Path.Combine(basePath, "failed");
                TmpPath     = Path.Combine(basePath, "tmp");

                Directory.CreateDirectory(ReadyPath);
                Directory.CreateDirectory(SendingPath);
                Directory.CreateDirectory(FailedPath);
                Directory.CreateDirectory(TmpPath);

                RecoverSendingFiles();
            }

            /// <summary>
            /// 程序启动恢复：sending → ready
            /// </summary>
            private void RecoverSendingFiles()
            {
                lock (_lock)
                {
                    foreach (var file in Directory.EnumerateFiles(SendingPath, "*.json"))
                    {
                        var dest = Path.Combine(ReadyPath, Path.GetFileName(file));
                        SafeMove(file, dest);
                    }
                }
            }

            /// <summary>
            /// 写入文件（原子写入）
            /// </summary>
            public void Enqueue(string fileName, string json)
            {
                lock (_lock)
                {
                    string tmp = Path.Combine(TmpPath, fileName + ".tmp");
                    string ready = Path.Combine(ReadyPath, fileName);

                    using (var fs = new FileStream(
                        tmp,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        4096,
                        FileOptions.WriteThrough))
                    using (var sw = new StreamWriter(fs))
                    {
                        sw.Write(json);
                        sw.Flush();
                        fs.Flush(true);
                    }

                    SafeMove(tmp, ready);

                    EnforceQuotaIfNeeded(ReadyPath, ReadyMaxBytes);
                }
            }

            /// <summary>
            /// 取 Ready 批次
            /// </summary>
            public string[] DequeueReadyBatch(int batchSize)
            {
                lock (_lock)
                {
                    var files = Directory.EnumerateFiles(ReadyPath, "*.json")
                        .OrderByDescending(Path.GetFileName) // 文件名即时间戳
                        .Take(batchSize)
                        .ToList();

                    var result = new List<string>(files.Count);

                    foreach (var file in files)
                    {
                        var dest = Path.Combine(SendingPath, Path.GetFileName(file));
                        SafeMove(file, dest);
                        result.Add(dest);
                    }

                    return result.ToArray();
                }
            }

            /// <summary>
            /// 取 Failed 批次
            /// </summary>
            public string[] DequeueFailBatch(int batchSize)
            {
                lock (_lock)
                {
                    var files = Directory.EnumerateFiles(FailedPath, "*.json")
                        .OrderByDescending(Path.GetFileName)
                        .Take(batchSize)
                        .ToList();

                    var result = new List<string>(files.Count);

                    foreach (var file in files)
                    {
                        var dest = Path.Combine(SendingPath, Path.GetFileName(file));
                        SafeMove(file, dest);
                        result.Add(dest);
                    }

                    return result.ToArray();
                }
            }

            /// <summary>
            /// 发送成功
            /// </summary>
            public void MarkSuccess(string sendingFile)
            {
                lock (_lock)
                {
                    SafeDelete(sendingFile);
                }
            }

            /// <summary>
            /// 发送失败
            /// </summary>
            public void MarkFailed(string sendingFile)
            {
                lock (_lock)
                {
                    var dest = Path.Combine(FailedPath, Path.GetFileName(sendingFile));
                    SafeMove(sendingFile, dest);
                    EnforceQuotaIfNeeded(FailedPath, FailMaxBytes);
                }
            }

            /// <summary>
            /// 仅在超限时才执行配额清理
            /// </summary>
            private void EnforceQuotaIfNeeded(string dir, long maxBytes)
            {
                long total = 0;

                foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
                {
                    total += new FileInfo(file).Length;
                    if (total > maxBytes)
                        break;
                }

                if (total <= maxBytes)
                    return;

                foreach (var file in Directory.EnumerateFiles(dir, "*.json")
                    .OrderBy(Path.GetFileName)) // 最旧先删
                {
                    var len = new FileInfo(file).Length;
                    SafeDelete(file);
                    total -= len;

                    if (total <= maxBytes)
                        break;
                }
            }

            #region ===== Safe IO =====

            private static void SafeMove(string src, string dest)
            {
                if (File.Exists(dest))
                    File.Delete(dest);

                File.Move(src, dest);
            }

            private static void SafeDelete(string path)
            {
                if (File.Exists(path))
                    File.Delete(path);
            }

            #endregion
        } */

}
