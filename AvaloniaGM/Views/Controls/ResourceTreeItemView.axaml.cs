using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaGM.ViewModels;

namespace AvaloniaGM.Views.Controls
{
    public partial class ResourceTreeItemView : UserControl
    {
        public ResourceTreeItemView()
        {
            InitializeComponent();
        }

        private ResourceTreeItemViewModel? TreeItem => DataContext as ResourceTreeItemViewModel;

        private MainWindowViewModel? MainViewModel =>
            TopLevel.GetTopLevel(this)?.DataContext as MainWindowViewModel;

        private void RowBorder_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (TreeItem is null || MainViewModel is null)
            {
                return;
            }

            MainViewModel.SelectTreeItem(TreeItem);
        }

        private void RowBorder_OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (TreeItem is null || MainViewModel is null)
            {
                return;
            }

            MainViewModel.SelectTreeItem(TreeItem);

            if (TreeItem.IsFolder)
            {
                MainViewModel.ToggleTreeItemExpansion(TreeItem);
            }
            else
            {
                MainViewModel.OpenResourceTab(TreeItem);
            }

            e.Handled = true;
        }

        private void ExpandCollapseButton_OnClick(object? sender, RoutedEventArgs e)
        {
            if (TreeItem is null || MainViewModel is null)
            {
                return;
            }

            MainViewModel.ToggleTreeItemExpansion(TreeItem);
            e.Handled = true;
        }

        private void CreateOrInsertResourceMenuItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: ResourceTreeItemViewModel treeItem })
            {
                return;
            }

            MainViewModel?.CreateOrInsertResource(treeItem);
            e.Handled = true;
        }

        private void CreateOrInsertFolderMenuItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: ResourceTreeItemViewModel treeItem })
            {
                return;
            }

            MainViewModel?.CreateOrInsertFolder(treeItem);
            e.Handled = true;
        }

        private void DeleteMenuItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: ResourceTreeItemViewModel treeItem })
            {
                return;
            }

            MainViewModel?.DeleteTreeItem(treeItem);
            e.Handled = true;
        }
    }
}
