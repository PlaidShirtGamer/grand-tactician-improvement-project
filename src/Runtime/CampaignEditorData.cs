using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed class CampaignEditorRecord
    {
        public object Target;
        public string Title, Detail;
        public int Depth;
        public readonly List<LiveEditField> Fields = new List<LiveEditField>();
    }

    internal static class CampaignEditorData
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        internal static string Tier(int type) => type == 18 ? "Army" : type == 16 ? "Corps" :
            type == 15 ? "Division" : type == 14 ? "Brigade" : type == 17 ? "Fleet" :
            type == 0 ? "Infantry regiment" : type == 1 ? "Cavalry regiment" : type == 2 ? "Artillery" : "Unit " + type;
        internal static string OfficerName(int id) => GameVars.commander != null && id >= 0 && id < GameVars.commander.Count
            ? Rank(GameVars.commander[id].rank) + " " + GameVars.commander[id].combinedname : "Unassigned";
        internal static string Rank(int rank) => GameVars.rankabbrev != null && rank >= 0 && rank < GameVars.rankabbrev.Length
            ? GameVars.rankabbrev[rank] : "Unknown rank";

        internal static List<CampaignEditorRecord> Read(int domain, int side, string search)
        {
            var records = new List<CampaignEditorRecord>();
            if (domain <= 2)
            {
                var units = (BattleUnits.completeunitlist ?? new List<Regiment>()).Where(u => u != null && u.alliance == side).ToArray();
                var included = units.Where(u => domain == 2 ? u.unittyp == 17 : u.unittyp != 17 &&
                    ((BattleUnits.GetHighestParent(u).garrisonreference != null || u.isgarrisonbasicunit) == (domain == 1))).ToArray();
                var byParent = included.ToLookup(u => u.transform.parent == null ? null : u.transform.parent.GetComponent<Regiment>());
                var totals = new Dictionary<Regiment, long>();
                var calculating = new HashSet<Regiment>();
                Func<Regiment, long> strength = null;
                strength = unit => {
                    if (totals.TryGetValue(unit, out long total)) return total;
                    if (!calculating.Add(unit)) return 0;
                    total = unit.unittyp <= 2 ? unit.strength : 0;
                    foreach (var child in byParent[unit]) total += strength(child);
                    calculating.Remove(unit); totals[unit] = total; return total;
                };
                var seen = new HashSet<Regiment>();
                Action<Regiment, int> add = null;
                add = (unit, depth) => {
                    if (!seen.Add(unit)) return;
                    var record = new CampaignEditorRecord { Target = unit, Title = unit.name, Depth = depth,
                        Detail = Tier(unit.unittyp) + "  |  " + OfficerName(unit.commander) + "\n" +
                            strength(unit).ToString("N0", Inv) + " men  |  ID " + unit.GetInstanceID() };
                    record.Fields.Add(Name(() => unit.name, value => unit.name = value, "Name"));
                    if (unit.unittyp <= 2)
                    {
                        Percentage(record, unit, "experience", "Experience (%)");
                        Percentage(record, unit, "training", "Training (%)");
                        Percentage(record, unit, "morale", "Morale (%)");
                        Percentage(record, unit, "conditionmen", "Condition (%)");
                    }
                    records.Add(record);
                    foreach (var child in byParent[unit].OrderBy(u => u.name)) add(child, depth + 1);
                };
                if (domain == 0 && BattleUnits.armygroups != null)
                    foreach (var group in BattleUnits.armygroups.Where(g => g != null && g.attachedunits != null && g.GetAlliance() == side))
                    {
                        var row = new CampaignEditorRecord { Target = group, Title = group.name, Detail = "Army Group | " + OfficerName(group.GetCommanderID()) };
                        row.Fields.Add(Name(() => group.name, value => group.name = value, "Name")); records.Add(row);
                        foreach (var root in group.attachedunits.Where(u => u != null && included.Contains(u)).OrderBy(u => u.name)) add(root, 1);
                    }
                foreach (var unit in included.Where(u => u.transform.parent == null || !included.Contains(u.transform.parent.GetComponent<Regiment>())).OrderBy(u => u.name)) add(unit, 0);
                foreach (var unit in included) add(unit, 0); // Surface malformed/orphaned records too.
            }
            else if (domain == 3 && GameVars.commander != null)
            {
                foreach (var officer in GameVars.commander.Where(c => c != null && c.alliance == side))
                {
                    var row = new CampaignEditorRecord { Target = officer, Title = Rank(officer.rank) + " " + officer.combinedname,
                        Detail = "ID " + GameVars.commander.IndexOf(officer) + "  |  " + (officer.currentcommand == null ? "No current command" : officer.currentcommand.name) +
                            "\nStatus: " + officer.status + "  |  " + (officer.activeincampaign ? "Active in campaign" : "Inactive") };
                    row.Fields.Add(Name(() => officer.firstname, value => officer.firstname = value, "First name"));
                    row.Fields.Add(Name(() => officer.name, value => officer.name = value, "Last name"));
                    foreach (var pair in new[] { "exp|Experience", "fame|Fame", "leadership|Leadership", "initiative|Initiative", "administration|Administration", "cunning|Cunning" })
                    { var parts = pair.Split('|'); Percentage(row, officer, parts[0], parts[1] + " (%)"); }
                    foreach (var pair in new[] { "veteran|Veteran", "westpoint|West Point", "political|Political" })
                    {
                        var parts = pair.Split('|'); string field = parts[0];
                        row.Fields.Add(new LiveEditField { Label = parts[1] + " (true/false)", Read = () => NativeAccess.Get<bool>(officer, field),
                            Write = value => NativeAccess.Set(officer, field, value), Parse = value => bool.Parse(value) });
                    }
                    records.Add(row);
                }
            }
            else if (domain == 4 && GameVars.weapon != null)
            {
                var stock = GameVars.alliance[side].weaponstock;
                for (int index = 0; index < Math.Min(stock.Length, GameVars.weapon.Length); index++)
                {
                    int id = index;
                    var row = new CampaignEditorRecord { Target = GameVars.weapon[id], Title = GameVars.weapon[id].name,
                        Detail = "Weapon ID " + id + "  |  Stock: " + stock[id].ToString("N0", Inv) };
                    row.Fields.Add(new LiveEditField { Label = "Stock", Read = () => stock[id], Write = value => stock[id] = (float)value,
                        Parse = value => LiveEditSession.Number(value, 0, 100000000) });
                    records.Add(row);
                }
            }
            else if (domain == 5 && GameVars.nation != null)
            {
                var alliance = GameVars.alliance[side];
                var treasury = new CampaignEditorRecord { Target = alliance, Title = alliance.name,
                    Detail = "Treasury: " + alliance.treasury.ToString("N0", Inv) + "\nNational cash balance. Projects and policy editing are not yet ported." };
                treasury.Fields.Add(new LiveEditField { Label = "Treasury ($)", Read = () => alliance.treasury,
                    Write = value => alliance.treasury = (float)value, Parse = text => LiveEditSession.Number(text, -100000000, 100000000000) });
                records.Add(treasury);
                foreach (var nation in GameVars.nation.Where(n => n != null))
                {
                    var row = new CampaignEditorRecord { Target = nation, Title = nation.name,
                        Detail = "Population: " + nation.population.ToString("N0", Inv) + "\nAvailable volunteers: " + Math.Max(0, nation.volunteers[side]).ToString("N0", Inv) +
                            " | Draftees: " + Math.Max(0, nation.drafts[side]).ToString("N0", Inv) +
                            "\nRecruit pools and support apply to the selected faction. Population is shared. Pool edits change current balances; the game recalculates them as the campaign advances." };
                    row.Fields.Add(new LiveEditField { Label = "Population (shared)", Read = () => nation.population,
                        Write = value => nation.population = (float)value, Parse = text => LiveEditSession.Number(text, 0, 1000000000) });
                    row.Fields.Add(new LiveEditField { Label = "Support (%)", Read = () => nation.support[side], Write = value => nation.support[side] = (float)value,
                        Parse = text => LiveEditSession.Number(text, 0, 100, 100), Format = value => ((float)value * 100).ToString("0.###", Inv) });
                    foreach (var pool in new[] { new { Label = "Volunteers", Values = nation.volunteers }, new { Label = "Draftees", Values = nation.drafts } })
                    {
                        var balances = pool.Values;
                        row.Fields.Add(new LiveEditField { Label = pool.Label + " balance (negative = deficit)", Read = () => balances[side],
                            Write = value => balances[side] = (int)value, Parse = text => {
                                if (!int.TryParse(text, NumberStyles.Integer, Inv, out int value) || value < 0 || value > 100000000)
                                    throw new InvalidOperationException("Enter a whole number from 0 to 100,000,000 for the available recruit pool.");
                                return value;
                            } });
                    }
                    records.Add(row);
                }
            }
            return string.IsNullOrWhiteSpace(search) ? records : records.Where(r =>
                (r.Title + " " + r.Detail).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        static LiveEditField Name(Func<string> read, Action<string> write, string label) => new LiveEditField {
            Label = label, Read = () => read(), Write = value => write((string)value), Parse = LiveEditSession.Name };
        static void Percentage(CampaignEditorRecord row, object target, string field, string label)
        {
            row.Fields.Add(new LiveEditField { Label = label, Read = () => NativeAccess.Get<float>(target, field),
                Write = value => NativeAccess.Set(target, field, value), Parse = text => LiveEditSession.Number(text, 0, 100, 100),
                Format = value => ((float)value * 100).ToString("0.###", Inv) });
        }
        internal static void Refresh()
        {
            if (GameVars.nation != null && GameVars.alliance != null)
                for (int faction = 0; faction < Math.Min(2, GameVars.alliance.Length); faction++)
                {
                    var activeStates = GameVars.nation.Where(n => n != null && n.recruitable && n.alliance <= 1).ToArray();
                    GameVars.alliance[faction].volunteersavailable = activeStates.Sum(n => (float)Math.Max(0, n.volunteers[faction]));
                    GameVars.alliance[faction].draftsavailable = activeStates.Sum(n => (float)Math.Max(0, n.drafts[faction]));
                }
            foreach (var unit in BattleUnits.completeunitlist.Where(u => u != null && u.unittyp >= 14))
                unit.UpdateGroupStrength(overridecalc: true, ignoreactivestate: true);
            CampaignMainPanel.lastregtextupdate = null;
            ToolsUnitList.SetDirty();
        }
    }
}
