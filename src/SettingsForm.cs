using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace StockPerpTicker
{
    internal sealed class SettingsForm : Form
    {
        private static readonly Color AccentColor = Color.FromArgb(8, 153, 129);
        private static readonly Color SecondaryTextColor = Color.FromArgb(90, 96, 110);
        private readonly TextBox _instrumentTextBox;
        private readonly NumericUpDown _refreshIntervalInput;
        private readonly Dictionary<int, CheckBox> _movingAverageChecks;
        private readonly CheckBox _showTaskbarTickerCheckBox;
        private readonly Label _tickerPositionLabel;
        private readonly RadioButton _bottomLeftRadioButton;
        private readonly RadioButton _bottomRightRadioButton;
        private readonly ErrorProvider _errorProvider;

        internal SettingsForm(AppSettings settings)
        {
            AppSettings editableSettings = SettingsStore.Clone(settings);
            _movingAverageChecks = new Dictionary<int, CheckBox>();
            _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink, ContainerControl = this };

            Text = "设置 - StockPerpTicker";
            ClientSize = new Size(500, 550);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.FromArgb(248, 250, 252)
            };
            header.Controls.Add(new Label
            {
                AutoSize = true,
                Location = new Point(18, 10),
                Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold, GraphicsUnit.Point),
                Text = "行情设置"
            });
            header.Controls.Add(new Label
            {
                AutoSize = true,
                Location = new Point(19, 37),
                ForeColor = SecondaryTextColor,
                Text = "保存后立即应用，无需重启程序"
            });

            Panel footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 62,
                BackColor = Color.FromArgb(248, 250, 252)
            };
            Button resetButton = CreateSecondaryButton("恢复默认", new Point(18, 16), new Size(88, 32));
            resetButton.Click += delegate { LoadSettings(SettingsStore.CreateDefault()); };
            Button cancelButton = CreateSecondaryButton("取消", new Point(298, 16), new Size(78, 32));
            cancelButton.DialogResult = DialogResult.Cancel;
            Button saveButton = new Button
            {
                Location = new Point(386, 16),
                Size = new Size(96, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentColor,
                ForeColor = Color.White,
                Text = "保存并应用",
                DialogResult = DialogResult.None
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.Click += SaveSettings;
            footer.Controls.Add(resetButton);
            footer.Controls.Add(cancelButton);
            footer.Controls.Add(saveButton);
            footer.Resize += delegate
            {
                saveButton.Left = Math.Max(0, footer.ClientSize.Width - saveButton.Width - 18);
                cancelButton.Left = Math.Max(0, saveButton.Left - cancelButton.Width - 10);
            };

            Panel content = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White
            };

            GroupBox instrumentGroup = CreateGroup("行情合约", new Point(18, 12), new Size(464, 95));
            _instrumentTextBox = new TextBox
            {
                Location = new Point(16, 24),
                Size = new Size(430, 25),
                CharacterCasing = CharacterCasing.Upper,
                MaxLength = 64
            };
            _instrumentTextBox.TextChanged += delegate { _errorProvider.SetError(_instrumentTextBox, string.Empty); };
            instrumentGroup.Controls.Add(_instrumentTextBox);
            instrumentGroup.Controls.Add(CreateHint("请输入完整的 OKX 永续合约代码，例如 AAPL-USDT-SWAP", new Point(16, 55), new Size(430, 21)));

            GroupBox refreshGroup = CreateGroup("刷新频率", new Point(18, 115), new Size(464, 78));
            _refreshIntervalInput = new NumericUpDown
            {
                Location = new Point(16, 25),
                Size = new Size(130, 25),
                Minimum = SettingsStore.MinimumRefreshIntervalMilliseconds,
                Maximum = SettingsStore.MaximumRefreshIntervalMilliseconds,
                Increment = 250,
                TextAlign = HorizontalAlignment.Right
            };
            refreshGroup.Controls.Add(_refreshIntervalInput);
            refreshGroup.Controls.Add(new Label { AutoSize = true, Location = new Point(153, 28), Text = "毫秒" });
            refreshGroup.Controls.Add(CreateHint("仅影响界面绘制，网络行情会持续接收", new Point(211, 27), new Size(235, 21)));

            GroupBox movingAverageGroup = CreateGroup("移动平均线", new Point(18, 201), new Size(464, 84));
            FlowLayoutPanel movingAveragePanel = new FlowLayoutPanel
            {
                Location = new Point(12, 25),
                Size = new Size(440, 40),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            foreach (int period in SettingsStore.MovingAverageOptions)
            {
                CheckBox checkBox = new CheckBox
                {
                    AutoSize = true,
                    Margin = new Padding(5, 6, 10, 3),
                    Text = "MA" + period
                };
                _movingAverageChecks[period] = checkBox;
                movingAveragePanel.Controls.Add(checkBox);
            }
            movingAverageGroup.Controls.Add(movingAveragePanel);

            GroupBox taskbarTickerGroup = CreateGroup("最小化行为", new Point(18, 293), new Size(464, 110));
            _showTaskbarTickerCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(16, 25),
                Text = "最小化到托盘时显示迷你行情条"
            };
            _showTaskbarTickerCheckBox.CheckedChanged += delegate { UpdateTickerPositionEnabled(); };
            _tickerPositionLabel = new Label { AutoSize = true, Location = new Point(16, 65), Text = "显示位置" };
            _bottomLeftRadioButton = new RadioButton { AutoSize = true, Location = new Point(104, 63), Text = "屏幕左下角" };
            _bottomRightRadioButton = new RadioButton { AutoSize = true, Location = new Point(224, 63), Text = "屏幕右下角" };
            taskbarTickerGroup.Controls.Add(_showTaskbarTickerCheckBox);
            taskbarTickerGroup.Controls.Add(_tickerPositionLabel);
            taskbarTickerGroup.Controls.Add(_bottomLeftRadioButton);
            taskbarTickerGroup.Controls.Add(_bottomRightRadioButton);

            content.Controls.Add(instrumentGroup);
            content.Controls.Add(refreshGroup);
            content.Controls.Add(movingAverageGroup);
            content.Controls.Add(taskbarTickerGroup);

            Controls.Add(content);
            Controls.Add(footer);
            Controls.Add(header);

            _errorProvider.SetIconAlignment(_instrumentTextBox, ErrorIconAlignment.MiddleRight);
            AcceptButton = saveButton;
            CancelButton = cancelButton;
            LoadSettings(editableSettings);
        }

        internal AppSettings Settings { get; private set; }

        private static GroupBox CreateGroup(string text, Point location, Size size)
        {
            return new GroupBox
            {
                Text = text,
                Location = location,
                Size = size,
                ForeColor = Color.FromArgb(19, 23, 34)
            };
        }

        private static Label CreateHint(string text, Point location, Size size)
        {
            return new Label
            {
                AutoSize = false,
                Location = location,
                Size = size,
                ForeColor = SecondaryTextColor,
                Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Regular, GraphicsUnit.Point),
                Text = text
            };
        }

        private static Button CreateSecondaryButton(string text, Point location, Size size)
        {
            Button button = new Button
            {
                Location = location,
                Size = size,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(19, 23, 34),
                Text = text
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(210, 214, 222);
            return button;
        }

        private void LoadSettings(AppSettings settings)
        {
            _instrumentTextBox.Text = settings.instrumentId;
            decimal refreshInterval = Math.Max(
                SettingsStore.MinimumRefreshIntervalMilliseconds,
                Math.Min(SettingsStore.MaximumRefreshIntervalMilliseconds, settings.refreshIntervalMilliseconds));
            _refreshIntervalInput.Value = refreshInterval;
            foreach (KeyValuePair<int, CheckBox> entry in _movingAverageChecks)
            {
                entry.Value.Checked = settings.movingAverages != null
                    && Array.IndexOf(settings.movingAverages, entry.Key) >= default(int);
            }

            _showTaskbarTickerCheckBox.Checked = settings.showTaskbarTickerOnMinimize;
            _bottomLeftRadioButton.Checked = settings.TickerPosition == TaskbarTickerPosition.BottomLeft;
            _bottomRightRadioButton.Checked = settings.TickerPosition != TaskbarTickerPosition.BottomLeft;
            UpdateTickerPositionEnabled();
            _instrumentTextBox.Focus();
            _instrumentTextBox.SelectAll();
        }

        private void UpdateTickerPositionEnabled()
        {
            bool enabled = _showTaskbarTickerCheckBox.Checked;
            _tickerPositionLabel.Enabled = enabled;
            _bottomLeftRadioButton.Enabled = enabled;
            _bottomRightRadioButton.Enabled = enabled;
        }

        private void SaveSettings(object sender, EventArgs args)
        {
            List<int> movingAverages = new List<int>();
            foreach (int period in SettingsStore.MovingAverageOptions)
            {
                if (_movingAverageChecks[period].Checked)
                {
                    movingAverages.Add(period);
                }
            }

            AppSettings candidate = new AppSettings
            {
                instrumentId = _instrumentTextBox.Text,
                refreshIntervalMilliseconds = decimal.ToInt32(_refreshIntervalInput.Value),
                movingAverages = movingAverages.ToArray(),
                showTaskbarTickerOnMinimize = _showTaskbarTickerCheckBox.Checked,
                taskbarTickerPosition = _bottomLeftRadioButton.Checked ? "bottomLeft" : "bottomRight"
            };
            AppSettings normalizedSettings;
            string error;
            if (!SettingsStore.TryNormalize(candidate, out normalizedSettings, out error))
            {
                _errorProvider.SetError(_instrumentTextBox, error);
                _instrumentTextBox.Focus();
                _instrumentTextBox.SelectAll();
                return;
            }

            Settings = normalizedSettings;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
