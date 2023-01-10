// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using System.Text.Json.Nodes;

var httpClient = new HttpClient();
var response = await httpClient.GetAsync("https://garlandtools.org/db/doc/core/en/3/data.json");
var data = JsonSerializer.Deserialize<JsonObject>(await response.Content.ReadAsStringAsync());
response.Dispose();
var patches = data!["patch"]!["partialIndex"]!.AsObject().Select(kv => kv.Key).ToArray();

var itemSet = new HashSet<uint>();
var clauses = new Dictionary<string, List<(uint start, uint end)>>();

foreach (var patch in patches) {
    response = await httpClient.GetAsync($"https://garlandtools.org/db/doc/patch/en/2/{patch}.json");
    data = JsonSerializer.Deserialize<JsonObject>(await response.Content.ReadAsStringAsync());
    response.Dispose();
    
    foreach (var (patchName, content) in data!["patch"]!["patches"]!.AsObject()) {
        var patchClauses = new List<(uint start, uint end)>();
        (uint start, uint end) lastItem = (uint.MaxValue, uint.MaxValue);

        void Flush() {
            if (lastItem.start != uint.MaxValue)
                patchClauses.Add(lastItem);
        }

        var itemIds = content!["item"]!.AsArray().Select(i => i!["i"]!.GetValue<uint>()).ToList();
        itemIds.Sort();
        foreach (var id in itemIds) {
            if (itemSet.Add(id)) {
                if (lastItem is var (_, e) && e == id - 1) {
                    lastItem.end = id;
                } else {
                    Flush();
                    lastItem = (id, id);
                }
            }
        }

        Flush();
        if (lastItem.start != uint.MaxValue)
            clauses.Add(patchName, patchClauses);
    }
}

foreach (var (_, cls) in clauses) {
    for (var i = 0; i < cls.Count - 1; i++) {
        var rangeStart = cls[i].end;
        var rangeEnd = cls[i + 1].start;
        if (!itemSet.Any(j => j > rangeStart && j < rangeEnd)) {
            cls[i] = (cls[i].start, cls[i + 1].end);
            cls.RemoveAt(i + 1);
            i--;
        }
    }
}

await using var file = File.CreateText("../../../../../WhichPatchWasThat/ItemPatchMapper.cs");

await file.WriteLineAsync("// File created by WhichPatchWasThat-Update");
await file.WriteLineAsync("// Do not modify manually");
await file.WriteLineAsync();
await file.WriteLineAsync("namespace WhichPatchWasThat;");
await file.WriteLineAsync();
await file.WriteLineAsync("public static class ItemPatchMapper {");
await file.WriteLineAsync("    public static string? GetPatch(ulong id) {");
await file.WriteLineAsync("        switch (id) {");

foreach (var (patch, cls) in clauses) {
    foreach (var (start, end) in cls) {
        if (start == end)
            await file.WriteLineAsync($"            case {start}:");
        else
            await file.WriteLineAsync($"            case >= {start} and <= {end}:");
    }
    await file.WriteLineAsync($"                return \"{patch}\";");
}

await file.WriteLineAsync("        }");
await file.WriteLineAsync();
await file.WriteLineAsync("        return null;");
await file.WriteLineAsync("    }");
await file.WriteLineAsync("}");
