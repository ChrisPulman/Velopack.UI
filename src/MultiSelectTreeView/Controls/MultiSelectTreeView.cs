﻿using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;

namespace System.Windows.Controls;

public class MultiSelectTreeView : ItemsControl
{
    public static readonly RoutedEvent SelectionChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(SelectionChanged),
        RoutingStrategy.Bubble,
        typeof(SelectionChangedEventHandler),
        typeof(MultiSelectTreeView));

    public static readonly RoutedEvent PreviewSelectionChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(PreviewSelectionChanged),
        RoutingStrategy.Bubble,
        typeof(PreviewSelectionChangedEventHandler),
        typeof(MultiSelectTreeView));

    public event SelectionChangedEventHandler SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    public event PreviewSelectionChangedEventHandler PreviewSelectionChanged
    {
        add => AddHandler(PreviewSelectionChangedEvent, value);
        remove => RemoveHandler(PreviewSelectionChangedEvent, value);
    }

    public static readonly DependencyProperty LastSelectedItemProperty;

    public static DependencyProperty BackgroundSelectionRectangleProperty = DependencyProperty.Register(
        nameof(BackgroundSelectionRectangle),
        typeof(Brush),
        typeof(MultiSelectTreeView),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(0x60, 0x33, 0x99, 0xFF)), null));

    public static DependencyProperty BorderBrushSelectionRectangleProperty = DependencyProperty.Register(
        nameof(BorderBrushSelectionRectangle),
        typeof(Brush),
        typeof(MultiSelectTreeView),
        new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0x33, 0x99, 0xFF)), null));

    public static DependencyProperty HoverHighlightingProperty = DependencyProperty.Register(
        nameof(HoverHighlighting),
        typeof(bool),
        typeof(MultiSelectTreeView),
        new FrameworkPropertyMetadata(true, null));

    public static DependencyProperty VerticalRulersProperty = DependencyProperty.Register(
        nameof(VerticalRulers),
        typeof(bool),
        typeof(MultiSelectTreeView),
        new FrameworkPropertyMetadata(false, null));

    public static DependencyProperty ItemIndentProperty = DependencyProperty.Register(
        nameof(ItemIndent),
        typeof(int),
        typeof(MultiSelectTreeView),
        new FrameworkPropertyMetadata(13, null));

    public static DependencyProperty IsKeyboardModeProperty = DependencyProperty.Register(
        nameof(IsKeyboardMode),
        typeof(bool),
        typeof(MultiSelectTreeView),
        new FrameworkPropertyMetadata(false, null));

    public static DependencyPropertyKey LastSelectedItemPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(LastSelectedItem),
        typeof(object),
        typeof(MultiSelectTreeView),
        new FrameworkPropertyMetadata(null));

    public static DependencyProperty SelectedItemsProperty = DependencyProperty.Register(
        nameof(SelectedItems),
        typeof(IList),
        typeof(MultiSelectTreeView),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnSelectedItemsPropertyChanged));

    public static DependencyProperty AllowEditItemsProperty = DependencyProperty.Register(
        nameof(AllowEditItems),
        typeof(bool),
        typeof(MultiSelectTreeView),
        new FrameworkPropertyMetadata(false, null));

    public static readonly DependencyProperty SelectionModeProperty = DependencyProperty.Register(
        nameof(SelectionMode),
        typeof(TreeViewSelectionMode),
        typeof(MultiSelectTreeView),
        new FrameworkPropertyMetadata(
            default(TreeViewSelectionMode),
            FrameworkPropertyMetadataOptions.None,
            OnSelectionModeChanged));

    static MultiSelectTreeView()
    {
        LastSelectedItemProperty = LastSelectedItemPropertyKey.DependencyProperty;
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MultiSelectTreeView), new FrameworkPropertyMetadata(typeof(MultiSelectTreeView)));
    }

    public MultiSelectTreeView()
    {
        SelectedItems = new ObservableCollection<object>();
        Selection = new SelectionMultiple(this);
        Selection.PreviewSelectionChanged += PreviewSelectionChangedHandler;
    }

    public Brush BackgroundSelectionRectangle
    {
        get => (Brush)GetValue(BackgroundSelectionRectangleProperty);
        set => SetValue(BackgroundSelectionRectangleProperty, value);
    }

    public Brush BorderBrushSelectionRectangle
    {
        get => (Brush)GetValue(BorderBrushSelectionRectangleProperty);
        set => SetValue(BorderBrushSelectionRectangleProperty, value);
    }

    public bool HoverHighlighting
    {
        get => (bool)GetValue(HoverHighlightingProperty);
        set => SetValue(HoverHighlightingProperty, value);
    }

    public bool VerticalRulers
    {
        get => (bool)GetValue(VerticalRulersProperty);
        set => SetValue(VerticalRulersProperty, value);
    }

    public int ItemIndent
    {
        get => (int)GetValue(ItemIndentProperty);
        set => SetValue(ItemIndentProperty, value);
    }

    [Browsable(false)]
    public bool IsKeyboardMode
    {
        get => (bool)GetValue(IsKeyboardModeProperty);
        set => SetValue(IsKeyboardModeProperty, value);
    }

    public bool AllowEditItems
    {
        get => (bool)GetValue(AllowEditItemsProperty);
        set => SetValue(AllowEditItemsProperty, value);
    }

    /// <summary>
    ///    Gets the last selected item.
    /// </summary>
    public object? LastSelectedItem
    {
        get => GetValue(LastSelectedItemProperty);
        private set => SetValue(LastSelectedItemPropertyKey, value);
    }

    /// <summary>
    ///    Determines whether multi-selection is enabled or not 
    /// </summary>
    public TreeViewSelectionMode SelectionMode
    {
        get => (TreeViewSelectionMode)GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    private MultiSelectTreeViewItem? _lastFocusedItem;

    /// <summary>
    /// Gets the last focused item.
    /// </summary>
    internal MultiSelectTreeViewItem? LastFocusedItem
    {
        get => _lastFocusedItem;
        set
        {
            // Only the last focused MultiSelectTreeViewItem may have IsTabStop = true
            // so that the keyboard focus only stops a single time for the MultiSelectTreeView control.
            if (_lastFocusedItem != null)
            {
                _lastFocusedItem.IsTabStop = false;
            }
            _lastFocusedItem = value;
            if (_lastFocusedItem != null)
            {
                _lastFocusedItem.IsTabStop = true;
            }
            // The MultiSelectTreeView control only has the tab stop if none of its items has it.
            IsTabStop = _lastFocusedItem == null;
        }
    }

    /// <summary>
    /// Gets or sets a list of selected items and can be bound to another list. If the source list
    /// implements <see cref="INotifyPropertyChanged"/> the changes are automatically taken over.
    /// </summary>
    public IList SelectedItems
    {
        get => (IList)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    internal ISelectionStrategy Selection { get; private set; }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        Selection.ApplyTemplate();
    }

    public bool ClearSelection()
    {
        if (SelectedItems.Count > 0)
        {
            // Make a copy of the list and ignore changes to the selection while raising events
            foreach (var selItem in new ArrayList(SelectedItems))
            {
                var e = new PreviewSelectionChangedEventArgs(false, selItem);
                OnPreviewSelectionChanged(e);
                if (e.CancelAny)
                {
                    return false;
                }
            }

            SelectedItems.Clear();
        }
        return true;
    }

    public void FocusItem(object item, bool bringIntoView = false)
    {
        var node = GetTreeViewItemsFor(new List<object> { item }).FirstOrDefault();
        if (node != null)
        {
            FocusHelper.Focus(node, bringIntoView);
        }
    }

    public void BringItemIntoView(object item)
    {
        var node = GetTreeViewItemsFor(new List<object> { item }).First();
        var itemContent = (FrameworkElement)node.Template.FindName("headerBorder", node);
        itemContent.BringIntoView();
    }

    public bool SelectNextItem() => Selection.SelectNextFromKey();

    public bool SelectPreviousItem() => Selection.SelectPreviousFromKey();

    public bool SelectFirstItem() => Selection.SelectFirstFromKey();

    public bool SelectLastItem() => Selection.SelectLastFromKey();

    public bool SelectAllItems() => Selection.SelectAllFromKey();

    public bool SelectParentItem() => Selection.SelectParentFromKey();

    internal bool DeselectRecursive(MultiSelectTreeViewItem item, bool includeSelf)
    {
        var selectedChildren = new List<MultiSelectTreeViewItem>();
        if (includeSelf)
        {
            if (item.IsSelected)
            {
                var e = new PreviewSelectionChangedEventArgs(false, item.DataContext);
                OnPreviewSelectionChanged(e);
                if (e.CancelAny)
                {
                    return false;
                }
                selectedChildren.Add(item);
            }
        }
        if (!CollectDeselectRecursive(item, selectedChildren))
        {
            return false;
        }
        foreach (var child in selectedChildren)
        {
            child.IsSelected = false;
        }
        return true;
    }

    private bool CollectDeselectRecursive(MultiSelectTreeViewItem item, List<MultiSelectTreeViewItem> selectedChildren)
    {
        foreach (var child in item.Items)
        {
            if (item.ItemContainerGenerator.ContainerFromItem(child) is MultiSelectTreeViewItem tvi)
            {
                if (tvi.IsSelected)
                {
                    var e = new PreviewSelectionChangedEventArgs(false, child);
                    OnPreviewSelectionChanged(e);
                    if (e.CancelAny)
                    {
                        return false;
                    }
                    selectedChildren.Add(tvi);
                }
                if (!CollectDeselectRecursive(tvi, selectedChildren))
                {
                    return false;
                }
            }
        }

        return true;
    }


    public static void RecursiveExpand(MultiSelectTreeViewItem item)
    {
        if (item == null) return;
        if (item.Items.Count > 0)
        {
            item.UpdateLayout();
            foreach (var child in item.Items)
            {
                if (item.ItemContainerGenerator.ContainerFromItem(child) is MultiSelectTreeViewItem tvi)
                {
                    tvi.IsExpanded = true;
                    RecursiveExpand(tvi);
                }
            }
        }
    }

    internal bool ClearSelectionByRectangle()
    {
        foreach (var item in new ArrayList(SelectedItems))
        {
            var e = new PreviewSelectionChangedEventArgs(false, item);
            OnPreviewSelectionChanged(e);
            if (e.CancelAny) return false;
        }

        SelectedItems.Clear();
        return true;
    }

    internal static MultiSelectTreeViewItem? GetNextItem(MultiSelectTreeViewItem? item, List<MultiSelectTreeViewItem> items)
    {
        var indexOfCurrent = item != null ? items.IndexOf(item) : -1;
        for (var i = indexOfCurrent + 1; i < items.Count; i++)
        {
            if (items[i].IsVisible)
            {
                return items[i];
            }
        }
        return null;
    }

    internal static MultiSelectTreeViewItem? GetPreviousItem(MultiSelectTreeViewItem? item, List<MultiSelectTreeViewItem> items)
    {
        var indexOfCurrent = item != null ? items.IndexOf(item) : -1;
        for (var i = indexOfCurrent - 1; i >= 0; i--)
        {
            if (items[i].IsVisible)
            {
                return items[i];
            }
        }
        return null;
    }

    internal static MultiSelectTreeViewItem? GetFirstItem(List<MultiSelectTreeViewItem> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].IsVisible)
            {
                return items[i];
            }
        }
        return null;
    }

    internal static MultiSelectTreeViewItem? GetLastItem(List<MultiSelectTreeViewItem> items)
    {
        for (var i = items.Count - 1; i >= 0; i--)
        {
            if (items[i].IsVisible)
            {
                return items[i];
            }
        }
        return null;
    }

    protected override DependencyObject GetContainerForItemOverride() => new MultiSelectTreeViewItem();

    protected override bool IsItemItsOwnContainerOverride(object item) => item is MultiSelectTreeViewItem;

    protected override AutomationPeer OnCreateAutomationPeer() => new MultiSelectTreeViewAutomationPeer(this);

    private static void OnSelectedItemsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var treeView = (MultiSelectTreeView)d;
        if (e.OldValue != null)
        {
            if (e.OldValue is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged -= treeView.OnSelectedItemsChanged;
            }
        }

        if (e.NewValue != null)
        {
            if (e.NewValue is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged += treeView.OnSelectedItemsChanged;
            }
        }
    }

    private static void OnSelectionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var treeView = (MultiSelectTreeView)d;

        if (treeView.Selection != null)
        {
            treeView.Selection.PreviewSelectionChanged -= treeView.PreviewSelectionChangedHandler;
        }

        switch ((TreeViewSelectionMode)e.NewValue)
        {
            case TreeViewSelectionMode.MultiSelectEnabled:
                treeView.Selection = new SelectionMultiple(treeView);
                break;
            case TreeViewSelectionMode.SingleSelectOnly:
                treeView.Selection = new SelectionSingle(treeView);
                break;
        }

        if (treeView.Selection != null)
        {
            treeView.Selection.PreviewSelectionChanged += treeView.PreviewSelectionChangedHandler;
        }
    }

    private void PreviewSelectionChangedHandler(object? sender, PreviewSelectionChangedEventArgs e) => OnPreviewSelectionChanged(e);

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        if (e == null) return;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Remove:
            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems != null)
                {
                    foreach (var item in e.OldItems)
                    {
                        SelectedItems.Remove(item);
                        // Don't preview and ask, it is already gone so it must be removed from
                        // the SelectedItems list
                    }
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                // If the items list has considerably changed, the selection is probably
                // useless anyway, clear it entirely.
                SelectedItems.Clear();
                break;
        }

        base.OnItemsChanged(e);
    }

    internal static IEnumerable<MultiSelectTreeViewItem> RecursiveTreeViewItemEnumerable(ItemsControl parent, bool includeInvisible) => RecursiveTreeViewItemEnumerable(parent, includeInvisible, true);

    internal static IEnumerable<MultiSelectTreeViewItem> RecursiveTreeViewItemEnumerable(ItemsControl parent, bool includeInvisible, bool includeDisabled)
    {
        foreach (var item in parent.Items)
        {
            var tve = (MultiSelectTreeViewItem)parent.ItemContainerGenerator.ContainerFromItem(item);
            if (tve == null)
            {
                // Container was not generated, therefore it is probably not visible, so we can ignore it.
                continue;
            }
            if (!includeInvisible && !tve.IsVisible)
            {
                continue;
            }
            if (!includeDisabled && !tve.IsEnabled)
            {
                continue;
            }

            yield return tve;
            if (includeInvisible || tve.IsExpanded)
            {
                foreach (var childItem in RecursiveTreeViewItemEnumerable(tve, includeInvisible, includeDisabled))
                {
                    yield return childItem;
                }
            }
        }
    }

    internal IEnumerable<MultiSelectTreeViewItem> GetNodesToSelectBetween(MultiSelectTreeViewItem firstNode, MultiSelectTreeViewItem lastNode)
    {
        var allNodes = RecursiveTreeViewItemEnumerable(this, false, false).ToList();
        var firstIndex = allNodes.IndexOf(firstNode);
        var lastIndex = allNodes.IndexOf(lastNode);

        if (firstIndex >= allNodes.Count)
        {
            throw new InvalidOperationException(
               "First node index " + firstIndex + "greater or equal than count " + allNodes.Count + ".");
        }

        if (lastIndex >= allNodes.Count)
        {
            throw new InvalidOperationException(
               "Last node index " + lastIndex + " greater or equal than count " + allNodes.Count + ".");
        }

        var nodesToSelect = new List<MultiSelectTreeViewItem>();

        if (lastIndex == firstIndex)
        {
            return [firstNode];
        }

        if (lastIndex > firstIndex)
        {
            for (var i = firstIndex; i <= lastIndex; i++)
            {
                if (allNodes[i].IsVisible)
                {
                    nodesToSelect.Add(allNodes[i]);
                }
            }
        }
        else
        {
            for (var i = firstIndex; i >= lastIndex; i--)
            {
                if (allNodes[i].IsVisible)
                {
                    nodesToSelect.Add(allNodes[i]);
                }
            }
        }

        return nodesToSelect;
    }

    /// <summary>
    /// Finds the treeview item for each of the specified data items.
    /// </summary>
    /// <param name="dataItems">List of data items to search for.</param>
    /// <returns></returns>
    internal IEnumerable<MultiSelectTreeViewItem> GetTreeViewItemsFor(IEnumerable? dataItems)
    {
        if (dataItems == null)
        {
            yield break;
        }

        foreach (var dataItem in dataItems)
        {
            foreach (var treeViewItem in RecursiveTreeViewItemEnumerable(this, true))
            {
                if (treeViewItem.DataContext == dataItem)
                {
                    yield return treeViewItem;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Gets all data items referenced in all treeview items of the entire control.
    /// </summary>
    /// <returns></returns>
    internal IEnumerable GetAllDataItems()
    {
        foreach (var treeViewItem in RecursiveTreeViewItemEnumerable(this, true))
        {
            yield return treeViewItem.DataContext;
        }
    }

    // this eventhandler reacts on the firing control to, in order to update the own status
    private void OnSelectedItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var addedItems = new ArrayList();
        var removedItems = new ArrayList();

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
#if DEBUG
                // Make sure we don't confuse MultiSelectTreeViewItems and their DataContexts while development
                if (e.NewItems?.OfType<MultiSelectTreeViewItem>().Any() == true)
                {
                    throw new ArgumentException("A MultiSelectTreeViewItem instance was added to the SelectedItems collection. Only their DataContext instances must be added to this list!");
                }
#endif
                object? last = null;
                foreach (var item in GetTreeViewItemsFor(e.NewItems))
                {
                    if (!item.IsSelected)
                    {
                        item.IsSelected = true;
                    }

                    last = item.DataContext;
                }

                addedItems.AddRange(e.NewItems!);
                LastSelectedItem = last;
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var item in GetTreeViewItemsFor(e.OldItems))
                {
                    item.IsSelected = false;
                    if (item.DataContext == LastSelectedItem)
                    {
                        if (SelectedItems.Count > 0)
                        {
                            LastSelectedItem = SelectedItems[SelectedItems.Count - 1];
                        }
                        else
                        {
                            LastSelectedItem = null;
                        }
                    }
                }

                removedItems.AddRange(e.OldItems!);
                break;
            case NotifyCollectionChangedAction.Reset:
                foreach (var item in RecursiveTreeViewItemEnumerable(this, true))
                {
                    if (item.IsSelected)
                    {
                        removedItems.Add(item.DataContext);
                        item.IsSelected = false;
                    }
                }

                LastSelectedItem = null;
                break;
            default:
                throw new InvalidOperationException();
        }

        var selectionChangedEventArgs = new SelectionChangedEventArgs(SelectionChangedEvent, addedItems, removedItems);

        OnSelectionChanged(selectionChangedEventArgs);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e == null) return;
        base.OnKeyDown(e);
        if (!e.Handled)
        {
            // Basically, this should not be needed anymore. It allows selecting an item with
            // the keyboard when the MultiSelectTreeView control has the focus. If there were already
            // items when the control was focused, an item has already been focused (and
            // subsequent key presses won't land here but at the item).
            var key = e.Key;
            switch (key)
            {
                case Key.Up:
                    // Select last item
                    var lastNode = RecursiveTreeViewItemEnumerable(this, false).LastOrDefault();
                    if (lastNode != null)
                    {
                        Selection.Select(lastNode);
                        e.Handled = true;
                    }
                    break;
                case Key.Down:
                    // Select first item
                    var firstNode = RecursiveTreeViewItemEnumerable(this, false).FirstOrDefault();
                    if (firstNode != null)
                    {
                        Selection.Select(firstNode);
                        e.Handled = true;
                    }
                    break;
            }
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!IsKeyboardMode)
        {
            IsKeyboardMode = true;
            //System.Diagnostics.Debug.WriteLine("Changing to keyboard mode from PreviewKeyDown");
        }
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!IsKeyboardMode)
        {
            IsKeyboardMode = true;
            //System.Diagnostics.Debug.WriteLine("Changing to keyboard mode from PreviewKeyUp");
        }
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        if (IsKeyboardMode)
        {
            IsKeyboardMode = false;
            //System.Diagnostics.Debug.WriteLine("Changing to mouse mode");
        }
    }

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        //System.Diagnostics.Debug.WriteLine("MultiSelectTreeView.OnGotFocus()");
        //System.Diagnostics.Debug.WriteLine(Environment.StackTrace);

        base.OnGotFocus(e);

        // If the MultiSelectTreeView control has gotten the focus, it needs to pass it to an
        // item instead. If there was an item focused before, return to that. Otherwise just
        // focus this first item in the list if any. If there are no items at all, the
        // MultiSelectTreeView control just keeps the focus.
        // In any case, the focussing must occur when the current event processing is finished,
        // i.e. be queued in the dispatcher. Otherwise the TreeView could keep its focus
        // because other focus things are still going on and interfering this final request.

        var lastFocusedItem = LastFocusedItem;
        if (lastFocusedItem != null)
        {
            Dispatcher.BeginInvoke((Action)(() => FocusHelper.Focus(lastFocusedItem)));
        }
        else
        {
            var firstNode = RecursiveTreeViewItemEnumerable(this, false).FirstOrDefault();
            if (firstNode != null)
            {
                Dispatcher.BeginInvoke((Action)(() => FocusHelper.Focus(firstNode)));
            }
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        // This happens when a mouse button was pressed in an area which is not covered by an
        // item. Then, it should be focused which in turn passes on the focus to an item.
        Focus();
    }

    protected virtual void OnPreviewSelectionChanged(PreviewSelectionChangedEventArgs e)
    {
        if (e == null) return;
        e.RoutedEvent = PreviewSelectionChangedEvent;
        RaiseEvent(e);
    }

    protected virtual void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        if (e == null) return;
        e.RoutedEvent = SelectionChangedEvent;
        RaiseEvent(e);
    }
}
