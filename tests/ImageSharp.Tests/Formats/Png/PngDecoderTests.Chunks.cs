// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers.Binary;
using System.IO;
using System.Text;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Png
{
    [Trait("Format", "Png")]
    public partial class PngDecoderTests
    {
        [Fact]
        public void CorruptCompressedText_IsIgnoredWithoutLosingImageData()
        {
            // A valid zlib header followed by the reserved DEFLATE block type.
            byte[] payload = { 107, 0, 0, 0x78, 0x9C, 0x07, 0, 0, 0, 0 };
            byte[] type = Encoding.ASCII.GetBytes("zTXt");
            byte[] crcData = new byte[type.Length + payload.Length];
            type.CopyTo(crcData, 0);
            payload.CopyTo(crcData, type.Length);
            using var stream = new MemoryStream();
            stream.Write(Raw1X1PngIhdrAndpHYs, 0, Raw1X1PngIhdrAndpHYs.Length);
            byte[] length = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, payload.Length);
            stream.Write(length, 0, 4);
            stream.Write(crcData, 0, crcData.Length);
            BinaryPrimitives.WriteUInt32BigEndian(length, SixLabors.ImageSharp.Compression.Zlib.Crc32.Calculate(crcData));
            stream.Write(length, 0, 4);
            stream.Write(Raw1X1PngIdatAndIend, 0, Raw1X1PngIdatAndIend.Length);
            byte[] data = stream.ToArray();

            using Image<Rgba32> image = Image.Load<Rgba32>(data);
            Assert.Empty(image.Metadata.GetPngMetadata().TextData);
            Assert.Equal(new Size(1, 1), image.Size());
            Assert.Empty(Image.Identify(data).Metadata.GetPngMetadata().TextData);
        }

        [Fact]
        public void DuplicateHeader_IsRejectedByDecodeAndIdentify()
        {
            using var stream = new MemoryStream();
            stream.Write(Raw1X1PngIhdrAndpHYs, 0, 33);
            stream.Write(Raw1X1PngIhdrAndpHYs, 8, 25);
            stream.Write(Raw1X1PngIdatAndIend, 0, Raw1X1PngIdatAndIend.Length);
            byte[] data = stream.ToArray();

            Assert.Throws<InvalidImageContentException>(() => Image.Load(data));
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(data));
        }

        [Theory]
        [InlineData(536870912, 8, 4)]
        [InlineData(int.MaxValue, 16, 8)]
        [InlineData(int.MaxValue, 8, 1)]
        public void ScanlineLength_ExceedingSupportedLength_Throws(int width, int bitDepth, int bytesPerPixel)
            => Assert.Throws<InvalidImageContentException>(() => PngDecoderCore.CalculateScanlineLength(width, bitDepth, bytesPerPixel));

        [Theory]
        [InlineData(536870912, 8, 1, 536870912)]
        [InlineData(9, 1, 1, 2)]
        [InlineData(3, 16, 8, 24)]
        public void ScanlineLength_UsesWideArithmetic(int width, int bitDepth, int bytesPerPixel, int expected)
            => Assert.Equal(expected, PngDecoderCore.CalculateScanlineLength(width, bitDepth, bytesPerPixel));

        // Represents ASCII string of "123456789"
        private readonly byte[] check = { 49, 50, 51, 52, 53, 54, 55, 56, 57 };

        // Contains the png marker, IHDR and pHYs chunks of a 1x1 pixel 32bit png 1 a single black pixel.
        private static readonly byte[] Raw1X1PngIhdrAndpHYs =
            {
                // PNG Identifier
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,

                // IHDR
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00,
                0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02,
                0x00, 0x00, 0x00,

                // IHDR CRC
                0x90, 0x77, 0x53, 0xDE,

                // pHYS
                0x00, 0x00, 0x00, 0x09, 0x70, 0x48, 0x59, 0x73, 0x00,
                0x00, 0x0E, 0xC3, 0x00, 0x00, 0x0E, 0xC3, 0x01,

                // pHYS CRC
                0xC7, 0x6F, 0xA8, 0x64
            };

        // Contains the png marker, IDAT and IEND chunks of a 1x1 pixel 32bit png 1 a single black pixel.
        private static readonly byte[] Raw1X1PngIdatAndIend =
            {
                // IDAT
                0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x18,
                0x57, 0x63, 0x60, 0x60, 0x60, 0x00, 0x00, 0x00, 0x04,
                0x00, 0x01,

                // IDAT CRC
                0x5C, 0xCD, 0xFF, 0x69,

                // IEND
                0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,

                // IEND CRC
                0xAE, 0x42, 0x60, 0x82
            };

        [Theory]
        [InlineData((uint)PngChunkType.Header)] // IHDR
        [InlineData((uint)PngChunkType.Palette)] // PLTE
        /* [InlineData(PngChunkTypes.Data)] TODO: Figure out how to test this */
        public void Decode_IncorrectCRCForCriticalChunk_ExceptionIsThrown(uint chunkType)
        {
            string chunkName = GetChunkTypeName(chunkType);

            using (var memStream = new MemoryStream())
            {
                WriteHeaderChunk(memStream);
                WriteChunk(memStream, chunkName);
                WriteDataChunk(memStream);

                var decoder = new PngDecoder();

                ImageFormatException exception =
                    Assert.Throws<InvalidImageContentException>(() => decoder.Decode<Rgb24>(Configuration.Default, memStream, default));

                Assert.Equal($"CRC Error. PNG {chunkName} chunk is corrupt!", exception.Message);
            }
        }

        private static string GetChunkTypeName(uint value)
        {
            var data = new byte[4];

            BinaryPrimitives.WriteUInt32BigEndian(data, value);

            return Encoding.ASCII.GetString(data);
        }

        private static void WriteHeaderChunk(MemoryStream memStream)
        {
            // Writes a 1x1 32bit png header chunk containing a single black pixel.
            memStream.Write(Raw1X1PngIhdrAndpHYs, 0, Raw1X1PngIhdrAndpHYs.Length);
        }

        private static void WriteChunk(MemoryStream memStream, string chunkName)
        {
            // Needs a minimum length of 9 for pHYs chunk.
            memStream.Write(new byte[] { 0, 0, 0, 9 }, 0, 4);
            memStream.Write(Encoding.ASCII.GetBytes(chunkName), 0, 4); // 4 bytes chunk header
            memStream.Write(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 }, 0, 9); // 9 bytes of chunk data
            memStream.Write(new byte[] { 0, 0, 0, 0 }, 0, 4); // Junk Crc
        }

        private static void WriteDataChunk(MemoryStream memStream)
        {
            // Writes a 1x1 32bit png data chunk containing a single black pixel.
            memStream.Write(Raw1X1PngIdatAndIend, 0, Raw1X1PngIdatAndIend.Length);
            memStream.Position = 0;
        }
    }
}
