using System.Buffers.Binary;
using System.Text;

namespace UniHome.Infrastructure.Analysis;

/// <summary>
/// Lector mínimo de metadatos EXIF (tag Software/ProcessingSoftware) y XMP de
/// JPEG/PNG. No usa una librería con licencia comercial ni expone los metadatos
/// completos: solo devuelve señales orientativas.
/// </summary>
internal static class ImageMetadataInspector
{
    private static readonly string[] EditingTools =
    [
        "photoshop", "gimp", "lightroom", "affinity", "pixelmator", "paintshop",
        "canva", "illustrator", "inkscape", "snapseed", "picsart", "fotor"
    ];

    public static (bool Available, IReadOnlyList<string> Signals, string? Detail) Inspect(
        byte[] content)
    {
        var signals = new List<string>();
        var available = false;

        foreach (var software in ReadExifSoftware(content))
        {
            available = true;
            AddToolSignal(signals, software);
        }

        var xmp = ReadXmpPacket(content);

        if (!string.IsNullOrWhiteSpace(xmp))
        {
            available = true;
            AddToolSignal(signals, xmp);
        }

        return (
            available,
            signals,
            available ? null : "La imagen no expone metadatos EXIF/XMP analizables.");
    }

    private static IEnumerable<string> ReadExifSoftware(byte[] content)
    {
        var tiff = TryGetTiff(content);

        if (tiff is null)
        {
            yield break;
        }

        var values = ReadTiffSoftware(tiff);

        foreach (var value in values)
        {
            yield return value;
        }
    }

    private static byte[]? TryGetTiff(byte[] content)
    {
        if (content.Length >= 4 &&
            content[0] == 0xFF &&
            content[1] == 0xD8)
        {
            var offset = 2;

            while (offset + 4 <= content.Length)
            {
                if (content[offset] != 0xFF)
                {
                    offset++;
                    continue;
                }

                var marker = content[offset + 1];

                if (marker is 0xD8 or 0xD9)
                {
                    offset += 2;
                    continue;
                }

                if (offset + 4 > content.Length)
                {
                    return null;
                }

                var length = BinaryPrimitives.ReadUInt16BigEndian(content.AsSpan(offset + 2, 2));

                if (length < 2 || offset + 2 + length > content.Length)
                {
                    return null;
                }

                if (marker == 0xE1 &&
                    length >= 8 &&
                    content.AsSpan(offset + 4, 6).SequenceEqual("Exif\0\0"u8))
                {
                    return content.AsSpan(offset + 10, length - 8).ToArray();
                }

                offset += 2 + length;
            }

            return null;
        }

        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        if (content.Length > png.Length && content.AsSpan(0, png.Length).SequenceEqual(png))
        {
            var offset = png.Length;

            while (offset + 12 <= content.Length)
            {
                var length = BinaryPrimitives.ReadInt32BigEndian(content.AsSpan(offset, 4));

                if (length < 0 || offset + 12 + length > content.Length)
                {
                    return null;
                }

                var type = content.AsSpan(offset + 4, 4);

                if (type.SequenceEqual("eXIf"u8))
                {
                    return content.AsSpan(offset + 8, length).ToArray();
                }

                offset += 12 + length;
            }
        }

        return null;
    }

    internal static List<string> ReadTiffSoftware(byte[] tiff)
    {
        if (tiff.Length < 8)
        {
            return [];
        }

        var littleEndian = tiff[0] == 'I' && tiff[1] == 'I';
        var bigEndian = tiff[0] == 'M' && tiff[1] == 'M';

        if (!littleEndian && !bigEndian)
        {
            return [];
        }

        var magic = ReadUInt16(tiff, 2, littleEndian);

        if (magic != 42)
        {
            return [];
        }

        var ifdOffset = ReadUInt32(tiff, 4, littleEndian);

        if (ifdOffset < 8 || ifdOffset + 2 > tiff.Length)
        {
            return [];
        }

        var count = ReadUInt16(tiff, (int)ifdOffset, littleEndian);
        var result = new List<string>();

        for (var index = 0; index < count; index++)
        {
            var entry = (int)ifdOffset + 2 + (index * 12);

            if (entry + 12 > tiff.Length)
            {
                break;
            }

            var tag = ReadUInt16(tiff, entry, littleEndian);

            if (tag is not (0x0131 or 0x000B))
            {
                continue;
            }

            var type = ReadUInt16(tiff, entry + 2, littleEndian);
            var valueCount = ReadUInt32(tiff, entry + 4, littleEndian);

            if (type != 2 || valueCount is 0 or > 512)
            {
                continue;
            }

            var valueOffset = valueCount <= 4
                ? entry + 8
                : (int)ReadUInt32(tiff, entry + 8, littleEndian);

            if (valueOffset < 0 || valueOffset + valueCount > tiff.Length)
            {
                continue;
            }

            var text = Encoding.ASCII.GetString(tiff, valueOffset, (int)valueCount)
                .Trim('\0', ' ', '\r', '\n');

            if (text.Length > 0)
            {
                result.Add(text);
            }
        }

        return result;
    }

    private static string? ReadXmpPacket(byte[] content)
    {
        var start = content.AsSpan().IndexOf("http://ns.adobe.com/xap/1.0/"u8);

        if (start < 0)
        {
            return null;
        }

        var length = Math.Min(content.Length - start, 256 * 1024);
        var packet = Encoding.UTF8.GetString(content, start, length);
        var end = packet.IndexOf("</x:xmpmeta>", StringComparison.OrdinalIgnoreCase);
        return end > 0 ? packet[..(end + 12)] : packet;
    }

    private static void AddToolSignal(List<string> signals, string text)
    {
        var tool = DetectEditingTool(text);

        if (tool is not null)
        {
            signals.Add($"Metadatos indican software de edición: {tool}.");
        }
    }

    internal static string? DetectEditingTool(string text) =>
        EditingTools.FirstOrDefault(candidate =>
            text.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private static ushort ReadUInt16(byte[] data, int offset, bool littleEndian) =>
        littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2))
            : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));

    private static uint ReadUInt32(byte[] data, int offset, bool littleEndian) =>
        littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4))
            : BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
}
