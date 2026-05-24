namespace WhisperPtt.ViewModels;

public enum WidgetState
{
    Hidden,
    Downloading,
    Loading,
    Recording,
    Transcribing,
    Error
}

public class WidgetViewModel : ViewModelBase
{
    private WidgetState _currentState = WidgetState.Hidden;
    public WidgetState CurrentState
    {
        get => _currentState;
        set
        {
            if (SetProperty(ref _currentState, value))
            {
                OnPropertyChanged(nameof(IsVisible));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    private double _rmsLevel;
    public double RmsLevel
    {
        get => _rmsLevel;
        set
        {
            if (SetProperty(ref _rmsLevel, Math.Clamp(value, 0.0, 1.0)))
            {
                OnPropertyChanged(nameof(PulseScale));
                OnPropertyChanged(nameof(HaloScale));
                OnPropertyChanged(nameof(HaloOpacity));
            }
        }
    }

    private int _downloadProgress;
    public int DownloadProgress
    {
        get => _downloadProgress;
        set => SetProperty(ref _downloadProgress, Math.Clamp(value, 0, 100));
    }

    private string _errorText = string.Empty;
    public string ErrorText
    {
        get => _errorText;
        set => SetProperty(ref _errorText, value);
    }

    public bool IsVisible => CurrentState != WidgetState.Hidden;

    private double BoostedRms => Math.Sqrt(RmsLevel);

    public double PulseScale => 1.0 + BoostedRms * 0.5;
    public double HaloScale => 1.0 + BoostedRms * 3.5;
    public double HaloOpacity => 0.15 + BoostedRms * 0.85;

    public string StatusText => CurrentState switch
    {
        WidgetState.Downloading => $"Скачивание модели... {DownloadProgress}%",
        WidgetState.Loading => "Загрузка модели...",
        WidgetState.Recording => "Запись...",
        WidgetState.Transcribing => "Распознавание...",
        WidgetState.Error => ErrorText,
        _ => string.Empty
    };
}
