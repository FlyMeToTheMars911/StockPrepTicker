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
        private const int BottomAxisHeight = 28;
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
        private const string OneDayRangeKey = "1D";
        private const string FiveDayRangeKey = "5D";
        private const string AllRangeKey = "ALL";
        private static readonly Color UpColor = Color.FromArgb(8, 153, 129);
        private static readonly Color DownColor = Color.FromArgb(242, 54, 69);
        private static readonly Color TextColor = Color.FromArgb(19, 23, 34);
        private static readonly Color SecondaryTextColor = Color.FromArgb(90, 96, 110);
        private static readonly Color GridColor = Color.FromArgb(224, 227, 235);
        private readonly Font _smallFont;
        private readonly Font _axisFont;
        private Bitmap _chartLayer;
        private List<Candle> _candles;
        private MarketSnapshot _snapshot;
        private RangeDefinition _range;
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

        internal ChartControl()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.Selectable, true);
            TabStop = false;
            BackColor = Color.White;
            _smallFont = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            _axisFont = new Font("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Point);
            _candles = new List<Candle>();
            _range = RangeDefinition.Find("1D");
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
            int[] movingAverages)
        {
            long rightmostVisibleTimestamp = GetRightmostVisibleTimestamp();
            bool followLatest = _rightOffset == NoViewportOffset;
            _candles = candles == null ? new List<Candle>() : new List<Candle>(candles);
            if (!followLatest && rightmostVisibleTimestamp > default(long))
            {
                RestoreRightOffset(rightmostVisibleTimestamp);
            }

            NormalizeViewport();
            _snapshot = snapshot;
            _range = range ?? RangeDefinition.Find("1D");
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
            DrawCrosshair(e.Graphics);
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
            if (!_isDragging && _hoverVisible)
            {
                _hoverVisible = false;
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
            if (e.Button != MouseButtons.Left || !plotArea.Contains(e.Location) || _candles.Count == EmptyCandleCount)
            {
                return;
            }

            Focus();
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

            if (hoverChanged)
            {
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
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
                    DateTime time = _candles[candleIndex].LocalTime;
                    string label = _range.Key == OneDayRangeKey
                        ? time.ToString("HH:mm", CultureInfo.InvariantCulture)
                        : (_range.Key == FiveDayRangeKey
                            ? time.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture)
                            : time.ToString("MM-dd", CultureInfo.InvariantCulture));
                    graphics.DrawString(label, _axisFont, brush, new RectangleF(x - 38, plotArea.Bottom + 4, 76, 20), centerFormat);
                }
            }
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
            string timeLabel = FormatCrosshairTime(_candles[candleIndex].LocalTime);

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
                RectangleF timeTag = new RectangleF(
                    timeLeft,
                    _lastPlotArea.Bottom + 2,
                    timeWidth,
                    CrosshairLabelHeight);
                graphics.FillRectangle(labelBrush, timeTag);
                graphics.DrawString(timeLabel, _axisFont, textBrush, timeTag, centerFormat);
            }
        }

        private string FormatCrosshairTime(DateTime time)
        {
            return _range.Key == AllRangeKey
                ? time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
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

        private static float PriceToY(decimal price, Rectangle area, decimal minimum, decimal maximum)
        {
            decimal ratio = maximum == minimum ? 0.5m : (maximum - price) / (maximum - minimum);
            ratio = Math.Max(0m, Math.Min(1m, ratio));
            return area.Top + area.Height * (float)ratio;
        }
    }
}
