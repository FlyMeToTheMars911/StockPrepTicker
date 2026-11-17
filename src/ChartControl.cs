using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace StockPerpTicker
{
    internal sealed class ChartControl : Control
    {
        private const int MinimumPaintDimension = 100;
        private const int EmptyCandleCount = 0;
        private const int ChartHeaderHeight = 46;
        private const int RightAxisWidth = 62;
        private const int IndexRightAxisWidth = 76;
        private const int IndexAlignedAxisMinimumHeight = 180;
        private const int IndexLegendRowHeight = 20;
        private const int IndexLegendMargin = 3;
        private const int IndexAxisNumberLeft = 1;
        private const int IndexAxisNumberWidth = 24;
        private const int IndexAxisLabelOffset = 26;
        private const int IndexCompactLegendRowHeight = 24;
        private const int IndexCompactLabelHeight = 14;
        private const int IndexCompactRangeHeight = 10;
        private const int IndexBandFillAlpha = 10;
        private const int IndexThresholdAlpha = 155;
        private const int IndexCurrentTagWidth = 54;
        private const int MaximumIndexTimeLabels = 5;
        private const int IndexTimeLabelGap = 12;
        private const int IndexTimeLabelPadding = 4;
        private const float IndexLineWidth = 2.4f;
        private const int BottomAxisHeight = 42;
        private const int LeftPadding = 8;
        private const int RightPadding = 2;
        private const int MinimumVisibleCandleCount = 20;
        private const int MouseWheelDelta = 120;
        private const int NoViewportOffset = 0;
        private const int FullViewportCandleCount = 0;
        private const int MissingCandleIndex = -1;
        private const float ZoomStep = 0.80f;
        private const int CrosshairTimeLabelHorizontalPadding = 6;
        private const int CrosshairLabelHeight = 20;
        private const int IntervalTooltipWidth = 320;
        private const int IntervalTooltipHeight = 154;
        private const int IntervalTooltipMargin = 10;
        private const int IntervalTooltipPadding = 10;
        private const int IntervalTooltipTitleHeight = 22;
        private const int IntervalTooltipLineHeight = 19;
        private const float MinimumIntervalDrawingWidth = 1f;
        private const long OneDayMinutes = 1440L;
        private const long FiveDayMinutes = 7200L;
        private const long OneYearMinutes = 525600L;
        private const long OneMonthPeriodMinutes = 43200L;
        private const int PreMarketStartMinutes = 240;
        private const int RegularMarketStartMinutes = 570;
        private const int AfterHoursStartMinutes = 960;
        private const int OvernightStartMinutes = 1200;
        private const float SessionBandTopOffset = 2f;
        private const float SessionBandHeight = 16f;
        private const float SessionLabelMinimumWidth = 42f;
        private const float SessionSeparatorMinimumWidth = 2f;
        private const float TimeLabelTopWithSessionBand = 20f;
        private const float TimeLabelTopWithoutSessionBand = 10f;
        private const int MinutesPerHour = 60;
        private const int MaximumDisplayTimeCacheEntries = 6000;
        private const int DetailsWidth = 300;
        private const int DetailsHeight = 282;
        private const int DetailsMargin = 10;
        private const int DetailsTitleHeight = 28;
        private const int DetailsRowHeight = 22;
        private const int DetailsFooterHeight = 24;
        private const int DetailsCloseSize = 24;
        private const int DetailsRowCount = 10;
        private const int IndexDetailsRowCount = 4;
        private const int SingleDataPointCount = 1;
        private const decimal MinimumIndexValue = 0m;
        private const decimal MaximumIndexValue = 100m;
        private const float IndexPointRadius = 3f;
        private const int FullOpacityAlpha = 255;
        private const int FullPercentage = 100;
        private static readonly Color UpColor = Color.FromArgb(8, 153, 129);
        private static readonly Color DownColor = Color.FromArgb(242, 54, 69);
        private static readonly Color TextColor = Color.FromArgb(19, 23, 34);
        private static readonly Color SecondaryTextColor = Color.FromArgb(90, 96, 110);
        private static readonly Color GridColor = Color.FromArgb(224, 227, 235);
        private static readonly Color IntervalColor = Color.FromArgb(41, 98, 255);
        private static readonly Color IntervalFillColor = Color.FromArgb(34, 41, 98, 255);
        private static readonly Color TooltipBackgroundColor = Color.FromArgb(242, 19, 23, 34);
        private static readonly Color OvernightSessionColor = Color.FromArgb(42, 69, 104, 220);
        private static readonly Color PreMarketSessionColor = Color.FromArgb(48, 245, 158, 11);
        private static readonly Color RegularMarketSessionColor = Color.FromArgb(48, 8, 153, 129);
        private static readonly Color AfterHoursSessionColor = Color.FromArgb(46, 139, 92, 246);
        private static readonly Color SessionSeparatorColor = Color.FromArgb(90, 148, 155, 170);
        private static readonly Color SessionTextColor = Color.FromArgb(55, 61, 75);
        private readonly Font _smallFont;
        private readonly Font _axisFont;
        private readonly Font _indexAxisFont;
        private readonly Font _intervalTitleFont;
        private readonly Font _indexLabelFont;
        private readonly Font _indexCompactLabelFont;
        private readonly Font _indexCompactRangeFont;
        private readonly ContextMenuStrip _drawingMenu;
        private readonly ToolStripMenuItem _intervalStatisticsMenuItem;
        private readonly ToolStripMenuItem _deleteIntervalStatisticsMenuItem;
        private readonly ToolStripMenuItem _candleDetailsMenuItem;
        private readonly Dictionary<long, DateTime> _displayTimeCache;
        private Bitmap _chartLayer;
        private List<Candle> _candles;
        private MarketSnapshot _snapshot;
        private RangeDefinition _range;
        private DisplayTimeZone _displayTimeZone;
        private decimal _tickSize;
        private int[] _movingAverages;
        private string _message;
        private bool _isError;
        private bool _lineMode;
        private bool _chartLayerDirty;
        private bool _hoverVisible;
        private bool _isDragging;
        private Point _hoverPoint;
        private Point _dragStartPoint;
        private int _visibleCandleCount;
        private int _rightOffset;
        private int _dragStartRightOffset;
        private Rectangle _lastPlotArea;
        private Rectangle _lastPriceArea;
        private int _lastVisibleStart;
        private int _lastVisibleCount;
        private decimal _lastMinimum;
        private decimal _lastMaximum;
        private bool _lastLayoutValid;
        private long? _contextMenuCandleTimestamp;
        private bool _contextMenuInsideInterval;
        private long? _intervalStartTimestamp;
        private long? _intervalEndTimestamp;
        private bool _isSelectingInterval;
        private bool _intervalHovered;
        private IntervalStatistics _intervalStatistics;
        private Candle _detailsCandle;
        private bool _detailsInHistory;
        private int _detailsScrollOffset;
        private int _detailsTransparency = SettingsStore.DefaultCandleDetailsTransparencyPercent;

        internal ChartControl()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.Selectable, true);
            TabStop = false;
            BackColor = Color.White;
            _smallFont = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            _axisFont = new Font("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Point);
            _indexAxisFont = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
            _intervalTitleFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
            _indexLabelFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
            _indexCompactLabelFont = new Font("Microsoft YaHei UI", 8f, FontStyle.Bold, GraphicsUnit.Point);
            _indexCompactRangeFont = new Font("Segoe UI", 6.5f, FontStyle.Regular, GraphicsUnit.Point);
            _drawingMenu = new ContextMenuStrip { ShowImageMargin = false };
            _candleDetailsMenuItem = new ToolStripMenuItem("查看详情");
            _candleDetailsMenuItem.Click += delegate { ShowCandleDetails(); };
            _drawingMenu.Items.Add(_candleDetailsMenuItem);
            _intervalStatisticsMenuItem = new ToolStripMenuItem("区间统计");
            _intervalStatisticsMenuItem.Click += delegate { BeginIntervalSelection(); };
            _deleteIntervalStatisticsMenuItem = new ToolStripMenuItem("删除统计");
            _deleteIntervalStatisticsMenuItem.Click += delegate { DeleteIntervalSelection(); };
            _drawingMenu.Items.Add(_intervalStatisticsMenuItem);
            _drawingMenu.Items.Add(_deleteIntervalStatisticsMenuItem);
            _displayTimeCache = new Dictionary<long, DateTime>();
            _candles = new List<Candle>();
            _range = RangeDefinition.Find(RangeDefinition.DefaultKey);
            _displayTimeZone = DisplayTimeZone.Beijing;
            _tickSize = 0.01m;
            _movingAverages = new int[0];
            _message = "正在加载行情…";
            _chartLayerDirty = true;
        }

        internal void SetLineMode(bool enabled)
        {
            if (_lineMode == enabled)
            {
                return;
            }

            _lineMode = enabled;
            _isDragging = false;
            Capture = false;
            ResetViewport();
            Logger.Info("图表显示模式：" + (_lineMode ? "CNN 指数折线" : "K 线"));
        }

        internal void SetData(
            IList<Candle> candles,
            MarketSnapshot snapshot,
            RangeDefinition range,
            decimal tickSize,
            int[] movingAverages,
            DisplayTimeZone displayTimeZone)
        {
            long rightmostVisibleTimestamp = GetRightmostVisibleTimestamp();
            bool followLatest = _rightOffset == NoViewportOffset;
            RangeDefinition nextRange = range ?? RangeDefinition.Find(RangeDefinition.DefaultKey);
            bool sessionSettingsChanged = _displayTimeZone != displayTimeZone
                || _range == null
                || _range.PeriodDurationMinutes != nextRange.PeriodDurationMinutes;
            _candles = candles == null ? new List<Candle>() : new List<Candle>(candles);
            _range = nextRange;
            if (_displayTimeZone != displayTimeZone
                || _displayTimeCache.Count > MaximumDisplayTimeCacheEntries)
            {
                _displayTimeCache.Clear();
            }

            _displayTimeZone = displayTimeZone;
            if (sessionSettingsChanged)
            {
                Logger.Info("交易时段标识设置：时区 " + _displayTimeZone
                    + " / 周期 " + _range.RestBar
                    + " / 显示 " + ShouldDrawUsMarketSessionBands());
            }

            UpdateIntervalStatistics();
            if (_detailsCandle != null)
            {
                int detailsIndex = FindCandleIndexByTimestamp(_detailsCandle.Timestamp);
                _detailsInHistory = detailsIndex != MissingCandleIndex;
                if (detailsIndex != MissingCandleIndex)
                {
                    _detailsCandle = _candles[detailsIndex];
                }
                // A rolling history window may drop the selected candle. Keep its last
                // values until an explicit reload or close, so appending never hides it.
            }

            if (!followLatest && rightmostVisibleTimestamp > default(long))
            {
                RestoreRightOffset(rightmostVisibleTimestamp);
            }

            NormalizeViewport();
            _snapshot = snapshot;
            _tickSize = tickSize;
            _movingAverages = movingAverages == null ? new int[0] : (int[])movingAverages.Clone();
            _message = string.Empty;
            _isError = false;
            MarkChartLayerDirty();
        }

        internal void ResetViewport()
        {
            _visibleCandleCount = FullViewportCandleCount;
            _rightOffset = NoViewportOffset;
            _hoverVisible = false;
            ClearIntervalSelection("视图重置");
            ClearCandleDetails("K 线重新加载");
            _drawingMenu.Close();
            MarkChartLayerDirty();
        }

        private void MarkChartLayerDirty()
        {
            _chartLayerDirty = true;
            _lastLayoutValid = false;
            Invalidate();
        }

        internal void SetMessage(string message, bool isError)
        {
            _message = message ?? string.Empty;
            _isError = isError;
            MarkChartLayerDirty();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _smallFont.Dispose();
                _axisFont.Dispose();
                _indexAxisFont.Dispose();
                _intervalTitleFont.Dispose();
                _indexLabelFont.Dispose();
                _indexCompactLabelFont.Dispose();
                _indexCompactRangeFont.Dispose();
                _drawingMenu.Dispose();
                if (_chartLayer != null)
                {
                    _chartLayer.Dispose();
                    _chartLayer = null;
                }
            }

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle client = ClientRectangle;
            if (client.Width < MinimumPaintDimension || client.Height < MinimumPaintDimension)
            {
                e.Graphics.Clear(BackColor);
                return;
            }

            EnsureChartLayer(client.Size);
            if (_chartLayerDirty)
            {
                using (Graphics layerGraphics = Graphics.FromImage(_chartLayer))
                {
                    DrawChartLayer(layerGraphics, client);
                }

                _chartLayerDirty = false;
            }

            e.Graphics.DrawImageUnscaled(_chartLayer, Point.Empty);
            DrawIntervalSelection(e.Graphics);
            DrawCrosshair(e.Graphics);
            DrawIntervalTooltip(e.Graphics);
            DrawCandleDetails(e.Graphics);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            MarkChartLayerDirty();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            Focus();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_isDragging && (_hoverVisible || _intervalHovered))
            {
                _hoverVisible = false;
                _intervalHovered = false;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_detailsCandle != null && GetDetailsBounds().Contains(e.Location))
            {
                _contextMenuCandleTimestamp = null;
                _contextMenuInsideInterval = false;
                if (e.Button == MouseButtons.Left && GetDetailsCloseBounds().Contains(e.Location))
                {
                    ClearCandleDetails("点击关闭");
                    Cursor = Cursors.Default;
                }
                return;
            }
            Rectangle plotArea;
            Rectangle priceArea;
            Rectangle volumeArea;
            GetChartAreas(ClientRectangle, out plotArea, out priceArea, out volumeArea);
            if (e.Button == MouseButtons.Right)
            {
                _contextMenuCandleTimestamp = null;
                _contextMenuInsideInterval = plotArea.Contains(e.Location)
                    && IsPointInVisibleInterval(e.Location);
                int contextCandleIndex;
                if (string.IsNullOrEmpty(_message) && priceArea.Contains(e.Location)
                    && TryGetCandleIndexAtX(e.X, out contextCandleIndex))
                {
                    _contextMenuCandleTimestamp = _candles[contextCandleIndex].Timestamp;
                }

                if (_contextMenuCandleTimestamp.HasValue || _contextMenuInsideInterval)
                {
                    Focus();
                }

                return;
            }

            if (e.Button != MouseButtons.Left || !plotArea.Contains(e.Location) || _candles.Count == EmptyCandleCount)
            {
                return;
            }

            Focus();
            if (_isSelectingInterval)
            {
                if (priceArea.Contains(e.Location))
                {
                    CompleteIntervalSelection(e.X);
                }

                return;
            }

            _isDragging = true;
            _dragStartPoint = e.Location;
            _dragStartRightOffset = _rightOffset;
            _hoverPoint = e.Location;
            _hoverVisible = priceArea.Contains(e.Location);
            Capture = true;
            Cursor = Cursors.SizeWE;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_isDragging && _detailsCandle != null && GetDetailsBounds().Contains(e.Location))
            {
                _hoverVisible = false;
                _intervalHovered = false;
                Cursor = GetDetailsCloseBounds().Contains(e.Location) ? Cursors.Hand : Cursors.Default;
                Invalidate();
                return;
            }
            if (!_isDragging && !_isSelectingInterval)
            {
                Cursor = Cursors.Default;
            }
            Rectangle plotArea;
            Rectangle priceArea;
            Rectangle volumeArea;
            GetChartAreas(ClientRectangle, out plotArea, out priceArea, out volumeArea);
            bool hoverVisible = priceArea.Contains(e.Location);
            bool hoverChanged = hoverVisible != _hoverVisible
                || (hoverVisible && e.Location != _hoverPoint);
            _hoverPoint = e.Location;
            _hoverVisible = hoverVisible;
            bool intervalHovered = !_isSelectingInterval && IsPointInVisibleInterval(e.Location);
            bool intervalHoverChanged = intervalHovered != _intervalHovered;
            _intervalHovered = intervalHovered;

            if (_isDragging)
            {
                int visibleStart;
                int visibleCount;
                GetViewport(out visibleStart, out visibleCount);
                float candleStep = plotArea.Width / (float)Math.Max(1, visibleCount);
                int candleDelta = (int)Math.Round((e.X - _dragStartPoint.X) / candleStep);
                int maximumOffset = Math.Max(NoViewportOffset, _candles.Count - visibleCount);
                int nextOffset = Math.Max(
                    NoViewportOffset,
                    Math.Min(maximumOffset, _dragStartRightOffset + candleDelta));
                if (nextOffset != _rightOffset)
                {
                    _rightOffset = nextOffset;
                    MarkChartLayerDirty();
                    return;
                }
            }

            if (_isSelectingInterval)
            {
                Cursor = Cursors.Cross;
            }

            if (hoverChanged || intervalHoverChanged)
            {
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                bool canStartIntervalStatistics = _contextMenuCandleTimestamp.HasValue;
                bool canDeleteIntervalStatistics = _contextMenuInsideInterval;
                _candleDetailsMenuItem.Available = canStartIntervalStatistics;
                _intervalStatisticsMenuItem.Available = canStartIntervalStatistics;
                _deleteIntervalStatisticsMenuItem.Available = canDeleteIntervalStatistics;
                if (canStartIntervalStatistics || canDeleteIntervalStatistics)
                {
                    _drawingMenu.Show(this, e.Location);
                }

                return;
            }

            if (e.Button != MouseButtons.Left || !_isDragging)
            {
                return;
            }

            _isDragging = false;
            Capture = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (_isDragging && !Capture)
            {
                _isDragging = false;
                Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_detailsCandle != null && GetDetailsBounds().Contains(e.Location))
            {
                int visibleRows = GetDetailsVisibleRows();
                _detailsScrollOffset = Math.Max(default(int), Math.Min(
                    GetDetailsRowCount() - visibleRows, _detailsScrollOffset - Math.Sign(e.Delta)));
                Invalidate();
                return;
            }
            Rectangle plotArea;
            Rectangle priceArea;
            Rectangle volumeArea;
            GetChartAreas(ClientRectangle, out plotArea, out priceArea, out volumeArea);
            if (!plotArea.Contains(e.Location) || _candles.Count <= MinimumVisibleCandleCount || e.Delta == default(int))
            {
                return;
            }

            int visibleStart;
            int visibleCount;
            GetViewport(out visibleStart, out visibleCount);
            int wheelSteps = Math.Max(1, Math.Abs(e.Delta) / MouseWheelDelta);
            double scale = Math.Pow(ZoomStep, wheelSteps);
            int nextVisibleCount = e.Delta > default(int)
                ? Math.Max(MinimumVisibleCandleCount, (int)Math.Round(visibleCount * scale))
                : Math.Min(_candles.Count, (int)Math.Ceiling(visibleCount / scale));
            if (nextVisibleCount == visibleCount)
            {
                return;
            }

            float pointerRatio = Math.Max(0f, Math.Min(1f, (e.X - plotArea.Left) / (float)Math.Max(1, plotArea.Width)));
            int anchorIndex = Math.Min(
                _candles.Count - 1,
                visibleStart + Math.Min(visibleCount - 1, (int)Math.Floor(pointerRatio * visibleCount)));
            if (nextVisibleCount >= _candles.Count)
            {
                _visibleCandleCount = FullViewportCandleCount;
                _rightOffset = NoViewportOffset;
            }
            else
            {
                int nextAnchorPosition = Math.Min(nextVisibleCount - 1, (int)Math.Floor(pointerRatio * nextVisibleCount));
                int maximumStart = _candles.Count - nextVisibleCount;
                int nextStart = Math.Max(default(int), Math.Min(maximumStart, anchorIndex - nextAnchorPosition));
                _visibleCandleCount = nextVisibleCount;
                _rightOffset = _candles.Count - nextStart - nextVisibleCount;
            }

            _hoverPoint = e.Location;
            _hoverVisible = priceArea.Contains(e.Location);
            MarkChartLayerDirty();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Escape || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode != Keys.Escape
                || (!_isSelectingInterval && !_intervalStartTimestamp.HasValue && !_intervalEndTimestamp.HasValue))
            {
                return;
            }

            ClearIntervalSelection("用户取消");
            e.Handled = true;
        }

        private void BeginIntervalSelection()
        {
            if (!_contextMenuCandleTimestamp.HasValue)
            {
                return;
            }

            int startIndex = FindCandleIndexByTimestamp(_contextMenuCandleTimestamp.Value);
            if (startIndex == MissingCandleIndex)
            {
                _contextMenuCandleTimestamp = null;
                return;
            }

            _intervalStartTimestamp = _candles[startIndex].Timestamp;
            _intervalEndTimestamp = null;
            _intervalStatistics = null;
            _isSelectingInterval = true;
            _intervalHovered = false;
            _contextMenuCandleTimestamp = null;
            Cursor = Cursors.Cross;
            Logger.Info("开始区间统计，起点：" + FormatIntervalTime(GetDisplayTime(_candles[startIndex])));
            Invalidate();
        }

        private void DeleteIntervalSelection()
        {
            if (!_contextMenuInsideInterval)
            {
                return;
            }

            ClearIntervalSelection("用户删除");
        }

        private void CompleteIntervalSelection(int x)
        {
            int endIndex;
            if (!_intervalStartTimestamp.HasValue || !TryGetCandleIndexAtX(x, out endIndex))
            {
                return;
            }

            int startIndex = FindCandleIndexByTimestamp(_intervalStartTimestamp.Value);
            if (startIndex == MissingCandleIndex)
            {
                ClearIntervalSelection("起点已不在当前 K 线数据中");
                return;
            }

            int firstIndex = Math.Min(startIndex, endIndex);
            int lastIndex = Math.Max(startIndex, endIndex);
            _intervalStartTimestamp = _candles[firstIndex].Timestamp;
            _intervalEndTimestamp = _candles[lastIndex].Timestamp;
            _isSelectingInterval = false;
            Cursor = Cursors.Default;
            UpdateIntervalStatistics();
            if (_intervalStatistics != null)
            {
                string changeDescription = _lineMode
                    ? " 个数据点，指数变动 " + FormatSignedPrice(_intervalStatistics.ChangeValue) + " 点"
                    : " 根 K 线，涨跌 " + FormatSignedPrice(_intervalStatistics.ChangeValue) + "（"
                        + FormatSignedPercentage(_intervalStatistics.ChangePercent) + "）";
                Logger.Info("完成区间统计：" + _intervalStatistics.CandleCount + changeDescription);
            }

            Invalidate();
        }

        private void ClearIntervalSelection(string reason)
        {
            bool hadSelection = _isSelectingInterval
                || _intervalStartTimestamp.HasValue
                || _intervalEndTimestamp.HasValue;
            _contextMenuCandleTimestamp = null;
            _contextMenuInsideInterval = false;
            _intervalStartTimestamp = null;
            _intervalEndTimestamp = null;
            _intervalStatistics = null;
            _isSelectingInterval = false;
            _intervalHovered = false;
            if (_drawingMenu.Visible)
            {
                _drawingMenu.Close(ToolStripDropDownCloseReason.CloseCalled);
            }

            if (!_isDragging)
            {
                Cursor = Cursors.Default;
            }

            if (hadSelection && !string.IsNullOrEmpty(reason))
            {
                Logger.Info("区间统计已清除：" + reason + "。");
            }

            Invalidate();
        }

        private bool TryGetCandleIndexAtX(int x, out int candleIndex)
        {
            candleIndex = MissingCandleIndex;
            if (_candles.Count == EmptyCandleCount)
            {
                return false;
            }

            Rectangle plotArea;
            Rectangle priceArea;
            Rectangle volumeArea;
            GetChartAreas(ClientRectangle, out plotArea, out priceArea, out volumeArea);
            int visibleStart;
            int visibleCount;
            GetViewport(out visibleStart, out visibleCount);
            float candleStep = plotArea.Width / (float)Math.Max(1, visibleCount);
            int relativeIndex = Math.Max(
                default(int),
                Math.Min(visibleCount - 1, (int)Math.Floor((x - plotArea.Left) / candleStep)));
            candleIndex = visibleStart + relativeIndex;
            return candleIndex >= default(int) && candleIndex < _candles.Count;
        }

        private int FindCandleIndexByTimestamp(long timestamp)
        {
            int low = default(int);
            int high = _candles.Count - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                long middleTimestamp = _candles[middle].Timestamp;
                if (middleTimestamp == timestamp)
                {
                    return middle;
                }

                if (middleTimestamp < timestamp)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return MissingCandleIndex;
        }

        private void UpdateIntervalStatistics()
        {
            _intervalStatistics = null;
            if (!_intervalStartTimestamp.HasValue)
            {
                return;
            }

            int startIndex = FindCandleIndexByTimestamp(_intervalStartTimestamp.Value);
            if (startIndex == MissingCandleIndex)
            {
                ClearIntervalSelection("区间起点已不在当前 K 线数据中");
                return;
            }

            if (!_intervalEndTimestamp.HasValue)
            {
                return;
            }

            int endIndex = FindCandleIndexByTimestamp(_intervalEndTimestamp.Value);
            if (endIndex == MissingCandleIndex)
            {
                ClearIntervalSelection("区间终点已不在当前 K 线数据中");
                return;
            }

            int firstIndex = Math.Min(startIndex, endIndex);
            int lastIndex = Math.Max(startIndex, endIndex);
            Candle firstCandle = _candles[firstIndex];
            Candle lastCandle = _candles[lastIndex];
            decimal maximum = firstCandle.High;
            decimal minimum = firstCandle.Low;
            decimal totalVolume = decimal.Zero;
            for (int index = firstIndex; index <= lastIndex; index++)
            {
                Candle candle = _candles[index];
                maximum = Math.Max(maximum, candle.High);
                minimum = Math.Min(minimum, candle.Low);
                totalVolume += candle.Volume;
            }

            decimal changeValue = lastCandle.Close - firstCandle.Close;
            decimal changePercent = firstCandle.Close == decimal.Zero
                ? decimal.Zero
                : changeValue / firstCandle.Close * 100m;
            _intervalStatistics = new IntervalStatistics
            {
                CandleCount = lastIndex - firstIndex + 1,
                StartTime = GetDisplayTime(firstCandle),
                EndTime = GetDisplayTime(lastCandle),
                StartClose = firstCandle.Close,
                EndClose = lastCandle.Close,
                ChangeValue = changeValue,
                ChangePercent = changePercent,
                Maximum = maximum,
                Minimum = minimum,
                TotalVolume = totalVolume
            };
        }

        private bool TryGetVisibleIntervalBounds(out RectangleF bounds)
        {
            bounds = RectangleF.Empty;
            if (!_lastLayoutValid || !_intervalStartTimestamp.HasValue || !_intervalEndTimestamp.HasValue)
            {
                return false;
            }

            int startIndex = FindCandleIndexByTimestamp(_intervalStartTimestamp.Value);
            int endIndex = FindCandleIndexByTimestamp(_intervalEndTimestamp.Value);
            if (startIndex == MissingCandleIndex || endIndex == MissingCandleIndex)
            {
                return false;
            }

            int firstIndex = Math.Min(startIndex, endIndex);
            int lastIndex = Math.Max(startIndex, endIndex);
            int visibleEndIndex = _lastVisibleStart + _lastVisibleCount - 1;
            if (lastIndex < _lastVisibleStart || firstIndex > visibleEndIndex)
            {
                return false;
            }

            int clippedFirstIndex = Math.Max(firstIndex, _lastVisibleStart);
            int clippedLastIndex = Math.Min(lastIndex, visibleEndIndex);
            float candleStep = _lastPlotArea.Width / (float)Math.Max(1, _lastVisibleCount);
            float left = _lastPlotArea.Left + candleStep * (clippedFirstIndex - _lastVisibleStart);
            float right = _lastPlotArea.Left + candleStep * (clippedLastIndex - _lastVisibleStart + 1);
            bounds = new RectangleF(
                left,
                _lastPlotArea.Top,
                Math.Max(MinimumIntervalDrawingWidth, right - left),
                _lastPlotArea.Height);
            return true;
        }

        private bool IsPointInVisibleInterval(Point point)
        {
            RectangleF bounds;
            return _lastPlotArea.Contains(point)
                && TryGetVisibleIntervalBounds(out bounds)
                && bounds.Contains(point.X, point.Y);
        }

        private void DrawIntervalSelection(Graphics graphics)
        {
            if (!_lastLayoutValid)
            {
                return;
            }

            RectangleF intervalBounds;
            if (TryGetVisibleIntervalBounds(out intervalBounds))
            {
                using (SolidBrush fillBrush = new SolidBrush(IntervalFillColor))
                using (Pen borderPen = new Pen(IntervalColor, 1.5f))
                {
                    borderPen.DashStyle = DashStyle.Dash;
                    graphics.FillRectangle(fillBrush, intervalBounds);
                    graphics.DrawRectangle(
                        borderPen,
                        intervalBounds.Left,
                        intervalBounds.Top,
                        intervalBounds.Width,
                        Math.Max(MinimumIntervalDrawingWidth, intervalBounds.Height - MinimumIntervalDrawingWidth));
                }
            }

            if (!_isSelectingInterval || !_intervalStartTimestamp.HasValue)
            {
                return;
            }

            int startIndex = FindCandleIndexByTimestamp(_intervalStartTimestamp.Value);
            int visibleEndIndex = _lastVisibleStart + _lastVisibleCount - 1;
            if (startIndex < _lastVisibleStart || startIndex > visibleEndIndex)
            {
                return;
            }

            float candleStep = _lastPlotArea.Width / (float)Math.Max(1, _lastVisibleCount);
            float markerX = _lastPlotArea.Left
                + candleStep * (startIndex - _lastVisibleStart + 0.5f);
            using (Pen markerPen = new Pen(IntervalColor, 1.5f))
            using (SolidBrush labelBrush = new SolidBrush(IntervalColor))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                markerPen.DashStyle = DashStyle.Dash;
                graphics.DrawLine(markerPen, markerX, _lastPlotArea.Top, markerX, _lastPlotArea.Bottom);
                const float HintWidth = 154f;
                const float HintHeight = 22f;
                float hintLeft = Math.Max(
                    _lastPlotArea.Left,
                    Math.Min(_lastPlotArea.Right - HintWidth, markerX + 5f));
                RectangleF hintBounds = new RectangleF(hintLeft, _lastPlotArea.Top + 4f, HintWidth, HintHeight);
                graphics.FillRectangle(labelBrush, hintBounds);
                graphics.DrawString(_lineMode ? "点击另一数据点完成" : "点击另一根 K 线完成", _smallFont, textBrush, hintBounds);
            }
        }

        private void DrawIntervalTooltip(Graphics graphics)
        {
            if (!_intervalHovered || _intervalStatistics == null)
            {
                return;
            }

            float left = _hoverPoint.X + IntervalTooltipMargin;
            if (left + IntervalTooltipWidth > ClientSize.Width - IntervalTooltipMargin)
            {
                left = _hoverPoint.X - IntervalTooltipWidth - IntervalTooltipMargin;
            }

            left = Math.Max(
                IntervalTooltipMargin,
                Math.Min(ClientSize.Width - IntervalTooltipWidth - IntervalTooltipMargin, left));
            float top = _hoverPoint.Y + IntervalTooltipMargin;
            if (top + IntervalTooltipHeight > ClientSize.Height - IntervalTooltipMargin)
            {
                top = _hoverPoint.Y - IntervalTooltipHeight - IntervalTooltipMargin;
            }

            top = Math.Max(
                IntervalTooltipMargin,
                Math.Min(ClientSize.Height - IntervalTooltipHeight - IntervalTooltipMargin, top));
            RectangleF tooltipBounds = new RectangleF(left, top, IntervalTooltipWidth, IntervalTooltipHeight);
            Color changeColor = _intervalStatistics.ChangeValue >= decimal.Zero ? UpColor : DownColor;
            using (SolidBrush backgroundBrush = new SolidBrush(TooltipBackgroundColor))
            using (Pen borderPen = new Pen(Color.FromArgb(90, Color.White), 1f))
            using (SolidBrush primaryBrush = new SolidBrush(Color.White))
            using (SolidBrush secondaryBrush = new SolidBrush(Color.FromArgb(200, 210, 216, 230)))
            using (SolidBrush changeBrush = new SolidBrush(changeColor))
            using (StringFormat rowFormat = new StringFormat
            {
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                graphics.FillRectangle(backgroundBrush, tooltipBounds);
                graphics.DrawRectangle(
                    borderPen,
                    tooltipBounds.Left,
                    tooltipBounds.Top,
                    tooltipBounds.Width,
                    tooltipBounds.Height);
                float textLeft = tooltipBounds.Left + IntervalTooltipPadding;
                float textWidth = tooltipBounds.Width - IntervalTooltipPadding * 2;
                float textTop = tooltipBounds.Top + 7f;
                graphics.DrawString(
                    "区间统计",
                    _intervalTitleFont,
                    primaryBrush,
                    new RectangleF(textLeft, textTop, textWidth, IntervalTooltipTitleHeight),
                    rowFormat);
                textTop += IntervalTooltipTitleHeight;
                DrawIntervalTooltipRow(
                    graphics,
                    FormatIntervalTime(_intervalStatistics.StartTime) + "  →  "
                        + FormatIntervalTime(_intervalStatistics.EndTime),
                    secondaryBrush,
                    rowFormat,
                    textLeft,
                    textTop,
                    textWidth);
                textTop += IntervalTooltipLineHeight;
                DrawIntervalTooltipRow(
                    graphics,
                    _lineMode
                        ? "数据点数量  " + _intervalStatistics.CandleCount
                        : "K 线数量  " + _intervalStatistics.CandleCount + "    成交量  "
                            + FormatHelper.CompactNumber(_intervalStatistics.TotalVolume),
                    primaryBrush,
                    rowFormat,
                    textLeft,
                    textTop,
                    textWidth);
                textTop += IntervalTooltipLineHeight;
                DrawIntervalTooltipRow(
                    graphics,
                    (_lineMode ? "起始指数  " : "起始收盘  ") + FormatHelper.Price(_intervalStatistics.StartClose, _tickSize)
                        + (_lineMode ? "    结束指数  " : "    结束收盘  ") + FormatHelper.Price(_intervalStatistics.EndClose, _tickSize),
                    primaryBrush,
                    rowFormat,
                    textLeft,
                    textTop,
                    textWidth);
                textTop += IntervalTooltipLineHeight;
                DrawIntervalTooltipRow(
                    graphics,
                    _lineMode
                        ? "指数变动  " + FormatSignedPrice(_intervalStatistics.ChangeValue) + " 点"
                        : "涨跌值  " + FormatSignedPrice(_intervalStatistics.ChangeValue)
                            + "    涨跌幅  " + FormatSignedPercentage(_intervalStatistics.ChangePercent),
                    changeBrush,
                    rowFormat,
                    textLeft,
                    textTop,
                    textWidth);
                textTop += IntervalTooltipLineHeight;
                DrawIntervalTooltipRow(
                    graphics,
                    "最高值  " + FormatHelper.Price(_intervalStatistics.Maximum, _tickSize)
                        + "    最低值  " + FormatHelper.Price(_intervalStatistics.Minimum, _tickSize),
                    primaryBrush,
                    rowFormat,
                    textLeft,
                    textTop,
                    textWidth);
            }
        }

        private void DrawIntervalTooltipRow(
            Graphics graphics,
            string text,
            Brush brush,
            StringFormat format,
            float left,
            float top,
            float width)
        {
            graphics.DrawString(
                text,
                _smallFont,
                brush,
                new RectangleF(left, top, width, IntervalTooltipLineHeight),
                format);
        }

        internal void SetDetailsTransparency(int transparencyPercent)
        {
            int normalized = Math.Max(SettingsStore.MinimumCandleDetailsTransparencyPercent,
                Math.Min(SettingsStore.MaximumCandleDetailsTransparencyPercent, transparencyPercent));
            if (_detailsTransparency != normalized)
            {
                _detailsTransparency = normalized;
                Invalidate();
            }
        }

        private void ShowCandleDetails()
        {
            if (!_contextMenuCandleTimestamp.HasValue)
            {
                return;
            }
            int index = FindCandleIndexByTimestamp(_contextMenuCandleTimestamp.Value);
            if (index == MissingCandleIndex)
            {
                return;
            }
            _detailsCandle = _candles[index];
            _detailsInHistory = true;
            _detailsScrollOffset = default(int);
            _hoverVisible = false;
            _intervalHovered = false;
            Logger.Info((_lineMode ? "查看 CNN 指数详情：时间戳 " : "查看 K 线详情：时间戳 ")
                + _detailsCandle.Timestamp + " / 周期 " + _range.RestBar);
            Invalidate();
        }

        private void ClearCandleDetails(string reason)
        {
            if (_detailsCandle != null)
            {
                Logger.Info((_lineMode ? "关闭 CNN 指数详情：时间戳 " : "关闭 K 线详情：时间戳 ")
                    + _detailsCandle.Timestamp + " / " + reason);
            }
            _detailsCandle = null;
            _detailsInHistory = false;
            _detailsScrollOffset = default(int);
            Invalidate();
        }

        private Rectangle GetDetailsBounds()
        {
            int width = Math.Min(DetailsWidth, Math.Max(1, ClientSize.Width - DetailsMargin * 2));
            int preferredHeight = _lineMode
                ? DetailsTitleHeight + IndexDetailsRowCount * DetailsRowHeight + DetailsFooterHeight
                : DetailsHeight;
            int height = Math.Min(preferredHeight, Math.Max(1, ClientSize.Height - DetailsMargin * 2));
            int top = Math.Max(DetailsMargin,
                Math.Min(ChartHeaderHeight, ClientSize.Height - height - DetailsMargin));
            return new Rectangle(DetailsMargin, top, width, height);
        }

        private Rectangle GetDetailsCloseBounds()
        {
            Rectangle bounds = GetDetailsBounds();
            return new Rectangle(bounds.Right - DetailsCloseSize - DetailsMargin,
                bounds.Top + (DetailsTitleHeight - DetailsCloseSize) / 2, DetailsCloseSize, DetailsCloseSize);
        }

        private int GetDetailsRowCount()
        {
            return _lineMode ? IndexDetailsRowCount : DetailsRowCount;
        }

        private int GetDetailsVisibleRows()
        {
            return Math.Max(1, Math.Min(GetDetailsRowCount(),
                (GetDetailsBounds().Height - DetailsTitleHeight - DetailsFooterHeight) / DetailsRowHeight));
        }

        private string[] GetDetailsValues()
        {
            Candle candle = _detailsCandle;
            if (_lineMode)
            {
                return new[]
                {
                    GetDisplayTime(candle).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    FormatHelper.Price(candle.Close, _tickSize),
                    FearGreedInstrument.GetRating(candle.Close),
                    _displayTimeZone == DisplayTimeZone.UsEastern ? "美东时间" : "北京时间"
                };
            }

            decimal change = candle.Close - candle.Open;
            bool hasOpeningPrice = candle.Open != decimal.Zero;
            return new[]
            {
                GetDisplayTime(candle).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                FormatHelper.Price(candle.Open, _tickSize),
                FormatHelper.Price(candle.High, _tickSize),
                FormatHelper.Price(candle.Low, _tickSize),
                FormatHelper.Price(candle.Close, _tickSize),
                candle.Volume.ToString("0.############################", CultureInfo.InvariantCulture),
                FormatSignedPrice(change),
                hasOpeningPrice ? FormatSignedPercentage(change / candle.Open * FullPercentage) : "--",
                hasOpeningPrice ? ((candle.High - candle.Low) / candle.Open * FullPercentage)
                    .ToString("0.00", CultureInfo.InvariantCulture) + "%" : "--",
                _displayTimeZone == DisplayTimeZone.UsEastern ? "美东时间" : "北京时间"
            };
        }

        private void DrawCandleDetails(Graphics graphics)
        {
            if (_detailsCandle == null)
            {
                return;
            }
            Rectangle bounds = GetDetailsBounds();
            Rectangle closeBounds = GetDetailsCloseBounds();
            int visibleRows = GetDetailsVisibleRows();
            int rowCount = GetDetailsRowCount();
            _detailsScrollOffset = Math.Min(_detailsScrollOffset, rowCount - visibleRows);
            string[] labels = _lineMode
                ? new[] { "数据日期", "指数", "市场情绪", "时区" }
                : new[] { "开盘时间", "开盘价", "最高价", "最低价", "收盘价", "成交量", "涨跌值", "涨跌幅", "振幅", "时区" };
            string[] values = GetDetailsValues();
            const int ChangeValueRow = 6;
            const int ChangePercentRow = 7;
            const int ValueLeftOffset = 82;
            int alpha = FullOpacityAlpha * (FullPercentage - _detailsTransparency) / FullPercentage;
            using (Brush background = new SolidBrush(Color.FromArgb(alpha, Color.White)))
            using (Brush foreground = new SolidBrush(TextColor))
            using (Brush secondary = new SolidBrush(SecondaryTextColor))
            using (Brush changeBrush = new SolidBrush(_detailsCandle.Close >= _detailsCandle.Open ? UpColor : DownColor))
            using (Pen border = new Pen(Color.FromArgb(alpha, GridColor)))
            using (StringFormat format = new StringFormat { FormatFlags = StringFormatFlags.NoWrap })
            using (StringFormat closeFormat = new StringFormat
            {
                Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center
            })
            {
                graphics.FillRectangle(background, bounds);
                graphics.DrawRectangle(border, bounds);
                graphics.DrawString(_lineMode ? "CNN 指数详情 · 日频" : "K 线详情 · " + _range.PeriodLabel, _intervalTitleFont, foreground,
                    new RectangleF(bounds.Left + DetailsMargin, bounds.Top + 4,
                        bounds.Width - DetailsCloseSize - DetailsMargin * 3, DetailsTitleHeight), format);
                graphics.DrawString("×", _intervalTitleFont, foreground, closeBounds, closeFormat);
                for (int row = default(int); row < visibleRows; row++)
                {
                    int index = row + _detailsScrollOffset;
                    float top = bounds.Top + DetailsTitleHeight + row * DetailsRowHeight;
                    graphics.DrawString(labels[index], _smallFont, secondary,
                        new RectangleF(bounds.Left + DetailsMargin, top, ValueLeftOffset - DetailsMargin, DetailsRowHeight), format);
                    Brush valueBrush = index == ChangeValueRow || index == ChangePercentRow ? changeBrush : foreground;
                    graphics.DrawString(values[index], _smallFont, valueBrush,
                        new RectangleF(bounds.Left + ValueLeftOffset, top,
                            bounds.Width - ValueLeftOffset - DetailsMargin, DetailsRowHeight), format);
                }
                string hint = _lineMode ? "指数范围 0～100 · 来源 CNN" : "涨跌/振幅相对开盘";
                if (!_detailsInHistory)
                {
                    hint = "历史窗口外：保留最后数据";
                }
                if (visibleRows < rowCount)
                {
                    hint = "滚轮查看更多 · " + hint;
                }
                graphics.DrawString(hint, _axisFont, secondary,
                    new RectangleF(bounds.Left + DetailsMargin, bounds.Bottom - DetailsFooterHeight,
                        bounds.Width - DetailsMargin * 2, DetailsFooterHeight), format);
            }
        }

        private string FormatSignedPrice(decimal value)
        {
            string prefix = value > decimal.Zero ? "+" : string.Empty;
            return prefix + FormatHelper.Price(value, _tickSize);
        }

        private static string FormatSignedPercentage(decimal value)
        {
            string prefix = value > decimal.Zero ? "+" : string.Empty;
            return prefix + value.ToString("0.00", CultureInfo.InvariantCulture) + "%";
        }

        private string FormatIntervalTime(DateTime time)
        {
            if (_range.PeriodDurationMinutes >= OneMonthPeriodMinutes)
            {
                return time.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            }

            if (_range.PeriodDurationMinutes >= OneDayMinutes)
            {
                return time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private void EnsureChartLayer(Size size)
        {
            if (_chartLayer != null && _chartLayer.Size == size)
            {
                return;
            }

            if (_chartLayer != null)
            {
                _chartLayer.Dispose();
            }

            _chartLayer = new Bitmap(size.Width, size.Height);
            _chartLayerDirty = true;
        }

        private void DrawChartLayer(Graphics graphics, Rectangle client)
        {
            graphics.Clear(BackColor);
            graphics.SmoothingMode = SmoothingMode.None;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            _lastLayoutValid = false;

            if (_candles.Count == EmptyCandleCount)
            {
                DrawCenteredMessage(graphics, client, string.IsNullOrEmpty(_message) ? "暂无行情数据" : _message, _isError);
                return;
            }

            Rectangle plotArea;
            Rectangle priceArea;
            Rectangle volumeArea;
            GetChartAreas(client, out plotArea, out priceArea, out volumeArea);
            int visibleStart;
            int visibleCount;
            GetViewport(out visibleStart, out visibleCount);

            decimal minimum;
            decimal maximum;
            decimal maximumVolume;
            GetVisibleValueRange(visibleStart, visibleCount, out minimum, out maximum, out maximumVolume);
            if (maximum <= minimum)
            {
                maximum = minimum + (_tickSize > decimal.Zero ? _tickSize : 0.01m);
            }

            if (_lineMode)
            {
                minimum = MinimumIndexValue;
                maximum = MaximumIndexValue;
            }
            else
            {
                decimal pricePadding = (maximum - minimum) * 0.06m;
                minimum -= pricePadding;
                maximum += pricePadding;
            }

            _lastPlotArea = plotArea;
            _lastPriceArea = priceArea;
            _lastVisibleStart = visibleStart;
            _lastVisibleCount = visibleCount;
            _lastMinimum = minimum;
            _lastMaximum = maximum;
            _lastLayoutValid = true;

            DrawGrid(graphics, plotArea, priceArea, minimum, maximum);
            if (_lineMode)
            {
                DrawIndexLine(graphics, priceArea, minimum, maximum, visibleStart, visibleCount);
            }
            else
            {
                DrawCandles(graphics, priceArea, volumeArea, minimum, maximum, maximumVolume, visibleStart, visibleCount);
                DrawMovingAverages(graphics, priceArea, minimum, maximum, visibleStart, visibleCount);
            }
            DrawHeader(graphics, client);
            DrawTimeAxis(graphics, plotArea, visibleStart, visibleCount);
            DrawCurrentPrice(graphics, plotArea, priceArea, minimum, maximum);
        }

        private void GetChartAreas(
            Rectangle client,
            out Rectangle plotArea,
            out Rectangle priceArea,
            out Rectangle volumeArea)
        {
            plotArea = new Rectangle(
                LeftPadding,
                ChartHeaderHeight,
                Math.Max(1, client.Width - LeftPadding - (_lineMode ? IndexRightAxisWidth : RightAxisWidth) - RightPadding),
                Math.Max(1, client.Height - ChartHeaderHeight - BottomAxisHeight));
            int volumeHeight = _lineMode ? default(int) : Math.Max(34, plotArea.Height / 5);
            priceArea = new Rectangle(plotArea.Left, plotArea.Top, plotArea.Width, Math.Max(1, plotArea.Height - volumeHeight));
            volumeArea = new Rectangle(plotArea.Left, priceArea.Bottom, plotArea.Width, volumeHeight);
        }

        private void GetViewport(out int visibleStart, out int visibleCount)
        {
            visibleCount = _visibleCandleCount == FullViewportCandleCount
                ? _candles.Count
                : Math.Min(_visibleCandleCount, _candles.Count);
            visibleCount = Math.Max(1, visibleCount);
            int maximumOffset = Math.Max(NoViewportOffset, _candles.Count - visibleCount);
            int normalizedOffset = Math.Max(NoViewportOffset, Math.Min(maximumOffset, _rightOffset));
            visibleStart = Math.Max(default(int), _candles.Count - normalizedOffset - visibleCount);
        }

        private void NormalizeViewport()
        {
            if (_candles.Count == EmptyCandleCount)
            {
                _visibleCandleCount = FullViewportCandleCount;
                _rightOffset = NoViewportOffset;
                return;
            }

            if (_visibleCandleCount >= _candles.Count)
            {
                _visibleCandleCount = FullViewportCandleCount;
                _rightOffset = NoViewportOffset;
                return;
            }

            int visibleCount = _visibleCandleCount == FullViewportCandleCount
                ? _candles.Count
                : Math.Max(1, _visibleCandleCount);
            _rightOffset = Math.Max(
                NoViewportOffset,
                Math.Min(_candles.Count - visibleCount, _rightOffset));
        }

        private long GetRightmostVisibleTimestamp()
        {
            if (_candles.Count == EmptyCandleCount || _rightOffset == NoViewportOffset)
            {
                return default(long);
            }

            int visibleStart;
            int visibleCount;
            GetViewport(out visibleStart, out visibleCount);
            return _candles[visibleStart + visibleCount - 1].Timestamp;
        }

        private void RestoreRightOffset(long rightmostVisibleTimestamp)
        {
            int low = default(int);
            int high = _candles.Count - 1;
            int matchedIndex = MissingCandleIndex;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                if (_candles[middle].Timestamp <= rightmostVisibleTimestamp)
                {
                    matchedIndex = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            _rightOffset = matchedIndex == MissingCandleIndex
                ? Math.Max(NoViewportOffset, _candles.Count - Math.Max(1, _visibleCandleCount))
                : _candles.Count - matchedIndex - 1;
        }

        private void GetVisibleValueRange(
            int visibleStart,
            int visibleCount,
            out decimal minimum,
            out decimal maximum,
            out decimal maximumVolume)
        {
            Candle first = _candles[visibleStart];
            minimum = first.Low;
            maximum = first.High;
            maximumVolume = first.Volume;
            int visibleEnd = visibleStart + visibleCount;
            for (int index = visibleStart + 1; index < visibleEnd; index++)
            {
                Candle candle = _candles[index];
                minimum = Math.Min(minimum, candle.Low);
                maximum = Math.Max(maximum, candle.High);
                maximumVolume = Math.Max(maximumVolume, candle.Volume);
            }
        }

        private void DrawGrid(Graphics graphics, Rectangle plotArea, Rectangle priceArea, decimal minimum, decimal maximum)
        {
            if (_lineMode)
            {
                DrawIndexLevels(graphics, plotArea, priceArea, minimum, maximum);
            }

            using (Pen gridPen = new Pen(GridColor, 1f))
            using (SolidBrush axisBrush = new SolidBrush(SecondaryTextColor))
            using (StringFormat nearFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                gridPen.DashStyle = DashStyle.Dot;
                const int HorizontalLines = 5;
                for (int index = 0; !_lineMode && index <= HorizontalLines; index++)
                {
                    float ratio = index / (float)HorizontalLines;
                    int y = priceArea.Top + (int)Math.Round(priceArea.Height * ratio);
                    graphics.DrawLine(gridPen, plotArea.Left, y, plotArea.Right, y);
                    decimal price = maximum - (maximum - minimum) * (decimal)ratio;
                    string label = FormatHelper.Price(price, _tickSize);
                    graphics.DrawString(label, _axisFont, axisBrush, new RectangleF(plotArea.Right + 5, y - 9, 55, 18), nearFormat);
                }

                const int VerticalLines = 4;
                for (int index = 0; index <= VerticalLines; index++)
                {
                    int x = plotArea.Left + (int)Math.Round(plotArea.Width * index / (float)VerticalLines);
                    graphics.DrawLine(gridPen, x, plotArea.Top, x, plotArea.Bottom);
                }
            }
        }

        /// <summary>Draws CNN's fixed sentiment bands, boundary waterlines and right axis labels.</summary>
        /// <param name="graphics">The chart layer drawing surface.</param>
        /// <param name="plotArea">The visible plot bounds.</param>
        /// <param name="priceArea">The fixed zero-to-one-hundred index area.</param>
        /// <param name="minimum">The index axis minimum.</param>
        /// <param name="maximum">The index axis maximum.</param>
        /// <returns>No value.</returns>
        private void DrawIndexLevels(Graphics graphics, Rectangle plotArea, Rectangle priceArea, decimal minimum, decimal maximum)
        {
            FearGreedBand[] bands = FearGreedInstrument.Bands;
            bool alignLabels = priceArea.Height >= IndexAlignedAxisMinimumHeight;
            using (StringFormat centered = new StringFormat(StringFormat.GenericTypographic)
            {
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip
            })
            using (StringFormat compact = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip
            })
            {
                foreach (FearGreedBand band in bands)
                {
                    float top = PriceToY(band.Maximum, priceArea, minimum, maximum);
                    float bottom = PriceToY(band.Minimum, priceArea, minimum, maximum);
                    using (Brush background = new SolidBrush(Color.FromArgb(IndexBandFillAlpha, band.Color)))
                    using (Brush edge = new SolidBrush(band.Color))
                    {
                        graphics.FillRectangle(background, plotArea.Left, top, plotArea.Width, bottom - top);
                        graphics.FillRectangle(edge, plotArea.Right, top, 3f, Math.Max(1f, bottom - top));
                    }

                    DrawIndexWaterline(graphics, plotArea, priceArea, band.Minimum, minimum, maximum, alignLabels, compact);
                    if (alignLabels)
                    {
                        using (Brush text = new SolidBrush(band.Color))
                        {
                            graphics.DrawString(band.Label, _indexLabelFont, text,
                                new RectangleF(plotArea.Right + IndexAxisLabelOffset,
                                    (top + bottom - IndexLegendRowHeight) / 2f,
                                    IndexRightAxisWidth - IndexAxisLabelOffset, IndexLegendRowHeight), centered);
                        }
                    }
                }

                DrawIndexWaterline(graphics, plotArea, priceArea, maximum, minimum, maximum, alignLabels, compact);
                if (!alignLabels)
                {
                    int legendHeight = bands.Length * IndexCompactLegendRowHeight;
                    int legendTop = Math.Max(0, (ClientSize.Height - legendHeight) / 2);
                    for (int index = default(int); index < bands.Length; index++)
                    {
                        FearGreedBand band = bands[bands.Length - index - 1];
                        int rowTop = legendTop + index * IndexCompactLegendRowHeight;
                        float left = plotArea.Right + IndexLegendMargin;
                        using (Brush text = new SolidBrush(band.Color))
                        {
                            graphics.FillRectangle(text, left + 1, rowTop + 4, 2, 16);
                            graphics.DrawString(band.Label, _indexCompactLabelFont, text,
                                new RectangleF(left + 5, rowTop,
                                    IndexRightAxisWidth - IndexLegendMargin - 6, IndexCompactLabelHeight), compact);
                            string range = band.Minimum.ToString("0", CultureInfo.InvariantCulture)
                                + "–" + band.Maximum.ToString("0", CultureInfo.InvariantCulture);
                            graphics.DrawString(range, _indexCompactRangeFont, text,
                                new RectangleF(left + 5, rowTop + IndexCompactLabelHeight,
                                    IndexRightAxisWidth - IndexLegendMargin - 6, IndexCompactRangeHeight), compact);
                        }
                    }
                }
            }
        }

        /// <summary>Draws a horizontal sentiment boundary at its real index value.</summary>
        /// <param name="graphics">The chart layer drawing surface.</param>
        /// <param name="plotArea">The visible plot bounds.</param>
        /// <param name="priceArea">The fixed index plotting area.</param>
        /// <param name="value">The boundary's index value.</param>
        /// <param name="minimum">The index axis minimum.</param>
        /// <param name="maximum">The index axis maximum.</param>
        /// <param name="showNumber">Whether the axis has enough height for aligned boundary numbers.</param>
        /// <param name="format">The boundary label alignment.</param>
        /// <returns>No value.</returns>
        private void DrawIndexWaterline(Graphics graphics, Rectangle plotArea, Rectangle priceArea,
            decimal value, decimal minimum, decimal maximum, bool showNumber, StringFormat format)
        {
            float y = PriceToY(value, priceArea, minimum, maximum);
            Color color = FearGreedInstrument.GetColor(value);
            using (Pen pen = new Pen(Color.FromArgb(IndexThresholdAlpha, color), 1f))
            using (Brush text = new SolidBrush(color))
            {
                pen.DashStyle = DashStyle.Dash;
                graphics.DrawLine(pen, plotArea.Left, y, plotArea.Right, y);
                if (showNumber)
                {
                    graphics.DrawString(value.ToString("0", CultureInfo.InvariantCulture), _indexAxisFont, text,
                        new RectangleF(plotArea.Right + IndexAxisNumberLeft, y - IndexLegendRowHeight / 2f,
                            IndexAxisNumberWidth, IndexLegendRowHeight), format);
                }
            }
        }

        private void DrawCandles(
            Graphics graphics,
            Rectangle priceArea,
            Rectangle volumeArea,
            decimal minimum,
            decimal maximum,
            decimal maximumVolume,
            int visibleStart,
            int visibleCount)
        {
            float step = priceArea.Width / (float)Math.Max(1, visibleCount);
            float bodyWidth = Math.Max(1f, Math.Min(7f, step * 0.66f));
            using (Pen upPen = new Pen(UpColor, 1f))
            using (Pen downPen = new Pen(DownColor, 1f))
            using (SolidBrush upBrush = new SolidBrush(UpColor))
            using (SolidBrush downBrush = new SolidBrush(DownColor))
            using (SolidBrush upVolume = new SolidBrush(Color.FromArgb(80, UpColor)))
            using (SolidBrush downVolume = new SolidBrush(Color.FromArgb(80, DownColor)))
            {
                int visibleEnd = visibleStart + visibleCount;
                for (int index = visibleStart; index < visibleEnd; index++)
                {
                    Candle candle = _candles[index];
                    bool rising = candle.Close >= candle.Open;
                    Pen pen = rising ? upPen : downPen;
                    Brush bodyBrush = rising ? upBrush : downBrush;
                    float x = priceArea.Left + step * (index - visibleStart + 0.5f);
                    float highY = PriceToY(candle.High, priceArea, minimum, maximum);
                    float lowY = PriceToY(candle.Low, priceArea, minimum, maximum);
                    float openY = PriceToY(candle.Open, priceArea, minimum, maximum);
                    float closeY = PriceToY(candle.Close, priceArea, minimum, maximum);
                    graphics.DrawLine(pen, x, highY, x, lowY);
                    float top = Math.Min(openY, closeY);
                    float height = Math.Max(1f, Math.Abs(closeY - openY));
                    graphics.FillRectangle(bodyBrush, x - bodyWidth / 2f, top, bodyWidth, height);

                    if (maximumVolume > decimal.Zero && candle.Volume > decimal.Zero)
                    {
                        float volumeRatio = (float)(candle.Volume / maximumVolume);
                        float volumeBarHeight = Math.Max(1f, volumeArea.Height * volumeRatio);
                        graphics.FillRectangle(
                            rising ? upVolume : downVolume,
                            x - bodyWidth / 2f,
                            volumeArea.Bottom - volumeBarHeight,
                            bodyWidth,
                            volumeBarHeight);
                    }
                }
            }
        }

        private void DrawIndexLine(
            Graphics graphics,
            Rectangle priceArea,
            decimal minimum,
            decimal maximum,
            int visibleStart,
            int visibleCount)
        {
            float step = priceArea.Width / (float)Math.Max(SingleDataPointCount, visibleCount);
            SmoothingMode previousSmoothingMode = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Dictionary<int, Pen> pens = new Dictionary<int, Pen>();
            try
            {
                foreach (FearGreedBand band in FearGreedInstrument.Bands)
                {
                    pens[band.Color.ToArgb()] = new Pen(band.Color, IndexLineWidth);
                }

                if (visibleCount > SingleDataPointCount)
                {
                    for (int index = SingleDataPointCount; index < visibleCount; index++)
                    {
                        decimal startValue = _candles[visibleStart + index - 1].Close;
                        decimal endValue = _candles[visibleStart + index].Close;
                        PointF start = new PointF(priceArea.Left + step * (index - 0.5f),
                            PriceToY(startValue, priceArea, minimum, maximum));
                        PointF end = new PointF(priceArea.Left + step * (index + 0.5f),
                            PriceToY(endValue, priceArea, minimum, maximum));
                        foreach (IndexLineSegment segment in SplitIndexLine(start, end, startValue, endValue))
                        {
                            graphics.DrawLine(pens[segment.Color.ToArgb()], segment.Start, segment.End);
                        }
                    }
                }
                else
                {
                    decimal value = _candles[visibleStart].Close;
                    PointF point = new PointF(priceArea.Left + step * 0.5f, PriceToY(value, priceArea, minimum, maximum));
                    using (Brush brush = new SolidBrush(FearGreedInstrument.GetColor(value)))
                    {
                        graphics.FillEllipse(brush, point.X - IndexPointRadius, point.Y - IndexPointRadius,
                            IndexPointRadius * 2f, IndexPointRadius * 2f);
                    }
                }
            }
            finally
            {
                foreach (Pen pen in pens.Values)
                {
                    pen.Dispose();
                }

                graphics.SmoothingMode = previousSmoothingMode;
            }
        }

        /// <summary>Splits a straight index edge at every crossed sentiment boundary.</summary>
        /// <param name="start">The first screen coordinate.</param>
        /// <param name="end">The second screen coordinate.</param>
        /// <param name="startValue">The first unrounded index value.</param>
        /// <param name="endValue">The second unrounded index value.</param>
        /// <returns>Continuous segments whose colors match their complete value interval.</returns>
        private static List<IndexLineSegment> SplitIndexLine(PointF start, PointF end, decimal startValue, decimal endValue)
        {
            List<decimal> cuts = new List<decimal> { decimal.Zero, decimal.One };
            decimal difference = endValue - startValue;
            decimal lower = Math.Min(startValue, endValue);
            decimal upper = Math.Max(startValue, endValue);
            foreach (decimal threshold in FearGreedInstrument.Thresholds)
            {
                if (threshold > lower && threshold < upper)
                {
                    cuts.Add((threshold - startValue) / difference);
                }
            }

            cuts.Sort();
            List<IndexLineSegment> segments = new List<IndexLineSegment>(cuts.Count - 1);
            for (int index = SingleDataPointCount; index < cuts.Count; index++)
            {
                decimal from = cuts[index - 1];
                decimal to = cuts[index];
                decimal midpoint = startValue + difference * (from + to) / 2m;
                segments.Add(new IndexLineSegment
                {
                    Start = new PointF(start.X + (end.X - start.X) * (float)from, start.Y + (end.Y - start.Y) * (float)from),
                    End = new PointF(start.X + (end.X - start.X) * (float)to, start.Y + (end.Y - start.Y) * (float)to),
                    Color = FearGreedInstrument.GetColor(midpoint)
                });
            }

            return segments;
        }

        private void DrawHeader(Graphics graphics, Rectangle client)
        {
            Candle latest = _candles[_candles.Count - 1];
            if (_lineMode)
            {
                decimal current = _snapshot == null ? latest.Close : _snapshot.LastPrice;
                using (Brush brush = new SolidBrush(FearGreedInstrument.GetColor(current)))
                using (Brush secondary = new SolidBrush(SecondaryTextColor))
                {
                    string headline = "CNN 指数 " + FormatHelper.Price(current, _tickSize)
                        + "  " + FearGreedInstrument.GetRating(current);
                    graphics.DrawString(headline, _smallFont, brush,
                        new RectangleF(8, 2, Math.Max(1, client.Width - IndexRightAxisWidth - LeftPadding - RightPadding), 20));
                    graphics.DrawString("日频折线 · 分级水位 · 来源 CNN", _smallFont, secondary,
                        new RectangleF(8, 22, Math.Max(1, client.Width - IndexRightAxisWidth - LeftPadding - RightPadding), 20));
                }
                return;
            }
            Color changeColor = latest.Close >= latest.Open ? UpColor : DownColor;
            string ohlc = "开 " + FormatHelper.Price(latest.Open, _tickSize)
                + "  高 " + FormatHelper.Price(latest.High, _tickSize)
                + "  低 " + FormatHelper.Price(latest.Low, _tickSize)
                + "  收 " + FormatHelper.Price(latest.Close, _tickSize)
                + "  量 " + FormatHelper.CompactNumber(latest.Volume);
            using (SolidBrush brush = new SolidBrush(changeColor))
            {
                graphics.DrawString(ohlc, _smallFont, brush, new RectangleF(8, 2, Math.Max(1, client.Width - 16), 20));
            }

            float left = 8f;
            foreach (int period in _movingAverages)
            {
                decimal average;
                if (!TryGetLatestMovingAverage(period, out average))
                {
                    continue;
                }

                string label = "MA" + period + " " + FormatHelper.Price(average, _tickSize);
                Color color = GetMovingAverageColor(period);
                using (SolidBrush averageBrush = new SolidBrush(color))
                {
                    SizeF size = graphics.MeasureString(label, _axisFont);
                    if (left + size.Width > client.Width - 8)
                    {
                        break;
                    }

                    graphics.DrawString(label, _axisFont, averageBrush, left, 22f);
                    left += size.Width + 10f;
                }
            }
        }

        private void DrawMovingAverages(
            Graphics graphics,
            Rectangle priceArea,
            decimal minimum,
            decimal maximum,
            int visibleStart,
            int visibleCount)
        {
            float step = priceArea.Width / (float)Math.Max(1, visibleCount);
            int visibleEnd = visibleStart + visibleCount;
            foreach (int period in _movingAverages)
            {
                if (_candles.Count < period)
                {
                    continue;
                }

                decimal rollingTotal = decimal.Zero;
                PointF? previous = null;
                int calculationStart = Math.Max(default(int), visibleStart - period + 1);
                using (Pen pen = new Pen(GetMovingAverageColor(period), 1.25f))
                {
                    pen.LineJoin = LineJoin.Round;
                    for (int index = calculationStart; index < visibleEnd; index++)
                    {
                        rollingTotal += _candles[index].Close;
                        if (index - calculationStart >= period)
                        {
                            rollingTotal -= _candles[index - period].Close;
                        }

                        if (index < period - 1 || index < visibleStart)
                        {
                            continue;
                        }

                        decimal average = rollingTotal / period;
                        PointF current = new PointF(
                            priceArea.Left + step * (index - visibleStart + 0.5f),
                            PriceToY(average, priceArea, minimum, maximum));
                        if (previous.HasValue)
                        {
                            graphics.DrawLine(pen, previous.Value, current);
                        }

                        previous = current;
                    }
                }
            }
        }

        private bool TryGetLatestMovingAverage(int period, out decimal average)
        {
            average = decimal.Zero;
            if (_candles.Count < period)
            {
                return false;
            }

            decimal total = decimal.Zero;
            for (int index = _candles.Count - period; index < _candles.Count; index++)
            {
                total += _candles[index].Close;
            }

            average = total / period;
            return true;
        }

        private static Color GetMovingAverageColor(int period)
        {
            switch (period)
            {
                case 5:
                    return Color.FromArgb(41, 98, 255);
                case 10:
                    return Color.FromArgb(255, 109, 0);
                case 20:
                    return Color.FromArgb(156, 39, 176);
                case 50:
                    return Color.FromArgb(0, 137, 123);
                case 100:
                    return Color.FromArgb(121, 85, 72);
                case 200:
                    return Color.FromArgb(213, 0, 0);
                default:
                    return SecondaryTextColor;
            }
        }

        private void DrawTimeAxis(Graphics graphics, Rectangle plotArea, int visibleStart, int visibleCount)
        {
            if (_candles.Count == EmptyCandleCount)
            {
                return;
            }

            if (_lineMode)
            {
                DrawIndexTimeAxis(graphics, plotArea, visibleStart, visibleCount);
                return;
            }

            bool showSessionBands = ShouldDrawUsMarketSessionBands();
            if (showSessionBands)
            {
                DrawUsMarketSessionBands(graphics, plotArea, visibleStart, visibleCount);
            }

            float timeLabelTop = plotArea.Bottom + (showSessionBands
                ? TimeLabelTopWithSessionBand
                : TimeLabelTopWithoutSessionBand);
            using (SolidBrush brush = new SolidBrush(SecondaryTextColor))
            using (StringFormat centerFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                const int Labels = 4;
                for (int index = 0; index <= Labels; index++)
                {
                    int candleIndex = visibleStart + Math.Min(
                        visibleCount - 1,
                        (int)Math.Round((visibleCount - 1) * index / (float)Labels));
                    float x = plotArea.Left + plotArea.Width * index / (float)Labels;
                    DateTime time = GetDisplayTime(_candles[candleIndex]);
                    string label;
                    if (_range.PeriodDurationMinutes >= OneMonthPeriodMinutes)
                    {
                        label = time.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                    }
                    else if (_range.IsAllHistory || _range.DurationMinutes > OneYearMinutes)
                    {
                        label = time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    }
                    else if (_range.DurationMinutes <= OneDayMinutes)
                    {
                        label = time.ToString("HH:mm", CultureInfo.InvariantCulture);
                    }
                    else if (_range.DurationMinutes <= FiveDayMinutes)
                    {
                        label = time.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        label = time.ToString("MM-dd", CultureInfo.InvariantCulture);
                    }

                    graphics.DrawString(label, _axisFont, brush, new RectangleF(x - 38, timeLabelTop, 76, 20), centerFormat);
                }
            }
        }

        /// <summary>Fits complete CNN date labels inside the plot without overlapping adjacent labels.</summary>
        /// <param name="graphics">The chart layer drawing surface.</param>
        /// <param name="plotArea">The plot bounds used to contain all date labels.</param>
        /// <param name="visibleStart">The first visible daily observation.</param>
        /// <param name="visibleCount">The number of visible daily observations.</param>
        /// <returns>No value.</returns>
        private void DrawIndexTimeAxis(Graphics graphics, Rectangle plotArea, int visibleStart, int visibleCount)
        {
            string dateFormat = _range.IsAllHistory || _range.DurationMinutes > OneYearMinutes
                ? "yyyy-MM-dd" : "MM-dd";
            float labelWidth = 0f;
            for (int index = visibleStart; index < visibleStart + visibleCount; index++)
            {
                string date = GetDisplayTime(_candles[index]).ToString(dateFormat, CultureInfo.InvariantCulture);
                labelWidth = Math.Max(labelWidth, graphics.MeasureString(date, _axisFont).Width);
            }

            labelWidth = Math.Min(plotArea.Width, (float)Math.Ceiling(labelWidth) + IndexTimeLabelPadding);
            int labelCount = Math.Min(visibleCount, Math.Min(MaximumIndexTimeLabels,
                Math.Max(1, (int)((plotArea.Width + IndexTimeLabelGap) / (labelWidth + IndexTimeLabelGap)))));
            using (Brush brush = new SolidBrush(SecondaryTextColor))
            using (StringFormat format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                for (int index = default(int); index < labelCount; index++)
                {
                    float fraction = labelCount == SingleDataPointCount ? 0.5f : index / (float)(labelCount - 1);
                    int candleIndex = visibleStart + (int)Math.Round((visibleCount - 1) * fraction);
                    string date = GetDisplayTime(_candles[candleIndex]).ToString(dateFormat, CultureInfo.InvariantCulture);
                    float left = plotArea.Left + (plotArea.Width - labelWidth) * fraction;
                    graphics.DrawString(date, _axisFont, brush,
                        new RectangleF(left, plotArea.Bottom + TimeLabelTopWithoutSessionBand,
                            labelWidth, CrosshairLabelHeight), format);
                }
            }
        }

        private bool ShouldDrawUsMarketSessionBands()
        {
            return !_lineMode
                && _displayTimeZone == DisplayTimeZone.UsEastern
                && _range != null
                && _range.PeriodDurationMinutes > default(long)
                && _range.PeriodDurationMinutes < OneDayMinutes;
        }

        private void DrawUsMarketSessionBands(
            Graphics graphics,
            Rectangle plotArea,
            int visibleStart,
            int visibleCount)
        {
            if (!ShouldDrawUsMarketSessionBands() || visibleCount <= EmptyCandleCount)
            {
                return;
            }

            float candleStep = plotArea.Width / (float)Math.Max(1, visibleCount);
            DateTime nextBoundaryUtc = DateTime.MinValue;
            DateTime previousEndUtc = DateTime.MinValue;
            UsMarketSession currentSession = UsMarketSession.Overnight;
            UsMarketSession bandSession = currentSession;
            float bandLeft = plotArea.Left;
            float bandRight = bandLeft;
            bool hasBand = false;
            using (SolidBrush overnightBrush = new SolidBrush(OvernightSessionColor))
            using (SolidBrush preMarketBrush = new SolidBrush(PreMarketSessionColor))
            using (SolidBrush regularMarketBrush = new SolidBrush(RegularMarketSessionColor))
            using (SolidBrush afterHoursBrush = new SolidBrush(AfterHoursSessionColor))
            using (SolidBrush textBrush = new SolidBrush(SessionTextColor))
            using (Pen separatorPen = new Pen(SessionSeparatorColor, 1f))
            using (StringFormat centerFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            })
            {
                GraphicsState state = graphics.Save();
                try
                {
                    graphics.SetClip(new RectangleF(
                        plotArea.Left,
                        plotArea.Bottom + SessionBandTopOffset,
                        plotArea.Width,
                        SessionBandHeight), CombineMode.Intersect);
                    for (int relativeIndex = 0; relativeIndex < visibleCount; relativeIndex++)
                    {
                        DateTime candleStartUtc = DisplayTimeConverter.FromUnixMillisecondsUtc(
                            _candles[visibleStart + relativeIndex].Timestamp);
                        DateTime candleEndUtc = candleStartUtc.AddMinutes(_range.PeriodDurationMinutes);
                        DateTime segmentStartUtc = candleStartUtc;
                        while (segmentStartUtc < candleEndUtc)
                        {
                            if (segmentStartUtc >= nextBoundaryUtc)
                            {
                                DateTime easternTime = DisplayTimeConverter.FromUtc(
                                    segmentStartUtc, DisplayTimeZone.UsEastern);
                                currentSession = GetUsMarketSession(easternTime);
                                nextBoundaryUtc = GetNextUsMarketSessionBoundaryUtc(easternTime, currentSession);
                            }

                            DateTime segmentEndUtc = nextBoundaryUtc < candleEndUtc
                                ? nextBoundaryUtc
                                : candleEndUtc;
                            float segmentLeft = plotArea.Left + candleStep * (relativeIndex
                                + (float)((segmentStartUtc - candleStartUtc).TotalMinutes / _range.PeriodDurationMinutes));
                            float segmentRight = plotArea.Left + candleStep * (relativeIndex
                                + (float)((segmentEndUtc - candleStartUtc).TotalMinutes / _range.PeriodDurationMinutes));
                            if (hasBand && (bandSession != currentSession || previousEndUtc != segmentStartUtc))
                            {
                                DrawUsMarketSessionBand(graphics, plotArea, bandLeft, bandRight, bandSession,
                                    GetSessionBrush(bandSession, overnightBrush, preMarketBrush, regularMarketBrush, afterHoursBrush),
                                    textBrush, separatorPen, centerFormat);
                                hasBand = false;
                            }

                            if (!hasBand)
                            {
                                bandLeft = segmentLeft;
                                bandSession = currentSession;
                                hasBand = true;
                            }

                            bandRight = segmentRight;
                            previousEndUtc = segmentEndUtc;
                            segmentStartUtc = segmentEndUtc;
                        }
                    }

                    if (hasBand)
                    {
                        DrawUsMarketSessionBand(graphics, plotArea, bandLeft, bandRight, bandSession,
                            GetSessionBrush(bandSession, overnightBrush, preMarketBrush, regularMarketBrush, afterHoursBrush),
                            textBrush, separatorPen, centerFormat);
                    }
                }
                finally
                {
                    graphics.Restore(state);
                }
            }
        }

        private void DrawUsMarketSessionBand(
            Graphics graphics,
            Rectangle plotArea,
            float left,
            float right,
            UsMarketSession session,
            Brush backgroundBrush,
            Brush textBrush,
            Pen separatorPen,
            StringFormat centerFormat)
        {
            RectangleF band = new RectangleF(left, plotArea.Bottom + SessionBandTopOffset,
                right - left, SessionBandHeight);
            graphics.FillRectangle(backgroundBrush, band);
            if (band.Width >= SessionSeparatorMinimumWidth)
            {
                graphics.DrawLine(separatorPen, left, band.Top, left, band.Bottom);
            }

            if (band.Width >= SessionLabelMinimumWidth)
            {
                graphics.DrawString(GetSessionLabel(session), _smallFont, textBrush, band, centerFormat);
            }
        }

        private static DateTime GetNextUsMarketSessionBoundaryUtc(DateTime easternTime, UsMarketSession session)
        {
            int boundaryMinutes;
            DateTime boundaryDate = easternTime.Date;
            switch (session)
            {
                case UsMarketSession.PreMarket:
                    boundaryMinutes = RegularMarketStartMinutes;
                    break;
                case UsMarketSession.RegularMarket:
                    boundaryMinutes = AfterHoursStartMinutes;
                    break;
                case UsMarketSession.AfterHours:
                    boundaryMinutes = OvernightStartMinutes;
                    break;
                default:
                    boundaryMinutes = PreMarketStartMinutes;
                    if (easternTime.TimeOfDay.TotalMinutes >= OvernightStartMinutes)
                    {
                        boundaryDate = boundaryDate.AddDays(1);
                    }
                    break;
            }

            return DisplayTimeConverter.ToUtc(boundaryDate.AddMinutes(boundaryMinutes), DisplayTimeZone.UsEastern);
        }

        private static Brush GetSessionBrush(
            UsMarketSession session,
            Brush overnightBrush,
            Brush preMarketBrush,
            Brush regularMarketBrush,
            Brush afterHoursBrush)
        {
            switch (session)
            {
                case UsMarketSession.PreMarket:
                    return preMarketBrush;
                case UsMarketSession.RegularMarket:
                    return regularMarketBrush;
                case UsMarketSession.AfterHours:
                    return afterHoursBrush;
                default:
                    return overnightBrush;
            }
        }

        private static string GetSessionLabel(UsMarketSession session)
        {
            switch (session)
            {
                case UsMarketSession.PreMarket:
                    return "盘前";
                case UsMarketSession.RegularMarket:
                    return "盘中";
                case UsMarketSession.AfterHours:
                    return "盘后";
                default:
                    return "夜盘";
            }
        }

        private static UsMarketSession GetUsMarketSession(DateTime easternTime)
        {
            int minutes = easternTime.Hour * MinutesPerHour + easternTime.Minute;
            if (minutes >= OvernightStartMinutes || minutes < PreMarketStartMinutes)
            {
                return UsMarketSession.Overnight;
            }

            if (minutes < RegularMarketStartMinutes)
            {
                return UsMarketSession.PreMarket;
            }

            if (minutes < AfterHoursStartMinutes)
            {
                return UsMarketSession.RegularMarket;
            }

            return UsMarketSession.AfterHours;
        }

        private DateTime GetDisplayTime(Candle candle)
        {
            DateTime displayTime;
            if (!_displayTimeCache.TryGetValue(candle.Timestamp, out displayTime))
            {
                displayTime = DisplayTimeConverter.FromUnixMilliseconds(candle.Timestamp, _displayTimeZone);
                _displayTimeCache[candle.Timestamp] = displayTime;
            }

            return displayTime;
        }

        private void DrawCurrentPrice(
            Graphics graphics,
            Rectangle plotArea,
            Rectangle priceArea,
            decimal minimum,
            decimal maximum)
        {
            if (_rightOffset != NoViewportOffset)
            {
                return;
            }

            decimal current = _snapshot != null && (_lineMode || _snapshot.LastPrice > decimal.Zero)
                ? _snapshot.LastPrice
                : _candles[_candles.Count - 1].Close;
            bool rising = _snapshot == null || _snapshot.Open24Hours == decimal.Zero || current >= _snapshot.Open24Hours;
            Color color = _lineMode ? FearGreedInstrument.GetColor(current) : rising ? UpColor : DownColor;
            float y = PriceToY(current, priceArea, minimum, maximum);
            using (Pen pen = new Pen(color, 1f))
            using (SolidBrush brush = new SolidBrush(color))
            using (SolidBrush whiteBrush = new SolidBrush(Color.White))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                pen.DashStyle = DashStyle.Dot;
                graphics.DrawLine(pen, plotArea.Left, y, plotArea.Right, y);
                RectangleF tag = _lineMode
                    ? new RectangleF(plotArea.Right - IndexCurrentTagWidth - 3,
                        Math.Max(priceArea.Top, Math.Min(priceArea.Bottom - CrosshairLabelHeight, y - CrosshairLabelHeight / 2f)),
                        IndexCurrentTagWidth, CrosshairLabelHeight)
                    : new RectangleF(plotArea.Right + 2, y - 10, 59, 20);
                graphics.FillRectangle(brush, tag);
                graphics.DrawString(FormatHelper.Price(current, _tickSize), _axisFont, whiteBrush, tag, format);
            }
        }

        private void DrawCrosshair(Graphics graphics)
        {
            if (!_hoverVisible || !_lastLayoutValid || !_lastPriceArea.Contains(_hoverPoint))
            {
                return;
            }

            float candleStep = _lastPlotArea.Width / (float)Math.Max(1, _lastVisibleCount);
            int relativeIndex = Math.Max(
                default(int),
                Math.Min(
                    _lastVisibleCount - 1,
                    (int)Math.Floor((_hoverPoint.X - _lastPlotArea.Left) / candleStep)));
            int candleIndex = _lastVisibleStart + relativeIndex;
            float crosshairX = _lastPlotArea.Left + candleStep * (relativeIndex + 0.5f);
            float crosshairY = Math.Max(_lastPriceArea.Top, Math.Min(_lastPriceArea.Bottom, _hoverPoint.Y));
            decimal priceRatio = (decimal)(crosshairY - _lastPriceArea.Top) / Math.Max(1, _lastPriceArea.Height);
            decimal price = _lastMaximum - (_lastMaximum - _lastMinimum) * priceRatio;
            string priceLabel = FormatHelper.Price(price, _tickSize);
            string timeLabel = FormatCrosshairTime(GetDisplayTime(_candles[candleIndex]));

            using (Pen crosshairPen = new Pen(SecondaryTextColor, 1f))
            using (SolidBrush labelBrush = new SolidBrush(TextColor))
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            using (StringFormat centerFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            })
            {
                crosshairPen.DashStyle = DashStyle.Dash;
                graphics.DrawLine(crosshairPen, _lastPlotArea.Left, crosshairY, _lastPlotArea.Right, crosshairY);
                graphics.DrawLine(crosshairPen, crosshairX, _lastPlotArea.Top, crosshairX, _lastPlotArea.Bottom);

                RectangleF priceTag = new RectangleF(
                    _lineMode ? _lastPlotArea.Right - IndexCurrentTagWidth - 3 : _lastPlotArea.Right + RightPadding,
                    _lineMode
                        ? Math.Max(_lastPriceArea.Top, Math.Min(_lastPriceArea.Bottom - CrosshairLabelHeight,
                            crosshairY - CrosshairLabelHeight / 2f))
                        : crosshairY - CrosshairLabelHeight / 2f,
                    _lineMode ? IndexCurrentTagWidth : RightAxisWidth - RightPadding - 1,
                    CrosshairLabelHeight);
                graphics.FillRectangle(labelBrush, priceTag);
                graphics.DrawString(priceLabel, _axisFont, textBrush, priceTag, centerFormat);

                SizeF timeSize = graphics.MeasureString(timeLabel, _axisFont);
                float timeWidth = timeSize.Width + CrosshairTimeLabelHorizontalPadding * 2;
                float timeLeft = Math.Max(
                    _lastPlotArea.Left,
                    Math.Min(_lastPlotArea.Right - timeWidth, crosshairX - timeWidth / 2f));
                float timeTagTop = _lastPlotArea.Bottom + (ShouldDrawUsMarketSessionBands()
                    ? TimeLabelTopWithSessionBand
                    : TimeLabelTopWithoutSessionBand);
                RectangleF timeTag = new RectangleF(
                    timeLeft,
                    timeTagTop,
                    timeWidth,
                    CrosshairLabelHeight);
                graphics.FillRectangle(labelBrush, timeTag);
                graphics.DrawString(timeLabel, _axisFont, textBrush, timeTag, centerFormat);
            }
        }

        private string FormatCrosshairTime(DateTime time)
        {
            if (_range.PeriodDurationMinutes >= OneMonthPeriodMinutes)
            {
                return time.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            }

            if (_range.PeriodDurationMinutes >= OneDayMinutes)
            {
                return time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return _range.IsAllHistory || _range.DurationMinutes > OneYearMinutes
                ? time.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                : time.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private void DrawCenteredMessage(Graphics graphics, Rectangle client, string message, bool error)
        {
            Color color = error ? DownColor : SecondaryTextColor;
            using (Font messageFont = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular, GraphicsUnit.Point))
            using (SolidBrush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            })
            {
                graphics.DrawString(message, messageFont, brush, new RectangleF(28, 28, client.Width - 56, client.Height - 56), format);
            }
        }

        /// <summary>Stores a continuous screen-space edge within a single CNN sentiment range.</summary>
        /// <remarks>Author: Codex; date: 2026-11-17.</remarks>
        private sealed class IndexLineSegment
        {
            internal PointF Start { get; set; }
            internal PointF End { get; set; }
            internal Color Color { get; set; }
        }

        private sealed class IntervalStatistics
        {
            internal int CandleCount { get; set; }
            internal DateTime StartTime { get; set; }
            internal DateTime EndTime { get; set; }
            internal decimal StartClose { get; set; }
            internal decimal EndClose { get; set; }
            internal decimal ChangeValue { get; set; }
            internal decimal ChangePercent { get; set; }
            internal decimal Maximum { get; set; }
            internal decimal Minimum { get; set; }
            internal decimal TotalVolume { get; set; }
        }

        private enum UsMarketSession
        {
            Overnight,
            PreMarket,
            RegularMarket,
            AfterHours
        }

        private static float PriceToY(decimal price, Rectangle area, decimal minimum, decimal maximum)
        {
            decimal ratio = maximum == minimum ? 0.5m : (maximum - price) / (maximum - minimum);
            ratio = Math.Max(0m, Math.Min(1m, ratio));
            return area.Top + area.Height * (float)ratio;
        }
    }
}
