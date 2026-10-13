// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Metadata.Profiles.Icc
{
    [Trait("Profile", "Icc")]
    public class IccReaderTests
    {
        [Theory]
        [InlineData(144U, 7U, false)]
        [InlineData(196U, 8U, false)]
        [InlineData(uint.MaxValue, 8U, false)]
        [InlineData(144U, 56U, true)]
        [InlineData(192U, 8U, true)]
        public void ReadTagData_ValidatesCompleteExtent(uint offset, uint size, bool valid)
        {
            // Unknown tag payloads are preserved, allowing the extent check itself to be tested.
            byte[] data = new byte[200];
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(128), 1);
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(132), 0x41324230);
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(136), offset);
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(140), size);

            IccTagDataEntry[] entries = this.CreateReader().ReadTagData(data);
            Assert.Equal(valid ? 1 : 0, entries.Length);
        }

        [Fact]
        public void ReadProfile_NoEntries()
        {
            IccReader reader = this.CreateReader();

            IccProfile output = reader.Read(IccTestDataProfiles.Header_Random_Array);

            Assert.Equal(0, output.Entries.Length);
            Assert.NotNull(output.Header);

            IccProfileHeader header = output.Header;
            IccProfileHeader expected = IccTestDataProfiles.Header_Random_Read;
            Assert.Equal(header.Class, expected.Class);
            Assert.Equal(header.CmmType, expected.CmmType);
            Assert.Equal(header.CreationDate, expected.CreationDate);
            Assert.Equal(header.CreatorSignature, expected.CreatorSignature);
            Assert.Equal(header.DataColorSpace, expected.DataColorSpace);
            Assert.Equal(header.DeviceAttributes, expected.DeviceAttributes);
            Assert.Equal(header.DeviceManufacturer, expected.DeviceManufacturer);
            Assert.Equal(header.DeviceModel, expected.DeviceModel);
            Assert.Equal(header.FileSignature, expected.FileSignature);
            Assert.Equal(header.Flags, expected.Flags);
            Assert.Equal(header.Id, expected.Id);
            Assert.Equal(header.PcsIlluminant, expected.PcsIlluminant);
            Assert.Equal(header.PrimaryPlatformSignature, expected.PrimaryPlatformSignature);
            Assert.Equal(header.ProfileConnectionSpace, expected.ProfileConnectionSpace);
            Assert.Equal(header.RenderingIntent, expected.RenderingIntent);
            Assert.Equal(header.Size, expected.Size);
            Assert.Equal(header.Version, expected.Version);
        }

        [Fact]
        public void ReadProfile_DuplicateEntry()
        {
            IccReader reader = this.CreateReader();

            IccProfile output = reader.Read(IccTestDataProfiles.Profile_Random_Array);

            Assert.Equal(2, output.Entries.Length);
            Assert.True(ReferenceEquals(output.Entries[0], output.Entries[1]));
        }

        private IccReader CreateReader()
        {
            return new IccReader();
        }
    }
}
