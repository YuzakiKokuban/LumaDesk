namespace JiYaoChu.Services;

/// <summary>Serializes brightness writes across page instances; newer requests supersede queued ones.</summary>
internal sealed class BrightnessWriter(Func<uint, Task> write)
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
            return true;
        }
        finally { _gate.Release(); }
    }
}
