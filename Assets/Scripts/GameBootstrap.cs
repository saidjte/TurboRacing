using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// يبني محتوى المشهد بالكود عند بدء اللعبة:
/// الحلبة المجسّمة + السيارة المجسّمة + الإضاءة الشمسية والظلال + الكانفاس
/// والأزرار اللمسية + نظام الأحداث + الكاميرا المنظورية.
/// الهدف: مشروع يعمل دون أي إعداد يدوي في محرر Unity.
/// v1.0.0: عالم ثلاثي الأبعاد حقيقي (XZ أرض، Y سماء) — الواجهات والصوت واللعب كما هي.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    void Awake()
    {
        Application.targetFrameRate = 60;

        // فرض الوضع الأفقي على الهاتف (أمان إضافي حتى لو كانت إعدادات المنصة عمودية)
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.orientation = ScreenOrientation.AutoRotation;

        CreateTrack();          // الحلبة أولاً حتى تنطلق السيارة من خط البداية
        CreateLighting();       // (v1.0.0) شمس + ظلال حقيقية + ضباب + إضاءة محيطية
        CreateGarage();         // (v1.4.0) مسرح الكراج خارج الحلبة (عرض + اختيار الموديلات)
        CreateCar();
        CreateAudio();          // مدير الصوت الإجرائي + آثار الإطارات
        CreateCanvasAndButtons();
        CreateHUD();            // عداد اللفات والمؤقت والمركز
        CreateMenus();          // القائمة الرئيسية + الكراج + النتائج + الإيقاف المؤقت + الإعدادات
        CreateCinematicOverlay(); // فينييت سينمائي تحت كل الواجهات
        CreateEventSystem();
        SetupCamera();
        CreateAiManager();      // (v1.4.0) مدير المنافسين (يولّد الشبكة عند كل سباق)
        CreateRaceFlow();       // آلة حالات السباق (أخيراً حتى يجد كل شيء)
    }

    // ================= الصوت والمؤثرات =================

    void CreateAudio()
    {
        // SoundManager يبني كل المقاطع إجرائياً في Awake ويجد السيارة في Start
        new GameObject("SoundManager").AddComponent<SoundManager>();

        // مخزون آثار الإطارات (علامات متلاشية خلف السيارة)
        new GameObject("SkidMarks").AddComponent<SkidMarks>();
    }

    // ================= الإضاءة المجسّمة (v1.0.0) =================

    void CreateLighting()
    {
        // شمس اتجاهية بظلال ناعمة — أقوى إشارة عمق في العالم المجسّم
        GameObject sunGO = new GameObject("Sun");
        Light sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.965f, 0.90f);   // دفء شمسي خفيف
        sun.intensity = 1.05f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.78f;
        sun.shadowNormalBias = 0.06f;
        sunGO.transform.rotation = Quaternion.Euler(50f, -28f, 0f);

        // إضاءة محيطية ثلاثية (سماء زرقاء / أفق محايد / انعكاس أرضي خضري)
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.63f, 0.78f);
        RenderSettings.ambientEquatorColor = new Color(0.50f, 0.50f, 0.52f);
        RenderSettings.ambientGroundColor = new Color(0.30f, 0.33f, 0.26f);
        RenderSettings.sun = sun;

        // ضباب سينمائي (المدى يُعاد ضبطه عند تبديل وضعي الكاميرا)
        Color haze = new Color(0.62f, 0.76f, 0.94f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = haze;
        RenderSettings.fogStartDistance = 90f;   // v1.5.0: مدى الحلبة البيضاوية
        RenderSettings.fogEndDistance = 340f;

        // مسافة الظلال ودرجتها (موبايل)
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowDistance = 120f;   // v1.5.0: ظلال أب للحلبة الأكبر
        QualitySettings.shadowCascades = 1;
    }

    // ================= السيارة المجسّمة =================

    void CreateCar()
    {
        GameObject car = new GameObject("Car");

        // الانطلاق من خط البداية على الحلبة (أو المركز احتياطياً)
        if (TrackBuilder.Instance != null)
        {
            car.transform.position = TrackBuilder.Instance.StartPosition; // y = 0 (الأرض)
            car.transform.rotation = Quaternion.Euler(0f, TrackBuilder.Instance.StartYaw, 0f);
        }
        else
        {
            car.transform.position = Vector3.zero;
        }

        GameSettings.EnsureLoaded();

        // الجسم المجسّم (هيكل + تفاصيل + عجلات) يبنيه CarModel3D داخل CarController.Awake.
        // هنا فقط: فيزياء ثلاثية الأبعاد + مصادم صندوقي بمساحة القيادة القديمة.
        car.AddComponent<Rigidbody>(); // CarController يضبط خصائصه في Awake

        BoxCollider box = car.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.45f, 0.05f);
        box.size = new Vector3(CarModel3D.ColliderWidth, CarModel3D.ColliderHeight, CarModel3D.ColliderLength);

        car.AddComponent<CarController>();
        car.AddComponent<SurfaceChecker>(); // عقاب العشب
    }

    // ================= الحلبة =================

    void CreateTrack()
    {
        // TrackBuilder يبني العالم المجسّم فوراً في Awake الخاص به
        GameObject track = new GameObject("Track");
        track.AddComponent<TrackBuilder>();
        track.AddComponent<LapManager>(); // بوابات اللفة والتوقيت والحفظ
        // v2.6.0: التزيين بأسلوب حزم المتجر (المكافئ الإجرائي):
        track.AddComponent<TrackStyle>(); // طلاء الطريق بأسلوب AK Road Editor (بلا مصادمات)
        track.AddComponent<TrackProps>(); // حزمة عناصر Cartoon Props حول الحلبة
    }

    // ================= الكراج (v1.4.0) =================

    void CreateGarage()
    {
        // مسرح عرض ثلاثي الأبعاد خارج الحلبة: منصة دوّارة + جدار + إضاءة
        new GameObject("GarageStage").AddComponent<GarageStage>();
    }

    // ================= مدير المنافسين (v1.4.0) =================

    void CreateAiManager()
    {
        // يولّد شبكة الانطلاق (4 منافسين) عند كل سباق ويحسب الترتيب الحي
        new GameObject("AiManager").AddComponent<AiManager>();
    }

    // ================= واجهة السباق =================

    void CreateHUD()
    {
        // RaceHUD يبني كانفاسه الخاص في Start (عرض فقط، لا يمس أزرار القيادة)
        new GameObject("RaceHUD").AddComponent<RaceHUD>();
    }

    // ================= واجهات القوائم =================

    void CreateMenus()
    {
        new GameObject("SettingsUI").AddComponent<SettingsUI>(); // صفحة الإعدادات (ترتيب 50)
        new GameObject("MenuUI").AddComponent<MenuUI>();         // القائمة الرئيسية (ترتيب 40)
        new GameObject("GarageUI").AddComponent<GarageUI>();     // الكراج (ترتيب 45) — v1.4.0
        new GameObject("ResultsUI").AddComponent<ResultsUI>();   // شاشة النتائج (ترتيب 30)
        new GameObject("PauseUI").AddComponent<PauseUI>();       // الإيقاف المؤقت (ترتيب 20)
    }

    void CreateRaceFlow()
    {
        // يُنشأ أخيراً: Start الخاص به يعمل بعد جاهزية كل المكونات ويفتح القائمة
        new GameObject("RaceFlow").AddComponent<RaceFlow>();
    }

    // ================= فينييت سينمائي =================

    /// <summary>
    /// طبقة تعتيم رقيقة على أطراف الشاشة (ترتيب -10: فوق العالم وتحت كل الواجهات)
    /// تمنح إحساس عدسة/عمق وتبرز مركز اللعب — raycast معطّل فلا تعيق اللمس.
    /// </summary>
    void CreateCinematicOverlay()
    {
        GameObject c = UiKit.NewCanvas("CinematicOverlay", -10);

        GameObject img = new GameObject("Vignette",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        img.transform.SetParent(c.transform, false);

        RectTransform rt = img.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image im = img.GetComponent<Image>();
        im.sprite = VignetteSprite();
        im.raycastTarget = false;
        im.color = Color.white;
    }

    /// <summary>تدرج شعاعي: شفاف بالمركز، تعتيم يتصاعد نحو الزوايا.</summary>
    Sprite VignetteSprite()
    {
        const int S = 512;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color[] px = new Color[S * S];
        float c = S * 0.5f;
        float maxD = Mathf.Sqrt(2f) * c;

        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) +
                                     (y + 0.5f - c) * (y + 0.5f - c)) / maxD;
                // 0 حتى 55% من نصف القطر ثم تتدرج إلى 0.34 عند الزوايا
                float a = Mathf.SmoothStep(0.55f, 1f, d) * 0.34f;
                px[y * S + x] = new Color(0f, 0f, 0.02f, a);
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, false);

        Sprite sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        sp.name = "VignetteSprite";
        return sp;
    }

    // ================= الكاميرا المنظورية =================

    void SetupCamera()
    {
        GameObject camGO = GameObject.Find("Main Camera");
        if (camGO == null) return;

        Camera cam = camGO.GetComponent<Camera>();
        if (cam != null)
        {
            // منظور حقيقي: أفق + سماء + عمق متناقص
            cam.orthographic = false;
            cam.fieldOfView = 58f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 900f; // v1.5.0: أفق أبعد للحلبة البيضاوية
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.76f, 0.94f); // نفس لون الضباب = أفق سلس
        }
        camGO.AddComponent<CameraFollow>();

        // مستمع صوتي واحد على الكاميرا
        if (camGO.GetComponent<AudioListener>() == null)
            camGO.AddComponent<AudioListener>();
    }

    // ================= الكانفاس والأزرار =================

    void CreateCanvasAndButtons()
    {
        GameObject canvasGO = new GameObject("GameCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();

        // التخطيط القياسي لألعاب السباق بالقيادة اليدوية (v1.2.0):
        // - زرا التوجيه مجمّعان معاً أسفل اليسار (إبهام أيسر واحد يوجه)
        // - زر البنزين GAS كبير أسفل اليمين ملاصق للحافة (الإبهام الأيمن يقود)
        // - زر الفرامل BRK أصغر على يسار GAS (المرور عليه بسرعة عند الحاجة)
        CreateButton(canvasRT, "BtnLeft",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(250f, 240f), new Vector2(300f, 300f));

        CreateButton(canvasRT, "BtnRight",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(580f, 240f), new Vector2(300f, 300f));

        // اليمين: البنزين (الأكبر - الزر الأساسي) ثم الفرامل على يساره
        CreateButton(canvasRT, "BtnGas",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(-250f, 240f), new Vector2(340f, 340f));

        CreateButton(canvasRT, "BtnBrake",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(-640f, 240f), new Vector2(280f, 280f));
    }

    void CreateButton(RectTransform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size)
    {
        GameObject btn = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btn.transform.SetParent(parent, false);

        RectTransform rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = btn.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.35f); // أسود شبه شفاف

        // نص الطفل (Legacy Text)
        GameObject textGO = new GameObject("Text",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textGO.transform.SetParent(btn.transform, false);

        RectTransform trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        Text text = textGO.GetComponent<Text>();
        text.font = UiKit.UIFont; // خط حقيقي مضمّن يُصيَّر بشكل مضمون على الأجهزة
        text.fontSize = 72;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        // CarController.Start يعيد ضبط النص والحجم والتنسيق تلقائياً
    }

    // ================= نظام الأحداث =================

    void CreateEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
