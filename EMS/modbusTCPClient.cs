using System;
using System.Collections.Generic;
using System.Linq;
using System.Text; 
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Modbus
{
   public class modbusTCPClient
    {
        private static byte[] result = new byte[1024];
        private static Socket clientSocket;

        public string ClientRT(string ipName,int ipo)
        {          
            //设定服务器IP地址  
            IPAddress ip = IPAddress.Parse(ipName);
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                clientSocket.Connect(new IPEndPoint(ip, ipo)); //配置服务器IP与端口  
                //Console.WriteLine("连接服务器成功");
                return "OK";
            }
            catch
            {
                //Console.WriteLine("连接服务器失败，请按回车键退出！");
                return "NG";
            }
        }
        public string ClientRT2(string sendMessage)
        {
            string Name = "NG";
                try
                {
                    Thread.Sleep(50);    //等待  
                    clientSocket.Send(Encoding.ASCII.GetBytes(sendMessage));
                    //通过clientSocket接收数据  
                    int receiveLength = clientSocket.Receive(result);
                    Name = Encoding.ASCII.GetString(result, 0, receiveLength);
                }
                catch
                {
                    //clientSocket.Shutdown(SocketShutdown.Both);
                    //clientSocket.Close();
                    //break;
                }
            return Name;
        }
        public void CloseIP()
        {
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
        }
        public string ClientRT1( string sendMessage)
        {
            string Name = "NG";
            //设定服务器IP地址  
            IPAddress ip = IPAddress.Parse("192.168.255.1");
            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                clientSocket.Connect(new IPEndPoint(ip, 80)); //配置服务器IP与端口  
                Console.WriteLine("连接服务器成功");
            }
            catch
            {
                Console.WriteLine("连接服务器失败，请按回车键退出！");
                return "NG";
            }
            //通过 clientSocket 发送数据  
            try
            {
                Thread.Sleep(50);    //等待  
                clientSocket.Send(Encoding.ASCII.GetBytes(sendMessage));
                //通过clientSocket接收数据  
                int receiveLength = clientSocket.Receive(result);
                Name = Encoding.ASCII.GetString(result, 0, receiveLength);
            }
            catch
            {
                clientSocket.Shutdown(SocketShutdown.Both);
                clientSocket.Close();
            }
            return Name;
        }
    }
}
