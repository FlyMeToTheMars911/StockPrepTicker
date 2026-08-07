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
        private const int DefaultRenderIntervalMilliseconds = 1000;
        private const int UnspecifiedWindowCoordinate = -1;
        private const int MinimumVisibleWindowOverlap = 100;
        private static readonly Color UpColor = Color.FromArgb(8, 153, 129);
        private static readonly Color DownColor = Color.FromArgb(242, 54, 69);
        private static readonly Color SecondaryTextColor = Color.FromArgb(90, 96, 110);
        private readonly ConfigLoadResult _configResult;
        private readonly WindowState _initialState;
        private readonly OkxMarketClient _marketClient;
        private readonly CancellationTokenSource _applicationCancellation;
        private readonly List<Candle> _candles;
        private readonly Dictionary<string, Button> _rangeButtons;
        private readonly ChartControl _chart;
        private readonly TaskbarTickerForm _taskbarTicker;
        private readonly Label _symbolLabel;
        private readonly Label _priceLabel;
        private readonly Label _changeLabel;
        private readonly Label _statusLabel;
        private readonly Label _clockLabel;
        private readonly Button _pinButton;
        private readonly NotifyIcon _notifyIcon;
        private readonly ToolStripMenuItem _topMostMenu;
        private readonly ToolStripMenuItem _autoStartMenu;
        private readonly System.Windows.Forms.Timer _clockTimer;
        private readonly System.Windows.Forms.Timer _tickerTimer;
        private readonly System.Windows.Forms.Timer _fallbackTimer;
        private readonly System.Windows.Forms.Timer _memoryTimer;
        private readonly System.Windows.Forms.Timer _renderTimer;
        private readonly Font _rangeFontRegular;
        private readonly Font _rangeFontBold;
        private readonly object _pendingCandleSync;
        private CancellationTokenSource _streamCancellation;
        private Task _streamTask;
        private InstrumentInfo _instrument;
        private MarketSnapshot _snapshot;
        private RangeDefinition _currentRange;
        private ConnectionStatus _connectionStatus;
        private bool _allowExit;
        private bool _fallbackBusy;
        private bool _minimizePending;
        private bool _restorePending;
        private Rectangle _lastNormalBounds;
        private int _rangeGeneration;
        private Candle _pendingCandle;
        private Icon _appIcon;

        internal MainForm(ConfigLoadResult configResult, WindowState initialState)
        {
            _configResult = configResult;
            _initialState = initialState;
            _marketClient = new OkxMarketClient();
            _applicationCancellation = new CancellationTokenSource();
            _candles = new List<Candle>();
            _rangeButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
            _pendingCandleSync = new object();
            _currentRange = RangeDefinition.Find(initialState.RangeKey);
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
            _taskbarTicker = configResult.IsValid && configResult.Config.showTaskbarTickerOnMinimize
                ? new TaskbarTickerForm(ShowFromTray, configResult.Config.TickerPosition)
                : null;

            RestoreWindowBounds(initialState);
            _lastNormalBounds = Bounds;
            TopMost = initialState.TopMost;

            Panel topBar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.White, Padding = new Padding(10, 6, 8, 4) };
            _symbolLabel = new Label
            {
                AutoSize = false,
                Location = new Point(10, 5),
                Size = new Size(170, 22),
                Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold, GraphicsUnit.Point),
                Text = configResult.IsValid ? configResult.Config.instrumentId : "配置错误",
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
            topBar.Controls.Add(_symbolLabel);
            topBar.Controls.Add(_priceLabel);
            topBar.Controls.Add(_changeLabel);
            topBar.Controls.Add(_statusLabel);
            topBar.Controls.Add(_pinButton);
            topBar.Resize += delegate
            {
                _pinButton.Left = Math.Max(0, topBar.ClientSize.Width - _pinButton.Width - 8);
                _pinButton.BringToFront();
            };

            _chart = new ChartControl { Dock = DockStyle.Fill };

            Panel bottomBar = new Panel { Dock = DockStyle.Bottom, Height = 42, BackColor = Color.White };
            int buttonLeft = 8;
            foreach (RangeDefinition range in RangeDefinition.All)
            {
                Button button = CreateRangeButton(range, buttonLeft);
                buttonLeft += range.Key == "1M" ? 63 : 47;
                bottomBar.Controls.Add(button);
                _rangeButtons[range.Key] = button;
            }

            _clockLabel = new Label
            {
                Dock = DockStyle.Right,
                Width = 122,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 8, 0),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point)
            };
            bottomBar.Controls.Add(_clockLabel);

            Controls.Add(_chart);
            Controls.Add(bottomBar);
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
                Interval = configResult.IsValid
                    ? configResult.Config.refreshIntervalMilliseconds
                    : DefaultRenderIntervalMilliseconds
            };
            _renderTimer.Tick += delegate { ApplyPendingRealtimeCandle(); };
            _renderTimer.Start();
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
            LocationChanged += CaptureNormalWindowBounds;
            SizeChanged += CaptureNormalWindowBounds;
            FormClosing += HandleFormClosing;
            FormClosed += HandleFormClosed;
            UpdateRangeButtons();
        }

        private Button CreateRangeButton(RangeDefinition range, int left)
        {
            Button button = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Location = new Point(left, 7),
                Size = new Size(range.Key == "1M" ? 58 : 42, 28),
                Text = range.Label,
                Tag = range,
                TabStop = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += async delegate
            {
                if (_instrument != null && !ReferenceEquals(_currentRange, range))
                {
                    await ChangeRangeAsync(range);
                }
            };
            return button;
        }

        private async Task InitializeMarketAsync()
        {
            if (!_configResult.IsValid)
            {
                SetConnectionStatus(ConnectionStatus.Error, "配置错误");
                _chart.SetMessage(_configResult.Error, true);
                Logger.Info(_configResult.Error);
                return;
            }

            try
            {
                SetConnectionStatus(ConnectionStatus.Loading, "正在校验合约");
                _instrument = await _marketClient.ValidateInstrumentAsync(
                    _configResult.Config.instrumentId,
                    _applicationCancellation.Token);
                Text = _instrument.InstrumentId + " - StockPerpTicker";
                Logger.Info("合约校验成功：" + _instrument.InstrumentId);
                await ChangeRangeAsync(_currentRange);
                _tickerTimer.Start();
                _fallbackTimer.Start();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                SetConnectionStatus(ConnectionStatus.Error, "行情初始化失败");
                _chart.SetMessage(ex.Message + Environment.NewLine + "配置文件：" + _configResult.Path, true);
                Logger.Error("行情初始化失败", ex);
            }
        }

        private async Task ChangeRangeAsync(RangeDefinition range)
        {
            int generation = ++_rangeGeneration;
            StopRealtimeStream();
            lock (_pendingCandleSync)
            {
                _pendingCandle = null;
            }

            _currentRange = range;
            UpdateRangeButtons();
            SetConnectionStatus(ConnectionStatus.Loading, "正在加载 " + range.Label + " 数据");
            _chart.SetMessage("正在加载 " + range.Label + " K 线…", false);

            try
            {
                List<Candle> history = await _marketClient.FetchCandlesAsync(
                    _instrument.InstrumentId,
                    range,
                    _instrument.ListingTime,
                    _applicationCancellation.Token);
                MarketSnapshot snapshot = await _marketClient.FetchTickerAsync(
                    _instrument.InstrumentId,
                    _applicationCancellation.Token);
                if (generation != _rangeGeneration || _applicationCancellation.IsCancellationRequested)
                {
                    return;
                }

                _candles.Clear();
                _candles.AddRange(history);
                _snapshot = snapshot;
                RenderMarket();
                UpdateHeader();
                StartRealtimeStream(range);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                SetConnectionStatus(ConnectionStatus.Error, "K 线加载失败");
                _chart.SetMessage(ex.Message, true);
                Logger.Error("加载 K 线失败：" + range.Key, ex);
                StartRealtimeStream(range);
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
                _snapshot = await _marketClient.FetchTickerAsync(_instrument.InstrumentId, _applicationCancellation.Token);
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
                Candle latest = await _marketClient.FetchLatestCandleAsync(
                    _instrument.InstrumentId,
                    _currentRange,
                    _applicationCancellation.Token);
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
            int existing = _candles.FindIndex(item => item.Timestamp == candle.Timestamp);
            if (existing > MissingCandleIndex)
            {
                _candles[existing] = candle;
            }
            else
            {
                _candles.Add(candle);
                _candles.Sort((left, right) => left.Timestamp.CompareTo(right.Timestamp));
            }

            while (_candles.Count > _currentRange.MaximumPoints)
            {
                _candles.RemoveAt(0);
            }
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
                    _configResult.Config.movingAverages);
                if (_taskbarTicker != null)
                {
                    _taskbarTicker.UpdateMarket(
                        _instrument.InstrumentId,
                        _snapshot,
                        _candles,
                        _instrument.TickSize);
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

        private void UpdateRangeButtons()
        {
            foreach (KeyValuePair<string, Button> entry in _rangeButtons)
            {
                bool selected = string.Equals(entry.Key, _currentRange.Key, StringComparison.OrdinalIgnoreCase);
                entry.Value.ForeColor = selected ? UpColor : Color.FromArgb(19, 23, 34);
                entry.Value.Font = selected ? _rangeFontBold : _rangeFontRegular;
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
                if (_taskbarTicker != null)
                {
                    _taskbarTicker.HideTicker();
                }

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
            try
            {
                if (IsDisposed || Disposing)
                {
                    return;
                }

                Rectangle targetBounds = NormalizeWindowBounds(_lastNormalBounds);
                WindowState = FormWindowState.Normal;
                Bounds = targetBounds;
                Show();
                WindowActivation.RestoreAndActivate(Handle, targetBounds);
                Bounds = targetBounds;
                _lastNormalBounds = targetBounds;
                BringToFront();
                Activate();
                Update();
                if (_taskbarTicker != null)
                {
                    _taskbarTicker.HideTicker();
                }

                Logger.Info(string.Format(
                    "主窗口已从托盘或迷你行情条恢复，位置：{0},{1}，尺寸：{2}x{3}。",
                    targetBounds.Left,
                    targetBounds.Top,
                    targetBounds.Width,
                    targetBounds.Height));
            }
            finally
            {
                _restorePending = false;
            }
        }

        private void HandleMainResize(object sender, EventArgs args)
        {
            if (WindowState != FormWindowState.Minimized || !Visible || _minimizePending)
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

                Rectangle referenceBounds = NormalizeWindowBounds(_lastNormalBounds);
                _lastNormalBounds = referenceBounds;
                SaveWindowState();
                Hide();
                if (_taskbarTicker != null)
                {
                    _taskbarTicker.ShowTicker(referenceBounds);
                }

                Logger.Info(_taskbarTicker == null
                    ? "窗口已最小化到托盘。"
                    : "窗口已最小化到托盘并显示迷你行情条。");
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
                if (_taskbarTicker != null)
                {
                    _taskbarTicker.HideTicker();
                }

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
            _applicationCancellation.Cancel();
            StopRealtimeStream();
            _notifyIcon.Visible = false;
            if (_taskbarTicker != null)
            {
                _taskbarTicker.HideTicker();
            }
        }

        private void SaveWindowState()
        {
            Rectangle bounds = WindowState == FormWindowState.Normal && IsWindowBoundsVisible(Bounds)
                ? NormalizeWindowBounds(Bounds)
                : NormalizeWindowBounds(_lastNormalBounds);
            _lastNormalBounds = bounds;
            StateStore.Save(new WindowState
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = Math.Max(MinimumSize.Width, bounds.Width),
                Height = Math.Max(MinimumSize.Height, bounds.Height),
                TopMost = TopMost,
                RangeKey = _currentRange.Key
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

        private void CaptureNormalWindowBounds(object sender, EventArgs args)
        {
            if (WindowState == FormWindowState.Normal && IsWindowBoundsVisible(Bounds))
            {
                _lastNormalBounds = Bounds;
            }
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
