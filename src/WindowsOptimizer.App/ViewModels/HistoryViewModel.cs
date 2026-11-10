using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsOptimizer.Core.Abstractions;
using WindowsOptimizer.Core.Models;
using WindowsOptimizer.Scanners;

namespace WindowsOptimizer_App.ViewModels;

public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly IRollbackStore _rollbackStore;
    private readonly FixOrchestrator _fixOrchestrator;
    private readonly IDialogService _dialogService;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<ChangeRecord> History { get; } = new();

    public HistoryViewModel(IRollbackStore rollbackStore, FixOrchestrator fixOrchestrator, IDialogService dialogService)
    {
        _rollbackStore = rollbackStore;
        _fixOrchestrator = fixOrchestrator;
        _dialogService = dialogService;
    }

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        IsLoading = true;
        History.Clear();

        try
        {
            var records = await _rollbackStore.GetHistoryAsync();
            foreach (var record in records)
            {
                History.Add(record);
            }

            var active = records.Count(r => !r.IsReverted);
            StatusMessage = $"{records.Count} cambios registrados · {active} activos";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RevertChangeAsync(ChangeRecord? record)
    {
        if (record is null || record.IsReverted) return;

        bool confirm = await _dialogService.ShowConfirmationAsync(
            "Confirmar reversión",
            $"¿Estás seguro de que deseas revertir el cambio '{record.Name}'?",
            "Revertir",
            "Cancelar");

        if (!confirm) return;

        try
        {
            await _fixOrchestrator.RevertFixAsync(record.Id);
            StatusMessage = $"✓ Revertido: {record.Name}";

            // Refrescar la lista
            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al revertir: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RevertAllAsync()
    {
        bool confirm = await _dialogService.ShowConfirmationAsync(
            "Revertir todo",
            "¿Estás seguro de que deseas revertir TODOS los cambios aplicados?",
            "Sí, revertir todo",
            "Cancelar");

        if (!confirm) return;

        try
        {
            await _fixOrchestrator.RevertAllAsync();
            StatusMessage = "✓ Todos los cambios han sido revertidos.";
            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
}
