using Bebop.JsonPath;
using System.Text.Json;

namespace TestsJsonPath;

public class Dbg
{
    [Fact]
    public void Test()
    {
        var path = JsonPath.Parse("$[0,2]");
        var json = @"[
            0,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            9
          ]";

        using var doc = JsonDocument.Parse(json);
        var result = path.Evaluate(doc.RootElement);

        Assert.Equal(2, result.Length);
    }
}
