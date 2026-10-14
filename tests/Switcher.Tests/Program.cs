using System.Buffers.Binary;
using System.Text.Json;
using Switcher.Core;

int checks = 0;
void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
void Reject(Action action, string why)
{
    checks++;
    try { action(); } catch (ArgumentException) { return; }
    throw new Exception(why);
}
Settings Parse(string json) { using var doc = JsonDocument.Parse(json); return Settings.Parse(doc.RootElement); }
WindowInfo Window(long handle, string app = "notepad", string title = "Document", long monitor = 1) =>
    new(handle, (uint)handle + 100, 1000, title, app, "Normal", monitor, (int)monitor, new(0, 0, 1280, 800));

var defaults = Settings.Defaults;
Check(Settings.DefinitionCount == 92 && Settings.DefinitionCount <= 100, "Definition budget includes sections.");
Check(Settings.ValueIds.Count() == 83, "Values must match the manifest.");
Check(defaults.ProfileName == "Verre boréal" && defaults.String("layout") == "grid", "Recommended profile.");
Check(Rgba.Parse("#10203040") == new Rgba(16, 32, 48, 64), "MyWallpaper colors are alpha-last.");
Reject(() => Parse("{\"surfaceColor\":\"#wrong\"}"), "Malformed colors.");
Reject(() => Parse("{\"gap\":1000}"), "Oversized range.");
Reject(() => Parse("{\"enabled\":1}"), "Wrong boolean.");
Reject(() => Parse("{\"profile\":\"absent\"}"), "Unknown preset.");
Reject(() => Parse("{\"unknown\":true}"), "Unknown setting.");
Check(Parse("{\"deviceSettings\":{\"profile\":\"paper\"}}").String("layout") == "list", "Device-scoped snapshot.");
Check(Parse("{\"gap\":5}").Int("gap") == 5 && Parse("{}").Int("gap") == 16, "Each settings update is a full snapshot, not a patch.");
foreach (string profile in new[] { "boreal", "midnight", "rose", "paper", "cinema", "classic", "flip" })
{
    var sample = defaults.With(("profile", profile), ("surfaceColor", "#11223344"), ("gap", 23));
    Check(sample.Int("gap") == 23, "Presets preserve interaction geometry.");
    Check(sample.With(("profile", "custom")).Color("surfaceColor") == new Rgba(17, 34, 51, 68), "Personal palette survives preset switches.");
    foreach (string color in new[] { "surfaceColor", "cardColor", "textColor", "accentColor", "mutedColor" }) _ = sample.Color(color);
}

var context = new FilterContext(1, 2);
var windows = new List<WindowInfo> { Window(1), Window(2, "editor", "Projet", 2), Window(3, "editor", "Fenêtre réduite") with { Minimized = true },
    Window(4) with { Cloaked = true }, Window(5) with { ToolWindow = true }, Window(6) with { Owner = 1 },
    Window(7) with { ClassName = "Ghost" }, Window(8) with { Bounds = new(0, 0, 4, 4) }, Window(9, title: ""),
    Window(10) with { Hung = true }, Window(11) with { ClassName = "Shell_TrayWnd" } };
Check(WindowFilter.Apply(windows, defaults, context).Select(x => x.Handle).SequenceEqual(new long[] { 1, 2, 3, 10 }), "Exclude shell, cloaked, tool, owned, ghost and tiny windows.");
Check(WindowFilter.Apply(windows, defaults.With(("windowScreen", "focused")), context).Single().Handle == 2, "Focused monitor filter.");
Check(WindowFilter.Apply(windows, defaults.With(("minimized", "only")), context).Single().Handle == 3, "Minimized-only filter.");
Check(WindowFilter.Apply(windows, defaults.With(("hideHung", true)), context).Count == 3, "Unresponsive filter is optional.");
Check(WindowFilter.Apply(windows, defaults.With(("includeApplications", "EDITOR.EXE"), ("excludeApplications", "notepad.exe")), context).Count == 2, "Case-insensitive executable allow/deny lists.");
Check(WindowFilter.Apply(windows, defaults, context, "projet").Single().Handle == 2, "Search by title.");
Check(WindowFilter.Apply(windows, defaults, context, "editor").Count == 2, "Search by application.");
Check(WindowFilter.Apply([Window(5), Window(6) with { AppWindow = true, ToolWindow = true, Owner = 5 }], defaults, context).Count == 2, "Explicit app windows take precedence over tool/owner flags.");

var session = new SwitchSession();
Check(session.Begin([Window(1), Window(2), Window(3)], 1, true, 1) && session.Current!.Handle == 2, "First Alt+Tab selects the next window.");
session.Move(-1, true); Check(session.Current!.Handle == 1, "Reverse selection.");
session.Move(-1, true); Check(session.Current!.Handle == 3, "Wrap to last.");
session.Move(7, false); Check(session.Current!.Handle == 3, "Clamp without wrap.");
session.Refresh([Window(1), Window(3)]); Check(session.Current!.Handle == 3, "Refresh keeps the selected HWND.");
session.Refresh([]); Check(session.Current is null, "No stale selection after filter empties.");
session.End(); Check(!session.Active && !session.HoldAlt && session.Windows.Count == 0, "Stop releases session state.");
Check(session.Begin([Window(1), Window(2)], 1, true, -1) && session.Current!.Handle == 2, "Reverse initial Alt+Tab.");
session.MakeSticky(); Check(!session.HoldAlt && session.Active && session.Current!.Handle == 2, "Close confirmation becomes sticky without changing selection.");

var many = Enumerable.Range(1, 230).Select(i => Window(i) with { Bounds = new(i % 8 * 210 - 1920, i % 4 * 160, 800, 600) }).ToList();
foreach (string mode in new[] { "grid", "list", "classic", "flip", "desktop" })
foreach (var size in new[] { (Width: 320.0, Height: 240.0), (Width: 1200.0, Height: 680.0) })
foreach (int selected in new[] { 0, 112, 229 })
{
    var configuration = defaults.With(("layout", mode), ("columns", 8), ("gap", 40), ("density", "airy"));
    var plan = Layout.Compute(many, selected, configuration, size.Width, size.Height, new(-1920, 0, 3840, 1080));
    Check(plan.Cards.Any(c => c.Index == selected), $"Selected card visible in {mode}.");
    foreach (var card in plan.Cards)
    {
        Check(card.Bounds.Width > 0 && card.Bounds.Height > 0 && card.Preview.Width >= 0 && card.Preview.Height >= 0, $"Positive geometry in {mode}.");
        Check(card.Bounds.X >= 0 && card.Bounds.Y >= 0 && card.Bounds.Right <= plan.Width + .01 && card.Bounds.Bottom <= plan.Height + .01, $"Card fits canvas in {mode}.");
    }
    Check(plan.Width <= size.Width + .01 && plan.Height <= size.Height + .01, $"Layout fits screen in {mode}.");
}

foreach (int payloadLength in new[] { 100, Wire.MaxChunk + 300, Wire.MaxChunk * 2 + 4 })
{
    using var stream = new MemoryStream();
    await Wire.WriteAsync(stream, new { type = "message", v = 5, data = new string('a', payloadLength) });
    stream.Position = 0;
    var read = (await Wire.ReadAsync(stream))!.Value;
    Check(read.GetProperty("data").GetString()!.Length == payloadLength, "Single and multi-chunk records round trip.");
}
async Task RefuseWire(byte[] bytes)
{
    checks++;
    try { await Wire.ReadAsync(new MemoryStream(bytes)); }
    catch (Exception e) when (e is InvalidDataException or EndOfStreamException or JsonException) { return; }
    throw new Exception("Malformed wire record accepted.");
}
byte[] Chunk(uint kind, string text)
{
    var payload = System.Text.Encoding.UTF8.GetBytes(text); byte[] result = new byte[4 + payload.Length];
    BinaryPrimitives.WriteUInt32LittleEndian(result, kind << 30 | (uint)payload.Length); payload.CopyTo(result, 4); return result;
}
await RefuseWire(Chunk(2, "{}")); await RefuseWire(Chunk(3, "{}"));
await RefuseWire(Chunk(1, "{").Concat(Chunk(0, "}")).ToArray());
await RefuseWire(Chunk(1, "{}")); await RefuseWire([1, 0]); await RefuseWire([0, 0, 0, 0]);
await RefuseWire(Chunk(0, "{"));
Console.WriteLine($"PASS: {checks} checks (settings, profiles, filters, navigation, 5 layouts, wire framing)");

