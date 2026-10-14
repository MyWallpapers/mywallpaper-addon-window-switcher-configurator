using System.Buffers.Binary;
using System.Text.Json;

namespace Switcher.Core;

// MyWallpaper process-v2 v5: high two bits are chunk type, low thirty bits are length.
public static class Wire
{
    public const int Version = 5;
    public const int MaxChunk = 1024 * 1024;
    public const int MaxRecord = 4 * MaxChunk; // The add-on's own bounded input policy, not a change to the host protocol.
    public static async Task<JsonElement?> ReadAsync(Stream stream, CancellationToken token = default)
    {
        using var record = new MemoryStream();
        bool first = true;
        while (true)
        {
            byte[] header = new byte[4];
            int start = await stream.ReadAsync(header.AsMemory(0, 1), token);
            if (start == 0)
            {
                if (first) return null;
                throw new InvalidDataException("Record interrompu.");
            }
            await stream.ReadExactlyAsync(header.AsMemory(1), token);
            uint raw = BinaryPrimitives.ReadUInt32LittleEndian(header);
            uint kind = raw >> 30;
            int length = (int)(raw & 0x3fffffff);
            if (length is <= 0 or > MaxChunk || record.Length + length > MaxRecord)
                throw new InvalidDataException("Taille de record refusée.");
            if (first ? kind is not (0 or 1) : kind is not (2 or 3))
                throw new InvalidDataException("Ordre des chunks incorrect.");
            byte[] payload = new byte[length];
            await stream.ReadExactlyAsync(payload, token);
            record.Write(payload);
            if (kind is 0 or 3)
            {
                using var json = JsonDocument.Parse(record.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
                return json.RootElement.Clone();
            }
            first = false;
        }
    }
    public static async Task WriteAsync(Stream stream, object value, CancellationToken token = default)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        if (bytes.Length is 0 or > MaxRecord) throw new InvalidDataException("Record sortant trop grand.");
        for (int offset = 0; offset < bytes.Length; offset += MaxChunk)
        {
            int length = Math.Min(MaxChunk, bytes.Length - offset);
            uint kind = bytes.Length <= MaxChunk ? 0u : offset == 0 ? 1u : offset + length == bytes.Length ? 3u : 2u;
            byte[] header = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(header, kind << 30 | (uint)length);
            await stream.WriteAsync(header, token);
            await stream.WriteAsync(bytes.AsMemory(offset, length), token);
        }
        await stream.FlushAsync(token);
    }
    public static string FrameType(JsonElement frame)
    {
        if (frame.ValueKind != JsonValueKind.Object || !frame.TryGetProperty("v", out var version)
            || !version.TryGetInt32(out int v) || v != Version || !frame.TryGetProperty("type", out var type)
            || type.ValueKind != JsonValueKind.String) throw new InvalidDataException("Frame process-v2 v5 attendue.");
        return type.GetString()!;
    }
    public static void ValidateInit(JsonElement frame)
    {
        if (FrameType(frame) != "init") throw new InvalidDataException("Le premier record doit être init.");
        if (frame.GetProperty("targetArchitecture").GetString() != "windows-x86_64") throw new InvalidDataException("Architecture incompatible.");
        string id = frame.GetProperty("addonId").GetString() ?? "";
        if (id.Length is 0 or > 256 || id.Trim() != id || id.Any(char.IsControl)) throw new InvalidDataException("Identité d’add-on incorrecte.");
        string digest = frame.GetProperty("artifactDigest").GetString() ?? "";
        if (digest.Length != 71 || !digest.StartsWith("sha256:", StringComparison.Ordinal) || digest[7..].Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("Empreinte de l’artefact incorrecte.");
        if (string.IsNullOrWhiteSpace(frame.GetProperty("version").GetString())) throw new InvalidDataException("Version manquante.");
        if (frame.GetProperty("layerSettings").ValueKind != JsonValueKind.Object || frame.GetProperty("deviceSettings").ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Snapshots de réglages incorrects.");
        _ = frame.GetProperty("scratchDirectory").GetString();
    }
}

