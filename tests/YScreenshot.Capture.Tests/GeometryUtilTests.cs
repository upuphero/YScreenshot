using System.Drawing;
using Xunit;
using YScreenshot.Overlay;

namespace YScreenshot.Capture.Tests
{
    public class GeometryUtilTests
    {
        [Fact]
        public void ToLocalRectangle_ContainerAtOrigin_ReturnsSameCoordinates()
        {
            var selection = new Rectangle(100, 50, 200, 150);
            var container = new Rectangle(0, 0, 1920, 1080);

            var local = GeometryUtil.ToLocalRectangle(selection, container);

            Assert.Equal(new Rectangle(100, 50, 200, 150), local);
        }

        [Fact]
        public void ToLocalRectangle_SecondaryMonitorWithNegativeOrigin_TranslatesCorrectly()
        {
            // Virtual screen spans a secondary monitor placed left of the primary, so the
            // virtual bounds' origin is negative (primary at 0,0; secondary at -1920,0).
            var selection = new Rectangle(-1800, 100, 300, 200);
            var container = new Rectangle(-1920, 0, 3840, 1080);

            var local = GeometryUtil.ToLocalRectangle(selection, container);

            Assert.Equal(new Rectangle(120, 100, 300, 200), local);
        }

        [Theory]
        [InlineData(10, 10, true)]
        [InlineData(0, 10, false)]
        [InlineData(10, 0, false)]
        [InlineData(-5, 10, false)]
        public void IsValidSelection_ChecksPositiveDimensions(int width, int height, bool expected)
        {
            var rect = new Rectangle(0, 0, width, height);

            Assert.Equal(expected, GeometryUtil.IsValidSelection(rect));
        }

        [Fact]
        public void NormalizeRectangle_DragDownRight_ReturnsExpectedRect()
        {
            var start = new Point(10, 10);
            var end = new Point(110, 60);

            var result = GeometryUtil.NormalizeRectangle(start, end);

            Assert.Equal(new Rectangle(10, 10, 100, 50), result);
        }

        [Fact]
        public void NormalizeRectangle_DragUpLeft_ReturnsExpectedRect()
        {
            // Same rectangle as the down-right drag, but the user started at the
            // bottom-right corner and dragged toward the top-left.
            var start = new Point(110, 60);
            var end = new Point(10, 10);

            var result = GeometryUtil.NormalizeRectangle(start, end);

            Assert.Equal(new Rectangle(10, 10, 100, 50), result);
        }

        [Fact]
        public void NormalizeRectangle_SamePoint_ReturnsZeroSizeRect()
        {
            var point = new Point(42, 42);

            var result = GeometryUtil.NormalizeRectangle(point, point);

            Assert.Equal(new Rectangle(42, 42, 0, 0), result);
        }
    }
}
