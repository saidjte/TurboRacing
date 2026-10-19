using UnityEngine;

/// <summary>
/// TurboRacing - المرحلة 5 (v0.7.0) — مطوّر v1.3.0 (أصوات محرك حقيقية من الأصل المطلوب)
/// مدير الصوت المركزي: المؤثرات مولّدة إجرائياً عبر AudioSynth، والمحرك الحقيقي
/// من الأصل المطلوب "Car and transportation sounds collection" (تسجيلات 2CV6):
/// - Ignition عند بدء العد التنازلي (تشغيل المحرك)
/// - EngineWarming حلقة خمول أثناء العد
/// - FirstGear/SecondGear/ThirdGear حلقات قيادة حقيقية تتبدل مع السرعة
///   (تبديل الحلقة = إحساس تغيير الترس) مع طبقة ترتفع داخل كل ترس
/// - احتياطي كامل: إذا غابت المقاطع يعمل المولّد الإجرائي القديم كما هو
/// - حلقات مصاحبة: صرير الفرامل عند السرعة + حفيف العشب (إجرائية)
/// - مؤثرات لحظية: تنبيهات العد + GO + رنة لفة + رقم قياسي + فانفار
///   + ارتطام تصادم + نقرة أزرار (إجرائية كما هي)
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    public enum EngineMode { Silent, Idle, Drive }

    EngineMode mode = EngineMode.Silent;

    AudioSource engineSrc;   // حلقة المحرك
    AudioSource skidSrc;     // حلقة صرير الفرامل
    AudioSource grassSrc;    // حلقة حفيف العشب
    AudioSource sfxA, sfxB;  // مصدرا المؤثرات اللحظية (تناوب)
    int sfxToggle;

    AudioClip beepClip, goClip, lapClip, recordClip, finishClip, crashClip, clickClip;

    // --- أصوات المحرك الحقيقية (v1.3.0) ---
    AudioClip realIgnition, realIdle, realGear1, realGear2, realGear3;
    bool realEngine;
    int currentGear; // 0 = خمول، 1..3 = ترس

    CarController car;       // قراءة دورية للسرعة/الفرامل
    SurfaceChecker surface;  // حالة العشب

    // (v0.8.0) مستويات الصوت من صفحة الإعدادات
    float masterVol = 1f;
    float engineVol = 1f;
    float sfxVol = 1f;

    void Awake()
    {
        Instance = this;

        // --- بناء كل المقاطع إجرائياً عند الإقلاع ---
        beepClip   = AudioSynth.Beep(880f, 0.16f, "Beep");
        goClip     = AudioSynth.Beep(1318.5f, 0.5f, "Go");
        lapClip    = AudioSynth.LapChime();
        recordClip = AudioSynth.RecordJingle();
        finishClip = AudioSynth.FinishFanfare();
        crashClip  = AudioSynth.Crash();
        clickClip  = AudioSynth.Beep(1200f, 0.035f, "Click", 0.7f);

        engineSrc = MakeSource(AudioSynth.EngineLoop(), loop: true);
        skidSrc   = MakeSource(AudioSynth.SkidLoop(),   loop: true);
        grassSrc  = MakeSource(AudioSynth.GrassLoop(),  loop: true);
        sfxA = MakeSource(null, loop: false);
        sfxB = MakeSource(null, loop: false);

        // --- محاولة تحميل أصوات المحرك الحقيقية (الأصل المطلوب) ---
        realIgnition   = Resources.Load<AudioClip>("GameAssets/Sounds/2CV6Ignition");
        realIdle       = Resources.Load<AudioClip>("GameAssets/Sounds/2CV6EngineWarming");
        realGear1      = Resources.Load<AudioClip>("GameAssets/Sounds/2CV6FirstGear");
        realGear2      = Resources.Load<AudioClip>("GameAssets/Sounds/2CV6SecondGear");
        realGear3      = Resources.Load<AudioClip>("GameAssets/Sounds/2CV6ThirdGear");
        realEngine = realIdle != null && realGear1 != null && realGear2 != null && realGear3 != null;

        if (realEngine)
        {
            engineSrc.clip = realIdle;
            Debug.Log("[SoundManager] أصوات المحرك الحقيقية (2CV6) جاهزة.");
        }
    }

    void Start()
    {
        GameObject carGO = GameObject.Find("Car");
        if (carGO != null)
        {
            car = carGO.GetComponent<CarController>();
            surface = carGO.GetComponent<SurfaceChecker>();
        }

        // (v0.8.0) سحب مستويات الصوت المحفوظة (نموذج السحب = لا مشاكل ترتيب)
        GameSettings.EnsureLoaded();
        ApplyVolumes(GameSettings.MasterVolume, GameSettings.EngineVolume, GameSettings.SfxVolume);
    }

    /// <summary>(v0.8.0) تطبيق مستويات الصوت الثلاثة (0..1) من صفحة الإعدادات.</summary>
    public void ApplyVolumes(float master, float engine, float sfx)
    {
        masterVol = Mathf.Clamp01(master);
        engineVol = Mathf.Clamp01(engine);
        sfxVol = Mathf.Clamp01(sfx);
    }

    AudioSource MakeSource(AudioClip clip, bool loop)
    {
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = loop;
        src.volume = 0f;
        src.playOnAwake = false;
        src.spatialBlend = 0f; // صوت ثنائي الأبعاد دائماً
        if (clip != null && loop) src.Play();
        return src;
    }

    void Update()
    {
        // ================= صمت: تلاشٍ سريع لكل الحلقات =================
        if (mode == EngineMode.Silent)
        {
            Fade(skidSrc, 0f);
            Fade(grassSrc, 0f);
            Fade(engineSrc, 0f);
            return;
        }

        float speed01 = car != null ? car.Speed01 : 0f;
        bool braking = car != null && car.IsBraking;
        bool grassNow = surface != null && surface.OnGrass;

        // ================= خمول: أثناء العد التنازلي =================
        if (mode == EngineMode.Idle)
        {
            currentGear = 0;
            if (realEngine)
            {
                if (engineSrc.clip != realIdle)
                {
                    engineSrc.clip = realIdle;
                    engineSrc.pitch = 1f;
                    engineSrc.Play();
                }
                Fade(engineSrc, 0.30f * masterVol * engineVol);
                engineSrc.pitch = Mathf.Lerp(engineSrc.pitch, 1.05f, 0.06f);
            }
            else
            {
                Fade(engineSrc, 0.13f * masterVol * engineVol);
                engineSrc.pitch = Mathf.Lerp(engineSrc.pitch, 0.8f, 0.1f);
            }
            Fade(skidSrc, 0f);
            Fade(grassSrc, 0f);
            return;
        }

        // ================= قيادة: قراءة حالة السيارة كل إطار =================
        if (realEngine)
        {
            // ترس حسب السرعة: تبديل الحلقة = إحساس تغيير الترس
            int gear = speed01 < 0.33f ? 1 : (speed01 < 0.66f ? 2 : 3);
            if (gear != currentGear || !engineSrc.isPlaying)
            {
                currentGear = gear;
                engineSrc.clip = gear == 1 ? realGear1 : (gear == 2 ? realGear2 : realGear3);
                engineSrc.pitch = 1f;
                engineSrc.Play();
            }

            // طبقة داخل الترس: 0.95 عند بدايته ← 1.35 نهايته (محاكاة الدوران)
            float gearBand = speed01 < 0.33f ? speed01 / 0.33f
                          : speed01 < 0.66f ? (speed01 - 0.33f) / 0.33f
                          : (speed01 - 0.66f) / 0.34f;
            float targetPitch = Mathf.Lerp(0.95f, 1.35f, gearBand) * (grassNow ? 0.92f : 1f);
            engineSrc.pitch = Mathf.Lerp(engineSrc.pitch, targetPitch, 0.09f);
            Fade(engineSrc, (0.26f + 0.16f * speed01) * masterVol * engineVol);
        }
        else
        {
            // المولّد الإجرائي القديم (احتياطي)
            float targetPitch = Mathf.Lerp(0.85f, 2.1f, speed01) * (grassNow ? 0.9f : 1f);
            engineSrc.pitch = Mathf.Lerp(engineSrc.pitch, targetPitch, 0.08f);
            Fade(engineSrc, (0.14f + 0.13f * speed01) * masterVol * engineVol);
        }

        // صرير الفرامل: فقط أثناء فرملة فعلية بسرعة ملموسة (يهدأ مع تباطؤ السيارة)
        bool skidding = braking && speed01 > 0.22f;
        skidSrc.pitch = Mathf.Lerp(skidSrc.pitch, Mathf.Lerp(1.35f, 1.0f, speed01), 0.1f);
        Fade(skidSrc, skidding ? 0.13f * masterVol * sfxVol : 0f, 0.2f);

        // حفيف العشب: عند الخروج عن الطريق بحركة
        Fade(grassSrc, grassNow && speed01 > 0.03f ? 0.2f * masterVol * sfxVol : 0f, 0.2f);
    }

    /// <summary>تلاشٍ سلس نحو مستوى صوت هدف (يمنع القفزات المزعجة).</summary>
    void Fade(AudioSource src, float target, float rate = 0.15f)
    {
        if (src == null) return;
        src.volume = Mathf.Lerp(src.volume, target, rate);
    }

    // ================= حالات المحرك (تستدعيها RaceFlow) =================

    /// <summary>خمول المحرك أثناء العد التنازلي — مع المحرك الحقيقي: تشغيل Ignition.</summary>
    public void EngineIdle()
    {
        mode = EngineMode.Idle;
        if (realEngine && realIgnition != null)
        {
            // صوت تشغيل المحرك الحقيقي (يستمر عبر العد التنازلي ثم يسلّم للترس الأول)
            AudioSource src = (sfxToggle++ & 1) == 0 ? sfxA : sfxB;
            src.pitch = 1f;
            src.volume = 0.5f * masterVol * engineVol;
            src.clip = realIgnition;
            src.Play();
        }
    }

    /// <summary>قيادة فعلية — الصوت يقرأ حالة السيارة دورياً.</summary>
    public void EngineDrive()
    {
        mode = EngineMode.Drive;
        currentGear = 0; // يجبر تحديث الترس الأول على اختيار الحلقة الصحيحة
    }

    /// <summary>صمت كامل (قائمة/إيقاف مؤقت/شاشة نتائج).</summary>
    public void EngineSilent()
    {
        mode = EngineMode.Silent;
        currentGear = 0;
    }

    // ================= المؤثرات اللحظية =================

    void PlaySfx(AudioClip clip, float vol, float pitch = 1f)
    {
        if (clip == null) return;
        AudioSource src = (sfxToggle++ & 1) == 0 ? sfxA : sfxB; // تناوب يسمح بالتداخل
        src.pitch = pitch;
        src.volume = vol * masterVol * sfxVol; // (v0.8.0) احترام مستويات الصوت
        src.clip = clip;
        src.Play();
    }

    /// <summary>نقرة العد التنازلي (3..2..1).</summary>
    public void PlayCountBeep() => PlaySfx(beepClip, 0.5f);

    /// <summary>نغمة GO! الأعلى والأطول.</summary>
    public void PlayGo() => PlaySfx(goClip, 0.55f);

    /// <summary>رنة عبور خط النهاية بلفة صحيحة.</summary>
    public void PlayLap() => PlaySfx(lapClip, 0.5f);

    /// <summary>رنة رقم قياسي جديد (تُشغَّل بدل فانفار النهاية).</summary>
    public void PlayRecord() => PlaySfx(recordClip, 0.6f);

    /// <summary>فانفار نهاية السباق.</summary>
    public void PlayFinish() => PlaySfx(finishClip, 0.6f);

    /// <summary>نقرة أزرار الواجهة.</summary>
    public void PlayClick() => PlaySfx(clickClip, 0.3f);

    /// <summary>ارتطام تصادم — مستوى الصوت يتناسب مع قوة الاصطدام (0..1).</summary>
    public void PlayCrash(float impact01)
    {
        float vol = Mathf.Lerp(0.3f, 0.85f, Mathf.Clamp01(impact01));
        PlaySfx(crashClip, vol, Random.Range(0.9f, 1.1f));
    }
}
