using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GTIP.Installer;

public static class RuntimeCompiler
{
    public static Assembly Resources => typeof(RuntimeCompiler).Assembly;
    public static void VerifyCompiler()
    {
        _ = CSharpSyntaxTree.ParseText("class SmokeTest {}");
        _ = typeof(Mono.Cecil.AssemblyDefinition).Assembly.FullName;
    }
    public static void Compile(string managed, string original, string output)
    {
        var trees = Resources.GetManifestResourceNames().Where(n => n.StartsWith("Runtime.")).OrderBy(n => n)
            .Select(n => { using var reader = new StreamReader(Resources.GetManifestResourceStream(n)!);
                return CSharpSyntaxTree.ParseText(reader.ReadToEnd(), new CSharpParseOptions(LanguageVersion.Latest), n); }).ToList();
        trees.Add(CSharpSyntaxTree.ParseText("[assembly: System.Reflection.AssemblyVersion(GTIP.MpBuild.Version)]"));
        var refs = new List<MetadataReference>();
        foreach (var path in Directory.GetFiles(managed, "*.dll").OrderBy(p => p))
        {
            if (Path.GetFileName(path).Equals("GTIP.Runtime.dll", StringComparison.OrdinalIgnoreCase)) continue;
            string reference = Path.GetFileName(path).Equals("Assembly-CSharp.dll", StringComparison.OrdinalIgnoreCase) ? original : path;
            try { AssemblyName.GetAssemblyName(reference); refs.Add(MetadataReference.CreateFromFile(reference)); }
            catch (BadImageFormatException) { /* Native Unity dependencies are not compiler references. */ }
        }
        var compilation = CSharpCompilation.Create("GTIP.Runtime", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, deterministic: true));
        var result = compilation.Emit(output);
        if (!result.Success) throw new InvalidOperationException("Cannot compile GTIP for this game installation:\n" +
            string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
    }
}
