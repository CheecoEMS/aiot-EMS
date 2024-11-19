using Squirrel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using log4net;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace EMS
{
    public partial class frmUpdateEms : Form
    {
        static public frmUpdateEms oneForm = null;
        private static ILog log = LogManager.GetLogger("frmUpdateEms");


        public frmUpdateEms()
        {
            InitializeComponent();
        }

        static public void CloseForm()
        {
            if (oneForm != null)
            {
                oneForm.Close();
                oneForm.Dispose();
                oneForm = null;
            }
        }

        private async Task<bool> CheckAndUpdateAsync(string version)
        {
            try
            {
                string updateUrl = $"https://aiot-data-ems.oss-cn-shanghai.aliyuncs.com/EMS/{version}";
                log.Error("更新文件版本: " + version);

                // 配置远程更新的 URL，指向包含 RELEASES 文件和 .nupkg 文件的服务器路径
                //using (var mgr = new UpdateManager("https://aiot-data-ems.oss-cn-shanghai.aliyuncs.com/EMS/v1.0.0"))
                using (var mgr = new UpdateManager(updateUrl))
                {
                    var updateInfo = await mgr.CheckForUpdate();
                    if (updateInfo.ReleasesToApply.Count > 0)
                    {
                        // 下载和应用更新
                        //mgr.UpdateApp().GetAwaiter().GetResult();
                        await mgr.UpdateApp();
                        log.Error("更新完成，准备重启应用。");
                        MessageBox.Show("更新完成，应用将重启以加载新版本。", "更新成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        /*                        // 重启应用
                                                UpdateManager.RestartAppWhenExited();

                                                // 退出当前进程  
                                                Application.Exit();*/


                        // 调用重启逻辑
                        UpdateManager.RestartAppWhenExited();
                        return true;
                    }
                    else
                    {
                        // 已是最新版本
                        MessageBox.Show("当前已是最新版本，无需更新。", "版本已同步", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("CheckAndUpdateAsync: " + ex.Message);
                MessageBox.Show($"更新失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        //新增数据
        static public void ShowForm()
        {
            if (oneForm == null)
            {
                oneForm = new frmUpdateEms(); // 创建新实例
            }

            oneForm.Show(); // 显示窗体
            oneForm.ShowData(); // 调用实例方法显示数据
        }

        private void ShowData()
        {
            // 获取当前程序集
            Assembly assembly = Assembly.GetExecutingAssembly();

            // 获取版本信息
            Version version = assembly.GetName().Version;
            tbNowVersion.Text = version.ToString();
        }


        private async void btnOK_ClickAsync(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(tbVersion.Text.Trim()))
            {
                MessageBox.Show("版本信息不能为空！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                string versionInput = tbVersion.Text.Trim();
                btnOK.Enabled = false; // 禁用按钮，防止重复点击

                try
                {
                    DialogResult result = MessageBox.Show($"确认更新到版本：{versionInput} 吗？", "确认更新", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        bool isUpdated = await CheckAndUpdateAsync(versionInput);
                    }
                }
                catch (Exception ex)
                {
                    log.Error("btnOK_ClickAsync: " + ex.Message);
                }
                finally
                {
                    btnOK.Enabled = true; // 恢复按钮
                }
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            CloseForm();
        }

    }
}
