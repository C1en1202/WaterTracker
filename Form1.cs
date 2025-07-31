using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using WinFormsTimer = System.Windows.Forms.Timer;

using System;


namespace WaterTracker
{
    public partial class Form1 : Form
    {
        private int currentVolume = 0;
        private int bottleNumber = 1; // 水瓶计数从1开始
        private const int MaxVolume = 3000;
        private bool _allowClose = false;
        private Button? drinkButton;
        private Button? startupButton;
        private Label? statusLabel;

        private Panel? waterPanel = null;
        private string savePath = Path.Combine(AppContext.BaseDirectory, "water.json");
        private NotifyIcon? notifyIcon;
        private WinFormsTimer? hourlyTimer;

        private class WaterSaveData
        {
            public int CurrentVolume { get; set; }
            public int BottleNumber { get; set; }
        }

        public Form1()
        {
            InitializeComponent();
              this.Icon = null;
              notifyIcon = new NotifyIcon();
              try
              {
                  InitializeUI();
            UpdateDrinkButtonText();
            UpdateStatusLabel();
              }
              catch (Exception ex)
              {
                  MessageBox.Show("初始化失败: " + ex.Message);
                  Application.Exit();
              }
              // 移除图标设置
              
              Shown += Form1_Shown;
              FormClosing += Form1_FormClosing;
        }

        private void LoadData()
        {
            try
            {
                if (File.Exists(savePath))
                {
                    string json = File.ReadAllText(savePath);
                    if (!string.IsNullOrEmpty(json) && json.Trim() != "{}")
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var data = JsonSerializer.Deserialize<WaterSaveData>(json, options);
                        if (data != null)
                        {
                            currentVolume = data.CurrentVolume;
                            bottleNumber = data.BottleNumber > 0 ? data.BottleNumber : 1;
                            System.Diagnostics.Debug.WriteLine($"LoadData: Loaded currentVolume={currentVolume}, bottleNumber={bottleNumber}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("LoadData: Deserialized data is null");
                            currentVolume = 0; // 设置默认水量为0
                            bottleNumber = 1;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("LoadData: JSON file is empty or contains only {}");
                        currentVolume = 0; // 设置默认水量为0
                        bottleNumber = 1;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("LoadData: Save file does not exist");
                    currentVolume = 0; // 设置默认水量为0
                    bottleNumber = 1;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadData error: {ex.Message}");
                // 出错时设置默认值
                currentVolume = 0;
                bottleNumber = 1;
            }
            
            UpdateStatusLabel();
            UpdateDrinkButtonText();
            UpdateWaterLevel();
            // 确保水位更新
            BeginInvoke(new Action(UpdateWaterLevel));
            System.Diagnostics.Debug.WriteLine("LoadData: Completed, calling UpdateWaterLevel");
        }

        private void DeleteSaveData()
        {
            try
            {
                if (File.Exists(savePath))
                {
                    File.Delete(savePath);
                    currentVolume = 0;
                    bottleNumber = 1;
                    UpdateWaterLevel();
                    UpdateStatusLabel();
                    UpdateDrinkButtonText();
                    UpdateWaterLevel();
                    var statusLabel = this.Controls.OfType<Label>().First();
                    statusLabel.Text = $"当前水瓶: {bottleNumber} | 当前水量: {currentVolume}ml / {MaxVolume}ml";
                    MessageBox.Show("存档已删除");
                }
                else
                {
                    MessageBox.Show("没有找到存档文件");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("删除存档失败: " + ex.Message);
            }
        }

        private void SaveData()
        {
            try
            {
                var data = new WaterSaveData
                {
                    CurrentVolume = currentVolume,
                    BottleNumber = bottleNumber
                };
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            string directory = Path.GetDirectoryName(savePath) ?? AppContext.BaseDirectory;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(savePath, json);
            }
            catch {}
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveData();
            if (!_allowClose)
            {
                // 取消关闭并最小化到系统托盘
                e.Cancel = true;
                this.Hide();
            }
        }

        private void OpenWaterTracker_Click(object? sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void ExitToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            // 清理资源并退出程序
            _allowClose = true;
            hourlyTimer?.Stop();
            hourlyTimer?.Dispose();
            notifyIcon?.Dispose();
            Application.Exit();
        }

        private void HourlyTimer_Tick(object? sender, EventArgs e)
        {
            CheckAndSendNotification();
            UpdateWaterLevel();
        }

        private void UpdateDrinkButtonText()
        {
            if (drinkButton != null)
            {
                drinkButton.Text = currentVolume >= MaxVolume ? "下一个水瓶" : "喝一次水";
            }
        }

        private void UpdateStatusLabel()
        {
            if (statusLabel != null)
            {
                statusLabel.Text = $"当前水瓶: {bottleNumber} | 当前水量: {currentVolume}ml / {MaxVolume}ml";
                statusLabel.Location = new Point(0, 10);
                statusLabel.Width = this.Width;
                statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            }
        }

        private bool IsStartupEnabled()
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
            {
                if (key == null) return false;
                string? value = key!.GetValue("WaterTracker") as string;
                return value != null;
            }
        }

        private void ToggleStartup()
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (key == null) return;
                
                if (IsStartupEnabled())
                {
                    key!.DeleteValue("WaterTracker");
                    MessageBox.Show("已禁用开机自启动", "提示");
                }
                else
                {
                    string appPath = Application.ExecutablePath;
                    key!.SetValue("WaterTracker", appPath);
                    MessageBox.Show("已启用开机自启动", "提示");
                }
                // 更新按钮文本
                if (startupButton != null)
                {
                    startupButton.Text = IsStartupEnabled() ? "禁用开机自启" : "启用开机自启";
                }
            }
        }

        private void CheckAndSendNotification()
        {
            // 检查是否为整点
            if (DateTime.Now.Minute == 0 && DateTime.Now.Second < 10)
            {
                // 发送系统通知
                notifyIcon?.ShowBalloonTip(15000,
                    "喝水提醒",
                    $"到{DateTime.Now.Hour}点啦,请喝水!",
                    ToolTipIcon.Info);
            }
        }

        private void Form1_Shown(object? sender, EventArgs e)
        {
            LoadData();
            // 确保UI完全加载后再更新水位
            BeginInvoke(new Action(() =>
            {
                // 强制重新计算和更新水位
                UpdateWaterLevel();
                // 调试信息
                System.Diagnostics.Debug.WriteLine("Form1_Shown: Updated water level");
            }));
        }

        private void InitializeUI()
        {
            // 设置窗口属性
            this.Text = "DrinkWater";
            this.Size = new Size(600, 800); // 增大窗口大小
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // 创建状态标签并置于窗口顶部居中
            statusLabel = new Label
            {
                Parent = this,
                Location = new Point(0, 10),
                Width = this.Width,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("微软雅黑", 10)
            };

            // 创建水瓶容器
            var bottleContainer = new Panel
            {
                Parent = this,
                Size = new Size(200, 400),
                Location = new Point((this.Width - 200) / 2, 50),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.LightGray
            };
            bottleContainer.Resize += (s, e) => UpdateWaterLevel();

            // 创建水面板
            waterPanel = new Panel
            {
                Parent = bottleContainer,
                Size = new Size(bottleContainer.Width - 2, 0),
                Location = new Point(1, bottleContainer.Height - 1),
                BackColor = Color.DodgerBlue,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // 创建第一行容器
            var firstRowPanel = new Panel
            {
                Parent = this,
                Size = new Size(300, 40),
                Location = new Point((this.Width - 300) / 2, 500)
            };

            // 创建喝水量输入框
            var amountTextBox = new TextBox
            {
                Parent = firstRowPanel,
                Text = "300",
                Size = new Size(80, 35),
                Location = new Point(0, 0),
                Font = new Font("微软雅黑", 10)
            };

            // 创建喝水按钮
            drinkButton = new Button
            {
                Parent = firstRowPanel,
                Text = "喝一次水",
                Size = new Size(120, 35),
                Location = new Point(180, 0),
                Font = new Font("微软雅黑", 10, FontStyle.Bold)
            };
            drinkButton.Click += (s, e) =>
            {
                int amount = int.TryParse(amountTextBox.Text, out int num) && num > 0 ? num : 300;
                DrinkButton_Click(amount);
            };

            // 创建第二行容器
            var secondRowPanel = new Panel
            {
                Parent = this,
                Size = new Size(300, 40),
                Location = new Point((this.Width - 300) / 2, 550)
            };
            amountTextBox.KeyPress += (s, e) =>
            {
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back)
                    e.Handled = true;
            };

            // 创建删除存档按钮
            var deleteSaveButton = new Button
            {
                Parent = secondRowPanel,
                Text = "删除存档",
                Size = new Size(140, 35),
                Location = new Point(0, 0),
                Font = new Font("微软雅黑", 10)
            };
            deleteSaveButton.Click += (s, e) => DeleteSaveData();

            // 创建开机自启动按钮
            startupButton = new Button
            {
                Parent = secondRowPanel,
                Text = IsStartupEnabled() ? "禁用开机自启" : "启用开机自启",
                Size = new Size(140, 35),
                Location = new Point(160, 0),
                Font = new Font("微软雅黑", 10)
            };
            startupButton.Click += (s, e) => ToggleStartup();

            // 创建显示标签
            statusLabel = new Label
            {
                Parent = this,
                Text = $"当前水瓶: {bottleNumber} | 当前水量: {currentVolume}ml / {MaxVolume}ml",
                Size = new Size(300, 20),
                Location = new Point(50, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("微软雅黑", 10)
            };

            this.PerformLayout();
            UpdateWaterLevel();

            // 创建系统托盘菜单
            ContextMenuStrip contextMenu = new ContextMenuStrip();
            ToolStripMenuItem openItem = new ToolStripMenuItem("打开WaterTracker");
            openItem.Click += new EventHandler(OpenWaterTracker_Click);
            contextMenu.Items.Add(openItem);

            // 添加300ml快速喝水菜单项
            ToolStripMenuItem quickDrinkItem = new ToolStripMenuItem("快速喝水(300ml)");
            quickDrinkItem.Click += (s, e) => DrinkButton_Click(300);
            contextMenu.Items.Add(quickDrinkItem);

            // 添加存档删除菜单项
            ToolStripMenuItem deleteSaveItem = new ToolStripMenuItem("删除存档");
            deleteSaveItem.Click += (s, e) => DeleteSaveData();
            contextMenu.Items.Add(deleteSaveItem);

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += new EventHandler(ExitToolStripMenuItem_Click);
            contextMenu.Items.Add(exitItem);

            // 配置系统通知图标属性

              notifyIcon!.Visible = true;
              notifyIcon!.Icon = SystemIcons.Application;
              notifyIcon!.Text = "WaterTracker";
              notifyIcon!.ContextMenuStrip = contextMenu;
              notifyIcon!.MouseClick += (s, e) =>
              {
                  if (e.Button == MouseButtons.Left)
                  {
                      this.Show();
                      this.WindowState = FormWindowState.Normal;
                  }
              };

            // 初始化整点提醒定时器
            hourlyTimer = new WinFormsTimer
            {
                Interval = 60000 // 每分钟检查一次
            };

            hourlyTimer.Tick += HourlyTimer_Tick;
            hourlyTimer.Start();

            // 立即检查一次是否需要发送通知
            CheckAndSendNotification();
        }

        private void DrinkButton_Click(int amount)
        {
            if (currentVolume >= MaxVolume)
            {
                // 进入下一个水瓶
                currentVolume = 0;
                bottleNumber++;
                UpdateDrinkButtonText();
                UpdateWaterLevel();
                UpdateStatusLabel();
                SaveData();
                MessageBox.Show($"已切换到第{bottleNumber}个水瓶！");
                return;
            }

            int newVolume = currentVolume + amount;
            if (newVolume > MaxVolume)
            {
                int excess = newVolume - MaxVolume;
                currentVolume = excess;
                bottleNumber++;
            }
            else
            {
                currentVolume = newVolume;
            }
            
            UpdateWaterLevel();
            UpdateStatusLabel();
            UpdateDrinkButtonText();
            SaveData();
        }

        private void UpdateWaterLevel()
        {
            System.Diagnostics.Debug.WriteLine("UpdateWaterLevel: Starting");
            
            if (waterPanel == null)
            {
                System.Diagnostics.Debug.WriteLine("UpdateWaterLevel: waterPanel is null");
                // 尝试查找waterPanel
                waterPanel = this.Controls.Find("waterPanel", true).FirstOrDefault() as Panel;
                if (waterPanel == null)
                {
                    System.Diagnostics.Debug.WriteLine("UpdateWaterLevel: Could not find waterPanel");
                    return;
                }
            }
            
            if (waterPanel?.Parent is Panel bottleContainer)
            {
                System.Diagnostics.Debug.WriteLine("UpdateWaterLevel: bottleContainer found");
                
                // 确保容器布局已更新
                bottleContainer.PerformLayout();
                
                // 计算水位百分比
                float percentage = (float)currentVolume / MaxVolume;
                
                // 获取容器高度
                int containerHeight = bottleContainer.ClientSize.Height;
                System.Diagnostics.Debug.WriteLine($"UpdateWaterLevel: containerHeight={containerHeight}");
                
                // 计算水面板高度
                int waterHeight = (int)(percentage * containerHeight);
                
                // 当水量为0时，设置高度为0，否则确保有一个最小高度
                waterHeight = currentVolume == 0 ? 0 : Math.Max(waterHeight, 5);
                
                // 确保水面板高度不会超过容器高度
                waterHeight = Math.Min(waterHeight, containerHeight);
                
                // 设置水面板大小和位置
                waterPanel.Size = new Size(bottleContainer.ClientSize.Width - 2, waterHeight);
                waterPanel.Location = new Point(1, bottleContainer.ClientSize.Height - waterHeight);
                
                // 确保水面板可见
                waterPanel.Visible = true;
                waterPanel.BringToFront();
                
                // 刷新控件
                waterPanel.Invalidate();
                waterPanel.Refresh();
                bottleContainer.Invalidate();
                bottleContainer.Refresh();
                
                // 调试输出
                System.Diagnostics.Debug.WriteLine($"UpdateWaterLevel: currentVolume={currentVolume}, percentage={percentage}, waterHeight={waterHeight}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("UpdateWaterLevel: bottleContainer is null or not a Panel");
                if (waterPanel?.Parent != null)
                {
                    System.Diagnostics.Debug.WriteLine($"UpdateWaterLevel: waterPanel.Parent type is {waterPanel.Parent.GetType().Name}");
                }
            }
            
            System.Diagnostics.Debug.WriteLine("UpdateWaterLevel: Completed");
        }
    }
}
