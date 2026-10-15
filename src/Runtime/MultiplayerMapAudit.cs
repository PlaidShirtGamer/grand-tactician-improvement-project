using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        void ApplyMapPoses(MpStateRow[] rows)
        {
            // A parent pose can displace an unchanged child's Transform. Restore absolute
            // poses for the whole source graph, applying only actual differences.
            foreach(var row in rows.Where(r=>r.Kind==4).OrderBy(r=>Depth(units[r.Id]))) {
                var s=ReadData<MpFormationState>(row);var u=units[row.Id];var p=new Vector3(s.X,s.Y,s.Z);var q=Quaternion.Euler(0,s.Yaw,0);
                if((u.transform.position-p).sqrMagnitude>.000001f || Quaternion.Angle(u.transform.rotation,q)>.001f)ApplyPose(u,p,q);
            }
        }
        void AuditNativeMap(MpStateRow[] rows,int revision,bool readback)
        {
            var states=rows.Where(r=>r.Kind==4).ToDictionary(r=>r.Id,ReadData<MpFormationState>);
            if(readback)foreach(var pair in states) {
                var p=units[pair.Key].transform.position;var s=pair.Value;
                if(Mathf.Abs(p.x-s.X)>.1f || Mathf.Abs(p.z-s.Z)>.1f)throw new InvalidDataException("Native map position diverged: formation="+pair.Key+" revision="+revision);
            }
            if(diagnosticPath==null || (revision!=1 && revision%30!=0))return;
            var data=rows.Where(r=>r.Kind==3).ToDictionary(r=>r.Id);
            int Index(string field)=>Array.FindIndex(DataSchemas[3].Fields,f=>f.Name==field);
            int inBattle=Index("inbattle"),retreat=Index("onretreat"),routed=Index("isrouted"),stance=Index("campaignstance"),status=Index("unitstatus");
            var csv=new StringBuilder("revision,id,name,side,parent,visible,active,inBattle,onRetreat,routed,stance,status,x,y,z\n");
            string F(float n)=>n.ToString("R",CultureInfo.InvariantCulture);
            foreach(var pair in states.OrderBy(p=>p.Key)) {
                var s=pair.Value;var row=data[pair.Key];var u=readback?units[pair.Key]:null;var pos=readback?u.transform.position:new Vector3(s.X,s.Y,s.Z);
                string name=(row.Name??"").Replace("\"","\"\"");
                csv.Append(revision).Append(',').Append(pair.Key).Append(",\"").Append(name).Append("\",").Append(s.Side).Append(',').Append(s.Parent).Append(',').Append(s.Visible).Append(',').Append(readback?u.gameObject.activeSelf:s.Active).Append(',')
                    .Append(readback?u.inbattle:(bool)row.Values[inBattle]).Append(',').Append(readback?u.onretreat:(bool)row.Values[retreat]).Append(',').Append(readback?u.isrouted:(bool)row.Values[routed]).Append(',').Append(readback?u.campaignstance:(int)row.Values[stance]).Append(',').Append(readback?u.unitstatus:(int)row.Values[status]).Append(',')
                    .Append(F(pos.x)).Append(',').Append(F(pos.y)).Append(',').Append(F(pos.z)).Append('\n');
            }
            try {File.WriteAllText(diagnosticPath+".state.csv",csv.ToString());}
            catch(Exception e){LogDiagnostic("Map audit file unavailable: "+e.Message);}
        }
    }
}
