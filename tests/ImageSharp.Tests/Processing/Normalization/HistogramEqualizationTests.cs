// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Normalization;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Normalization
{
    // ReSharper disable InconsistentNaming
    [Trait("Category", "Processors")]
    public class HistogramEqualizationTests
    {
        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.0456F);

        [Theory]
        [InlineData(float.NaN, 0)]
        [InlineData(float.NegativeInfinity, 0)]
        [InlineData(float.PositiveInfinity, 255)]
        [InlineData(-2F, 0)]
        [InlineData(2F, 255)]
        [InlineData(0.5F, 128)]
        public void HistogramLuminance_ClampsFloatingPointSamples(float sample, int expected)
        {
            var vector = new Vector4(sample, sample, sample, 1F);
            Assert.Equal(expected, ColorNumerics.GetBT709Luminance(ref vector, 256));
        }

        [Theory]
        [InlineData(float.PositiveInfinity)]
        [InlineData(float.NegativeInfinity)]
        [InlineData(float.NaN)]
        [InlineData(2F)]
        [InlineData(-2F)]
        [InlineData(0.5F)]
        public void GlobalHistogramEqualization_DecodedFloatingPointSamples(float sample)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((ushort)0x4949);
            writer.Write((ushort)42);
            writer.Write(8U);
            writer.Write((ushort)10);
            WriteEntry(writer, 256, 4, 8);
            WriteEntry(writer, 257, 4, 1);
            WriteEntry(writer, 258, 3, 32);
            WriteEntry(writer, 259, 3, 1);
            WriteEntry(writer, 262, 3, 1);
            WriteEntry(writer, 273, 4, 134);
            WriteEntry(writer, 277, 3, 1);
            WriteEntry(writer, 278, 4, 1);
            WriteEntry(writer, 279, 4, 32);
            WriteEntry(writer, 339, 3, 3);
            writer.Write(0U);
            for (int i = 0; i < 8; i++)
            {
                writer.Write(sample);
            }

            using Image<HalfVector4> image = Image.Load<HalfVector4>(stream.ToArray());
            image.Mutate(x => x.HistogramEqualization());
            Assert.Equal(new Size(8, 1), image.Size());
        }

        /// <summary>
        /// Writes a single inline TIFF directory entry for the floating-point regression fixture.
        /// </summary>
        private static void WriteEntry(BinaryWriter writer, ushort tag, ushort type, uint value)
        {
            writer.Write(tag);
            writer.Write(type);
            writer.Write(1U);
            writer.Write(value);
        }

        [Theory]
        [InlineData(256)]
        [InlineData(65536)]
        public void GlobalHistogramEqualization_WithDifferentLuminanceLevels(int luminanceLevels)
        {
            // Arrange
            byte[] pixels =
            {
                52,  55,  61,  59,  70,  61,  76,  61,
                62,  59,  55, 104,  94,  85,  59,  71,
                63,  65,  66, 113, 144, 104,  63,  72,
                64,  70,  70, 126, 154, 109,  71,  69,
                67,  73,  68, 106, 122,  88,  68,  68,
                68,  79,  60,  79,  77,  66,  58,  75,
                69,  85,  64,  58,  55,  61,  65,  83,
                70,  87,  69,  68,  65,  73,  78,  90
            };

            using (var image = new Image<Rgba32>(8, 8))
            {
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        byte luminance = pixels[(y * 8) + x];
                        image[x, y] = new Rgba32(luminance, luminance, luminance);
                    }
                }

                byte[] expected =
                {
                    0,    12,   53,   32,  146,   53,  174,   53,
                    57,   32,   12,  227,  219,  202,   32,  154,
                    65,   85,   93,  239,  251,  227,   65,  158,
                    73,  146,  146,  247,  255,  235,  154,  130,
                    97,  166,  117,  231,  243,  210,  117,  117,
                    117, 190,   36,  190,  178,   93,   20,  170,
                    130, 202,   73,   20,   12,   53,   85,  194,
                    146, 206,  130,  117,   85,  166,  182,  215
                };

                // Act
                image.Mutate(x => x.HistogramEqualization(new HistogramEqualizationOptions
                {
                    LuminanceLevels = luminanceLevels,
                    Method = HistogramEqualizationMethod.Global
                }));

                // Assert
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        Rgba32 actual = image[x, y];
                        Assert.Equal(expected[(y * 8) + x], actual.R);
                        Assert.Equal(expected[(y * 8) + x], actual.G);
                        Assert.Equal(expected[(y * 8) + x], actual.B);
                    }
                }
            }
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.HistogramEqImage, PixelTypes.Rgba32)]
        public void GlobalHistogramEqualization_CompareToReferenceOutput<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var options = new HistogramEqualizationOptions
                {
                    Method = HistogramEqualizationMethod.Global,
                    LuminanceLevels = 256,
                };
                image.Mutate(x => x.HistogramEqualization(options));
                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider, extension: "png");
            }
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.LowContrast, PixelTypes.Rgba32)]
        public void Adaptive_SlidingWindow_15Tiles_WithClipping<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var options = new HistogramEqualizationOptions
                {
                    Method = HistogramEqualizationMethod.AdaptiveSlidingWindow,
                    LuminanceLevels = 256,
                    ClipHistogram = true,
                    NumberOfTiles = 15
                };
                image.Mutate(x => x.HistogramEqualization(options));
                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }

        [Theory]
        [WithFile(TestImages.Jpeg.Baseline.LowContrast, PixelTypes.Rgba32)]
        public void Adaptive_TileInterpolation_10Tiles_WithClipping<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var options = new HistogramEqualizationOptions
                {
                    Method = HistogramEqualizationMethod.AdaptiveTileInterpolation,
                    LuminanceLevels = 256,
                    ClipHistogram = true,
                    NumberOfTiles = 10
                };
                image.Mutate(x => x.HistogramEqualization(options));
                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }

        /// <summary>
        /// This is regression test for a bug with the calculation of the y-start positions,
        /// where it could happen that one too much start position was calculated in some cases.
        /// See: https://github.com/SixLabors/ImageSharp/pull/984
        /// </summary>
        /// <typeparam name="TPixel">The pixel type of the image.</typeparam>
        /// <param name="provider">The test image provider.</param>
        [Theory]
        [WithTestPatternImages(110, 110, PixelTypes.Rgb24)]
        [WithTestPatternImages(170, 170, PixelTypes.Rgb24)]
        public void Issue984<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var options = new HistogramEqualizationOptions()
                {
                    Method = HistogramEqualizationMethod.AdaptiveTileInterpolation,
                    LuminanceLevels = 256,
                    ClipHistogram = true,
                    ClipLimit = 5,
                    NumberOfTiles = 10
                };
                image.Mutate(x => x.HistogramEqualization(options));
                image.DebugSave(provider);
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }

        [Theory]
        [WithTestPatternImages(5120, 9234, PixelTypes.L16)]
        public unsafe void Issue1640<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            if (!TestEnvironment.Is64BitProcess)
            {
                return;
            }

            // https://github.com/SixLabors/ImageSharp/discussions/1640
            // Test using isolated memory to ensure clean buffers for reference
            provider.Configuration = Configuration.CreateDefaultInstance();
            var options = new HistogramEqualizationOptions
            {
                Method = HistogramEqualizationMethod.AdaptiveTileInterpolation,
                LuminanceLevels = 4096,
                ClipHistogram = false,
                ClipLimit = 350,
                NumberOfTiles = 8
            };

            using Image<TPixel> image = provider.GetImage();
            using Image<TPixel> referenceResult = image.Clone(ctx =>
            {
                ctx.HistogramEqualization(options);
                ctx.Resize(image.Width / 4, image.Height / 4, KnownResamplers.Bicubic);
            });

            using Image<TPixel> processed = image.Clone(ctx =>
            {
                ctx.HistogramEqualization(options);
                ctx.Resize(image.Width / 4, image.Height / 4, KnownResamplers.Bicubic);
            });

            ValidatorComparer.VerifySimilarity(referenceResult, processed);
        }
    }
}
