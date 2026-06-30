using Robust.Shared.GameStates;

namespace Content.Shared._Sich.Nuke;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(MriyaRMCNuclearChargeSharedSystem))]
public sealed partial class MriyaRMCNuclearChargeMarkerComponent : Component
{
    /// <summary>
    /// Item slot used to hold the nuclear authentication disk.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string DiskSlotId = "mriya-rmc-nuke-disk";
}
