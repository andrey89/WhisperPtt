using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using WhisperPtt.Helpers;
using WhisperPtt.ViewModels;

namespace WhisperPtt.Views;

public partial class WidgetWindow : Window
{
    public WidgetWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        IsVisibleChanged += OnIsVisibleChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionBottomCenter();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
        if (hwndSource != null)
        {
            Win32Helper.MakeWindowNoActivate(hwndSource.Handle);
        }
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        // Immediately deactivate — we never want focus on the widget
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            StartAnimationsIfNeeded();
        }
    }

    private void StartAnimationsIfNeeded()
    {
        if (DataContext is WidgetViewModel vm)
        {
            if (vm.CurrentState == WidgetState.Loading)
            {
                TryStartStoryboard("SpinAnimation");
            }
            else if (vm.CurrentState == WidgetState.Transcribing)
            {
                TryStartStoryboard("SpinAnimationTranscribe");
            }
        }
    }

    public void OnStateChanged()
    {
        if (DataContext is WidgetViewModel vm)
        {
            if (vm.CurrentState == WidgetState.Loading)
            {
                TryStartStoryboard("SpinAnimation");
            }
            else if (vm.CurrentState == WidgetState.Transcribing)
            {
                TryStartStoryboard("SpinAnimationTranscribe");
            }
        }
    }

    private void TryStartStoryboard(string name)
    {
        if (Resources[name] is Storyboard sb)
        {
            try
            {
                sb.Begin(this, true);
            }
            catch
            {
                // Storyboard target may not be rendered yet
            }
        }
    }

    private void PositionBottomCenter()
    {
        var workArea = SystemParameters.WorkArea;
        const double margin = 20;
        Left = workArea.Left + (workArea.Width - ActualWidth) / 2;
        Top = workArea.Bottom - ActualHeight - margin;
    }
}
