using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text; 
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace Modbus
{
     public class modbusUDP
    {
         
        /// <summary>
        /// 获取本地IP
        /// </summary>
        private void label1_Click(object sender, EventArgs e)
        {
            string ip = IPAddress.Any.ToString();
            //textBox1.Text = ip;
        }
        Socket server;
        private void button1_Click(object sender, EventArgs e)
        {
            //1.创建服务器端 
            server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            //2.创建手机卡
            IPAddress iP = IPAddress.Parse("192.169.1.1");
            IPEndPoint endPoint = new IPEndPoint(iP, int.Parse("8001"));
            //3. 绑定端口号和IP 
            server.Bind(endPoint);
           // listBox1.Items.Add("服务器已经成功开启!");
            //开启接收消息线程
            Thread t = new Thread(ReciveMsg);
            t.IsBackground = true;
            t.Start();
        }
        /// <summary>
        /// 向特定ip的主机的端口发送数据
        /// </summary>te
        void SendMsg()
        {
            //string hostName = Dns.GetHostName();   //获取本机名
            //IPHostEntry localhost = Dns.GetHostEntry(hostName);   //获取IPv6地址
            //IPAddress localaddr = localhost.AddressList[0];
            EndPoint point = new IPEndPoint(IPAddress.Parse("127.0.0.1"), int.Parse("8001"));
            string msg = "";// textBox3.Text;
            server.SendTo(Encoding.UTF8.GetBytes(msg), point);
        }
        /// <summary>
        /// 接收发送给本机ip对应端口号的数据
        /// </summary>
        void ReciveMsg()
        {
            while (true)
            {
                EndPoint point = new IPEndPoint(IPAddress.Any, 0);//用来保存发送方的ip和端口号
                byte[] buffer = new byte[1024 * 1024];
                int length = server.ReceiveFrom(buffer, ref point);//接收数据报
                string message = Encoding.UTF8.GetString(buffer, 0, length);
               // listBox1.Items.Add(point.ToString() + "：" + message);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //if (textBox3.Text != "")
            {
                //开启发送消息线程
                Thread t2 = new Thread(SendMsg);
                t2.Start();
            }
        }
    }
} 