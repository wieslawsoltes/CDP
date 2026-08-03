using Chrome.DevTools.Protocol.Inspector;

namespace Chrome.DevTools.Protocol.Inspector.Tests;

public sealed class V8SourceMapTests
{
    [Fact]
    public void ParsesRevisionThreeMappingsAndEmbeddedSources()
    {
        const string json = """
            {
              "version": 3,
              "file": "bundle.js",
              "sources": ["webpack:///src/App.tsx"],
              "sourcesContent": ["const App = () => <main>Hello</main>;"],
              "names": ["App"],
              "mappings": "AAAAA;AACA"
            }
            """;

        var map = V8SourceMap.Parse(json);

        Assert.Equal("webpack:///src/App.tsx", Assert.Single(map.Sources));
        Assert.Contains("<main>Hello</main>", Assert.Single(map.SourcesContent));
        Assert.Equal(new V8SourceMapEntry(0, 0, 0, 0, 0, 0), map.FindOriginalLocation(0, 0));
        Assert.Equal(new V8SourceMapEntry(1, 0, 0, 1, 0, null), map.FindGeneratedLocation(0, 1));
    }

    [Fact]
    public void UsesClosestMappingAtOrBeforeGeneratedColumn()
    {
        const string json = """{"version":3,"sources":["source.ts"],"names":[],"mappings":"AAAA,KAAK"}""";
        var map = V8SourceMap.Parse(json);

        var location = map.FindOriginalLocation(0, 7);

        Assert.NotNull(location);
        Assert.Equal(5, location.GeneratedColumn);
        Assert.Equal(5, location.OriginalColumn);
    }
}
