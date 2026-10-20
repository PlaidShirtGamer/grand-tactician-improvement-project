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
            AuditEconomy(rows,revision,readback);
            var states=rows.Where(r=>r.Kind==4).ToDictionary(r=>r.Id,ReadData<MpFormationState>);
            if(readback)foreach(var pair in states) {
                var p=units[pair.Key].transform.position;var s=pair.Value;
                if(!MpRules.Finite(p.x) || !MpRules.Finite(p.y) || !MpRules.Finite(p.z) || Mathf.Abs(p.x-s.X)>.1f || Mathf.Abs(p.z-s.Z)>.1f) {
                    string Coordinate(float value)=>value.ToString("R",CultureInfo.InvariantCulture);
                    string failure="Native map position diverged: formation="+pair.Key+" revision="+revision+
                        " expected=("+Coordinate(s.X)+","+Coordinate(s.Y)+","+Coordinate(s.Z)+") actual=("+Coordinate(p.x)+","+Coordinate(p.y)+","+Coordinate(p.z)+") parent="+s.Parent;
                    LogDiagnostic(failure);WriteMapPoseFailure(rows,states,pair.Key,revision);
                    throw new InvalidDataException(failure);
                }
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
        void WriteMapPoseFailure(MpStateRow[] rows,Dictionary<int,MpFormationState> states,int failed,int revision)
        {
            if(diagnosticPath==null)return;
            try {
                var names=rows.Where(r=>r.Kind==3).ToDictionary(r=>r.Id,r=>r.Name??"");
                var csv=new StringBuilder("revision,id,name,parent,nativeParent,active,expectedX,expectedY,expectedZ,actualX,actualY,actualZ,cachedX,cachedY,cachedZ,blockX,blockY,blockZ\n");
                string F(float value)=>value.ToString("R",CultureInfo.InvariantCulture);
                string Position(Vector3 value)=>F(value.x)+","+F(value.y)+","+F(value.z);
                var seen=new HashSet<int>();int id=failed;
                while(id!=0 && seen.Count<128 && seen.Add(id) && states.TryGetValue(id,out var state) && units.TryGetValue(id,out var unit)) {
                    var cached=NativeAccess.Get<Vector3>(unit,"currentgameobjectposition");
                    string block=unit.blockobject!=null && unit.blockobject.Length>0 && unit.blockobject[0]!=null?Position(unit.blockobject[0].transform.position):",,";
                    csv.Append(revision).Append(',').Append(id).Append(",\"").Append(names.TryGetValue(id,out var name)?name.Replace("\"","\"\""):"").Append("\",")
                        .Append(state.Parent).Append(',').Append(ParentId(unit)).Append(',').Append(unit.gameObject.activeSelf).Append(',')
                        .Append(Position(new Vector3(state.X,state.Y,state.Z))).Append(',').Append(Position(unit.transform.position)).Append(',').Append(Position(cached)).Append(',').Append(block).Append('\n');
                    id=state.Parent;
                }
                File.WriteAllText(diagnosticPath+".pose-failure.csv",csv.ToString());
            } catch(Exception error){LogDiagnostic("Map pose failure detail unavailable: "+error.Message);}
        }
    }
}
