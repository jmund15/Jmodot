namespace Jmodot.Implementation.Registry;

using Godot.Collections;

/// <summary>
///     A helper Resource that defines a set of other resources for collection purposes.
///     It supports both bulk-loading from directories and explicit inclusion/exclusion,
///     providing a scalable yet granular way to populate registries.
/// </summary>
[GlobalClass]
public partial class ResourceCollection : Resource
{
    /// <summary>
    ///     A list of directory paths to scan recursively for resources.
    /// </summary>
    [Export(PropertyHint.Dir)]
    public Array<string> ScanDirectories { get; private set; } = new();

    /// <summary>
    ///     An explicit list of resources to include. Useful for adding resources that
    ///     are not in the scanned directories.
    /// </summary>
    [Export]
    public Array<Resource> Include { get; private set; } = new();

    /// <summary>
    ///     An explicit list of resources to exclude from the final collection.
    ///     This is useful for temporarily disabling a resource that is located
    ///     within a scanned directory without having to move the file.
    /// </summary>
    [Export]
    public Array<Resource> Exclude { get; private set; } = new();
}
