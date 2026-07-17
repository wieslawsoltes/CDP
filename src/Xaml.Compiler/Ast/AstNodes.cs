using System;
using System.Collections.Generic;
using System.Text;

namespace Xaml.Compiler.Ast
{
    public record struct SourcePosition(int Offset, int Line, int Column)
    {
        public readonly SourcePosition Shift(int changeStart, int changeLength, int editEndLine, int editEndColumn, int newTextEndLine, int newTextEndColumn, int lengthDelta, int lineDelta)
        {
            if (Offset < changeStart + changeLength)
            {
                return this;
            }

            int newOffset = Offset + lengthDelta;
            int newLine = Line;
            int newColumn = Column;

            if (Line > editEndLine)
            {
                newLine += lineDelta;
            }
            else if (Line == editEndLine)
            {
                newLine += lineDelta;
                newColumn = newTextEndColumn + (Column - editEndColumn);
            }

            return new SourcePosition(newOffset, newLine, newColumn);
        }
    }

    public record struct SourceSpan(SourcePosition Start, SourcePosition End)
    {
        public readonly SourceSpan Shift(int changeStart, int changeLength, int editEndLine, int editEndColumn, int newTextEndLine, int newTextEndColumn, int lengthDelta, int lineDelta)
        {
            return new SourceSpan(
                Start.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta),
                End.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta)
            );
        }
    }

    public enum DiagnosticSeverity
    {
        Error,
        Warning,
        Info
    }

    public record Diagnostic(string Code, string Message, DiagnosticSeverity Severity, SourceSpan Span);

    public abstract class XamlSyntaxNode
    {
        public SourceSpan Span { get; set; }
        public List<XamlTrivia> LeadingTrivia { get; } = new();
        public List<XamlTrivia> TrailingTrivia { get; } = new();

        public abstract string ToFullString();

        public virtual void Shift(int changeStart, int changeLength, int editEndLine, int editEndColumn, int newTextEndLine, int newTextEndColumn, int lengthDelta, int lineDelta)
        {
            Span = Span.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);

            for (int i = 0; i < LeadingTrivia.Count; i++)
            {
                LeadingTrivia[i] = LeadingTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }
            for (int i = 0; i < TrailingTrivia.Count; i++)
            {
                TrailingTrivia[i] = TrailingTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }
        }
    }

    public class XamlTrivia
    {
        public SourceSpan Span { get; set; }
        public string RawText { get; set; } = string.Empty;

        public XamlTrivia(string rawText, SourceSpan span)
        {
            RawText = rawText;
            Span = span;
        }

        public XamlTrivia Shift(int changeStart, int changeLength, int editEndLine, int editEndColumn, int newTextEndLine, int newTextEndColumn, int lengthDelta, int lineDelta)
        {
            return new XamlTrivia(RawText, Span.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta));
        }
    }

    public enum XamlTokenType
    {
        None,
        LessThan,               // <
        LessThanSlash,          // </
        GreaterThan,            // >
        SlashGreaterThan,       // />
        Equals,                 // =
        Colon,                  // :
        OpenBrace,              // {
        CloseBrace,             // }
        Comma,                  // ,
        Name,                   // Tag names, attributes, prefixes
        StringLiteral,          // Quoted attribute values (preserving quote style)
        Text,                   // Inner text content
        Comment,                // <!-- ... -->
        CData,                  // <![CDATA[ ... ]]>
        ProcessingInstruction,  // <?xml ... ?>
        EndOfFile,
        Error                   // Lexer error token
    }

    public class XamlToken
    {
        public XamlTokenType Type { get; set; }
        public string Text { get; set; } = string.Empty;
        public SourceSpan Span { get; set; }

        public XamlToken(XamlTokenType type, string text, SourceSpan span)
        {
            Type = type;
            Text = text;
            Span = span;
        }

        public XamlToken Shift(int changeStart, int changeLength, int editEndLine, int editEndColumn, int newTextEndLine, int newTextEndColumn, int lengthDelta, int lineDelta)
        {
            return new XamlToken(Type, Text, Span.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta));
        }
    }

    public class XamlDocumentSyntax : XamlSyntaxNode
    {
        public List<XamlTrivia> HeaderTrivia { get; } = new();
        public XamlElementSyntax? RootElement { get; set; }
        public List<XamlTrivia> FooterTrivia { get; } = new();
        public List<Diagnostic> Diagnostics { get; } = new();

        public override string ToFullString()
        {
            var sb = new StringBuilder();
            foreach (var trivia in LeadingTrivia) sb.Append(trivia.RawText);
            foreach (var trivia in HeaderTrivia) sb.Append(trivia.RawText);
            if (RootElement != null) sb.Append(RootElement.ToFullString());
            foreach (var trivia in FooterTrivia) sb.Append(trivia.RawText);
            foreach (var trivia in TrailingTrivia) sb.Append(trivia.RawText);
            return sb.ToString();
        }

        public override void Shift(int changeStart, int changeLength, int editEndLine, int editEndColumn, int newTextEndLine, int newTextEndColumn, int lengthDelta, int lineDelta)
        {
            base.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);

            for (int i = 0; i < HeaderTrivia.Count; i++)
            {
                HeaderTrivia[i] = HeaderTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }

            RootElement?.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);

            for (int i = 0; i < FooterTrivia.Count; i++)
            {
                FooterTrivia[i] = FooterTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }

            for (int i = 0; i < Diagnostics.Count; i++)
            {
                var diag = Diagnostics[i];
                var newSpan = diag.Span.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
                Diagnostics[i] = diag with { Span = newSpan };
            }
        }
    }

    public class XamlElementSyntax : XamlSyntaxNode
    {
        public string Prefix { get; set; } = string.Empty;
        public string LocalName { get; set; } = string.Empty;
        public List<XamlAttributeSyntax> Attributes { get; } = new();
        public List<XamlSyntaxNode> Children { get; } = new();
        public bool IsSelfClosing { get; set; }

        // Formatting trivia
        public List<XamlTrivia> BeforeCloseBracketTrivia { get; } = new();      // Whitespace before > or />
        public List<XamlTrivia> CloseTagLeadingTrivia { get; } = new();         // Whitespace before </
        public string CloseTagPrefix { get; set; } = string.Empty;
        public string CloseTagLocalName { get; set; } = string.Empty;
        public List<XamlTrivia> CloseTagBeforeCloseTrivia { get; } = new();     // Whitespace inside </Button   >

        public override string ToFullString()
        {
            var sb = new StringBuilder();

            // 1. Leading trivia
            foreach (var trivia in LeadingTrivia) sb.Append(trivia.RawText);

            // 2. Open bracket and name
            sb.Append('<');
            if (!string.IsNullOrEmpty(Prefix))
            {
                sb.Append(Prefix);
                sb.Append(':');
            }
            sb.Append(LocalName);

            // 3. Attributes
            foreach (var attr in Attributes)
            {
                sb.Append(attr.ToFullString());
            }

            // 4. Close tag / Self-closing tag
            foreach (var trivia in BeforeCloseBracketTrivia) sb.Append(trivia.RawText);
            if (IsSelfClosing)
            {
                sb.Append("/>");
            }
            else
            {
                sb.Append('>');

                // 5. Children
                foreach (var child in Children)
                {
                    sb.Append(child.ToFullString());
                }

                // 6. End tag
                foreach (var trivia in CloseTagLeadingTrivia) sb.Append(trivia.RawText);
                sb.Append("</");
                if (!string.IsNullOrEmpty(CloseTagPrefix))
                {
                    sb.Append(CloseTagPrefix);
                    sb.Append(':');
                }
                sb.Append(CloseTagLocalName);
                foreach (var trivia in CloseTagBeforeCloseTrivia) sb.Append(trivia.RawText);
                sb.Append('>');
            }

            // 7. Trailing trivia
            foreach (var trivia in TrailingTrivia) sb.Append(trivia.RawText);
            return sb.ToString();
        }

        public override void Shift(int changeStart, int changeLength, int editEndLine, int editEndColumn, int newTextEndLine, int newTextEndColumn, int lengthDelta, int lineDelta)
        {
            base.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);

            foreach (var attr in Attributes)
            {
                attr.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }
            foreach (var child in Children)
            {
                child.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }
            for (int i = 0; i < BeforeCloseBracketTrivia.Count; i++)
            {
                BeforeCloseBracketTrivia[i] = BeforeCloseBracketTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }
            for (int i = 0; i < CloseTagLeadingTrivia.Count; i++)
            {
                CloseTagLeadingTrivia[i] = CloseTagLeadingTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }
            for (int i = 0; i < CloseTagBeforeCloseTrivia.Count; i++)
            {
                CloseTagBeforeCloseTrivia[i] = CloseTagBeforeCloseTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }
        }
    }

    public abstract class XamlValueSyntax : XamlSyntaxNode
    {
    }

    public class XamlAttributeSyntax : XamlSyntaxNode
    {
        public string Prefix { get; set; } = string.Empty;
        public string LocalName { get; set; } = string.Empty;
        public char QuoteChar { get; set; } = '"';
        public XamlValueSyntax? ValueNode { get; set; }

        public List<XamlTrivia> BeforeEqualsTrivia { get; } = new();
        public List<XamlTrivia> AfterEqualsTrivia { get; } = new();

        public override string ToFullString()
        {
            var sb = new StringBuilder();
            foreach (var trivia in LeadingTrivia) sb.Append(trivia.RawText);

            if (!string.IsNullOrEmpty(Prefix))
            {
                sb.Append(Prefix);
                sb.Append(':');
            }
            sb.Append(LocalName);

            foreach (var trivia in BeforeEqualsTrivia) sb.Append(trivia.RawText);
            sb.Append('=');
            foreach (var trivia in AfterEqualsTrivia) sb.Append(trivia.RawText);

            if (ValueNode != null)
            {
                sb.Append(QuoteChar);
                var valueStr = ValueNode.ToFullString();
                valueStr = System.Text.RegularExpressions.Regex.Replace(valueStr, @"&(?!(amp|lt|gt|quot|apos|#[0-9]+|#x[0-9a-fA-F]+);)", "&amp;");
                valueStr = valueStr.Replace("<", "&lt;").Replace(">", "&gt;");
                if (QuoteChar == '"')
                {
                    valueStr = valueStr.Replace("\"", "&quot;");
                }
                else if (QuoteChar == '\'')
                {
                    valueStr = valueStr.Replace("'", "&apos;");
                }
                sb.Append(valueStr);
                sb.Append(QuoteChar);
            }

            foreach (var trivia in TrailingTrivia) sb.Append(trivia.RawText);
            return sb.ToString();
        }

        public override void Shift(int changeStart, int changeLength, int editEndLine, int editEndColumn, int newTextEndLine, int newTextEndColumn, int lengthDelta, int lineDelta)
        {
            base.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);

            ValueNode?.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);

            for (int i = 0; i < BeforeEqualsTrivia.Count; i++)
            {
                BeforeEqualsTrivia[i] = BeforeEqualsTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }
            for (int i = 0; i < AfterEqualsTrivia.Count; i++)
            {
                AfterEqualsTrivia[i] = AfterEqualsTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }
        }
    }

    public class XamlLiteralValueSyntax : XamlValueSyntax
    {
        public string Value { get; set; } = string.Empty;

        public XamlLiteralValueSyntax(string value)
        {
            Value = value;
        }

        public override string ToFullString()
        {
            var sb = new StringBuilder();
            foreach (var trivia in LeadingTrivia) sb.Append(trivia.RawText);
            sb.Append(Value);
            foreach (var trivia in TrailingTrivia) sb.Append(trivia.RawText);
            return sb.ToString();
        }

        public override void Shift(int changeStart, int changeLength, int editEndLine, int editEndColumn, int newTextEndLine, int newTextEndColumn, int lengthDelta, int lineDelta)
        {
            base.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
        }
    }

    public class XamlMarkupExtensionSyntax : XamlValueSyntax
    {
        public string ExtensionName { get; set; } = string.Empty;
        public List<XamlMarkupExtensionArgumentSyntax> Arguments { get; } = new();

        // Trivia for markup extension spacing
        public List<XamlTrivia> BetweenNameAndArgumentsTrivia { get; } = new();
        public List<XamlTrivia> BeforeCloseBraceTrivia { get; } = new();

        public override string ToFullString()
        {
            var sb = new StringBuilder();
            foreach (var trivia in LeadingTrivia) sb.Append(trivia.RawText);

            sb.Append('{');
            sb.Append(ExtensionName);
            foreach (var trivia in BetweenNameAndArgumentsTrivia) sb.Append(trivia.RawText);

            foreach (var arg in Arguments)
            {
                sb.Append(arg.ToFullString());
            }

            foreach (var trivia in BeforeCloseBraceTrivia) sb.Append(trivia.RawText);
            sb.Append('}');

            foreach (var trivia in TrailingTrivia) sb.Append(trivia.RawText);
            return sb.ToString();
        }

        public override void Shift(int changeStart, int changeLength, int editEndLine, int editEndColumn, int newTextEndLine, int newTextEndColumn, int lengthDelta, int lineDelta)
        {
            base.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);

            foreach (var arg in Arguments)
            {
                arg.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }
            for (int i = 0; i < BetweenNameAndArgumentsTrivia.Count; i++)
            {
                BetweenNameAndArgumentsTrivia[i] = BetweenNameAndArgumentsTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }
            for (int i = 0; i < BeforeCloseBraceTrivia.Count; i++)
            {
                BeforeCloseBraceTrivia[i] = BeforeCloseBraceTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }
        }
    }

    public class XamlMarkupExtensionArgumentSyntax : XamlSyntaxNode
    {
        public string? Name { get; set; } // Null if it is a positional argument
        public string Value { get; set; } = string.Empty;
        public XamlValueSyntax? ValueNode { get; set; } // For nested markup extensions

        public List<XamlTrivia> BeforeEqualsTrivia { get; } = new();
        public List<XamlTrivia> AfterEqualsTrivia { get; } = new();
        public List<XamlTrivia> TrailingCommaTrivia { get; } = new();

        public override string ToFullString()
        {
            var sb = new StringBuilder();
            foreach (var trivia in LeadingTrivia) sb.Append(trivia.RawText);

            if (Name != null)
            {
                sb.Append(Name);
                foreach (var trivia in BeforeEqualsTrivia) sb.Append(trivia.RawText);
                sb.Append('=');
                foreach (var trivia in AfterEqualsTrivia) sb.Append(trivia.RawText);
            }

            if (ValueNode != null)
            {
                sb.Append(ValueNode.ToFullString());
            }
            else
            {
                sb.Append(Value);
            }

            foreach (var trivia in TrailingCommaTrivia) sb.Append(trivia.RawText);
            foreach (var trivia in TrailingTrivia) sb.Append(trivia.RawText);
            return sb.ToString();
        }

        public override void Shift(int changeStart, int changeLength, int editEndLine, int editEndColumn, int newTextEndLine, int newTextEndColumn, int lengthDelta, int lineDelta)
        {
            base.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);

            ValueNode?.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);

            for (int i = 0; i < BeforeEqualsTrivia.Count; i++)
            {
                BeforeEqualsTrivia[i] = BeforeEqualsTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }
            for (int i = 0; i < AfterEqualsTrivia.Count; i++)
            {
                AfterEqualsTrivia[i] = AfterEqualsTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }
            for (int i = 0; i < TrailingCommaTrivia.Count; i++)
            {
                TrailingCommaTrivia[i] = TrailingCommaTrivia[i].Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
            }
        }
    }

    public class XamlTextSyntax : XamlSyntaxNode
    {
        public string Value { get; set; } = string.Empty;

        public XamlTextSyntax(string value)
        {
            Value = value;
        }

        public override string ToFullString()
        {
            var sb = new StringBuilder();
            foreach (var trivia in LeadingTrivia) sb.Append(trivia.RawText);
            sb.Append(Value);
            foreach (var trivia in TrailingTrivia) sb.Append(trivia.RawText);
            return sb.ToString();
        }

        public override void Shift(int changeStart, int changeLength, int editEndLine, int editEndColumn, int newTextEndLine, int newTextEndColumn, int lengthDelta, int lineDelta)
        {
            base.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
        }
    }

    public class XamlCommentSyntax : XamlSyntaxNode
    {
        public string CommentText { get; set; } = string.Empty;

        public override string ToFullString()
        {
            var sb = new StringBuilder();
            foreach (var trivia in LeadingTrivia) sb.Append(trivia.RawText);
            sb.Append("<!--");
            sb.Append(CommentText);
            sb.Append("-->");
            foreach (var trivia in TrailingTrivia) sb.Append(trivia.RawText);
            return sb.ToString();
        }

        public override void Shift(int changeStart, int changeLength, int editEndLine, int editEndColumn, int newTextEndLine, int newTextEndColumn, int lengthDelta, int lineDelta)
        {
            base.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
        }
    }

    public class XamlCDataSyntax : XamlSyntaxNode
    {
        public string Value { get; set; } = string.Empty;

        public override string ToFullString()
        {
            var sb = new StringBuilder();
            foreach (var trivia in LeadingTrivia) sb.Append(trivia.RawText);
            sb.Append("<![CDATA[");
            sb.Append(Value);
            sb.Append("]]>");
            foreach (var trivia in TrailingTrivia) sb.Append(trivia.RawText);
            return sb.ToString();
        }

        public override void Shift(int changeStart, int changeLength, int editEndLine, int editEndColumn, int newTextEndLine, int newTextEndColumn, int lengthDelta, int lineDelta)
        {
            base.Shift(changeStart, changeLength, editEndLine, editEndColumn, newTextEndLine, newTextEndColumn, lengthDelta, lineDelta);
        }
    }
}
