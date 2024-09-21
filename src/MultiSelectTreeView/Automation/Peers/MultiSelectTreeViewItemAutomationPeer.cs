using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace System.Windows.Automation.Peers;

/// <summary>
/// Macht <see cref="T:System.Windows.Controls.MultiSelectTreeViewItem"/>-Typen für
/// UI-Automatisierung verfügbar.
/// </summary>
public class MultiSelectTreeViewItemAutomationPeer :
    ItemsControlAutomationPeer,
    IExpandCollapseProvider,
    ISelectionItemProvider,
    IScrollItemProvider,
    IValueProvider,
    IInvokeProvider
{

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiSelectTreeViewItemAutomationPeer"/>
    /// class. 
    /// </summary>
    /// <param name="owner">
    /// Das <see cref="T:System.Windows.Controls.MultiSelectTreeViewItem"/>, das diesem
    /// <see cref="T:System.Windows.Automation.Peers.MultiSelectTreeViewItemAutomationPeer"/>
    /// zugeordnet ist.
    /// </param>
    public MultiSelectTreeViewItemAutomationPeer(MultiSelectTreeViewItem owner)
        : base(owner)
    {
    }

    protected override Rect GetBoundingRectangleCore()
    {
        var treeViewItem = (MultiSelectTreeViewItem)Owner;
        var contentPresenter = GetContentPresenter(treeViewItem);
        if (contentPresenter != null)
        {
            var offset = VisualTreeHelper.GetOffset(contentPresenter);
            var p = new Point(offset.X, offset.Y);
            p = contentPresenter.PointToScreen(p);
            return new Rect(p.X, p.Y, contentPresenter.ActualWidth, contentPresenter.ActualHeight);
        }

        return base.GetBoundingRectangleCore();
    }

    protected override Point GetClickablePointCore()
    {
        var treeViewItem = (MultiSelectTreeViewItem)Owner;
        var contentPresenter = GetContentPresenter(treeViewItem);
        if (contentPresenter != null)
        {
            var offset = VisualTreeHelper.GetOffset(contentPresenter);
            var p = new Point(offset.X, offset.Y);
            p = contentPresenter.PointToScreen(p);
            return p;
        }

        return base.GetClickablePointCore();
    }

    private static ContentPresenter GetContentPresenter(MultiSelectTreeViewItem treeViewItem)
    {
        var contentPresenter = treeViewItem.Template.FindName("PART_Header", treeViewItem) as ContentPresenter;
        return contentPresenter;
    }

    /// <summary>
    /// Overridden because original wpf tree does show the expander button and the contents of the
    /// header as children, too. That was requested by the users.
    /// </summary>
    /// <returns>Returns a list of children.</returns>
    protected override List<AutomationPeer> GetChildrenCore()
    {
        //System.Diagnostics.Trace.WriteLine("MultiSelectTreeViewItemAutomationPeer.GetChildrenCore()");
        var owner = (MultiSelectTreeViewItem)Owner;

        var children = new List<AutomationPeer>();
        var button = owner.Template.FindName("Expander", owner) as ToggleButton;
        AddAutomationPeer(children, button);
        //System.Diagnostics.Trace.WriteLine("- Adding ToggleButton, " + (button == null ? "IS" : "is NOT") + " null, now " + children.Count + " items");

        var contentPresenter = GetContentPresenter(owner);

        if (contentPresenter != null)
        {
            var childrenCount = VisualTreeHelper.GetChildrenCount(contentPresenter);
            for (var i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(contentPresenter, i) as UIElement;
                AddAutomationPeer(children, child);
                //System.Diagnostics.Trace.WriteLine("- Adding child UIElement, " + (child == null ? "IS" : "is NOT") + " null, now " + children.Count + " items");
            }
        }

        var items = owner.Items;
        for (var i = 0; i < items.Count; i++)
        {
            var treeViewItem = owner.ItemContainerGenerator.ContainerFromIndex(i) as MultiSelectTreeViewItem;
            AddAutomationPeer(children, treeViewItem);
            //System.Diagnostics.Trace.WriteLine("- Adding MultiSelectTreeViewItem, " + (treeViewItem == null ? "IS" : "is NOT") + " null, now " + children.Count + " items");
        }

        if (children.Count > 0)
        {
            //System.Diagnostics.Trace.WriteLine("MultiSelectTreeViewItemAutomationPeer.GetChildrenCore(): returning " + children.Count + " children");
            //for (int i = 0; i < children.Count; i++)
            //{
            //    System.Diagnostics.Trace.WriteLine("- Item " + i + " " + (children[i] == null ? "IS" : "is NOT") + " null");
            //}
            return children;
        }

        //System.Diagnostics.Trace.WriteLine("MultiSelectTreeViewItemAutomationPeer.GetChildrenCore(): returning null");
        return null;
    }

    private static void AddAutomationPeer(List<AutomationPeer> children, UIElement child)
    {
        if (child != null)
        {
            var peer = FromElement(child);
            if (peer == null)
            {
                peer = CreatePeerForElement(child);
            }

            if (peer != null)
            {
                // In the array that GetChildrenCore returns, which is used by AutomationPeer.EnsureChildren,
                // no null entries are allowed or a NullReferenceException will be thrown from the guts of WPF.
                // This has reproducibly been observed null on certain systems so the null check was added.
                // This may mean that some child controls are missing for automation, but at least the
                // application doesn't crash in normal usage.
                children.Add(peer);
            }
        }
    }

    public ExpandCollapseState ExpandCollapseState
    {
        get
        {
            var treeViewItem = (MultiSelectTreeViewItem)Owner;
            if (!treeViewItem.HasItems)
            {
                return ExpandCollapseState.LeafNode;
            }

            if (!treeViewItem.IsExpanded)
            {
                return ExpandCollapseState.Collapsed;
            }

            return ExpandCollapseState.Expanded;
        }
    }

    bool ISelectionItemProvider.IsSelected
    {
        get
        {
            return ((MultiSelectTreeViewItem)Owner).IsSelected;
        }
    }

    IRawElementProviderSimple ISelectionItemProvider.SelectionContainer
    {
        get
        {
            ItemsControl parentItemsControl = ((MultiSelectTreeViewItem)Owner).ParentTreeView;
            if (parentItemsControl != null)
            {
                var automationPeer = FromElement(parentItemsControl);
                if (automationPeer != null)
                {
                    return ProviderFromPeer(automationPeer);
                }
            }

            return null;
        }
    }

    public void Collapse()
    {
        if (!IsEnabled())
        {
            throw new ElementNotEnabledException();
        }

        var treeViewItem = (MultiSelectTreeViewItem)Owner;
        if (!treeViewItem.HasItems)
        {
            throw new InvalidOperationException("Cannot collapse because item has no children.");
        }

        treeViewItem.IsExpanded = false;
    }

    public void Expand()
    {
        if (!IsEnabled())
        {
            throw new ElementNotEnabledException();
        }

        var treeViewItem = (MultiSelectTreeViewItem)Owner;
        if (!treeViewItem.HasItems)
        {
            throw new InvalidOperationException("Cannot expand because item has no children.");
        }

        treeViewItem.IsExpanded = true;
    }

    public override object GetPattern(PatternInterface patternInterface)
    {
        if (patternInterface == PatternInterface.ExpandCollapse)
        {
            return this;
        }

        if (patternInterface == PatternInterface.SelectionItem)
        {
            return this;
        }

        if (patternInterface == PatternInterface.ScrollItem)
        {
            return this;
        }

        if (patternInterface == PatternInterface.Value)
        {
            return this;
        }

        return base.GetPattern(patternInterface);
    }

    void IScrollItemProvider.ScrollIntoView() => ((MultiSelectTreeViewItem)Owner).BringIntoView();

    void ISelectionItemProvider.AddToSelection() => throw new NotImplementedException();

    void ISelectionItemProvider.RemoveFromSelection() => throw new NotImplementedException();

    void ISelectionItemProvider.Select() => ((MultiSelectTreeViewItem)Owner).ParentTreeView.Selection.SelectCore((MultiSelectTreeViewItem)Owner);

    protected override ItemAutomationPeer CreateItemAutomationPeer(object item) => new MultiSelectTreeViewItemDataAutomationPeer(item, this);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TreeItem;

    protected override string GetClassNameCore() => "MultiSelectTreeViewItem";

    public bool IsReadOnly
    {
        get { return false; }
    }

    string? _requestedValue;

    public void SetValue(string value)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            var ids = value.Split(new[] { ';' });

            object obj;
            if (ids.Length > 0 && ids[0] == "Context")
            {
                var treeViewItem = (MultiSelectTreeViewItem)Owner;
                obj = treeViewItem.DataContext;
            }
            else
            {
                obj = Owner;
            }

            if (ids.Length < 2)
            {
                _requestedValue = obj.ToString();
            }
            else
            {
                var type = obj.GetType();
                var pi = type.GetProperty(ids[1]);
                _requestedValue = pi?.GetValue(obj, null)?.ToString();
            }
        }
        catch (Exception ex)
        {
            _requestedValue = ex.ToString();
        }
    }

    public string? Value
    {
        get
        {
            if (_requestedValue == null)
            {
                var treeViewItem = (MultiSelectTreeViewItem)Owner;
                return treeViewItem.DataContext.ToString();
            }

            return _requestedValue;
        }
    }

    public void Invoke()
    {
        var treeViewItem = (MultiSelectTreeViewItem)Owner;
        treeViewItem.InvokeMouseDown();
    }
}
