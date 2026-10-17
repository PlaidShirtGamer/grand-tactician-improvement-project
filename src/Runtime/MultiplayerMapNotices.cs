using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        readonly HashSet<int> seenMapNotices=new HashSet<int>();
        readonly Dictionary<int,RollingText> mapNotices=new Dictionary<int,RollingText>();
        public static void ReplicaMapNotice(RollingText target,Vector3 position,string text,int type,GameObject reference,bool showsymbol,string tooltipheadline,string tooltiptext,float checkforsimilartypeswithinthistime)
        {
            NativeAccess.Call(target,"GTIP_Original_ReplicaMapNotice",position,text,type,reference,showsymbol,tooltipheadline,tooltiptext,checkforsimilartypeswithinthistime);
            var mp=instance;
            // Construction feedback must not depend on a transient rolling-text object
            // surviving until the next full campaign snapshot is captured and applied.
            if(mp==null || !mp.host || !mp.authenticated || mp.phase!="live" || type!=RollingText.type_buildingconstructed)return;
            var row=DataRow(15,target.uniqueid,new MpMapNotice{Type=type,X=position.x,Y=position.y,Z=position.z,Age=0,Text=text,Icon=showsymbol});
            mp.Send(new MpMessage{kind="map-notice",session=mp.session,blob=MpStateCodec.Encode(DataSchemas,new[]{row})});
            mp.LogDiagnostic("Native construction notice sent: "+target.uniqueid);
        }
        void CaptureMapNotices(List<MpStateRow> rows)
        {
            if(RollingText.rollingtext==null)return;
            foreach(var n in RollingText.rollingtext.Where(n=>n!=null)) {
                float age=Time.realtimeSinceStartup-n.timeshown;
                if(age<0 || age>=GamePrefs.timeshowrollingtext)continue;
                var unit=n.reference==null?null:n.reference.GetComponent<Regiment>();
                int reference=unit!=null && ids.TryGetValue(unit,out int id)?id:0;
                rows.Add(DataRow(15,n.uniqueid,new MpMapNotice{Type=n.type,Reference=reference,X=n.position.x,Y=n.position.y,Z=n.position.z,Age=age,Text=n.text,Icon=n.showsymbol}));
            }
        }
        void ApplyMapNotices(MpStateRow[] changes)
        {
            foreach(var row in changes.Where(r=>r.Kind==15)) {
                if(row.Deleted) {RemoveMapNotice(row.Id);continue;}
                var n=ReadData<MpMapNotice>(row);
                if(row.Id<0 || n.Type<0 || n.Type>100 || n.Reference<0 || !MpRules.Finite(n.X) || !MpRules.Finite(n.Y) || !MpRules.Finite(n.Z) || !MpRules.Finite(n.Age) || n.Age<0 || n.Age>3600 || n.Text==null)throw new InvalidDataException("Invalid map notice.");
                if(!seenMapNotices.Add(row.Id) || n.Age>=GamePrefs.timeshowrollingtext || GameVars.papermapvisible)continue;
                var reference=n.Reference!=0 && units.TryGetValue(n.Reference,out var unit)?unit.gameObject:gameObject;
                var notice=reference.AddComponent<RollingText>();
                notice.AddNew(new Vector3(n.X,n.Y,n.Z),n.Text,n.Type,reference,n.Icon);
                notice.timeshown=Time.realtimeSinceStartup-n.Age;
                RollingText.rollingtext.Add(notice);mapNotices[row.Id]=notice;
            }
        }
        void RemoveMapNotice(int id)
        {
            if(!mapNotices.TryGetValue(id,out var notice))return;
            if(notice!=null) {
                int index=RollingText.rollingtext?.IndexOf(notice)??-1;
                if(index>=0)RollingText.DeleteEntry(index);
                Destroy(notice);
            }
            mapNotices.Remove(id);
        }
        void ClearMapNotices()
        {foreach(var id in mapNotices.Keys.ToArray())RemoveMapNotice(id);seenMapNotices.Clear();}
    }
}
