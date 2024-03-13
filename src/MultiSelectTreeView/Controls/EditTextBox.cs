using System.Windows.Data;
using System.Windows.Input;

namespace System.Windows.Controls
{
    /// <summary>
    /// Text box which focuses itself on load and selects all text in it.
    /// </summary>
    public class EditTextBox : TextBox
    {
        private string? _startText;

        static EditTextBox() => DefaultStyleKeyProperty.OverrideMetadata(typeof(EditTextBox), new FrameworkPropertyMetadata(typeof(EditTextBox)));

        public EditTextBox() => Loaded += OnTreeViewEditTextBoxLoaded;

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            _startText = Text;
            SelectAll();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e == null) return;
            base.OnKeyDown(e);
            if (!e.Handled)
            {
                var key = e.Key;
                switch (key)
                {
                    case Key.Escape:
                        Text = _startText;
                        break;
                }
            }
        }

        private void OnTreeViewEditTextBoxLoaded(object sender, RoutedEventArgs e)
        {
            var be = GetBindingExpression(TextProperty);
            be?.UpdateTarget();
            FocusHelper.Focus(this);
        }
    }
}
