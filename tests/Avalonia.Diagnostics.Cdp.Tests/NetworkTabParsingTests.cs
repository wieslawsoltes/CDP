using Xunit;
using CdpInspectorApp.Models;
using CdpInspectorApp.ViewModels;
using System.Linq;
using System.Collections.ObjectModel;

namespace Avalonia.Diagnostics.Cdp.Tests;

public class NetworkTabParsingTests
{
    [Fact]
    public void Test_QueryStringParsing()
    {
        var model = new NetworkRequestModel();

        // 1. No query string
        model.Url = "https://example.com/api/users";
        Assert.Empty(model.QueryParameters);

        // 2. Simple query string
        model.Url = "https://example.com/api/users?name=john&age=30";
        Assert.Equal(2, model.QueryParameters.Count);
        Assert.Equal("name", model.QueryParameters[0].Key);
        Assert.Equal("john", model.QueryParameters[0].Value);
        Assert.Equal("age", model.QueryParameters[1].Key);
        Assert.Equal("30", model.QueryParameters[1].Value);

        // 3. Encoded values
        model.Url = "https://example.com/api/users?q=hello%20world&email=john%40doe.com";
        Assert.Equal(2, model.QueryParameters.Count);
        Assert.Equal("q", model.QueryParameters[0].Key);
        Assert.Equal("hello world", model.QueryParameters[0].Value);
        Assert.Equal("email", model.QueryParameters[1].Key);
        Assert.Equal("john@doe.com", model.QueryParameters[1].Value);

        // 4. Value containing equals signs
        model.Url = "https://example.com/api/users?token=abc=123=xyz&status=active";
        Assert.Equal(2, model.QueryParameters.Count);
        Assert.Equal("token", model.QueryParameters[0].Key);
        Assert.Equal("abc=123=xyz", model.QueryParameters[0].Value);
        Assert.Equal("status", model.QueryParameters[1].Key);
        Assert.Equal("active", model.QueryParameters[1].Value);
    }

    [Fact]
    public void Test_PostParametersParsing()
    {
        var model = new NetworkRequestModel();

        // 1. JSON object post parameters
        model.ParsePostParameters("{\"name\": \"John Doe\", \"active\": true}");
        Assert.Equal(2, model.PostParameters.Count);
        Assert.Equal("name", model.PostParameters[0].Key);
        Assert.Equal("John Doe", model.PostParameters[0].Value);
        Assert.Equal("active", model.PostParameters[1].Key);
        Assert.Equal("true", model.PostParameters[1].Value);

        // 2. JSON array post parameters
        model.ParsePostParameters("[\"first\", \"second\"]");
        Assert.Equal(2, model.PostParameters.Count);
        Assert.Equal("[0]", model.PostParameters[0].Key);
        Assert.Equal("first", model.PostParameters[0].Value);
        Assert.Equal("[1]", model.PostParameters[1].Key);
        Assert.Equal("second", model.PostParameters[1].Value);

        // 3. Form urlencoded post parameters
        model.ParsePostParameters("username=johndoe&token=abc%20123");
        Assert.Equal(2, model.PostParameters.Count);
        Assert.Equal("username", model.PostParameters[0].Key);
        Assert.Equal("johndoe", model.PostParameters[0].Value);
        Assert.Equal("token", model.PostParameters[1].Key);
        Assert.Equal("abc 123", model.PostParameters[1].Value);

        // 4. Fallback raw text
        model.ParsePostParameters("Plain text post data");
        Assert.Single(model.PostParameters);
        Assert.Equal("raw", model.PostParameters[0].Key);
        Assert.Equal("Plain text post data", model.PostParameters[0].Value);

        // 5. Value containing equals signs in form urlencoded
        model.ParsePostParameters("token=abc=123=xyz&status=active");
        Assert.Equal(2, model.PostParameters.Count);
        Assert.Equal("token", model.PostParameters[0].Key);
        Assert.Equal("abc=123=xyz", model.PostParameters[0].Value);
        Assert.Equal("status", model.PostParameters[1].Key);
        Assert.Equal("active", model.PostParameters[1].Value);
    }
}
