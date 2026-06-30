using Content.Server.Administration;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._Sich.Nuke;

[ToolshedCommand, AdminCommand(AdminFlags.VarEdit)]
public sealed class MriyaNukeCommand : ToolshedCommand
{
    private static readonly EntProtoId ChargePrototype = "MriyaRMCNuclearCharge";

    [CommandImplementation("unlock")]
    public void Unlock(IInvocationContext context)
    {
        var intel = Sys<IntelSystem>();
        var tree = intel.EnsureTechTree();
        var changed = false;

        foreach (var tier in tree.Comp.Tree.Options)
        {
            for (var i = 0; i < tier.Count; i++)
            {
                var option = tier[i];
                if (!DeliversCharge(option))
                    continue;

                tier[i] = option with
                {
                    Disabled = false,
                    TimeLock = TimeSpan.Zero,
                };
                changed = true;
            }
        }

        if (!changed)
        {
            context.WriteLine("Mriya nuclear charge tech option was not found.");
            return;
        }

        EntityManager.Dirty(tree);
        intel.UpdateTree(tree);
        context.WriteLine("Mriya nuclear charge tech option unlocked for this round.");
    }

    private static bool DeliversCharge(TechOption option)
    {
        foreach (var ev in option.Events)
        {
            if (ev is TechLogisticsDeliveryEvent delivery &&
                delivery.Object == ChargePrototype)
            {
                return true;
            }
        }

        return false;
    }
}
