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
        private const long MaximumSessionMarkerPeriodMinutes = 60L;
        private const int PreMarketStartMinutes = 240;
        private const int RegularMarketStartMinutes = 570;
        private const int AfterHoursStartMinutes = 960;
        private const int OvernightStartMinutes = 1200;
        private const float SessionBandTopOffset = 2f;
        private const float SessionBandHeight = 16f;
        private const float SessionLabelMinimumWidth = 42f;
        private const float TimeLabelTopWithSessionBand = 20f;
        private const float TimeLabelTopWithoutSessionBand = 10f;
        private const int MinutesPerHour = 60;
        private const int MaximumDisplayTimeCacheEntries = 6000;
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
        private readonly Font _intervalTitleFont;
        private readonly ContextMenuStrip _drawingMenu;
        private readonly ToolStripMenuItem _intervalStatisticsMenuItem;
        private readonly ToolStripMenuItem _deleteIntervalStatisticsMenuItem;
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

        internal ChartControl()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.Selectable, true);
            TabStop = false;
            BackColor = Color.White;
            _smallFont = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            _axisFont = new Font("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Point);
            _intervalTitleFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
            _drawingMenu = new ContextMenuStrip { ShowImageMargin = false };
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
            _candles = candles == null ? new List<Candle>() : new List<Candle>(candles);
            _range = range ?? RangeDefinition.Find(RangeDefinition.DefaultKey);
            if (_displayTimeZone != displayTimeZone
                || _displayTimeCache.Count > MaximumDisplayTimeCacheEntries)
            {
                _displayTimeCache.Clear();
            }

            _displayTimeZone = displayTimeZone;
            UpdateIntervalStatistics();

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
                _intervalTitleFont.Dispose();
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
                if (priceArea.Contains(e.Location) && TryGetCandleIndexAtX(e.X, out contextCandleIndex))
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
                Logger.Info(
                    "完成区间统计：" + _intervalStatistics.CandleCount + " 根 K 线，涨跌 "
                    + FormatSignedPrice(_intervalStatistics.ChangeValue) + "（"
                    + FormatSignedPercentage(_intervalStatistics.ChangePercent) + "）");
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
                graphics.DrawString("点击另一根 K 线完成", _smallFont, textBrush, hintBounds);
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
                    "K 线数量  " + _intervalStatistics.CandleCount + "    成交量  "
                        + FormatHelper.CompactNumber(_intervalStatistics.TotalVolume),
                    primaryBrush,
                    rowFormat,
                    textLeft,
                    textTop,
                    textWidth);
                textTop += IntervalTooltipLineHeight;
                DrawIntervalTooltipRow(
                    graphics,
                    "起始收盘  " + FormatHelper.Price(_intervalStatistics.StartClose, _tickSize)
                        + "    结束收盘  " + FormatHelper.Price(_intervalStatistics.EndClose, _tickSize),
                    primaryBrush,
                    rowFormat,
                    textLeft,
                    textTop,
                    textWidth);
                textTop += IntervalTooltipLineHeight;
                DrawIntervalTooltipRow(
                    graphics,
                    "涨跌值  " + FormatSignedPrice(_intervalStatistics.ChangeValue)
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

            decimal pricePadding = (maximum - minimum) * 0.06m;
            minimum -= pricePadding;
            maximum += pricePadding;

            _lastPlotArea = plotArea;
            _lastPriceArea = priceArea;
            _lastVisibleStart = visibleStart;
            _lastVisibleCount = visibleCount;
            _lastMinimum = minimum;
            _lastMaximum = maximum;
            _lastLayoutValid = true;

            DrawGrid(graphics, plotArea, priceArea, minimum, maximum);
            DrawCandles(graphics, priceArea, volumeArea, minimum, maximum, maximumVolume, visibleStart, visibleCount);
            DrawMovingAverages(graphics, priceArea, minimum, maximum, visibleStart, visibleCount);
            DrawHeader(graphics, client);
            DrawTimeAxis(graphics, plotArea, visibleStart, visibleCount);
            DrawCurrentPrice(graphics, plotArea, priceArea, minimum, maximum);
        }

        private static void GetChartAreas(
            Rectangle client,
            out Rectangle plotArea,
            out Rectangle priceArea,
            out Rectangle volumeArea)
        {
            plotArea = new Rectangle(
                LeftPadding,
                ChartHeaderHeight,
                Math.Max(1, client.Width - LeftPadding - RightAxisWidth - RightPadding),
                Math.Max(1, client.Height - ChartHeaderHeight - BottomAxisHeight));
            int volumeHeight = Math.Max(34, plotArea.Height / 5);
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
            using (Pen gridPen = new Pen(GridColor, 1f))
            using (SolidBrush axisBrush = new SolidBrush(SecondaryTextColor))
            using (StringFormat nearFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
            {
                gridPen.DashStyle = DashStyle.Dot;
                const int HorizontalLines = 5;
                for (int index = 0; index <= HorizontalLines; index++)
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

        private void DrawHeader(Graphics graphics, Rectangle client)
        {
            Candle latest = _candles[_candles.Count - 1];
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

        private bool ShouldDrawUsMarketSessionBands()
        {
            return _displayTimeZone == DisplayTimeZone.UsEastern
                && _range != null
                && _range.PeriodDurationMinutes <= MaximumSessionMarkerPeriodMinutes;
        }

        private void DrawUsMarketSessionBands(
            Graphics graphics,
            Rectangle plotArea,
            int visibleStart,
            int visibleCount)
        {
            float candleStep = plotArea.Width / (float)Math.Max(1, visibleCount);
            int segmentStart = default(int);
            UsMarketSession currentSession = GetUsMarketSession(GetDisplayTime(_candles[visibleStart]));
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
                for (int relativeIndex = 1; relativeIndex <= visibleCount; relativeIndex++)
                {
                    UsMarketSession nextSession = currentSession;
                    if (relativeIndex < visibleCount)
                    {
                        nextSession = GetUsMarketSession(
                            GetDisplayTime(_candles[visibleStart + relativeIndex]));
                    }

                    if (relativeIndex < visibleCount && nextSession == currentSession)
                    {
                        continue;
                    }

                    float segmentLeft = plotArea.Left + candleStep * segmentStart;
                    float segmentRight = plotArea.Left + candleStep * relativeIndex;
                    RectangleF band = new RectangleF(
                        segmentLeft,
                        plotArea.Bottom + SessionBandTopOffset,
                        Math.Max(MinimumIntervalDrawingWidth, segmentRight - segmentLeft),
                        SessionBandHeight);
                    graphics.FillRectangle(
                        GetSessionBrush(
                            currentSession,
                            overnightBrush,
                            preMarketBrush,
                            regularMarketBrush,
                            afterHoursBrush),
                        band);
                    graphics.DrawLine(separatorPen, segmentLeft, band.Top, segmentLeft, band.Bottom);
                    if (band.Width >= SessionLabelMinimumWidth)
                    {
                        graphics.DrawString(
                            GetSessionLabel(currentSession),
                            _smallFont,
                            textBrush,
                            band,
                            centerFormat);
                    }

                    segmentStart = relativeIndex;
                    currentSession = nextSession;
                }
            }
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

            decimal current = _snapshot != null && _snapshot.LastPrice > decimal.Zero
                ? _snapshot.LastPrice
                : _candles[_candles.Count - 1].Close;
            bool rising = _snapshot == null || _snapshot.Open24Hours == decimal.Zero || current >= _snapshot.Open24Hours;
            Color color = rising ? UpColor : DownColor;
            float y = PriceToY(current, priceArea, minimum, maximum);
            using (Pen pen = new Pen(color, 1f))
            using (SolidBrush brush = new SolidBrush(color))
            using (SolidBrush whiteBrush = new SolidBrush(Color.White))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                pen.DashStyle = DashStyle.Dot;
                graphics.DrawLine(pen, plotArea.Left, y, plotArea.Right, y);
                RectangleF tag = new RectangleF(plotArea.Right + 2, y - 10, 59, 20);
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
                    _lastPlotArea.Right + RightPadding,
                    crosshairY - CrosshairLabelHeight / 2f,
                    RightAxisWidth - RightPadding - 1,
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
