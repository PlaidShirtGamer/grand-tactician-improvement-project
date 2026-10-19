using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        readonly Dictionary<int,MpBuildingProduction> buildingProduction=new Dictionary<int,MpBuildingProduction>();
        readonly Dictionary<GameObject,int> productionRowsRefreshed=new Dictionary<GameObject,int>();
        int productionRevision;
        void ClearBuildingEconomy(){buildingProduction.Clear();productionRowsRefreshed.Clear();productionRevision=0;}
        void CaptureBuildingEconomy(List<MpStateRow> rows)
        {
            var caps=Economy.resources.Select(r=>r.maxprice).ToArray();
            foreach(var pair in worldBuildings) {
                var b=pair.Value;
                var source=new MpBuildingProduction {
                    ProducedGoods=b.producedgoods?.ToArray()??Array.Empty<int>(),
                    RankedGoods=MpProductionRules.RankSales(b.stock?.Length??0,b.historicaltrades?.Select(t=>new MpProductionSale{Resource=t.resid,Direction=t.buyorsell,Pcs=t.pcs,Price=t.price}),caps)
                };
                buildingProduction[pair.Key]=source;
                rows.Add(DataRow(40,pair.Key,source));rows.Add(DataRow(41,pair.Key,CaptureBuildingHistory(b)));
            }
            productionRevision++;
        }
        static MpBuildingHistory CaptureBuildingHistory(CBuilding b) => new MpBuildingHistory {
            ProfitValues=b.profitpermonth?.values,ProfitTime=b.profitpermonth?.campaigndate,ProfitRhythm=b.profitpermonth?.updaterythm??0,ProfitName=b.profitpermonth?.name,
            VolumeValues=b.volumesold?.values,VolumeTime=b.volumesold?.campaigndate,VolumeRhythm=b.volumesold?.updaterythm??0,VolumeName=b.volumesold?.name,
            TaxValues=b.corporatetax?.values,TaxTime=b.corporatetax?.campaigndate,TaxRhythm=b.corporatetax?.updaterythm??0,TaxName=b.corporatetax?.name,
            EfficiencyValues=b.historicefficiency?.values,EfficiencyTime=b.historicefficiency?.campaigndate,EfficiencyRhythm=b.historicefficiency?.updaterythm??0,EfficiencyName=b.historicefficiency?.name,
            WorkforceValues=b.historicworkforce?.values,WorkforceTime=b.historicworkforce?.campaigndate,WorkforceRhythm=b.historicworkforce?.updaterythm??0,WorkforceName=b.historicworkforce?.name
        };
        static Economy.TrailingData ApplyBuildingHistorySeries(Economy.TrailingData target,float[] values,float[] times,float rhythm,string name)
        {
            if(values==null)return null;
            target=target??new Economy.TrailingData(0f);
            target.values=(float[])values.Clone();target.campaigndate=(float[])times.Clone();target.updaterythm=rhythm;target.name=name;return target;
        }
        void ApplyBuildingEconomy(MpStateRow[] changes)
        {
            bool changed=false;
            foreach(var row in changes.Where(r=>r.Kind==18 && !r.Deleted)) {
                if(!worldBuildings.TryGetValue(row.Id,out var b))throw new InvalidDataException("Unknown building values.");
                string mismatch=MpStateBaseline.FirstDifference(DataSchemas[18],row.Values,DataSchemas[18].Capture(b));
                if(mismatch!=null)throw new InvalidDataException("Building values failed native readback: "+row.Id+" "+mismatch);
            }
            foreach(var row in changes.Where(r=>r.Kind==40 || r.Kind==41)) {
                if(row.Deleted){if(worldBuildings.ContainsKey(row.Id))throw new InvalidDataException("Live building lost production history.");buildingProduction.Remove(row.Id);continue;}
                if(!worldBuildings.TryGetValue(row.Id,out var b) || b==null)throw new InvalidDataException("Unknown building production record.");
                if(row.Kind==40) {
                    var source=ReadData<MpBuildingProduction>(row);MpProductionRules.Production(source,Economy.resources.Length);
                    b.producedgoods=source.ProducedGoods.ToList();buildingProduction[row.Id]=source;
                } else {
                    var s=ReadData<MpBuildingHistory>(row);MpProductionRules.History(s);
                    b.profitpermonth=ApplyBuildingHistorySeries(b.profitpermonth,s.ProfitValues,s.ProfitTime,s.ProfitRhythm,s.ProfitName);
                    b.volumesold=ApplyBuildingHistorySeries(b.volumesold,s.VolumeValues,s.VolumeTime,s.VolumeRhythm,s.VolumeName);
                    b.corporatetax=ApplyBuildingHistorySeries(b.corporatetax,s.TaxValues,s.TaxTime,s.TaxRhythm,s.TaxName);
                    b.historicefficiency=ApplyBuildingHistorySeries(b.historicefficiency,s.EfficiencyValues,s.EfficiencyTime,s.EfficiencyRhythm,s.EfficiencyName);
                    b.historicworkforce=ApplyBuildingHistorySeries(b.historicworkforce,s.WorkforceValues,s.WorkforceTime,s.WorkforceRhythm,s.WorkforceName);
                }
                var actual=row.Kind==40?DataSchemas[40].Capture(new MpBuildingProduction{ProducedGoods=b.producedgoods.ToArray(),RankedGoods=buildingProduction[row.Id].RankedGoods}):DataSchemas[41].Capture(CaptureBuildingHistory(b));
                string mismatch=MpStateBaseline.FirstDifference(DataSchemas[row.Kind],row.Values,actual);
                if(mismatch!=null)throw new InvalidDataException("Building production failed native readback: "+row.Id+" "+mismatch);
                changed=true;
            }
            if(changed || changes.Any(r=>r.Kind==18))productionRevision++;
        }
        void RefreshProductionRows()
        {
            if(productionView==null || !productionView.isActiveAndEnabled)return;
            var rows=NativeAccess.Get<IEnumerable>(productionView,"industryrows");if(rows==null)return;
            var economy=NativeAccess.Get<Economy>(productionView,"economy");if(economy==null)return;
            // Retain native row objects, filter selection and scroll position. Only
            // refresh labels/icons after values change or a native row is created.
            foreach(var row in rows) {
                var root=NativeAccess.Get<GameObject>(row,"rowobject");var b=NativeAccess.Get<CBuilding>(row,"cbuildingref");
                if(root==null || b==null || (productionRowsRefreshed.TryGetValue(root,out int revision) && revision==productionRevision))continue;
                int id=BuildingReference(b);if(!buildingProduction.TryGetValue(id,out var source))continue;
                void Text(string path,string value){var text=root.transform.Find(path)?.GetComponent<TextMeshProUGUI>();if(text!=null)text.text=value;}
                Text("Workforce",b.absoluteworkforce.ToString("0"));Text("Slaves",(b.slaveratio*100).ToString("0")+"%");
                if(b.volumesold!=null)Text("Value","$ "+Tools.GetAbrevStrValue(b.volumesold.GetSum(),GamePrefs.commafiguresfinancial));
                if(b.corporatetax!=null)Text("Tax","$ "+Tools.GetAbrevStrValue(b.corporatetax.GetTtmValue(),GamePrefs.commafiguresfinancial));
                string tooltip="";
                for(int i=0;i<4;i++) {
                    var icon=root.transform.Find("Production/880_List/Prod"+(i+1));if(icon==null)continue;
                    bool shown=i<source.RankedGoods.Length && b.producedpcspa!=null && source.RankedGoods[i]<b.producedpcspa.Length;
                    icon.gameObject.SetActive(shown);var amount=icon.Find("Amount");if(amount!=null)amount.gameObject.SetActive(shown);
                    if(!shown)continue;
                    int resource=source.RankedGoods[i];icon.GetComponent<Image>().sprite=economy.resourceicons[resource];
                    if(amount!=null)amount.GetComponent<TextMeshProUGUI>().text=Tools.GetAbrevStrValue(b.producedpcspa[resource],GamePrefs.commafiguresfinancial,useplus:true);
                    tooltip+=Economy.resources[resource].name+": "+Tools.GetAbrevStrValue(b.producedpcspa[resource],GamePrefs.commafiguresfinancial)+" pcs\n";
                }
                root.transform.Find("Production/880_List")?.GetComponent<ButtonTooltip>()?.ExchangeTooltipText("Company production per annum",tooltip);
                root.transform.Find("Production/880_List/Plus")?.gameObject.SetActive(source.ProducedGoods.Length>4);
                for(int i=0;i<6;i++)root.transform.Find("Efficiency/Experience/"+(865+i)+"_Efficiency")?.gameObject.SetActive(b.avgprodefficiency>=i*.167f && b.avgprodefficiency<(i+1)*.167f);
                productionRowsRefreshed[root]=productionRevision;
            }
            if(productionRowsRefreshed.Count>1000)foreach(var row in productionRowsRefreshed.Keys.Where(r=>r==null).ToArray())productionRowsRefreshed.Remove(row);
        }
    }
}
