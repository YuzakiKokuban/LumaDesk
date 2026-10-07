using JiYaoChu.Model;

namespace JiYaoChu.Services;

/// <summary>Deterministic checks; no hardware writes, clocks, sleeps or window needed.</summary>
internal static class DisplayTrendVerification
{
    public static async Task RunAsync(Dictionary<string, object> report)
    {
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        var at = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var history = new TrendHistory();
        var status = new HardwareStatus { Cpu = new() { Temp = 48, Load = 20 }, Gpu = new() { Present = true, Temp = null, Load = 0 }, Fans = new() { Available = false, CpuRpm = 999 } };
        history.Record(at, status, active: false, visible: true);
        history.Record(at, status, active: true, visible: false);
        Require(history.Snapshot().Count == 0, "Hidden/inactive snapshots accumulated in trends");
        history.Record(at, status, true, true);
        var first = history.Snapshot().Single();
        Require(first.GpuTemp is null && first.CpuRpm is null && first.GpuLoad == 0, "Missing sensors were converted to zero or genuine zero was discarded");
        history.MarkGap();
        history.Record(at.AddSeconds(1), status, true, true);
        Require(history.Snapshot().Last().BreakBefore, "Resume bridged a hidden interval");
        history.Record(at.AddSeconds(10), status, true, true);
        Require(history.Snapshot().Last().BreakBefore, "Missed polls were joined by a continuous line");
        for (var i = 11; i < 1000; i++) history.Record(at.AddSeconds(i), status, true, true);
        var retained = history.Snapshot();
        Require(retained.Count <= TrendHistory.Capacity && retained[0].At >= at.AddSeconds(699), "Trend retention exceeded five minutes");
        history.Clear();
        Require(history.Snapshot().Count == 0, "Trend clear left points behind");
        for (var i = 0; i < 2000; i++) history.Record(at.AddMilliseconds(i), status, true, true);
        Require(history.Snapshot().Count == TrendHistory.Capacity, "Rapid refreshes exceeded the bounded ring capacity");
        var malformed = status with { Cpu = new() { Temp = double.NaN, Load = double.PositiveInfinity }, Gpu = new() { Present = false, Temp = 60, Load = 80 } };
        history.Record(at.AddMinutes(1), malformed, true, true);
        var last = history.Snapshot().Last();
        Require(last.CpuTemp is null && last.CpuLoad is null && last.GpuTemp is null && last.GpuLoad is null, "Invalid or absent GPU sensor values entered the graph");
        var monitor = new DisplayInfo { DeviceName = "panel", CurrentHz = 120, AvailableHz = [120, 0, 60, 60] };
        Require(DisplayPolicy.Rates(monitor).SequenceEqual(new uint[] { 60, 120 }), "Refresh-rate choices are not valid distinct sorted modes");
        Require(DisplayPolicy.Find([monitor], "unplugged") is null && !DisplayPolicy.CanApply([monitor], "unplugged", 60), "Disconnected monitor remains writable");
        Require(!DisplayPolicy.CanApply([monitor], "panel", 144) && DisplayPolicy.CanApply([monitor], "panel", 60), "Unsupported refresh-rate write allowed");
        Require(DisplayPolicy.Brightness(double.NaN) is null && DisplayPolicy.Brightness(101) == 100 && DisplayPolicy.Brightness(-1) == 0, "Brightness input validation failed");
        report["display_policy_checks"] = true;
        report["trend_bounded_visible_history_checks"] = true;
        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var writes = new List<uint>();
        var writer = new BrightnessWriter(async level =>
        {
            writes.Add(level);
            if (level == 20) { entered.SetResult(); await release.Task; }
        });
        var firstWrite = writer.ApplyAsync(20, writer.NextRequest());
        await entered.Task;
        var obsoletePageWrite = writer.ApplyAsync(83, writer.NextRequest());
        var reopenedPageWrite = writer.ApplyAsync(60, writer.NextRequest());
        release.SetResult();
        await Task.WhenAll(firstWrite, obsoletePageWrite, reopenedPageWrite);
        Require(writes.SequenceEqual(new uint[] { 20, 60 }) && !obsoletePageWrite.Result && reopenedPageWrite.Result, "An old page brightness request overtook a newer one or writes overlapped");
        var attempts = new List<uint>();
        var failingWriter = new BrightnessWriter(level =>
        {
            attempts.Add(level);
            return level == 20 ? Task.FromException(new IOException("write failed")) : Task.CompletedTask;
        });
        try { await failingWriter.ApplyAsync(20, failingWriter.NextRequest()); throw new InvalidOperationException("Failed brightness write succeeded"); }
        catch (IOException) { }
        Require(await failingWriter.ApplyAsync(83, failingWriter.NextRequest()) && attempts.SequenceEqual(new uint[] { 20, 83 }), "A failed brightness write blocked the next request");
        report["brightness_serialization_latest_request_and_failure_checks"] = true;
        var delayedReads = 0;
        var delayedWriter = new BrightnessWriter(_ => Task.CompletedTask,
            () => Task.FromResult(++delayedReads < 3 ? 50u : 51u));
        Require(await delayedWriter.ApplyAsync(51, delayedWriter.NextRequest()) && delayedReads == 3, "Delayed brightness readback was treated as an immediate mismatch");
        var settlingEntered = new TaskCompletionSource();
        var settlingRelease = new TaskCompletionSource();
        uint visibleLevel = 50;
        var settlingWriter = new BrightnessWriter(level => { visibleLevel = level; return Task.CompletedTask; }, async () =>
        {
            if (visibleLevel == 20) { settlingEntered.SetResult(); await settlingRelease.Task; }
            return visibleLevel;
        });
        var settlingOld = settlingWriter.ApplyAsync(20, settlingWriter.NextRequest());
        await settlingEntered.Task;
        var settlingNew = settlingWriter.ApplyAsync(83, settlingWriter.NextRequest());
        settlingRelease.SetResult();
        await Task.WhenAll(settlingOld, settlingNew);
        Require(!settlingOld.Result && settlingNew.Result && visibleLevel == 83, "A stale settling check blocked or overrode a newer brightness request");
        report["brightness_delayed_readback_and_superseded_settling_checks"] = true;
    }
}
