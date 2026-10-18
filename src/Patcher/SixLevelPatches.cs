using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class SixLevelPatches
{
    internal static List<string> Apply(AssemblyDefinition game, AssemblyDefinition helper)
    {
        var module = game.MainModule;
        var hooks = new List<string>();
        MethodReference Helper(string type, string name) => module.ImportReference(helper.MainModule.Types.Single(t => t.Name == type).Methods.Single(m => m.Name == name));
        MethodDefinition Method(string type, string name, string? first = null,string? last=null) => Types(module.Types).Single(t => t.Name == type).Methods.Single(m => m.Name == name && (first == null || m.Parameters[0].ParameterType.Name == first) && (last==null || m.Parameters.Last().ParameterType.Name==last));
        void Wrap(string type, string name, string target, string helperType = "SixLevelRuntime", string? first = null,string? last=null)
        {
            var original = Method(type, name, first,last);
            var hook = Helper(helperType, target);
            var expected = original.Parameters.Select(p => p.ParameterType.FullName).ToList();
            if (!original.IsStatic) expected.Insert(0, original.DeclaringType.FullName);
            if (expected.Count != hook.Parameters.Count || expected.Where((name, i) => name != hook.Parameters[i].ParameterType.FullName &&
                    !(hook.Parameters[i].ParameterType.FullName == "System.Object" && (name == "AICampaign/AIFaction" || name=="BuildingList/BuildingLine"))).Any() || original.ReturnType.FullName != hook.ReturnType.FullName)
                throw new InvalidOperationException("Hook signature mismatch: " + original.FullName);
            original.Name = "GTIP_Original_" + target;
            var wrapper = new MethodDefinition(name, original.Attributes, original.ReturnType);
            foreach (var p in original.Parameters)
            {
                var copy = new ParameterDefinition(p.Name, p.Attributes, p.ParameterType);
                if (p.HasConstant) copy.Constant = p.Constant;
                wrapper.Parameters.Add(copy);
            }
            original.DeclaringType.Methods.Add(wrapper);
            var il = wrapper.Body.GetILProcessor();
            if (!original.IsStatic) il.Emit(OpCodes.Ldarg_0);
            foreach (var p in wrapper.Parameters) il.Emit(OpCodes.Ldarg, p);
            il.Emit(OpCodes.Call, hook);
            il.Emit(OpCodes.Ret);
            foreach (var t in Types(module.Types)) foreach (var m in t.Methods.Where(m => m.HasBody))
                foreach (var instruction in m.Body.Instructions)
                    if (instruction.Operand is MethodReference r && r.Resolve() == original) instruction.Operand = wrapper;
            hooks.Add(type + "." + name);
        }
        Wrap("Regiment", "CalculateFightingSpirit", "FightingSpirit");
        Wrap("Regiment", "GetUnitSymbolData", "Symbol");
        Wrap("CampaignMainPanel", "CheckArmyStateSelection", "CheckArmyStateSelection");
        Wrap("CampaignMainPanel", "RecruitGroup", "RecruitGroup");
        Wrap("CampaignMainPanel", "UpgradeCorpsToArmy", "UpgradeArmy");
        Wrap("ArmyGroup", "AddUnit", "AddArmyGroupUnit");
        Wrap("BattleUI", "UpdateCommanderPanel", "CommanderPanel");
        Wrap("CampaignMainPanel", "UpdateCreateCorpsButton", "UpgradeButtons");
        Wrap("ToolsUnitList", "CreateList", "Sidebar", "HierarchySidebar");
        Wrap("BattleUnits", "GetUnitVisibilityStateOnCampaignMap", "Visibility", first: "Regiment");
        Wrap("ArmyGroup", "CreateNewArmyGroup", "CreateArmyGroup");
        Wrap("AICampaign", "UnitIsFightingForce", "FightingForce");
        Wrap("AICampaign", "GetClosestFightingForce", "ClosestFightingForce");
        Wrap("AICampaign", "RaiseNewCampaignGroup", "RaiseAiGroup");
        Wrap("BattleUI", "Update", "EditorBattleUI", "CampaignEditor");
        Wrap("Camera_Battlefield", "Update", "EditorCamera", "CampaignEditor");
        Wrap("AICampaign", "Update", "EditorCampaignAi", "CampaignEditor");
        Wrap("SceneManagement", "SaveAll", "ReplicaSave", "MultiplayerCampaign");
        Wrap("GameVars", "SetGameSpeed", "ReplicaSpeed", "MultiplayerCampaign");
        Wrap("PanelHandler","CheckGamePausePanel","ReplicaPausePanel","MultiplayerCampaign",first:"Panel");
        Wrap("PanelHandler","CheckGamePausePanel","ReplicaPauseObject","MultiplayerCampaign",first:"GameObject");
        Wrap("RollingText","AddNew","ReplicaMapNotice","MultiplayerCampaign");
        Wrap("BattleUnits","SetWaypoint","ReplicaWaypoint","MultiplayerCampaign",first:"Regiment");
        Wrap("PanelHandler","CallPanelOnRMBReleased","ReplicaMovementPanel","MultiplayerCampaign");
        Wrap("BattleUnits","StopAllUnits","ReplicaStopAll","MultiplayerCampaign");
        Wrap("CampaignController", "Update", "ReplicaCampaign", "MultiplayerCampaign");
        Wrap("BattleUnits", "Update", "ReplicaUnits", "MultiplayerCampaign");
        Wrap("Regiment", "Update", "ReplicaRegiment", "MultiplayerCampaign");
        Wrap("Regiment", "UpdateGroupStrength", "ReplicaGroupStats", "MultiplayerCampaign");
        Wrap("Town", "Update", "ReplicaTown", "MultiplayerCampaign");
        Wrap("IIP", "Update", "ReplicaIip", "MultiplayerCampaign");
        Wrap("CBuilding", "Update", "ReplicaBuilding", "MultiplayerCampaign");
        Wrap("Autocalc", "Update", "ReplicaAutocalc", "MultiplayerCampaign");
        Wrap("ArmyGroup", "IncreaseCommanderExperience", "ReplicaGroupExperience", "MultiplayerCampaign");
        Wrap("Messages", "Update", "ReplicaMessages", "MultiplayerCampaign");
        Wrap("Messages", "RemoveMessage", "ReplicaRemoveDispatch", "MultiplayerCampaign");
        Wrap("Policy", "CheckSelections", "ReplicaPolicySelection", "MultiplayerCampaign");
        Wrap("BattleUI", "LateUpdate", "ReplicaUiLate", "MultiplayerCampaign");
        Wrap("BattleUnits", "MoveUnit", "ReplicaMoveUnit", "MultiplayerCampaign");
        Wrap("Projects", "AppointProject", "ReplicaProject", "MultiplayerCampaign");
        Wrap("Newspaper", "UpdateShow", "ReplicaNewspaperShow", "MultiplayerCampaign");
        Wrap("Newspaper", "Close", "ReplicaNewspaperClose", "MultiplayerCampaign");
        Wrap("Newspaper", "UpdateNewspaperQueue", "ReplicaNewspaperQueue", "MultiplayerCampaign");
        Wrap("CampaignMainPanel", "RecruitSingleBrigade", "ReplicaRecruitDialog", "MultiplayerCampaign");
        Wrap("GameVars", "LoadKeyCodes", "LoadHotkeys", "MultiplayerCampaign");
        Wrap("Finances", "UpdateInitialSliderValues", "FinanceInitial", "MultiplayerCampaign");
        Wrap("Finances", "UpdateSliders", "FinanceSliders", "MultiplayerCampaign");
        Wrap("Finances", "CheckCheckBoxes", "FinanceCheckbox", "MultiplayerCampaign");
        Wrap("AlarmInfo", "UpdateButtons", "ReplicaAlertButtons", "MultiplayerCampaign");
        Wrap("AlarmInfo", "MoveToPosition", "ReplicaAlertPosition", "MultiplayerCampaign");
        Wrap("CampaignMainPanel", "UpdateRegimentDummy", "ReplicaRecruitPreview", "MultiplayerCampaign");
        Wrap("CampaignMainPanel", "Recruit", "ReplicaRecruitCommit", "MultiplayerCampaign");
        Wrap("CampaignMainPanel", "CancelRecruiting", "ReplicaRecruitCancel", "MultiplayerCampaign");
        Wrap("CampaignMainPanel", "ChangeSelection", "ReplicaMainSelection", "MultiplayerCampaign");
        Wrap("CampaignMainPanel", "OnDisable", "ReplicaMainPanelClose", "MultiplayerCampaign");
        Wrap("CommandersList", "AssignCommander", "ReplicaCommanderAssign", "MultiplayerCampaign");
        Wrap("CampaignMainPanel", "UpdateCommanderData", "ReplicaCommanderPreview", "MultiplayerCampaign");
        Wrap("WeaponList","AssignWeapon","ReplicaAssignWeapon","MultiplayerCampaign");
        Wrap("WeaponList","PlaceWeaponOrder","ReplicaWeaponOrder","MultiplayerCampaign");
        Wrap("CampaignMainPanel","Promote","ReplicaPromote","MultiplayerCampaign");
        Wrap("CBuilding","UpdateManualBuildingPlacement","ReplicaBuildingPlacement","MultiplayerCampaign");
        Wrap("CBuildingIcon","Upgrade","ReplicaBuildingUpgrade","MultiplayerCampaign");
        Wrap("Production","CheckUpgradeButtonInteraction","ReplicaProductionUpgrade","MultiplayerCampaign");
        Wrap("Production","Update","ReplicaProductionView","MultiplayerCampaign");
        Wrap("Trade","Update","ReplicaTradeView","MultiplayerCampaign");
        Wrap("CBuildingIcon","SwitchStorageLevelTarget","ReplicaBuildingStock","MultiplayerCampaign");
        Wrap("RailroadList","StartConstruction","ReplicaRailroadConstruction","MultiplayerCampaign");
        Wrap("BuildingList","StartAutoConstruction","ReplicaAutoBuilding","MultiplayerCampaign");
        Wrap("BuildingList","UpdateCheckBox","ReplicaConstructionOptions","MultiplayerCampaign");
        Wrap("RailroadList","UpdateCheckBox","ReplicaRailroadOptions","MultiplayerCampaign");
        Wrap("BattleUnits","CreateGarrisonDummy","ReplicaGarrison","MultiplayerCampaign");
        Wrap("Vessel","UpdateConsumation","ReplicaVesselConsumption","MultiplayerCampaign");
        Wrap("CampaignMainPanel","CreateFleet","ReplicaCreateFleet","MultiplayerCampaign");
        Wrap("CampaignMainPanel","CreateShip","ReplicaCreateShip","MultiplayerCampaign");
        Wrap("BattleUnits","DeleteShip","ReplicaDeleteShip","MultiplayerCampaign");
        Wrap("BattleUnits","MoveShip","ReplicaShipToFleet","MultiplayerCampaign",last:"Regiment");
        Wrap("BattleUnits","MoveShip","ReplicaShipToPool","MultiplayerCampaign",last:"Int32");
        Wrap("BattleUI","SetBattlePanelShownState","ReplicaBattleDecision","MultiplayerCampaign");
        Wrap("BattlePanel","Update","ReplicaBattlePanelUpdate","MultiplayerCampaign");
        Wrap("BuildingList","Update","ReplicaBuildingList","MultiplayerCampaign");
        Wrap("RailroadList","Update","ReplicaRailroadList","MultiplayerCampaign");
        Wrap("WeaponList","Update","ReplicaWeaponList","MultiplayerCampaign");
        Wrap("WeaponList","UpdateCheckBox","ReplicaWeaponOptions","MultiplayerCampaign");
        Wrap("SpottedEnemy","Update","ReplicaSightings","MultiplayerCampaign");
        Wrap("CBuilding","UpdateBuildingLists","ReplicaBuildingLists","MultiplayerCampaign");
        Wrap("Fleet","Update","ReplicaFleetUpdate","MultiplayerCampaign");
        Wrap("CampaignMainPanel","UpdateUnitNameInputs","ReplicaUnitName","MultiplayerCampaign");
        Wrap("FleetInfoPanel","UpdateFleetNameInputs","ReplicaFleetName","MultiplayerCampaign");
        Wrap("FleetInfoPanel","UpdateShipNameInputs","ReplicaShipName","MultiplayerCampaign");
        Wrap("AICampaign", "ZoneRecruiting", "Recruit", "AiOrganizationDiagnostics");
        Wrap("AICampaign", "CheckCombinationOfUnits", "Combine", "AiOrganizationDiagnostics");
        Wrap("AICampaign", "CheckTransferOfUnits", "Transfer", "AiOrganizationDiagnostics");
        Wrap("AICampaign", "CheckCombinationOfBrigades", "CombineCombat", "AiOrganizationDiagnostics");
        Wrap("AICampaign", "CheckArmyGroupManagement", "ManageGroups", "AiOrganizationDiagnostics");
        Wrap("OOB", "UpdateOOB", "Update", "HierarchyOob");
        Wrap("OOB", "AddPlusIcon", "PlusIcon", "HierarchyOob");
        Wrap("BattleUnits", "SetGroupFormation", "Formation", "HierarchyFormation", "Regiment");

        var visibility=Method("Regiment","CheckRegimentVisibility");
        var hiddenCalls=visibility.Body.Instructions.Where(i=>i.Operand is MethodReference r && r.Name=="UnitIsHidden").ToArray();
        if(hiddenCalls.Length!=2)throw new InvalidOperationException("Unexpected native fog checks.");
        var vil=visibility.Body.GetILProcessor();
        foreach(var call in hiddenCalls) {
            var unit=vil.Create(OpCodes.Ldarg_0);vil.InsertAfter(call,unit);
            vil.InsertAfter(unit,vil.Create(OpCodes.Call,Helper("MultiplayerCampaign","ReplicaHidden")));
        }
        hooks.Add("Regiment.HostFogVisibility");

        // Replace reads only inside drawing routines. Logical transforms, order
        // validators and the authoritative map audit retain native coordinates.
        foreach(var drawing in new[]{Method("PanelHandler","UpdatePanels"),Method("Ship","UpdatePosition")}) {
            var reads=drawing.Body.Instructions.Where(i=>i.Operand is MethodReference m && m.DeclaringType.FullName=="UnityEngine.Transform" && m.Name=="get_position" &&
                (drawing.DeclaringType.Name!="PanelHandler" || (i.Previous?.Previous?.Operand is FieldReference f && f.Name=="referencego"))).ToArray();
            if(reads.Length==0)throw new InvalidOperationException("Native visual position reads changed: "+drawing.FullName);
            if(drawing.DeclaringType.Name=="PanelHandler" && reads.Length!=4)throw new InvalidOperationException("Native panel anchor reads changed.");
            foreach(var read in reads){read.OpCode=OpCodes.Call;read.Operand=Helper("MultiplayerCampaign","ReplicaDrawPosition");}
            hooks.Add(drawing.DeclaringType.Name+".ReplicaVisualPosition");
        }
        var spriteDrawing=Method("Regiment","UpdateRegimentSpritePos");
        var blockReads=spriteDrawing.Body.Instructions.Where(i=>i.Operand is MethodReference m && m.DeclaringType.Name=="Regiment" && m.Name=="GetBlockobjectPosition").ToArray();
        if(blockReads.Length==0)throw new InvalidOperationException("Native sprite position reads changed.");
        foreach(var read in blockReads){read.OpCode=OpCodes.Call;read.Operand=Helper("MultiplayerCampaign","ReplicaDrawBlock");}
        hooks.Add("Regiment.ReplicaVisualSprites");

        // The first read diagnoses garrison placeholders; preserve it. The
        // second read removes unassigned dummy officers from the native list.
        var officerList = Method("CommandersList", "UpdateList");
        var dummyReads = officerList.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldfld &&
            i.Operand is FieldReference f && f.Name == "usedummyphoto").ToArray();
        if (dummyReads.Length != 2) throw new InvalidOperationException("Unexpected officer-list dummy filters.");
        dummyReads[1].OpCode = OpCodes.Call;
        dummyReads[1].Operand = Helper("OfficerVisibility", "HideDummy");
        hooks.Add("CommandersList.UnassignedDummyFilter");
        var stateReads = officerList.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldfld &&
            i.Operand is FieldReference f && f.Name == "recruitable").ToArray();
        if (stateReads.Length != 1) throw new InvalidOperationException("Unexpected officer state filter.");
        stateReads[0].OpCode = OpCodes.Call; stateReads[0].Operand = Helper("OfficerVisibility", "RecruitableOfficerState");
        var rankChecks = officerList.Body.Instructions.Where(i => i.Operand is MethodReference r &&
            r.Name == "CheckSuitableRank" && r.Parameters.Count == 1 && r.Parameters[0].ParameterType.Name == "Regiment").ToArray();
        if (rankChecks.Length != 2) throw new InvalidOperationException("Unexpected officer rank checks.");
        foreach (var check in rankChecks) { check.OpCode = OpCodes.Call; check.Operand = Helper("OfficerVisibility", "Suitable"); }
        hooks.Add("CommandersList.StateAndMinimumRank");


        var create = Method("BattleUnits", "create_regiment");
        var cil = create.Body.GetILProcessor();
        var entry = create.Body.Instructions[0];
        cil.InsertBefore(entry, cil.Create(OpCodes.Ldarg, create.Parameters.Single(p => p.Name == "typ")));
        cil.InsertBefore(entry, cil.Create(OpCodes.Ldarg, create.Parameters.Single(p => p.Name == "overridelev")));
        cil.InsertBefore(entry, cil.Create(OpCodes.Call, Helper("SixLevelRuntime", "ImportedType")));
        cil.InsertBefore(entry, cil.Create(OpCodes.Starg, create.Parameters.Single(p => p.Name == "typ")));
        foreach (var ret in create.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray())
        {
            ret.OpCode = OpCodes.Call; ret.Operand = Helper("SixLevelRuntime", "CreatedUnit");
            cil.InsertAfter(ret, cil.Create(OpCodes.Ret));
        }
        hooks.Add("BattleUnits.create_regiment");

        void ExpandArray(MethodDefinition method, string field, int before)
        {
            var store = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Stfld && i.Operand is FieldReference f && f.Name == field);
            var size = store.Previous.Previous;
            if (store.Previous.OpCode != OpCodes.Newarr || Convert.ToInt32(size.Operand) != before)
                throw new InvalidOperationException("Unexpected allocation: " + field);
            size.OpCode = OpCodes.Ldc_I4; size.Operand = 19;
            hooks.Add(method.DeclaringType.Name + "." + field);
        }
        ExpandArray(module.Types.Single(t => t.Name == "BattleUnits").NestedTypes.Single(t => t.Name == "AllUnitsOfAlliance").Methods.Single(m => m.IsConstructor), "unitsinlevel", 17);
        ExpandArray(Method("OOB", "Initialize"), "unitsinhierarchylevel", 18);
        var naming = Method("Regiment", "UpdateUnitNameDefault");
        var comparison = naming.Body.Instructions.Single(i => i.Operand is FieldReference f && f.Name == "unittyp" && i.Next.OpCode == OpCodes.Ldc_I4_S && Convert.ToInt32(i.Next.Operand) == 16);
        naming.Body.GetILProcessor().InsertAfter(comparison.Next, Instruction.Create(OpCodes.Call, Helper("SixLevelRuntime", "ArmyType")));
        hooks.Add("Regiment.UpdateUnitNameDefault");
        // Type 18 also denotes native Army Group eligibility when no Regiment
        // is supplied. Remove only that extra experience gate for actual Armies.
        var suitability = module.Types.Single(t => t.Name == "GameVars").NestedTypes.Single(t => t.Name == "Commander")
            .Methods.Single(m => m.Name == "CheckSuitableRank" && m.Parameters[0].ParameterType.FullName == "System.Int32");
        var experience = suitability.Body.Instructions.Single(i => i.OpCode == OpCodes.Ldsfld &&
            i.Operand is FieldReference f && f.Name == "minimumexpcommanderarmygroup");
        var sil = suitability.Body.GetILProcessor();
        var unitArgument = sil.Create(OpCodes.Ldarg, suitability.Parameters.Single(p => p.Name == "unit"));
        sil.InsertAfter(experience, unitArgument);
        sil.InsertAfter(unitArgument, sil.Create(OpCodes.Call, Helper("SixLevelRuntime", "CommanderExperienceMinimum")));
        hooks.Add("GameVars.Commander.CheckSuitableRank");
        // Insertions can push existing short branches past their signed-byte range.
        foreach (var t in Types(module.Types)) foreach (var m in t.Methods.Where(m => m.HasBody))
            if (m.Body.Instructions.Any(i => i.Operand is MethodReference r && r.DeclaringType.Namespace == "GTIP"))
                foreach (var instruction in m.Body.Instructions)
                    if (instruction.OpCode.OperandType == OperandType.ShortInlineBrTarget)
                        instruction.OpCode = LongBranch(instruction.OpCode);
        return hooks;
    }
    internal static void Verify(AssemblyDefinition game)
    {
        var officerList = game.MainModule.Types.Single(t => t.Name == "CommandersList").Methods.Single(m => m.Name == "UpdateList");
        if (officerList.Body.Instructions.Count(i => i.Operand is MethodReference r && r.DeclaringType.FullName == "GTIP.OfficerVisibility" && r.Name == "HideDummy") != 1 ||
            officerList.Body.Instructions.Count(i => i.OpCode == OpCodes.Ldfld && i.Operand is FieldReference f && f.Name == "usedummyphoto") != 1)
            throw new InvalidOperationException("Officer-list filter verification failed.");
        foreach (var expected in new[] { ("RecruitableOfficerState", 1), ("Suitable", 2) })
            if (officerList.Body.Instructions.Count(i => i.Operand is MethodReference r && r.DeclaringType.FullName == "GTIP.OfficerVisibility" && r.Name == expected.Item1) != expected.Item2)
                throw new InvalidOperationException("Officer state/rank hook verification failed.");
        int originals = 0;
        foreach (var type in Types(game.MainModule.Types))
        foreach (var original in type.Methods.Where(m => m.Name.StartsWith("GTIP_Original_")))
        {
            string helperName = original.Name.Substring("GTIP_Original_".Length);
            var wrappers = type.Methods.Where(m => m.HasBody && m.Body.Instructions.Any(i => i.Operand is MethodReference r &&
                r.DeclaringType.Namespace == "GTIP" && r.Name == helperName)).ToArray();
            if (wrappers.Length != 1 || wrappers[0].Parameters.Count != original.Parameters.Count)
                throw new InvalidOperationException("Serialized hook failed verification: " + original.FullName);
            originals++;
        }
        if (originals != 98) throw new InvalidOperationException("Unexpected number of preserved methods: " + originals);
        var suitability = game.MainModule.Types.Single(t => t.Name == "GameVars").NestedTypes.Single(t => t.Name == "Commander")
            .Methods.Single(m => m.Name == "CheckSuitableRank" && m.Parameters[0].ParameterType.FullName == "System.Int32");
        if (suitability.Body.Instructions.Count(i => i.Operand is MethodReference r &&
            r.DeclaringType.Namespace == "GTIP" && r.Name == "CommanderExperienceMinimum") != 1)
            throw new InvalidOperationException("Commander experience eligibility hook missing");
        foreach (var type in Types(game.MainModule.Types)) foreach (var method in type.Methods.Where(m => m.HasBody))
            foreach (var instruction in method.Body.Instructions)
                if (instruction.Operand is MethodReference r && r.Name.StartsWith("GTIP_Original_"))
                    throw new InvalidOperationException("Native call bypasses hook: " + method.FullName);
    }
    static OpCode LongBranch(OpCode code) => code.Code switch
    {
        Code.Br_S => OpCodes.Br, Code.Brfalse_S => OpCodes.Brfalse, Code.Brtrue_S => OpCodes.Brtrue,
        Code.Beq_S => OpCodes.Beq, Code.Bge_S => OpCodes.Bge, Code.Bgt_S => OpCodes.Bgt,
        Code.Ble_S => OpCodes.Ble, Code.Blt_S => OpCodes.Blt, Code.Bne_Un_S => OpCodes.Bne_Un,
        Code.Bge_Un_S => OpCodes.Bge_Un, Code.Bgt_Un_S => OpCodes.Bgt_Un,
        Code.Ble_Un_S => OpCodes.Ble_Un, Code.Blt_Un_S => OpCodes.Blt_Un, Code.Leave_S => OpCodes.Leave,
        _ => throw new InvalidOperationException("Unknown short branch: " + code)
    };
    static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types)
    { foreach (var t in types) { yield return t; foreach (var n in Types(t.NestedTypes)) yield return n; } }
}
