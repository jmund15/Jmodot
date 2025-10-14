namespace Jmodot.Implementation.AI.BB;

/// <summary>
///     Provides a centralized and performant registry of StringName keys for use with the Blackboard system.
///     Using a static registry combines the performance benefits of StringName with the discoverability and
///     typo-prevention of enums.
/// </summary>
/// <remarks>Use partial to allow for extensions for each new Godot Project.</remarks>
public static partial class BBDataSig
{
    #region CORE_PROPERTIES

    public static readonly StringName Agent = new("Agent");
    public static readonly StringName Sprite = new("Sprite");
    public static readonly StringName Anim = new("Anim");
    public static readonly StringName CurrentTarget = new("CurrentTarget");
    public static readonly StringName CharacterController = new("CharacterController");
    public static readonly StringName MovementProcessor = new("MovementProcessor");
    public static readonly StringName MoveComp = new("MoveComp");
    public static readonly StringName VelComp = new("VelComp");
    public static readonly StringName HealthComp = new("HealthComp");
    public static readonly StringName HurtboxComp = new("HurtboxComp");
    public static readonly StringName HitboxComp = new("HitboxComp");
    public static readonly StringName AINavComp = new("AINavComp");
    public static readonly StringName DetectComp = new("DetectComp");
    public static readonly StringName SquadComp = new("SquadComp");
    public static readonly StringName Affinities = new("Affinities");
    public static readonly StringName CombatComp = new("CombatComp");
    public static readonly StringName MovementSM = new("MovementSM");
    public static readonly StringName AISM = new("AISM");
    public static readonly StringName QueuedNextAttack = new("QueuedNextAttack");
    public static readonly StringName SelfInteruptible = new("SelfInteruptible");

    #endregion

    #region ROBBER_PROPERTIES

    public static readonly StringName RobberBag = new("RobberBag");
    public static readonly StringName RobberEffects = new("RobberEffects");

    #endregion

    #region RAMPAGE_PROPERTIES

    public static readonly StringName ClimberComp = new("ClimberComp");
    public static readonly StringName CurrentAttackType = new("CurrentAttackType");
    public static readonly StringName GroundNormalAttack = new("GroundNormalAttack");
    public static readonly StringName GroundSpecialAttack = new("GroundSpecialAttack");
    public static readonly StringName WallNormalAttack = new("WallNormalAttack");
    public static readonly StringName WallSpecialAttack = new("WallSpecialAttack");
    public static readonly StringName EaterComp = new("EaterComp");
    public static readonly StringName EatableComp = new("EatableComp");
    public static readonly StringName OccupantComp = new("OccupantComp");
    public static readonly StringName JumpsLeft = new("JumpsLeft");

    #endregion

    #region AI

    public static readonly StringName PerceptionComp = new("PerceptionComponent");

    #region AI_PROPERTIES

    public static readonly StringName OwnedVehicle = new("OwnedVehicle");
    public static readonly StringName TargetOrOccupiedVehicleSeat = new("TargetOrOccupiedVehicleSeat");
    public static readonly StringName TargetOrOccupiedVehicle = new("TargetOrOccupiedVehicle");

    #endregion

    #endregion

    #region SQUAD_PROPERTIES

    public static readonly StringName ActiveSquadTag = new("ActiveSquadTag");
    public static readonly StringName HasSquadTag = new("HasSquadTag");
    public static readonly StringName SquadAverageHealth = new("SquadAverageHealth");

    #endregion
}
