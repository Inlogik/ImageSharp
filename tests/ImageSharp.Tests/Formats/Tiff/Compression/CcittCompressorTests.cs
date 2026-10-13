// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.Compression
{
    [Trait("Format", "Tiff")]
    public class CcittCompressorTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Initialize_OversizedBuffer_Throws(bool group4)
        {
            using var output = new MemoryStream();
            using TiffCcittCompressor compressor = group4
                ? new T6BitCompressor(output, MemoryAllocator.Create(), int.MaxValue, 1)
                : new T4BitCompressor(output, MemoryAllocator.Create(), int.MaxValue, 1);

            Assert.Throws<InvalidMemoryOperationException>(() => compressor.Initialize(1));
        }

        [Theory]
        [InlineData(0U, 8U)]
        [InlineData(255U, 8U)]
        [InlineData(0U, 5U)]
        [InlineData(31U, 5U)]
        public void WriteCode_InsufficientCapacity_DoesNotModifyBuffer(uint code, uint firstCodeLength)
        {
            using var compressor = new TestCompressor();
            byte[] buffer = { 0xAA, 0xBB };
            compressor.Write(firstCodeLength, code, buffer.AsSpan(0, 1));
            byte[] expected = (byte[])buffer.Clone();

            // Both zero and one bit paths must reject the entire code before writing.
            Assert.Throws<InvalidMemoryOperationException>(() => compressor.Write(4, code, buffer.AsSpan(0, 1)));
            Assert.Equal(expected, buffer);
        }

        /// <summary>
        /// Exposes code writing to verify capacity checks without invoking an encoder.
        /// </summary>
        private sealed class TestCompressor : TiffCcittCompressor
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="TestCompressor"/> class.
            /// </summary>
            public TestCompressor()
                : base(Stream.Null, MemoryAllocator.Create(), 1, 1)
            {
            }

            /// <inheritdoc />
            public override TiffCompression Method => TiffCompression.CcittGroup3Fax;

            /// <summary>
            /// Writes a code to the supplied destination.
            /// </summary>
            public void Write(uint length, uint code, Span<byte> destination) => this.WriteCode(length, code, destination);

            /// <inheritdoc />
            protected override long GetMaximumEncodedBits(int rowsPerStrip) => 8;

            /// <inheritdoc />
            protected override void CompressStrip(Span<byte> pixelsAsGray, int height, Span<byte> compressedData)
            {
            }
        }
    }
}
