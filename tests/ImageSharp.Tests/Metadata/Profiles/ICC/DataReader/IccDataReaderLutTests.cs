// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.ICC.DataReader
{
    [Trait("Profile", "Icc")]
    public class IccDataReaderLutTests
    {
        [Theory]
        [InlineData(3)]
        [InlineData(255)]
        public void ReadClut_OversizedDimensions_Throws(byte gridPoints)
        {
            byte[] gridPointCount = Enumerable.Repeat(gridPoints, 15).ToArray();
            Assert.Throws<InvalidIccProfileException>(() => CreateReader(new byte[8]).ReadClut8(15, 15, gridPointCount));
            Assert.Throws<InvalidIccProfileException>(() => CreateReader(new byte[8]).ReadClut16(15, 15, gridPointCount));
            Assert.Throws<InvalidIccProfileException>(() => CreateReader(new byte[8]).ReadClutF32(15, 15, gridPointCount));
        }

        [Theory]
        [InlineData(0, 1, 2)]
        [InlineData(16, 1, 2)]
        [InlineData(1, 0, 2)]
        [InlineData(1, 16, 2)]
        [InlineData(1, 1, 0)]
        public void ReadClut_InvalidDimensions_Throws(int inputChannels, int outputChannels, byte gridPoints)
        {
            byte[] gridPointCount = Enumerable.Repeat(gridPoints, inputChannels).ToArray();
            Assert.Throws<InvalidIccProfileException>(() => CreateReader(new byte[128]).ReadClut8(inputChannels, outputChannels, gridPointCount));
            Assert.Throws<InvalidIccProfileException>(() => CreateReader(new byte[128]).ReadClut16(inputChannels, outputChannels, gridPointCount));
            Assert.Throws<InvalidIccProfileException>(() => CreateReader(new byte[128]).ReadClutF32(inputChannels, outputChannels, gridPointCount));
        }

        [Fact]
        public void ReadClut_TruncatedStorage_Throws()
        {
            byte[] gridPointCount = { 2, 3 };
            Assert.Throws<InvalidIccProfileException>(() => CreateReader(new byte[5]).ReadClut8(2, 1, gridPointCount));
            Assert.Throws<InvalidIccProfileException>(() => CreateReader(new byte[11]).ReadClut16(2, 1, gridPointCount));
            Assert.Throws<InvalidIccProfileException>(() => CreateReader(new byte[23]).ReadClutF32(2, 1, gridPointCount));
        }

        [Fact]
        public void ReadClut_UnequalGridDimensions_UsesProduct()
        {
            byte[] gridPointCount = { 2, 3 };
            float[][] values = Enumerable.Range(0, 6).Select(_ => new float[1]).ToArray();
            Assert.Equal(new IccClut(values, gridPointCount, IccClutDataType.UInt8), CreateReader(new byte[6]).ReadClut8(2, 1, gridPointCount));
            Assert.Equal(new IccClut(values, gridPointCount, IccClutDataType.UInt16), CreateReader(new byte[12]).ReadClut16(2, 1, gridPointCount));
            Assert.Equal(new IccClut(values, gridPointCount, IccClutDataType.Float), CreateReader(new byte[24]).ReadClutF32(2, 1, gridPointCount));
        }

        [Theory]
        [MemberData(nameof(IccTestDataLut.ClutTestData), MemberType = typeof(IccTestDataLut))]
        internal void ReadClut(byte[] data, IccClut expected, int inChannelCount, int outChannelCount, bool isFloat)
        {
            IccDataReader reader = CreateReader(data);

            IccClut output = reader.ReadClut(inChannelCount, outChannelCount, isFloat);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataLut.Clut8TestData), MemberType = typeof(IccTestDataLut))]
        internal void ReadClut8(byte[] data, IccClut expected, int inChannelCount, int outChannelCount, byte[] gridPointCount)
        {
            IccDataReader reader = CreateReader(data);

            IccClut output = reader.ReadClut8(inChannelCount, outChannelCount, gridPointCount);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataLut.Clut16TestData), MemberType = typeof(IccTestDataLut))]
        internal void ReadClut16(byte[] data, IccClut expected, int inChannelCount, int outChannelCount, byte[] gridPointCount)
        {
            IccDataReader reader = CreateReader(data);

            IccClut output = reader.ReadClut16(inChannelCount, outChannelCount, gridPointCount);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataLut.ClutF32TestData), MemberType = typeof(IccTestDataLut))]
        internal void ReadClutF32(byte[] data, IccClut expected, int inChannelCount, int outChannelCount, byte[] gridPointCount)
        {
            IccDataReader reader = CreateReader(data);

            IccClut output = reader.ReadClutF32(inChannelCount, outChannelCount, gridPointCount);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataLut.Lut8TestData), MemberType = typeof(IccTestDataLut))]
        internal void ReadLut8(byte[] data, IccLut expected)
        {
            IccDataReader reader = CreateReader(data);

            IccLut output = reader.ReadLut8();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataLut.Lut16TestData), MemberType = typeof(IccTestDataLut))]
        internal void ReadLut16(byte[] data, IccLut expected, int count)
        {
            IccDataReader reader = CreateReader(data);

            IccLut output = reader.ReadLut16(count);

            Assert.Equal(expected, output);
        }

        private static IccDataReader CreateReader(byte[] data)
        {
            return new IccDataReader(data);
        }
    }
}
