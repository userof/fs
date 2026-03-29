using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using FileManager.Models;
using FileManager.ViewModels;

namespace FileManager.Views;

public partial class FilePanelView : UserControl
{
    private Point _dragStartPoint;
    private bool _isDragging;
    private const double DragThreshold = 8;

    // Shared drag state between panels
    private static FileItem? _draggedItem;

    public FilePanelView()
    {
        InitializeComponent();

        FileGrid.AddHandler(DragDrop.DropEvent, OnDrop);
        FileGrid.AddHandler(DragDrop.DragOverEvent, OnDragOver);

        GotFocus += OnPanelGotFocus;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        FileGrid.PointerPressed += FileGrid_PointerPressed;
        FileGrid.PointerMoved += FileGrid_PointerMoved;
    }

    private void FileGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(FileGrid).Properties.IsLeftButtonPressed)
        {
            _dragStartPoint = e.GetPosition(FileGrid);
            _isDragging = false;
        }
    }

    private async void FileGrid_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!e.GetCurrentPoint(FileGrid).Properties.IsLeftButtonPressed)
            return;

        if (_isDragging)
            return;

        var pos = e.GetPosition(FileGrid);
        var diff = pos - _dragStartPoint;
        if (Math.Abs(diff.X) < DragThreshold && Math.Abs(diff.Y) < DragThreshold)
            return;

        if (DataContext is not FilePanelViewModel vm || vm.SelectedItem == null)
            return;

        _isDragging = true;
        _draggedItem = vm.SelectedItem;

#pragma warning disable CS0618 // DataObject is obsolete but needed for DoDragDrop
        var data = new DataObject();
        data.Set("FileItem", "drag");
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy | DragDropEffects.Move);
#pragma warning restore CS0618

        _isDragging = false;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = _draggedItem != null
            ? DragDropEffects.Copy | DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not FilePanelViewModel vm)
            return;

        var item = _draggedItem;
        if (item == null)
            return;

        // Don't drop onto the same directory
        if (string.Equals(System.IO.Path.GetDirectoryName(item.FullPath),
                vm.CurrentPath, StringComparison.OrdinalIgnoreCase))
            return;

        e.Handled = true;
        ShowDropMenu(item, vm);
    }

    private void ShowDropMenu(FileItem item, FilePanelViewModel targetVm)
    {
        var menu = new ContextMenu();

        var copyItem = new MenuItem { Header = "Copy Here" };
        copyItem.Click += (_, _) =>
        {
            targetVm.DropCopy(item.FullPath, item.IsDirectory);
        };

        var moveItem = new MenuItem { Header = "Move Here" };
        moveItem.Click += (_, _) =>
        {
            targetVm.DropMove(item.FullPath, item.IsDirectory);
        };

        var cancelItem = new MenuItem { Header = "Cancel" };

        menu.Items.Add(copyItem);
        menu.Items.Add(moveItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(cancelItem);

        menu.Open(FileGrid);
    }

    private void FolderMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
            btn.Flyout?.ShowAt(btn);
    }

    private void OnPanelGotFocus(object? sender, GotFocusEventArgs e)
    {
        // Deactivate all other panels in the window
        var window = this.FindAncestorOfType<MainWindow>();
        if (window == null) return;

        DeactivateAllPanels(window);

        if (DataContext is FilePanelViewModel vm)
            vm.IsActive = true;

        PanelBorder.Classes.Add("active");
    }

    private static void DeactivateAllPanels(Visual root)
    {
        foreach (var panel in FindDescendants<FilePanelView>(root))
        {
            if (panel.DataContext is FilePanelViewModel vm)
                vm.IsActive = false;
            panel.PanelBorder.Classes.Remove("active");
        }
    }

    private static IEnumerable<T> FindDescendants<T>(Visual root) where T : Visual
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is T match)
                yield return match;
            if (child is Visual v)
            {
                foreach (var desc in FindDescendants<T>(v))
                    yield return desc;
            }
        }
    }

    private void DataGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is FilePanelViewModel vm && vm.SelectedItem is not null)
            vm.OpenCommand.Execute(vm.SelectedItem);
    }

    private void Breadcrumb_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is FilePanelViewModel vm)
        {
            vm.IsEditingAddress = true;
            // Focus the address box after it becomes visible
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                AddressBox.Focus();
                AddressBox.SelectAll();
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    private void AddressBar_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is FilePanelViewModel vm)
        {
            vm.FinishEditAddressCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && DataContext is FilePanelViewModel vm2)
        {
            vm2.AddressBarPath = vm2.CurrentPath;
            vm2.IsEditingAddress = false;
            e.Handled = true;
        }
    }

    private void AddressBar_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FilePanelViewModel vm && vm.IsEditingAddress)
            vm.FinishEditAddressCommand.Execute(null);
    }

    private void RenameBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not FilePanelViewModel vm) return;
        if (e.Key == Key.Enter)
        {
            vm.ConfirmRenameCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelRenameCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void NewFolderBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not FilePanelViewModel vm) return;
        if (e.Key == Key.Enter)
        {
            vm.ConfirmNewFolderCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelNewFolderCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ShellMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not FilePanelViewModel vm) return;
        if (vm.SelectedItem == null) return;

        FileGrid.ContextMenu?.Close();

        var pos = GetScreenCursorPosition();
        vm.ShowWindowsContextMenu(vm.SelectedItem.FullPath, pos.x, pos.y);
    }

    private void FolderShellMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not FilePanelViewModel vm) return;

        FolderMenuButton.Flyout?.Hide();

        var pos = GetScreenCursorPosition();
        vm.ShowFolderWindowsContextMenu(pos.x, pos.y);
    }

    private static (int x, int y) GetScreenCursorPosition()
    {
        GetCursorPos(out var pt);
        return (pt.X, pt.Y);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}
