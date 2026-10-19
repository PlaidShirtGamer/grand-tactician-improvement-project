using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        readonly Dictionary<int,int> pathSamples=new Dictionary<int,int>();
        readonly MpPathOrder receivedPaths=new MpPathOrder();
        int nextPathSample;
        MpStateRow CaptureMapPath(int id,Regiment u)
        {
            var lengths=new int[u.regimentpaths];var corners=new List<float>();
            for(int i=0;i<u.regimentpaths;i++) {
                var points=u.regimentpath[i].corners;lengths[i]=points.Length;
                foreach(var p in points){corners.Add(p.x);corners.Add(p.y);corners.Add(p.z);}
            }
            return DataRow(28,id,new MpPathState{Sample=pathSamples.TryGetValue(id,out int sample)?sample:0,Lengths=lengths,Status=(u.pathstatus??new int[0]).Take(u.regimentpaths).ToArray(),Corners=corners.ToArray(),BlockPath=u.blockobjectcurrentpath?.ToArray()??new int[0],BlockCorner=u.blockobjectcurrentcorner?.ToArray()??new int[0],
                Destinations=(u.lastwaypointposition??new Vector3[0]).SelectMany(p=>new[]{p.x,p.y,p.z}).ToArray(),DestinationYaw=u.lastwaypointrotation?.ToArray()??new float[0]});
        }
        void CaptureMapPaths(List<MpStateRow> rows)
        {
            foreach(var pair in units)rows.Add(CaptureMapPath(pair.Key,pair.Value));
        }
        void SendFastPaths(Regiment root)
        {
            if(!host || !authenticated || phase!="live" || root==null || root.alliance!=guestSide)return;
            try {CaptureAndSendFastPaths(root);}
            catch(System.Exception error){LogDiagnostic("Priority route deferred to validated campaign snapshot: "+error.Message);}
        }
        void CaptureAndSendFastPaths(Regiment root)
        {
            int sample=checked(++nextPathSample);
            var rows=new List<MpStateRow>();
            foreach(var u in root.GetComponentsInChildren<Regiment>().Concat(root.allattachedunits??new Regiment[0]).Append(root).Distinct()) {
                if(u==null || u.alliance!=guestSide || !ids.TryGetValue(u,out int id))continue;
                pathSamples[id]=sample;rows.Add(CaptureMapPath(id,u));
            }
            for(int i=0;i<rows.Count;i+=8)SendPathBatch(rows.Skip(i).Take(8).ToArray());
            LogDiagnostic("Priority native route sent: sample="+sample+"; records="+rows.Count);
        }
        void SendPathBatch(MpStateRow[] rows)
        {
            var bytes=MpStateCodec.Encode(DataSchemas,rows);
            if(bytes.Length>MpCheckpoint.ChunkBytes) {
                if(rows.Length<=1){LogDiagnostic("Large native route deferred to campaign snapshot.");return;}
                int mid=rows.Length/2;SendPathBatch(rows.Take(mid).ToArray());SendPathBatch(rows.Skip(mid).ToArray());return;
            }
            Send(new MpMessage{kind="movement-paths",session=session,blob=bytes});
        }
        void ReceiveFastPaths(MpMessage message)
        {
            var rows=MpStateCodec.Decode(DataSchemas,message.blob);
            if(rows.Length>8 || rows.Any(r=>r.Kind!=28 || r.Deleted))throw new InvalidDataException("Invalid native route update.");
            ApplyMapPaths(rows.Where(r=>units.TryGetValue(r.Id,out var u) && u!=null && u.alliance==guestSide).ToArray());
            if(rows.Length>0)LogDiagnostic("Priority native route received: sample="+rows.Max(r=>ReadData<MpPathState>(r).Sample)+"; records="+rows.Length);
        }
        void ApplyMapPaths(MpStateRow[] changes)
        {
            foreach(var row in changes.Where(r=>r.Kind==28 && !r.Deleted)) {
                if(!units.TryGetValue(row.Id,out var u))throw new InvalidDataException("Unknown path formation.");
                var s=ReadData<MpPathState>(row);MpWorldRules.Path(s);
                if(!receivedPaths.Accept(row.Id,s.Sample))continue;
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
                if(u.pathstatus==null || u.pathstatus.Length<=s.Status.Length)System.Array.Resize(ref u.pathstatus,s.Status.Length+1);
                System.Array.Copy(s.Status,u.pathstatus,s.Status.Length);u.regimentpaths=s.Lengths.Length;
                u.blockobjectcurrentpath=s.BlockPath;u.blockobjectcurrentcorner=s.BlockCorner;
                u.lastwaypointposition=Enumerable.Range(0,s.DestinationYaw.Length).Select(i=>new Vector3(s.Destinations[i*3],s.Destinations[i*3+1],s.Destinations[i*3+2])).ToArray();
                u.lastwaypointrotation=s.DestinationYaw;
                // The native renderer caches its mesh until selection or the current corner
                // changes. A new host route can retain both, so explicitly invalidate it.
                u.DeletePathArrows();
                if(u.blockobjectcurrentpath.Length>0)u.ShowPathArrows();
                RefreshReplicaMapDestinations(u);
            }
        }
        static void RefreshReplicaMapDestinations(Regiment u)
        {
            // Native destination squares are a separate renderer from the blue route
            // ribbon. Use the host's per-block endpoint/rotation, never guest navigation.
            var rendered=NativeAccess.Get<GameObject[]>(u,"waypoint");
            if(rendered!=null && rendered.Length!=u.blockobjects) {
                // Native cleanup loops over the current block count. Destroy every
                // old marker before a fleet/formation block-count change can orphan it.
                foreach(var marker in rendered)if(marker!=null)Object.Destroy(marker);
                NativeAccess.Set(u,"waypoint",null);rendered=null;
            }
            if(u.regimentpaths<=0) {
                if(rendered!=null)u.ShowWaypoints(true);
                return;
            }
            if(u.blockobjects<=0 || u.lastwaypointposition==null || u.lastwaypointrotation==null ||
                u.lastwaypointposition.Length<u.blockobjects || u.lastwaypointrotation.Length<u.blockobjects ||
                (u.regimentpaths>0 && (u.regimentpath==null || u.regimentpath.Length<u.regimentpaths || u.regimentpath[u.regimentpaths-1]==null))) {
                if(rendered!=null)u.ShowWaypoints(true);
                return;
            }
            u.ShowWaypoints();
        }
    }
}
