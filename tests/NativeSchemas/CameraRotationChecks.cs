using Mono.Cecil;
using Mono.Cecil.Cil;

static class CameraRotationChecks
{
    public static void Run(string gamePath,string runtimePath)
    {
        using var game=AssemblyDefinition.ReadAssembly(gamePath);
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        var camera=game.MainModule.Types.Single(t=>t.Name=="Camera_Battlefield");
        var consume=camera.Methods.Single(m=>m.Name=="update_scrolls");
        var entry=camera.Methods.Single(m=>m.Name=="Update");
        if(!entry.Body.Instructions.Any(i=>i.Operand is MethodReference m && m.Name=="update_scrolls") ||
            !consume.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.Name=="mrot"))throw new Exception("Native camera rotation consumption boundary changed.");
        foreach(string field in new[]{"mrot","scrolltarget_rotation"})if(camera.Fields.Single(f=>f.Name==field).FieldType.FullName!="System.Single")throw new Exception("Native camera rotation cache layout changed: "+field);
        if(camera.Fields.Single(f=>f.Name=="IsCampaignCam").FieldType.FullName!="System.Boolean")throw new Exception("Native campaign-camera identity changed.");
        var wrapper=runtime.MainModule.Types.Single(t=>t.Name=="CampaignCameraRuntime").Methods.Single(m=>m.Name=="LockedCameraScrolls");
        var strings=wrapper.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldstr).Select(i=>(string)i.Operand).ToHashSet();
        if(!new[]{"mrot","scrolltarget_rotation","GTIP_Original_LockedCameraScrolls"}.All(strings.Contains) ||
            !wrapper.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldsfld && i.Operand is FieldReference f && f.Name=="preventcamerarotation"))throw new Exception("Campaign rotation wrapper does not enforce the native option at the scroll boundary.");
        if(wrapper.Body.Instructions.Any(i=>i.OpCode==OpCodes.Ldstr && new[]{"mspeedx","mspeedy","mspeedz","wheely"}.Contains((string)i.Operand)))throw new Exception("Rotation lock must not clear native pan or zoom inputs.");
        Console.WriteLine("Native camera lock: native campaign preference, angular momentum and pending target boundary verified; pan/zoom and native pitch computation remain in the preserved method.");
    }
}
