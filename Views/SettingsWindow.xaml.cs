using System.Windows;
using System.Windows.Input;
using WhisperPtt.ViewModels;

namespace WhisperPtt.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        
        try
        {
            var iconUri = new Uri("pack://application:,,,/WhisperPtt;component/app_icon.ico", UriKind.RelativeOrAbsolute);
            this.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
        }
        catch
        {
            // Fallback gracefully without crash if the icon fails to load
        }

        _viewModel = new SettingsViewModel();
        DataContext = _viewModel;

        _viewModel.CloseRequested += () => Close();
    }

    public SettingsViewModel ViewModel => _viewModel;

    private void HotkeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        _viewModel.CaptureHotkey(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);
    }

    private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        _viewModel.IsCapturingHotkey = true;
    }

    private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        _viewModel.IsCapturingHotkey = false;
    }
}
