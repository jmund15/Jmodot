namespace Jmodot.Implementation.Registry;

using System.Collections.Generic;
using Core.AI.Affinities;
using Core.Identification;
using Core.Input;
using Core.Stats;
using Shared;
using GCol = Godot.Collections;

/// <summary>
///     A central, project-wide Resource that acts as a manifest for all core game-defining
///     semantic assets. It provides a "source of truth" for the framework, allowing programmers
///     to get type-safe, non-brittle references to fundamental Categories and InputActions.
/// </summary>
[GlobalClass]
public partial class GameRegistry : Resource
{
    /// <summary>
    ///     All affinities in the game, indexed by their AffinityName for fast lookup.
    ///     This is automatically populated from the Game GlobalRegistry.
    /// </summary>
    private Dictionary<StringName, Affinity> _affinityLookup = new();

    /// <summary>
    ///     All attributes in the game, indexed by their AttributeName for fast lookup.
    ///     This is automatically populated from the Game GlobalRegistry.
    /// </summary>
    private Dictionary<StringName, Attribute> _attributeLookup = new();

    /// <summary>
    ///     All categories in the game, indexed by their CategoryName for fast lookup.
    ///     This is automatically populated from the Game GlobalRegistry.
    /// </summary>
    private Dictionary<StringName, Category> _categoryLookup = new();

    /// <summary>
    ///     All identities in the game, indexed by their IdentityName for fast lookup.
    ///     This is automatically populated from the Game GlobalRegistry.
    /// </summary>
    private Dictionary<StringName, Identity> _identityLookup = new();

    /// <summary>
    ///     All input actions in the game, indexed by their ActionName for fast lookup.
    ///     This is automatically populated from the Game GlobalRegistry.
    /// </summary>
    private Dictionary<StringName, InputAction> _inputActionLookup = new();

    [ExportGroup("Resource Collectors")]
    [Export]
    public GCol.Array<ResourceCollection> IdentityCollection { get; private set; } = new();

    [Export] public GCol.Array<ResourceCollection> CategoryCollection { get; private set; } = new();
    [Export] public GCol.Array<ResourceCollection> InputActionCollection { get; private set; } = new();
    [Export] public GCol.Array<ResourceCollection> AttributeCollection { get; private set; } = new();
    [Export] public GCol.Array<ResourceCollection> AffinityCollection { get; private set; } = new();

    [ExportGroup("Loaded GlobalRegistry")]
    [Export]
    public GCol.Array<Identity> Identities { get; private set; } = new();

    [Export] public GCol.Array<Category> Categories { get; private set; } = new();
    [Export] public GCol.Array<InputAction> InputActions { get; private set; } = new();
    [Export] public GCol.Array<Attribute> Attributes { get; private set; } = new();
    [Export] public GCol.Array<Affinity> Affinities { get; private set; } = new();

    [ExportGroup("Core Semantic Categories")]
    [Export]
    public Category EnemyCategory { get; private set; } = null!;

    [Export] public Category FriendlyCategory { get; private set; } = null!;
    [Export] public Category ItemCategory { get; private set; } = null!;
    [Export] public Category ObjectiveCategory { get; private set; } = null!;
    [Export] public Category PlayerFactionCategory { get; private set; } = null!;

    [ExportGroup("Unique Core Identities")]
    [Export]
    public Identity PlayerIdentity { get; private set; } = null!;

    [ExportGroup("Core Input Actions")]
    [Export]
    public InputAction MoveAction { get; private set; } = null!;

    [Export] public InputAction JumpAction { get; private set; } = null!;

    [ExportGroup("Core Attributes")]
    [Export]
    public Attribute HealthAttr { get; private set; } = null!;

    [Export] public Attribute MaxSpeedAttr { get; private set; } = null!;
    [Export] public Attribute AccelerationAttr { get; private set; } = null!;
    [Export] public Attribute FrictionAttr { get; private set; } = null!;

    [ExportGroup("Core Affinities")]
    [Export]
    public Affinity FearAffinity { get; private set; } = null!;

    [Export] public Affinity AggressionAffinity { get; private set; } = null!;

    public void RebuildRegistry()
    {
        // Clear existing data
        Identities.Clear();
        Categories.Clear();
        InputActions.Clear();
        Attributes.Clear();
        Affinities.Clear();

        // Load resources from collections
        Identities = ProcessCollections<Identity>(IdentityCollection);
        Categories = ProcessCollections<Category>(CategoryCollection);
        InputActions = ProcessCollections<InputAction>(InputActionCollection);
        Attributes = ProcessCollections<Attribute>(AttributeCollection);
        Affinities = ProcessCollections<Affinity>(AffinityCollection);

        // Clear lookup dictionaries to force rebuild on next access
        _identityLookup = null;
        _categoryLookup = null;
        _inputActionLookup = null;
        _attributeLookup = null;
        _affinityLookup = null;
    }

    #region Public_API_Lookups

    public bool TryGetIdentity(StringName identityKey, out Identity? identity)
    {
        // This is the lazy-loading pattern. The dictionary is only built once,
        // the very first time an identity is requested.
        if (this._identityLookup == null)
        {
            this.BuildIdentityLookup();
        }

        return this._identityLookup!.TryGetValue(identityKey, out identity);
    }

    public Identity GetIdentity(StringName identityKey)
    {
        if (!this.TryGetIdentity(identityKey, out var identity))
        {
            throw JmoLogger.LogAndRethrow(
                new KeyNotFoundException($"Identity with key '{identityKey}' not found in GameRegistry."),
                this
            );
        }
        return identity!;
    }

    /// <summary>
    ///     Attempts to get a category by its key. The lookup dictionary is built on first access.
    /// </summary>
    public bool TryGetCategory(StringName categoryKey, out Category? category)
    {
        // This is the lazy-loading pattern. The dictionary is only built once,
        // the very first time a category is requested.
        if (this._categoryLookup == null)
        {
            this.BuildCategoryLookup();
        }

        return this._categoryLookup!.TryGetValue(categoryKey, out category);
    }

    /// <summary>
    ///     Asserts a category exists and gets by its key. The lookup dictionary is built on first access.
    /// </summary>
    public Category GetCategory(StringName categoryKey)
    {
        if (!this.TryGetCategory(categoryKey, out var category))
        {
            throw JmoLogger.LogAndRethrow(
                new KeyNotFoundException($"Category with key '{categoryKey}' not found in GameRegistry."),
                this
            );
        }
        return category!;
    }

    public bool TryGetInputAction(StringName actionKey, out InputAction? action)
    {
        // This is the lazy-loading pattern. The dictionary is only built once,
        // the very first time an input action is requested.
        if (this._inputActionLookup == null)
        {
            this.BuildInputActionLookup();
        }

        return this._inputActionLookup!.TryGetValue(actionKey, out action);
    }

    public InputAction GetInputAction(StringName actionKey)
    {
        if (!this.TryGetInputAction(actionKey, out var action))
        {
            throw JmoLogger.LogAndRethrow(
                new KeyNotFoundException($"InputAction with key '{actionKey}' not found in GameRegistry."),
                this
            );
        }
        return action!;
    }

    public bool TryGetAttribute(StringName attributeKey, out Attribute? attribute)
    {
        // This is the lazy-loading pattern. The dictionary is only built once,
        // the very first time an attribute is requested.
        if (this._attributeLookup == null)
        {
            this.BuildAttributeLookup();
        }

        return this._attributeLookup!.TryGetValue(attributeKey, out attribute);
    }

    public Attribute GetAttribute(StringName attributeKey)
    {
        if (!this.TryGetAttribute(attributeKey, out var attribute))
        {
            throw JmoLogger.LogAndRethrow(
                new KeyNotFoundException($"Attribute with key '{attributeKey}' not found in GameRegistry."),
                this
            );
        }
        return attribute!;
    }

    public bool TryGetAffinity(StringName affinityKey, out Affinity? affinity)
    {
        // This is the lazy-loading pattern. The dictionary is only built once,
        // the very first time an affinity is requested.
        if (this._affinityLookup == null)
        {
            this.BuildAffinityLookup();
        }

        return this._affinityLookup!.TryGetValue(affinityKey, out affinity);
    }

    public Affinity GetAffinity(StringName affinityKey)
    {
        if (!this.TryGetAffinity(affinityKey, out var affinity))
        {
            throw JmoLogger.LogAndRethrow(
                new KeyNotFoundException($"Affinity with key '{affinityKey}' not found in GameRegistry."),
                this
            );
        }
        return affinity!;
    }

    #endregion

    #region Lookup_Builders

    public void BuildIdentityLookup()
    {
        this._identityLookup = new Dictionary<StringName, Identity>();
        if (this.Identities == null)
        {
            return;
        }

        foreach (var identity in this.Identities)
        {
            if (identity != null && !string.IsNullOrEmpty(identity.IdentityName))
            {
                // This prevents crashes if a designer makes a duplicate.
                if (this._identityLookup.ContainsKey(identity.IdentityName))
                {
                    JmoLogger.Warning(this,
                        $"GameRegistry Error: Duplicate identity name '{identity.IdentityName}'. The first one found was kept.");
                    continue;
                }

                this._identityLookup[new StringName(identity.IdentityName)] = identity;
            }
        }
    }

    private void BuildCategoryLookup()
    {
        this._categoryLookup = new Dictionary<StringName, Category>();
        if (this.Categories == null)
        {
            return;
        }

        foreach (var category in this.Categories)
        {
            if (category != null && !string.IsNullOrEmpty(category.CategoryName))
            {
                // This prevents crashes if a designer makes a duplicate.
                if (this._categoryLookup.ContainsKey(category.CategoryName))
                {
                    JmoLogger.Warning(this,
                        $"GameRegistry Error: Duplicate category name '{category.CategoryName}'. The first one found was kept.");
                    continue;
                }

                this._categoryLookup[new StringName(category.CategoryName)] = category;
            }
        }
    }

    private void BuildInputActionLookup()
    {
        this._inputActionLookup = new Dictionary<StringName, InputAction>();
        if (this.InputActions == null)
        {
            return;
        }

        foreach (var action in this.InputActions)
        {
            if (action != null && !string.IsNullOrEmpty(action.ActionName))
            {
                // This prevents crashes if a designer makes a duplicate.
                if (this._inputActionLookup.ContainsKey(action.ActionName))
                {
                    JmoLogger.Warning(this,
                        $"GameRegistry Error: Duplicate input action name '{action.ActionName}'. The first one found was kept.");
                    continue;
                }

                this._inputActionLookup[new StringName(action.ActionName)] = action;
            }
        }
    }

    private void BuildAttributeLookup()
    {
        this._attributeLookup = new Dictionary<StringName, Attribute>();
        if (this.Attributes == null)
        {
            return;
        }

        foreach (var attr in this.Attributes)
        {
            if (attr != null && !string.IsNullOrEmpty(attr.AttributeName))
            {
                // This prevents crashes if a designer makes a duplicate.
                if (this._attributeLookup.ContainsKey(attr.AttributeName))
                {
                    JmoLogger.Warning(this,
                        $"GameRegistry Error: Duplicate attribute name '{attr.AttributeName}'. The first one found was kept.");
                    continue;
                }

                this._attributeLookup[new StringName(attr.AttributeName)] = attr;
            }
        }
    }

    private void BuildAffinityLookup()
    {
        this._affinityLookup = new Dictionary<StringName, Affinity>();
        if (this.Affinities == null)
        {
            return;
        }

        foreach (var affinity in this.Affinities)
        {
            if (affinity != null && !string.IsNullOrEmpty(affinity.AffinityName))
            {
                // This prevents crashes if a designer makes a duplicate.
                if (this._affinityLookup.ContainsKey(affinity.AffinityName))
                {
                    JmoLogger.Warning(this,
                        $"GameRegistry Error: Duplicate affinity name '{affinity.AffinityName}'. The first one found was kept.");
                    continue;
                }

                this._affinityLookup[new StringName(affinity.AffinityName)] = affinity;
            }
        }
    }

    #endregion

    #region Resource_Loading

    private GCol.Array<T> ProcessCollections<[MustBeVariant] T>(GCol.Array<ResourceCollection> collections)
        where T : Resource
    {
        // set for no duplicates
        HashSet<T> resultSet = new();


        foreach (var collection in collections)
        {
            foreach (var include in collection.Include)
            {
                if (include is T tRes)
                {
                    resultSet.Add(tRes);
                }
            }

            foreach (var dir in collection.ScanDirectories)
            {
                if (string.IsNullOrWhiteSpace(dir))
                {
                    continue;
                }

                this.LoadFromDirectory(dir, ref resultSet);
            }

            // optimize?
            foreach (var exclude in collection.Exclude)
            {
                if (exclude is T tRes)
                {
                    resultSet.Remove(tRes);
                }
            }
        }

        return new GCol.Array<T>(resultSet);
    }

    private void LoadFromDirectory<[MustBeVariant] T>(string directory, ref HashSet<T> resultSet) where T : Resource
    {
        using var dir = DirAccess.Open(directory);
        if (dir == null)
        {
            JmoLogger.Error(this, $"Failed to open directory: {directory}");
            return;
        }

        foreach (var subDir in dir.GetDirectories())
        {
            LoadFromDirectory(subDir, ref resultSet);
        }

        foreach (var resPath in ResourceLoader.ListDirectory(directory))
        {
            var res = ResourceLoader.Load($"{directory}/{resPath}");
            if (res is T tRes)
            {
                resultSet.Add(tRes);
            }
        }
    }

    #endregion
}
