using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    // Observes method boundaries only: no orders, random draws, or unit writes.
    public static class AiOrganizationDiagnostics
    {
        static bool checkedConfig, enabled;
        static StreamWriter writer;
        static object world;
        static string lastDay;
        static long sequence;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const long MaximumBytes = 100 * 1024 * 1024;

        public static void Recruit(int faction) => Observe(typeof(AICampaign), "Recruit", faction);
        public static void Combine(AICampaign ai, int faction) => Observe(ai, "Combine", faction, () => AiArmyOrganization.Consolidate(faction));
        public static void Transfer(AICampaign ai, int faction) => Observe(ai, "Transfer", faction);
        public static void CombineCombat(int faction) => Observe(typeof(AICampaign), "CombineCombat", faction);
        public static void ManageGroups(AICampaign ai, int faction) => Observe(ai, "ManageGroups", faction);

        static void Observe(object target, string operation, int faction, Action additional = null)
        {
            Dictionary<int, string> before = null;
            long eventId = 0;
            Safe(() => {
                if (!checkedConfig)
                {
                    checkedConfig = true;
                    string config = Path.Combine(Root, "Config", "GTIP-ai-logging.txt");
                    enabled = File.Exists(config) && File.ReadAllLines(config)
                        .Any(line => line.Trim().Equals("enabled=true", StringComparison.OrdinalIgnoreCase));
                }
                if (!enabled || !SixLevelRuntime.Active || BattleUnits.completeunitlist == null) return;
                var campaignRoot = NativeAccess.Get<object>(typeof(BattleUnits), "unitfolder");
                if (writer == null || !ReferenceEquals(world, campaignRoot))
                {
                    Close();
                    string folder = Path.Combine(Root, "GTIP-Logs");
                    Directory.CreateDirectory(folder);
                    writer = new StreamWriter(new FileStream(Path.Combine(folder,
                        "ai-organization-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", Inv) + "-" + Guid.NewGuid().ToString("N") + ".csv"),
                        FileMode.CreateNew, FileAccess.Write, FileShare.Read));
                    writer.WriteLine("utc,campaign_date,event,operation,ai_faction_index,phase,instance_id,name,native_type,faction,parent_id,army_group_id,army_group_name,commander_id,strength,direct_infantry,direct_cavalry,direct_artillery,direct_other_combat,direct_brigades,direct_divisions,direct_corps,direct_armies,child_ids");
                    world = campaignRoot;
                }
                before = Capture();
                eventId = ++sequence;
                var clock = NativeAccess.Get<object>(typeof(GameVars), "uniStormSystem");
                string day = clock == null ? "unknown" : NativeAccess.Get<int>(clock, "monthCounter") + "/" + NativeAccess.Get<int>(clock, "dayCounter");
                if (lastDay != day)
                {
                    foreach (var row in before.Values) Write(eventId, "snapshot", faction, "snapshot", row);
                    lastDay = day;
                }
                writer.Flush();
            });
            // Native exceptions must propagate; diagnostic failures must not.
            try { NativeAccess.Call(target, "GTIP_Original_" + operation, faction); additional?.Invoke(); }
            finally
            {
                Safe(() => {
                    if (!enabled || before == null || writer == null) return;
                    var after = Capture();
                    foreach (int id in before.Keys.Union(after.Keys).OrderBy(id => id))
                    {
                        before.TryGetValue(id, out string previous);
                        after.TryGetValue(id, out string current);
                        if (previous == current) continue;
                        if (previous != null) Write(eventId, operation, faction, current == null ? "removed" : "before", previous);
                        if (current != null) Write(eventId, operation, faction, previous == null ? "created" : "after", current);
                    }
                    writer.Flush();
                    if (writer.BaseStream.Position >= MaximumBytes) { enabled = false; Close(); }
                });
            }
        }

        static string Root => Path.GetDirectoryName(Application.dataPath);
        static Dictionary<int, string> Capture()
        {
            var units = BattleUnits.completeunitlist.Where(u => u != null).ToArray();
            var children = units.ToLookup(u => u.transform.parent == null ? null : u.transform.parent.GetComponent<Regiment>());
            var result = new Dictionary<int, string>();
            foreach (var unit in units)
            {
                var parent = unit.transform.parent == null ? null : unit.transform.parent.GetComponent<Regiment>();
                var direct = children[unit].ToArray();
                var group = unit.assignedarmygroup;
                result[unit.GetInstanceID()] = Csv(unit.GetInstanceID(), unit.name, unit.unittyp, unit.alliance,
                    parent == null ? 0 : parent.GetInstanceID(), group == null ? 0 : group.GetInstanceID(),
                    group == null ? "" : group.name, unit.commander, unit.unittyp < 14 ? unit.strength : unit.groupstrengthactive,
                    direct.Count(u => u.unittyp == 0), direct.Count(u => u.unittyp == 1), direct.Count(u => u.unittyp == 2),
                    direct.Count(u => u.unittyp > 2 && u.unittyp < 14), direct.Count(u => u.unittyp == 14),
                    direct.Count(u => u.unittyp == 15), direct.Count(u => u.unittyp == 16), direct.Count(u => u.unittyp == 18),
                    string.Join(";", direct.Select(u => u.GetInstanceID()).OrderBy(id => id)));
            }
            return result;
        }

        internal static string Csv(params object[] values) => string.Join(",", values.Select(value =>
            "\"" + Convert.ToString(value, Inv).Replace("\"", "\"\"") + "\""));
        static void Write(long id, string operation, int faction, string phase, string row) =>
            writer.WriteLine(Csv(DateTime.UtcNow.ToString("O", Inv), GameVars.GetCampaignFloatingDate(), id, operation, faction, phase) + "," + row);
        static void Safe(Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                enabled = false;
                try { Close(); Debug.LogWarning("[GTIP] AI logging stopped: " + error.Message); } catch { }
            }
        }
        static void Close() { var old = writer; writer = null; world = null; lastDay = null; old?.Dispose(); }
        public static void ResetSession() { Safe(Close); }
    }
}
