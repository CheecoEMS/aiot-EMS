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
        public MqttClient mqttClient { get; set; }
        public bool FirstRun = true;

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
        }


        public MqttClient CreateClient()
        {
            INIFile ConfigINI = new INIFile();
            string strSysPath = Convert.ToString(System.AppDomain.CurrentDomain.BaseDirectory);
            string INIPath = strSysPath + "Config.ini";
            String iotcode = ConfigINI.INIRead("System Set", "SysID", "202300001", INIPath).Trim();


            EMQX_CLIENT_ID = iotcode;
            log.Error("EMQX_CLIENT_ID: " + EMQX_CLIENT_ID);

            try
            {
                //mqttClient = null;
                if (mqttClient != null)
                {
                   // mqttClient.d
                }
                 //   mqttClient.Disconnect();
                // mqttClient.ConnectionClosed
                //建立连接
                //mqttClient = new MqttClient(EMQX_BROKER_IP, EMQX_BROKER_PORT, false, null, null, MqttSslProtocols.TLSv1_2);
                //mqttClient = new MqttClient(EMQX_BROKER_IP, EMQX_BROKER_PORT, false, null, null, MqttSslProtocols.None);
                mqttClient = new MqttClient(EMQX_BROKER_IP, EMQX_BROKER_PORT, true, null, null, MqttSslProtocols.TLSv1_2);
                //下面这种方法是个坑，并不能正常访问到MQTT服务
                // mqttClient = new MqttClient(IPAddress.Parse(EMQX_BROKER_IP));
                //mqttClient.ProtocolVersion = MqttProtocolVersion.Version_3_1;               
                mqttClient.Connect(EMQX_CLIENT_ID,
                                            "aiot",// user,
                                            "Lab123123123",//pwd,
                                            true, // cleanSession
                                            60); // keepAlivePeriod 
                //log.Error("mqttClient连接成功");
                //2.21
                //AutoCloud();
                connectflag = 1;

                //2.21 暂时注释
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
                if ((mqttClient != null) && (mqttClient.IsConnected))
                {
                    //断开连接
                    mqttClient.Disconnect();
                }
                CreateClient();
                ListenTopic(PriceTopic + "request");
                ListenTopic(TacticTopic + "request");
                ListenTopic(EMSLimitTopic + "request");
                ListenTopic(AIOTTableTopic + "request");
                ListenTopic(BalaTableTopic + "request");
                FirstRun = true;
            }
            catch {
               // mqttClient.IsConnected = false;
            }
        }


        public void mqttReconnect()
        {
            //string RecCommandInfo = new StackTrace().ToString();
            //Logger.Error("err:"+RecCommandInfo);


            //Logger.LogInit();
            //DateTime now = DateTime.Now;

            //Logger.Info("CreateClient: " + now);
            try
            {
                //mqttClient = null;
                /*                if (mqttClient != null)
                                    mqttClient.Disconnect();*/
                if (mqttClient != null)
                {
                    mqttClient.Connect(EMQX_CLIENT_ID,
                                               "aiot",// user,
                                               "Lab123123123",//pwd,
                                               true, // cleanSession
                                               60); // keepAlivePeriod 
                                                    //mqttClient.Connect(EMQX_CLIENT_ID);
                                                    //mqttClient.MqttMsgPublishReceived += Client_MqttMsgPublishReceived;

                    ListenTopic(PriceTopic + "request");
                    ListenTopic(TacticTopic + "request");
                    ListenTopic(EMSLimitTopic + "request");
                    ListenTopic(AIOTTableTopic + "request");
                    ListenTopic(BalaTableTopic + "request");
                }
            }
            catch (Exception ex)
            {

            }

        }



        public void CheckConnect()
        {
            if (connectflag == 1)
            {
                //log.Debug("重连");
                //mqttClient.Disconnect();
                if (mqttClient != null)
                {
                    if (!mqttClient.IsConnected)
                    {
                        mqttReconnect();
                    }
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
                strID = GetServerTactics(message, ref Result);
                if (Result && strID!="")
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
                strID = GetServerEPrices(message);
                if (strID != "")
                {
                    mqttClient.Publish(PriceTopic + "response/" + strID, System.Text.Encoding.UTF8.GetBytes(strResponse + strID + "\"}"),
                     MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, true);
                    frmMain.TacticsList.LoadJFPGFromSQL();
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
/*            else if (topic == BalaTacticTopic)
            {
                //log.Info("接收到均衡策略");
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
                ConvertToJson(Parent.LiquidCool, strUpPath, "\\" +  "0liq" + strTime + ".json");
            }//除湿机
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
        public string GetServerTactics(string astrData, ref bool result)
        {
            if (astrData == "")
            {
                return "";
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
                    return "";
                int iTacticCount = jsonObject["params"]["strategy"].Count();

                //只有设置接受云策略 且 为主机 才接收云下发的策略
                if ((!frmSet.UseYunTactics)|| (!frmSet.IsMaster))
                {
                    result = false;
                    return strID;
                }

                //清理旧数据
                int count1 = 10;
                while (!DBConnection.ExecSQL("delete FROM tactics") && count1 > 0)
                {
                    Thread.Sleep(60000);
                    count1--;
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

                    int count2 = 10;

                    while (count2 > 0)
                    {
                        if (DBConnection.ExecSQL(strData))
                        {
                            result = true;
                            break;
                        }
                        else
                        {
                            Thread.Sleep(60000);
                            count2--;
                        }
                    }
                }
                if (result)
                {
                    result = false;
                    if (frmMain.TacticsList.LoadFromMySQL())
                    {
                        frmMain.ShowShedule2Char(false);
                        frmMain.TacticsList.ActiveIndex = -1;
                        result = true;
                    }
                }
            }
            catch
            { }
            return strID;
        }

        /// <summary>
        /// 尖峰平谷的设置
        ///    "start":"11:30:00",
        ///    "end":"13:30:00",
        ///     "price":0.8,
        ///     "range":3 //  尖：1峰：2平：3谷：4
        /// </summary>
        /// <param name="astrTacticFile"></param>
        public string  GetServerEPrices(string astrData, bool aIsFileData = false)
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
                int iPriceCount = jsonObject["params"]["price"].Count();
                string strTopic = jsonObject["method"].ToString();
                if (strTopic != "meter/price")
                    return "";

                 
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
                    DBConnection.ExecSQL(strData);
                }
                //更新策略
                frmSet.SaveSet2File();
                frmMain.TacticsList.LoadJFPGFromSQL();
                if (aIsFileData)
                    File.Delete(strDataFile);
            }
            catch
            { }
            //输出返回数据
            return strID;
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
/*                    string requireLimit = jsonObject["params"]["requireLimit"].ToString();
                    log.Debug("requireLimit:" + requireLimit);
                    int limit = (int)double.Parse(requireLimit);
                    log.Debug("limit:" + limit);*/
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

  
        //连控数据中读取数据-----3读取
        static public byte[] Back3Data(int aAddr ) 
        { 
            switch (aAddr)
            {
                case 0x6001://计划功率
                    return ModbusBase.BuildMSG3Back((byte)frmSet.i485Addr, 3, (ushort)(Math.Abs(frmMain.Selffrm.AllEquipment.PCSScheduleKVA)));
                case 0x6002://实际功率
                    double value = Math.Abs(frmMain.Selffrm.AllEquipment.PCSKVA);
                    return ModbusBase.BuildMSG3Back((byte)frmSet.i485Addr, 3,  (ushort)value);
                case 0x6003://充放电 
                    if (frmMain.Selffrm.AllEquipment.wTypeActive == "充电")            
                        return ModbusBase.BuildMSG3Back((byte)frmSet.i485Addr, 3, 0);
                    else
                        return ModbusBase.BuildMSG3Back((byte)frmSet.i485Addr, 3, 1);                 
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
                    log.Debug("从机接收Command执行参数:"+ frmMain.Selffrm.AllEquipment.wTypeActive + frmMain.Selffrm.AllEquipment.PCSTypeActive + data);
                    lock (frmMain.Selffrm.AllEquipment)
                    {
                        frmMain.Selffrm.AllEquipment.HostStart = true;
                        frmMain.Selffrm.AllEquipment.PCSScheduleKVA = data;
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
