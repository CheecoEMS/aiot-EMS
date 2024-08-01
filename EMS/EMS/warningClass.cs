﻿using MySql.Data.MySqlClient;
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
            DBConnection.ExecSQL("INSERT INTO warning (WaringID, rTime, wClass,WarningID, Warning,wLevels,memo)" +
                " VALUES('" + rID + "', '"
                + rDate.ToString("yyyy-M-d H:m:s") + "', '"
                + wClass + "', '"
                + WarningID.ToString() + "', '"
                + Warning + "', '"
                + wLevels.ToString() + "','"
                + memo + "'); ");
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
                DBConnection.ExecSQL(" UPDATE warning SET  "
                    // + "CheckTime = null,"
                    + "  UserID=''  where id='" + aID.ToString() + "'");
            }
            else
            {
                DateTime tempTime = DateTime.Now;
                //oneWarning.UserID = aUserID;
                //oneWarning.CheckTime = tempTime;
                DBConnection.ExecSQL(" UPDATE warning SET "
                    + "CheckTime = '" + tempTime.ToString("yyyy-M-d H:m:s")
                    + "', UserID='" + aUserID + "' where id='"
                    + aID.ToString() + "'");
            }
        }

        //增加确认 CheckTime UserID ResetTime
        public void Recovery(int aID)
        {
            DBConnection.ExecSQL(" UPDATE warning SET ("
                + "ResetTime = '" + DateTime.Now.ToString("yyyy-M-d H:m:s")
                + "') where id='" + aID.ToString() + "'");
        }


    }
}
