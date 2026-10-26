// Minimal native boundaries for exercising the production scene loader. These
// doubles do not claim to load Unity or initialize a battlefield.
namespace UnityEngine
{
    public static class Application
    {
        public static string dataPath="";
        public static Func<string,bool> Available=_=>true;
        public static bool CanStreamedLevelBeLoaded(string scene)=>Available(scene);
    }
    public sealed class GameObject
    {
        public static GameObject Controller=new();
        public static GameObject Find(string name)=>name=="GameController"?Controller:null!;
        public T GetComponent<T>()=>typeof(T)==typeof(BattlefieldSetup)?(T)(object)BattlefieldSetup.Instance:default!;
    }
}
public sealed class BattlefieldSetup
{
    public static BattlefieldSetup Instance=new();
    public static int Clears;
    public void ClearBattlefield(){Clears++;NativeTrace.Calls.Add("clear");}
}
public static class NativeTrace {public static readonly List<string> Calls=new();}
public static class SceneManagement
{
    public static int currentoperation;
    public static string currentcampaignscenario="";
    public static readonly int bo_showblackscreen=14,bo_rebuildscene=4,bo_resetloadingscreen=2,bo_loadcampaignscenario=6,bo_hideblackscreen=15,bo_loadcampaignbattlefromsave=13;
    public static List<BatchOperation> batchoperation=new();
    public sealed class BatchOperation
    {
        public readonly int operationid,startoffset;
        public readonly object[] Arguments;
        public BatchOperation(int id,int offset=0,object a0=null!,object a1=null!,object a2=null!,object a3=null!,object a4=null!,object a5=null!,object a6=null!,object a7=null!)
        {operationid=id;startoffset=offset;Arguments=new[]{a0,a1,a2,a3,a4,a5,a6,a7};}
    }
    public static void ExitCampaignBattleAndRebuildCampaign()=>throw new Exception("A replica must never calculate native battle results");
}
public static class MainMenuControls {public static float currentversion=1.142f;}
public static class Saving {public static string latestfolder="old-save",priorloadedfolder="old-save";}
public static class GameVars {public static int playeralliance;}
public static class BattleController {public static bool quitbattlescene;}
public static class CampaignController {public static bool quitcampaignscene;}
public static class LoadingScreen
{
    public static readonly int[] sound_loadingcampaign={101,102},sound_loadingbattle={201,202};
}
public static class ImportExportUnitData
{
    public static readonly List<(string Path,bool Realism)> Imports=new();
    public static string LoadScenarioFile(string path,bool realism=false){Imports.Add((path,realism));NativeTrace.Calls.Add("import");return "loaded";}
}
