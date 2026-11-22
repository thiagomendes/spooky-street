using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SpookyStreet.Core.Nonverbal;

// Samples every case 01 vignette at 15 fps and writes the part states and running cues as JSON, plus the line
// table (line key -> vignette ID). Read by tools/ui_mock/vignette_sheet.py.
internal static class Program
{
    private const float Fps = 15f;

    private static int Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "vignettes.json";
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append("{\"lines\":[");
        bool first = true;
        foreach (var row in C01Vignettes.LineTable)
        {
            sb.Append(first ? "" : ",").Append("{\"key\":\"").Append(row.LineKey).Append("\",\"vignette\":")
                .Append(row.VignetteId == null ? "null" : "\"" + row.VignetteId + "\"").Append('}');
            first = false;
        }

        sb.Append("],\"vignettes\":[");
        var cues = new List<CueState>();
        int errors = 0;
        for (int v = 0; v < C01Vignettes.All.Count; v++)
        {
            var vg = C01Vignettes.All[v];
            var problems = VignetteValidator.Validate(vg);
            foreach (string p in problems)
            {
                Console.Error.WriteLine(p);
            }

            errors += problems.Count;
            sb.Append(v == 0 ? "" : ",").Append("{\"id\":\"").Append(vg.Id).Append("\",\"length\":")
                .Append(vg.Length.ToString(inv)).Append(",\"moving\":").Append(vg.MaxMovingAtOnce())
                .Append(",\"beats\":[");
            for (int b = 0; b < vg.Beats.Count; b++)
            {
                sb.Append(b == 0 ? "" : ",").Append('[').Append(vg.Beats[b].Start.ToString(inv)).Append(',')
                    .Append(vg.Beats[b].End.ToString(inv)).Append(']');
            }

            sb.Append("],\"frames\":[");
            int n = (int)Math.Floor(vg.Length * Fps);
            for (int f = 0; f <= n; f++)
            {
                float t = f / Fps;
                var parts = VignetteSampler.Sample(vg, t);
                VignetteSampler.ActiveCues(vg, t, cues);
                sb.Append(f == 0 ? "" : ",").Append("{\"t\":").Append(t.ToString(inv)).Append(",\"parts\":[");
                for (int p = 0; p < parts.Length; p++)
                {
                    var s = parts[p];
                    var pose = s.Pose;
                    sb.Append(p == 0 ? "" : ",").Append("{\"track\":\"").Append(s.TrackId).Append("\",\"part\":\"")
                        .Append(s.Part).Append("\",\"visible\":").Append(s.Visible ? "true" : "false")
                        .Append(",\"x\":").Append(pose.X.ToString(inv)).Append(",\"y\":").Append(pose.Y.ToString(inv))
                        .Append(",\"scale\":").Append(pose.Scale.ToString(inv)).Append(",\"rot\":")
                        .Append(pose.Rotation.ToString(inv)).Append(",\"alpha\":").Append(pose.Alpha.ToString(inv))
                        .Append('}');
                }

                sb.Append("],\"cues\":[");
                for (int c = 0; c < cues.Count; c++)
                {
                    var cs = cues[c];
                    sb.Append(c == 0 ? "" : ",").Append("{\"kind\":\"").Append(cs.Cue.Kind).Append("\",\"target\":\"")
                        .Append(cs.Cue.Target).Append("\",\"progress\":").Append(cs.Progress.ToString(inv))
                        .Append(",\"elapsed\":").Append(cs.Elapsed.ToString(inv)).Append('}');
                }

                sb.Append("]}");
            }

            sb.Append("]}");
        }

        sb.Append("]}");
        File.WriteAllText(path, sb.ToString());
        Console.WriteLine($"{C01Vignettes.All.Count} vignettes, {C01Vignettes.LineTable.Count} lines, {errors} errors -> {path}");
        return errors == 0 ? 0 : 1;
    }
}
