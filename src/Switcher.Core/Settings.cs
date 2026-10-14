using System.Globalization;
using System.Text.Json;

namespace Switcher.Core;

public readonly record struct Rgba(byte R, byte G, byte B, byte A)
{
    public static Rgba Parse(string text)
    {
        if (text.Length is not (7 or 9) || text[0] != '#' || text[1..].Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Couleur attendue : #RRGGBB ou #RRGGBBAA.");
        byte Byte(int i) => byte.Parse(text.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new(Byte(1), Byte(3), Byte(5), text.Length == 9 ? Byte(7) : (byte)255);
    }
}

public sealed class Settings
{
    private static readonly JsonElement Schema = Resource("Switcher.manifest.json");
    private static readonly Dictionary<string, JsonElement> Definitions = Schema.GetProperty("settings")
        .EnumerateArray().Where(x => x.GetProperty("type").GetString() != "section")
        .ToDictionary(x => x.GetProperty("id").GetString()!, x => x.Clone());
    private readonly Dictionary<string, JsonElement> values;
    private readonly Dictionary<string, JsonElement> effective;
    private Settings(Dictionary<string, JsonElement> source)
    {
        values = source;
        effective = new(source);
        string profile = String("profile");
        if (profile != "custom")
        {
            foreach (var property in Resource($"Switcher.profiles.{profile}.json").EnumerateObject())
                if (Definitions.ContainsKey(property.Name) && property.Name != "layout")
                    effective[property.Name] = property.Value.Clone();
        }
        if (String("layout") == "profile")
            effective["layout"] = JsonSerializer.SerializeToElement(profile == "custom" ? "grid" :
                Resource($"Switcher.profiles.{profile}.json").GetProperty("layout").GetString());
    }
    private static JsonElement Resource(string name)
    {
        using var stream = typeof(Settings).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Ressource manquante : {name}");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }
    public static int DefinitionCount => Schema.GetProperty("settings").GetArrayLength();
    public static IEnumerable<string> ValueIds => Definitions.Keys;
    public static Settings Defaults => Parse(JsonSerializer.SerializeToElement(new { }));
    public static Settings Parse(JsonElement snapshot)
    {
        if (snapshot.ValueKind != JsonValueKind.Object) throw new ArgumentException("Les réglages doivent être un objet.");
        if (snapshot.TryGetProperty("deviceSettings", out var device)) snapshot = device;
        if (snapshot.ValueKind != JsonValueKind.Object) throw new ArgumentException("deviceSettings doit être un objet.");
        var source = Definitions.ToDictionary(x => x.Key, x => x.Value.GetProperty("default").Clone());
        foreach (var property in snapshot.EnumerateObject())
        {
            if (!Definitions.TryGetValue(property.Name, out var definition))
                throw new ArgumentException($"Réglage inconnu : {property.Name}");
            string type = definition.GetProperty("type").GetString()!;
            bool valid = type switch
            {
                "boolean" => property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "range" => ValidNumber(property.Value, definition),
                "select" => property.Value.ValueKind == JsonValueKind.String && definition.GetProperty("options").EnumerateArray()
                    .Any(x => x.GetProperty("value").GetString() == property.Value.GetString()),
                "color" or "textarea" => property.Value.ValueKind == JsonValueKind.String && property.Value.GetString()!.Length <= 4096,
                _ => false
            };
            if (!valid) throw new ArgumentException($"Valeur incorrecte pour {property.Name}.");
            if (type == "color")
            {
                if (property.Value.GetString()!.Length != 9) throw new ArgumentException($"Couleur alpha attendue pour {property.Name} : #RRGGBBAA.");
                _ = Rgba.Parse(property.Value.GetString()!);
            }
            source[property.Name] = property.Value.Clone();
        }
        return new Settings(source);
    }
    private static bool ValidNumber(JsonElement value, JsonElement definition)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double n) || !double.IsFinite(n)) return false;
        double minimum = definition.GetProperty("min").GetDouble();
        if (n < minimum || n > definition.GetProperty("max").GetDouble()) return false;
        double quotient = (n - minimum) / definition.GetProperty("step").GetDouble();
        return Math.Abs(quotient - Math.Round(quotient)) <= 2.2204460492503131e-16 * Math.Max(1, Math.Abs(quotient)) * 8;
    }
    public bool Bool(string id) => effective[id].GetBoolean();
    public double Number(string id) => effective[id].GetDouble();
    public int Int(string id) => (int)Math.Round(Number(id));
    public string String(string id) => effective[id].GetString()!;
    public Rgba Color(string id) => Rgba.Parse(String(id));
    public string ProfileName => String("profile") == "custom" ? "Personnalisée" :
        Resource($"Switcher.profiles.{String("profile")}.json").GetProperty("name").GetString()!;
    public JsonElement Snapshot => JsonSerializer.SerializeToElement(values);
    public Settings With(params (string Key, object Value)[] changes)
    {
        var copy = new Dictionary<string, JsonElement>(values);
        foreach (var (key, value) in changes) copy[key] = JsonSerializer.SerializeToElement(value);
        return Parse(JsonSerializer.SerializeToElement(copy));
    }
    public HashSet<string> Applications(string id) => String(id).Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Trim()).Where(x => x.Length > 0)
        .Select(x => x.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? x[..^4] : x)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

