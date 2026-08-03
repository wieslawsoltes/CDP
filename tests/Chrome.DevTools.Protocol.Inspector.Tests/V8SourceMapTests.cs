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

    [Fact]
    public void ParsesIndexedMapsAndAppliesSectionOffsets()
    {
        const string json = """
            {
              "version": 3,
              "sections": [
                {
                  "offset": { "line": 0, "column": 0 },
                  "map": { "version": 3, "sources": ["startup.ts"], "names": [], "mappings": "AAAA" }
                },
                {
                  "offset": { "line": 2, "column": 5 },
                  "map": {
                    "version": 3,
                    "sourceRoot": "../src",
                    "sources": ["App.tsx"],
                    "sourcesContent": ["export const App = () => <main />;"],
                    "names": [],
                    "mappings": "AAAA;AACA"
                  }
                }
              ]
            }
            """;

        var map = V8SourceMap.Parse(json, new Uri("https://example.test/maps/index.js.map"));

        Assert.True(map.IsIndexed);
        Assert.Equal(2, map.Sources.Count);
        Assert.Equal("https://example.test/src/App.tsx", map.ResolveSourceUrl(1));
        Assert.Equal(new V8SourceMapEntry(2, 5, 1, 0, 0, null), map.FindOriginalLocation(2, 5));
        Assert.Equal(new V8SourceMapEntry(3, 0, 1, 1, 0, null), map.FindGeneratedLocation(1, 1));
    }

    [Fact]
    public async Task LoadsExternalIndexedSectionsRelativeToTheirOwnMapUrl()
    {
        const string json = """
            {
              "version": 3,
              "sections": [
                { "offset": { "line": 1, "column": 2 }, "url": "parts/feature.js.map" }
              ]
            }
            """;
        Uri? requestedUri = null;

        var map = await V8SourceMap.ParseAsync(
            json,
            new Uri("https://example.test/assets/maps/index.js.map"),
            (uri, _) =>
            {
                requestedUri = uri;
                return Task.FromResult("""
                    {
                      "version": 3,
                      "sourceRoot": "../../src",
                      "sources": ["Feature.tsx"],
                      "names": [],
                      "mappings": "AAAA"
                    }
                    """);
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(new Uri("https://example.test/assets/maps/parts/feature.js.map"), requestedUri);
        Assert.Equal("https://example.test/assets/src/Feature.tsx", map.ResolveSourceUrl(0));
        Assert.Equal(new V8SourceMapEntry(1, 2, 0, 0, 0, null), Assert.Single(map.Entries));
    }

    [Fact]
    public void ReadsIgnoreListsAndBuildsBlackboxTransitions()
    {
        const string json = """
            {
              "version": 3,
              "sources": ["App.tsx", "vendor.ts"],
              "names": [],
              "ignoreList": [1],
              "mappings": "AAAA,CCAA,CDAA"
            }
            """;

        var map = V8SourceMap.Parse(json);

        Assert.False(map.IsIgnoredSource(0));
        Assert.True(map.IsIgnoredSource(1));
        Assert.Equal(
            new[] { new V8SourceMapPosition(0, 1), new V8SourceMapPosition(0, 2) },
            map.GetBlackboxedStateTransitions());

        var legacy = V8SourceMap.Parse("""
            {"version":3,"sources":["vendor.ts"],"names":[],"x_google_ignoreList":[0],"mappings":"AAAA"}
            """);
        Assert.True(legacy.IsIgnoredSource(0));
    }

    [Fact]
    public void RejectsOverlappingIndexedSections()
    {
        const string json = """
            {
              "version": 3,
              "sections": [
                { "offset": { "line": 1, "column": 0 }, "map": { "version": 3, "sources": [], "names": [], "mappings": "" } },
                { "offset": { "line": 1, "column": 0 }, "map": { "version": 3, "sources": [], "names": [], "mappings": "" } }
              ]
            }
            """;

        Assert.Throws<FormatException>(() => V8SourceMap.Parse(json));
    }
}
