# Bugs To Fix

## Crash: "Cannot Reopen undo unit while another unit is already open" when typing `.` in an empty Decimal field

**Reported:** 2026-10-01
**Version:** Timelapse 2.5.1.1
**Status:** Diagnosed, not yet fixed

### User-facing symptom
Timelapse crashes (unhandled dispatcher exception, app shuts down) while the user is typing into a data field.

### Crash report
```
System.InvalidOperationException: Cannot Reopen undo unit while another unit is already open.
   at MS.Internal.Documents.UndoManager.Reopen(IParentUndoUnit unit)
   at System.Windows.Documents.TextEditorTyping.OnTextInput(Object sender, TextCompositionEventArgs e)
   at System.Windows.EventRoute.InvokeHandlersImpl(Object source, RoutedEventArgs args, Boolean reRaised)
   at System.Windows.UIElement.RaiseEventImpl(DependencyObject sender, RoutedEventArgs args)
   at System.Windows.UIElement.RaiseEvent(RoutedEventArgs args, Boolean trusted)
   at System.Windows.Input.InputManager.ProcessStagingArea()
   at System.Windows.Input.TextCompositionManager.PostProcessInput(Object sender, ProcessInputEventArgs e)
   at System.Windows.Input.InputManager.ProcessStagingArea()
   at System.Windows.Input.TextCompositionManager.PostProcessInput(Object sender, ProcessInputEventArgs e)
   at System.Windows.Input.InputManager.ProcessStagingArea()
   at System.Windows.Interop.HwndKeyboardInputProvider.ProcessTextInputAction(IntPtr hwnd, WindowMessage msg, IntPtr wParam, IntPtr lParam, Boolean& handled)
   at System.Windows.Interop.HwndSource.OnPreprocessMessage(Object param)
   at System.Windows.Threading.ExceptionWrapper.InternalRealCall(Delegate callback, Object args, Int32 numArgs)
   at System.Windows.Threading.ExceptionWrapper.TryCatchWhen(Object source, Delegate callback, Object args, Int32 numArgs, Delegate catchHandler)
```

Log context: session was `Z:\490R\Brenna_sorted`, crash occurred in `TimelapseWindow.xaml.cs(444) OnDispatcherUnhandledException`.

### Root cause
[ControlsDataHelpers.cs:405-415](src/Timelapse/ControlsCore/ControlsDataHelpers.cs#L405-L415), in `TextBoxHandleDecimalInputOnly`:

```csharp
string tmpString = textBox.SelectionLength > 0 
    ? textBox.Text.Remove(...).Insert(...)
    : textBox.Text.Insert(textBox.CaretIndex, args.Text);
if (tmpString == ".")
{
    textBox.Text ="0.";
    textBox.CaretIndex = 2;
    args.Handled = true;
    return true;
}
```

This method is wired up as a `PreviewTextInput` handler (via [DataEntryDecimalBase.cs:65,70](src/Timelapse/ControlsDataEntry/DataEntryDecimalBase.cs#L65) → `ValidationCallbacks.PreviewInput_DecimalCharacterOnly` / `PreviewInput_DecimalPositiveCharacterOnly`) on a Decimal data-entry field's `DoubleUpDown`/`WatermarkTextBox`.

When the user types `.` as the first character into an empty decimal field, `tmpString` becomes `"."`, and the code directly assigns `textBox.Text = "0."` from inside the `PreviewTextInput` event handler. WPF's internal `TextEditorTyping` is still mid-flight processing that same keystroke (it has an undo unit open for the pending composition). Setting `TextBox.Text` directly re-enters WPF's text-editing/undo machinery before the original unit has closed, so when control returns to `TextEditorTyping.OnTextInput`, it tries to open/reopen an undo unit and finds one already open — throwing the exception above.

### Reproduction conditions
1. Field is a Decimal-type data entry control (`DecimalAny` or `DecimalPositive`).
2. The textbox is empty (or selection/caret state makes the resulting preview text exactly `"."`) when the user types `.`.

This is an easy, very plausible action during normal data entry (e.g. typing `.5` into an empty decimal field).

### Scope / notes
- The equivalent integer/alphanumeric validators (`TextBoxHandleIntegerInputOnly`, `TextBoxHandleAlphanumericInputOnly`) don't have this problem — they only set `args.Handled` during `PreviewTextInput`, never reassign `.Text` mid-keystroke.
- `TextBoxHandleDecimalInputOnly` is shared code, so this likely affects both regular Decimal data-entry controls and the Metadata decimal controls (`MetadataDataEntryDecimalBase`) that reuse the same `ValidationCallbacks`/`ControlsDataHelpers` path.

### Suggested fix direction (not yet implemented)
Don't reassign `TextBox.Text` synchronously inside the `PreviewTextInput` handler. Options:
- Defer the `"0."` auto-correction via `Dispatcher.BeginInvoke` so it runs after the current input/undo transaction completes.
- Avoid the substitution entirely and instead just let `"."` through as valid input text (similar to how `"-"` is already allowed through unmodified for the integer/decimal "any" case), fixing up to `"0."` only on `TextChanged`/`LostFocus` rather than mid-keystroke.
