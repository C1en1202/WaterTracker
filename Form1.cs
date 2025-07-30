using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
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
                  LoadData();
                  InitializeUI();
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
            // 取消关闭并最小化到系统托盘
            e.Cancel = true;
            this.Hide();
        }

        private void OpenWaterTracker_Click(object? sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void ExitToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            // 清理资源并退出程序
            hourlyTimer?.Stop();
            hourlyTimer?.Dispose();
            notifyIcon?.Dispose();
            Application.Exit();
        }

        private void HourlyTimer_Tick(object? sender, EventArgs e)
        {
            CheckAndSendNotification();
        }

        private void CheckAndSendNotification()
        {
            // 检查是否为整点
            if (DateTime.Now.Minute == 0 && DateTime.Now.Second < 10)
            {
                // 发送系统通知
                notifyIcon?.ShowBalloonTip(10000,
                    "喝水提醒",
                    $"到{DateTime.Now.Hour}点啦,请喝水!",
                    ToolTipIcon.Info);
            }
        }

        private void Form1_Load(object? sender, EventArgs e)
        {
            UpdateWaterLevel();
        }

        private void InitializeUI()
        {
            // 设置窗口属性
            this.Text = "DrinkWater";
            this.Size = new Size(400, 600);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // 创建水瓶容器
            var bottleContainer = new Panel
            {
                Parent = this,
                Size = new Size(200, 400),
                Location = new Point(90, 50),
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

            // 创建喝水量输入框
            var amountTextBox = new TextBox
            {
                Parent = this,
                Text = "300",
                Size = new Size(60, 25),
                Location = new Point(100, 505),
                Font = new Font("微软雅黑", 10)
            };
            amountTextBox.KeyPress += (s, e) =>
            {
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back)
                    e.Handled = true;
            };

            // 创建喝水按钮
            var drinkButton = new Button
            {
                Parent = this,
                Text = "喝一次水",
                Size = new Size(100, 40),
                Location = new Point(180, 500),
                Font = new Font("微软雅黑", 10, FontStyle.Bold)
            };
            drinkButton.Click += (s, e) =>
            {
                int amount = int.TryParse(amountTextBox.Text, out int num) && num > 0 ? num : 300;
                DrinkButton_Click(amount);
            };

            // 创建删除存档按钮
            var deleteSaveButton = new Button
            {
                Parent = this,
                Text = "删除存档",
                Size = new Size(100, 40),
                Location = new Point(290, 500),
                Font = new Font("微软雅黑", 10, FontStyle.Bold)
            };
            deleteSaveButton.Click += (s, e) => DeleteSaveData();

            // 创建显示标签
            var statusLabel = new Label
            {
                Parent = this,
                Text = $"当前水瓶: {bottleNumber} | 当前水量: {currentVolume}ml / {MaxVolume}ml",
                Size = new Size(300, 20),
                Location = new Point(50, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("微软雅黑", 10)
            };

            this.PerformLayout();

            // 创建系统托盘菜单
            ContextMenuStrip contextMenu = new ContextMenuStrip();
            ToolStripMenuItem openItem = new ToolStripMenuItem("打开WaterTracker");
            openItem.Click += new EventHandler(OpenWaterTracker_Click);
            contextMenu.Items.Add(openItem);

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += new EventHandler(ExitToolStripMenuItem_Click);
            contextMenu.Items.Add(exitItem);

            // 添加存档删除菜单项
            ToolStripMenuItem deleteSaveItem = new ToolStripMenuItem("删除存档");
            deleteSaveItem.Click += (s, e) => DeleteSaveData();
            contextMenu.Items.Add(deleteSaveItem);

            // 添加300ml快速喝水菜单项
            ToolStripMenuItem quickDrinkItem = new ToolStripMenuItem("快速喝水(300ml)");
            quickDrinkItem.Click += (s, e) => DrinkButton_Click(300);
            contextMenu.Items.Add(quickDrinkItem);

            // 配置系统通知图标属性

              notifyIcon!.Visible = true;
              notifyIcon!.Icon = SystemIcons.Application;
              notifyIcon!.Text = "WaterTracker";
              notifyIcon!.ContextMenuStrip = contextMenu;

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
            int newVolume = currentVolume + amount;
            System.Diagnostics.Debug.WriteLine($"DrinkButton_Click: currentVolume={currentVolume}, amount={amount}, newVolume={newVolume}");
            // 检查是否超过水瓶容量
            if (newVolume > MaxVolume)
            {
                int excess = newVolume - MaxVolume;
                currentVolume = excess;
                bottleNumber++;
                MessageBox.Show($"水瓶 {bottleNumber - 1} 已喝完！开始新的水瓶 {bottleNumber}，继承 {excess}ml。");
            }
            else
            {
                currentVolume = newVolume;
            }

            // 更新水面板高度和状态标签
            UpdateWaterLevel();
            var statusLabel = this.Controls.OfType<Label>().First();
            statusLabel.Text = $"当前水瓶: {bottleNumber} | 当前水量: {currentVolume}ml / {MaxVolume}ml";
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
