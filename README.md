# WhisperPtt — Push-to-Talk Speech Recognition for Windows

**WhisperPtt** is a lightweight Windows tray application that transcribes speech to text using OpenAI Whisper and types the result directly into any active window. Supports GPU acceleration via CUDA.

---

## Requirements

- Windows 10/11 (x64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- NVIDIA GPU with CUDA 12 support (optional, for GPU acceleration)
- A Whisper model file (`.bin`), e.g. [ggml-small.bin](https://huggingface.co/ggerganov/whisper.cpp)

---

## Quick Start

```bash
# 1. Clone the repo
git clone https://github.com/andrey89/WhisperPtt.git
cd WhisperPtt

# 2. Restore NuGet packages (includes all native Whisper + CUDA DLLs automatically)
dotnet restore

# 3. Build
dotnet build -c Release

# 4. Run
dotnet run -c Release
```

On first launch, go to **Settings** and specify the path to your Whisper model `.bin` file.

---

## Whisper Model Download

Models are stored in `%AppData%\WhisperPtt\Models\`. Download the desired `.bin` file from Hugging Face and place it there:

| Model          | File                      | Size   | Speed  | Accuracy |
|----------------|---------------------------|--------|--------|----------|
| tiny           | ggml-tiny.bin             | 75 MB  | ⚡⚡⚡⚡ | ★★☆☆     |
| base           | ggml-base.bin             | 142 MB | ⚡⚡⚡  | ★★★☆     |
| small          | ggml-small.bin            | 466 MB | ⚡⚡    | ★★★★     |
| medium         | ggml-medium.bin           | 1.5 GB | ⚡     | ★★★★★    |
| large-v3-turbo | ggml-large-v3-turbo.bin   | 1.6 GB | ⚡     | ★★★★★    |

Download: https://huggingface.co/ggerganov/whisper.cpp/tree/main

After placing the file, select the model name (e.g. `base`) in the app Settings.

---

## Features

- 🎙️ Hold a hotkey → speak → release → text is typed automatically
- 🖥️ System tray icon, no taskbar clutter
- ⚡ CUDA GPU acceleration (auto-detected, falls back to CPU)
- ⌨️ Direct Unicode input — no clipboard required
- 🌐 Multi-language support (auto-detect or manual)

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

Available models: `tiny`, `base`, `small`, `medium`, `large-v3-turbo`, `large-v3`

---

## GPU Support

GPU acceleration is enabled automatically if:
- An NVIDIA GPU with CUDA 12 is detected
- The `Whisper.net.Runtime.Cuda12.Windows` NuGet package is installed (included by default)

No manual DLL copying is required — everything is handled by NuGet.

---

## Project Structure

```
WhisperPtt/
├── App.xaml / App.xaml.cs        # Application entry point, tray icon
├── WhisperPtt.csproj             # Project file with NuGet dependencies
├── app_icon.ico                  # Application icon
├── Helpers/
│   └── Win32Helper.cs            # P/Invoke: SendInput, window styles
├── Models/
│   └── AppSettings.cs            # Settings model (JSON serializable)
├── Services/
│   ├── AudioService.cs           # Microphone capture via NAudio
│   ├── HotkeyService.cs          # Global hotkey registration
│   ├── InputService.cs           # Unicode text injection via SendInput
│   └── WhisperService.cs         # Whisper model loading + transcription
├── ViewModels/                   # MVVM ViewModels
└── Views/                        # WPF Windows (Settings, Widget)
```

---

## License

MIT
