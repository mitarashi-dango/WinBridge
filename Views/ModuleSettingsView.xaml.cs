using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinBridge.Models;
using WinBridge.ViewModels;

namespace WinBridge.Views;

public partial class ModuleSettingsView : UserControl
{
    private Point _dragStart;
    public ModuleSettingsView() => InitializeComponent();

    private void VisibilityCheckBox_Click(object sender, RoutedEventArgs e) => Save();
    private void FavoriteCheckBox_Click(object sender, RoutedEventArgs e) => Save();

    private void Save()
    {
        if (DataContext is ModuleSettingsViewModel viewModel && viewModel.SaveCommand.CanExecute(null))
            viewModel.SaveCommand.Execute(null);
    }

    private void NestedList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        PageScrollViewer.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = Mouse.MouseWheelEvent,
            Source = sender
        });
    }

    private void ModuleList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _dragStart = e.GetPosition(null);

    private void ModuleList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext is ModuleDefinition module)
            DragDrop.DoDragDrop(ModuleList, module, DragDropEffects.Move);
    }

    private async void ModuleList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ModuleDefinition)) is not ModuleDefinition dragged ||
            DataContext is not ModuleSettingsViewModel viewModel) return;
        var target = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as ModuleDefinition;
        var index = target is null ? viewModel.Modules.Count - 1 : viewModel.Modules.IndexOf(target);
        await viewModel.MoveToAsync(dragged, index);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
