// See https://aka.ms/new-console-template for more information

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CsvHelper;
using CsvHelper.Configuration;

var httpClient = new HttpClient();
using var allPatchesResponse = await httpClient.GetAsync("https://garlandtools.org/db/doc/core/en/3/data.json");
var data = JsonSerializer.Deserialize<JsonObject>(await allPatchesResponse.Content.ReadAsStringAsync());
var patches = data!["patch"]!["partialIndex"]!.AsObject().Select(kv => kv.Key).ToArray();

var itemSet = new HashSet<uint>();
var clauses = new Dictionary<string, List<(uint start, uint end)>>();
var questSet = new HashSet<uint>();
var questClauses = new Dictionary<string, List<(uint start, uint end)>>();

foreach (var patch in patches) {
    using var patchResponse = await httpClient.GetAsync($"https://garlandtools.org/db/doc/patch/en/2/{patch}.json");
    data = JsonSerializer.Deserialize<JsonObject>(await patchResponse.Content.ReadAsStringAsync());

    foreach (var (patchName, content) in data!["patch"]!["patches"]!.AsObject()) {
        void Aggregate(string type, IDictionary<string, List<(uint start, uint end)>> dictionary, ISet<uint> set) {
            var patchClauses = new List<(uint start, uint end)>();
            (uint start, uint end) last = (uint.MaxValue, uint.MaxValue);

            void Flush() {
                if (last.start != uint.MaxValue)
                    patchClauses.Add(last);
            }

            var ids = content![type]?.AsArray().Select(i => i!["i"]!.GetValue<uint>()).ToList();
            if (ids == null) return;
            ids.Sort();
            foreach (var id in ids) {
                if (set.Add(id)) {
                    if (last is var (_, e) && e == id - 1) {
                        last.end = id;
                    } else {
                        Flush();
                        last = (id, id);
                    }
                }
            }

            Flush();
            if (last.start != uint.MaxValue)
                dictionary.Add(patchName, patchClauses);
        }

        Aggregate("item", clauses, itemSet);
        Aggregate("quest", questClauses, questSet);
    }
}

void Simplify(Dictionary<string, List<(uint start, uint end)>> dictionary, IReadOnlyCollection<uint> set) {
    foreach (var (_, cls) in dictionary) {
        for (var i = 0; i < cls.Count - 1; i++) {
            var rangeStart = cls[i].end;
            var rangeEnd = cls[i + 1].start;
            if (!set.Any(j => j > rangeStart && j < rangeEnd)) {
                cls[i] = (cls[i].start, cls[i + 1].end);
                cls.RemoveAt(i + 1);
                i--;
            }
        }
    }
}
Simplify(clauses, itemSet);
Simplify(questClauses, questSet);

{
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
}
{
    await using var file = File.CreateText("../../../../../WhichPatchWasThat/QuestPatchMapper.cs");

    await file.WriteLineAsync("// File created by WhichPatchWasThat-Update");
    await file.WriteLineAsync("// Do not modify manually");
    await file.WriteLineAsync();
    await file.WriteLineAsync("namespace WhichPatchWasThat;");
    await file.WriteLineAsync();
    await file.WriteLineAsync("public static class QuestPatchMapper {");
    await file.WriteLineAsync("    public static string? GetPatch(uint id) {");
    await file.WriteLineAsync("        switch (id) {");

    foreach (var (patch, cls) in questClauses) {
        foreach (var (start, end) in cls) {
            if (start == end)
                await file.WriteLineAsync($"            case {start ^ (1 << 16)}:");
            else
                await file.WriteLineAsync($"            case >= {start ^ (1 << 16)} and <= {end ^ (1 << 16)}:");
        }

        await file.WriteLineAsync($"                return \"{patch}\";");
    }

    await file.WriteLineAsync("        }");
    await file.WriteLineAsync();
    await file.WriteLineAsync("        return null;");
    await file.WriteLineAsync("    }");
    await file.WriteLineAsync("}");
}

using var itemResponse = await httpClient.GetAsync("https://github.com/xivapi/ffxiv-datamining/raw/master/csv/en/Item.csv");
using var itemCsv = new CsvReader(new StreamReader(await itemResponse.Content.ReadAsStreamAsync()), CultureInfo.InvariantCulture);

var itemActionMap = new Dictionary<string, string>();
var glassesAdditionalDataMap = new Dictionary<string, string>();

await foreach (IDictionary<string, object> row in itemCsv.GetRecordsAsync<dynamic>()) {
    if (row["ItemAction"] is not "0") {
        itemActionMap[(string)row["ItemAction"]] = (string)row["#"];
    }
    if (row["ItemAction"] is "2251" && row["AdditionalData"] is not "0") {
        glassesAdditionalDataMap[(string)row["AdditionalData"]] = (string)row["#"];
    }
}

using var itemActionResponse = await httpClient.GetAsync("https://github.com/xivapi/ffxiv-datamining/raw/master/csv/en/ItemAction.csv");
using var itemActionCsv = new CsvReader(new StreamReader(await itemActionResponse.Content.ReadAsStreamAsync()), new CsvConfiguration(CultureInfo.InvariantCulture));

var mounts = new SortedDictionary<ulong, string>();
var minions = new SortedDictionary<ulong, string>();
var fashionAccs = new SortedDictionary<ulong, string>();

await foreach (IDictionary<string, object> row in itemActionCsv.GetRecordsAsync<dynamic>()) {
    if (row["Action"] is "20086" && itemActionMap.TryGetValue((string)row["#"], out var itemId)) {
        fashionAccs[Convert.ToUInt64(row["Data[0]"])] = itemId;
    }

    if (row["Action"] is "853" && itemActionMap.TryGetValue((string)row["#"], out itemId)) {
        minions[Convert.ToUInt64(row["Data[0]"])] = itemId;
    }

    if (row["Action"] is "1322" && itemActionMap.TryGetValue((string)row["#"], out itemId)) {
        mounts[Convert.ToUInt64(row["Data[0]"])] = itemId;
    }
}

using var glassesStyleResponse = await httpClient.GetAsync("https://github.com/xivapi/ffxiv-datamining/raw/master/csv/en/GlassesStyle.csv");
using var glassesStyleCsv = new CsvReader(new StreamReader(await glassesStyleResponse.Content.ReadAsStreamAsync()), new CsvConfiguration(CultureInfo.InvariantCulture));

var glasses = new SortedDictionary<ulong, string>();

await foreach (IDictionary<string, object> row in glassesStyleCsv.GetRecordsAsync<dynamic>()) {
    for (var i = 0; i < 12; i++) {
        if (row[$"Glasses[{i}]"] is not "0" && glassesAdditionalDataMap.TryGetValue((string)row["Glasses[0]"], out var itemId)) {
            glasses[Convert.ToUInt64(row[$"Glasses[{i}]"])] = itemId;
        }
    }
}

{
    await using var file = File.CreateText("../../../../../WhichPatchWasThat/ActionToItemMapper.cs");
    await file.WriteLineAsync("// File created by WhichPatchWasThat-Update");
    await file.WriteLineAsync("// Do not modify manually");
    await file.WriteLineAsync();
    await file.WriteLineAsync("namespace WhichPatchWasThat;");
    await file.WriteLineAsync();
    await file.WriteLineAsync("public static class ActionToItemMapper {");
    foreach (var (name, items) in new[] { ("Minion", minions), ("Mount", mounts), ("FashionAccessory", fashionAccs), ("Glasses", glasses) }) {
        await file.WriteLineAsync($"    public static ulong? GetItemOf{name}(ulong id) {{");
        await file.WriteLineAsync("        return id switch {");

        foreach (var (id, item) in items) {
            await file.WriteLineAsync($"            {id} => {item},");
        }

        await file.WriteLineAsync("            _ => null");
        await file.WriteLineAsync("        };");
        await file.WriteLineAsync("    }");
        await file.WriteLineAsync();
    }

    await file.WriteLineAsync("}");
}
