// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats
{
    public class InvalidImageDimensionsTests
    {
        [Theory]
        [InlineData(0, 1)]
        [InlineData(-1, 1)]
        [InlineData(1, 0)]
        [InlineData(1, int.MinValue)]
        public void Bmp_InvalidDimensions_Throws(int width, int height)
        {
            using var image = new Image<Rgba32>(1, 1);
            using var output = new MemoryStream();
            image.Save(output, new BmpEncoder());
            byte[] data = output.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(18), width);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(22), height);

            Assert.Throws<InvalidImageContentException>(() => Image.Load(data));
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(data));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 0)]
        public void Gif_InvalidLogicalScreen_Throws(ushort width, ushort height)
        {
            using var image = new Image<Rgba32>(1, 1);
            using var output = new MemoryStream();
            image.Save(output, new GifEncoder());
            byte[] data = output.ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6), width);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8), height);

            Assert.Throws<InvalidImageContentException>(() => Image.Load(data));
            Assert.Throws<InvalidImageContentException>(() => Image.Identify(data));
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(-1, 1)]
        [InlineData(1, 0)]
        [InlineData(1, -1)]
        public void Tga_InvalidDimensions_Throws(short width, short height)
        {
            using var image = new Image<Rgba32>(1, 1);
            using var output = new MemoryStream();
            image.Save(output, new TgaEncoder());
            byte[] data = output.ToArray();
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), width);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(14), height);

            // TGA detection rejects zero dimensions itself. An explicit decoder also
            // exercises the allocation-boundary validation for every malformed header.
            Assert.Throws<InvalidImageContentException>(() => Image.Load<Rgba32>(data, new TgaDecoder()));
        }
    }
}
