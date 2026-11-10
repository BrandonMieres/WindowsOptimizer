using System;
using System.Threading.Tasks;

namespace WindowsOptimizer.Core.Abstractions;

public interface IDialogService
{
    Task<bool> ShowConfirmationAsync(string title, string message, string confirmText = "Aceptar", string cancelText = "Cancelar");
    Task ShowMessageAsync(string title, string message);
}
