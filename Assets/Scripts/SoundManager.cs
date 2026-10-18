using UnityEngine;

/// <summary>
/// TurboRacing - المرحلة 5 (v0.7.0)
/// مدير الصوت المركزي: كل الأصوات مولّدة إجرائياً عبر AudioSynth.
/// - حلقة محرك دائمة يتغير طبقها وعلوّها مع السرعة بثلاث حالات:
///   Silent (قائمة/إيقاف/نهاية) ← Idle (خمول أثناء العد التنازلي) ← Drive (قيادة)
/// - حلقات مصاحبة في وضع القيادة: صرير الفرامل عند السرعة + حفيف العشب
///   (ملاحظة: AudioSource لا يتأثر بـ Time.timeScale لذا الإيقاف المؤقت
///    يمر عبر EngineSilent() صراحة من RaceFlow)
/// - مؤثرات لحظية عبر مصدرين متناوبين (يسمححان بتداخل النغمات):
///   تنبيهات العد + GO + رنة لفة + رنة رقم قياسي + فانفار نهاية
///   + ارتطام تصادم متدرج القوة + نقرة أزرار الواجهة
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

    CarController car;       // قراءة دورية للسرعة/الفرامل
    SurfaceChecker surface;  // حالة العشب

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
    }

    void Start()
    {
        GameObject carGO = GameObject.Find("Car");
        if (carGO != null)
        {
            car = carGO.GetComponent<CarController>();
            surface = carGO.GetComponent<SurfaceChecker>();
        }
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
            Fade(engineSrc, 0.13f);
            engineSrc.pitch = Mathf.Lerp(engineSrc.pitch, 0.8f, 0.1f);
            Fade(skidSrc, 0f);
            Fade(grassSrc, 0f);
            return;
        }

        // ================= قيادة: قراءة حالة السيارة كل إطار =================
        // طبق المحرك: 0.85x خمول متحرك ← 2.1x عند السرعة القصوى (يخفت قليلاً على العشب)
        float targetPitch = Mathf.Lerp(0.85f, 2.1f, speed01) * (grassNow ? 0.9f : 1f);
        engineSrc.pitch = Mathf.Lerp(engineSrc.pitch, targetPitch, 0.08f);
        Fade(engineSrc, 0.14f + 0.13f * speed01);

        // صرير الفرامل: فقط أثناء فرملة فعلية بسرعة ملموسة (يهدأ مع تباطؤ السيارة)
        bool skidding = braking && speed01 > 0.22f;
        skidSrc.pitch = Mathf.Lerp(skidSrc.pitch, Mathf.Lerp(1.35f, 1.0f, speed01), 0.1f);
        Fade(skidSrc, skidding ? 0.13f : 0f, 0.2f);

        // حفيف العشب: عند الخروج عن الطريق بحركة
        Fade(grassSrc, grassNow && speed01 > 0.03f ? 0.2f : 0f, 0.2f);
    }

    /// <summary>تلاشٍ سلس نحو مستوى صوت هدف (يمنع القفزات المزعجة).</summary>
    void Fade(AudioSource src, float target, float rate = 0.15f)
    {
        if (src == null) return;
        src.volume = Mathf.Lerp(src.volume, target, rate);
    }

    // ================= حالات المحرك (تستدعيها RaceFlow) =================

    /// <summary>خمول المحرك أثناء العد التنازلي.</summary>
    public void EngineIdle() { mode = EngineMode.Idle; }

    /// <summary>قيادة فعلية — الصوت يقرأ حالة السيارة دورياً.</summary>
    public void EngineDrive() { mode = EngineMode.Drive; }

    /// <summary>صمت كامل (قائمة/إيقاف مؤقت/شاشة نتائج).</summary>
    public void EngineSilent() { mode = EngineMode.Silent; }

    // ================= المؤثرات اللحظية =================

    void PlaySfx(AudioClip clip, float vol, float pitch = 1f)
    {
        if (clip == null) return;
        AudioSource src = (sfxToggle++ & 1) == 0 ? sfxA : sfxB; // تناوب يسمح بالتداخل
        src.pitch = pitch;
        src.volume = vol;
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
