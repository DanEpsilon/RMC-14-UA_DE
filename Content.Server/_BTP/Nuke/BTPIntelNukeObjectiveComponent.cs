using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._BTP.Nuke;

[RegisterComponent, Access(typeof(BTPIntelNukeSystem))]
public sealed partial class BTPIntelNukeObjectiveComponent : Component
{
    /// <summary>
    /// Amount of earned intelligence points required before the nuclear charge authorization sequence can begin.
    /// </summary>
    [DataField]
    public FixedPoint2 RequiredIntelPoints = FixedPoint2.New(8);

    /// <summary>
    /// Number of marine-controlled communication towers required to progress decryption.
    /// </summary>
    [DataField]
    public int RequiredTowers = 2;

    /// <summary>
    /// Total time the towers must remain controlled to unlock the nuclear charge purchase.
    /// </summary>
    [DataField]
    public TimeSpan DecodeDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Prototype delivered by the authorized tech option.
    /// </summary>
    [DataField]
    public EntProtoId ChargePrototype = "BTPRMCNuclearCharge";

    /// <summary>
    /// Current authorization state for the nuclear objective.
    /// </summary>
    public BTPIntelNukeStage Stage = BTPIntelNukeStage.WaitingForIntel;

    /// <summary>
    /// Accumulated time spent decrypting while enough towers are controlled.
    /// </summary>
    public TimeSpan DecodeProgress;

    /// <summary>
    /// Last game time used to advance the objective.
    /// </summary>
    public TimeSpan LastUpdatedAt;

    /// <summary>
    /// Next time a tower status reminder may be announced.
    /// </summary>
    public TimeSpan NextTowerStatusAt;

    /// <summary>
    /// Last announced number of active towers, used to avoid repeating status messages.
    /// </summary>
    public int LastReportedActiveTowers = -1;

    /// <summary>
    /// Countdown thresholds that have already produced a decryption progress announcement.
    /// </summary>
    public readonly HashSet<int> DecodeAnnouncedAtSeconds = new();
}

public enum BTPIntelNukeStage
{
    WaitingForIntel,
    WaitingForTowers,
    Decoding,
    ChargeAuthorized,
}
