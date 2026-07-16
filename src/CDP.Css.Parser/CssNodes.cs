using System;
using System.Collections.Generic;

namespace CDP.Css.Parser;

public readonly struct Specificity : IComparable<Specificity>
{
    public int IdCount { get; }
    public int ClassCount { get; }
    public int TagCount { get; }

    public Specificity(int idCount, int classCount, int tagCount)
    {
        IdCount = idCount;
        ClassCount = classCount;
        TagCount = tagCount;
    }

    public int CompareTo(Specificity other)
    {
        int idCompare = IdCount.CompareTo(other.IdCount);
        if (idCompare != 0) return idCompare;

        int classCompare = ClassCount.CompareTo(other.ClassCount);
        if (classCompare != 0) return classCompare;

        return TagCount.CompareTo(other.TagCount);
    }

    public override string ToString() => $"({IdCount}, {ClassCount}, {TagCount})";

    public override bool Equals(object? obj) => obj is Specificity other && Equals(other);
    public bool Equals(Specificity other) => IdCount == other.IdCount && ClassCount == other.ClassCount && TagCount == other.TagCount;
    public override int GetHashCode() => HashCode.Combine(IdCount, ClassCount, TagCount);

    public static bool operator ==(Specificity left, Specificity right) => left.Equals(right);
    public static bool operator !=(Specificity left, Specificity right) => !left.Equals(right);
    public static bool operator <(Specificity left, Specificity right) => left.CompareTo(right) < 0;
    public static bool operator >(Specificity left, Specificity right) => left.CompareTo(right) > 0;
    public static bool operator <=(Specificity left, Specificity right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Specificity left, Specificity right) => left.CompareTo(right) >= 0;
}

public class CssSelector
{
    public string Text { get; set; } = string.Empty;
    public string? TagName { get; set; }
    
    private List<string>? _classes;
    public List<string> Classes => _classes ??= new();
    
    public string? Id { get; set; }
    
    private List<string>? _pseudoClasses;
    public List<string> PseudoClasses => _pseudoClasses ??= new(); // Track pseudo-classes/elements

    // For hierarchy/descendants:
    public CssSelector? ParentSelector { get; set; }
    public string? Combinator { get; set; } // " " (descendant), ">" (child)

    public Specificity Specificity { get; set; }
}

public class CssRule
{
    public List<CssSelector> Selectors { get; } = new();
    public Dictionary<string, string> Declarations { get; } = new(System.StringComparer.OrdinalIgnoreCase);
    public string? MediaCondition { get; set; }
}

public class CssStyleSheet
{
    public List<CssRule> Rules { get; } = new();
}
