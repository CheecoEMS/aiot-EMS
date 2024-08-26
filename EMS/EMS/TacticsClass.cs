using log4net;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms.DataVisualization.Charting;

namespace EMS
{
    //策略的一个节点
    public class TacticsClass
    {
        //
        public DateTime startTime;
        public DateTime endTime;
        public string tType;
        public string PCSType;
        public int waValue;
    }

    //全部策列，策略类
    public class TacticsListClass
    {
        public static string[] PCSTypes = { "待机", "恒流", "恒压", "恒功率", "时段内均充均放" };
        public static string[] tTypes = { "待机", "充电", "放电" };
        //策略列表
        public volatile List<TacticsClass> TacticsList = new List<TacticsClass>();
        public DateTime WorkingDate = Convert.ToDateTime("2000-01-01 00:00:01");
        public bool TacticsOn = false;  //策略标识符
        public int ActiveIndex = -2;
        public AllEquipmentClass Parent = null;
        
        private static ILog log = LogManager.GetLogger("TacticsClass");


        public void TacticsClass(AllEquipmentClass aParent)
        {
            Parent = aParent;
        }

        public void LoadJFPGFromSQL()
        {
            string astrSQL = "select startTime, eName  from electrovalence ";

            try
            {
                byte[] tempJFPG = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                    0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                    0, 0 };//14*3=42    14个时段 ： 号 时 分
                int i = 0;
                DateTime dtTemp;

                using (MySqlConnection connection = new MySqlConnection(DBConnection.connectionStr))
                {
                    connection.Open();
                    using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                    {
                        using (MySqlDataReader rd = sqlCmd.ExecuteReader())
                        {
                            if (rd != null && rd.HasRows)
                            {
                                while (rd.Read())
                                {
                                    tempJFPG[i * 3 + 0] = (byte)rd.GetInt32(1);  //获取 费率号（0：无 1：尖 2：峰 3：平 4：谷） eName
                                    dtTemp = Convert.ToDateTime("2022-01-01 " + rd.GetString(0));   //获取起始时间 startTime
                                    tempJFPG[i * 3 + 1] = (byte)dtTemp.Minute;
                                    tempJFPG[i * 3 + 2] = (byte)dtTemp.Hour;
                                    i++;
                                }
                                byte[] atable1 = { 3, 1, 1, 3, 1, 3, 3, 1, 6, 3, 1, 9 };//使用第三套表 1.1-3.1  3.1-6.1 6.1-9.1 9.1-12.1 拼成1年
                                byte[] atable2 = { 1, 1, 1, 1, 1, 3, 1, 1, 6, 1, 1, 9 };
                                if (frmMain.Selffrm.AllEquipment.Elemeter2 != null)
                                {
                                    frmMain.Selffrm.AllEquipment.Elemeter2.SetJFTG(atable1, tempJFPG);
                                }
                                if (frmMain.Selffrm.AllEquipment.Elemeter3!=null)
                                    frmMain.Selffrm.AllEquipment.Elemeter3.SetJFTG(atable2, tempJFPG);
                            }
                        }
                    }
                }
            }
            catch { }
            finally
            {

            }
        }

        //数据库中重新装载策略数据
        public bool LoadFromMySQL()
        {
            bool Result = false;
            string astrSQL = "select startTime,endTime, tType, PCSType, waValue"
                    + " from tactics  order by startTime";
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
                                lock (TacticsList)
                                {
                                    while (TacticsList.Count > 0)
                                    {
                                        TacticsList.RemoveAt(0);
                                    }
                                    while (rd.Read())
                                    {
                                        TacticsClass oneTactics = new TacticsClass();
                                        oneTactics.startTime = Convert.ToDateTime("2022-01-01 " + rd.GetString(0));
                                        oneTactics.endTime = Convert.ToDateTime("2022-01-01 " + rd.GetString(1));
                                        oneTactics.tType = rd.GetString(2);
                                        oneTactics.PCSType = rd.GetString(3);
                                        if (oneTactics.PCSType == "恒流")
                                            oneTactics.waValue = (int)(oneTactics.waValue * 0.8);
                                        if (oneTactics.PCSType == "恒压")
                                        {
                                            oneTactics.waValue = (int)((oneTactics.waValue - 648) * 0.7);
                                            if (oneTactics.waValue < 0)
                                                oneTactics.waValue = 0;
                                        }

                                        //9.5 源码注释
                                        //oneTactics.PCSType = "恒功率";


                                        //限额
                                        oneTactics.waValue = Math.Abs(oneTactics.waValue);
                                        if (oneTactics.waValue > 110)
                                            oneTactics.waValue = 110;
                                        //修正充放电的正负功率
                                        if (oneTactics.tType == "放电")
                                            oneTactics.waValue = -rd.GetInt32(4);
                                        else
                                            oneTactics.waValue = rd.GetInt32(4);

                                        TacticsList.Add(oneTactics);
                                    }
                                }
                                Result = true;
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
            return Result;
        }

        /// <summary>
        /// 检查昨天的数据是否存在
        /// </summary>
        private bool CheckYesterdayProfit()
        {
            //qiao
            return false;
        }

        /// <summary>
        /// 获取昨天的电表数据
        /// </summary> 
        public void GetYesterdayData()
        {
            //qiao
            try
            {
                if (CheckYesterdayProfit())
                    return;
                // BeRequisitioned = true;
            }
            //设置电表为停止询问
            //头一次运行检车昨天的电表数据是否存在
            //profit
            //获取昨天的电表数据
            //计算成本
            //保存到数据库
            //如果没有就查询并保存昨天电表数据
            //2如果日期变化也读取昨日的数据
            //记录开始的电表等信息

            //设置电表为巡查模式
            catch { }
            finally
            {
                //BeRequisitioned = true;
            }
        }

        /// <summary>
        /// 策略监视线程
        /// </summary>
        /// 
        public void CheckTacticsOnce()
        {
            TacticsClass oneTactics = null;

            if (!TacticsOn)//策略标识符没有开启，延长线程睡眠时间
            {
                // 只有在策略模式才会运行策略
                if (frmSet.config.SysMode == 1)
                    TacticsOn = true;
                return;
            }

            //开启策略，若EMS无策略则重新读取数据库
            if (TacticsList.Count == 0)
            {
                LoadFromMySQL();
            }

            DateTime now = DateTime.Now;

            //没有策略的执行策略就要停止输出
            if (TacticsList.Count == 0)
            {
                lock (frmMain.Selffrm.AllEquipment)
                {
                    frmMain.Selffrm.AllEquipment.waValueActive = 0;
                    //主从计划功率清零
                    frmMain.Selffrm.AllEquipment.PCSScheduleKVA = 0;
                    //主机停止中断PCS执行线程，中断向从机发送pcs工作指令
                    frmMain.Selffrm.AllEquipment.HostStart = false;
                    frmMain.Selffrm.AllEquipment.SlaveStart = false;
                }
            }


            //判断时间所在的区间和工作内容
            int i;
            for (i = 0; i < TacticsList.Count; i++)
            {
                oneTactics = TacticsList[i];
                if (CheckTimeInShedule(oneTactics, now))
                    break;//找到list中第一条符合条件的策略(遇到新的策略会立刻中断当前策略，执行新的策略)
            }//for

            //没找到就停止
            if (i == TacticsList.Count)
            {
                lock (frmMain.Selffrm.AllEquipment)
                {
                    frmMain.Selffrm.AllEquipment.eState = 1;
                    //主从计划功率清零
                    frmMain.Selffrm.AllEquipment.PCSScheduleKVA = 0;
                    //主机停止中断PCS执行线程，中断向从机发送pcs工作指令
                    frmMain.Selffrm.AllEquipment.HostStart = false;
                    frmMain.Selffrm.AllEquipment.SlaveStart= false;
                }
                return;
            }
            //找到区段处理方法
            //ActiveIndex 初始默认为-2 是因为防止更新TacticsList后 指针指向空的位置
            //循环读取策略列表，只有运行第一条策略或者更新策略才会下发指令
            if (ActiveIndex != i)
            {
                //更换策略点
                if (ActiveIndex >= 0)
                {
                    //从策略中取出PCS的执行参数，打开hostStart，在com1线程中唯一PCS执行
                    while (frmMain.Selffrm.AllEquipment.PCSTypeActive != oneTactics.PCSType || frmMain.Selffrm.AllEquipment.wTypeActive != oneTactics.tType || frmMain.Selffrm.AllEquipment.PCSScheduleKVA != oneTactics.waValue/frmSet.config.SysCount)
                    {
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            //2.21
                            frmMain.Selffrm.AllEquipment.PrewTypeActive = oneTactics.tType;
                            frmMain.Selffrm.AllEquipment.PrePCSTypeActive = oneTactics.PCSType;

                            if (frmMain.Selffrm.AllEquipment.PrePCSTypeActive == "恒功率")
                            {
                                frmMain.Selffrm.AllEquipment.GotoSchedule = true;
                            }

                            if (frmMain.Selffrm.AllEquipment.GotoSchedule)
                            {
                                frmMain.Selffrm.AllEquipment.dRate = 0;
                                frmMain.Selffrm.AllEquipment.eState = 1;
                                frmMain.Selffrm.AllEquipment.PCSTypeActive = oneTactics.PCSType;
                                frmMain.Selffrm.AllEquipment.wTypeActive = oneTactics.tType;
                                //下发的功率值恒为正数
                                frmMain.Selffrm.AllEquipment.PCSScheduleKVA = oneTactics.waValue/frmSet.config.SysCount;
                                log.Error("更换策略点的PCS计划功率：" + frmMain.Selffrm.AllEquipment.PCSScheduleKVA+ " "+oneTactics.tType + " "+oneTactics.PCSType);
                                frmMain.Selffrm.AllEquipment.HostStart = true;
                                frmMain.Selffrm.AllEquipment.SlaveStart = true;

                            }
                        }
                    }
                    ActiveIndex = i;
                }
                else
                {
                    //运行策略
                    while (frmMain.Selffrm.AllEquipment.PCSTypeActive != oneTactics.PCSType || frmMain.Selffrm.AllEquipment.wTypeActive != oneTactics.tType || frmMain.Selffrm.AllEquipment.PCSScheduleKVA != oneTactics.waValue/frmSet.config.SysCount)
                    {
                        lock (frmMain.Selffrm.AllEquipment)
                        {
                            //2.21
                            frmMain.Selffrm.AllEquipment.PrewTypeActive = oneTactics.tType;
                            frmMain.Selffrm.AllEquipment.PrePCSTypeActive = oneTactics.PCSType;
                            if (frmMain.Selffrm.AllEquipment.PrePCSTypeActive == "恒功率")
                            {
                                frmMain.Selffrm.AllEquipment.GotoSchedule = true;
                            }

                            if (frmMain.Selffrm.AllEquipment.GotoSchedule)
                            {
                                frmMain.Selffrm.AllEquipment.eState = 1;
                                //frmMain.Selffrm.AllEquipment.runState = 0;
                                frmMain.Selffrm.AllEquipment.PCSTypeActive = TacticsList[i].PCSType;
                                frmMain.Selffrm.AllEquipment.wTypeActive = TacticsList[i].tType;
                                frmMain.Selffrm.AllEquipment.PCSScheduleKVA = oneTactics.waValue/frmSet.config.SysCount;
                                log.Error("运行策略点的PCS计划功率：" + frmMain.Selffrm.AllEquipment.PCSScheduleKVA+ " "+oneTactics.tType + " "+oneTactics.PCSType);

                                frmMain.Selffrm.AllEquipment.HostStart = true;
                                frmMain.Selffrm.AllEquipment.SlaveStart = true;
                            }
                        }
                        ActiveIndex = i;
                    }
                }
            }
        }


        //判断是否在时间段内
        private bool CheckTimeInShedule(TacticsClass aTactics, DateTime aTime)
        {
            string strStrtTime = aTactics.startTime.ToString("HH:mm:ss");
            string strEndTime = aTactics.endTime.ToString("HH:mm:ss");
              
            string strNow = aTime.ToString("HH:mm:ss");
            if (strStrtTime.CompareTo(strEndTime) < 0)
            {
                if ((strNow.CompareTo(strStrtTime) >= 0) &&
                    (strNow.CompareTo(strEndTime) <= 0))
                {
                    return true;
                }
                else
                    return false;
            }
            //今晚到明天的策略
            else if (strStrtTime.CompareTo(strEndTime) > 0)
            {
                if ((strNow.CompareTo(strStrtTime) >= 0) ||
                        (strNow.CompareTo(strEndTime) < 0))
                {
                    return true;
                }
                else
                    return false;
            }
            else //if (strStrtTime.CompareTo(strEndTime) == 0)
                return false;
        }




        //获取时间对应的充放电的具体数值
        private int GetTacticPower(string astrTime)
        {
            TacticsClass oneTactics;
            int iResult = 0;
            string strStrtTime;
            string strEndTime;
            for (int i = 0; i < TacticsList.Count; i++)
            {
                oneTactics = TacticsList[i];
                strStrtTime = oneTactics.startTime.ToString("HH:mm");
                strEndTime = oneTactics.endTime.ToString("HH:mm");
                if (strStrtTime.CompareTo(strEndTime) < 0)
                {
                    if ((astrTime.CompareTo(strStrtTime) >= 0) &&
                        (astrTime.CompareTo(strEndTime) <= 0))
                    {
                        //if (oneTactics.tType == "充电")
                        //    iResult = -1 * oneTactics.waValue;
                        //else
                        iResult = -oneTactics.waValue;
                        //StartIndex = i; 
                        break;
                    }
                }
                else
                {
                    if ((astrTime.CompareTo(strStrtTime) >= 0) ||
                            (astrTime.CompareTo(strEndTime) <= 0))
                    {
                        //if (oneTactics.tType == "充电")
                        //    iResult = -1 * oneTactics.waValue;
                        //else
                        iResult = -oneTactics.waValue;
                        //StartIndex = i;
                        break;
                    }
                }
            }
            return iResult;
        }

        /// <summary>
        /// 将chart数组中位置换算成时间
        /// </summary>
        /// <param name="aCount"></param>
        /// <returns></returns>

        private string Count2Time(int aCount)
        {
            //Math.Round()：四舍六入五取偶      Math.Floor()：向下取整   Math.Ceiling()：向上取整 
            return ((int)Math.Floor(aCount / 60.0)).ToString("D2") + ":" + (aCount % 60).ToString("D2");
        }

        /// <summary>
        /// 将时间换算成chart数组的位置
        /// </summary>
        /// <param name="aTime"></param>
        /// <returns></returns>
        private int Time2Count(DateTime aTime)
        {
            return aTime.Hour * 60 + aTime.Minute;
        }

        public void AddOneStep(Chart aOneChar, DateTime aDateTime, double aMainKw, double aGridKW, double aSubKW)
        {
            int iIndex = Time2Count(aDateTime);
            aOneChar.Series[1].Points[iIndex].SetValueY(aMainKw);
            aOneChar.Series[2].Points[iIndex].SetValueY(aGridKW);
            aOneChar.Series[3].Points[iIndex].SetValueY(aSubKW);

        }

        /// <summary>
        /// 显示历史数据
        /// </summary>
        /// <param name="aOneChar"></param>
        public void LoadHistay(Chart aOneChar)
        {
            // string strDate = DateTime.Now.ToString("yyyy-MM-dd ");
            MySqlConnection ctTemp = null;

            string astrSQL = "select rTime, AllUkva, Gridkva, Subkw from elemeter2 "
             + " where rTime>='" + DateTime.Now.ToString("yyyy-MM-dd 00:00:00")
              + "'and rTime<='" + DateTime.Now.ToString("yyyy-MM-dd 23:59:59")
                  + "'  order by rTime";
            try
            {
                DateTime dtTemp;

                using (MySqlConnection connection = new MySqlConnection(DBConnection.connectionStr))
                {
                    connection.Open();
                    using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                    {
                        using (MySqlDataReader rd = sqlCmd.ExecuteReader())
                        {
                            if (rd != null && rd.HasRows)
                            {
                                while (rd.Read())
                                {
                                    dtTemp = Convert.ToDateTime(rd.GetString(0));
                                    AddOneStep(aOneChar, dtTemp, -1 * rd.GetDouble(1), rd.GetDouble(2), rd.GetDouble(3));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {

            }
        }


        /// <summary>
        /// 显示计划2Chart
        /// </summary>
        /// <param name="aOneChart"></param>
        /// <param name="aCleanAllSeries"></param>
        public void ShowTactic2Char(Chart aOneChart, bool aCleanAllSeries)
        {
            //int iIndex = 0;
            string strData;
            int iData;
            //if (aOneChart.Series[0].Points.Count>0)
            aOneChart.Series[0].Points.Clear();
            if (aCleanAllSeries)
            {
                aOneChart.Series[1].Points.Clear();
                aOneChart.Series[2].Points.Clear();
                aOneChart.Series[3].Points.Clear();
            }
            for (int i = 0; i < 1440; i++)//1一天60*24=1440分钟
            {
                strData = Count2Time(i);
                iData = GetTacticPower(strData);//, ref iIndex);
                if (aCleanAllSeries)
                {
                    aOneChart.Series[1].Points.AddXY(strData, 0);
                    aOneChart.Series[2].Points.AddXY(strData, 0);
                    aOneChart.Series[3].Points.AddXY(strData, 0);
                }
                if (iData > 100)
                    aOneChart.Series[0].Points.AddXY(strData, iData);
                else
                    aOneChart.Series[0].Points.AddXY(strData, iData);

            }
             //aOneChart.ChartAreas[0].AxisX.ScaleView.Size = 1500;
           // aOneChart.ChartAreas[0].AxisX.Minimum = DateTime.Parse("00:00:00").ToOADate();
           // aOneChart.ChartAreas[0].AxisX.Maximum = DateTime.Parse("23:59:59").ToOADate();
            //aOneChart.ChartAreas[0].AxisX.IntervalType = DateTimeIntervalType.Minutes;//如果是时间类型的数据，间隔方式可以是秒、分、时
            //chart1.ChartAreas[0].AxisX.Interval = DateTime.Parse("00:05:00").Millisecond;//间隔为5分钟
            // aOneChart.ChartAreas[0].AxisX.Interval = DateTime.Parse("00:01:00").Second;//TODO 测试--间隔为5秒 
           // aOneChart.ChartAreas[0].AxisX.LabelStyle.Format = "HH:mm";         //毫秒格式： hh:mm:ss.fff ，后面几个f则保留几位毫秒小数，此时要注意轴的最大值和最小值不要差太大
            aOneChart.ChartAreas[0].AxisX.LabelStyle.IntervalType = DateTimeIntervalType.Days;
            aOneChart.ChartAreas[0].AxisX.MajorGrid.IntervalType = DateTimeIntervalType.Days;
            aOneChart.ChartAreas[0].AxisX.Minimum = -30;
            aOneChart.ChartAreas[0].AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount; 
            aOneChart.ChartAreas[0].AxisX.Interval = 120; 
        }


    }
}
