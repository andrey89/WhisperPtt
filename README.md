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

Download a model from Hugging Face and place it anywhere on disk:

| Model   | Size   | Speed  | Accuracy |
|---------|--------|--------|----------|
| tiny    | 75 MB  | ⚡⚡⚡⚡ | ★★☆☆     |
| base    | 142 MB | ⚡⚡⚡  | ★★★☆     |
| small   | 466 MB | ⚡⚡    | ★★★★     |
| medium  | 1.5 GB | ⚡     | ★★★★★    |

Download link: https://huggingface.co/ggerganov/whisper.cpp/tree/main

---

## Features

- 🎙️ Hold a hotkey → speak → release → text is typed automatically
- 🖥️ System tray icon, no taskbar clutter
- ⚡ CUDA GPU acceleration (auto-detected, falls back to CPU)
- ⌨️ Direct Unicode input — no clipboard required
- 🌐 Multi-language support (auto-detect or manual)

---

## Configuration

Settings are saved to `settings.json` next to the executable:

```json
{
  "ModelPath": "C:\\Models\\ggml-small.bin",
  "HotkeyVirtualKeyCode": 120,
  "Language": "auto",
  "UseGpu": true
}
```

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
