using System;
using System.IO;
using System.Linq;
using Xaml.Compiler.Registry;
using Xaml.Compiler.TypeSystem;
using Xaml.Compiler.TypeSystemImpl;
using Xaml.Compiler.Tests.Mocks;

namespace Xaml.Compiler.Tests;

public class TypeSystemTests
{
    private readonly XamlSchemaRegistry _registry;
    private readonly IXamlNamespace _mockNamespace;

    public TypeSystemTests()
    {
        _registry = new XamlSchemaRegistry();
        _mockNamespace = new XamlNamespace(
            uri: "http://schemas.mock.com/xaml",
            defaultPrefix: "mock",
            targetAssemblies: new[] { "Xaml.Compiler.Tests" },
            clrNamespaces: new[] { "Xaml.Compiler.Tests.Mocks" }
        );
        _registry.RegisterNamespace(_mockNamespace);
    }

    [Fact]
    public void test_resolve_standard_type_from_registry_resolves_correct_metadata()
    {
        var type = _registry.ResolveType("http://schemas.mock.com/xaml", "MockControl");
        Assert.NotNull(type);
        Assert.Equal("MockControl", type.Name);
        Assert.False(type.IsMarkupExtension);
        Assert.False(type.IsCollection);
        Assert.Null(type.BaseType);
    }

    [Fact]
    public void test_resolve_implicit_markup_extension_resolves_with_suffix()
    {
        var type = _registry.ResolveType("http://schemas.mock.com/xaml", "MockMarkup");
        Assert.NotNull(type);
        Assert.Equal("MockMarkupExtension", type.Name);
        Assert.True(type.IsMarkupExtension);
    }

    [Fact]
    public void test_resolve_property_on_type_returns_correct_property_metadata()
    {
        var type = _registry.ResolveType("http://schemas.mock.com/xaml", "MockControl");
        Assert.NotNull(type);
        
        var nameProp = type.GetProperty("Name");
        Assert.NotNull(nameProp);
        Assert.Equal("Name", nameProp.Name);
        Assert.False(nameProp.IsAttached);
        Assert.False(nameProp.IsReadOnly);
    }

    [Fact]
    public void test_resolve_attached_property_returns_is_attached_true()
    {
        var type = _registry.ResolveType("http://schemas.mock.com/xaml", "MockGrid");
        Assert.NotNull(type);

        var rowProp = type.GetProperty("Row");
        Assert.NotNull(rowProp);
        Assert.True(rowProp.IsAttached);
        Assert.False(rowProp.IsReadOnly);
    }

    [Fact]
    public void test_convert_value_with_primitive_types_converts_correctly()
    {
        var type = _registry.ResolveType("http://schemas.mock.com/xaml", "MockControl");
        Assert.NotNull(type);

        var widthProp = type.GetProperty("Width");
        Assert.NotNull(widthProp);

        var converted = widthProp.ConvertValue("123.45");
        Assert.Equal(123.45, converted);
    }

    [Fact]
    public void test_convert_value_with_type_level_converter_converts_correctly()
    {
        var type = _registry.ResolveType("http://schemas.mock.com/xaml", "MockControl");
        Assert.NotNull(type);

        var marginProp = type.GetProperty("Margin");
        Assert.NotNull(marginProp);

        var convertedSingle = marginProp.ConvertValue("10") as MockThickness;
        Assert.NotNull(convertedSingle);
        Assert.Equal(10.0, convertedSingle.Left);
        Assert.Equal(10.0, convertedSingle.Top);

        var convertedFour = marginProp.ConvertValue("5,10,15,20") as MockThickness;
        Assert.NotNull(convertedFour);
        Assert.Equal(5.0, convertedFour.Left);
        Assert.Equal(10.0, convertedFour.Top);
        Assert.Equal(15.0, convertedFour.Right);
        Assert.Equal(20.0, convertedFour.Bottom);
    }

    [Fact]
    public void test_convert_value_with_property_level_converter_converts_correctly()
    {
        var type = _registry.ResolveType("http://schemas.mock.com/xaml", "MockControl");
        Assert.NotNull(type);

        var backgroundProp = type.GetProperty("Background");
        Assert.NotNull(backgroundProp);

        var converted = backgroundProp.ConvertValue("#FF0000") as MockColor;
        Assert.NotNull(converted);
        Assert.Equal("#FF0000", converted.Hex);
    }

    [Fact]
    public void test_resolve_non_existent_type_returns_null()
    {
        var result = _registry.ResolveType("http://schemas.mock.com/xaml", "NonExistentControl");
        Assert.Null(result);
    }

    [Fact]
    public void test_resolve_clr_namespace_dynamically_registers_and_resolves_type()
    {
        var type = _registry.ResolveType("clr-namespace:System;assembly=System.Private.CoreLib", "Int32");
        Assert.NotNull(type);
        Assert.Equal("Int32", type.Name);
    }

    [Fact]
    public void test_resolve_base_type_properties_recursively()
    {
        var type = _registry.ResolveType("http://schemas.mock.com/xaml", "MockButton");
        Assert.NotNull(type);

        // Content is defined on MockButton
        var contentProp = type.GetProperty("Content");
        Assert.NotNull(contentProp);

        // Width is inherited from MockControl
        var widthProp = type.GetProperty("Width");
        Assert.NotNull(widthProp);
        Assert.Equal("Width", widthProp.Name);
    }

    [Fact]
    public void test_get_set_value_on_standard_property()
    {
        var type = _registry.ResolveType("http://schemas.mock.com/xaml", "MockControl");
        Assert.NotNull(type);

        var widthProp = type.GetProperty("Width");
        Assert.NotNull(widthProp);

        var instance = new MockControl();
        widthProp.SetValue(instance, 450.0);
        Assert.Equal(450.0, instance.Width);
        Assert.Equal(450.0, widthProp.GetValue(instance));
    }

    [Fact]
    public void test_get_set_value_on_attached_property()
    {
        var type = _registry.ResolveType("http://schemas.mock.com/xaml", "MockGrid");
        Assert.NotNull(type);

        var rowProp = type.GetProperty("Row");
        Assert.NotNull(rowProp);

        var target = new MockControl();
        rowProp.SetValue(target, 2);
        Assert.Equal(2, MockGrid.GetRow(target));
        Assert.Equal(2, rowProp.GetValue(target));
    }

    [Fact]
    public void test_content_property_resolution_resolves_correct_property()
    {
        var type = _registry.ResolveType("http://schemas.mock.com/xaml", "MockButton");
        Assert.NotNull(type);

        Assert.NotNull(type.ContentProperty);
        Assert.Equal("Content", type.ContentProperty.Name);
    }

    [Fact]
    public void test_resolve_property_using_dot_notation_resolves_attached_property()
    {
        var type = _registry.ResolveType("http://schemas.mock.com/xaml", "MockControl");
        Assert.NotNull(type);

        // Should resolve Grid.Row when querying on control type context
        var resolved = _registry.ResolveProperty(type, "MockGrid.Row");
        Assert.NotNull(resolved);
        Assert.True(resolved.IsAttached);
        Assert.Equal("Row", resolved.Name);
    }

    [Fact]
    public void test_resolve_local_namespace_without_assembly_resolves_correctly()
    {
        var localRegistry = new XamlSchemaRegistry();
        var nsWithoutAssembly = new XamlNamespace(
            uri: "http://schemas.mock.com/local-no-asm",
            defaultPrefix: "local",
            targetAssemblies: Array.Empty<string>(),
            clrNamespaces: new[] { "Xaml.Compiler.Tests.Mocks" }
        );
        localRegistry.RegisterNamespace(nsWithoutAssembly);

        var type = localRegistry.ResolveType("http://schemas.mock.com/local-no-asm", "MockControl");
        Assert.NotNull(type);
        Assert.Equal("MockControl", type.Name);
    }
}
