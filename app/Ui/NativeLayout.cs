using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Core.V1Protocol;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using JiYaoChu.Services;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;

namespace JiYaoChu.Ui;

/// <summary>Set native content alignment before any Reactor controls mount.</summary>
public static class NativeLayout
{
    private static readonly ConditionalWeakTable<ScrollViewer, PageScrollSession> PageScrollSessions = new();
    internal static List<object> ScrollVerificationTrace { get; } = [];
    internal static NavigationView? VerificationNavigation { get; private set; }
    public static void Configure()
    {
        TrendGraph.Configure();
        var previousNavigation = NavigationViewElement.Descriptor.AfterChildrenMount;
        NavigationViewElement.Descriptor.WithAfterChildrenMount((in MountContext context, NavigationViewElement element, NavigationView control) =>
        {
            previousNavigation?.Invoke(in context, element, control);
            if (Environment.GetCommandLineArgs().Any(arg => arg.StartsWith("--verify-shell=")))
            {
                VerificationNavigation = control;
                control.Unloaded += (_, _) => { if (ReferenceEquals(VerificationNavigation, control)) VerificationNavigation = null; };
            }
            control.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            control.VerticalContentAlignment = VerticalAlignment.Stretch;
            control.OpenPaneLength = 220;
            control.CompactPaneLength = 48;
            void SizePowerIcon()
            {
                foreach (var item in control.MenuItems.OfType<NavigationViewItem>())
                    if (item.Tag?.ToString() == "tuning" && item.Icon is FontIcon icon)
                    {
                        icon.FontSize = 28;
                        // FontIcon scales its glyph to its layout slot; enlarge both.
                        icon.Width = 24;
                        icon.Height = 24;
                    }
            }
            SizePowerIcon();
            control.Loaded += (_, _) => SizePowerIcon();
            control.Loaded += (_, _) => JiYaoChu.Services.BackgroundHost.Attach(
                Microsoft.UI.Windowing.AppWindow.GetFromWindowId(control.XamlRoot.ContentIslandEnvironment.AppWindowId));
            if (control.Content is FrameworkElement content)
            {
                content.HorizontalAlignment = HorizontalAlignment.Stretch;
                content.VerticalAlignment = VerticalAlignment.Stretch;
            }
        });
        var previousButton = ButtonElement.Descriptor.AfterChildrenMount;
        ButtonElement.Descriptor.WithAfterChildrenMount((in MountContext context, ButtonElement element, Button control) =>
        {
            previousButton?.Invoke(in context, element, control);
            control.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            control.VerticalContentAlignment = VerticalAlignment.Stretch;
        });
        var previousScroll = ScrollViewerElement.Descriptor.AfterChildrenMount;
        ScrollViewerElement.Descriptor.WithAfterChildrenMount((in MountContext context, ScrollViewerElement element, ScrollViewer control) =>
        {
            previousScroll?.Invoke(in context, element, control);
            // Reactor applies general modifiers after descriptor mount callbacks.
            // Read the declared name rather than the not-yet-updated native value.
            var scrollName = element.Modifiers?.AutomationName ?? AutomationProperties.GetName(control);
            if (scrollName.StartsWith(PageContext.ScrollAutomationPrefix, StringComparison.Ordinal))
            {
                var session = PageScrollSessions.GetValue(control, viewer => new PageScrollSession(viewer));
                session.SetKey(scrollName[PageContext.ScrollAutomationPrefix.Length..]);
            }
            control.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            control.HorizontalScrollMode = ScrollMode.Disabled;
            control.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            void Fit()
            {
                if (control.Content is FrameworkElement content && control.ActualWidth > 0)
                {
                    content.HorizontalAlignment = HorizontalAlignment.Stretch;
                    content.Width = Math.Max(0, control.ActualWidth - 16);
                    if (Environment.GetCommandLineArgs().Contains("--layout-check"))
                        control.DispatcherQueue.TryEnqueue(() => RecordLayout(control, content));
                }
            }
            control.SizeChanged += (_, _) => Fit();
            control.Loaded += (_, _) => Fit();
        });
    }

    // Only Chrome.Page's marked outer viewer owns a saved offset. Nested control viewers
    // never register, and native event handlers are installed once per viewer.
    private sealed class PageScrollSession
    {
        private readonly ScrollViewer _viewer;
        private string _key = "";
        private bool _loaded;
        private double _lastOffset;
        private PageScrollRestore _restore = new(0);
        private readonly DispatcherQueueTimer _retry;

        public PageScrollSession(ScrollViewer viewer)
        {
            _viewer = viewer;
            _loaded = viewer.IsLoaded;
            _retry = viewer.DispatcherQueue.CreateTimer();
            _retry.Interval = TimeSpan.FromMilliseconds(30);
            _retry.IsRepeating = true;
            _retry.Tick += (_, _) => RestoreAfterLayout(null, null!);
            viewer.Loaded += (_, _) => { if (!_loaded) { _loaded = true; BeginRestore(); } };
            viewer.Unloaded += (_, _) =>
            {
                Trace("unload");
                // Content may already be detached by the renderer. Keep the last
                // ViewChanged offset rather than capturing a teardown-time zero.
                if (!_restore.Pending && _key.Length > 0) PageContext.RememberScroll(_key, _lastOffset);
                _loaded = false;
                viewer.LayoutUpdated -= RestoreAfterLayout;
                _restore.Cancel();
                _retry.Stop();
            };
            viewer.ViewChanged += (_, args) => { if (!args.IsIntermediate) Remember(); };
            // Accessibility and intentional programmatic focus must still bring the
            // target into view. A pending restore must not override that request.
            viewer.BringIntoViewRequested += (_, args) =>
            {
                if (args.TargetElement is Control { FocusState: FocusState.Programmatic } target) CancelRestore("programmatic:" + AutomationProperties.GetName(target));
            };
            viewer.GettingFocus += (_, args) =>
            {
                if (args.FocusState is FocusState.Keyboard or FocusState.Pointer) CancelRestore("focus:" + args.FocusState);
            };
            viewer.PointerPressed += (_, _) => CancelRestore("pointer");
            viewer.PointerWheelChanged += (_, _) => CancelRestore("wheel");
        }

        public void SetKey(string key)
        {
            if (_key == key) return;
            if (_loaded && !_restore.Pending && _key.Length > 0) PageContext.RememberScroll(_key, _lastOffset);
            _key = key;
            Trace("key");
            if (_loaded) BeginRestore();
        }

        private void Remember()
        {
            if (_loaded && !_restore.Pending && _key.Length > 0 && _viewer.Content is DependencyObject content && HasPageHeading(content))
            {
                if (_lastOffset != _viewer.VerticalOffset) Trace("remember");
                _lastOffset = _viewer.VerticalOffset;
                PageContext.RememberScroll(_key, _lastOffset);
            }
        }

        private void CancelRestore(string reason)
        {
            Trace("cancel:" + reason);
            _restore.Cancel();
            _viewer.LayoutUpdated -= RestoreAfterLayout;
            _retry.Stop();
            Remember();
        }

        private void BeginRestore()
        {
            _viewer.LayoutUpdated -= RestoreAfterLayout;
            _retry.Stop();
            _lastOffset = PageContext.ScrollOffset(_key);
            _restore = new(_lastOffset);
            Trace("begin");
            if (!_restore.Pending) return;
            _viewer.LayoutUpdated += RestoreAfterLayout;
            _viewer.DispatcherQueue.TryEnqueue(() => RestoreAfterLayout(null, null!));
        }

        private void RestoreAfterLayout(object? sender, object args)
        {
            if (!_loaded || !_restore.Pending) return;
            var ready = _viewer.Content is DependencyObject content && HasPageHeading(content) && !HasLoadingRing(content);
            if (!_restore.TryRestore(_viewer.ScrollableHeight, _viewer.ViewportHeight, ready, out var target)) return;
            var alreadyAtTarget = Math.Abs(_viewer.VerticalOffset - target) < 1;
            var accepted = !alreadyAtTarget && _viewer.ChangeView(null, target, null, disableAnimation: true);
            Trace($"restore:{target}:accepted={accepted}:ready={ready}");
            if (!_restore.Pending) return;
            if (!_restore.ConfirmRestore(accepted, _viewer.VerticalOffset, target))
            {
                // A native refusal may not produce another layout event. Retry only
                // while this loaded page still owns an outstanding restore request.
                if (!_retry.IsRunning) _retry.Start();
                return;
            }
            _viewer.LayoutUpdated -= RestoreAfterLayout;
            _retry.Stop();
            _lastOffset = target;
            PageContext.RememberScroll(_key, target);
            Trace("complete");
        }

        private void Trace(string action)
        {
            if (!Environment.GetCommandLineArgs().Any(arg => arg.StartsWith("--verify-shell=")) || ScrollVerificationTrace.Count >= 300) return;
            ScrollVerificationTrace.Add(new { action, key = _key, viewer = RuntimeHelpers.GetHashCode(_viewer), actual = _viewer.VerticalOffset,
                last = _lastOffset, saved = PageContext.ScrollOffset(_key), pending = _restore.Pending, loaded = _loaded, nativeLoaded = _viewer.IsLoaded,
                extent = _viewer.ScrollableHeight, viewport = _viewer.ViewportHeight,
                heading = _viewer.Content is DependencyObject content && HasPageHeading(content) });
        }

        private bool HasPageHeading(DependencyObject root)
        {
            if (root is TextBlock { IsLoaded: true, FontSize: >= 25 } heading && heading.Text == _key) return true;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                if (HasPageHeading(VisualTreeHelper.GetChild(root, i))) return true;
            return false;
        }

        private static bool HasLoadingRing(DependencyObject root)
        {
            if (root is ProgressRing { Visibility: Visibility.Visible }) return true;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                if (HasLoadingRing(VisualTreeHelper.GetChild(root, i))) return true;
            return false;
        }
    }

    private static void RecordLayout(ScrollViewer viewer, FrameworkElement content)
    {
        var report = new
        {
            captured = DateTimeOffset.Now,
            viewport = viewer.ViewportWidth,
            content = content.ActualWidth,
            extent = viewer.ExtentWidth,
            horizontalOverflow = viewer.ScrollableWidth,
        };
        var directory = JiYaoChu.Services.StartupLog.DataDirectory;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "layout-debug.json"), System.Text.Json.JsonSerializer.Serialize(report));
    }
}
