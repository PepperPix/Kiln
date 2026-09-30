namespace Kiln.Abstractions;

/// <summary>
/// The environment a site build targets.
/// </summary>
public enum BuildEnvironment
{
    /// <summary>
    /// A development build. The output directory is updated in place instead of being deleted first
    /// (stale files are pruned afterwards), and the production asset pipeline is skipped.
    /// </summary>
    Development,

    /// <summary>
    /// A production build. The output directory is recreated and the asset pipeline runs
    /// (minification, fingerprinting and link check, each subject to the build options).
    /// </summary>
    Production,
}
