using Content.Server._RMC14.Announce;
using Content.Server._RMC14.Explosion;
using Content.Server.RoundEnd;
using Content.Shared.Access.Systems;
using Content.Shared._Sich.Nuke;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Construction.Components;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction;
using Content.Shared.Nuke;
using Content.Shared.Projectiles;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Sich.Nuke;

public sealed partial class MriyaRMCNuclearChargeSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCExplosionSystem _rmcExplosion = default!;
    [Dependency] private readonly MriyaRMCNukeSystem _mriyaNuke = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoAnnounceSystem _xenoAnnounce = default!;

    private static readonly int[] AnnouncementThresholds = [300, 180, 60, 30, 10];
    private static readonly TimeSpan ThemeLeadTime = TimeSpan.FromSeconds(46);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, ItemSlotEjectAttemptEvent>(OnItemSlotEjectAttempt);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, MriyaNukeActivateDoAfterEvent>(OnActivateDoAfter);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, UnanchorAttemptEvent>(OnUnanchorAttempt);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, EntityTerminatingEvent>(OnTerminating);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MriyaRMCNuclearChargeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var charge, out var xform))
        {
            if (charge.Destroyed)
            {
                QueueDel(uid);
                continue;
            }

            if (charge.Detonated)
            {
                if (_timing.CurTime < charge.NukeMapAt)
                    continue;

                _mriyaNuke.NukeMap(xform.MapID);
                EndRoundMinorMarineVictory();
                QueueDel(uid);
                continue;
            }

            if (!charge.Armed)
                continue;

            var remaining = charge.DetonatesAt - _timing.CurTime;
            foreach (var threshold in AnnouncementThresholds)
            {
                if (remaining.TotalSeconds > threshold ||
                    !charge.AnnouncedAtSeconds.Add(threshold))
                {
                    continue;
                }

                Announce(Loc.GetString("mriya-nuke-detonation-countdown", ("remaining", FormatRemaining(threshold))));
                AnnounceXenos(Loc.GetString("mriya-nuke-xeno-detonation-countdown", ("remaining", FormatRemainingUkrainian(threshold))));

                if (threshold == 180)
                    StartWarningSiren(charge, xform.MapID);
            }

            if (!charge.ThemeStarted && remaining <= ThemeLeadTime)
            {
                charge.ThemeStarted = true;
                charge.WarheadThemeStream = _audio.PlayGlobal(charge.WarheadThemeSound, Filter.Broadcast(), true, charge.WarheadThemeSound.Params)?.Entity;
            }

            if (remaining > TimeSpan.Zero)
                continue;

            StartDetonation(uid, charge, xform);
        }
    }

    private void OnExamined(Entity<MriyaRMCNuclearChargeComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Armed)
        {
            var remaining = ent.Comp.DetonatesAt - _timing.CurTime;
            args.PushMarkup(Loc.GetString("mriya-nuke-examine-armed", ("remaining", FormatRemaining(Math.Max(0, (int) remaining.TotalSeconds)))));
            return;
        }

        if (ent.Comp.Activating)
        {
            args.PushMarkup(Loc.GetString("mriya-nuke-examine-activating"));
            return;
        }

        if (HasAuthenticationDisk(ent))
            args.PushMarkup(Loc.GetString("mriya-nuke-examine-disk-inserted"));
        else
            args.PushMarkup(Loc.GetString("mriya-nuke-examine-ready"));
    }

    private void OnItemSlotInsertAttempt(Entity<MriyaRMCNuclearChargeComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID != ent.Comp.DiskSlotId ||
            !HasComp<NukeDiskComponent>(args.Item) ||
            args.User == null)
        {
            return;
        }

        if (ent.Comp.Armed || ent.Comp.Detonated || ent.Comp.Activating)
        {
            args.Cancelled = true;
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-port-locked"), ent, args.User.Value, PopupType.MediumCaution);
            return;
        }

        if (!Transform(ent).Anchored)
        {
            args.Cancelled = true;
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-anchor-before-disk"), ent, args.User.Value, PopupType.MediumCaution);
            return;
        }

        if (!_access.IsAllowed(args.User.Value, ent))
        {
            args.Cancelled = true;
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-officer-disk-required"), ent, args.User.Value, PopupType.MediumCaution);
        }
    }

    private void OnItemSlotEjectAttempt(Entity<MriyaRMCNuclearChargeComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Slot.ID != ent.Comp.DiskSlotId)
            return;

        if (ent.Comp.Armed || ent.Comp.Detonated || ent.Comp.Activating)
        {
            args.Cancelled = true;
            if (args.User != null)
                _popup.PopupClient(Loc.GetString("mriya-nuke-popup-port-locked"), ent, args.User.Value, PopupType.MediumCaution);
        }
    }

    private void OnInteractHand(Entity<MriyaRMCNuclearChargeComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Armed)
        {
            var remaining = ent.Comp.DetonatesAt - _timing.CurTime;
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-already-armed", ("remaining", FormatRemaining(Math.Max(0, (int) remaining.TotalSeconds)))), ent, args.User, PopupType.LargeCaution);
            return;
        }

        if (ent.Comp.Activating)
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-already-activating"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (!_access.IsAllowed(args.User, ent))
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-officer-activation-required"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (!Transform(ent).Anchored)
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-anchor-before-activation"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (!HasAuthenticationDisk(ent))
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-disk-before-activation"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        var ev = new MriyaNukeActivateDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.ActivationDelay, ev, ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        ent.Comp.Activating = true;
        _popup.PopupClient(Loc.GetString("mriya-nuke-popup-activation-started"), ent, args.User, PopupType.LargeCaution);
    }

    private void OnActivateDoAfter(Entity<MriyaRMCNuclearChargeComponent> ent, ref MriyaNukeActivateDoAfterEvent args)
    {
        ent.Comp.Activating = false;

        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled)
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-activation-interrupted"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (ent.Comp.Armed || ent.Comp.Detonated)
            return;

        if (!Transform(ent).Anchored || !HasAuthenticationDisk(ent))
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-final-check-failed"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        ent.Comp.Armed = true;
        ent.Comp.DetonatesAt = _timing.CurTime + ent.Comp.DetonationDelay;
        var seconds = Math.Max(0, (int) ent.Comp.DetonationDelay.TotalSeconds);
        Announce(Loc.GetString("mriya-nuke-armed", ("remaining", FormatRemaining(seconds))));
        AnnounceXenos(Loc.GetString("mriya-nuke-xeno-armed", ("remaining", FormatRemainingUkrainian(seconds))));
    }

    private void OnUnanchorAttempt(Entity<MriyaRMCNuclearChargeComponent> ent, ref UnanchorAttemptEvent args)
    {
        if (!ent.Comp.Armed)
            return;

        args.Cancel();
        _popup.PopupClient(Loc.GetString("mriya-nuke-popup-armed-anchor-locked"), ent, args.User, PopupType.LargeCaution);
    }

    private void OnBeforeDamageChanged(Entity<MriyaRMCNuclearChargeComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (HasComp<ProjectileComponent>(args.Source) ||
            HasComp<XenoProjectileComponent>(args.Source))
        {
            args.Cancelled = true;
            return;
        }

        if (ent.Comp.Detonated || ent.Comp.Destroyed ||
            !TryComp(ent, out DamageableComponent? damageable))
        {
            return;
        }

        var currentDamage = damageable.TotalDamage.Float();
        var incomingDamage = args.Damage.GetTotal().Float();
        if (currentDamage < ent.Comp.DisableDamage &&
            (incomingDamage <= 0 || currentDamage + incomingDamage < ent.Comp.DisableDamage))
        {
            return;
        }

        args.Cancelled = true;
        DefuseDestroyedCharge(ent);
    }

    private void OnDamageChanged(Entity<MriyaRMCNuclearChargeComponent> ent, ref DamageChangedEvent args)
    {
        if (args.Damageable.TotalDamage.Float() < ent.Comp.DisableDamage)
            return;

        DefuseDestroyedCharge(ent);
    }

    private void OnDestroyed(Entity<MriyaRMCNuclearChargeComponent> ent, ref DestructionEventArgs args)
    {
        DefuseDestroyedCharge(ent);
    }

    private void OnTerminating(Entity<MriyaRMCNuclearChargeComponent> ent, ref EntityTerminatingEvent args)
    {
        StopWarningSiren(ent.Comp);
        StopWarheadTheme(ent.Comp);
    }

    private void DefuseDestroyedCharge(Entity<MriyaRMCNuclearChargeComponent> ent)
    {
        if (ent.Comp.Detonated || ent.Comp.Destroyed)
            return;

        ent.Comp.Destroyed = true;
        ent.Comp.Armed = false;
        ent.Comp.Activating = false;
        StopWarningSiren(ent.Comp);
        StopWarheadTheme(ent.Comp);
        Announce(Loc.GetString("mriya-nuke-defused"));
        AnnounceXenos(Loc.GetString("mriya-nuke-xeno-defused"));
        QueueDel(ent);
    }

    private string FormatRemaining(int seconds)
    {
        if (seconds >= 60)
        {
            var minutes = (int) Math.Ceiling(seconds / 60f);
            return Loc.GetString(minutes == 1 ? "mriya-nuke-time-minute" : "mriya-nuke-time-minutes", ("minutes", minutes));
        }

        return Loc.GetString(seconds == 1 ? "mriya-nuke-time-second" : "mriya-nuke-time-seconds", ("seconds", seconds));
    }

    private string FormatRemainingUkrainian(int seconds)
    {
        if (seconds >= 60)
        {
            var minutes = (int) Math.Ceiling(seconds / 60f);
            return Loc.GetString("mriya-nuke-time-ukrainian", ("value", minutes), ("unit", GetUkrainianPlural(minutes, "mriya-nuke-time-ukrainian-minute-one", "mriya-nuke-time-ukrainian-minute-few", "mriya-nuke-time-ukrainian-minute-many")));
        }

        return Loc.GetString("mriya-nuke-time-ukrainian", ("value", seconds), ("unit", GetUkrainianPlural(seconds, "mriya-nuke-time-ukrainian-second-one", "mriya-nuke-time-ukrainian-second-few", "mriya-nuke-time-ukrainian-second-many")));
    }

    private string GetUkrainianPlural(int value, string oneKey, string fewKey, string manyKey)
    {
        var mod100 = value % 100;
        if (mod100 is >= 11 and <= 14)
            return Loc.GetString(manyKey);

        var key = (value % 10) switch
        {
            1 => oneKey,
            >= 2 and <= 4 => fewKey,
            _ => manyKey,
        };
        return Loc.GetString(key);
    }

    private void StartDetonation(EntityUid uid, MriyaRMCNuclearChargeComponent charge, TransformComponent xform)
    {
        charge.Detonated = true;
        charge.NukeMapAt = _timing.CurTime + charge.MapKillDelay;

        var coordinates = _transform.GetMapCoordinates(uid, xform);
        Announce(Loc.GetString("mriya-nuke-detonated"));
        AnnounceXenos(Loc.GetString("mriya-nuke-xeno-detonated"));

        StopWarningSiren(charge);
        StopWarheadTheme(charge);
        _audio.PlayGlobal(charge.MapExplosionSound, Filter.BroadcastMap(coordinates.MapId), true);
        _audio.PlayGlobal(charge.FlybyExplosionSound, GetAwayFromMapFilter(coordinates.MapId), true);
        _mriyaNuke.NukeMap(coordinates.MapId);

        _rmcExplosion.QueueExplosion(
            coordinates,
            charge.ExplosionType,
            charge.ExplosionTotalIntensity,
            charge.ExplosionSlope,
            charge.ExplosionMaxTileIntensity,
            uid,
            tileBreakScale: 1,
            maxTileBreak: int.MaxValue,
            canCreateVacuum: false);
    }

    private void StartWarningSiren(MriyaRMCNuclearChargeComponent charge, MapId mapId)
    {
        if (charge.WarningSirenStream != null)
            return;

        StopWarningSiren(charge);
        charge.WarningSirenStream = _audio.PlayGlobal(charge.ThirtySecondWarningSound, Filter.BroadcastMap(mapId), true, charge.ThirtySecondWarningSound.Params)?.Entity;
    }

    private void StopWarningSiren(MriyaRMCNuclearChargeComponent charge)
    {
        charge.WarningSirenStream = _audio.Stop(charge.WarningSirenStream);
    }

    private void StopWarheadTheme(MriyaRMCNuclearChargeComponent charge)
    {
        charge.WarheadThemeStream = _audio.Stop(charge.WarheadThemeStream);
    }

    private bool HasAuthenticationDisk(Entity<MriyaRMCNuclearChargeComponent> ent)
    {
        return _itemSlots.TryGetSlot(ent.Owner, ent.Comp.DiskSlotId, out var slot) &&
               slot.HasItem;
    }

    private void Announce(string message)
    {
        _marineAnnounce.AnnounceARESStaging(null, message);
    }

    private void AnnounceXenos(string message)
    {
        _xenoAnnounce.AnnounceQueenMother(message);
    }

    private Filter GetAwayFromMapFilter(MapId mapId)
    {
        return Filter.Empty().AddWhereAttachedEntity(ent => IsAwayFromMap(ent, mapId));
    }

    private bool IsAwayFromMap(EntityUid ent, MapId mapId)
    {
        return TryComp(ent, out TransformComponent? xform) &&
               xform.MapID != mapId;
    }

    private void EndRoundMinorMarineVictory()
    {
        var ended = false;
        var query = EntityQueryEnumerator<CMDistressSignalRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var distress, out _))
        {
            if (distress.Result is not null and not DistressSignalRuleResult.None)
                continue;

            distress.Result = DistressSignalRuleResult.MinorMarineVictory;
            Dirty(uid, distress);
            ended = true;
        }

        if (ended)
            _roundEnd.EndRound();
    }
}
