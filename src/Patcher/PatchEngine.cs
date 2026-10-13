using System.Security.Cryptography;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class PatchEngine
{
    public const string SupportedHash = "AE15E3E479F4D79B70E40CED42EB93A953569B8185E075B3873C7410462AE8C4";
    public static void Stage(string gameInstall, string outputDirectory, string runtimeAssembly, string? originalAssembly = null)
    {
    string install = Path.GetFullPath(gameInstall);
    string output = Path.GetFullPath(outputDirectory);
    string managed = Path.Combine(install, "The Civil War (1861-1865)_Data", "Managed");
    string input = originalAssembly != null ? Path.GetFullPath(originalAssembly) : Path.Combine(managed, "Assembly-CSharp.dll");
    string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input)));
    if (hash != SupportedHash) throw new InvalidOperationException("Unsupported game assembly SHA-256: " + hash);
    if (output.Equals(managed, StringComparison.OrdinalIgnoreCase) ||
        output.StartsWith(managed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Output must be outside the game's Managed directory.");
    string runtime = Path.GetFullPath(runtimeAssembly);
    if (!File.Exists(runtime)) throw new FileNotFoundException("Build Runtime and copy GTIP.Runtime.dll beside Patcher first.", runtime);
    using var resolver = new DefaultAssemblyResolver();
    resolver.AddSearchDirectory(Path.GetDirectoryName(runtime));
    resolver.AddSearchDirectory(managed);
    using var game = AssemblyDefinition.ReadAssembly(input, new ReaderParameters { AssemblyResolver = resolver });
    using var helper = AssemblyDefinition.ReadAssembly(runtime, new ReaderParameters { AssemblyResolver = resolver });
    var type = game.MainModule.Types.Single(t => t.Name == "BattleUnits");
    var original = type.Methods.Single(m => m.Name == "PerformActionForGroupForAllUnitsOnLevel" && m.Parameters[0].ParameterType.Name == "Regiment");
    if (original.Parameters.Count != 7 || !original.HasBody || original.Body.ExceptionHandlers.Count != 0)
        throw new InvalidOperationException("Unexpected dispatch signature or body.");
    var helperMethod = helper.MainModule.Types.Single(t => t.Name == "HierarchyCommands").Methods.Single(m => m.Name == "Dispatch");
    if (helperMethod.Parameters.Count != 8) throw new InvalidOperationException("Unexpected helper signature.");
    string methodName = original.Name;
    original.Name = "GTIP_Original_Dispatch";
    var wrapper = new MethodDefinition(methodName, original.Attributes, original.ReturnType);
    foreach (var parameter in original.Parameters)
    {
        var copy = new ParameterDefinition(parameter.Name, parameter.Attributes, parameter.ParameterType);
        if (parameter.HasConstant) copy.Constant = parameter.Constant;
        wrapper.Parameters.Add(copy);
    }
    type.Methods.Add(wrapper);
    var il = wrapper.Body.GetILProcessor();
    il.Append(il.Create(OpCodes.Ldarg_0));
    foreach (var parameter in wrapper.Parameters) il.Append(il.Create(OpCodes.Ldarg, parameter));
    il.Append(il.Create(OpCodes.Call, game.MainModule.ImportReference(helperMethod)));
    il.Append(il.Create(OpCodes.Ret));
    int rerouted = 0;
    foreach (var t in AllTypes(game.MainModule.Types))
    foreach (var m in t.Methods.Where(m => m.HasBody))
    foreach (var instruction in m.Body.Instructions)
        if (instruction.Operand is MethodReference reference && reference.Resolve() == original)
        {
            instruction.Operand = wrapper;
            rerouted++;
        }
    if (rerouted == 0) throw new InvalidOperationException("No dispatch call sites found.");
    var menuActivation = game.MainModule.Types.Single(t => t.Name == "MainMenuControls")
        .Methods.Single(m => m.Name == "Activate");
    if (!menuActivation.IsStatic || menuActivation.Parameters.Count != 1 ||
        menuActivation.Parameters[0].ParameterType.MetadataType != MetadataType.Boolean ||
        menuActivation.ReturnType.MetadataType != MetadataType.Void || !menuActivation.HasBody ||
        menuActivation.Body.ExceptionHandlers.Count != 0)
        throw new InvalidOperationException("Unexpected main-menu activation signature or body.");
    var statusHook = helper.MainModule.Types.Single(t => t.Name == "MenuStatus")
        .Methods.Single(m => m.Name == "SetVisible");
    var menuIl = menuActivation.Body.GetILProcessor();
    var returns = menuActivation.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray();
    if (returns.Length != 1) throw new InvalidOperationException("Unexpected main-menu return count.");
    // Keep the original return instruction as the branch target, so every return
    // path passes through the hook, including any branches to the former ret.
    var exit = returns[0];
    exit.OpCode = OpCodes.Ldarg_0;
    var callStatus = menuIl.Create(OpCodes.Call, game.MainModule.ImportReference(statusHook));
    menuIl.InsertAfter(exit, callStatus);
    menuIl.InsertAfter(callStatus, menuIl.Create(OpCodes.Ret));
    var sixLevelHooks = SixLevelPatches.Apply(game, helper);
    Directory.CreateDirectory(output);
    string destination = Path.Combine(output, "Assembly-CSharp.dll");
    if (File.Exists(destination)) throw new IOException("Output already exists; choose a new output directory.");
    game.Write(destination);
    File.Copy(runtime, Path.Combine(output, "GTIP.Runtime.dll"), false);
    // Re-read the artifact to verify serialization and hook wiring.
    using var staged = AssemblyDefinition.ReadAssembly(destination, new ReaderParameters { AssemblyResolver = resolver });
    SixLevelPatches.Verify(staged);
    var stagedType = staged.MainModule.Types.Single(t => t.Name == "BattleUnits");
    if (stagedType.Methods.Count(m => m.Name == "GTIP_Original_Dispatch") != 1 ||
        !stagedType.Methods.Single(m => m.Name == methodName && m.Parameters[0].ParameterType.Name == "Regiment")
            .Body.Instructions.Any(i => i.Operand is MethodReference r && r.DeclaringType.FullName == "GTIP.HierarchyCommands"))
        throw new InvalidOperationException("Staged wrapper verification failed.");
    var stagedMenu = staged.MainModule.Types.Single(t => t.Name == "MainMenuControls")
        .Methods.Single(m => m.Name == "Activate");
    if (stagedMenu.Body.Instructions.Count(i => i.OpCode == OpCodes.Call &&
        i.Operand is MethodReference r && r.DeclaringType.FullName == "GTIP.MenuStatus" &&
        r.Name == "SetVisible") != 1)
        throw new InvalidOperationException("Staged menu indicator verification failed.");
    File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(new
    {
        status = "experimental-six-level", modVersion = "0.3.5", sixLevelHooks, menuIndicator = true, originalSha256 = hash,
        patchedSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(destination))),
        reroutedCallSites = rerouted, runtimeSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(runtime))),
        sixTierGameplayComplete = false
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Staged and structurally verified experimental six-level patch: {destination}\nRerouted {rerouted} dispatch call sites and verified {sixLevelHooks.Count} additional hooks/changes. In-game validation remains required.");
    }
static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
{
    foreach (var type in types)
    {
        yield return type;
        foreach (var nested in AllTypes(type.NestedTypes)) yield return nested;
    }
}

}
