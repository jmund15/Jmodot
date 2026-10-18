namespace Jmodot.Implementation.AI.Navigation.Targets;

using System;
using Core.Shared;
using Godot;

/// <summary>
/// Decides the shape of a category target's sample from the members perception already judged
/// eligible. A policy holds no sample and no member list; it only transforms the caller's.
/// </summary>
[GlobalClass, Tool]
public abstract partial class TargetPolicy3D : Resource, IResourceConfigurationWarnings
{
    /// <summary>The sample shape this policy produces.</summary>
    public abstract TargetSampleGeometry Geometry { get; }

    /// <summary>
    /// Narrows <paramref name="sample"/>'s filled members in place and seals the sample with this
    /// policy's <see cref="Geometry"/>. Allocates nothing: it runs every steering tick.
    /// </summary>
    public abstract void Apply(TargetSample sample);

    public virtual string[] GetResourceConfigurationWarnings() => Array.Empty<string>();
}
