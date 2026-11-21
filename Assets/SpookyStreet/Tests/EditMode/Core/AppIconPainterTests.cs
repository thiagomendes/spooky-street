using NUnit.Framework;
using SpookyStreet.Core.Art;

namespace SpookyStreet.Tests.Core
{
    public class AppIconPainterTests
    {
        private static int[] PixelAt(byte[] rgb, int size, float u, float v)
        {
            int x = (int)(u * size);
            int y = (int)(v * size);
            int i = (y * size + x) * 3;
            return new[] { (int)rgb[i], rgb[i + 1], rgb[i + 2] };
        }

        [Test]
        public void ProducesOpaqueRgbAtAppStoreSize()
        {
            byte[] rgb = AppIconPainter.PaintRgb(1024);
            Assert.AreEqual(1024 * 1024 * 3, rgb.Length, "RGB24, no alpha channel");
        }

        [Test]
        public void AtticWindowIsAmberAndSkyIsTwilight()
        {
            const int size = 256;
            byte[] rgb = AppIconPainter.PaintRgb(size);
            int[] window = PixelAt(rgb, size, 0.5f, 0.64f);
            Assert.AreEqual(new[] { 0xF6, 0xAD, 0x58 }, window, "attic window centre uses pumpkin amber");
            int[] sky = PixelAt(rgb, size, 0.05f, 0.95f);
            Assert.Less(sky[0], 0x50, "sky stays dark");
            Assert.Greater(sky[2], sky[0], "sky is blue-dominant");
        }

        [Test]
        public void IsDeterministic()
        {
            CollectionAssert.AreEqual(AppIconPainter.PaintRgb(64), AppIconPainter.PaintRgb(64));
        }

        [Test]
        public void RejectsTinySizes()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => AppIconPainter.PaintRgb(8));
        }
    }
}
