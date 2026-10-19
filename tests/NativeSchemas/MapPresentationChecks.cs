using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class MapPresentationChecks
{
    public static void Run(string gamePath,string runtimePath,Array schemas)
    {
        using var game=AssemblyDefinition.ReadAssembly(gamePath);
        using var runtime=AssemblyDefinition.ReadAssembly(runtimePath);
        var regiment=game.MainModule.Types.Single(t=>t.Name=="Regiment");
        var campaign=runtime.MainModule.Types.Single(t=>t.Name=="MultiplayerCampaign");
        MethodDefinition Method(TypeDefinition owner,string name)=>owner.Methods.Single(m=>m.Name==name);
        string[] ReadFields(MethodDefinition m)=>m.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ldfld && i.Operand is FieldReference f && f.DeclaringType.Name=="Regiment").Select(i=>((FieldReference)i.Operand).Name).Distinct().ToArray();
        bool Calls(MethodDefinition m,string owner,string name)=>m.Body.Instructions.Any(i=>i.Operand is MethodReference call && call.DeclaringType.Name==owner && call.Name==name);
        var nativeMarkers=Method(regiment,"ShowWaypoints");
        if(!new[]{"lastwaypointposition","lastwaypointrotation","regimentpaths","selected","blockobjects"}.All(ReadFields(nativeMarkers).Contains))throw new Exception("Native destination-marker inputs changed.");
        var refreshed=Method(campaign,"RefreshReplicaMapDestinations");
        if(!Calls(refreshed,"Regiment","ShowWaypoints") || !Calls(Method(campaign,"ReplicaRegiment"),"MultiplayerCampaign","RefreshReplicaMapDestinations") || !Calls(Method(campaign,"ApplyMapPaths"),"MultiplayerCampaign","RefreshReplicaMapDestinations"))throw new Exception("Guest destination renderer is not refreshed after route/selection changes.");
        var pathFields=((FieldInfo[])schemas.GetValue(28)!.GetType().GetField("Fields")!.GetValue(schemas.GetValue(28))!).Select(f=>f.Name).ToHashSet();
        if(!pathFields.Contains("Destinations") || !pathFields.Contains("DestinationYaw"))throw new Exception("Host destination marker fields are missing from the native route schema.");
        var apply=Method(campaign,"ApplyMapPaths");
        var writes=apply.Body.Instructions.Where(i=>i.OpCode==OpCodes.Stfld && i.Operand is FieldReference f && f.DeclaringType.Name=="Regiment").Select(i=>((FieldReference)i.Operand).Name).ToHashSet();
        if(!new[]{"lastwaypointposition","lastwaypointrotation"}.All(writes.Contains))throw new Exception("Host destination positions/facings are not applied to native renderer inputs.");
        foreach(var field in new[]{("regroupingareas","List`1"),("supplydepotsconnected","List`1"),("waypoint","GameObject[]")})
            if(regiment.Fields.Single(f=>f.Name==field.Item1).FieldType.Name!=field.Item2)throw new Exception("Native map presentation bridge changed: "+field.Item1);
        var ranges=Method(regiment,"UpdateRegroupingRadius");
        if(!ReadFields(ranges).Contains("regroupingareas"))throw new Exception("Native supply circles no longer use host regrouping-area references.");
        var supply=Method(campaign,"ApplyMapSupply");
        if(!Calls(supply,"Regiment","UpdateRegroupingRadius"))throw new Exception("Applying host supply topology does not refresh native selection circles.");
        foreach(var display in new[]{nativeMarkers,ranges,supply,refreshed})
            if(display.Body.Instructions.Any(i=>i.Operand is MethodReference m && new[]{"RegimentSetPath","CalculateSupplyLine","UpdateRegroupingAreas","UpdateIIPSupplyLink","UpdateInitialSupplyDepots","UpdateReadiness"}.Contains(m.Name)))throw new Exception("Guest map presentation invokes simulation: "+display.Name);
        var supplyFields=((FieldInfo[])schemas.GetValue(42)!.GetType().GetField("Fields")!.GetValue(schemas.GetValue(42))!).Select(f=>f.Name).ToHashSet();
        if(!supplyFields.SetEquals(new[]{"Regrouping","Depots"}))throw new Exception("Host supply display topology schema changed.");
        Console.WriteLine("Native map presentation: host destination positions/facings and supply-location references feed native renderers; guest refreshes do not invoke pathfinding, supply or readiness simulation.");
    }
}
