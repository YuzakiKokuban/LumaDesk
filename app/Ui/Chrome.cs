using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using JiYaoChu.Model;
using JiYaoChu.Services;
using static Microsoft.UI.Reactor.Factories;

namespace JiYaoChu.Ui;

/// <summary>
/// The furniture every page is built from.
/// </summary>
/// <remarks>
/// Deliberately small and uniform. The original front end was criticised for
/// cards whose contents sat on different baselines and for tiles that forced
/// unequal labels into equal boxes; the answer is one card shape with fixed
/// internal geometry, so a row of them lines up no matter what it contains.
/// </remarks>
public static class Chrome
{
    /// <summary>A scrollable page with a heading and evenly spaced sections.</summary>
    public static Element Page(string title, string? subtitle, params Element[] sections)
    {
        var body = new List<Element>(sections.Length + 1) { Header(title, subtitle).WithKey("page-heading") };
        body.AddRange(sections.Where(section => section is not null));

        return ScrollViewer(
            VStack(14, [.. body]).HAlign(HorizontalAlignment.Stretch).Padding(18, 12, 18, 20))
            .AutomationName(PageContext.ScrollAutomationPrefix + title) with
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
        };
    }

    /// <summary>Just the heading block, for pages that lay themselves out.</summary>
    public static Element Header(string title, string? subtitle = null)
    {
        var lines = new List<Element> { TextBlock(title).FontSize(26).SemiBold() };
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            lines.Add(Body(subtitle).Foreground(Theme.SecondaryText).TextWrapping(TextWrapping.Wrap));
        }

        return VStack(2, [.. lines]);
    }

    /// <summary>
    /// A reading, rendered on a three-line grid: name, value, note.
    /// </summary>
    /// <param name="label">What is being measured, e.g. "CPU 温度".</param>
    /// <param name="value">The number, already formatted, or "—" when unknown.</param>
    /// <param name="unit">The unit, shown smaller beside the number.</param>
    /// <param name="note">A second line of context, e.g. "1900 MHz · 0 %".</param>
    /// <param name="tone">Optional colour for the number. Defaults to primary text.</param>
    public static Element StatCard(
        string label,
        string value,
        string? unit = null,
        string? note = null,
        ThemeRef? tone = null)
    {
        var number = TextBlock(value)
            .FontSize(28)
            .SemiBold()
            .Foreground(tone ?? Theme.PrimaryText);

        var reading = unit is null
            ? (Element)HStack(6, number)
            : HStack(6, number, Body(unit).Foreground(Theme.SecondaryText).Margin(0, 0, 0, 4));

        return Border(
                Grid(
                    columns: [GridSize.Star()],
                    rows: [GridSize.Auto, GridSize.Star(), GridSize.Auto],
                    Caption(label).Foreground(Theme.SecondaryText).Grid(row: 0, column: 0),
                    reading.Grid(row: 1, column: 0).VAlign(VerticalAlignment.Center),
                    Caption(note ?? "")
                        .Foreground(Theme.TertiaryText)
                        .MaxLines(2)
                        .TextWrapping(TextWrapping.Wrap)
                        .Grid(row: 2, column: 0)))
            .CornerRadius(12)
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke, 1)
            .Padding(16)
            .MinHeight(108);
    }

    /// <summary>A card with a heading and a body.</summary>
    public static Element SectionCard(string title, params Element[] body)
        => Border(
                VStack(10, [Body(title).SemiBold().FontSize(16), .. body]))
            .CornerRadius(12)
            .Background(Theme.CardBackground)
            .WithBorder(Theme.CardStroke, 1)
            .Padding(16).WithKey("section:" + title);

    /// <summary>A label on the left, a value on the right, on one baseline.</summary>
    public static Element Field(string label, string value)
        => Grid(
            columns: [GridSize.Px(140), GridSize.Star()],
            rows: [GridSize.Auto],
            Body(label).Foreground(Theme.SecondaryText).TextWrapping(TextWrapping.Wrap).Grid(row: 0, column: 0),
            Body(value).TextWrapping(TextWrapping.Wrap).Grid(row: 0, column: 1)).WithKey("field:" + label);

    /// <summary>A setting: name and explanation on the left, a control on the right.</summary>
    public static Element SettingRow(string label, string? description, Element control)
        => Grid(
            columns: [GridSize.Star(), GridSize.Auto],
            rows: [GridSize.Auto],
            VStack(2,
                Body(label),
                description is null
                    ? null
                    : Caption(description).Foreground(Theme.SecondaryText).TextWrapping(TextWrapping.Wrap)).Grid(row: 0, column: 0),
            control.Grid(row: 0, column: 1).VAlign(VerticalAlignment.Center).Margin(16, 0, 0, 0)).WithKey("setting:" + label);

    /// <summary>A thin rule between groups of settings.</summary>
    public static Element Rule()
        => Border(null).Height(1).Background(Theme.DividerStroke).Margin(0, 2, 0, 2);

    /// <summary>A short status word, coloured by severity.</summary>
    public static Element Pill(string text, InfoBarSeverity severity)
        => Caption(text).SemiBold().Foreground(Tone(severity)).HAlign(HorizontalAlignment.Left);

    /// <summary>A full-width message strip, used for errors and warnings.</summary>
    public static Element Notice(string title, string message, InfoBarSeverity severity)
        => InfoBar(title, message).Severity(severity).IsClosable(false).WithKey("notice:" + title);

    public static Element Feedback(string? error, string? status)
        => VStack(0, error is null
                ? Caption(status ?? " ").Foreground(Theme.SecondaryText)
                : Caption(error).Foreground(Theme.SystemCritical).TextWrapping(TextWrapping.Wrap))
            .MinHeight(22).WithKey("operation-feedback");

    /// <summary>
    /// One option in a set of mutually exclusive choices.
    /// </summary>
    /// <remarks>
    /// The whole card is the click target. Selection is drawn as an accent
    /// outline plus a pill rather than an accent fill, so the description keeps
    /// its normal contrast and stays legible either way.
    /// </remarks>
    public static Element ChoiceCard(
        string title,
        string? tag,
        string? description,
        bool selected,
        bool enabled,
        Action onClick,
        string? badge = null)
        => Button(
                Border(
                        VStack(
                            6,
                            tag is null
                                ? Body(title).SemiBold()
                                : HStack(
                                    8,
                                    Body(title).SemiBold(),
                                    Caption(tag).Foreground(Theme.TertiaryText)),
                            description is null
                                ? null
                                : Caption(description)
                                    .Foreground(Theme.SecondaryText)
                                    .TextWrapping(TextWrapping.Wrap),
                            Caption(selected ? $"✓ {badge ?? "当前"}" : " ")
                                .Foreground(selected ? Theme.Accent : Theme.SecondaryText)))
                    .CornerRadius(10)
                    .Padding(14)
                    .Background(Theme.CardBackground)
                    .WithBorder(selected ? Theme.Accent : Theme.CardStroke, selected ? 2 : 1)
                    .MinHeight(96),
                onClick)
            .SubtleButton().Padding(0).HAlign(HorizontalAlignment.Stretch)
            .AutomationName(title)
            .IsEnabled(enabled).WithKey("choice:" + title);

    /// <summary>Lays a set of <see cref="ChoiceCard"/>s out as equal columns.</summary>
    public static Element ChoiceRow(params Element[] cards)
        => FlexRow([.. cards.Select(card => card.MinWidth(0).Flex(grow: 1, shrink: 1, basis: 210))]) with
        {
            Wrap = FlexWrap.Wrap,
            ColumnGap = 10,
            RowGap = 10,
        };

    /// <summary>
    /// Lays cards out <paramref name="columns"/> to a row, several rows deep.
    /// </summary>
    /// <remarks>
    /// A single <see cref="ChoiceRow"/> of twenty-one effects would squash every
    /// label into an unreadable column. Rows of a few keep the cards legible and
    /// still leave everything on one baseline per row.
    /// </remarks>
    public static Element ChoiceGrid(int columns, IReadOnlyList<Element> cards)
    {
        var rows = new List<Element>();
        for (var start = 0; start < cards.Count; start += columns)
        {
            rows.Add(ChoiceRow([.. cards.Skip(start).Take(columns)]));
        }

        return VStack(12, [.. rows]);
    }

    /// <summary>A clickable colour chip, with an optional caption underneath.</summary>
    public static Element Swatch(string hex, string? label, bool selected, bool enabled, Action onClick)
    {
        var color = Hex.Normalise(hex);
        return Button(Border(Grid(
                columns: [GridSize.Px(28), GridSize.Star(), GridSize.Px(18)], rows: [GridSize.Auto],
                Border(null).Width(18).Height(18).CornerRadius(9).Background(color)
                    .WithBorder(Theme.CardStroke, 1).Grid(0, 0).VAlign(VerticalAlignment.Center),
                Caption(label ?? color).Grid(0, 1).VAlign(VerticalAlignment.Center),
                Caption(selected ? "✓" : "").Foreground(Theme.Accent).Grid(0, 2).VAlign(VerticalAlignment.Center)))
                .Padding(10, 8).CornerRadius(8).WithBorder(selected ? Theme.Accent : Theme.CardStroke, 1)
                .Background(Theme.CardBackground), onClick)
            .SubtleButton().Padding(0).Width(96).Height(40)
            .AutomationName(label is null ? "选择颜色" : $"颜色 {label}")
            .IsEnabled(enabled);
    }

    public static Element CompactChoice(string label, bool selected, bool enabled, Action onClick)
        => Button(Border(Caption(label).HAlign(HorizontalAlignment.Center).VAlign(VerticalAlignment.Center))
                .CornerRadius(8).Padding(12, 8).Background(Theme.CardBackground)
                .WithBorder(selected ? Theme.Accent : Theme.CardStroke, 1), onClick)
            .SubtleButton().Padding(0).Width(68).Height(36).AutomationName($"亮度 {label}").IsEnabled(enabled);

    /// <summary>The colour a reading should take, given how hot it is.</summary>
    /// <param name="celsius">A temperature, or null when the sensor is unreadable.</param>
    public static ThemeRef? TemperatureTone(double? celsius) => celsius switch
    {
        null => Theme.TertiaryText,
        >= 95 => Theme.SystemCritical,
        >= 85 => Theme.SystemCaution,
        _ => null,
    };

    /// <summary>Formats a nullable number, using an em dash when it is unknown.</summary>
    public static string Number(double? value, string format = "0.#", string unknown = "—")
        => value is { } v ? v.ToString(format, System.Globalization.CultureInfo.InvariantCulture) : unknown;

    /// <summary>Formats a nullable integer the same way.</summary>
    public static string Number(ulong? value, string unknown = "—")
        => value is { } v ? v.ToString(System.Globalization.CultureInfo.InvariantCulture) : unknown;

    /// <summary>Formats a nullable integer the same way.</summary>
    public static string Number(uint? value, string unknown = "—")
        => value is { } v ? v.ToString(System.Globalization.CultureInfo.InvariantCulture) : unknown;

    private static ThemeRef Tone(InfoBarSeverity severity) => severity switch
    {
        InfoBarSeverity.Error => Theme.SystemCritical,
        InfoBarSeverity.Warning => Theme.SystemCaution,
        InfoBarSeverity.Success => Theme.SystemSuccess,
        _ => Theme.SecondaryText,
    };

    private static ThemeRef Background(InfoBarSeverity severity) => severity switch
    {
        InfoBarSeverity.Error => Theme.SystemCriticalBackground,
        InfoBarSeverity.Warning => Theme.SystemCautionBackground,
        InfoBarSeverity.Success => Theme.SystemSuccessBackground,
        _ => Theme.SystemNeutralBackground,
    };
}

