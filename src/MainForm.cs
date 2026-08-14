using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StockPerpTicker
{
    internal sealed class MainForm : Form
    {
        private const int MissingCandleIndex = -1;
        private const string ChartConfigurationErrorTitle = "K 线配置不可用";
        private const int DefaultRenderIntervalMilliseconds = 1000;
        private const int UnspecifiedWindowCoordinate = -1;
        private const int MinimumVisibleWindowOverlap = 100;
        private static readonly Color UpColor = Color.FromArgb(8, 153, 129);
        private static readonly Color DownColor = Color.FromArgb(242, 54, 69);
        private static readonly Color SecondaryTextColor = Color.FromArgb(90, 96, 110);
        private static readonly Candle[] EmptyCandles = new Candle[0];
        private AppSettings _settings;
        private readonly OkxMarketClient _marketClient;
        private readonly CancellationTokenSource _applicationCancellation;
        private readonly List<Candle> _candles;
        private readonly Dictionary<string, Button> _instrumentButtons;
        private readonly Dictionary<string, InstrumentInfo> _instrumentInfos;
        private readonly Dictionary<string, List<Candle>> _miniTickerCandles;
        private readonly ChartControl _chart;
        private readonly Panel _bottomBar;
        private readonly FlowLayoutPanel _instrumentStrip;
        private TaskbarTickerForm _taskbarTicker;
        private readonly Label _symbolLabel;
        private readonly Label _priceLabel;
        private readonly Label _changeLabel;
        private readonly Label _statusLabel;
        private readonly Label _clockLabel;
        private readonly ComboBox _rangeComboBox;
        private readonly ComboBox _periodComboBox;
        private readonly Button _pinButton;
        private readonly Button _settingsButton;
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _topMostMenu;
        private readonly ToolStripMenuItem _autoStartMenu;
        private readonly System.Windows.Forms.Timer _clockTimer;
        private readonly System.Windows.Forms.Timer _tickerTimer;
        private readonly System.Windows.Forms.Timer _fallbackTimer;
        private readonly System.Windows.Forms.Timer _memoryTimer;
        private readonly System.Windows.Forms.Timer _renderTimer;
        private readonly System.Windows.Forms.Timer _miniTickerRotationTimer;
        private readonly Font _rangeFontRegular;
        private readonly Font _rangeFontBold;
        private readonly object _pendingCandleSync;
        private CancellationTokenSource _streamCancellation;
        private CancellationTokenSource _rangeLoadCancellation;
        private Task _streamTask;
        private InstrumentInfo _instrument;
        private MarketSnapshot _snapshot;
        private RangeDefinition _currentRange;
        private ConnectionStatus _connectionStatus;
        private string _connectionMessage;
        private bool _allowExit;
        private bool _fallbackBusy;
        private bool _minimizePending;
        private bool _restorePending;
        private bool _windowRestoreInProgress;
        private bool _normalBoundsCapturePending;
        private bool _miniTickerBusy;
        private bool _rangeSelectionReady;
        private bool _periodSelectionReady;
        private Rectangle _lastNormalBounds;
        private FormWindowState _windowStateBeforeMinimize;
        private int _rangeGeneration;
        private int _settingsGeneration;
        private int _miniTickerIndex;
        private string _selectedInstrumentId;
        private Candle _pendingCandle;
        private Icon _appIcon;

        internal MainForm(AppSettings settings, WindowState initialState)
        {
            _settings = SettingsStore.Clone(settings);
            _marketClient = new OkxMarketClient();
            _applicationCancellation = new CancellationTokenSource();
            _candles = new List<Candle>();
            _instrumentButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
            _instrumentInfos = new Dictionary<string, InstrumentInfo>(StringComparer.OrdinalIgnoreCase);
            _miniTickerCandles = new Dictionary<string, List<Candle>>(StringComparer.OrdinalIgnoreCase);
            _pendingCandleSync = new object();
            _currentRange = RangeDefinition.Create(_settings.timeRange, _settings.candlePeriod);
            _settings.timeRange = _currentRange.Key;
            _settings.candlePeriod = _currentRange.SelectedPeriodKey;
            _selectedInstrumentId = ResolveInitialInstrumentId(initialState.InstrumentId, _settings.instrumentIds);
            _rangeFontRegular = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            _rangeFontBold = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold, GraphicsUnit.Point);

            Text = "StockPerpTicker";
            BackColor = Color.White;
            MinimumSize = new Size(420, 280);
            Size = new Size(initialState.Width, initialState.Height);
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            _appIcon = AppIconFactory.Create();
            Icon = _appIcon;
            _taskbarTicker = _settings.showTaskbarTickerOnMinimize
                ? CreateTaskbarTicker()
                : null;

            RestoreWindowBounds(initialState);
            _lastNormalBounds = Bounds;
            _windowStateBeforeMinimize = FormWindowState.Normal;
            TopMost = initialState.TopMost;

            Panel topBar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.White, Padding = new Padding(10, 6, 8, 4) };
            _symbolLabel = new Label
            {
                AutoSize = false,
                Location = new Point(10, 5),
                Size = new Size(170, 22),
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold, GraphicsUnit.Point),
                Text = _selectedInstrumentId,
                TextAlign = ContentAlignment.MiddleLeft
            };
            _priceLabel = new Label
            {
                AutoSize = false,
                Location = new Point(184, 5),
                Size = new Size(90, 22),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Point),
                Text = "--",
                TextAlign = ContentAlignment.MiddleRight
            };
            _changeLabel = new Label
            {
                AutoSize = false,
                Location = new Point(278, 5),
                Size = new Size(70, 22),
                Text = "--",
                TextAlign = ContentAlignment.MiddleLeft
            };
            _statusLabel = new Label
            {
                AutoSize = false,
                Location = new Point(10, 26),
                Size = new Size(310, 17),
                ForeColor = SecondaryTextColor,
                Font = new Font("Microsoft YaHei UI", 8f, FontStyle.Regular, GraphicsUnit.Point),
                Text = "● 正在启动"
            };
            _pinButton = new Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(Width - 63, 7),
                Size = new Size(42, 28),
                Text = "置顶",
                ForeColor = TopMost ? UpColor : SecondaryTextColor,
                TabStop = false
            };
            _pinButton.FlatAppearance.BorderColor = Color.FromArgb(224, 227, 235);
            _pinButton.Click += delegate { ToggleTopMost(); };
            _settingsButton = new Button
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(Width - 111, 7),
                Size = new Size(42, 28),
                Text = "设置",
                ForeColor = SecondaryTextColor,
                TabStop = false
            };
            _settingsButton.FlatAppearance.BorderColor = Color.FromArgb(224, 227, 235);
            _settingsButton.Click += async delegate { await ShowSettingsAsync(); };
            topBar.Controls.Add(_symbolLabel);
            topBar.Controls.Add(_priceLabel);
            topBar.Controls.Add(_changeLabel);
            topBar.Controls.Add(_statusLabel);
            topBar.Controls.Add(_pinButton);
            topBar.Controls.Add(_settingsButton);
            topBar.Resize += delegate
            {
                LayoutTopBar(topBar);
                _pinButton.BringToFront();
                _settingsButton.BringToFront();
            };
            LayoutTopBar(topBar);

            _chart = new ChartControl { Dock = DockStyle.Fill };
            _bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                BackColor = Color.White
            };
            Panel rangeBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = Color.White
            };
            _instrumentStrip = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 32,
                AutoScroll = true,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8, 0, 8, 0),
                BackColor = Color.FromArgb(248, 250, 252),
                Visible = false
            };

            rangeBar.Controls.Add(new Label
            {
                AutoSize = true,
                Location = new Point(8, 14),
                Text = "范围"
            });
            _rangeComboBox = new ComboBox
            {
                Location = new Point(42, 9),
                Size = new Size(80, 25),
                DropDownWidth = 104,
                DropDownStyle = ComboBoxStyle.DropDownList,
                TabStop = false
            };
            foreach (RangeDefinition range in RangeDefinition.All)
            {
                _rangeComboBox.Items.Add(range);
            }

            SelectTimeRange(_currentRange.Key);
            _rangeComboBox.SelectedIndexChanged += async delegate { await ChangeTimeRangeFromSelectionAsync(); };
            _rangeSelectionReady = true;
            rangeBar.Controls.Add(_rangeComboBox);

            rangeBar.Controls.Add(new Label
            {
                AutoSize = true,
                Location = new Point(128, 14),
                Text = "周期"
            });
            _periodComboBox = new ComboBox
            {
                Location = new Point(162, 9),
                Size = new Size(88, 25),
                DropDownWidth = 120,
                DropDownStyle = ComboBoxStyle.DropDownList,
                TabStop = false
            };
            foreach (CandlePeriodDefinition period in CandlePeriodDefinition.All)
            {
                _periodComboBox.Items.Add(period);
            }

            SelectPeriod(_currentRange.SelectedPeriodKey);
            _periodComboBox.SelectedIndexChanged += async delegate { await ChangeCandlePeriodAsync(); };
            _periodSelectionReady = true;
            rangeBar.Controls.Add(_periodComboBox);

            _clockLabel = new Label
            {
                Dock = DockStyle.Right,
                Width = 122,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 8, 0),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point)
            };
            rangeBar.Controls.Add(_clockLabel);
            _bottomBar.Controls.Add(_instrumentStrip);
            _bottomBar.Controls.Add(rangeBar);
            RebuildInstrumentButtons();

            Controls.Add(_chart);
            Controls.Add(_bottomBar);
            Controls.Add(topBar);

            ContextMenuStrip trayMenu = new ContextMenuStrip();
            ToolStripMenuItem showMenu = new ToolStripMenuItem("显示/隐藏");
            showMenu.Click += delegate { ToggleVisible(); };
            _topMostMenu = new ToolStripMenuItem("窗口置顶") { Checked = TopMost, CheckOnClick = false };
            _topMostMenu.Click += delegate { ToggleTopMost(); };
            _autoStartMenu = new ToolStripMenuItem("开机启动") { CheckOnClick = false };
            _autoStartMenu.Click += delegate { ToggleAutoStart(); };
            ToolStripMenuItem exitMenu = new ToolStripMenuItem("彻底退出");
            exitMenu.Click += delegate { ExitApplication(); };
            trayMenu.Items.Add(showMenu);
            trayMenu.Items.Add(_topMostMenu);
            trayMenu.Items.Add(_autoStartMenu);
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(exitMenu);

            _notifyIcon = new NotifyIcon
            {
                Icon = _appIcon,
                Text = "StockPerpTicker",
                ContextMenuStrip = trayMenu,
                Visible = true
            };
            _notifyIcon.DoubleClick += delegate { ShowFromTray(); };

            _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _clockTimer.Tick += delegate { UpdateClock(); };
            _clockTimer.Start();
            UpdateClock();

            _tickerTimer = new System.Windows.Forms.Timer { Interval = 60000 };
            _tickerTimer.Tick += async delegate { await RefreshTickerAsync(); };
            _fallbackTimer = new System.Windows.Forms.Timer { Interval = 10000 };
            _fallbackTimer.Tick += async delegate { await RefreshFallbackAsync(); };
            _renderTimer = new System.Windows.Forms.Timer
            {
                Interval = settings.refreshIntervalMilliseconds > default(int)
                    ? settings.refreshIntervalMilliseconds
                    : DefaultRenderIntervalMilliseconds
            };
            _renderTimer.Tick += delegate { ApplyPendingRealtimeCandle(); };
            _renderTimer.Start();
            _miniTickerRotationTimer = new System.Windows.Forms.Timer
            {
                Interval = _settings.taskbarTickerRotationIntervalSeconds * 1000
            };
            _miniTickerRotationTimer.Tick += async delegate { await RotateMiniTickerAsync(); };
            _memoryTimer = new System.Windows.Forms.Timer { Interval = 10000 };
            _memoryTimer.Tick += delegate
            {
                MemoryManager.TrimWorkingSet();
                _memoryTimer.Interval = 300000;
            };
            _memoryTimer.Start();

            try
            {
                _autoStartMenu.Checked = AutoStartManager.IsEnabled();
            }
            catch (Exception ex)
            {
                Logger.Error("读取开机启动状态失败", ex);
            }

            Shown += async delegate { await InitializeMarketAsync(); };
            Resize += HandleMainResize;
            LocationChanged += QueueNormalWindowBoundsCapture;
            SizeChanged += QueueNormalWindowBoundsCapture;
            FormClosing += HandleFormClosing;
            FormClosed += HandleFormClosed;
        }

        private async Task ChangeTimeRangeFromSelectionAsync()
        {
            if (!_rangeSelectionReady)
            {
                return;
            }

            RangeDefinition selectedRange = _rangeComboBox.SelectedItem as RangeDefinition;
            if (selectedRange == null
                || string.Equals(selectedRange.Key, _currentRange.Key, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await ChangeTimeRangeAsync(selectedRange.Key);
        }

        private async Task ChangeTimeRangeAsync(string rangeKey)
        {
            RangeDefinition range;
            string error;
            if (!RangeDefinition.TryCreate(rangeKey, _currentRange.SelectedPeriodKey, out range, out error))
            {
                SelectTimeRange(_currentRange.Key);
                MessageBox.Show(
                    this,
                    error,
                    ChartConfigurationErrorTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (_instrument == null)
            {
                _currentRange = range;
                _settings.timeRange = range.Key;
                return;
            }

            await ChangeRangeAsync(range, true);
        }

        private async Task ChangeCandlePeriodAsync()
        {
            if (!_periodSelectionReady)
            {
                return;
            }

            CandlePeriodDefinition period = _periodComboBox.SelectedItem as CandlePeriodDefinition;
            if (period == null
                || string.Equals(period.Key, _currentRange.SelectedPeriodKey, StringComparison.Ordinal))
            {
                return;
            }

            RangeDefinition range;
            string error;
            if (!RangeDefinition.TryCreate(_currentRange.Key, period.Key, out range, out error))
            {
                SelectPeriod(_currentRange.SelectedPeriodKey);
                MessageBox.Show(
                    this,
                    error,
                    ChartConfigurationErrorTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (_instrument == null)
            {
                _currentRange = range;
                _settings.candlePeriod = range.SelectedPeriodKey;
                return;
            }

            await ChangeRangeAsync(range, true);
        }

        private void SelectTimeRange(string rangeKey)
        {
            bool selectionWasReady = _rangeSelectionReady;
            _rangeSelectionReady = false;
            try
            {
                for (int index = default(int); index < _rangeComboBox.Items.Count; index++)
                {
                    RangeDefinition range = _rangeComboBox.Items[index] as RangeDefinition;
                    if (range != null && string.Equals(range.Key, rangeKey, StringComparison.OrdinalIgnoreCase))
                    {
                        _rangeComboBox.SelectedIndex = index;
                        return;
                    }
                }

                _rangeComboBox.SelectedIndex = default(int);
            }
            finally
            {
                _rangeSelectionReady = selectionWasReady;
            }
        }

        private void SelectPeriod(string periodKey)
        {
            bool selectionWasReady = _periodSelectionReady;
            _periodSelectionReady = false;
            try
            {
                for (int index = default(int); index < _periodComboBox.Items.Count; index++)
                {
                    CandlePeriodDefinition period = _periodComboBox.Items[index] as CandlePeriodDefinition;
                    if (period != null && string.Equals(period.Key, periodKey, StringComparison.Ordinal))
                    {
                        _periodComboBox.SelectedIndex = index;
                        return;
                    }
                }

                _periodComboBox.SelectedIndex = default(int);
            }
            finally
            {
                _periodSelectionReady = selectionWasReady;
            }
        }

        private void LayoutTopBar(Panel topBar)
        {
            _settingsButton.Left = Math.Max(0, topBar.ClientSize.Width - _settingsButton.Width - 8);
            _pinButton.Left = Math.Max(0, _settingsButton.Left - _pinButton.Width - 6);
            int contentRight = Math.Max(210, _pinButton.Left - 4);
            _symbolLabel.Width = Math.Max(108, Math.Min(170, contentRight - 188));
            _priceLabel.Left = _symbolLabel.Right + 4;
            _changeLabel.Left = _priceLabel.Right + 4;
            _changeLabel.Width = Math.Max(54, contentRight - _changeLabel.Left);
            _statusLabel.Width = Math.Max(140, contentRight - _statusLabel.Left);
        }

        private static string ResolveInitialInstrumentId(string savedInstrumentId, string[] configuredInstrumentIds)
        {
            if (!string.IsNullOrWhiteSpace(savedInstrumentId)
                && configuredInstrumentIds.Any(item => string.Equals(item, savedInstrumentId, StringComparison.OrdinalIgnoreCase)))
            {
                return savedInstrumentId;
            }

            return configuredInstrumentIds[0];
        }

        private void RebuildInstrumentButtons()
        {
            foreach (Button existingButton in _instrumentButtons.Values)
            {
                existingButton.Dispose();
            }

            _instrumentButtons.Clear();
            _instrumentStrip.Controls.Clear();
            foreach (string instrumentId in _settings.instrumentIds)
            {
                string capturedInstrumentId = instrumentId;
                string displayName = CompactInstrumentId(instrumentId);
                int buttonWidth = Math.Max(48, TextRenderer.MeasureText(displayName, _rangeFontBold).Width + 20);
                Button button = new Button
                {
                    FlatStyle = FlatStyle.Flat,
                    Margin = new Padding(0, 2, 8, 2),
                    Padding = new Padding(4, 0, 4, 0),
                    Size = new Size(buttonWidth, 27),
                    Font = _rangeFontRegular,
                    Text = displayName,
                    TabStop = false,
                    UseVisualStyleBackColor = false
                };
                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = Color.White;
                button.Click += async delegate { await HandleInstrumentSelectionChangedAsync(capturedInstrumentId); };
                _instrumentButtons[instrumentId] = button;
                _instrumentStrip.Controls.Add(button);
            }

            bool showInstrumentStrip = _settings.instrumentIds.Length > 1;
            _instrumentStrip.Visible = showInstrumentStrip;
            _bottomBar.Height = showInstrumentStrip ? 74 : 42;
            UpdateInstrumentSelection();
        }

        private void UpdateInstrumentSelection()
        {
            foreach (KeyValuePair<string, Button> entry in _instrumentButtons)
            {
                bool selected = string.Equals(entry.Key, _selectedInstrumentId, StringComparison.OrdinalIgnoreCase);
                entry.Value.Font = selected ? _rangeFontBold : _rangeFontRegular;
                entry.Value.ForeColor = selected ? UpColor : SecondaryTextColor;
                entry.Value.BackColor = selected ? Color.White : _instrumentStrip.BackColor;
                entry.Value.FlatAppearance.BorderSize = selected ? 1 : 0;
                entry.Value.FlatAppearance.BorderColor = Color.FromArgb(210, 218, 226);
            }
        }

        private static string CompactInstrumentId(string instrumentId)
        {
            int separator = instrumentId.IndexOf('-');
            return separator > default(int) ? instrumentId.Substring(0, separator) : instrumentId;
        }

        private async Task HandleInstrumentSelectionChangedAsync(string instrumentId)
        {
            if (string.Equals(instrumentId, _selectedInstrumentId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _instrumentStrip.Enabled = false;
            string previousInstrumentId = _selectedInstrumentId;
            try
            {
                ++_settingsGeneration;
                _selectedInstrumentId = instrumentId;
                UpdateInstrumentSelection();
                await ActivateInstrumentAsync(instrumentId, true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _selectedInstrumentId = previousInstrumentId;
                Logger.Error("切换行情标的失败：" + instrumentId, ex);
                MessageBox.Show(ex.Message, "无法切换行情标的", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (!IsDisposed)
                {
                    _instrumentStrip.Enabled = true;
                    UpdateInstrumentSelection();
                }
            }
        }

        private async Task InitializeMarketAsync()
        {
            int settingsGeneration = _settingsGeneration;
            try
            {
                SetConnectionStatus(ConnectionStatus.Loading, "正在校验合约");
                InstrumentInfo instrument = await _marketClient.ValidateInstrumentAsync(
                    _selectedInstrumentId,
                    _applicationCancellation.Token);
                if (settingsGeneration != _settingsGeneration || _applicationCancellation.IsCancellationRequested)
                {
                    return;
                }

                _instrumentInfos[instrument.InstrumentId] = instrument;
                await ActivateInstrumentAsync(_selectedInstrumentId, false);
                Logger.Info("当前行情标的校验成功：" + instrument.InstrumentId + "。");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                SetConnectionStatus(ConnectionStatus.Error, "行情初始化失败");
                _chart.SetMessage(ex.Message + Environment.NewLine + "请点击右上角“设置”检查合约代码。", true);
                Logger.Error("行情初始化失败", ex);
            }
        }

        private async Task<Dictionary<string, InstrumentInfo>> ValidateInstrumentSetAsync(string[] instrumentIds)
        {
            Dictionary<string, InstrumentInfo> result = new Dictionary<string, InstrumentInfo>(StringComparer.OrdinalIgnoreCase);
            for (int index = default(int); index < instrumentIds.Length; index++)
            {
                string instrumentId = instrumentIds[index];
                InstrumentInfo existing;
                if (_instrumentInfos.TryGetValue(instrumentId, out existing))
                {
                    result[instrumentId] = existing;
                    continue;
                }

                SetConnectionStatus(
                    ConnectionStatus.Loading,
                    "正在校验合约 " + (index + 1) + "/" + instrumentIds.Length);
                try
                {
                    result[instrumentId] = await _marketClient.ValidateInstrumentAsync(
                        instrumentId,
                        _applicationCancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("校验 " + instrumentId + " 失败：" + ex.Message, ex);
                }
            }

            return result;
        }

        private void ReplaceInstrumentInfos(Dictionary<string, InstrumentInfo> instruments)
        {
            _instrumentInfos.Clear();
            foreach (KeyValuePair<string, InstrumentInfo> entry in instruments)
            {
                _instrumentInfos[entry.Key] = entry.Value;
            }

            List<string> removedCacheKeys = _miniTickerCandles.Keys
                .Where(key => !_instrumentInfos.ContainsKey(key))
                .ToList();
            foreach (string key in removedCacheKeys)
            {
                _miniTickerCandles.Remove(key);
            }
        }

        private async Task ActivateInstrumentAsync(string instrumentId, bool saveState)
        {
            InstrumentInfo instrument;
            if (!_instrumentInfos.TryGetValue(instrumentId, out instrument))
            {
                SetConnectionStatus(ConnectionStatus.Loading, "正在校验 " + instrumentId);
                instrument = await _marketClient.ValidateInstrumentAsync(
                    instrumentId,
                    _applicationCancellation.Token);
                _instrumentInfos[instrumentId] = instrument;
            }

            _tickerTimer.Stop();
            _fallbackTimer.Stop();
            StopRealtimeStream();
            _instrument = instrument;
            _selectedInstrumentId = instrument.InstrumentId;
            _snapshot = null;
            _candles.Clear();
            _symbolLabel.Text = instrument.InstrumentId;
            _priceLabel.Text = "--";
            _changeLabel.Text = "--";
            Text = instrument.InstrumentId + " - StockPerpTicker";
            _notifyIcon.Text = "StockPerpTicker";
            UpdateInstrumentSelection();
            await ChangeRangeAsync(_currentRange, false);
            _tickerTimer.Start();
            _fallbackTimer.Start();
            if (saveState)
            {
                SaveWindowState();
            }
        }

        private async Task ChangeRangeAsync(RangeDefinition range, bool persistConfiguration)
        {
            int generation = ++_rangeGeneration;
            CancelRangeLoad();
            CancellationTokenSource loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _applicationCancellation.Token);
            _rangeLoadCancellation = loadCancellation;
            CancellationToken loadToken = loadCancellation.Token;
            StopRealtimeStream();
            _chart.ResetViewport();
            lock (_pendingCandleSync)
            {
                _pendingCandle = null;
            }

            _currentRange = range;
            _settings.timeRange = range.Key;
            _settings.candlePeriod = range.SelectedPeriodKey;
            SelectTimeRange(range.Key);
            SelectPeriod(range.SelectedPeriodKey);
            if (persistConfiguration)
            {
                PersistChartConfiguration();
            }

            Logger.Info(
                "开始加载 K 线：" + _instrument.InstrumentId + " / " + range.Key + " / " + range.RestBar
                + " / 最多 " + range.MaximumPoints + " 根");
            SetConnectionStatus(ConnectionStatus.Loading, "正在加载 " + range.Label + " 数据");
            _chart.SetMessage("正在加载 " + range.Label + " K 线…", false);

            try
            {
                List<Candle> history = await _marketClient.FetchCandlesAsync(
                    _instrument.InstrumentId,
                    range,
                    _instrument.ListingTime,
                    loadToken);
                MarketSnapshot snapshot = await _marketClient.FetchTickerAsync(
                    _instrument.InstrumentId,
                    loadToken);
                if (generation != _rangeGeneration || _applicationCancellation.IsCancellationRequested)
                {
                    return;
                }

                _candles.Clear();
                _candles.AddRange(history);
                _snapshot = snapshot;
                RenderMarket();
                UpdateHeader();
                Logger.Info(
                    "K 线加载完成：" + _instrument.InstrumentId + " / " + range.Key + " / " + range.RestBar
                    + " / " + history.Count + " 根");
                StartRealtimeStream(range);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (generation != _rangeGeneration || _applicationCancellation.IsCancellationRequested)
                {
                    return;
                }

                SetConnectionStatus(ConnectionStatus.Error, "K 线加载失败");
                _chart.SetMessage(ex.Message, true);
                Logger.Error("加载 K 线失败：" + range.Key, ex);
                StartRealtimeStream(range);
            }
            finally
            {
                if (ReferenceEquals(_rangeLoadCancellation, loadCancellation))
                {
                    _rangeLoadCancellation = null;
                }

                loadCancellation.Dispose();
            }
        }

        private void PersistChartConfiguration()
        {
            try
            {
                SettingsStore.Save(_settings);
                SaveWindowState();
                Logger.Info(
                    "K 线配置已保存：范围 " + _settings.timeRange + " / 周期 " + _settings.candlePeriod);
            }
            catch (Exception ex)
            {
                Logger.Error("保存 K 线配置失败", ex);
            }
        }

        private void StartRealtimeStream(RangeDefinition range)
        {
            StopRealtimeStream();
            _streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(_applicationCancellation.Token);
            CancellationToken token = _streamCancellation.Token;
            int generation = _rangeGeneration;
            _streamTask = _marketClient.RunRealtimeLoopAsync(
                _instrument.InstrumentId,
                range,
                delegate(Candle candle)
                {
                    if (generation == _rangeGeneration)
                    {
                        HandleRealtimeCandle(candle);
                    }
                },
                HandleConnectionStatus,
                token);
            _streamTask.ContinueWith(
                task => Logger.Error("实时行情任务异常", task.Exception),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        private void StopRealtimeStream()
        {
            if (_streamCancellation != null)
            {
                _streamCancellation.Cancel();
                _streamCancellation.Dispose();
                _streamCancellation = null;
            }
        }

        private void HandleRealtimeCandle(Candle candle)
        {
            lock (_pendingCandleSync)
            {
                _pendingCandle = candle;
            }
        }

        private void ApplyPendingRealtimeCandle()
        {
            Candle candle;
            lock (_pendingCandleSync)
            {
                candle = _pendingCandle;
                _pendingCandle = null;
            }

            if (candle == null)
            {
                return;
            }

            MergeCandle(candle);
            if (_snapshot == null)
            {
                _snapshot = new MarketSnapshot();
            }

            _snapshot.LastPrice = candle.Close;
            _snapshot.UpdatedAt = DateTime.Now;
            RenderMarket();
            UpdateHeader();
        }

        private void HandleConnectionStatus(ConnectionStatus status, string message)
        {
            SafeBeginInvoke(delegate { SetConnectionStatus(status, message); });
        }

        private async Task RefreshTickerAsync()
        {
            if (_instrument == null || _applicationCancellation.IsCancellationRequested)
            {
                return;
            }

            try
            {
                int settingsGeneration = _settingsGeneration;
                InstrumentInfo instrument = _instrument;
                MarketSnapshot snapshot = await _marketClient.FetchTickerAsync(instrument.InstrumentId, _applicationCancellation.Token);
                if (settingsGeneration != _settingsGeneration)
                {
                    return;
                }

                _snapshot = snapshot;
                UpdateHeader();
                RenderMarket();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Error("刷新 24 小时行情失败", ex);
            }
        }

        private async Task RefreshFallbackAsync()
        {
            if (_fallbackBusy || _instrument == null || _connectionStatus == ConnectionStatus.Live || _applicationCancellation.IsCancellationRequested)
            {
                return;
            }

            _fallbackBusy = true;
            try
            {
                int settingsGeneration = _settingsGeneration;
                InstrumentInfo instrument = _instrument;
                Candle latest = await _marketClient.FetchLatestCandleAsync(
                    instrument.InstrumentId,
                    _currentRange,
                    _applicationCancellation.Token);
                if (settingsGeneration != _settingsGeneration)
                {
                    return;
                }

                if (latest != null)
                {
                    MergeCandle(latest);
                    if (_snapshot == null)
                    {
                        _snapshot = new MarketSnapshot();
                    }

                    _snapshot.LastPrice = latest.Close;
                    _snapshot.UpdatedAt = DateTime.Now;
                    RenderMarket();
                    UpdateHeader();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Error("REST 降级刷新失败", ex);
            }
            finally
            {
                _fallbackBusy = false;
            }
        }

        private void MergeCandle(Candle candle)
        {
            int candleIndex = FindCandleIndex(candle.Timestamp);
            if (candleIndex > MissingCandleIndex)
            {
                _candles[candleIndex] = candle;
            }
            else
            {
                int insertionIndex = ~candleIndex;
                if (insertionIndex == _candles.Count)
                {
                    _candles.Add(candle);
                }
                else
                {
                    _candles.Insert(insertionIndex, candle);
                }
            }

            int overflowCount = _candles.Count - _currentRange.MaximumPoints;
            if (overflowCount > default(int))
            {
                _candles.RemoveRange(default(int), overflowCount);
            }
        }

        private void CancelRangeLoad()
        {
            if (_rangeLoadCancellation == null)
            {
                return;
            }

            _rangeLoadCancellation.Cancel();
            _rangeLoadCancellation = null;
        }

        private int FindCandleIndex(long timestamp)
        {
            int low = default(int);
            int high = _candles.Count - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                long candidateTimestamp = _candles[middle].Timestamp;
                if (candidateTimestamp == timestamp)
                {
                    return middle;
                }

                if (candidateTimestamp < timestamp)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return ~low;
        }

        private void RenderMarket()
        {
            if (_instrument != null)
            {
                _chart.SetData(
                    _candles,
                    _snapshot,
                    _currentRange,
                    _instrument.TickSize,
                    _settings.movingAverages);
                if (_taskbarTicker != null)
                {
                    _taskbarTicker.UpdateMarket(
                        _instrument.InstrumentId,
                        _snapshot,
                        _candles,
                        _instrument.TickSize,
                        false);
                }
            }
        }

        private void UpdateHeader()
        {
            if (_snapshot == null || _instrument == null)
            {
                return;
            }

            _priceLabel.Text = FormatHelper.Price(_snapshot.LastPrice, _instrument.TickSize);
            decimal change = _snapshot.ChangePercent;
            Color color = change >= decimal.Zero ? UpColor : DownColor;
            _priceLabel.ForeColor = color;
            _changeLabel.ForeColor = color;
            _changeLabel.Text = (change >= decimal.Zero ? "+" : string.Empty) + change.ToString("0.00") + "%";
            _notifyIcon.Text = (_instrument.InstrumentId + "  " + _priceLabel.Text).Substring(0, Math.Min(63, _instrument.InstrumentId.Length + 2 + _priceLabel.Text.Length));
        }

        private void SetConnectionStatus(ConnectionStatus status, string message)
        {
            _connectionStatus = status;
            _connectionMessage = message;
            Color color;
            switch (status)
            {
                case ConnectionStatus.Live:
                    color = UpColor;
                    break;
                case ConnectionStatus.Error:
                case ConnectionStatus.Offline:
                    color = DownColor;
                    break;
                default:
                    color = Color.FromArgb(245, 158, 11);
                    break;
            }

            _statusLabel.ForeColor = color;
            string refreshText = _renderTimer.Interval % 1000 == default(int)
                ? (_renderTimer.Interval / 1000m).ToString("0.##") + "秒刷新"
                : _renderTimer.Interval + "毫秒刷新";
            _statusLabel.Text = "● " + message + " · " + _currentRange.PeriodLabel + " · " + refreshText;
        }

        private void UpdateClock()
        {
            DateTime now = DateTime.Now;
            TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(now);
            string sign = offset < TimeSpan.Zero ? "-" : "+";
            TimeSpan absolute = offset.Duration();
            _clockLabel.Text = now.ToString("HH:mm:ss") + " UTC" + sign + absolute.Hours.ToString();
        }

        private async Task ShowSettingsAsync()
        {
            AppSettings editableSettings = SettingsStore.Clone(_settings);
            while (!_applicationCancellation.IsCancellationRequested && !IsDisposed)
            {
                using (SettingsForm settingsForm = new SettingsForm(editableSettings))
                {
                    if (settingsForm.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }

                    editableSettings = settingsForm.Settings;
                }

                _settingsButton.Enabled = false;
                bool applied;
                try
                {
                    applied = await TryApplySettingsAsync(editableSettings);
                }
                finally
                {
                    if (!IsDisposed)
                    {
                        _settingsButton.Enabled = true;
                    }
                }

                if (applied)
                {
                    return;
                }
            }
        }

        private async Task<bool> TryApplySettingsAsync(AppSettings candidate)
        {
            ConnectionStatus previousStatus = _connectionStatus;
            string previousMessage = _connectionMessage;
            RangeDefinition targetRange;
            string chartConfigurationError;
            if (!RangeDefinition.TryCreate(
                candidate.timeRange,
                candidate.candlePeriod,
                out targetRange,
                out chartConfigurationError))
            {
                Logger.Info("已拒绝无效的 K 线配置：" + chartConfigurationError);
                MessageBox.Show(
                    this,
                    chartConfigurationError,
                    ChartConfigurationErrorTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return false;
            }

            bool chartConfigurationChanged = !string.Equals(
                _currentRange.Key,
                targetRange.Key,
                StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    _currentRange.SelectedPeriodKey,
                    targetRange.SelectedPeriodKey,
                    StringComparison.Ordinal);
            string currentInstrumentId = _instrument == null ? _selectedInstrumentId : _instrument.InstrumentId;
            string targetInstrumentId = candidate.instrumentIds.Any(
                item => string.Equals(item, currentInstrumentId, StringComparison.OrdinalIgnoreCase))
                ? currentInstrumentId
                : candidate.instrumentIds[0];
            bool instrumentChanged = _instrument == null
                || !string.Equals(_instrument.InstrumentId, targetInstrumentId, StringComparison.OrdinalIgnoreCase);
            try
            {
                Dictionary<string, InstrumentInfo> validatedInstruments = await ValidateInstrumentSetAsync(
                    candidate.instrumentIds);

                SettingsStore.Save(candidate);
                ++_settingsGeneration;
                _settings = SettingsStore.Clone(candidate);
                _currentRange = targetRange;
                SelectTimeRange(targetRange.Key);
                SelectPeriod(targetRange.SelectedPeriodKey);
                _selectedInstrumentId = targetInstrumentId;
                ReplaceInstrumentInfos(validatedInstruments);
                RebuildInstrumentButtons();
                _renderTimer.Interval = _settings.refreshIntervalMilliseconds;
                _miniTickerRotationTimer.Interval = _settings.taskbarTickerRotationIntervalSeconds * 1000;
                _miniTickerIndex = default(int);
                ConfigureTaskbarTicker();

                if (instrumentChanged)
                {
                    Logger.Info(
                        "设置已保存，正在切换合约：" + targetInstrumentId + " / " + targetRange.Key
                        + " / " + targetRange.RestBar);
                    await ActivateInstrumentAsync(targetInstrumentId, true);
                }
                else if (chartConfigurationChanged)
                {
                    Logger.Info(
                        "设置已保存，正在切换 K 线配置：" + targetRange.Key + " / " + targetRange.RestBar);
                    await ChangeRangeAsync(targetRange, false);
                    SaveWindowState();
                }
                else
                {
                    RenderMarket();
                    SetConnectionStatus(previousStatus, previousMessage);
                    Logger.Info("行情设置已保存并应用。");
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                return _applicationCancellation.IsCancellationRequested;
            }
            catch (Exception ex)
            {
                Logger.Error("保存或应用设置失败", ex);
                if (!IsDisposed)
                {
                    SetConnectionStatus(previousStatus, previousMessage);
                    MessageBox.Show(
                        ex.Message,
                        "无法应用设置",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                return false;
            }
        }

        private void ConfigureTaskbarTicker()
        {
            _miniTickerRotationTimer.Stop();
            if (_taskbarTicker != null)
            {
                _taskbarTicker.HideTicker();
                _taskbarTicker.Dispose();
                _taskbarTicker = null;
            }

            if (_settings.showTaskbarTickerOnMinimize)
            {
                _taskbarTicker = CreateTaskbarTicker();
            }
        }

        private void StartMiniTickerRotation()
        {
            _miniTickerRotationTimer.Stop();
            if (_taskbarTicker == null || !_taskbarTicker.Visible || _settings.instrumentIds.Length <= 1)
            {
                return;
            }

            int activeIndex = Array.FindIndex(
                _settings.instrumentIds,
                item => string.Equals(item, _selectedInstrumentId, StringComparison.OrdinalIgnoreCase));
            _miniTickerIndex = activeIndex >= default(int) ? activeIndex : default(int);
            _miniTickerRotationTimer.Interval = _settings.taskbarTickerRotationIntervalSeconds * 1000;
            _miniTickerRotationTimer.Start();
        }

        private async Task RotateMiniTickerAsync()
        {
            if (_miniTickerBusy
                || _taskbarTicker == null
                || !_taskbarTicker.Visible
                || _settings.instrumentIds.Length <= 1
                || _applicationCancellation.IsCancellationRequested)
            {
                return;
            }

            _miniTickerBusy = true;
            int settingsGeneration = _settingsGeneration;
            TaskbarTickerForm ticker = _taskbarTicker;
            try
            {
                _miniTickerIndex = (_miniTickerIndex + 1) % _settings.instrumentIds.Length;
                string instrumentId = _settings.instrumentIds[_miniTickerIndex];
                InstrumentInfo instrument;
                if (!_instrumentInfos.TryGetValue(instrumentId, out instrument))
                {
                    instrument = await _marketClient.ValidateInstrumentAsync(
                        instrumentId,
                        _applicationCancellation.Token);
                    if (settingsGeneration != _settingsGeneration)
                    {
                        return;
                    }

                    _instrumentInfos[instrumentId] = instrument;
                }

                MarketSnapshot snapshot = await _marketClient.FetchTickerAsync(
                    instrumentId,
                    _applicationCancellation.Token);
                IList<Candle> miniCandles;
                bool needsMiniCandles = false;
                if (_instrument != null
                    && string.Equals(_instrument.InstrumentId, instrumentId, StringComparison.OrdinalIgnoreCase))
                {
                    miniCandles = _candles;
                }
                else
                {
                    List<Candle> cachedCandles;
                    if (!_miniTickerCandles.TryGetValue(instrumentId, out cachedCandles))
                    {
                        needsMiniCandles = true;
                        miniCandles = EmptyCandles;
                    }
                    else
                    {
                        miniCandles = cachedCandles;
                    }
                }

                if (settingsGeneration == _settingsGeneration
                    && ReferenceEquals(ticker, _taskbarTicker)
                    && ticker.Visible)
                {
                    ticker.UpdateMarket(instrumentId, snapshot, miniCandles, instrument.TickSize, true);
                }

                if (needsMiniCandles)
                {
                    List<Candle> loadedCandles = await _marketClient.FetchMiniTickerCandlesAsync(
                        instrumentId,
                        _applicationCancellation.Token);
                    _miniTickerCandles[instrumentId] = loadedCandles;
                    if (settingsGeneration == _settingsGeneration
                        && ReferenceEquals(ticker, _taskbarTicker)
                        && ticker.Visible)
                    {
                        ticker.UpdateMarket(instrumentId, snapshot, loadedCandles, instrument.TickSize, false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Error("轮播迷你行情标的失败", ex);
            }
            finally
            {
                _miniTickerBusy = false;
            }
        }

        private void HideTaskbarTicker()
        {
            _miniTickerRotationTimer.Stop();
            if (_taskbarTicker != null)
            {
                _taskbarTicker.HideTicker();
            }
        }

        private TaskbarTickerForm CreateTaskbarTicker()
        {
            return new TaskbarTickerForm(
                ShowFromTray,
                _settings.TickerPosition,
                _settings.hasCustomTaskbarTickerPosition,
                _settings.taskbarTickerCustomLeft,
                _settings.taskbarTickerCustomTop,
                SaveCustomTaskbarTickerLocation);
        }

        private void SaveCustomTaskbarTickerLocation(Point location)
        {
            _settings.taskbarTickerPosition = "custom";
            _settings.TickerPosition = TaskbarTickerPosition.Custom;
            _settings.hasCustomTaskbarTickerPosition = true;
            _settings.taskbarTickerCustomLeft = location.X;
            _settings.taskbarTickerCustomTop = location.Y;
            try
            {
                SettingsStore.Save(_settings);
                Logger.Info("迷你行情条位置已保存：" + location.X + "," + location.Y + "。");
            }
            catch (Exception ex)
            {
                Logger.Error("保存迷你行情条自定义位置失败", ex);
            }
        }

        private void ToggleTopMost()
        {
            TopMost = !TopMost;
            _topMostMenu.Checked = TopMost;
            _pinButton.ForeColor = TopMost ? UpColor : SecondaryTextColor;
            SaveWindowState();
        }

        private void ToggleAutoStart()
        {
            try
            {
                bool enabled = !AutoStartManager.IsEnabled();
                AutoStartManager.SetEnabled(enabled);
                _autoStartMenu.Checked = enabled;
                Logger.Info("开机启动已" + (enabled ? "启用" : "关闭") + "。");
            }
            catch (Exception ex)
            {
                Logger.Error("修改开机启动失败", ex);
                MessageBox.Show(ex.Message, "开机启动", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ToggleVisible()
        {
            if (Visible)
            {
                SaveWindowState();
                HideTaskbarTicker();

                Hide();
            }
            else
            {
                ShowFromTray();
            }
        }

        private void ShowFromTray()
        {
            if (_restorePending || IsDisposed || Disposing)
            {
                return;
            }

            _restorePending = true;
            try
            {
                BeginInvoke(new Action(RestoreMainWindowCore));
            }
            catch (InvalidOperationException)
            {
                _restorePending = false;
            }
        }

        internal void RestoreFromExternalLaunch()
        {
            Logger.Info("收到重复启动请求，正在恢复已有窗口。");
            ShowFromTray();
        }

        private void RestoreMainWindowCore()
        {
            FormWindowState restoreState = _windowStateBeforeMinimize == FormWindowState.Maximized
                ? FormWindowState.Maximized
                : FormWindowState.Normal;
            try
            {
                if (IsDisposed || Disposing)
                {
                    return;
                }

                Rectangle targetBounds = NormalizeWindowBounds(_lastNormalBounds);
                _windowRestoreInProgress = true;
                Show();
                WindowActivation.RestoreAndActivate(
                    Handle,
                    targetBounds,
                    restoreState == FormWindowState.Maximized);
                _lastNormalBounds = targetBounds;
                BringToFront();
                Activate();
                PerformLayout();
                Update();
                HideTaskbarTicker();

                Logger.Info(string.Format(
                    "主窗口已从托盘或迷你行情条恢复，目标状态：{0}，实际状态：{1}，实际边界：{2},{3},{4}x{5}，正常边界：{6},{7},{8}x{9}。",
                    restoreState,
                    WindowState,
                    Bounds.Left,
                    Bounds.Top,
                    Bounds.Width,
                    Bounds.Height,
                    targetBounds.Left,
                    targetBounds.Top,
                    targetBounds.Width,
                    targetBounds.Height));
            }
            finally
            {
                _windowStateBeforeMinimize = restoreState;
                _windowRestoreInProgress = false;
                _restorePending = false;
            }
        }

        private void HandleMainResize(object sender, EventArgs args)
        {
            if (_windowRestoreInProgress)
            {
                return;
            }

            if (WindowState != FormWindowState.Minimized)
            {
                _windowStateBeforeMinimize = WindowState;
                return;
            }

            if (!Visible || _minimizePending)
            {
                return;
            }

            _minimizePending = true;
            BeginInvoke(new Action(delegate
            {
                _minimizePending = false;
                if (!Visible || WindowState != FormWindowState.Minimized)
                {
                    return;
                }

                Rectangle referenceBounds = ResolveNormalWindowBounds();
                _lastNormalBounds = referenceBounds;
                SaveWindowState();
                Hide();
                if (_taskbarTicker != null)
                {
                    _taskbarTicker.ShowTicker(referenceBounds);
                    StartMiniTickerRotation();
                }

                Logger.Info(string.Format(
                    _taskbarTicker == null
                        ? "窗口已最小化到托盘，最小化前状态：{0}，正常位置：{1},{2}，正常尺寸：{3}x{4}。"
                        : "窗口已最小化到托盘并显示迷你行情条，最小化前状态：{0}，正常位置：{1},{2}，正常尺寸：{3}x{4}。",
                    _windowStateBeforeMinimize,
                    referenceBounds.Left,
                    referenceBounds.Top,
                    referenceBounds.Width,
                    referenceBounds.Height));
            }));
        }

        private void ExitApplication()
        {
            _allowExit = true;
            SaveWindowState();
            Close();
        }

        private void HandleFormClosing(object sender, FormClosingEventArgs args)
        {
            Logger.Info("收到窗口关闭请求，原因：" + args.CloseReason);
            if (!_allowExit && args.CloseReason != CloseReason.WindowsShutDown)
            {
                args.Cancel = true;
                SaveWindowState();
                HideTaskbarTicker();

                Hide();
                return;
            }

            SaveWindowState();
        }

        private void HandleFormClosed(object sender, FormClosedEventArgs args)
        {
            _clockTimer.Stop();
            _tickerTimer.Stop();
            _fallbackTimer.Stop();
            _renderTimer.Stop();
            _memoryTimer.Stop();
            _miniTickerRotationTimer.Stop();
            _applicationCancellation.Cancel();
            CancelRangeLoad();
            StopRealtimeStream();
            _notifyIcon.Visible = false;
            HideTaskbarTicker();
        }

        private void SaveWindowState()
        {
            Rectangle bounds = ResolveNormalWindowBounds();
            _lastNormalBounds = bounds;
            StateStore.Save(new WindowState
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = Math.Max(MinimumSize.Width, bounds.Width),
                Height = Math.Max(MinimumSize.Height, bounds.Height),
                TopMost = TopMost,
                RangeKey = _currentRange.Key,
                InstrumentId = _selectedInstrumentId
            });
        }

        private void RestoreWindowBounds(WindowState state)
        {
            Rectangle requested = new Rectangle(state.Left, state.Top, state.Width, state.Height);
            bool hasUnspecifiedPosition = state.Left == UnspecifiedWindowCoordinate
                && state.Top == UnspecifiedWindowCoordinate;
            if (hasUnspecifiedPosition)
            {
                Rectangle working = Screen.PrimaryScreen.WorkingArea;
                Bounds = new Rectangle(
                    working.Left + Math.Max(0, (working.Width - Width) / 2),
                    working.Top + Math.Max(0, (working.Height - Height) / 2),
                    Width,
                    Height);
                return;
            }

            Bounds = NormalizeWindowBounds(requested);
        }

        private void QueueNormalWindowBoundsCapture(object sender, EventArgs args)
        {
            if (_windowRestoreInProgress || _normalBoundsCapturePending || IsDisposed || Disposing)
            {
                return;
            }

            _normalBoundsCapturePending = true;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    _normalBoundsCapturePending = false;
                    CaptureNormalWindowBounds();
                }));
            }
            catch (InvalidOperationException)
            {
                _normalBoundsCapturePending = false;
            }
        }

        private void CaptureNormalWindowBounds()
        {
            if (_windowRestoreInProgress || WindowState != FormWindowState.Normal)
            {
                return;
            }

            if (IsWindowBoundsVisible(Bounds))
            {
                _lastNormalBounds = NormalizeWindowBounds(Bounds);
            }
        }

        private Rectangle ResolveNormalWindowBounds()
        {
            if (WindowState == FormWindowState.Normal && IsWindowBoundsVisible(Bounds))
            {
                return NormalizeWindowBounds(Bounds);
            }

            Rectangle restoreBounds = RestoreBounds;
            if (restoreBounds.Width >= MinimumSize.Width
                && restoreBounds.Height >= MinimumSize.Height
                && IsWindowBoundsVisible(restoreBounds))
            {
                return NormalizeWindowBounds(restoreBounds);
            }

            return NormalizeWindowBounds(_lastNormalBounds);
        }

        private Rectangle NormalizeWindowBounds(Rectangle requested)
        {
            int width = Math.Max(MinimumSize.Width, requested.Width);
            int height = Math.Max(MinimumSize.Height, requested.Height);
            Rectangle resized = new Rectangle(requested.Left, requested.Top, width, height);
            if (!IsWindowBoundsVisible(resized))
            {
                Rectangle primaryWorkingArea = Screen.PrimaryScreen.WorkingArea;
                width = Math.Min(width, primaryWorkingArea.Width);
                height = Math.Min(height, primaryWorkingArea.Height);
                return new Rectangle(
                    primaryWorkingArea.Left + Math.Max(0, (primaryWorkingArea.Width - width) / 2),
                    primaryWorkingArea.Top + Math.Max(0, (primaryWorkingArea.Height - height) / 2),
                    width,
                    height);
            }

            Rectangle workingArea = Screen.FromRectangle(resized).WorkingArea;
            width = Math.Min(width, workingArea.Width);
            height = Math.Min(height, workingArea.Height);
            int left = Math.Max(workingArea.Left, Math.Min(resized.Left, workingArea.Right - width));
            int top = Math.Max(workingArea.Top, Math.Min(resized.Top, workingArea.Bottom - height));
            return new Rectangle(left, top, width, height);
        }

        private static bool IsWindowBoundsVisible(Rectangle bounds)
        {
            return Screen.AllScreens.Any(screen =>
            {
                Rectangle intersection = Rectangle.Intersect(screen.WorkingArea, bounds);
                return intersection.Width >= MinimumVisibleWindowOverlap
                    && intersection.Height >= MinimumVisibleWindowOverlap;
            });
        }

        private void SafeBeginInvoke(Action action)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // Window is closing.
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _marketClient.Dispose();
                _applicationCancellation.Dispose();
                if (_streamCancellation != null)
                {
                    _streamCancellation.Dispose();
                }

                _clockTimer.Dispose();
                _tickerTimer.Dispose();
                _fallbackTimer.Dispose();
                _renderTimer.Dispose();
                _memoryTimer.Dispose();
                _miniTickerRotationTimer.Dispose();
                _rangeFontRegular.Dispose();
                _rangeFontBold.Dispose();
                _notifyIcon.Dispose();
                if (_taskbarTicker != null)
                {
                    _taskbarTicker.Dispose();
                }

                _appIcon.Dispose();
            }

            base.Dispose(disposing);
        }
    }

}
