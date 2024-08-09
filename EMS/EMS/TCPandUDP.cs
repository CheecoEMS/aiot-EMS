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
using Org.BouncyCastle.Bcpg;
using MySqlX.XDevAPI.Common;
using System.Collections.Concurrent;

namespace Modbus
{
    public class SocketWrapper
    {
        private volatile Socket socket;
        private static ILog log = LogManager.GetLogger("SocketWrapper");
        private readonly object socketLock = new object();
        public volatile int Max_Size = 1024;

        public SocketWrapper(Socket socket)
        {
            this.socket = socket;
        }

        private void DestorySocket(Socket aSocket)
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
                }
            }
            catch (SocketException ex)
            {
                log.Error("DestorySocket: " + ex.Message);
            }
            catch (Exception ex)
            {
                log.Error("DestorySocket: " + ex.Message);
            }
        }

        public bool Send(byte[] data)
        {
            lock (socketLock)
            {
                try
                {
                    int res = 0;
                    if (socket != null)
                    {
                        res = socket.Send(data);
                        return res > 0;
                    }
                    else
                    {
                        return false;
                    }

                }
                catch (SocketException ex)
                {
                    DestorySocket(socket);
                    return false;
                }
                catch (Exception ex)
                {
                    DestorySocket(socket);
                    return false;
                }
            }

        }

        public byte[] Receive()
        {
            byte[] buffer = new byte[Max_Size];
            lock (socketLock)
            {
                try
                {
                    int receiveNumber = 0;
                    if (socket != null)
                    {
                        receiveNumber = socket.Receive(buffer);
                       // byte[] recdata = buffer.Take(receiveNumber).ToArray();
                        byte[] recdata = buffer.Cast<byte>().Take(receiveNumber).ToArray();
                        return recdata;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (SocketException ex)
                {
                    DestorySocket(socket);
                    return null;
                }
            }
        }

        public void CloseSocket()
        {
            lock (socketLock)
            {
                DestorySocket(socket);
                socket = null; // 在这里设置 null，以确保 SocketWrapper 中的 socket 字段也被清理  
            }
        }
    }


    public class ClientManager
    {
        private static ILog log = LogManager.GetLogger("ClientManager");

        public void AddClientOverwrite(int ID, SocketWrapper clientWrapper, ConcurrentDictionary<int, SocketWrapper> clientMap)
        {
            // 使用 AddOrUpdate 方法尝试添加或更新键值对  
            // 如果键不存在，则添加新键值对  
            // 如果键已存在，则使用提供的值（这里是 clientWrapper）更新旧值  
            // 这里的 addValueFactory 实际上并未使用，因为我们直接传递了 clientWrapper  
            clientMap.AddOrUpdate(ID, clientWrapper, (key, existingVal) => clientWrapper);
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
        public delegate void OnReceiveDataEventDelegate2(byte[] strData);//建立事件委托
        public event OnReceiveDataEventDelegate2 OnReceiveDataEvent2;//收到数据的事件
       
        // 定义Soket发送和接收 
        public Socket ServerSocket_502 = null;//服务器绑定端口初始化socket对象
        // 定义104-Soket发送和接收 
        public Socket ServerSocket_2404 = null;//服务器绑定端口初始化socket对象

        public volatile SocketWrapper socketWrapper104 = null; //针对104


        //服务器端的IP与端口 
        private IPEndPoint ServerIPE = null;
        public int LocalPort = 0;

        //Soket Sever Connnect监听线程
        private Thread MonitorThread;
        
        //104数据接收与发送 
        private volatile Thread ClientRecThread = null;//104：socket接收线程
        //private byte[] RecData = new byte[1024];//104：socket接收缓冲区
        //private static readonly object sendLock = new object();//针对104 server发送锁

        //tcp
        public ClientManager clientManager = null;
        public static ConcurrentDictionary<int, SocketWrapper> clientMap = new ConcurrentDictionary<int, SocketWrapper>();//保存502端口接入的所有从机

        private volatile CancellationTokenSource cts;//104：主机是否开启监听线程的token的source

        private static ILog log = LogManager.GetLogger("TCPServerClass");


        private void CheckBackground()
        {
            if (cts != null)
            {
                CancleCTS(); // 请求取消当前的接收数据操作  
                WaitThreadEnd();// 等待接收线程终止
            }

            if (socketWrapper104 != null)
            {
                socketWrapper104.CloseSocket();
                socketWrapper104 = null;
            }
        }

        /// <summary>
        /// 等待线程终止
        /// </summary>
        /// <param name="athread"></param>
        private void WaitThreadEnd()
        {
            if (ClientRecThread != null)
            {
                ClientRecThread.Join(); // 等待接收线程终止
            }
        }

        /// <summary>
        /// 停止CTS关联线程运行
        /// </summary>
        /// <param name="cts"></param>
        private void CancleCTS()
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
        private void DisposeCTS()
        {
            if (cts != null)
            {
                try
                {
                    cts.Dispose(); // 释放CancellationTokenSource资源  
                    cts = null; // 将cts设置为null，以防止后续误用
                }
                catch (ObjectDisposedException ex)
                {
                    log.Error("DisposeCTS: " + ex.Message);
                }
            }
        }

        public bool TCPServerIni(int aLocadPort)
        {
            try
            {
                switch (aLocadPort)
                {
                    case 2404:
                        //实例化TcpSetver对象  
                        ServerIPE = new IPEndPoint(IPAddress.Any, aLocadPort);
                        ServerSocket_2404 = new Socket(ServerIPE.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        //绑定IP
                        ServerSocket_2404.Bind(ServerIPE);
                        ServerSocket_2404.Listen(10);
                        return true;
                    case 502:
                        //实例化TcpSetver对象  
                        ServerIPE = new IPEndPoint(IPAddress.Any, aLocadPort);
                        ServerSocket_502 = new Socket(ServerIPE.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        //绑定IP
                        ServerSocket_502.Bind(ServerIPE);
                        ServerSocket_502.Listen(10);
                        return true;
                    default:
                        return false;
                }

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
            MonitorThread.Priority = ThreadPriority.Normal;
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
            MonitorThread.Priority = ThreadPriority.Normal;
            MonitorThread.Start();
        }
        //等待连接Soket Server的请求

        //502
        private void WaitConnectRequest502()
        {
            while (true)
            {
                try
                {
                    Socket acceptSocket = ServerSocket_502.Accept();//accept()阻塞方法接收客户端的连接，返回一个连接上的Socket对象           

                    if (OnConectedEvent != null)
                        OnConectedEvent(acceptSocket);

                    //设置从机回复消息的等待时长
                    acceptSocket.ReceiveTimeout = 2000; //2s
                    SocketWrapper socketWrapper = new SocketWrapper(acceptSocket);
                    object socketLock = new object();
                    //发送问询报文
                    int virtualID = AskEmsID(ref socketWrapper);
                    if (virtualID != -1)
                    {
                        log.Error("加入新的virtualID:" + virtualID);

                        if (frmMain.Selffrm.ModbusTcpServer.clientManager != null && clientMap != null)
                        {
                            frmMain.Selffrm.ModbusTcpServer.clientManager.AddClientOverwrite(virtualID, socketWrapper, clientMap);
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
                    Socket acceptSocket = ServerSocket_2404.Accept();//accept()阻塞方法接收客户端的连接，返回一个连接上的Socket对象                                                                
                    //acceptSocket.ReceiveTimeout = 2000; ///设置从机回复消息的等待时长:2s          
                    CheckBackground();//检查是否存在资源未释放
                                      //
                    socketWrapper104 = new SocketWrapper(acceptSocket);
                    cts = new CancellationTokenSource();
                    CancellationToken cancelReceiveToken = cts.Token;

                    //开启接收线程  
                    ClientRecThread = new Thread(() => ReceiveData(cancelReceiveToken));
                    ClientRecThread.IsBackground = true;
                    ClientRecThread.Priority = ThreadPriority.Highest;
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

        //104数据发送
        public bool SendMsg_byte(byte[] msg)
        {
            try
            {
                if (socketWrapper104 != null)
                {
                    if (socketWrapper104.Send(msg))
                    {
                        return true;
                    }
                    else
                    {
                        CancleCTS();
                        return false;
                    }
                }
                else
                {
                    CancleCTS();
                    return false;
                }          
            }
            catch (Exception ex)
            {
                CancleCTS();
                log.Error("SendMsg_byte: " + ex.Message);
                return false;
            }
        }

        //线程监听函数
        private void ReceiveData(CancellationToken cancelReceiveToken)
        {
            while (!cancelReceiveToken.IsCancellationRequested)
            {
                try
                {
                    byte[] recdata = socketWrapper104.Receive();
                    if (recdata != null)
                    {
                        if (OnReceiveDataEvent2 != null)
                            OnReceiveDataEvent2(recdata);
                    }
                }
                catch (SocketException ex)
                {
                    log.Error("Server ReceiveData is false: " + ex.Message);
                    CancleCTS();//通知接受线程终止 
                }
                catch (Exception ex)
                {
                    CancleCTS();//通知接受线程终止
                    log.Error("Server ReceiveData is false: " + ex.Message);
                }
            }
            //释放cts资源（唯一）
            DisposeCTS();
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
        public bool GetUShort(int ID, ref SocketWrapper client, byte CommandType, ushort aRegStart, ushort aRegLength, ref ushort aResult)
        {
            if (client != null)
            {
                ushort[] ResultData = null;//=new byte[100];
                if (Send3MSG(ID, ref client, CommandType, aRegStart, aRegLength, ref ResultData))
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
        public bool Send3MSG(int ID, ref SocketWrapper client, byte CommandType, ushort aRegStart, ushort aRegLength, ref ushort[] values)
        {
            if (client != null)
            {
                byte[] response = null;
                if (!Read3Response(ID, ref client, CommandType, aRegStart, aRegLength, ref response))
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

        private bool Read3Response(int ID, ref SocketWrapper client, byte CommandType, ushort aRegAddr, ushort aRegLength, ref byte[] aResponse)
        {
            byte[] message = ModbusBase.BuildMSG3((byte)ID, CommandType, aRegAddr, aRegLength);

            byte[] response = new byte[5 + 2 * aRegLength];

            if (client != null)
            {
                if (!GetSocketDada(ID, ref client, message, ref response))
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
        public int Send6MSG(int ID, ref SocketWrapper client, byte CommandType, ushort aRegStart, ushort aData)
        {
            if (client != null)
            {
                int result = 0;
                byte[] response = null;
                if (!Read6Response(ID, ref client,  CommandType, aRegStart, aData, ref response))
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

        private bool Read6Response(int ID, ref SocketWrapper client, byte CommandType, ushort aRegAddr, ushort aData, ref byte[] aResponse)
        {
            byte aAddress = 0xFF;
            byte[] message = ModbusBase.BuildMSG6(aAddress, CommandType, aRegAddr, aData);

            byte[] response = new byte[8];

            //Send modbus message to Serial Port:
            if (!GetSocketDada(ID, ref client, message, ref response))
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
        /// socket收发函数
        /// </summary>
        /// <param name="ID"></param>
        /// <param name="clientSocket"></param>
        /// <param name="socketLock"></param>
        /// <param name="aMessage"></param>
        /// <param name="aResponse"></param>
        /// <returns></returns>
        private bool GetSocketDada(int ID, ref SocketWrapper client, byte[] aMessage, ref byte[] aResponse)
        {
            bool bResult = false;
            try
            {
                if (client.Send(aMessage))
                {
                    if (GetSocketResponse(ID, ref client, ref aResponse))
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
                    //发送失败
                    bResult = false;
                }               
            }
            catch (Exception ex)
            {
                bResult = false;
                log.Error("GetSocketDada捕获ex: " + ex.Message);
            }
            return bResult;
        }
        private bool GetSocketResponse(int ID, ref SocketWrapper client, ref byte[] response)
        {
            bool bResult = false;
            try
            {
                byte[] buffer = client.Receive();
                if (buffer != null)
                {
                    Array.Copy(buffer, 0, response, 0, response.Length);
                    bResult = true;
                }
                else
                {
                    bResult = false;
                }
            }
            catch (Exception ex)
            {
                log.Error("GetSocketResponse捕获ex: " + ex.Message);
                bResult = false;
            }
            finally
            {

            }
            return bResult;

        }

        /*********
         * 1.连接时问询
         * 2.下发控制指令
         * 
         * ************/

        //Modbus502
        //主机首次连接问询从机
        public int AskEmsID(ref SocketWrapper client)
        {
            int result = frmMain.Selffrm.ModbusTcpServer.SendAskMSG(0, ref client, 32, 0x6003, 1);
            log.Error("1次问");
            return result;
        }
        public int SendAskMSG(int ID, ref SocketWrapper client, byte CommandType, ushort aRegStart, ushort aData)
        {
            int result = 0;
            byte[] response = null;
            if (!ReadASKResponse(ID, ref client, CommandType, aRegStart, aData, ref response))
            {
                return -1;

            }
            //[11][05][00][AC][FF][00][CRC高][CRC低]
            //返回数据转换，成功元数据返回，失败将不反悔
            if (response !=null)
            {
                result = response[0];
            }
            return result;
        }

        private bool ReadASKResponse(int ID, ref SocketWrapper client, byte CommandType, ushort aRegAddr, ushort aData, ref byte[] aResponse)
        {
            byte aAddress = 0xFF;
            byte[] message = ModbusBase.BuildMSG6(aAddress, CommandType, aRegAddr, aData);

            byte[] response = new byte[7];

            //Send modbus message to Serial Port:
            if (!GetASKDada(ID, ref client, message, ref response))
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

        private bool GetASKDada(int ID, ref SocketWrapper client, byte[] aMessage, ref byte[] aResponse)
        {
            bool bResult= GetASKFreeData(ID, ref client, aMessage, ref aResponse);
            if (bResult)
            {
                return bResult;
            }
            else
            {
                return bResult;
            }
        }
        private bool GetASKFreeData(int ID, ref SocketWrapper client, byte[] aMessage, ref byte[] aResponse)
        {

            bool bResult = false;
            try
            {
                if (client.Send(aMessage))
                {
                    if (GetASKResponse(ID, ref client, ref aResponse))
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
                    //发送失败
                    bResult = false;
                }
            }
            catch (Exception ex)
            {
                log.Error("ex: " + ex.Message);
            }
            return bResult;
        }
        private bool GetASKResponse(int ID,ref SocketWrapper client, ref byte[] response)
        {
            bool bResult = false;
            try
            {
                byte[] buffer = client.Receive();
                if (buffer != null)
                {
                    Array.Copy(buffer, 0, response, 0, response.Length);
                    bResult = true;
                }
                else
                {
                    bResult = false;
                }
            }
            catch (Exception ex)
            {
                bResult = false;
                log.Error("ex: " + ex.Message);
            }
            finally
            {
                
            }
            return bResult;
        }


    }//class

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
                //DBConnection.RecordLOG("云数据", "云端口连接失败", ex.Message.ToString());//记录连接失败
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
                ClientRecThread.Priority = ThreadPriority.Highest;
                ClientRecThread.Start();
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

    }




}//all


