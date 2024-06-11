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

namespace Modbus

{
    public class ClientManager
    {
        //public int count = 1;
        //public IdManager IDmap;
        //public Dictionary<int, Socket> clientMap ;
        public List<int> IDs = new List<int>();
        /*        public ClientManager()
                { 
                    //clientMap = new Dictionary<int, Socket>();
                    //IDmap = new IdManager();
                }*/
        public void AddClient(int ID, Socket clientSocket, ref Dictionary<int, Socket> clientMap)
        {
            clientMap.Add(ID, clientSocket);
            IDs.Add(ID);
            //IDmap.AddId(client.ID, 0);    // client.ID ：虚拟ID，此ID在连接Socket时获得  0：连接后后续主机发送闻讯SN报文获得返回修改
        }
        public void RemoveClient(int ID, ref Dictionary<int, Socket> clientMap)
        {
            clientMap.Remove(ID);
            IDs.Remove(ID);
        }
        public Socket GetClient(int ID, ref Dictionary<int, Socket> clientMap)
        {
            Socket clientSocket = clientMap[ID];
            return clientSocket;
        }
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
                DBConnection.RecordLOG("云数据", "绑定端口失败", ex.Message.ToString());
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
                DBConnection.RecordLOG("云数据", "数据发送失败", ex.Message.ToString());
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
                    //frmMain.WriteLog(e.ToString()); 
                    DBConnection.RecordLOG("云数据", "数据接受异常", ex.Message.ToString());
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
        public Socket ServerSocket = null;
        public Socket ClientSocket = null;
        //服务器端的IP与端口 
        private IPEndPoint ServerIPE = null;
        public int LocalPort = 0;

        //Soket Sever Connnect监听线程
        private Thread MonitorThread;
        //数据接收 
        private Thread ClientRecThread = null;
        private byte[] RecData = new byte[1024];

        //tcp
        public ClientManager clientManager = null;
        public Dictionary<int, Socket> clientMap = null;
        private static ILog log = LogManager.GetLogger("TCPServerClass");

        //tcp:从机超时不回消息，则剔除
        public void DestroyClient(int ID)
        {
            clientManager.RemoveClient(ID, ref frmMain.Selffrm.ModbusTcpServer.clientMap);
        }

        // 销毁Socket对象 
        private static void DestroySocket(Socket socket)
        {
            try
            {
                if (socket.Connected)
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                socket.Close();
            }
            catch
            { }
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


        //  servierIpAddress  服务器iP地址或者域名，sevierPort 服务器监听端口， locadPort 本地监听端口   
        public bool TCPServerIni2(string aServierIpAddress, int aLocadPort)
        {
            try
            {
                //实例化TcpSetver对象  
                ServerIPE = new IPEndPoint(IPAddress.Parse(aServierIpAddress), aLocadPort); //server绑定一个IP和Port
                ServerSocket = new Socket(ServerIPE.AddressFamily, SocketType.Stream, ProtocolType.Tcp);//监听套接字
                //绑定一个IP和Port
                ServerSocket.Bind(ServerIPE);
                ServerSocket.Listen(10);//限制10个
                //ServerSocket.BeginAccept(new AsyncCallback(Accept), server);
                return true;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("绑定端口失败" + ex.Message.ToString());
                return false;
            }
        }

        public bool TCPServerIni( int aLocadPort)
        {
            try
            {
                //实例化TcpSetver对象  
                ServerIPE = new IPEndPoint(IPAddress.Any, aLocadPort);
                //ServerIPE = new IPEndPoint(IPAddress.Parse("192.168.110.130"), aLocadPort);
                ServerSocket = new Socket(ServerIPE.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                //绑定IP
                ServerSocket.Bind(ServerIPE);
                ServerSocket.Listen(10);
                //ServerSocket.BeginAccept(new AsyncCallback(Accept), server);
                return true;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("绑定端口失败" + ex.Message.ToString());
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
                DestroySocket(ClientSocket);
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
                    ClientRecThread.Abort();
                    ClientRecThread = null;

                    if (OnDisconectEvent != null)
                        OnDisconectEvent(ClientSocket);
                    ClientSocket.Shutdown(SocketShutdown.Both);
                    ClientSocket.Close();
                    DestroySocket(ClientSocket);
                    ClientSocket = null;
                }
            }
            catch
            { }

        }

        //等待连接Soket Server的请求

        //502
        private void WaitConnectRequest502()
        {
            try
            {
                while (true)
                {

                    Socket acceptSocket = ServerSocket.Accept();//accept()阻塞方法接收客户端的连接，返回一个连接上的Socket对象
                    try
                    {
                        //CloseClientSocket();
                        
                        //acceptSocket.RemoteEndPoint()l
                        //socket.ReceiveTimeout = timeout;                    
                        
                        if (OnConectedEvent != null)
                            OnConectedEvent(acceptSocket);
                        
                        //ClientSocket = acceptSocket;

                        //设置从机回复消息的等待时长
                        acceptSocket.ReceiveTimeout = 6000; //6s


                        //发送问询报文
                        int virtualID = AskEmsID(acceptSocket);
                        if (virtualID != -1)
                        {
                            log.Debug("加入新的virtualID:" + virtualID);
                            //Socket包装成client加入clientManagaer
                            //Client client = new Client(virtualID , ClientSocket);

                            if (frmMain.Selffrm.ModbusTcpServer.clientManager != null && frmMain.Selffrm.ModbusTcpServer.clientMap != null)
                            {
                                frmMain.Selffrm.ModbusTcpServer.clientManager.AddClient(virtualID, acceptSocket, ref frmMain.Selffrm.ModbusTcpServer.clientMap);
                            }
                            log.Debug("加入完成");
                        }
                    }
                    catch { }

                }

            }
            catch
            {
                // MonitorThread.Abort();
                CloseClientSocket();
            }
        }

        //104
        private void WaitConnectRequest104()
        {
            try
            {
                while (true)
                {

                    //Socket acceptSocket = ServerSocket.Accept();//注释
                    try
                    {
                        Socket acceptSocket = ServerSocket.Accept();//accept()阻塞方法接收客户端的连接，返回一个连接上的Socket对象
                        CloseClientSocket();
                        //acceptSocket.RemoteEndPoint()l
                        //socket.ReceiveTimeout = timeout;                    
                        if (OnConectedEvent != null)
                            OnConectedEvent(acceptSocket);
                        ClientSocket = acceptSocket;
                        //开启接收线程  
                        ClientRecThread = new Thread(ReceiveData);
                        ClientRecThread.IsBackground = true;
                        ulong LpId = SetCpuID(3);
                        SetThreadAffinityMask(GetCurrentThread(), new UIntPtr(LpId));
                        ClientRecThread.Start();
                    }
                    catch(SocketException ex) 
                    {
                        
                    }

                }

            }
            catch
            {
                // MonitorThread.Abort();
                CloseClientSocket();
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

        public bool SendMsg_byte(byte[] msg , Socket aCllientSoket)
        {
            if ((aCllientSoket == null) || (!aCllientSoket.Connected))
                return false;
            try
            {
                ClientSocket.Send(msg);
                return true;
            }
            catch
            {
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
        private void ReceiveData()
        {
            if ((ClientSocket == null) || (!ClientSocket.Connected))
                return;
            string strRecData = "";
            while (true)
            {

                try
                {
                    if (ClientSocket == null)
                    {
                        DestroySocket(ClientSocket);
                        if (ClientSocket != null)
                        {
                            ClientRecThread.Abort();
                            ClientRecThread = null;
                            ClientSocket = null;
                        }
                        return;
                    }

                    //if ()//
                    if (!ClientSocket.Connected)
                    {
                        ClientRecThread.Abort();
                        ClientRecThread = null;
                        ClientSocket = null;
                        return;
                    }

                    int receiveNumber = ClientSocket.Receive(RecData);
                    if (receiveNumber > 0)
                    {
                        //strRecData = Encoding.ASCII.GetString(RecData, 0, receiveNumber);
                        byte[] recdata = RecData.Take(receiveNumber).ToArray();
                        //strRecData = BitConverter.ToString(recdata).Replace("-", "");

                        if (OnReceiveDataEvent2 != null)
                             //OnReceiveDataEvent(ClientSocket, strRecData, ClientSocket.ToString(), LocalPort);
                             OnReceiveDataEvent2(ClientSocket, recdata, ClientSocket.ToString(), LocalPort);
                    }
                }
                catch
                {
                    if (OnDisconectEvent != null)
                        OnDisconectEvent(ClientSocket);
                    try
                    {
                        ClientSocket.Shutdown(SocketShutdown.Both);
                        ClientSocket.Close();
                        DestroySocket(ClientSocket);
                        ClientRecThread.Abort();
                        ClientRecThread = null;
                        ClientSocket = null;
                    }
                    catch { }
                    break;
                }
            } //while
        }//func


        //Modbus502
        //主机首次连接问询从机
        public int AskEmsID(Socket ClientSocket)
        {
            IPEndPoint localEndPoint = (IPEndPoint)ClientSocket.LocalEndPoint;
            log.Debug("Local IP address: " + localEndPoint.Address);
            log.Debug("Local port: " + localEndPoint.Port);

            // Get the remote endpoint information
            IPEndPoint remoteEndPoint = (IPEndPoint)ClientSocket.RemoteEndPoint;
            log.Debug("Remote IP address: " + remoteEndPoint.Address);
            log.Debug("Remote port: " + remoteEndPoint.Port);

            int result = frmMain.Selffrm.ModbusTcpServer.SendAskMSG(0, ClientSocket, 32, 0x6003, 1);
            log.Debug("1次问");
            /*            int result1 = frmMain.Selffrm.ModbusTcpServer.SendAskMSG(ClientSocket, 32, 0x6003, 1);
                        log.Debug("2次问");*/
            return result;
        }
        public int SendAskMSG(int ID, Socket clientSocket, byte CommandType, ushort aRegStart, ushort aData)
        {
            int result = 0;
            byte[] response = null;
            if (!ReadASKResponse(ID, clientSocket, CommandType, aRegStart, aData, ref response))
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

        private bool ReadASKResponse(int ID, Socket clientSocket, byte CommandType, ushort aRegAddr, ushort aData, ref byte[] aResponse)
        {
            byte aAddress = 0xFF;
            byte[] message = ModbusBase.BuildMSG6(aAddress, CommandType, aRegAddr, aData);

            byte[] response = new byte[8];

            //Send modbus message to Serial Port:
            if (!GetASKDada(ID, clientSocket, message, ref response))
                return false;

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

        private bool GetASKDada(int ID, Socket clientSocket, byte[] aMessage, ref byte[] aResponse)
        {

            bool bResult = false;
            bResult= GetASKFreeData(ID, clientSocket, aMessage, ref aResponse);
            if (bResult)
            {
                return bResult;
            }
            else
            {
                // DBConnection.RecordLOG("通讯异常", "反应超时", "无法判断具体设备");
                return bResult;
            }

        }
        private bool GetASKFreeData(int ID, Socket clientSocket, byte[] aMessage, ref byte[] aResponse)
        {
            bool bResult = false;

            int res = clientSocket.Send(aMessage);
            if (GetASKResponse(ID, clientSocket, ref aResponse))
            {
                bResult = true;

            }
            else
            {
                bResult = false;
                //消息发送失败
                //MsgReturn = false;
            }

            return bResult;
        }
        private bool GetASKResponse(int ID, Socket clientSocket, ref byte[] response)
        {
            bool bResult = false;
            try
            {
                int receiveNumber = clientSocket.Receive(RecData);

                log.Debug("receive:" + receiveNumber);

                if (receiveNumber > 0)
                {
                    Array.Copy(RecData, 0, response, 0, receiveNumber);
                }
                bResult = true;
            }
            catch (SocketException ex)
            {
                bResult = false;
                //frmMain.ShowDebugMSG(ex.ToString());
                if (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    // Log the timeout
                    log.Debug("接收超时");
                    if (ID != 0)//0是从机首次连接时，主机发送的问询报文
                    {
                        frmMain.Selffrm.ModbusTcpServer.DestroyClient(ID);
                        DestroySocket(clientSocket);
                        log.Debug("剔除"+ID+"号从机");
                    }
                }
                else
                {
                    log.Debug("物联断开或从机EMS下线");
                    if (ID != 0)//0是从机首次连接时，主机发送的问询报文
                    {
                        frmMain.Selffrm.ModbusTcpServer.DestroyClient(ID);
                        DestroySocket(clientSocket);
                        log.Debug("剔除"+ID+"号从机");
                    }
                }
            }

            return bResult;
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
                log.Debug("报文插入成功");
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
        //SetThreadAffinityMask: Set hThread run on logical processer(LP:) dwThreadAffinityMask
        [DllImport("kernel32.dll")]
        static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

        //Get the handler of current thread
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentThread();

        public int ID;
        public bool Connected = false;  //从机判断主机是否离线，断线重连
        public bool Disposed = false;
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
        private static byte[] bytes = new byte[1024];
        private byte[] RecData = new byte[8];//private byte[] RecData = new byte[1024];
        public Socket clientSocket = null;
        private bool IsMonitoring = true;
        private IPAddress ipAddress;
        private int iSevierPort;
        private IPEndPoint ipEndpoint;
        private Thread ClientRecThread = null;
        public int ReconnectTime = 1000;//ms
        private static ILog log = LogManager.GetLogger("TCPClientClass");
        public SocketBufferQueue ClientBuffer = new SocketBufferQueue();


        ~TCPClientClass()
        {
            //ClientRecThread.Abort();
        }

        //  servierIpAddress  服务器iP地址或者域名，sevierPort 服务器监听端口
        public void TCPClientIni(string aServierIpAddress, int aSevierPort)
        {
            // if (UdpClass.IsIpaddress(aServierIpAddress) == false)
            //     return false;
            //设定服务器IP地址  
            //IPAddress ip = IPAddress.Parse(aServierIpAddress);
            Disposed = false;
            iSevierPort = aSevierPort;
            ipAddress = IPAddress.Parse(aServierIpAddress);
            ipEndpoint = new IPEndPoint(ipAddress, iSevierPort);
            ConnectTCP();
        }

        public bool ConnectTCP()
        {
            log.Debug("ConnectTCP");
            IsMonitoring = false;
            if (clientSocket != null)
            {
                CloseCenect();
                //clientSocket.Dispose();
            }
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                if (clientSocket != null)
                {
                    //clientSocket.Connect(new IPEndPoint(IPAddress.Parse("192.168.186.2"), 502)); //链接服务器IP与端口
                    clientSocket.Connect(ipEndpoint); //链接服务器IP与端口  
                    clientSocket.Blocking = true;
                    clientSocket.ReceiveTimeout = 6000;
                    clientSocket.SendTimeout = 6000;
                    if (OnConectedEvent != null)
                        OnConectedEvent.Invoke();
                    Connected = true;
                    return true;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                DBConnection.RecordLOG("云数据", "云端口连接失败", ex.Message.ToString());//记录连接失败
                Connected = false; ;
                return false;
            }
        }

        //关闭链接
        public void CloseCenect()
        {
            IsMonitoring = false;
            Connected = false;
            if ((clientSocket != null) && (clientSocket.Connected))
            {
                clientSocket.Shutdown(SocketShutdown.Both);
                Thread.Sleep(100);
                //clientSocket.Close();
                //clientSocket.Dispose();                
            }
            clientSocket = null;
            GC.Collect();
            //GC.SuppressFinalize(this);
        }


        //开始监控
        public bool StartMonitor()
        {
            try
            {
                //ConnectTCP(); 
                //实例化等待连接的线程
                ClientRecThread = new Thread(ReceiveData);
                ClientRecThread.IsBackground = true;
                ClientRecThread.Start();
                IsMonitoring = true;
                if (OnConectedEvent != null)
                    OnConectedEvent.Invoke();
                return true;
            }
            catch
            {
                IsMonitoring = false;
                return false;
            }
        }

        //接收数据做服务 
        private void ReceiveData()
        {
            while (true)
            {
                //log.Debug("正在监听");
                try
                {
                    int receiveNumber = clientSocket.Receive(RecData);
                    if (receiveNumber > 0)
                    {
                        byte[] data = new byte[receiveNumber];
                        Array.Copy(RecData, 0, data, 0, receiveNumber);
                        string hexString = BitConverter.ToString(data);
                        log.Debug("收到TCP消息：" + hexString);
                        OnReceiveDataEvent2(clientSocket, data);
                    }
                }
                catch { }

            }
        }//func


        //发送信息
        public bool SendMSG(byte[] aByteData)
        {
            if ((clientSocket == null) || (!clientSocket.Connected))
            {
                CloseCenect();
                Thread.Sleep(100);
                ConnectTCP();
                //StartMonitor();
                //return false;
            }

            if (!IsMonitoring)
                StartMonitor();
            try
            {
                clientSocket.Send(aByteData);
                return true;
            }
            catch (Exception ex)
            {
                if (clientSocket.Connected)
                {
                    clientSocket.Shutdown(SocketShutdown.Both);
                    clientSocket.Close();
                }
                if (OnDisconectEvent != null)
                    OnDisconectEvent.Invoke();
                DBConnection.RecordLOG("云数据", "数据发送失败", ex.Message.ToString());
                Connected = false;
                clientSocket = null;
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
                Thread.Sleep(ReconnectTime * 1000);
                if (!Connected)
                {
                    // CloseCenect();
                    if ((ConnectTCP()) && (StartMonitor()))
                    { }
                    else if (OnReconnectFailed != null)
                        OnReconnectFailed.Invoke();
                }
                // Thread.Sleep(ReconnectTime);
            }

        }

    }




}//all


