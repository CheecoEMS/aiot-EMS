using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Modbus
{
    class ModbusBase
    {
        //验证码
        #region CRC Computation
        static private void GetCRC(byte[] message, ref byte[] CRC)
        {
            //Function expects a modbus message of any length as well as a 2 byte CRC array in which to 
            //return the CRC values:

            ushort CRCFull = 0xFFFF;
            byte CRCHigh = 0xFF, CRCLow = 0xFF;
            char CRCLSB;

            for (int i = 0; i < (message.Length) - 2; i++)
            {
                CRCFull = (ushort)(CRCFull ^ message[i]);

                for (int j = 0; j < 8; j++)
                {
                    CRCLSB = (char)(CRCFull & 0x0001);
                    CRCFull = (ushort)((CRCFull >> 1) & 0x7FFF);

                    if (CRCLSB == 1)
                        CRCFull = (ushort)(CRCFull ^ 0xA001);
                }
            }
            CRC[1] = CRCHigh = (byte)((CRCFull >> 8) & 0xFF);
            CRC[0] = CRCLow = (byte)(CRCFull & 0xFF);
        }
        #endregion

        //编译Modebus的发送数据
        //address设备地址，type功能类型3/16，start开始的寄存器，registers长度，编译好的信息message
        #region Build Message
        static public void BuildMessage(byte address, byte type, ushort regstart, ushort reglength, ref byte[] message)
        {
            //Array to receive CRC bytes:
            byte[] CRC = new byte[2];
            //byte[] 
           // message=new byte[100];
            message[0] = address;
            message[1] = type;
            message[2] = (byte)(regstart >> 8);
            message[3] = (byte)regstart;
            message[4] = (byte)(reglength >> 8);
            message[5] = (byte)reglength;

            GetCRC(message, ref CRC);
            message[message.Length - 2] = CRC[0];
            message[message.Length - 1] = CRC[1];
        }
        #endregion

        //检测返回数据是否符合
        #region Check Response
        static public bool CheckResponse(byte[] response)
        {
            //Perform a basic CRC check:
            byte[] CRC = new byte[2];
            GetCRC(response, ref CRC);
            if (CRC[0] == response[response.Length - 2] && CRC[1] == response[response.Length - 1])
                return true;
            else
                return false;
        }
        #endregion



    }
}
