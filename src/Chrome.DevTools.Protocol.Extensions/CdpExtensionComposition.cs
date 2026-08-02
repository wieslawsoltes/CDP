namespace Chrome.DevTools.Protocol;

public static class CdpExtensionComposition
{
    public static void RegisterDefaultDomains() => CdpProfilingComposition.Register();
}
