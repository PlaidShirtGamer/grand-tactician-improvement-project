using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        void CaptureGoods(List<MpStateRow> rows)
        {
            if(Economy.resources==null)return;
            for(int r=0;r<Economy.resources.Length;r++) {
                var data=Economy.resources[r].productalliancedata;
                for(int a=0;a<data.Length;a++) {
                    rows.Add(DataRow(14,checked(r*GameVars.alliance.Length+a),data[a]));
                    for(int partner=0;partner<GameVars.alliance.Length;partner++) {
                        int id=MpEconomyRules.FlowId(r,a,partner,GameVars.alliance.Length);
                        rows.Add(DataRow(32,id,data[a].importvalue[partner]));
                        rows.Add(DataRow(32,id+1,data[a].exportvalue[partner]));
                    }
                }
            }
            for(int a=0;a<GameVars.alliance.Length;a++) {
                rows.Add(DataRow(33,a*2,GameVars.alliance[a].constructioncostspa));
                rows.Add(DataRow(33,a*2+1,GameVars.alliance[a].weaponcostspa));
            }
            CaptureChart(rows,Economy.goodschartref,16);
            CaptureChart(rows,Economy.financeschartref,31);
        }
        static void CaptureChart(List<MpStateRow> rows,Chart chart,int kind)
        {
            if(chart==null || chart.chartdatabase==null)return;
            for(int i=0;i<chart.chartdatabase.Count;i++) {
                var d=chart.chartdatabase[i];
                rows.Add(DataRow(kind,i,new MpChartPoints{Time=d.time.ToArray(),Value=d.value.ToArray()}));
            }
        }
        void ApplyGoods(MpStateRow[] changes)
        {
            bool changed=false;
            foreach(var row in changes.Where(r=>r.Kind==14)) {
                int resource=row.Id/GameVars.alliance.Length,side=row.Id%GameVars.alliance.Length;
                if(row.Deleted || Economy.resources==null || resource<0 || resource>=Economy.resources.Length || side>=Economy.resources[resource].productalliancedata.Length)
                    throw new InvalidDataException("Unknown goods/alliance record.");
                var target=Economy.resources[resource].productalliancedata[side];
                DataSchemas[14].Apply(target,row.Values);
                if(MpStateBaseline.FirstDifference(DataSchemas[14],row.Values,DataSchemas[14].Capture(target))!=null)
                    throw new InvalidDataException("Goods data failed native readback.");
                changed=true;
            }
            bool goodsChart=false,financeChart=false;
            foreach(var row in changes.Where(r=>r.Kind==16 || r.Kind==31)) {
                var chart=row.Kind==16?Economy.goodschartref:Economy.financeschartref;
                if(row.Deleted || chart==null || chart.chartdatabase==null || row.Id<0 || row.Id>=chart.chartdatabase.Count)throw new InvalidDataException("Unknown economy chart dataset.");
                var s=ReadData<MpChartPoints>(row);MpPresentationRules.ValidateChart(s);
                var d=chart.chartdatabase[row.Id];d.time=s.Time.ToList();d.value=s.Value.ToList();changed=true;
                if(row.Kind==16)goodsChart=true;else financeChart=true;
            }
            foreach(var row in changes.Where(r=>r.Kind==32 || r.Kind==33)) {
                Economy.TrailingData target;
                if(row.Deleted)throw new InvalidDataException("Economy history removed.");
                if(row.Kind==32) {
                    int n=GameVars.alliance.Length,record=row.Id/2,partner=record%n,side=(record/n)%n,resource=record/n/n;
                    if(resource<0 || resource>=Economy.resources.Length)throw new InvalidDataException("Unknown trade history.");
                    var data=Economy.resources[resource].productalliancedata[side];
                    target=row.Id%2==0?data.importvalue[partner]:data.exportvalue[partner];
                } else {
                    int side=row.Id/2;if(side<0 || side>=GameVars.alliance.Length)throw new InvalidDataException("Unknown expense history.");
                    target=row.Id%2==0?GameVars.alliance[side].constructioncostspa:GameVars.alliance[side].weaponcostspa;
                }
                var schema=DataSchemas[row.Kind];
                var values=(float[])row.Values[Array.FindIndex(schema.Fields,f=>f.Name=="values")];
                var dates=(float[])row.Values[Array.FindIndex(schema.Fields,f=>f.Name=="campaigndate")];
                MpPresentationRules.ValidateChart(new MpChartPoints{Time=dates,Value=values});
                if(values.Length<2)throw new InvalidDataException("Economy history is too short.");
                schema.Apply(target,row.Values);
                if(MpStateBaseline.FirstDifference(schema,row.Values,schema.Capture(target))!=null)throw new InvalidDataException("Economy history failed native readback.");
                changed=true;
            }
            if(changed)RefreshGoodsView();
            if(goodsChart)Economy.goodschartref.UpdateChart();
            if(financeChart)Economy.financeschartref.UpdateChart();
            if(changed || changes.Any(r=>r.Kind==0))RefreshFinanceValues();
        }
        void RefreshGoodsView()
        {
            var h=ui?.panelhandler;if(h==null || h.panel[h.cpanel_trade].state<=0)return;
            var trade=h.panel[h.cpanel_trade].go.GetComponent<Trade>();if(trade==null)return;
            // Keep the native rows, scroll position and current selection. Rebuilding the
            // whole table every network revision destroys buttons under the pointer.
            var rows=NativeAccess.Get<IEnumerable>(trade,"goodsrows");if(rows==null)return;
            foreach(var row in rows) {
                int id=NativeAccess.Get<int>(row,"resourceid");
                if(id<0 || id>=Economy.resources.Length)continue;
                var root=NativeAccess.Get<GameObject>(row,"lineobjref").transform;
                var d=Economy.resources[id].productalliancedata[GameVars.playeralliance];
                void Text(string name,string value){var t=root.Find(name)?.GetComponent<TextMeshProUGUI>();if(t!=null)t.text=value;}
                Text("Price","$ "+d.averageprice.ToString("0.00"));Text("Demand",Tools.GetAbrevStrValue(d.demandpa));
                Text("Production",Tools.GetAbrevStrValue(d.productionpa));Text("Markets",Tools.GetAbrevStrValue(d.stockiniips));
                Text("Depots",Tools.GetAbrevStrValue(d.stockindepots));Text("Trade",Tools.GetAbrevStrValue(d.totaltradevolume,GamePrefs.commafiguresfinancial));
                Text("Tax",Tools.GetAbrevStrValue(d.excisetax,GamePrefs.commafiguresfinancial));Text("Tariffs",Tools.GetAbrevStrValue(d.tariffs,GamePrefs.commafiguresfinancial));
                int coins=Mathf.Clamp(Mathf.FloorToInt(d.value*6),0,5);
                for(int j=0;j<5;j++){var coin=root.Find("Value/Coin"+j);if(coin!=null)coin.gameObject.SetActive(j<coins);}
            }
            NativeAccess.Call(trade,"UpdateGoodsInformation");
        }
    }
}
