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
using System.Collections.Generic;
using System.Linq;
using MySqlX.XDevAPI.Common;

namespace EMS
{
    /// <summary>
    /// 定义构建数据库所需字段
    /// </summary>
    public class Column
    {
        public string Name { get; set; }//字段名
        public string Type { get; set; }//数据类型
        public bool IsNullable { get; set; }//是否可以为空
        public string Key { get; set; }//是否为主键
        public string Comment { get; set; }//注释
    }



    class DBConnection
    {
        static public MySqlConnection connection;
        static public bool IsConnected = false;
        static private string DataID = "qiao";
        static private string DataPassword = "1100";
        static public string connectionStr = "Database=emsdata;Data Source=127.0.0.1;port=3306;User Id=" + DataID + ";Password=" + DataPassword + ";Pooling=True;Min Pool Size=5;Max Pool Size=100;";
        private static ILog log = LogManager.GetLogger("DB");


        //链接数据库
        public DBConnection()
        {

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


        //为读取数据库具体数据
        static public MySqlDataReader GetData(string astrSQL, ref MySqlConnection aConnect)
        {

            return null;
        }


        //运行SQL，用于增加，删除，编辑
        static public bool ExecSQL(string astrSQL)
        {
            ChecMysql80();
            bool bResult = false;

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionStr))
                {
                    connection.Open();
                    using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                    {
                        if (sqlCmd.ExecuteNonQuery() > 0)
                            bResult = true;
                        else
                            bResult = false;
                    }
                }
            }
            catch (MySqlException ex)
            {
                log.Error(ex.Message);
                bResult = false;
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
                bResult = false;
            }
            finally
            {

            }     
            return bResult;
        }

        //获取最后一个记录的ID
        static public int GetLastID(string astrSQL)
        {
            ChecMysql80();
            int iResult = -1;

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionStr))
                {
                    connection.Open();
                    using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                    {
                        using (MySqlDataReader rd = sqlCmd.ExecuteReader())
                        {
                            if (rd != null && rd.HasRows)
                            {
                                if (rd.Read())
                                    iResult = rd.GetInt32(0);
                            }                          
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                log.Error(ex.Message);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }
            finally
            {

            }
            return iResult;
        }

        //检查是否存在SQL约定的数据 （含有为True，不存在为False）
        static public bool CheckRec(string astrSQL )
        {
            ChecMysql80();
            bool bResult = false;

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionStr))
                {
                    connection.Open();
                    using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                    {
                        using (MySqlDataReader rd = sqlCmd.ExecuteReader())
                        {
                            if (rd != null && rd.HasRows)
                            {
                                bResult = true;
                            }
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                log.Error(ex.Message);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }
            finally
            {

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

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionStr))
                {
                    connection.Open();
                    using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                    {
                        using (MySqlDataReader rd = sqlCmd.ExecuteReader())
                        {
                            if (rd != null && rd.HasRows)
                            {
                                if (rd.Read())
                                {
                                    aPower = rd.GetInt32(0);
                                    bResult = true;
                                }
                            }
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                log.Error(ex.Message);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }
            finally
            {

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
            DataSet dataset = new DataSet();
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionStr))
                {
                    connection.Open();
                    using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                    {
                        using (MySqlDataAdapter sda = new MySqlDataAdapter(sqlCmd))
                        {
                            if (sda != null)
                            {
                                sda.Fill(dataset);
                                if (dataset == null)
                                {
                                    return;
                                }
                                adDtaGrid.DataSource = dataset.Tables[0];
                                adDtaGrid.Update();
                            }

                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                log.Error(ex.Message);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }
            finally
            {

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
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionStr))
                {
                    connection.Open();
                    using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                    {
                        using (MySqlDataReader rd = sqlCmd.ExecuteReader())
                        {
                            if (rd != null && rd.HasRows)
                            {
                                while (rd.Read())//调用 Read 方法读取 SqlDataReader
                                {
                                    for (int i = 0; i < aDataCount; i++)
                                    {
                                        aChart.Series[i].Points.AddXY(
                                           rd.GetDateTime(0).ToString(aTimeFormat),//
                                           rd.GetFloat(i + 1).ToString());
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                log.Error(ex.Message);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }
            finally
            {

            }
        }

        //应对功率部分的正负代表充放电
        static public void ShowData2ChartPower(Chart aChart, string astrSQL, int aDataCount, string aTimeFormat)
        {
            ChecMysql80();
            //清理旧的数据
            for (int i = 1; i <= aDataCount; i++)
                aChart.Series[i].Points.Clear();

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionStr))
                {
                    connection.Open();
                    using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                    {
                        using (MySqlDataReader rd = sqlCmd.ExecuteReader())
                        {
                            if (rd != null && rd.HasRows)
                            {
                                while (rd.Read())//调用 Read 方法读取 SqlDataReader
                                {
                                    aChart.Series[1].Points.AddXY(
                                          rd.GetDateTime(0).ToString(aTimeFormat),//
                                          rd.GetFloat(1).ToString());
                                    aChart.Series[2].Points.AddXY(
                                          rd.GetDateTime(0).ToString(aTimeFormat),//
                                          rd.GetFloat(2).ToString());
                                    //充电为负
                                    if (rd.GetFloat(3) > 0)
                                        aChart.Series[3].Points.AddXY(
                                          rd.GetDateTime(0).ToString(aTimeFormat),//
                                          rd.GetFloat(3).ToString());
                                    else
                                        aChart.Series[3].Points.AddXY(
                                        rd.GetDateTime(0).ToString(aTimeFormat), "0");
                                    //放电为负
                                    if (rd.GetFloat(3) < 0)
                                        aChart.Series[4].Points.AddXY(
                                             rd.GetDateTime(0).ToString(aTimeFormat),//
                                             Math.Abs(rd.GetFloat(3)).ToString());
                                    else
                                        aChart.Series[4].Points.AddXY(
                                        rd.GetDateTime(0).ToString(aTimeFormat), "0");
                                }
                            }
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                log.Error(ex.Message);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }
            finally
            {

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

        static public bool UploadCloud(string astrSQL)
        {
            ChecMysql80();
            bool bResult = false;

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionStr))
                {
                    connection.Open();
                    using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                    {
                        using (MySqlDataReader rd = sqlCmd.ExecuteReader())
                        {
                            if (rd != null && rd.HasRows)
                            {
                                var rows = new System.Collections.Generic.List<Dictionary<string, object>>();
                                while (rd.Read())
                                {
                                    var row = new System.Collections.Generic.Dictionary<string, object>();
                                    for (int i = 0; i < rd.FieldCount; i++)
                                    {
                                        row[rd.GetName(i)] = rd.GetValue(i);
                                    }
                                    rows.Add(row);
                                }

                                if (rows.Count > 0)
                                {
                                    SaveJsonToFile(JsonConvert.SerializeObject(rows, Formatting.Indented));
                                    bResult = true;
                                }
                            }
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                log.Error(ex.Message);
            }
            catch (Exception ex)
            {
                log.Error(ex.Message);
            }
            finally
            {

            }
            return bResult;
        }


        public static void SaveJsonToFile(string jsonResult)
        {
            if (!string.IsNullOrEmpty(jsonResult))
            {
                var jsonArray = JArray.Parse(jsonResult);
                foreach (JObject jsonObject in jsonArray)
                {
                    var output = new
                    {
                         time = ConvertToUnixTimestamp(jsonObject["rTime"].Value<DateTime>()),
                         iot_code = frmMain.Selffrm.AllEquipment.iot_code,
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
                    string UploadID = Guid.NewGuid().ToString();
                    frmMain.Selffrm.AllEquipment.Report2Cloud.UploadProfit2Cloud(outputJson, UploadID);
                }
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

        /// <summary>
        ///  注册数据库相关操作
        /// </summary>
        /// <returns></returns>


        public static void UpdateDatabaseTable(string tableName, List<Column> targetColumns)
        {
            if (TableExists(tableName))
            {
                List<Column> existingColumns = GetTableColumns(tableName);

                if (!TableStructureMatches(existingColumns, targetColumns))
                {
                    // Backup the existing table
                    string backupTableName = tableName + "_backup";
                    string createBackupTableQuery = $"CREATE TABLE {backupTableName} AS SELECT * FROM {tableName};";
                    ExecSQL(createBackupTableQuery);

                    // Drop the original table
                    string dropTableQuery = $"DROP TABLE {tableName};";
                    ExecSQL(dropTableQuery);

                    // Create the new table with the target structure
                    CreateTable(tableName, targetColumns);

                    // Insert the data back into the new table
                    List<string> commonColumns = GetCommonColumns(existingColumns, targetColumns);
                    string columnsList = string.Join(", ", commonColumns);
                    string insertDataQuery = $"INSERT INTO {tableName} ({columnsList}) SELECT {columnsList} FROM {backupTableName};";
                    ExecSQL(insertDataQuery);

                    // Drop the backup table
                    string dropBackupTableQuery = $"DROP TABLE {backupTableName};";
                    ExecSQL(dropBackupTableQuery);
                }
                else
                {
                    // Drop columns that are not in targetColumns
                    List<string> columnsToDrop = existingColumns
                        .Where(col => !targetColumns.Any(tc => tc.Name.Equals(col.Name, StringComparison.OrdinalIgnoreCase)))
                        .Select(col => col.Name)
                        .ToList();

                    foreach (var columnName in columnsToDrop)
                    {
                        string dropColumnQuery = $"ALTER TABLE {tableName} DROP COLUMN {columnName};";
                        ExecSQL(dropColumnQuery);
                    }
                }
            }
            else
            {
                CreateTable(tableName, targetColumns);
            }
        }

        public static bool TableExists(string tableName)
        {
            if (connection == null || !IsConnected)
            {
                CreateConnection();
            }
            string query = $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'emsdata' AND table_name = '{tableName}';";
            MySqlCommand cmd = new MySqlCommand(query, connection);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static List<Column> GetTableColumns(string tableName)
        {
            // 按照创建时的顺序查询列信息
            string query = $@"
        SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_KEY
        FROM information_schema.columns
        WHERE table_schema = 'emsdata' AND table_name = '{tableName}'
        ORDER BY ORDINAL_POSITION;"; // 按照列的顺序排列

            MySqlCommand cmd = new MySqlCommand(query, connection);
            MySqlDataReader reader = cmd.ExecuteReader();

            List<Column> columns = new List<Column>();
            while (reader.Read())
            {
                columns.Add(new Column
                {
                    Name = reader.GetString("COLUMN_NAME"),
                    Type = reader.GetString("COLUMN_TYPE"),
                    IsNullable = reader.GetString("IS_NULLABLE") == "YES",
                    Key = reader.GetString("COLUMN_KEY") == "PRI" ? "PRIMARY KEY" : string.Empty
                });
            }
            reader.Close();

            // 检查 AUTO_INCREMENT
            string autoIncrementQuery = $"SELECT COLUMN_NAME FROM information_schema.columns WHERE table_schema = 'emsdata' AND table_name = '{tableName}' AND EXTRA LIKE '%auto_increment%';";
            MySqlCommand autoIncrementCmd = new MySqlCommand(autoIncrementQuery, connection);
            reader = autoIncrementCmd.ExecuteReader();

            while (reader.Read())
            {
                string columnName = reader.GetString("COLUMN_NAME");
                var column = columns.FirstOrDefault(c => c.Name == columnName);
                if (column != null)
                {
                    column.Key += " AUTO_INCREMENT";
                }
            }
            reader.Close();

            return columns;
        }

        public static void CreateTable(string tableName, List<Column> columns)
        {
            // 基础的 CREATE TABLE 语句
            string createTableQuery = $"CREATE TABLE `{tableName}` (";
            List<string> columnDefinitions = new List<string>();

            foreach (var column in columns)
            {
                // 确保类型定义中不包含不需要的精度信息
                string columnDefinition = $"`{column.Name}` {column.Type}";

                if (!column.IsNullable)
                    columnDefinition += " NOT NULL";

                if (!string.IsNullOrEmpty(column.Key))
                    columnDefinition += $" {column.Key}";

                if (!string.IsNullOrEmpty(column.Comment))
                    columnDefinition += $" COMMENT '{column.Comment}'";

                columnDefinitions.Add(columnDefinition);
            }

            createTableQuery += string.Join(", ", columnDefinitions);
            createTableQuery += ")";

            // 设置表级别的选项
            string tableOptions = " ENGINE=InnoDB AUTO_INCREMENT=1 ROW_FORMAT=DYNAMIC DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;";

            createTableQuery += tableOptions;

            // 输出生成的 SQL 语句以进行调试
            //log.Error(createTableQuery);

            // 执行 SQL 语句
            ExecSQL(createTableQuery); // Wait for table creation to complete
        }



        private static List<string> GetCommonColumns(List<Column> existingColumns, List<Column> targetColumns)
        {
            HashSet<string> existingColumnNames = new HashSet<string>(existingColumns.ConvertAll(c => c.Name));
            List<string> commonColumns = targetColumns.FindAll(c => existingColumnNames.Contains(c.Name)).ConvertAll(c => c.Name);
            return commonColumns;
        }

        private static bool TableStructureMatches(List<Column> existingColumns, List<Column> targetColumns)
        {
            if (existingColumns.Count != targetColumns.Count)
                return false;

            for (int i = 0; i < existingColumns.Count; i++)
            {
                if (existingColumns[i].Name != targetColumns[i].Name ||
                    existingColumns[i].Type != targetColumns[i].Type ||
                    existingColumns[i].IsNullable != targetColumns[i].IsNullable ||
                    existingColumns[i].Key != targetColumns[i].Key)
                    return false;
            }

            return true;
        }


        /// <summary>
        /// 对齐EMS与适配的数据库
        /// </summary>
        /// <returns></returns>
        /// 

        public static void CheckTables()
        {
            // 定义多个表的结构
            var tableStructures = new Dictionary<string, List<Column>>
            {
                {
                    "balatactics", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "startTime", Type = "time", IsNullable = true, Key = "", Comment = "均衡策略开始时间" },
                        new Column { Name = "endTime", Type = "time", IsNullable = true, Key = "", Comment = "均衡策略结束时间" },
                    }
                },
                {
                    "battery", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true, Key = "" },
                        new Column { Name = "batteryID", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v", Type = "float", IsNullable = true, Key = "", Comment = "电池簇电压" },
                        new Column { Name = "a", Type = "float", IsNullable = true, Key = "", Comment = "电池簇电流" },
                        new Column { Name = "soc", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "soh", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "insulationR", Type = "float", IsNullable = true, Key = "", Comment = "绝缘电阻" },
                        new Column { Name = "positiveR", Type = "float", IsNullable = true, Key = "", Comment = "正极绝缘" },
                        new Column { Name = "negativeR", Type = "float", IsNullable = true, Key = "", Comment = "负极绝缘" },
                        new Column { Name = "cellMaxV", Type = "float", IsNullable = true, Key = "", Comment = "单体最高电压" },
                        new Column { Name = "cellIDMaxV", Type = "int", IsNullable = true, Key = "", Comment = "高电压Cell ID" },
                        new Column { Name = "cellMinV", Type = "float", IsNullable = true, Key = "", Comment = "单体最低电压" },
                        new Column { Name = "cellIDMinV", Type = "int", IsNullable = true, Key = "", Comment = "低电压CellID" },
                        new Column { Name = "cellMaxTemp", Type = "float", IsNullable = true, Key = "", Comment = "最高温度" },
                        new Column { Name = "cellIDMaxtemp", Type = "int", IsNullable = true, Key = "", Comment = "最高温CellID" },
                        new Column { Name = "averageV", Type = "float", IsNullable = true, Key = "", Comment = "平均电压" },
                        new Column { Name = "averageTemp", Type = "float", IsNullable = true, Key = "", Comment = "平均温度" }
                    }
                },
                {
                    "cellstemp", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true, Key = "" },
                        new Column { Name = "clusterID", Type = "int", IsNullable = true, Key = "", Comment = "电池簇ID" },
                        new Column { Name = "a", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v1", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v2", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v3", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v4", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v5", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v6", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v7", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v8", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v9", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v10", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v11", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v12", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v13", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v14", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v15", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v16", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v17", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v18", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v19", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v20", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v21", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v22", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v23", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v24", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v25", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v26", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v27", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v28", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v29", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v30", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v31", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v32", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v33", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v34", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v35", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v36", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v37", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v38", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v39", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v40", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v41", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v42", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v43", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v44", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v45", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v46", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v47", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v48", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v49", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v50", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v51", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v52", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v53", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v54", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v55", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v56", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v57", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v58", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v59", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v60", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v61", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v62", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v63", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v64", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v65", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v66", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v67", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v68", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v69", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v70", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v71", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v72", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v73", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v74", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v75", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v76", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v77", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v78", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v79", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v80", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v81", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v82", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v83", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v84", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v85", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v86", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v87", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v88", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v89", Type = "float", IsNullable = false, Key = "" },
                        new Column { Name = "v90", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v91", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v92", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v93", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v94", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v95", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v96", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v97", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v98", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v99", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v100", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v101", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v102", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v103", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v104", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v105", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v106", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v107", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v108", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v109", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v110", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v111", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v112", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v113", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v114", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v115", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v116", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v117", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v118", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v119", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v120", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v121", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v122", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v123", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v124", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v125", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v126", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v127", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v128", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v129", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v130", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v131", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v132", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v133", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v134", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v135", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v136", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v137", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v138", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v139", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v140", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v141", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v142", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v143", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v144", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v145", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v146", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v147", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v148", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v149", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v150", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v151", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v152", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v153", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v154", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v155", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v156", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v157", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v158", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v159", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v160", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v161", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v162", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v163", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v164", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v165", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v166", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v167", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v168", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v169", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v170", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v171", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v172", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v173", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v174", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v175", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v176", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v177", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v178", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v179", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v180", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v181", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v182", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v183", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v184", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v185", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v186", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v187", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v188", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v189", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v190", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v191", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v192", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v193", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v194", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v195", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v196", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v197", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v198", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v199", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v200", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v201", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v202", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v203", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v204", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v205", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v206", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v207", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v208", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v209", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v210", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v211", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v212", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v213", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v214", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v215", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v216", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v217", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v218", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v219", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v220", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v221", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v222", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v223", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v224", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v225", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v226", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v227", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v228", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v229", Type = "float", IsNullable = false, Key = "" },
                        new Column { Name = "v230", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v231", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v232", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v233", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v234", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v235", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v236", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v237", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v238", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v239", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v240", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v241", Type = "float", IsNullable = true, Key = "" },
                    }
                },
                {
                    "cellsv", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true, Key = "" },
                        new Column { Name = "clusterID", Type = "int", IsNullable = true, Key = "", Comment = "单体最高温度ID" },
                        new Column { Name = "a", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v1", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v2", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v3", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v4", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v5", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v6", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v7", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v8", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v9", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v10", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v11", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v12", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v13", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v14", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v15", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v16", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v17", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v18", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v19", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v20", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v21", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v22", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v23", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v24", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v25", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v26", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v27", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v28", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v29", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v30", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v31", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v32", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v33", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v34", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v35", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v36", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v37", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v38", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v39", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v40", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v41", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v42", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v43", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v44", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v45", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v46", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v47", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v48", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v49", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v50", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v51", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v52", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v53", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v54", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v55", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v56", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v57", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v58", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v59", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v60", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v61", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v62", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v63", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v64", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v65", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v66", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v67", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v68", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v69", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v70", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v71", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v72", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v73", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v74", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v75", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v76", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v77", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v78", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v79", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v80", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v81", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v82", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v83", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v84", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v85", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v86", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v87", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v88", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v89", Type = "float", IsNullable = false, Key = "" },
                        new Column { Name = "v90", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v91", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v92", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v93", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v94", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v95", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v96", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v97", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v98", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v99", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v100", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v101", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v102", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v103", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v104", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v105", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v106", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v107", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v108", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v109", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v110", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v111", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v112", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v113", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v114", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v115", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v116", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v117", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v118", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v119", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v120", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v121", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v122", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v123", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v124", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v125", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v126", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v127", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v128", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v129", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v130", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v131", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v132", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v133", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v134", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v135", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v136", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v137", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v138", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v139", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v140", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v141", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v142", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v143", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v144", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v145", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v146", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v147", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v148", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v149", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v150", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v151", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v152", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v153", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v154", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v155", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v156", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v157", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v158", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v159", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v160", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v161", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v162", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v163", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v164", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v165", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v166", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v167", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v168", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v169", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v170", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v171", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v172", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v173", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v174", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v175", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v176", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v177", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v178", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v179", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v180", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v181", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v182", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v183", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v184", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v185", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v186", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v187", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v188", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v189", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v190", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v191", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v192", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v193", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v194", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v195", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v196", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v197", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v198", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v199", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v200", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v201", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v202", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v203", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v204", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v205", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v206", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v207", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v208", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v209", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v210", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v211", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v212", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v213", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v214", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v215", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v216", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v217", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v218", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v219", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v220", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v221", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v222", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v223", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v224", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v225", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v226", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v227", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v228", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v229", Type = "float", IsNullable = false, Key = "" },
                        new Column { Name = "v230", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v231", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v232", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v233", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v234", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v235", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v236", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v237", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v238", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v239", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v240", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "v241", Type = "float", IsNullable = true, Key = "" },
                    }
                },
                {
                    "chargeinform", new List<Column>
                    {
                        new Column { Name = "cellIDMaxtemp", Type = "double", IsNullable = true, Key = "", Comment = "单体最高温度ID" },
                        new Column { Name = "cellMaxTemp", Type = "double", IsNullable = true, Key = "", Comment = "单体最高温度" },
                        new Column { Name = "cellIDMaxV", Type = "double", IsNullable = true, Key = "", Comment = "单体最高电压ID" },
                        new Column { Name = "cellMaxV", Type = "double", IsNullable = true, Key = "", Comment = "单体最高电压" },
                        new Column { Name = "cellIDMinV", Type = "double", IsNullable = true, Key = "", Comment = "单体最低电压ID" },
                        new Column { Name = "cellMinV", Type = "double", IsNullable = true, Key = "", Comment = "单体最低电压" },
                        new Column { Name = "BMSa", Type = "double", IsNullable = true, Key = "", Comment = "电流" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true, Key = "", Comment = "发生时间" },
                        new Column { Name = "Warning", Type = "varchar(255)", IsNullable = true, Key = "", Comment = "告警信息" },
                    }
                },
                {
                    "config", new List<Column>
                    {
                        new Column { Name = "SysID", Type = "varchar(255)", IsNullable = false, Key = "PRIMARY KEY" },
                        new Column { Name = "Open104", Type = "int", IsNullable = true, Key = "" , Comment = "是否开启104服务 0关1开" },
                        new Column { Name = "NetTick", Type = "int", IsNullable = true, Key = "" , Comment = "判断超时的时间间隔" },
                        new Column { Name = "SysName", Type = "varchar(255)", IsNullable = true, Key = ""},
                        new Column { Name = "SysPower", Type = "int", IsNullable = true, Key = "" , Comment = "储能柜容量规格" },
                        new Column { Name = "SysSelfPower", Type = "int", IsNullable = true, Key = ""},
                        new Column { Name = "SysAddr", Type = "varchar(255)", IsNullable = true, Key = "" },
                        new Column { Name = "SysInstTime", Type = "datetime", IsNullable = true, Key = "" },
                        new Column { Name = "CellCount", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "SysInterval", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "YunInterval", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "IsMaster", Type = "int", IsNullable = true, Key = ""  , Comment = "是否是主机"},
                        new Column { Name = "Master485Addr", Type = "int", IsNullable = true, Key = "" , Comment = "主机设备地址" },
                        new Column { Name = "i485Addr", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "AutoRun", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "SysMode", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "PCSGridModel", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DebugComName", Type = "varchar(255)", IsNullable = true, Key = "" },
                        new Column { Name = "DebugRate", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "SysCount", Type = "int", IsNullable = true, Key = ""  , Comment = "储能电站总台数"},
                        new Column { Name = "UseYunTactics", Type = "int", IsNullable = true, Key = ""  , Comment = "是否接收云策略"},
                        new Column { Name = "UseBalaTactics", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "iPCSfactory", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "BMSVerb", Type = "int", IsNullable = true, Key = ""  , Comment = "BMS供应商版本"},
                        new Column { Name = "PCSForceRun", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "EMSstatus", Type = "int", IsNullable = true, Key = ""  , Comment = "EMS工作状态"},                     
                        new Column { Name = "GPIOSelect", Type = "int", IsNullable = true, Key = ""  , Comment = "IO口配置"},
                        new Column { Name = "MasterIp", Type = "varchar(255)", IsNullable = true, Key = "" , Comment = "主机IP地址" },
                        new Column { Name = "ConnectStatus", Type = "varchar(255)", IsNullable = true, Key = "" , Comment = "主从通讯方式" },
                        new Column { Name = "CellVNum", Type = "int", IsNullable = true, Key = "" , Comment = "电池电压数量" },
                        new Column { Name = "CellTNum", Type = "int", IsNullable = true, Key = "" , Comment = "电池温度数量" }
                    }
                },
                {
                    "ComponentSettings", new List<Column>
                    {
                        //空调
                        new Column { Name = "SetHotTemp", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "SetCoolTemp", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "CoolTempReturn", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "HotTempReturn", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "SetHumidity", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "HumiReturn", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "TCRunWithSys", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "TCAuto", Type = "int", IsNullable = false, Key = "" },
                        new Column { Name = "TCMode", Type = "int", IsNullable = true, Key = ""  },
                        new Column { Name = "TCMaxTemp", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "TCMinTemp", Type = "double", IsNullable = true, Key = ""},
                        new Column { Name = "TCMaxHumi", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "TCMinHumi", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "FenMaxTemp", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "FenMinTemp", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "FenMode", Type = "int", IsNullable = true, Key = "" },
                        //液冷
                        new Column { Name = "LCModel", Type = "int", IsNullable = true, Key = ""},
                        new Column { Name = "LCTemperSelect", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "LCWaterPump", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "LCSetHotTemp", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "LCSetCoolTemp", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "LCHotTempReturn", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "LCCoolTempReturn", Type = "double", IsNullable = true, Key = "" },
                        //除湿机
                        new Column { Name = "DHSetRunStatus", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "DHSetTempBoot", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "DHSetTempStop", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "DHSetHumidityBoot", Type = "double", IsNullable = true, Key = "" },
                        new Column { Name = "DHSetHumidityStop", Type = "double", IsNullable = true, Key = "" }
                    }
                },
                {
                    "electrovalence", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true, Key = "" },
                        new Column { Name = "section", Type = "int", IsNullable = true, Key = "", Comment = "区段0/1" },
                        new Column { Name = "startTime", Type = "time", IsNullable = true, Key = "" },
                        new Column { Name = "eName", Type = "varchar(10)", IsNullable = true, Key = "", Comment = "无尖峰平谷" }
                    }
                },
                {
                    "elemeter1", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true, Key = "" },
                        new Column { Name = "Ukwh", Type = "float", IsNullable = false, Key = "", Comment = "组合有功总电能" },
                        new Column { Name = "Nukwh", Type = "float", IsNullable = true, Key = "", Comment = "综合无功总电能" },
                        new Column { Name = "AllUkva", Type = "float", IsNullable = true, Key = "", Comment = "总有用功率" },
                        new Column { Name = "AllNukva", Type = "float", IsNullable = true, Key = "", Comment = "总无用功率" },
                        new Column { Name = "AllAAkva", Type = "float", IsNullable = true, Key = "", Comment = "总视在用功率" },
                        new Column { Name = "AllPFoctor", Type = "float", IsNullable = true, Key = "", Comment = "总功率因数" },
                        new Column { Name = "HZ", Type = "float", IsNullable = true, Key = "", Comment = "频率" }
                    }
                },
                {
                    "elemeter2", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true, Key = "" },
                        new Column { Name = "Ukwh", Type = "float", IsNullable = false, Key = "", Comment = "组合有功总电能" },
                        new Column { Name = "UkwhJ", Type = "float", IsNullable = true, Key = "", Comment = "组合有功尖" },
                        new Column { Name = "UkwhF", Type = "float", IsNullable = true, Key = "", Comment = "组合有功峰" },
                        new Column { Name = "UkwhP", Type = "float", IsNullable = true, Key = "", Comment = "组合有功平" },
                        new Column { Name = "UkwhG", Type = "float", IsNullable = true, Key = "", Comment = "组合有功谷" },
                        new Column { Name = "PUkwh", Type = "float", IsNullable = true, Key = "", Comment = "正向有功总电能" },
                        new Column { Name = "PUkwhJ", Type = "float", IsNullable = true, Key = "", Comment = "正有功J尖" },
                        new Column { Name = "PUkwhF", Type = "float", IsNullable = true, Key = "", Comment = "正有功J峰" },
                        new Column { Name = "PUkwhP", Type = "float", IsNullable = true, Key = "", Comment = "正有功J平" },
                        new Column { Name = "PUkwhG", Type = "float", IsNullable = true, Key = "", Comment = "正有功J谷" },
                        new Column { Name = "OUkwh", Type = "float", IsNullable = true, Key = "", Comment = "反向有功总电能" },
                        new Column { Name = "OUkwhJ", Type = "float", IsNullable = true, Key = "", Comment = "反有功尖" },
                        new Column { Name = "OUkwhF", Type = "float", IsNullable = true, Key = "", Comment = "反有功峰" },
                        new Column { Name = "OUkwhP", Type = "float", IsNullable = true, Key = "", Comment = "反有功平" },
                        new Column { Name = "OUkwhG", Type = "float", IsNullable = true, Key = "", Comment = "反有功谷" },
                        new Column { Name = "Nukwh", Type = "float", IsNullable = true, Key = "", Comment = "综合无功总电能" },
                        new Column { Name = "NukwhJ", Type = "float", IsNullable = true, Key = "", Comment = "综合无功总尖" },
                        new Column { Name = "NukwhF", Type = "float", IsNullable = true, Key = "", Comment = "综合无功总峰" },
                        new Column { Name = "NukwhP", Type = "float", IsNullable = true, Key = "", Comment = "综合无功总平" },
                        new Column { Name = "NukwhG", Type = "float", IsNullable = true, Key = "", Comment = "综合无功总谷" },
                        new Column { Name = "PNukwh", Type = "float", IsNullable = true, Key = "", Comment = "正向无功总电能" },
                        new Column { Name = "PNukwhJ", Type = "float", IsNullable = true, Key = "", Comment = "正向无功尖" },
                        new Column { Name = "PNukwhF", Type = "float", IsNullable = true, Key = "", Comment = "正向无功峰" },
                        new Column { Name = "PNukwhP", Type = "float", IsNullable = true, Key = "", Comment = "正向无功平" },
                        new Column { Name = "PNukwhG", Type = "float", IsNullable = true, Key = "", Comment = "正向无功谷" },
                        new Column { Name = "ONukwh", Type = "float", IsNullable = true, Key = "", Comment = "反向无功总电能" },
                        new Column { Name = "ONukwhJ", Type = "float", IsNullable = true, Key = "", Comment = "反向无功尖" },
                        new Column { Name = "ONukwhF", Type = "float", IsNullable = true, Key = "", Comment = "反向无功峰" },
                        new Column { Name = "ONukwhP", Type = "float", IsNullable = true, Key = "", Comment = "反向无功平" },
                        new Column { Name = "ONukwhG", Type = "float", IsNullable = true, Key = "", Comment = "反向无功谷" },
                        new Column { Name = "AllUkva", Type = "float", IsNullable = true, Key = "", Comment = "总有用功率" },
                        new Column { Name = "AUkva", Type = "float", IsNullable = true, Key = "", Comment = "A 有功功率" },
                        new Column { Name = "BUkva", Type = "float", IsNullable = true, Key = "", Comment = "B 有功功率" },
                        new Column { Name = "CUkva", Type = "float", IsNullable = true, Key = "", Comment = "C 有功功率" },
                        new Column { Name = "AllNukva", Type = "float", IsNullable = true, Key = "", Comment = "总无用功率" },
                        new Column { Name = "ANukva", Type = "float", IsNullable = true, Key = "", Comment = "A 无功功率" },
                        new Column { Name = "BNukva", Type = "float", IsNullable = true, Key = "", Comment = "B 无功功率" },
                        new Column { Name = "CNukva", Type = "float", IsNullable = true, Key = "", Comment = "C 无功功率" },
                        new Column { Name = "AllAAkva", Type = "float", IsNullable = true, Key = "", Comment = "总视在用功率" },
                        new Column { Name = "AAkva", Type = "float", IsNullable = true, Key = "", Comment = "A 视在功功率" },
                        new Column { Name = "BAkva", Type = "float", IsNullable = true, Key = "", Comment = "B 视在功功率" },
                        new Column { Name = "CAkva", Type = "float", IsNullable = true, Key = "", Comment = "C 视在功功率" },
                        new Column { Name = "Aa", Type = "float", IsNullable = true, Key = "", Comment = "A电流" },
                        new Column { Name = "Ba", Type = "float", IsNullable = true, Key = "", Comment = "B电流" },
                        new Column { Name = "Ca", Type = "float", IsNullable = true, Key = "", Comment = "C电流" },
                        new Column { Name = "Akv", Type = "float", IsNullable = true, Key = "", Comment = "电压A" },
                        new Column { Name = "Bkv", Type = "float", IsNullable = true, Key = "", Comment = "电压B" },
                        new Column { Name = "Ckv", Type = "float", IsNullable = true, Key = "", Comment = "电压C" },
                        new Column { Name = "ABkv", Type = "float", IsNullable = true, Key = "", Comment = "两项电压AB" },
                        new Column { Name = "BCkv", Type = "float", IsNullable = true, Key = "", Comment = "两项电压BC" },
                        new Column { Name = "CAkv", Type = "float", IsNullable = true, Key = "", Comment = "两项电压CA" },
                        new Column { Name = "AllPFoctor", Type = "float", IsNullable = true, Key = "", Comment = "总功率因数" },
                        new Column { Name = "APFoctor", Type = "float", IsNullable = true, Key = "", Comment = "A功率因数" },
                        new Column { Name = "BPFoctor", Type = "float", IsNullable = true, Key = "", Comment = "B功率因数" },
                        new Column { Name = "CPFoctor", Type = "float", IsNullable = true, Key = "", Comment = "C功率因数" },
                        new Column { Name = "HZ", Type = "float", IsNullable = true, Key = "", Comment = "频率" },
                        new Column { Name = "Gridkva", Type = "float", IsNullable = true, Key = "", Comment = "电网功率" },
                        new Column { Name = "Totalkva", Type = "float", IsNullable = true, Key = "", Comment = "总功率" },
                        new Column { Name = "Subkw", Type = "float", IsNullable = true, Key = "", Comment = "辅组电表功率（有功）" },
                        new Column { Name = "Subkwh", Type = "float", IsNullable = true, Key = "", Comment = "辅组电表的电能（有功）" },
                        new Column { Name = "PlanKW", Type = "float", IsNullable = true, Key = "", Comment = "计划功率（策略、手工或者调度功率）" }
                    }
                },
                {
                    "elemeter3", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true, Key = "" },
                        new Column { Name = "Akwh", Type = "float", IsNullable = false, Key = "", Comment = "组合有功总电能" },
                        new Column { Name = "Ukva", Type = "float", IsNullable = true, Key = "", Comment = "总有用功率" },
                        new Column { Name = "Nukva", Type = "float", IsNullable = true, Key = "", Comment = "总无用功率" },
                        new Column { Name = "Akva", Type = "float", IsNullable = true, Key = "", Comment = "总视在用功率" },
                        new Column { Name = "AkwhJ", Type = "float", IsNullable = false, Key = "", Comment = "组合有功总电能尖" },
                        new Column { Name = "AkwhF", Type = "float", IsNullable = false, Key = "", Comment = "组合有功总电能峰" },
                        new Column { Name = "AkwhP", Type = "float", IsNullable = false, Key = "", Comment = "组合有功总电能平" },
                        new Column { Name = "AkwhG", Type = "float", IsNullable = false, Key = "", Comment = "组合有功总电能谷" },
                        new Column { Name = "V", Type = "float", IsNullable = true, Key = "", Comment = "电压" },
                        new Column { Name = "A", Type = "float", IsNullable = true, Key = "", Comment = "电流" }
                    }
                },
                {
                    "elemeter4", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true, Key = "" },
                        new Column { Name = "Ukwh", Type = "float", IsNullable = false, Key = "", Comment = "组合有功总电能" },
                        new Column { Name = "UkwhJ", Type = "float", IsNullable = true, Key = "", Comment = "组合有功尖" },
                        new Column { Name = "UkwhF", Type = "float", IsNullable = true, Key = "", Comment = "组合有功峰" },
                        new Column { Name = "UkwhP", Type = "float", IsNullable = true, Key = "", Comment = "组合有功平" },
                        new Column { Name = "UkwhG", Type = "float", IsNullable = true, Key = "", Comment = "组合有功谷" },
                        new Column { Name = "PUkwh", Type = "float", IsNullable = true, Key = "", Comment = "正向有功总电能" },
                        new Column { Name = "PUkwhJ", Type = "float", IsNullable = true, Key = "", Comment = "正有功J尖" },
                        new Column { Name = "PUkwhF", Type = "float", IsNullable = true, Key = "", Comment = "正有功J峰" },
                        new Column { Name = "PUkwhP", Type = "float", IsNullable = true, Key = "", Comment = "正有功J平" },
                        new Column { Name = "PUkwhG", Type = "float", IsNullable = true, Key = "", Comment = "正有功J谷" },
                        new Column { Name = "OUkwh", Type = "float", IsNullable = true, Key = "", Comment = "反向有功总电能" },
                        new Column { Name = "OUkwhJ", Type = "float", IsNullable = true, Key = "", Comment = "反有功尖" },
                        new Column { Name = "OUkwhF", Type = "float", IsNullable = true, Key = "", Comment = "反有功峰" },
                        new Column { Name = "OUkwhP", Type = "float", IsNullable = true, Key = "", Comment = "反有功平" },
                        new Column { Name = "OUkwhG", Type = "float", IsNullable = true, Key = "", Comment = "反有功谷" },
                        new Column { Name = "Nukwh", Type = "float", IsNullable = true, Key = "", Comment = "综合无功总电能" },
                        new Column { Name = "NukwhJ", Type = "float", IsNullable = true, Key = "", Comment = "综合无功总尖" },
                        new Column { Name = "NukwhF", Type = "float", IsNullable = true, Key = "", Comment = "综合无功总峰" },
                        new Column { Name = "NukwhP", Type = "float", IsNullable = true, Key = "", Comment = "综合无功总平" },
                        new Column { Name = "NukwhG", Type = "float", IsNullable = true, Key = "", Comment = "综合无功总谷" },
                        new Column { Name = "PNukwh", Type = "float", IsNullable = true, Key = "", Comment = "正向无功总电能" },
                        new Column { Name = "PNukwhJ", Type = "float", IsNullable = true, Key = "", Comment = "正向无功尖" },
                        new Column { Name = "PNukwhF", Type = "float", IsNullable = true, Key = "", Comment = "正向无功峰" },
                        new Column { Name = "PNukwhP", Type = "float", IsNullable = true, Key = "", Comment = "正向无功平" },
                        new Column { Name = "PNukwhG", Type = "float", IsNullable = true, Key = "", Comment = "正向无功谷" },
                        new Column { Name = "ONukwh", Type = "float", IsNullable = true, Key = "", Comment = "反向无功总电能" },
                        new Column { Name = "ONukwhJ", Type = "float", IsNullable = true, Key = "", Comment = "反向无功尖" },
                        new Column { Name = "ONukwhF", Type = "float", IsNullable = true, Key = "", Comment = "反向无功峰" },
                        new Column { Name = "ONukwhP", Type = "float", IsNullable = true, Key = "", Comment = "反向无功平" },
                        new Column { Name = "ONukwhG", Type = "float", IsNullable = true, Key = "", Comment = "反向无功谷" },
                        new Column { Name = "AllUkva", Type = "float", IsNullable = true, Key = "", Comment = "总有用功率" },
                        new Column { Name = "AUkva", Type = "float", IsNullable = true, Key = "", Comment = "A 有功功率" },
                        new Column { Name = "BUkva", Type = "float", IsNullable = true, Key = "", Comment = "B 有功功率" },
                        new Column { Name = "CUkva", Type = "float", IsNullable = true, Key = "", Comment = "C 有功功率" },
                        new Column { Name = "AllNukva", Type = "float", IsNullable = true, Key = "", Comment = "总无用功率" },
                        new Column { Name = "ANukva", Type = "float", IsNullable = true, Key = "", Comment = "A 无功功率" },
                        new Column { Name = "BNukva", Type = "float", IsNullable = true, Key = "", Comment = "B 无功功率" },
                        new Column { Name = "CNukva", Type = "float", IsNullable = true, Key = "", Comment = "C 无功功率" },
                        new Column { Name = "AllAAkva", Type = "float", IsNullable = true, Key = "", Comment = "总视在用功率" },
                        new Column { Name = "AAkva", Type = "float", IsNullable = true, Key = "", Comment = "A 视在功功率" },
                        new Column { Name = "BAkva", Type = "float", IsNullable = true, Key = "", Comment = "B 视在功功率" },
                        new Column { Name = "CAkva", Type = "float", IsNullable = true, Key = "", Comment = "C 视在功功率" },
                        new Column { Name = "Aa", Type = "float", IsNullable = true, Key = "", Comment = "A电流" },
                        new Column { Name = "Ba", Type = "float", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "Ca", Type = "float", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "Akv", Type = "float", IsNullable = true, Key = "", Comment = "电压" },
                        new Column { Name = "Bkv", Type = "float", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "Ckv", Type = "float", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "ABkv", Type = "float", IsNullable = true, Key = "", Comment = "两项电压" },
                        new Column { Name = "BCkv", Type = "float", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "CAkv", Type = "float", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "AllPFoctor", Type = "float", IsNullable = true, Key = "", Comment = "总功率因数" },
                        new Column { Name = "APFoctor", Type = "float", IsNullable = true, Key = "", Comment = "A功率因数" },
                        new Column { Name = "BPFoctor", Type = "float", IsNullable = true, Key = "", Comment = "B功率因数" },
                        new Column { Name = "CPFoctor", Type = "float", IsNullable = true, Key = "", Comment = "C功率因数" },
                        new Column { Name = "HZ", Type = "float", IsNullable = true, Key = "", Comment = "频率" },
                        new Column { Name = "Gridkva", Type = "float", IsNullable = true, Key = "", Comment = "电网功率" },
                        new Column { Name = "Totalkva", Type = "float", IsNullable = true, Key = "", Comment = "总功率" },
                        new Column { Name = "Subkw", Type = "float", IsNullable = true, Key = "", Comment = "辅组电表功率（有功）" },
                        new Column { Name = "Subkwh", Type = "float", IsNullable = true, Key = "", Comment = "辅组电表的电能（有功）" }
                    }
                },
                {
                    "equipment", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "eName", Type = "varchar(50)", IsNullable = true, Key = "", Comment = "设备唯一名称(设备唯一ID)"},
                        new Column { Name = "eID", Type = "int", IsNullable = true, Key = "", Comment = "设备地址" },
                        new Column { Name = "eType", Type = "varchar(50)", IsNullable = true, Key = "", Comment = "设备类型" },
                        new Column { Name = "eModel", Type = "varchar(50)", IsNullable = true, Key = "", Comment = "型号" },
                        new Column { Name = "comType", Type = "int", IsNullable = true, Key = "", Comment = "通讯类型0：485；1TCP；2UDP" },
                        new Column { Name = "comName", Type = "varchar(6)", IsNullable = true, Key = "", Comment = "串口名"},
                        new Column { Name = "comRate", Type = "int", IsNullable = true, Key = "", Comment = "速率" },
                        new Column { Name = "comBits", Type = "int", IsNullable = true, Key = "", Comment = "数据位" },
                        new Column { Name = "serverIP", Type = "varchar(15)", IsNullable = true, Key = "", Comment = "IP"},
                        new Column { Name = "TCPType", Type = "varchar(15)", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "SerPort", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "LocPort", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "memo", Type = "text", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "pc", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "pt", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "ct", Type = "int", IsNullable = true, Key = "", Comment = "" }
                    }
                },
                {
                    "errorstate", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "TCError", Type = "bigint", IsNullable = true, Key = "", Comment = "32位" },
                        new Column { Name = "PCSError1", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "PCSError2", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "PCSError3", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "PCSError4", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "PCSError5", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "PCSError6", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "PCSError7", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "PCSError8", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "BMSError1", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "BMSError2", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "BMSError3", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "BMSError4", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "BMSError5", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "EMSError", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "EMSError1", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "EMSError2", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "EMSError3", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "EMSError4", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "LCError1", Type = "int", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "LCError2", Type = "int", IsNullable = true, Key = "", Comment = "" }
                    }
                },
                {
                    "fire", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "firestate", Type = "int", IsNullable = true, Key = "", Comment = "消防系统状态" },
                        new Column { Name = "temp", Type = "float", IsNullable = true, Key = "", Comment = "温度" },
                        new Column { Name = "humidity", Type = "float", IsNullable = true, Key = "", Comment = "湿度" },
                        new Column { Name = "waterlogging1", Type = "int", IsNullable = true, Key = "", Comment = "水浸1" },
                        new Column { Name = "waterlogging2", Type = "int", IsNullable = true, Key = "", Comment = "水浸2" },
                        new Column { Name = "smoke", Type = "int", IsNullable = true, Key = "", Comment = "烟感100-10000ppm" },
                        new Column { Name = "CO", Type = "int", IsNullable = true, Key = "", Comment = "一氧化碳含量 0.001精度 ppm" }
                    }
                },
                {
                    "CloudLimits", new List<Column>
                    {
                        new Column { Name = "MaxGridKW", Type = "int", IsNullable = true, Key = "", Comment = "目标电网功率上限" },
                        new Column { Name = "MinGridKW", Type = "int", IsNullable = true, Key = "", Comment = "目标电网功率下限" },
                        new Column { Name = "MaxSOC", Type = "int", IsNullable = true, Key = "", Comment = "最高SOC" },
                        new Column { Name = "MinSOC", Type = "int", IsNullable = true, Key = "", Comment = "最低SOC" },
                        new Column { Name = "WarnMaxGridKW", Type = "int", IsNullable = true, Key = "", Comment = "限制电网功率上限" },
                        new Column { Name = "WarnMinGridKW", Type = "int", IsNullable = true, Key = "", Comment = "限制电网功率下限" },
                        new Column { Name = "PcsKva", Type = "int", IsNullable = true, Key = "", Comment = "触发需量抬升的放电功率" },
                        new Column { Name = "Pre_Client_PUMdemand_Max", Type = "int", IsNullable = true, Key = "", Comment = "设置客户本月预计最大需量" },
                        new Column { Name = "EnableActiveReduce", Type = "int", IsNullable = true, Key = "", Comment = "开启主动降容：1(开) 0(关)" },
                        new Column { Name = "PumScale", Type = "int", IsNullable = true, Key = "", Comment = "需量比例" },
                        new Column { Name = "AllUkvaWindowSize", Type = "int", IsNullable = true, Key = "", Comment = "电网功率队列大小" },
                        new Column { Name = "PumTime", Type = "int", IsNullable = true, Key = "", Comment = "强制放电时间" },
                        new Column { Name = "BmsDerateRatio", Type = "int", IsNullable = true, Key = "", Comment = "BMS触发1级告警致功率降额比" },
                        new Column { Name = "FrigOpenLower", Type = "int", IsNullable = true, Key = "", Comment = "制冷设备开启下限" },
                        new Column { Name = "FrigOffLower", Type = "int", IsNullable = true, Key = "", Comment = "制冷设备关闭启下限" },
                        new Column { Name = "FrigOffUpper", Type = "int", IsNullable = true, Key = "", Comment = "制冷设备关闭启上限" },
                        new Column { Name = "BoxHTemperAlarm", Type = "int", IsNullable = true, Key = "", Comment = "柜内高温告警值" },
                        new Column { Name = "BoxLTemperAlarm", Type = "int", IsNullable = true, Key = "", Comment = "柜内低温告警值" }
                    }
                },
                {
                    "liquidcool", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "state", Type = "int", IsNullable = true, Key = "", Comment = "开关状态" },
                        new Column { Name = "OutwaterTemp", Type = "float", IsNullable = true, Key = "", Comment = "出水温度" },
                        new Column { Name = "InwaterTemp", Type = "float", IsNullable = true, Key = "", Comment = "回水温度" },
                        new Column { Name = "environmentTemp", Type = "float", IsNullable = true, Key = "", Comment = "环境温度" },
                        new Column { Name = "ExgasTemp", Type = "float", IsNullable = true, Key = "", Comment = "排气温度" },
                        new Column { Name = "InwaterPressure", Type = "float", IsNullable = true, Key = "", Comment = "进水压力" },
                        new Column { Name = "OutwaterPressure", Type = "float", IsNullable = true, Key = "", Comment = "出水压力" },
                        new Column { Name = "Error1", Type = "int", IsNullable = true, Key = "", Comment = "故障码" },
                        new Column { Name = "Error2", Type = "int", IsNullable = true, Key = "", Comment = "故障码" }
                    }
                },
                {
                   "log", new List<Column>
                   {
                       new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true, Key = "", Comment = "" },
                        new Column {Name = "eClass", Type = "varchar(20)", IsNullable = true, Key = "", Comment = ""},
                        new Column {Name = "Event", Type = "varchar(255)", IsNullable = true, Key = "", Comment = ""},
                        new Column { Name = "eTime", Type = "datetime", IsNullable = true, Key = "", Comment = "" },
                        new Column { Name = "Memo", Type = "text", IsNullable = true, Key = "", Comment = "" }
                    }
                },
                {
                    "pcs", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true, Key = "" },
                        new Column { Name = "State", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "aV", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "bV", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "cV", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "aA", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "bA", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "cA", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "hz", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "aUkwa", Type = "float", IsNullable = true,  Key = "", Comment = "A 相输出有功功率" },
                        new Column { Name = "bUkwa", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "cUkwa", Type = "float", IsNullable = true , Key = ""},
                        new Column { Name = "allUkwa", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "aNUkwr", Type = "float", IsNullable = true,  Key = "", Comment = "A 相输出无功功率" },
                        new Column { Name = "bNUkwr", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "cNUkwr", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "allNUkwr", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "aAkwa", Type = "float", IsNullable = true,  Key = "", Comment = "A 相输出视在功率" },
                        new Column { Name = "bAkwa", Type = "float", IsNullable = true,  Key = "", Comment = "apparent pow" },
                        new Column { Name = "cAkwa", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "allAkwa", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "aPFactor", Type = "float", IsNullable = true,  Key = "", Comment = "A相功率因数" },
                        new Column { Name = "bPFactor", Type = "float", IsNullable = true,  Key = "", Comment = "Power Factor" },
                        new Column { Name = "cPFactor", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "allPFactor", Type = "float", IsNullable = true, Key = "" },
                        new Column { Name = "inputPower", Type = "float", IsNullable = true,  Key = "", Comment = "输入功率" },
                        new Column { Name = "inputV", Type = "float", IsNullable = true,  Key = "", Comment = "输入电压" },
                        new Column { Name = "inputA", Type = "float", IsNullable = true,  Key = "", Comment = "输入电流" },
                        new Column { Name = "PCSTemp", Type = "float", IsNullable = true,  Key = "", Comment = "散热片温度" },
                        new Column { Name = "ACInkwh", Type = "float", IsNullable = true,  Key = "", Comment = "交流累计充电电量" },
                        new Column { Name = "ACOutkwh", Type = "float", IsNullable = true, Key = "", Comment = "交流累计放电电量" },
                        new Column { Name = "DCinkwh", Type = "float", IsNullable = true,  Key = "", Comment = "直流累计充电电量" },
                        new Column { Name = "DCOutkwh", Type = "float", IsNullable = true,  Key = "", Comment = "直流累计放电电量" },
                        new Column { Name = "Error1", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "Error2", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "Error3", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "Error4", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "Error7", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DCInputV", Type = "int", IsNullable = true,  Key = "", Comment = "直流母线电压" },
                        new Column { Name = "DSP2aV", Type = "int", IsNullable = true , Key = ""},
                        new Column { Name = "DSP2bV", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DSP2cV", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DSP2aA", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DSP2bA", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DSP2cA", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DSP2hz", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DSP2aUkva", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DSP2bUkva", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DSP2cUkva", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DSP2allUkva", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DSP2allNUkvar", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DSP2allAkva", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DSP2allPFactor", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DSP2inputkva", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DSP2inputV", Type = "int", IsNullable = true, Key = "" },
                        new Column { Name = "DSP2inputA", Type = "int", IsNullable = true , Key = ""},
                        new Column { Name = "DSP2DCInputV", Type = "int", IsNullable = true, Key = "" }
                    }
                },
                {
                    "profit", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true,  Key = "", Comment = "日期" },
                        new Column { Name = "profit", Type = "float", IsNullable = true,  Key = "", Comment = "收益" },
                        new Column { Name = "inPower", Type = "float", IsNullable = true,  Key = "", Comment = "充电量kwh" },
                        new Column { Name = "auxkwhAll", Type = "float", IsNullable = true,  Key = "", Comment = "辅助电用总量" },
                        new Column { Name = "outPower", Type = "float", IsNullable = true,  Key = "", Comment = "放电量kwh" },
                        new Column { Name = "in1kwh", Type = "float", IsNullable = true,  Key = "", Comment = "充电尖" },
                        new Column { Name = "in1Price", Type = "float", IsNullable = true,  Key = "", Comment = "尖价格" },
                        new Column { Name = "in2kwh", Type = "float", IsNullable = true,  Key = "", Comment = "峰" },
                        new Column { Name = "in2Price", Type = "float", IsNullable = true,  Key = "", Comment = "峰价格" },
                        new Column { Name = "in3kwh", Type = "float", IsNullable = true,  Key = "", Comment = "平" },
                        new Column { Name = "in3Price", Type = "float", IsNullable = true,  Key = "", Comment = "平价格" },
                        new Column { Name = "in4kwh", Type = "float", IsNullable = true,  Key = "", Comment = "谷" },
                        new Column { Name = "in4Price", Type = "float", IsNullable = true,  Key = "", Comment = "谷价格" },
                        new Column { Name = "out1kwh", Type = "float", IsNullable = true,  Key = "", Comment = "放电尖" },
                        new Column { Name = "out1Price", Type = "float", IsNullable = true,  Key = "", Comment = "放电尖价" },
                        new Column { Name = "out2kwh", Type = "float", IsNullable = true,  Key = "", Comment = "放电峰" },
                        new Column { Name = "out2Price", Type = "float", IsNullable = true,  Key = "", Comment = "放电峰价" },
                        new Column { Name = "out3kwh", Type = "float", IsNullable = true, Key = "",  Comment = "放电平" },
                        new Column { Name = "out3Price", Type = "float", IsNullable = true,  Key = "", Comment = "放电平价" },
                        new Column { Name = "out4kwh", Type = "float", IsNullable = true,  Key = "", Comment = "放电谷" },
                        new Column { Name = "out4Price", Type = "float", IsNullable = true,  Key = "", Comment = "放电谷价" },
                        new Column { Name = "auxkwh1", Type = "float", IsNullable = true,  Key = "", Comment = "辅组电尖" },
                        new Column { Name = "auxkwh2", Type = "float", IsNullable = true,  Key = "", Comment = "峰" },
                        new Column { Name = "auxkwh3", Type = "float", IsNullable = true,  Key = "", Comment = "平" },
                        new Column { Name = "auxkwh4", Type = "float", IsNullable = true,  Key = "", Comment = "谷" },
                        new Column { Name = "gridPower", Type = "float", IsNullable = true,  Key = "", Comment = "国网用量kwh" }
                    }
                },
                {
                    "tactics", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "startTime", Type = "time", IsNullable = true,  Key = "", Comment = "策略开始时间" },
                        new Column { Name = "tType", Type = "varchar(20)", IsNullable = true,  Key = "", Comment = "充放电类型" },
                        new Column {Name = "PCSType", Type = "varchar(20)", IsNullable = true,  Key = "", Comment = "PCS状态，恒压、恒流、恒功率"},
                        new Column { Name = "waValue", Type = "float", IsNullable = true,  Key = "", Comment = "恒流为电流，恒功率为功率" },
                        new Column { Name = "MinPower", Type = "int", IsNullable = true,  Key = "", Comment = "最小功率" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true,  Key = "", Comment = "" },
                        new Column { Name = "endTime", Type = "time", IsNullable = true,  Key = "", Comment = "" }
                    }
                },
                {
                    "tempcontrol", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column{ Name = "rTime", Type = "datetime", IsNullable = true,  Key = "", Comment = "" },
                        new Column{ Name = "state", Type = "int", IsNullable = true, Key = "", Comment = "开关状态1开0关" },
                        new Column{ Name = "indoorTemp", Type = "float", IsNullable = true, Key = "", Comment = "室内温度" },
                        new Column{ Name = "indoorHumidity", Type = "float", IsNullable = true, Key = "", Comment = "室内湿度" },
                        new Column{ Name = "environmentTemp", Type = "float", IsNullable = true, Key = "", Comment = "环境温度" },
                        new Column{ Name = "condenserTemp", Type = "float", IsNullable = true, Key = "", Comment = "冷凝器" },
                        new Column{ Name = "evaporationTemp", Type = "float", IsNullable = true, Key = "", Comment = "蒸发器温度" },
                        new Column{ Name = "fanControl", Type = "int", IsNullable = true, Key = "", Comment = "冷凝风机输出/加湿器输出" },
                        new Column{ Name = "error", Type = "bigint", IsNullable = true, Key = "", Comment = "故障码" },
                        new Column{ Name = "mode", Type = "int", IsNullable = true, Key = "", Comment = "工作模式" }
                    }
                },
                {
                    "ups", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column{ Name = "rTime", Type = "datetime", IsNullable = true, Key ="", Comment = "" },
                        new Column{ Name = "V", Type = "double", IsNullable = true,  Key = "", Comment = "" },
                        new Column{ Name = "A", Type = "double", IsNullable = true,  Key = "", Comment = "" }
                    }
                },
                {
                    "users", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column{ Name = "UName", Type = "varchar(10)", IsNullable = false,  Key = "", Comment = "" },
                        new Column{ Name = "UPassword", Type = "varchar(6)", IsNullable = true,  Key = "", Comment = "" },
                        new Column{ Name = "UPower", Type = "int", IsNullable = true,  Key = "", Comment = "" },
                        new Column{ Name = "AddTime", Type = "datetime", IsNullable = true,  Key = "", Comment = "" },
                        new Column{ Name = "Memo", Type = "varchar(255)", IsNullable = true,  Key = "", Comment = "" },
                        new Column{ Name = "rTime", Type = "datetime", IsNullable = true,  Key = "", Comment = "" }
                    }
                },
                {
                    "warning", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column{ Name = "rTime", Type = "datetime", IsNullable = true,  Key = "", Comment = "发生时间" },
                        new Column{ Name = "wClass", Type = "varchar(20)", IsNullable = true,  Key = "", Comment = "类型（设备）" },
                        new Column{ Name = "eID", Type = "varchar(20)", IsNullable = true,  Key = "", Comment = "设备编号" },
                        new Column{ Name = "WaringID", Type = "int", IsNullable = true,  Key = "", Comment = "警告编号" },
                        new Column{ Name = "wLevels", Type = "int", IsNullable = true,  Key = "", Comment = "级别" },
                        new Column{ Name = "Warning", Type = "varchar(50)", IsNullable = true, Key = "",  Comment = "警告说明" },
                        new Column{ Name = "CheckTime", Type = "datetime", IsNullable = true,  Key = "", Comment = "人工确认时间" },
                        new Column{ Name = "UserID", Type = "varchar(15)", IsNullable = true,  Key = "", Comment = "确认人" },
                        new Column{ Name = "ResetTime", Type = "datetime", IsNullable = true,  Key = "", Comment = "恢复时间" },
                        new Column{ Name = "Memo", Type = "text", IsNullable = true,  Key = "", Comment = "备注"}
                    }
                },
                {
                    "pncontroler", new List<Column>
                    {
                        new Column { Name = "id", Type = "int", IsNullable = false, Key = "PRIMARY KEY AUTO_INCREMENT" },
                        new Column { Name = "rTime", Type = "datetime", IsNullable = true, Key = "" },                       
                        new Column { Name = "controlID", Type = "int", IsNullable = true, Key = "" , Comment = "调度编号"},
                        new Column { Name = "passTime", Type = "datetime", IsNullable = true, Key = "" , Comment = "下达时间"},
                        new Column { Name = "cModel", Type = "varchar(255)", IsNullable = true, Key = "" , Comment = "命令模式"},
                        new Column { Name = "cName", Type = "varchar(255)", IsNullable = true, Key = "" , Comment = "命令类型"},
                        new Column { Name = "cPower", Type = "float", IsNullable = true, Key = "" , Comment = "目标功率"},
                        new Column { Name = "response", Type = "varchar(255)", IsNullable = true, Key = "" , Comment = "相应状态"},
                        new Column { Name = "rTimeLength", Type = "int", IsNullable = true, Key = "" , Comment = "响应时长（秒）"},
                        new Column { Name = "rOutPower", Type = "float", IsNullable = true, Key = "" , Comment = "放电电量（kwh）"},
                        new Column { Name = "rInPower", Type = "float", IsNullable = true, Key = "" , Comment = "充电电量（kwh)"},
                        new Column { Name = "operator", Type = "varchar(255)", IsNullable = true, Key = "" , Comment = "操作员"}
                    }
                },
                {
                    "VariCharge", new List<Column>
                    {
                        new Column { Name = "UBmsPcsState", Type = "int", IsNullable = true, Key = "" , Comment = "充电限制"},
                        new Column { Name = "OBmsPcsState", Type = "int", IsNullable = true, Key = "" , Comment = "放电限制"},
                    }

                },
                {
                    "HistoricalData", new List<Column>
                    {
                        new Column { Name = "E1PUMdemandMaxOld", Type = "int", IsNullable = true, Key = "" , Comment = "总上个月当前正向有功最大需量"},
                        new Column { Name = "ClientPUMdemandMaxOld", Type = "int", IsNullable = true, Key = "" , Comment = "客户上个月当前正向有功最大器量"},
                        new Column { Name = "ClientPUMdemandMax", Type = "int", IsNullable = true, Key = "" , Comment = "客户当前正向有功最大器量"},
                        new Column { Name = "ErrorState2", Type = "int", IsNullable = true, Key = "" , Comment = "记录三级告警标志位" }
                    }
                }
                // Add more tables as needed
            };

            // 遍历每个表结构，进行检查和更新
            foreach (var tableStructure in tableStructures)
            {
                string tableName = tableStructure.Key;
                List<Column> columns = tableStructure.Value;

                // 检查表是否存在
                UpdateDatabaseTable(tableName, columns);
            }
        }
    }
}

