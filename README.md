# WhisperPtt — Push-to-Talk Speech Recognition for Windows

**WhisperPtt** is a lightweight Windows tray application that transcribes speech to text using OpenAI Whisper and types the result directly into any active window — no clipboard involved.

---

## Requirements

- Windows 10/11 (x64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — for building from source
- NVIDIA GPU with CUDA 12 support *(optional, for GPU acceleration)*

---

## Quick Start

```bash
# 1. Clone the repo
git clone https://github.com/andrey89/WhisperPtt.git
cd WhisperPtt

# 2. Restore NuGet packages (downloads all native Whisper + CUDA DLLs automatically)
dotnet restore

# 3. Build
dotnet build -c Release

# 4. Run
dotnet run -c Release
```

On first launch, open **Settings** (tray icon → right-click → Настройки) and choose a Whisper model. The model will be downloaded automatically.

---

## How It Works

1. **Hold** the configured hotkey (default: `F2`) → recording starts
2. **Speak** into the microphone
3. **Release** the hotkey → speech is transcribed and typed into the active window

Hold the hotkey again **during transcription** to cancel it instantly.

The model is **downloaded automatically** on first use and cached in `%AppData%\WhisperPtt\Models\`.

---

## Whisper Models

Models are downloaded automatically from [Hugging Face](https://huggingface.co/ggerganov/whisper.cpp/tree/main) and stored in `%AppData%\WhisperPtt\Models\`.

| Model          | File                    | Size   | Speed  | Accuracy |
|----------------|-------------------------|--------|--------|----------|
| tiny           | ggml-tiny.bin           | 75 MB  | ⚡⚡⚡⚡ | ★★☆☆     |
| base           | ggml-base.bin           | 142 MB | ⚡⚡⚡  | ★★★☆     |
| small          | ggml-small.bin          | 466 MB | ⚡⚡    | ★★★★     |
| medium         | ggml-medium.bin         | 1.5 GB | ⚡     | ★★★★★    |
| large-v3-turbo | ggml-large-v3-turbo.bin | 1.6 GB | ⚡     | ★★★★★    |
| large-v3       | ggml-large-v3.bin       | 3.1 GB | ⚡     | ★★★★★    |

Select the model name in Settings — download starts automatically on first use.

---

## Configuration

Settings are saved automatically to `%AppData%\WhisperPtt\settings.json`:

```json
{
  "SelectedModel": "base",
  "SelectedLanguage": "ru",
  "SelectedAudioDevice": "Системный по умолчанию",
  "CustomPrompt": "",
  "UnloadTimeoutMinutes": 10,
  "Hotkey": "F2"
}
```

| Field                  | Description                                                   |
|------------------------|---------------------------------------------------------------|
| `SelectedModel`        | Model name: `tiny`, `base`, `small`, `medium`, `large-v3-turbo`, `large-v3` |
| `SelectedLanguage`     | Language code: `ru`, `en`, `auto`, etc.                       |
| `SelectedAudioDevice`  | Microphone name or `"Системный по умолчанию"` for default     |
| `CustomPrompt`         | Optional hint for Whisper (improves punctuation/accuracy)     |
| `UnloadTimeoutMinutes` | Minutes of inactivity before model is unloaded from RAM (0 = never) |
| `Hotkey`               | Key or combination: `F2`, `Alt+F2`, `Ctrl+Shift+R`, etc.     |

---

## GPU Support

CUDA acceleration is enabled **automatically** if an NVIDIA GPU with CUDA 12 is present. The app tries CUDA first and silently falls back to CPU if unavailable.

No manual configuration is required — the `Whisper.net.Runtime.Cuda12.Windows` NuGet package bundles all required native DLLs and they are restored automatically via `dotnet restore`.

A diagnostic log is written to `cuda_diag.txt` next to the executable if CUDA loading fails.

---

## Project Structure

```
WhisperPtt/
├── App.xaml / App.xaml.cs        # Entry point: tray icon, hotkey workflow, services lifecycle
├── WhisperPtt.csproj             # Project + NuGet dependencies
├── app_icon.ico                  # Application icon (multi-resolution: 16/32/48/256px)
├── Helpers/
│   └── Win32Helper.cs            # P/Invoke: SendInput (Unicode text injection), window styles
├── Models/
│   └── AppSettings.cs            # Settings model — JSON serializable, singleton, stored in AppData
├── Services/
│   ├── AudioService.cs           # Microphone capture via NAudio (WASAPI)
│   ├── HotkeyService.cs          # Global hotkey registration via Win32 RegisterHotKey
│   ├── InputService.cs           # Unicode text injection via SendInput (no clipboard)
│   └── WhisperService.cs         # Model download, loading (CUDA/CPU), transcription
├── ViewModels/                   # MVVM ViewModels (SettingsViewModel, WidgetViewModel)
└── Views/                        # WPF Windows (SettingsWindow, WidgetWindow overlay)
```

---

## License

MIT
