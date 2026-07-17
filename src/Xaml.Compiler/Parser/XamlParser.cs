using System;
using System.Collections.Generic;
using System.Text;
using Xaml.Compiler.Ast;

namespace Xaml.Compiler.Parser
{
    public record struct TextChange(int Start, int Length, string NewText);

    public static class XamlParser
    {
        public static XamlDocumentSyntax Parse(string source)
        {
            return Parse(source.AsSpan());
        }

        public static XamlDocumentSyntax Parse(ReadOnlySpan<char> source)
        {
            var scanner = new XamlScanner(source);
            return ParseDocument(ref scanner);
        }

        public static XamlElementSyntax ParseElement(string elementText)
        {
            return ParseElement(elementText, new List<Diagnostic>());
        }

        public static XamlElementSyntax ParseElement(string elementText, List<Diagnostic> diagnostics)
        {
            var scanner = new XamlScanner(elementText.AsSpan());

            ScanWhitespaceAndComments(ref scanner, out var leadingTrivia);

            if (scanner.Peek() != '<')
            {
                throw new InvalidOperationException("Fragment does not start with '<'");
            }

            var element = ParseElement(ref scanner, diagnostics, new Stack<XamlElementSyntax>());
            element.LeadingTrivia.AddRange(leadingTrivia);
            return element;
        }

        public static XamlDocumentSyntax ParseIncremental(XamlDocumentSyntax oldDocument, string newText, TextChange change)
        {
            return IncrementalParser.ApplyChange(oldDocument, newText, change);
        }

        private static XamlDocumentSyntax ParseDocument(ref XamlScanner scanner)
        {
            var doc = new XamlDocumentSyntax();
            var startPos = scanner.CurrentPosition;

            ParseHeaderTrivia(ref scanner, doc.HeaderTrivia);

            if (!scanner.IsAtEnd && scanner.Peek() == '<')
            {
                doc.RootElement = ParseElement(ref scanner, doc.Diagnostics, new Stack<XamlElementSyntax>());
            }

            ParseFooterTrivia(ref scanner, doc.FooterTrivia);

            var endPos = scanner.CurrentPosition;
            doc.Span = new SourceSpan(startPos, endPos);

            return doc;
        }

        private static XamlElementSyntax ParseElement(
            ref XamlScanner scanner,
            List<Diagnostic> diagnostics,
            Stack<XamlElementSyntax> activeTags)
        {
            var startPos = scanner.CurrentPosition;
            var element = new XamlElementSyntax();

            // Consume '<'
            scanner.Read();

            // Parse prefix and name
            var (prefix, name) = ScanTagName(ref scanner);
            element.Prefix = prefix;
            element.LocalName = name;

            activeTags.Push(element);

            try
            {
                // Parse attributes
                while (!scanner.IsAtEnd)
                {
                    ScanWhitespaceAndComments(ref scanner, out var triviaList);

                    var peekChar = scanner.Peek();
                    if (peekChar == '/' || peekChar == '>')
                    {
                        element.BeforeCloseBracketTrivia.AddRange(triviaList);
                        break;
                    }

                    var attribute = ParseAttribute(ref scanner, diagnostics);
                    if (attribute != null)
                    {
                        attribute.LeadingTrivia.AddRange(triviaList);
                        // Check for duplicate attributes
                        if (element.Attributes.Exists(a => a.LocalName == attribute.LocalName && a.Prefix == attribute.Prefix))
                        {
                            diagnostics.Add(new Diagnostic(
                                "XAML001",
                                $"Duplicate attribute '{attribute.LocalName}'",
                                DiagnosticSeverity.Warning,
                                attribute.Span));
                        }
                        element.Attributes.Add(attribute);
                    }
                }

                if (scanner.IsAtEnd)
                {
                    diagnostics.Add(new Diagnostic("XAML009", $"Element '{name}' was not closed", DiagnosticSeverity.Error, new SourceSpan(startPos, scanner.CurrentPosition)));
                    element.Span = new SourceSpan(startPos, scanner.CurrentPosition);
                    return element;
                }

                // Close tags
                if (scanner.Peek() == '/')
                {
                    scanner.Read(); // Consume '/'
                    if (scanner.Peek() == '>')
                    {
                        scanner.Read(); // Consume '>'
                        element.IsSelfClosing = true;
                    }
                    else
                    {
                        diagnostics.Add(new Diagnostic("XAML002", "Expected '>' after '/'", DiagnosticSeverity.Error, new SourceSpan(scanner.CurrentPosition, scanner.CurrentPosition)));
                    }
                }
                else if (scanner.Peek() == '>')
                {
                    scanner.Read(); // Consume '>'

                    // Parse children
                    while (!scanner.IsAtEnd)
                    {
                        // Look for end tag
                        if (scanner.Peek() == '<' && scanner.Peek(1) == '/')
                        {
                            if (PeekCloseTagName(scanner, out var closePrefix, out var closeName))
                            {
                                if (closeName == element.LocalName && closePrefix == element.Prefix)
                                {
                                    break;
                                }
                                else
                                {
                                    // Check if it matches an ancestor
                                    bool matchesAncestor = false;
                                    foreach (var ancestor in activeTags)
                                    {
                                        if (ancestor != element && ancestor.LocalName == closeName && ancestor.Prefix == closePrefix)
                                        {
                                            matchesAncestor = true;
                                            break;
                                        }
                                    }
                                    if (matchesAncestor)
                                    {
                                        break;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                        }

                        if (scanner.Peek() == '<')
                        {
                            if (scanner.Peek(1) == '!' && scanner.Peek(2) == '-' && scanner.Peek(3) == '-')
                            {
                                var comment = ParseComment(ref scanner);
                                element.Children.Add(comment);
                            }
                            else if (scanner.Peek(1) == '!' && scanner.Peek(2) == '[' &&
                                     scanner.Peek(3) == 'C' && scanner.Peek(4) == 'D' &&
                                     scanner.Peek(5) == 'A' && scanner.Peek(6) == 'T' &&
                                     scanner.Peek(7) == 'A' && scanner.Peek(8) == '[')
                            {
                                var cdata = ParseTextNode(ref scanner);
                                element.Children.Add(cdata);
                            }
                            else
                            {
                                var childElement = ParseElement(ref scanner, diagnostics, activeTags);
                                element.Children.Add(childElement);
                            }
                        }
                        else
                        {
                            var textNode = ParseTextNode(ref scanner);
                            if (textNode != null)
                            {
                                element.Children.Add(textNode);
                            }
                        }
                    }

                    // Parse closing tag if matching
                    bool hasCloseTag = false;
                    if (scanner.Peek() == '<' && scanner.Peek(1) == '/')
                    {
                        if (PeekCloseTagName(scanner, out var closePrefix, out var closeName))
                        {
                            // If it matches an ancestor (other than element), we do not consume it here.
                            bool matchesOtherAncestor = false;
                            foreach (var ancestor in activeTags)
                            {
                                if (ancestor != element && ancestor.LocalName == closeName && ancestor.Prefix == closePrefix)
                                {
                                    matchesOtherAncestor = true;
                                    break;
                                }
                            }

                            if (matchesOtherAncestor)
                            {
                                diagnostics.Add(new Diagnostic(
                                    "XAML004",
                                    $"Missing close tag </{(string.IsNullOrEmpty(element.Prefix) ? "" : element.Prefix + ":")}{element.LocalName}>",
                                    DiagnosticSeverity.Error,
                                    new SourceSpan(startPos, scanner.CurrentPosition)));
                            }
                            else
                            {
                                hasCloseTag = true;
                                ScanWhitespaceAndComments(ref scanner, out var closeTagTrivia);
                                element.CloseTagLeadingTrivia.AddRange(closeTagTrivia);

                                scanner.Read(); // Consume '<'
                                scanner.Read(); // Consume '/'

                                var (cpfx, cname) = ScanTagName(ref scanner);
                                element.CloseTagPrefix = cpfx;
                                element.CloseTagLocalName = cname;

                                ScanWhitespaceAndComments(ref scanner, out var closeTagNameTrivia);
                                element.CloseTagBeforeCloseTrivia.AddRange(closeTagNameTrivia);

                                if (scanner.Peek() == '>')
                                {
                                    scanner.Read(); // Consume '>'
                                }
                                else
                                {
                                    diagnostics.Add(new Diagnostic("XAML010", "Expected '>' at end of closing tag", DiagnosticSeverity.Error, new SourceSpan(scanner.CurrentPosition, scanner.CurrentPosition)));
                                }

                                if (cname != element.LocalName || cpfx != element.Prefix)
                                {
                                    diagnostics.Add(new Diagnostic(
                                        "XAML003",
                                        $"Mismatched close tag. Expected '</{(string.IsNullOrEmpty(element.Prefix) ? "" : element.Prefix + ":")}{element.LocalName}>' but found '</{(string.IsNullOrEmpty(cpfx) ? "" : cpfx + ":")}{cname}>'",
                                        DiagnosticSeverity.Error,
                                        new SourceSpan(startPos, scanner.CurrentPosition)));
                                }
                            }
                        }
                    }

                    if (!hasCloseTag && !element.IsSelfClosing)
                    {
                        bool alreadyAdded = false;
                        foreach (var diag in diagnostics)
                        {
                            if (diag.Code == "XAML004" && diag.Span.End == scanner.CurrentPosition)
                            {
                                alreadyAdded = true;
                                break;
                            }
                        }
                        if (!alreadyAdded)
                        {
                            diagnostics.Add(new Diagnostic(
                                "XAML004",
                                $"Missing close tag </{(string.IsNullOrEmpty(element.Prefix) ? "" : element.Prefix + ":")}{element.LocalName}>",
                                DiagnosticSeverity.Error,
                                new SourceSpan(startPos, scanner.CurrentPosition)));
                        }
                    }
                }
            }
            finally
            {
                activeTags.Pop();
            }

            element.Span = new SourceSpan(startPos, scanner.CurrentPosition);
            return element;
        }

        private static XamlAttributeSyntax ParseAttribute(ref XamlScanner scanner, List<Diagnostic> diagnostics)
        {
            var startPos = scanner.CurrentPosition;
            var attr = new XamlAttributeSyntax();

            var (prefix, name) = ScanTagName(ref scanner);
            if (string.IsNullOrEmpty(name))
            {
                var invalidChar = scanner.Read();
                diagnostics.Add(new Diagnostic("XAML011", $"Invalid character '{invalidChar}' in attribute name", DiagnosticSeverity.Error, new SourceSpan(startPos, scanner.CurrentPosition)));
                attr.Span = new SourceSpan(startPos, scanner.CurrentPosition);
                return attr;
            }
            attr.Prefix = prefix;
            attr.LocalName = name;

            ScanWhitespaceAndComments(ref scanner, out var beforeEquals);
            attr.BeforeEqualsTrivia.AddRange(beforeEquals);

            if (scanner.Peek() != '=')
            {
                diagnostics.Add(new Diagnostic("XAML005", $"Expected '=' after attribute name '{name}'", DiagnosticSeverity.Error, new SourceSpan(startPos, scanner.CurrentPosition)));
                attr.Span = new SourceSpan(startPos, scanner.CurrentPosition);
                return attr;
            }
            scanner.Read(); // Consume '='

            ScanWhitespaceAndComments(ref scanner, out var afterEquals);
            attr.AfterEqualsTrivia.AddRange(afterEquals);

            var quote = scanner.Peek();
            if (quote != '"' && quote != '\'')
            {
                diagnostics.Add(new Diagnostic("XAML006", "Expected string quote character", DiagnosticSeverity.Error, new SourceSpan(startPos, scanner.CurrentPosition)));
                attr.Span = new SourceSpan(startPos, scanner.CurrentPosition);
                return attr;
            }
            attr.QuoteChar = quote;
            scanner.Read(); // Consume quote

            var valueStart = scanner.CurrentPosition;
            if (scanner.Peek() == '{')
            {
                attr.ValueNode = ParseMarkupExtension(ref scanner, diagnostics, quote);
            }
            else
            {
                string valueStr = scanner.ReadAttributeValue(quote);
                var lit = new XamlLiteralValueSyntax(valueStr);
                lit.Span = new SourceSpan(valueStart, scanner.CurrentPosition);
                attr.ValueNode = lit;
            }

            if (scanner.Peek() == quote)
            {
                scanner.Read(); // Consume closing quote
            }
            else
            {
                diagnostics.Add(new Diagnostic("XAML007", "Unclosed attribute string value", DiagnosticSeverity.Error, new SourceSpan(startPos, scanner.CurrentPosition)));
            }

            attr.Span = new SourceSpan(startPos, scanner.CurrentPosition);
            return attr;
        }

        private static XamlMarkupExtensionSyntax ParseMarkupExtension(ref XamlScanner scanner, List<Diagnostic> diagnostics, char stopChar = '\0')
        {
            var startPos = scanner.CurrentPosition;
            scanner.Read(); // Consume '{'

            var extName = ScanExtensionName(ref scanner);
            var ext = new XamlMarkupExtensionSyntax { ExtensionName = extName };

            ScanWhitespaceAndComments(ref scanner, out var betweenNameAndArgs);
            ext.BetweenNameAndArgumentsTrivia.AddRange(betweenNameAndArgs);

            while (!scanner.IsAtEnd && scanner.Peek() != '}' && (stopChar == '\0' || scanner.Peek() != stopChar))
            {
                ScanWhitespaceAndComments(ref scanner, out var argLeadingTrivia);
                var arg = ParseMarkupExtensionArgument(ref scanner, diagnostics, stopChar);
                if (arg != null)
                {
                    arg.LeadingTrivia.AddRange(argLeadingTrivia);
                    ext.Arguments.Add(arg);
                }

                ScanWhitespaceAndComments(ref scanner, out var separatorTrivia);
                if (scanner.Peek() == ',')
                {
                    var commaStart = scanner.CurrentPosition;
                    scanner.Read(); // Consume ','
                    var commaEnd = scanner.CurrentPosition;
                    var commaTrivia = new XamlTrivia(",", new SourceSpan(commaStart, commaEnd));

                    if (arg != null)
                    {
                        arg.TrailingCommaTrivia.AddRange(separatorTrivia);
                        arg.TrailingCommaTrivia.Add(commaTrivia);
                    }
                }
                else
                {
                    if (arg != null)
                    {
                        arg.TrailingTrivia.AddRange(separatorTrivia);
                    }
                }
            }

            ScanWhitespaceAndComments(ref scanner, out var beforeCloseBrace);
            ext.BeforeCloseBraceTrivia.AddRange(beforeCloseBrace);

            if (scanner.Peek() == '}')
            {
                scanner.Read(); // Consume '}'
            }
            else
            {
                diagnostics.Add(new Diagnostic("XAML008", "Unterminated markup extension", DiagnosticSeverity.Error, new SourceSpan(startPos, scanner.CurrentPosition)));
            }

            ext.Span = new SourceSpan(startPos, scanner.CurrentPosition);
            return ext;
        }

        private static XamlMarkupExtensionArgumentSyntax ParseMarkupExtensionArgument(ref XamlScanner scanner, List<Diagnostic> diagnostics, char stopChar = '\0')
        {
            var startPos = scanner.CurrentPosition;
            var arg = new XamlMarkupExtensionArgumentSyntax();

            int offset = 0;
            bool isNamed = false;
            while (scanner.Peek(offset) != '\0' && scanner.Peek(offset) != '}' && scanner.Peek(offset) != ',' && (stopChar == '\0' || scanner.Peek(offset) != stopChar))
            {
                if (scanner.Peek(offset) == '=')
                {
                    isNamed = true;
                    break;
                }
                offset++;
            }

            if (isNamed)
            {
                arg.Name = ScanExtensionName(ref scanner);

                ScanWhitespaceAndComments(ref scanner, out var beforeEquals);
                arg.BeforeEqualsTrivia.AddRange(beforeEquals);

                if (scanner.Peek() == '=')
                {
                    scanner.Read(); // Consume '='
                }

                ScanWhitespaceAndComments(ref scanner, out var afterEquals);
                arg.AfterEqualsTrivia.AddRange(afterEquals);
            }

            var valueStart = scanner.CurrentPosition;
            if (scanner.Peek() == '{')
            {
                arg.ValueNode = ParseMarkupExtension(ref scanner, diagnostics, stopChar);
            }
            else
            {
                var sb = new StringBuilder();
                char quote = scanner.Peek();
                if (quote == '\'' || quote == '"')
                {
                    scanner.Read(); // Consume quote
                    while (!scanner.IsAtEnd && scanner.Peek() != quote)
                    {
                        sb.Append(scanner.Read());
                    }
                    if (scanner.Peek() == quote)
                    {
                        scanner.Read(); // Consume quote
                    }
                }
                else
                {
                    while (!scanner.IsAtEnd && scanner.Peek() != '}' && scanner.Peek() != ',' && (stopChar == '\0' || scanner.Peek() != stopChar))
                    {
                        sb.Append(scanner.Read());
                    }
                }
                arg.Value = sb.ToString();
            }

            arg.Span = new SourceSpan(startPos, scanner.CurrentPosition);
            return arg;
        }

        private static (string prefix, string name) ScanTagName(ref XamlScanner scanner)
        {
            string fullName = scanner.ReadTagName();
            int colonIdx = fullName.IndexOf(':');
            if (colonIdx > 0 && colonIdx < fullName.Length - 1)
            {
                string prefix = fullName.Substring(0, colonIdx);
                string name = fullName.Substring(colonIdx + 1);
                return (prefix, name);
            }

            return (string.Empty, fullName);
        }

        private static string ScanExtensionName(ref XamlScanner scanner)
        {
            return scanner.ReadExtensionName();
        }

        private static void ScanWhitespaceAndComments(ref XamlScanner scanner, out List<XamlTrivia> triviaList)
        {
            triviaList = new List<XamlTrivia>();
            while (!scanner.IsAtEnd)
            {
                if (char.IsWhiteSpace(scanner.Peek()))
                {
                    var start = scanner.CurrentPosition;
                    string val = scanner.ReadWhitespace();
                    triviaList.Add(new XamlTrivia(val, new SourceSpan(start, scanner.CurrentPosition)));
                }
                else
                {
                    break;
                }
            }
        }

        private static XamlCommentSyntax ParseComment(ref XamlScanner scanner)
        {
            var start = scanner.CurrentPosition;
            scanner.Read(); // <
            scanner.Read(); // !
            scanner.Read(); // -
            scanner.Read(); // -

            string text = scanner.ReadCommentText();

            if (scanner.Peek() == '-')
            {
                scanner.Read(); // -
                scanner.Read(); // -
                scanner.Read(); // >
            }

            var comment = new XamlCommentSyntax { CommentText = text };
            comment.Span = new SourceSpan(start, scanner.CurrentPosition);
            return comment;
        }

        private static XamlSyntaxNode ParseTextNode(ref XamlScanner scanner)
        {
            var start = scanner.CurrentPosition;

            if (scanner.Peek() == '<' && scanner.Peek(1) == '!' && scanner.Peek(2) == '[' &&
                scanner.Peek(3) == 'C' && scanner.Peek(4) == 'D' && scanner.Peek(5) == 'A' &&
                scanner.Peek(6) == 'T' && scanner.Peek(7) == 'A' && scanner.Peek(8) == '[')
            {
                for (int i = 0; i < 9; i++) scanner.Read();

                string text = scanner.ReadCDataText();

                if (scanner.Peek() == ']')
                {
                    scanner.Read(); // ]
                    scanner.Read(); // ]
                    scanner.Read(); // >
                }

                var cdata = new XamlCDataSyntax { Value = text };
                cdata.Span = new SourceSpan(start, scanner.CurrentPosition);
                return cdata;
            }
            else
            {
                string text = scanner.ReadTextNodeText();
                var textNode = new XamlTextSyntax(text);
                textNode.Span = new SourceSpan(start, scanner.CurrentPosition);
                return textNode;
            }
        }

        private static void ParseHeaderTrivia(ref XamlScanner scanner, List<XamlTrivia> list)
        {
            while (!scanner.IsAtEnd)
            {
                if (scanner.Peek() == '<')
                {
                    if (scanner.Peek(1) == '?' || (scanner.Peek(1) == '!' && scanner.Peek(2) == '-' && scanner.Peek(3) == '-'))
                    {
                        var start = scanner.CurrentPosition;
                        if (scanner.Peek(1) == '?')
                        {
                            scanner.Read(); // <
                            scanner.Read(); // ?
                            string content = scanner.ReadProcessingInstructionText();
                            string total = "<?" + content;
                            if (!scanner.IsAtEnd)
                            {
                                scanner.Read(); // ?
                                scanner.Read(); // >
                                total += "?>";
                            }
                            list.Add(new XamlTrivia(total, new SourceSpan(start, scanner.CurrentPosition)));
                        }
                        else
                        {
                            scanner.Read(); // <
                            scanner.Read(); // !
                            scanner.Read(); // -
                            scanner.Read(); // -
                            string content = scanner.ReadCommentText();
                            string total = "<!--" + content;
                            if (!scanner.IsAtEnd)
                            {
                                scanner.Read(); // -
                                scanner.Read(); // -
                                scanner.Read(); // >
                                total += "-->";
                            }
                            list.Add(new XamlTrivia(total, new SourceSpan(start, scanner.CurrentPosition)));
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                else if (char.IsWhiteSpace(scanner.Peek()))
                {
                    var start = scanner.CurrentPosition;
                    string val = scanner.ReadWhitespace();
                    list.Add(new XamlTrivia(val, new SourceSpan(start, scanner.CurrentPosition)));
                }
                else
                {
                    var start = scanner.CurrentPosition;
                    string val = scanner.ReadTextNodeText();
                    list.Add(new XamlTrivia(val, new SourceSpan(start, scanner.CurrentPosition)));
                }
            }
        }

        private static void ParseFooterTrivia(ref XamlScanner scanner, List<XamlTrivia> list)
        {
            while (!scanner.IsAtEnd)
            {
                var start = scanner.CurrentPosition;
                if (scanner.Peek() == '<' && scanner.Peek(1) == '!' && scanner.Peek(2) == '-' && scanner.Peek(3) == '-')
                {
                    scanner.Read(); // <
                    scanner.Read(); // !
                    scanner.Read(); // -
                    scanner.Read(); // -
                    string content = scanner.ReadCommentText();
                    string total = "<!--" + content;
                    if (!scanner.IsAtEnd)
                    {
                        scanner.Read(); // -
                        scanner.Read(); // -
                        scanner.Read(); // >
                        total += "-->";
                    }
                    list.Add(new XamlTrivia(total, new SourceSpan(start, scanner.CurrentPosition)));
                }
                else if (char.IsWhiteSpace(scanner.Peek()))
                {
                    string val = scanner.ReadWhitespace();
                    list.Add(new XamlTrivia(val, new SourceSpan(start, scanner.CurrentPosition)));
                }
                else
                {
                    int startIndex = scanner.Index;
                    while (!scanner.IsAtEnd)
                    {
                        scanner.Read();
                    }
                    string val = scanner.Source.Slice(startIndex).ToString();
                    list.Add(new XamlTrivia(val, new SourceSpan(start, scanner.CurrentPosition)));
                }
            }
        }

        private static bool PeekCloseTagName(XamlScanner scanner, out string prefix, out string name)
        {
            prefix = string.Empty;
            name = string.Empty;

            if (scanner.Peek() == '<' && scanner.Peek(1) == '/')
            {
                scanner.Read(); // <
                scanner.Read(); // /

                while (!scanner.IsAtEnd && char.IsWhiteSpace(scanner.Peek()))
                {
                    scanner.Read();
                }

                (prefix, name) = ScanTagName(ref scanner);
                return true;
            }

            return false;
        }
    }

    public ref struct XamlScanner
    {
        private readonly ReadOnlySpan<char> _source;
        private int _index;
        private int _line;
        private int _column;

        public XamlScanner(ReadOnlySpan<char> source)
        {
            _source = source;
            _index = 0;
            _line = 1;
            _column = 1;
        }

        public char Peek() => _index < _source.Length ? _source[_index] : '\0';
        public char Peek(int offset) => _index + offset < _source.Length ? _source[_index + offset] : '\0';

        public char Read()
        {
            if (_index >= _source.Length) return '\0';
            char ch = _source[_index++];
            if (ch == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
            return ch;
        }

        public SourcePosition CurrentPosition => new SourcePosition(_index, _line, _column);
        public bool IsAtEnd => _index >= _source.Length;
        public int Index => _index;
        public ReadOnlySpan<char> Source => _source;

        public string ReadTagName()
        {
            int start = _index;
            while (_index < _source.Length)
            {
                char c = _source[_index];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' || c == ':')
                {
                    _index++;
                    _column++;
                }
                else
                {
                    break;
                }
            }
            return _source.Slice(start, _index - start).ToString();
        }

        public string ReadExtensionName()
        {
            int start = _index;
            while (_index < _source.Length)
            {
                char c = _source[_index];
                if (char.IsWhiteSpace(c) || c == '=' || c == '}' || c == ',')
                {
                    break;
                }
                _index++;
                _column++;
            }
            return _source.Slice(start, _index - start).ToString();
        }

        public string ReadWhitespace()
        {
            int start = _index;
            while (_index < _source.Length)
            {
                char c = _source[_index];
                if (!char.IsWhiteSpace(c))
                {
                    break;
                }
                _index++;
                if (c == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
            }
            return _source.Slice(start, _index - start).ToString();
        }

        public string ReadAttributeValue(char quote)
        {
            int start = _index;
            while (_index < _source.Length)
            {
                char c = _source[_index];
                if (c == quote)
                {
                    break;
                }
                _index++;
                if (c == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
            }
            return _source.Slice(start, _index - start).ToString();
        }

        public string ReadCommentText()
        {
            int start = _index;
            while (_index < _source.Length)
            {
                if (_index + 2 < _source.Length && _source[_index] == '-' && _source[_index + 1] == '-' && _source[_index + 2] == '>')
                {
                    break;
                }
                char c = _source[_index++];
                if (c == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
            }
            return _source.Slice(start, _index - start).ToString();
        }

        public string ReadCDataText()
        {
            int start = _index;
            while (_index < _source.Length)
            {
                if (_index + 2 < _source.Length && _source[_index] == ']' && _source[_index + 1] == ']' && _source[_index + 2] == '>')
                {
                    break;
                }
                char c = _source[_index++];
                if (c == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
            }
            return _source.Slice(start, _index - start).ToString();
        }

        public string ReadTextNodeText()
        {
            int start = _index;
            while (_index < _source.Length)
            {
                char c = _source[_index];
                if (c == '<')
                {
                    break;
                }
                _index++;
                if (c == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
            }
            return _source.Slice(start, _index - start).ToString();
        }

        public string ReadProcessingInstructionText()
        {
            int start = _index;
            while (_index < _source.Length)
            {
                if (_index + 1 < _source.Length && _source[_index] == '?' && _source[_index + 1] == '>')
                {
                    break;
                }
                char c = _source[_index++];
                if (c == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }
            }
            return _source.Slice(start, _index - start).ToString();
        }
    }

    public static class IncrementalParser
    {
        public static XamlDocumentSyntax ApplyChange(XamlDocumentSyntax oldDocument, string newText, TextChange change)
        {
            var targetElement = FindEnclosingElement(oldDocument, change.Start, change.Start + change.Length);

            if (targetElement == null || targetElement == oldDocument.RootElement)
            {
                return XamlParser.Parse(newText);
            }

            var parent = FindParent(oldDocument, targetElement);
            if (parent == null)
            {
                return XamlParser.Parse(newText);
            }

            int oldStart = targetElement.Span.Start.Offset;
            int oldEnd = targetElement.Span.End.Offset;
            int lengthDelta = change.NewText.Length - change.Length;
            int newLength = (oldEnd - oldStart) + lengthDelta;

            if (newLength < 0 || oldStart + newLength > newText.Length)
            {
                return XamlParser.Parse(newText);
            }

            string newElementText = newText.Substring(oldStart, newLength);

            XamlElementSyntax newElement;
            var newDiagnostics = new List<Diagnostic>();
            try
            {
                newElement = XamlParser.ParseElement(newElementText, newDiagnostics);
            }
            catch
            {
                return XamlParser.Parse(newText);
            }

            int lineDelta = targetElement.Span.Start.Line - 1;
            int startColumn = targetElement.Span.Start.Column;
            TranslateNode(newElement, oldStart, lineDelta, startColumn);

            int idx = parent.Children.IndexOf(targetElement);
            if (idx >= 0)
            {
                oldDocument.Diagnostics.RemoveAll(d => d.Span.Start.Offset >= oldStart && d.Span.End.Offset <= oldEnd);
                parent.Children[idx] = newElement;
            }
            else
            {
                return XamlParser.Parse(newText);
            }

            string oldText = oldDocument.ToFullString();
            var editStart = GetPositionAtOffset(oldText, change.Start);
            var editEnd = GetPositionAtOffset(oldText, change.Start + change.Length);

            int newLines = 0;
            int lastLineLength = 0;
            foreach (char c in change.NewText)
            {
                if (c == '\n')
                {
                    newLines++;
                    lastLineLength = 0;
                }
                else
                {
                    lastLineLength++;
                }
            }

            int newTextEndLine;
            int newTextEndColumn;
            if (newLines == 0)
            {
                newTextEndLine = editStart.Line;
                newTextEndColumn = editStart.Column + change.NewText.Length;
            }
            else
            {
                newTextEndLine = editStart.Line + newLines;
                newTextEndColumn = 1 + lastLineLength;
            }

            int lineDeltaShift = newTextEndLine - editEnd.Line;

            ShiftDownstream(oldDocument, newElement, change.Start, change.Length, editEnd.Line, editEnd.Column, newTextEndLine, newTextEndColumn, lengthDelta, lineDeltaShift);

            foreach (var diag in newDiagnostics)
            {
                var translatedSpan = TranslateSpan(diag.Span, oldStart, lineDelta, startColumn);
                oldDocument.Diagnostics.Add(diag with { Span = translatedSpan });
            }

            return oldDocument;
        }

        private static XamlElementSyntax? FindEnclosingElement(XamlSyntaxNode node, int start, int end)
        {
            if (node is XamlElementSyntax el)
            {
                if (el.Span.Start.Offset <= start && el.Span.End.Offset >= end)
                {
                    foreach (var child in el.Children)
                    {
                        var sub = FindEnclosingElement(child, start, end);
                        if (sub != null) return sub;
                    }
                    return el;
                }
            }
            else if (node is XamlDocumentSyntax doc && doc.RootElement != null)
            {
                return FindEnclosingElement(doc.RootElement, start, end);
            }
            return null;
        }

        private static XamlElementSyntax? FindParent(XamlSyntaxNode root, XamlElementSyntax childToFind)
        {
            if (root is XamlElementSyntax el)
            {
                foreach (var child in el.Children)
                {
                    if (child == childToFind) return el;
                    if (child is XamlElementSyntax elChild)
                    {
                        var p = FindParent(elChild, childToFind);
                        if (p != null) return p;
                    }
                }
            }
            else if (root is XamlDocumentSyntax doc && doc.RootElement != null)
            {
                if (doc.RootElement == childToFind) return null;
                return FindParent(doc.RootElement, childToFind);
            }
            return null;
        }

        private static SourcePosition GetPositionAtOffset(string text, int offset)
        {
            int line = 1;
            int col = 1;
            for (int i = 0; i < offset && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                    col = 1;
                }
                else
                {
                    col++;
                }
            }
            return new SourcePosition(offset, line, col);
        }

        private static void TranslateNode(XamlSyntaxNode node, int offsetDelta, int lineDelta, int startColumn)
        {
            node.Span = TranslateSpan(node.Span, offsetDelta, lineDelta, startColumn);

            foreach (var trivia in node.LeadingTrivia)
            {
                trivia.Span = TranslateSpan(trivia.Span, offsetDelta, lineDelta, startColumn);
            }
            foreach (var trivia in node.TrailingTrivia)
            {
                trivia.Span = TranslateSpan(trivia.Span, offsetDelta, lineDelta, startColumn);
            }

            if (node is XamlDocumentSyntax doc)
            {
                foreach (var trivia in doc.HeaderTrivia)
                {
                    trivia.Span = TranslateSpan(trivia.Span, offsetDelta, lineDelta, startColumn);
                }
                if (doc.RootElement != null)
                {
                    TranslateNode(doc.RootElement, offsetDelta, lineDelta, startColumn);
                }
                foreach (var trivia in doc.FooterTrivia)
                {
                    trivia.Span = TranslateSpan(trivia.Span, offsetDelta, lineDelta, startColumn);
                }
            }
            else if (node is XamlElementSyntax el)
            {
                foreach (var attr in el.Attributes)
                {
                    TranslateNode(attr, offsetDelta, lineDelta, startColumn);
                }
                foreach (var child in el.Children)
                {
                    TranslateNode(child, offsetDelta, lineDelta, startColumn);
                }
                foreach (var trivia in el.BeforeCloseBracketTrivia)
                {
                    trivia.Span = TranslateSpan(trivia.Span, offsetDelta, lineDelta, startColumn);
                }
                foreach (var trivia in el.CloseTagLeadingTrivia)
                {
                    trivia.Span = TranslateSpan(trivia.Span, offsetDelta, lineDelta, startColumn);
                }
                foreach (var trivia in el.CloseTagBeforeCloseTrivia)
                {
                    trivia.Span = TranslateSpan(trivia.Span, offsetDelta, lineDelta, startColumn);
                }
            }
            else if (node is XamlAttributeSyntax attr)
            {
                if (attr.ValueNode != null)
                {
                    TranslateNode(attr.ValueNode, offsetDelta, lineDelta, startColumn);
                }
                foreach (var trivia in attr.BeforeEqualsTrivia)
                {
                    trivia.Span = TranslateSpan(trivia.Span, offsetDelta, lineDelta, startColumn);
                }
                foreach (var trivia in attr.AfterEqualsTrivia)
                {
                    trivia.Span = TranslateSpan(trivia.Span, offsetDelta, lineDelta, startColumn);
                }
            }
            else if (node is XamlMarkupExtensionSyntax ext)
            {
                foreach (var arg in ext.Arguments)
                {
                    TranslateNode(arg, offsetDelta, lineDelta, startColumn);
                }
                foreach (var trivia in ext.BetweenNameAndArgumentsTrivia)
                {
                    trivia.Span = TranslateSpan(trivia.Span, offsetDelta, lineDelta, startColumn);
                }
                foreach (var trivia in ext.BeforeCloseBraceTrivia)
                {
                    trivia.Span = TranslateSpan(trivia.Span, offsetDelta, lineDelta, startColumn);
                }
            }
            else if (node is XamlMarkupExtensionArgumentSyntax arg)
            {
                if (arg.ValueNode != null)
                {
                    TranslateNode(arg.ValueNode, offsetDelta, lineDelta, startColumn);
                }
                foreach (var trivia in arg.BeforeEqualsTrivia)
                {
                    trivia.Span = TranslateSpan(trivia.Span, offsetDelta, lineDelta, startColumn);
                }
                foreach (var trivia in arg.AfterEqualsTrivia)
                {
                    trivia.Span = TranslateSpan(trivia.Span, offsetDelta, lineDelta, startColumn);
                }
                foreach (var trivia in arg.TrailingCommaTrivia)
                {
                    trivia.Span = TranslateSpan(trivia.Span, offsetDelta, lineDelta, startColumn);
                }
            }
        }

        private static SourceSpan TranslateSpan(SourceSpan span, int offsetDelta, int lineDelta, int startColumn)
        {
            return new SourceSpan(
                TranslatePosition(span.Start, offsetDelta, lineDelta, startColumn),
                TranslatePosition(span.End, offsetDelta, lineDelta, startColumn)
            );
        }

        private static SourcePosition TranslatePosition(SourcePosition pos, int offsetDelta, int lineDelta, int startColumn)
        {
            int newOffset = pos.Offset + offsetDelta;
            int newLine;
            int newColumn;
            if (pos.Line == 1)
            {
                newLine = lineDelta + 1;
                newColumn = startColumn + pos.Column - 1;
            }
            else
            {
                newLine = lineDelta + pos.Line;
                newColumn = pos.Column;
            }
            return new SourcePosition(newOffset, newLine, newColumn);
        }

        private static void ShiftDownstream(XamlSyntaxNode node, XamlSyntaxNode excludedNode, int changeStart, int changeLength, int editEndLine, int editEndColumn, int newTextEndLine, int newTextEndColumn, int lengthDelta, int lineDelta)
        {
            if (node == excludedNode)
            {
                return;
            }

            node.Span = node.Span.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            for (int i = 0; i < node.LeadingTrivia.Count; i++)
            {
                node.LeadingTrivia[i] = node.LeadingTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }
            for (int i = 0; i < node.TrailingTrivia.Count; i++)
            {
                node.TrailingTrivia[i] = node.TrailingTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }

            if (node is XamlDocumentSyntax doc)
            {
                for (int i = 0; i < doc.HeaderTrivia.Count; i++)
                {
                    doc.HeaderTrivia[i] = doc.HeaderTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
                if (doc.RootElement != null)
                {
                    ShiftDownstream(doc.RootElement, excludedNode, changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
                for (int i = 0; i < doc.FooterTrivia.Count; i++)
                {
                    doc.FooterTrivia[i] = doc.FooterTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
                for (int i = 0; i < doc.Diagnostics.Count; i++)
                {
                    var diag = doc.Diagnostics[i];
                    var newSpan = diag.Span.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                    doc.Diagnostics[i] = diag with { Span = newSpan };
                }
            }
            else if (node is XamlElementSyntax el)
            {
                foreach (var attr in el.Attributes)
                {
                    ShiftDownstream(attr, excludedNode, changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
                foreach (var child in el.Children)
                {
                    ShiftDownstream(child, excludedNode, changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
                for (int i = 0; i < el.BeforeCloseBracketTrivia.Count; i++)
                {
                    el.BeforeCloseBracketTrivia[i] = el.BeforeCloseBracketTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
                for (int i = 0; i < el.CloseTagLeadingTrivia.Count; i++)
                {
                    el.CloseTagLeadingTrivia[i] = el.CloseTagLeadingTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
                for (int i = 0; i < el.CloseTagBeforeCloseTrivia.Count; i++)
                {
                    el.CloseTagBeforeCloseTrivia[i] = el.CloseTagBeforeCloseTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
            }
            else if (node is XamlAttributeSyntax attr)
            {
                if (attr.ValueNode != null)
                {
                    ShiftDownstream(attr.ValueNode, excludedNode, changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
                for (int i = 0; i < attr.BeforeEqualsTrivia.Count; i++)
                {
                    attr.BeforeEqualsTrivia[i] = attr.BeforeEqualsTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
                for (int i = 0; i < attr.AfterEqualsTrivia.Count; i++)
                {
                    attr.AfterEqualsTrivia[i] = attr.AfterEqualsTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
            }
            else if (node is XamlMarkupExtensionSyntax ext)
            {
                foreach (var arg in ext.Arguments)
                {
                    ShiftDownstream(arg, excludedNode, changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
                for (int i = 0; i < ext.BetweenNameAndArgumentsTrivia.Count; i++)
                {
                    ext.BetweenNameAndArgumentsTrivia[i] = ext.BetweenNameAndArgumentsTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
                for (int i = 0; i < ext.BeforeCloseBraceTrivia.Count; i++)
                {
                    ext.BeforeCloseBraceTrivia[i] = ext.BeforeCloseBraceTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
            }
            else if (node is XamlMarkupExtensionArgumentSyntax arg)
            {
                if (arg.ValueNode != null)
                {
                    ShiftDownstream(arg.ValueNode, excludedNode, changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
                for (int i = 0; i < arg.BeforeEqualsTrivia.Count; i++)
                {
                    arg.BeforeEqualsTrivia[i] = arg.BeforeEqualsTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
                for (int i = 0; i < arg.AfterEqualsTrivia.Count; i++)
                {
                    arg.AfterEqualsTrivia[i] = arg.AfterEqualsTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
                for (int i = 0; i < arg.TrailingCommaTrivia.Count; i++)
                {
                    arg.TrailingCommaTrivia[i] = arg.TrailingCommaTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                }
            }
        }
    }
}
