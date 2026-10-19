using UnityEngine;

/// <summary>
/// TurboRacing - v0.8.0
/// مخزن الإعدادات المركزي: قيم حية + حفظ دائم في PlayerPrefs.
/// - يُقرأ عند الإقلاع (EnsureLoaded) وكل مكوّن يسحب ما يحتاجه في Start
///   (نموذج السحب يمنع أي مشاكل ترتيب تهيئة)
/// - واجهة الإعدادات (SettingsUI) تعدّل الحقول ثم Save + Apply المناسب
/// - الإعدادات الافتراضية = السلوك الحالي تماماً (صفر تراجع لمن لا يفتح الإعدادات)
/// </summary>
public static class GameSettings
{
    // ================= الصوت =================
    public static float MasterVolume = 1f;   // 0..1
    public static float EngineVolume = 1f;   // 0..1
    public static float SfxVolume    = 1f;   // 0..1

    // ================= التحكم (تخطيط الأزرار) =================
    public static float BtnScale   = 1f;     // 0.8..1.4 حجم الأزرار
    public static float BtnOpacity = 1f;     // 0.4..1.2 مضاعف شفافية الأزرار
    public static float SteerSens  = 1f;     // 0.7..1.4 حساسية التوجيه
    public static bool  SwapButtons = false; // تبديل الجانبين (ليَساري)

    // ================= السيارة =================
    public static int CarColorIndex = 0;     // فهرس في CarColors
    public static int CarPreset     = 0;     // 0=STOCK 1=SPORT 2=TURBO (معامل سرعة فوق مواصفات الموديل)
    static int selectedCarModel;             // v1.4.0: الموديل المختار من الكراج

    /// <summary>الموديل المختار من الكراج (0..CarModels.Count-1) — v1.4.0.</summary>
    public static int SelectedCarModel
    {
        get { EnsureLoaded(); return Mathf.Clamp(selectedCarModel, 0, CarModels.Count - 1); }
        set { selectedCarModel = Mathf.Clamp(value, 0, CarModels.Count - 1); }
    }

    // ================= الكاميرا =================
    public static float CamZoom = 1f;        // 0.8..1.3 مسافة الكاميرا
    public static bool  CamShake = true;     // اهتزاز التصادم

    // ================= ثوابت العرض =================
    public static readonly Color[] CarColors =
    {
        new Color(0.85f, 0.20f, 0.20f), // أحمر (افتراضي)
        new Color(0.20f, 0.45f, 0.90f), // أزرق
        new Color(0.20f, 0.72f, 0.35f), // أخضر
        new Color(0.95f, 0.78f, 0.20f), // أصفر
        new Color(0.95f, 0.50f, 0.15f), // برتقالي
        new Color(0.60f, 0.30f, 0.85f), // بنفسجي
        new Color(0.15f, 0.70f, 0.70f), // فيروزي
        new Color(0.92f, 0.92f, 0.94f), // أبيض
    };

    public static readonly string[] PresetNames = { "STOCK", "SPORT", "TURBO" };

    /// <summary>لون السيارة الحالي.</summary>
    public static Color CurrentColor
    {
        get
        {
            EnsureLoaded();
            int i = Mathf.Clamp(CarColorIndex, 0, CarColors.Length - 1);
            return CarColors[i];
        }
    }

    // ================= التحميل والحفظ =================

    static bool loaded;

    public static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        MasterVolume = PlayerPrefs.GetFloat("tr_master", 1f);
        EngineVolume = PlayerPrefs.GetFloat("tr_engine", 1f);
        SfxVolume    = PlayerPrefs.GetFloat("tr_sfx", 1f);

        BtnScale     = PlayerPrefs.GetFloat("tr_btnScale", 1f);
        BtnOpacity   = PlayerPrefs.GetFloat("tr_btnOpacity", 1f);
        SteerSens    = PlayerPrefs.GetFloat("tr_steer", 1f);
        SwapButtons  = PlayerPrefs.GetInt("tr_swap", 0) == 1;

        CarColorIndex = PlayerPrefs.GetInt("tr_color", 0);
        CarPreset     = PlayerPrefs.GetInt("tr_preset", 0);
        selectedCarModel = PlayerPrefs.GetInt("tr_model", 0);

        CamZoom = PlayerPrefs.GetFloat("tr_zoom", 1f);
        CamShake = PlayerPrefs.GetInt("tr_shake", 1) == 1;
    }

    public static void Save()
    {
        EnsureLoaded();
        PlayerPrefs.SetFloat("tr_master", MasterVolume);
        PlayerPrefs.SetFloat("tr_engine", EngineVolume);
        PlayerPrefs.SetFloat("tr_sfx", SfxVolume);
        PlayerPrefs.SetFloat("tr_btnScale", BtnScale);
        PlayerPrefs.SetFloat("tr_btnOpacity", BtnOpacity);
        PlayerPrefs.SetFloat("tr_steer", SteerSens);
        PlayerPrefs.SetInt("tr_swap", SwapButtons ? 1 : 0);
        PlayerPrefs.SetInt("tr_color", CarColorIndex);
        PlayerPrefs.SetInt("tr_preset", CarPreset);
        PlayerPrefs.SetInt("tr_model", selectedCarModel);
        PlayerPrefs.SetFloat("tr_zoom", CamZoom);
        PlayerPrefs.SetInt("tr_shake", CamShake ? 1 : 0);
        PlayerPrefs.Save();
    }

    // ================= التطبيق على عالم اللعبة =================

    /// <summary>تطبيق كل شيء (يستخدم عند الإعادة الافتراضية).</summary>
    public static void ApplyAll()
    {
        ApplyAudio();
        ApplyCar();
        ApplyCarColor();
        ApplyButtons();
        ApplyCamera();
    }

    public static void ApplyAudio()
    {
        EnsureLoaded();
        if (SoundManager.Instance != null)
            SoundManager.Instance.ApplyVolumes(MasterVolume, EngineVolume, SfxVolume);
    }

    /// <summary>ضبط الأداء (مستوى + حساسية توجيه).</summary>
    public static void ApplyCar()
    {
        EnsureLoaded();
        CarController car = FindCar();
        if (car != null) car.ApplyTuning(CarPreset, SteerSens);
    }

    public static void ApplyCarColor()
    {
        EnsureLoaded();
        CarController car = FindCar();
        if (car != null) car.ApplyModelPaint(); // (v1.4.0) يحترم دهان المصنع للموديلات 1..5
    }

    public static void ApplyButtons()
    {
        EnsureLoaded();
        DriveButtonsLayout.Apply(null);
    }

    public static void ApplyCamera()
    {
        EnsureLoaded();
        if (CameraFollow.Instance != null)
        {
            CameraFollow.Instance.ApplyZoom(CamZoom);
            CameraFollow.Instance.ApplyShake(CamShake);
        }
    }

    /// <summary>إعادة كل الإعدادات للافتراضي + حفظ + تطبيق فوري.</summary>
    public static void ResetDefaults()
    {
        MasterVolume = EngineVolume = SfxVolume = 1f;
        BtnScale = BtnOpacity = SteerSens = 1f;
        SwapButtons = false;
        CarColorIndex = 0;
        CarPreset = 0;
        selectedCarModel = 0;
        CamZoom = 1f;
        CamShake = true;
        Save();
        ApplyAll();
    }

    static CarController FindCar()
    {
        GameObject go = GameObject.Find("Car");
        return go != null ? go.GetComponent<CarController>() : null;
    }
}
