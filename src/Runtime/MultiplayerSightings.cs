using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        SpottedEnemy sightingView;
        bool sightingsInitialized;
        int sightingCursor;
        SpottedEnemy SightingView => sightingView!=null?sightingView:(sightingView=EditorFormationOperations.Engine.GetComponent<SpottedEnemy>());
        void CaptureSightings(List<MpStateRow> rows)
        {
            var view=SightingView;if(view?.unitsattached==null)return;
            foreach(var u in view.unitsattached) {
                int id=DispatchReference(u.regref);if(id==0 || u.spottedmarks==null || u.spottedmarks.Count==0)continue;
                var marks=new List<float>();
                foreach(var m in u.spottedmarks.Where(m=>m.obj!=null)) {
                    var p=m.obj.transform.position;var v=m.vectorvariation;
                    marks.AddRange(new[]{p.x,p.y,p.z,m.timeshown,m.intelligence,v.x,v.y,v.z,m.obj.activeSelf?1f:0f});
                }
                rows.Add(DataRow(29,id,new MpSpottedState{LastSpotted=u.lastspotted??"",Marks=marks.ToArray()}));
            }
        }
        void ApplySightings(MpStateRow[] changes)
        {
            var view=SightingView;if(view==null)throw new InvalidOperationException("Native sighting controller unavailable.");
            if(!sightingsInitialized) {
                if(!NativeAccess.Get<bool>(view,"initialized"))view.Initialize();
                for(int i=view.unitsattached.Count-1;i>=0;i--)NativeAccess.Call(view,"RemoveMarks",i);
                view.unitsattached.Clear();sightingsInitialized=true;
            }
            foreach(var row in changes.Where(r=>r.Kind==29)) {
                int index=view.unitsattached.FindIndex(u=>DispatchReference(u.regref)==row.Id);
                if(index>=0){NativeAccess.Call(view,"RemoveMarks",index);view.unitsattached.RemoveAt(index);}
                if(row.Deleted)continue;
                if(!units.TryGetValue(row.Id,out var unit))throw new InvalidDataException("Sighting references unknown unit.");
                var s=ReadData<MpSpottedState>(row);
                if(s.Marks==null || s.Marks.Length%9!=0 || s.Marks.Length>18000 || s.LastSpotted==null)throw new InvalidDataException("Invalid sighting history.");
                var entry=new SpottedEnemy.UnitsAttached(unit){lastspotted=s.LastSpotted};view.unitsattached.Add(entry);
                var prefab=NativeAccess.Get<PreLoadedModels>(view,"preloadedmodels").SpottedMark;
                for(int i=0;i<s.Marks.Length;i+=9) {
                    var mark=new SpottedEnemy.UnitsAttached.SpottedMarks(prefab,s.Marks[i+4],unit,s.Marks[i+3],new Vector3(s.Marks[i+5],s.Marks[i+6],s.Marks[i+7]));
                    mark.obj.transform.position=new Vector3(s.Marks[i],s.Marks[i+1],s.Marks[i+2]);mark.obj.SetActive(s.Marks[i+8]==1);
                    if(entry.spottedmarks.Count>0) {
                        var prior=entry.spottedmarks[entry.spottedmarks.Count-1];
                        if(Tools.GetXZDistance(prior.obj.transform.position,mark.obj.transform.position)>=5) {
                            mark.connectionline=mark.obj.AddComponent<ConnectionLine>();mark.connectionline.Initialize(prior.obj,mark.obj,unit.alliance,view.ConnectionMaterial);mark.connectionline.UpdateLine(1,true);
                        }
                    }
                    entry.spottedmarks.Add(mark);
                }
                NativeAccess.Call(view,"UpdateIcon",view.unitsattached.Count-1);NativeAccess.Call(view,"UpdateTooltips",view.unitsattached.Count-1);
            }
        }
        public static void ReplicaSightings(SpottedEnemy target)
        {
            if(!IsReplica){NativeAccess.Call(target,"GTIP_Original_ReplicaSightings");return;}
            var mp=instance;if(mp==null || !mp.sightingsInitialized || target.unitsattached==null || target.unitsattached.Count==0)return;
            mp.sightingCursor%=target.unitsattached.Count;
            var entry=target.unitsattached[mp.sightingCursor];
            if(entry.regref==null){NativeAccess.Call(target,"RemoveMarks",mp.sightingCursor);target.unitsattached.RemoveAt(mp.sightingCursor);return;}
            NativeAccess.Call(target,"UpdateIcon",mp.sightingCursor);NativeAccess.Call(target,"UpdateTooltips",mp.sightingCursor);
            for(int i=1;i<entry.spottedmarks.Count;i++)if(entry.spottedmarks[i].connectionline!=null) {
                float alpha=Mathf.Clamp01(1-(GameVars.GetCampaignFloatingDate()-entry.spottedmarks[i-1].timeshown)/Mathf.Max(.00001f,GamePrefs.timetoblendoutspottingmarks));
                entry.spottedmarks[i].connectionline.UpdateAlpha(false,alpha);
            }
            mp.sightingCursor++;
        }
        void ClearSightings(){sightingView=null;sightingsInitialized=false;sightingCursor=0;}
    }
}
