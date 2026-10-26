using Mono.Cecil;
using Mono.Cecil.Cil;

// Native prerequisites for the first paused tactical transition milestone. These
// checks establish the supported call paths, not synchronized battle gameplay.
static class TacticalContracts
{
    public static void Run(string gamePath,string runtimePath)
    {
        using var game=AssemblyDefinition.ReadAssembly(gamePath);
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        MethodDefinition Native(string owner,string name)=>game.MainModule.Types.Single(t=>t.Name==owner).Methods.Single(m=>m.Name==name);
        bool Calls(MethodDefinition method,string owner,string name)=>method.HasBody && method.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.DeclaringType.Name==owner && m.Name==name);
        bool Field(MethodDefinition method,string owner,string name)=>method.HasBody && method.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.DeclaringType.Name==owner && f.Name==name);
        bool Text(MethodDefinition method,string value)=>method.HasBody && method.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr && Equals(i.Operand,value));
        bool Signature(MethodDefinition method,string returns,params string[] parameters)=>method.IsStatic && method.ReturnType.Name==returns &&
            method.Parameters.Select(p=>p.ParameterType.Name).SequenceEqual(parameters);
        int? Integer(Instruction i)=>i.OpCode.Code switch {
            Code.Ldc_I4_M1=>-1,Code.Ldc_I4_0=>0,Code.Ldc_I4_1=>1,Code.Ldc_I4_2=>2,Code.Ldc_I4_3=>3,Code.Ldc_I4_4=>4,
            Code.Ldc_I4_5=>5,Code.Ldc_I4_6=>6,Code.Ldc_I4_7=>7,Code.Ldc_I4_8=>8,
            Code.Ldc_I4=>(int)i.Operand,Code.Ldc_I4_S=>(sbyte)i.Operand,_=>null};
        bool Number(MethodDefinition method,int value)=>method.Body.Instructions.Any(i=>Integer(i)==value);
        int Index(MethodDefinition method,Func<Instruction,bool> predicate)=>method.Body.Instructions.ToList().FindIndex(i=>predicate(i));
        bool FrameBodyContains(MethodDefinition method,int frame,Func<Instruction,bool> predicate)
        {
            // This supported build emits each staged initialization step as a
            // frame equality followed by a forward bne.un to the next step.
            foreach(var read in method.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldsfld && i.Operand is FieldReference f && f.DeclaringType.Name=="GameVars" && f.Name=="frame")) {
                if(read.Next==null || Integer(read.Next)!=frame)continue;
                var branch=read.Next.Next;
                if(branch==null || (branch.OpCode.Code!=Code.Bne_Un && branch.OpCode.Code!=Code.Bne_Un_S) || branch.Operand is not Instruction end || end.Offset<=branch.Offset)continue;
                for(var instruction=branch.Next;instruction!=null && instruction.Offset<end.Offset;instruction=instruction.Next)
                    if(predicate(instruction))return true;
            }
            return false;
        }
        bool FrameCall(MethodDefinition method,int frame,string owner,string name)=>FrameBodyContains(method,frame,
            i=>i.Operand is MethodReference m && m.DeclaringType.Name==owner && m.Name==name);
        int checks=0;
        void Check(bool valid,string reason){if(!valid)throw new Exception(reason);checks++;}

        var prepare=Native("SceneManagement","PrepareLaunchingBattleFromCampaign");
        Check(Signature(prepare,"Void","String","String","GameObject") && Calls(prepare,"SceneManagement","SaveAll") &&
            Field(prepare,"SceneManagement","battlefromcampaign") && Field(prepare,"SceneManagement","battlesubfromcampaign"),
            "Campaign battle entry must retain its native save and battlefield/sublevel handoff.");
        var enter=Native("SceneManagement","LoadBattleScenarioFromCampaign");
        Check(new[]{"bo_exitcampaignscenario","bo_rebuildscene","bo_resetloadingscreen","bo_loadcampaignbattle"}.All(name=>Field(enter,"SceneManagement",name)),
            "A campaign battle replaces the scene and native unit objects; it is not a campaign-map panel.");
        var load=Native("SceneManagement","LoadCampaignBattleScenario");
        Check(Signature(load,"Void","Boolean") && Calls(load,"SceneManager","LoadScene") && Text(load,"BattlefieldBase") && Number(load,3) && Number(load,8) &&
            Field(load,"SceneManagement","currentoperation"),
            "Campaign battle loading must retain separate fresh (3) and saved (8) operation paths.");
        var batch=Native("SceneManagement","UpdateBatchOperations");
        Check(Field(batch,"SceneManagement","bo_loadcampaignbattlefromsave") && Field(batch,"SceneManagement","currentcampaignscenario") &&
            Field(batch,"SceneManagement","battlefromcampaign") && Field(batch,"SceneManagement","battlesubfromcampaign") && Calls(batch,"SceneManagement","LoadCampaignBattleScenario"),
            "The saved campaign-battle batch path must restore both battle and campaign context.");

        var save=Native("ImportExportUnitData","SaveBattle");
        Check(Signature(save,"Void","Boolean","String") && Number(save,3) && Text(save,"Campaigns/") && Text(save,"/Battle") &&
            Field(save,"SceneManagement","currentcampaignscenario") && Field(save,"Saving","latestfolder"),
            "A campaign tactical checkpoint belongs in its campaign save's Battle subdirectory.");
        Check(new[]{"SaveScenarioFile","SaveBattleObjectives","SaveBattleEntryPoints","SavePontoons","SaveBattleBuildings","SaveObstacles","SaveArtillery",
            "SaveAdditionalUnitData","SaveBattleUnitData","SaveFrontlineData","SaveBattleSideInfo","SaveCommanders","CreateVersionFile"}.All(name=>Calls(save,"ImportExportUnitData",name)) &&
            Calls(save,"BattleUnits","SaveAllUnits") && Calls(save,"FallenLayer2","Save"),
            "Native tactical saves must retain their unit, terrain/object, side, commander and fallen-unit coverage.");
        var restore=Native("ImportExportUnitData","LoadBattle");
        Check(Signature(restore,"Void") && Number(restore,8) && Text(restore,"/Battle") && Field(restore,"SceneManagement","currentcampaignscenario") && Field(restore,"Saving","latestfolder"),
            "A guest loading a saved campaign battle must retain operation-eight's campaign/save lookup.");
        Check(new[]{"LoadBattleObjectives","LoadBattleEntryPoints","LoadPontoons","LoadBattleBuildings","LoadObstacles","LoadArtillery","LoadAdditionalUnitData",
            "LoadBattleSideInfo","LoadBattleUnitData","LoadFrontlineData"}.All(name=>Calls(restore,"ImportExportUnitData",name)) &&
            Calls(restore,"BattleUnits","SetTimeVariables") && Calls(restore,"FallenLayer2","Load") && Calls(restore,"GameVars","import_commanders"),
            "Native tactical restore must load the full battlefield record before readiness comparison.");

        var startup=Native("BattleController","Update");
        Check(FrameCall(startup,7,"BattleUnits","InitBattleUnits") && FrameCall(startup,8,"BattleUI","InitCompleteUI"),
            "Battle startup still needs native roster and UI initialization.");
        Check(FrameBodyContains(startup,11,i=>i.OpCode==OpCodes.Stsfld && i.Operand is FieldReference f && f.Name=="initializeregiments") &&
            FrameBodyContains(startup,12,i=>i.OpCode==OpCodes.Stsfld && i.Operand is FieldReference f && f.Name=="allregimentsinitialized"),
            "Tactical native initialization still spans the regiment initialization stages.");
        Check(FrameCall(startup,29,"ImportExportUnitData","LoadBattle") && FrameCall(startup,30,"BattleUnits","AllocateUnitsAndObjectivesToAI"),
            "A saved battle is restored before later startup allocation, so readiness cannot be announced merely after roster creation.");
        Check(FrameCall(startup,39,"BattleUI","UpdateArmyOrderButtons") && FrameBodyContains(startup,40,i=>i.OpCode==OpCodes.Stsfld && i.Operand is FieldReference f && f.DeclaringType.Name=="LoadingScreen" && f.Name=="callssofar"),
            "Tactical readiness must wait past final UI/loading initialization.");
        Check(FrameBodyContains(startup,1,i=>i.OpCode==OpCodes.Stsfld && i.Operand is FieldReference f && f.DeclaringType.Name=="GameVars" && f.Name=="gamepaused") &&
            FrameCall(startup,1,"GameVars","SetGameSpeed"),
            "Battle startup writes clock state itself; transition authority must cover startup clock restoration.");
        var regiment=Native("Regiment","Update");
        Check(Calls(regiment,"Regiment","Initialize") && Field(regiment,"GameVars","initializeregiments") &&
            Native("Regiment","Initialize").Parameters.Single().ParameterType.Name=="Boolean",
            "A replica must preserve native Regiment.Initialize instead of suppressing every tactical Update before initialization.");
        Check(!Field(regiment,"GameVars","gamepaused") && new[]{"ProcessOrders","UpdateFiringAndReloading","UpdateMorale","UpdateMelees","UpdateSurrender"}.All(name=>Calls(regiment,"Regiment",name)),
            "Pausing is not a Regiment simulation boundary: native tactical Update dispatches mutation helpers without a top-level pause guard.");
        var ai=Native("AIBattle","Update");
        int blocked=Index(ai,i=>i.Operand is MethodReference m && m.DeclaringType.Name=="BlockedMovingOrder" && m.Name=="UpdateOrders");
        int pause=Index(ai,i=>i.Operand is FieldReference f && f.DeclaringType.Name=="GameVars" && f.Name=="gamepaused");
        Check(blocked>=0 && pause>blocked,"Tactical AI processes blocked orders before its pause return; pause alone cannot establish replica authority.");
        var units=Native("BattleUnits","Update");
        Check(new[]{"CheckTimeIssues","CalculateSideStatsAndUpdateAITasks","DoUnitPositioning","CheckReinforcementArrivals"}.All(name=>Calls(units,"BattleUnits",name)) &&
            Calls(units,"GameVars","CheckAutosaveBattle"),
            "BattleUnits owns tactical placement, reinforcements, time, AI work and autosaving outside Regiment.Update.");
        var ui=Native("BattleUI","Update");
        Check(new[]{"CheckPontoonConstruction","CheckObstacleConstruction","CheckPathSetting","CheckGroupRotation","CheckKeys","CheckPrompts"}.All(name=>Calls(ui,"BattleUI",name)),
            "Native tactical UI dispatches construction, orders and ending prompts; a spectator input bridge needs its own boundary.");

        var exit=Native("SceneManagement","ExitCampaignBattleAndRebuildCampaign");
        Check(Signature(exit,"Void") && Calls(exit,"BattleUnits","SaveBattleResult") && Calls(exit,"SceneManagement","SaveAll") &&
            Field(exit,"SceneManagement","bo_loadcampaignscenario"),
            "Native campaign-battle exit computes results, saves them and queues their import; it cannot be independently executed by a replica.");
        var results=Native("BattleUnits","SaveBattleResult");
        Check(results.ReturnType.Name=="BattleResult" && Calls(results,"Random","Range"),
            "Battle result processing contains randomness and must have a single authoritative owner.");
        Check(Calls(Native("BattleController","Update"),"SceneManagement","ExitCampaignBattleAndRebuildCampaign"),
            "The native quitbattlescene route can reach authoritative battle-result processing and must be guarded on guests.");

        // Inspect the actual compiled integration as well as native prerequisites.
        // CFG checks below follow normal IL edges (not exception dispatch); they
        // establish guard/ordering contracts and never construct Unity objects.
        var bridge=runtime.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign");
        MethodDefinition Bridge(string name)=>bridge.Methods.Single(m=>m.Name==name);
        MethodDefinition Compiled(string owner,string name)=>runtime.MainModule.Types.Single(t=>t.Name==owner).Methods.Single(m=>m.Name==name);
        Instruction CallAt(MethodDefinition method,string owner,string name)=>method.Body.Instructions.First(i=>i.Operand is MethodReference m && m.DeclaringType.Name==owner && m.Name==name);
        Instruction FieldAt(MethodDefinition method,string name)=>method.Body.Instructions.First(i=>i.Operand is FieldReference f && f.Name==name);
        Instruction TextAt(MethodDefinition method,string value)=>method.Body.Instructions.First(i=>i.OpCode==OpCodes.Ldstr && Equals(i.Operand,value));
        IEnumerable<Instruction> Successors(Instruction i)
        {
            if(i.OpCode.FlowControl==FlowControl.Return || i.OpCode.FlowControl==FlowControl.Throw || i.OpCode==OpCodes.Endfinally)yield break;
            if(i.Operand is Instruction target) {
                yield return target;
                if(i.OpCode.FlowControl==FlowControl.Branch)yield break;
            } else if(i.Operand is Instruction[] targets)foreach(var nextTarget in targets)yield return nextTarget;
            if(i.Next!=null)yield return i.Next;
        }
        bool Reaches(Instruction start,Instruction target,Instruction excluded=null)
        {
            var pending=new Stack<Instruction>();var seen=new HashSet<Instruction>();if(start!=null)pending.Push(start);
            while(pending.Count>0) {
                var current=pending.Pop();if(current==excluded || !seen.Add(current))continue;
                if(current==target)return true;
                foreach(var next in Successors(current))pending.Push(next);
            }
            return false;
        }
        bool Dominates(MethodDefinition method,Instruction guard,Instruction target)=>Reaches(method.Body.Instructions[0],target) && !Reaches(method.Body.Instructions[0],target,guard);
        bool RequiresBoolean(MethodDefinition method,Instruction value,bool required,Instruction target)
        {
            var branch=value.Next;while(branch!=null && branch.OpCode==OpCodes.Nop)branch=branch.Next;
            if(branch==null || branch.Operand is not Instruction jumped)return false;
            bool onTrue=branch.OpCode.Code==Code.Brtrue || branch.OpCode.Code==Code.Brtrue_S;
            bool onFalse=branch.OpCode.Code==Code.Brfalse || branch.OpCode.Code==Code.Brfalse_S;
            if(!onTrue && !onFalse)return false;
            var yes=required==onTrue?jumped:branch.Next;var no=required==onTrue?branch.Next:jumped;
            return Dominates(method,value,target) && Reaches(yes,target) && !Reaches(no,target);
        }
        bool RequiresPhase(MethodDefinition method,int phase,Instruction target)
        {
            foreach(var read in method.Body.Instructions.Where(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="MpTacticalLifecycle" && m.Name=="get_Phase")) {
                if(phase==0 && RequiresBoolean(method,read,false,target))return true;
                var number=read.Next;var branch=number?.Next;
                if(number==null || Integer(number)!=phase || branch?.Operand is not Instruction jumped)continue;
                bool unequal=branch.OpCode.Code==Code.Bne_Un || branch.OpCode.Code==Code.Bne_Un_S;
                bool equal=branch.OpCode.Code==Code.Beq || branch.OpCode.Code==Code.Beq_S;
                if((unequal || equal) && Dominates(method,read,target) && Reaches(equal?jumped:branch.Next,target) && !Reaches(equal?branch.Next:jumped,target))return true;
            }
            return false;
        }
        bool RequiresEquality(MethodDefinition method,Instruction right,Instruction target)
        {
            var branch=right.Next;
            if(branch?.Operand is not Instruction jumped)return false;
            bool unequal=branch.OpCode.Code==Code.Bne_Un || branch.OpCode.Code==Code.Bne_Un_S;
            bool equal=branch.OpCode.Code==Code.Beq || branch.OpCode.Code==Code.Beq_S;
            return (equal || unequal) && Dominates(method,right,target) && Reaches(equal?jumped:branch.Next,target) && !Reaches(equal?branch.Next:jumped,target);
        }
        bool Ordered(MethodDefinition method,params Instruction[] points)=>points.Zip(points.Skip(1),(a,b)=>a.Offset<b.Offset && Reaches(a,b)).All(v=>v);
        bool FinallyResets(MethodDefinition method,string field)=>method.Body.ExceptionHandlers.Where(h=>h.HandlerType==ExceptionHandlerType.Finally).Any(h=>
            method.Body.Instructions.Any(i=>i.Offset>=h.HandlerStart.Offset && (h.HandlerEnd==null || i.Offset<h.HandlerEnd.Offset) &&
                i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.Name==field && Integer(i.Previous)==0));
        void Hook(string nativeOwner,string nativeName,string hook)
        {
            var original=Native(nativeOwner,nativeName);var wrapper=Bridge(hook);
            var expected=original.Parameters.Select(p=>p.ParameterType.FullName);
            if(!original.IsStatic)expected=new[]{original.DeclaringType.FullName}.Concat(expected);
            Check(wrapper.IsStatic && wrapper.ReturnType.FullName==original.ReturnType.FullName && wrapper.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(expected),
                "Compiled tactical hook signature must match native "+nativeOwner+"."+nativeName+" -> "+hook+".");
        }
        Hook("SceneManagement","PrepareLaunchingBattleFromCampaign","TacticalPrepare");
        Hook("SceneManagement","ExitCampaignBattleAndRebuildCampaign","TacticalExit");
        Hook("LoadingScreen","HideBlackBackground","TacticalHideBlack");
        foreach(var item in new[]{("BattleController","TacticalBattleController"),("AIBattle","TacticalBattleAi"),("Projectile","TacticalProjectile"),
            ("Objectives","TacticalObjectives"),("Building","TacticalBuilding"),("ArmyGroup","TacticalArmyGroup"),("BattleFront","TacticalBattleFront"),
            ("BattlefieldSetup","TacticalBattlefieldSetup"),("Weather","TacticalWeather"),("UniStormWeatherSystem_C","TacticalStorm")})Hook(item.Item1,"Update",item.Item2);
        Hook("Regiment","FireBullet","TacticalFireBullet");Hook("Coy","OnTriggerEnter","TacticalCoyEnter");Hook("Coy","OnTriggerExit","TacticalCoyExit");Hook("EntryPoint","OnTriggerEnter","TacticalEntryPointEnter");
        Hook("BattleUI","SetBattlePanelShownState","ReplicaBattleDecision");

        var request=Bridge("TacticalPrepare");var begin=CallAt(request,"MultiplayerCampaign","BeginTacticalHold");
        Check(RequiresBoolean(request,CallAt(request,"MultiplayerCampaign","get_TacticalHolding"),false,begin) &&
            RequiresBoolean(request,FieldAt(request,"host"),true,begin) && Field(request,"MultiplayerCampaign","tacticalProofEnabled") &&
            Ordered(request,CallAt(request,"MultiplayerCampaign","EnsureTacticalSession"),begin,CallAt(request,"MpTacticalDrain","Offer"),CallAt(request,"MultiplayerCampaign","Send")) && !Calls(request,"SceneManagement","SaveAll"),
            "Connected entry must establish the opt-in host hold and offer before any campaign save/scene destruction.");
        var hold=Bridge("BeginTacticalHold");
        Check(Ordered(hold,FieldAt(hold,"tacticalHolding"),CallAt(hold,"MultiplayerCampaign","HoldTacticalClock"),CallAt(hold,"MultiplayerCampaign","BlockInput"),CallAt(hold,"MultiplayerCampaign","ClosePanel")),
            "Both peer hold receipt paths must pause and block native input before closing the local session panel.");
        Check(Calls(Bridge("TacticalMessage"),"MpTacticalDrain","ValidateOffer") && Calls(Bridge("TacticalMessage"),"MultiplayerCampaign","BeginTacticalHold") &&
            Ordered(Bridge("Handle"),CallAt(Bridge("Handle"),"MultiplayerCampaign","TacticalMessage"),CallAt(Bridge("Handle"),"MultiplayerCampaign","DataMessage")),
            "Incoming tactical holds must be validated and processed before ordinary campaign frames.");
        var main=Bridge("Update");var tick=Bridge("TacticalTick");
        Check(Ordered(main,CallAt(main,"MultiplayerCampaign","TacticalTick"),CallAt(main,"MultiplayerCampaign","DataTick"),CallAt(main,"MultiplayerCampaign","GuestMapInput")) &&
            RequiresBoolean(main,CallAt(main,"MultiplayerCampaign","TacticalTick"),false,CallAt(main,"MultiplayerCampaign","GuestMapInput")),
            "The live frame loop must stop ordinary campaign updates/input when the tactical loop handles that frame.");
        Check(Ordered(tick,CallAt(tick,"MultiplayerCampaign","DataTick"),CallAt(tick,"MpTacticalDrain","Fence")) &&
            Ordered(tick,CallAt(tick,"MultiplayerCampaign","DataTick"),CallAt(tick,"MpTacticalDrain","Commit"),CallAt(tick,"MultiplayerCampaign","StartNativeBattle")) &&
            Calls(tick,"MpTransport","get_PendingOutgoing") && Calls(tick,"MultiplayerCampaign","get_TacticalDataQuiet") &&
            RequiresBoolean(tick,CallAt(tick,"MpTacticalDrain","Commit"),true,CallAt(tick,"MultiplayerCampaign","StartNativeBattle")),
            "Native scene replacement must follow FIFO drain/fence processing and a successful matching-revision commit.");
        var start=Bridge("StartNativeBattle");var saveCampaign=CallAt(start,"SceneManagement","SaveAll");
        Check(RequiresBoolean(start,CallAt(start,"MpTacticalDrain","get_Committed"),true,saveCampaign) &&
            RequiresBoolean(start,CallAt(start,"MultiplayerCampaign","get_TacticalDataQuiet"),true,saveCampaign) &&
            Ordered(start,saveCampaign,CallAt(start,"MpCheckpoint","Pack"),TextAt(start,"GTIP_Original_TacticalPrepare")) && FinallyResets(start,"tacticalNativeWrite"),
            "Host staging must capture a pre-battle campaign only after drain commit, then enter native battle under a scoped write permission.");
        Check(Ordered(tick,CallAt(tick,"MultiplayerCampaign","NativeTacticalReady"),CallAt(tick,"ImportExportUnitData","SaveBattle"),CallAt(tick,"MpTacticalCheckpoint","Pack"),CallAt(tick,"MpTacticalSession","BeginBattle")) &&
            RequiresBoolean(tick,CallAt(tick,"MultiplayerCampaign","NativeTacticalReady"),true,CallAt(tick,"ImportExportUnitData","SaveBattle")),
            "Host staging must finish native initialization before capturing and distributing the common operation-eight battle save.");

        foreach(var item in new[]{("ReplicaRegiment","TryTacticalRegiment","GTIP_Original_ReplicaRegiment"),("ReplicaUnits","TryTacticalUnits","GTIP_Original_ReplicaUnits"),
            ("ReplicaCampaign","TryTacticalCampaign","GTIP_Original_ReplicaCampaign")}) {
            var wrapper=Bridge(item.Item1);var gate=CallAt(wrapper,"MultiplayerCampaign",item.Item2);var original=TextAt(wrapper,item.Item3);
            Check(RequiresBoolean(wrapper,gate,false,original) && Ordered(wrapper,gate,CallAt(wrapper,"MultiplayerCampaign","get_IsReplica"),original) &&
                Calls(Bridge(item.Item2),"MultiplayerCampaign","get_TacticalHolding") && !Calls(Bridge(item.Item2),"MultiplayerCampaign","get_IsReplica"),
                "Held native "+item.Item1+" must be intercepted for both host and guest before campaign replica fallback.");
        }
        foreach(var name in new[]{"TacticalBattleAi","TacticalProjectile","TacticalBuilding","TacticalArmyGroup","TacticalBattleFront","TacticalBattlefieldSetup","TacticalWeather","TacticalStorm","TacticalCoyEnter","TacticalCoyExit","TacticalEntryPointEnter","TacticalFireBullet"}) {
            var wrapper=Bridge(name);
            Check(RequiresBoolean(wrapper,CallAt(wrapper,"MultiplayerCampaign","get_TacticalHolding"),false,CallAt(wrapper,"NativeAccess","Call")) && !Calls(wrapper,"MultiplayerCampaign","get_IsReplica"),
                "Held "+name+" must not invoke native mutation on either peer.");
        }
        Check(Calls(Bridge("TryTacticalRegiment"),"MultiplayerCampaign","InitializeTactical") && !Text(Bridge("TryTacticalRegiment"),"GTIP_Original_ReplicaRegiment") &&
            Calls(Bridge("TryTacticalUnits"),"MultiplayerCampaign","InitializeTactical") && !Text(Bridge("TryTacticalUnits"),"GTIP_Original_ReplicaUnits") && Number(Bridge("TryTacticalUnits"),24) && Number(Bridge("TryTacticalUnits"),28),
            "Held initialization uses scoped construction, with placement windows ending before saved campaign/battle restoration.");
        var editor=runtime.MainModule.Types.Single(t=>t.Name=="CampaignEditor");var aiGate=editor.Methods.Single(m=>m.Name=="EditorCampaignAi");
        var inputGate=editor.Methods.Single(m=>m.Name=="EditorBattleUI");
        Check(RequiresBoolean(aiGate,CallAt(aiGate,"MultiplayerCampaign","get_TacticalHolding"),false,CallAt(aiGate,"NativeAccess","Call")) &&
            Calls(Bridge("get_BlocksInput"),"MultiplayerCampaign","get_TacticalHolding") &&
            RequiresBoolean(inputGate,CallAt(inputGate,"MultiplayerCampaign","get_BlocksInput"),false,CallAt(inputGate,"NativeAccess","Call")),
            "Campaign AI and native UI input must remain held for the host as well as the replica.");

        var loadNative=Compiled("MpTacticalNativeScene","Load");var loadCheckpoint=Bridge("LoadTacticalCheckpoint");
        Check(bridge.Fields.Single(f=>f.Name=="tacticalTicket").FieldType.Name=="MpTacticalNativeTicket" &&
            loadCheckpoint.Parameters.Single().ParameterType.Name=="MpTacticalLoadRequest" && loadNative.ReturnType.Name=="MpTacticalNativeTicket" &&
            Ordered(loadCheckpoint,CallAt(loadCheckpoint,"MultiplayerCampaign","ClearTacticalCampaignState"),CallAt(loadCheckpoint,"MpTacticalNativeScene","Load"),
                loadCheckpoint.Body.Instructions.Single(i=>i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.Name=="tacticalTicket")),
            "Each native reload must clear obsolete campaign bindings and retain the typed ticket for that exact requested archive.");
        Check(Ordered(loadNative,CallAt(loadNative,"MpTacticalLoadPlan","Read"),CallAt(loadNative,"Application","CanStreamedLevelBeLoaded"),
                CallAt(loadNative,"BattlefieldSetup","ClearBattlefield"),CallAt(loadNative,"ImportExportUnitData","LoadScenarioFile"),CallAt(loadNative,"List`1","AddRange")) &&
            !Calls(loadNative,"SceneManagement","ExitCampaignBattleAndRebuildCampaign") && !Calls(loadNative,"BattleUnits","SaveBattleResult") && Number(loadNative,6) && Number(loadNative,8),
            "Native reload preflight must precede cleanup/import/queueing, with operation six/eight and no randomized result path.");
        var ready=Bridge("NativeTacticalReady");
        Check(ready.ReturnType.Name=="Boolean" && Number(ready,60) && Number(ready,4) &&
            new[]{"frame","allregimentsinitialized","unitfolder","tacticalOldRoot","currentoperation","batchoperation","initialized","currentbattlescenario"}.All(name=>ready.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name==name)) &&
            Calls(ready,"Enumerable","Any") && Field(ready,"MultiplayerCampaign","tacticalTicket") && Calls(ready,"MpTacticalNativeTicket","get_Battlefield"),
            "Native readiness must verify the new initialized scene, completed batches, expected campaign/battle context and ticket battlefield.");
        var captureWorld=CallAt(tick,"MpTacticalNativeState","Capture");
        var hashWorld=tick.Body.Instructions.First(i=>i.Offset>captureWorld.Offset && i.Operand is MethodReference m && m.DeclaringType.Name=="MpCheckpoint" && m.Name=="Hash");
        Check(Ordered(tick,captureWorld,FieldAt(tick,"ContentHash"),hashWorld,
                CallAt(tick,"MpTacticalSession","SceneLoaded")) && Field(tick,"MpTacticalNativeTicket","Request"),
            "Readiness must attest the initialized native graph plus installed content, using the exact native ticket request rather than received bytes alone.");
        var sceneLoaded=Compiled("MpTacticalSession","SceneLoaded");
        Check(sceneLoaded.Parameters[0].ParameterType.Name=="MpTacticalLoadRequest" &&
            // Roslyn lowers Object.ReferenceEquals(request,activeLoad) to a direct
            // reference branch, so inspect its accepted/rejected paths, not a call name.
            RequiresEquality(sceneLoaded,FieldAt(sceneLoaded,"activeLoad"),CallAt(sceneLoaded,"MpTacticalLifecycle","BattleLoaded")) &&
            new[]{"Generation","ReturningToCampaign","Hash","activeLoad","transferHash"}.All(name=>sceneLoaded.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name==name)),
            "A stale or substituted native completion ticket cannot satisfy the current session readiness barrier.");
        var complete=Compiled("MpTacticalLifecycle","CompleteBarrier");var phaseWrite=CallAt(complete,"MpTacticalLifecycle","set_Phase");
        Check(RequiresBoolean(complete,FieldAt(complete,"localReady"),true,phaseWrite) && RequiresBoolean(complete,FieldAt(complete,"peerReady"),true,phaseWrite) &&
            RequiresBoolean(complete,CallAt(complete,"String","op_Inequality"),false,phaseWrite) && Calls(complete,"MpTacticalLifecycle","Fault"),
            "The compiled lifecycle may release a scene only after both readiness receipts carry matching initialized baselines.");

        var reveal=Bridge("CompleteTacticalBarrier");var nativeHide=TextAt(reveal,"GTIP_Original_TacticalHideBlack");
        Check(RequiresPhase(reveal,4,nativeHide) && Ordered(reveal,CallAt(reveal,"MultiplayerCampaign","CloseTacticalStartupPanels"),nativeHide) &&
            RequiresPhase(reveal,0,CallAt(reveal,"MultiplayerCampaign","RestoreTacticalCampaign")),
            "The shared battlefield must reveal only after the both-ready PausedBattle phase; campaign release uses its verified return path.");
        var hide=Bridge("TacticalHideBlack");var overlay=Bridge("DrawTacticalLoading");
        Check(RequiresBoolean(hide,CallAt(hide,"MultiplayerCampaign","get_TacticalLoading"),false,CallAt(hide,"NativeAccess","Call")) &&
            RequiresBoolean(overlay,CallAt(overlay,"MultiplayerCampaign","get_TacticalLoading"),true,CallAt(overlay,"GUI","DrawTexture")) &&
            Calls(overlay,"Color","get_black") && Calls(overlay,"Screen","get_width") && Calls(overlay,"Screen","get_height") && Calls(Bridge("OnGUI"),"MultiplayerCampaign","DrawTacticalLoading"),
            "A full-screen held overlay and native hide gate must survive independent loading-screen fade/HideBlackBackground callbacks.");
        var panels=Bridge("CloseTacticalStartupPanels");
        Check(new[]{"panel_choosepaper","panel_briefing","panel_objectivespaper","panel_statistics"}.All(name=>Field(panels,"PanelHandler",name)) &&
            Calls(panels,"PanelHandler","HidePanel") && Calls(panels,"Camera_Battlefield","PreventCamMovement"),
            "Revealing the paused proof must clear native startup panel state and release its local camera locks.");
        var returnCampaign=Bridge("ReturnTacticalCampaign");var endTactical=Bridge("EndTactical");
        Check(Field(returnCampaign,"MultiplayerCampaign","tacticalReturnArchive") && Calls(returnCampaign,"MpTacticalSession","BeginReturn") &&
            RequiresPhase(returnCampaign,4,CallAt(returnCampaign,"MpTacticalSession","BeginReturn")) &&
            !Calls(returnCampaign,"BattleUnits","SaveBattleResult") && !Calls(returnCampaign,"SceneManagement","ExitCampaignBattleAndRebuildCampaign") &&
            RequiresBoolean(Bridge("TacticalExit"),CallAt(Bridge("TacticalExit"),"MultiplayerCampaign","get_TacticalHolding"),false,TextAt(Bridge("TacticalExit"),"GTIP_Original_TacticalExit")),
            "Ending the shared proof must restore the pre-battle archive while the ordinary native result-producing exit remains blocked.");
        Check(Ordered(Bridge("End"),CallAt(Bridge("End"),"MultiplayerCampaign","EndTactical"),CallAt(Bridge("End"),"MultiplayerCampaign","RestoreReplicaState")) &&
            Calls(endTactical,"MpTacticalSession","Dispose") && Field(endTactical,"MultiplayerCampaign","tacticalFailureHeld") && Calls(endTactical,"MultiplayerCampaign","HoldTacticalClock") &&
            !Calls(endTactical,"BattleUnits","SaveBattleResult"),
            "A dropped tactical connection must invalidate transfer tickets and retain a held native world instead of restoring stale campaign objects or calculating results.");

        var decision=Bridge("ReplicaBattleDecision");var choice=Bridge("ExecuteEngagementAction");
        Check(Text(decision,"engagement-choice") && Calls(decision,"MultiplayerCampaign","RequestNative") && Calls(choice,"BattleUI","SetBattlePanelShownState") &&
            Native("BattleUI","SetBattlePanelShownState").Body.Instructions.Any(i=>i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.Name=="battlepanelshown") &&
            Calls(Native("BattleUI","Update"),"BattleUI","UpdateBattlePanel") && Calls(Native("BattleUI","UpdateBattlePanel"),"SceneManagement","PrepareLaunchingBattleFromCampaign"),
            "A guest initial engagement choice must reach the host native Prepare hook through its validated shared decision route.");
        Console.WriteLine($"Native tactical contracts: {checks} checks; native ABI and compiled hold/drain/load/readiness/reveal/return gates verified. Network transport and Unity gameplay are not exercised.");
    }
}
