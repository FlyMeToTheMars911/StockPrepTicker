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
        private readonly ListBox _instrumentListBox;
        private readonly TextBox _instrumentInput;
        private readonly Button _removeInstrumentButton;
        private readonly NumericUpDown _refreshIntervalInput;
        private readonly Dictionary<int, CheckBox> _movingAverageChecks;
        private readonly CheckBox _showTaskbarTickerCheckBox;
        private readonly Label _tickerPositionLabel;
        private readonly ComboBox _tickerPositionComboBox;
        private readonly Label _tickerPositionHint;
        private readonly Label _tickerRotationLabel;
        private readonly NumericUpDown _tickerRotationIntervalInput;
        private readonly Label _tickerRotationUnitLabel;
        private readonly ErrorProvider _errorProvider;
        private bool _hasCustomTickerLocation;
        private int _customTickerLeft;
        private int _customTickerTop;

        internal SettingsForm(AppSettings settings)
        {
            AppSettings editableSettings = SettingsStore.Clone(settings);
            _movingAverageChecks = new Dictionary<int, CheckBox>();
            _errorProvider = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink, ContainerControl = this };

            Text = "设置 - StockPerpTicker";
            ClientSize = new Size(500, 670);
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

            GroupBox instrumentGroup = CreateGroup("行情标的", new Point(18, 12), new Size(464, 180));
            _instrumentListBox = new ListBox
            {
                Location = new Point(16, 24),
                Size = new Size(300, 91),
                IntegralHeight = false
            };
            _instrumentListBox.SelectedIndexChanged += delegate
            {
                _removeInstrumentButton.Enabled = _instrumentListBox.SelectedIndex >= default(int);
            };
            _instrumentInput = new TextBox
            {
                Location = new Point(16, 124),
                Size = new Size(300, 25),
                CharacterCasing = CharacterCasing.Upper,
                MaxLength = 64
            };
            _instrumentInput.TextChanged += delegate { _errorProvider.SetError(_instrumentInput, string.Empty); };
            _instrumentInput.KeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.KeyCode == Keys.Enter)
                {
                    AddInstrument();
                    args.SuppressKeyPress = true;
                }
            };
            Button addInstrumentButton = CreateSecondaryButton("添加标的", new Point(326, 123), new Size(120, 28));
            addInstrumentButton.Click += delegate { AddInstrument(); };
            _removeInstrumentButton = CreateSecondaryButton("移除选中", new Point(326, 24), new Size(120, 28));
            _removeInstrumentButton.Enabled = false;
            _removeInstrumentButton.Click += delegate { RemoveSelectedInstrument(); };
            instrumentGroup.Controls.Add(_instrumentListBox);
            instrumentGroup.Controls.Add(_instrumentInput);
            instrumentGroup.Controls.Add(addInstrumentButton);
            instrumentGroup.Controls.Add(_removeInstrumentButton);
            instrumentGroup.Controls.Add(CreateHint(
                "输入完整的 OKX 永续合约代码并添加，最多 " + SettingsStore.MaximumInstrumentCount + " 个",
                new Point(16, 154),
                new Size(430, 20)));

            GroupBox refreshGroup = CreateGroup("刷新频率", new Point(18, 200), new Size(464, 78));
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

            GroupBox movingAverageGroup = CreateGroup("移动平均线", new Point(18, 286), new Size(464, 84));
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

            GroupBox taskbarTickerGroup = CreateGroup("最小化行为", new Point(18, 378), new Size(464, 154));
            _showTaskbarTickerCheckBox = new CheckBox
            {
                AutoSize = true,
                Location = new Point(16, 25),
                Text = "最小化到托盘时显示迷你行情条"
            };
            _showTaskbarTickerCheckBox.CheckedChanged += delegate { UpdateTickerPositionEnabled(); };
            _tickerPositionLabel = new Label { AutoSize = true, Location = new Point(16, 64), Text = "显示位置" };
            _tickerPositionComboBox = new ComboBox
            {
                Location = new Point(104, 59),
                Size = new Size(188, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _tickerPositionComboBox.Items.AddRange(new object[]
            {
                "屏幕左上角",
                "屏幕左下角",
                "屏幕右下角",
                "自定义位置"
            });
            _tickerPositionHint = CreateHint(
                "也可直接拖动迷你行情条，拖动后会自动保存位置",
                new Point(104, 89),
                new Size(340, 21));
            _tickerRotationLabel = new Label { AutoSize = true, Location = new Point(16, 122), Text = "标的轮播" };
            _tickerRotationIntervalInput = new NumericUpDown
            {
                Location = new Point(104, 117),
                Size = new Size(90, 25),
                Minimum = SettingsStore.MinimumTickerRotationIntervalSeconds,
                Maximum = SettingsStore.MaximumTickerRotationIntervalSeconds,
                TextAlign = HorizontalAlignment.Right
            };
            _tickerRotationUnitLabel = new Label { AutoSize = true, Location = new Point(201, 121), Text = "秒切换一次" };
            taskbarTickerGroup.Controls.Add(_showTaskbarTickerCheckBox);
            taskbarTickerGroup.Controls.Add(_tickerPositionLabel);
            taskbarTickerGroup.Controls.Add(_tickerPositionComboBox);
            taskbarTickerGroup.Controls.Add(_tickerPositionHint);
            taskbarTickerGroup.Controls.Add(_tickerRotationLabel);
            taskbarTickerGroup.Controls.Add(_tickerRotationIntervalInput);
            taskbarTickerGroup.Controls.Add(_tickerRotationUnitLabel);

            content.Controls.Add(instrumentGroup);
            content.Controls.Add(refreshGroup);
            content.Controls.Add(movingAverageGroup);
            content.Controls.Add(taskbarTickerGroup);

            Controls.Add(content);
            Controls.Add(footer);
            Controls.Add(header);

            _errorProvider.SetIconAlignment(_instrumentInput, ErrorIconAlignment.MiddleRight);
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
            _instrumentListBox.Items.Clear();
            string[] instrumentIds = settings.instrumentIds != null && settings.instrumentIds.Length > default(int)
                ? settings.instrumentIds
                : new[] { settings.instrumentId };
            foreach (string instrumentId in instrumentIds)
            {
                if (!string.IsNullOrWhiteSpace(instrumentId))
                {
                    _instrumentListBox.Items.Add(instrumentId);
                }
            }

            if (_instrumentListBox.Items.Count > default(int))
            {
                _instrumentListBox.SelectedIndex = default(int);
            }

            _instrumentInput.Clear();
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
            _hasCustomTickerLocation = settings.hasCustomTaskbarTickerPosition;
            _customTickerLeft = settings.taskbarTickerCustomLeft;
            _customTickerTop = settings.taskbarTickerCustomTop;
            _tickerRotationIntervalInput.Value = Math.Max(
                SettingsStore.MinimumTickerRotationIntervalSeconds,
                Math.Min(
                    SettingsStore.MaximumTickerRotationIntervalSeconds,
                    settings.taskbarTickerRotationIntervalSeconds == default(int)
                        ? SettingsStore.DefaultTickerRotationIntervalSeconds
                        : settings.taskbarTickerRotationIntervalSeconds));
            switch (settings.TickerPosition)
            {
                case TaskbarTickerPosition.TopLeft:
                    _tickerPositionComboBox.SelectedIndex = 0;
                    break;
                case TaskbarTickerPosition.BottomLeft:
                    _tickerPositionComboBox.SelectedIndex = 1;
                    break;
                case TaskbarTickerPosition.Custom:
                    _tickerPositionComboBox.SelectedIndex = 3;
                    break;
                default:
                    _tickerPositionComboBox.SelectedIndex = 2;
                    break;
            }

            UpdateTickerPositionEnabled();
            _instrumentInput.Focus();
        }

        private void UpdateTickerPositionEnabled()
        {
            bool enabled = _showTaskbarTickerCheckBox.Checked;
            _tickerPositionLabel.Enabled = enabled;
            _tickerPositionComboBox.Enabled = enabled;
            _tickerPositionHint.Enabled = enabled;
            _tickerRotationLabel.Enabled = enabled;
            _tickerRotationIntervalInput.Enabled = enabled;
            _tickerRotationUnitLabel.Enabled = enabled;
        }

        private void SaveSettings(object sender, EventArgs args)
        {
            if (!string.IsNullOrWhiteSpace(_instrumentInput.Text) && !AddInstrument())
            {
                return;
            }

            List<string> instrumentIds = new List<string>();
            foreach (object item in _instrumentListBox.Items)
            {
                instrumentIds.Add(Convert.ToString(item));
            }

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
                instrumentId = instrumentIds.Count > default(int) ? instrumentIds[0] : null,
                instrumentIds = instrumentIds.ToArray(),
                refreshIntervalMilliseconds = decimal.ToInt32(_refreshIntervalInput.Value),
                movingAverages = movingAverages.ToArray(),
                showTaskbarTickerOnMinimize = _showTaskbarTickerCheckBox.Checked,
                taskbarTickerPosition = GetSelectedTickerPosition(),
                hasCustomTaskbarTickerPosition = _hasCustomTickerLocation,
                taskbarTickerCustomLeft = _customTickerLeft,
                taskbarTickerCustomTop = _customTickerTop,
                taskbarTickerRotationIntervalSeconds = decimal.ToInt32(_tickerRotationIntervalInput.Value)
            };
            AppSettings normalizedSettings;
            string error;
            if (!SettingsStore.TryNormalize(candidate, out normalizedSettings, out error))
            {
                _errorProvider.SetError(_instrumentInput, error);
                _instrumentInput.Focus();
                return;
            }

            Settings = normalizedSettings;
            DialogResult = DialogResult.OK;
            Close();
        }

        private bool AddInstrument()
        {
            string normalizedInstrumentId;
            string error;
            if (!SettingsStore.TryNormalizeInstrumentId(_instrumentInput.Text, out normalizedInstrumentId, out error))
            {
                _errorProvider.SetError(_instrumentInput, error);
                _instrumentInput.Focus();
                _instrumentInput.SelectAll();
                return false;
            }

            for (int index = default(int); index < _instrumentListBox.Items.Count; index++)
            {
                if (string.Equals(
                    Convert.ToString(_instrumentListBox.Items[index]),
                    normalizedInstrumentId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    _instrumentListBox.SelectedIndex = index;
                    _instrumentInput.Clear();
                    return true;
                }
            }

            if (_instrumentListBox.Items.Count >= SettingsStore.MaximumInstrumentCount)
            {
                _errorProvider.SetError(
                    _instrumentInput,
                    "最多可以配置 " + SettingsStore.MaximumInstrumentCount + " 个行情标的。");
                return false;
            }

            _instrumentListBox.Items.Add(normalizedInstrumentId);
            _instrumentListBox.SelectedIndex = _instrumentListBox.Items.Count - 1;
            _instrumentInput.Clear();
            _instrumentInput.Focus();
            return true;
        }

        private void RemoveSelectedInstrument()
        {
            int selectedIndex = _instrumentListBox.SelectedIndex;
            if (selectedIndex < default(int))
            {
                return;
            }

            _instrumentListBox.Items.RemoveAt(selectedIndex);
            if (_instrumentListBox.Items.Count > default(int))
            {
                _instrumentListBox.SelectedIndex = Math.Min(selectedIndex, _instrumentListBox.Items.Count - 1);
            }

            _instrumentInput.Focus();
        }

        private string GetSelectedTickerPosition()
        {
            switch (_tickerPositionComboBox.SelectedIndex)
            {
                case 0:
                    return "topLeft";
                case 1:
                    return "bottomLeft";
                case 3:
                    return "custom";
                default:
                    return "bottomRight";
            }
        }
    }
}
