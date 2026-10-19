using GTIP;
using UnityEngine;

int checks=0;
void Check(bool valid,string label){if(!valid)throw new Exception(label);checks++;}
var camera=new Camera_Battlefield{IsCampaignCam=true,mrot=8,scrolltarget_rotation=125,PanX=3,PanZ=-4,Zoom=2};
camera.transform.rotation=Quaternion.Euler(0,50,0);GamePrefs.preventcamerarotation=true;
CampaignCameraRuntime.LockedCameraScrolls(camera);
Check(NativeAccess.Calls==1 && NativeAccess.Method=="GTIP_Original_LockedCameraScrolls","The preserved native camera scrolling method still executes");
Check(camera.mrot==0 && camera.scrolltarget_rotation==-1,"Locked campaign camera discards keyboard rotation momentum and queued rotation targets");
Check(camera.transform.eulerAngles.y==0,"Enabling the campaign lock restores north from an existing rotation");
Check(camera.PanX==3 && camera.PanZ==-4 && camera.Zoom==2 && camera.NativePan==7,"Locked rotation preserves native pan and zoom inputs");
Check(camera.NativePitch==42,"Native height-dependent camera pitch still updates while locked");
camera.mrot=-5;CampaignCameraRuntime.LockedCameraScrolls(camera);
Check(camera.mrot==0 && camera.transform.eulerAngles.y==0,"Repeated keyboard rotation remains blocked on host, guest and offline cameras");
GamePrefs.preventcamerarotation=false;camera.mrot=4;camera.scrolltarget_rotation=80;
CampaignCameraRuntime.LockedCameraScrolls(camera);
Check(camera.NativeRotationInput==4 && camera.scrolltarget_rotation==80,"Unlocked campaign camera retains native rotation inputs and targets");
Check(camera.transform.eulerAngles.y==4,"Unlocked campaign rotation remains native");
camera.IsCampaignCam=false;GamePrefs.preventcamerarotation=true;camera.mrot=6;camera.scrolltarget_rotation=65;
CampaignCameraRuntime.LockedCameraScrolls(camera);
Check(camera.NativeRotationInput==6 && camera.scrolltarget_rotation==65,"Campaign lock does not override tactical camera controls");
Check(camera.transform.eulerAngles.y==10,"Tactical rotation remains native");
camera.IsCampaignCam=true;CampaignCameraRuntime.LockedCameraScrolls(camera);
Check(camera.transform.eulerAngles.y==0 && camera.scrolltarget_rotation==-1,"Returning to the locked campaign clears a stale tactical yaw/target");
GamePrefs.preventcamerarotation=false;camera.mrot=0;CampaignCameraRuntime.LockedCameraScrolls(camera);
Check(camera.NativeRotationInput==0 && camera.scrolltarget_rotation==-1,"Unlocking does not resurrect a discarded rotation target");
NativeAccess.Fail=true;bool failed=false;try{CampaignCameraRuntime.LockedCameraScrolls(camera);}catch(InvalidOperationException){failed=true;}
Check(failed,"Native camera failures remain visible");
Console.WriteLine($"Campaign camera rotation: {checks} checks passed using the production wrapper and native-operation doubles; no live Unity input exercised.");

public static class GamePrefs {public static bool preventcamerarotation;}
public sealed class Camera_Battlefield
{
    public bool IsCampaignCam;
    public float mrot,scrolltarget_rotation,PanX,PanZ,Zoom,NativeRotationInput,NativePan,NativePitch;
    public Transform transform=new Transform();
    public void NativeScrolls(){NativeRotationInput=mrot;NativePan=PanX-PanZ;NativePitch=40+Zoom;transform.rotation=Quaternion.Euler(0,transform.eulerAngles.y+mrot,0);}
}
namespace UnityEngine
{
    public struct Vector3 {public float x,y,z;}
    public struct Quaternion
    {
        public Vector3 eulerAngles;
        public static Quaternion Euler(float x,float y,float z)=>new Quaternion{eulerAngles=new Vector3{x=x,y=y,z=z}};
    }
    public sealed class Transform
    {
        public Quaternion rotation;
        public Vector3 eulerAngles=>rotation.eulerAngles;
    }
    public static class Mathf
    {
        public static float Abs(float value)=>Math.Abs(value);
        public static float DeltaAngle(float a,float b)=>(b-a+540)%360-180;
    }
}
namespace GTIP
{
    public static class NativeAccess
    {
        public static int Calls;
        public static string Method;
        public static bool Fail;
        public static void Set(Camera_Battlefield target,string name,object value)=>typeof(Camera_Battlefield).GetField(name)!.SetValue(target,value);
        public static object Call(Camera_Battlefield target,string name,params object[] args)
        {if(Fail)throw new InvalidOperationException("Native camera failure");Calls++;Method=name;target.NativeScrolls();return null;}
    }
}
