# WhisperPtt — Распознавание речи «нажал-говори» для Windows

**WhisperPtt** — лёгкое приложение в системном трее, которое распознаёт речь с помощью OpenAI Whisper и **напрямую печатает текст** в любое активное окно — без буфера обмена.

---

## Требования

- Windows 10/11 (x64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — для сборки из исходников
- NVIDIA GPU с поддержкой CUDA 12 *(опционально, для ускорения на GPU)*

---

## Быстрый старт

```bash
# 1. Клонировать репозиторий
git clone https://github.com/andrey89/WhisperPtt.git
cd WhisperPtt

# 2. Восстановить NuGet-пакеты (все нативные DLL Whisper + CUDA скачаются автоматически)
dotnet restore

# 3. Собрать
dotnet build -c Release

# 4. Запустить
dotnet run -c Release
```

При первом запуске откройте **Настройки** (иконка в трее → правая кнопка → Настройки) и выберите модель. Модель скачается автоматически.

---

## Как это работает

1. **Зажмите** настроенную горячую клавишу (по умолчанию: `F2`) → запись начинается
2. **Говорите** в микрофон
3. **Отпустите** клавишу → речь распознаётся и **печатается** в активное окно

Нажмите горячую клавишу повторно **во время распознавания** — оно мгновенно отменится.

Модель **скачивается автоматически** при первом использовании и кешируется в `%AppData%\WhisperPtt\Models\`.

---

## Модели Whisper

Модели скачиваются автоматически с [Hugging Face](https://huggingface.co/ggerganov/whisper.cpp/tree/main) и сохраняются в `%AppData%\WhisperPtt\Models\`.

| Модель         | Файл                    | Размер | Скорость | Точность |
|----------------|-------------------------|--------|----------|----------|
| tiny           | ggml-tiny.bin           | 75 МБ  | ⚡⚡⚡⚡    | ★★☆☆     |
| base           | ggml-base.bin           | 142 МБ | ⚡⚡⚡     | ★★★☆     |
| small          | ggml-small.bin          | 466 МБ | ⚡⚡       | ★★★★     |
| medium         | ggml-medium.bin         | 1.5 ГБ | ⚡        | ★★★★★    |
| large-v3-turbo | ggml-large-v3-turbo.bin | 1.6 ГБ | ⚡        | ★★★★★    |
| large-v3       | ggml-large-v3.bin       | 3.1 ГБ | ⚡        | ★★★★★    |

Выберите имя модели в Настройках — загрузка начнётся автоматически при первом использовании.

---

## Настройки

Файл настроек сохраняется автоматически в `%AppData%\WhisperPtt\settings.json`:

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

| Поле                   | Описание                                                                         |
|------------------------|----------------------------------------------------------------------------------|
| `SelectedModel`        | Имя модели: `tiny`, `base`, `small`, `medium`, `large-v3-turbo`, `large-v3`     |
| `SelectedLanguage`     | Код языка: `ru`, `en`, `auto` и др.                                              |
| `SelectedAudioDevice`  | Имя микрофона или `"Системный по умолчанию"` для устройства по умолчанию        |
| `CustomPrompt`         | Подсказка для Whisper (улучшает пунктуацию и точность)                           |
| `UnloadTimeoutMinutes` | Минуты простоя до выгрузки модели из памяти (0 = не выгружать)                  |
| `Hotkey`               | Клавиша или комбинация: `F2`, `Alt+F2`, `Ctrl+Shift+R` и т.д.                   |

---

## Поддержка GPU

Ускорение на CUDA включается **автоматически** при наличии видеокарты NVIDIA с CUDA 12. Приложение сначала пробует CUDA, и при неудаче молча переключается на CPU.

Никакой ручной настройки не требуется — пакет NuGet `Whisper.net.Runtime.Cuda12.Windows` включает все нужные нативные DLL, которые восстанавливаются через `dotnet restore`.

При ошибке загрузки CUDA рядом с исполняемым файлом создаётся диагностический лог `cuda_diag.txt`.

---

## Структура проекта

```
WhisperPtt/
├── App.xaml / App.xaml.cs        # Точка входа: трей, горячая клавиша, жизненный цикл сервисов
├── WhisperPtt.csproj             # Проект и NuGet-зависимости
├── app_icon.ico                  # Иконка приложения (мульти-разрешение: 16/32/48/256px)
├── Helpers/
│   └── Win32Helper.cs            # P/Invoke: SendInput (ввод Unicode), стили окна
├── Models/
│   └── AppSettings.cs            # Модель настроек — JSON, синглтон, хранится в AppData
├── Services/
│   ├── AudioService.cs           # Захват микрофона через NAudio (WASAPI)
│   ├── HotkeyService.cs          # Глобальная горячая клавиша через Win32 RegisterHotKey
│   ├── InputService.cs           # Ввод Unicode-текста через SendInput (без буфера обмена)
│   └── WhisperService.cs         # Загрузка модели, скачивание, транскрипция (CUDA/CPU)
├── ViewModels/                   # MVVM ViewModel'и (SettingsViewModel, WidgetViewModel)
└── Views/                        # WPF-окна (SettingsWindow, WidgetWindow — оверлей)
```

---

## Лицензия

MIT
