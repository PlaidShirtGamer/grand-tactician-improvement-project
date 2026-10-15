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
                for(int a=0;a<data.Length;a++)rows.Add(DataRow(14,checked(r*GameVars.alliance.Length+a),data[a]));
            }
            var chart=Economy.goodschartref;
            if(chart?.chartdatabase==null)return;
            for(int i=0;i<chart.chartdatabase.Count;i++) {
                var d=chart.chartdatabase[i];
                rows.Add(DataRow(16,i,new MpChartPoints{Time=d.time.ToArray(),Value=d.value.ToArray()}));
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
            foreach(var row in changes.Where(r=>r.Kind==16)) {
                var chart=Economy.goodschartref;
                if(row.Deleted || chart?.chartdatabase==null || row.Id<0 || row.Id>=chart.chartdatabase.Count)throw new InvalidDataException("Unknown goods chart dataset.");
                var s=ReadData<MpChartPoints>(row);MpPresentationRules.ValidateChart(s);
                var d=chart.chartdatabase[row.Id];d.time=s.Time.ToList();d.value=s.Value.ToList();changed=true;
            }
            if(changed)RefreshGoodsView();
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
