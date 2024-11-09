﻿using System.Windows.Threading;

namespace System.Windows.Controls;

/// <summary>
/// Helper methods to focus.
/// </summary>
public static class FocusHelper
{
    public static void Focus(EditTextBox element)
    {
        if (element == null) return;
        //System.Diagnostics.Debug.WriteLine("Focus textbox with helper:" + element.Text);
        FocusCore(element);
        element.BringIntoView();
    }

    public static void Focus(MultiSelectTreeViewItem element, bool bringIntoView = false)
    {
        if (element == null) return;
        //System.Diagnostics.Debug.WriteLine("FocusHelper focusing " + (bringIntoView ? "[into view] " : "") + element.DataContext);
        FocusCore(element);

        if (bringIntoView)
        {
            var itemContent = (FrameworkElement)element.Template.FindName("PART_Header", element);
            if (itemContent != null)   // May not be rendered yet...
            {
                ((FrameworkElement)itemContent.Parent).BringIntoView();
            }
        }
    }

    public static void Focus(MultiSelectTreeView element)
    {
        if (element == null) return;
        //System.Diagnostics.Debug.WriteLine("Focus Tree with helper");
        FocusCore(element);
        element.BringIntoView();
    }

    private static void FocusCore(FrameworkElement element)
    {
        //System.Diagnostics.Debug.WriteLine("Focusing element " + element.ToString());
        //System.Diagnostics.Debug.WriteLine(Environment.StackTrace);
        if (!element.Focus())
        {
            //System.Diagnostics.Debug.WriteLine("- Element could not be focused, invoking in dispatcher thread");
            element.Dispatcher.BeginInvoke(DispatcherPriority.Input, new ThreadStart(() => element.Focus()));
        }

#if DEBUG
        // no good idea, seems to block sometimes
        var i = 0;
        while (i < 5)
        {
            if (element.IsFocused)
            {
                //if (i > 0)
                //    System.Diagnostics.Debug.WriteLine("- Element is focused now in round " + i + ", leaving");
                return;
            }
            Thread.Sleep(20);
            i++;
        }
        //if (i >= 5)
        //{
        //    System.Diagnostics.Debug.WriteLine("- Element is not focused after 500 ms, giving up");
        //}
#endif
    }
}
