// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Provides methods to read ICC data types
    /// </summary>
    internal sealed partial class IccDataReader
    {
        /// <summary>
        /// Reads an 8bit lookup table
        /// </summary>
        /// <returns>The read LUT</returns>
        public IccLut ReadLut8()
        {
            return new IccLut(this.ReadBytes(256));
        }

        /// <summary>
        /// Reads a 16bit lookup table
        /// </summary>
        /// <param name="count">The number of entries</param>
        /// <returns>The read LUT</returns>
        public IccLut ReadLut16(int count)
        {
            var values = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = this.ReadUInt16();
            }

            return new IccLut(values);
        }

        /// <summary>
        /// Reads a CLUT depending on type
        /// </summary>
        /// <param name="inChannelCount">Input channel count</param>
        /// <param name="outChannelCount">Output channel count</param>
        /// <param name="isFloat">If true, it's read as CLUTf32,
        /// else read as either CLUT8 or CLUT16 depending on embedded information</param>
        /// <returns>The read CLUT</returns>
        public IccClut ReadClut(int inChannelCount, int outChannelCount, bool isFloat)
        {
            // The descriptor has only 16 grid dimensions. Validate before allocating or copying.
            if (inChannelCount < 1 || inChannelCount > 15 || outChannelCount < 1 || outChannelCount > 15
                || this.data.Length - this.currentIndex < 16)
            {
                throw new InvalidIccProfileException("Invalid CLUT dimensions or truncated grid descriptor.");
            }

            // Grid-points are always 16 bytes long but only 0-inChCount are used
            var gridPointCount = new byte[inChannelCount];
            Buffer.BlockCopy(this.data, this.AddIndex(16), gridPointCount, 0, inChannelCount);

            if (!isFloat)
            {
                byte size = this.data[this.AddIndex(4)];   // First byte is info, last 3 bytes are reserved
                if (size == 1)
                {
                    return this.ReadClut8(inChannelCount, outChannelCount, gridPointCount);
                }

                if (size == 2)
                {
                    return this.ReadClut16(inChannelCount, outChannelCount, gridPointCount);
                }

                throw new InvalidIccProfileException($"Invalid CLUT size of {size}");
            }

            return this.ReadClutF32(inChannelCount, outChannelCount, gridPointCount);
        }

        /// <summary>
        /// Reads an 8 bit CLUT
        /// </summary>
        /// <param name="inChannelCount">Input channel count</param>
        /// <param name="outChannelCount">Output channel count</param>
        /// <param name="gridPointCount">Grid point count for each CLUT channel</param>
        /// <returns>The read CLUT8</returns>
        public IccClut ReadClut8(int inChannelCount, int outChannelCount, byte[] gridPointCount)
        {
            int start = this.currentIndex;
            int length = this.GetClutLength(inChannelCount, outChannelCount, gridPointCount, 1);

            const float Max = byte.MaxValue;

            var values = new float[length][];
            for (int i = 0; i < length; i++)
            {
                values[i] = new float[outChannelCount];
                for (int j = 0; j < outChannelCount; j++)
                {
                    values[i][j] = this.data[this.currentIndex++] / Max;
                }
            }

            this.currentIndex = start + (length * outChannelCount);
            return new IccClut(values, gridPointCount, IccClutDataType.UInt8);
        }

        /// <summary>
        /// Reads a 16 bit CLUT
        /// </summary>
        /// <param name="inChannelCount">Input channel count</param>
        /// <param name="outChannelCount">Output channel count</param>
        /// <param name="gridPointCount">Grid point count for each CLUT channel</param>
        /// <returns>The read CLUT16</returns>
        public IccClut ReadClut16(int inChannelCount, int outChannelCount, byte[] gridPointCount)
        {
            int start = this.currentIndex;
            int length = this.GetClutLength(inChannelCount, outChannelCount, gridPointCount, 2);

            const float Max = ushort.MaxValue;

            var values = new float[length][];
            for (int i = 0; i < length; i++)
            {
                values[i] = new float[outChannelCount];
                for (int j = 0; j < outChannelCount; j++)
                {
                    values[i][j] = this.ReadUInt16() / Max;
                }
            }

            this.currentIndex = start + (length * outChannelCount * 2);
            return new IccClut(values, gridPointCount, IccClutDataType.UInt16);
        }

        /// <summary>
        /// Reads a 32bit floating point CLUT
        /// </summary>
        /// <param name="inChCount">Input channel count</param>
        /// <param name="outChCount">Output channel count</param>
        /// <param name="gridPointCount">Grid point count for each CLUT channel</param>
        /// <returns>The read CLUTf32</returns>
        public IccClut ReadClutF32(int inChCount, int outChCount, byte[] gridPointCount)
        {
            int start = this.currentIndex;
            int length = this.GetClutLength(inChCount, outChCount, gridPointCount, 4);

            var values = new float[length][];
            for (int i = 0; i < length; i++)
            {
                values[i] = new float[outChCount];
                for (int j = 0; j < outChCount; j++)
                {
                    values[i][j] = this.ReadSingle();
                }
            }

            this.currentIndex = start + (length * outChCount * 4);
            return new IccClut(values, gridPointCount, IccClutDataType.Float);
        }

        /// <summary>
        /// Validates CLUT dimensions and available storage before allocating the value arrays.
        /// </summary>
        /// <param name="inputChannelCount">The number of input channels.</param>
        /// <param name="outputChannelCount">The number of output channels.</param>
        /// <param name="gridPointCount">The grid size for each input channel.</param>
        /// <param name="bytesPerValue">The encoded size of each output value.</param>
        /// <returns>The number of grid points.</returns>
        private int GetClutLength(int inputChannelCount, int outputChannelCount, byte[] gridPointCount, int bytesPerValue)
        {
            if (inputChannelCount < 1 || inputChannelCount > 15 || outputChannelCount < 1 || outputChannelCount > 15
                || gridPointCount.Length != inputChannelCount)
            {
                throw new InvalidIccProfileException("Invalid CLUT channel count.");
            }

            // The table is a Cartesian product, including when channel grid sizes differ.
            int length = 1;
            for (int i = 0; i < inputChannelCount; i++)
            {
                int gridPoints = gridPointCount[i];
                if (gridPoints == 0 || length > int.MaxValue / gridPoints)
                {
                    throw new InvalidIccProfileException("Invalid CLUT dimensions.");
                }

                length *= gridPoints;
            }

            // Check encoded storage before allocating even the outer jagged array.
            long valueCount = (long)length * outputChannelCount;
            long byteCount = valueCount * bytesPerValue;
            if (valueCount > int.MaxValue || byteCount > this.data.Length - this.currentIndex)
            {
                throw new InvalidIccProfileException("The CLUT data is shorter than its declared dimensions.");
            }

            return length;
        }
    }
}
