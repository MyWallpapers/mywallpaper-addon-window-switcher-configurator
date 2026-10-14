using System.Windows;
using System.ComponentModel;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Switcher.Core;

namespace Switcher.Windows;

internal sealed class SwitcherWindow : Window
{
    private readonly Grid root = new();
    private readonly StackPanel header = new();
    private readonly TextBlock heading = new(), status = new(), hints = new();
    private readonly TextBox search = new();
    private readonly Grid searchHost = new();
    private readonly TextBlock watermark = new() { Text = "Rechercher un titre ou une application…", IsHitTestVisible = false, Margin = new(13, 8, 13, 8), VerticalAlignment = VerticalAlignment.Center, FontSize = 15 };
    private readonly Canvas cards = new();
    private readonly Border surface = new();
    private readonly Dictionary<long, (Thumbnail Thumbnail, FrameworkElement Preview)> thumbnails = [];
    private readonly Dictionary<long, (WindowInfo Info, ImageSource? Icon)> icons = [];
    private long iconLifetime;
    private readonly HashSet<long> pendingIcons = [];
    private HashSet<int> previewIndices = [];
    private long lastRenderedSelection;
    private readonly SemaphoreSlim iconWorkers = new(2);
    private readonly Action<KeyAction> keyAction;
    private readonly Action<long, bool> cardAction;
    private readonly Action<long> closeAction;
    private readonly Action<string> queryAction;
    private readonly Action<bool> cancel;
    private readonly DimmerWindow dimmer;
    private readonly ShadowWindow shadow = new();
    private Settings settings = Settings.Defaults;
    private SwitchSession? session;
    private LayoutPlan? plan;
    private nint hwnd;
    private bool updating, suspending;
    private long hovered;
    private bool allowActivate = true;
    private bool allowClose;
    internal bool GlassApplied { get; private set; }
    internal int SuccessfulPreviews => thumbnails.Values.Count(t => t.Thumbnail.Live && t.Thumbnail.LastResult >= 0);
    internal LayoutPlan? Plan => plan;
    internal bool ModalInteraction { get; set; }

    internal SwitcherWindow(Action<KeyAction> keyAction, Action<long, bool> cardAction,
        Action<long> closeAction, Action<string> queryAction, Action<bool> cancel)
    {
        this.keyAction = keyAction; this.cardAction = cardAction; this.closeAction = closeAction;
        this.queryAction = queryAction; this.cancel = cancel;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true;
        Title = "Sélecteur de fenêtres — MyWallpaper"; Background = Brushes.Transparent; UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        header.Children.Add(heading); header.Children.Add(status);
        searchHost.Margin = new(0, 16, 0, 16); search.Padding = new(12, 8, 12, 8); search.FontSize = 15;
        searchHost.Children.Add(search); searchHost.Children.Add(watermark);
        AutomationProperties.SetName(search, "Rechercher une fenêtre par titre ou application");
        search.TextChanged += (_, _) => { watermark.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; if (!updating) queryAction(search.Text); };
        var top = new StackPanel(); top.Children.Add(header); top.Children.Add(searchHost);
        root.Children.Add(top); Grid.SetRow(cards, 1); root.Children.Add(cards);
        hints.Margin = new(0, 18, 0, 0); hints.TextWrapping = TextWrapping.Wrap; hints.FontSize = 12;
        Grid.SetRow(hints, 2); root.Children.Add(hints);
        surface.Child = root; Content = surface;
        dimmer = new DimmerWindow(() => { if (settings.Bool("dismissOutside")) cancel(true); });
        SourceInitialized += (_, _) =>
        {
            hwnd = new WindowInteropHelper(this).Handle;
            Native.SetWindowLongPtr(hwnd, -20, Native.GetWindowLongPtr(hwnd, -20) | 0x80);
            HwndSource.FromHwnd(hwnd)?.AddHook(OnNativeMessage);
        };
        PreviewKeyDown += OnKey;
        PreviewMouseWheel += (_, e) => { if (settings.Bool("mouseWheel")) { keyAction(e.Delta < 0 ? KeyAction.Forward : KeyAction.Backward); e.Handled = true; } };
        Deactivated += (_, _) =>
        {
            if (suspending || ModalInteraction || !allowActivate || !IsVisible || !settings.Bool("dismissOnFocusLoss")) return;
            Dispatcher.BeginInvoke(() =>
            {
                if (!suspending && !ModalInteraction && IsVisible && !IsActive && !OwnedWindows.Cast<Window>().Any(w => w.IsActive)) cancel(false);
            }, DispatcherPriority.Background);
        };
        LayoutUpdated += (_, _) => UpdatePreviews();
        Closing += OnClosing;
        Closed += (_, _) => { CompositionTarget.Rendering -= OnRendering; ClearPreviews(); session = null; plan = null; dimmer.Close(); shadow.Close(); };
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose) return;
        e.Cancel = true; cancel(true); // SC_CLOSE / Alt+F4 ends the current selection; the reusable HWND stays alive.
    }
    internal void CloseOwned() { allowClose = true; Close(); }
    private nint OnNativeMessage(nint window, int message, nint wparam, nint lparam, ref bool handled)
    {
        if (message == 0x2e0) Dispatcher.BeginInvoke(() => { if (session is not null && plan is not null) Render(session, settings); });
        if (message == 0x1a) Dispatcher.BeginInvoke(() => { if (session is not null && IsVisible) Render(session, settings); });
        return 0;
    }
    private void OnKey(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        bool inSearch = search.IsKeyboardFocusWithin && search.Text.Length > 0;
        KeyAction? command = key switch
        {
            Key.Tab => Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? KeyAction.Backward : KeyAction.Forward,
            Key.Left when !inSearch => KeyAction.Left, Key.Right when !inSearch => KeyAction.Right,
            Key.Up => KeyAction.Up, Key.Down => KeyAction.Down, Key.Enter => KeyAction.Commit,
            Key.Escape when !inSearch || search.Text.Length == 0 => KeyAction.Cancel,
            Key.Delete when !inSearch => KeyAction.Close, _ => null
        };
        if (key == Key.Escape && command is null) { search.Clear(); e.Handled = true; }
        if (command is { } action) { keyAction(action); e.Handled = true; }
        if (key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && settings.Bool("searchEnabled"))
        { search.Focus(); search.SelectAll(); e.Handled = true; }
    }
    internal void Open(SwitchSession value, Settings configuration, bool activate = true)
    {
        allowActivate = activate;
        ShowActivated = activate; dimmer.ShowActivated = false;
        updating = true; search.Clear(); updating = false;
        _ = new WindowInteropHelper(this).EnsureHandle();
        Render(value, configuration);
        Position(value, configuration);
        UpdateAuxiliaryWindows(value, configuration);
        Show();
        CompositionTarget.Rendering -= OnRendering;
        CompositionTarget.Rendering += OnRendering;
        ApplyMaterial();
        if (activate) { Activate(); if (configuration.Bool("searchEnabled") && !value.HoldAlt) search.Focus(); else Focus(); }
        UpdateLayout(); UpdatePreviews(); AnimateOpen();
    }
    internal void Render(SwitchSession value, Settings configuration)
    {
        long previousSelection = lastRenderedSelection;
        lastRenderedSelection = value.Current?.Handle ?? 0;
        session = value; settings = configuration;
        ClearPreviews(); cards.Children.Clear();
        icons.Keys.Where(handle => !value.Windows.Any(w => w.Handle == handle)).ToList().ForEach(handle => icons.Remove(handle));
        root.Margin = new(configuration.Number("padding"));
        FontFamily = new(configuration.String("fontFamily") switch { "system" => "Segoe UI Variable", "segoe" => "Segoe UI", "arial" => "Arial", _ => "Consolas" });
        Foreground = Brush("textColor"); surface.CornerRadius = new(configuration.Number("panelRadius"));
        surface.BorderThickness = new(configuration.Number("panelBorderWidth")); surface.BorderBrush = Brush("panelBorderColor");
        surface.Background = BackgroundBrush();
        heading.Text = "Vos fenêtres"; heading.FontSize = 24; heading.FontWeight = FontWeights.SemiBold; heading.Foreground = Brush("textColor");
        status.Text = $"{configuration.ProfileName}  ·  {value.Windows.Count} fenêtre{(value.Windows.Count > 1 ? "s" : "")}";
        status.Foreground = Brush("mutedColor"); status.FontSize = 12; status.Margin = new(0, 4, 0, 0);
        search.Visibility = configuration.Bool("searchEnabled") && !value.HoldAlt ? Visibility.Visible : Visibility.Collapsed;
        searchHost.Visibility = search.Visibility; watermark.Foreground = Brush("mutedColor");
        search.Background = Brush("cardColor"); search.Foreground = Brush("textColor"); search.CaretBrush = Brush("accentColor");
        search.BorderBrush = Brush("accentColor"); search.SelectionBrush = Brush("selectedColor");
        hints.Foreground = Brush("mutedColor"); hints.Visibility = configuration.Bool("showHints") ? Visibility.Visible : Visibility.Collapsed;
        hints.Text = value.HoldAlt ? "Tab / Maj + Tab : parcourir   ·   Relâcher Alt : choisir   ·   Échap : annuler"
            : "Tab / flèches : parcourir   ·   Entrée : choisir   ·   Échap : annuler   ·   Suppr : fermer";
        var monitor = Monitor(value, configuration);
        // Native monitor rectangles are physical pixels; WPF layout is device independent.
        var dpi = VisualTreeHelper.GetDpi(this);
        double width = monitor.Work.Width / dpi.DpiScaleX * configuration.Number("panelWidthPct") / 100 - 2 * configuration.Number("padding");
        double height = monitor.Work.Height / dpi.DpiScaleY * configuration.Number("panelHeightPct") / 100 - 2 * configuration.Number("padding") - (search.Visibility == Visibility.Visible ? 160 : 112);
        var all = WindowCatalog.Monitors();
        RectD desktop = monitor.Bounds;
        if (configuration.String("windowScreen") == "all" && all.Count > 0)
        {
            double left = all.Min(m => m.Bounds.X), top = all.Min(m => m.Bounds.Y);
            desktop = new(left, top, all.Max(m => m.Bounds.Right) - left, all.Max(m => m.Bounds.Bottom) - top);
        }
        plan = Layout.Compute(value.Windows, value.Selected, configuration, width, height, desktop);
        cards.Width = plan.Width; cards.Height = plan.Height;
        Width = plan.Width + 2 * configuration.Number("padding");
        Height = plan.Height + 2 * configuration.Number("padding") + (search.Visibility == Visibility.Visible ? 148 : 94) + (configuration.Bool("showHints") ? 36 : 0);
        if (plan.Pages > 1) status.Text += $"  ·  Page {plan.Page + 1}/{plan.Pages}";
        if (value.Windows.Count == 0)
        {
            var empty = new TextBlock { Text = "Aucune fenêtre ne correspond.\nEffacez la recherche ou assouplissez vos filtres.", TextWrapping = TextWrapping.Wrap,
                FontSize = 16, Foreground = Brush("mutedColor"), Margin = new(20), Width = Math.Max(180, plan.Width - 40) };
            cards.Children.Add(empty);
        }
        previewIndices = plan.Cards.Where(c => c.LivePreview && !value.Windows[c.Index].Minimized && !value.Windows[c.Index].Hung)
            .OrderByDescending(c => c.Index == value.Selected).Take(configuration.Int("previewBudget")).Select(c => c.Index).ToHashSet();
        foreach (var card in plan.Cards) BuildCard(card, value.Windows[card.Index], card.Index == value.Selected);
        if (IsVisible)
        {
            ApplyMaterial(); Position(value, configuration); UpdateLayout(); UpdatePreviews();
            UpdateAuxiliaryWindows(value, configuration);
            if (!ReducedMotion && previousSelection != value.Current?.Handle)
            {
                var animation = new DoubleAnimation(.75, 1, TimeSpan.FromMilliseconds(configuration.Number("selectionDurationMs"))) { EasingFunction = new ExponentialEase { Exponent = 4, EasingMode = EasingMode.EaseOut } };
                heading.BeginAnimation(OpacityProperty, animation);
            }
        }
    }
    private void BuildCard(CardLayout layout, WindowInfo window, bool selected)
    {
        var rect = layout.Bounds;
        var border = new Border
        {
            Width = rect.Width, Height = rect.Height, CornerRadius = new(settings.Number("cardRadius")),
            Background = Brush(selected ? "selectedColor" : hovered == window.Handle ? "hoverColor" : "cardColor"),
            BorderBrush = Brush(selected ? "accentColor" : "panelBorderColor"),
            BorderThickness = new(selected ? settings.Number("selectionBorderWidth") : settings.Number("cardBorderWidth")),
            Opacity = selected ? 1 : settings.Number("inactiveOpacity") / 100
        };
        var content = new Grid { Margin = new(8) };
        bool list = settings.String("layout") == "list", classic = settings.String("layout") == "classic";
        bool behindCascade = settings.String("layout") == "flip" && layout.Depth > 0;
        if (list)
        {
            content.ColumnDefinitions.Add(new() { Width = new GridLength(layout.Preview.Width > 0 ? 116 : 0) });
            content.ColumnDefinitions.Add(new());
        }
        else if (classic) content.RowDefinitions.Add(new());
        else
        {
            double previewHeight = behindCascade ? Math.Max(28, rect.Height - Math.Min(32, rect.Y / layout.Depth - 6) - 16) : classic ? 36 : Math.Max(28, layout.Preview.Height + 4);
            content.RowDefinitions.Add(new() { Height = new GridLength(previewHeight) });
            content.RowDefinitions.Add(new());
        }
        var preview = new Border { Background = Brush("surfaceColor"), CornerRadius = new(6),
            Margin = list ? new(0, 0, 8, 0) : new(0, 0, 0, 6) };
        var placeholder = new TextBlock { Text = window.Minimized ? "Fenêtre réduite" : window.Hung ? "Application occupée" : layout.LivePreview ? "Aperçu indisponible" : "",
            FontSize = 11, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("mutedColor"), Margin = new(10) };
        preview.Child = placeholder; if (!classic) content.Children.Add(preview);
        preview.Tag = !selected;
        var caption = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = list ? new(0, 0, 30, 0) : classic ? new(6, 4, 26, 4) : new(2, 2, 26, 0) };
        if (list) Grid.SetColumn(caption, 1); else if (!classic) Grid.SetRow(caption, 1);
        var titleRow = new Grid(); titleRow.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); titleRow.ColumnDefinitions.Add(new());
        if (settings.Bool("showIcon") && !behindCascade)
        {
            var icon = new Image { Width = classic ? 32 : 18, Height = classic ? 32 : 18, Margin = new(0, 0, 8, 0) };
            titleRow.Children.Add(icon);
            if (icons.TryGetValue(window.Handle, out var cached) && cached.Info.ProcessTime == window.ProcessTime) icon.Source = cached.Icon;
            else LoadIcon(window, icon);
        }
        if (settings.Bool("showTitle"))
        {
            var title = new TextBlock { Text = window.Title, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Brush("textColor"), FontSize = behindCascade ? 12 : settings.Number("titleFontSize"),
                FontWeight = settings.String("titleWeight") switch { "normal" => FontWeights.Normal, "medium" => FontWeights.Medium, _ => FontWeights.SemiBold }, VerticalAlignment = VerticalAlignment.Center };
            if (classic) { title.TextWrapping = TextWrapping.Wrap; title.MaxHeight = settings.Number("titleFontSize") * 2.7; title.TextTrimming = TextTrimming.WordEllipsis; }
            Grid.SetColumn(title, 1); titleRow.Children.Add(title);
        }
        caption.Children.Add(titleRow);
        var labels = new List<string>();
        if (settings.Bool("showApplication")) labels.Add(window.Application);
        if (settings.Bool("showScreen")) labels.Add($"Écran {window.ScreenIndex}");
        if (settings.Bool("showMinimizedBadge") && window.Minimized) labels.Add("Réduite");
        if (!behindCascade) caption.Children.Add(new TextBlock { Text = string.Join("  ·  ", labels), TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Brush("mutedColor"), FontSize = settings.Number("appFontSize"), Margin = new(0, 4, 0, 0) });
        content.Children.Add(caption);
        var button = new Button { Content = content, Background = Brushes.Transparent, BorderThickness = new(0), Padding = new(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Cursor = Cursors.Hand,
            Focusable = true, IsTabStop = false, ToolTip = $"{window.Title}\n{window.Application} — Écran {window.ScreenIndex}" };
        button.Template = FlatButtonTemplate();
        AutomationProperties.SetName(button, $"{window.Title}, {window.Application}{(window.Minimized ? ", réduite" : "")}{(selected ? ", sélectionnée" : "")}");
        AutomationProperties.SetAutomationId(button, $"window-{window.Handle}");
        button.Click += (_, _) => cardAction(window.Handle, true);
        border.Child = button;
        border.MouseEnter += (_, _) =>
        {
            hovered = window.Handle;
            border.Background = Brush(selected ? "selectedColor" : "hoverColor");
            AnimateHover(border, true);
            if (settings.Bool("hoverSelect") && session?.Current?.Handle != window.Handle) cardAction(window.Handle, false);
        };
        border.MouseLeave += (_, _) => { if (hovered == window.Handle) hovered = 0; border.Background = Brush(selected ? "selectedColor" : "cardColor"); AnimateHover(border, false); };
        Canvas.SetLeft(border, rect.X); Canvas.SetTop(border, rect.Y); cards.Children.Add(border);
        if (settings.Bool("showClose"))
        {
            var close = new Button { Width = 26, Height = 26, Background = Brush("cardColor"), BorderThickness = new(0), Cursor = Cursors.Hand,
                Content = new System.Windows.Shapes.Path { Data = Geometry.Parse("M 0,0 L 9,9 M 9,0 L 0,9"), Stroke = Brush("textColor"), StrokeThickness = 1.6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                ToolTip = "Fermer cette fenêtre", IsTabStop = false };
            close.Template = FlatButtonTemplate(); AutomationProperties.SetName(close, $"Fermer {window.Title}");
            close.Click += (_, _) => closeAction(window.Handle);
            Canvas.SetLeft(close, rect.Right - 31); Canvas.SetTop(close, rect.Bottom - 31); cards.Children.Add(close);
        }
        if (previewIndices.Contains(layout.Index))
        {
            var thumbnail = new Thumbnail(hwnd, window);
            if (thumbnail.Live) { thumbnails[window.Handle] = (thumbnail, preview); placeholder.Text = ""; }
            else thumbnail.Dispose();
        }
    }
    private static ControlTemplate FlatButtonTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(Border)); factory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Stretch);
        factory.AppendChild(presenter);
        return new ControlTemplate(typeof(Button)) { VisualTree = factory };
    }
    private async void LoadIcon(WindowInfo info, Image image)
    {
        if (!pendingIcons.Add(info.Handle)) return;
        long lifetime = iconLifetime;
        try
        {
            await iconWorkers.WaitAsync();
            ImageSource? icon;
            try { icon = await Task.Run(() => WindowCatalog.Icon(info)); }
            finally { iconWorkers.Release(); }
            if (lifetime != iconLifetime) return;
            icons[info.Handle] = (info, icon);
            if (IsVisible && image.IsLoaded) image.Source = icon;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        { Console.Error.WriteLine("window-switcher: icône indisponible"); }
        finally { pendingIcons.Remove(info.Handle); }
    }
    private Brush Brush(string id) { var color = settings.Color(id); return new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B)); }
    private Brush BackgroundBrush()
    {
        var first = settings.Color("surfaceColor"); var last = settings.Color("gradientEnd");
        byte alpha = (byte)Math.Round(first.A * settings.Number("surfaceOpacity") / 100);
        if (settings.String("material") == "gradient")
        {
            double rad = settings.Number("gradientAngle") * Math.PI / 180;
            return new LinearGradientBrush(Color.FromArgb(alpha, first.R, first.G, first.B), Color.FromArgb((byte)Math.Round(last.A * settings.Number("surfaceOpacity") / 100), last.R, last.G, last.B),
                new Point(.5 - Math.Cos(rad) / 2, .5 - Math.Sin(rad) / 2), new Point(.5 + Math.Cos(rad) / 2, .5 + Math.Sin(rad) / 2));
        }
        return new SolidColorBrush(Color.FromArgb(alpha, first.R, first.G, first.B));
    }
    private void ApplyMaterial()
    {
        if (hwnd == 0) return;
        int dark = settings.String("profile") == "paper" ? 0 : 1; Native.DwmSetWindowAttribute(hwnd, 20, ref dark, 4);
        int corner = settings.Number("panelRadius") == 0 ? 1 : 2; Native.DwmSetWindowAttribute(hwnd, 33, ref corner, 4);
        int backdrop = settings.String("material") == "glass" ? 3 : 1;
        int result = Native.DwmSetWindowAttribute(hwnd, 38, ref backdrop, 4);
        GlassApplied = backdrop == 3 && result >= 0;
        var margins = new Native.Margins { Left = GlassApplied ? -1 : 0, Right = GlassApplied ? -1 : 0, Top = GlassApplied ? -1 : 0, Bottom = GlassApplied ? -1 : 0 };
        Native.DwmExtendFrameIntoClientArea(hwnd, ref margins);
        if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target)
            target.BackgroundColor = GlassApplied ? Colors.Transparent : Color.FromRgb(settings.Color("surfaceColor").R, settings.Color("surfaceColor").G, settings.Color("surfaceColor").B);
        if (!GlassApplied && settings.String("material") == "glass") status.Text += "  ·  Fond uni de secours";
    }
    private static MonitorData Monitor(SwitchSession value, Settings settings)
    {
        var monitors = WindowCatalog.Monitors();
        long handle = settings.String("placement") switch { "cursor" => WindowCatalog.CursorMonitor(), "focused" => (long)Native.MonitorFromWindow((nint)value.PreviousForeground, 2), _ => 0 };
        return monitors.Find(m => m.Handle == handle) ?? monitors.FirstOrDefault(m => m.Primary)
            ?? new MonitorData(0, 1, new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), true);
    }
    private void Position(SwitchSession value, Settings settings)
    {
        var monitor = Monitor(value, settings); var dpi = VisualTreeHelper.GetDpi(this);
        Left = monitor.Work.X / dpi.DpiScaleX + (monitor.Work.Width / dpi.DpiScaleX - Width) / 2;
        Top = monitor.Work.Y / dpi.DpiScaleY + (monitor.Work.Height / dpi.DpiScaleY - Height) / 2;
        // Set physical coordinates explicitly; negative/mixed-DPI monitor origins are not global WPF DIPs.
        Native.SetWindowPos(hwnd, 0, (int)Math.Round(monitor.Work.X + (monitor.Work.Width - Width * dpi.DpiScaleX) / 2),
            (int)Math.Round(monitor.Work.Y + (monitor.Work.Height - Height * dpi.DpiScaleY) / 2),
            (int)Math.Round(Width * dpi.DpiScaleX), (int)Math.Round(Height * dpi.DpiScaleY), 0x14);
        int radius = (int)Math.Round(settings.Number("panelRadius") * dpi.DpiScaleX * 2);
        nint region = Native.CreateRoundRectRgn(0, 0, (int)Math.Round(Width * dpi.DpiScaleX) + 1, (int)Math.Round(Height * dpi.DpiScaleY) + 1, radius, radius);
        if (region != 0 && Native.SetWindowRgn(hwnd, region, true) == 0) Native.DeleteObject(region);
        // Windows owns a region after a successful SetWindowRgn and frees it on replacement/destruction.
    }
    internal bool ReducedMotion => settings.Bool("reduceMotion") || settings.Bool("followWindowsMotion") &&
        Native.SystemParametersInfo(0x1042, 0, out bool animation, 0) && !animation;
    private void AnimateOpen()
    {
        surface.BeginAnimation(OpacityProperty, null); surface.RenderTransform = Transform.Identity;
        if (ReducedMotion || settings.String("animationMode") == "none" || settings.Number("animationDurationMs") == 0) return;
        var duration = TimeSpan.FromMilliseconds(settings.Number("animationDurationMs"));
        surface.BeginAnimation(OpacityProperty, new DoubleAnimation(.35, 1, duration) { EasingFunction = new ExponentialEase { Exponent = 5, EasingMode = EasingMode.EaseOut } });
        if (settings.String("animationMode") is "slide" or "spring")
        {
            var translate = new TranslateTransform(); surface.RenderTransform = translate;
            IEasingFunction easing = settings.String("animationMode") == "spring" ? new BackEase { Amplitude = .3, EasingMode = EasingMode.EaseOut }
                : new ExponentialEase { Exponent = 5, EasingMode = EasingMode.EaseOut };
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(16, 0, duration) { EasingFunction = easing });
        }
    }
    private void AnimateHover(Border card, bool over)
    {
        if (ReducedMotion) return;
        var scale = new ScaleTransform(); var lift = new TranslateTransform(); var transforms = new TransformGroup();
        transforms.Children.Add(scale); transforms.Children.Add(lift); card.RenderTransformOrigin = new(.5, .5); card.RenderTransform = transforms;
        var duration = TimeSpan.FromMilliseconds(settings.Number("selectionDurationMs"));
        var easing = new ExponentialEase { Exponent = 4, EasingMode = EasingMode.EaseOut };
        double amount = over ? settings.Number("hoverScale") : 1;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(amount, duration) { EasingFunction = easing });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(amount, duration) { EasingFunction = easing });
        lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(over ? -settings.Number("hoverLift") : 0, duration) { EasingFunction = easing });
    }
    private void UpdatePreviews()
    {
        if (!IsVisible || hwnd == 0 || updating) return;
        foreach (var (_, entry) in thumbnails)
        {
            if (!entry.Preview.IsLoaded || entry.Preview.ActualWidth < 1 || entry.Preview.ActualHeight < 1) continue;
            var bounds = entry.Preview.TransformToAncestor(this).TransformBounds(new Rect(0, 0, entry.Preview.ActualWidth, entry.Preview.ActualHeight));
            var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
            Point tl = transform.Transform(bounds.TopLeft), br = transform.Transform(bounds.BottomRight);
            byte alpha = (byte)Math.Round(255 * settings.Number("previewOpacity") / 100 * surface.Opacity * (entry.Preview.Tag is true ? settings.Number("inactiveOpacity") / 100 : 1));
            entry.Thumbnail.Update(new Rect(tl, br), settings, alpha);
            if (entry.Thumbnail.LastResult < 0 && entry.Preview is Border { Child: TextBlock label }) label.Text = "Aperçu indisponible";
        }
    }
    private void OnRendering(object? sender, EventArgs e) => UpdatePreviews();
    private void ClearPreviews() { foreach (var entry in thumbnails.Values) entry.Thumbnail.Dispose(); thumbnails.Clear(); }
    internal void Suspend()
    {
        ++iconLifetime; icons.Clear();
        suspending = true; CompositionTarget.Rendering -= OnRendering; ClearPreviews(); Hide(); dimmer.Hide(); shadow.Hide(); cards.Children.Clear(); session = null; plan = null; lastRenderedSelection = 0; suspending = false;
    }
    private void UpdateAuxiliaryWindows(SwitchSession value, Settings configuration)
    {
        if (configuration.Number("desktopDim") > 0)
        {
            dimmer.SetArea(Monitor(value, configuration).Bounds, configuration.Number("desktopDim") / 100);
            if (!dimmer.IsVisible) dimmer.Show();
        }
        else dimmer.Hide();
        if (configuration.Number("shadowOpacity") > 0)
        {
            shadow.SetArea(this, configuration);
            if (!shadow.IsVisible) shadow.Show();
        }
        else shadow.Hide();
        // Showing an auxiliary topmost window must not cover the selection panel.
        if (dimmer.IsVisible) Native.SetWindowPos(new WindowInteropHelper(dimmer).Handle, hwnd, 0, 0, 0, 0, 0x13);
        if (shadow.IsVisible) Native.SetWindowPos(new WindowInteropHelper(shadow).Handle, hwnd, 0, 0, 0, 0, 0x13);
    }
    internal (bool DimmerVisible, double DimmerOpacity, bool ShadowVisible, int CachedIcons) AuxiliaryState
        => (dimmer.IsVisible, dimmer.Opacity, shadow.IsVisible, icons.Count);
    internal void EndAnimations() { surface.BeginAnimation(OpacityProperty, null); surface.Opacity = 1; surface.RenderTransform = Transform.Identity; UpdateLayout(); UpdatePreviews(); }
}

internal sealed class ShadowWindow : Window
{
    private readonly Canvas canvas = new();
    private readonly Border body = new() { Background = Brushes.Black };
    internal ShadowWindow()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; Content = canvas;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; ResizeMode = ResizeMode.NoResize;
        canvas.Children.Add(body); IsHitTestVisible = false;
        SourceInitialized += (_, _) =>
        {
            nint hwnd = new WindowInteropHelper(this).Handle;
            Native.SetWindowLongPtr(hwnd, -20, Native.GetWindowLongPtr(hwnd, -20) | 0x80 | 0x20 | 0x08000000);
        };
    }
    internal void SetArea(Window owner, Settings settings)
    {
        var dpi = VisualTreeHelper.GetDpi(owner);
        Native.GetWindowRect(new WindowInteropHelper(owner).Handle, out var rect);
        double margin = settings.Number("shadowBlur") + settings.Number("shadowDistance") + 4;
        body.Width = owner.Width; body.Height = owner.Height; body.CornerRadius = new(settings.Number("panelRadius"));
        Canvas.SetLeft(body, margin); Canvas.SetTop(body, margin);
        body.Effect = new DropShadowEffect { Color = Colors.Black, Opacity = settings.Number("shadowOpacity") / 100,
            BlurRadius = settings.Number("shadowBlur"), ShadowDepth = settings.Number("shadowDistance"), Direction = 270 };
        Width = owner.Width + 2 * margin; Height = owner.Height + 2 * margin;
        canvas.Clip = Geometry.Combine(new RectangleGeometry(new Rect(0, 0, Width, Height)),
            new RectangleGeometry(new Rect(margin, margin, owner.Width, owner.Height), settings.Number("panelRadius"), settings.Number("panelRadius")), GeometryCombineMode.Exclude, null);
        Left = rect.Left / dpi.DpiScaleX - margin; Top = rect.Top / dpi.DpiScaleY - margin;
        nint hwnd = new WindowInteropHelper(this).EnsureHandle();
        Native.SetWindowPos(hwnd, 0, rect.Left - (int)Math.Round(margin * dpi.DpiScaleX), rect.Top - (int)Math.Round(margin * dpi.DpiScaleY),
            (int)Math.Round(Width * dpi.DpiScaleX), (int)Math.Round(Height * dpi.DpiScaleY), 0x14);
        if (settings.Number("shadowOpacity") == 0) Hide();
    }
}

internal sealed class DimmerWindow : Window
{
    private readonly Action outside;
    internal DimmerWindow(Action outside)
    {
        this.outside = outside; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Black;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true; ResizeMode = ResizeMode.NoResize;
        MouseLeftButtonDown += (_, _) => this.outside();
        SourceInitialized += (_, _) =>
        {
            nint handle = new WindowInteropHelper(this).Handle;
            Native.SetWindowLongPtr(handle, -20, Native.GetWindowLongPtr(handle, -20) | 0x80 | 0x08000000);
        };
    }
    internal void SetArea(RectD rect, double opacity)
    {
        var dpi = VisualTreeHelper.GetDpi(this); Left = rect.X / dpi.DpiScaleX; Top = rect.Y / dpi.DpiScaleY;
        Width = rect.Width / dpi.DpiScaleX; Height = rect.Height / dpi.DpiScaleY; Opacity = opacity;
        nint hwnd = new WindowInteropHelper(this).EnsureHandle();
        Native.SetWindowPos(hwnd, 0, (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height, 0x14);
    }
}

