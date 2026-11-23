using System.Collections.Generic;
using Avalonia;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Gml.Launcher.ViewModels.Components;

/// <summary>A dependency shown inside the card of the mod that needs it; <see cref="Mod" /> is shared state.</summary>
public sealed record DependencyRow(OptionalModItemViewModel Mod, Thickness Indent);

/// <summary>An optional mod on the mods page.</summary>
public sealed class OptionalModItemViewModel(string key, string title, string description) : ReactiveObject
{
    /// <summary>The enabled jar file name, e.g. <c>sodium-optional-mod.jar</c>.</summary>
    public string Key { get; } = key;

    public string Title { get; } = title;

    public string Description { get; } = description;

    /// <summary>Everything turned on together with this mod, as a tree; set once before the page shows it.</summary>
    public IReadOnlyList<DependencyRow> Dependencies { get; set; } = [];

    public bool HasDependencies => Dependencies.Count > 0;

    [Reactive] public bool IsSelected { get; set; }

    /// <summary>An enabled mod needs this one, so the player cannot turn it off.</summary>
    [Reactive] public bool IsLocked { get; set; }

    [Reactive] public bool CanToggle { get; set; } = true;

    /// <summary>Which enabled mods need this one; empty when none.</summary>
    [Reactive] public string NeededByText { get; set; } = string.Empty;

    [Reactive] public bool HasNeededBy { get; set; }
}
