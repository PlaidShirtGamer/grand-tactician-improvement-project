using UnityEngine;

namespace GTIP
{
    public static class CampaignCameraRuntime
    {
        public static void LockedCameraScrolls(Camera_Battlefield target)
        {
            if(target.IsCampaignCam && GamePrefs.preventcamerarotation) {
                // Native mouse-edge rotation honors the option, but keyboard rotation
                // bypasses that branch and leaves angular momentum in mrot. Enforce
                // the option where every native rotation source is consumed.
                NativeAccess.Set(target,"mrot",0f);
                NativeAccess.Set(target,"scrolltarget_rotation",-1f);
                // The option promises a north-facing campaign map. Native scrolls
                // still computes child-camera pitch from zoom/height below.
                if(Mathf.Abs(Mathf.DeltaAngle(target.transform.eulerAngles.y,0f))>.001f)
                    target.transform.rotation=Quaternion.Euler(0f,0f,0f);
            }
            NativeAccess.Call(target,"GTIP_Original_LockedCameraScrolls");
        }
    }
}
