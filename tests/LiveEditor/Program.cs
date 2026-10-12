using GTIP;
using System.Globalization;
void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); }
void Reject(Action action, string label) { bool rejected = false; try { action(); } catch { rejected = true; } Check(rejected, label); }
string name = "Original"; float experience = .12345678f;
bool valid = true, failWrite = false, failRefresh = false;
int refreshes = 0;
var fields = new[] {
    new LiveEditField { Label="Name", Read=()=>name, Write=v=>name=(string)v, Parse=LiveEditSession.Name },
    new LiveEditField { Label="Experience", Read=()=>experience,
        Write=v=> { experience=(float)v; if(failWrite) { failWrite=false; throw new Exception("write failure"); } },
        Parse=v=>LiveEditSession.Number(v,0,100,100), Format=v=>((float)v*100).ToString("0.###",CultureInfo.InvariantCulture) }
};
var session = new LiveEditSession(()=> { if(!valid) throw new Exception("campaign changed"); },()=> { refreshes++; if(failRefresh) { failRefresh=false; throw new Exception("refresh failure"); } });
session.Apply(fields,new[]{ "Renamed", fields[1].Format(experience) });
Check(name=="Renamed" && experience==.12345678f && session.UndoCount==1,"Unchanged rounded display value preserves exact native precision");
session.Undo(); Check(name=="Original" && session.RedoCount==1,"Undo restores original values");
session.Redo(); Check(name=="Renamed","Redo reapplies complete transaction");
session.Apply(fields,new[]{name,"50"}); Check(experience==.5f,"Percent is converted to native fraction");
foreach(var text in new[]{"NaN","Infinity","-1","101","1e50"}) Reject(()=>session.Apply(fields,new[]{"Should not write",text}),"Reject invalid percentage "+text);
Check(name=="Renamed" && experience==.5f,"Invalid later field prevents all writes");
Reject(()=>session.Apply(fields,new[]{"bad\nname","25"}),"Reject save-breaking line breaks");
int history=session.UndoCount;
failWrite=true; Reject(()=>session.Apply(fields,new[]{"Should roll back","75"}),"Setter failure reported");
Check(name=="Renamed" && experience==.5f && session.UndoCount==history,"Partial mutation rolls back and leaves history intact");
failRefresh=true; Reject(()=>session.Apply(fields,new[]{"Should roll back","75"}),"Refresh failure reported");
Check(name=="Renamed" && experience==.5f && session.UndoCount==history,"Refresh failure restores all original fields");
valid=false; Reject(()=>session.Apply(fields,new[]{"Stale","25"}),"Stale/unpaused session rejects writes");
Reject(session.Undo,"Stale session rejects undo"); valid=true;
experience=.6f; Reject(session.Undo,"Undo refuses to overwrite a native change"); experience=.5f;
session.Undo(); session.Apply(fields,new[]{"New branch",fields[1].Format(experience)});
Check(session.RedoCount==0,"New edit discards obsolete redo history");
session.Clear(); Check(session.UndoCount==0 && session.RedoCount==0,"Closing session discards undo/redo references");
Check(refreshes>0,"Successful and rolled-back writes refresh dependent data");
Console.WriteLine("Transaction tests do not validate the Unity UI or native field mappings.");
