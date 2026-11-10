using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Devinno.UI.Video;

// SDL2 오디오 장치 하나에 48000 Hz·스테레오·float32 소리를 밀어 넣는다(SDL_QueueAudio, 콜백 없음).
// SDL 은 오디오 서브시스템만 쓴다 — 창·렌더러는 만들지 않는다.
//
// 네이티브 라이브러리(SDL2 2.x, 이 패키지에 담지 않음):
//  - Windows: SDL2.dll(공식 SDL2-2.x-win32-x64.zip 의 것)을 앱 폴더나 VideoPlayer.FFmpegDirectory 폴더에 둔다. 없으면 PATH 검색.
//  - Linux(라즈베리파이 포함): OS 패키지 `sudo apt install libsdl2-2.0-0` → libSDL2-2.0.so.0.
//    버전 없는 libSDL2.so 는 개발 패키지(libsdl2-dev)에만 있으므로 버전 붙은 이름을 먼저 찾는다.
//    출력 백엔드(PipeWire·PulseAudio·ALSA)는 SDL 이 고른다. 바꾸려면 환경 변수 SDL_AUDIODRIVER=alsa 등.
//  - 이름 후보를 차례로 NativeLibrary.TryLoad 하고 함수는 GetExport 함수 포인터로 부른다 — OS 별 코드 분기가 없고,
//    DllImport 해석기(SetDllImportResolver)를 쓰지 않으므로 앱의 다른 SDL 바인딩과 부딪히지 않는다.
//  - 함수 시그니처는 SDL2-CS(flibitijibibo, zlib)와 같다.
//  - SDL_Init·SDL_QuitSubSystem 은 스레드 안전하지 않다(여러 재생기가 동시에 열면 SDL_Init 안에서 접근 위반으로 죽는 것을 재현함).
//    그래서 오디오 서브시스템은 프로세스에서 한 번만 켜고 끄지 않으며, 장치 열기·닫기는 정적 잠금 하나로 줄 세운다.
//
// 모든 멤버는 아무 스레드에서나 불러도 된다(SDL_QueueAudio·SDL_GetQueuedAudioSize 는 장치 잠금 안에서 동작).
internal sealed unsafe class AudioOutput : IDisposable
{
    public const int SampleRate = AudioDecoder.SampleRate;
    public const int Channels = AudioDecoder.Channels;
    private const int BytesPerSecond = SampleRate * Channels * sizeof(float);
    private const ushort DeviceSamples = 1024; // 장치 버퍼(약 21 ms) — 장치가 큐에서 이만큼씩 가져간다

    private readonly object _gate = new();
    private uint _device;
    private double _endPts = double.NaN; // 마지막으로 넣은 샘플의 끝 시각. Clear 뒤 = 모름
    private float _volume = 1;
    private float[] _scratch = [];
    // 큐에 넣은 소리(게인 곱하기 전) 최근 1초 — 볼륨이 바뀌면 아직 안 나간 부분을 새 게인으로 다시 넣는다
    private readonly float[] _ring = new float[SampleRate * Channels];
    private int _ringEnd, _ringCount;
    private bool _disposed;

    // 장치를 못 연 이유(SDL2 없음·소리 장치 없음). 열렸으면 null
    public string? Error { get; private set; }

    // 장치가 큐에서 한 번에 가져가는 길이(초) — 시계 보정용
    public double ChunkSeconds { get; private set; } = DeviceSamples / (double)SampleRate;

    // 0~1. 큐에 넣기 전에 곱한다. 바꾸면 큐에 남은(아직 안 나간) 소리도 새 볼륨으로 바꿔 넣는다 —
    // 반영은 장치가 이미 가져간 한 덩어리(ChunkSeconds) 뒤, 큐 잔량(최대 0.5초)만큼 늦지 않다
    public double Volume
    {
        get => _volume;
        set
        {
            var v = (float)Math.Clamp(double.IsNaN(value) ? 1 : value, 0, 1);
            lock (_gate)
            {
                if (v == _volume)
                    return;
                _volume = v;
                Requeue();
            }
        }
    }

    // 큐에 남은 소리(초)
    public double QueuedSeconds
    {
        get
        {
            lock (_gate)
                return _device == 0 ? 0 : Sdl.GetQueuedAudioSize(_device) / (double)BytesPerSecond;
        }
    }

    // 지금 장치로 나가는 위치(초) = 마지막으로 넣은 샘플의 끝 − 큐 잔량. 일시정지 중에는 큐가 줄지 않으므로 멈춰 있다.
    // Clear 뒤 첫 Queue 전에는 NaN
    public double CurrentTime
    {
        get
        {
            lock (_gate)
                return _device == 0 || double.IsNaN(_endPts) ? double.NaN : _endPts - Sdl.GetQueuedAudioSize(_device) / (double)BytesPerSecond;
        }
    }

    // 장치를 연다(일시정지 상태로). 실패하면 false, 이유는 Error
    public bool Open()
    {
        lock (_gate)
        {
            if (_device != 0)
                return true;
            var want = new Sdl.AudioSpec { Freq = SampleRate, Format = Sdl.AudioF32, Channels = Channels, Samples = DeviceSamples };
            Sdl.AudioSpec have;
            lock (Sdl.Gate)
            {
                if (Sdl.EnsureAudio() is { } initError)
                {
                    Error = initError;
                    return false;
                }
                _device = Sdl.OpenAudioDevice(null, 0, &want, &have, 0); // 0 = 바꾸지 않음 — 장치 형식이 다르면 SDL 이 변환한다
                if (_device == 0)
                {
                    Error = "cannot open a sound device: " + Sdl.LastError();
                    return false;
                }
            }
            if (have.Samples > 0)
                ChunkSeconds = have.Samples / (double)SampleRate;
            Error = null;
            return true;
        }
    }

    // samples(스테레오 interleaved) 를 큐 끝에 넣는다. startPts = 첫 샘플의 시각(초)
    public void Queue(ReadOnlySpan<float> samples, double startPts)
    {
        if (samples.IsEmpty)
            return;
        lock (_gate)
        {
            if (_device == 0 || !Push(samples, _volume))
                return;
            Remember(samples);
            _endPts = startPts + samples.Length / Channels / (double)SampleRate;
        }
    }

    // _gate 안에서. 게인을 곱해 SDL 큐에 넣는다
    private bool Push(ReadOnlySpan<float> samples, float gain)
    {
        var data = samples;
        if (gain != 1)
        {
            if (_scratch.Length < samples.Length)
                _scratch = new float[samples.Length];
            for (var i = 0; i < samples.Length; i++)
                _scratch[i] = samples[i] * gain;
            data = _scratch.AsSpan(0, samples.Length);
        }
        fixed (float* p = data)
            return Sdl.QueueAudio(_device, p, (uint)(data.Length * sizeof(float))) == 0;
    }

    private void Remember(ReadOnlySpan<float> samples)
    {
        if (samples.Length >= _ring.Length)
            samples = samples[^_ring.Length..];
        var first = Math.Min(samples.Length, _ring.Length - _ringEnd);
        samples[..first].CopyTo(_ring.AsSpan(_ringEnd));
        samples[first..].CopyTo(_ring);
        _ringEnd = (_ringEnd + samples.Length) % _ring.Length;
        _ringCount = Math.Min(_ring.Length, _ringCount + samples.Length);
    }

    // _gate 안에서. 큐에 남은 소리를 지금 볼륨으로 다시 넣는다(끝 시각·잔량은 그대로)
    private void Requeue()
    {
        if (_device == 0 || _ringCount == 0)
            return;
        var queued = (int)(Sdl.GetQueuedAudioSize(_device) / sizeof(float)) & ~1; // 스테레오 짝
        var n = Math.Min(queued, _ringCount);
        if (n == 0)
            return;
        Sdl.ClearQueuedAudio(_device);
        var start = (_ringEnd - n + _ring.Length) % _ring.Length;
        var first = Math.Min(n, _ring.Length - start);
        Push(_ring.AsSpan(start, first), _volume);
        if (n > first)
            Push(_ring.AsSpan(0, n - first), _volume);
    }

    public void Pause(bool pause)
    {
        lock (_gate)
            if (_device != 0)
                Sdl.PauseAudioDevice(_device, pause ? 1 : 0);
    }

    // 큐에 남은 소리를 버린다(seek). 장치가 이미 가져간 한 덩어리(ChunkSeconds 이하)는 끝까지 나간다
    public void Clear()
    {
        lock (_gate)
        {
            if (_device != 0)
                Sdl.ClearQueuedAudio(_device);
            _endPts = double.NaN;
            _ringCount = 0;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_device != 0)
            {
                lock (Sdl.Gate)
                    Sdl.CloseAudioDevice(_device);
                _device = 0;
            }
        }
    }

    // SDL2 함수 8개(SDL2-CS 와 같은 시그니처)
    private static class Sdl
    {
        public const uint InitAudio = 0x00000010;  // SDL_INIT_AUDIO
        public const ushort AudioF32 = 0x8120;     // AUDIO_F32LSB = AUDIO_F32(리틀 엔디언 — x64·arm64)

        [StructLayout(LayoutKind.Sequential)]
        public struct AudioSpec
        {
            public int Freq;
            public ushort Format;
            public byte Channels;
            public byte Silence;
            public ushort Samples;
            public ushort Padding;
            public uint Size;
            public IntPtr Callback; // null = 콜백 없음(SDL_QueueAudio 방식)
            public IntPtr Userdata;
        }

        public static readonly object Gate = new(); // 불러오기·오디오 켜기·장치 열기/닫기를 줄 세운다
        private static bool _tried;
        private static string? _error;
        private static bool _audioOn;

        private static delegate* unmanaged[Cdecl]<uint, int> _init;
        private static delegate* unmanaged[Cdecl]<byte*, int, AudioSpec*, AudioSpec*, int, uint> _openAudioDevice;
        private static delegate* unmanaged[Cdecl]<uint, void*, uint, int> _queueAudio;
        private static delegate* unmanaged[Cdecl]<uint, uint> _getQueuedAudioSize;
        private static delegate* unmanaged[Cdecl]<uint, void> _clearQueuedAudio;
        private static delegate* unmanaged[Cdecl]<uint, int, void> _pauseAudioDevice;
        private static delegate* unmanaged[Cdecl]<uint, void> _closeAudioDevice;
        private static delegate* unmanaged[Cdecl]<byte*> _getError;

        // Gate 안에서. 라이브러리를 불러오고 오디오 서브시스템을 한 번 켠다(끄지 않음). 실패 이유를 돌려준다
        public static string? EnsureAudio()
        {
            if (Load() is { } loadError)
                return loadError;
            if (_audioOn)
                return null;
            if (_init(InitAudio) != 0)
                return "SDL audio init failed: " + LastError(); // 다음 Open 에서 다시 시도
            _audioOn = true;
            return null;
        }

        public static uint OpenAudioDevice(byte* device, int capture, AudioSpec* want, AudioSpec* have, int allowedChanges) => _openAudioDevice(device, capture, want, have, allowedChanges);
        public static int QueueAudio(uint dev, void* data, uint len) => _queueAudio(dev, data, len);
        public static uint GetQueuedAudioSize(uint dev) => _getQueuedAudioSize(dev);
        public static void ClearQueuedAudio(uint dev) => _clearQueuedAudio(dev);
        public static void PauseAudioDevice(uint dev, int pause) => _pauseAudioDevice(dev, pause);
        public static void CloseAudioDevice(uint dev) => _closeAudioDevice(dev);
        public static string LastError() => Marshal.PtrToStringUTF8((IntPtr)_getError()) ?? "";

        // 한 번만 불러 본다. 실패 이유를 돌려준다(성공 = null)
        public static string? Load()
        {
            lock (Gate)
            {
                if (_tried)
                    return _error;
                _tried = true;
                var handle = IntPtr.Zero;
                foreach (var name in Candidates())
                {
                    if (NativeLibrary.TryLoad(name, out handle))
                        break;
                }
                if (handle == IntPtr.Zero)
                {
                    _error = "SDL2 not found — sound is off. Windows: put SDL2.dll next to the app; Linux: sudo apt install libsdl2-2.0-0";
                    Trace.TraceWarning($"Devinno.UI: {_error}");
                    return _error;
                }

                try
                {
                    _init = (delegate* unmanaged[Cdecl]<uint, int>)NativeLibrary.GetExport(handle, "SDL_Init");
                    _openAudioDevice = (delegate* unmanaged[Cdecl]<byte*, int, AudioSpec*, AudioSpec*, int, uint>)NativeLibrary.GetExport(handle, "SDL_OpenAudioDevice");
                    _queueAudio = (delegate* unmanaged[Cdecl]<uint, void*, uint, int>)NativeLibrary.GetExport(handle, "SDL_QueueAudio");
                    _getQueuedAudioSize = (delegate* unmanaged[Cdecl]<uint, uint>)NativeLibrary.GetExport(handle, "SDL_GetQueuedAudioSize");
                    _clearQueuedAudio = (delegate* unmanaged[Cdecl]<uint, void>)NativeLibrary.GetExport(handle, "SDL_ClearQueuedAudio");
                    _pauseAudioDevice = (delegate* unmanaged[Cdecl]<uint, int, void>)NativeLibrary.GetExport(handle, "SDL_PauseAudioDevice");
                    _closeAudioDevice = (delegate* unmanaged[Cdecl]<uint, void>)NativeLibrary.GetExport(handle, "SDL_CloseAudioDevice");
                    _getError = (delegate* unmanaged[Cdecl]<byte*>)NativeLibrary.GetExport(handle, "SDL_GetError");
                }
                catch (EntryPointNotFoundException ex)
                {
                    _error = $"SDL2 library is too old or not SDL2 ({ex.Message}) — sound is off";
                    Trace.TraceWarning($"Devinno.UI: {_error}");
                }
                return _error;
            }
        }

        // 앱 폴더 → FFmpeg 폴더(설정했으면) → OS 기본 검색. Windows 이름과 Linux 이름을 함께 두고 열리는 것을 쓴다
        private static IEnumerable<string> Candidates()
        {
            string[] names = ["SDL2.dll", "libSDL2-2.0.so.0", "libSDL2.so"];
            foreach (var dir in new[] { AppContext.BaseDirectory, VideoPlayer.FFmpegDirectory })
            {
                if (string.IsNullOrEmpty(dir))
                    continue;
                foreach (var n in names)
                {
                    var path = System.IO.Path.Combine(dir, n);
                    if (File.Exists(path))
                        yield return path;
                }
            }
            foreach (var n in names)
                yield return n;
        }

        internal static void ResetForTests()
        {
            lock (Gate)
            {
                _tried = false;
                _error = null;
            }
        }
    }
}
