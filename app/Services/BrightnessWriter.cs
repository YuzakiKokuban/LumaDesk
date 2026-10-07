namespace JiYaoChu.Services;

/// <summary>Serializes brightness writes across page instances; newer requests supersede queued ones.</summary>
internal sealed class BrightnessWriter(Func<uint, Task> write, Func<Task<uint>>? readback = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _version;

    public long NextRequest() => Interlocked.Increment(ref _version);

    public async Task<bool> ApplyAsync(uint level, long version)
    {
        await _gate.WaitAsync();
        try
        {
            if (version != Interlocked.Read(ref _version)) return false;
            await write(level);
            if (readback is not null)
            {
                var deadline = Environment.TickCount64 + 1200;
                while (true)
                {
                    if (version != Interlocked.Read(ref _version)) return false;
                    var actual = await readback();
                    if (version != Interlocked.Read(ref _version)) return false;
                    if (actual == level) break;
                    if (Environment.TickCount64 >= deadline) throw new InvalidOperationException($"请求亮度 {level}%，实际读回 {actual}%。");
                    await Task.Delay(50);
                }
            }
            return true;
        }
        finally { _gate.Release(); }
    }
}
