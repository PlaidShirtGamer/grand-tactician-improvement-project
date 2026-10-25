using System.Collections.Generic;
using System.IO;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        readonly Dictionary<int,int> mapStatusIcons=new Dictionary<int,int>();
        static int NativeMapStatusIndex(Regiment unit)
        {
            var mp=instance;
            if(IsReplica) {
                if(mp!=null && mp.ids.TryGetValue(unit,out int id) && mp.mapStatusIcons.TryGetValue(id,out int status))return status;
                throw new InvalidDataException("Authoritative map status icon is unavailable for readback.");
            }
            return (int)NativeAccess.Call(unit,"GTIP_NativeMapStatusIndex");
        }
        void ClearMapStatus() => mapStatusIcons.Clear();

        void ApplyMapStatus(MpStateRow[] changes)
        {
            foreach(var row in changes)if(row.Kind==4) {
                if(row.Deleted){mapStatusIcons.Remove(row.Id);continue;}
                int status=ReadData<MpFormationState>(row).StatusTexture;
                if(status<0 || status>28 || !units.TryGetValue(row.Id,out var unit))throw new InvalidDataException("Invalid map status icon identity.");
                mapStatusIcons[row.Id]=status;
                ReplicaMapStatusIcon(unit);
            }
        }

        public static void ReplicaMapStatusIcon(Regiment unit)
        {
            var mp=instance;
            if(!IsReplica || mp==null || !mp.ids.TryGetValue(unit,out int id) || !mp.mapStatusIcons.TryGetValue(id,out int status)) {
                NativeAccess.Call(unit,"GTIP_Original_ReplicaMapStatusIcon");return;
            }
            int index=NativeAccess.Get<int>(unit,"uniticonid");
            if(index<0)return;
            var handler=NativeAccess.Get<PanelHandler>(unit,"panelhandler");
            if(handler==null || index>=handler.multiplepanel.Count)return;
            var panel=handler.multiplepanel[index];
            if(panel==null || panel.go==null || panel.secondaryreference!=unit.gameObject)return;
            if(status>=handler.UnitStatusTextures.Length || status>=handler.UnitStatusTexturesHighlighted.Length)
                throw new InvalidDataException("Native map status texture table changed.");
            handler.UpdateButtonPic(panel,183,handler.UnitStatusTextures[status],handler.UnitStatusTexturesHighlighted[status],handler.UnitStatusTextures[status]);
        }
    }
}
