namespace JiYaoChu.Services;

/// <summary>Deterministic readback checks without hardware access or window interaction.</summary>
internal static class SettingsResourceVerification
{
    private sealed record State(int First = 0, int Second = 0);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static async Task RunAsync(Dictionary<string, object> report)
    {
        var entered = Signal();
        var release = Signal();
        var trailingEntered = Signal();
        var trailingRelease = Signal();
        var thirdEntered = Signal();
        var thirdRelease = Signal();
        var reads = 0;
        var source = 1;
        using (var reader = new SettingsResource<State>(async () =>
        {
            var call = ++reads;
            var captured = source;
            if (call == 1) { entered.SetResult(); await release.Task; }
            if (call == 2) { trailingEntered.SetResult(); await trailingRelease.Task; }
            if (call == 3) { thirdEntered.SetResult(); await thirdRelease.Task; }
            return new(captured);
        }))
        {
            var first = reader.RefreshAsync();
            await entered.Task;
            source = 2;
            var burst = Enumerable.Range(0, 8).Select(_ => reader.RefreshAsync()).ToArray();
            Require(burst.All(task => !task.IsCompleted), "Queued refresh callers completed before their readback");
            release.SetResult();
            await first;
            await trailingEntered.Task;
            Require(reads == 2 && reader.Snapshot.Value?.First == 1 && burst.All(task => !task.IsCompleted), "Late refresh was incorrectly satisfied by the old in-flight state");
            source = 3;
            var late = reader.RefreshAsync();
            trailingRelease.SetResult();
            await Task.WhenAll(burst);
            await thirdEntered.Task;
            Require(reads == 3 && reader.Snapshot.Value?.First == 2 && !late.IsCompleted, "A request during the trailing read was dropped or completed with old state");
            thirdRelease.SetResult();
            await late;
            Require(reads == 3 && reader.Snapshot.Value?.First == 3 && !reader.Snapshot.Refreshing, "Redundant full refreshes were not coalesced or the final late request was lost");
        }
        report["settings_resource_coalesced_trailing_read_checks"] = true;

        entered = Signal();
        release = Signal();
        var firstSectionReads = 0;
        var secondSectionReads = 0;
        using (var reader = new SettingsResource<State>(() => Task.FromResult(new State())))
        {
            await reader.RefreshAsync();
            var activeSection = reader.RefreshAsync("first", async previous =>
            {
                firstSectionReads++;
                entered.SetResult();
                await release.Task;
                return previous with { First = 1 };
            });
            await entered.Task;
            var obsolete = reader.RefreshAsync("first", previous =>
            {
                firstSectionReads++;
                return Task.FromResult(previous with { First = 2 });
            });
            var latest = reader.RefreshAsync("first", previous =>
            {
                firstSectionReads++;
                return Task.FromResult(previous with { First = 3 });
            });
            var distinct = reader.RefreshAsync("second", previous =>
            {
                secondSectionReads++;
                Require(previous.First == 3, "Distinct section did not receive the latest partial state");
                return Task.FromResult(previous with { Second = 4 });
            });
            release.SetResult();
            await Task.WhenAll(activeSection, obsolete, latest, distinct);
            Require(firstSectionReads == 2 && secondSectionReads == 1 && reader.Snapshot.Value == new State(3, 4), "Keyed refresh lost a section or failed to coalesce same-section requests");
        }
        report["settings_resource_distinct_partial_and_same_key_checks"] = true;

        entered = Signal();
        release = Signal();
        reads = 0;
        var supersededPartials = 0;
        using (var reader = new SettingsResource<State>(async () =>
        {
            if (++reads == 1) { entered.SetResult(); await release.Task; }
            return new(5, 6);
        }))
        {
            var initial = reader.RefreshAsync();
            await entered.Task;
            Task<State> Partial(State previous)
            {
                supersededPartials++;
                return Task.FromResult(previous with { First = 100 });
            }
            var beforeFull = reader.RefreshAsync("first", Partial);
            var full = reader.RefreshAsync();
            var afterFull = reader.RefreshAsync("second", Partial);
            release.SetResult();
            await Task.WhenAll(initial, beforeFull, full, afterFull);
            Require(reads == 2 && supersededPartials == 0 && reader.Snapshot.Value == new State(5, 6), "Full refresh did not safely supersede pending partial reads");
        }
        report["settings_resource_full_superset_checks"] = true;

        entered = Signal();
        release = Signal();
        using (var reader = new SettingsResource<State>(() => Task.FromResult(new State(7, 8))))
        {
            await reader.RefreshAsync();
            var blocker = reader.RefreshAsync("blocker", async previous =>
            {
                entered.SetResult();
                await release.Task;
                return previous;
            });
            await entered.Task;
            var failedSection = reader.RefreshAsync("first", _ => Task.FromException<State>(new IOException("partial readback failure")));
            var goodSection = reader.RefreshAsync("second", previous => Task.FromResult(previous with { Second = 9 }));
            release.SetResult();
            await Task.WhenAll(blocker, failedSection, goodSection);
            Require(reader.Snapshot.Value == new State(7, 9) && reader.Snapshot.Error is not null && !reader.Snapshot.Refreshing, "One partial failure lost last-good state or suppressed another section");
        }
        report["settings_resource_partial_failure_checks"] = true;

        var fail = false;
        using (var reader = new SettingsResource<State>(() => fail
            ? Task.FromException<State>(new IOException("deterministic readback failure"))
            : Task.FromResult(new State(7, 8))))
        {
            await reader.RefreshAsync();
            fail = true;
            await reader.RefreshAsync();
            Require(reader.Snapshot.Value == new State(7, 8) && reader.Snapshot.Error is not null && !reader.Snapshot.Refreshing, "Failed readback discarded the last good value or remained busy");
            fail = false;
            await reader.RefreshAsync();
            Require(reader.Snapshot.Error is null && reader.Snapshot.Value == new State(7, 8), "Successful recovery retained an obsolete error");
        }
        report["settings_resource_failure_retention_checks"] = true;

        entered = Signal();
        release = Signal();
        var notifications = 0;
        var disposedReads = 0;
        var disposedReader = new SettingsResource<State>(async () =>
        {
            disposedReads++;
            entered.SetResult();
            await release.Task;
            return new(9);
        });
        disposedReader.Subscribe(() => notifications++);
        var active = disposedReader.RefreshAsync();
        await entered.Task;
        var queued = disposedReader.RefreshAsync();
        disposedReader.Dispose();
        var notificationsAtDispose = notifications;
        release.SetResult();
        await Task.WhenAll(active, queued);
        await disposedReader.RefreshAsync();
        Require(disposedReads == 1 && notifications == notificationsAtDispose && disposedReader.Snapshot.Value is null, "Disposed resource read again, published a value or notified subscribers");
        report["settings_resource_disposal_checks"] = true;
    }
}
