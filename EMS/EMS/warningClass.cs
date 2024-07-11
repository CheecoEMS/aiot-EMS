using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;

namespace EMS
{
    //单个告警信息
    class WarmingClass
    {
        public int rID;
        public int WarningID;
        public int wLevels;
        public DateTime rDate;
        public string wClass;
        public string Warning;
        public DateTime CheckTime;
        public string UserID;
        //public DateTime ResetTime;
        public string memo;
        public int InsertWaring()
        {
            string sql = "INSERT INTO warning (WaringID, rTime, wClass,WarningID, Warning,wLevels,memo)" +
                " VALUES('" + rID + "', '"
                + rDate.ToString("yyyy-M-d H:m:s") + "', '"
                + wClass + "', '"
                + WarningID.ToString() + "', '"
                + Warning + "', '"
                + wLevels.ToString() + "','"
                + memo + "')";

            try
            {
                bool result = SqlExecutor.ExecuteSqlTaskAsync(sql, 3);

                if (result)
                {
                    // 处理执行成功的逻辑
                }
                else
                {
                    // 处理执行失败的逻辑
                }
            }
            catch (Exception ex)
            {
                // 处理异常情况
            }

            return DBConnection.GetLastID("select MAX(id) AS max_id from warning ");
        }
    }

    //警告信息类列表类
    public class WarmingListClass
    {
        List<WarmingClass> WarningList = new List<WarmingClass>();

        //增加一个记录 
        public void InsertWarming(int aWaringID, int wLevels, string awClass, string aWarning, string aMemo)
        {
            WarmingClass oneWarning = new WarmingClass();
            oneWarning.rDate = DateTime.Now;
            oneWarning.WarningID = aWaringID;
            oneWarning.wLevels = wLevels;
            oneWarning.wClass = awClass;
            oneWarning.Warning = aWarning;
            oneWarning.memo = aMemo;
            oneWarning.rID = oneWarning.InsertWaring();
            WarningList.Add(oneWarning);
        }

        public void BeChecked(int aID, string aUserID, bool aRecovery = false)
        {
            WarmingClass oneWarning = null;
            for (int i = 0; i < WarningList.Count; i++)
            {
                if (WarningList[i].rID == aID)
                {
                    oneWarning = WarningList[i];
                    break;
                }
            }

            if (aRecovery)
            {
                string sql = " UPDATE warning SET  "
                    // + "CheckTime = null,"
                    + "  UserID=''  where id='" + aID.ToString() + "'";

                try
                {
                    bool result = SqlExecutor.ExecuteSqlTaskAsync(sql, 3);

                    if (result)
                    {
                        // 处理执行成功的逻辑
                    }
                    else
                    {
                        // 处理执行失败的逻辑
                    }
                }
                catch (Exception ex)
                {
                    // 处理异常情况
                }
            }
            else
            {
                DateTime tempTime = DateTime.Now;

                string sql = " UPDATE warning SET "
                    + "CheckTime = '" + tempTime.ToString("yyyy-M-d H:m:s")
                    + "', UserID='" + aUserID + "' where id='"
                    + aID.ToString() + "'";

                try
                {
                    bool result = SqlExecutor.ExecuteSqlTaskAsync(sql, 3);

                    if (result)
                    {
                        // 处理执行成功的逻辑
                    }
                    else
                    {
                        // 处理执行失败的逻辑
                    }
                }
                catch (Exception ex)
                {
                    // 处理异常情况
                }
            }
        }

        //增加确认 CheckTime UserID ResetTime
        public void Recovery(int aID)
        {
            string sql = " UPDATE warning SET ("
                + "ResetTime = '" + DateTime.Now.ToString("yyyy-M-d H:m:s")
                + "') where id='" + aID.ToString() + "'";

            SqlExecutor.EnqueueSqlTask(sql, 1, outcome =>
            {
                if (outcome)
                {

                }
                else
                {

                }
            });

        }


        public void LoadFromMySQL()
        {
            while (WarningList.Count > 0)
            {
                // WarningList[0].Dispose();                
                WarningList.RemoveAt(0);
            }
            MySqlConnection ctTemp = null;
            MySqlDataReader rd = DBConnection.GetData("select WaringID, rTime,wLevels, wClass, Warning,memo,CheckTime,UserID,ResetTime "
                 + " from warning where ResetTime IS NULL", ref ctTemp);
            try
            {
                while (rd.Read())
                {
                    WarmingClass oneWarning = new WarmingClass();
                    oneWarning.rDate = rd.GetDateTime(1);//Convert.ToDateTime();
                    oneWarning.WarningID = rd.GetInt32(0);
                    oneWarning.wLevels = rd.GetInt32(2); ;
                    oneWarning.wClass = rd.GetString(3);
                    oneWarning.Warning = rd.GetString(4);
                    oneWarning.memo = rd.GetString(5);
                    oneWarning.CheckTime = rd.GetDateTime(6);
                    oneWarning.UserID = rd.GetString(7);
                    //oneWarning.rID = oneWarning.InsertWaring();
                    WarningList.Add(oneWarning);
                }
            }
            catch { }
            finally
            {
                if (!rd.IsClosed)
                    rd.Close();
                rd.Dispose();
                ctTemp.Close();
                ctTemp.Dispose();
            }
        }


    }
}
