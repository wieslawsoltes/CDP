using System;
using System.Collections.Generic;

namespace CdpInspectorApp.Services;

public enum DiffType
{
    Unchanged,
    Added,
    Deleted
}

public class DiffLine
{
    public string Text { get; set; } = "";
    public DiffType Type { get; set; }
    public int? LeftLineNumber { get; set; }
    public int? RightLineNumber { get; set; }
    
    // Character ranges (Offset, Length) indicating specific intra-line edits
    public List<(int Offset, int Length)> ChangeRanges { get; } = new();
}

public static class DiffEngine
{
    private static readonly string[] LineSeparators = new[] { "\r\n", "\n" };

    public static string[] SplitIntoLines(string text)
    {
        if (text == null) return Array.Empty<string>();
        return text.Split(LineSeparators, StringSplitOptions.None);
    }

    public static List<DiffLine> ComputeDiff(string leftText, string rightText)
    {
        if (string.IsNullOrEmpty(leftText) && string.IsNullOrEmpty(rightText))
        {
            return new List<DiffLine>();
        }

        string[] left = SplitIntoLines(leftText);
        string[] right = SplitIntoLines(rightText);

        // 1. Optimize: Trim common prefix and suffix
        int prefixCount = 0;
        while (prefixCount < left.Length && prefixCount < right.Length && left[prefixCount] == right[prefixCount])
        {
            prefixCount++;
        }

        int suffixCount = 0;
        while (suffixCount < left.Length - prefixCount && 
               suffixCount < right.Length - prefixCount && 
               left[left.Length - 1 - suffixCount] == right[right.Length - 1 - suffixCount])
        {
            suffixCount++;
        }

        // Extract active middle section
        int activeLeftLen = left.Length - prefixCount - suffixCount;
        int activeRightLen = right.Length - prefixCount - suffixCount;

        var activeLeft = new string[activeLeftLen];
        Array.Copy(left, prefixCount, activeLeft, 0, activeLeftLen);

        var activeRight = new string[activeRightLen];
        Array.Copy(right, prefixCount, activeRight, 0, activeRightLen);

        // 2. Run LCS on mapped integer IDs of active lines
        var activeDiff = ComputeDiffLcsHashed(activeLeft, activeRight, prefixCount);

        // 3. Assemble prefix, middle, suffix
        var result = new List<DiffLine>(left.Length + right.Length);

        // Prefix
        for (int i = 0; i < prefixCount; i++)
        {
            result.Add(new DiffLine
            {
                Text = left[i],
                Type = DiffType.Unchanged,
                LeftLineNumber = i + 1,
                RightLineNumber = i + 1
            });
        }

        // Active Middle
        result.AddRange(activeDiff);

        // Suffix
        for (int i = 0; i < suffixCount; i++)
        {
            int leftIdx = left.Length - suffixCount + i;
            int rightIdx = right.Length - suffixCount + i;
            result.Add(new DiffLine
            {
                Text = left[leftIdx],
                Type = DiffType.Unchanged,
                LeftLineNumber = leftIdx + 1,
                RightLineNumber = rightIdx + 1
            });
        }

        // 4. Compute intra-line details for adjacent deletions & additions
        for (int i = 0; i < result.Count - 1; i++)
        {
            var line1 = result[i];
            var line2 = result[i + 1];

            if (line1.Type == DiffType.Deleted && line2.Type == DiffType.Added)
            {
                var delRanges = ComputeCharDiff(line1.Text, line2.Text, isDeletion: true);
                var addRanges = ComputeCharDiff(line1.Text, line2.Text, isDeletion: false);

                line1.ChangeRanges.AddRange(delRanges);
                line2.ChangeRanges.AddRange(addRanges);
                i++; // skip next since we paired it
            }
        }

        return result;
    }

    private static List<DiffLine> ComputeDiffLcsHashed(string[] left, string[] right, int lineOffset)
    {
        // Map unique string lines to unique integer IDs for high performance
        var stringToId = new Dictionary<string, int>();
        int nextId = 0;

        int[] leftIds = new int[left.Length];
        for (int i = 0; i < left.Length; i++)
        {
            if (!stringToId.TryGetValue(left[i], out int id))
            {
                id = nextId++;
                stringToId[left[i]] = id;
            }
            leftIds[i] = id;
        }

        int[] rightIds = new int[right.Length];
        for (int i = 0; i < right.Length; i++)
        {
            if (!stringToId.TryGetValue(right[i], out int id))
            {
                id = nextId++;
                stringToId[right[i]] = id;
            }
            rightIds[i] = id;
        }

        // Standard LCS Dynamic Programming on integer IDs
        int[,] lcs = new int[leftIds.Length + 1, rightIds.Length + 1];
        for (int i = 0; i <= leftIds.Length; i++)
        {
            for (int j = 0; j <= rightIds.Length; j++)
            {
                if (i == 0 || j == 0)
                    lcs[i, j] = 0;
                else if (leftIds[i - 1] == rightIds[j - 1])
                    lcs[i, j] = lcs[i - 1, j - 1] + 1;
                else
                    lcs[i, j] = Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
            }
        }

        var result = new List<DiffLine>();
        int x = leftIds.Length;
        int y = rightIds.Length;

        while (x > 0 || y > 0)
        {
            if (x > 0 && y > 0 && leftIds[x - 1] == rightIds[y - 1])
            {
                result.Insert(0, new DiffLine
                {
                    Text = left[x - 1],
                    Type = DiffType.Unchanged,
                    LeftLineNumber = lineOffset + x,
                    RightLineNumber = lineOffset + y
                });
                x--;
                y--;
            }
            else if (y > 0 && (x == 0 || lcs[x, y - 1] >= lcs[x - 1, y]))
            {
                result.Insert(0, new DiffLine
                {
                    Text = right[y - 1],
                    Type = DiffType.Added,
                    RightLineNumber = lineOffset + y
                });
                y--;
            }
            else
            {
                result.Insert(0, new DiffLine
                {
                    Text = left[x - 1],
                    Type = DiffType.Deleted,
                    LeftLineNumber = lineOffset + x
                });
                x--;
            }
        }

        return result;
    }

    private static List<(int Offset, int Length)> ComputeCharDiff(string oldStr, string newStr, bool isDeletion)
    {
        var result = new List<(int Offset, int Length)>();
        if (string.IsNullOrEmpty(oldStr) || string.IsNullOrEmpty(newStr)) return result;

        int[,] lcs = new int[oldStr.Length + 1, newStr.Length + 1];
        for (int i = 0; i <= oldStr.Length; i++)
        {
            for (int j = 0; j <= newStr.Length; j++)
            {
                if (i == 0 || j == 0)
                    lcs[i, j] = 0;
                else if (oldStr[i - 1] == newStr[j - 1])
                    lcs[i, j] = lcs[i - 1, j - 1] + 1;
                else
                    lcs[i, j] = Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
            }
        }

        int x = oldStr.Length;
        int y = newStr.Length;

        var changes = new List<int>();

        while (x > 0 || y > 0)
        {
            if (x > 0 && y > 0 && oldStr[x - 1] == newStr[y - 1])
            {
                x--;
                y--;
            }
            else if (y > 0 && (x == 0 || lcs[x, y - 1] >= lcs[x - 1, y]))
            {
                if (!isDeletion) changes.Add(y - 1);
                y--;
            }
            else
            {
                if (isDeletion) changes.Add(x - 1);
                x--;
            }
        }

        changes.Reverse();

        int start = -1;
        int len = 0;
        foreach (var idx in changes)
        {
            if (start == -1)
            {
                start = idx;
                len = 1;
            }
            else if (idx == start + len)
            {
                len++;
            }
            else
            {
                result.Add((start, len));
                start = idx;
                len = 1;
            }
        }
        if (start != -1)
        {
            result.Add((start, len));
        }

        return result;
    }
}
