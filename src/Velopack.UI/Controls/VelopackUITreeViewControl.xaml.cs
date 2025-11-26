// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ReactiveMarbles.ObservableEvents;

namespace Velopack.UI.Controls;

/// <summary>
/// VelopackUITreeViewControl.
/// </summary>
/// <seealso cref="System.Windows.Controls.UserControl" />
/// <seealso cref="System.Windows.Markup.IComponentConnector" />
[SupportedOSPlatform("windows10.0.19041.0")]
public partial class VelopackUITreeViewControl
{
    /// <summary>
    /// The items source property.
    /// </summary>
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(VelopackUITreeViewControl));

    /// <summary>
    /// The selected items property.
    /// </summary>
    public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.Register(
        nameof(SelectedItems), typeof(IList), typeof(VelopackUITreeViewControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemsChanged));

    /// <summary>
    /// The item template property.
    /// </summary>
    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
        nameof(ItemTemplate), typeof(HierarchicalDataTemplate), typeof(VelopackUITreeViewControl));

    private object? _lastAnchor;

    /// <summary>
    /// Initializes a new instance of the <see cref="VelopackUITreeViewControl"/> class.
    /// </summary>
    public VelopackUITreeViewControl()
    {
        InitializeComponent();
        this.Events().Loaded
            .Subscribe(_ => PART_Tree.Tag = SelectedItems);
        this.Events().DataContextChanged
            .Subscribe(_ => PART_Tree.Tag = SelectedItems);
    }

    /// <summary>
    /// Gets or sets the items source.
    /// </summary>
    /// <value>
    /// The items source.
    /// </value>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the selected items.
    /// </summary>
    /// <value>
    /// The selected items.
    /// </value>
    public IList? SelectedItems
    {
        get => (IList?)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    /// <summary>
    /// Gets or sets the item template.
    /// </summary>
    /// <value>
    /// The item template.
    /// </value>
    public HierarchicalDataTemplate? ItemTemplate
    {
        get => (HierarchicalDataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    private static bool IsCtrlDown => (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

    private static bool IsShiftDown => (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

    private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctl = (VelopackUITreeViewControl)d;
        ctl.PART_Tree?.Tag = ctl.SelectedItems;
    }

    private static bool IsOnExpander(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is ToggleButton)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
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
                    foreach (var child in Flatten(tvi))
                    {
                        yield return child;
                    }
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

    private void OnTreeViewPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsOnExpander(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var container = FindContainer(e.OriginalSource as DependencyObject);
        if (container == null)
        {
            return;
        }

        if (SelectedItems == null)
        {
            return;
        }

        var item = container.DataContext;
        if (!IsCtrlDown && !IsShiftDown)
        {
            ClearSelectionInternal();
            AddToSelection(item);
            _lastAnchor = item;
        }
        else if (IsCtrlDown)
        {
            if (SelectedItems.Contains(item))
            {
                RemoveFromSelection(item);
            }
            else
            {
                AddToSelection(item);
                _lastAnchor = item;
            }
        }
        else if (IsShiftDown && _lastAnchor != null)
        {
            var flat = Flatten(PART_Tree).ToList();
            var a = flat.FindIndex(x => Equals(x?.DataContext, _lastAnchor));
            var b = flat.FindIndex(x => Equals(x?.DataContext, item));
            if (a >= 0 && b >= 0)
            {
                if (a > b)
                {
                    (a, b) = (b, a);
                }

                ClearSelectionInternal();
                for (var i = a; i <= b; i++)
                {
                    AddToSelection(flat[i].DataContext);
                }
            }
        }

        e.Handled = true;
    }

    private void OnTreeViewPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var container = FindContainer(e.OriginalSource as DependencyObject);
        if (container == null)
        {
            return;
        }

        if (SelectedItems == null)
        {
            return;
        }

        var item = container.DataContext;
        if (!SelectedItems.Contains(item))
        {
            if (!IsCtrlDown)
            {
                ClearSelectionInternal();
            }

            AddToSelection(item);
            _lastAnchor = item;
        }

        // allow context menu
    }

    private void OnTreeViewPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (SelectedItems == null)
            {
                return;
            }

            ClearSelectionInternal();
            foreach (var tvi in Flatten(PART_Tree))
            {
                AddToSelection(tvi.DataContext);
            }

            e.Handled = true;
        }
    }

    private void ClearSelectionInternal()
    {
        if (SelectedItems == null)
        {
            return;
        }

        foreach (var obj in SelectedItems.Cast<object>().ToList())
        {
            SetItemSelected(obj, false);
            SelectedItems.Remove(obj);
        }
    }

    private void AddToSelection(object obj)
    {
        if (SelectedItems == null)
        {
            return;
        }

        if (!SelectedItems.Contains(obj))
        {
            SelectedItems.Add(obj);
            SetItemSelected(obj, true);
        }
    }

    private void RemoveFromSelection(object obj)
    {
        if (SelectedItems == null)
        {
            return;
        }

        if (SelectedItems.Contains(obj))
        {
            SelectedItems.Remove(obj);
            SetItemSelected(obj, false);
        }
    }
}
