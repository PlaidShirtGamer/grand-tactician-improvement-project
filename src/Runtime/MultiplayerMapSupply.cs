using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        void CaptureMapSupply(List<MpStateRow> rows)
        {
            foreach(var pair in units) {
                var u=pair.Value;
                var regrouping=NativeAccess.Get<List<Object>>(u,"regroupingareas");
                var references=new List<int>();
                foreach(var place in regrouping??new List<Object>()) {
                    if(place is CBuilding building && BuildingReference(building)>0)references.Add(BuildingReference(building));
                    else if(place is Town town) {
                        int index=BattleUnits.towns.IndexOf(town);
                        if(index>=0)references.Add(-index-1);
                    }
                }
                rows.Add(DataRow(42,pair.Key,new MpMapSupplyState{Regrouping=references.Distinct().ToArray(),
                    Depots=(u.supplydepotsconnected??new List<CBuilding>()).Select(BuildingReference).Where(id=>id>0).Distinct().ToArray()}));
            }
        }
        void ApplyMapSupply(MpStateRow[] changes)
        {
            foreach(var row in changes.Where(r=>r.Kind==42 && !r.Deleted)) {
                if(!units.TryGetValue(row.Id,out var u))throw new InvalidDataException("Unknown supply display formation.");
                var value=ReadData<MpMapSupplyState>(row);
                MpMapSupplyRules.Validate(value,BattleUnits.towns.Count,id=>worldBuildings.TryGetValue(id,out var b) && b!=null);
                var places=value.Regrouping.Select(id=>id>0?(Object)worldBuildings[id]:BattleUnits.towns[-id-1]).ToList();
                NativeAccess.Set(u,"regroupingareas",places);
                u.supplydepotsconnected=value.Depots.Select(id=>worldBuildings[id]).ToList();
                // These are native display calls. Supply ownership, readiness and
                // source selection remain untouched; the host supplies the topology.
                u.UpdateRegroupingRadius();
                if(ui.selectedunit==u.gameObject && ResourceTree.ReferenceUsed(u.gameObject))ResourceTree.HideResourceTree();
            }
        }
    }
}
