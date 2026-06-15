using Robust.Shared.Audio;

namespace Content.Server._BTP.Nuke;

[RegisterComponent, Access(typeof(BTPRMCNuclearChargeSystem))]
public sealed partial class BTPRMCNuclearChargeComponent : Component
{
    /// <summary>
    /// Слот предмета, що використовується для зберігання диска ядерної автентифікації.
    /// </summary>
    [DataField]
    public string DiskSlotId = "btp-rmc-nuke-disk";

    /// <summary>
    /// Час, необхідний авторизованому користувачеві для завершення послідовності активації.
    /// </summary>
    [DataField]
    public TimeSpan ActivationDelay = TimeSpan.FromSeconds(12);

    /// <summary>
    /// Тривалість зворотного відліку після активації заряду.
    /// </summary>
    [DataField]
    public TimeSpan DetonationDelay = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Затримка між початком візуальної детонації та ядерним очищенням усієї карти.
    /// </summary>
    [DataField]
    public TimeSpan MapKillDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Сирена зациклювалася на ураженій карті після трихвилинного попередження.
    /// </summary>
    [DataField]
    public SoundSpecifier ThirtySecondWarningSound = new SoundPathSpecifier("/Audio/_BTP/Nuke/30sec_nuke_warning.ogg", AudioParams.Default.WithVolume(-1).WithLoop(true));

    /// <summary>
    /// Глобальна музична репліка заграла незадовго до детонації.
    /// </summary>
    [DataField]
    public SoundSpecifier WarheadThemeSound = new SoundPathSpecifier("/Audio/_BTP/Nuke/warhead_theme.ogg", AudioParams.Default.WithVolume(0));

    /// <summary>
    /// Звук вибуху, чутний об'єктами на ураженій карті.
    /// </summary>
    [DataField]
    public SoundSpecifier MapExplosionSound = new SoundPathSpecifier("/Audio/_BTP/Nuke/Nuke_explosion_map_sound.ogg", AudioParams.Default.WithVolume(2));

    /// <summary>
    /// Звук вибуху, що пролітає, чують сутності, що знаходяться далеко від ураженої карти.
    /// </summary>
    [DataField]
    public SoundSpecifier FlybyExplosionSound = new SoundPathSpecifier("/Audio/_BTP/Nuke/Alamo_Flyby_Nukesoundeffect.ogg", AudioParams.Default.WithVolume(-1));

    /// <summary>
    /// Прототип вибуху, використаний для візуальної вибухової хвилі.
    /// </summary>
    [DataField]
    public string ExplosionType = "BTPNuke";

    /// <summary>
    /// Загальна візуальна інтенсивність вибуху.
    /// </summary>
    [DataField]
    public float ExplosionTotalIntensity = 80000000;

    /// <summary>
    /// Візуальний вибух, що призводить до falloff.
    /// </summary>
    [DataField]
    public float ExplosionSlope = 25;

    /// <summary>
    /// Максимальна інтенсивність візуального вибуху на плитку.
    /// </summary>
    [DataField]
    public float ExplosionMaxTileIntensity = 400;

    /// <summary>
    /// Поріг пошкодження, при якому фізична шкода знешкоджує та знищує заряд.
    /// </summary>
    [DataField]
    public float DisableDamage = 350;

    /// <summary>
    /// Чи виконується наразі операція активації після її завершення.
    /// </summary>
    public bool Activating;

    /// <summary>
    /// Чи було заряд активовано та чи триває зворотний відлік.
    /// </summary>
    public bool Armed;

    /// <summary>
    /// Чи вже розпочалася послідовність детонації.
    /// </summary>
    public bool Detonated;

    /// <summary>
    /// Чи був заряд знищений або знешкоджений перед детонацією.
    /// </summary>
    public bool Destroyed;

    /// <summary>
    /// Чи вже почалася фінальна музична підказка.
    /// </summary>
    public bool ThemeStarted;

    /// <summary>
    /// Час гри, в який детонує заряд.
    /// </summary>
    public TimeSpan DetonatesAt;

    /// <summary>
    /// Час гри, коли має розпочатися ядерне очищення карти.
    /// </summary>
    public TimeSpan NukeMapAt;

    /// <summary>
    /// Аудіопотокова сутність для зацикленої попереджувальної сирени.
    /// </summary>
    public EntityUid? WarningSirenStream;

    /// <summary>
    /// Аудіопоток для останньої музичної репліки.
    /// </summary>
    public EntityUid? WarheadThemeStream;

    /// <summary>
    /// Пороги зворотного відліку, які вже призвели до оголошення.
    /// </summary>
    public readonly HashSet<int> AnnouncedAtSeconds = new();
}
