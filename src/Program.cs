using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StockPerpTicker
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            using (SingleInstanceCoordinator instanceCoordinator = new SingleInstanceCoordinator())
            {
                if (!instanceCoordinator.IsPrimaryInstance)
                {
                    instanceCoordinator.SignalPrimaryInstance();
                    return;
                }

                RunPrimaryInstance(instanceCoordinator);
            }
        }

        private static void RunPrimaryInstance(SingleInstanceCoordinator instanceCoordinator)
        {
            Logger.Initialize();
            Logger.Info("StockPerpTicker 启动。");
            TaskbarIntegration.Initialize();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs args)
            {
                Logger.Error("UI 未处理异常", args.Exception);
                MessageBox.Show("程序发生异常，详细信息已写入日志。\n\n" + args.Exception.Message, "StockPerpTicker", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs args)
            {
                Logger.Error("未处理异常", args.ExceptionObject as Exception);
            };
            TaskScheduler.UnobservedTaskException += delegate(object sender, UnobservedTaskExceptionEventArgs args)
            {
                Logger.Error("未观察到的任务异常", args.Exception);
                args.SetObserved();
            };

            ConfigLoadResult config = ConfigStore.Load();
            WindowState state = StateStore.Load();
            using (MainForm form = new MainForm(config, state))
            {
                IntPtr mainWindowHandle = form.Handle;
                instanceCoordinator.StartListening(form.RestoreFromExternalLaunch);
                Application.Run(form);
            }

            Logger.Info("StockPerpTicker 退出。");
        }
    }

    internal sealed class SingleInstanceCoordinator : IDisposable
    {
        private const string MutexName = "Local\\StockPerpTicker.SingleInstance.v1";
        private const string ActivationEventName = "Local\\StockPerpTicker.Activate.v1";
        private const string ApplicationProcessName = "StockPerpTicker";
        private const int ActivationEventIndex = 0;
        private const int ShutdownEventIndex = 1;
        private const int ListenerJoinTimeoutMilliseconds = 1000;
        private readonly Mutex _instanceMutex;
        private readonly EventWaitHandle _activationEvent;
        private readonly EventWaitHandle _shutdownEvent;
        private Thread _listenerThread;
        private bool _disposed;

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int processId);

        internal SingleInstanceCoordinator()
        {
            _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
            _shutdownEvent = new EventWaitHandle(false, EventResetMode.ManualReset);
            bool createdNew;
            _instanceMutex = new Mutex(true, MutexName, out createdNew);
            IsPrimaryInstance = createdNew;
        }

        internal bool IsPrimaryInstance { get; private set; }

        internal void SignalPrimaryInstance()
        {
            try
            {
                GrantForegroundPermissionToPrimaryInstance();
            }
            catch (Exception)
            {
                // The activation event still restores the window if Windows denies foreground permission.
            }

            _activationEvent.Set();
        }

        private static void GrantForegroundPermissionToPrimaryInstance()
        {
            int currentProcessId = Process.GetCurrentProcess().Id;
            foreach (Process process in Process.GetProcessesByName(ApplicationProcessName))
            {
                using (process)
                {
                    if (process.Id != currentProcessId)
                    {
                        AllowSetForegroundWindow(process.Id);
                    }
                }
            }
        }

        internal void StartListening(Action activationAction)
        {
            if (!IsPrimaryInstance || _listenerThread != null)
            {
                return;
            }

            _listenerThread = new Thread(new ThreadStart(delegate
            {
                WaitHandle[] waitHandles = { _activationEvent, _shutdownEvent };
                while (true)
                {
                    int signaledIndex = WaitHandle.WaitAny(waitHandles);
                    if (signaledIndex == ShutdownEventIndex)
                    {
                        return;
                    }

                    if (signaledIndex == ActivationEventIndex)
                    {
                        try
                        {
                            activationAction();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("处理重复启动的窗口恢复请求失败", ex);
                        }
                    }
                }
            }))
            {
                IsBackground = true,
                Name = "StockPerpTicker 单实例监听"
            };
            _listenerThread.Start();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _shutdownEvent.Set();
            if (_listenerThread != null)
            {
                _listenerThread.Join(ListenerJoinTimeoutMilliseconds);
            }

            if (IsPrimaryInstance)
            {
                _instanceMutex.ReleaseMutex();
            }

            _instanceMutex.Dispose();
            _activationEvent.Dispose();
            _shutdownEvent.Dispose();
        }
    }
}
