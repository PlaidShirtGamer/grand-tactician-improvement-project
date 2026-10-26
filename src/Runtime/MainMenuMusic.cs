using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace GTIP
{
    public static class MainMenuMusic
    {
        internal const int MenuTrack=31,MenuPurpose=2;
        static bool setupWarning;
        internal static bool IsMenuEngine(SoundEngine target)=>target.GetComponent<LoadingScreen>()!=null &&
            string.Equals(Path.GetFileName(target.SoundPurposeFileName),"lsounds.txt",StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Path.GetFileName(target.TrackListFileName),"ltracklist.txt",StringComparison.OrdinalIgnoreCase);
        internal static IList PurposeObjects(SoundEngine target)=>NativeAccess.Get<IList>(target,"soundobject");

        public static void LoadMenuMusic(SoundEngine target)
        {
            NativeAccess.Call(target,"GTIP_Original_LoadMenuMusic");
            MainMenuMusicLoader owner=null;
            try {
                if(!IsMenuEngine(target))return;
                owner=target.GetComponent<MainMenuMusicLoader>() ?? target.gameObject.AddComponent<MainMenuMusicLoader>();
                owner.Begin(target);
            } catch(Exception error) {
                if(owner!=null)owner.WarnOnce(error.Message);
                else if(!setupWarning){setupWarning=true;Debug.LogWarning("[GTIP] Main-menu music override setup unavailable; using native music. "+error.Message);}
            }
        }
        public static void ChangeMenuMusic(SoundEngine target,int id,float distance,string debuginfo)
        {
            var owner=target.GetComponent<MainMenuMusicLoader>();
            if(!IsMenuEngine(target) || owner==null || owner.Clip==null || target.audioclips==null || target.audioclips.Length<=MenuTrack) {
                NativeAccess.Call(target,"GTIP_Original_ChangeMenuMusic",id,distance,debuginfo);return;
            }
            var objects=PurposeObjects(target);
            if(id<0 || id>=objects.Count || NativeAccess.Get<int>(objects[id],"purposeid")!=MenuPurpose) {
                NativeAccess.Call(target,"GTIP_Original_ChangeMenuMusic",id,distance,debuginfo);return;
            }
            // The loading playlists can also select track 31. Substitute only
            // while native menu-purpose selection runs, then restore its table.
            var clips=target.audioclips;var stock=clips[MenuTrack];
            clips[MenuTrack]=owner.Clip;
            try {NativeAccess.Call(target,"GTIP_Original_ChangeMenuMusic",id,distance,debuginfo);}
            finally {clips[MenuTrack]=stock;}
        }
    }

    public sealed class MainMenuMusicLoader : MonoBehaviour
    {
        SoundEngine engine;
        UnityWebRequest request;
        bool started,destroyed,warned;
        internal AudioClip Clip {get;private set;}
        internal void Begin(SoundEngine target)
        {
            if(started)return;started=true;engine=target;
            string path=Path.Combine(Path.GetDirectoryName(Application.dataPath),"GTIP","Audio","main-menu.mp3");
            if(!File.Exists(path))return;
            StartCoroutine(Load(path));
        }
        IEnumerator Load(string path)
        {
            UnityWebRequestAsyncOperation pending;
            try {
                // Absolute file URI handles spaces and Unicode without permitting
                // a remote URL or changing the game's current working directory.
                request=UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.GetFullPath(path)).AbsoluteUri,AudioType.MPEG);
                pending=request.SendWebRequest();
            } catch(Exception error) {WarnOnce(error.Message);DisposeRequest();yield break;}
            yield return pending;
            if(destroyed)yield break;
            try {
                if(request.result!=UnityWebRequest.Result.Success)WarnOnce(request.error);
                else {
                    Clip=DownloadHandlerAudioClip.GetContent(request);
                    if(Clip==null || Clip.samples<=0 || Clip.channels<=0 || Clip.frequency<=0) {
                        if(Clip!=null){Destroy(Clip);Clip=null;}
                        WarnOnce("the local MP3 did not decode to a valid audio clip");
                    } else Debug.Log("[GTIP] Main-menu music override loaded: "+path+"; duration="+Clip.length.ToString("0.00",CultureInfo.InvariantCulture)+"s; channels="+Clip.channels+"; frequency="+Clip.frequency+"Hz.");
                }
            } catch(Exception error) {WarnOnce(error.Message);}
            finally {DisposeRequest();}
            if(Clip==null || engine==null || !MainMenuMusic.IsMenuEngine(engine) || !MainMenuControls.isactive || LoadingScreen.currentsoundplayed!=4)yield break;
            // Decoding may finish after native menu playback already began.
            // Re-select through the same native source, fades and volume path.
            var objects=MainMenuMusic.PurposeObjects(engine);
            for(int id=0;id<objects.Count;id++)
                if(NativeAccess.Get<int>(objects[id],"purposeid")==MainMenuMusic.MenuPurpose && NativeAccess.Get<bool>(objects[id],"active"))
                    MainMenuMusic.ChangeMenuMusic(engine,id,-1f,"GTIP local main-menu music ready");
        }
        internal void WarnOnce(string reason)
        {
            if(warned)return;warned=true;
            Debug.LogWarning("[GTIP] Main-menu music override unavailable; using native music. "+reason);
        }
        void DisposeRequest()
        {
            var old=request;request=null;old?.Dispose();
        }
        void OnDestroy()
        {
            destroyed=true;StopAllCoroutines();
            if(request!=null)request.Abort();
            DisposeRequest();
            if(Clip!=null){Destroy(Clip);Clip=null;}
        }
    }
}
