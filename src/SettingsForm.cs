using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace StockPerpTicker
{
    internal sealed class SettingsForm : Form
    {
        private const int DefaultComboBoxIndex = 0;
        private const int BeijingTimeZoneIndex = 0;
        private const int UsEasternTimeZoneIndex = 1;
        private const string PositionCostFormat = "0.############################";
        private static readonly Color AccentColor = Color.FromArgb(8, 153, 129);
        private static readonly Color ErrorColor = Color.FromArgb(192, 57, 43);
        private static readonly Color SecondaryTextColor = Color.FromArgb(90, 96, 110);
        private readonly ListBox _instrumentListBox;
        private readonly TextBox _instrumentInput;
        private readonly TextBox _positionCostInput;
        private readonly Button _removeInstrumentButton;
        private readonly NumericUpDown _refreshIntervalInput;
        private readonly NumericUpDown _detailsTransparencyInput;
        private readonly ComboBox _timeZoneComboBox;
        private readonly ComboBox _candlePeriodComboBox;
        private readonly ComboBox _timeRangeComboBox;
        private readonly Label _chartConfigHint;
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
            ClientSize = new Size(500, 720);
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

            GroupBox instrumentGroup = CreateGroup("行情标的", new Point(18, 12), new Size(464, 210));
            _instrumentListBox = new ListBox
            {
                Location = new Point(16, 24),
                Size = new Size(300, 87),
                IntegralHeight = false,
                HorizontalScrollbar = true
            };
            _instrumentListBox.SelectedIndexChanged += delegate
            {
                _removeInstrumentButton.Enabled = _instrumentListBox.SelectedIndex >= default(int);
                LoadSelectedInstrument();
            };
            instrumentGroup.Controls.Add(new Label { AutoSize = true, Location = new Point(16, 119), Text = "合约代码" });
            _instrumentInput = new TextBox
            {
                Location = new Point(16, 140),
                Size = new Size(200, 25),
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
            instrumentGroup.Controls.Add(new Label
            {
                AutoSize = true,
                Location = new Point(226, 119),
                Text = "持仓成本（选填）"
            });
            _positionCostInput = new TextBox
            {
                Location = new Point(226, 140),
                Size = new Size(90, 25),
                MaxLength = 40,
                TextAlign = HorizontalAlignment.Right
            };
            _positionCostInput.TextChanged += delegate
            {
                _errorProvider.SetError(_positionCostInput, string.Empty);
            };
            _positionCostInput.KeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.KeyCode == Keys.Enter)
                {
                    AddInstrument();
                    args.SuppressKeyPress = true;
                }
            };
            Button addInstrumentButton = CreateSecondaryButton("添加/更新", new Point(326, 138), new Size(120, 28));
            addInstrumentButton.Click += delegate { AddInstrument(); };
            _removeInstrumentButton = CreateSecondaryButton("移除选中", new Point(326, 24), new Size(120, 28));
            _removeInstrumentButton.Enabled = false;
            _removeInstrumentButton.Click += delegate { RemoveSelectedInstrument(); };
            instrumentGroup.Controls.Add(_instrumentListBox);
            instrumentGroup.Controls.Add(_instrumentInput);
            instrumentGroup.Controls.Add(_positionCostInput);
            instrumentGroup.Controls.Add(addInstrumentButton);
            instrumentGroup.Controls.Add(_removeInstrumentButton);
            instrumentGroup.Controls.Add(CreateHint(
                "选择已有标的可修改成本；成本留空则不展示盈亏。最多 " + SettingsStore.MaximumInstrumentCount + " 个",
                new Point(16, 174),
                new Size(430, 24)));

            GroupBox refreshGroup = CreateGroup("刷新频率", new Point(18, 230), new Size(464, 78));
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

            GroupBox timeZoneGroup = CreateGroup("时间显示", new Point(18, 316), new Size(464, 78));
            timeZoneGroup.Controls.Add(new Label { AutoSize = true, Location = new Point(16, 28), Text = "时区" });
            _timeZoneComboBox = new ComboBox
            {
                Location = new Point(60, 23),
                Size = new Size(190, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _timeZoneComboBox.Items.AddRange(new object[]
            {
                "北京时间（UTC+8）",
                "美东时间（自动夏令时）"
            });
            timeZoneGroup.Controls.Add(_timeZoneComboBox);
            timeZoneGroup.Controls.Add(CreateHint(
                "同步应用到底部时钟、K 线时间轴和区间统计",
                new Point(266, 27),
                new Size(180, 36)));

            GroupBox chartGroup = CreateGroup("K 线显示", new Point(18, 402), new Size(464, 146));
            chartGroup.Controls.Add(new Label { AutoSize = true, Location = new Point(16, 28), Text = "周期" });
            _candlePeriodComboBox = new ComboBox
            {
                Location = new Point(60, 23),
                Size = new Size(142, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (CandlePeriodDefinition period in CandlePeriodDefinition.All)
            {
                _candlePeriodComboBox.Items.Add(period);
            }

            chartGroup.Controls.Add(_candlePeriodComboBox);
            chartGroup.Controls.Add(new Label { AutoSize = true, Location = new Point(226, 28), Text = "范围" });
            _timeRangeComboBox = new ComboBox
            {
                Location = new Point(270, 23),
                Size = new Size(176, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (RangeDefinition range in RangeDefinition.All)
            {
                _timeRangeComboBox.Items.Add(range);
            }

            chartGroup.Controls.Add(_timeRangeComboBox);
            _chartConfigHint = CreateHint(string.Empty, new Point(16, 57), new Size(430, 40));
            chartGroup.Controls.Add(_chartConfigHint);
            chartGroup.Controls.Add(new Label
            {
                AutoSize = true, Location = new Point(16, 112), Text = "详情背景透明度"
            });
            _detailsTransparencyInput = new NumericUpDown
            {
                Location = new Point(128, 107), Size = new Size(65, 25),
                Minimum = SettingsStore.MinimumCandleDetailsTransparencyPercent,
                Maximum = SettingsStore.MaximumCandleDetailsTransparencyPercent,
                TextAlign = HorizontalAlignment.Right
            };
            chartGroup.Controls.Add(_detailsTransparencyInput);
            chartGroup.Controls.Add(CreateHint(
                "%（越大越透明，文字保持清晰）", new Point(201, 112), new Size(245, 23)));
            _candlePeriodComboBox.SelectedIndexChanged += delegate
            {
                _errorProvider.SetError(_candlePeriodComboBox, string.Empty);
                UpdateChartConfigHint();
            };
            _timeRangeComboBox.SelectedIndexChanged += delegate
            {
                _errorProvider.SetError(_candlePeriodComboBox, string.Empty);
                UpdateChartConfigHint();
            };

            GroupBox movingAverageGroup = CreateGroup("移动平均线", new Point(18, 556), new Size(464, 84));
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

            GroupBox taskbarTickerGroup = CreateGroup("最小化行为", new Point(18, 648), new Size(464, 154));
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
            content.Controls.Add(timeZoneGroup);
            content.Controls.Add(chartGroup);
            content.Controls.Add(movingAverageGroup);
            content.Controls.Add(taskbarTickerGroup);

            Controls.Add(content);
            Controls.Add(footer);
            Controls.Add(header);

            _errorProvider.SetIconAlignment(_instrumentInput, ErrorIconAlignment.MiddleRight);
            _errorProvider.SetIconAlignment(_positionCostInput, ErrorIconAlignment.MiddleRight);
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
            _detailsTransparencyInput.Value = Math.Max(
                SettingsStore.MinimumCandleDetailsTransparencyPercent,
                Math.Min(SettingsStore.MaximumCandleDetailsTransparencyPercent,
                    settings.candleDetailsTransparencyPercent ?? SettingsStore.DefaultCandleDetailsTransparencyPercent));
            _instrumentListBox.Items.Clear();
            string[] instrumentIds = settings.instrumentIds != null && settings.instrumentIds.Length > default(int)
                ? settings.instrumentIds
                : new[] { settings.instrumentId };
            foreach (string instrumentId in instrumentIds)
            {
                if (!string.IsNullOrWhiteSpace(instrumentId))
                {
                    decimal positionCost = decimal.Zero;
                    bool hasPositionCost = settings.positionCosts != null
                        && settings.positionCosts.TryGetValue(instrumentId, out positionCost);
                    _instrumentListBox.Items.Add(new InstrumentSettingItem(
                        instrumentId,
                        hasPositionCost ? (decimal?)positionCost : null));
                }
            }

            _instrumentListBox.ClearSelected();
            _instrumentInput.Clear();
            _positionCostInput.Clear();
            decimal refreshInterval = Math.Max(
                SettingsStore.MinimumRefreshIntervalMilliseconds,
                Math.Min(SettingsStore.MaximumRefreshIntervalMilliseconds, settings.refreshIntervalMilliseconds));
            _refreshIntervalInput.Value = refreshInterval;
            _timeZoneComboBox.SelectedIndex = settings.SelectedTimeZone == DisplayTimeZone.UsEastern
                ? UsEasternTimeZoneIndex
                : BeijingTimeZoneIndex;
            SelectCandlePeriod(settings.candlePeriod);
            SelectTimeRange(settings.timeRange);
            UpdateChartConfigHint();
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

        private void SelectCandlePeriod(string key)
        {
            for (int index = default(int); index < _candlePeriodComboBox.Items.Count; index++)
            {
                CandlePeriodDefinition period = _candlePeriodComboBox.Items[index] as CandlePeriodDefinition;
                if (period != null && string.Equals(period.Key, key, StringComparison.Ordinal))
                {
                    _candlePeriodComboBox.SelectedIndex = index;
                    return;
                }
            }

            _candlePeriodComboBox.SelectedIndex = DefaultComboBoxIndex;
        }

        private void SelectTimeRange(string key)
        {
            for (int index = default(int); index < _timeRangeComboBox.Items.Count; index++)
            {
                RangeDefinition range = _timeRangeComboBox.Items[index] as RangeDefinition;
                if (range != null && string.Equals(range.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    _timeRangeComboBox.SelectedIndex = index;
                    return;
                }
            }

            _timeRangeComboBox.SelectedIndex = DefaultComboBoxIndex;
        }

        private void UpdateChartConfigHint()
        {
            CandlePeriodDefinition period = _candlePeriodComboBox.SelectedItem as CandlePeriodDefinition;
            RangeDefinition selectedRange = _timeRangeComboBox.SelectedItem as RangeDefinition;
            if (period == null || selectedRange == null)
            {
                return;
            }

            RangeDefinition configuredRange;
            string error;
            if (!RangeDefinition.TryCreate(selectedRange.Key, period.Key, out configuredRange, out error))
            {
                _chartConfigHint.ForeColor = ErrorColor;
                _chartConfigHint.Text = error;
                return;
            }

            _chartConfigHint.ForeColor = SecondaryTextColor;
            _chartConfigHint.Text = configuredRange.IsAllHistory
                ? "按当前周期尽可能回溯，最多加载 " + configuredRange.MaximumPoints + " 根 " + configuredRange.PeriodLabel
                    + "，实际范围受上市时间和接口历史限制"
                : "预计加载 " + configuredRange.MaximumPoints + " 根 " + configuredRange.PeriodLabel
                    + "，上限 " + RangeDefinition.MaximumConfigurablePoints + " 根";
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
            if ((!string.IsNullOrWhiteSpace(_instrumentInput.Text)
                || !string.IsNullOrWhiteSpace(_positionCostInput.Text))
                && !AddInstrument())
            {
                return;
            }

            List<string> instrumentIds = new List<string>();
            Dictionary<string, decimal> positionCosts = new Dictionary<string, decimal>(
                StringComparer.OrdinalIgnoreCase);
            foreach (object item in _instrumentListBox.Items)
            {
                InstrumentSettingItem instrument = item as InstrumentSettingItem;
                if (instrument == null)
                {
                    continue;
                }

                instrumentIds.Add(instrument.InstrumentId);
                if (instrument.PositionCost.HasValue)
                {
                    positionCosts[instrument.InstrumentId] = instrument.PositionCost.Value;
                }
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
                positionCosts = positionCosts,
                refreshIntervalMilliseconds = decimal.ToInt32(_refreshIntervalInput.Value),
                candlePeriod = GetSelectedCandlePeriodKey(),
                timeRange = GetSelectedTimeRangeKey(),
                timeZone = GetSelectedTimeZone(),
                candleDetailsTransparencyPercent = decimal.ToInt32(_detailsTransparencyInput.Value),
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
                RangeDefinition configuredRange;
                string chartError;
                if (!RangeDefinition.TryCreate(
                    candidate.timeRange,
                    candidate.candlePeriod,
                    out configuredRange,
                    out chartError))
                {
                    _chartConfigHint.ForeColor = ErrorColor;
                    _chartConfigHint.Text = chartError;
                    _errorProvider.SetError(_candlePeriodComboBox, chartError);
                    _candlePeriodComboBox.Focus();
                }
                else
                {
                    _errorProvider.SetError(_instrumentInput, error);
                    _instrumentInput.Focus();
                }

                return;
            }

            Settings = normalizedSettings;
            DialogResult = DialogResult.OK;
            Close();
        }

        private string GetSelectedCandlePeriodKey()
        {
            CandlePeriodDefinition period = _candlePeriodComboBox.SelectedItem as CandlePeriodDefinition;
            return period == null ? null : period.Key;
        }

        private string GetSelectedTimeRangeKey()
        {
            RangeDefinition range = _timeRangeComboBox.SelectedItem as RangeDefinition;
            return range == null ? null : range.Key;
        }

        private string GetSelectedTimeZone()
        {
            return _timeZoneComboBox.SelectedIndex == UsEasternTimeZoneIndex
                ? SettingsStore.UsEasternTimeZone
                : SettingsStore.BeijingTimeZone;
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

            decimal? positionCost;
            if (!TryReadPositionCost(out positionCost))
            {
                return false;
            }

            for (int index = default(int); index < _instrumentListBox.Items.Count; index++)
            {
                InstrumentSettingItem existingInstrument = _instrumentListBox.Items[index] as InstrumentSettingItem;
                if (string.Equals(
                    existingInstrument == null ? null : existingInstrument.InstrumentId,
                    normalizedInstrumentId,
                    StringComparison.OrdinalIgnoreCase))
                {
                    _instrumentListBox.Items[index] = new InstrumentSettingItem(normalizedInstrumentId, positionCost);
                    ClearInstrumentEditor();
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

            _instrumentListBox.Items.Add(new InstrumentSettingItem(normalizedInstrumentId, positionCost));
            ClearInstrumentEditor();
            _instrumentInput.Focus();
            return true;
        }

        private bool TryReadPositionCost(out decimal? positionCost)
        {
            positionCost = null;
            string text = _positionCostInput.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            decimal parsedPositionCost;
            bool parsed = decimal.TryParse(
                text,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out parsedPositionCost)
                || decimal.TryParse(
                    text,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out parsedPositionCost);
            if (!parsed || parsedPositionCost <= decimal.Zero)
            {
                _errorProvider.SetError(_positionCostInput, "持仓成本必须是大于 0 的数字。");
                _positionCostInput.Focus();
                _positionCostInput.SelectAll();
                return false;
            }

            positionCost = parsedPositionCost;
            return true;
        }

        private void LoadSelectedInstrument()
        {
            InstrumentSettingItem selectedInstrument = _instrumentListBox.SelectedItem as InstrumentSettingItem;
            if (selectedInstrument == null)
            {
                return;
            }

            _instrumentInput.Text = selectedInstrument.InstrumentId;
            _positionCostInput.Text = selectedInstrument.PositionCost.HasValue
                ? selectedInstrument.PositionCost.Value.ToString(PositionCostFormat, CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private void ClearInstrumentEditor()
        {
            _instrumentListBox.ClearSelected();
            _instrumentInput.Clear();
            _positionCostInput.Clear();
        }

        private void RemoveSelectedInstrument()
        {
            int selectedIndex = _instrumentListBox.SelectedIndex;
            if (selectedIndex < default(int))
            {
                return;
            }

            _instrumentListBox.Items.RemoveAt(selectedIndex);
            ClearInstrumentEditor();
            _instrumentInput.Focus();
        }

        private sealed class InstrumentSettingItem
        {
            internal InstrumentSettingItem(string instrumentId, decimal? positionCost)
            {
                InstrumentId = instrumentId;
                PositionCost = positionCost;
            }

            internal string InstrumentId { get; private set; }
            internal decimal? PositionCost { get; private set; }

            public override string ToString()
            {
                return PositionCost.HasValue
                    ? InstrumentId + "    持仓成本 "
                        + PositionCost.Value.ToString(PositionCostFormat, CultureInfo.InvariantCulture)
                    : InstrumentId;
            }
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
