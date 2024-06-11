using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Windows.Forms;

namespace Modbus
{
     public class modbus485
    {
        public SerialPort sp=null;//= new SerialPort();
        public string modbusStatus;
        public List<modbus485> ParentList=null;

        #region Constructor / Deconstructor
        public modbus485()
        {
            byte[] response = new byte[100];
        }
        ~modbus485()
        {
        }
        #endregion

       

        private SerialPort Checksp(string portName)
        {
            if (sp != null)
                return sp;
            if ((ParentList == null)||(ParentList.Count==0))
            {
                return new SerialPort();
            }
            else
            {
                for (int i = 0; i < ParentList.Count; i++)
                {
                    if (ParentList[i].sp.PortName == portName)
                    {
                        if (ParentList[i].sp != null)
                            return ParentList[i].sp;
                        else
                            new SerialPort();                    
                    }
                
                }
                return new SerialPort();
            }
        } 
        
        
        
        #region //Open / Close Procedures
        
        public bool Open(string portName, int baudRate, int databits, Parity parity, StopBits stopBits)
        {
            sp = Checksp( portName);
                           
            //Ensure port isn't already opened:
            if (!sp.IsOpen)
            {
                //Assign desired settings to the serial port:
                sp.PortName = portName;
                sp.BaudRate = baudRate;
                sp.DataBits = databits;
                sp.Parity = parity;
                sp.StopBits = stopBits;
                //These timeouts are default and cannot be editted through the class at this point:
                sp.ReadTimeout = 1000;
                sp.WriteTimeout = 1000;

                try
                {
                    sp.Open();
                }
                catch (Exception err)
                {
                    modbusStatus = "Error opening " + portName + ": " + err.Message;
                    return false;
                }
                modbusStatus = portName + " opened successfully";
                return true;
            }
            else
            {
                modbusStatus = portName + " already opened";
                return false;
            }
        }
        public bool Close()
        {
            //Ensure port is opened before attempting to close:
            if (sp.IsOpen)
            {
                try
                {
                    sp.Close();
                }
                catch (Exception err)
                {
                    modbusStatus = "Error closing " + sp.PortName + ": " + err.Message;
                    return false;
                }
                modbusStatus = sp.PortName + " closed successfully";
                return true;
            }
            else
            {
                modbusStatus = sp.PortName + " is not open";
                return false;
            }
        }
        #endregion

         
        #region //Get Response
        private void GetResponse(ref byte[] response)
        {
            //There is a bug in .Net 2.0 DataReceived Event that prevents people from using this
            //event as an interrupt to handle data (it doesn't fire all of the time).  Therefore
            //we have to use the ReadByte command for a fixed length as it's been shown to be reliable.
            for (int i = 0; i < response.Length; i++)
            {
                response[i] = (byte)(sp.ReadByte());
            }
        }
        #endregion

        #region Function 16 - Write Multiple Registers
        public bool SendFc16(byte address, ushort start, ushort registers, short[] values)
        {
            //Ensure port is open:
            if (sp.IsOpen)
            {
                //Clear in/out buffers:
                sp.DiscardOutBuffer();
                sp.DiscardInBuffer();
                //Message is 1 addr + 1 fcn + 2 start + 2 reg + 1 count + 2 * reg vals + 2 CRC
                byte[] message = new byte[9 + 2 * registers];
                //Function 16 response is fixed at 8 bytes
                byte[] response = new byte[8];

                //Add bytecount to message:
                message[6] = (byte)(registers * 2);
                //Put write values into message prior to sending:
                for (int i = 0; i < registers; i++)
                {
                    message[7 + 2 * i] = (byte)(values[i] >> 8);
                    message[8 + 2 * i] = (byte)(values[i]);
                }
                //Build outgoing message:
                ModbusBase.BuildMessage(address, (byte)16, start, registers, ref message);
                
                //Send Modbus message to Serial Port:
                try
                {
                    sp.Write(message, 0, message.Length);
                    GetResponse(ref response);
                }
                catch (Exception err)
                {
                    modbusStatus = "Error in write event: " + err.Message;
                    return false;
                }
                //Evaluate message:
                if (ModbusBase.CheckResponse(response))
                {
                    modbusStatus = "Write successful";
                    return true;
                }
                else
                {
                    modbusStatus = "CRC error";
                    return false;
                }
            }
            else
            {
                modbusStatus = "Serial port not open";
                return false;
            }
        }
        #endregion
          
        #region Function Query - Read Registers
        public bool SendstrMSG(byte aAddress,byte bComType, ushort aRegStart, ushort aRegLebgth, ref string strBack)//short[] values
        {
            //Ensure port is open:
            strBack = "";
            if (sp.IsOpen)
            {
                //Clear in/out buffers:
                sp.DiscardOutBuffer();
                sp.DiscardInBuffer();
                //Function 3 request is always 8 bytes:
                byte[] message = new byte[8];//8
                //Function 3 response buffer:
                byte[] response = new byte[5 + 2 * aRegLebgth];
                //Build outgoing modbus message:
                ModbusBase.BuildMessage(aAddress, bComType, aRegStart, aRegLebgth, ref message);
                //Send modbus message to Serial Port:
                try
                {
                    sp.Write(message, 0, message.Length);
                    GetResponse(ref response);
                }
                catch (Exception err)
                {
                    modbusStatus = "Error in read event: " + err.Message;
                    return false;
                }
                //Evaluate message:
                if (ModbusBase.CheckResponse(response))
                {
                    int i = 0;
                    //Return requested register values:
                    //for (i = 0; i < (response.Length - 5) / 2; i++)
                    //{
                    //    values[i] = response[2 * i + 3];
                    //    values[i] <<= 8;
                    //    values[i] += response[2 * i + 4];
                    //}
                    for (i = 0; i < response.Length; i++)
                    {
                        strBack += response[i].ToString("x2");
                    }
                    modbusStatus = "Read successful";
                    return true;
                }
                else
                {
                    modbusStatus = "CRC error";
                    return false;
                }
            }
            else
            {
                modbusStatus = "Serial port not open";
                return false;
            }

        }
        #endregion

        #region Function sendMSG ,func3 
        public bool SendMSG(byte aAddress, ushort aRegStart, ushort aRegLength, ref short[] values)
        {
            //Ensure port is open:
            if (sp.IsOpen)
            {
                //Clear in/out buffers:
                sp.DiscardOutBuffer();
                sp.DiscardInBuffer();
                //Function 3 request is always 8 bytes:
                byte[] message = new byte[8];
                //Function 3 response buffer:
                byte[] response = new byte[5 + 2 * aRegLength];
                //Build outgoing modbus message:
                ModbusBase.BuildMessage(aAddress, (byte)3, aRegStart, aRegLength, ref message);
                //Send modbus message to Serial Port:
                try
                {
                    sp.Write(message, 0, message.Length);
                    GetResponse(ref response);
                }
                catch (Exception err)
                {
                    modbusStatus = "Error in read event: " + err.Message;
                    return false;
                }
                //Evaluate message:
                if (ModbusBase.CheckResponse(response))
                {
                    //
                    values = new short[aRegLength];
                    //Return requested register values:
                    for (int i = 0; i < (response.Length - 5) / 2; i++)
                    {
                        values[i] = response[2 * i + 3];
                        values[i] <<= 8;
                        values[i] += response[2 * i + 4];
                    }
                    modbusStatus = "Read successful";
                    return true;
                }
                else
                {
                    modbusStatus = "CRC error";
                    return false;
                }
            }
            else
            {
                modbusStatus = "Serial port not open";
                return false;
            }
        }
        #endregion


        public bool Get1BoolData(byte aAddress, ushort aRegStart, ushort aRegLength,ref bool aResult)
        {
            short[] ResultData=null;//=new byte[100];
            if (SendMSG(aAddress, aRegStart, aRegLength, ref ResultData))
            {             
                //aResult="";
                return true;
            }
            else
                return false;
        }

        public bool Get1Byte(byte aAddress, ushort aRegStart, ushort aRegLength, ref byte aResult)
        {
            short[] ResultData = null;//=new byte[100];
            if (SendMSG(aAddress, aRegStart, aRegLength, ref ResultData))
            {
                //aResult="";
                return true;
            }
            else
                return false;
        }

        public bool Get1Short(byte aAddress, ushort aRegStart, ushort aRegLength,ref short aResult)
        {
            short[] ResultData = null;//=new byte[100];
            if (SendMSG(aAddress, aRegStart, aRegLength, ref ResultData))
            {
                if (ResultData.Length>0)
                    aResult = ResultData[0];                
                return true;
            }
            else
                return false;
        }

        public bool Get1Long(byte aAddress, ushort aRegStart, ushort aRegLength, ref long aResult)
        {
            short[] ResultData = null;//=new byte[100];
            if (SendMSG(aAddress, aRegStart, aRegLength, ref ResultData))
            {
                if (ResultData.Length > 1)
                    aResult = ResultData[1]<<16+ ResultData[0];
                else if (ResultData.Length > 0)
                            aResult = ResultData[0];
                return true;
            }
            else
                return false;
        }

        public bool GetString(byte aAddress, ushort aRegStart, ushort aRegLength, ref string aResult)
        {
            short[] ResultData = null;//=new byte[100];
            if (SendMSG(aAddress, aRegStart, aRegLength, ref ResultData))
            {
                //aResult="";
                return true;
            }
            else
                return false;
        }




    }
}
