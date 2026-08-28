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
using System.Globalization;
using System.Text.RegularExpressions;
using MySqlX.XDevAPI.Common;

namespace EMS
{
    public class BalaTableResult
    {
        public bool Result { get; set; }
        public string BalaStartMsg { get; set; }
        public List<double> BalaList { get; set; } = new List<double>();
    }

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
        public string BalaControlTopic;
        //public string BalaTacticTopic;
        public string OtaTopic;
        public string CloudConsoleTopic;

        public string PriceTopic_new;
        public string TacticTopic_new;
        public string EMSLimitTopic_new;
        public string AIOTTableTopic_new;
        public string BalaControlTopic_new;
        //public string BalaTacticTopic_new;
        public string CloudConsoleTopic_new;
        #endregion

        #region ===== 文件上传 =====

        public string DataPath;
        public string Filters = "*.json";
        public int ReadyBatchSize = 100;
        public int FailBatchSize = 10;
        private FileQueue _fileQueue;

        #endregion

        private long _faultSeq = 0;

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
                string responseTopic;

                if (topic == TacticTopic + "request" || topic == TacticTopic_new + "request")
                {
                    result = GetServerTactics(message);
                    responseTopic = topic == TacticTopic_new + "request" ? TacticTopic_new : TacticTopic;
                    PublishResult(responseTopic, strID, result);
                }
                else if (topic == PriceTopic + "request" || topic == PriceTopic_new + "request")
                {
                    result = GetServerEPrices(message);
                    responseTopic = topic == PriceTopic_new + "request" ? PriceTopic_new : PriceTopic;
                    PublishResult(responseTopic, strID, result);
                }
                else if (topic == EMSLimitTopic + "request" || topic == EMSLimitTopic_new + "request")
                {
                    result = GetServerEMSLimit(message);
                    responseTopic = topic == EMSLimitTopic_new + "request" ? EMSLimitTopic_new : EMSLimitTopic;
                    PublishResult(responseTopic, strID, result);
                }
                else if (topic == AIOTTableTopic + "request" || topic == AIOTTableTopic_new + "request")
                {
                    strID = GetAiotTable(message);
                    responseTopic = topic == AIOTTableTopic_new + "request" ? AIOTTableTopic_new : AIOTTableTopic;
                    PublishResult(responseTopic, strID, true);
                }
                else if (topic == BalaControlTopic + "request" || topic == BalaControlTopic_new + "request")
                {
                    BalaTableResult balaTableResult = GetBalaControl(message);
                    responseTopic = topic == BalaControlTopic_new + "request" ? BalaControlTopic_new : BalaControlTopic;
                    PublishBalaTableResult(responseTopic, strID, balaTableResult);
                }
                else if (topic == CloudConsoleTopic + "request" || topic == CloudConsoleTopic_new + "request")
                {
                    result = GetCloudConsole(message);
                    responseTopic = topic == CloudConsoleTopic_new + "request" ? CloudConsoleTopic_new : CloudConsoleTopic;
                    PublishResult(responseTopic, strID, true);
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
                _mqtt.Publish(baseTopic + "response/" + id, payload, true, PublishQos.AtLeastOnce);
            }
        }

        private void PublishBalaTableResult(string baseTopic, string id, BalaTableResult balaTableResult)
        {
            JObject payload = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["result"] = balaTableResult.Result,
                ["id"] = id,
                ["msg"] = new JObject
                {
                    ["BalaStart"] = balaTableResult.BalaStartMsg,
                    ["BalaList"] = JArray.FromObject(balaTableResult.BalaList)
                }
            };

            lock (_lockMqtt)
            {
                _mqtt.Publish(baseTopic + "response/" + id, payload.ToString(Newtonsoft.Json.Formatting.None), true, PublishQos.AtLeastOnce);
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
                    //string topic = Path.GetFileName(file).Substring(1, 3);
                    string topic = Path.GetFileName(file).Substring(1, 3) + "/" + frmMain.Selffrm.AllEquipment.full_iot_code;

                    lock (_lockMqtt)
                    {
                        _mqtt.Publish(topic, content, false, PublishQos.AtLeastOnce);
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
            _mqtt.Subscribe(BalaControlTopic + "request");
            //_mqtt.Subscribe(BalaTacticTopic + "request");
            //_mqtt.Subscribe(OtaTopic + "request");
            _mqtt.Subscribe(CloudConsoleTopic + "request");

            _mqtt.Subscribe(PriceTopic_new + "request");
            _mqtt.Subscribe(TacticTopic_new + "request");
            _mqtt.Subscribe(EMSLimitTopic_new + "request");
            _mqtt.Subscribe(AIOTTableTopic_new + "request");
            _mqtt.Subscribe(BalaControlTopic_new + "request");
            //_mqtt.Subscribe(BalaTacticTopic_new + "request");
            _mqtt.Subscribe(CloudConsoleTopic_new + "request");
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

                BalaControlTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/bms/BalaControl/";
                //BalaTacticTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/ems/BalaStrategy/";
                //OtaTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/aiot/ota/";
                CloudConsoleTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/ems/CloudConsole/";

                //新版topic
                PriceTopic_new = "/rpc/" + frmMain.Selffrm.AllEquipment.full_iot_code + "/meter/price/";
                TacticTopic_new = "/rpc/" + frmMain.Selffrm.AllEquipment.full_iot_code + "/ems/strategy/";
                EMSLimitTopic_new = "/rpc/" + frmMain.Selffrm.AllEquipment.full_iot_code + "/ems/limit/";
                AIOTTableTopic_new = "/rpc/ctl" + frmMain.Selffrm.AllEquipment.full_iot_code + "/aiot/table/";
                BalaControlTopic_new = "/rpc/" + frmMain.Selffrm.AllEquipment.full_iot_code + "/bms/BalaControl/";
                //BalaTacticTopic_new = "/rpc/" + frmMain.Selffrm.AllEquipment.full_iot_code + "/ems/BalaStrategy/";
                CloudConsoleTopic_new = "/rpc/" + frmMain.Selffrm.AllEquipment.full_iot_code + "/ems/CloudConsole/";
            }
            catch (Exception ex)
            {
                log.Error("IniClound: " + ex.Message);
            }
        }

        #endregion

        #region ===== 订阅到消息后的回调函数实现方法 =====

        public bool GetCloudConsole(string astrData)
        {
            bool result = true;

            try
            {
                if (astrData == "")
                    return false;

                JObject jsonObject = JObject.Parse(astrData);
                string strTopic = jsonObject["method"].ToString();
                if (strTopic != "ems/CloudConsole")
                {
                    return false;
                }
                else
                {
                    if (jsonObject["params"] != null)
                    {
                        var parameters = jsonObject["params"];
                        if (parameters["CloseSystemLock"] != null && int.Parse(parameters["CloseSystemLock"].ToString()) == 1)
                        {
                            frmSet.CloseSystemLock();
                        }
                    }
                    else
                    {
                        result = false;
                    }
                }
            }
            catch (Exception ex) {
                log.Error("GetCloudConsoleTopic: " + ex.Message);
            }

            return result;
        }

        public BalaTableResult GetBalaControl(string astrData)
        {
            BalaTableResult result = new BalaTableResult { Result = true };
            try
            {
                if (astrData == "")
                    return result;
                JObject jsonObject = JObject.Parse(astrData);
                string strTopic = jsonObject["method"].ToString();
                if (strTopic != "bms/BalaControl") {
                    result.Result = false;
                    result.BalaStartMsg = "method属性内容错误";
                    return result;
                }

                //9.11
                /*                int iBalaStart = int.Parse(jsonObject["params"]["table"]["BalaStart"].ToString());
                                frmSet.cloudLimits.OpenBala = iBalaStart;
                                frmSet.Set_Cloudlimits();*/
                //frmMain.Selffrm.AllEquipment.BMS.FunctionLevel = 1;

                if (jsonObject["params"] != null)
                {
                    var parameters = jsonObject["params"];
                    var table = parameters["table"];
                    if (table == null)
                        return result;

                    if (table["BalaStart"] != null)
                    {
                        int iBalaStart = int.Parse(table["BalaStart"].ToString());

                        if (iBalaStart == 1 && frmMain.Selffrm.AllEquipment.BMS.FunctionLevel == 0)
                        {
                            result.Result = false;
                            result.BalaStartMsg = "BMS版本不支持均衡";
                        }
                        else
                        {
                            frmSet.cloudLimits.OpenBala = iBalaStart;
                            frmSet.Set_Cloudlimits();
                            result.BalaStartMsg = iBalaStart == 1 ? "BMS开启均衡" : "BMS关闭均衡";
                        }
                    }

                    if (table["BalaList"] != null)
                    {
                        JToken listToken = table["BalaList"];
                        if (listToken.Type != JTokenType.Array)
                        {
                            throw new FormatException("params.table.BalaList 必须是 JSON 数组。");
                        }

                        List<double> parsedCellIds = new List<double>();
                        foreach (JToken cellId in listToken.Children())
                        {
                            double parsedCellId;
                            if (!double.TryParse(cellId.ToString(), NumberStyles.Float,
                                CultureInfo.InvariantCulture, out parsedCellId))
                            {
                                throw new FormatException("单体 ID 无效: " + cellId);
                            }
                            parsedCellIds.Add(parsedCellId);
                        }

                        // BalaCell.txt 与本地均衡策略读取的文件保持一致，每行一个单体 ID。
                        File.WriteAllText(frmSet.BalaPath, string.Empty);
                        using (StreamWriter writer = new StreamWriter(frmSet.BalaPath))
                        {
                            foreach (double cellId in parsedCellIds)
                            {
                                writer.WriteLine(cellId.ToString(CultureInfo.InvariantCulture));
                            }
                        }

                        string[] savedCellIdLines = File.ReadAllLines(frmSet.BalaPath);
                        List<double> savedCellIds = new List<double>();
                        foreach (string savedCellIdLine in savedCellIdLines)
                        {
                            double savedCellId;
                            if (!double.TryParse(savedCellIdLine, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out savedCellId))
                            {
                                throw new FormatException("BalaCell.txt 中的单体 ID 无效: " + savedCellIdLine);
                            }
                            savedCellIds.Add(savedCellId);
                        }

                        result.BalaList = savedCellIds;
                        if (!parsedCellIds.SequenceEqual(savedCellIds))
                        {
                            result.Result = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Result = false;
                log.Error("GetBalaTable: " + ex.Message);
            }
            return result;
        }

        public bool GetServerTactics(string astrData)
        {
            bool result = true;

            try
            {
                /* ----------------------------------------------------
                 * 0. 权限与配置校验
                 * ---------------------------------------------------- */
                if (frmSet.config == null)
                {
                    log.Error("frmSet.config 未初始化成功，GetServerTactics 无法判断");
                    return false;
                }

                if (frmSet.config.UseYunTactics == 0 || frmSet.config.IsMaster == 0)
                    return false;

                if (string.IsNullOrWhiteSpace(astrData))
                    return false;

                JObject jsonObject = JObject.Parse(astrData);

                if (jsonObject["method"]?.ToString() != "ems/strategy")
                    return false;

                var paramsObj = jsonObject["params"];
                if (paramsObj == null || paramsObj["strategy"] == null)
                    return false;

                string defaultDate = paramsObj["date"]?.ToString();
                JArray strategyArray = (JArray)paramsObj["strategy"];
                if (strategyArray.Count == 0)
                    return true;

                /* ----------------------------------------------------
                 * 1. 先收集所有涉及到的 strategyDate
                 * ---------------------------------------------------- */
                HashSet<string> strategyDates = new HashSet<string>();

                foreach (var item in strategyArray)
                {
                    string strategyDate = item["strategyDate"]?.ToString();
                    if (string.IsNullOrEmpty(strategyDate))
                        strategyDate = defaultDate;

                    if (!string.IsNullOrEmpty(strategyDate))
                        strategyDates.Add(strategyDate);
                }

                /* ----------------------------------------------------
                 * 2. 统一删除数据库中对应日期的策略
                 * ---------------------------------------------------- */
                foreach (string strategyDate in strategyDates)
                {
                    string deleteSql = "DELETE FROM tactics WHERE rTime = @rTime";
                    var deleteParams = new Dictionary<string, object>
                    {
                        { "@rTime", strategyDate }
                    };

                    DBConnection.ExecSQLWithParams(deleteSql, deleteParams);
                    // 不依赖返回值，确保每个日期只删一次
                }

                /* ----------------------------------------------------
                 * 3. 插入新的策略数据
                 * ---------------------------------------------------- */
                foreach (var item in strategyArray)
                {
                    string start = item["start"]?.ToString();
                    string end = item["end"]?.ToString();

                    string charge = "";
                    if (item["charge"] != null)
                        charge = bool.Parse(item["charge"].ToString()) ? "充电" : "放电";

                    string mode = "";
                    if (item["mode"] != null)
                    {
                        int modeValue = int.Parse(item["mode"].ToString());
                        if (modeValue == 3)
                            mode = "恒功率";
                        else if (modeValue == 5)
                            mode = "自适应需量";
                    }

                    string value = item["value"]?.ToString();

                    string strategyDate = item["strategyDate"]?.ToString();
                    if (string.IsNullOrEmpty(strategyDate))
                        strategyDate = defaultDate;

                    // 严格校验，避免插入“空策略”
                    if (string.IsNullOrEmpty(start)
                        || string.IsNullOrEmpty(end)
                        || string.IsNullOrEmpty(charge)
                        || string.IsNullOrEmpty(mode)
                        || string.IsNullOrEmpty(value)
                        || string.IsNullOrEmpty(strategyDate))
                    {
                        log.Warn("策略字段不完整，已跳过一条 strategy");
                        continue;
                    }

                    string insertSql =
                        "INSERT INTO tactics (startTime, endTime, tType, PCSType, waValue, rTime) " +
                        "VALUES (@start, @end, @charge, @mode, @value, @rTime)";

                    var insertParams = new Dictionary<string, object>
                    {
                        { "@start", start },
                        { "@end", end },
                        { "@charge", charge },
                        { "@mode", mode },
                        { "@value", value },
                        { "@rTime", strategyDate }
                    };

                    if (DBConnection.ExecSQLWithParams(insertSql, insertParams) <= 0)
                    {
                        result = false;
                        log.Error($"插入策略失败：date={strategyDate}, {start}-{end}");
                    }
                }

                /* ----------------------------------------------------
                 * 4. 刷新内存策略
                 * ---------------------------------------------------- */
                if (!frmMain.TacticsList.LoadFromMySQL(0))
                {
                    result = false;
                    log.Error("策略加载到内存失败");
                }
                else
                {
                    frmMain.TacticsList.ActiveIndex = -1;
                }
            }
            catch (Exception ex)
            {
                result = false;
                log.Error("GetServerTactics Exception: " + ex);
            }

            return result;
        }
        /*        public bool GetServerTactics(string astrData)
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
                }*/

        /*        public bool GetServerEPrices(string astrData, bool aIsFileData = false)
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
                }*/

        public bool GetServerEPrices(string astrData, bool aIsFileData = false)
        {
            bool result = true;

            try
            {
                if (string.IsNullOrWhiteSpace(astrData))
                    return false;

                JObject jsonObject = JObject.Parse(astrData);

                // 1. 校验 method
                if (jsonObject["method"]?.ToString() != "meter/price")
                    return false;

                var paramsObj = jsonObject["params"];
                if (paramsObj == null || paramsObj["price"] == null)
                    return false;

                string defaultDate = paramsObj["date"]?.ToString();
                JArray priceArray = (JArray)paramsObj["price"];
                if (priceArray.Count == 0)
                    return true;

                /* ----------------------------------------------------
                 * 2. 先收集所有涉及到的策略日期
                 * ---------------------------------------------------- */
                HashSet<string> priceDates = new HashSet<string>();

                foreach (var item in priceArray)
                {
                    string priceDate = item["pricDate"]?.ToString();
                    if (string.IsNullOrEmpty(priceDate))
                        priceDate = defaultDate;

                    if (!string.IsNullOrEmpty(priceDate))
                        priceDates.Add(priceDate);
                }

                /* ----------------------------------------------------
                 * 3. 先删除数据库中与这些日期重叠的数据
                 * ---------------------------------------------------- */
                foreach (string priceDate in priceDates)
                {
                    string deleteSql = "DELETE FROM electrovalence WHERE rTime = @rTime";
                    var deleteParams = new Dictionary<string, object>
                    {
                        { "@rTime", priceDate }
                    };

                    DBConnection.ExecSQLWithParams(deleteSql, deleteParams);
                    // 不依赖返回值，不管删没删到，都视为成功执行过
                }

                /* ----------------------------------------------------
                 * 4. 再执行插入
                 * ---------------------------------------------------- */
                foreach (var item in priceArray)
                {
                    int section = -1;
                    string start = item["start"]?.ToString();

                    if (item["range"] != null)
                    {
                        section = int.Parse(item["range"].ToString());

                        if (item["buyPrice"] != null)
                        {
                            frmSet.Prices[0, section] =
                                (int)Math.Round(double.Parse(item["buyPrice"].ToString()) * 100);
                        }

                        if (item["sellPrice"] != null)
                        {
                            frmSet.Prices[1, section] =
                                (int)Math.Round(double.Parse(item["sellPrice"].ToString()) * 100);
                        }
                    }

                    string priceDate = item["pricDate"]?.ToString();
                    if (string.IsNullOrEmpty(priceDate))
                        priceDate = defaultDate;

                    if (string.IsNullOrEmpty(start) || section == -1 || string.IsNullOrEmpty(priceDate))
                        continue;

                    string insertSql =
                        "INSERT INTO electrovalence (startTime, eName, section, rTime) " +
                        "VALUES (@startTime, @eName, @section, @rTime)";

                    var insertParams = new Dictionary<string, object>
                    {
                        { "@startTime", start },
                        { "@eName", section.ToString() },
                        { "@section", "0" },
                        { "@rTime", priceDate }
                    };

                    if (DBConnection.ExecSQLWithParams(insertSql, insertParams) <= 0)
                    {
                        result = false;
                        log.Error($"插入费率失败：date={priceDate}, start={start}, section={section}");
                    }
                }

                // 写入电表状态
                frmMain.Selffrm.AllEquipment.LoadJFPGSuccess = false;
            }
            catch (Exception ex)
            {
                result = false;
                log.Error("GetServerEPrices Exception: " + ex);
            }

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
                        if (parameters["OpenWarning"] != null)
                        {
                            frmSet.cloudLimits.OpenWarning = int.Parse(parameters["OpenWarning"].ToString());
                            frmSet.cloudLimits.ModifiedFields.Add("OpenWarning");
                            if (frmSet.cloudLimits.OpenWarning == 0) {
                                // 设置关闭故障指示灯蜂鸣器功能，先确保关闭当前正在执行的故障指示灯蜂鸣器
                                frmSet.ErrorGPIO(0);
                            }
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
/*        public void SaveFault2Cloud()
        {
            string strTime = DateTime.Now.ToString("yyyyMMddHHmmss"); ;
            ConvertToJsonQueue(Parent.Fault2Cloud, $"0fau{strTime}.json");
        }*/

        public void SaveFault2Cloud()
        {
            var now = DateTime.Now;
            var ts = now.ToString("yyyyMMddHHmmssfff"); // 毫秒
            var seq = Interlocked.Increment(ref _faultSeq) & 0xFFFF;

            ConvertToJsonQueue(
                Parent.Fault2Cloud,
                $"0fau{ts}_{seq}.json"
            );
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
        public readonly string TmpPath;
        public readonly string LogPath;

        // ===== 配额 =====
        public long ReadyMaxBytes = 1024L * 1024 * 1024; // 1GB

        // ===== rolling.log 配置 =====
        private const long MaxLogBytes = 10L * 1024 * 1024; // 10MB
        private const int MaxLogFiles = 3;

        private readonly object _lock = new object();
        private readonly object _logLock = new object();

        /*
         * key = <timestamp>|<bucket>|<filename>
         * 排序只看 timestamp（字符串排序 = 时间排序）
         */
        private readonly SortedSet<string> _readyIndex =
            new SortedSet<string>(StringComparer.Ordinal);

        private long _readyBytes;

        private static readonly ILog log = LogManager.GetLogger("FileQueue");

        // =========================================================
        // ctor
        // =========================================================
        public FileQueue(string basePath)
        {
            try
            {
                ReadyPath   = Path.Combine(basePath, "ready");
                SendingPath = Path.Combine(basePath, "sending");
                TmpPath     = Path.Combine(basePath, "tmp");
                LogPath     = Path.Combine(basePath, "log");

                Directory.CreateDirectory(ReadyPath);
                Directory.CreateDirectory(SendingPath);
                Directory.CreateDirectory(TmpPath);
                Directory.CreateDirectory(LogPath);

                CleanupTmpOnStartup();
                RecoverSendingFiles();
                BuildIndexOnce();
            }
            catch (Exception ex) {
                log.Error("Init FileQueue: " + ex);
            }
        }

        // =========================================================
        // 启动清理 tmp（孤儿文件）
        // =========================================================
        private void CleanupTmpOnStartup()
        {
            foreach (var f in Directory.EnumerateFiles(TmpPath, "*.tmp"))
            {
                TryDelete(f);
            }
        }

        // =========================================================
        // 启动索引
        // =========================================================
        private void BuildIndexOnce()
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(ReadyPath))
                {
                    var bucket = Path.GetFileName(dir);

                    foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
                    {
                        var name = Path.GetFileName(file);
                        var len = new FileInfo(file).Length;

                        var ts = ExtractTimestamp(name);

                        _readyIndex.Add($"{ts}|{bucket}|{name}");
                        _readyBytes += len;
                    }
                }
            }
            catch (Exception ex) {
                log.Error("BuildIndexOnce: " + ex);
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

                var tmp = Path.Combine(
                    TmpPath,
                    $"{fileName}.{Guid.NewGuid():N}.tmp"
                );

                var ready = Path.Combine(readyBucket, fileName);

                try
                {
                    File.WriteAllText(tmp, json, Encoding.UTF8);

                    if (File.Exists(ready))
                        throw new IOException($"Ready file exists: {ready}");

                    File.Move(tmp, ready);

                    var len = new FileInfo(ready).Length;
                    var ts = ExtractTimestamp(fileName);

                    _readyIndex.Add($"{ts}|{bucket}|{fileName}");
                    _readyBytes += len;

                    EnforceReadyQuota();
                }
                catch (Exception ex)
                {
                    WriteEnqueueError(tmp, ready, ex);
                    TryDelete(tmp);
                }
            }
        }

        // =========================================================
        // Ready → Sending（时间最新优先）
        // =========================================================
        public string[] DequeueReadyBatch(int batchSize)
        {
            lock (_lock)
            {
                var result = new List<string>(batchSize);

                while (result.Count < batchSize && _readyIndex.Count > 0)
                {
                    var key = _readyIndex.Max; // 时间戳最大的
                    var (bucket, file) = SplitKey(key);

                    // 验证 SplitKey 结果
                    if (bucket == null || file == null)
                    {
                        log.Warn($"DequeueReadyBatch: 跳过无效 key={key}");
                        _readyIndex.Remove(key);
                        continue;
                    }

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
                    catch (Exception ex)
                    {
                        _readyIndex.Remove(key);
                        WriteRollingLog(src, ex);
                    }
                }

                return result.ToArray();
            }
        }

        // =========================================================
        // 发送成功
        // =========================================================
        public void MarkSuccess(string sendingFile)
        {
            TryDelete(sendingFile);
        }

        // =========================================================
        // 发送失败
        // =========================================================
        public void MarkFailed(string sendingFile, Exception ex = null)
        {
            WriteRollingLog(sendingFile, ex);
            TryDelete(sendingFile);
        }

        // =========================================================
        // Ready 配额（最旧时间先删）
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
            var key = _readyIndex.Min; // 时间戳最小
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
        // rolling.log
        // =========================================================
        private void WriteRollingLog(string file, Exception ex)
        {
            lock (_logLock)
            {
                try
                {
                    var logFile = Path.Combine(LogPath, "send_fail.log");
                    RotateLogIfNeeded(logFile);

                    var sb = new StringBuilder();
                    sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]");
                    sb.AppendLine($"File={file}");
                    sb.AppendLine($"Error={ex?.GetType().Name}: {ex?.Message}");
                    sb.AppendLine();

                    File.AppendAllText(logFile, sb.ToString(), Encoding.UTF8);
                }
                catch (Exception e) {
                    log.Error("WriteRollingLog: " + e);
                }
            }
        }

        private void WriteEnqueueError(string tmp, string ready, Exception ex)
        {
            lock (_logLock)
            {
                try
                {
                    var logFile = Path.Combine(LogPath, "enqueue_error.log");

                    var sb = new StringBuilder();
                    sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]");
                    sb.AppendLine($"Tmp={tmp}");
                    sb.AppendLine($"Ready={ready}");
                    sb.AppendLine($"Error={ex.GetType().Name}: {ex.Message}");
                    sb.AppendLine();

                    File.AppendAllText(logFile, sb.ToString(), Encoding.UTF8);
                }
                catch (Exception e)
                {
                    log.Error("WriteEnqueueError: " + e);
                }
            }
        }

        private void RotateLogIfNeeded(string logFile)
        {
            if (File.Exists(logFile) && new FileInfo(logFile).Length < MaxLogBytes)
                return;

            for (int i = MaxLogFiles - 1; i >= 1; i--)
            {
                var src = $"{logFile}.{i}";
                var dst = $"{logFile}.{i + 1}";

                if (File.Exists(dst)) File.Delete(dst);
                if (File.Exists(src)) File.Move(src, dst);
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
                    File.Move(file, Path.Combine(readyBucket, Path.GetFileName(file)));
                }
            }
        }

        // =========================================================
        // utils
        // =========================================================

        // 从文件名中提取时间戳
        private static string ExtractTimestamp(string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);

            // 先匹配 17 位：yyyyMMddHHmmssfff
            var match17 = Regex.Match(name, @"\d{17}");
            if (match17.Success)
                return match17.Value;

            // 再匹配 14 位：yyyyMMddHHmmss
            var match14 = Regex.Match(name, @"\d{14}");
            if (match14.Success)
                return match14.Value;

            throw new FormatException($"Invalid timestamp in filename: {fileName}");
        }

        private static void TryDelete(string file)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch (Exception ex) {
                log.Error("TryDelete: " + ex);
            }
        }

        private static (string bucket, string file) SplitKey(string key)
        {
            try
            {
                // key = ts|bucket|file
                var i1 = key.IndexOf('|');
                var i2 = key.IndexOf('|', i1 + 1);

                var bucket = key.Substring(i1 + 1, i2 - i1 - 1);
                var file = key.Substring(i2 + 1);

                return (bucket, file);
            }
            catch (Exception ex) {
                log.Error("SplitKey异常: " + ex);
                return (null, null);
            }
        }
    }
}
