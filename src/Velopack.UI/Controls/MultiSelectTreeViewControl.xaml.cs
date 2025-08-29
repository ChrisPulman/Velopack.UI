using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Velopack.UI.MultiSelectTreeView.Controls;

public partial class MultiSelectTreeViewControl : UserControl
{
    public MultiSelectTreeViewControl()
    {
        InitializeComponent();
        SelectedItems = new ObservableCollection<object>();

        // Ensure ContextMenus can find VM and SelectedItems via PlacementTarget.Tag
        Loaded += (_, _) =>
        {
            PART_Tree.Tag = SelectedItems;
        };

        this.DataContextChanged += (_, __) =>
        {
            // update TreeViewItem Tag is bound via ancestor UserControl, so no action here
            PART_Tree.Tag = SelectedItems;
        };
    }

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(MultiSelectTreeViewControl));

    public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.Register(
        nameof(SelectedItems), typeof(IList), typeof(MultiSelectTreeViewControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemsChanged));

    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
        nameof(ItemTemplate), typeof(HierarchicalDataTemplate), typeof(MultiSelectTreeViewControl));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IList SelectedItems
    {
        get => (IList)GetValue(SelectedItemsProperty)!;
        set => SetValue(SelectedItemsProperty, value);
    }

    public HierarchicalDataTemplate? ItemTemplate
    {
        get => (HierarchicalDataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctl = (MultiSelectTreeViewControl)d;
        if (ctl.PART_Tree != null)
        {
            ctl.PART_Tree.Tag = ctl.SelectedItems;
        }
    }

    private static bool IsCtrlDown => (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
    private static bool IsShiftDown => (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

    private object? _lastAnchor;

    private static bool IsOnExpander(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is ToggleButton) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private void OnTreeViewPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsOnExpander(e.OriginalSource as DependencyObject)) return;
        var container = FindContainer(e.OriginalSource as DependencyObject);
        if (container == null) return;

        var item = container.DataContext;
        if (!IsCtrlDown && !IsShiftDown)
        {
            ClearSelectionInternal();
            AddToSelection(item);
            _lastAnchor = item;
        }
        else if (IsCtrlDown)
        {
            if (SelectedItems.Contains(item)) RemoveFromSelection(item);
            else { AddToSelection(item); _lastAnchor = item; }
        }
        else if (IsShiftDown && _lastAnchor != null)
        {
            var flat = Flatten(PART_Tree).ToList();
            int a = flat.FindIndex(x => Equals((x as TreeViewItem)?.DataContext, _lastAnchor));
            int b = flat.FindIndex(x => Equals((x as TreeViewItem)?.DataContext, item));
            if (a >= 0 && b >= 0)
            {
                if (a > b) (a, b) = (b, a);
                ClearSelectionInternal();
                for (int i = a; i <= b; i++) AddToSelection(((TreeViewItem)flat[i]).DataContext);
            }
        }
        e.Handled = true;
    }

    private void OnTreeViewPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var container = FindContainer(e.OriginalSource as DependencyObject);
        if (container == null) return;
        var item = container.DataContext;
        if (!SelectedItems.Contains(item))
        {
            if (!IsCtrlDown) ClearSelectionInternal();
            AddToSelection(item);
            _lastAnchor = item;
        }
        // allow context menu
    }

    private void OnTreeViewPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            ClearSelectionInternal();
            foreach (var tvi in Flatten(PART_Tree)) AddToSelection(tvi.DataContext);
            e.Handled = true;
        }
    }

    private void ClearSelectionInternal()
    {
        foreach (var obj in SelectedItems.Cast<object>().ToList())
        {
            SetItemSelected(obj, false);
            SelectedItems.Remove(obj);
        }
    }

    private void AddToSelection(object obj)
    {
        if (!SelectedItems.Contains(obj))
        {
            SelectedItems.Add(obj);
            SetItemSelected(obj, true);
        }
    }

    private void RemoveFromSelection(object obj)
    {
        if (SelectedItems.Contains(obj))
        {
            SelectedItems.Remove(obj);
            SetItemSelected(obj, false);
        }
    }

    private static void SetItemSelected(object obj, bool selected)
    {
        var type = obj.GetType();
        var prop = type.GetProperty("IsSelected");
        if (prop != null && prop.PropertyType == typeof(bool) && prop.CanWrite)
        {
            prop.SetValue(obj, selected);
        }
    }

    private static IEnumerable<TreeViewItem> Flatten(ItemsControl parent)
    {
        foreach (var obj in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(obj) is TreeViewItem tvi)
            {
                yield return tvi;
                if (tvi.IsExpanded)
                {
                    foreach (var child in Flatten(tvi)) yield return child;
                }
            }
        }
    }

    private static TreeViewItem? FindContainer(DependencyObject? d)
    {
        while (d != null && d is not TreeViewItem)
        {
            d = VisualTreeHelper.GetParent(d);
        }
        return d as TreeViewItem;
    }
}
