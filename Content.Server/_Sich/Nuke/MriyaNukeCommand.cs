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
        if (!UnlockTechOption(context))
            return;

        AuthorizeCharge(context);
        context.WriteLine("Mriya nuclear charge tech option and authorization unlocked for this round.");
    }

    [CommandImplementation("unlocktech")]
    public void UnlockTech(IInvocationContext context)
    {
        if (UnlockTechOption(context))
            context.WriteLine("Mriya nuclear charge tech option unlocked for this round. Decryption authorization is unchanged.");
    }

    [CommandImplementation("decrypt")]
    public void Decrypt(IInvocationContext context)
    {
        AuthorizeCharge(context);
        context.WriteLine("Mriya nuclear charge decryption authorization completed for this round. Tech option lock is unchanged.");
    }

    private bool UnlockTechOption(IInvocationContext context)
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
            return false;
        }

        EntityManager.Dirty(tree);
        intel.UpdateTree(tree);
        return true;
    }

    private void AuthorizeCharge(IInvocationContext context)
    {
        var intel = Sys<IntelSystem>();
        var tree = intel.EnsureTechTree();
        var objective = EntityManager.EnsureComponent<MriyaIntelNukeObjectiveComponent>(tree.Owner);
        objective.Stage = MriyaIntelNukeStage.ChargeAuthorized;
        objective.DecodeProgress = objective.DecodeDuration;
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
