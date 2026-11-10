using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WindowsOptimizer_App.ViewModels;

namespace WindowsOptimizer_App.Views;

public sealed partial class DiskPage : Page
{
    public DiskViewModel ViewModel { get; }

    public DiskPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<DiskViewModel>();

        // Actualizar visibilidad cuando cambie el RootNode
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(DiskViewModel.RootNode))
            {
                DispatcherQueue.TryEnqueue(() => UpdateEmptyState());
            }
        };
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyState.Visibility = ViewModel.RootNode == null ? Visibility.Visible : Visibility.Collapsed;
        DiskTreeView.Visibility = ViewModel.RootNode == null ? Visibility.Collapsed : Visibility.Visible;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static")]
    private void DiskTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is WindowsOptimizer.Core.Models.DiskNode { IsDirectory: false })
            return;

        var container = sender.ContainerFromItem(args.InvokedItem) as TreeViewItem;
        if (container != null)
        {
            container.IsExpanded = !container.IsExpanded;
        }
    }

    private async void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.DataContext is WindowsOptimizer.Core.Models.DiskNode node)
        {
            await ViewModel.DeleteFolderAsync(node);
        }
    }
}
