using System.Windows;
using Switcher.Core;

namespace Switcher.Windows;

internal sealed class Thumbnail : IDisposable
{
    private nint handle;
    private readonly nint sourceWindow;
    private Native.NativeSize sourceSize;
    private Native.ThumbnailProperties? lastProperties;
    internal static int ActiveCount { get; private set; }
    internal int LastResult { get; private set; }
    internal bool Live => handle != 0;
    internal Thumbnail(nint destination, WindowInfo source)
    {
        sourceWindow = (nint)source.Handle;
        if (source.Minimized || source.Hung || !Native.Matches(source)) return;
        LastResult = Native.DwmRegisterThumbnail(destination, (nint)source.Handle, out handle);
        if (LastResult < 0) handle = 0;
        if (handle != 0) ++ActiveCount;
        if (handle != 0) LastResult = Native.DwmQueryThumbnailSourceSize(handle, out sourceSize);
    }
    internal void Update(Rect target, Settings settings, byte opacity)
    {
        if (handle == 0 || target.Width < 1 || target.Height < 1) return;
        var size = sourceSize;
        if (LastResult < 0 || size.Width <= 0 || size.Height <= 0) { Hide(); return; }
        double sourceWidth = size.Width, sourceHeight = size.Height;
        var source = new Native.Rect { Right = size.Width, Bottom = size.Height };
        if (settings.Bool("previewClientOnly"))
        {
            if (Native.GetClientRect(sourceWindow, out var client) && client.Right > 0 && client.Bottom > 0)
            { size.Width = client.Right; size.Height = client.Bottom; sourceWidth = size.Width; sourceHeight = size.Height; source = new Native.Rect { Right = size.Width, Bottom = size.Height }; }
        }
        bool crop = settings.String("previewFit") == "cover";
        if (crop)
        {
            double ratio = target.Width / target.Height;
            if (sourceWidth / sourceHeight > ratio)
            {
                int width = (int)(sourceHeight * ratio); source.Left = (size.Width - width) / 2; source.Right = source.Left + width;
            }
            else { int height = (int)(sourceWidth / ratio); source.Top = (size.Height - height) / 2; source.Bottom = source.Top + height; }
        }
        else
        {
            double scale = Math.Min(target.Width / sourceWidth, target.Height / sourceHeight);
            double w = sourceWidth * scale, h = sourceHeight * scale;
            target = new(target.X + (target.Width - w) / 2, target.Y + (target.Height - h) / 2, w, h);
        }
        var properties = new Native.ThumbnailProperties
        {
            Flags = 1 | 4 | 8 | 16 | (crop ? 2u : 0u),
            Destination = new Native.Rect { Left = (int)Math.Round(target.Left), Top = (int)Math.Round(target.Top), Right = (int)Math.Round(target.Right), Bottom = (int)Math.Round(target.Bottom) },
            Source = source, Opacity = opacity, Visible = true, ClientOnly = settings.Bool("previewClientOnly")
        };
        if (lastProperties is { } previous && previous.Equals(properties)) return;
        LastResult = Native.DwmUpdateThumbnailProperties(handle, ref properties); lastProperties = properties;
        if (LastResult < 0) Hide();
    }
    internal void Hide()
    {
        if (handle == 0) return;
        var properties = new Native.ThumbnailProperties { Flags = 8, Visible = false };
        Native.DwmUpdateThumbnailProperties(handle, ref properties);
        lastProperties = null;
    }
    public void Dispose()
    {
        if (handle == 0) return;
        Native.DwmUnregisterThumbnail(handle); handle = 0; --ActiveCount;
    }
}

