using System;
using System.Collections.Generic;
using System.Linq;
using System.Text; 
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace Modbus
{
    public class modbusTCPServer
    {
       private static byte[] result = new byte[1024];
       private static int myProt;   //端口
       private static Socket serverSocket;
       private static Socket clientSocket;
       private static string _sendName;//发送内容
       public static string _RXName;//接收内容
       public static string _RXName1;//接收内容1

       public void ServerLink(string ipName, string sendName, int myProt1)
        {
            myProt = myProt1;
            //服务器IP地址
            IPAddress ip = IPAddress.Parse(ipName);
            serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.Bind(new IPEndPoint(ip, myProt));  //绑定IP地址：端口
            serverSocket.Listen(10);    //设定最多10个排队连接请求
            _sendName = sendName;
            Console.WriteLine("启动监听{0}成功", serverSocket.LocalEndPoint.ToString());
            //通过Clientsoket发送数据
            Thread myThread = new Thread(ListenClientConnect);
            myThread.Start();
        }
            

        // <summary>
        /// 监听客户端连接
        /// </summary>
        private static void ListenClientConnect()
        {
            while (true)
            {
               clientSocket    = serverSocket.Accept();
               clientSocket.Send(Encoding.ASCII.GetBytes(_sendName));
               Thread receiveThread = new Thread(ReceiveMessage);
               receiveThread.Start(clientSocket); // 线程间传参
            }
        }
        //GPS        $GPRMC,131913.000,A,3029.64972,N,11423.62352,E,0.00,0.00,200617,,,A*67 
         //if(NULL != (ptr=strstr(buff,"$GPRMC")))         sscanf(ptr,"$GPRMC,%d.000,%c,%f,N,%f,E,%f,%f,%d,,,%c*",&(gps_data->time),&(gps_data->pos_state),&(gps_data->latitude),&(gps_data->longitude),&(gps_data->speed),&(gps_data->direction),&(gps_data->date),&(gps_data->mode));else if(NULL != (ptr=strstr(buff,"$GNRMC")))         sscanf(ptr,"$GNRMC,%d.000,%c,%f,N,%f,E,%f,%f,%d,,,%c*",&(gps_data->time),&(gps_data->pos_state),&(gps_data->latitude),&(gps_data->longitude),&(gps_data->speed),&(gps_data->direction),&(gps_data->date),&(gps_data->mode));


        //private void timer2_Tick(object sender, EventArgs e)
        //{
        //    int i = str.IndexOf("$GPRMC");
        //    if (i > -1)
        //    {
        //        int j = str.IndexOf("\r\n", i);

        //        if (j > -1)
        //        {
        //            string GPS_text = str.Substring(i, j - i);

        //            if (GPS_text.Length > 60)
        //            {
        //                string[] GPS_info = GPS_text.Split(',');

        //                /************时间********************/
        //                a = Convert.ToInt32(Convert.ToDouble(GPS_info[1]));
        //                // a = GPS_info[1];
        //                hour = a / 10000;
        //                min = a % 10000 / 100;
        //                sec = a % 100;
        //                if (16 <= hour && hour <= 24)
        //                {
        //                    hour = 8 - (24 - hour);
        //                }
        //                else
        //                {
        //                    hour = hour + 8;
        //                }

        //                // label6.Text = GPS_info[1];
        //                label6.Text = hour.ToString().PadLeft(2, '0') + ":" + min.ToString().PadLeft(2, '0') + ":" + sec.ToString().PadLeft(2, '0');

        //                /*******经度**************/
        //                b = Convert.ToDouble(GPS_info[5].ToString());
        //                lat1 = (int)b / 100;
        //                lat2 = (int)b % 100;
        //                lat3 = (int)(b % 1 * 60 * 100);
        //                lat = lat1.ToString() + "." + lat2.ToString() + lat3.ToString();
        //                label7.Text = lat + GPS_info[4];
        //                // label7.Text = GPS_info[5];

        //                /*****************纬度**************/
        //                c = Convert.ToDouble(GPS_info[3].ToString());
        //                lon1 = (int)c / 100;
        //                lon2 = (int)c % 100;
        //                lon3 = (int)(c % 1 * 60 * 100);
        //                lon = lon1.ToString() + "." + lon2.ToString() + lon3.ToString();
        //                label11.Text = lon + GPS_info[6];
        //                //  label11.Text = GPS_info[6];
        //                /************日期*****************/
        //                d = Convert.ToInt32(GPS_info[9]);
        //                day = d / 10000;
        //                mon = d % 10000 / 100;
        //                year = d % 100 + 2000;
        //                label12.Text = year.ToString() + "-" + mon.ToString().PadLeft(2, '0') + "-" + day.ToString().PadLeft(2, '0');
        //                //label12.Text = GPS_info[9];
        //                str = "";

        //            }


        //        }

        //    }

        //}

        private static void ReceiveMessage(object clientSocket)
        {
            Socket myClientSocket = (Socket)clientSocket;
            while (true)
            {
                try
                {
                    //通过clientSocket接收数据
                    int receiveNumber = myClientSocket.Receive(result);
                    _RXName = myClientSocket.RemoteEndPoint.ToString();
                    _RXName1 = Encoding.ASCII.GetString(result, 0, receiveNumber);
                 }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    myClientSocket.Shutdown(SocketShutdown.Both);
                    myClientSocket.Close();
                    break;
                }
            }
        }
    }
}
