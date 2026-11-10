using WindowsOptimizer.Core.Abstractions;
using Xunit;

namespace WindowsOptimizer.Core.Tests;

public class NotificationServiceContractTests
{
    private sealed class TestNotificationService : INotificationService
    {
        public string? LastTitle { get; private set; }
        public string? LastMessage { get; private set; }
        public string? LastState { get; private set; }

        public void ShowProgress(string title, string message)
        {
            LastState = "Progress";
            LastTitle = title;
            LastMessage = message;
        }

        public void ShowSuccess(string title, string message, int autoDismissMs = 3000)
        {
            LastState = "Success";
            LastTitle = title;
            LastMessage = message;
        }

        public void ShowError(string title, string message, int autoDismissMs = 4000)
        {
            LastState = "Error";
            LastTitle = title;
            LastMessage = message;
        }

        public void Dismiss()
        {
            LastState = "Dismissed";
        }
    }

    [Fact]
    public void NotificationService_RecordsStateTransitionsCorrectly()
    {
        var service = new TestNotificationService();

        service.ShowProgress("Aplicando", "Modo Juego");
        Assert.Equal("Progress", service.LastState);
        Assert.Equal("Aplicando", service.LastTitle);
        Assert.Equal("Modo Juego", service.LastMessage);

        service.ShowSuccess("Completado", "Modo Juego aplicado");
        Assert.Equal("Success", service.LastState);
        Assert.Equal("Completado", service.LastTitle);
        Assert.Equal("Modo Juego aplicado", service.LastMessage);

        service.Dismiss();
        Assert.Equal("Dismissed", service.LastState);
    }
}

