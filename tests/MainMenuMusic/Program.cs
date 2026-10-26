using System.Collections;
using System.Reflection;
using GTIP;
using UnityEngine;
using UnityEngine.Networking;

int checks=0;
void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
string root=Path.Combine(Path.GetTempPath(),"gtip-menu-music-"+Guid.NewGuid().ToString("N"),"Local music # é");
Directory.CreateDirectory(root);Application.dataPath=Path.Combine(root,"Game_Data");
string mp3=Path.Combine(root,"GTIP","Audio","main-menu.mp3");
SoundEngine Engine(bool loading=true)
{
    var go=new GameObject();var engine=go.AddComponent<SoundEngine>();if(loading)go.AddComponent<LoadingScreen>();return engine;
}
MainMenuMusicLoader Owner(SoundEngine engine)=>engine.GetComponent<MainMenuMusicLoader>();
void Ready(SoundEngine engine,AudioClip clip=null)
{UnityWebRequest.Last.Decoded=clip??new AudioClip("Local MP3");Owner(engine).Complete();}
try {
    Debug.Warnings.Clear();var missing=Engine();MainMenuMusic.LoadMenuMusic(missing);
    Check(missing.Loads==1 && Owner(missing)!=null && UnityWebRequest.Created==0,"Native clips load before the optional missing-file fallback");
    MainMenuMusic.LoadMenuMusic(missing);Check(missing.Loads==2 && UnityWebRequest.Created==0 && Debug.Warnings.Count==0,"A missing optional file silently keeps stock music and never retries every menu selection");
    Directory.CreateDirectory(Path.GetDirectoryName(mp3)!);File.WriteAllBytes(mp3,new byte[]{1,2,3});
    var campaign=Engine(false);MainMenuMusic.LoadMenuMusic(campaign);
    Check(campaign.Loads==1 && Owner(campaign)==null,"A campaign engine without LoadingScreen keeps native loading and has no custom loader");
    foreach(bool purposeFile in new[]{true,false}) {
        var wrong=Engine();if(purposeFile)wrong.SoundPurposeFileName="Config/sounds.txt";else wrong.TrackListFileName="Config/tracklist.txt";
        MainMenuMusic.LoadMenuMusic(wrong);
        Check(Owner(wrong)==null && wrong.Loads==1,"The "+(purposeFile?"purpose":"track-list")+" config basename must match the native loading/menu engine");
    }

    MainMenuControls.isactive=true;LoadingScreen.currentsoundplayed=4;
    var menu=Engine();MainMenuMusic.LoadMenuMusic(menu);var stock=menu.audioclips[31];var pending=UnityWebRequest.Last;
    Check(menu.Loads==1 && pending.Sent && pending.Type==AudioType.MPEG && new Uri(pending.Url).IsFile && new Uri(pending.Url).LocalPath==mp3,
        "The async MPEG decoder receives only the absolute local file URI, preserving spaces, Unicode and #");
    MainMenuMusic.ChangeMenuMusic(menu,0,23f,"before ready");
    Check(menu.LastClip==stock && menu.LastDistance==23f && menu.LastDebug=="before ready","Before decoding, the exact native selection arguments and stock track remain intact");
    MainMenuMusic.LoadMenuMusic(menu);Check(UnityWebRequest.Last==pending && menu.Loads==2,"Repeated native clip loading does not create another request for the same engine");
    stock=menu.audioclips[31];Ready(menu);var custom=menu.LastClip;
    Check(custom!=stock && custom.Name=="Local MP3" && menu.Changes==2 && menu.LastDebug=="GTIP local main-menu music ready",
        "A ready clip refreshes the already active menu-purpose source through native ChangeClip");
    Check(menu.audioclips[31]==stock && pending.Disposed==1 && !pending.Aborted,"Ready playback restores the exact stock table entry and disposes the completed request");
    Check(Debug.Messages.Any(m=>m.Contains(mp3) && m.Contains("duration=") && m.Contains("channels=2") && m.Contains("frequency=44100Hz")),
        "A decoded override reports its local path and audio metadata for native verification");
    Check(menu.Loop && menu.Volume==0.001f && menu.Music && menu.gameObject.Count==3,"Native playback retains loop, fade and music classification without an extra AudioSource or persistent table mutation");
    MainMenuMusic.ChangeMenuMusic(menu,1,5f,"loading playlist");
    Check(menu.LastClip==stock && menu.audioclips[31]==stock,"A loading playlist that also chooses resource 31 keeps the stock clip");
    MainMenuMusic.ChangeMenuMusic(menu,2,6f,"other purpose");
    Check(menu.LastClip==stock,"A non-menu purpose on the same engine remains native");
    MainMenuMusic.ChangeMenuMusic(menu,0,7f,"next menu selection");
    Check(menu.LastClip==custom && menu.LastDistance==7f && menu.LastDebug=="next menu selection" && menu.audioclips[31]==stock,
        "A later menu selection uses the decoded clip only during its native purpose-2 call");
    menu.SoundPurposeFileName="Config/other.txt";MainMenuMusic.ChangeMenuMusic(menu,0,0f,"");
    Check(menu.LastClip==stock,"A changed engine configuration cannot reuse an earlier menu-only override");menu.SoundPurposeFileName="Config/lsounds.txt";
    menu.ThrowChange=true;bool threw=false;try{MainMenuMusic.ChangeMenuMusic(menu,0,0f,"");}catch(InvalidOperationException){threw=true;}
    Check(threw && menu.audioclips[31]==stock,"Even a throwing native ChangeClip restores the exact prior table reference in finally");menu.ThrowChange=false;

    MainMenuControls.isactive=false;var later=Engine();MainMenuMusic.LoadMenuMusic(later);Ready(later);
    Check(later.Changes==0,"Decode completion outside the main menu does not interrupt a loading or campaign source");
    MainMenuControls.isactive=true;MainMenuMusic.ChangeMenuMusic(later,0,-1f,"returned to menu");
    Check(later.LastClip.Name=="Local MP3","Returning to the menu can use an already decoded per-engine clip");
    LoadingScreen.currentsoundplayed=2;var loading=Engine();MainMenuMusic.LoadMenuMusic(loading);Ready(loading);
    Check(loading.Changes==0,"The active main-menu flag alone does not refresh a different loading-screen sound");LoadingScreen.currentsoundplayed=4;
    var inactive=Engine();inactive.soundobject[0].active=false;MainMenuMusic.LoadMenuMusic(inactive);Ready(inactive);
    Check(inactive.Changes==0,"Readiness refresh touches only already active purpose-2 objects");

    Debug.Warnings.Clear();var bad=Engine();MainMenuMusic.LoadMenuMusic(bad);var failed=UnityWebRequest.Last;failed.result=UnityWebRequest.Result.DataProcessingError;failed.error="Local decoder failure";Owner(bad).Complete();
    MainMenuMusic.ChangeMenuMusic(bad,0,0f,"");bool keptStock=bad.LastClip==bad.audioclips[31];MainMenuMusic.LoadMenuMusic(bad);
    Check(Debug.Warnings.Count==1 && keptStock && failed.Disposed==1 && UnityWebRequest.Last==failed,
        "A failed local request warns once, disposes its request and keeps native playback without repeated retries");
    Debug.Warnings.Clear();var empty=Engine();MainMenuMusic.LoadMenuMusic(empty);Owner(empty).Complete();
    Check(Debug.Warnings.Count==1 && empty.Changes==0,"A request without a decoded AudioClip falls back to native music");
    foreach(string invalid in new[]{"samples","channels","frequency"}) {
        Debug.Warnings.Clear();var invalidEngine=Engine();MainMenuMusic.LoadMenuMusic(invalidEngine);var decoded=new AudioClip("Bad MP3");
        typeof(AudioClip).GetField(invalid)!.SetValue(decoded,0);Ready(invalidEngine,decoded);
        Check(decoded.Destroyed && invalidEngine.Changes==0 && Debug.Warnings.Count==1,"A decoded MP3 with invalid "+invalid+" is released and native music remains active");
    }
    Debug.Warnings.Clear();var setup=Engine();string dataPath=Application.dataPath;Application.dataPath=null;
    MainMenuMusic.LoadMenuMusic(setup);MainMenuMusic.LoadMenuMusic(setup);Application.dataPath=dataPath;
    Check(setup.Loads==2 && Debug.Warnings.Count==1,"Optional loader setup failures cannot interrupt native clip loading and warn only once");
    Debug.Warnings.Clear();var noComponent=Engine();noComponent.gameObject.ThrowLoader=true;
    MainMenuMusic.LoadMenuMusic(noComponent);MainMenuMusic.LoadMenuMusic(noComponent);
    Check(noComponent.Loads==2 && Debug.Warnings.Count==1,"A failed optional component attachment leaves native startup intact with one setup warning");
    var interrupted=Engine();MainMenuMusic.LoadMenuMusic(interrupted);var aborted=UnityWebRequest.Last;Owner(interrupted).DestroyForTest();
    Check(aborted.Aborted && aborted.Disposed==1 && Owner(interrupted).Stopped,"Destroying an owner aborts and disposes its in-flight local request and coroutine");
    Owner(menu).DestroyForTest();Check(custom.Destroyed && !later.LastClip.Destroyed && menu.audioclips[31]==stock,"Destroying one owner releases only its custom clip and leaves stock clips and other engine ownership intact");
    Owner(menu).DestroyForTest();Check(custom.DestroyCount==1,"Repeated teardown cannot destroy a released custom clip twice");
    Console.WriteLine($"Main-menu music: {checks} checks using production loader and selection bridge; Unity decoding and native playback are doubled.");
} finally {
    var parent=Path.GetDirectoryName(root)!;var temp=Path.GetFullPath(Path.GetTempPath());
    if(Path.GetFullPath(parent).StartsWith(temp,StringComparison.OrdinalIgnoreCase) && Path.GetFileName(parent).StartsWith("gtip-menu-music-",StringComparison.Ordinal))Directory.Delete(parent,true);
}

public sealed class SoundEngine : MonoBehaviour
{
    public sealed class SoundObject {public int purposeid;public bool active;}
    public string SoundPurposeFileName="Config/lsounds.txt",TrackListFileName="Config/ltracklist.txt";
    public readonly List<SoundObject> soundobject=new(){new(){purposeid=2,active=true},new(){purposeid=0,active=true},new(){purposeid=1,active=true}};
    public AudioClip[] audioclips;public int Loads,Changes;public AudioClip LastClip;public float LastDistance,Volume;public string LastDebug;public bool Loop,Music,ThrowChange;
    public void GTIP_Original_LoadMenuMusic(){Loads++;audioclips=new AudioClip[32];audioclips[31]=new AudioClip("Stock menu/loading");}
    public void GTIP_Original_ChangeMenuMusic(int id,float distance,string debuginfo)
    {Changes++;LastClip=audioclips[31];LastDistance=distance;LastDebug=debuginfo;Loop=soundobject[id].purposeid==2;Volume=0.001f;Music=true;if(ThrowChange)throw new InvalidOperationException("Native test failure");}
}
public sealed class LoadingScreen : MonoBehaviour {public static int currentsoundplayed;}
public static class MainMenuControls {public static bool isactive;}
namespace UnityEngine
{
    public class Object
    {
        public static void Destroy(Object value){if(value is AudioClip clip){clip.Destroyed=true;clip.DestroyCount++;}}
    }
    public sealed class AudioClip : Object {public string Name;public bool Destroyed;public int DestroyCount;public int samples=44100,channels=2,frequency=44100;public float length=>samples/(float)frequency;public AudioClip(string name){Name=name;}}
    public enum AudioType {MPEG}
    public static class Application {public static string dataPath;}
    public static class Debug {public static readonly List<string> Warnings=new(),Messages=new();public static void LogWarning(object value)=>Warnings.Add(value.ToString()!);public static void Log(object value)=>Messages.Add(value.ToString()!);}
    public sealed class GameObject
    {
        readonly List<MonoBehaviour> components=new();public int Count=>components.Count;public bool ThrowLoader;
        public T GetComponent<T>() where T:class=>components.OfType<T>().FirstOrDefault();
        public T AddComponent<T>() where T:MonoBehaviour,new(){if(ThrowLoader && typeof(T)==typeof(MainMenuMusicLoader))throw new InvalidOperationException("Cannot add optional loader");var item=new T{gameObject=this};components.Add(item);return item;}
    }
    public sealed class Coroutine {}
    public class MonoBehaviour : Object
    {
        public GameObject gameObject;IEnumerator pending;public bool Stopped;
        public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
        public Coroutine StartCoroutine(IEnumerator work){pending=work;work.MoveNext();return new Coroutine();}
        public void StopAllCoroutines(){Stopped=true;pending=null;}
        public void Complete(){pending?.MoveNext();pending=null;}
        public void DestroyForTest()=>GetType().GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(this,null);
    }
}
namespace UnityEngine.Networking
{
    public sealed class UnityWebRequestAsyncOperation {}
    public sealed class UnityWebRequest : IDisposable
    {
        public static UnityWebRequest Last;public static int Created;
        public enum Result {Success,DataProcessingError}
        public string Url,error;public AudioType Type;public bool Sent,Aborted;public Result result=Result.Success;public int Disposed;public AudioClip Decoded;
        public UnityWebRequest(string url,AudioType type){Url=url;Type=type;Last=this;Created++;}
        public UnityWebRequestAsyncOperation SendWebRequest(){Sent=true;return new();}
        public void Abort(){Aborted=true;}public void Dispose(){Disposed++;}
    }
    public static class UnityWebRequestMultimedia {public static UnityWebRequest GetAudioClip(string uri,AudioType type)=>new(uri,type);}
    public static class DownloadHandlerAudioClip {public static AudioClip GetContent(UnityWebRequest request)=>request.Decoded;}
}
