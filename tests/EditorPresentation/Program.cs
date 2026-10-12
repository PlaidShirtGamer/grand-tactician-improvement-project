using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GTIP;

int checks = 0;
void Check(bool test, string message) { if (!test) throw new Exception(message); checks++; }
var all = EditorTreeLayout.Build(new[] { 0, 1, 2, 3, 4, 5, 0, 1 }, null, new HashSet<int>());
Check(all.Count == 8 && all.Single(n => n.Index == 5).Parent == 4, "Six levels and independent roots");
Check(all.Single(n => n.Index == 6).Parent == -1, "Roots must not attach to previous branch");
Check(all.All(n => n.Parent < 0 || n.Y > all.Single(p => p.Index == n.Parent).Y), "Children below parent");
Check(all.GroupBy(n => n.Depth).All(g => g.Select(n => n.X).Distinct().Count() == g.Count()), "No overlapping siblings");
var collapsed = EditorTreeLayout.Build(new[] { 0, 1, 2, 2, 1 }, null, new HashSet<int> { 1 });
Check(collapsed.Count == 3 && collapsed.Single(n => n.Index == 1).Children.Count == 2, "Collapse retains expansion metadata");
var found = EditorTreeLayout.Build(new[] { 0, 1, 2, 2, 0 }, new[] { false, false, true, false, false }, new HashSet<int> { 0 });
Check(found.Select(n => n.Index).OrderBy(i => i).SequenceEqual(new[] { 0, 1, 2 }), "Search retains ancestors and expands match path");
Check(EditorTreeLayout.Build(Array.Empty<int>(), null, new HashSet<int>()).Count == 0, "Empty tree");
Check(EditorTreeLayout.Build(new[] { 0, 3, 4 }, null, new HashSet<int>()).Single(n => n.Index == 1).Depth == 1, "Skipped tiers compact safely");
string file = Path.GetTempFileName();
try
{
    byte[] bytes = new byte[24]; byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 }; signature.CopyTo(bytes, 0);
    bytes[12] = 73; bytes[13] = 72; bytes[14] = 68; bytes[15] = 82; bytes[18] = 1; bytes[22] = 1;
    File.WriteAllBytes(file, bytes);
    Check(EditorPortraitFiles.Read(file).Length == 24, "Valid bounded header (decoding remains Unity's job)");
    void Rejected(string label) { bool rejected = false; try { EditorPortraitFiles.Read(file); } catch (InvalidOperationException) { rejected = true; } Check(rejected, label); }
    bytes[0] = 0; File.WriteAllBytes(file, bytes); Rejected("Reject non-PNG");
    bytes[0] = 137; bytes[16] = 127; File.WriteAllBytes(file, bytes); Rejected("Reject excessive dimensions before decode");
    File.WriteAllBytes(file, new byte[3]); Rejected("Reject truncated file");
    File.WriteAllBytes(file, new byte[10 * 1024 * 1024 + 1]); Rejected("Reject oversized file before decode");
}
finally { File.Delete(file); }
Console.WriteLine($"Editor presentation: {checks} checks passed.");
