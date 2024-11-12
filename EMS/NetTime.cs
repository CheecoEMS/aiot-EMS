using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;


namespace EMS
{

    /// 网络时间 
    public class NetTime
    {


        //设置系统时间的API函数
        [DllImport("kernel32.dll")] private static extern bool SetLocalTime(ref SYSTEMTIME time);
        [DllImport("Kernel32.dll")] private static extern void GetLocalTime(ref SYSTEMTIME Time);
        //  [StructLayout(LayoutKind.Sequential)]
        [DllImport("wininet.dll")] private extern static bool InternetGetConnectedState(int Description, int ReservedValue);

        #region 检查网络是否可以连接互联网
        /// <summary>
        /// 用于检查网络是否可以连接互联网,true表示连接成功,false表示连接失败 
        /// </summary>
        /// <returns></returns>
        public static bool IsConnectInternet()
        {
            int Description = 0;
            return InternetGetConnectedState(Description, 0);
        }
        #endregion

        private struct SYSTEMTIME
        {
            public short year;
            public short month;
            public short dayOfWeek;
            public short day;
            public short hour;
            public short minute;
            public short second;
            public short milliseconds;
            //利用System.DateTime设置SYSTEMTIME数据成员
            public void FromDateTime(DateTime dt)
            {
                year = (short)dt.Year;
                month = (short)dt.Month;
                dayOfWeek = (short)dt.DayOfWeek;
                day = (short)dt.Day;
                hour = (short)dt.Hour;
                minute = (short)dt.Minute;
                second = (short)dt.Second;
                milliseconds = (short)dt.Millisecond;
            }
        }

        /// <summary> 
        /// 获取标准北京时间，读取http://www.beijing-time.org/time.asp 
        /// </summary> 
        /// <returns>返回网络时间</returns> 
        private static DateTime GetBeijingTime()
        {
            DateTime dt;
            WebRequest wrt = null;
            WebResponse wrp = null;
            try
            {
                wrt = WebRequest.Create("http://www.beijing-time.org/time.asp");
                wrp = wrt.GetResponse();
                string html = string.Empty;
                using (Stream stream = wrp.GetResponseStream())
                {
                    using (StreamReader sr = new StreamReader(stream, Encoding.UTF8))
                    {
                        html = sr.ReadToEnd();
                    }
                }
                string[] tempArray = html.Split(';');
                for (int i = 0; i < tempArray.Length; i++)
                {
                    tempArray[i] = tempArray[i].Replace("\r\n", "");
                }
                string year = tempArray[1].Split('=')[1];
                string month = tempArray[2].Split('=')[1];
                string day = tempArray[3].Split('=')[1];
                string hour = tempArray[5].Split('=')[1];
                string minite = tempArray[6].Split('=')[1];
                string second = tempArray[7].Split('=')[1];
                dt = DateTime.Parse(year + "-" + month + "-" + day + " " + hour + ":" + minite + ":" + second);
            }
            catch (WebException)
            {
                return DateTime.Parse("2011-1-1");
            }
            catch (Exception)
            {
                return DateTime.Parse("2011-1-1");
            }
            finally
            {
                if (wrp != null)
                    wrp.Close();
                if (wrt != null)
                    wrt.Abort();
            }
            return dt;
        }

        // 小端存储与大端存储的转换
        private static uint swapEndian(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
            ((x & 0x0000ff00) << 8) +
            ((x & 0x00ff0000) >> 8) +
            ((x & 0xff000000) >> 24));
        }

        // 方法1、获取NTP网络时间
        private static DateTime getWebTime()
        {
            // default ntp server
            const string ntpServer = "ntp1.aliyun.com";

            // NTP message size - 16 bytes of the digest (RFC 2030)
            byte[] ntpData = new byte[48];
            // Setting the Leap Indicator, Version Number and Mode values
            ntpData[0] = 0x1B; // LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

            IPAddress[] addresses = Dns.GetHostEntry(ntpServer).AddressList;
            foreach (var item in addresses)
            {
                Console.WriteLine("IP:" + item);
            }

            // The UDP port number assigned to NTP is 123
            IPEndPoint ipEndPoint = new IPEndPoint(addresses[0], 123);

            // NTP uses UDP
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(ipEndPoint);
            // Stops code hang if NTP is blocked
            socket.ReceiveTimeout = 3000;
            socket.Send(ntpData);
            socket.Receive(ntpData);
            socket.Close();

            // Offset to get to the "Transmit Timestamp" field (time at which the reply 
            // departed the server for the client, in 64-bit timestamp format."
            const byte serverReplyTime = 40;
            // Get the seconds part
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);
            // Get the seconds fraction
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);
            // Convert From big-endian to little-endian
            intPart = swapEndian(intPart);
            fractPart = swapEndian(fractPart);
            ulong milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000UL);

            // UTC time
            DateTime webTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds(milliseconds);
            // Local time
            return webTime.ToLocalTime();
        }

        //方法2、获取ntp时间
        private static DateTime DataStandardTime()//使用时，将static 关键字删除，在其它位置方可使用?2010-11-24
        {//返回国际标准时间
            //只使用的TimerServer的IP地址，未使用域名
            string[,] TimerServer = new string[14, 2];
            int[] ServerTab = new int[] { 3, 2, 4, 8, 9, 6, 11, 5, 10, 0, 1, 7, 12 };

            TimerServer[0, 0] = "time-a.nist.gov";
            TimerServer[0, 1] = "129.6.15.28";
            TimerServer[1, 0] = "time-b.nist.gov";
            TimerServer[1, 1] = "129.6.15.29";
            TimerServer[2, 0] = "time-a.timefreq.bldrdoc.gov";
            TimerServer[2, 1] = "132.163.4.101";
            TimerServer[3, 0] = "time-b.timefreq.bldrdoc.gov";
            TimerServer[3, 1] = "132.163.4.102";
            TimerServer[4, 0] = "time-c.timefreq.bldrdoc.gov";
            TimerServer[4, 1] = "132.163.4.103";
            TimerServer[5, 0] = "utcnist.colorado.edu";
            TimerServer[5, 1] = "128.138.140.44";
            TimerServer[6, 0] = "time.nist.gov";
            TimerServer[6, 1] = "192.43.244.18";
            TimerServer[7, 0] = "time-nw.nist.gov";
            TimerServer[7, 1] = "131.107.1.10";
            TimerServer[8, 0] = "nist1.symmetricom.com";
            TimerServer[8, 1] = "69.25.96.13";
            TimerServer[9, 0] = "nist1-dc.glassey.com";
            TimerServer[9, 1] = "216.200.93.8";
            TimerServer[10, 0] = "nist1-ny.glassey.com";
            TimerServer[10, 1] = "208.184.49.9";
            TimerServer[11, 0] = "nist1-sj.glassey.com";
            TimerServer[11, 1] = "207.126.98.204";
            TimerServer[12, 0] = "nist1.aol-ca.truetime.com";
            TimerServer[12, 1] = "207.200.81.113";
            TimerServer[13, 0] = "nist1.aol-va.truetime.com";
            TimerServer[13, 1] = "64.236.96.53";
            int portNum = 13;
            string hostName;
            byte[] bytes = new byte[1024];
            int bytesRead = 0;
            System.Net.Sockets.TcpClient client = new System.Net.Sockets.TcpClient();
            for (int i = 0; i < 13; i++)
            {
                hostName = TimerServer[ServerTab[i], 0];

                Console.WriteLine("hostName:" + hostName);
                try
                {
                    client.Connect(hostName, portNum);

                    System.Net.Sockets.NetworkStream ns = client.GetStream();
                    bytesRead = ns.Read(bytes, 0, bytes.Length);
                    client.Close();
                    break;
                }
                catch (System.Exception)
                {
                    Console.WriteLine("错误！");
                }
            }
            char[] sp = new char[1];
            sp[0] = ' ';
            System.DateTime dt = new DateTime();
            string str1;
            str1 = System.Text.Encoding.ASCII.GetString(bytes, 0, bytesRead);
            Console.WriteLine("ntp time:" + str1);

            string[] s;
            s = str1.Split(sp);
            dt = System.DateTime.Parse(s[1] + " " + s[2]);//得到标准时间
            Console.WriteLine("get:" + dt.ToShortTimeString());
            //dt=dt.AddHours (8);//得到北京时间*/
            return dt;

        }

        //方法3、获取网页时间

        //Bdpagetype:2
        //Bdqid:0xaff4e50f00011b53
        //Cache-Control:private
        //Connection:Keep-Alive
        //Content-Encoding:gzip
        //Content-Type:text/html;charset=utf-8
        //Date:Tue, 23 Oct 2018 03:24:38 GMTv
        private static string GetNetDateTime()
        {
            WebRequest request = null;
            WebResponse response = null;
            WebHeaderCollection headerCollection = null;
            string datetime = string.Empty;
            try
            {
                request = WebRequest.Create("https://www.baidu.com");
                request.Timeout = 1000;
                request.Credentials = CredentialCache.DefaultCredentials;
                response = (WebResponse)request.GetResponse();
                headerCollection = response.Headers;
                foreach (var h in headerCollection.AllKeys)
                { if (h == "Date") { datetime = headerCollection[h]; } }
                return datetime;
            }
            catch (Exception) { return datetime; }
            finally
            {
                if (request != null)
                { request.Abort(); }
                if (response != null)
                { response.Close(); }
                if (headerCollection != null)
                { headerCollection.Clear(); }
            }
        }

        /// <summary>
        /// 设置系统时间
        /// </summary>
        /// <param name="dt">需要设置的时间</param>
        /// <returns>返回系统时间设置状态，true为成功，false为失败</returns>
        private static bool SetDate(DateTime dt)
        {
            SYSTEMTIME st = new SYSTEMTIME();
            st.FromDateTime(dt);
            bool rt = SetLocalTime(ref st);
            return rt;
        }


        //对外函数，获取网络时间并设置到系统
        public static bool GetandSetTime()
        {
            bool bResult = false;
            DateTime m_dtNow = DateTime.Now;
            try
            {
                //GetLocalTime(ref MySystemTime);
                DateTime dt1 = getWebTime();
                // MessageBox.Show("获取的网络时间为：" + dt1.ToString());
                dt1.AddMilliseconds(3);
                bResult = SetDate(dt1);
            }
            catch
            {
                bResult = false;

            }
            return bResult;
        }
    }




}
