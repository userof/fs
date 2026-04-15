using System;
using System.Collections.Generic;
using System.Linq;
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
    private static FilePanelViewModel? _dragSourceVm;

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

    // ── Keyboard shortcuts ──

    private void Panel_KeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not FilePanelViewModel vm) return;

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        switch (e.Key)
        {
            case Key.F5:
                vm.RefreshCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F2:
                if (!vm.IsEditingAddress)
                {
                    vm.StartRenameCommand.Execute(null);
                    e.Handled = true;
                }
                break;
            case Key.Delete:
                if (!vm.IsEditingAddress && !vm.IsRenaming && !vm.IsCreatingFolder)
                {
                    vm.DeleteCommand.Execute(null);
                    e.Handled = true;
                }
                break;
            case Key.Back:
                if (!vm.IsEditingAddress && !vm.IsRenaming && !vm.IsCreatingFolder)
                {
                    vm.GoBackCommand.Execute(null);
                    e.Handled = true;
                }
                break;
            case Key.Up when alt:
                vm.GoUpCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.L when ctrl:
                vm.StartEditAddressCommand.Execute(null);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => AddressBox?.Focus());
                e.Handled = true;
                break;
            case Key.N when ctrl:
                vm.StartNewFolderCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.C when ctrl:
                vm.CopyCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.X when ctrl:
                vm.CutCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.V when ctrl:
                vm.PasteCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F when ctrl:
                FilterBox?.Focus();
                e.Handled = true;
                break;
            case Key.P when ctrl:
                vm.TogglePreviewCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                if (vm.IsRenaming)
                    vm.CancelRenameCommand.Execute(null);
                else if (vm.IsCreatingFolder)
                    vm.CancelNewFolderCommand.Execute(null);
                else if (vm.IsEditingAddress)
                    vm.IsEditingAddress = false;
                else if (!string.IsNullOrEmpty(vm.FilterText))
                    vm.FilterText = string.Empty;
                e.Handled = true;
                break;
            case Key.Enter:
                if (!vm.IsEditingAddress && !vm.IsRenaming && !vm.IsCreatingFolder && vm.SelectedItem != null)
                {
                    vm.OpenCommand.Execute(vm.SelectedItem);
                    e.Handled = true;
                }
                break;
        }
    }

    // ── Multi-select sync ──

    private void FileGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not FilePanelViewModel vm) return;

        vm.SelectedItems.Clear();
        foreach (var item in FileGrid.SelectedItems!)
        {
            if (item is FileItem fi)
                vm.SelectedItems.Add(fi);
        }
        vm.UpdateStatus();
    }

    // ── Double-click open ──

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

    // ── Breadcrumb click to switch to address bar ──

    private void Breadcrumb_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is FilePanelViewModel vm)
        {
            vm.IsEditingAddress = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                AddressBox.Focus();
                AddressBox.SelectAll();
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    // ── Address bar ──

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

    // ── Rename / New Folder ──

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

    // ── Folder menu ──

    private void FolderMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
            btn.Flyout?.ShowAt(btn);
    }

    // ── Shell context menus ──

    private void ShellMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not FilePanelViewModel vm) return;

        var selected = vm.SelectedItems.Count > 0
            ? vm.SelectedItems.Select(i => i.FullPath).ToArray()
            : vm.SelectedItem != null
                ? new[] { vm.SelectedItem.FullPath }
                : Array.Empty<string>();

        if (selected.Length == 0) return;

        FileGrid.ContextMenu?.Close();

        var pos = GetScreenCursorPosition();
        vm.ShowWindowsContextMenu(selected, pos.x, pos.y);
    }

    private void FolderShellMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not FilePanelViewModel vm) return;

        FolderMenuButton.Flyout?.Hide();

        var pos = GetScreenCursorPosition();
        vm.ShowFolderWindowsContextMenu(pos.x, pos.y);
    }

    // ── Drag and Drop ──

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
        _dragSourceVm = vm;

#pragma warning disable CS0618
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
        var sourceVm = _dragSourceVm;
        if (item == null)
            return;

        // Don't drop onto the same directory
        if (string.Equals(System.IO.Path.GetDirectoryName(item.FullPath),
                vm.CurrentPath, StringComparison.OrdinalIgnoreCase))
            return;

        e.Handled = true;
        ShowDropMenu(item, vm, sourceVm);
    }

    private void ShowDropMenu(FileItem item, FilePanelViewModel targetVm, FilePanelViewModel? sourceVm)
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
            // Refresh source panel too
            sourceVm?.Refresh();
        };

        var cancelItem = new MenuItem { Header = "Cancel" };

        menu.Items.Add(copyItem);
        menu.Items.Add(moveItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(cancelItem);

        menu.Open(FileGrid);
    }

    // ── Helpers ──

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
