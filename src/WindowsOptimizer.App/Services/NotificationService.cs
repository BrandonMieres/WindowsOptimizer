using System;
using WindowsOptimizer.Core.Abstractions;

namespace WindowsOptimizer_App.Services;

public sealed class NotificationService : INotificationService
{
    public void ShowProgress(string title, string message)
    {
        if (App.MainWindow is MainWindow main)
        {
            main.ShowToastProgress(title, message);
        }
    }

    public void ShowSuccess(string title, string message, int autoDismissMs = 3000)
    {
        if (App.MainWindow is MainWindow main)
        {
            main.ShowToastSuccess(title, message, autoDismissMs);
        }
    }

    public void ShowError(string title, string message, int autoDismissMs = 4000)
    {
        if (App.MainWindow is MainWindow main)
        {
            main.ShowToastError(title, message, autoDismissMs);
        }
    }

    public void Dismiss()
    {
        if (App.MainWindow is MainWindow main)
        {
            main.DismissToast();
        }
    }
}

