using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace StockPerpTicker
{
    internal sealed class TaskbarTickerForm : Form
    {
        private const int WindowWidth = 280;
        private const int WindowHeight = 42;
        private const int ScreenEdgeGap = 6;
        private const int RoundedCornerRadius = 9;
        private const int ContentInset = 5;
        private const float BorderThickness = 3f;
        private const int ToolWindowStyle = 0x00000080;
        private const int NoActivateStyle = 0x08000000;
        private static readonly Color BorderColor = Color.FromArgb(76, 88, 105);
        private readonly Action _restoreAction;
        private readonly Action<Point> _customLocationChanged;
        private readonly System.Windows.Forms.Timer _scrollTimer;
        private TickerPage _currentPage;
        private TickerPage _incomingPage;
        private TaskbarTickerPosition _tickerPosition;
        private bool _hasCustomLocation;
        private int _customLeft;
        private int _customTop;
        private bool _pointerDown;
        private bool _dragMoved;
        private Point _pointerDownScreenPosition;
        private Point _dragStartLocation;
        private Control _capturedControl;

        internal TaskbarTickerForm(
            Action restoreAction,
            TaskbarTickerPosition tickerPosition,
            bool hasCustomLocation,
            int customLeft,
            int customTop,
            Action<Point> customLocationChanged)
        {
            _restoreAction = restoreAction;
            _tickerPosition = tickerPosition;
            _hasCustomLocation = hasCustomLocation;
            _customLeft = customLeft;
            _customTop = customTop;
            _customLocationChanged = customLocationChanged;
            Text = "StockPerpTicker 迷你行情";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Size = new Size(WindowWidth, WindowHeight);
            BackColor = BorderColor;
            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = new Padding(ContentInset);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

            _currentPage = new TickerPage
            {
                Location = new Point(ContentInset, ContentInset),
                Size = new Size(WindowWidth - (ContentInset * 2), WindowHeight - (ContentInset * 2))
            };
            _incomingPage = new TickerPage
            {
                Location = new Point(ContentInset, WindowHeight),
                Size = new Size(WindowWidth - (ContentInset * 2), WindowHeight - (ContentInset * 2)),
                Visible = false
            };
            Controls.Add(_currentPage);
            Controls.Add(_incomingPage);
            _scrollTimer = new System.Windows.Forms.Timer { Interval = 15 };
            _scrollTimer.Tick += AnimateNextPage;
            AttachPointerInteraction(this);
            Resize += delegate { UpdateRoundedRegion(); };
            UpdateRoundedRegion();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= ToolWindowStyle | NoActivateStyle;
                return parameters;
            }
        }

        internal void UpdateMarket(
            string instrumentId,
            MarketSnapshot snapshot,
            IList<Candle> candles,
            decimal tickSize,
            bool allowSwitch)
        {
            if (_scrollTimer.Enabled
                && string.Equals(_incomingPage.InstrumentId, instrumentId, StringComparison.OrdinalIgnoreCase))
            {
                _incomingPage.UpdateMarket(instrumentId, snapshot, candles, tickSize);
                return;
            }

            if (string.IsNullOrEmpty(_currentPage.InstrumentId)
                || string.Equals(_currentPage.InstrumentId, instrumentId, StringComparison.OrdinalIgnoreCase))
            {
                _currentPage.UpdateMarket(instrumentId, snapshot, candles, tickSize);
                return;
            }

            if (!allowSwitch)
            {
                return;
            }

            CompleteScrollAnimation();
            _incomingPage.UpdateMarket(instrumentId, snapshot, candles, tickSize);
            if (!Visible)
            {
                SwapPages();
                return;
            }

            _incomingPage.Top = Height;
            _incomingPage.Visible = true;
            _incomingPage.BringToFront();
            _scrollTimer.Start();
        }

        internal void ShowTicker(Rectangle referenceBounds)
        {
            Screen screen = Screen.FromRectangle(referenceBounds);
            Rectangle workingArea = screen.WorkingArea;
            Point desiredLocation;
            switch (_tickerPosition)
            {
                case TaskbarTickerPosition.TopLeft:
                    desiredLocation = new Point(
                        workingArea.Left + ScreenEdgeGap,
                        workingArea.Top + ScreenEdgeGap);
                    break;
                case TaskbarTickerPosition.BottomLeft:
                    desiredLocation = new Point(
                        workingArea.Left + ScreenEdgeGap,
                        workingArea.Bottom - Height - ScreenEdgeGap);
                    break;
                case TaskbarTickerPosition.Custom:
                    if (_hasCustomLocation)
                    {
                        desiredLocation = new Point(_customLeft, _customTop);
                        workingArea = Screen.FromPoint(desiredLocation).WorkingArea;
                    }
                    else
                    {
                        desiredLocation = new Point(
                            workingArea.Right - Width - ScreenEdgeGap,
                            workingArea.Bottom - Height - ScreenEdgeGap);
                    }
                    break;
                default:
                    desiredLocation = new Point(
                        workingArea.Right - Width - ScreenEdgeGap,
                        workingArea.Bottom - Height - ScreenEdgeGap);
                    break;
            }

            Location = ClampToWorkingArea(desiredLocation, workingArea);
            if (!Visible)
            {
                Show();
            }

            Invalidate();
        }

        internal void HideTicker()
        {
            CompleteScrollAnimation();
            if (Visible)
            {
                Hide();
            }
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle borderBounds = new Rectangle(2, 2, Width - 5, Height - 5);
            using (GraphicsPath borderPath = CreateRoundedRectanglePath(borderBounds, RoundedCornerRadius - 1))
            using (Pen borderPen = new Pen(BorderColor, BorderThickness))
            {
                args.Graphics.DrawPath(borderPen, borderPath);
            }
        }

        private void AnimateNextPage(object sender, EventArgs args)
        {
            const int ScrollStep = 6;
            _currentPage.Top -= ScrollStep;
            _incomingPage.Top -= ScrollStep;
            if (_incomingPage.Top <= ContentInset)
            {
                SwapPages();
            }
        }

        private void CompleteScrollAnimation()
        {
            if (_scrollTimer.Enabled)
            {
                SwapPages();
            }
        }

        private void SwapPages()
        {
            _scrollTimer.Stop();
            _currentPage.Visible = false;
            TickerPage previousPage = _currentPage;
            _currentPage = _incomingPage;
            _incomingPage = previousPage;
            _currentPage.Top = ContentInset;
            _currentPage.Visible = true;
            _incomingPage.Top = Height;
            _incomingPage.Visible = false;
        }

        private void AttachPointerInteraction(Control control)
        {
            control.Cursor = Cursors.SizeAll;
            control.MouseDown += BeginPointerInteraction;
            control.MouseMove += ContinuePointerInteraction;
            control.MouseUp += EndPointerInteraction;
            foreach (Control child in control.Controls)
            {
                AttachPointerInteraction(child);
            }
        }

        private void BeginPointerInteraction(object sender, MouseEventArgs args)
        {
            if (args.Button != MouseButtons.Left)
            {
                return;
            }

            CompleteScrollAnimation();
            _pointerDown = true;
            _dragMoved = false;
            _pointerDownScreenPosition = Cursor.Position;
            _dragStartLocation = Location;
            _capturedControl = sender as Control;
            if (_capturedControl != null)
            {
                _capturedControl.Capture = true;
            }
        }

        private void ContinuePointerInteraction(object sender, MouseEventArgs args)
        {
            if (!_pointerDown)
            {
                return;
            }

            Point cursorPosition = Cursor.Position;
            int horizontalDelta = cursorPosition.X - _pointerDownScreenPosition.X;
            int verticalDelta = cursorPosition.Y - _pointerDownScreenPosition.Y;
            if (!_dragMoved)
            {
                Size dragSize = SystemInformation.DragSize;
                _dragMoved = Math.Abs(horizontalDelta) >= Math.Max(2, dragSize.Width / 2)
                    || Math.Abs(verticalDelta) >= Math.Max(2, dragSize.Height / 2);
            }

            if (!_dragMoved)
            {
                return;
            }

            Point desiredLocation = new Point(
                _dragStartLocation.X + horizontalDelta,
                _dragStartLocation.Y + verticalDelta);
            Rectangle workingArea = Screen.FromPoint(cursorPosition).WorkingArea;
            Location = ClampToWorkingArea(desiredLocation, workingArea);
        }

        private void EndPointerInteraction(object sender, MouseEventArgs args)
        {
            if (!_pointerDown || args.Button != MouseButtons.Left)
            {
                return;
            }

            _pointerDown = false;
            if (_capturedControl != null)
            {
                _capturedControl.Capture = false;
                _capturedControl = null;
            }

            if (_dragMoved)
            {
                _tickerPosition = TaskbarTickerPosition.Custom;
                _hasCustomLocation = true;
                _customLeft = Left;
                _customTop = Top;
                if (_customLocationChanged != null)
                {
                    _customLocationChanged(Location);
                }

                return;
            }

            if (_restoreAction != null)
            {
                _restoreAction();
            }
        }

        private Point ClampToWorkingArea(Point location, Rectangle workingArea)
        {
            int maximumLeft = Math.Max(workingArea.Left, workingArea.Right - Width);
            int maximumTop = Math.Max(workingArea.Top, workingArea.Bottom - Height);
            return new Point(
                Math.Max(workingArea.Left, Math.Min(location.X, maximumLeft)),
                Math.Max(workingArea.Top, Math.Min(location.Y, maximumTop)));
        }

        private void UpdateRoundedRegion()
        {
            using (GraphicsPath path = CreateRoundedRectanglePath(ClientRectangle, RoundedCornerRadius))
            {
                Region oldRegion = Region;
                Region = new Region(path);
                if (oldRegion != null)
                {
                    oldRegion.Dispose();
                }
            }
        }

        internal static GraphicsPath CreateRoundedRectanglePath(Rectangle rectangle, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            Rectangle arc = new Rectangle(rectangle.Left, rectangle.Top, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = rectangle.Right - diameter - 1;
            path.AddArc(arc, 270, 90);
            arc.Y = rectangle.Bottom - diameter - 1;
            path.AddArc(arc, 0, 90);
            arc.X = rectangle.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _scrollTimer.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    internal sealed class TickerPage : Panel
    {
        private const int ContentCornerRadius = 5;
        private static readonly Color UpColor = Color.FromArgb(8, 153, 129);
        private static readonly Color DownColor = Color.FromArgb(242, 54, 69);
        private static readonly Color TextColor = Color.FromArgb(19, 23, 34);
        private static readonly Color TickerBackground = Color.FromArgb(248, 250, 252);
        private readonly Label _symbolLabel;
        private readonly Label _priceLabel;
        private readonly Label _changeLabel;
        private readonly SparklineControl _sparkline;

        internal TickerPage()
        {
            BackColor = TickerBackground;
            _symbolLabel = new Label
            {
                Location = new Point(4, 0),
                Size = new Size(50, 32),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = TextColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "--"
            };
            _priceLabel = new Label
            {
                Location = new Point(54, 0),
                Size = new Size(60, 32),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = TextColor,
                TextAlign = ContentAlignment.MiddleRight,
                Text = "--"
            };
            _changeLabel = new Label
            {
                Location = new Point(116, 0),
                Size = new Size(60, 32),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = TextColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "--"
            };
            _sparkline = new SparklineControl
            {
                Location = new Point(178, 3),
                Size = new Size(86, 26)
            };
            Controls.Add(_symbolLabel);
            Controls.Add(_priceLabel);
            Controls.Add(_changeLabel);
            Controls.Add(_sparkline);
            Resize += delegate { UpdateRoundedRegion(); };
            UpdateRoundedRegion();
        }

        internal string InstrumentId { get; private set; }

        internal void UpdateMarket(
            string instrumentId,
            MarketSnapshot snapshot,
            IList<Candle> candles,
            decimal tickSize)
        {
            InstrumentId = instrumentId;
            _symbolLabel.Text = GetCompactSymbol(instrumentId);
            if (snapshot == null)
            {
                _priceLabel.Text = "--";
                _changeLabel.Text = "--";
                _changeLabel.ForeColor = TextColor;
                _sparkline.SetData(candles, UpColor);
                return;
            }

            decimal change = snapshot.ChangePercent;
            Color trendColor = change >= decimal.Zero ? UpColor : DownColor;
            _priceLabel.Text = FormatHelper.Price(snapshot.LastPrice, tickSize);
            _changeLabel.Text = (change >= decimal.Zero ? "+" : string.Empty) + change.ToString("0.00") + "%";
            _changeLabel.ForeColor = trendColor;
            _sparkline.SetData(candles, trendColor);
        }

        private static string GetCompactSymbol(string instrumentId)
        {
            if (string.IsNullOrEmpty(instrumentId))
            {
                return "--";
            }

            int separator = instrumentId.IndexOf('-');
            return separator > default(int) ? instrumentId.Substring(0, separator) : instrumentId;
        }

        private void UpdateRoundedRegion()
        {
            using (GraphicsPath path = TaskbarTickerForm.CreateRoundedRectanglePath(ClientRectangle, ContentCornerRadius))
            {
                Region oldRegion = Region;
                Region = new Region(path);
                if (oldRegion != null)
                {
                    oldRegion.Dispose();
                }
            }
        }
    }

    internal sealed class SparklineControl : Control
    {
        private const int MaximumPoints = 48;
        private const int MinimumPoints = 2;
        private readonly List<decimal> _values;
        private Color _lineColor;
        private static readonly Color TickerBackground = Color.FromArgb(248, 250, 252);

        internal SparklineControl()
        {
            DoubleBuffered = true;
            BackColor = TickerBackground;
            _values = new List<decimal>();
            _lineColor = Color.FromArgb(8, 153, 129);
        }

        internal void SetData(IList<Candle> candles, Color lineColor)
        {
            _values.Clear();
            if (candles != null)
            {
                int firstIndex = Math.Max(default(int), candles.Count - MaximumPoints);
                for (int index = firstIndex; index < candles.Count; index++)
                {
                    _values.Add(candles[index].Close);
                }
            }

            _lineColor = lineColor;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            if (_values.Count < MinimumPoints)
            {
                return;
            }

            decimal minimum = _values.Min();
            decimal maximum = _values.Max();
            if (maximum == minimum)
            {
                maximum += decimal.One;
                minimum -= decimal.One;
            }

            PointF[] points = new PointF[_values.Count];
            for (int index = 0; index < _values.Count; index++)
            {
                float x = 2f + (Width - 4f) * index / (_values.Count - 1f);
                float ratio = (float)((maximum - _values[index]) / (maximum - minimum));
                float y = 2f + (Height - 4f) * ratio;
                points[index] = new PointF(x, y);
            }

            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen linePen = new Pen(_lineColor, 1.6f))
            using (SolidBrush pointBrush = new SolidBrush(_lineColor))
            {
                linePen.LineJoin = LineJoin.Round;
                args.Graphics.DrawLines(linePen, points);
                PointF last = points[points.Length - 1];
                args.Graphics.FillEllipse(pointBrush, last.X - 2f, last.Y - 2f, 4f, 4f);
            }
        }
    }
}
