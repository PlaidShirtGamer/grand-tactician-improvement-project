using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        void CaptureMapPaths(List<MpStateRow> rows)
        {
            foreach(var pair in units) {
                var u=pair.Value;var lengths=new int[u.regimentpaths];var corners=new List<float>();
                for(int i=0;i<u.regimentpaths;i++) {
                    var points=u.regimentpath[i].corners;lengths[i]=points.Length;
                    foreach(var p in points){corners.Add(p.x);corners.Add(p.y);corners.Add(p.z);}
                }
                rows.Add(DataRow(28,pair.Key,new MpPathState{Lengths=lengths,Status=(u.pathstatus??new int[0]).Take(u.regimentpaths).ToArray(),Corners=corners.ToArray(),BlockPath=u.blockobjectcurrentpath?.ToArray()??new int[0],BlockCorner=u.blockobjectcurrentcorner?.ToArray()??new int[0]}));
            }
        }
        void ApplyMapPaths(MpStateRow[] changes)
        {
            foreach(var row in changes.Where(r=>r.Kind==28 && !r.Deleted)) {
                if(!units.TryGetValue(row.Id,out var u))throw new InvalidDataException("Unknown path formation.");
                var s=ReadData<MpPathState>(row);MpWorldRules.Path(s);
                if(u.regimentpath==null || u.regimentpath.Length<s.Lengths.Length)System.Array.Resize(ref u.regimentpath,s.Lengths.Length);
                int offset=0;
                for(int i=0;i<s.Lengths.Length;i++) {
                    if(u.regimentpath[i]==null)u.regimentpath[i]=new NavMeshPath();
                    var points=new Vector3[s.Lengths[i]];
                    for(int j=0;j<points.Length;j++) {points[j]=new Vector3(s.Corners[offset],s.Corners[offset+1],s.Corners[offset+2]);offset+=3;}
                    // Unity's public path is read-only. Its managed corner cache is the data
                    // native presentation reads; no guest pathfinding or movement is run.
                    NativeAccess.Set(u.regimentpath[i],"m_Corners",points);
                }
                if(u.pathstatus==null || u.pathstatus.Length<s.Status.Length)System.Array.Resize(ref u.pathstatus,s.Status.Length);
                System.Array.Copy(s.Status,u.pathstatus,s.Status.Length);u.regimentpaths=s.Lengths.Length;
                u.blockobjectcurrentpath=s.BlockPath;u.blockobjectcurrentcorner=s.BlockCorner;
            }
        }
    }
}
