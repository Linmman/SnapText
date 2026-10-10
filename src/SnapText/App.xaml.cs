using System.Threading;
using System.Windows;
using SnapText.Cli;
using SnapText.Core;

namespace SnapText;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private SnapTextController? _controller;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error("Unhandled dispatcher exception", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error("Unhandled exception", args.ExceptionObject as Exception);

        // 无界面模式（--ocr / --capture / --version）：即使已有实例在运行也可执行
        if (CliRunner.TryRun(e.Args, out var exitCode))
        {
            Shutdown(exitCode);
            return;
        }

        _instanceMutex = new Mutex(true, @"Local\SnapText.SingleInstance", out var isNew);
        if (!isNew)
        {
            Log.Info("Another instance is already running; exiting.");
            Shutdown(0);
            return;
        }

        try
        {
            _controller = new SnapTextController();
            _controller.Start();
        }
        catch (Exception ex)
        {
            Log.Error("Startup failed", ex);
            MessageBox.Show(ex.ToString(), "SnapText 启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
