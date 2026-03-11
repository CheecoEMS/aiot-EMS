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
            string sql = "INSERT INTO warning (WaringID, rTime, wClass, WarningID, Warning, wLevels, memo) " +
                         "VALUES(@WaringID, @rTime, @wClass, @WarningID, @Warning, @wLevels, @memo);";
            var parameters = new Dictionary<string, object>
            {
                { "@WaringID", rID },
                { "@rTime", rDate },
                { "@wClass", wClass ?? string.Empty },
                { "@WarningID", WarningID },
                { "@Warning", Warning ?? string.Empty },
                { "@wLevels", wLevels },
                { "@memo", memo ?? string.Empty }
            };
            DBConnection.ExecSQLWithParams(sql, parameters);
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
                // oneWarning.UserID = "";
                //oneWarning.CheckTime=null;
                string sql = "UPDATE warning SET UserID = '' WHERE id = @id";
                var parameters = new Dictionary<string, object> { { "@id", aID } };
                DBConnection.ExecSQLWithParams(sql, parameters);
            }
            else
            {
                DateTime tempTime = DateTime.Now;
                //oneWarning.UserID = aUserID;
                //oneWarning.CheckTime = tempTime;
                string sql = "UPDATE warning SET CheckTime = @checkTime, UserID = @userId WHERE id = @id";
                var parameters = new Dictionary<string, object>
                {
                    { "@checkTime", tempTime },
                    { "@userId", aUserID ?? string.Empty },
                    { "@id", aID }
                };
                DBConnection.ExecSQLWithParams(sql, parameters);
            }
        }

        //增加确认 CheckTime UserID ResetTime
        public void Recovery(int aID)
        {
            string sql = "UPDATE warning SET ResetTime = @resetTime WHERE id = @id";
            var parameters = new Dictionary<string, object>
            {
                { "@resetTime", DateTime.Now },
                { "@id", aID }
            };
            DBConnection.ExecSQLWithParams(sql, parameters);
        }


    }
}
