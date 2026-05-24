using System.IO;
using System.Net.Http;
using System.Text;
using System.Runtime.InteropServices;
using Whisper.net;
using WhisperPtt.Models;

namespace WhisperPtt.Services;

public class WhisperService : IDisposable
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr LoadLibrary(string lpLibFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hLibModule);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetDllDirectory(string? lpPathName);

    private WhisperFactory? _factory;
    private System.Threading.Timer? _unloadTimer;
    private string? _loadedModelName;
    private bool _disposed;

    public event Action<int>? DownloadProgress;
    public event Action<string>? StatusChanged;

    public bool IsModelLoaded => _factory is not null;

    /// <summary>
    /// Ensures the specified model file exists locally. Downloads from HuggingFace if missing.
    /// </summary>
    public async Task EnsureModelAsync(string modelName, CancellationToken ct)
    {
        var modelsDir = AppSettings.GetModelsDir();
        Directory.CreateDirectory(modelsDir);

        var fileName = AppSettings.ResolveModelFileName(modelName);
        var modelPath = Path.Combine(modelsDir, fileName);
        if (File.Exists(modelPath))
        {
            StatusChanged?.Invoke($"Model '{modelName}' found locally.");
            return;
        }

        var url = $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{fileName}";
        var tmpPath = modelPath + ".tmp";

        StatusChanged?.Invoke($"Downloading model '{modelName}'...");
        DownloadProgress?.Invoke(0);

        try
        {
            using var httpClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = true
            });
            httpClient.Timeout = TimeSpan.FromMinutes(30);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WhisperPtt/1.0");

            using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            long receivedBytes = 0;
            int lastReportedPercent = -1;

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: 81920, useAsync: true);

            var buffer = new byte[81920];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                receivedBytes += bytesRead;

                if (totalBytes > 0)
                {
                    int percent = (int)(receivedBytes * 100 / totalBytes);
                    if (percent != lastReportedPercent)
                    {
                        lastReportedPercent = percent;
                        DownloadProgress?.Invoke(percent);
                    }
                }
            }

            // Flush and close before rename
            await fileStream.FlushAsync(ct);
        }
        catch
        {
            // Clean up partial download
            try { if (File.Exists(tmpPath)) File.Delete(tmpPath); }
            catch { /* ignore cleanup errors */ }
            throw;
        }

        // Rename .tmp to .bin on successful completion
        File.Move(tmpPath, modelPath, overwrite: true);
        DownloadProgress?.Invoke(100);
        StatusChanged?.Invoke($"Model '{modelName}' downloaded successfully.");
    }

    /// <summary>
    /// Loads the Whisper model. Attempts CUDA first, falls back to CPU on failure.
    /// </summary>
    public async Task LoadModelAsync(string modelName)
    {
        if (_loadedModelName == modelName && _factory is not null)
            return;

        UnloadModel();

        var fileName = AppSettings.ResolveModelFileName(modelName);
        var modelPath = Path.Combine(AppSettings.GetModelsDir(), fileName);
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Model file not found: {modelPath}");
        }

        await Task.Run(() =>
        {
            bool cudaLoaded = false;
            var chosenRuntime = Whisper.net.LibraryLoader.RuntimeLibrary.Cpu;
            var cuda12Dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"runtimes\cuda12\win-x64");
            var cudaDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"runtimes\cuda\win-x64");
            
            StatusChanged?.Invoke("Checking CUDA library compatibility...");

            // Reset diagnostics log
            var diagPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cuda_diag.txt");
            try { File.WriteAllText(diagPath, "--- WhisperPtt CUDA Diagnostic Log ---\n"); } catch { }

            // 1. Try loading from runtimes\cuda\win-x64 folder first (since our MSBuild target copies CUDA 12 binaries there to support Whisper.net 1.9.0's loader)
            var dllPathCuda = Path.Combine(cudaDir, "whisper.dll");
            if (File.Exists(dllPathCuda))
            {
                // Set DLL search directory to resolve all dependencies (e.g. cublas, cudart, nvrtc, ggml-cuda-whisper) from the cuda folder
                SetDllDirectory(cudaDir);
                
                IntPtr hModule = LoadLibrary(dllPathCuda);
                if (hModule != IntPtr.Zero)
                {
                    FreeLibrary(hModule);
                    cudaLoaded = true;
                    chosenRuntime = Whisper.net.LibraryLoader.RuntimeLibrary.Cuda;
                    try { File.AppendAllText(diagPath, $"CUDA whisper.dll loaded successfully using SetDllDirectory({cudaDir})\n"); } catch { }
                }
                else
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    try { File.AppendAllText(diagPath, $"CUDA LoadLibrary Failed. Path: {dllPathCuda}\nWin32 Error Code: {errorCode}\n(Note: Error 126 means a dependent DLL is missing or incompatible)\n"); } catch { }
                    SetDllDirectory(null); // Reset
                }
            }
            else
            {
                try { File.AppendAllText(diagPath, $"CUDA whisper.dll not found at: {dllPathCuda}\n"); } catch { }
            }

            // 2. Try runtimes\cuda12\win-x64 folder as fallback
            if (!cudaLoaded)
            {
                var dllPath12 = Path.Combine(cuda12Dir, "whisper.dll");
                if (File.Exists(dllPath12))
                {
                    SetDllDirectory(cuda12Dir);
                    IntPtr hModule = LoadLibrary(dllPath12);
                    if (hModule != IntPtr.Zero)
                    {
                        FreeLibrary(hModule);
                        cudaLoaded = true;
                        chosenRuntime = Whisper.net.LibraryLoader.RuntimeLibrary.Cuda; // Maps to Cuda runtime
                        try { File.AppendAllText(diagPath, $"CUDA 12 whisper.dll loaded successfully using SetDllDirectory({cuda12Dir})\n"); } catch { }
                    }
                    else
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        try { File.AppendAllText(diagPath, $"CUDA 12 LoadLibrary Failed. Path: {dllPath12}\nWin32 Error Code: {errorCode}\n"); } catch { }
                        SetDllDirectory(null); // Reset
                    }
                }
                else
                {
                    try { File.AppendAllText(diagPath, $"CUDA 12 whisper.dll not found at: {dllPath12}\n"); } catch { }
                }
            }

            // If we failed to load CUDA, let's write out detailed dependency diagnostics
            if (!cudaLoaded)
            {
                try
                {
                    var filesToCheck = new string[] 
                    { 
                        "cudart64_12.dll", 
                        "cublas64_12.dll", 
                        "cublasLt64_12.dll", 
                        "nvrtc64_120_0.dll", 
                        "ggml-cuda-whisper.dll" 
                    };
                    
                    var sb = new StringBuilder();
                    sb.AppendLine("\n--- Detailed DLL Probe (Root Folder) ---");
                    foreach (var file in filesToCheck)
                    {
                        var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
                        var exists = File.Exists(fullPath);
                        sb.AppendLine($"File: {file} | Exists: {exists}");
                        if (exists)
                        {
                            IntPtr h = LoadLibrary(fullPath);
                            if (h != IntPtr.Zero)
                            {
                                sb.AppendLine($"  -> LoadLibrary: SUCCESS");
                                FreeLibrary(h);
                            }
                            else
                            {
                                sb.AppendLine($"  -> LoadLibrary: FAILED. Win32 Error: {Marshal.GetLastWin32Error()}");
                            }
                        }
                    }

                    sb.AppendLine("\n--- Detailed DLL Probe (runtimes/cuda/win-x64) ---");
                    foreach (var file in filesToCheck)
                    {
                        var fullPath = Path.Combine(cudaDir, file);
                        var exists = File.Exists(fullPath);
                        sb.AppendLine($"File: {file} | Exists: {exists}");
                        if (exists)
                        {
                            IntPtr h = LoadLibrary(fullPath);
                            if (h != IntPtr.Zero)
                            {
                                sb.AppendLine($"  -> LoadLibrary: SUCCESS");
                                FreeLibrary(h);
                            }
                            else
                            {
                                sb.AppendLine($"  -> LoadLibrary: FAILED. Win32 Error: {Marshal.GetLastWin32Error()}");
                            }
                        }
                    }

                    File.AppendAllText(diagPath, sb.ToString());
                }
                catch { }
            }

            if (cudaLoaded)
            {
                try
                {
                    StatusChanged?.Invoke("Loading model with CUDA...");
                    
                    Whisper.net.LibraryLoader.RuntimeOptions.RuntimeLibraryOrder = new System.Collections.Generic.List<Whisper.net.LibraryLoader.RuntimeLibrary> 
                    { 
                        chosenRuntime 
                    };

                    _factory = WhisperFactory.FromPath(modelPath);
                    _loadedModelName = modelName;
                    StatusChanged?.Invoke($"Model '{modelName}' loaded (CUDA).");
                    return;
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.AppendAllText(diagPath, $"\n\nWhisperFactory.FromPath CUDA Exception:\n{ex.ToString()}");
                    }
                    catch { }
                    SetDllDirectory(null); // Reset DLL search path
                }
            }

            // Fallback to CPU
            try
            {
                StatusChanged?.Invoke("CUDA unavailable, loading model on CPU...");
                
                Whisper.net.LibraryLoader.RuntimeOptions.RuntimeLibraryOrder = new System.Collections.Generic.List<Whisper.net.LibraryLoader.RuntimeLibrary> 
                { 
                    Whisper.net.LibraryLoader.RuntimeLibrary.Cpu 
                };

                _factory = WhisperFactory.FromPath(modelPath);
                _loadedModelName = modelName;
                StatusChanged?.Invoke($"Model '{modelName}' loaded (CPU).");
            }
            catch (Exception cpuEx)
            {
                StatusChanged?.Invoke($"Failed to load model: {cpuEx.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// Transcribes audio data using the loaded Whisper model.
    /// Thread-safe: utilizes a localized WhisperProcessor instance to prevent concurrent disposal conflicts.
    /// </summary>
    public async Task<string> TranscribeAsync(float[] audioData, string language, string prompt, CancellationToken ct = default)
    {
        if (_factory is null)
            throw new InvalidOperationException("Model is not loaded. Call LoadModelAsync first.");

        // Create and dispose the processor locally inside a using block
        using var processor = _factory.CreateBuilder()
            .WithLanguage(language)
            .WithPrompt(prompt)
            .WithThreads(Math.Min(4, Math.Max(1, Environment.ProcessorCount / 2)))
            .Build();

        var sb = new StringBuilder();

        await foreach (var segment in processor.ProcessAsync(audioData, ct))
        {
            var text = segment.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            // Filter out common Whisper artifacts
            if (IsArtifact(text))
                continue;

            sb.Append(text);
            sb.Append(' ');
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Resets or starts the unload timer. When it fires, the model is unloaded to free memory.
    /// </summary>
    public void ResetUnloadTimer(int timeoutMinutes)
    {
        _unloadTimer?.Dispose();
        _unloadTimer = null;

        if (timeoutMinutes <= 0)
            return;

        var timeout = TimeSpan.FromMinutes(timeoutMinutes);
        _unloadTimer = new System.Threading.Timer(_ =>
        {
            UnloadModel();
        }, null, timeout, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Unloads the model and frees associated resources.
    /// </summary>
    public void UnloadModel()
    {
        _factory?.Dispose();
        _factory = null;

        _loadedModelName = null;
        StatusChanged?.Invoke("Model unloaded.");
    }

    private static bool IsArtifact(string text)
    {
        // Common Whisper hallucination artifacts
        var artifacts = new[]
        {
            "[BLANK_AUDIO]",
            "[blank_audio]",
            "(blank audio)",
            "[MUSIC]",
            "[music]",
            "(music)",
            "[SILENCE]",
            "[silence]",
            "[ Silence ]",
            "[Музыка]",
            "[музыка]",
            "(Музыка)"
        };

        foreach (var artifact in artifacts)
        {
            if (text.Equals(artifact, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _unloadTimer?.Dispose();
            _unloadTimer = null;

            _factory?.Dispose();
            _factory = null;

            _disposed = true;
        }
    }
}
