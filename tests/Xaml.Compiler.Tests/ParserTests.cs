using System;
using System.IO;
using System.Linq;
using Xaml.Compiler.Ast;
using Xaml.Compiler.Parser;
using Xunit;

namespace Xaml.Compiler.Tests
{
    public class ParserTests
    {
        [Fact]
        public void test_lossless_roundtrip_valid_xaml()
        {
            var xaml = @"<Grid xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
      xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
      RowDefinitions=""Auto,*,Auto"">
    <!-- Header Comment -->
    <TextBlock Text=""Title"" Grid.Row=""0"" Margin=""10"" />
    
    <ContentControl Grid.Row=""1"">
        <![CDATA[Some CDATA text control content here]]>
    </ContentControl>
    
    <Button Content='Click Me' 
            Command=""{Binding ClickCommand, Mode=OneWay, Converter={StaticResource Conv}}""
            Grid.Row=""2"" />
</Grid>";
            var doc = XamlParser.Parse(xaml);
            var roundtrip = doc.ToFullString();
            Assert.Equal(xaml, roundtrip);
            Assert.Empty(doc.Diagnostics);
        }

        [Fact]
        public void test_error_recovery_unclosed_tag()
        {
            var xaml = "<Grid><Button></Grid>";
            var doc = XamlParser.Parse(xaml);

            Assert.NotNull(doc.RootElement);
            Assert.Equal("Grid", doc.RootElement.LocalName);
            Assert.Single(doc.RootElement.Children);

            var button = doc.RootElement.Children[0] as XamlElementSyntax;
            Assert.NotNull(button);
            Assert.Equal("Button", button.LocalName);
            Assert.True(button.Span.End.Offset > button.Span.Start.Offset);

            Assert.NotEmpty(doc.Diagnostics);
            var btnDiag = doc.Diagnostics.FirstOrDefault(d => d.Message.Contains("Missing close tag"));
            Assert.NotNull(btnDiag);
        }

        [Fact]
        public void test_error_recovery_mismatched_tag_ancestor()
        {
            var xaml = "<Grid><StackPanel><Button></StackPanel></Grid>";
            var doc = XamlParser.Parse(xaml);

            Assert.NotNull(doc.RootElement);
            Assert.Equal("Grid", doc.RootElement.LocalName);

            var panel = doc.RootElement.Children[0] as XamlElementSyntax;
            Assert.NotNull(panel);
            Assert.Equal("StackPanel", panel.LocalName);

            var button = panel.Children[0] as XamlElementSyntax;
            Assert.NotNull(button);
            Assert.Equal("Button", button.LocalName);

            Assert.NotEmpty(doc.Diagnostics);
            var btnDiag = doc.Diagnostics.FirstOrDefault(d => d.Message.Contains("Missing close tag"));
            Assert.NotNull(btnDiag);
        }

        [Fact]
        public void test_error_recovery_duplicate_attributes()
        {
            var xaml = "<Button Content=\"A\" Content=\"B\" />";
            var doc = XamlParser.Parse(xaml);

            Assert.NotNull(doc.RootElement);
            Assert.Equal(2, doc.RootElement.Attributes.Count);
            Assert.Equal("A", (doc.RootElement.Attributes[0].ValueNode as XamlLiteralValueSyntax)?.Value);
            Assert.Equal("B", (doc.RootElement.Attributes[1].ValueNode as XamlLiteralValueSyntax)?.Value);

            Assert.NotEmpty(doc.Diagnostics);
            var warning = doc.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Warning);
            Assert.NotNull(warning);
            Assert.Contains("Duplicate attribute", warning.Message);

            Assert.Equal(xaml, doc.ToFullString());
        }

        [Fact]
        public void test_error_recovery_malformed_markup_extension()
        {
            var xaml = "<Button Command=\"{Binding Path=Name\" />";
            var doc = XamlParser.Parse(xaml);

            Assert.NotNull(doc.RootElement);
            Assert.Single(doc.RootElement.Attributes);
            var attr = doc.RootElement.Attributes[0];
            Assert.NotNull(attr.ValueNode);
            Assert.IsType<XamlMarkupExtensionSyntax>(attr.ValueNode);

            var ext = (XamlMarkupExtensionSyntax)attr.ValueNode;
            Assert.Equal("Binding", ext.ExtensionName);
            Assert.Single(ext.Arguments);
            Assert.Equal("Path", ext.Arguments[0].Name);
            Assert.Equal("Name", ext.Arguments[0].Value);

            Assert.NotEmpty(doc.Diagnostics);
            Assert.Contains(doc.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        }

        [Fact]
        public void test_incremental_parsing_inside_element()
        {
            var originalXaml = @"<Grid>
    <StackPanel>
        <Button Content=""Click"" />
    </StackPanel>
</Grid>";
            var doc = XamlParser.Parse(originalXaml);

            int editStart = originalXaml.IndexOf("Click");
            int editLength = "Click".Length;
            string newText = "Hello World";

            string updatedXaml = originalXaml.Replace("Click", "Hello World");

            var newDoc = XamlParser.ParseIncremental(doc, updatedXaml, new TextChange(editStart, editLength, newText));

            Assert.Equal(updatedXaml, newDoc.ToFullString());

            var grid = newDoc.RootElement;
            Assert.NotNull(grid);
            var panel = grid!.Children.OfType<XamlElementSyntax>().First();
            Assert.NotNull(panel);
            var button = panel!.Children.OfType<XamlElementSyntax>().First();

            var contentAttr = button.Attributes.First(a => a.LocalName == "Content");
            Assert.Equal("Hello World", (contentAttr.ValueNode as XamlLiteralValueSyntax)?.Value);

            int originalPanelEnd = originalXaml.IndexOf("</StackPanel>");
            int newPanelEnd = updatedXaml.IndexOf("</StackPanel>");

            Assert.Equal(originalPanelEnd + (newText.Length - editLength), newPanelEnd);
            Assert.Equal(newPanelEnd, panel.Span.End.Offset - "</StackPanel>".Length);
        }

        [Fact]
        public void test_incremental_parsing_with_newlines()
        {
            var original = "<Grid>\n  <Button Content=\"A\" />\n</Grid>";
            var doc = XamlParser.Parse(original);

            int editStart = original.IndexOf("Content=\"A\"");
            int editLen = "Content=\"A\"".Length;
            string newText = "Content=\"A\"\n    Width=\"100\"";

            string expected = original.Replace("Content=\"A\"", "Content=\"A\"\n    Width=\"100\"");

            var newDoc = XamlParser.ParseIncremental(doc, expected, new TextChange(editStart, editLen, newText));

            Assert.Equal(expected, newDoc.ToFullString());

            var grid = newDoc.RootElement;
            Assert.NotNull(grid);
            Assert.Equal(4, grid!.Span.End.Line);
            Assert.Equal(8, grid!.Span.End.Column);
        }

        [Fact]
        public void test_incremental_parsing_diagnostics_update_and_shift()
        {
            // 1. Test clearing/resolving diagnostics
            var original = "<Button Content=\"A\" Content=\"B\" />";
            var doc = XamlParser.Parse(original);

            Assert.NotEmpty(doc.Diagnostics);
            Assert.Contains(doc.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("Duplicate attribute"));

            int editStart = original.IndexOf("Content=\"B\"");
            int editLen = "Content=\"B\"".Length;
            string newText = "Width=\"100\"";
            string expected = original.Replace("Content=\"B\"", "Width=\"100\"");

            var newDoc = XamlParser.ParseIncremental(doc, expected, new TextChange(editStart, editLen, newText));

            Assert.Equal(expected, newDoc.ToFullString());
            Assert.Empty(newDoc.Diagnostics);

            // 2. Test introducing diagnostics
            var originalValid = "<Button Content=\"A\" Width=\"100\" />";
            var docValid = XamlParser.Parse(originalValid);
            Assert.Empty(docValid.Diagnostics);

            int editStart2 = originalValid.IndexOf("Width=\"100\"");
            int editLen2 = "Width=\"100\"".Length;
            string newText2 = "Content=\"B\"";
            string expectedInvalid = originalValid.Replace("Width=\"100\"", "Content=\"B\"");

            var newDocInvalid = XamlParser.ParseIncremental(docValid, expectedInvalid, new TextChange(editStart2, editLen2, newText2));

            Assert.Equal(expectedInvalid, newDocInvalid.ToFullString());
            Assert.NotEmpty(newDocInvalid.Diagnostics);
            Assert.Contains(newDocInvalid.Diagnostics, d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("Duplicate attribute"));

            // 3. Test shifting diagnostics downstream
            var originalShift = @"<Grid>
    <StackPanel>
        <TextBlock Text=""Hello"" />
        <Button Content=""A"" Content=""B"" />
    </StackPanel>
</Grid>";
            var docShift = XamlParser.Parse(originalShift);
            Assert.NotEmpty(docShift.Diagnostics);
            var initialDiag = docShift.Diagnostics.First(d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("Duplicate attribute"));
            int initialLine = initialDiag.Span.Start.Line;
            Assert.Equal(4, initialLine);

            int editStartShift = originalShift.IndexOf("Text=\"Hello\"");
            int editLenShift = "Text=\"Hello\"".Length;
            string newTextShift = "Text=\"Hello\"\n           Height=\"50\"";
            string expectedShift = originalShift.Replace("Text=\"Hello\"", newTextShift);

            var newDocShift = XamlParser.ParseIncremental(docShift, expectedShift, new TextChange(editStartShift, editLenShift, newTextShift));

            Assert.Equal(expectedShift, newDocShift.ToFullString());
            Assert.NotEmpty(newDocShift.Diagnostics);
            var shiftedDiag = newDocShift.Diagnostics.First(d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("Duplicate attribute"));
            Assert.Equal(initialLine + 1, shiftedDiag.Span.Start.Line);
        }

        [Fact]
        public void test_markup_extension_quote_preservation()
        {
            var xaml = @"<TextBlock Text=""{Binding SliderValue, StringFormat='Slider Value: {0:0}'}"" />";
            var doc = XamlParser.Parse(xaml);
            var roundtrip = doc.ToFullString();
            Assert.Equal(xaml, roundtrip);
            Assert.Empty(doc.Diagnostics);

            var attr = doc.RootElement.Attributes[0];
            var ext = Assert.IsType<XamlMarkupExtensionSyntax>(attr.ValueNode);
            Assert.Equal("Binding", ext.ExtensionName);
            Assert.Equal(2, ext.Arguments.Count);
            
            var formatArg = ext.Arguments[1];
            Assert.Equal("StringFormat", formatArg.Name);
            Assert.Equal("Slider Value: {0:0}", formatArg.Value);
            Assert.Equal('\'', formatArg.QuoteChar);
        }
    }
}
