using Robust.Shared.Serialization;

namespace Content.Shared._Sich.Nuke;

[Serializable, NetSerializable]
public enum MriyaRMCNuclearChargeUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class MriyaRMCNuclearChargeBuiState : BoundUserInterfaceState
{
    public readonly string Status;
    public readonly bool CanStart;
    public readonly bool CanAbort;

    public MriyaRMCNuclearChargeBuiState(string status, bool canStart, bool canAbort)
    {
        Status = status;
        CanStart = canStart;
        CanAbort = canAbort;
    }
}

[Serializable, NetSerializable]
public sealed class MriyaRMCNuclearChargeStartBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class MriyaRMCNuclearChargeAbortBuiMsg : BoundUserInterfaceMessage;
