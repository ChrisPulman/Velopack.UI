using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace System.Windows.Automation.Peers;

public class MultiSelectTreeViewItemDataAutomationPeer(object item, ItemsControlAutomationPeer itemsControlAutomationPeer) :
    ItemAutomationPeer(item, itemsControlAutomationPeer),
    ISelectionItemProvider,
    IScrollItemProvider,
    IExpandCollapseProvider,
    IValueProvider
{
    ExpandCollapseState IExpandCollapseProvider.ExpandCollapseState => ItemPeer.ExpandCollapseState;

    bool ISelectionItemProvider.IsSelected => ((ISelectionItemProvider)ItemPeer).IsSelected;

    IRawElementProviderSimple? ISelectionItemProvider.SelectionContainer =>
            // TreeViewItemAutomationPeer treeViewItemAutomationPeer = GetWrapperPeer() as TreeViewItemAutomationPeer;
            // if (treeViewItemAutomationPeer != null)
            // {
            // ISelectionItemProvider selectionItemProvider = treeViewItemAutomationPeer;
            // return selectionItemProvider.SelectionContainer;
            // }

            // this.ThrowElementNotAvailableException();
            null;

    private MultiSelectTreeViewItemAutomationPeer ItemPeer
    {
        get
        {
            AutomationPeer? automationPeer = null;
            var wrapper = GetWrapper();
            if (wrapper != null)
            {
                automationPeer = UIElementAutomationPeer.CreatePeerForElement(wrapper);
                if (automationPeer == null)
                {
                    if (wrapper is FrameworkElement element)
                    {
                        automationPeer = new FrameworkElementAutomationPeer(element);
                    }
                    else
                    {
                        automationPeer = new UIElementAutomationPeer(wrapper);
                    }
                }
            }


            if (automationPeer is not MultiSelectTreeViewItemAutomationPeer treeViewItemAutomationPeer)
            {
                throw new InvalidOperationException("Could not find parent automation peer.");
            }

            return treeViewItemAutomationPeer;
        }
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

        if (patternInterface == PatternInterface.ItemContainer
            || patternInterface == PatternInterface.SynchronizedInput)
        {
            return ItemPeer;
        }

        return base.GetPattern(patternInterface);
    }

    void IExpandCollapseProvider.Collapse() => ItemPeer.Collapse();

    void IExpandCollapseProvider.Expand() => ItemPeer.Expand();

    void IScrollItemProvider.ScrollIntoView() => ((IScrollItemProvider)ItemPeer).ScrollIntoView();

    void ISelectionItemProvider.AddToSelection() => ((ISelectionItemProvider)ItemPeer).AddToSelection();

    void ISelectionItemProvider.RemoveFromSelection() => ((ISelectionItemProvider)ItemPeer).RemoveFromSelection();

    void ISelectionItemProvider.Select() => ((ISelectionItemProvider)ItemPeer).Select();

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TreeItem;

    protected override string GetClassNameCore() => "TreeViewItem";

    private UIElement? GetWrapper()
    {
        UIElement? result = null;
        var itemsControlAutomationPeer = ItemsControlAutomationPeer;
        if (itemsControlAutomationPeer != null)
        {
            var itemsControl = (ItemsControl)itemsControlAutomationPeer.Owner;
            if (itemsControl != null)
            {
                result = itemsControl.ItemContainerGenerator.ContainerFromItem(Item) as UIElement;
            }
        }

        return result;
    }

    bool IValueProvider.IsReadOnly => ((IValueProvider)ItemPeer).IsReadOnly;

    void IValueProvider.SetValue(string value) => ((IValueProvider)ItemPeer).SetValue(value);

    string IValueProvider.Value => ((IValueProvider)ItemPeer).Value;
}
