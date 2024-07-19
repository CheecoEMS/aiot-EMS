using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;

namespace EMS
{
    //电表的阶梯电价一个节点
    public class ElectrovalenceClass
    {
        public int section = 0;
        public DateTime startTime;
        public string eName;
        //public float price;
    }


    //全部电表的阶梯电价
    public class ElectrovalenceListClass
    {
        public List<ElectrovalenceClass> ElectrovalenceList = new List<ElectrovalenceClass>();

        string[] JFPGs = { "无", "尖", "峰", "平", "谷" };

        //数据库的尖峰平谷数据保存到电表的数据格式
        public byte[] GetJFPGBytesData(int aSection)
        {
            byte[] bsResult = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                     0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };//14*1.5*2=21*2=42
            for (int i = 0; i < ElectrovalenceList.Count; i++)
            {
                if (ElectrovalenceList[i].section != aSection)
                    continue;
                bsResult[i * 3] = (byte)Array.IndexOf(JFPGs, ElectrovalenceList[i].eName);
                bsResult[i * 3 + 1] = Convert.ToByte(ElectrovalenceList[i].startTime.ToString("H"));
                bsResult[i * 3 + 2] = Convert.ToByte(ElectrovalenceList[i].startTime.ToString("m"));
            }
            return bsResult;
        }


        //数据库中装载电价的阶梯数据
        public void LoadFromMySQL()
        {
            SqlExecutor.ExecuteEnqueueSqlElectrovalenceTask(3, frmMain.ElectrovalenceList.ElectrovalenceList);
        }
    }
}
