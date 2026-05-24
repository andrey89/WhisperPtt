using System.IO;
using NAudio.Wave;

namespace WhisperPtt.Services;

public class AudioService : IDisposable
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _memoryStream;
    private bool _isRecording;
    private bool _disposed;

    public event Action<float>? RmsCalculated;

    public static List<string> GetInputDevices()
    {
        var list = new List<string> { "Системный по умолчанию" };
        int deviceCount = WaveIn.DeviceCount;
        for (int i = 0; i < deviceCount; i++)
        {
            try
            {
                var caps = WaveIn.GetCapabilities(i);
                list.Add(caps.ProductName);
            }
            catch
            {
                // ignore
            }
        }
        return list;
    }

    public static int FindDeviceIndex(string deviceName)
    {
        if (string.IsNullOrEmpty(deviceName) || deviceName == "Системный по умолчанию")
        {
            return -1;
        }

        int deviceCount = WaveIn.DeviceCount;
        for (int i = 0; i < deviceCount; i++)
        {
            try
            {
                var caps = WaveIn.GetCapabilities(i);
                if (caps.ProductName.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            catch
            {
                // ignore
            }
        }
        return -1;
    }

    /// <summary>
    /// Starts recording audio at 16 kHz, 16-bit, mono. Accumulates PCM data internally.
    /// </summary>
    public void StartRecording(string deviceName = "Системный по умолчанию")
    {
        if (_isRecording)
            return;

        _memoryStream = new MemoryStream();

        int deviceIndex = FindDeviceIndex(deviceName);

        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceIndex >= 0 ? deviceIndex : 0,
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 50
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;

        _waveIn.StartRecording();
        _isRecording = true;
    }

    /// <summary>
    /// Stops recording and returns normalized float[] PCM data suitable for Whisper processing.
    /// </summary>
    public float[] StopRecording()
    {
        if (!_isRecording || _waveIn is null || _memoryStream is null)
            return Array.Empty<float>();

        _isRecording = false;
        _waveIn.StopRecording();

        var pcmBytes = _memoryStream.ToArray();
        _memoryStream.Dispose();
        _memoryStream = null;

        _waveIn.DataAvailable -= OnDataAvailable;
        _waveIn.RecordingStopped -= OnRecordingStopped;
        _waveIn.Dispose();
        _waveIn = null;

        return ConvertToFloat(pcmBytes);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        // Accumulate raw PCM bytes
        _memoryStream?.Write(e.Buffer, 0, e.BytesRecorded);

        // Calculate RMS from this chunk
        float rms = CalculateRms(e.Buffer, e.BytesRecorded);
        RmsCalculated?.Invoke(rms);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            System.Diagnostics.Debug.WriteLine($"Recording stopped with error: {e.Exception.Message}");
        }
    }

    /// <summary>
    /// Calculates RMS level from 16-bit PCM data, normalized to 0.0–1.0 range.
    /// </summary>
    private static float CalculateRms(byte[] buffer, int bytesRecorded)
    {
        int sampleCount = bytesRecorded / 2; // 16-bit = 2 bytes per sample
        if (sampleCount == 0)
            return 0f;

        double sumSquares = 0;
        for (int i = 0; i < bytesRecorded - 1; i += 2)
        {
            short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
            double normalized = sample / 32768.0;
            sumSquares += normalized * normalized;
        }

        float rms = (float)Math.Sqrt(sumSquares / sampleCount);
        return Math.Clamp(rms, 0f, 1f);
    }

    /// <summary>
    /// Converts 16-bit PCM byte array to normalized float array (range -1.0 to 1.0).
    /// </summary>
    private static float[] ConvertToFloat(byte[] pcmBytes)
    {
        int sampleCount = pcmBytes.Length / 2;
        var floats = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = (short)(pcmBytes[i * 2] | (pcmBytes[i * 2 + 1] << 8));
            floats[i] = sample / 32768f;
        }

        return floats;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_isRecording)
            {
                try { StopRecording(); }
                catch { /* Suppress during dispose */ }
            }

            _waveIn?.Dispose();
            _memoryStream?.Dispose();
            _disposed = true;
        }
    }
}
