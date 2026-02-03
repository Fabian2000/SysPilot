using System.Windows;

namespace SysPilot.Controls;

public partial class CustomDialog : Window
{
    public enum DialogType { Info, Warning, Error, Question }
    public enum ButtonResult { Primary, Secondary, None }

    public ButtonResult Result { get; private set; } = ButtonResult.None;

    public CustomDialog()
    {
        InitializeComponent();
    }

    public static ButtonResult Show(
        string title,
        string message,
        DialogType type = DialogType.Info,
        string primaryText = "OK",
        string? secondaryText = null,
        Window? owner = null)
    {
        var dialog = new CustomDialog
        {
            Owner = owner ?? Application.Current.MainWindow
        };

        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.PrimaryButton.Content = primaryText;

        // Show/hide secondary button
        if (secondaryText is not null)
        {
            dialog.SecondaryButton.Content = secondaryText;
            dialog.SecondaryButton.Visibility = Visibility.Visible;
        }
        else
        {
            dialog.SecondaryButton.Visibility = Visibility.Collapsed;
        }

        dialog.ShowDialog();
        return dialog.Result;
    }

    public static Task<ButtonResult> ShowAsync(
        string title,
        string message,
        DialogType type = DialogType.Info,
        string primaryText = "OK",
        string? secondaryText = null,
        Window? owner = null)
    {
        return Task.FromResult(Show(title, message, type, primaryText, secondaryText, owner));
    }

    public static ButtonResult ShowRichContent(
        string title,
        UIElement content,
        string primaryText = "OK",
        string? secondaryText = null,
        Window? owner = null)
    {
        var dialog = new CustomDialog
        {
            Owner = owner ?? Application.Current.MainWindow
        };

        dialog.TitleText.Text = title;
        dialog.MessageText.Visibility = Visibility.Collapsed;
        dialog.RichContent.Content = content;
        dialog.RichContent.Visibility = Visibility.Visible;
        dialog.PrimaryButton.Content = primaryText;

        if (secondaryText is not null)
        {
            dialog.SecondaryButton.Content = secondaryText;
            dialog.SecondaryButton.Visibility = Visibility.Visible;
        }

        dialog.ShowDialog();
        return dialog.Result;
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        Result = ButtonResult.Primary;
        Close();
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        Result = ButtonResult.Secondary;
        Close();
    }
}
