if ((args.Length != 3 && args.Length != 4) || args[0] != "stage")
{
    Console.Error.WriteLine("Usage: Patcher stage <game install> <output directory> [original assembly backup]");
    return 2;
}
try
{
    PatchEngine.Stage(args[1], args[2], Path.Combine(AppContext.BaseDirectory, "GTIP.Runtime.dll"), args.Length == 4 ? args[3] : null);
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
