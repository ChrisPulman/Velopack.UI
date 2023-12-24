using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace System.Windows.Controls
{
	internal class BorderSelectionLogic : IDisposable
	{
		private MultiSelectTreeView _treeView;
		private readonly Border _border;
		private readonly ScrollViewer _scrollViewer;
		private readonly ItemsPresenter _content;
		private readonly IEnumerable<MultiSelectTreeViewItem> _items;

		private bool _isFirstMove;
		private bool _mouseDown;
		private Point _startPoint;
		private DateTime _lastScrollTime;
		private HashSet<object> _initialSelection;

		public BorderSelectionLogic(MultiSelectTreeView treeView, Border selectionBorder, ScrollViewer scrollViewer, ItemsPresenter content, IEnumerable<MultiSelectTreeViewItem> items)
		{
			if (treeView == null)
			{
				throw new ArgumentNullException(nameof(treeView));
			}
			if (selectionBorder == null)
			{
				throw new ArgumentNullException(nameof(selectionBorder));
			}
			if (scrollViewer == null)
			{
				throw new ArgumentNullException(nameof(scrollViewer));
			}
			if (content == null)
			{
				throw new ArgumentNullException(nameof(content));
			}
			if (items == null)
			{
				throw new ArgumentNullException(nameof(items));
			}

			this._treeView = treeView;
			this._border = selectionBorder;
			this._scrollViewer = scrollViewer;
			this._content = content;
			this._items = items;

			treeView.MouseDown += OnMouseDown;
			treeView.MouseMove += OnMouseMove;
			treeView.MouseUp += OnMouseUp;
			treeView.KeyDown += OnKeyDown;
			treeView.KeyUp += OnKeyUp;
		}

		public void Dispose()
		{
			if (_treeView != null)
			{
				_treeView.MouseDown -= OnMouseDown;
				_treeView.MouseMove -= OnMouseMove;
				_treeView.MouseUp -= OnMouseUp;
				_treeView.KeyDown -= OnKeyDown;
				_treeView.KeyUp -= OnKeyUp;
				_treeView = null;
			}
			GC.SuppressFinalize(this);
		}

		private void OnMouseDown(object sender, MouseButtonEventArgs e)
		{
			_mouseDown = true;
			_startPoint = Mouse.GetPosition(_content);

			// Debug.WriteLine("Initialize drwawing");
			_isFirstMove = true;
			// Capture the mouse right now so that the MouseUp event will not be missed
			Mouse.Capture(_treeView);

			_initialSelection = new HashSet<object>(_treeView.SelectedItems.Cast<object>());
		}

		private void OnMouseMove(object sender, MouseEventArgs e)
		{
			if (_mouseDown)
			{
				if (DateTime.UtcNow > _lastScrollTime.AddMilliseconds(100))
				{
					Point currentPointWin = Mouse.GetPosition(_scrollViewer);
					if (currentPointWin.Y < 16)
					{
						_scrollViewer.LineUp();
						_scrollViewer.UpdateLayout();
						_lastScrollTime = DateTime.UtcNow;
					}
					if (currentPointWin.Y > _scrollViewer.ActualHeight - 16)
					{
						_scrollViewer.LineDown();
						_scrollViewer.UpdateLayout();
						_lastScrollTime = DateTime.UtcNow;
					}
				}

				Point currentPoint = Mouse.GetPosition(_content);
				double width = currentPoint.X - _startPoint.X + 1;
				double height = currentPoint.Y - _startPoint.Y + 1;
				double left = _startPoint.X;
				double top = _startPoint.Y;

				if (_isFirstMove)
				{
					if (Math.Abs(width) <= SystemParameters.MinimumHorizontalDragDistance &&
						Math.Abs(height) <= SystemParameters.MinimumVerticalDragDistance)
					{
						return;
					}

					_isFirstMove = false;
					if (!SelectionMultiple.IsControlKeyDown)
					{
						if (!_treeView.ClearSelectionByRectangle())
						{
							EndAction();
							return;
						}
					}
				}

				// Debug.WriteLine(string.Format("Drawing: {0};{1};{2};{3}",startPoint.X,startPoint.Y,width,height));
				if (width < 1)
				{
					width = Math.Abs(width - 1) + 1;
					left = _startPoint.X - width + 1;
				}

				if (height < 1)
				{
					height = Math.Abs(height - 1) + 1;
					top = _startPoint.Y - height + 1;
				}

				_border.Width = width;
				Canvas.SetLeft(_border, left);
				_border.Height = height;
				Canvas.SetTop(_border, top);

				_border.Visibility = Visibility.Visible;

				double right = left + width - 1;
				double bottom = top + height - 1;

				// Debug.WriteLine(string.Format("left:{1};right:{2};top:{3};bottom:{4}", null, left, right, top, bottom));
				SelectionMultiple selection = (SelectionMultiple) _treeView.Selection;
				bool foundFocusItem = false;
				foreach (var item in _items)
				{
					FrameworkElement itemContent = (FrameworkElement) item.Template.FindName("PART_Header", item);
					if (itemContent == null) 
					{
						continue;
					}

					Point p = ((FrameworkElement)itemContent.Parent).TransformToAncestor(_content).Transform(new Point());
					double itemLeft = p.X;
					double itemRight = p.X + itemContent.ActualWidth - 1;
					double itemTop = p.Y;
					double itemBottom = p.Y + itemContent.ActualHeight - 1;

					// Debug.WriteLine(string.Format("element:{0};itemleft:{1};itemright:{2};itemtop:{3};itembottom:{4}",item.DataContext,itemLeft,itemRight,itemTop,itemBottom));

					// Compute the current input states for determining the new selection state of the item
					bool intersect = !(itemLeft > right || itemRight < left || itemTop > bottom || itemBottom < top);
					bool initialSelected = _initialSelection != null && _initialSelection.Contains(item.DataContext);
					bool ctrl = SelectionMultiple.IsControlKeyDown;

					// Decision matrix:
					// If the Ctrl key is pressed, each intersected item will be toggled from its initial selection.
					// Without the Ctrl key, each intersected item is selected, others are deselected.
					//
					// newSelected
					// ─────────┬───────────────────────
					//          │ intersect
					//          │  0        │  1
					//          ├───────────┴───────────
					//          │ initial
					//          │  0  │  1  │  0  │  1
					// ─────────┼─────┼─────┼─────┼─────
					// ctrl  0  │  0  │  0  │  1  │  1   = intersect
					// ─────────┼─────┼─────┼─────┼─────
					//       1  │  0  │  1  │  1  │  0   = intersect XOR initial
					//
					bool newSelected = intersect ^ (initialSelected && ctrl);

					// The new selection state for this item has been determined. Apply it.
					if (newSelected)
					{
						// The item shall be selected
						if (!_treeView.SelectedItems.Contains(item.DataContext))
						{
							// The item is not currently selected. Try to select it.
							if (!selection.SelectByRectangle(item))
							{
								if (selection.LastCancelAll)
								{
									EndAction();
									return;
								}
							}
						}
					}
					else
					{
						// The item shall be deselected
						if (_treeView.SelectedItems.Contains(item.DataContext))
						{
							// The item is currently selected. Try to deselect it.
							if (!selection.DeselectByRectangle(item))
							{
								if (selection.LastCancelAll)
								{
									EndAction();
									return;
								}
							}
						}
					}

					// Always focus and bring into view the item under the mouse cursor
					if (!foundFocusItem &&
						currentPoint.X >= itemLeft && currentPoint.X <= itemRight &&
						currentPoint.Y >= itemTop && currentPoint.Y <= itemBottom)
					{
						FocusHelper.Focus(item, true);
						_scrollViewer.UpdateLayout();
						foundFocusItem = true;
					}
				}

				if (e != null)
				{
					e.Handled = true;
				}
			}
		}

		private void OnMouseUp(object sender, MouseButtonEventArgs e)
		{
			EndAction();

			// Clear selection if this was a non-ctrl click outside of any item (i.e. in the background)
			Point currentPoint = e.GetPosition(_content);
			double width = currentPoint.X - _startPoint.X + 1;
			double height = currentPoint.Y - _startPoint.Y + 1;
			if (Math.Abs(width) <= SystemParameters.MinimumHorizontalDragDistance &&
				Math.Abs(height) <= SystemParameters.MinimumVerticalDragDistance &&
				!SelectionMultiple.IsControlKeyDown)
			{
				_treeView.ClearSelection();
			}
		}

		private void OnKeyDown(object sender, KeyEventArgs e)
		{
			// The mouse move handler reads the Ctrl key so is dependent on it.
			// If the key state has changed, the selection needs to be updated.
			OnMouseMove(null, null);
		}

		private void OnKeyUp(object sender, KeyEventArgs e)
		{
			// The mouse move handler reads the Ctrl key so is dependent on it.
			// If the key state has changed, the selection needs to be updated.
			OnMouseMove(null, null);
		}

		private void EndAction()
		{
			Mouse.Capture(null);
			_mouseDown = false;
			_border.Visibility = Visibility.Collapsed;
			_initialSelection = null;

			// Debug.WriteLine("End drawing");
		}
	}
}
