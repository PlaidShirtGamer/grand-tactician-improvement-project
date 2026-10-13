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
Check(EditorNaming.Number(11,0)=="11th" && EditorNaming.Number(22,0)=="22nd" && EditorNaming.Number(113,0)=="113th", "Ordinal exceptions");
Check(EditorNaming.Number(1944,2)=="MCMXLIV", "Roman subtraction");
Check(EditorNaming.Render("{number} {state} {type}",2,0,1,"Ohio","Infantry","Regiment","old")=="2ND OHIO INFANTRY", "Naming tokens and case");
Check(EditorNaming.Render("Guard {old}",1,1,0,"","","","Existing")=="Guard Existing", "Literal text and existing name");
bool invalid=false;try{EditorNaming.Render("{unknown}",1,0,0,"","","","");}catch(InvalidOperationException){invalid=true;}Check(invalid,"Unknown naming tokens rejected");
Check(!OfficerVisibilityRules.Hide(true,true,true,0,7,""), "Unassigned generated MG remains visible");
Check(!OfficerVisibilityRules.Hide(true,true,true,1,6,null), "Generated land BG remains visible");
Check(!OfficerVisibilityRules.Hide(true,true,true,0,5,"Created with GTIP Aide-de-Camp"), "GTIP-created colonel remains visible");
Check(OfficerVisibilityRules.Hide(true,true,true,0,5,""), "Unmarked lower-rank placeholders remain hidden");
Check(OfficerVisibilityRules.Hide(true,true,true,4,7,""), "Unmarked naval placeholders unchanged");
Check(OfficerVisibilityRules.Hide(false,true,true,0,7,""), "Outside GTIP mode unchanged");
Check(OfficerVisibilityRules.Hide(true,true,false,0,7,""), "Inactive dummy unchanged");
Check(!OfficerVisibilityRules.Hide(true,false,true,0,7,""), "Historical officer unchanged");
Check(!OfficerVisibilityRules.Hide(true,true,true,-1,7,""), "Generated MG with native unspecified land branch is visible");
Check(OfficerVisibilityRules.Hide(true,true,true,-1,3,""), "Unspecified-branch junior placeholder stays hidden");
Check(!OfficerVisibilityRules.Hide(true,true,true,2,4,"",true), "Veteran or professional dummy lieutenant colonel visible");
Check(OfficerVisibilityRules.Hide(true,true,true,4,4,"",true), "Naval placeholder background does not change land rule");
Console.WriteLine($"Editor presentation: {checks} checks passed.");
