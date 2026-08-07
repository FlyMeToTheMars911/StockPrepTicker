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
    internal sealed class AppConfig
    {
        public string instrumentId { get; set; }
        public int refreshIntervalMilliseconds { get; set; }
        public int[] movingAverages { get; set; }
        public bool showTaskbarTickerOnMinimize { get; set; }
        public string taskbarTickerPosition { get; set; }
        internal TaskbarTickerPosition TickerPosition { get; set; }
    }

    internal enum TaskbarTickerPosition
    {
        BottomLeft,
        BottomRight
    }

    internal sealed class ConfigLoadResult
    {
        internal AppConfig Config { get; set; }
        internal string Error { get; set; }
        internal string Path { get; set; }

        internal bool IsValid
        {
            get { return Config != null && string.IsNullOrEmpty(Error); }
        }
    }

    internal static class ConfigStore
    {
        private const string ConfigFileName = "config.json";
        private const string SwapSuffix = "-SWAP";
        private const int DefaultRefreshIntervalMilliseconds = 1000;
        private const int MinimumRefreshIntervalMilliseconds = 250;
        private const int MaximumRefreshIntervalMilliseconds = 60000;
        private const string BottomLeftTickerPosition = "bottomLeft";
        private const string BottomRightTickerPosition = "bottomRight";
        private static readonly int[] DefaultMovingAverages = { 5, 10, 20, 50 };
        private static readonly int[] SupportedMovingAverages = { 5, 10, 20, 50, 100, 200 };

        internal static ConfigLoadResult Load()
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            ConfigLoadResult result = new ConfigLoadResult { Path = path };
            try
            {
                if (!File.Exists(path))
                {
                    result.Error = "配置文件不存在：" + path;
                    return result;
                }

                string json = File.ReadAllText(path, Encoding.UTF8);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                AppConfig config = serializer.Deserialize<AppConfig>(json);
                if (config == null || string.IsNullOrWhiteSpace(config.instrumentId))
                {
                    result.Error = "配置项 instrumentId 不能为空。配置文件：" + path;
                    return result;
                }

                string instrumentId = config.instrumentId.Trim().ToUpperInvariant();
                if (!instrumentId.EndsWith(SwapSuffix, StringComparison.Ordinal))
                {
                    result.Error = "instrumentId 必须是完整的 OKX 永续合约 ID，例如 RAM-USDT-SWAP。配置文件：" + path;
                    return result;
                }

                config.instrumentId = instrumentId;
                if (config.refreshIntervalMilliseconds == default(int))
                {
                    config.refreshIntervalMilliseconds = DefaultRefreshIntervalMilliseconds;
                }

                if (config.refreshIntervalMilliseconds < MinimumRefreshIntervalMilliseconds
                    || config.refreshIntervalMilliseconds > MaximumRefreshIntervalMilliseconds)
                {
                    result.Error = "refreshIntervalMilliseconds 必须在 "
                        + MinimumRefreshIntervalMilliseconds + " 到 " + MaximumRefreshIntervalMilliseconds
                        + " 之间。配置文件：" + path;
                    return result;
                }

                if (config.movingAverages == null)
                {
                    config.movingAverages = (int[])DefaultMovingAverages.Clone();
                }

                HashSet<int> uniqueMovingAverages = new HashSet<int>();
                List<int> normalizedMovingAverages = new List<int>();
                foreach (int period in config.movingAverages)
                {
                    if (Array.IndexOf(SupportedMovingAverages, period) < default(int))
                    {
                        result.Error = "不支持 MA" + period + "。支持的移动平均线为：MA5、MA10、MA20、MA50、MA100、MA200。配置文件：" + path;
                        return result;
                    }

                    if (uniqueMovingAverages.Add(period))
                    {
                        normalizedMovingAverages.Add(period);
                    }
                }

                config.movingAverages = normalizedMovingAverages.ToArray();
                if (string.IsNullOrWhiteSpace(config.taskbarTickerPosition))
                {
                    config.taskbarTickerPosition = BottomRightTickerPosition;
                }

                string tickerPosition = config.taskbarTickerPosition.Trim();
                if (string.Equals(tickerPosition, BottomLeftTickerPosition, StringComparison.OrdinalIgnoreCase))
                {
                    config.taskbarTickerPosition = BottomLeftTickerPosition;
                    config.TickerPosition = TaskbarTickerPosition.BottomLeft;
                }
                else if (string.Equals(tickerPosition, BottomRightTickerPosition, StringComparison.OrdinalIgnoreCase))
                {
                    config.taskbarTickerPosition = BottomRightTickerPosition;
                    config.TickerPosition = TaskbarTickerPosition.BottomRight;
                }
                else
                {
                    result.Error = "taskbarTickerPosition 仅支持 bottomLeft 或 bottomRight。配置文件：" + path;
                    return result;
                }

                result.Config = config;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = "配置文件无法解析：" + ex.Message + Environment.NewLine + path;
                Logger.Error("读取配置失败", ex);
                return result;
            }
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
                RangeKey = "1D"
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
        private const int RestoreWindowCommand = 9;
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

        internal static void RestoreAndActivate(IntPtr windowHandle, Rectangle bounds)
        {
            ShowWindow(windowHandle, RestoreWindowCommand);
            SetWindowPos(
                windowHandle,
                TopWindow,
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                ShowWindowPositionFlag);
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
