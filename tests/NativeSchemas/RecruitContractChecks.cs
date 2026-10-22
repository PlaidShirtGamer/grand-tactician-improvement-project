using Mono.Cecil;
using Mono.Cecil.Cil;

static class RecruitContractChecks
{
    public static void Run(string gamePath,string runtimePath)
    {
        using var native=AssemblyDefinition.ReadAssembly(gamePath);
        using var compiled=AssemblyDefinition.ReadAssembly(runtimePath);
        var mp=compiled.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign");
        MethodDefinition Native(string type,string name)=>native.MainModule.Types.Single(t=>t.Name==type).Methods.Single(m=>m.Name==name);
        MethodDefinition Bridge(string name)=>mp.Methods.Single(m=>m.Name==name);
        IEnumerable<MethodDefinition> Methods(TypeDefinition type)=>type.Methods.Concat(type.NestedTypes.SelectMany(Methods));
        bool Calls(MethodDefinition method,string name)=>method.HasBody && method.Body.Instructions.Any(i=>i.Operand is MethodReference call && call.Name==name);
        int checks=0;
        void Check(bool valid,string failure){if(!valid)throw new Exception(failure);checks++;}
        var init=Native("RecruitingList","Initialize");
        var offmap=init.Parameters.Single(p=>p.Name=="showstatesoffmap");
        Check(offmap.IsOptional && offmap.HasConstant && Equals(offmap.Constant,true),"Native single-unit recruitment must retain its supported off-map home-state option.");
        var execute=Bridge("ExecuteRecruit");
        Check(!execute.Body.Instructions.Any(i=>i.Operand is FieldReference f && f.Name=="averageposition") && Calls(execute,"HasEnoughRecruits") && Calls(execute,"IsRatingOkForRecruitment") && Calls(execute,"IsHorseArtilleryAvailable"),
            "Host recruitment must match native pool/rating/horse eligibility without inventing a home-map position gate.");
        var info=Native("CampaignMainPanel","UpdateUnitInfoPanel");
        Check(Calls(info,"HasEnoughRecruits") && Calls(info,"IsRatingOkForRecruitment"),"Native recruiting panel eligibility contract changed.");
        var panel=Native("CampaignMainPanel","SwitchCommanderPanel");
        Check(panel.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.Name=="currentcommand") && Calls(panel,"UpdateUnitInfoPanel"),
            "Native pane switch must keep its officer-command-dependent unit/commander selection behavior.");
        var restore=Bridge("RestoreRecruitPreviewPanel");
        Check(Calls(restore,"WithRecruitPreviewCommander") && Methods(mp).Where(m=>m.Name.Contains("RestoreRecruitPreviewPanel")).Any(m=>Calls(m,"SwitchCommanderPanel")),
            "A changed recruiting state must normalize the whole native unit pane in scoped preview-officer context.");
        var subpanels=Native("CampaignMainPanel","ShowSubPanels");
        Check(subpanels.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.Name=="ObjectsToDeactivateForRecruiting") &&
            subpanels.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="SetActive" && i.Previous?.OpCode==OpCodes.Ldc_I4_0),
            "Native recruiting layout no longer applies its serialized existing-unit control mask.");
        var reinforcement=Native("CampaignMainPanel","UpdateReinforcementButtons");
        var recruitingMode=reinforcement.Body.Instructions.Single(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.Name=="contentselection");
        Check(recruitingMode.Next.OpCode==OpCodes.Ldc_I4_S && Convert.ToInt32(recruitingMode.Next.Operand)==10 &&
            recruitingMode.Next.Next.OpCode.FlowControl==FlowControl.Cond_Branch && recruitingMode.Next.Next.Next.OpCode==OpCodes.Ret &&
            !reinforcement.Body.Instructions.Any(i=>i.Offset<recruitingMode.Offset && i.Operand is MethodReference m && m.Name=="SetButtonsCondition"),
            "Native reinforcement controls no longer return before button updates during recruitment; review stale activation handling.");
        var maskCleanup=restore.Body.ExceptionHandlers.Single(h=>h.HandlerType==ExceptionHandlerType.Finally);
        bool InMaskCleanup(Instruction i)=>i.Offset>=maskCleanup.HandlerStart.Offset && (maskCleanup.HandlerEnd==null || i.Offset<maskCleanup.HandlerEnd.Offset);
        Check(restore.Body.Instructions.Any(i=>InMaskCleanup(i) && i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.Name=="ObjectsToDeactivateForRecruiting") &&
            restore.Body.Instructions.Any(i=>InMaskCleanup(i) && i.Operand is MethodReference m && m.Name=="SetActive"),
            "Recruit preview redraw must reapply the native visibility mask even when the redraw throws.");
        var commit=Bridge("ReplicaRecruitCommit");
        Check(Calls(commit,"BeginRecruitRequest") && !Calls(commit,"ReplicaRecruitCancel"),"Guest draft must remain open until the matching host result.");
        Check(Calls(Bridge("Handle"),"HandleRecruitResult") && Calls(Bridge("End"),"ClearRecruitRequest"),"Recruitment receipts and session cleanup are not integrated with the live session.");
        Check(Calls(Bridge("HandleRecruitResult"),"ReplicaRecruitCancel") && Calls(Bridge("HandleRecruitResult"),"RestoreRecruitPreviewPanel") && Calls(Bridge("DrawGuestDialogs"),"get_RecruitNotice"),
            "Recruitment result must support success cleanup, rejected draft retention and visible refusal feedback.");
        Console.WriteLine($"Native recruitment contract: {checks} checks; native control masks, off-map state parity, pane context, retained drafts and matching host receipts verified.");
    }
}
