using System;
using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms.DataVisualization.Charting;
using System.IO;

namespace EMS
{
    //策略的一个节点
    public class BalaTacticsClass
    {
        //
        public DateTime startTime;
        public DateTime endTime;
    }

    //全部策列，策略类
    public class BalaTacticsListClass
    {
        //SetThreadAffinityMask: Set hThread run on logical processer(LP:) dwThreadAffinityMask
        [DllImport("kernel32.dll")]
        static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

        //Get the handler of current thread
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentThread();


        public static string[] PCSTypes = { "待机", "恒流", "恒压", "恒功率", "时段内均充均放" };
        public static string[] tTypes = { "待机", "充电", "放电" };
        //策略列表
        List<BalaTacticsClass> BalaTacticsList = new List<BalaTacticsClass>();
        public DateTime WorkingDate = Convert.ToDateTime("2000-01-01 00:00:01");
        public bool BalaHasOn = false;  //策略标识符
        public int ActiveIndex = -2;
        public AllEquipmentClass Parent = null;
        private static ILog log = LogManager.GetLogger("BalaTacticsClass");


        public void BalaTacticsClass(AllEquipmentClass aParent)
        {
            Parent = aParent;
        }
        ////获取当前策略的位置
        //public int GetTacticsIndex(DateTime aTime)
        //{
        //    int iResult = -1;
        //    DateTime aStartTime;
        //    DateTime aEndTime;
        //    string strDate = aTime.ToString("yyyy-M-d");
        //    for (int i = 0; i < TacticsList.Count; i++)
        //    {
        //        aStartTime= Convert.ToDateTime(TacticsList[i].startTime.ToString(strDate + " H:m:s"));
        //        aEndTime = Convert.ToDateTime(TacticsList[i].endTime.ToString(strDate + " H:m:s"));
        //        if ((aTime>= aStartTime) &&(aTime<= aEndTime))
        //        {
        //            iResult = i;
        //            break;
        //        }
        //    } 
        //    return iResult; 
        //}


        /*        public void LoadJFPGFromSQL()
                {
                    MySqlConnection ctTemp = null;
                    MySqlDataReader rd = DBConnection.GetData("select startTime, eName "
                         + " from electrovalence ", ref ctTemp);
                    try
                    {
                        byte[] tempJFPG = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                            0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                                            0, 0 };//14*3=42    14个时段 ： 号 时 分
                        int i = 0;
                        DateTime dtTemp;
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
                    catch { }
                    finally
                    {
                        if (!rd.IsClosed)
                            rd.Close();
                        rd.Dispose();
                        ctTemp.Close();
                        ctTemp.Dispose();
                    }
                }*/

        //数据库中重新装载策略数据
        public void LoadFromMySQL()
        {
            MySqlConnection ctTemp = null;
            MySqlDataReader rd = null;
            try
            {
                 rd = DBConnection.GetData("select startTime,endTime"
                    + " from balatactics  order by startTime", ref ctTemp);

                if (rd != null)
                {
                    lock (BalaTacticsList)
                    {
                        if (rd.HasRows)
                        {
                            while (BalaTacticsList.Count > 0)
                            {
                                BalaTacticsList.RemoveAt(0);
                            }

                            while (rd.Read())
                            {
                                BalaTacticsClass oneBalaTactics = new BalaTacticsClass();
                                oneBalaTactics.startTime = Convert.ToDateTime("2022-01-01 " + rd.GetString(0));
                                oneBalaTactics.endTime = Convert.ToDateTime("2022-01-01 " + rd.GetString(1));

                                BalaTacticsList.Add(oneBalaTactics);
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            finally
            {
                if (rd != null)
                {
                    if (!rd.IsClosed)
                        rd.Close();
                    rd.Dispose();
                }

                if (ctTemp != null)
                {
                    ctTemp.Close();
                    DBConnection._connectionPool.ReturnConnection(ctTemp);
                }
            }
        }

        /*        /// <summary>
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
                }*/

        static ulong SetCpuID(int lpIdx)
        {
            ulong cpuLogicalProcessorId = 0;
            if (lpIdx < 0 || lpIdx >= System.Environment.ProcessorCount)
            {
                lpIdx = 0;
            }
            cpuLogicalProcessorId |= 1UL << lpIdx;
            return cpuLogicalProcessorId;
        }

        /// <summary>
        /// 策略监视线程
        /// </summary>
        public void AutoCheckBalaTactics()
        {
            try
            {
                //实例化等待连接的线程
                Thread ClientRecThread = new Thread(CheckBalaTactics);
                ClientRecThread.IsBackground = true;
                ClientRecThread.Priority = ThreadPriority.Lowest;
                ClientRecThread.Start();
            }
            catch
            {

            }
        }
        //每分钟检查一次
        private void CheckBalaTactics()
        {
            DateTime now;
            BalaTacticsClass oneBalaTactics = null;
            int sleepCount = 1000;
            while (true)
            {
                Thread.Sleep(sleepCount);
                if (BalaTacticsList.Count == 0)
                    continue;
                else if (!frmSet.UseBalaTactics)
                {
                    Thread.Sleep(60000);
                    continue;
                }
                now = DateTime.Now;

                lock (BalaTacticsList)
                {
                    //没有策略的执行策略就要停止输出
                    if (BalaTacticsList.Count == 0)
                    {               
                        if (frmMain.Selffrm.AllEquipment.BalaRun == 1)
                        {
                            frmMain.Selffrm.AllEquipment.BMS.ClearBmsBala();
                        }
                        sleepCount = 5000;
                        continue;
                    }


                    //判断时间所在的区间和工作内容
                    int i = 0;
                    for (i = 0; i < BalaTacticsList.Count; i++)
                    {
                        oneBalaTactics = BalaTacticsList[i];
                        if (CheckTimeInShedule(oneBalaTactics, now))
                            break;//找到list中第一条符合条件的策略(遇到新的策略会立刻中断当前策略，执行新的策略)
                    }//for

                    //没找到就停止
                    if (i == BalaTacticsList.Count)
                    {
                        if (frmMain.Selffrm.AllEquipment.BalaRun == 1)
                        {
                            frmMain.Selffrm.AllEquipment.BMS.ClearBmsBala();
                        }
                        sleepCount = 5000;
                        continue;
                    }
                    //找到区段处理方法
                    //ActiveIndex 初始默认为-2 是因为防止更新TacticsList后 指针指向空的位置
                    //循环读取策略列表，只有运行第一条策略或者更新策略才会下发指令
                    if (ActiveIndex != i)
                    {
                        //更换策略点
                        if (ActiveIndex >= 0)
                        {
                            // Thread.Sleep(120000);
                            //从策略中取出PCS的执行参数，打开hostStart，在com1线程中唯一PCS执行
                            if (frmMain.Selffrm.AllEquipment.BalaRun == 0)
                            {
                                frmMain.Selffrm.AllEquipment.BMS.ClearBmsBala();
                                lock (frmMain.Selffrm.AllEquipment)
                                {
                                    if (frmMain.Selffrm.AllEquipment.balaCellID.Count != 0)
                                        frmMain.Selffrm.AllEquipment.balaCellID.Clear();

                                    using (StreamReader reader = new StreamReader(frmSet.BalaPath))
                                    {
                                        string line;
                                        while ((line = reader.ReadLine()) != null)
                                        {
                                            frmMain.Selffrm.AllEquipment.balaCellID.Add(double.Parse(line));
                                        }
                                    }
                                }

                                if (frmMain.Selffrm.AllEquipment.balaCellID.Count != 0)
                                {
                                    frmMain.Selffrm.AllEquipment.BMS.StartBmsBala();
                                }

                            }
                        }
                        ActiveIndex = i;
                        sleepCount = 60000;
                    }
                    else
                    {
                        //运行策略 
                        if (frmMain.Selffrm.AllEquipment.BalaRun == 0)
                        {
                            frmMain.Selffrm.AllEquipment.BMS.ClearBmsBala();

                            if (frmMain.Selffrm.AllEquipment.balaCellID.Count != 0)
                                frmMain.Selffrm.AllEquipment.balaCellID.Clear();

                            using (StreamReader reader = new StreamReader(frmSet.BalaPath))
                            {
                                string line;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    frmMain.Selffrm.AllEquipment.balaCellID.Add(double.Parse(line));
                                }
                            }
                            
                            //10.8
                            if (frmMain.Selffrm.AllEquipment.balaCellID.Count != 0)
                            {
                                frmMain.Selffrm.AllEquipment.BMS.StartBmsBala();
                            }

                        }
                        ActiveIndex = i;
                        sleepCount = 60000;//执行一条策略，线程睡1分钟
                    }
                }
            }//lock  
            sleepCount = 6000;
        }

        //判断是否在时间段内
        private bool CheckTimeInShedule(BalaTacticsClass aTactics, DateTime aTime)
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
            else 
                return false;
        }
    }

}


