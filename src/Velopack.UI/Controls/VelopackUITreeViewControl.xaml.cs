// <copyright file="VelopackUITreeViewControl.xaml.cs" company="Chris Pulman">
// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

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
    /// The selected item property.
    /// </summary>
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(object), typeof(VelopackUITreeViewControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// The item template property.
    /// </summary>
    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
        nameof(ItemTemplate), typeof(HierarchicalDataTemplate), typeof(VelopackUITreeViewControl));

    private object? lastAnchor;

    /// <summary>
    /// Initializes a new instance of the <see cref="VelopackUITreeViewControl"/> class.
    /// </summary>
    public VelopackUITreeViewControl()
    {
        this.InitializeComponent();
        this.Loaded += (_, _) => this.PART_Tree.Tag = this.SelectedItems;
        this.DataContextChanged += (_, _) => this.PART_Tree.Tag = this.SelectedItems;
    }

    /// <summary>
    /// Gets or sets the items source.
    /// </summary>
    /// <value>
    /// The items source.
    /// </value>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)this.GetValue(ItemsSourceProperty);
        set => this.SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the selected items.
    /// </summary>
    /// <value>
    /// The selected items.
    /// </value>
    public IList? SelectedItems
    {
        get => (IList?)this.GetValue(SelectedItemsProperty);
        set => this.SetValue(SelectedItemsProperty, value);
    }

    /// <summary>
    /// Gets or sets the first selected item.
    /// </summary>
    /// <value>
    /// The first selected item.
    /// </value>
    public object? SelectedItem
    {
        get => this.GetValue(SelectedItemProperty);
        set => this.SetValue(SelectedItemProperty, value);
    }

    /// <summary>
    /// Gets or sets the item template.
    /// </summary>
    /// <value>
    /// The item template.
    /// </value>
    public HierarchicalDataTemplate? ItemTemplate
    {
        get => (HierarchicalDataTemplate?)this.GetValue(ItemTemplateProperty);
        set => this.SetValue(ItemTemplateProperty, value);
    }

    private static bool IsCtrlDown => (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

    private static bool IsShiftDown => (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

    private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctl = (VelopackUITreeViewControl)d;
        ctl.PART_Tree?.Tag = ctl.SelectedItems;
        ctl.UpdateSelectedItem();
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
        if (prop == null || prop.PropertyType != typeof(bool) || !prop.CanWrite)
        {
            return;
        }

        prop.SetValue(obj, selected);
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

        if (this.SelectedItems == null)
        {
            return;
        }

        var item = container.DataContext;
        if (!IsCtrlDown && !IsShiftDown)
        {
            this.ClearSelectionInternal();
            this.AddToSelection(item);
            this.lastAnchor = item;
        }
        else if (IsCtrlDown)
        {
            if (this.SelectedItems.Contains(item))
            {
                this.RemoveFromSelection(item);
            }
            else
            {
                this.AddToSelection(item);
                this.lastAnchor = item;
            }
        }
        else if (IsShiftDown && this.lastAnchor != null)
        {
            var flat = Flatten(this.PART_Tree).ToList();
            var a = flat.FindIndex(x => Equals(x?.DataContext, this.lastAnchor));
            var b = flat.FindIndex(x => Equals(x?.DataContext, item));
            if (a >= 0 && b >= 0)
            {
                if (a > b)
                {
                    (a, b) = (b, a);
                }

                this.ClearSelectionInternal();
                for (var i = a; i <= b; i++)
                {
                    this.AddToSelection(flat[i].DataContext);
                }
            }
        }
        else if (IsShiftDown)
        {
            this.ClearSelectionInternal();
            this.AddToSelection(item);
            this.lastAnchor = item;
        }

        Keyboard.Focus(container);
        e.Handled = true;
    }

    private void OnTreeViewPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var container = FindContainer(e.OriginalSource as DependencyObject);
        if (container == null)
        {
            return;
        }

        if (this.SelectedItems == null)
        {
            return;
        }

        var item = container.DataContext;
        if (!this.SelectedItems.Contains(item))
        {
            if (!IsCtrlDown)
            {
                this.ClearSelectionInternal();
            }

            this.AddToSelection(item);
            this.lastAnchor = item;
        }

        Keyboard.Focus(container);

        // allow context menu
    }

    private void OnTreeViewPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.A || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return;
        }

        if (this.SelectedItems == null)
        {
            return;
        }

        this.ClearSelectionInternal();
        foreach (var tvi in Flatten(this.PART_Tree))
        {
            this.AddToSelection(tvi.DataContext);
        }

        this.UpdateSelectedItem();
        e.Handled = true;
    }

    private void ClearSelectionInternal()
    {
        if (this.SelectedItems == null)
        {
            return;
        }

        foreach (var obj in this.SelectedItems.Cast<object>().ToList())
        {
            SetItemSelected(obj, false);
            this.SelectedItems.Remove(obj);
        }

        this.UpdateSelectedItem();
    }

    private void AddToSelection(object obj)
    {
        if (this.SelectedItems == null)
        {
            return;
        }

        if (this.SelectedItems.Contains(obj))
        {
            return;
        }

        this.SelectedItems.Add(obj);
        SetItemSelected(obj, true);
        this.UpdateSelectedItem();
    }

    private void RemoveFromSelection(object obj)
    {
        if (this.SelectedItems == null)
        {
            return;
        }

        if (!this.SelectedItems.Contains(obj))
        {
            return;
        }

        this.SelectedItems.Remove(obj);
        SetItemSelected(obj, false);
        this.UpdateSelectedItem();
    }

    private void UpdateSelectedItem()
    {
        this.SetCurrentValue(SelectedItemProperty, this.SelectedItems?.Cast<object>().FirstOrDefault());
    }
}
