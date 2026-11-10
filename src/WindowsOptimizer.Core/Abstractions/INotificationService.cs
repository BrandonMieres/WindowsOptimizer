using System;

namespace WindowsOptimizer.Core.Abstractions;

public interface INotificationService
{
    void ShowProgress(string title, string message);
    void ShowSuccess(string title, string message, int autoDismissMs = 3000);
    void ShowError(string title, string message, int autoDismissMs = 4000);
    void Dismiss();
}

