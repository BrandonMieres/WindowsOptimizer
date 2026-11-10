using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using WindowsOptimizer.Core.Abstractions;

namespace WindowsOptimizer_App.Services;

public sealed class DialogService : IDialogService
{
    public async Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Aceptar", string cancelText = "Cancelar")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = confirmText,
            CloseButtonText = cancelText,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow?.Content?.XamlRoot
        };

        if (dialog.XamlRoot == null) return false;

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = App.MainWindow?.Content?.XamlRoot
        };

        if (dialog.XamlRoot != null)
        {
            await dialog.ShowAsync();
        }
    }
}
