using System.Buffers.Binary;
using System.Text;
using UniHome.Infrastructure.Analysis;

namespace UniHome.UnitTests.Infrastructure;

public sealed class ImageMetadataInspectorTests
{
    [Fact]
    public void TiffLittleEndian_ConSoftware_DebeLeerElValor()
    {
        var tiff = BuildTiff("Adobe Photoshop 25.0", littleEndian: true);

        var values = ImageMetadataInspector.ReadTiffSoftware(tiff);

        Assert.Equal(["Adobe Photoshop 25.0"], values);
    }

    [Fact]
    public void TiffBigEndian_ConProcessingSoftware_DebeLeerElValor()
    {
        var tiff = BuildTiff(
            "GIMP 2.10",
            littleEndian: false,
            tag: 0x000B);

        var values = ImageMetadataInspector.ReadTiffSoftware(tiff);

        Assert.Equal(["GIMP 2.10"], values);
    }

    [Fact]
    public void TiffConValorInline_DebeLeerloSinDesbordar()
    {
        var tiff = BuildTiff("G\0", inline: true);

        var values = ImageMetadataInspector.ReadTiffSoftware(tiff);

        Assert.Equal(["G"], values);
    }

    [Fact]
    public void TiffSinSoftwareOMalFormado_DebeDevolverVacio()
    {
        Assert.Empty(ImageMetadataInspector.ReadTiffSoftware([]));
        Assert.Empty(ImageMetadataInspector.ReadTiffSoftware(Encoding.ASCII.GetBytes("XX******")));
        Assert.Empty(ImageMetadataInspector.ReadTiffSoftware(BuildTiff("Photoshop", magic: 41)));
        Assert.Empty(ImageMetadataInspector.ReadTiffSoftware(BuildTiff("Photoshop", ifdOffset: 0)));
        Assert.Empty(ImageMetadataInspector.ReadTiffSoftware(BuildTiff("Photoshop", tag: 0x010F)));
        Assert.Empty(ImageMetadataInspector.ReadTiffSoftware(BuildTiff("Photoshop", type: 3)));
        Assert.Empty(ImageMetadataInspector.ReadTiffSoftware(BuildTiff("Photoshop", declaredCount: 0)));
        Assert.Empty(ImageMetadataInspector.ReadTiffSoftware(BuildTiff("Photoshop", declaredCount: 513)));
        Assert.Empty(ImageMetadataInspector.ReadTiffSoftware(BuildTiff("Photoshop")[..12]));
    }

    [Fact]
    public void TiffConOffsetFueraDeRangoODatoVacio_DebeIgnorarlo()
    {
        var outside = BuildTiff("Photoshop");
        BinaryPrimitives.WriteUInt32LittleEndian(outside.AsSpan(18, 4), 0xFFFF);

        Assert.Empty(ImageMetadataInspector.ReadTiffSoftware(outside));
        Assert.Empty(ImageMetadataInspector.ReadTiffSoftware(BuildTiff("\0", inline: true)));
    }

    [Fact]
    public void JpegConExif_DebeDetectarHerramientaYMetadatosDisponibles()
    {
        var jpeg = WrapJpegExif(BuildTiff("Adobe Photoshop 25.0"));

        var result = ImageMetadataInspector.Inspect(jpeg);

        Assert.True(result.Available);
        Assert.Contains(result.Signals, signal =>
            signal.Contains("photoshop", StringComparison.OrdinalIgnoreCase));
        Assert.Null(result.Detail);
    }

    [Fact]
    public void JpegConExifSinHerramientaConocida_DebeMarcarMetadatosSinSenal()
    {
        var jpeg = WrapJpegExif(BuildTiff("Microsoft Word"));

        var result = ImageMetadataInspector.Inspect(jpeg);

        Assert.True(result.Available);
        Assert.Empty(result.Signals);
    }

    [Fact]
    public void PngConChunkExif_DebeDetectarHerramienta()
    {
        var png = WrapPngExif(BuildTiff("GIMP 2.10"));

        var result = ImageMetadataInspector.Inspect(png);

        Assert.True(result.Available);
        Assert.Contains(result.Signals, signal =>
            signal.Contains("gimp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void XmpConCierre_DebeDetectarHerramienta()
    {
        var xmp = Encoding.UTF8.GetBytes(
            "<x:xmpmeta>http://ns.adobe.com/xap/1.0/ Adobe Photoshop</x:xmpmeta>");

        var result = ImageMetadataInspector.Inspect(xmp);

        Assert.True(result.Available);
        Assert.Contains(result.Signals, signal =>
            signal.Contains("photoshop", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void XmpSinCierre_DebeConservarElPaqueteYDetectarHerramienta()
    {
        var xmp = Encoding.UTF8.GetBytes(
            "http://ns.adobe.com/xap/1.0/ Affinity Photo");

        var result = ImageMetadataInspector.Inspect(xmp);

        Assert.True(result.Available);
        Assert.Contains(result.Signals, signal =>
            signal.Contains("affinity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void JpegOPngMalFormados_DebenQuedarNoDisponibles()
    {
        var invalidJpeg = new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xE1, 0x00, 0x01, 0xFF, 0xD9
        };
        var invalidPng = new List<byte>
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0xFF, 0xFF, 0xFF, 0xFF,
            (byte)'e', (byte)'X', (byte)'I', (byte)'f'
        };

        Assert.False(ImageMetadataInspector.Inspect(invalidJpeg).Available);
        Assert.False(ImageMetadataInspector.Inspect(invalidPng.ToArray()).Available);
        Assert.False(ImageMetadataInspector.Inspect([]).Available);
    }

    [Fact]
    public void JpegSinExifONoExif_DebeQuedarNoDisponible()
    {
        var markersOnly = new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xD9, 0x00, 0x01, 0xFF, 0xD8
        };
        var app1NoExif = new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xE1, 0x00, 0x08, (byte)'N', (byte)'o',
            (byte)'t', (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0xFF, 0xD9
        };

        Assert.False(ImageMetadataInspector.Inspect(markersOnly).Available);
        Assert.False(ImageMetadataInspector.Inspect(app1NoExif).Available);
    }

    [Fact]
    public void PngSinExifONoExif_DebeQuedarNoDisponible()
    {
        var header = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        };
        var otherChunk = new List<byte>(header)
        {
            0x00, 0x00, 0x00, 0x04,
            (byte)'I', (byte)'H', (byte)'D', (byte)'R',
            0x00, 0x00, 0x00, 0x00
        };
        var truncatedExif = new List<byte>(header)
        {
            0x00, 0x00, 0x01, 0x00,
            (byte)'e', (byte)'X', (byte)'I', (byte)'f'
        };

        Assert.False(ImageMetadataInspector.Inspect(otherChunk.ToArray()).Available);
        Assert.False(ImageMetadataInspector.Inspect(truncatedExif.ToArray()).Available);
    }

    private static byte[] BuildTiff(
        string software,
        bool littleEndian = true,
        ushort tag = 0x0131,
        ushort type = 2,
        bool inline = false,
        uint? declaredCount = null,
        ushort magic = 42,
        uint ifdOffset = 8)
    {
        var value = Encoding.ASCII.GetBytes(software + (software.EndsWith('\0') ? string.Empty : "\0"));
        var dataOffset = 26;
        var buffer = new byte[dataOffset + (inline ? 0 : value.Length)];
        buffer[0] = littleEndian ? (byte)'I' : (byte)'M';
        buffer[1] = littleEndian ? (byte)'I' : (byte)'M';
        WriteUInt16(buffer, 2, magic, littleEndian);
        WriteUInt32(buffer, 4, ifdOffset, littleEndian);

        if (ifdOffset == 8 && buffer.Length >= 10)
        {
            WriteUInt16(buffer, 8, 1, littleEndian);
            WriteUInt16(buffer, 10, tag, littleEndian);
            WriteUInt16(buffer, 12, type, littleEndian);
            WriteUInt32(buffer, 14, declaredCount ?? (uint)value.Length, littleEndian);

            if (inline)
            {
                value.AsSpan(0, Math.Min(4, value.Length)).CopyTo(buffer.AsSpan(18));
            }
            else
            {
                WriteUInt32(buffer, 18, (uint)dataOffset, littleEndian);
                value.CopyTo(buffer, dataOffset);
            }
        }

        return buffer;
    }

    private static byte[] WrapJpegExif(byte[] tiff)
    {
        var payload = new byte[6 + tiff.Length];
        "Exif\0\0"u8.CopyTo(payload);
        tiff.CopyTo(payload, 6);
        var length = payload.Length + 2;
        var jpeg = new byte[2 + 4 + payload.Length + 2];
        jpeg[0] = 0xFF;
        jpeg[1] = 0xD8;
        jpeg[2] = 0xFF;
        jpeg[3] = 0xE1;
        BinaryPrimitives.WriteUInt16BigEndian(jpeg.AsSpan(4, 2), (ushort)length);
        payload.CopyTo(jpeg, 6);
        jpeg[^2] = 0xFF;
        jpeg[^1] = 0xD9;
        return jpeg;
    }

    private static byte[] WrapPngExif(byte[] tiff)
    {
        var png = new byte[8 + 12 + tiff.Length];
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(png);
        BinaryPrimitives.WriteInt32BigEndian(png.AsSpan(8, 4), tiff.Length);
        "eXIf"u8.CopyTo(png.AsSpan(12, 4));
        tiff.CopyTo(png, 16);
        return png;
    }

    private static void WriteUInt16(
        byte[] data,
        int offset,
        ushort value,
        bool littleEndian)
    {
        if (littleEndian)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(offset, 2), value);
        }
    }

    private static void WriteUInt32(
        byte[] data,
        int offset,
        uint value,
        bool littleEndian)
    {
        if (littleEndian)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset, 4), value);
        }
    }
}
