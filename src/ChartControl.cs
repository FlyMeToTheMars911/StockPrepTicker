using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace StockPerpTicker
{
    internal sealed class ChartControl : Control
    {
        private const int MinimumPaintDimension = 100;
        private const int EmptyCandleCount = 0;
        private const int ChartHeaderHeight = 46;
        private static readonly Color UpColor = Color.FromArgb(8, 153, 129);
        private static readonly Color DownColor = Color.FromArgb(242, 54, 69);
        private static readonly Color TextColor = Color.FromArgb(19, 23, 34);
        private static readonly Color SecondaryTextColor = Color.FromArgb(90, 96, 110);
        private static readonly Color GridColor = Color.FromArgb(224, 227, 235);
        private readonly Font _smallFont;
        private readonly Font _axisFont;
        private List<Candle> _candles;
        private MarketSnapshot _snapshot;
        private RangeDefinition _range;
        private decimal _tickSize;
        private int[] _movingAverages;
        private string _message;
        private bool _isError;

        internal ChartControl()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.White;
            _smallFont = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
            _axisFont = new Font("Segoe UI", 8f, FontStyle.Regular, GraphicsUnit.Point);
            _candles = new List<Candle>();
            _range = RangeDefinition.Find("1D");
            _tickSize = 0.01m;
            _movingAverages = new int[0];
            _message = "正在加载行情…";
        }

        internal void SetData(
            IList<Candle> candles,
            MarketSnapshot snapshot,
            RangeDefinition range,
            decimal tickSize,
            int[] movingAverages)
        {
            _candles = candles == null ? new List<Candle>() : new List<Candle>(candles);
            _snapshot = snapshot;
            _range = range ?? RangeDefinition.Find("1D");
            _tickSize = tickSize;
            _movingAverages = movingAverages == null ? new int[0] : (int[])movingAverages.Clone();
            _message = string.Empty;
            _isError = false;
            Invalidate();
        }

        internal void SetMessage(string message, bool isError)
        {
            _message = message ?? string.Empty;
            _isError = isError;
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _smallFont.Dispose();
                _axisFont.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.None;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

            Rectangle client = ClientRectangle;
            if (client.Width < MinimumPaintDimension || client.Height < MinimumPaintDimension)
            {
                return;
            }

            if (_candles.Count == EmptyCandleCount)
            {
                DrawCenteredMessage(e.Graphics, client, string.IsNullOrEmpty(_message) ? "暂无行情数据" : _message, _isError);
                return;
            }

            const int RightAxisWidth = 62;
            const int BottomAxisHeight = 28;
            const int LeftPadding = 8;
            const int RightPadding = 2;
            Rectangle plotArea = new Rectangle(
                LeftPadding,
                ChartHeaderHeight,
                Math.Max(1, client.Width - LeftPadding - RightAxisWidth - RightPadding),
                Math.Max(1, client.Height - ChartHeaderHeight - BottomAxisHeight));
            int volumeHeight = Math.Max(34, plotArea.Height / 5);
            Rectangle priceArea = new Rectangle(plotArea.Left, plotArea.Top, plotArea.Width, Math.Max(1, plotArea.Height - volumeHeight));
            Rectangle volumeArea = new Rectangle(plotArea.Left, priceArea.Bottom, plotArea.Width, volumeHeight);

            decimal minimum = _candles.Min(item => item.Low);
            decimal maximum = _candles.Max(item => item.High);
            if (maximum <= minimum)
            {
                maximum = minimum + (_tickSize > decimal.Zero ? _tickSize : 0.01m);
            }

            decimal pricePadding = (maximum - minimum) * 0.06m;
            minimum -= pricePadding;
            maximum += pricePadding;
            decimal maximumVolume = _candles.Max(item => item.Volume);

            DrawGrid(e.Graphics, plotArea, priceArea, minimum, maximum);
            DrawCandles(e.Graphics, priceArea, volumeArea, minimum, maximum, maximumVolume);
            DrawMovingAverages(e.Graphics, priceArea, minimum, maximum);
            DrawHeader(e.Graphics, client);
            DrawTimeAxis(e.Graphics, plotArea);
            DrawCurrentPrice(e.Graphics, plotArea, priceArea, minimum, maximum);
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
            decimal maximumVolume)
        {
            float step = priceArea.Width / (float)Math.Max(1, _candles.Count);
            float bodyWidth = Math.Max(1f, Math.Min(7f, step * 0.66f));
            using (Pen upPen = new Pen(UpColor, 1f))
            using (Pen downPen = new Pen(DownColor, 1f))
            using (SolidBrush upBrush = new SolidBrush(UpColor))
            using (SolidBrush downBrush = new SolidBrush(DownColor))
            using (SolidBrush upVolume = new SolidBrush(Color.FromArgb(80, UpColor)))
            using (SolidBrush downVolume = new SolidBrush(Color.FromArgb(80, DownColor)))
            {
                for (int index = 0; index < _candles.Count; index++)
                {
                    Candle candle = _candles[index];
                    bool rising = candle.Close >= candle.Open;
                    Pen pen = rising ? upPen : downPen;
                    Brush bodyBrush = rising ? upBrush : downBrush;
                    float x = priceArea.Left + step * (index + 0.5f);
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

        private void DrawMovingAverages(Graphics graphics, Rectangle priceArea, decimal minimum, decimal maximum)
        {
            float step = priceArea.Width / (float)Math.Max(1, _candles.Count);
            foreach (int period in _movingAverages)
            {
                if (_candles.Count < period)
                {
                    continue;
                }

                decimal rollingTotal = decimal.Zero;
                PointF? previous = null;
                using (Pen pen = new Pen(GetMovingAverageColor(period), 1.25f))
                {
                    pen.LineJoin = LineJoin.Round;
                    for (int index = 0; index < _candles.Count; index++)
                    {
                        rollingTotal += _candles[index].Close;
                        if (index >= period)
                        {
                            rollingTotal -= _candles[index - period].Close;
                        }

                        if (index < period - 1)
                        {
                            continue;
                        }

                        decimal average = rollingTotal / period;
                        PointF current = new PointF(
                            priceArea.Left + step * (index + 0.5f),
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

        private void DrawTimeAxis(Graphics graphics, Rectangle plotArea)
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
                    int candleIndex = Math.Min(_candles.Count - 1, (int)Math.Round((_candles.Count - 1) * index / (float)Labels));
                    float x = plotArea.Left + plotArea.Width * index / (float)Labels;
                    DateTime time = _candles[candleIndex].LocalTime;
                    string label = _range.Key == "1D"
                        ? time.ToString("HH:mm", CultureInfo.InvariantCulture)
                        : (_range.Key == "5D" ? time.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) : time.ToString("MM-dd", CultureInfo.InvariantCulture));
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
