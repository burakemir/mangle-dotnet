using Mangle;

Console.WriteLine($"mangle-ffi native version: {MangleEngine.NativeVersion()}");
Console.WriteLine();

using var engine = new MangleEngine(enableProvenance: false);

engine.LoadRules("""
    edge(1, 2).
    edge(2, 3).
    edge(3, 4).
    reachable(X, Y) :- edge(X, Y).
    reachable(X, Z) :- edge(X, Y), reachable(Y, Z).
""");

Console.WriteLine("reachable(1, Y):");
foreach (var row in engine.Query("reachable(1, Y)"))
{
    // row[0] is the bound "1", row[1] is the Y binding.
    Console.WriteLine($"  1 -> {row[1].AsInt64()}");
}
Console.WriteLine();

Console.WriteLine("schema snapshot:");
Console.WriteLine($"  {engine.SchemaSnapshotJson()}");
Console.WriteLine();

Console.WriteLine("facts snapshot (limit 3):");
Console.WriteLine($"  {engine.FactsSnapshotJson(perRelationLimit: 3)}");
