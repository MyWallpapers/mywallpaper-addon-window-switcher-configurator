namespace Switcher.Core;

public sealed record CardLayout(int Index, RectD Bounds, RectD Preview, bool LivePreview, int Depth = 0);
public sealed record LayoutPlan(double Width, double Height, int Columns, int Page, int Pages, IReadOnlyList<CardLayout> Cards);

public static class Layout
{
    public static LayoutPlan Compute(IReadOnlyList<WindowInfo> windows, int selected, Settings settings,
        double maxWidth, double maxHeight, RectD desktop)
    {
        maxWidth = Math.Max(240, maxWidth); maxHeight = Math.Max(180, maxHeight);
        string mode = settings.String("layout");
        double gap = settings.Number("gap") * (settings.String("density") switch { "compact" => .65, "airy" => 1.35, _ => 1 });
        double aspect = settings.String("previewAspect") switch
        {
            "square" => 1,
            "natural" when windows.Count > 0 => Math.Clamp(windows[Math.Clamp(selected, 0, windows.Count - 1)].Bounds.Width /
                Math.Max(1, windows[Math.Clamp(selected, 0, windows.Count - 1)].Bounds.Height), .7, 2.5),
            _ => 1.6
        };
        double textHeight = Math.Max(54, settings.Number("titleFontSize") + settings.Number("appFontSize") + 25);
        if (mode == "flip")
        {
            int count = Math.Min(windows.Count, settings.Int("flipVisible"));
            double depth = Math.Min(settings.Number("flipDepth"), Math.Max(12, maxHeight / (count + 4)));
            double width = Math.Min(settings.Number("cardWidth") * 1.65, Math.Max(140, maxWidth - depth * (count - 1)));
            double height = Math.Min(width / aspect + textHeight, Math.Max(textHeight + 50, maxHeight - depth * (count - 1)));
            var cards = new List<CardLayout>();
            for (int d = count - 1; d >= 0; --d)
            {
                int index = (selected + d) % windows.Count;
                var rect = new RectD(d * depth, 0, width, height);
                // Behind cards expose their lower caption; only the front rectangle has a live DWM thumbnail.
                rect = rect with { Y = d * depth };
                cards.Add(new(index, rect, new(rect.X + 8, rect.Y + 8, width - 16, height - textHeight - 16), d == 0, d));
            }
            return new(width + depth * Math.Max(0, count - 1), height + depth * Math.Max(0, count - 1), 1, 0, 1, cards);
        }
        if (mode == "desktop")
        {
            int pageSize = settings.Int("pageSize"), page = windows.Count == 0 ? 0 : selected / pageSize;
            double height = Math.Min(maxHeight, maxWidth * desktop.Height / Math.Max(1, desktop.Width));
            var cards = new List<CardLayout>();
            for (int i = page * pageSize; i < Math.Min(windows.Count, (page + 1) * pageSize); ++i)
            {
                var window = windows[i];
                double width = Math.Clamp(window.Bounds.Width / Math.Max(1, desktop.Width) * maxWidth, 120, maxWidth);
                double h = Math.Clamp(window.Bounds.Height / Math.Max(1, desktop.Height) * height, textHeight + 46, Math.Max(textHeight + 46, height));
                double x = Math.Clamp((window.Bounds.X - desktop.X) / Math.Max(1, desktop.Width) * maxWidth, 0, Math.Max(0, maxWidth - width));
                double y = Math.Clamp((window.Bounds.Y - desktop.Y) / Math.Max(1, desktop.Height) * height, 0, Math.Max(0, height - h));
                var rect = new RectD(x, y, width, h);
                cards.Add(new(i, rect, new(x + 8, y + 8, width - 16, h - textHeight - 16), i == selected));
            }
            return new(maxWidth, Math.Max(height, textHeight + 46), 1, page, Math.Max(1, (windows.Count + pageSize - 1) / pageSize),
                cards.OrderBy(c => c.Index == selected).ToList());
        }
        bool list = mode == "list", classic = mode == "classic";
        double desiredWidth = classic ? Math.Max(220, settings.Number("cardWidth")) : settings.Number("cardWidth");
        int columns = list ? 1 : settings.Int("columns") == 0 ? Math.Max(1, (int)((maxWidth + gap) / (desiredWidth + gap))) : settings.Int("columns");
        columns = Math.Max(1, Math.Min(columns, Math.Max(1, windows.Count)));
        columns = Math.Min(columns, Math.Max(1, (int)((maxWidth + gap) / (96 + gap))));
        double cardWidth = list ? maxWidth : Math.Min(desiredWidth, (maxWidth - gap * (columns - 1)) / columns);
        double cardHeight = list ? (settings.Bool("listPreview") ? Math.Max(86, textHeight + 22) : textHeight)
            : classic ? Math.Max(94, 2 * settings.Number("titleFontSize") * 1.35 + settings.Number("appFontSize") * 1.4 + 24) : cardWidth / aspect + textHeight;
        cardHeight = Math.Min(cardHeight, maxHeight);
        int rows = Math.Max(1, (int)((maxHeight + gap) / (cardHeight + gap)));
        int capacity = Math.Max(1, Math.Min(settings.Int("pageSize"), rows * columns));
        int currentPage = windows.Count == 0 ? 0 : selected / capacity;
        int visible = Math.Min(capacity, Math.Max(0, windows.Count - currentPage * capacity));
        var result = new List<CardLayout>();
        for (int n = 0; n < visible; ++n)
        {
            double x = n % columns * (cardWidth + gap), y = n / columns * (cardHeight + gap);
            RectD preview = list ? new(x + 8, y + 8, settings.Bool("listPreview") ? 102 : 0, cardHeight - 16)
                : classic ? new(x + 8, y + 8, 0, 0) : new(x + 8, y + 8, Math.Max(0, cardWidth - 16), Math.Max(0, cardHeight - textHeight - 16));
            result.Add(new(currentPage * capacity + n, new(x, y, cardWidth, cardHeight), preview, preview.Width > 0));
        }
        return new(columns * cardWidth + gap * (columns - 1), Math.Max(80, Math.Ceiling(visible / (double)columns) * (cardHeight + gap) - gap),
            columns, currentPage, Math.Max(1, (windows.Count + capacity - 1) / capacity), result);
    }
}

