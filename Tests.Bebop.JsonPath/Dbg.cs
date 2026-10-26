using Bebop.JsonPath;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TestsJsonPath;

public class Dbg
{
    [Fact(Skip = "Only for debugging")]
    public void Test()
    {
        var jsonStr = """
                   {
                     "name": "index selector, min exact index",
                     "selector": "$[-9007199254740991]",
                     "document": [
                       "first",
                       "second"
                     ],
                     "result": [],
                     "result_paths": [],
                     "tags": [
                       "boundary",
                       "index"
                     ]
                   }
                   """;
        var json = JsonNode.Parse(jsonStr);
        var path = JsonPath.Parse(json["selector"]!.GetValue<string>()!);

        using var doc = JsonDocument.Parse(json["document"]!.ToJsonString());
        var result = path.Evaluate(doc.RootElement);

        Assert.Equal(0, result.Count());
    }
}
