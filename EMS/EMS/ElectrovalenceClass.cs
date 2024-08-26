using log4net;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Common;
using System;
using System.Collections.Generic;

namespace EMS
{
    //电表的阶梯电价一个节点
    class ElectrovalenceClass
    {
        public int section = 0;
        public DateTime startTime;
        public string eName;
        //public float price;
    }


    //全部电表的阶梯电价
    public class ElectrovalenceListClass
    {
        List<ElectrovalenceClass> ElectrovalenceList = new List<ElectrovalenceClass>();
        private static ILog log = LogManager.GetLogger("ElectrovalenceListClass");

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
            string astrSQL = "select section ,startTime, eName  from electrovalence ";


            try
            {
                using (MySqlConnection connection = new MySqlConnection(DBConnection.connectionStr))
                {
                    connection.Open();
                    using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                    {
                        using (MySqlDataReader rd = sqlCmd.ExecuteReader())
                        {
                            if (rd != null && rd.HasRows)
                            {
                                while (ElectrovalenceList.Count > 0)
                                {
                                    ElectrovalenceList.RemoveAt(0);
                                }

                                while (rd.Read())
                                {
                                    ElectrovalenceClass oneElectrovalence = new ElectrovalenceClass();
                                    oneElectrovalence.section = rd.GetInt32(0);
                                    oneElectrovalence.startTime = Convert.ToDateTime("2022-01-01 " + rd.GetString(1));
                                    // oneElectrovalence.endTime = Convert.ToDateTime("2022-01-01 " + rd.GetString(1));
                                    oneElectrovalence.eName = rd.GetString(2);
                                    // oneElectrovalence.MaxPower = rd.GetInt32(3);
                                    // oneElectrovalence.price = rd.GetFloat(3);
                                    ElectrovalenceList.Add(oneElectrovalence);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }
            finally
            {

            }
        }
    }
}
