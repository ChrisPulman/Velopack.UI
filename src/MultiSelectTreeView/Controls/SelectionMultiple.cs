﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;

namespace System.Windows.Controls;

/// <summary>
/// Implements the logic for the multiple selection strategy.
/// </summary>
public sealed class SelectionMultiple : ISelectionStrategy
{
    public event EventHandler<PreviewSelectionChangedEventArgs> PreviewSelectionChanged;

    private readonly MultiSelectTreeView _treeView;
    private BorderSelectionLogic _borderSelectionLogic;
    private object _lastShiftRoot;

    public SelectionMultiple(MultiSelectTreeView treeView) => _treeView = treeView;

    public bool LastCancelAll { get; private set; }

    internal static bool IsControlKeyDown
    {
        get
        {
            return (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        }
    }

    private static bool IsShiftKeyDown
    {
        get
        {
            return (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        }
    }

    public void InvalidateLastShiftRoot(object item)
    {
        if (_lastShiftRoot == item)
        {
            _lastShiftRoot = null;
        }
    }

    public void ApplyTemplate() => _borderSelectionLogic = new BorderSelectionLogic(
           _treeView,
           _treeView.Template.FindName("selectionBorder", _treeView) as Border,
           _treeView.Template.FindName("scrollViewer", _treeView) as ScrollViewer,
           _treeView.Template.FindName("content", _treeView) as ItemsPresenter,
           MultiSelectTreeView.RecursiveTreeViewItemEnumerable(_treeView, false, false));

    public bool Select(MultiSelectTreeViewItem item)
    {
        if (item == null)
        {
            return false;
        }

        if (IsControlKeyDown)
        {
            if (_treeView.SelectedItems.Contains(item.DataContext))
            {
                return Deselect(item, true);
            }
            else
            {
                var e = new PreviewSelectionChangedEventArgs(true, item.DataContext);
                OnPreviewSelectionChanged(e);
                if (e.CancelAny)
                {
                    FocusHelper.Focus(item, true);
                    return false;
                }

                return SelectCore(item);
            }
        }
        else
        {
            if (_treeView.SelectedItems.Count == 1 &&
                _treeView.SelectedItems[0] == item.DataContext)
            {
                // Requested to select the single already-selected item. Don't change the selection.
                FocusHelper.Focus(item, true);
                _lastShiftRoot = item.DataContext;
                return true;
            }
            else
            {
                return SelectCore(item);
            }
        }
    }

    internal bool SelectByRectangle(MultiSelectTreeViewItem item)
    {
        var e = new PreviewSelectionChangedEventArgs(true, item.DataContext);
        OnPreviewSelectionChanged(e);
        if (e.CancelAny)
        {
            _lastShiftRoot = item.DataContext;
            return false;
        }

        if (!_treeView.SelectedItems.Contains(item.DataContext))
        {
            _treeView.SelectedItems.Add(item.DataContext);
        }
        _lastShiftRoot = item.DataContext;
        return true;
    }

    internal bool DeselectByRectangle(MultiSelectTreeViewItem item)
    {
        var e = new PreviewSelectionChangedEventArgs(false, item.DataContext);
        OnPreviewSelectionChanged(e);
        if (e.CancelAny)
        {
            _lastShiftRoot = item.DataContext;
            return false;
        }

        _treeView.SelectedItems.Remove(item.DataContext);
        if (item.DataContext == _lastShiftRoot)
        {
            _lastShiftRoot = null;
        }
        return true;
    }

    public bool SelectCore(MultiSelectTreeViewItem item)
    {
        if (item == null)
        {
            return false;
        }

        if (IsControlKeyDown)
        {
            if (!_treeView.SelectedItems.Contains(item.DataContext))
            {
                _treeView.SelectedItems.Add(item.DataContext);
            }
            _lastShiftRoot = item.DataContext;
        }
        else if (IsShiftKeyDown && _treeView.SelectedItems.Count > 0)
        {
            var firstSelectedItem = _lastShiftRoot ?? _treeView.SelectedItems.First();
            var shiftRootItem = _treeView.GetTreeViewItemsFor(new List<object> { firstSelectedItem }).First();

            var newSelection = _treeView.GetNodesToSelectBetween(shiftRootItem, item).Select(n => n.DataContext).ToList();
            // Make a copy of the list because we're modifying it while enumerating it
            var selectedItems = new object[_treeView.SelectedItems.Count];
            _treeView.SelectedItems.CopyTo(selectedItems, 0);
            // Remove all items no longer selected
            foreach (var selItem in selectedItems.Where(i => !newSelection.Contains(i)))
            {
                var e = new PreviewSelectionChangedEventArgs(false, selItem);
                OnPreviewSelectionChanged(e);
                if (e.CancelAll)
                {
                    FocusHelper.Focus(item);
                    return false;
                }
                if (!e.CancelThis)
                {
                    _treeView.SelectedItems.Remove(selItem);
                }
            }
            // Add new selected items
            foreach (var newItem in newSelection.Where(i => !selectedItems.Contains(i)))
            {
                var e = new PreviewSelectionChangedEventArgs(true, newItem);
                OnPreviewSelectionChanged(e);
                if (e.CancelAll)
                {
                    FocusHelper.Focus(item, true);
                    return false;
                }
                if (!e.CancelThis)
                {
                    _treeView.SelectedItems.Add(newItem);
                }
            }
        }
        else
        {
            if (_treeView.SelectedItems.Count > 0)
            {
                foreach (var selItem in new ArrayList(_treeView.SelectedItems))
                {
                    var e2 = new PreviewSelectionChangedEventArgs(false, selItem);
                    OnPreviewSelectionChanged(e2);
                    if (e2.CancelAll)
                    {
                        FocusHelper.Focus(item);
                        _lastShiftRoot = item.DataContext;
                        return false;
                    }
                    if (!e2.CancelThis)
                    {
                        _treeView.SelectedItems.Remove(selItem);
                    }
                }
            }

            var e = new PreviewSelectionChangedEventArgs(true, item.DataContext);
            OnPreviewSelectionChanged(e);
            if (e.CancelAny)
            {
                FocusHelper.Focus(item, true);
                _lastShiftRoot = item.DataContext;
                return false;
            }

            _treeView.SelectedItems.Add(item.DataContext);
            _lastShiftRoot = item.DataContext;
        }

        FocusHelper.Focus(item, true);
        return true;
    }

    public bool SelectCurrentBySpace()
    {
        // Another item was focused by Ctrl+Arrow key
        var item = GetFocusedItem();
        if (_treeView.SelectedItems.Contains(item.DataContext))
        {
            // With Ctrl key, toggle this item selection (deselect now).
            // Without Ctrl key, always select it (is already selected).
            if (IsControlKeyDown)
            {
                if (!Deselect(item, true)) return false;
                item.IsSelected = false;
            }
        }
        else
        {
            var e = new PreviewSelectionChangedEventArgs(true, item.DataContext);
            OnPreviewSelectionChanged(e);
            if (e.CancelAny)
            {
                FocusHelper.Focus(item, true);
                return false;
            }

            item.IsSelected = true;
            if (!_treeView.SelectedItems.Contains(item.DataContext))
            {
                _treeView.SelectedItems.Add(item.DataContext);
            }
        }
        FocusHelper.Focus(item, true);
        return true;
    }

    private MultiSelectTreeViewItem GetFocusedItem()
    {
        foreach (var item in MultiSelectTreeView.RecursiveTreeViewItemEnumerable(_treeView, false, false))
        {
            if (item.IsFocused) return item;
        }
        return null;
    }

    private bool SelectFromKey(MultiSelectTreeViewItem item)
    {
        if (item == null)
        {
            return false;
        }

        // If Ctrl is pressed just focus it, so it can be selected by Space. Otherwise select it.
        if (IsControlKeyDown)
        {
            FocusHelper.Focus(item, true);
            return true;
        }
        else
        {
            return SelectCore(item);
        }
    }

    public bool SelectNextFromKey()
    {
        var items = MultiSelectTreeView.RecursiveTreeViewItemEnumerable(_treeView, false, false).ToList();
        var item = MultiSelectTreeView.GetNextItem(GetFocusedItem(), items);
        return SelectFromKey(item);
    }

    public bool SelectPreviousFromKey()
    {
        var items = MultiSelectTreeView.RecursiveTreeViewItemEnumerable(_treeView, false, false).ToList();
        var item = MultiSelectTreeView.GetPreviousItem(GetFocusedItem(), items);
        return SelectFromKey(item);
    }

    public bool SelectFirstFromKey()
    {
        var items = MultiSelectTreeView.RecursiveTreeViewItemEnumerable(_treeView, false, false).ToList();
        var item = MultiSelectTreeView.GetFirstItem(items);
        return SelectFromKey(item);
    }

    public bool SelectLastFromKey()
    {
        var items = MultiSelectTreeView.RecursiveTreeViewItemEnumerable(_treeView, false, false).ToList();
        var item = MultiSelectTreeView.GetLastItem(items);
        return SelectFromKey(item);
    }

    private bool SelectPageUpDown(bool down)
    {
        var items = MultiSelectTreeView.RecursiveTreeViewItemEnumerable(_treeView, false, false).ToList();
        var item = GetFocusedItem();
        if (item == null)
        {
            return down ? SelectLastFromKey() : SelectFirstFromKey();
        }

        var targetY = item.TransformToAncestor(_treeView).Transform(new Point()).Y;
        var itemContent = (FrameworkElement)item.Template.FindName("PART_Header", item);
        if (itemContent == null)
        {
            return down ? SelectLastFromKey() : SelectFirstFromKey();
        }

        var offset = _treeView.ActualHeight - 2 * ((FrameworkElement)itemContent.Parent).ActualHeight;
        if (!down) offset = -offset;
        targetY += offset;
        while (true)
        {
            var newItem = down ? MultiSelectTreeView.GetNextItem(item, items) : MultiSelectTreeView.GetPreviousItem(item, items);
            if (newItem == null) break;
            item = newItem;
            var itemY = item.TransformToAncestor(_treeView).Transform(new Point()).Y;
            if (down && itemY > targetY ||
                !down && itemY < targetY)
            {
                break;
            }
        }
        return SelectFromKey(item);
    }

    public bool SelectPageUpFromKey() => SelectPageUpDown(false);

    public bool SelectPageDownFromKey() => SelectPageUpDown(true);

    public bool SelectAllFromKey()
    {
        var items = MultiSelectTreeView.RecursiveTreeViewItemEnumerable(_treeView, false, false).ToList();
        // Add new selected items
        foreach (var item in items.Where(i => !_treeView.SelectedItems.Contains(i.DataContext)))
        {
            var e = new PreviewSelectionChangedEventArgs(true, item.DataContext);
            OnPreviewSelectionChanged(e);
            if (e.CancelAll)
            {
                return false;
            }
            if (!e.CancelThis)
            {
                _treeView.SelectedItems.Add(item.DataContext);
            }
        }
        return true;
    }

    public bool SelectParentFromKey()
    {
        DependencyObject parent = GetFocusedItem();
        while (parent != null)
        {
            parent = VisualTreeHelper.GetParent(parent);
            if (parent is MultiSelectTreeViewItem) break;
        }
        return SelectFromKey(parent as MultiSelectTreeViewItem);
    }

    public bool Deselect(MultiSelectTreeViewItem item, bool bringIntoView = false)
    {
        if (item == null)
        {
            return false;
        }

        var e = new PreviewSelectionChangedEventArgs(false, item.DataContext);
        OnPreviewSelectionChanged(e);
        if (e.CancelAny) return false;

        _treeView.SelectedItems.Remove(item.DataContext);
        if (item.DataContext == _lastShiftRoot)
        {
            _lastShiftRoot = null;
        }
        FocusHelper.Focus(item, bringIntoView);
        return true;
    }

    public void Dispose()
    {
        if (_borderSelectionLogic != null)
        {
            _borderSelectionLogic.Dispose();
            _borderSelectionLogic = null;
        }

        GC.SuppressFinalize(this);
    }

    protected void OnPreviewSelectionChanged(PreviewSelectionChangedEventArgs e)
    {
        if (e == null)
        {
            throw new ArgumentNullException(nameof(e));
        }

        var handler = PreviewSelectionChanged;
        if (handler != null)
        {
            handler(this, e);
            LastCancelAll = e.CancelAll;
        }
    }
}
