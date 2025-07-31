using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Text;
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

        // 时间设置窗口
        public class TimeForm : Form
        {
        private TextBox timeTextBox;
        private System.Windows.Forms.Timer? reminderTimer;
        private Font customFont;

        public TimeForm()
        {
            // 加载自定义字体
            try
            {
                string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ttf", "ttf.ttf");
                if (File.Exists(fontPath))
                {
                    PrivateFontCollection fontCollection = new PrivateFontCollection();
                    fontCollection.AddFontFile(fontPath);
                    if (fontCollection.Families.Length > 0)
                    {
                        customFont = new Font(fontCollection.Families[0], 10, FontStyle.Regular);
                    }
                    else
                    {
                        customFont = new Font("微软雅黑", 10, FontStyle.Regular);
                    }
                }
                else
                {
                    customFont = new Font("微软雅黑", 10, FontStyle.Regular);
                }
            }
            catch
            {
                customFont = new Font("微软雅黑", 10, FontStyle.Regular);
            }
            
            this.Text = "Time";
            this.Size = new Size(300, 150);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            // 创建标签
            var label = new Label
            {
                Parent = this,
                Text = "请设置提醒时间:",
                Location = new Point(20, 20),
                Font = customFont
            };

            // 创建时间输入框
            timeTextBox = new TextBox
            {
                Parent = this,
                Size = new Size(100, 25),
                Location = new Point(140, 20),
                Font = customFont,
                Text = "输入格式:14:30", // 24小时制示例
                ForeColor = Color.Gray
            };
            timeTextBox.Enter += (s, e) =>
            {
                if (timeTextBox.Text == "输入格式:11:30")
                {
                    timeTextBox.Text = "";
                    timeTextBox.ForeColor = Color.Black;
                }
            };
            timeTextBox.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(timeTextBox.Text))
                {
                    timeTextBox.Text = "输入格式:11:30";
                    timeTextBox.ForeColor = Color.Gray;
                }
            };
            timeTextBox.KeyPress += (s, e) =>
            {
                if (e.KeyChar == (char)Keys.Enter)
                {
                    SetReminder();
                }
            };

            // 创建确认按钮
            var confirmButton = new Button
            {
                Parent = this,
                Text = "确认",
                Size = new Size(80, 30),
                Location = new Point((this.Width - 80) / 2, 60),
                Font = customFont
            };
            confirmButton.Click += (s, e) => SetReminder();
        }

        private void SetReminder()
        {
            string timeText = timeTextBox.Text.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(timeText, @"^([01]?[0-9]|2[0-3]):[0-5][0-9]$"))
            {
                MessageBox.Show("时间格式不正确，请使用24小时制HH:mm格式");
                return;
            }

            try
            {
                // 解析时间
                string[] parts = timeText.Split(':');
                int hour = int.Parse(parts[0]);
                int minute = int.Parse(parts[1]);

                // 设置提醒定时器
                reminderTimer = new System.Windows.Forms.Timer
                {
                    Interval = 60000 // 每分钟检查一次
                };
                reminderTimer.Tick += (s, e) => CheckCustomReminder(hour, minute);
                reminderTimer.Start();

                MessageBox.Show("已设置在" + hour + ":" + minute + "提醒喝水");
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("设置提醒失败: " + ex.Message);
            }
        }

        private void CheckCustomReminder(int targetHour, int targetMinute)
        {
            DateTime now = DateTime.Now;
            if (now.Hour == targetHour && now.Minute == targetMinute && now.Second < 10)
            {
                // 不需要停止定时器，让它继续每分钟检查一次
                // 这样可以实现每天同一时间提醒

                // 计算累计喝水量
                Form1? mainForm = Application.OpenForms["Form1"] as Form1;
                if (mainForm != null)
                {
                    int totalVolume = (mainForm.bottleNumber - 1) * Form1.MaxVolume + mainForm.currentVolume;

                    // 发送系统通知
                    mainForm.notifyIcon?.ShowBalloonTip(15000,
                        "喝水提醒",
                        $"到{targetHour}:{targetMinute}啦,请喝水!\n本存档已记录喝水量{totalVolume}ml",
                        ToolTipIcon.Info);
                }
            }
        }
    }

        public Form1()
        {
            InitializeComponent();
              this.Icon = null;
              notifyIcon = new NotifyIcon();
              // 设置通知图标
              notifyIcon.Icon = SystemIcons.Application;
              notifyIcon.Visible = true;
              try
              {
                  LoadData();
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

              Load += Form1_Load;
              FormClosing += Form1_FormClosing;
        }

        private void LoadData()
        {
            if (File.Exists(savePath))
            {
                try
                {
                    string json = File.ReadAllText(savePath);
                    var data = JsonSerializer.Deserialize<WaterSaveData>(json);
                    if (data != null)
                    {
                        currentVolume = data.CurrentVolume;
                    bottleNumber = data.BottleNumber > 0 ? data.BottleNumber : 1;
                }
                UpdateStatusLabel();
                UpdateDrinkButtonText();
                }
                catch {}
            }
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
            this.BringToFront();
            this.Activate();
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
                // 计算累计喝水量
                int totalVolume = (bottleNumber - 1) * MaxVolume + currentVolume;
                
                // 发送系统通知
                notifyIcon?.ShowBalloonTip(15000,
                    "喝水提醒",
                    $"到{DateTime.Now.Hour}点啦,请喝水!\n本存档已记录喝水量{totalVolume}ml",
                    ToolTipIcon.Info);
            }
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            UpdateWaterLevel();
        }

        private System.Windows.Forms.Timer? reminderTimer;

        private void InitializeUI()
        {
            // 加载自定义字体
            Font customFont = null;
            try
            {
                string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ttf", "ttf.ttf");
                if (File.Exists(fontPath))
                {
                    PrivateFontCollection fontCollection = new PrivateFontCollection();
                    fontCollection.AddFontFile(fontPath);
                    if (fontCollection.Families.Length > 0)
                    {
                        customFont = new Font(fontCollection.Families[0], 10, FontStyle.Regular);
                    }
                    else
                    {
                        customFont = new Font("微软雅黑", 10, FontStyle.Regular);
                    }
                }
                else
                {
                    customFont = new Font("微软雅黑", 10, FontStyle.Regular);
                }
            }
            catch
            {
                customFont = new Font("微软雅黑", 10, FontStyle.Regular);
            }

            // 设置窗口属性
            this.Text = "DrinkWater";
            this.Size = new Size(500, 700);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

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
                Font = customFont
            };

            // 创建喝水按钮
            drinkButton = new Button
            {
                Parent = firstRowPanel,
                Text = "喝一次水",
                Size = new Size(120, 35),
                Location = new Point(180, 0),
                Font = new Font(customFont.FontFamily, 10, FontStyle.Bold)
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
                Font = customFont
            };
            deleteSaveButton.Click += (s, e) => DeleteSaveData();

            // 创建开机自启动按钮
            startupButton = new Button
            {
                Parent = secondRowPanel,
                Text = IsStartupEnabled() ? "禁用开机自启" : "启用开机自启",
                Size = new Size(140, 35),
                Location = new Point(160, 0),
                Font = customFont
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
                Font = customFont
            };

            this.PerformLayout();

            // 初始化整点提醒定时器
            hourlyTimer = new System.Windows.Forms.Timer
            {
                Interval = 60000 // 每分钟检查一次
            };
            hourlyTimer.Tick += HourlyTimer_Tick;
            hourlyTimer.Start();

            // 创建第三行容器
            var thirdRowPanel = new Panel
            {
                Parent = this,
                Size = new Size(300, 40),
                Location = new Point((this.Width - 300) / 2, 600)
            };

            // 创建提醒喝水按钮
            var remindButton = new Button
            {
                Parent = thirdRowPanel,
                Text = "请提醒我喝水",
                Size = new Size(150, 35),
                Location = new Point((thirdRowPanel.Width - 150) / 2, 0),
                Font = customFont
            };
            remindButton.Click += (s, e) =>
            {
                var timeForm = new TimeForm();
                timeForm.ShowDialog();
            };

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
              notifyIcon!.MouseClick += NotifyIcon_MouseClick;

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

        private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                OpenWaterTracker_Click(sender, e);
            }
        }

        private void UpdateWaterLevel()
        {
            if (waterPanel?.Parent is Panel bottleContainer)
            {
                float percentage = (float)currentVolume / MaxVolume;
                int containerHeight = bottleContainer.ClientSize.Height;
                int waterHeight = (int)(percentage * containerHeight);
                waterPanel.Size = new Size(bottleContainer.ClientSize.Width - 2, waterHeight);
                waterPanel.Location = new Point(1, containerHeight - waterHeight);
                waterPanel.Invalidate();
                bottleContainer.Invalidate();
                // 调试输出
                System.Diagnostics.Debug.WriteLine($"UpdateWaterLevel: currentVolume={currentVolume}, percentage={percentage}, waterHeight={waterHeight}");
            }
        }
    }
}