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
        var commit=Bridge("ReplicaRecruitCommit");
        Check(Calls(commit,"BeginRecruitRequest") && !Calls(commit,"ReplicaRecruitCancel"),"Guest draft must remain open until the matching host result.");
        Check(Calls(Bridge("Handle"),"HandleRecruitResult") && Calls(Bridge("End"),"ClearRecruitRequest"),"Recruitment receipts and session cleanup are not integrated with the live session.");
        Check(Calls(Bridge("HandleRecruitResult"),"ReplicaRecruitCancel") && Calls(Bridge("HandleRecruitResult"),"RestoreRecruitPreviewPanel") && Calls(Bridge("DrawGuestDialogs"),"get_RecruitNotice"),
            "Recruitment result must support success cleanup, rejected draft retention and visible pending/refusal feedback.");
        Console.WriteLine($"Native recruitment contract: {checks} checks; off-map state parity, native pane context, retained drafts and matching host receipts verified.");
    }
}
