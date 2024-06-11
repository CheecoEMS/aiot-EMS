using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Modbus;

namespace EMSPCTools
{
    public class ModbusCommand
    {
        public string strMemo = "";
        public string strCOmmand="";
        public int ComType = 3; 
        public int DataAddr=0;
        public int DataLongth = 1;
        public int DataType = 0;       //读写一体类型 0 short;1:小数.;2byte;3long;4string
        public double Coefficient = 0;         //系数，返回数*系数=实际数字
        public string strData = "";    //返沪数据
        public string strResult="";    //返回数据的结果     
    }


    public class ParkClass
    {
        public int ClassType=0;  //0表；1pcs逆变；2BMU；3空调系统
        public string strCap="";
        public string strCommandFile="";
        public int InputType=0;  //0:modbus485;1udp;2TCP Clint;3TCP Server
        public string Comname="Com1";
        public int CSType=0;//0 clint;1:Server
        public string strIP="192.168.1.100";
        public int iServerPort = 9001;
        public int iLocoPort = 9001;
        public bool bUsed=false;
        public int iBaudrate = 9600;//
        public int iDatabits = 8;
        public int SysID = 1;
        public modbus485 m485;
        public modbusTCPServer mTCPServer;
        public modbusTCPClient mTCPClient;
        public modbusUDP mUDP;

        public List<ModbusCommand> ComList = new List<ModbusCommand>();

        public ParkClass()
        {
            //ComList = new List<ModbusCommand>();
        }

        private string GetoneData(ref string strSource, string astrDef)
        {
            string strData = astrDef;
            //
            if (strSource.IndexOf(";") >= 0)
            {
                strData = strSource.Substring(0, strSource.IndexOf(";"));
                strSource = strSource.Substring(strSource.IndexOf(";") + 1, strSource.Length- strSource.IndexOf(";")-1);
            }
            else
            {
                if (strSource.Length > 0)
                    strData = strSource;
                strSource = "";
            }            
            return strData;
        }

        //
        private bool strData2Park(string astrData,ref ModbusCommand aoneCommand)
        {
            aoneCommand.strMemo = GetoneData(ref astrData,"string Command");
            aoneCommand.strCOmmand= GetoneData(ref astrData,"");
            //aoneCommand.SysID = Convert.ToInt16(GetoneData(ref astrData, "1"));
            aoneCommand.ComType = Convert.ToInt16( GetoneData(ref astrData,"3" ),16);
            aoneCommand.DataAddr = Convert.ToInt32( GetoneData(ref astrData, "0"),16);
            aoneCommand.DataLongth = Convert.ToInt32( GetoneData(ref astrData, "1"),16);
            aoneCommand.DataType = Convert.ToInt32(GetoneData(ref astrData, "0"));//读写一体类型 0 short;1:小数.;2byte;3long;4string
            aoneCommand.Coefficient = Convert.ToDouble(GetoneData(ref astrData, "1"));       
            // FloatLen=0;       //浮点苏时有效；0任意长度；其他是小数点后长度/（n*10）
            aoneCommand.strData = "";         //返沪数据
            aoneCommand.strResult = "";  //返回数据的结果  
            return true;
        }

        //下载command文件
        public void  LoadCommandFromFile()
        {
            if (!File.Exists(strCommandFile))
              return;
            //读取数据
            StreamReader srFile = File.OpenText(strCommandFile);
            try
            {
                 string strData = srFile.ReadLine();
                 while (strData != null)
                 {
                     if((strData=="")||(strData.Substring(0,1)=="#"))
                     {
                         strData = srFile.ReadLine();
                        continue;
                     }

                     ModbusCommand oneCommand=new ModbusCommand();
                     if(strData2Park(strData,ref oneCommand))
                         ComList.Add(oneCommand); 
                     strData = srFile.ReadLine();
                 }

            }
            catch { }
            finally 
            {
                srFile.Close();
            }
          
           
        }
    }

    public class ClassAllPark
    {
        public List<ParkClass> ParkList = new List<ParkClass>();
        string[] delNameList = { "电表", "PCS逆变器", "BMS", "空调系统", "其他", "", "" };
        public ClassAllPark()
        {
           // ParkList = new List<ParkClass>();

        }
        
        ~ClassAllPark()
        {
            //ParkClass onePark;
            //ModbusCommand oneCommand;
            //while (ParkList.Count > 0)
            //{ 
            //    onePark=ParkList[0];
            //    ParkList.Remove(onePark);
            //    while(onePark.ComList.Count>0)
            //    {
            //        oneCommand= onePark.ComList[0];
            //        //onePark.ComList.
            //       //free  oneCommand

            //    }
            //  //free onePark;
            //}
        }
        

        //读取一个数据
        private string ReadoneData(StreamReader asrFile,string aDefData)
        {
            string strData = asrFile.ReadLine();
            if (strData != null)
                return strData;
            else
                return aDefData;
        }

        //从文件中读取设置信息
        public void LoadSetFromFile(string astrFileName)
        {

            if (!File.Exists(astrFileName))
            {
                FileStream  tempFile = File.Create(astrFileName);
                tempFile.Close();
            } ;
            int iDataCount=6;
            StreamReader srFile = File.OpenText(astrFileName);
            string strData = srFile.ReadLine();
            if ((strData != null) && (strData!=""))
                 iDataCount = Convert.ToInt16(strData); 
            ParkClass oneParkClass;
            try
            {
                for (int i = 0; i < iDataCount; i++)
                {
                    //iDataCount--;
                    oneParkClass = new ParkClass();
                    oneParkClass.ClassType = Convert.ToInt32(ReadoneData(srFile, oneParkClass.ClassType.ToString()));
                    //
                    if ((strData == null) || (strData == ""))
                    {
                        switch (i)
                        {
                            case 0:
                                oneParkClass.ClassType = 0;
                                break;
                            case 1:
                                oneParkClass.ClassType = 0;
                                break;

                            case 2:
                                oneParkClass.ClassType = 1;
                                break;

                            case 3:
                                oneParkClass.ClassType = 2;
                                break;

                            case 4:
                                oneParkClass.ClassType = 3;
                                break;

                            case 5:
                                oneParkClass.ClassType = 4;
                                break;
                            default:
                                oneParkClass.ClassType = 4;
                                break;

                        }
                    }                    
                    oneParkClass.strCap = ReadoneData(srFile, oneParkClass.strCap);
                    if(oneParkClass.strCap=="")
                        oneParkClass.strCap = delNameList[oneParkClass.ClassType];
                    oneParkClass.strCommandFile = ReadoneData(srFile, oneParkClass.strCommandFile);
                    oneParkClass.InputType = Convert.ToInt32(ReadoneData(srFile, oneParkClass.InputType.ToString()));
                    oneParkClass.Comname = ReadoneData(srFile, oneParkClass.Comname);
                    oneParkClass.CSType = Convert.ToInt32(ReadoneData(srFile, oneParkClass.CSType.ToString()));
                    oneParkClass.strIP = ReadoneData(srFile, oneParkClass.strIP);
                    oneParkClass.iServerPort = Convert.ToInt32(ReadoneData(srFile, oneParkClass.iServerPort.ToString()));
                    oneParkClass.iLocoPort = Convert.ToInt32(ReadoneData(srFile, oneParkClass.iLocoPort.ToString()));
                    oneParkClass.bUsed = Convert.ToBoolean(ReadoneData(srFile, oneParkClass.bUsed.ToString()));
                    oneParkClass.iBaudrate = Convert.ToInt32(ReadoneData(srFile, oneParkClass.iBaudrate.ToString()));
                    oneParkClass.iDatabits = Convert.ToInt32(ReadoneData(srFile, oneParkClass.iDatabits.ToString()));
                    oneParkClass.SysID = Convert.ToInt32(ReadoneData(srFile, oneParkClass.SysID.ToString()));

                    switch (oneParkClass.CSType)
                    {
                        case 0:
                            oneParkClass.m485 = new modbus485();
                            if(oneParkClass.bUsed)
                                oneParkClass.m485.Open(oneParkClass.Comname, oneParkClass.iBaudrate, oneParkClass.iDatabits,
                                    System.IO.Ports.Parity.None, System.IO.Ports.StopBits.One);
                            break;
                        case 1:
                            //oneParkClass.mTCPClient
                            if (oneParkClass.bUsed)
                                ;
                            break;
                        case 2:
                            if (oneParkClass.bUsed)
                                ;
                            break;
                        case 3:
                            if (oneParkClass.bUsed)
                                ;
                            break;
                        default:

                            break;

                    
                    }
                    
                    oneParkClass.LoadCommandFromFile();
                    //LoadCommadData(oneParkClass.ComList, oneParkClass.strCommandFile);
                    ParkList.Add(oneParkClass);
                }
            }
            catch{}
            finally{
                srFile.Close();            
            }             
        }


        //保存到文件
        public void Save2File(string astrFileName)
        {
            if (File.Exists(astrFileName))
            {
                File.Delete(astrFileName); 
            }
            //File.Create(astrFileName);
            FileInfo DataFile = new FileInfo(astrFileName);
            StreamWriter swFile = DataFile.CreateText();
            try
            {
                swFile.WriteLine(ParkList.Count.ToString());
                ParkClass oneParkClass;
                for (int i = 0; i < ParkList.Count; i++)
                { 
                    oneParkClass=ParkList[i];
                    swFile.WriteLine(oneParkClass.ClassType.ToString());
                    swFile.WriteLine(oneParkClass.strCap);
                    swFile.WriteLine(oneParkClass.strCommandFile);
                    swFile.WriteLine(oneParkClass.InputType.ToString());
                    swFile.WriteLine(oneParkClass.Comname);
                    swFile.WriteLine(oneParkClass.CSType.ToString());  
                    swFile.WriteLine(oneParkClass.strIP);
                    swFile.WriteLine(oneParkClass.iServerPort.ToString());
                    swFile.WriteLine(oneParkClass.iLocoPort.ToString());
                    swFile.WriteLine(oneParkClass.bUsed.ToString());
                    swFile.WriteLine(oneParkClass.iBaudrate.ToString());
                    swFile.WriteLine(oneParkClass.iDatabits.ToString());
                    swFile.WriteLine(oneParkClass.SysID.ToString());
                }
             }
             catch{}
             finally
             {
                 swFile.Close(); 
             }
        
        
        }

    }



}
