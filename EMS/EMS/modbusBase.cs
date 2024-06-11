namespace Modbus
{
    /// <summary>
    /// modbus功能吗
    /// </summary>
    enum ModbusTcpFunctionCode
    {
        /// <summary>
        /// 读单个线圈
        /// </summary>
        ReadCoilStatus = 1,
        //[11][01][00][13][00][25][CRC低][CRC高]
        //回[11][01][05][CD][6B][B2][0E][1B] [CRC高] [CRC低]

        /// <summary>
        /// 读单个输入状态
        /// </summary>
        ReadInputStatus = 2,
        /// <summary>
        /// 读保持寄存器
        /// </summary>
        ReadHoldingRegister = 3,
        //[11][03][00][6B][00][03] [CRC高][CRC低]
        //回[11][03][06][02][2B][00][00][00][64] [CRC高] [CRC低]
        /// <summary>
        /// 读输入寄存器
        /// </summary>
        ReadInputRegister = 4,
        /// <summary>
        /// 写单个线圈
        /// </summary>
        WriteSingleCoil = 5,
        //[11][05][00][AC][FF][00][CRC高][CRC低]
        /// <summary>
        /// 写单个寄存器
        /// </summary>
        WriteSingleRegister = 6,
        //[11][06][00][01][00][03] [CRC高] [CRC低]
        /// <summary>
        /// 写多个线圈
        /// </summary>
        WriteMultipleCoil = 15,
        /// <summary>
        /// 写多个寄存器
        /// </summary>
        WriteMultipleRegister = 16,
        //[11][16][00][01][00][01][00][05] [CRC高] [CRC低]
    }

    class ModbusBase
    {
        #region CRC Computation验证码
        static private void GetCRC(byte[] message, ref byte[] CRC)
        {
            //Function expects a modbus message of any length as well as a 2 byte CRC array in which to 
            //return the CRC values 
            ushort CRCFull = 0xFFFF;
            //byte CRCHigh = 0xFF, CRCLow = 0xFF;
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
            CRC[1] = /*CRCHigh = */(byte)((CRCFull >> 8) & 0xFF);
            CRC[0] = /* CRCLow = */(byte)(CRCFull & 0xFF);
        }
        #endregion

        #region Check Response检测返回数据是否符合
        static public bool CheckResponse(byte[] response)
        {
            try
            {
                //Perform a basic CRC check:
                byte[] CRC = new byte[2];
                GetCRC(response, ref CRC);
                if (CRC[0] == response[response.Length - 2] && CRC[1] == response[response.Length - 1])
                    return true;
                else
                    return false;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        //编译读取或者控制Modebus的发送数据
        //address设备地址，type功能类型22,27??????，start开始的寄存器，registers长度，编译好的信息message
        #region Build Message Clound
        static public byte[] BuildCloundMSG(byte aAddress, byte aType, byte aDataLength, short[] aValues)
        {
            //Array to receive CRC bytes:
            byte[] CRC = new byte[2];
            int ataLength = aValues.Length;
            //Message is 1 addr + 1 fcn + 2 Count +  2 * reglenth  vals + 2 CRC
            byte[] message = new byte[5 + 2 * ataLength];//aDataLength
            // message=new byte[100];
            message[0] = aAddress;
            message[1] = aType;
            //Add bytecount to message:
            message[2] = (byte)((ataLength * 2));   //aDataLength
            //Put write values into message prior to sending:
            for (int i = 0; i < ataLength; i++)  //aDataLength
            {
                message[3 + 2 * i] = (byte)(aValues[i] >> 8);
                message[4 + 2 * i] = (byte)(aValues[i]);
            }
            GetCRC(message, ref CRC);
            message[message.Length - 2] = CRC[0];
            message[message.Length - 1] = CRC[1];
            return message;
        }
        #endregion

        //编译读取或者控制Modebus3的发送数据
        //address设备地址，type功能类型3/16，start开始的寄存器，registers长度，编译好的信息message
        #region Build Message 1\2\3
        static public byte[] BuildMSG3(byte aAddress, byte aType, ushort aRegstart, ushort aReglength)
        {
            byte[] message = new byte[8];
            //Array to receive CRC bytes:
            byte[] CRC = new byte[2];
            //byte[] 
            // message=new byte[100];
            message[0] = aAddress;
            message[1] = aType;
            message[2] = (byte)(aRegstart >> 8);
            message[3] = (byte)aRegstart;
            message[4] = (byte)(aReglength >> 8);
            message[5] = (byte)aReglength;

            GetCRC(message, ref CRC);
            message[message.Length - 2] = CRC[0];
            message[message.Length - 1] = CRC[1];
            return message;
        }
        #endregion

        //编译读取或者控制Modebus5的发送数据
        #region Build Message 5
        static public byte[] BuildMSG5(byte aAddress, byte aType, ushort aRegstart, bool aData)
        {
            byte[] message = new byte[8];
            //Array to receive CRC bytes:
            byte[] CRC = new byte[2];
            //byte[] 
            // message=new byte[100];
            message[0] = aAddress;
            message[1] = aType;
            message[2] = (byte)(aRegstart >> 8);
            message[3] = (byte)aRegstart;
            if (aData)
                message[4] = (byte)(0xFF);
            else
                message[4] = (byte)(0);
            message[5] = (byte)(0);
            GetCRC(message, ref CRC);
            message[message.Length - 2] = CRC[0];
            message[message.Length - 1] = CRC[1];
            return message;
        }
        #endregion

        //编译读取或者控制Modebus6的发送数据
        #region Build Message 6
        static public byte[] BuildMSG6(byte aAddress, byte aType, ushort aRegstart, ushort aData)
        {
            byte[] message = new byte[8];
            //Array to receive CRC bytes:
            byte[] CRC = new byte[2];
            //byte[] 
            // message=new byte[100];
            message[0] = aAddress;
            message[1] = aType;
            message[2] = (byte)(aRegstart >> 8);
            message[3] = (byte)aRegstart;
            message[4] = (byte)(aData >> 8);
            message[5] = (byte)aData;
            GetCRC(message, ref CRC);
            message[message.Length - 2] = CRC[0];
            message[message.Length - 1] = CRC[1];
            return message;
        }
        #endregion

        //编译读取或者控制Modebus6的发送数据
        #region Build Message 6
        static public byte[] BuildMSG6(byte aAddress, byte aType, ushort aRegstart, byte[] aData)
        {
            byte[] message = new byte[8];
            //Array to receive CRC bytes:
            byte[] CRC = new byte[2];
            //byte[] 
            // message=new byte[100];
            message[0] = aAddress;
            message[1] = aType;
            message[2] = (byte)(aRegstart >> 8);
            message[3] = (byte)aRegstart;
            message[4] = (byte)(aData[0]);
            message[5] = (byte)aData[1];
            GetCRC(message, ref CRC);
            message[message.Length - 2] = CRC[0];
            message[message.Length - 1] = CRC[1];
            return message;
        }
        #endregion

        //编译读取或者控制Modebus的发送数据
        //address设备地址，type功能类型16，start开始的寄存器，registers长度，编译好的信息message
        #region Build Message 16
        static public byte[] BuildMSG16(byte aAddress, byte aType, ushort aRegstart, ushort aRegLength, short[] aValues)
        {
            //Array to receive CRC bytes:
            byte[] CRC = new byte[2];
            //Message is 1 addr + 1 fcn + 2 aRegStart + 2 reg + 1 count + 2 * reg vals + 2 CRC
            byte[] message = new byte[9 + 2 * aRegLength];
            // message=new byte[100];
            message[0] = aAddress;
            message[1] = aType;
            //addr 2byte
            message[2] = (byte)(aRegstart >> 8);
            message[3] = (byte)aRegstart;
            //length 2byte 
            message[4] = (byte)(aRegLength >> 8);
            message[5] = (byte)aRegLength;
            //Add bytecount to message:
            message[6] = (byte)(aRegLength * 2); //qiao
            //Put write values into message prior to sending:
            for (int i = 0; i < aRegLength; i++) //BitConverter.GetBytes((short)1)
            {
                message[7 + 2 * i] = (byte)(aValues[i] >> 8);
                message[8 + 2 * i] = (byte)(aValues[i]);
            }
            GetCRC(message, ref CRC);
            message[message.Length - 2] = CRC[0];
            message[message.Length - 1] = CRC[1];
            return message;
        }
        #endregion


        //编译读取或者控制Modebus的发送数据
        //address设备地址，type功能类型16，start开始的寄存器，registers长度，编译好的信息message
        #region Build Message 16
        static public byte[] BuildMSG16(byte aAddress, byte aType, ushort aRegstart, ushort aRegLength, byte[] aValues)
        {
            //Array to receive CRC bytes:
            byte[] CRC = new byte[2];
            //Message is 1 addr + 1 fcn + 2 aRegStart + 2 reg + 1 count + 2 * reg vals + 2 CRC
            byte[] message = new byte[9 + 2 * aRegLength];
            // message=new byte[100];
            message[0] = aAddress;
            message[1] = aType;
            //addr 2byte
            message[2] = (byte)(aRegstart >> 8);
            message[3] = (byte)aRegstart;
            //length 2byte 
            message[4] = (byte)(aRegLength >> 8);
            message[5] = (byte)aRegLength;
            //Add bytecount to message:
            message[6] = (byte)(aRegLength * 2);
            //Put write values into message prior to sending:
            for (int i = 0; i < aRegLength; i++) //BitConverter.GetBytes((short)1)
            {
                message[7 + 2 * i] = aValues[2 * i];
                message[8 + 2 * i] = aValues[2 * i + 1];
            }
            GetCRC(message, ref CRC);
            message[message.Length - 2] = CRC[0];
            message[message.Length - 1] = CRC[1];
            return message;
        }
        #endregion


        #region Build BackMessage 1\2\3  one Short
        static public byte[] BuildMSG3Back(byte aAddress, byte aType,  ushort aData)
        {
            byte[] message = new byte[5+ 2];
            //Array to receive CRC bytes:
            byte[] CRC = new byte[2];  
            message[0] = aAddress;
            message[1] = aType;
            message[2] = 2;//1; 
            message[3] = (byte)(aData >> 8);
            message[4] = (byte)aData; 
            GetCRC(message, ref CRC);
            message[message.Length - 2] = CRC[0];
            message[message.Length - 1] = CRC[1];
            return message;
        }


        #endregion

        #region Build BackMessage 1\2\3 mutiShort
        static public byte[] BuildMSG3Back(byte aAddress, byte aType, byte aDataLen, ushort[] aData)
        {
            byte[] message = new byte[5 + aDataLen*2]; 
            byte[] CRC = new byte[2]; 
            message[0] = aAddress;
            message[1] = aType;
            message[2] = (byte)(aDataLen*2);
            for (int i = 0; i < aDataLen; i++)
            {
                message[4+2*i] = (byte)(aData[i] >> 8);
                message[5 + 2 * i] = (byte)aData[i];
            } 
            GetCRC(message, ref CRC); 
            message[message.Length - 2] = CRC[0];
            message[message.Length - 1] = CRC[1];
            return message;
        }
        #endregion

    }
}
