using GTIP;
using UnityEngine;
using System.Reflection;

void Check(bool pass, string message) { if (!pass) throw new Exception(message); Console.WriteLine("PASS " + message); }
AICampaign.Faction Reset()
{
    BattleUnits.completeunitlist = new();
    AICampaign.bunits = new();
    var f = new AICampaign.Faction(); AICampaign.aifaction = new() { f };
    SixLevelRuntime.Active = true; GameVars.playeralliance = 0;
    return f;
}
Regiment Add(AICampaign.Faction f, int type, Regiment parent = null)
{
    var u = new Regiment { unittyp = type }; f.ownunits.Add(u);
    if(parent != null) { u.transform.SetParent(parent.transform, true); u.istopunit = false; }
    return u;
}
var faction = Reset(); var army = Add(faction, 18); var corps = Add(faction, 16);
AiArmyOrganization.IntegrateRaisedCorps(faction, corps, false);
Check(corps.transform.parent == army.transform && corps.permanentlydetached && !corps.istopunit, "New Corps reuses nearby Army and preserves independent movement");
for(int i=0; i<4; i++) Add(faction, 16, army);
corps = Add(faction, 16); AiArmyOrganization.IntegrateRaisedCorps(faction, corps, false);
Check(corps.transform.parent != army.transform && army.GetAttachedUnitsReg(false,true,16,true).Length == 5, "Full Army receives no sixth Corps");

foreach(string excluded in new[] { "enemy", "distant", "battle", "retreat", "transfer", "operation", "garrison", "raid", "theater", "group", "moving" })
{
    faction = Reset(); army = Add(faction,18); var child = Add(faction,15, army); corps = Add(faction,16);
    switch(excluded)
    {
        case "enemy": army.alliance = 0; break;
        case "distant": army.transform.position = new Vector3 { x = 500 }; break;
        case "battle": child.inbattle = true; break;
        case "retreat": child.onretreat = true; break;
        case "transfer": child.transfertime = 1; break;
        case "operation": faction.unitsinoffensiveoperations.Add(child); break;
        case "garrison": child.garrisonreference = new object(); break;
        case "raid": child.raiding = true; break;
        case "theater": army.theaterposition = new Vector3 { x = 500 }; break;
        case "group": army.assignedarmygroup = new ArmyGroup(); break;
        case "moving": child.regimentpaths = 1; break;
    }
    AiArmyOrganization.IntegrateRaisedCorps(faction, corps, false);
    Check(corps.transform.parent != army.transform, "Excludes " + excluded + " Army/subordinate");
}
faction = Reset(); army = Add(faction,18); corps = Add(faction,16);
AiArmyOrganization.IntegrateRaisedCorps(faction,corps,true);
Check(corps.transform.parent != army.transform, "Forced independent creation does not reuse an Army");
faction = Reset(); corps = Add(faction,16); corps.alliance = 0;
AiArmyOrganization.IntegrateRaisedCorps(faction,corps,false);
Check(corps.transform.parent == null, "Player formations remain untouched");
faction = Reset(); corps = Add(faction,16); SixLevelRuntime.Active = false;
AiArmyOrganization.IntegrateRaisedCorps(faction,corps,false);
Check(corps.transform.parent == null, "Inactive six-level mode remains untouched");
faction = Reset(); corps = Add(faction,16); AICampaign.bunits.FailCreation = true;
AiArmyOrganization.IntegrateRaisedCorps(faction,corps,false);
Check(corps.transform.parent == null && corps.istopunit, "Failed HQ creation preserves Corps");
faction = Reset(); var first = Add(faction,16); var second = Add(faction,16);
AiArmyOrganization.Consolidate(0);
Check(first.transform.parent != null && first.transform.parent == second.transform.parent && faction.ownunits.Count(u=>u.unittyp==18)==1,
    "Two independent Corps consolidate into one Army");
faction = Reset(); first = Add(faction,16); second = Add(faction,16); var group = new ArmyGroup();
group.GTIP_Original_AddArmyGroupUnit(first,false,false); group.GTIP_Original_AddArmyGroupUnit(second,false,false);
AiArmyOrganization.Consolidate(0);
Check(!group.Destroyed && group.Units.Count == 1 && group.Units[0].unittyp == 18 && first.transform.parent == second.transform.parent,
    "Consolidation preserves Army Group and replaces Corps membership with Army");
faction = Reset(); first = Add(faction,16); AiArmyOrganization.Consolidate(0);
Check(first.transform.parent == null, "Lone existing Corps is not given an unnecessary HQ");

string root = Path.Combine(Path.GetTempPath(), "gtip-ai-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root,"Config")); Application.dataPath = Path.Combine(root,"Game_Data");
string ReadLog(string path) { using var reader = new StreamReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)); return reader.ReadToEnd(); }
void ResetLog()
{
    AiOrganizationDiagnostics.ResetSession();
    foreach(var field in new[] { "checkedConfig", "enabled" }) typeof(AiOrganizationDiagnostics).GetField(field,BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,false);
    AICampaign.NativeAction = null; AICampaign.Calls = 0;
}
ResetLog(); AiOrganizationDiagnostics.Recruit(0);
Check(AICampaign.Calls == 1 && !Directory.Exists(Path.Combine(root,"GTIP-Logs")), "Absent config invokes original once without creating logs");
File.WriteAllText(Path.Combine(root,"Config","GTIP-ai-logging.txt"),"enabled=true\n");
ResetLog(); faction = Reset(); corps = Add(faction,16); corps.name = "Quoted \"corps\", name";
AICampaign.NativeAction = () => corps.name = "Changed";
AiOrganizationDiagnostics.Recruit(0);
string log = Directory.GetFiles(Path.Combine(root,"GTIP-Logs")).Single(); string content = ReadLog(log);
Check(content.Contains("\"snapshot\"") && content.Contains("\"before\"") && content.Contains("\"after\"") && content.Contains("Quoted \"\"corps\"\", name"), "Snapshot and before/after CSV safely quote names");
BattleUnits.completeunitlist = BattleUnits.completeunitlist.ToList(); // Native UpdateUnitList routinely replaces the list.
int lines = ReadLog(log).Split('\n').Length;
AICampaign.NativeAction = null; AiOrganizationDiagnostics.Recruit(0);
Check(ReadLog(log).Split('\n').Length == lines && AICampaign.Calls == 2, "No-op pass/list refresh neither floods nor rotates logs");
((GameVars.Clock)GameVars.uniStormSystem).dayCounter++;
AiOrganizationDiagnostics.Recruit(0);
Check(ReadLog(log).Split('\n').Length > lines, "New campaign day produces another snapshot");
var originalError = new InvalidOperationException("native failure"); AICampaign.NativeAction = () => throw originalError;
Exception observed = null; try { AiOrganizationDiagnostics.Recruit(0); } catch(Exception e) { observed = e; }
Check(ReferenceEquals(originalError,observed) && AICampaign.Calls==4, "Native exception propagates without retry");
AiOrganizationDiagnostics.ResetSession(); AICampaign.NativeAction = null; AiOrganizationDiagnostics.Recruit(0);
Check(Directory.GetFiles(Path.Combine(root,"GTIP-Logs")).Length==2, "Menu/session reset creates a new log without overwriting old log");
ResetLog(); Application.dataPath = Path.Combine(root,"Blocked","Game_Data"); Directory.CreateDirectory(Path.Combine(root,"Blocked","Config"));
File.WriteAllText(Path.Combine(root,"Blocked","Config","GTIP-ai-logging.txt"),"enabled=true");
File.WriteAllText(Path.Combine(root,"Blocked","GTIP-Logs"),"blocks directory creation");
AiOrganizationDiagnostics.Recruit(0); AiOrganizationDiagnostics.Recruit(0);
Check(AICampaign.Calls==2 && Debug.Warnings==1, "Filesystem failure disables logging once, leaving native AI calls intact");
AiOrganizationDiagnostics.ResetSession();
Directory.Delete(root,true);
Console.WriteLine("AI checks use game stubs; live campaign validation is still required.");
