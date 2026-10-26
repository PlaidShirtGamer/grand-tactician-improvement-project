using Mono.Cecil;
using Mono.Cecil.Cil;

static class MenuMusicChecks
{
    public static void Run(string gamePath,string runtimePath,string managedPath)
    {
        using var game=AssemblyDefinition.ReadAssembly(gamePath);
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        MethodDefinition Native(string owner,string name)=>game.MainModule.Types.Single(t=>t.Name==owner).Methods.Single(m=>m.Name==name);
        bool Calls(MethodDefinition method,string owner,string name)=>method.HasBody && method.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name==owner && m.Name==name);
        bool Text(MethodDefinition method,string value)=>method.HasBody && method.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr && Equals(i.Operand,value));
        bool Field(MethodDefinition method,string owner,string name)=>method.HasBody && method.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.DeclaringType.Name==owner && f.Name==name);
        int checks=0;
        void Check(bool valid,string reason){if(!valid)throw new Exception(reason);checks++;}

        var menu=Native("MainMenuControls","Update");
        Check(menu.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="LoadingScreen" && m.Name=="JustPlay" && i.Previous.OpCode==OpCodes.Ldc_I4_4),
            "The native main menu no longer selects loading sound object four.");
        var init=Native("LoadingScreen","InitSounds");
        var add=init.Body.Instructions.Where(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="SoundEngine" && m.Name=="AddPurpose").ToArray();
        Check(add.Length==5 && add.Last().Previous.Previous.Previous.Previous.OpCode==OpCodes.Ldc_I4_2 &&
            Calls(Native("LoadingScreen","Awake"),"LoadingScreen","InitSounds"),
            "The native loading screen no longer creates the dedicated menu-purpose object after its four loading objects.");
        var load=Native("SoundEngine","LoadAudioClips");
        Check(Field(load,"SoundEngine","TrackListFileName") && Calls(load,"Resources","Load") && Text(load,"Sounds/") && Field(load,"SoundEngine","audioclipismusic"),
            "Native audio clips no longer load through their configured resource names and music classification.");
        var change=Native("SoundEngine","ChangeClip");
        Check(change.Parameters.Count==3 && change.Parameters[0].ParameterType.FullName=="System.Int32" && change.Parameters[1].ParameterType.FullName=="System.Single" &&
            change.Parameters[2].ParameterType.FullName=="System.String" && Calls(change,"AudioSource","set_loop") && Calls(change,"AudioSource","Play") && Field(change,"SoundEngine","audioclipismusic"),
            "Native clip selection must still own loop, playback and music classification through its original three-argument API.");
        var volume=Native("SoundEngine","ChangeVolume");
        Check(Field(volume,"GameVars","musicvolume") && Field(volume,"GameVars","disablemusic") && Calls(volume,"AudioSource","set_volume"),
            "Native music volume and mute must remain on the original sound-engine path.");
        Check(Calls(Native("LoadingScreen","JustPlay"),"SoundEngine","Activate") &&
            Native("LoadingScreen","UpdateAlpha").Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="SoundEngine" && m.Name=="Activate"),
            "Loading transitions must retain the native activation and fade-out lifecycle.");

        var install=new DirectoryInfo(Path.GetFullPath(managedPath)).Parent!.Parent!.FullName;
        Dictionary<int,(string Name,bool Music)> Tracks(string file)
        {
            var lines=File.ReadAllLines(Path.Combine(install,"Config",file));int count=int.Parse(lines[0]);
            Check(lines.Length==1+count*3,"Native track-list shape changed: "+file);
            return Enumerable.Range(0,count).ToDictionary(i=>int.Parse(lines[1+i*3]),i=>(lines[2+i*3],bool.Parse(lines[3+i*3])));
        }
        var loading=Tracks("ltracklist.txt");var campaign=Tracks("ctracklist.txt");
        var purpose=File.ReadAllLines(Path.Combine(install,"Config","lsounds.txt"));
        Check(purpose.Length==1+int.Parse(purpose[0])*44 && purpose[1+2*44]=="2" && purpose[2+2*44]=="Main menu" &&
            float.Parse(purpose[1+2*44+11],System.Globalization.CultureInfo.InvariantCulture)==1 && purpose[1+2*44+13]=="31" && purpose[1+2*44+14]=="31",
            "The dedicated loading-engine main-menu purpose no longer loops only track 31.");
        Check(loading[31]==("ST_BlackestCrow",true) && campaign.Any(p=>p.Value==loading[31]),
            "The menu resource identity/shared campaign use changed; replacement scope needs re-audit.");

        var music=runtime.MainModule.Types.Single(t=>t.Name=="MainMenuMusic");
        MethodDefinition Bridge(string name)=>music.Methods.Single(m=>m.Name==name);
        var loadBridge=Bridge("LoadMenuMusic");var clipBridge=Bridge("ChangeMenuMusic");
        Check(Text(loadBridge,"GTIP_Original_LoadMenuMusic") && Calls(loadBridge,"NativeAccess","Call") && Calls(loadBridge,"MainMenuMusic","IsMenuEngine"),
            "Optional menu loading must retain the original native audio load and scope its override to the loading-screen engine.");
        var scope=Bridge("IsMenuEngine");
        Check(Text(scope,"lsounds.txt") && Text(scope,"ltracklist.txt") && Field(scope,"SoundEngine","SoundPurposeFileName") && Field(scope,"SoundEngine","TrackListFileName") &&
            scope.Body.Instructions.Any(i=>i.Operand is GenericInstanceMethod m && m.Name=="GetComponent" && m.GenericArguments.Single().Name=="LoadingScreen"),
            "Menu audio scope must match both actual loading configs and the owning LoadingScreen component.");
        var restore=clipBridge.Body.ExceptionHandlers.SingleOrDefault(h=>h.HandlerType==ExceptionHandlerType.Finally);
        Check(Text(clipBridge,"GTIP_Original_ChangeMenuMusic") && Calls(clipBridge,"NativeAccess","Call") && Calls(clipBridge,"MainMenuMusic","IsMenuEngine") && restore!=null &&
            clipBridge.Body.Instructions.Any(i=>i.OpCode==OpCodes.Stelem_Ref && i.Offset>=restore.HandlerStart.Offset && (restore.HandlerEnd==null || i.Offset<restore.HandlerEnd.Offset)),
            "Menu clip replacement must call the native selector and restore its shared track slot in finally, including native exceptions.");
        Check(clipBridge.Parameters.Count==4 && clipBridge.Parameters[0].ParameterType.Name=="SoundEngine" &&
            clipBridge.Body.Instructions.All(i=>!(i.Operand is MethodReference m && m.Name=="AddComponent" && m is GenericInstanceMethod g && g.GenericArguments.Any(t=>t.Name=="AudioSource"))),
            "Menu replacement must retain the existing sound-object API rather than introducing another AudioSource.");
        Check(Calls(clipBridge,"MainMenuMusic","PurposeObjects") && Text(clipBridge,"purposeid") &&
            clipBridge.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldc_I4_2) && Text(Bridge("PurposeObjects"),"soundobject"),
            "The override must select the existing native menu-purpose object rather than its numeric object position or a campaign engine.");
        var loader=runtime.MainModule.Types.Single(t=>t.Name=="MainMenuMusicLoader");
        MethodDefinition Loader(string name)=>loader.Methods.Single(m=>m.Name==name);
        var begin=Loader("Begin");
        Check(new[]{"GTIP","Audio","main-menu.mp3"}.All(value=>Text(begin,value)) && Calls(begin,"File","Exists") && Calls(begin,"MonoBehaviour","StartCoroutine"),
            "The optional menu replacement must load only the documented local MP3 and preserve missing-file fallback.");
        var decode=loader.NestedTypes.Single(t=>t.Name.StartsWith("<Load>")).Methods.Single(m=>m.Name=="MoveNext");
        Check(Calls(decode,"UnityWebRequestMultimedia","GetAudioClip") && Calls(decode,"Path","GetFullPath") && Calls(decode,"Uri","get_AbsoluteUri") &&
            Calls(decode,"UnityWebRequest","SendWebRequest") && Calls(decode,"DownloadHandlerAudioClip","GetContent"),
            "The override must asynchronously decode a local absolute file URI through Unity's supported audio loader.");
        Check(Field(decode,"MainMenuControls","isactive") && Field(decode,"LoadingScreen","currentsoundplayed") &&
            Text(decode,"purposeid") && Text(decode,"active") && Calls(decode,"MainMenuMusic","ChangeMenuMusic"),
            "Decode completion may refresh only the currently active native menu purpose, not a loading/campaign source.");
        Check(Calls(Loader("OnDestroy"),"MonoBehaviour","StopAllCoroutines") && Calls(Loader("OnDestroy"),"UnityWebRequest","Abort") &&
            Calls(Loader("OnDestroy"),"MainMenuMusicLoader","DisposeRequest") && Calls(Loader("OnDestroy"),"Object","Destroy") &&
            Calls(Loader("DisposeRequest"),"UnityWebRequest","Dispose") && Calls(decode,"MainMenuMusicLoader","DisposeRequest"),
            "Menu music ownership must dispose completed requests and release pending work plus its decoded clip on destruction.");
        Console.WriteLine($"Native menu music: {checks} checks; actual menu/loading/shared-resource contracts, native volume/mute/loop and scoped temporary slot restoration verified. Audio output is not exercised.");
    }
}
