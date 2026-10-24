# Extended WPF Toolkit replacement

Velopack.UI used `DotNetProjects.Extended.Wpf.Toolkit` only in `Views/MainView.xaml`: `BusyIndicator` for package progress/cancellation and `IntegerUpDown` for signing concurrency. Both uses now use the existing `CrissCross.WPF.UI` 5.0.2 dependency, allowing the toolkit package to be removed.

| Previous control | Replacement | Preserved behavior |
| --- | --- | --- |
| `BusyIndicator` | `ui:BusyOverlay` with a local control template | Immediate busy overlay, live package stage, indeterminate progress and Cancel command. Underlying content is disabled while busy, including keyboard interaction. |
| `IntegerUpDown` | `ui:NumberBox` | Two-way signing concurrency binding, minimum 1, whole-number precision and increment of 1. Maximum is explicitly `Int32.MaxValue` to match the model. Expressions are disabled. |

## API evidence

The installed package declares repository commit `fb590577d7d31c12800da71826d75232e4a74acc`. Its XML documentation and the matching primary source establish the APIs used here:

- [BusyOverlay source](https://github.com/reactivemarbles/CrissCross/blob/fb590577d7d31c12800da71826d75232e4a74acc/src/CrissCross.WPF.UI/Controls/BusyOverlay/BusyOverlay.cs): derives from WPF `ContentControl` and exposes `IsBusy` and `Operation` dependency properties.
- [BusyOverlay default template](https://github.com/reactivemarbles/CrissCross/blob/fb590577d7d31c12800da71826d75232e4a74acc/src/CrissCross.WPF.UI/Controls/BusyOverlay/BusyOverlay.xaml): overlays content with progress, operation title/message, and cancellation. The application uses a local template to bind its existing live stage and cancellation command directly.
- [NumberBox source](https://github.com/reactivemarbles/CrissCross/blob/fb590577d7d31c12800da71826d75232e4a74acc/src/CrissCross.WPF.UI/Controls/NumberBox/NumberBox.cs): exposes nullable-double `Value`, `Minimum`, `Maximum`, `MaxDecimalPlaces`, `SmallChange`, `AcceptsExpression`, and two-way binding metadata.

## Behavioral differences

The busy presentation uses the CrissCross progress ring and themed panel. It does not reproduce the old toolkit visual style. `NumberBox` evaluates typed input on Enter or focus loss; spin buttons update immediately. `MaxDecimalPlaces=0` rounds numeric input to a whole number rather than rejecting decimal keystrokes. The model remains an integer and the control bounds match its range.

The CrissCross skill was consulted. Its dedicated MCP tools were unavailable in this session; installed package documentation and matching repository source supplied the API evidence. XML parsing checks markup structure; compiled application and desktop checks remain necessary to verify binding and interaction behavior.
