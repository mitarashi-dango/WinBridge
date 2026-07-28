using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinBridge.Models;
using WinBridge.ViewModels;

namespace WinBridge.Views;

public partial class SettingsCatalogView : UserControl
{
    private Point _dragStart;
    public SettingsCatalogView() => InitializeComponent();

    private void SettingOptionCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsCatalogViewModel viewModel && viewModel.SaveCommand.CanExecute(null))
            viewModel.SaveCommand.Execute(null);
    }

    private void SelectedList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _dragStart = e.GetPosition(null);

    private void SelectedList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext is SettingDefinition setting)
            DragDrop.DoDragDrop(SelectedList, setting, DragDropEffects.Move);
    }

    private async void SelectedList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(SettingDefinition)) is not SettingDefinition dragged ||
            DataContext is not SettingsCatalogViewModel viewModel) return;
        var target = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as SettingDefinition;
        var index = target is null ? viewModel.SelectedSettings.Count - 1 : viewModel.SelectedSettings.IndexOf(target);
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
