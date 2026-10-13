// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.WebP
{
    public class WebpMetadataBoundsTests
    {
        [Theory]
        [InlineData("EXIF", false)]
        [InlineData("EXIF", true)]
        [InlineData("XMP ", false)]
        [InlineData("XMP ", true)]
        public void TruncatedTrailingMetadata_PreservesDecodedPixels(string type, bool ignoreMetadata)
        {
            using var image = new Image<Rgba32>(1, 1);
            image[0, 0] = new Rgba32(20, 40, 60);
            image.Metadata.XmpProfile = new SixLabors.ImageSharp.Metadata.Profiles.Xmp.XmpProfile(Encoding.UTF8.GetBytes("<x/>"));
            using var output = new MemoryStream();
            image.Save(output, new WebpEncoder { FileFormat = WebpFileFormatType.Lossless });
            byte[] encoded = output.ToArray();
            // Preserve the complete image chunk, replacing trailing XMP with a short
            // metadata payload whose declared size would otherwise cause a huge allocation.
            int offset = 12;
            while (Encoding.ASCII.GetString(encoded, offset, 4) != "XMP ")
            {
                uint length = BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(offset + 4));
                offset += 8 + (int)(length + (length & 1));
            }

            byte[] data = new byte[offset + 8];
            Array.Copy(encoded, data, offset);
            Encoding.ASCII.GetBytes(type).CopyTo(data, offset);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 4), uint.MaxValue);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), (uint)data.Length - 8);
            data[20] |= (byte)(type == "EXIF" ? 8 : 4);

            using Image<Rgba32> decoded = Image.Load<Rgba32>(data, new WebpDecoder { IgnoreMetadata = ignoreMetadata });
            Assert.Equal(image[0, 0], decoded[0, 0]);
            Assert.Null(decoded.Metadata.ExifProfile);
            Assert.Null(decoded.Metadata.XmpProfile);
        }

        [Theory]
        [InlineData("ICCP", false)]
        [InlineData("ICCP", true)]
        public void TruncatedIccFraming_ThrowsEvenWhenIgnored(string type, bool ignoreMetadata)
        {
            byte[] data = BuildExtendedWebp(type, uint.MaxValue);
            Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(data, new WebpDecoder { IgnoreMetadata = ignoreMetadata }));
        }

        [Theory]
        [InlineData("EXIF", false)]
        [InlineData("EXIF", true)]
        [InlineData("XMP ", false)]
        [InlineData("XMP ", true)]
        public void TruncatedOptionalMetadata_DoesNotAllocateDeclaredLength(string type, bool ignoreMetadata)
        {
            // Optional metadata is ignored, but an extended header alone is not a decodable image.
            byte[] data = BuildExtendedWebp(type, uint.MaxValue);
            Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(data, new WebpDecoder { IgnoreMetadata = ignoreMetadata }));
        }

        [Fact]
        public void UnknownChunk_PaddingOverflow_Throws()
            => Assert.Throws<InvalidImageContentException>(() => Image.Load(BuildExtendedWebp("JUNK", uint.MaxValue)));

        /// <summary>
        /// Creates a minimal extended WebP canvas followed by a deliberately truncated chunk.
        /// </summary>
        private static byte[] BuildExtendedWebp(string type, uint length)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(new byte[] { 82, 73, 70, 70 });
            writer.Write(30U);
            writer.Write(new byte[] { 87, 69, 66, 80, 86, 80, 56, 88 });
            writer.Write(10U);
            writer.Write((byte)(type == "ICCP" ? 32 : type == "EXIF" ? 8 : type == "XMP " ? 4 : 0));
            writer.Write(new byte[9]);
            writer.Write(System.Text.Encoding.ASCII.GetBytes(type));
            writer.Write(length);
            return stream.ToArray();
        }
    }
}
