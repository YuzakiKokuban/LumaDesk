using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Core.V1Protocol;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace JiYaoChu.Ui;

/// <summary>Set native content alignment before any Reactor controls mount.</summary>
public static class NativeLayout
{
    public static void Configure()
    {
        var previousNavigation = NavigationViewElement.Descriptor.AfterChildrenMount;
        NavigationViewElement.Descriptor.WithAfterChildrenMount((in MountContext context, NavigationViewElement element, NavigationView control) =>
        {
            previousNavigation?.Invoke(in context, element, control);
            control.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            control.VerticalContentAlignment = VerticalAlignment.Stretch;
            control.OpenPaneLength = 220;
            control.CompactPaneLength = 48;
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
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JiYaoChu");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "layout-debug.json"), System.Text.Json.JsonSerializer.Serialize(report));
    }
}
