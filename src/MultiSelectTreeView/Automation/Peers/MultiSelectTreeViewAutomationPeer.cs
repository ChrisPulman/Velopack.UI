using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace System.Windows.Automation.Peers
{
    public class MultiSelectTreeViewAutomationPeer(MultiSelectTreeView owner) : ItemsControlAutomationPeer(owner), ISelectionProvider
    {
        public bool CanSelectMultiple => false;

        public bool IsSelectionRequired => false;

        public override object GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.Selection)
            {
                return this;
            }

            if (patternInterface == PatternInterface.Scroll)
            {
                var itemsControl = (ItemsControl)Owner;
                ////if (itemsControl.ScrollHost != null)
                ////{
                ////    var automationPeer = UIElementAutomationPeer.CreatePeerForElement(itemsControl.ScrollHost);
                ////    if (automationPeer != null && automationPeer is IScrollProvider provider)
                ////    {
                ////        automationPeer.EventsSource = this;
                ////        return provider;
                ////    }
                ////}
            }

            return base.GetPattern(patternInterface);
        }

        IRawElementProviderSimple[]? ISelectionProvider.GetSelection()
        {
            IRawElementProviderSimple[]? array = null;

            ////MultiSelectTreeViewItem selectedContainer = ((MultiSelectTreeView)base.Owner).SelectedContainer;
            ////if (selectedContainer != null)
            ////{
            ////    var automationPeer = FromElement(selectedContainer);
            ////    if (automationPeer.EventsSource != null)
            ////    {
            ////        automationPeer = automationPeer.EventsSource;
            ////    }

            ////    if (automationPeer != null)
            ////    {
            ////        array = [ProviderFromPeer(automationPeer)];
            ////    }
            ////}

            if (array == null)
            {
                array = [];
            }
            return array;
        }

        /// <summary>
        /// When overridden in a derived class, creates a new instance of the
        /// <see cref="T:System.Windows.Automation.Peers.ItemAutomationPeer"/> for a data item in
        /// the <see cref="P:System.Windows.Controls.ItemsControl.Items"/> collection of this
        /// <see cref="T:System.Windows.Controls.ItemsControl"/>.
        /// </summary>
        /// <param name="item">
        /// The data item that is associated with this <see cref="T:System.Windows.Automation.Peers.ItemAutomationPeer"/>.
        /// </param>
        /// <returns>
        /// The new <see cref="T:System.Windows.Automation.Peers.ItemAutomationPeer"/> created.
        /// </returns>
        protected override ItemAutomationPeer CreateItemAutomationPeer(object item) =>
            new MultiSelectTreeViewItemDataAutomationPeer(item, this);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tree;

        protected override string GetClassNameCore() => nameof(MultiSelectTreeView);
    }
}
