namespace CDP.Html.Renderer.Style;

public enum LengthUnit
{
    Auto,
    Px,
    Percent
}

public readonly struct CssLength
{
    public float Value { get; }
    public LengthUnit Unit { get; }

    public static CssLength Auto => new CssLength(0f, LengthUnit.Auto);
    public static CssLength Zero => new CssLength(0f, LengthUnit.Px);

    public bool IsAuto => Unit == LengthUnit.Auto;
    public bool IsPercent => Unit == LengthUnit.Percent;
    public bool IsPx => Unit == LengthUnit.Px;

    public CssLength(float value, LengthUnit unit)
    {
        Value = value;
        Unit = unit;
    }

    public float Resolve(float parentSize)
    {
        return Unit switch
        {
            LengthUnit.Percent => parentSize * (Value / 100f),
            LengthUnit.Px => Value,
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
            _ => $"{Value}px"
        };
    }
}
