using System.Drawing;
using System.Drawing.Imaging;
using Xunit;
using YScreenshot.Capture;

namespace YScreenshot.Capture.Tests
{
    public class FrameStitcherTests
    {
        private const int Width = 4;

        /// <summary>
        /// Builds a bitmap where every row's color encodes its absolute row index (mod
        /// 256), so any two crops of the same "document" can be compared for exact
        /// overlap, and crops of two different "documents" reliably differ.
        /// </summary>
        private static Bitmap CreateStripedDocument(int height, int seed)
        {
            var bitmap = new Bitmap(Width, height, PixelFormat.Format32bppArgb);
            for (int y = 0; y < height; y++)
            {
                var color = Color.FromArgb(255, (seed + y) % 256, 0, 0);
                for (int x = 0; x < Width; x++)
                {
                    bitmap.SetPixel(x, y, color);
                }
            }

            return bitmap;
        }

        private static Bitmap Crop(Bitmap source, int startRow, int height)
        {
            return source.Clone(new Rectangle(0, startRow, source.Width, height), source.PixelFormat);
        }

        [Fact]
        public void FindVerticalOverlap_PartialOverlap_ReturnsOverlapRowCount()
        {
            using (var document = CreateStripedDocument(300, seed: 0))
            using (var frame1 = Crop(document, 0, 100))
            using (var frame2 = Crop(document, 60, 100))
            {
                int overlap = FrameStitcher.FindVerticalOverlap(frame1, frame2, maxOverlapRows: 100);

                Assert.Equal(40, overlap);
            }
        }

        [Fact]
        public void FindVerticalOverlap_UnrelatedContent_ReturnsZero()
        {
            using (var documentA = CreateStripedDocument(100, seed: 0))
            using (var documentB = CreateStripedDocument(100, seed: 123))
            {
                int overlap = FrameStitcher.FindVerticalOverlap(documentA, documentB, maxOverlapRows: 100);

                Assert.Equal(0, overlap);
            }
        }

        [Fact]
        public void FindVerticalOverlap_SmallFrameChange_UsesTolerantFallback()
        {
            using (var document = CreateStripedDocument(300, seed: 11))
            using (var frame1 = Crop(document, 0, 100))
            using (var frame2 = Crop(document, 60, 100))
            {
                // Change one sampled row in the second frame. Exact row hashing must
                // reject the full seam, while the tolerant matcher should still find
                // the 40-row overlap used by a real browser with minor redraw noise.
                for (int x = 0; x < frame2.Width; x++)
                {
                    frame2.SetPixel(x, 21, Color.White);
                }

                int overlap = FrameStitcher.FindVerticalOverlap(frame1, frame2, maxOverlapRows: 100);

                Assert.Equal(40, overlap);
            }
        }

        [Fact]
        public void FindVerticalOverlap_IdenticalFrames_ReturnsFullHeight_MeaningNoNewContent()
        {
            using (var document = CreateStripedDocument(300, seed: 0))
            using (var frame1 = Crop(document, 60, 100))
            using (var frame2 = Crop(document, 60, 100))
            {
                int overlap = FrameStitcher.FindVerticalOverlap(frame1, frame2, maxOverlapRows: 100);

                Assert.Equal(100, overlap);
            }
        }

        [Fact]
        public void AppendBelow_PartialOverlap_ProducesCorrectlyStitchedImage()
        {
            using (var document = CreateStripedDocument(300, seed: 0))
            using (var frame1 = Crop(document, 0, 100))
            using (var frame2 = Crop(document, 60, 100))
            using (var expected = Crop(document, 0, 160))
            {
                int overlap = FrameStitcher.FindVerticalOverlap(frame1, frame2, maxOverlapRows: 100);

                using (var stitched = FrameStitcher.AppendBelow(frame1, frame2, overlap))
                {
                    Assert.Equal(expected.Size, stitched.Size);
                    AssertBitmapsEqual(expected, stitched);
                }
            }
        }

        [Fact]
        public void AppendBelow_NoNewRows_ReturnsCopyOfAccumulated()
        {
            using (var document = CreateStripedDocument(300, seed: 0))
            using (var frame1 = Crop(document, 0, 100))
            using (var frame2 = Crop(document, 0, 100))
            {
                using (var stitched = FrameStitcher.AppendBelow(frame1, frame2, overlapRows: 100))
                {
                    Assert.Equal(frame1.Size, stitched.Size);
                    AssertBitmapsEqual(frame1, stitched);
                }
            }
        }

        [Fact]
        public void ScrollSequence_ThreeOverlappingFrames_StitchesToExactWholeDocument()
        {
            // Simulates a short scroll session: three overlapping visible-region
            // captures of a taller "page" should stitch back into that exact page,
            // matching the plan's "no duplicate or missing rows at seams" criterion.
            using (var document = CreateStripedDocument(220, seed: 7))
            using (var frame1 = Crop(document, 0, 100))
            using (var frame2 = Crop(document, 70, 100))
            using (var frame3 = Crop(document, 120, 100))
            using (var expected = Crop(document, 0, 220))
            {
                int overlap1 = FrameStitcher.FindVerticalOverlap(frame1, frame2, maxOverlapRows: 100);
                using (var stitched1 = FrameStitcher.AppendBelow(frame1, frame2, overlap1))
                {
                    int overlap2 = FrameStitcher.FindVerticalOverlap(frame2, frame3, maxOverlapRows: 100);
                    using (var stitched2 = FrameStitcher.AppendBelow(stitched1, frame3, overlap2))
                    {
                        Assert.Equal(expected.Size, stitched2.Size);
                        AssertBitmapsEqual(expected, stitched2);
                    }
                }
            }
        }

        private static void AssertBitmapsEqual(Bitmap expected, Bitmap actual)
        {
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Height, actual.Height);

            for (int y = 0; y < expected.Height; y++)
            {
                for (int x = 0; x < expected.Width; x++)
                {
                    Assert.Equal(expected.GetPixel(x, y), actual.GetPixel(x, y));
                }
            }
        }
    }
}
