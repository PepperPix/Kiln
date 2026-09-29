namespace Kiln.Services;

using Scriban;

/// <summary>
/// Creates Scriban contexts for trusted theme and plugin templates without the default safety limits.
/// </summary>
internal static class TemplateContextFactory
{
    public static TemplateContext Create() => new()
    {
        // Scriban 7.4: 0 means unlimited; the defaults (1000 iterations, 1 MiB per string) throw or silently truncate.
        LoopLimit = 0,
        LimitToString = 0,
    };
}
