using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CDP.Document.Parser.AST;

namespace CDP.Document.Parser;

public class RtfDocumentParser : IDocumentParser
{
    private static readonly HashSet<string> SkippedGroups = new(StringComparer.Ordinal)
    {
        "fonttbl", "stylesheet", "info", "generator", "colortbl"
    };

    public Task<DocumentRoot> ParseAsync(Stream stream)
    {
        return Task.FromResult(Parse(stream));
    }

    public DocumentRoot Parse(Stream stream)
    {
        using var reader = new StreamReader(stream);
        string rtfContent = reader.ReadToEnd();

        var docRoot = new WordDocument();
        var currentPara = new ParagraphBlock();
        docRoot.AddChild(currentPara);

        var lexer = new RtfLexer(rtfContent);
        var stateStack = new Stack<RtfStyleState>();
        var currentState = new RtfStyleState();
        var colors = new List<string?> { null }; // default color index 0

        int currentDepth = 0;
        int skipGroupDepth = -1;
        int colorTableGroupDepth = -1;
        bool inColorTable = false;
        bool hasColorComponent = false;
        int charsToSkip = 0;

        int currentRed = 0;
        int currentGreen = 0;
        int currentBlue = 0;

        // New flags
        bool inHeader = false;
        bool inFooter = false;
        int headerGroupDepth = -1;
        int footerGroupDepth = -1;
        string headerText = "";
        string footerText = "";

        bool inPict = false;
        int pictGroupDepth = -1;
        string pictType = "image/png";
        var hexBuilder = new System.Text.StringBuilder();

        RtfToken? tokenObj;
        while ((tokenObj = lexer.NextToken()) != null)
        {
            var token = tokenObj.Value;

            if (token.Type == RtfTokenType.GroupStart)
            {
                currentDepth++;
                stateStack.Push(currentState.Clone());
            }
            else if (token.Type == RtfTokenType.GroupEnd)
            {
                if (inColorTable && currentDepth == colorTableGroupDepth)
                {
                    inColorTable = false;
                }
                if (inHeader && currentDepth == headerGroupDepth)
                {
                    inHeader = false;
                }
                if (inFooter && currentDepth == footerGroupDepth)
                {
                    inFooter = false;
                }
                if (inPict && currentDepth == pictGroupDepth)
                {
                    inPict = false;
                    string hex = hexBuilder.ToString().Trim();
                    hex = new string(hex.Where(c => !char.IsWhiteSpace(c)).ToArray());
                    try
                    {
                        byte[] bytes = HexStringToBytes(hex);
                        string base64 = Convert.ToBase64String(bytes);
                        var imgNode = new ImageInline
                        {
                            Source = $"data:{pictType};base64,{base64}"
                        };
                        currentPara.AddChild(imgNode);
                    }
                    catch { }
                }

                currentDepth--;
                if (skipGroupDepth != -1 && currentDepth < skipGroupDepth)
                {
                    skipGroupDepth = -1;
                }
                if (stateStack.Count > 0)
                {
                    currentState = stateStack.Pop();
                }
            }
            else if (token.Type == RtfTokenType.ControlWord)
            {
                if (token.Name == "*")
                {
                    if (skipGroupDepth == -1)
                    {
                        skipGroupDepth = currentDepth;
                    }
                }
                else if (SkippedGroups.Contains(token.Name))
                {
                    if (skipGroupDepth == -1)
                    {
                        skipGroupDepth = currentDepth;
                    }
                    if (token.Name == "colortbl")
                    {
                        inColorTable = true;
                        colorTableGroupDepth = currentDepth;
                        colors.Clear();
                        colors.Add(null); // default
                    }
                }
                else if (inColorTable)
                {
                    if (token.Name == "red") { currentRed = token.Parameter ?? 0; hasColorComponent = true; }
                    else if (token.Name == "green") { currentGreen = token.Parameter ?? 0; hasColorComponent = true; }
                    else if (token.Name == "blue") { currentBlue = token.Parameter ?? 0; hasColorComponent = true; }
                }
                else
                {
                    // Style controls
                    switch (token.Name)
                    {
                        case "header":
                            inHeader = true;
                            headerGroupDepth = currentDepth;
                            break;
                        case "footer":
                            inFooter = true;
                            footerGroupDepth = currentDepth;
                            break;
                        case "pict":
                            inPict = true;
                            pictGroupDepth = currentDepth;
                            pictType = "image/png";
                            hexBuilder.Clear();
                            break;
                        case "pngblip":
                            pictType = "image/png";
                            break;
                        case "jpegblip":
                            pictType = "image/jpeg";
                            break;
                        case "ilvl":
                            currentState.BulletLevel = token.Parameter ?? 0;
                            currentState.IsBullet = true;
                            break;
                        case "pnbullet":
                            currentState.IsBullet = true;
                            currentState.BulletStyle = "bullet";
                            break;
                        case "pndec":
                            currentState.IsBullet = true;
                            currentState.BulletStyle = "decimal";
                            break;
                        case "b":
                            currentState.Bold = (token.Parameter ?? 1) != 0;
                            break;
                        case "i":
                            currentState.Italic = (token.Parameter ?? 1) != 0;
                            break;
                        case "ul":
                            currentState.Underline = (token.Parameter ?? 1) != 0;
                            break;
                        case "ulnone":
                            currentState.Underline = false;
                            break;
                        case "fs":
                            // Font size in RTF is in half-points. e.g. \fs28 = 12pt
                            currentState.FontSize = (token.Parameter ?? 24) / 2.0;
                            break;
                        case "cf":
                            if (token.Parameter.HasValue && token.Parameter.Value >= 0 && token.Parameter.Value < colors.Count)
                            {
                                currentState.Color = colors[token.Parameter.Value];
                            }
                            break;
                        case "uc":
                            currentState.UnicodeSkip = token.Parameter ?? 1;
                            break;
                        case "u":
                            if (token.Parameter.HasValue)
                            {
                                int val = token.Parameter.Value;
                                char unicodeChar = (char)(ushort)val;
                                if (skipGroupDepth == -1)
                                {
                                    if (inHeader)
                                    {
                                        headerText += unicodeChar;
                                    }
                                    else if (inFooter)
                                    {
                                        footerText += unicodeChar;
                                    }
                                    else
                                    {
                                        currentPara.AddChild(new TextRun
                                        {
                                            Text = unicodeChar.ToString(),
                                            Bold = currentState.Bold,
                                            Italic = currentState.Italic,
                                            Underline = currentState.Underline,
                                            FontSize = currentState.FontSize,
                                            Color = currentState.Color
                                        });
                                    }
                                }
                                charsToSkip = currentState.UnicodeSkip;
                            }
                            break;
                        case "par":
                            currentPara = new ParagraphBlock
                            {
                                IsBullet = currentState.IsBullet,
                                BulletLevel = currentState.BulletLevel,
                                BulletStyle = currentState.BulletStyle
                            };
                            docRoot.AddChild(currentPara);
                            break;
                        case "line":
                            if (skipGroupDepth == -1 && !inHeader && !inFooter)
                            {
                                currentPara.AddChild(new LineBreakInline());
                            }
                            break;
                        case "tab":
                            if (skipGroupDepth == -1)
                            {
                                if (inHeader)
                                {
                                    headerText += "\t";
                                }
                                else if (inFooter)
                                {
                                    footerText += "\t";
                                }
                                else
                                {
                                    currentPara.AddChild(new TextRun
                                    {
                                        Text = "\t",
                                        Bold = currentState.Bold,
                                        Italic = currentState.Italic,
                                        Underline = currentState.Underline,
                                        FontSize = currentState.FontSize,
                                        Color = currentState.Color
                                    });
                                }
                            }
                            break;
                    }
                }
            }
            else if (token.Type == RtfTokenType.Text)
            {
                if (inColorTable)
                {
                    if (token.Name.Contains(";"))
                    {
                        var parts = token.Name.Split(';');
                        for (int i = 0; i < parts.Length - 1; i++)
                        {
                            if (hasColorComponent)
                            {
                                string hex = $"#{currentRed:X2}{currentGreen:X2}{currentBlue:X2}";
                                colors.Add(hex);
                            }
                            else
                            {
                                colors.Add(null);
                            }
                            currentRed = 0;
                            currentGreen = 0;
                            currentBlue = 0;
                            hasColorComponent = false;
                        }
                    }
                }
                else if (inPict)
                {
                    hexBuilder.Append(token.Name);
                }
                else
                {
                    string textVal = token.Name;
                    if (charsToSkip > 0)
                    {
                        if (textVal.Length <= charsToSkip)
                        {
                            charsToSkip -= textVal.Length;
                            textVal = string.Empty;
                        }
                        else
                        {
                            textVal = textVal.Substring(charsToSkip);
                            charsToSkip = 0;
                        }
                    }

                    if (textVal.Length > 0 && skipGroupDepth == -1)
                    {
                        if (inHeader)
                        {
                            headerText += textVal;
                        }
                        else if (inFooter)
                        {
                            footerText += textVal;
                        }
                        else
                        {
                            var textRun = new TextRun
                            {
                                Text = textVal,
                                Bold = currentState.Bold,
                                Italic = currentState.Italic,
                                Underline = currentState.Underline,
                                FontSize = currentState.FontSize,
                                Color = currentState.Color
                            };
                            currentPara.AddChild(textRun);
                        }
                    }
                }
            }
        }

        // Clean up empty paragraphs at the end or if they have no children
        var emptyParas = docRoot.Children.OfType<ParagraphBlock>().Where(p => p.Children.Count == 0).ToList();
        foreach (var p in emptyParas)
        {
            docRoot.Children.Remove(p);
        }

        docRoot.Header = headerText.Trim();
        docRoot.Footer = footerText.Trim();

        return docRoot;
    }

    private static byte[] HexStringToBytes(string hex)
    {
        if (hex.Length % 2 != 0) hex = hex.Substring(0, hex.Length - 1);
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}

public enum RtfTokenType
{
    GroupStart,
    GroupEnd,
    ControlWord,
    Text
}

public struct RtfToken
{
    public RtfTokenType Type;
    public string Name;
    public int? Parameter;
}

public class RtfStyleState
{
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public double? FontSize { get; set; }
    public string? Color { get; set; }
    public int UnicodeSkip { get; set; } = 1;
    public bool IsBullet { get; set; }
    public int BulletLevel { get; set; }
    public string? BulletStyle { get; set; }

    public RtfStyleState Clone()
    {
        return new RtfStyleState
        {
            Bold = this.Bold,
            Italic = this.Italic,
            Underline = this.Underline,
            FontSize = this.FontSize,
            Color = this.Color,
            UnicodeSkip = this.UnicodeSkip,
            IsBullet = this.IsBullet,
            BulletLevel = this.BulletLevel,
            BulletStyle = this.BulletStyle
        };
    }
}

public class RtfLexer
{
    private readonly string _input;
    private int _index = 0;

    public RtfLexer(string input)
    {
        _input = input;
    }

    public RtfToken? NextToken()
    {
        while (_index < _input.Length)
        {
            char c = _input[_index];
            if (c == '{')
            {
                _index++;
                return new RtfToken { Type = RtfTokenType.GroupStart };
            }
            if (c == '}')
            {
                _index++;
                return new RtfToken { Type = RtfTokenType.GroupEnd };
            }
            if (c == '\\')
            {
                _index++;
                if (_index >= _input.Length) return null;

                char next = _input[_index];
                if (next == '\\' || next == '{' || next == '}')
                {
                    _index++;
                    return new RtfToken { Type = RtfTokenType.Text, Name = next.ToString() };
                }
                if (next == '*')
                {
                    _index++;
                    return new RtfToken { Type = RtfTokenType.ControlWord, Name = "*" };
                }
                if (next == '\'')
                {
                    // Check if there is enough space for hex escape \'xx (which is '\'' followed by 2 characters)
                    if (_index + 2 < _input.Length)
                    {
                        string hex = _input.Substring(_index + 1, 2);
                        if (byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out byte b))
                        {
                            _index += 3; // consume '\'', and the 2 hex digits
                            return new RtfToken { Type = RtfTokenType.Text, Name = ((char)b).ToString() };
                        }
                    }
                    // Hex parsing failed: do not advance/consume any characters from the input stream.
                    // We only consumed '\\' initially, so we return "\\" as text, leaving _index at '\''.
                    return new RtfToken { Type = RtfTokenType.Text, Name = "\\" };
                }

                int start = _index;
                while (_index < _input.Length && char.IsLetter(_input[_index]))
                {
                    _index++;
                }
                string name = _input.Substring(start, _index - start);

                int? parameter = null;
                if (_index < _input.Length && (char.IsDigit(_input[_index]) || _input[_index] == '-'))
                {
                    bool isNegative = false;
                    if (_input[_index] == '-')
                    {
                        isNegative = true;
                        _index++;
                    }

                    int val = 0;
                    bool hasDigits = false;
                    while (_index < _input.Length && char.IsDigit(_input[_index]))
                    {
                        val = val * 10 + (_input[_index] - '0');
                        hasDigits = true;
                        _index++;
                    }

                    if (hasDigits)
                    {
                        parameter = isNegative ? -val : val;
                    }
                }

                if (_index < _input.Length && _input[_index] == ' ')
                {
                    _index++;
                }

                return new RtfToken { Type = RtfTokenType.ControlWord, Name = name, Parameter = parameter };
            }

            int textStart = _index;
            while (_index < _input.Length && _input[_index] != '{' && _input[_index] != '}' && _input[_index] != '\\')
            {
                _index++;
            }
            string text = _input.Substring(textStart, _index - textStart);
            text = text.Replace("\r", "").Replace("\n", "");
            if (text.Length > 0)
            {
                return new RtfToken { Type = RtfTokenType.Text, Name = text };
            }
            // if text.Length == 0 (e.g. it was just newlines/carriage returns), it loops again, avoiding recursion!
        }
        return null;
    }
}
