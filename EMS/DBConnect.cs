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
using System.Drawing;

namespace EMS
{
    class DBConnection
    {
        static public bool IsConnected = false;
        static private string DataID = "qiao";
        static private string DataPassword = "1100";
        public static string connectionStr =
            "Database=emsdata;" +
            "Data Source=127.0.0.1;" +
            "Port=3306;" +
            "User Id=" + DataID + ";" +
            "Password=" + DataPassword + ";" +
            "Pooling=true;" +
            "Min Pool Size=5;" +
            "Max Pool Size=100;";
        private static ILog log = LogManager.GetLogger("DB");


        //链接数据库
        public DBConnection()
        {

        }

        //检查mysql服务是否开启 -- 禁用
/*        public static void InitializeMysql80()
        {
            if (!IsMySqlServiceRunning())
            {
                log.Error("MySQL 服务未启动");
                throw new InvalidOperationException("MySQL 服务未启动");
            }
        }

        //启动mysql服务
        static public void StartMysql80()
        {
            string serviceName = "MySQL80";
            log.Error("StartMysql80");
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
                log.Error($"Error starting service: {ex.Message}");
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
        }*/


        //为读取数据库具体数据
        static public MySqlDataReader GetData(string astrSQL, ref MySqlConnection aConnect)
        {

            return null;
        }

        #region 根据 C# 对象的具体类型，创建显式类型的 MySqlParameter。
        private static MySqlParameter CreateTypedParameter(string name, object value)
        {
            switch (value)
            {
                case string s:
                    return new MySqlParameter(name, MySqlDbType.VarChar)
                    {
                        Value = s
                    };

                case int i:
                    return new MySqlParameter(name, MySqlDbType.Int32)
                    {
                        Value = i
                    };

                case long l:
                    return new MySqlParameter(name, MySqlDbType.Int64)
                    {
                        Value = l
                    };

                case short sh:
                    return new MySqlParameter(name, MySqlDbType.Int16)
                    {
                        Value = sh
                    };

                case decimal d:
                    return new MySqlParameter(name, MySqlDbType.Decimal)
                    {
                        Value = d
                    };

                case double db:
                    return new MySqlParameter(name, MySqlDbType.Double)
                    {
                        Value = db
                    };

                case float f:
                    return new MySqlParameter(name, MySqlDbType.Float)
                    {
                        Value = f
                    };

                case bool b:
                    return new MySqlParameter(name, MySqlDbType.Bit)
                    {
                        Value = b
                    };

                case DateTime dt:
                    return new MySqlParameter(name, MySqlDbType.DateTime)
                    {
                        Value = dt
                    };

                case byte[] bytes:
                    return new MySqlParameter(name, MySqlDbType.Blob)
                    {
                        Value = bytes
                    };

                default:
                    throw new ArgumentException($"Unsupported parameter type: {value.GetType().Name} for parameter {name}");
            }
        }
        #endregion

        #region 执行带参数的SQL查询并返回DataTable
        public static DataTable QueryDataTableWithParams(
            string sql,
            Dictionary<string, object> parameters,
            int commandTimeout = 30,
            uint connectionTimeout = 10)
        {
            var csb = new MySqlConnectionStringBuilder(connectionStr)
            {
                ConnectionTimeout = connectionTimeout
            };

            try
            {
                using (var conn = new MySqlConnection(csb.ConnectionString))
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = commandTimeout;

                    // ✅ 类型安全参数绑定
                    if (parameters != null)
                    {
                        foreach (var kv in parameters)
                        {
                            var paramName = kv.Key;
                            var value = kv.Value;

                            MySqlParameter p;

                            if (value == null || value == DBNull.Value)
                            {
                                p = new MySqlParameter(paramName, MySqlDbType.VarChar)
                                {
                                    Value = DBNull.Value
                                };
                            }
                            else
                            {
                                p = CreateTypedParameter(paramName, value);
                            }

                            cmd.Parameters.Add(p);
                        }
                    }

                    using (var adapter = new MySqlDataAdapter(cmd))
                    {
                        conn.Open();
                        var dt = new DataTable();
                        adapter.Fill(dt);
                        return dt;
                    }
                }
            }
            catch (ArgumentException ex)
            {
                // ✅ 参数类型不支持（Fail Fast）
                log.Error($"SQL 参数类型错误: {ex.Message}, SQL: {sql}");
                throw;
            }
            catch (Exception ex)
            {
                log.Error($"QueryDataTableWithParams Error: {ex.Message}, SQL: {sql}");
                throw;
            }
        }

        #endregion

        #region 执行带参数的SQL并返回影响行数
        public static int ExecSQLWithParams(
            string sql,
            Dictionary<string, object> parameters,
            int commandTimeout = 30,
            uint connectionTimeout = 10)
        {
            var csb = new MySqlConnectionStringBuilder(connectionStr)
            {
                ConnectionTimeout = connectionTimeout
            };

            try
            {
                using (var connection = new MySqlConnection(csb.ConnectionString))
                using (var sqlCmd = new MySqlCommand(sql, connection))
                {
                    sqlCmd.CommandTimeout = commandTimeout;

                    // ✅ 类型安全参数绑定（与 QueryDataTableWithParams 保持一致）
                    if (parameters != null)
                    {
                        foreach (var kv in parameters)
                        {
                            var paramName = kv.Key;
                            var value = kv.Value;

                            MySqlParameter p;

                            if (value == null || value == DBNull.Value)
                            {
                                p = new MySqlParameter(paramName, MySqlDbType.VarChar)
                                {
                                    Value = DBNull.Value
                                };
                            }
                            else
                            {
                                p = CreateTypedParameter(paramName, value);
                            }

                            sqlCmd.Parameters.Add(p);
                        }
                    }

                    connection.Open();
                    return sqlCmd.ExecuteNonQuery();
                }
            }
            catch (MySqlException ex) when (ex.Number == 1040 || ex.Number == 1203)
            {
                // Too many connections / User connection limit
                log.Warn("数据库连接池已满，拒绝请求");
                return -1;
            }
            catch (MySqlException ex) when (
                ex.Number == 1042 ||    // Unable to connect
                ex.Number == 0)         // Generic connection error / timeout
            {
                log.Warn(
                    $"数据库连接或命令超时 (Conn:{connectionTimeout}s, Cmd:{commandTimeout}s): " +
                    $"{sql.Substring(0, Math.Min(100, sql.Length))}...");
                return -1;
            }
            catch (ArgumentException ex)
            {
                // ✅ 参数类型不支持（Fail Fast）
                log.Error($"SQL 参数类型错误: {ex.Message}, SQL: {sql}");
                return -1;
            }
            catch (Exception ex)
            {
                log.Error($"ExecSQLWithParams error: {ex.Message}, SQL: {sql}");
                return -1;
            }
        }

        #endregion


        #region 执行查询并返回 DataTable
        public static DataTable QueryDataTable(
            string sql,
            int commandTimeout = 30,
            uint connectionTimeout = 10)
        {
            var csb = new MySqlConnectionStringBuilder(connectionStr)
            {
                ConnectionTimeout = connectionTimeout
            };

            try
            {
                using (var conn = new MySqlConnection(csb.ConnectionString))
                using (var cmd = new MySqlCommand(sql, conn))
                using (var adapter = new MySqlDataAdapter(cmd))
                {
                    cmd.CommandTimeout = commandTimeout;
                    conn.Open();

                    var dt = new DataTable();
                    adapter.Fill(dt);
                    return dt;
                }
            }
            catch (Exception ex)
            {
                log.Error($"QueryDataTable Error: {ex.Message}, SQL: {sql}");
                return null;
            }
        }

        #endregion


        #region 执行查询并返回 MySqlDataReader
        public static MySqlDataReader QueryDataReader(
            string sql,
            int commandTimeout = 30,
            uint connectionTimeout = 10)
        {
            var csb = new MySqlConnectionStringBuilder(connectionStr)
            {
                ConnectionTimeout = connectionTimeout
            };

            var conn = new MySqlConnection(csb.ConnectionString);

            try
            {
                conn.Open();

                var cmd = new MySqlCommand(sql, conn)
                {
                    CommandTimeout = commandTimeout
                };

                // ✅ reader 关闭时，自动关闭 connection
                return cmd.ExecuteReader(CommandBehavior.CloseConnection);
            }
            catch (Exception ex)
            {
                conn.Dispose(); // ✅ 防止 Open / ExecuteReader 失败时泄漏
                log.Error($"QueryDataReader Error: {ex.Message}, SQL: {sql}");
                return null;
            }
        }

        public static MySqlDataReader QueryDataReader(
            string sql,
            Dictionary<string, object> parameters,
            int commandTimeout = 30,
            uint connectionTimeout = 10)
        {
            var csb = new MySqlConnectionStringBuilder(connectionStr)
            {
                ConnectionTimeout = connectionTimeout
            };

            var conn = new MySqlConnection(csb.ConnectionString);

            try
            {
                var cmd = new MySqlCommand(sql, conn)
                {
                    CommandTimeout = commandTimeout
                };

                // ✅ 类型安全参数绑定（与 DataTable 版本完全一致）
                if (parameters != null)
                {
                    foreach (var kv in parameters)
                    {
                        var paramName = kv.Key;
                        var value = kv.Value;

                        MySqlParameter p;

                        if (value == null || value == DBNull.Value)
                        {
                            p = new MySqlParameter(paramName, MySqlDbType.VarChar)
                            {
                                Value = DBNull.Value
                            };
                        }
                        else
                        {
                            p = CreateTypedParameter(paramName, value);
                        }

                        cmd.Parameters.Add(p);
                    }
                }

                conn.Open();

                // ✅ reader.Dispose() → 自动关闭 connection
                return cmd.ExecuteReader(CommandBehavior.CloseConnection);
            }
            catch (ArgumentException ex)
            {
                conn.Dispose();
                log.Error($"QueryDataReader 参数类型错误: {ex.Message}, SQL: {sql}");
                throw;
            }
            catch (Exception ex)
            {
                conn.Dispose();
                log.Error($"QueryDataReader Error: {ex.Message}, SQL: {sql}");
                throw;
            }
        }

        #endregion

        #region QuerySingleValue

        /// <summary>
        /// 无参版
        /// </summary>
        public static object QuerySingleValue(
            string sql,
            int commandTimeout = 30,
            uint connectionTimeout = 10)
        {
            var csb = new MySqlConnectionStringBuilder(connectionStr)
            {
                ConnectionTimeout = connectionTimeout
            };

            try
            {
                using (var conn = new MySqlConnection(csb.ConnectionString))
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = commandTimeout;
                    conn.Open();

                    var result = cmd.ExecuteScalar();
                    return result == DBNull.Value ? null : result;
                }
            }
            catch (Exception ex)
            {
                log.Error($"QuerySingleValue Error: {ex.Message}, SQL: {sql}");
                return null;
            }
        }

        /// <summary>
        /// 带参版
        /// </summary>
        public static object QuerySingleValue(
            string sql,
            Dictionary<string, object> parameters,
            int commandTimeout = 30,
            uint connectionTimeout = 10)
        {
            var csb = new MySqlConnectionStringBuilder(connectionStr)
            {
                ConnectionTimeout = connectionTimeout
            };

            try
            {
                using (var conn = new MySqlConnection(csb.ConnectionString))
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.CommandTimeout = commandTimeout;

                    // ✅ 类型安全参数绑定（与其他方法完全一致）
                    if (parameters != null)
                    {
                        foreach (var kv in parameters)
                        {
                            var paramName = kv.Key;
                            var value = kv.Value;

                            MySqlParameter p;

                            if (value == null || value == DBNull.Value)
                            {
                                p = new MySqlParameter(paramName, MySqlDbType.VarChar)
                                {
                                    Value = DBNull.Value
                                };
                            }
                            else
                            {
                                p = CreateTypedParameter(paramName, value);
                            }

                            cmd.Parameters.Add(p);
                        }
                    }

                    conn.Open();

                    var result = cmd.ExecuteScalar();
                    return result == DBNull.Value ? null : result;
                }
            }
            catch (ArgumentException ex)
            {
                // ✅ 参数类型错误：属于代码问题，建议暴露
                log.Error($"QuerySingleValue 参数类型错误: {ex.Message}, SQL: {sql}");
                return null;
            }
            catch (Exception ex)
            {
                log.Error($"QuerySingleValue Error: {ex.Message}, SQL: {sql}");
                return null;
            }
        }

        #endregion

        #region ExecTransaction

        /// <summary>
        /// 执行多条 SQL（事务）
        /// </summary>
        public static bool ExecTransaction(
            List<string> sqlList,
            int commandTimeout = 30,
            uint connectionTimeout = 10)
        {
            if (sqlList == null || sqlList.Count == 0)
                return true;

            var csb = new MySqlConnectionStringBuilder(connectionStr)
            {
                ConnectionTimeout = connectionTimeout
            };

            try
            {
                using (var conn = new MySqlConnection(csb.ConnectionString))
                {
                    conn.Open();

                    using (var transaction = conn.BeginTransaction())
                    using (var cmd = new MySqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.Transaction = transaction;
                        cmd.CommandTimeout = commandTimeout;

                        try
                        {
                            foreach (var sql in sqlList)
                            {
                                cmd.CommandText = sql;
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit();
                            return true;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"ExecTransaction Error: {ex.Message}");
                return false;
            }
        }

        #endregion

        //获取最后一个记录的ID
        static public int GetLastID(string astrSQL)
        {
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

        //检查是否存在 SQL 约定的数据（含有为 True，不存在为 False）- 参数化查询版本
        static public bool CheckRec(string astrSQL, Dictionary<string, object> parameters)
        {
            bool bResult = false;

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionStr))
                {
                    connection.Open();
                    using (MySqlCommand sqlCmd = new MySqlCommand(astrSQL, connection))
                    {
                        // 添加参数
                        if (parameters != null)
                        {
                            foreach (var param in parameters)
                            {
                                MySqlParameter p = CreateTypedParameter(param.Key, param.Value);
                                if (p != null)
                                {
                                    sqlCmd.Parameters.Add(p);
                                }
                            }
                        }
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

        public static bool CheckTableExists(string tableName)
        {
            try
            {
                string checkTableQuery = $"SELECT COUNT(*) FROM information_schema.tables WHERE table_name = '{tableName}'";
                using (MySqlConnection connection = new MySqlConnection(connectionStr))
                {
                    connection.Open();
                    using (MySqlCommand sqlCmd = new MySqlCommand(checkTableQuery, connection))
                    {
                        object result = sqlCmd.ExecuteScalar();
                        return Convert.ToInt32(result) > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error checking if table exists: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查用户的权限
        /// </summary>
        /// <param name="astrSQL"></param>
        /// <returns></returns>
        static public bool ChecUserc(string astrSQL,ref int aPower)
        {
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

        //记录LOg事件
        static public void RecordLOG(string aEClasse, string aEvemt, string aMemo)
        {
            //log表已经删除
        }

    }
}
