using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Diagnostics;
using System.Threading;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace EMS
{
    class MySqlConnectionPool : IDisposable
    {
        private readonly string _connectionString;
        private readonly ConcurrentBag<MySqlConnection> _connections;
        private readonly int _maxConnections;
        private int _currentConnections;

        public MySqlConnectionPool(string connectionString, int maxConnections)
        {
            _connectionString = connectionString;
            _maxConnections = maxConnections;
            _connections = new ConcurrentBag<MySqlConnection>();
            _currentConnections = 0;
        }

        public MySqlConnection GetConnection()
        {
            if (_connections.TryTake(out var connection))
            {
                return connection;
            }
            else if (_currentConnections < _maxConnections)
            {
                Interlocked.Increment(ref _currentConnections);
                return new MySqlConnection(_connectionString);
            }
            else
            {
                throw new InvalidOperationException("No available connections in the pool.");
            }
        }

        public void ReturnConnection(MySqlConnection connection)
        {
            if (connection != null)
            {
                _connections.Add(connection);
            }
        }

        public void Dispose()
        {
            while (_connections.TryTake(out var connection))
            {
                connection.Dispose();
            }
        }
    }


    class DBConnection
    {
        static public MySqlConnection connection;
        static public bool IsConnected = false;
        static private string DataID = "qiao";
        static private string DataPassword = "1100";
        static public string connectionStr = "Database=emsdata;Data Source=127.0.0.1;port=3306;User Id=" + DataID + ";Password=" + DataPassword + ";";
        private static ILog log = LogManager.GetLogger("DB");
        public static MySqlConnectionPool _connectionPool;

        //链接数据库
        public DBConnection()
        {
            //创建连接池
            _connectionPool = new MySqlConnectionPool(connectionStr, 10);
        }
        
        //检查mysql服务是否开启
        static public void ChecMysql80()
        {
            if (!IsMySqlServiceRunning())
            {
                StartMysql80();
            }
        }

        //启动mysql服务
        static public void StartMysql80()
        {
            string serviceName = "MySQL80";
            StartService(serviceName);
        }

        static public void StartService(string serviceName)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "net.exe";
            psi.Arguments = $"start {serviceName}";
            psi.UseShellExecute = true;
            psi.Verb = "runas"; // 以管理员权限运行  
            try
            {
                Process.Start(psi).WaitForExit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting service: {ex.Message}");
            }
        }

        static public bool IsMySqlServiceRunning()
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("cmd.exe", "/c sc query MySQL80 | findstr RUNNING");
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output.Contains("RUNNING");
            }
        }
        //建立一个链接
        static private void CreateConnection()
        {
            try
            {
                // connectionStr = ConfigurationManager.ConnectionStrings["connStr"].ConnectionString;
                ChecMysql80();
                if (connection == null)
                {
                    connection = new MySqlConnection(connectionStr);
                }
                connection.Close();
                connection.Open();
                IsConnected = true;

            }
            catch (MySqlException ex)
            {
                IsConnected = false;
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
                IsConnected = false;
            }
        }

        //获取SQL的数据，返回dataset
        static public DataSet GetDataSet(string astrSQL)
        {
            MySqlConnection connection = null;
            try
            {
                ChecMysql80();
                /*                if ((connection == null) || (!IsConnected))
                                    CreateConnection();*/

                if (_connectionPool != null)
                {
                    connection = _connectionPool.GetConnection();
                    if (connection == null)
                    {
                        throw new InvalidOperationException("Failed to obtain a database connection from the pool.");
                    }
                    connection.Open();


                    MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection);// "Select * from XXXXXXX";
                    MySqlDataAdapter sda = new MySqlDataAdapter(sqlCmd);

                    DataSet ds = new DataSet();
                    sda.Fill(ds);
                    IsConnected = true;

                    return ds;
                }
                else
                {
                    return null;
                }
            }
            catch (MySqlException ex)
            {
                IsConnected = false;
                return null;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                return null; ;
            }
            finally
            {
                if (connection != null)
                {
                    connection.Close();
                    _connectionPool.ReturnConnection(connection);
                }
            }
        }

        //为读取数据库具体数据
        static public MySqlDataReader GetData(string astrSQL, ref MySqlConnection aConnect)
        {
            MySqlConnection connection = null;
            try
            {
                ChecMysql80();
                if (_connectionPool != null)
                {
                    connection = _connectionPool.GetConnection();
                    if (connection == null)
                    {
                        throw new InvalidOperationException("Failed to obtain a database connection from the pool.");
                    }
                    connection.Open();
                    MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection);// "Select * from XXXXXXX"; 
                                                                                    //使用 ExecuteReader 方法创建 SqlDataReader 对象 
                    MySqlDataReader sdr = sqlCmd.ExecuteReader();
                    return sdr;
                }
                else { return null; }
            }
            catch (MySqlException ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
                return null;
            }
            catch (Exception ex)
            {
                //IsConnected = false; 
                frmMain.ShowDebugMSG(ex.ToString());
                return null;
            }
            finally
            {
            
            }
        }

        //运行SQL，用于增加，删除，编辑
        static public bool ExecSQL(string astrSQL)
        {
            ChecMysql80();
            bool bResult = false;
            MySqlConnection connection = null;
            MySqlCommand sqlCmd = null;


            try
            {
                if (_connectionPool != null)
                {
                    connection = _connectionPool.GetConnection();
                    if (connection == null)
                    {
                        throw new InvalidOperationException("Failed to obtain a database connection from the pool.");
                    }
                    connection.Open();

                    sqlCmd = new MySqlCommand(astrSQL, connection);
                    IsConnected = true;
                    if (sqlCmd.ExecuteNonQuery() > 0)
                        bResult = true;
                    else
                        bResult = false;
                }
                else
                {
                    bResult = false;
                }
            }
            catch (MySqlException ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
                IsConnected = false;
                return false;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                frmMain.ShowDebugMSG(astrSQL + "\n" + ex.ToString());
                return false;
            }
            finally
            {
                if(sqlCmd != null)
                {
                    sqlCmd.Dispose();
                }

                if (connection != null)
                {
                    connection.Close();
                    _connectionPool.ReturnConnection(connection);
                }
            }     
            return bResult;
        }

        //获取最后一个记录的ID
        static public int GetLastID(string astrSQL)
        {
            ChecMysql80();
            int iResult = -1;
            MySqlConnection ctTemp = null;
            MySqlDataReader rd = GetData(astrSQL, ref ctTemp);
            try
            {
                if (rd != null)
                {
                    if (rd.HasRows)
                    {
                        if (rd.Read())
                            iResult = rd.GetInt32(0);
                    }
                }
                else { iResult = -1; }
            }
            catch (MySqlException ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            finally
            {
                if (rd != null)
                {
                    if (rd.IsClosed)
                        rd.Close();
                    rd.Dispose();
                }
                if (ctTemp != null)
                {
                    ctTemp.Close();
                    _connectionPool.ReturnConnection(ctTemp);
                }
            }
            return iResult;
        }

        //检查是否存在SQL约定的数据 （含有为True，不存在为False）
        static public bool CheckRec(string astrSQL )
        {
            ChecMysql80();
            bool bResult = false;
            MySqlConnection connection = null;
            MySqlCommand sqlCmd = null;
            MySqlDataReader rd = null;

            try
            {
                if (_connectionPool != null)
                {
                    connection = _connectionPool.GetConnection();
                    if (connection == null)
                    {
                        throw new InvalidOperationException("Failed to obtain a database connection from the pool.");
                    }
                    connection.Open();

                    sqlCmd = new MySqlCommand(astrSQL, connection);// "Select * from XXXXXXX";  
                    rd = sqlCmd.ExecuteReader();

                    bResult = rd.HasRows;
                }
                else
                {
                    bResult = false;
                }
            }
            catch (MySqlException ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            finally
            {
                if (rd != null)
                {
                    if (rd.IsClosed)
                        rd.Close();
                    rd.Dispose();
                }

                if (sqlCmd != null)
                {
                    sqlCmd.Dispose();
                }

                if (connection != null)
                {
                    connection.Close();
                    _connectionPool.ReturnConnection(connection);
                }
            }
            return bResult;
        }


        /// <summary>
        /// 检查用户的权限
        /// </summary>
        /// <param name="astrSQL"></param>
        /// <returns></returns>
        static public bool ChecUserc(string astrSQL,ref int aPower)
        {
            ChecMysql80();
            aPower = -1;
            bool bResult = false;
            MySqlConnection connection = null;
            MySqlCommand sqlCmd = null;
            MySqlDataReader rd = null;
            try
            {
                if (_connectionPool != null)
                {
                    connection = _connectionPool.GetConnection();
                    if (connection == null)
                    {
                        throw new InvalidOperationException("Failed to obtain a database connection from the pool.");
                    }
                    connection.Open();

                    sqlCmd = new MySqlCommand(astrSQL, connection);
                    rd = sqlCmd.ExecuteReader();

                    if (rd.Read())
                        aPower = rd.GetInt32(0);
                    bResult = rd.HasRows;
                }
                else
                {
                    bResult = false;
                }
            }
            catch (MySqlException ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            finally
            {
                if (rd != null)
                {
                    if (rd.IsClosed)
                        rd.Close();
                    rd.Dispose();
                }

                if (sqlCmd != null)
                {
                    sqlCmd.Dispose();
                }

                if (connection != null)
                {
                    connection.Close();
                    _connectionPool.ReturnConnection(connection);
                }
            }
            return bResult;
        }


        //功能：设置dbgrid
        //1将dbgrid的去掉前面的 //2只读设置  //3整行选择显示
        static public void SetDBGrid(DataGridView adDtaGrid)
        {
            adDtaGrid.AllowUserToAddRows = false;
            adDtaGrid.RowHeadersVisible = false; // 行头隐藏 
            adDtaGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            adDtaGrid.ReadOnly = true;
            //设置对齐方式和字体
            // dataGridView1.RowHeadersBorderStyle = DataGridViewContentAlignment.MiddleCenter;
            //dataGridView1.Font = new Font("宋体", 11);
            adDtaGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            adDtaGrid.MultiSelect = false;
            adDtaGrid.AutoGenerateColumns = false;
        }

        //将查询结果显示在DBGrid
        static public void ShowData2DBGrid(DataGridView adDtaGrid, string astrSQL)
        {
            ChecMysql80();
            MySqlConnection connection = null;
            MySqlCommand sqlCmd = null;
            MySqlDataAdapter sda = null;
            DataSet dataset = new DataSet();
            try
            {
                if (_connectionPool != null)
                {
                    connection = _connectionPool.GetConnection();
                    if (connection == null)
                    {
                        throw new InvalidOperationException("Failed to obtain a database connection from the pool.");
                    }
                    connection.Open();

                    sqlCmd = new MySqlCommand(astrSQL, connection);// "Select * from XXXXXXX";
                    sda = new MySqlDataAdapter(sqlCmd);
                    sda.Fill(dataset);

                    if (dataset == null)
                    {

                        return;
                    }
                    adDtaGrid.DataSource = dataset.Tables[0];
                    adDtaGrid.Update();
                }
                else
                {
                    return;
                }
            }
            catch (MySqlException ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            finally
            {
                if (sqlCmd != null)
                    sqlCmd.Dispose();

                if (sda != null)
                    sda.Dispose();

                if (connection != null)
                {
                    connection.Close();
                    _connectionPool.ReturnConnection(connection);
                }

                if (dataset != null)
                    dataset.Dispose();

            }
        }

        //显示查询数据
        static public void ShowData2Chart(Chart aChart, string astrSQL, int aDataCount, string aTimeFormat)
        {
            ChecMysql80();
            //清理旧的数据
            for (int i = 0; i < aDataCount; i++)
            {
                aChart.Series[i].Points.Clear();
            }
            //creat reader 
            MySqlConnection ctTemp = null;
            MySqlDataReader sdr = GetData(astrSQL, ref ctTemp);
            try
            {
                if (sdr != null)
                {
                    if (sdr.HasRows)
                    {
                        while (sdr.Read())//调用 Read 方法读取 SqlDataReader
                        {
                            for (int i = 0; i < aDataCount; i++)
                            {
                                aChart.Series[i].Points.AddXY(
                                   sdr.GetDateTime(0).ToString(aTimeFormat),//
                                   sdr.GetFloat(i + 1).ToString());
                            }
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            finally
            {
                if (sdr != null)
                {
                    if (!sdr.IsClosed)
                        sdr.Close();
                    sdr.Dispose();
                }

                if (ctTemp != null)
                {
                    ctTemp.Close();
                    _connectionPool.ReturnConnection(ctTemp);
                }
            }
        }

        //只清理和增加一个series的数据
        static public void ShowData2Chart(Chart aChart, string astrSQL, int aDataCount, string aTimeFormat, int aSeriesIndex)
        {
            ChecMysql80();
            //清理旧的数据 
            aChart.Series[aSeriesIndex].Points.Clear();

            //creat reader 
            MySqlConnection ctTemp = null;
            MySqlDataReader sdr = GetData(astrSQL, ref ctTemp);
            try
            {
                if (sdr != null)
                {
                    if (sdr.HasRows)
                    {
                        while (sdr.Read())//调用 Read 方法读取 SqlDataReader
                        {
                            aChart.Series[aSeriesIndex].Points.AddXY(
                               sdr.GetDateTime(0).ToString(aTimeFormat),//
                               sdr.GetFloat(1).ToString());
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            finally
            {
                if (sdr != null)
                {
                    if (!sdr.IsClosed)
                        sdr.Close();
                    sdr.Dispose();
                }
                if (ctTemp != null)
                {
                    ctTemp.Close();
                    _connectionPool.ReturnConnection(ctTemp);
                }
            }
        }

        //应对功率部分的正负代表充放电
        static public void ShowData2ChartPower(Chart aChart, string astrSQL, int aDataCount, string aTimeFormat)
        {
            ChecMysql80();
            //清理旧的数据
            for (int i = 1; i <= aDataCount; i++)
                aChart.Series[i].Points.Clear();

            //creat reader 
            MySqlConnection ctTemp = null;
            MySqlDataReader sdr = GetData(astrSQL, ref ctTemp);
            try
            {
                if (sdr != null)
                {
                    if (sdr.HasRows)
                    {
                        while (sdr.Read())//调用 Read 方法读取 SqlDataReader
                        {
                            aChart.Series[1].Points.AddXY(
                                  sdr.GetDateTime(0).ToString(aTimeFormat),//
                                  sdr.GetFloat(1).ToString());
                            aChart.Series[2].Points.AddXY(
                                  sdr.GetDateTime(0).ToString(aTimeFormat),//
                                  sdr.GetFloat(2).ToString());
                            //充电为负
                            if (sdr.GetFloat(3) > 0)
                                aChart.Series[3].Points.AddXY(
                                  sdr.GetDateTime(0).ToString(aTimeFormat),//
                                  sdr.GetFloat(3).ToString());
                            else
                                aChart.Series[3].Points.AddXY(
                                sdr.GetDateTime(0).ToString(aTimeFormat), "0");
                            //放电为负
                            if (sdr.GetFloat(3) < 0)
                                aChart.Series[4].Points.AddXY(
                                     sdr.GetDateTime(0).ToString(aTimeFormat),//
                                     Math.Abs(sdr.GetFloat(3)).ToString());
                            else
                                aChart.Series[4].Points.AddXY(
                                sdr.GetDateTime(0).ToString(aTimeFormat), "0");
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            finally
            {
                if (sdr != null)
                {
                    if (!sdr.IsClosed)
                        sdr.Close();
                    sdr.Dispose();
                }

                if (ctTemp != null)
                {
                    ctTemp.Close();
                    _connectionPool.ReturnConnection(ctTemp);
                }
            }
        }

        //将dbgrid的数据保存到文件
        static public void SaveGrid2File(DataGridView aDataGrid)
        {
            ChecMysql80();
            if (aDataGrid.RowCount <= 0)
                return;
            string fileName = DateTime.Now.ToString("yyMMdd");//可以在这里设置默认文件名 
            SaveFileDialog saveDialog = new SaveFileDialog();//实例化文件对象
            saveDialog.DefaultExt = "txt";//文件默认扩展名xls
            saveDialog.Filter = "LOG文件|*.txt";//获取或设置当前文件名筛选器字符串，该字符串决定对话框的“另存为文件类型”或“文件类型”框中出现的选择内容。
            saveDialog.FileName = fileName;
            DialogResult aResult = saveDialog.ShowDialog();//打开保存窗口给你选择路径和设置文件名
            if (aResult != DialogResult.OK)
                return;
            string saveFileName = saveDialog.FileName;//文件保存名
            //实例化一个文件流--->与写入文件相关联  
            FileStream fs = new FileStream(saveFileName, FileMode.Create);
            //实例化一个StreamWriter-->与fs相关联  
            StreamWriter sw = new StreamWriter(fs);

            try
            {
                string strDataLine = "";
                for (int i = 0; i < aDataGrid.ColumnCount; i++)//遍历循环获取DataGridView标题
                {
                    strDataLine += aDataGrid.Columns[i].HeaderText + "    "; //Columns[i].HeaderText表示第i列的表头
                }
                sw.WriteLine(strDataLine);
                strDataLine = "";
                //写入数值
                for (int r = 0; r < aDataGrid.Rows.Count; r++)//这里表示数据的行标,dataGridView1.Rows.Count表示行数
                {
                    for (int i = 0; i < aDataGrid.ColumnCount; i++)//遍历r行的列数
                    {
                        strDataLine += aDataGrid.Rows[r].Cells[i].Value + "   "; //dataGridView1.Rows[r].Cells[i].Value获取列的r行i值
                    }
                    sw.WriteLine(strDataLine);
                    strDataLine = "";
                }
            }
            catch (MySqlException ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            finally
            {
                //清空缓冲区  
                sw.Flush();
                //关闭流  
                sw.Close();
                fs.Close();
            }
        }

        //记录LOg事件
        static public void RecordLOG(string aEClasse, string aEvemt, string aMemo)
        {
/*            DBConnection.ExecSQL("insert into log (eTime,eClass,Event,Memo)values ('"
                + DateTime.Now.ToString("yyyy-M-d H:m:s") + "','"
                + aEClasse + "','"
                + aEvemt + "','"
                 + aMemo + "')");*/
        }

        static public bool UploadCloud(string sql)
        {
            ChecMysql80();
            bool bResult = false;
            MySqlConnection connection = null;
            MySqlCommand sqlCmd = null;
            MySqlDataReader rd = null;
            try
            {
                if (_connectionPool != null)
                {
                    connection = _connectionPool.GetConnection();
                    if (connection == null)
                    {
                        throw new InvalidOperationException("Failed to obtain a database connection from the pool.");
                    }
                    connection.Open();

                    sqlCmd = new MySqlCommand(sql, connection);
                    rd = sqlCmd.ExecuteReader();

                    var row = new System.Collections.Generic.Dictionary<string, object>();
                    for (int i = 0; i < rd.FieldCount; i++)
                    {
                        row[rd.GetName(i)] = rd.GetValue(i);
                    }
                    SaveJsonToFile(JsonConvert.SerializeObject(row, Formatting.Indented));
                    bResult = true;
                }
                else
                {
                    bResult = false;
                }
            }
            catch (MySqlException ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            finally
            {
                if (rd != null)
                {
                    if (rd.IsClosed)
                        rd.Close();
                    rd.Dispose();
                }

                if (sqlCmd != null)
                {
                    sqlCmd.Dispose();
                }

                if (connection != null)
                {
                    connection.Close();
                    _connectionPool.ReturnConnection(connection);
                }
            }
            return bResult;

        }

        public static void SaveJsonToFile(string jsonResult)
        {
            if (!string.IsNullOrEmpty(jsonResult))
            {
                /*                string filePath = Path.Combine(directoryPath, "profit_data.json");
                                File.WriteAllText(filePath, jsonResult);
                                Console.WriteLine($"Data has been written to {filePath}");*/
                JObject jsonObject = JObject.Parse(jsonResult);
                var output = new
                {
                    time = ConvertToUnixTimestamp(jsonObject["rTime"].Value<DateTime>()),
                    iot_code = frmSet.SysID,
                    DaliyAuxiliaryKWH = new string[]
                    {
                        FormatValue(jsonObject["auxkwhAll"]),
                        FormatValue(jsonObject["auxkwh1"]),
                        FormatValue(jsonObject["auxkwh2"]),
                        FormatValue(jsonObject["auxkwh3"]),
                        FormatValue(jsonObject["auxkwh4"])
                    },
                    DaliyE2PKWH = new string[]
                    {
                        FormatValue(jsonObject["inPower"]),
                        FormatValue(jsonObject["in1kwh"]),
                        FormatValue(jsonObject["in2kwh"]),
                        FormatValue(jsonObject["in3kwh"]),
                        FormatValue(jsonObject["in4kwh"])
                    },
                    DaliyE2OKWH = new string[]
                    {
                        FormatValue(jsonObject["outPower"]),
                        FormatValue(jsonObject["out1kwh"]),
                        FormatValue(jsonObject["out2kwh"]),
                        FormatValue(jsonObject["out3kwh"]),
                        FormatValue(jsonObject["out4kwh"])
                    },
                    DaliyPrice = new string[]
                    {
                        "0",
                        "0",
                        "0",
                        "0",
                        "0"
                    },
                    DaliyProfit = FormatValue(jsonObject["profit"])
                };
                string outputJson = JsonConvert.SerializeObject(output, Formatting.Indented);
                string rDate = DateTime.Now.ToString("yyyy-MM-dd");
                frmMain.Selffrm.AllEquipment.Report2Cloud.UploadProfit2Cloud(outputJson, rDate);
            }
            else
            {
                Console.WriteLine("No data to write.");
            }
        }

        private static long ConvertToUnixTimestamp(DateTime date)
        {
            DateTimeOffset dateTimeOffset = new DateTimeOffset(date);
            return dateTimeOffset.ToUnixTimeMilliseconds();
        }

        private static string FormatValue(JToken value)
        {
            return value.Value<double>().ToString("0.000");
        }

    }
}

