using M2Mqtt;
using M2Mqtt.Messages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Modbus;
using System.Threading;
using log4net;
using System.Diagnostics;
//2.21
using System.Runtime.InteropServices;
using MySqlX.XDevAPI.Common;
using Mysqlx.Session;

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
        public MqttClient mqttClient { get; set; }
        public bool FirstRun = true;
        public bool receivedHeartbeatResponse = false;
        public bool SendAgain = true;
        public string HeartbeatID;

        private static System.Threading.Timer Publish_Timer;
        //计时器
        public TimeMeasurement Clock_Watch = new TimeMeasurement();

        private static ILog log = LogManager.GetLogger("CloudClass");

        public CloudClass()
        {
          
            //mqttConnect(); 
        }
        static ulong SetCpuID(int lpIdx)
        {
            ulong cpuLogicalProcessorId = 0;
            if (lpIdx < 0 || lpIdx >= System.Environment.ProcessorCount)
            {
                lpIdx = 0;
            }
            cpuLogicalProcessorId |= 1UL << lpIdx;
            return cpuLogicalProcessorId;
        }
        //2.21
        public void AutoCloud()
        {
            try
            {
                //实例化等待连接的线程
                Thread mqttThread = new Thread(ListenCloud);
                mqttThread.IsBackground = true;
                ulong LpId = SetCpuID(1);
                SetThreadAffinityMask(GetCurrentThread(), new UIntPtr(LpId));
                mqttThread.Start();
                //8.4
                mqttThread.Priority = ThreadPriority.Highest;
            }
            catch
            {

            }
        }
        public void ListenCloud()
        {
            //log.Error("ListenCloud");
            mqttClient.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
            while (true) { }
            
        }


        public void InitializePublish_Timer()
        {
            Publish_Timer = new System.Threading.Timer(Publish_TimerCallback, null, 0, 10000);
        }
        private void Publish_TimerCallback(Object state)
        {
            //数据上云
            log.Error("数据上云");
            
        }





        public void IniClound()
        {
            PriceTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/meter/price/";
            TacticTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/ems/strategy/";//request
            EMSLimitTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/ems/limit/";
            //AIOTTableTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/ctl/table/";
            string strID = frmSet.SysID;
            if (strID.Length >= 7)
                strID = strID.Substring(strID.Length - 7, 7);
            AIOTTableTopic = "/rpc/ctl" + strID + "/aiot/table/";
            BalaTableTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/aiot/table/";
            BalaTacticTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/ems/BalaStrategy/";
            HeartbeatTopic = "/rpc/" + frmMain.Selffrm.AllEquipment.iot_code + "/Heartbeat";
        }


        public MqttClient CreateClient()
        {
            INIFile ConfigINI = new INIFile();
            string strSysPath = Convert.ToString(System.AppDomain.CurrentDomain.BaseDirectory);
            string INIPath = strSysPath + "Config.ini";
            String iotcode = ConfigINI.INIRead("System Set", "SysID", "202300001", INIPath).Trim();

            EMQX_CLIENT_ID = iotcode;
            try
            {
                // 断开并释放现有的 MQTT 客户端
                if (mqttClient != null)
                {
                    if (mqttClient.IsConnected)
                    {
                        mqttClient.Disconnect();
                    }
                    mqttClient = null;
                }
                //建立连接
                mqttClient = new MqttClient(EMQX_BROKER_IP, EMQX_BROKER_PORT, true, null, null, MqttSslProtocols.TLSv1_2);            
                mqttClient.Connect(EMQX_CLIENT_ID,
                                            "aiot",// user,
                                            "Lab123123123",//pwd,
                                            true, // cleanSession
                                            60); // keepAlivePeriod 
                connectflag = 1;
                mqttClient.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;
                return mqttClient;
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                //MessageBox.Show(ex.Message);
                //meterg,meterp,metera,air  meter
                //ename==iot_code,eTime=time
            }
            return null;
        }

        // 建立MQTT连接
        public void mqttConnect()
        {
            try
            {
                CreateClient();
                ListenTopic(PriceTopic + "request");
                ListenTopic(TacticTopic + "request");
                ListenTopic(EMSLimitTopic + "request");
                ListenTopic(AIOTTableTopic + "request");
                ListenTopic(BalaTableTopic + "request");
                ListenTopic(HeartbeatTopic);
                
                log.Error(HeartbeatTopic);
                FirstRun = true;
            }
            catch {
               // mqttClient.IsConnected = false;
            }
        }


        public void mqttReconnect()
        {
            try
            {
                //停止定时器
                if (Publish_Timer != null)
                {
                    Publish_Timer.Change(Timeout.Infinite, Timeout.Infinite);
                    Publish_Timer.Dispose();
                    Publish_Timer = null;

                    log.Error("停止定时器");
                }

                CreateClient();
                ListenTopic(PriceTopic + "request");
                ListenTopic(TacticTopic + "request");
                ListenTopic(EMSLimitTopic + "request");
                ListenTopic(AIOTTableTopic + "request");
                ListenTopic(BalaTableTopic + "request");
                ListenTopic(HeartbeatTopic);

                // 重新启动定时器
                log.Error("重新启动定时器");
                InitializePublish_Timer();
                SendAgain = true;
            }
            catch (Exception ex)
            {

            }

        }



        public void CheckConnect()
        {
/*            if (connectflag == 1)
            {
                //mqttClient.Disconnect();
                if (mqttClient != null)
                {
                    if (!mqttClient.IsConnected)
                    {
                        mqttReconnect();
                    }
                }

            }*/
        }

        // 发送心跳报文的方法
        public void SendHeartbeat()
        {
            if (mqttClient != null)
            {
/*                if (Clock_Watch.MeasureIntervalInSeconds() > 13)
                {
                    //检测超时
                    mqttReconnect();
                }*/
                if (SendAgain)
                {
                    HeartbeatID = Guid.NewGuid().ToString();
                    string heartbeatMessage = $"{{\"HeartBeatID\":\"{HeartbeatID}\"}}";
                    mqttClient.Publish(HeartbeatTopic, System.Text.Encoding.UTF8.GetBytes(heartbeatMessage),
                        MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);

                    SendAgain = false;
                }
                else 
                {
                    mqttReconnect();
                }
            }
        }


        /// <summary>
        /// 给一个topic写数据
        /// </summary>
        /// <param name="currentTopic"></param>
        /// <param name="content"></param>
        public void Write2Topic(string currentTopic, string content)
        {
            if (mqttClient != null && !string.IsNullOrEmpty(currentTopic) && !string.IsNullOrEmpty(content))
            {
                mqttClient.Publish(currentTopic, System.Text.Encoding.UTF8.GetBytes(content),
                    MqttMsgBase.QOS_LEVEL_GRANTED_FAILURE, true);//qos                                                              
            }
        }

        /// <summary>
        /// 设置监听一个topic
        /// </summary>
        /// <param name="aTopic"></param>
        public void ListenTopic(string aTopic)
        {
            if (mqttClient != null && !string.IsNullOrEmpty(aTopic))
            {
                mqttClient.Subscribe(new string[] { aTopic },
                new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });//QOS_LEVEL_EXACTLY_ONCE
            }
        }

        //这段定义了收到消息之后做什么事情
        private void Client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            bool Result = false;
            string strResponse = "{ \"jsonrpc\":\"2.0\", \"result\":true, \"id\":\"";
            string ErrorstrResponse = "{ \"jsonrpc\":\"2.0\", \"result\":false, \"id\":\"";
            string topic = e.Topic.ToString();
            string message = System.Text.Encoding.Default.GetString(e.Message);
            string strID = "";
            //同时订阅两个或者以上主题时，分类收集收到的信息

            if (topic == TacticTopic + "request")
            {
                Result = GetServerTactics(message);
                if (Result)
                {
                    mqttClient.Publish(TacticTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(strResponse + strID + "\"}"),
                        MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
                }
                else
                {
                    mqttClient.Publish(TacticTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(ErrorstrResponse + strID + "\"}"),
                        MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
                }
            }
            else if (topic == PriceTopic + "request")
            {
                Result = GetServerEPrices(message);
                if (Result)
                {
                    mqttClient.Publish(PriceTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(strResponse + strID + "\"}"),
                     MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
                }
            }
            else if (topic == EMSLimitTopic + "request")
            {
                strID = GetServerEMSLimit(message);
                mqttClient.Publish(EMSLimitTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(strResponse + strID + "\"}"),
                     MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
            }
            else if (topic == AIOTTableTopic + "request")
            {
                strID = GetAiotTable(message);
                mqttClient.Publish(AIOTTableTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(strResponse + strID + "\"}"),
                     MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
            }
            else if (topic == BalaTableTopic + "request")
            {
                strID = GetBalaTable(message);
                mqttClient.Publish(BalaTableTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(strResponse + strID + "\"}"),
                     MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
            }
            else if (topic == HeartbeatTopic)
            {
                GetHeartbeat(message);
            }


/*            else if (topic == BalaTacticTopic)
            {
                strID = GetServerBalaTactics(message);
                mqttClient.Publish(BalaTableTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(strResponse + strID + "\"}"),
                    MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
            }*/
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
        public static void ConvertToJson(object aObj, string aDirection, string aSavePath)
        {
            try
            {
                // 创建一个 StreamReader 的实例来读取文件 
                // using (StreamReader sr = new StreamReader("c:/jamaica.txt"))  while ((line = sr.ReadLine()) != null)
                if (!Directory.Exists(aDirection))
                    Directory.CreateDirectory(aDirection);
                using (StreamWriter sw = new StreamWriter(aDirection + "\\" + aSavePath))
                {
                    sw.WriteLine(GetProperties(aObj));
                    //sw.Close();
                    //sw.Dispose();
                }
            }
            catch (Exception e)
            {
                // 向用户显示出错消息
                Console.WriteLine("The file could not be read:" + e.Message);
            }

            //string str = JsonConvert.SerializeObject(obj);

            ////json格式化
            //JsonSerializer jsonSerializer = new JsonSerializer();
            //TextReader textReader = new StringReader(str);
            //JsonTextReader jsonTextReader = new JsonTextReader(textReader);
            //object _object = jsonSerializer.Deserialize(jsonTextReader);
            //if (_object != null)
            //{
            //    StringWriter stringWriter = new StringWriter();
            //    JsonTextWriter jsonWriter = new JsonTextWriter(stringWriter)
            //    {
            //        Formatting = Formatting.Indented,
            //        Indentation = 4,
            //        IndentChar = ' '
            //    };
            //    jsonSerializer.Serialize(jsonWriter, _object);
            //    File.WriteAllText(savePath, stringWriter.ToString());
            //}
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
                ConvertToJson(Parent.LiquidCool, strUpPath, "\\" +  "0air" + strTime + ".json");
                //log.
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

        public void GetHeartbeat(string astrData, bool aIsFileData = false)
        {
            JObject jsonObject = null;
            jsonObject = JObject.Parse(astrData);
            string ID = jsonObject["HeartBeatID"].ToString();
            if (ID == HeartbeatID)
            {
                receivedHeartbeatResponse = true;
                SendAgain = true;
                Clock_Watch.RestartMeasurement();
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
                if (!frmSet.UseBalaTactics)
                    return strID;

                //清理旧数据
                SqlExecutor.EnqueueSqlTask("delete FROM balatactics", 2, outcome =>
                {
                    if (outcome)
                    {
                        log.Error("SQL  1 execution succeeded.");
                    }
                    else
                    {
                        log.Error("SQL 1 execution failed.");
                    }
                });

                string strData = "";
                //增加新数据
                for (int i = 0; i < iTacticCount; i++)
                {
                    strData = jsonObject["params"]["strategy"][i]["start"].ToString() + "','"
                        + jsonObject["params"]["strategy"][i]["end"].ToString();

                    //从云获取策略插入数据库中
                    strData = "INSERT into balatactics (startTime,endTime)VALUES('" + strData + "')";

                    //DBConnection.ExecSQL(strData);
                    SqlExecutor.EnqueueSqlTask(strData, 2, outcome =>
                    {
                        if (outcome)
                        {
                            log.Error("SQL  1 execution succeeded.");
                        }
                        else
                        {
                            log.Error("SQL 1 execution failed.");
                        }
                    });

                }
                //更新策略
                SqlExecutor.ExecuteEnqueueSqlBalaTacticsTask(3, frmMain.BalaTacticsList.BalaTacticsList);
                //frmMain.BalaTacticsList.LoadFromMySQL();
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
            if (astrData == "")
                return "";
            JObject jsonObject = JObject.Parse(astrData);
            string strID = "";
            try
            {
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
            catch
            { }
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
            if (astrData == "")
            {
                return false;
            }
            JObject jsonObject = null;
            jsonObject = JObject.Parse(astrData);
            string strID = "";
            try
            {
                strID = jsonObject["id"].ToString(); //int.Parse   bool.Parse
                string date = jsonObject["params"]["date"].ToString();
                string strTopic = jsonObject["method"].ToString();
                if (strTopic != "ems/strategy")
                    return false;
                int iTacticCount = jsonObject["params"]["strategy"].Count();

                //只有设置接受云策略 且 为主机 才接收云下发的策略
                if ((!frmSet.UseYunTactics)|| (!frmSet.IsMaster))
                {
                    return true;
                }

                //清理旧数据
                try
                {
                    bool hasData = SqlExecutor.CheckRec("select * from tactics");
                    if (hasData)
                    {
                        result = SqlExecutor.ExecuteSqlTasksSync("delete FROM tactics", 3);

                        if (result)
                        {
                            // 处理执行成功的逻辑
                        }
                        else
                        {
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 处理异常情况
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
                    try
                    {
                        result = SqlExecutor.ExecuteSqlTasksSync(strData, 3);

                        if (result)
                        {
                            // 处理执行成功的逻辑
                        }
                        else
                        {
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 处理异常情况
                    }
                }

                //if (frmMain.TacticsList.LoadFromMySQL())
                if(SqlExecutor.ExecuteEnqueueSqlTacticsTask(3, frmMain.TacticsList.TacticsList))
                {
                    frmMain.ShowShedule2Char(false);
                    frmMain.TacticsList.ActiveIndex = -1;
                }
            }
            catch
            { }
            return true;
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
            string strID = "";
            try
            {
                strID = jsonObject["id"].ToString(); //int.Parse   bool.Parse
                string date = jsonObject["params"]["date"].ToString();
                int iPriceCount = jsonObject["params"]["price"].Count();
                string strTopic = jsonObject["method"].ToString();
                if (strTopic != "meter/price")
                    return false;


                //清理旧数据
                try
                {
                    result = SqlExecutor.ExecuteSqlTasksSync("delete FROM electrovalence", 3);

                    if (result)
                    {
                        // 处理执行成功的逻辑
                    }
                    else
                    {
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    // 处理异常情况
                }

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

                    try
                    {
                        result = SqlExecutor.ExecuteSqlTasksSync(strData, 3);

                        if (result)
                        {
                            // 处理执行成功的逻辑
                        }
                        else
                        {
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 处理异常情况
                    }
                }
                //更新策略
                frmSet.SaveSet2File();
                SqlExecutor.ExecuteEnqueueJFPGSqlTask(3);
                //frmMain.TacticsList.LoadJFPGFromSQL();
                if (aIsFileData)
                    File.Delete(strDataFile);
            }
            catch
            { }
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
                    if ((frmSet.IsMaster)&&(ipcsSet!=4))
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
            catch
            { }
            return strID;
        }

        //设置窗口的几个限制值
        public string GetServerEMSLimit(string astrData)
        {
            if (astrData == "")
                return "";
            JObject jsonObject = JObject.Parse(astrData);
            string strID = "";
            try
            {
                strID = jsonObject["id"].ToString(); //int.Parse   bool.Parse
               //string date = jsonObject["params"]["date"].ToString();
                string strTopic = jsonObject["method"].ToString();
                if (strTopic != "ems/limit")
                {
                    return "";
                }
                else
                {
                    frmSet.MaxGridKW = (int)double.Parse(jsonObject["params"]["requireLimit"].ToString());//需量控制
                    frmSet.MinGridKW = (int)double.Parse(jsonObject["params"]["invertPower"].ToString());//逆功率限制值
                    frmSet.MaxSOC = (int)(double.Parse(jsonObject["params"]["socUp"].ToString())); //SOC上限
                    frmSet.MinSOC = (int)(double.Parse(jsonObject["params"]["socDown"].ToString())); //SOC下限
                    //frmSet.SaveSet2File();
                    frmSet.SetToGlobalSet();
                }
            }
            catch 
            { }
            //输出返回数据
            return strID; 
        }

        static public byte[] Back3Data(int aAddr, short iLen)
        {
            byte[] returnMsg = null;
            ushort aMsg;
            int index = 3;
            returnMsg = ModbusBase.BuildMSG3sTitle((byte)frmSet.i485Addr, 3, (ushort)iLen);
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
                    return ModbusBase.BuildMSG3Back((byte)frmSet.i485Addr, 3, (ushort)(Math.Abs(frmMain.Selffrm.AllEquipment.PCSScheduleKVA)));
                case 0x6002://实际功率
                    double value = Math.Abs(frmMain.Selffrm.AllEquipment.PCSList[0].allUkva);
                    return ModbusBase.BuildMSG3Back((byte)frmSet.i485Addr, 3,  (ushort)value);
                case 0x6003://充放电 
                    if (frmMain.Selffrm.AllEquipment.PCSKVA < -0.5)//充电            
                        return ModbusBase.BuildMSG3Back((byte)frmSet.i485Addr, 3, 0);
                    else if (frmMain.Selffrm.AllEquipment.PCSKVA > 0.5)//放电
                        return ModbusBase.BuildMSG3Back((byte)frmSet.i485Addr, 3, 1);
                    else//待机
                        return ModbusBase.BuildMSG3Back((byte)frmSet.i485Addr, 3, 2);
                case 0x6004: //PCSType 恒压横流恒功率、AC恒压
                    return ModbusBase.BuildMSG3Back((byte)frmSet.i485Addr, 3,(ushort)Array.IndexOf(PCSClass.PCSTypes, frmMain.Selffrm.AllEquipment.PCSTypeActive));
                case 0x6005: //EMS运行状态 ： 0正常，1故障，2停机
                    return ModbusBase.BuildMSG3Back((byte)frmSet.i485Addr, 3, (ushort)frmMain.Selffrm.AllEquipment.runState);
                case 0x6006: //BMS是否告警
                    if (frmMain.Selffrm.AllEquipment.BMS.Error[1] + frmMain.Selffrm.AllEquipment.BMS.Error[2] + frmMain.Selffrm.AllEquipment.BMS.Error[3] > 0)
                        return ModbusBase.BuildMSG3Back((byte)frmSet.i485Addr, 3, 1);
                    else
                        return ModbusBase.BuildMSG3Back((byte)frmSet.i485Addr, 3, 0);
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
