using System.Windows.Input;

namespace JapaneseDictionary.Controls;

/// <summary>
/// Long-press as attached properties (MAUI has no long-press gesture):
/// <c>controls:LongPress.Command="{Binding CopyTextCommand}"</c> plus
/// <c>controls:LongPress.CommandParameter</c>. Android-only, like the rest of the app.
/// </summary>
public static class LongPress
{
    public static readonly BindableProperty CommandProperty = BindableProperty.CreateAttached(
        "Command", typeof(ICommand), typeof(LongPress), null, propertyChanged: OnCommandChanged);

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.CreateAttached(
        "CommandParameter", typeof(object), typeof(LongPress), null);

    public static ICommand? GetCommand(BindableObject view) => (ICommand?)view.GetValue(CommandProperty);
    public static void SetCommand(BindableObject view, ICommand? value) => view.SetValue(CommandProperty, value);

    public static object? GetCommandParameter(BindableObject view) => view.GetValue(CommandParameterProperty);
    public static void SetCommandParameter(BindableObject view, object? value) => view.SetValue(CommandParameterProperty, value);

    private static void OnCommandChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is not View view) return;

        // The native view only exists once the handler is set, and is replaced
        // if the handler is; re-applying the listener is idempotent.
        view.HandlerChanged -= OnHandlerChanged;
        if (newValue is not null) view.HandlerChanged += OnHandlerChanged;
        Apply(view);
    }

    private static void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is View view) Apply(view);
    }

    private static void Apply(View view)
    {
        if (view.Handler?.PlatformView is not Android.Views.View native) return;
        native.SetOnLongClickListener(GetCommand(view) is null ? null : new Listener(view));
    }

    private sealed class Listener(View view) : Java.Lang.Object, Android.Views.View.IOnLongClickListener
    {
        public bool OnLongClick(Android.Views.View? v)
        {
            var command = GetCommand(view);
            var parameter = GetCommandParameter(view);
            if (command is null || !command.CanExecute(parameter)) return false;

            v?.PerformHapticFeedback(Android.Views.FeedbackConstants.LongPress);
            command.Execute(parameter);
            return true;
        }
    }
}
