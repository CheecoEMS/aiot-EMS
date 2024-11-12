using System;
using System.Linq;

namespace EMS
{
    class iec104Base
    {
        //
        public static byte[] SuperviseCmd(int receiveSerialNum)
        {
            var data = new byte[255];
            var i = 0;
            data[i++] = 104;
            data[i++] = 0x4; //长度
            data[i++] = 1;
            data[i++] = 0;
            data[i++] = (byte)((receiveSerialNum & 0b1111111) << 1);//接收序列号  
            data[i++] = (byte)((receiveSerialNum >> 7) & 0b11111111);
            return data.Take(i).ToArray();
        }

        /// <summary>
        /// 启动命令
        /// </summary>
        /// <returns></returns>
        public static byte[] UStartCmd()
        {
            var data = new byte[255];

            var i = 0;
            data[i++] = 104;
            data[i++] = 0x4; //长度
            data[i++] = 07;
            data[i++] = 0;
            data[i++] = 0;
            data[i++] = 0;
            return data.Take(i).ToArray();
        }


        public static byte[] TimeCmd(int sendSerialNum, int receiveSerialNum)
        {
            var data = new byte[255];

            int i = 0;
            data[i++] = 104;
            data[i++] = 0x14; //长度
            data[i++] = (byte)((sendSerialNum & 0b1111111) << 1);//发送序列号2位
            data[i++] = (byte)((sendSerialNum >> 7) & 0b11111111);
            data[i++] = (byte)((receiveSerialNum & 0b1111111) << 1);//接收序列号2位
            data[i++] = (byte)((receiveSerialNum >> 7) & 0b11111111);

            //u帧
            data[i++] = 0x67; //类型103=时钟同步
            data[i++] = 1; //可变结构限定
            data[i++] = 0x06; //传动原因=6=激活
            data[i++] = 0;
            data[i++] = 0; //公共地址
            data[i++] = 0;
            data[i++] = 0; //信息对象地址
            data[i++] = 0;
            data[i++] = 0;
            //时间=7字节表示。毫秒到年
            byte[] tm = Time2Byte(DateTime.Now);
            data[i++] = tm[0];
            data[i++] = tm[1];
            data[i++] = tm[2];
            data[i++] = tm[3];
            data[i++] = tm[4];
            data[i++] = tm[5];
            data[i++] = tm[6];
            return data.Take(i).ToArray();
        }

        /// <summary>
        /// Cp56Time2a格式的7位字节---->时间
        /// </summary>
        /// <param name="bts"></param>
        /// <returns></returns>
        public static DateTime Byte2Time(byte[] bts)
        {
            var year = bts[6] + 2000;
            var month = bts[5];
            var week = bts[4] & 0b11100000;
            var day = bts[4] & 0b00011111;
            var hour = bts[3] & 0b00011111;
            var minute = bts[2] & 0b00111111;
            var ms = BitConverter.ToInt32(new byte[] { bts[0], bts[1], 0, 0 }, 0);
            var sec = ms / 1000;
            var millionSec = ms % 1000;
            var dt = new DateTime(year, month, day, hour, minute, sec);
            var newDt = dt.AddMilliseconds(millionSec);
            return newDt;
        }

        /// <summary>
        /// 时间---->Cp56Time2a格式的7位字节
        /// </summary>
        /// <param name="now"></param>
        /// <returns></returns>
        public static byte[] Time2Byte(DateTime now)
        {
            byte[] bt = new byte[7];
            //年
            bt[6] = (byte)(now.Year - 2000);
            //月
            bt[5] = (byte)now.Month;

            //星期（3位）+日（5位）
            var week = (int)now.DayOfWeek < 7 ? (int)now.DayOfWeek : 7;
            bt[4] = (byte)((week << 4) + (now.Day & 0b00011111));
            //小时
            bt[3] = (byte)now.Hour;
            //分钟
            bt[2] = (byte)now.Minute;

            var ms = BitConverter.GetBytes(now.Second * 1000 + now.Millisecond);
            //毫秒高 
            bt[1] = ms[1];
            //毫秒低
            bt[0] = ms[0];
            return bt;
        }
    }
}
