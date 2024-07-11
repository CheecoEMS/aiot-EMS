using log4net;
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Common.Utilities;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using MySqlX.XDevAPI.Common;


namespace EMS
{
    public class Column
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsNullable { get; set; }
        public string Key { get; set; }
    }
    public class SqlTask
    {
        public string Sql { get; set; }
        public int Priority { get; set; }
        public bool Result { get; set; }
        public Action<bool> Callback { get; set; }

        //public Func<bool, bool> CallbackF { get; set; }
        public Action<string> ReadCallback { get; set; }
        public DataGridView DataGridView { get; set; }


        public SqlTask(string sql, int priority, Action<bool> callback)
        {
            Sql = sql;
            Priority = priority;
            Callback = callback;
        }

        public SqlTask(string sql, int priority, Action<string> readCallback)
        {
            Sql = sql;
            Priority = priority;
            ReadCallback = readCallback;
        }

        public SqlTask(string sql, int priority, DataGridView dataGridView)
        {
            Sql = sql;
            Priority = priority;
            DataGridView = dataGridView;
        }

        public void SetResult(bool result)
        {
            Result = result;
        }
    }

    public class SqlReadTask : SqlTask
    {
        public string DirectoryPath { get; set; }
        public Action<string, string> ReadCallbackWithPath { get; set; }

        public SqlReadTask(string sql, int priority, Action<string, string> readCallbackWithPath, string directoryPath)
            : base(sql, priority, result => readCallbackWithPath(result, directoryPath))
        {
            DirectoryPath = directoryPath;
            ReadCallbackWithPath = readCallbackWithPath;
        }
    }

    public class SqlReadWithParamsTask : SqlTask
    {
        public Action<object[]> ReadCallbackWithParams { get; }
        public object[] Parameters { get; }

        public SqlReadWithParamsTask(string sql, int priority, Action<object[]> readCallbackWithParams, params object[] parameters)
            : base(sql, priority, result => ExecuteReadCallbackWithParams(result, readCallbackWithParams, parameters))
        {
            ReadCallbackWithParams = readCallbackWithParams;
            Parameters = parameters;
        }

        private static void ExecuteReadCallbackWithParams(string result, Action<object[]> readCallbackWithParams, object[] parameters)
        {
            // Assuming the result is a JSON string that needs to be deserialized into the parameters array
            // Use a JSON library such as Newtonsoft.Json to deserialize
            var deserializedParams = Newtonsoft.Json.JsonConvert.DeserializeObject<object[]>(result);
            if (deserializedParams != null)
            {
                for (int i = 0; i < deserializedParams.Length; i++)
                {
                    parameters[i] = deserializedParams[i];
                }
            }
            readCallbackWithParams(parameters);
        }
    }

    public class SqlDataGridViewTask : SqlTask
    {
        public SqlDataGridViewTask(string sql, int priority, DataGridView dataGridView)
            : base(sql, priority, dataGridView)
        {
        }
    }

    public class SqlCheckTask : SqlTask
    {
        //public bool Result { get; private set; }
        public Action<bool> CheckCallback { get; set; }

        public SqlCheckTask(string sql, int priority, Action<bool> checkCallback)
            : base(sql, priority, checkCallback)
        {
            CheckCallback = checkCallback;
        }

        /*        public void SetResult(bool result)
                {
                    Result = result;
                }*/
    }

    public class SqlUpdateTableTask : SqlTask
    {
        public string TableName { get; set; }
        public List<Column> TableStructure { get; set; }

        public Action<bool> UpdateCallback { get; set; }//无用

        public SqlUpdateTableTask(string tableName, List<Column> tableStructure, Action<bool> callback)
            : base("", 1, callback) // 调用基类构造函数并提供适当的参数
        {
            TableName = tableName;
            TableStructure = tableStructure;
            UpdateCallback = callback;
        }
    }

    public class PriorityQueue<T>
    {
        private readonly SortedDictionary<int, Queue<T>> _queues = new SortedDictionary<int, Queue<T>>();

        public void Enqueue(T item, int priority)
        {
            if (!_queues.TryGetValue(priority, out var queue))
            {
                queue = new Queue<T>();
                _queues[priority] = queue;
            }
            queue.Enqueue(item);
        }

        public T Dequeue()
        {
            if (_queues.Count == 0)
            {
                throw new InvalidOperationException("The queue is empty.");
            }

            var pair = _queues.First();
            var item = pair.Value.Dequeue();

            if (pair.Value.Count == 0)
            {
                _queues.Remove(pair.Key);
            }

            return item;
        }

        public int Count => _queues.Sum(q => q.Value.Count);
    }

    public class SqlExecutor
    {
        private static MySqlConnection connection;
        private static bool IsConnected = false;
        private static readonly PriorityQueue<SqlTask> sqlTaskQueue = new PriorityQueue<SqlTask>();
        private static readonly object lockObject = new object();
        private static readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private static readonly Task processingTask;
        static private string DataID = "qiao";
        static private string DataPassword = "1100";
        static public string connectionStr = "Database=emsdata;Data Source=127.0.0.1;port=3306;User Id=" + DataID + ";Password=" + DataPassword + ";";

        static SqlExecutor()
        {
            processingTask = Task.Run(ProcessSqlTasks, cancellationTokenSource.Token);
        }

        static private void CreateConnection()
        {
            try
            {
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
                frmMain.ShowDebugMSG(ex.ToString());
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
                IsConnected = false;
            }
        }

        private static async Task ProcessSqlTasks()
        {
            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                SqlTask sqlTask = null;

                lock (lockObject)
                {
                    if (sqlTaskQueue.Count > 0)
                    {
                        sqlTask = sqlTaskQueue.Dequeue();
                    }
                }

                if (sqlTask != null)
                {
                    if (sqlTask is SqlReadTask sqlReadTask)
                    {
                        string result = await ReadSQLAsync(sqlReadTask.Sql);
                        sqlReadTask.ReadCallbackWithPath?.Invoke(result, sqlReadTask.DirectoryPath);
                    }
                    else if (sqlTask is SqlReadWithParamsTask sqlReadWithParamsTask)
                    {
                        object[] results = await ReadSQLWithParamsAsync(sqlReadWithParamsTask.Sql, sqlReadWithParamsTask.Parameters.Length);
                        sqlReadWithParamsTask.ReadCallbackWithParams?.Invoke(results);
                    }
                    else if (sqlTask is SqlDataGridViewTask sqlDataGridViewTask)
                    {
                        await BindDataToDataGridViewAsync(sqlDataGridViewTask.Sql, sqlDataGridViewTask.DataGridView);
                    }
                    else if (sqlTask is SqlCheckTask sqlCheckTask)
                    {
                        bool result = await CheckSQLAsync(sqlCheckTask.Sql);
                        sqlCheckTask.SetResult(result);
                        sqlCheckTask.CheckCallback?.Invoke(result);
                    }
                    else if (sqlTask is SqlUpdateTableTask sqlUpdateTableTask)
                    {
                        await UpdateDatabaseTable(sqlUpdateTableTask.TableName, sqlUpdateTableTask.TableStructure);
                    }
                    else
                    {
                        bool result = await ExecSQLAsync(sqlTask.Sql);
                        sqlTask.SetResult(result);
                        sqlTask.Callback?.Invoke(result);
                    }
                }
                else
                {
                    await Task.Delay(100); // No task to process, wait for a while
                }
            }
        }

        public static void EnqueueSqlTask(string sql, int priority, Action<bool> callback)
        {
            lock (lockObject)
            {
                sqlTaskQueue.Enqueue(new SqlTask(sql, priority, callback), priority);
            }
        }

        public static void EnqueueSqlReadTask(string sql, int priority, Action<string, string> readCallback, string directoryPath)
        {
            lock (lockObject)
            {
                sqlTaskQueue.Enqueue(new SqlReadTask(sql, priority, readCallback, directoryPath), priority);
            }
        }

        public static void EnqueueSqlReadWithParamsTask(string sql, int priority, Action<object[]> readCallbackWithParams, params object[] parameters)
        {
            lock (lockObject)
            {
                sqlTaskQueue.Enqueue(new SqlReadWithParamsTask(sql, priority, readCallbackWithParams, parameters), priority);
            }
        }

        public static void EnqueueSqlDataGridViewTask(string sql, int priority, DataGridView dataGridView)
        {
            lock (lockObject)
            {
                sqlTaskQueue.Enqueue(new SqlDataGridViewTask(sql, priority, dataGridView), priority);
            }
        }

        public static void EnqueueSqlCheckTask(string sql, int priority, Action<bool> callback)
        {
            lock (lockObject)
            {
                sqlTaskQueue.Enqueue(new SqlCheckTask(sql, priority, callback), priority);
            }
        }

        public static void EnqueueUpdateTableTask(string tableName, List<Column> tableStructure, Action<bool> callback)
        {
            lock (lockObject)
            {
                sqlTaskQueue.Enqueue(new SqlUpdateTableTask(tableName, tableStructure, callback), 1);
            }
        }

        public static async Task UpdateDatabaseTable(string tableName, List<Column> targetColumns)
        {
            if (TableExists(tableName))
            {
                List<Column> existingColumns = GetTableColumns(tableName);

                if (!TableStructureMatches(existingColumns, targetColumns))
                {
                    // Backup the existing table
                    string backupTableName = tableName + "_backup";
                    string createBackupTableQuery = $"CREATE TABLE {backupTableName} AS SELECT * FROM {tableName};";
                    await ExecSQLAsync(createBackupTableQuery);

                    // Drop the original table
                    string dropTableQuery = $"DROP TABLE {tableName};";
                    await ExecSQLAsync(dropTableQuery);

                    // Create the new table with the target structure
                    CreateTable(tableName, targetColumns);

                    // Insert the data back into the new table
                    List<string> commonColumns = GetCommonColumns(existingColumns, targetColumns);
                    string columnsList = string.Join(", ", commonColumns);
                    string insertDataQuery = $"INSERT INTO {tableName} ({columnsList}) SELECT {columnsList} FROM {backupTableName};";
                    await ExecSQLAsync(insertDataQuery);

                    // Drop the backup table
                    string dropBackupTableQuery = $"DROP TABLE {backupTableName};";
                    await ExecSQLAsync(dropBackupTableQuery);
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
            string query = $"SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_KEY FROM information_schema.columns WHERE table_schema = 'emsdata' AND table_name = '{tableName}';";
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
                    Key = reader.GetString("COLUMN_KEY")
                });
            }

            reader.Close();
            return columns;
        }

        public static void CreateTable(string tableName, List<Column> columns)
        {
            string createTableQuery = $"CREATE TABLE `{tableName}` (";
            List<string> columnDefinitions = new List<string>();

            foreach (var column in columns)
            {
                string columnDefinition = $"`{column.Name}` {column.Type}";
                if (!column.IsNullable)
                    columnDefinition += " NOT NULL";
                if (!string.IsNullOrEmpty(column.Key))
                    columnDefinition += $" {column.Key}";

                columnDefinitions.Add(columnDefinition);
            }

            createTableQuery += string.Join(", ", columnDefinitions);
            createTableQuery += ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_unicode_520_ci;";

            ExecSQLAsync(createTableQuery).Wait(); // Wait for table creation to complete
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


        private static async Task<bool> CheckSQLAsync(string sql)
        {
            ChecMysql80();

            bool result = false;
            var tempConnection = new MySqlConnection(connectionStr);
            await tempConnection.OpenAsync();

            try
            {
                MySqlCommand sqlCmd = new MySqlCommand(sql, tempConnection);
                using (MySqlDataReader reader = (MySqlDataReader)await sqlCmd.ExecuteReaderAsync())
                {
                    result = reader.HasRows;
                }
            }
            catch (MySqlException ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(sql + "\n" + ex.ToString());
            }
            finally
            {
                await tempConnection.CloseAsync();
            }

            return result;
        }

        private static async Task<object[]> ReadSQLWithParamsAsync(string sql, int paramCount)
        {
            if ((connection == null) || (!IsConnected))
                CreateConnection();

            var tempConnection = new MySqlConnection(connectionStr);
            await tempConnection.OpenAsync();

            try
            {
                MySqlCommand sqlCmd = new MySqlCommand(sql, tempConnection);
                using (MySqlDataReader reader = (MySqlDataReader)await sqlCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        object[] values = new object[paramCount];
                        reader.GetValues(values);
                        return values;
                    }
                }
            }
            catch (MySqlException ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
                IsConnected = false;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                frmMain.ShowDebugMSG(sql + "\n" + ex.ToString());
            }
            finally
            {
                await tempConnection.CloseAsync();
            }

            return null;
        }

        public static async Task<bool> ExecSQLAsync(string sql)
        {
            ChecMysql80();

            bool result;
            if ((connection == null) || (!IsConnected))
                CreateConnection();

            var tempConnection = new MySqlConnection(connectionStr);
            await tempConnection.OpenAsync();

            try
            {
                MySqlCommand sqlCmd = new MySqlCommand(sql, tempConnection);
                result = await sqlCmd.ExecuteNonQueryAsync() > 0;
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
                frmMain.ShowDebugMSG(sql + "\n" + ex.ToString());
                return false;
            }
            finally
            {
                await tempConnection.CloseAsync();
            }

            return result;
        }

        public static async Task<string> ReadSQLAsync(string sql)
        {
            ChecMysql80();

            if ((connection == null) || (!IsConnected))
                CreateConnection();

            var tempConnection = new MySqlConnection(connectionStr);
            await tempConnection.OpenAsync();

            try
            {
                MySqlCommand sqlCmd = new MySqlCommand(sql, tempConnection);
                using (MySqlDataReader reader = (MySqlDataReader)await sqlCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        var row = new System.Collections.Generic.Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = reader.GetValue(i);
                        }
                        return JsonConvert.SerializeObject(row, Formatting.Indented);
                    }
                }
            }
            catch (MySqlException ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
                IsConnected = false;
                return null;
            }
            catch (Exception ex)
            {
                IsConnected = false;
                frmMain.ShowDebugMSG(sql + "\n" + ex.ToString());
                return null;
            }
            finally
            {
                await tempConnection.CloseAsync();
            }

            return null;
        }

        public static async Task BindDataToDataGridViewAsync(string sql, DataGridView dataGridView)
        {
            ChecMysql80();

            var tempConnection = new MySqlConnection(connectionStr);
            await tempConnection.OpenAsync();

            try
            {
                MySqlCommand sqlCmd = new MySqlCommand(sql, tempConnection);
                MySqlDataAdapter sda = new MySqlDataAdapter(sqlCmd);
                DataSet dataset = new DataSet();
                await Task.Run(() => sda.Fill(dataset)); // Use Task.Run to run synchronous Fill method asynchronously

                if (dataset.Tables.Count > 0)
                {
                    dataGridView.Invoke(new Action(() =>
                    {
                        dataGridView.DataSource = dataset.Tables[0];
                        dataGridView.Update();
                    }));
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
                await tempConnection.CloseAsync();
            }
        }

        public static void SaveJsonToFile(string jsonResult, string directoryPath)
        {
            if (!string.IsNullOrEmpty(jsonResult))
            {
                string filePath = Path.Combine(directoryPath, "profit_data.json");
                File.WriteAllText(filePath, jsonResult);
                Console.WriteLine($"Data has been written to {filePath}");
            }
            else
            {
                Console.WriteLine("No data to write.");
            }
        }

        static public void ChecMysql80()
        {
            if (!IsMySqlServiceRunning())
            {
                StartMysql80();
            }
        }
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

        public static void StopProcessing()
        {
            cancellationTokenSource.Cancel();
            processingTask.Wait();
        }


        public static void ShowData2DBGrid(DataGridView adDtaGrid, string astrSQL)
        {
            SqlExecutor.EnqueueSqlDataGridViewTask(astrSQL, 1, adDtaGrid);
        }

        public static bool CheckRec(string astrSQL)
        {
            bool bResult = false;

            var resetEvent = new System.Threading.AutoResetEvent(false);

            SqlExecutor.EnqueueSqlCheckTask(astrSQL, 3, (result) =>
            {
                bResult = result;
                resetEvent.Set();
            });

            resetEvent.WaitOne(); // 等待任务完成

            return bResult;
        }

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

        static public void RecordLOG(string aEClasse, string aEvemt, string aMemo)
        {
            string sql = "insert into log (eTime,eClass,Event,Memo)values ('"
                + DateTime.Now.ToString("yyyy-M-d H:m:s") + "','"
                + aEClasse + "','"
                + aEvemt + "','"
                 + aMemo + "')";

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

        public static bool ExecuteSqlTaskAsync(string astrSQL, int prior)
        {
            bool bResult = false;

            var resetEvent = new System.Threading.AutoResetEvent(false);

            SqlExecutor.EnqueueSqlTask(astrSQL, prior, (result) =>
            {
                bResult = result;
                resetEvent.Set();
            });

            resetEvent.WaitOne(); // 等待任务完成

            return bResult;
        }

        public static bool CompareAndUpdateTableStructure(string tableName, List<Column> targetColumns, Action<bool> callback)
        {
            bool bResult = false;

            var resetEvent = new System.Threading.AutoResetEvent(false);

            SqlExecutor.EnqueueUpdateTableTask(tableName, targetColumns, callback);

            resetEvent.WaitOne(); // 等待任务完成

            return bResult;
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
                log.Error(ex.Message);
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
            try
            {
                ChecMysql80();
                if ((connection == null) || (!IsConnected))
                    CreateConnection();

                MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection);// "Select * from XXXXXXX";
                MySqlDataAdapter sda = new MySqlDataAdapter(sqlCmd);

                DataSet ds = new DataSet();
                sda.Fill(ds);
                IsConnected = true;
                return ds;
            }
            catch (MySqlException ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
                IsConnected = false;
                return null;
            }
            catch (Exception ex)
            {
                frmMain.ShowDebugMSG(ex.ToString());
                IsConnected = false;
                return null; ;
            }
            finally
            {
                //connection.Close();
            }
        }

        //为读取数据库具体数据
        static public MySqlDataReader GetData(string astrSQL, ref MySqlConnection aConnect)
        {
            //MySqlConnection tempConnection = new MySqlConnection(connectionStr);
            try
            {
                ChecMysql80();
                //if ((connection == null) || (!IsConnected)) 
                //    CreateConnection(); 
                MySqlConnection tempConnection = new MySqlConnection(connectionStr);
                aConnect = tempConnection;
                tempConnection.Open();
                MySqlCommand sqlCmd = new MySqlCommand(astrSQL, tempConnection);// "Select * from XXXXXXX"; 
                //使用 ExecuteReader 方法创建 SqlDataReader 对象 
                MySqlDataReader sdr = sqlCmd.ExecuteReader();
                return sdr;
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
                //if (tempConnection != null)
                //{
                //    tempConnection.Dispose();
                //    tempConnection = null;
                //}
            }
        }

        //运行SQL，用于增加，删除，编辑
        static public bool ExecSQL(string astrSQL)
        {
            ChecMysql80();

            bool bResult;
            if ((connection == null) || (!IsConnected))
                CreateConnection();

            lock (connection)
            {
                MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection);
                try
                {
                    //MySqlDataAdapter sda = new MySqlDataAdapter(sqlCmd);
                    IsConnected = true;
                    if (sqlCmd.ExecuteNonQuery() > 0)
                        bResult = true;
                    else
                        bResult = false;

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
                    //connection.Close();
                    //connection.Dispose();
                    frmMain.ShowDebugMSG(astrSQL + "\n" + ex.ToString());
                    return false;
                }
                finally
                {
                    //sqlCmd.Cancel();if
                    sqlCmd.Dispose();
                    //connection.Close();
                    //return false;
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
                if (rd.Read())
                    iResult = rd.GetInt32(0);
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
                    ctTemp.Dispose();
                }
            }
            return iResult;
        }

        //检查是否存在SQL约定的数据 （含有为True，不存在为False）
        static public bool CheckRec(string astrSQL )
        {
            ChecMysql80();
            //aiData = -1;
            bool bResult = false;
            MySqlConnection tempConnection = new MySqlConnection(connectionStr);
            tempConnection.Open();
            MySqlCommand sqlCmd = new MySqlCommand(astrSQL, tempConnection);// "Select * from XXXXXXX";  
            MySqlDataReader rd = sqlCmd.ExecuteReader();
            try
            {
               // if (rd.Read())
               //     aiData = rd.GetInt32(0);
                bResult = rd.HasRows;
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
                if (tempConnection != null)
                {
                    tempConnection.Dispose();
                    tempConnection = null;
                }
                // ctTemp.Close();
                //tempConnection.Close();
                //tempConnection.Dispose();
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
            MySqlConnection tempConnection = new MySqlConnection(connectionStr);
            tempConnection.Open();
            MySqlCommand sqlCmd = new MySqlCommand(astrSQL, tempConnection);// "Select * from XXXXXXX";  
            MySqlDataReader rd = sqlCmd.ExecuteReader();
            try
            {
                 if (rd.Read())
                    aPower = rd.GetInt32(0);
                bResult = rd.HasRows;
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
                if(tempConnection!=null)
                {
                    tempConnection.Dispose();
                    tempConnection = null;
                }
                
                //tempConnection.Close();
                //tempConnection.Dispose();
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
            //adDtaGrid.Rows.Clear(); 
            //if (adDtaGrid.DataSource != null)
            //    adDtaGrid.DataSource = null;
            MySqlConnection oneConnection = new MySqlConnection(connectionStr);
            MySqlCommand sqlCmd = new MySqlCommand(astrSQL, oneConnection);// "Select * from XXXXXXX";
            MySqlDataAdapter sda = new MySqlDataAdapter(sqlCmd);
            DataSet dataset = new DataSet();
            try
            {
                sda.Fill(dataset);
                // DataSet dataset = DBConnection.GetDataSet(astrSQL);
                if (dataset == null)
                {
                    // MessageBox.Show("没有数据");
                    return;
                }
                adDtaGrid.DataSource = dataset.Tables[0];
                adDtaGrid.Update();
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
                //adDtaGrid.DataBind(); 
                if (oneConnection != null)
                    oneConnection.Dispose();
                if (sqlCmd != null)
                    sqlCmd.Dispose();
                if (sda != null)
                    sda.Dispose();
                if (dataset != null)
                    dataset.Dispose();
                GC.Collect();
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
                if (sdr == null)
                {
                    ctTemp.Dispose();
                    return;
                }

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
                    //ctTemp.Close();
                    ctTemp.Dispose();
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
                while (sdr.Read())//调用 Read 方法读取 SqlDataReader
                {
                    aChart.Series[aSeriesIndex].Points.AddXY(
                       sdr.GetDateTime(0).ToString(aTimeFormat),//
                       sdr.GetFloat(1).ToString());
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
                    //ctTemp.Close();
                    ctTemp.Dispose();
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
                    ctTemp.Dispose();
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


    }
}

