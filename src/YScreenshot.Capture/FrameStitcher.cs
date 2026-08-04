using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace YScreenshot.Capture
{
    /// <summary>
    /// Pure bitmap math for scrolling capture: finds how much two consecutively
    /// captured frames overlap, and appends only the new part of the second frame onto
    /// a growing stitched image. Kept free of any window/Win32 dependency so it is
    /// directly unit-testable against synthetic bitmaps.
    /// </summary>
    public static class FrameStitcher
    {
        /// <summary>
        /// Finds the number of rows at the bottom of <paramref name="previous"/> that
        /// are pixel-identical to rows at the top of <paramref name="next"/> (both must
        /// have the same width). Returns 0 if no overlap is found; returns
        /// <paramref name="next"/>.Height if the frames are fully identical (i.e.
        /// scrolling produced no new content).
        /// </summary>
        /// <remarks>
        /// Uses exact row hashing first, then a tolerant sampled-pixel fallback when
        /// the app/browser changes anti-aliasing or a small dynamic element between
        /// frames. The fallback is deliberately conservative so unrelated frames do
        /// not get treated as a seam.
        /// </remarks>
        public static int FindVerticalOverlap(Bitmap previous, Bitmap next, int maxOverlapRows)
        {
            if (previous == null) throw new ArgumentNullException(nameof(previous));
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (previous.Width != next.Width)
            {
                throw new ArgumentException("Frames must have the same width to compare.", nameof(next));
            }

            int candidateMax = Math.Min(maxOverlapRows, Math.Min(previous.Height, next.Height));
            if (candidateMax <= 0)
            {
                return 0;
            }

            int exactOverlap = FindExactVerticalOverlap(previous, next, candidateMax);
            if (exactOverlap > 0)
            {
                return exactOverlap;
            }

            return FindApproximateVerticalOverlap(previous, next, candidateMax);
        }

        private static int FindExactVerticalOverlap(Bitmap previous, Bitmap next, int candidateMax)
        {
            long[] previousTailHashes = HashRows(previous, previous.Height - candidateMax, candidateMax);
            long[] nextHeadHashes = HashRows(next, 0, candidateMax);

            for (int overlap = candidateMax; overlap >= 1; overlap--)
            {
                int previousStartIndex = candidateMax - overlap;
                if (RowHashesMatch(previousTailHashes, previousStartIndex, nextHeadHashes, 0, overlap))
                {
                    return overlap;
                }
            }

            return 0;
        }

        private static int FindApproximateVerticalOverlap(Bitmap previous, Bitmap next, int candidateMax)
        {
            const int MinimumOverlapRows = 12;
            const int SampleRows = 10;
            const int SampleColumns = 16;
            // Keep this deliberately small. A large tolerance can mistake a smooth
            // gradient for a shifted copy of the page and create a wrong seam.
            const int PerChannelTolerance = 12;

            if (candidateMax < MinimumOverlapRows)
            {
                return 0;
            }

            int bestOverlap = 0;
            int bestExactSamples = -1;
            int bestMatchingSamples = -1;
            int bestTotalSamples = 0;
            long bestTotalError = long.MaxValue;

            for (int overlap = candidateMax; overlap >= MinimumOverlapRows; overlap--)
            {
                int rowsToCheck = Math.Min(SampleRows, overlap);
                int columnsToCheck = Math.Min(SampleColumns, previous.Width);
                int exactSamples = 0;
                int matchingSamples = 0;
                int totalSamples = rowsToCheck * columnsToCheck;
                long totalError = 0;

                for (int rowIndex = 0; rowIndex < rowsToCheck; rowIndex++)
                {
                    int offset = rowsToCheck == 1
                        ? 0
                        : rowIndex * (overlap - 1) / (rowsToCheck - 1);
                    int previousY = previous.Height - overlap + offset;
                    int nextY = offset;

                    for (int columnIndex = 0; columnIndex < columnsToCheck; columnIndex++)
                    {
                        int x = columnsToCheck == 1
                            ? 0
                            : columnIndex * (previous.Width - 1) / (columnsToCheck - 1);
                        Color previousPixel = previous.GetPixel(x, previousY);
                        Color nextPixel = next.GetPixel(x, nextY);

                        int redError = Math.Abs(previousPixel.R - nextPixel.R);
                        int greenError = Math.Abs(previousPixel.G - nextPixel.G);
                        int blueError = Math.Abs(previousPixel.B - nextPixel.B);
                        int alphaError = Math.Abs(previousPixel.A - nextPixel.A);
                        int maxChannelError = Math.Max(
                            Math.Max(redError, greenError),
                            Math.Max(blueError, alphaError));
                        totalError += redError + greenError + blueError + alphaError;

                        if (maxChannelError == 0)
                        {
                            exactSamples++;
                        }

                        if (maxChannelError <= PerChannelTolerance)
                        {
                            matchingSamples++;
                        }
                    }
                }

                // Prefer candidates with more unchanged pixels before considering
                // merely near-equal pixels. This matters for smooth gradients: the
                // correct seam may have one redraw artifact, while a wrong seam can
                // be uniformly close in color across the whole sample.
                if (exactSamples > bestExactSamples
                    || (exactSamples == bestExactSamples && matchingSamples > bestMatchingSamples)
                    || (exactSamples == bestExactSamples
                        && matchingSamples == bestMatchingSamples
                        && totalError < bestTotalError))
                {
                    bestOverlap = overlap;
                    bestExactSamples = exactSamples;
                    bestMatchingSamples = matchingSamples;
                    bestTotalSamples = totalSamples;
                    bestTotalError = totalError;
                }
            }

            if (bestOverlap > 0
                && (bestExactSamples * 100 >= bestTotalSamples * 75
                    || bestMatchingSamples * 100 >= bestTotalSamples * 88))
            {
                return bestOverlap;
            }

            return 0;
        }

        /// <summary>
        /// Appends the bottom <c>next.Height - overlapRows</c> rows of <paramref name="next"/>
        /// (the part that doesn't duplicate <paramref name="accumulated"/>'s existing
        /// bottom) below <paramref name="accumulated"/>, returning a new bitmap. Neither
        /// input is disposed or modified.
        /// </summary>
        public static Bitmap AppendBelow(Bitmap accumulated, Bitmap next, int overlapRows)
        {
            if (accumulated == null) throw new ArgumentNullException(nameof(accumulated));
            if (next == null) throw new ArgumentNullException(nameof(next));

            int newRows = next.Height - overlapRows;
            if (newRows <= 0)
            {
                return new Bitmap(accumulated);
            }

            int width = accumulated.Width;
            var stitched = new Bitmap(width, accumulated.Height + newRows, PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(stitched))
            {
                g.DrawImageUnscaled(accumulated, 0, 0);
                g.DrawImage(
                    next,
                    new Rectangle(0, accumulated.Height, width, newRows),
                    new Rectangle(0, overlapRows, width, newRows),
                    GraphicsUnit.Pixel);
            }

            return stitched;
        }

        private static bool RowHashesMatch(long[] a, int aStart, long[] b, int bStart, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (a[aStart + i] != b[bStart + i])
                {
                    return false;
                }
            }

            return true;
        }

        private static long[] HashRows(Bitmap bitmap, int startRow, int rowCount)
        {
            var hashes = new long[rowCount];
            int width = bitmap.Width;

            for (int row = 0; row < rowCount; row++)
            {
                unchecked
                {
                    long hash = 17;
                    for (int x = 0; x < width; x++)
                    {
                        hash = hash * 31 + bitmap.GetPixel(x, startRow + row).ToArgb();
                    }

                    hashes[row] = hash;
                }
            }

            return hashes;
        }
    }
}
