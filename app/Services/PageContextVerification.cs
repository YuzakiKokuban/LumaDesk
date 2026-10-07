using JiYaoChu.Model;

namespace JiYaoChu.Services;

/// <summary>Deterministic session checks; does not create a window or read/write hardware.</summary>
internal static class PageContextVerification
{
    public static Task RunAsync(Dictionary<string, object> report)
    {
        static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
        var panel = new DisplayInfo { DeviceName = "panel", CurrentHz = 120, AvailableHz = [60, 90, 120] };
        var external = new DisplayInfo { DeviceName = "external", CurrentHz = 60, AvailableHz = [60, 144] };
        var selection = new DisplayPageDraft("external", 144);
        Require(PageContext.ReconcileDisplay(selection, [panel, external]) == selection, "Valid unsubmitted display draft was discarded");
        Require(PageContext.ReconcileDisplay(selection, [panel, external with { CurrentHz = 144 }]).RefreshRate is null, "An applied rate remained a draft");
        Require(PageContext.ReconcileDisplay(selection, [panel, external with { AvailableHz = [60] }]).RefreshRate is null, "A stale unavailable rate remained selectable");
        var disconnected = PageContext.ReconcileDisplay(selection, [panel]);
        Require(disconnected.DeviceName == "external" && disconnected.RefreshRate is null, "Disconnected monitor draft silently targeted another device");
        Require(PageContext.ReconcileDisplay(new(), [panel, external]).DeviceName == "panel", "Initial display did not follow enumeration");
        Require(PageContext.ReconcileDisplay(new("panel", 90), [panel with { CurrentHz = 60 }]).RefreshRate == 90, "External readback discarded a valid unsaved refresh rate");

        var gpu = new GpuModeInfo { ConfiguredMode = GpuMode.Hybrid, Supported = true, SupportsIgpu = true };
        Require(PageContext.ReconcileGpu(GpuMode.Dgpu, gpu) == GpuMode.Dgpu, "GPU draft was discarded before applying");
        Require(PageContext.ReconcileGpu(GpuMode.Dgpu, gpu with { ConfiguredMode = GpuMode.Dgpu }) is null, "Applied GPU draft did not follow readback");
        Require(PageContext.ReconcileGpu(GpuMode.Igpu, gpu with { SupportsIgpu = false }) is null, "Unsupported GPU draft remained applicable");
        Require(PageContext.ReconcileGpu(GpuMode.Dgpu, gpu with { Supported = false }) is null, "Unsupported platform retained an applicable GPU draft");

        var savedDisplay = PageContext.DisplayDraft;
        var savedColor = PageContext.KeyboardColorInput;
        var savedGpu = PageContext.GpuDraft;
        try
        {
            PageContext.DisplayDraft = selection;
            PageContext.KeyboardColorInput = "  #12ABEF ";
            PageContext.GpuDraft = GpuMode.Igpu;
            Require(PageContext.DisplayDraft == selection && PageContext.KeyboardColorInput == "  #12ABEF " && PageContext.GpuDraft == GpuMode.Igpu, "Page remount cannot recover the exact session draft");
            PageContext.ClearDisplayRate("external", 60);
            PageContext.ClearGpuDraft(GpuMode.Dgpu);
            Require(PageContext.DisplayDraft == selection && PageContext.GpuDraft == GpuMode.Igpu, "Old-page completion discarded a newer draft");
            PageContext.ClearDisplayRate("EXTERNAL", 144);
            PageContext.ClearGpuDraft(GpuMode.Igpu);
            Require(PageContext.DisplayDraft.RefreshRate is null && PageContext.GpuDraft is null, "Successful matching completion retained the applied draft");
        }
        finally
        {
            PageContext.DisplayDraft = savedDisplay;
            PageContext.KeyboardColorInput = savedColor;
            PageContext.GpuDraft = savedGpu;
        }

        var restore = new PageScrollRestore(260);
        Require(!restore.TryRestore(20, 400, contentReady: false, out _), "Loading placeholder clamped the saved page offset");
        Require(!restore.TryRestore(500, 400, contentReady: false, out _), "Partial resource layout restored before all loading rings disappeared");
        Require(restore.TryRestore(500, 400, contentReady: true, out var offset) && offset == 260, "Page did not restore its saved offset");
        Require(restore.Pending && !restore.ConfirmRestore(accepted: false, actualOffset: 0, offset), "Native refusal completed the restore");
        Require(restore.TryRestore(500, 400, contentReady: true, out offset) && restore.ConfirmRestore(accepted: true, actualOffset: 0, offset), "Native refusal did not allow a later accepted retry");
        Require(!restore.TryRestore(500, 400, contentReady: true, out _), "Page write restored an old scroll offset again");
        var shorter = new PageScrollRestore(260);
        Require(shorter.TryRestore(80, 400, contentReady: true, out offset) && offset == 80, "Changed page extent was not clamped");
        Require(shorter.ConfirmRestore(accepted: false, actualOffset: 80, offset) && !shorter.Pending, "Already restored native offset did not complete the request");
        var cancelled = new PageScrollRestore(260);
        cancelled.Cancel();
        Require(!cancelled.TryRestore(500, 400, contentReady: true, out _), "User interaction did not cancel a pending restore");
        Require(!new PageScrollRestore(double.NaN).Pending && !new PageScrollRestore(-1).Pending, "Invalid offsets were accepted");
        var invalidLayout = new PageScrollRestore(260);
        Require(!invalidLayout.TryRestore(double.NaN, 400, true, out _) && !invalidLayout.TryRestore(500, 0, true, out _) && invalidLayout.Pending, "Unmeasured layout completed the restore");
        report["page_session_drafts_reconcile_and_matching_completion"] = true;
        report["page_scroll_once_after_layout_and_clamp"] = true;
        report["page_scroll_native_refusal_retry_and_already_restored"] = true;
        return Task.CompletedTask;
    }
}
