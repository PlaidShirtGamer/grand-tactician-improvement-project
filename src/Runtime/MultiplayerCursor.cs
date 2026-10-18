using UnityEngine;

namespace GTIP
{
    public sealed partial class MultiplayerCampaign
    {
        readonly MpCursorGuard cursorGuard=new MpCursorGuard();
        public static void SetCursorLock(CursorLockMode mode,string source)
        {
            if(instance==null){Cursor.lockState=mode;return;}
            instance.cursorGuard.SetLock(mode,source);
        }
        void OnApplicationFocus(bool focused){cursorGuard.Focus(focused);}
    }
}
