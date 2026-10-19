using Mono.Cecil;
using Mono.Cecil.Cil;

static class TransientChecks
{
    public static void Run(string gamePath,string runtimePath)
    {
        using var game=AssemblyDefinition.ReadAssembly(gamePath);
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        var campaign=runtime.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign");
        IEnumerable<MethodDefinition> Methods(TypeDefinition type)=>type.Methods.Concat(type.NestedTypes.SelectMany(Methods));
        MethodDefinition Native(string type,string method)=>game.MainModule.Types.Single(t=>t.Name==type).Methods.Single(m=>m.Name==method);
        MethodDefinition Bridge(string method)=>campaign.Methods.Single(m=>m.Name==method);
        bool Calls(MethodDefinition method,string owner,string name)=>method.HasBody && method.Body.Instructions.Any(i=>i.Operand is MethodReference call && call.DeclaringType.Name==owner && call.Name==name);
        string[] ReadFields(MethodDefinition method,string owner)=>method.Body.Instructions.Where(i=>i.Operand is FieldReference field && field.DeclaringType.Name==owner && (i.OpCode==OpCodes.Ldfld || i.OpCode==OpCodes.Ldsfld)).Select(i=>((FieldReference)i.Operand).Name).ToArray();
        int checks=0;
        void Check(bool condition,string failure){if(!condition)throw new Exception(failure);checks++;}
        // Enumerate actual producers, rather than asserting a short list of popup
        // strings. A single accepted-creation hook covers this entire native path.
        var producers=game.MainModule.Types.SelectMany(Methods).Where(m=>Calls(m,"RollingText","Add")).ToArray();
        var owners=producers.Select(m=>m.DeclaringType.Name).ToHashSet();
        Check(new[]{"Autocalc","BattleUnits","CBuilding","DLC_WL","Regiment"}.All(owners.Contains),"Native transient producer coverage changed; review the shared creation boundary");
        Check(producers.Sum(m=>m.Body.Instructions.Count(i=>i.Operand is MethodReference call && call.DeclaringType.Name=="RollingText" && call.Name=="Add"))>=25,"Native rolling-notice call-site coverage unexpectedly contracted");
        var add=Native("RollingText","Add");
        Check(Calls(add,"RollingText","AddNew") && Calls(add,"RollingTextDatabase","Exists"),"The accepted native creation path must retain its own rate/duplicate suppression");
        var send=Bridge("ReplicaMapNotice");
        Check(send.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr && (string)i.Operand=="GTIP_Original_ReplicaMapNotice") && Calls(send,"MultiplayerCampaign","Send"),"Successful native popup creation is not connected to immediate delivery");
        Check(!ReadFields(send,"MultiplayerCampaign").Contains("remoteIntent") && !ReadFields(send,"RollingText").Contains("type_buildingconstructed"),"Fast transient delivery is still restricted to only construction or remote commands");
        Check(Calls(send,"MpStateCodec","Encode") && !Calls(send,"Autocalc","Update"),"Popup delivery bypasses the validated presentation codec or executes combat");
        var apply=Bridge("ApplyMapNotices");
        Check(Calls(apply,"MpMapNoticeReceipts","Take"),"Fast and state-delivered rolling notices do not share once-only receipts");
        Check(new[]{"showrollingtext","papermapvisible"}.All(ReadFields(apply,"GameVars").Contains),"Guest popup presentation ignores local visibility settings");
        Check(Calls(apply,"MultiplayerCampaign","PruneMapNotices") && Calls(Bridge("RemoveMapNotice"),"RollingText","DeleteEntry") && Calls(Bridge("RemoveMapNotice"),"Object","Destroy"),"Expired fast-only notice objects lack bounded cleanup");
        Check(Calls(Bridge("ClearMapNotices"),"MpMapNoticeReceipts","Clear"),"Session cleanup leaves the prior campaign's notice identities behind");

        var notification=Bridge("GuestDispatchNotification");
        var notificationFields=Methods(campaign).Where(m=>m.HasBody && m.Name.Contains("GuestDispatchNotification")).SelectMany(m=>ReadFields(m,"Panel")).ToArray();
        Check(ReadFields(notification,"PanelHandler").Contains("symbol") && !ReadFields(notification,"PanelHandler").Contains("multiplepanel"),"Transient dispatch picking searches the wrong native UI collection");
        Check(Calls(notification,"MpDispatchSymbols","Matches") && new[]{"uniqueid","referencego","secondaryreference"}.All(notificationFields.Contains),"Dispatch picking does not validate complete native symbol identity");
        Check(Calls(notification,"Messages","InteractScreenMessage") && !Calls(notification,"Messages","OpenMessage"),"Notification clicks bypass the native screen-message lifecycle");
        Check(Calls(Bridge("ReplicaBattleUi"),"MultiplayerCampaign","GuestDispatchNotification"),"The replica input dispatcher does not route transient notification clicks");
        Check(Calls(Native("Messages","ShowMessageOnScreen"),"PanelHandler","ShowSymbol") &&
            Native("PanelHandler","ShowSymbol").Body.Instructions.Any(i=>i.Operand is FieldReference field && field.DeclaringType.Name=="PanelHandler" && field.Name=="symbol"),"Native dispatch notifications no longer originate in the symbol collection");
        Check(Calls(Native("Messages","InteractScreenMessage"),"Messages","OpenMessage") && Calls(Native("Messages","InteractScreenMessage"),"Messages","RemoveScreenMessage"),"Native dispatch interaction ABI changed");
        Check(!notification.Body.Instructions.Any(i=>i.Operand is MethodReference call && new[]{"Submit","Execute","ExecuteNative","Send","SetGameSpeed"}.Contains(call.Name)),"Reading a guest notification can invoke authoritative gameplay");
        Console.WriteLine($"Native transient presentation: {checks} checks; {producers.Length} native producer methods covered by the shared accepted-notice hook, native dispatch symbol ownership and local click lifecycle verified.");
    }
}
