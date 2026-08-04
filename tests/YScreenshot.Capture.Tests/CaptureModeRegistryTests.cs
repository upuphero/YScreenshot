using System.Linq;
using System.Threading.Tasks;
using Xunit;
using YScreenshot.Capture;

namespace YScreenshot.Capture.Tests
{
    public class CaptureModeRegistryTests
    {
        private sealed class FakeCaptureMode : ICaptureMode
        {
            public FakeCaptureMode(string id)
            {
                Id = id;
            }

            public string Id { get; }
            public string DisplayName => Id;

            public Task<CaptureResult> CaptureAsync(CaptureContext ctx) =>
                Task.FromResult<CaptureResult>(null);
        }

        [Fact]
        public void Register_ThenTryGet_ReturnsSameInstance()
        {
            var registry = new CaptureModeRegistry();
            var mode = new FakeCaptureMode("fullscreen");

            registry.Register(mode);

            Assert.True(registry.TryGet("fullscreen", out var found));
            Assert.Same(mode, found);
        }

        [Fact]
        public void TryGet_UnknownId_ReturnsFalse()
        {
            var registry = new CaptureModeRegistry();

            Assert.False(registry.TryGet("does-not-exist", out var found));
            Assert.Null(found);
        }

        [Fact]
        public void TryGet_IsCaseInsensitive()
        {
            var registry = new CaptureModeRegistry();
            registry.Register(new FakeCaptureMode("Region"));

            Assert.True(registry.TryGet("region", out _));
        }

        [Fact]
        public void Enumeration_PreservesRegistrationOrder()
        {
            var registry = new CaptureModeRegistry();
            registry.Register(new FakeCaptureMode("fullscreen"));
            registry.Register(new FakeCaptureMode("region"));
            registry.Register(new FakeCaptureMode("scrolling"));

            var ids = registry.Select(m => m.Id).ToArray();

            Assert.Equal(new[] { "fullscreen", "region", "scrolling" }, ids);
        }
    }
}
