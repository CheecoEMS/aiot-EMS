using EMS;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using log4net;
using System.Web.WebSockets;
using System.Web.UI.WebControls;
using System.Diagnostics.Eventing.Reader;
using System.Diagnostics;
using MySqlX.XDevAPI;
using DotNetty.Transport.Channels.Pool;
using System.IO;


namespace Modbus

{
    public class ClientManager
    {
        //public int count = 1;
        //public IdManager IDmap;
        //public Dictionary<int, Socket> clientMap ;
        public List<int> IDs = new List<int>();
        public int[] IDss = new int[] { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };


        private static ILog log = LogManager.GetLogger("ClientManager");

        public bool FindClient(int ID, Socket clientSocket, ref Dictionary<int, (Socket, object)> clientMap)
        {
            for (int i = 0; i < 10; ++i)
            {
                if (IDss[i] == ID)
                {
                    log.Error("更新clientSocket");
                    if (clientMap.ContainsKey(ID))
                    {
                        clientMap[ID] = (clientSocket, clientMap[ID].Item2);
                    }
                    return true;
                }
            }
            return false;
        }

        public void AddClient(int ID, Socket clientSocket, ref object socketLock, ref Dictionary<int, (Socket, object)> clientMap)
        {
            clientMap.Add(ID, (clientSocket, socketLock));
            for (int i = 0; i < 10; ++i)
            {
                if (IDss[i] == -1)
                {
                    IDss[i] = ID;
                    break;
                }
            }

            for (int i = 0; i < 10; ++i)
            {
                log.Error("设备：" + IDss[i]);
            }
        }
        public void RemoveClient(int ID, ref Dictionary<int, (Socket, object)> clientMap)
        {
            if (clientMap.ContainsKey(ID))
            {
                clientMap.Remove(ID);
            }

            for (int i = 0; i < 10; ++i)
            {
                if (IDss[i] == ID)
                {
                    IDss[i] = -1;
                    break;
                }
            }
        }

        public Socket GetClient(int ID, ref Dictionary<int, (Socket, object)> clientMap)
        {
            if (clientMap != null)
            {
                if (clientMap.ContainsKey(ID))
                {
                    Socket clientSocket = clientMap[ID].Item1;
                    return clientSocket;
                }
                else
                {
                    return null;
                }
            }
            else { return null; }
        }

        public object GetsocketLock(int ID, ref Dictionary<int, (Socket, object)> clientMap)
        {
            if (clientMap != null)
            {
                if (clientMap.ContainsKey(ID))
                {
                    object socketLock = clientMap[ID].Item2;
                    return socketLock;
                }
                else
                {
                    return null;
                }
            }
            else { return null; }
        }

/*        public byte[] GetBuffer(int ID, ref Dictionary<int, (Socket, byte[])> clientMap)
        {
            if (clientMap != null)
            {
                if (clientMap.ContainsKey(ID))
                {
                    byte[] buffer = clientMap[ID].Item2;
                    return buffer;
                }
                else
                {
                    return null;
                }
            }
            else { return null; }
        }*/

    }

    //UDP Class
    class UdpClass
    {
        // 接收到服务器消息改变后触发的事件 
        public delegate void OnReceiveDataEventDelegate(object sender, string strData, string strFromIP, int iPort);//建立事件委托
        public event OnReceiveDataEventDelegate OnReceiveDataEvent;//收到数据的事件

        //监控标志
        bool IsMonitoring = false;
        // 定义UDP发送和接收
        private UdpClient udpReceiver = null;
        private UdpClient udpSender = null;
        //服务器端的IP与端口 
        private IPEndPoint ServerIPE = null;
        public int LocalPort = 0;
        public int ServerlPort = 0;
        //数据接收监听线程
        private Thread ClientRecThread;

        // 判断是否是正确的ip地址   
        public static bool IsIpaddress(string ipaddress)
        {
            string[] nums = ipaddress.Split('.');
            if (nums.Length != 4) return false;
            foreach (string num in nums)
            {
                if (Convert.ToInt32(num) < 0 || Convert.ToInt32(num) > 255) return false;
            }
            return true;
        }

        //构造函数 
        public UdpClass()
        {
            //实例化udpclient对象   
            //udpSender = new UdpClient();
            //udpReceiver = new UdpClient();
        }

        //  servierIpAddress  服务器iP地址或者域名，sevierPort 服务器监听端口， locadPort 本地监听端口   
        public bool UDPServerIni(string aServierIp, int aSevierPort, int aLocadPort)//string LocatIp,
        {
            if ((IsIpaddress(aServierIp) == false) || (udpSender != null))//(IsIpaddress(LocatIp) == false) || 
                return false;

            LocalPort = aLocadPort;
            ServerlPort = aSevierPort;
            ServerIPE = new IPEndPoint(IPAddress.Parse(aServierIp), aSevierPort);
            //IPEndPoint LocationIPE = new IPEndPoint(IPAddress.Parse(LocatIp), aLocadPort);
            try
            {   //实例化udpclient对象    
                udpSender = new UdpClient();//本地端口固定通信端口aSevierPortServerIPE
                udpReceiver = new UdpClient(aLocadPort); // LocationIPE
                //调用connect建立默认远程主机   
                // udpReceiver.Connect();    
                return true;
            }
            catch (System.Exception ex)
            {
                SqlExecutor.RecordLOG("云数据", "绑定端口失败", ex.Message.ToString());
                return false;
            }

        }

        //发送函数
        public void Send(string strSendData)
        {
            try
            {
                if (!IsMonitoring)
                    StartMonitor();
                //定义一个字节数组,用来存放发送到远程主机的信息 
                byte[] btSendData = Encoding.Default.GetBytes(strSendData);//UTF8 
                int len1 = udpSender.Send(btSendData, btSendData.Length, ServerIPE);
            }
            catch (Exception ex)
            {
                SqlExecutor.RecordLOG("云数据", "数据发送失败", ex.Message.ToString());
            }
        }

        //开始监控
        public void StartMonitor()
        {
            IsMonitoring = true;
            //开启接收线程   
            ClientRecThread = new Thread(new ThreadStart(ReceiveData));//启动新线程做接收
            ClientRecThread.IsBackground = true;
            ClientRecThread.Start();
        }//启动并且 监听 服务器发来的数据

        //停止监听
        private void StopMonitor()
        {
            IsMonitoring = false;
            ClientRecThread.Abort();
        }

        //接收数据做服务 
        private void ReceiveData()
        {
            //接收数据包   
            byte[] btRecData = null;
            string strRecData;
            IPEndPoint remoteIPE = new IPEndPoint(IPAddress.Any, 0);
            while (IsMonitoring)
            {
                try
                {
                    btRecData = udpReceiver.Receive(ref remoteIPE);//UDP接收数据
                    //qiao 2017-11-10 需要增加限制
                    if ((btRecData.Length > 0))
                    //&& (remoteIPE.Address.ToString() == ServerIPE.Address.ToString()))
                    //&&(remoteIPE.Port==ServerlPort))//只处理特定的服务端的数据
                    {
                        //将得到的数据包转换为字符串形式   
                        strRecData = Encoding.Default.GetString(btRecData, 0, btRecData.Length);//UTF8
                        //Ondata
                        if (OnReceiveDataEvent != null)
                        {
                            //this.Invoke(OnReceiveDataEvent, new object[] { this, strRecData, remoteIPE.Address.ToString(), remoteIPE.Port });
                            OnReceiveDataEvent.Invoke(this, strRecData, remoteIPE.Address.ToString(), remoteIPE.Port);
                        }
                    }
                    else
                    {
                        //No use data, do nothing
                    }
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    SqlExecutor.RecordLOG("云数据", "数据接受异常", ex.Message.ToString());
                    Thread.Sleep(1000);
                }
            }//while循环接收数据
        }//func




    }//UDPClass


    //Soket Class 38834.95
    public class TCPServerClass
    {
        //SetThreadAffinityMask: Set hThread run on logical processer(LP:) dwThreadAffinityMask
        [DllImport("kernel32.dll")]
        static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

        //Get the handler of current thread
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentThread();


        // 接收到服务器消息改变后触发的事件 
        public delegate void OnReceiveDataEventDelegate(Socket sender, string strData, string strFromIP, int iPort);//建立事件委托
        public event OnReceiveDataEventDelegate OnReceiveDataEvent;//收到数据的事件

        // 连接后触发的事件 
        public delegate void OnConcectEventDelegate(Socket sender);//建立事件委托
        public event OnConcectEventDelegate OnConectedEvent;//连接事件
        public event OnConcectEventDelegate OnDisconectEvent;//断开连接事件

        //tcp
        public delegate void OnReceiveDataEventDelegate2(Socket sender, byte[] strData, string strFromIP, int iPort);//建立事件委托
        public event OnReceiveDataEventDelegate2 OnReceiveDataEvent2;//收到数据的事件

        // 定义Soket发送和接收 
        public Socket ServerSocket = null;//服务器绑定端口初始化socket对象
        public Socket ClientSocket = null;//针对104连接的客户端从机

        //服务器端的IP与端口 
        private IPEndPoint ServerIPE = null;
        public int LocalPort = 0;

        //Soket Sever Connnect监听线程
        private Thread MonitorThread;
        
        //104数据接收与发送 
        private Thread ClientRecThread = null;//104：socket接收线程
        private byte[] RecData = new byte[1024];//104：socket接收缓冲区
        private static readonly object sendLock = new object();//针对104 server发送锁

        //tcp
        public ClientManager clientManager = null;
        public Dictionary<int, (Socket, object)> clientMap = null;//保存502端口接入的所有从机

        private CancellationTokenSource cts;//104：主机是否开启监听线程的token的source

        private static ILog log = LogManager.GetLogger("TCPServerClass");


        /// <summary>
        /// 检查是否有未释放的ClientSocket
        /// </summary>
        private void CheckClientSocketExist()
        {
            if (ClientSocket != null)
            {
                CancleCTS(ref cts);
                WaitThreadEnd(ref ClientRecThread);
                DestorySocket(ref ClientSocket);
            }
        }

        /// <summary>
        ///检查是否有未释放的cts
        /// </summary>
        private void CheckCtsExist()
        {
            if (cts != null)
            {
                CancleCTS(ref cts); // 请求取消当前的接收数据操作  
                WaitThreadEnd(ref ClientRecThread);// 等待接收线程终止
                DisposeCTS(ref cts);//释放资源
            }
        }

        /// <summary>
        /// 等待线程终止
        /// </summary>
        /// <param name="athread"></param>
        private void WaitThreadEnd(ref Thread athread)
        {
            if (athread != null)
            {
                athread.Join(); // 等待接收线程终止
            }
        }

        /// <summary>
        /// 停止CTS关联线程运行
        /// </summary>
        /// <param name="cts"></param>
        private void CancleCTS(ref CancellationTokenSource cts)
        {
            if (cts != null)
            {
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException ex)
                {
                    log.Error("CancleCTS: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 释放CTS资源
        /// </summary>
        /// <param name="cts"></param>
        private void DisposeCTS(ref CancellationTokenSource cts)
        {
            if (cts != null)
            {
                try
                {
                    cts.Dispose(); // 释放CancellationTokenSource资源  
                    cts = null; // 将cts设置为null，以防止后续误用
                    GC.Collect();
                }
                catch (ObjectDisposedException ex)
                {
                    log.Error("DisposeCTS: " + ex.Message);
                }
            }
        }

        //tcp:从机超时不回消息，则剔除
        public void DestroyClient(int ID)
        {
            clientManager.RemoveClient(ID, ref frmMain.Selffrm.ModbusTcpServer.clientMap);
        }

        // 销毁Socket对象 
        private static void DestorySocket(ref Socket aSocket)
        {
            try
            {
                if (aSocket != null)
                {
                    if (aSocket.Connected)
                    {
                        aSocket.Shutdown(SocketShutdown.Both);//关闭了套接字连接
                        aSocket.Close();//释放相关的资源
                    }
                    aSocket = null;// //将 clientSocket 变量设置为 null:避免在后续代码中误用已经关闭的套接字,若错误使用clientSocket，会引发NullReferenceException
                    GC.Collect();
                }
            }
            catch (SocketException ex)
            {
                log.Error("server释放socket资源失败：" + ex.Message);
            }
            catch (Exception ex)
            {
                log.Error("server释放socket资源失败：" + ex.Message);
            }
        }

        public static ulong SetCpuID(int lpIdx)
        {
            ulong cpuLogicalProcessorId = 0;
            if (lpIdx < 0 || lpIdx >= System.Environment.ProcessorCount)
            {
                lpIdx = 0;
            }
            cpuLogicalProcessorId |= 1UL << lpIdx;
            return cpuLogicalProcessorId;
        }

        public bool TCPServerIni(int aLocadPort)
        {
            try
            {
                //实例化TcpSetver对象  
                ServerIPE = new IPEndPoint(IPAddress.Any, aLocadPort);
                ServerSocket = new Socket(ServerIPE.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                //绑定IP
                ServerSocket.Bind(ServerIPE);
                ServerSocket.Listen(10);
                return true;
            }
            catch (SocketException ex)
            {
                log.Error("Init Server false: " + ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                log.Error("Init Server false: " + ex.Message);
                return false;
            }
        }

        //tcp
        public void StartMonitor502()
        {
            //新建一个委托线程
            ThreadStart myThreadDelegate = new ThreadStart(WaitConnectRequest502);
            //实例化等待连接的线程
            MonitorThread = new Thread(WaitConnectRequest502);
            MonitorThread.IsBackground = true;
            ulong LpId = SetCpuID(3);
            SetThreadAffinityMask(GetCurrentThread(), new UIntPtr(LpId));
            MonitorThread.Start();
        }



        //开始监控
        public void StartMonitor104()
        {
            //新建一个委托线程
            ThreadStart myThreadDelegate = new ThreadStart(WaitConnectRequest104);
            //实例化等待连接的线程
            MonitorThread = new Thread(WaitConnectRequest104);
            MonitorThread.IsBackground = true;
            ulong LpId = SetCpuID(3);
            SetThreadAffinityMask(GetCurrentThread(), new UIntPtr(LpId));
            MonitorThread.Start();
        }

        //停止监听
        private void StopMonitor()
        {
            MonitorThread.Abort();
            if (ClientSocket != null)
            {
                DestorySocket(ref ClientSocket);
                ClientRecThread.Abort();
                ClientRecThread = null;
                ClientSocket = null;
            }
        }

        private void CloseClientSocket()
        {
            try
            {
                if (ClientSocket != null)
                {
                    if (OnDisconectEvent != null)
                        OnDisconectEvent(ClientSocket);
                    
                    CancleCTS(ref cts);
                    WaitThreadEnd(ref ClientRecThread);
                    DestorySocket(ref ClientSocket);
                    DisposeCTS(ref cts);
                }
            }
            catch (SocketException ex)
            {
                log.Error("CloseClientSocket is false: " + ex.Message);
            }
            catch (Exception ex)
            {
                log.Error("CloseClientSocket is false: " + ex.Message);
            }

        }

        //等待连接Soket Server的请求

        //502
        private void WaitConnectRequest502()
        {
            while (true)
            {
                try
                {
                    Socket acceptSocket = ServerSocket.Accept();//accept()阻塞方法接收客户端的连接，返回一个连接上的Socket对象           

                    if (OnConectedEvent != null)
                        OnConectedEvent(acceptSocket);

                    //设置从机回复消息的等待时长
                    acceptSocket.ReceiveTimeout = 2000; //2s
                    object socketLock = new object();
                    //发送问询报文
                    int virtualID = AskEmsID(ref acceptSocket);
                    if (virtualID != -1)
                    {
                        log.Error("加入新的virtualID:" + virtualID);
                        if (frmMain.Selffrm.ModbusTcpServer.clientManager != null && frmMain.Selffrm.ModbusTcpServer.clientMap != null)
                        {
                            if (!frmMain.Selffrm.ModbusTcpServer.clientManager.FindClient(virtualID, acceptSocket, ref frmMain.Selffrm.ModbusTcpServer.clientMap))
                            {
                                frmMain.Selffrm.ModbusTcpServer.clientManager.AddClient(virtualID, acceptSocket, ref socketLock, ref frmMain.Selffrm.ModbusTcpServer.clientMap);
                                log.Error("新加入完成");
                            }                              
                        }
                    }
                }
                catch (SocketException ex)
                {
                    log.Error("WaitConnectRequest502 is false: " + ex.Message);
                }
                catch (Exception ex)
                {
                    log.Error("WaitConnectRequest502 is false: " + ex.Message);
                }
            }
        }

        //104
        private void WaitConnectRequest104()
        {
            while (true)
            {    
                try
                {
                    Socket acceptSocket = ServerSocket.Accept();//accept()阻塞方法接收客户端的连接，返回一个连接上的Socket对象                                                                
                    //acceptSocket.ReceiveTimeout = 2000; ///设置从机回复消息的等待时长:2s
                    CheckClientSocketExist();                
                    if (OnConectedEvent != null)
                        OnConectedEvent(acceptSocket);
                    ClientSocket = acceptSocket;

                    CheckCtsExist();
                    // 初始化CancellationTokenSource  
                    cts = new CancellationTokenSource();
                    CancellationToken cancelReceiveToken = cts.Token;

                    //开启接收线程  
                    ClientRecThread = new Thread(() => ReceiveData(cancelReceiveToken));
                    ClientRecThread.IsBackground = true;
                    ulong LpId = SetCpuID(3);
                    SetThreadAffinityMask(GetCurrentThread(), new UIntPtr(LpId));
                    ClientRecThread.Start();
                }
                catch (SocketException ex)
                {
                    log.Error("WaitConnectRequest104 is false: " + ex.Message);
                }
                catch (Exception ex)
                {
                    log.Error("WaitConnectRequest104 is false: " + ex.Message);
                }
            }

        }



        //发送信息
        public bool Send(string strMessage)
        {
            if ((ClientSocket == null) || (!ClientSocket.Connected))
                return false;
            try
            {
                ClientSocket.Send(Encoding.ASCII.GetBytes(strMessage));
                return true;
            }
            catch
            {
                return false;
            }
        }

        //104数据发送
        public bool SendMsg_byte(byte[] msg, ref Socket aCllientSoket)
        {
            if ((aCllientSoket == null) || (!aCllientSoket.Connected))
                return false;
            try
            {
                lock (sendLock)
                {
                    aCllientSoket.Send(msg);
                }
                return true;
            }
            catch (SocketException ex)
            {
                CancleCTS(ref cts);
                log.Error ("SendMsg_byte: " + ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                CancleCTS(ref cts);
                log.Error("SendMsg_byte: " + ex.Message);
                return false;
            }
        }


        //发送信息
        public bool SendMSG(string strMessage, Socket aCllientSoket)
        {
            if ((aCllientSoket == null) || (!aCllientSoket.Connected))
                return false;
            try
            {
                ClientSocket.Send(Encoding.ASCII.GetBytes(strMessage));
                return true;
            }
            catch
            {
                return false;
            }
        }


        //线程监听函数
        private void ReceiveData(CancellationToken cancelReceiveToken)
        {
            if ((ClientSocket == null) || (cts == null) || cancelReceiveToken == null)
                return;

            while (!cancelReceiveToken.IsCancellationRequested)
            {
                try
                {
                    int receiveNumber = ClientSocket.Receive(RecData);
                    if (receiveNumber > 0)
                    {
                        byte[] recdata = RecData.Take(receiveNumber).ToArray();

                        if (OnReceiveDataEvent2 != null)
                            OnReceiveDataEvent2(ClientSocket, recdata, ClientSocket.ToString(), LocalPort);
                    }
                }
                catch (SocketException ex)
                {
                    log.Error("Server ReceiveData is false: " + ex.Message);
                    if (OnDisconectEvent != null)
                        OnDisconectEvent(ClientSocket);

                    CancleCTS(ref cts);//通知接受线程终止 
                }
                catch (Exception ex)
                {
                    CancleCTS(ref cts);//通知接受线程终止
                    log.Error("Server ReceiveData is false: " + ex.Message);
                }
            }
            //释放socket资源
            DestorySocket(ref ClientSocket);
            //释放cts资源
            DisposeCTS(ref cts);
        }



        /***************************************************************************/
        /*                                                                         */
        /*                     modbusRTU 在TCP上的实现                             */
        /*                                                                         */
        /**************************************************************************/
/*        public bool GetString(int ID, ref Socket clientSocket, ref object socketLock, byte CommandType, ushort aRegStart, ushort aRegLength, ref string aResult, bool aIxX2 = true)
        {
            ushort[] ResultData = null;//=new byte[100];
            if (Send3MSG(ID, ref clientSocket, ref socketLock, CommandType, aRegStart, aRegLength, ref ResultData))
            {
                for (int i = 0; i < ResultData.Length; i++)
                {
                    if (aIxX2)
                        aResult += ((byte)(ResultData[i] >> 8)).ToString("X2") + ((byte)(ResultData[i])).ToString("X2");
                    else
                        aResult += (char)(ResultData[i] >> 8) + (byte)(ResultData[i]);
                }
                return true;
            }
            else
                return false;
        }*/


        /// <summary>
        /// 功能码03
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="clientSocket"></param>
        /// <param name="socketLock"></param>
        /// <param name="CommandType"></param>
        /// <param name="aRegStart"></param>
        /// <param name="aRegLength"></param>
        /// <param name="aResult"></param>
        /// <returns></returns>
        public bool GetUShort(int ID, ref Socket clientSocket, ref object socketLock, byte CommandType, ushort aRegStart, ushort aRegLength, ref ushort aResult)
        {
            if (clientSocket != null && clientSocket.Connected)
            {
                ushort[] ResultData = null;//=new byte[100];
                if (Send3MSG(ID, ref clientSocket, ref socketLock, CommandType, aRegStart, aRegLength, ref ResultData))
                {
                    if (ResultData.Length > 0)
                        aResult = (UInt16)ResultData[0];
                    return true;
                }
                else
                    return false;
            }
            else
                return false;
        }
        public bool Send3MSG(int ID, ref Socket clientSocket, ref object socketLock, byte CommandType, ushort aRegStart, ushort aRegLength, ref ushort[] values)
        {
            if (clientSocket != null && clientSocket.Connected)
            {
                byte[] response = null;
                if (!Read3Response(ID, ref clientSocket, ref socketLock, CommandType, aRegStart, aRegLength, ref response))
                {
                    return false;
                }
                //返回数据转换
                values = new ushort[aRegLength];
                //Return requested register values:
                for (int i = 0; i < (response.Length - 5) / 2; i++) //5 ：设备地址1字节+功能码1字节+字节数1字节+CRC2字节 = 5字节 , /2 :2个字节作为1个ushort类型的vlaue
                {
                    values[i] = response[2 * i + 3];//modbus response从第4个字节开始是寄存器值
                    values[i] <<= 8;
                    values[i] += response[2 * i + 4];
                }

                return true;
            }
            else
                return false;
        }

        private bool Read3Response(int ID, ref Socket clientSocket, ref object socketLock, byte CommandType, ushort aRegAddr, ushort aRegLength, ref byte[] aResponse)
        {
            byte[] message = ModbusBase.BuildMSG3((byte)ID, CommandType, aRegAddr, aRegLength);

            byte[] response = new byte[5 + 2 * aRegLength];

            if (clientSocket != null && clientSocket.Connected)
            {
                if (!GetSocketDada(ID, ref clientSocket, ref socketLock, message, ref response))
                    return false;

                //Evaluate message:
                if (ModbusBase.CheckResponse(response))
                {
                    aResponse = response;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else { return false; }
        }

        /*************/


        /// <summary>
        /// 功能吗06
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="clientSocket"></param>
        /// <param name="CommandType"></param>
        /// <param name="aRegStart"></param>
        /// <param name="aData"></param>
        /// <returns></returns>
        public int Send6MSG(int ID, ref Socket clientSocket, ref object socketLock, byte CommandType, ushort aRegStart, ushort aData)
        {
            if (clientSocket != null)
            {
                int result = 0;
                byte[] response = null;
                if (!Read6Response(ID, ref clientSocket, ref socketLock,  CommandType, aRegStart, aData, ref response))
                {
                    return -1;

                }
                //[11][05][00][AC][FF][00][CRC高][CRC低]
                //返回数据转换，成功元数据返回，失败将不反悔
                if (response !=null)
                {
                    result = response[0];
                }


                //values = new byte[BackDataLen];
                //Return requested register values:
                // Array.Copy(response, 4, values, 0, 2);
                return result;
            }
            else
            {
                return -1;
            }

        }

        private bool Read6Response(int ID, ref Socket clientSocket, ref object socketLock, byte CommandType, ushort aRegAddr, ushort aData, ref byte[] aResponse)
        {
            byte aAddress = 0xFF;
            byte[] message = ModbusBase.BuildMSG6(aAddress, CommandType, aRegAddr, aData);

            byte[] response = new byte[8];

            //Send modbus message to Serial Port:
            if (!GetSocketDada(ID, ref clientSocket, ref socketLock, message, ref response))
                return false;

            //Evaluate message:
            if (ModbusBase.CheckResponse(response))
            {
                aResponse = response;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 带socketLock的socket收发函数
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="clientSocket"></param>
        /// <param name="socketLock"></param>
        /// <param name="aMessage"></param>
        /// <param name="aResponse"></param>
        /// <returns></returns>
        private bool GetSocketDada(int ID, ref Socket clientSocket, ref object socketLock, byte[] aMessage, ref byte[] aResponse)
        {
            bool bResult = false;
            if (clientSocket != null && clientSocket.Connected)
            {
                try
                {
                    lock (socketLock)
                    {
                        int res = clientSocket.Send(aMessage);
                        if (GetSocketResponse(ID, ref clientSocket, ref aResponse))
                        {
                            bResult = true;

                        }
                        else
                        {
                            bResult = false;
                        }
                    }
                }
                catch (SocketException ex)
                {
                    bResult = false;
                    log.Error("发送client捕获ex: " + ex.Message);
                    if (ID != 0)//0是从机首次连接时，主机发送的问询报文
                    {
                        frmMain.Selffrm.ModbusTcpServer.DestroyClient(ID);
                        DestorySocket(ref clientSocket);
                        log.Error("剔除"+ID+"号从机");
                    }
                }
                return bResult;
            }
            else { return false; }

        }
        private bool GetSocketResponse(int ID, ref Socket clientSocket, ref byte[] response)
        {
            int receiveNumber = 0;
            bool bResult = false;
            if (clientSocket != null)
            {
                try
                {
                    byte[] buffer = new byte[1024];
                    receiveNumber = clientSocket.Receive(buffer);
                    if (receiveNumber > 0)
                    {
                        Array.Copy(buffer, 0, response, 0, response.Length);
                        bResult = true;
                    }
                    else
                    {
                        bResult = false;
                    }

                }
                catch (SocketException ex)
                {
                    bResult = false;
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        // Log the timeout
                        log.Error("接收超时");
                        if (ID != 0)//0是从机首次连接时，主机发送的问询报文
                        {
                            frmMain.Selffrm.ModbusTcpServer.DestroyClient(ID);
                            DestorySocket(ref clientSocket);
                            log.Error("剔除"+ID+"号从机");
                        }
                    }
                    else
                    {
                        log.Error("物联断开或从机EMS下线");
                        if (ID != 0)//0是从机首次连接时，主机发送的问询报文
                        {
                            frmMain.Selffrm.ModbusTcpServer.DestroyClient(ID);
                            DestorySocket(ref clientSocket);
                            log.Error("剔除"+ID+"号从机");
                        }
                    }
                }
                return bResult;
            }
            else
                return false;
        }

        /*********
         * 1.连接时问询
         * 2.下发控制指令
         * 
         * ************/

        //Modbus502
        //主机首次连接问询从机
        public int AskEmsID(ref Socket ClientSocket)
        {
            IPEndPoint localEndPoint = (IPEndPoint)ClientSocket.LocalEndPoint;
            log.Error("Local IP address: " + localEndPoint.Address);
            log.Error("Local port: " + localEndPoint.Port);

            // Get the remote endpoint information
            IPEndPoint remoteEndPoint = (IPEndPoint)ClientSocket.RemoteEndPoint;
            log.Error("Remote IP address: " + remoteEndPoint.Address);
            log.Error("Remote port: " + remoteEndPoint.Port);

            int result = frmMain.Selffrm.ModbusTcpServer.SendAskMSG(0, ref ClientSocket, 32, 0x6003, 1);
            log.Error("1次问");
            return result;
        }
        public int SendAskMSG(int ID, ref Socket clientSocket, byte CommandType, ushort aRegStart, ushort aData)
        {
            if (clientSocket != null)
            {
                int result = 0;
                byte[] response = null;
                if (!ReadASKResponse(ID, ref clientSocket, CommandType, aRegStart, aData, ref response))
                {
                    return -1;

                }
                //[11][05][00][AC][FF][00][CRC高][CRC低]
                //返回数据转换，成功元数据返回，失败将不反悔
                if (response !=null)
                {
                    result = response[0];
                }


                //values = new byte[BackDataLen];
                //Return requested register values:
                // Array.Copy(response, 4, values, 0, 2);
                return result;
            }
            else
            {
                return -1;
            }

        }

        private bool ReadASKResponse(int ID, ref Socket clientSocket, byte CommandType, ushort aRegAddr, ushort aData, ref byte[] aResponse)
        {
            byte aAddress = 0xFF;
            byte[] message = ModbusBase.BuildMSG6(aAddress, CommandType, aRegAddr, aData);

            byte[] response = new byte[8];

            //Send modbus message to Serial Port:
            if (!GetASKDada(ID, ref clientSocket, message, ref response))
                return false;

            //Evaluate message:
            if (ModbusBase.CheckResponse(response))
            {
                aResponse = response;
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool GetASKDada(int ID, ref Socket clientSocket, byte[] aMessage, ref byte[] aResponse)
        {
            bool bResult = false;
            if (clientSocket != null && clientSocket.Connected)
            {
                bResult= GetASKFreeData(ID, ref clientSocket, aMessage, ref aResponse);
                if (bResult)
                {
                    return bResult;
                }
                else
                {
                    // SqlExecutor.RecordLOG("通讯异常", "反应超时", "无法判断具体设备");
                    return bResult;
                }
            }
            else
                return false;

        }
        private bool GetASKFreeData(int ID, ref Socket clientSocket, byte[] aMessage, ref byte[] aResponse)
        {
            bool bResult = false;
            if (clientSocket != null && clientSocket.Connected)
            {
                try
                {
                    int res = clientSocket.Send(aMessage);
                    if (GetASKResponse(ID, ref clientSocket, ref aResponse))
                    {
                        bResult = true;

                    }
                    else
                    {
                        bResult = false;
                    }             
                }
                catch (SocketException ex)
                {
                    bResult = false;
                    log.Error("发送client捕获ex: " + ex.Message);
                    if (ID != 0)//0是从机首次连接时，主机发送的问询报文
                    {
                        frmMain.Selffrm.ModbusTcpServer.DestroyClient(ID);
                        DestorySocket(ref clientSocket);
                        log.Error("剔除"+ID+"号从机");
                    }
                }
                return bResult;
            }
            else { return false; }
        }
        private bool GetASKResponse(int ID,ref  Socket clientSocket, ref byte[] response)
        {
            int receiveNumber = 0;
            bool bResult = false;
            if (clientSocket != null)
            {
                try
                {
                    byte[] buffer = new byte[1024];
                    receiveNumber = clientSocket.Receive(buffer);
                    if (receiveNumber > 0)
                    {
                        Array.Copy(buffer, 0, response, 0, response.Length);
                        bResult = true;
                    }
                    else
                    {
                        bResult = false;
                    }

                }
                catch (SocketException ex)
                {
                    bResult = false;
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        // Log the timeout
                        log.Error("接收超时");
                        if (ID != 0)//0是从机首次连接时，主机发送的问询报文
                        {
                            frmMain.Selffrm.ModbusTcpServer.DestroyClient(ID);
                            DestorySocket(ref clientSocket);
                            log.Error("剔除"+ID+"号从机");
                        }
                    }   
                    else
                    {
                        log.Error("物联断开或从机EMS下线");
                        if (ID != 0)//0是从机首次连接时，主机发送的问询报文
                        {
                            frmMain.Selffrm.ModbusTcpServer.DestroyClient(ID);
                            DestorySocket(ref clientSocket);
                            log.Error("剔除"+ID+"号从机");
                        }
                    }            
                }
                return bResult;
            }
            else
                return false;
        }


    }//class

    public class SocketBufferQueue
    {
        private Queue<byte[]> bufferQueue = new Queue<byte[]>();
        private static ILog log = LogManager.GetLogger("SocketBufferQueue");

        // Method to add 8 bytes to the buffer queue
        public void AddToQueue(byte[] data)
        {
            if (data.Length == 8)
            {
                bufferQueue.Enqueue(data);
                //报文插入成功
            }
            else
            {
                throw new ArgumentException("Data must be 8 bytes long");
            }
        }

        // Method to retrieve 8 bytes from the buffer queue
        public byte[] GetFromQueue()
        {
            if (bufferQueue.Count > 0)
            {
                return bufferQueue.Dequeue();
            }
            else
            {
                throw new InvalidOperationException("Buffer queue is empty");
            }
        }
        public bool IsEmpty()
        {
            return bufferQueue.Count >= 3;
        }
    }


    public class TCPClientClass
    {
        /****************线程亲核性***********************/
        //SetThreadAffinityMask: Set hThread run on logical processer(LP:) dwThreadAffinityMask
        [DllImport("kernel32.dll")]
        static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

        //Get the handler of current thread
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentThread();
        /*****************线程亲核性**********************/

        public bool Connected = false;  //从机判断主机是否离线，断线重连
        // 接收到服务器消息改变后触发的事件 
        public delegate void OnReceiveDataEventDelegate(object sender, byte[] aByteData);//建立事件委托
        public event OnReceiveDataEventDelegate OnReceiveDataEvent;//收到数据的事件

        //tcp
        public delegate void OnReceiveDataEventDelegate2(Socket sender, byte[] strData);//建立事件委托
        public event OnReceiveDataEventDelegate2 OnReceiveDataEvent2;//收到数据的事件


        // 连接后触发的事件 
        public delegate void OnConcectEventDelegate();// (Socket sender);//建立事件委托
        public event OnConcectEventDelegate OnConectedEvent;//连接事件
        public event OnConcectEventDelegate OnDisconectEvent;//断开连接事件
        public event OnConcectEventDelegate OnReconnectFailed;

        private byte[] RecData = new byte[1024];//socket接收缓冲区
        public Socket clientSocket = null;//保存成功连接socket对象

        //配置参数
        private IPAddress ipAddress;
        private int iSevierPort;
        private IPEndPoint ipEndpoint;
        public int ReconnectTime = 1000;//ms

        private Thread ClientRecThread = null;//socket接收数据线程

        public SocketBufferQueue ClientBuffer = new SocketBufferQueue();

        private CancellationTokenSource cts;//客户端是否开启监听线程的token的source

        //客户端收发锁：只适用于与主机连接的唯一clientSocket
        private static readonly object sendLock = new object();//发送锁
        //private static readonly object receiveLock = new object();

        private static ILog log = LogManager.GetLogger("TCPClientClass");

        ~TCPClientClass()
        {
            //ClientRecThread.Abort();
        }

        //  servierIpAddress  服务器iP地址或者域名，sevierPort 服务器监听端口
        public void TCPClientIni(string aServierIpAddress, int aSevierPort)
        {
            iSevierPort = aSevierPort;
            ipAddress = IPAddress.Parse(aServierIpAddress);
            ipEndpoint = new IPEndPoint(ipAddress, iSevierPort);
            ConnectTCP();//连接服务器端口
        }

        public bool ConnectTCP()
        {
            try
            {
                CheckClientSocketExist();
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                if (clientSocket != null)
                {
                    clientSocket.Connect(ipEndpoint); //链接服务器IP与端口  
                    clientSocket.Blocking = true;
                    clientSocket.ReceiveTimeout = 6000;
                    clientSocket.SendTimeout = 6000;
                    if (OnConectedEvent != null)
                        OnConectedEvent.Invoke();
                    Connected = true;

                    CheckCtsExist();
                    // 初始化CancellationTokenSource  
                    cts = new CancellationTokenSource();
                    CancellationToken cancelReceiveToken = cts.Token;
                    StartMonitor(cancelReceiveToken);
                    return true;
                }
                else
                {
                    log.Error("failed to new clientSocket");
                    Connected = false;
                    return false;
                }
            }
            catch (SocketException ex)
            {
                log.Error("server is not alive: " + ex.Message);
                Connected = false;
                return false;
            }
            catch (Exception ex)
            {
                //SqlExecutor.RecordLOG("云数据", "云端口连接失败", ex.Message.ToString());//记录连接失败
                Connected = false; 
                return false;
            }
        }

        //关闭链接
        private void DestorySocket(ref Socket aSocket)
        {
            try
            {
                if (aSocket != null)
                {
                    if (aSocket.Connected)
                    {
                        aSocket.Shutdown(SocketShutdown.Both);//关闭了套接字连接
                        aSocket.Close();//释放相关的资源
                    }
                    aSocket = null;// //将 clientSocket 变量设置为 null:避免在后续代码中误用已经关闭的套接字,若错误使用clientSocket，会引发NullReferenceException
                    GC.Collect();
                }
            }
            catch (SocketException ex)
            {
                log.Error("server释放socket资源失败：" + ex.Message);
            }
            catch (Exception ex)
            {
                log.Error("server释放socket资源失败：" + ex.Message);
            }
        }

        /// <summary>
        ///检查是否有未释放的cts
        /// </summary>
        private void CheckCtsExist()
        {
            if (cts != null)
            {
                CancleCTS(ref cts); // 请求取消当前的接收数据操作  
                WaitThreadEnd(ref ClientRecThread);// 等待接收线程终止
                DisposeCTS(ref cts);//释放资源
            }
        }

        /// <summary>
        /// 检查是否有未释放的ClientSocket
        /// </summary>
        private void CheckClientSocketExist()
        {
            if (clientSocket != null)
            {
                CancleCTS(ref cts);
                WaitThreadEnd(ref ClientRecThread);
                DestorySocket(ref clientSocket);
            }
        }

        /// <summary>
        /// 关闭端口连接,释放所有资源
        /// </summary>
        public void CloseConnect()
        {
            Connected = false;//socket连接标志位置false
            CancleCTS(ref cts);
            WaitThreadEnd(ref ClientRecThread);
            DestorySocket(ref clientSocket);
            DisposeCTS(ref cts);      
        }

        /// <summary>
        /// 等待线程终止
        /// </summary>
        /// <param name="athread"></param>
        private void WaitThreadEnd(ref Thread athread)
        {
            if (athread != null)
            {
                athread.Join(); // 等待接收线程终止
            }
        }

        /// <summary>
        /// 停止CTS关联线程运行
        /// </summary>
        /// <param name="cts"></param>
        private void CancleCTS(ref CancellationTokenSource cts)
        {
            if (cts != null)
            {
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException ex)
                {
                    log.Error("CancleCTS: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 释放CTS资源
        /// </summary>
        /// <param name="cts"></param>
        private void DisposeCTS(ref CancellationTokenSource cts)
        {
            if (cts != null)
            {
                try
                {
                    cts.Dispose(); // 释放CancellationTokenSource资源  
                    cts = null; // 将cts设置为null，以防止后续误用
                    GC.Collect();
                }
                catch (ObjectDisposedException ex)
                {
                    log.Error("DisposeCTS: " + ex.Message);
                }
            }
        }



        //开始监控
        public bool StartMonitor(CancellationToken cancelReceiveToken)
        {
            try
            {
                //实例化等待连接的线程
                ClientRecThread = new Thread(() => ReceiveData(cancelReceiveToken));
                ClientRecThread.IsBackground = true;
                ulong LpId = SetCpuID(0);
                SetThreadAffinityMask(GetCurrentThread(), new UIntPtr(LpId));
                ClientRecThread.Start();
                ClientRecThread.Priority = ThreadPriority.Highest;
                //IsMonitoring = true;
                if (OnConectedEvent != null)
                    OnConectedEvent.Invoke();
                return true;
            }
            catch
            {
                //IsMonitoring = false;
                return false;
            }
        }

        //接收数据做服务 
        private void ReceiveData(CancellationToken cancelReceiveToken)
        {
            if ((clientSocket == null) || cancelReceiveToken == null || cts == null)
                return;

            while (!cancelReceiveToken.IsCancellationRequested)
            {
                try
                {
                    if (clientSocket != null)
                    {
                        //lock (receiveLock)
                        {
                            int receiveNumber = clientSocket.Receive(RecData);
                            if (receiveNumber > 0)
                            {
                                byte[] data = new byte[receiveNumber];
                                Array.Copy(RecData, 0, data, 0, receiveNumber);
                                // string hexString = BitConverter.ToString(data);
                                if (OnReceiveDataEvent2 != null)
                                {
                                    OnReceiveDataEvent2(clientSocket, data);
                                }
                                else
                                {
                                    log.Error("OnReceiveDataEvent2 is null");
                                }
                            }
                        }
                    }
                }
                catch (SocketException ex)
                {
                    CancleCTS(ref cts);//通知接受线程终止
                    log.Error("client ReceiveData is false: " + ex.Message);
                }
                catch (Exception ex)
                {
                    CancleCTS(ref cts);//通知接受线程终止
                    log.Error("client ReceiveData is false: " + ex.Message);
                }
            }
            //释放socket资源
            DestorySocket(ref clientSocket);
            //释放cts资源
            DisposeCTS(ref cts);
        }//func


        //发送信息
        public bool SendMSG(byte[] aByteData)
        {
            try
            {
                if (clientSocket != null)
                {
                    lock (sendLock)
                    {
                        clientSocket.Send(aByteData);
                    }
                }
                return true;
            }
            catch (SocketException ex)
            {
                CancleCTS(ref cts);             
                return false;
            }
            catch (Exception ex)
            {
                CancleCTS(ref cts);
                return false;
            }
        }

        public void Tick()
        {
            byte[] oneByte = { 1, 1, 0 };
            SendMSG(oneByte);
        }

        public static ulong SetCpuID(int lpIdx)
        {
            ulong cpuLogicalProcessorId = 0;
            if (lpIdx < 0 || lpIdx >= System.Environment.ProcessorCount)
            {
                lpIdx = 0;
            }
            cpuLogicalProcessorId |= 1UL << lpIdx;
            return cpuLogicalProcessorId;
        }

        // 检查云的连接问题，连接失败，重新连接 
        public void AutoConnect()
        {
            try
            {
                //ConnectTCP(); 
                //实例化等待连接的线程
                Thread ClientRecThread = new Thread(CheckandReconnect);
                ulong LpId = SetCpuID(1);
                SetThreadAffinityMask(GetCurrentThread(), new UIntPtr(LpId));
                ClientRecThread.IsBackground = true;
                ClientRecThread.Start();
                ClientRecThread.Priority = ThreadPriority.Lowest;
            }
            catch
            {
            }
        }

        private void CheckandReconnect()
        {
            while (true)
            {
                //Thread.Sleep(ReconnectTime * 1000);
                if (!frmMain.Selffrm.AllEquipment.NetControl && Connected)
                {
                    ConnectTCP();
                }
                /*                if (!Connected)
                                {
                                    // CloseCenect();
                                    if ((ConnectTCP()) && (StartMonitor()))
                                    { }
                                    else if (OnReconnectFailed != null)
                                        OnReconnectFailed.Invoke();
                                }*/
                // Thread.Sleep(ReconnectTime);
            }

        }

    }




}//all


