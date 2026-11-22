// Dumps the handcrafted sky's textures and cut shapes for tools/sky_preview/sky_preview.py, using the game's own
// engine-free code. Arguments: output directory, then the base height of each cloud ring's first cloud.
// The ring placement below is a copy of Runtime/World/SkyMeshes.CloudRing: keep them in step.
using System;
using System.IO;
using System.Text;
using System.Linq;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.World;
class P {
  static void Main(string[] a) {
    string o = a[0];
    File.WriteAllBytes(o + "/sky.rgb", SkyPainter.PaintSky(1024, 512, false));
    File.WriteAllBytes(o + "/stars.rg", SkyPainter.PaintStarMask(1024, 512));
    File.WriteAllBytes(o + "/moon.rgb", SkyPainter.PaintMoon(256, 0.92f));
    File.WriteAllBytes(o + "/grain.rgb", SkyPainter.PaintPaperGrain(128));
    var sb = new StringBuilder();
    Dump(sb, "bat", SkyShapes.Bat(1f));
    File.WriteAllText(o + "/shapes.txt", sb.ToString());
    var ci = System.Globalization.CultureInfo.InvariantCulture;
    var rb = new StringBuilder();
    for (int li = 0; li < SkyModel.Layers.Length; li++) {
      var layer = SkyModel.Layers[li];
      var random = new Random(4201 + li * 97);
      for (int c = 0; c < layer.Clouds; c++) {
        float spread = 360f / layer.Clouds;
        float azimuth = c == 0 ? 0f : c * spread + ((float)random.NextDouble() - 0.5f) * spread * 0.35f;
        float baseY = c == 0 ? float.Parse(a[1 + li], ci) : layer.Height + ((float)random.NextDouble() - 0.5f) * layer.CloudHeight;
        float width = layer.CloudWidth * (0.85f + 0.3f * (float)random.NextDouble());
        float height = layer.CloudHeight * (0.9f + 0.2f * (float)random.NextDouble());
        int seed = 100 + li * 31 + c * 7;
        float lean = ((float)random.NextDouble() - 0.5f) * width * 0.24f;
        Sheet(rb, li, 1, layer.Radius + 0.5f, azimuth, lean, baseY + height * 0.1f, layer.Mist, SkyShapes.Cloud(seed + 3, width * 0.78f, height * 1.22f));
        Sheet(rb, li, 0, layer.Radius, azimuth, 0f, baseY, layer.Mist, SkyShapes.Cloud(seed, width, height));
      }
    }
    File.WriteAllText(o + "/rings.txt", rb.ToString());
  }
  static void Sheet(StringBuilder sb, int layer, int back, float radius, float az, float shift, float baseY, float mist, PaperShape p) {
    var ci = System.Globalization.CultureInfo.InvariantCulture;
    sb.Append(string.Join(" ", new object[]{layer, back, radius, az, shift, baseY, mist}.Select(v => Convert.ToString(v, ci)))).Append('\n');
    int n = p.VertexCount / 4;
    var xs = new float[n]; var bo = new float[n]; var to = new float[n]; var rx = new float[n]; var ry = new float[n]; var ins = new float[n];
    for (int c = 0; c < n; c++) { xs[c] = p.X[c*4]; bo[c] = p.Y[c*4]; to[c] = p.Y[c*4+3]; rx[c] = p.RimX[c*4+3]; ry[c] = p.RimY[c*4+3]; ins[c] = p.Y[c*4+3]-p.Y[c*4+2]; }
    foreach (var arr in new[]{xs,bo,to,rx,ry,ins}) sb.Append(string.Join(" ", Array.ConvertAll(arr, v => v.ToString(ci)))).Append('\n');
  }
  static void Dump(StringBuilder sb, string name, PaperShape p) {
    sb.Append(name).Append('\n');
    var ci = System.Globalization.CultureInfo.InvariantCulture;
    sb.Append(string.Join(" ", Array.ConvertAll(p.X, v => v.ToString(ci)))).Append('\n');
    sb.Append(string.Join(" ", Array.ConvertAll(p.Y, v => v.ToString(ci)))).Append('\n');
    sb.Append(string.Join(" ", Array.ConvertAll(p.Shade, v => v.ToString(ci)))).Append('\n');
    sb.Append(string.Join(" ", Array.ConvertAll(p.RimY, v => v.ToString(ci)))).Append('\n');
    sb.Append(string.Join(" ", p.Triangles)).Append('\n');
  }
}
