using System;
using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        readonly MpPanelPause panelPause=new MpPanelPause();
        bool blockPanelResume;
        static void NativePanelPause(object panel,bool visible,Action native)
        {
            var mp=instance;
            if(mp==null || mp.transport==null || !mp.host || panel==null){native();return;}
            bool prior=mp.blockPanelResume;
            mp.blockPanelResume=mp.panelPause.Update(panel,visible,GameVars.gamepaused);
            try {native();}finally {mp.blockPanelResume=prior;}
        }
        public static void ReplicaPausePanel(PanelHandler target,PanelHandler.Panel usedpanel)
        {NativePanelPause(usedpanel,usedpanel!=null && usedpanel.state>0,()=>NativeAccess.Call(target,"GTIP_Original_ReplicaPausePanel",usedpanel));}
        public static void ReplicaPauseObject(PanelHandler target,GameObject usedpanel)
        {NativePanelPause(usedpanel,usedpanel!=null && usedpanel.activeInHierarchy,()=>NativeAccess.Call(target,"GTIP_Original_ReplicaPauseObject",usedpanel));}
    }
}
