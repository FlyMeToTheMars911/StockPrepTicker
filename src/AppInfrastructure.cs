using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace StockPerpTicker
{
    internal sealed class AppSettings
    {
        public string instrumentId { get; set; }
        public string[] instrumentIds { get; set; }
        public int refreshIntervalMilliseconds { get; set; }
        public string candlePeriod { get; set; }
        public string timeRange { get; set; }
        public int[] movingAverages { get; set; }
        public bool showTaskbarTickerOnMinimize { get; set; }
        public string taskbarTickerPosition { get; set; }
        public bool hasCustomTaskbarTickerPosition { get; set; }
        public int taskbarTickerCustomLeft { get; set; }
        public int taskbarTickerCustomTop { get; set; }
        public int taskbarTickerRotationIntervalSeconds { get; set; }
        internal TaskbarTickerPosition TickerPosition { get; set; }
    }

    internal enum TaskbarTickerPosition
    {
        TopLeft,
        BottomLeft,
        BottomRight,
        Custom
    }

    internal static class SettingsStore
    {
        private const string SettingsFileName = "settings.json";
        private const string LegacyConfigFileName = "config.json";
        private const string SwapSuffix = "-SWAP";
        internal const int DefaultRefreshIntervalMilliseconds = 1000;
        internal const int MinimumRefreshIntervalMilliseconds = 250;
        internal const int MaximumRefreshIntervalMilliseconds = 60000;
        internal const int DefaultTickerRotationIntervalSeconds = 5;
        internal const int MinimumTickerRotationIntervalSeconds = 2;
        internal const int MaximumTickerRotationIntervalSeconds = 60;
        internal const int MaximumInstrumentCount = 20;
        private const string BottomLeftTickerPosition = "bottomLeft";
        private const string BottomRightTickerPosition = "bottomRight";
        private const string TopLeftTickerPosition = "topLeft";
        private const string CustomTickerPosition = "custom";
        private static readonly int[] DefaultMovingAverages = { 5, 10, 20, 50 };
        private static readonly int[] SupportedMovingAverages = { 5, 10, 20, 50, 100, 200 };

        internal static int[] MovingAverageOptions
        {
            get { return (int[])SupportedMovingAverages.Clone(); }
        }

        internal static AppSettings Load()
        {
            string path = Path.Combine(StateStore.AppDataDirectory, SettingsFileName);
            try
            {
                if (File.Exists(path))
                {
                    AppSettings storedSettings = Deserialize(path);
                    AppSettings normalizedSettings;
                    string validationError;
                    if (TryNormalize(storedSettings, out normalizedSettings, out validationError))
                    {
                        return normalizedSettings;
                    }

                    Logger.Info("已忽略无效的用户设置：" + validationError);
                }
                else
                {
                    AppSettings migratedSettings = TryLoadLegacySettings();
                    if (migratedSettings != null)
                    {
                        try
                        {
                            Save(migratedSettings);
                            Logger.Info("已将旧版 config.json 迁移到用户设置。");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("旧版设置已载入，但暂时无法保存迁移结果", ex);
                        }

                        return migratedSettings;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("读取用户设置失败，将使用默认设置", ex);
            }

            return CreateDefault();
        }

        internal static void Save(AppSettings settings)
        {
            AppSettings normalizedSettings;
            string validationError;
            if (!TryNormalize(settings, out normalizedSettings, out validationError))
            {
                throw new ArgumentException(validationError, "settings");
            }

            Directory.CreateDirectory(StateStore.AppDataDirectory);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            string path = Path.Combine(StateStore.AppDataDirectory, SettingsFileName);
            File.WriteAllText(path, serializer.Serialize(normalizedSettings), new UTF8Encoding(false));
        }

        internal static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                instrumentId = "RAM-USDT-SWAP",
                instrumentIds = new[] { "RAM-USDT-SWAP" },
                refreshIntervalMilliseconds = DefaultRefreshIntervalMilliseconds,
                candlePeriod = CandlePeriodDefinition.AutomaticKey,
                timeRange = RangeDefinition.DefaultKey,
                movingAverages = (int[])DefaultMovingAverages.Clone(),
                showTaskbarTickerOnMinimize = true,
                taskbarTickerPosition = BottomRightTickerPosition,
                hasCustomTaskbarTickerPosition = false,
                taskbarTickerRotationIntervalSeconds = DefaultTickerRotationIntervalSeconds,
                TickerPosition = TaskbarTickerPosition.BottomRight
            };
        }

        internal static AppSettings Clone(AppSettings settings)
        {
            return new AppSettings
            {
                instrumentId = settings.instrumentId,
                instrumentIds = settings.instrumentIds == null ? null : (string[])settings.instrumentIds.Clone(),
                refreshIntervalMilliseconds = settings.refreshIntervalMilliseconds,
                candlePeriod = settings.candlePeriod,
                timeRange = settings.timeRange,
                movingAverages = settings.movingAverages == null ? null : (int[])settings.movingAverages.Clone(),
                showTaskbarTickerOnMinimize = settings.showTaskbarTickerOnMinimize,
                taskbarTickerPosition = settings.taskbarTickerPosition,
                hasCustomTaskbarTickerPosition = settings.hasCustomTaskbarTickerPosition,
                taskbarTickerCustomLeft = settings.taskbarTickerCustomLeft,
                taskbarTickerCustomTop = settings.taskbarTickerCustomTop,
                taskbarTickerRotationIntervalSeconds = settings.taskbarTickerRotationIntervalSeconds,
                TickerPosition = settings.TickerPosition
            };
        }

        internal static bool TryNormalize(AppSettings settings, out AppSettings normalizedSettings, out string error)
        {
            normalizedSettings = null;
            error = null;
            if (settings == null)
            {
                error = "设置内容不能为空。";
                return false;
            }

            string[] sourceInstrumentIds = settings.instrumentIds != null && settings.instrumentIds.Length > default(int)
                ? settings.instrumentIds
                : new[] { settings.instrumentId };
            List<string> normalizedInstrumentIds = new List<string>();
            HashSet<string> uniqueInstrumentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string sourceInstrumentId in sourceInstrumentIds)
            {
                string normalizedInstrumentId;
                if (!TryNormalizeInstrumentId(sourceInstrumentId, out normalizedInstrumentId, out error))
                {
                    return false;
                }

                if (uniqueInstrumentIds.Add(normalizedInstrumentId))
                {
                    normalizedInstrumentIds.Add(normalizedInstrumentId);
                }
            }

            if (normalizedInstrumentIds.Count == default(int))
            {
                error = "请至少添加一个 OKX 永续合约。";
                return false;
            }

            if (normalizedInstrumentIds.Count > MaximumInstrumentCount)
            {
                error = "最多可以配置 " + MaximumInstrumentCount + " 个行情标的。";
                return false;
            }

            int refreshInterval = settings.refreshIntervalMilliseconds == default(int)
                ? DefaultRefreshIntervalMilliseconds
                : settings.refreshIntervalMilliseconds;
            if (refreshInterval < MinimumRefreshIntervalMilliseconds
                || refreshInterval > MaximumRefreshIntervalMilliseconds)
            {
                error = "界面刷新间隔必须在 " + MinimumRefreshIntervalMilliseconds
                    + " 到 " + MaximumRefreshIntervalMilliseconds + " 毫秒之间。";
                return false;
            }

            string candlePeriod = string.IsNullOrWhiteSpace(settings.candlePeriod)
                ? CandlePeriodDefinition.AutomaticKey
                : settings.candlePeriod.Trim();
            string timeRange = string.IsNullOrWhiteSpace(settings.timeRange)
                ? RangeDefinition.DefaultKey
                : settings.timeRange.Trim();
            RangeDefinition configuredRange;
            if (!RangeDefinition.TryCreate(timeRange, candlePeriod, out configuredRange, out error))
            {
                return false;
            }

            int tickerRotationInterval = settings.taskbarTickerRotationIntervalSeconds == default(int)
                ? DefaultTickerRotationIntervalSeconds
                : settings.taskbarTickerRotationIntervalSeconds;
            if (tickerRotationInterval < MinimumTickerRotationIntervalSeconds
                || tickerRotationInterval > MaximumTickerRotationIntervalSeconds)
            {
                error = "迷你行情轮播间隔必须在 " + MinimumTickerRotationIntervalSeconds
                    + " 到 " + MaximumTickerRotationIntervalSeconds + " 秒之间。";
                return false;
            }

            int[] movingAverages = settings.movingAverages == null
                ? (int[])DefaultMovingAverages.Clone()
                : settings.movingAverages;
            HashSet<int> selectedMovingAverages = new HashSet<int>(movingAverages);
            foreach (int period in movingAverages)
            {
                if (Array.IndexOf(SupportedMovingAverages, period) < default(int))
                {
                    error = "不支持 MA" + period + "。请选择设置窗口中提供的移动平均线。";
                    return false;
                }
            }

            List<int> normalizedMovingAverages = new List<int>();
            foreach (int period in SupportedMovingAverages)
            {
                if (selectedMovingAverages.Contains(period))
                {
                    normalizedMovingAverages.Add(period);
                }
            }

            string tickerPosition = string.IsNullOrWhiteSpace(settings.taskbarTickerPosition)
                ? BottomRightTickerPosition
                : settings.taskbarTickerPosition.Trim();
            TaskbarTickerPosition normalizedTickerPosition;
            if (string.Equals(tickerPosition, TopLeftTickerPosition, StringComparison.OrdinalIgnoreCase))
            {
                tickerPosition = TopLeftTickerPosition;
                normalizedTickerPosition = TaskbarTickerPosition.TopLeft;
            }
            else if (string.Equals(tickerPosition, BottomLeftTickerPosition, StringComparison.OrdinalIgnoreCase))
            {
                tickerPosition = BottomLeftTickerPosition;
                normalizedTickerPosition = TaskbarTickerPosition.BottomLeft;
            }
            else if (string.Equals(tickerPosition, BottomRightTickerPosition, StringComparison.OrdinalIgnoreCase))
            {
                tickerPosition = BottomRightTickerPosition;
                normalizedTickerPosition = TaskbarTickerPosition.BottomRight;
            }
            else if (string.Equals(tickerPosition, CustomTickerPosition, StringComparison.OrdinalIgnoreCase))
            {
                tickerPosition = CustomTickerPosition;
                normalizedTickerPosition = TaskbarTickerPosition.Custom;
            }
            else
            {
                error = "迷你行情条位置无效。";
                return false;
            }

            normalizedSettings = new AppSettings
            {
                instrumentId = normalizedInstrumentIds[0],
                instrumentIds = normalizedInstrumentIds.ToArray(),
                refreshIntervalMilliseconds = refreshInterval,
                candlePeriod = configuredRange.SelectedPeriodKey,
                timeRange = configuredRange.Key,
                movingAverages = normalizedMovingAverages.ToArray(),
                showTaskbarTickerOnMinimize = settings.showTaskbarTickerOnMinimize,
                taskbarTickerPosition = tickerPosition,
                hasCustomTaskbarTickerPosition = settings.hasCustomTaskbarTickerPosition,
                taskbarTickerCustomLeft = settings.taskbarTickerCustomLeft,
                taskbarTickerCustomTop = settings.taskbarTickerCustomTop,
                taskbarTickerRotationIntervalSeconds = tickerRotationInterval,
                TickerPosition = normalizedTickerPosition
            };
            return true;
        }

        internal static bool TryNormalizeInstrumentId(string instrumentId, out string normalizedInstrumentId, out string error)
        {
            normalizedInstrumentId = null;
            error = null;
            if (string.IsNullOrWhiteSpace(instrumentId))
            {
                error = "请输入 OKX 永续合约代码。";
                return false;
            }

            normalizedInstrumentId = instrumentId.Trim().ToUpperInvariant();
            if (!normalizedInstrumentId.EndsWith(SwapSuffix, StringComparison.Ordinal))
            {
                error = "合约代码必须是完整的 OKX 永续合约 ID，例如 RAM-USDT-SWAP。";
                normalizedInstrumentId = null;
                return false;
            }

            return true;
        }

        private static AppSettings TryLoadLegacySettings()
        {
            string legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LegacyConfigFileName);
            if (!File.Exists(legacyPath))
            {
                return null;
            }

            try
            {
                AppSettings legacySettings = Deserialize(legacyPath);
                AppSettings normalizedSettings;
                string validationError;
                return TryNormalize(legacySettings, out normalizedSettings, out validationError)
                    ? normalizedSettings
                    : null;
            }
            catch (Exception ex)
            {
                Logger.Error("迁移旧版 config.json 失败", ex);
                return null;
            }
        }

        private static AppSettings Deserialize(string path)
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            return serializer.Deserialize<AppSettings>(json);
        }
    }

    internal sealed class WindowState
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool TopMost { get; set; }
        public string RangeKey { get; set; }
        public string InstrumentId { get; set; }
    }

    internal static class StateStore
    {
        private const string StateFileName = "state.json";
        private const int MinimumWindowWidth = 420;
        private const int MinimumWindowHeight = 280;

        internal static string AppDataDirectory
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StockPerpTicker");
            }
        }

        internal static WindowState Load()
        {
            string path = Path.Combine(AppDataDirectory, StateFileName);
            try
            {
                if (!File.Exists(path))
                {
                    return CreateDefault();
                }

                string json = File.ReadAllText(path, Encoding.UTF8);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                WindowState state = serializer.Deserialize<WindowState>(json);
                if (state == null || state.Width < MinimumWindowWidth || state.Height < MinimumWindowHeight)
                {
                    return CreateDefault();
                }

                state.RangeKey = RangeDefinition.Find(state.RangeKey).Key;
                return state;
            }
            catch (Exception ex)
            {
                Logger.Error("读取窗口状态失败", ex);
                return CreateDefault();
            }
        }

        internal static void Save(WindowState state)
        {
            try
            {
                Directory.CreateDirectory(AppDataDirectory);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                File.WriteAllText(Path.Combine(AppDataDirectory, StateFileName), serializer.Serialize(state), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Logger.Error("保存窗口状态失败", ex);
            }
        }

        private static WindowState CreateDefault()
        {
            return new WindowState
            {
                Left = -1,
                Top = -1,
                Width = 500,
                Height = 360,
                TopMost = false,
                RangeKey = RangeDefinition.DefaultKey,
                InstrumentId = null
            };
        }
    }

    internal static class AutoStartManager
    {
        private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RegistryValueName = "StockPerpTicker";

        internal static bool IsEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
            {
                return key != null && key.GetValue(RegistryValueName) != null;
            }
        }

        internal static void SetEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("无法打开当前用户的开机启动注册表项。");
                }

                if (enabled)
                {
                    string executable = System.Windows.Forms.Application.ExecutablePath;
                    key.SetValue(RegistryValueName, "\"" + executable + "\"");
                }
                else
                {
                    key.DeleteValue(RegistryValueName, false);
                }
            }
        }
    }

    internal static class Logger
    {
        private const long MaximumLogBytes = 1024L * 1024L;
        private static readonly object SyncRoot = new object();
        private static string _logDirectory;
        private static string _logPath;

        internal static void Initialize()
        {
            _logDirectory = Path.Combine(StateStore.AppDataDirectory, "logs");
            _logPath = Path.Combine(_logDirectory, "app.log");
            try
            {
                Directory.CreateDirectory(_logDirectory);
                RotateIfNeeded();
            }
            catch
            {
                _logPath = null;
            }
        }

        internal static void Info(string message)
        {
            Write("INFO", message, null);
        }

        internal static void Error(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        private static void Write(string level, string message, Exception exception)
        {
            if (string.IsNullOrEmpty(_logPath))
            {
                return;
            }

            lock (SyncRoot)
            {
                try
                {
                    RotateIfNeeded();
                    StringBuilder line = new StringBuilder();
                    line.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                    line.Append(" [").Append(level).Append("] ").Append(message);
                    if (exception != null)
                    {
                        line.Append(" | ").Append(exception);
                    }

                    line.AppendLine();
                    File.AppendAllText(_logPath, line.ToString(), new UTF8Encoding(false));
                }
                catch
                {
                    // Logging must never terminate the quote window.
                }
            }
        }

        private static void RotateIfNeeded()
        {
            if (!File.Exists(_logPath) || new FileInfo(_logPath).Length < MaximumLogBytes)
            {
                return;
            }

            string firstArchive = Path.Combine(_logDirectory, "app.1.log");
            string secondArchive = Path.Combine(_logDirectory, "app.2.log");
            if (File.Exists(secondArchive))
            {
                File.Delete(secondArchive);
            }

            if (File.Exists(firstArchive))
            {
                File.Move(firstArchive, secondArchive);
            }

            File.Move(_logPath, firstArchive);
        }
    }

    internal static class AppIconFactory
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        internal static Icon Create()
        {
            try
            {
                Icon embeddedIcon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
                if (embeddedIcon != null)
                {
                    using (embeddedIcon)
                    {
                        return (Icon)embeddedIcon.Clone();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("读取内嵌程序图标失败", ex);
            }

            using (Bitmap bitmap = new Bitmap(32, 32))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (SolidBrush background = new SolidBrush(Color.FromArgb(8, 153, 129)))
            using (Pen whitePen = new Pen(Color.White, 2f))
            using (SolidBrush whiteBrush = new SolidBrush(Color.White))
            {
                graphics.Clear(Color.Transparent);
                graphics.FillEllipse(background, 1, 1, 30, 30);
                graphics.DrawLine(whitePen, 10, 7, 10, 24);
                graphics.FillRectangle(whiteBrush, 7, 11, 6, 8);
                graphics.DrawLine(whitePen, 21, 8, 21, 25);
                graphics.FillRectangle(whiteBrush, 18, 15, 6, 6);
                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle))
                    {
                        return (Icon)temporary.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }
    }

    internal static class TaskbarIntegration
    {
        private const string ApplicationUserModelId = "StockPerpTicker.Desktop";

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string applicationId);

        internal static void Initialize()
        {
            try
            {
                int result = SetCurrentProcessExplicitAppUserModelID(ApplicationUserModelId);
                if (result != default(int))
                {
                    Logger.Info("设置任务栏应用标识返回：" + result);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("设置任务栏应用标识失败", ex);
            }
        }
    }

    internal static class WindowActivation
    {
        private const int MaximizeWindowCommand = 3;
        private const int ShowNormalWindowCommand = 1;
        private const uint ShowWindowPositionFlag = 0x0040;
        private static readonly IntPtr TopWindow = IntPtr.Zero;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr windowHandle, int command);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr windowHandle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr windowHandle,
            IntPtr insertAfter,
            int left,
            int top,
            int width,
            int height,
            uint flags);

        internal static void RestoreAndActivate(IntPtr windowHandle, Rectangle normalBounds, bool maximize)
        {
            ShowWindow(windowHandle, ShowNormalWindowCommand);
            SetWindowPos(
                windowHandle,
                TopWindow,
                normalBounds.Left,
                normalBounds.Top,
                normalBounds.Width,
                normalBounds.Height,
                ShowWindowPositionFlag);
            if (maximize)
            {
                ShowWindow(windowHandle, MaximizeWindowCommand);
            }

            SetForegroundWindow(windowHandle);
        }
    }

    internal static class MemoryManager
    {
        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize);

        internal static void TrimWorkingSet()
        {
            try
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized);
                GC.WaitForPendingFinalizers();
                using (Process process = Process.GetCurrentProcess())
                {
                    SetProcessWorkingSetSize(process.Handle, new IntPtr(-1), new IntPtr(-1));
                }
            }
            catch (Exception ex)
            {
                Logger.Error("整理工作集失败", ex);
            }
        }
    }
}
