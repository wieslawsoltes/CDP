using System;
using System.Collections.Generic;

namespace CDP.Html.Renderer.Style;

public enum LengthUnit
{
    Auto,
    Px,
    Percent,
    Calc
}

public readonly struct CssLength
{
    public float Value { get; }
    public LengthUnit Unit { get; }
    public string? CalcExpression { get; }

    public static CssLength Auto => new CssLength(0f, LengthUnit.Auto);
    public static CssLength Zero => new CssLength(0f, LengthUnit.Px);

    public bool IsAuto => Unit == LengthUnit.Auto;
    public bool IsPercent => Unit == LengthUnit.Percent;
    public bool IsPx => Unit == LengthUnit.Px;
    public bool IsCalc => Unit == LengthUnit.Calc;

    public CssLength(float value, LengthUnit unit)
    {
        Value = value;
        Unit = unit;
        CalcExpression = null;
    }

    public CssLength(float value, LengthUnit unit, string? calcExpression)
    {
        Value = value;
        Unit = unit;
        CalcExpression = calcExpression;
    }

    public float Resolve(float parentSize)
    {
        return Unit switch
        {
            LengthUnit.Percent => parentSize * (Value / 100f),
            LengthUnit.Px => Value,
            LengthUnit.Calc => EvaluateCalc(CalcExpression, parentSize),
            _ => 0f
        };
    }

    public float ResolveWithDefault(float parentSize, float defaultValue)
    {
        if (IsAuto) return defaultValue;
        return Resolve(parentSize);
    }

    public override string ToString()
    {
        return Unit switch
        {
            LengthUnit.Auto => "auto",
            LengthUnit.Percent => $"{Value}%",
            LengthUnit.Calc => $"calc({CalcExpression})",
            _ => $"{Value}px"
        };
    }

    private enum TokenType
    {
        Number,
        Operator,
        LParen,
        RParen
    }

    private struct Token
    {
        public TokenType Type;
        public float Value;
        public LengthUnit Unit;
        public char Op;
    }

    private static List<Token> Tokenize(string expr, float parentSize)
    {
        var tokens = new List<Token>();
        int i = 0;
        int len = expr.Length;

        while (i < len)
        {
            char c = expr[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '(')
            {
                tokens.Add(new Token { Type = TokenType.LParen });
                i++;
                continue;
            }
            if (c == ')')
            {
                tokens.Add(new Token { Type = TokenType.RParen });
                i++;
                continue;
            }
            if (c == '+' || c == '-' || c == '*' || c == '/')
            {
                bool isSign = false;
                if (c == '+' || c == '-')
                {
                    if (tokens.Count == 0)
                    {
                        isSign = true;
                    }
                    else
                    {
                        var last = tokens[tokens.Count - 1].Type;
                        if (last == TokenType.Operator || last == TokenType.LParen)
                        {
                            isSign = true;
                        }
                    }
                }

                if (!isSign)
                {
                    tokens.Add(new Token { Type = TokenType.Operator, Op = c });
                    i++;
                    continue;
                }
            }

            int start = i;
            if (c == '+' || c == '-')
            {
                i++;
            }
            while (i < len && (char.IsDigit(expr[i]) || expr[i] == '.'))
            {
                i++;
            }

            if (i > start)
            {
                string numStr = expr.Substring(start, i - start);
                if (float.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
                {
                    LengthUnit unit = LengthUnit.Auto;
                    if (i < len && expr[i] == '%')
                    {
                        unit = LengthUnit.Percent;
                        i++;
                    }
                    else if (i + 1 < len && expr[i] == 'p' && expr[i + 1] == 'x')
                    {
                        unit = LengthUnit.Px;
                        i += 2;
                    }

                    tokens.Add(new Token { Type = TokenType.Number, Value = val, Unit = unit });
                }
                else
                {
                    i++;
                }
            }
            else
            {
                i++;
            }
        }

        return tokens;
    }

    private static float EvaluateCalc(string? expression, float parentSize)
    {
        if (string.IsNullOrEmpty(expression))
            return 0f;

        try
        {
            var tokens = Tokenize(expression, parentSize);
            if (tokens.Count == 0)
                return 0f;

            int index = 0;
            return ParseExpression(tokens, ref index, parentSize);
        }
        catch
        {
            return 0f;
        }
    }

    private static float ParseExpression(List<Token> tokens, ref int index, float parentSize)
    {
        float val = ParseTerm(tokens, ref index, parentSize);
        while (index < tokens.Count)
        {
            var tok = tokens[index];
            if (tok.Type == TokenType.Operator && (tok.Op == '+' || tok.Op == '-'))
            {
                index++;
                float nextVal = ParseTerm(tokens, ref index, parentSize);
                if (tok.Op == '+')
                    val += nextVal;
                else
                    val -= nextVal;
            }
            else
            {
                break;
            }
        }
        return val;
    }

    private static float ParseTerm(List<Token> tokens, ref int index, float parentSize)
    {
        float val = ParseFactor(tokens, ref index, parentSize);
        while (index < tokens.Count)
        {
            var tok = tokens[index];
            if (tok.Type == TokenType.Operator && (tok.Op == '*' || tok.Op == '/'))
            {
                index++;
                float nextVal = ParseFactor(tokens, ref index, parentSize);
                if (tok.Op == '*')
                    val *= nextVal;
                else
                {
                    if (nextVal != 0f)
                        val /= nextVal;
                    else
                        val = 0f;
                }
            }
            else
            {
                break;
            }
        }
        return val;
    }

    private static float ParseFactor(List<Token> tokens, ref int index, float parentSize)
    {
        if (index >= tokens.Count)
            return 0f;

        var tok = tokens[index];
        if (tok.Type == TokenType.Number)
        {
            index++;
            return tok.Unit switch
            {
                LengthUnit.Percent => parentSize * (tok.Value / 100f),
                LengthUnit.Px => tok.Value,
                _ => tok.Value
            };
        }
        else if (tok.Type == TokenType.LParen)
        {
            index++;
            float val = ParseExpression(tokens, ref index, parentSize);
            if (index < tokens.Count && tokens[index].Type == TokenType.RParen)
            {
                index++;
            }
            return val;
        }

        index++;
        return 0f;
    }
}
