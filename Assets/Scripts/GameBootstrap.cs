using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// يبني محتوى المشهد بالكود عند بدء اللعبة:
/// الحلبة (Tilemap) + السيارة + الكانفاس والأزرار اللمسية + نظام الأحداث + الكاميرا.
/// الهدف: مشروع يعمل دون أي إعداد يدوي في محرر Unity.
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

        CreateTrack();          // المرحلة 2: الحلبة أولاً حتى تنطلق السيارة من خط البداية
        CreateCar();
        CreateAudio();          // المرحلة 5 (v0.7.0): مدير الصوت الإجرائي + آثار الإطارات
        CreateCanvasAndButtons();
        CreateHUD();            // المرحلة 3: عداد اللفات والمؤقت
        CreateMenus();          // المرحلة 4: القائمة الرئيسية + النتائج + الإيقاف المؤقت
        CreateCinematicOverlay(); // (v0.9.0) فينييت سينمائي تحت كل الواجهات
        CreateEventSystem();
        SetupCamera();
        CreateRaceFlow();       // المرحلة 4: آلة حالات السباق (أخيراً حتى يجد كل شيء)
    }

    // ================= الصوت والمؤثرات (المرحلة 5) =================

    void CreateAudio()
    {
        // SoundManager يبني كل المقاطع إجرائياً في Awake ويجد السيارة في Start
        new GameObject("SoundManager").AddComponent<SoundManager>();

        // مخزون آثار الإطارات (علامات متلاشية خلف السيارة)
        new GameObject("SkidMarks").AddComponent<SkidMarks>();
    }

    // ================= السيارة =================

    void CreateCar()
    {
        GameObject car = new GameObject("Car");

        // الانطلاق من خط البداية على الحلبة (أو المركز احتياطياً)
        if (TrackBuilder.Instance != null)
        {
            car.transform.position = TrackBuilder.Instance.StartPosition;
            car.transform.rotation = Quaternion.Euler(0f, 0f, TrackBuilder.Instance.StartRotationZ);
        }
        else
        {
            car.transform.position = Vector3.zero;
        }

        // (v0.8.0) سيارة مرسومة إجرائياً من طبقتين:
        // CarBody: هيكل أبيض يُلوّن بطلاء اللاعب من الإعدادات
        // CarTrim: تفاصيل ثابتة (عجلات/قمرة/جناح/مصابيح) فوق الهيكل
        // السبرايت 128×256 بـ PPU=128 = 1×2 وحدة عالمية — نفس أبعاد القديم تماماً
        GameSettings.EnsureLoaded();

        GameObject bodyGO = new GameObject("CarBody");
        bodyGO.transform.SetParent(car.transform, false);
        SpriteRenderer bodySR = bodyGO.AddComponent<SpriteRenderer>();
        bodySR.sprite = CarSkin.Body();
        bodySR.sortingOrder = 10;
        bodySR.color = GameSettings.CurrentColor;

        GameObject trimGO = new GameObject("CarTrim");
        trimGO.transform.SetParent(car.transform, false);
        SpriteRenderer trimSR = trimGO.AddComponent<SpriteRenderer>();
        trimSR.sprite = CarSkin.Trim();
        trimSR.sortingOrder = 11;
        trimSR.color = Color.white;

        // (v0.9.0) ظل أرضي تحت السيارة: أقوى إشارة عمق في المنظور العلوي/المطاردة
        GameObject shadowGO = new GameObject("CarShadow");
        shadowGO.transform.SetParent(car.transform, false);
        SpriteRenderer shadowSR = shadowGO.AddComponent<SpriteRenderer>();
        shadowSR.sprite = CarSkin.Shadow();
        shadowSR.sortingOrder = 9; // فوق آثار الإطارات وتحت الهيكل
        shadowSR.color = new Color(0f, 0f, 0f, 0.85f);
        shadowGO.transform.localPosition = new Vector3(0.12f, -0.16f, 0f);
        shadowGO.transform.localScale = new Vector3(1.12f, 1.07f, 1f);

        car.AddComponent<Rigidbody2D>(); // CarController يضبط خصائصه في Awake
        BoxCollider2D box = car.AddComponent<BoxCollider2D>();
        box.size = new Vector2(1f, 2f); // 1×2 وحدات عالمية (السبرايت أصلاً بهذا القياس الآن)
        car.AddComponent<CarController>();
        car.AddComponent<SurfaceChecker>(); // المرحلة 2: عقاب العشب
    }

    // ================= الحلبة (المرحلة 2) =================

    void CreateTrack()
    {
        // TrackBuilder يبني الطبقات الثلاث (عشب/طريق/جدران) فوراً في Awake الخاص به
        GameObject track = new GameObject("Track");
        track.AddComponent<TrackBuilder>();
        track.AddComponent<LapManager>(); // المرحلة 3: بوابات اللفة والتوقيت والحفظ
    }

    // ================= واجهة السباق (المرحلة 3) =================

    void CreateHUD()
    {
        // RaceHUD يبني كانفاسه الخاص في Start (عرض فقط، لا يمس أزرار القيادة)
        new GameObject("RaceHUD").AddComponent<RaceHUD>();
    }

    // ================= واجهات القوائم (المرحلة 4) =================

    void CreateMenus()
    {
        new GameObject("SettingsUI").AddComponent<SettingsUI>(); // (v0.8.0) صفحة الإعدادات (ترتيب 50)
        new GameObject("MenuUI").AddComponent<MenuUI>();         // القائمة الرئيسية (ترتيب 40)
        new GameObject("ResultsUI").AddComponent<ResultsUI>();   // شاشة النتائج (ترتيب 30)
        new GameObject("PauseUI").AddComponent<PauseUI>();       // الإيقاف المؤقت (ترتيب 20)
    }

    void CreateRaceFlow()
    {
        // يُنشأ أخيراً: Start الخاص به يعمل بعد جاهزية كل المكونات ويفتح القائمة
        new GameObject("RaceFlow").AddComponent<RaceFlow>();
    }

    // ================= فينييت سينمائي (v0.9.0) =================

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

    // ================= الكاميرا =================

    void SetupCamera()
    {
        GameObject camGO = GameObject.Find("Main Camera");
        if (camGO == null) return;

        Camera cam = camGO.GetComponent<Camera>();
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.22f, 0.13f); // أخضر داكن خارج الحلبة
        }
        camGO.AddComponent<CameraFollow>();

        // (v0.7.0) مستمع صوتي واحد على الكاميرا — بلا لم يُسمع أي صوت مولّد
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

        // التخطيط القياسي لألعاب السباق بالتسارع التلقائي (v0.6.0):
        // - زرا التوجيه مجمّعان معاً أسفل اليسار (إبهام أيسر واحد يوجه)
        // - زر الفرامل كبير أسفل اليمين (إبهام أيمن يفرمل) - بعيداً عن منتصف الشاشة
        //   حيث تظهر السيارة مع كاميرا المطاردة، وبمسافة مريحة من حواف الجهاز
        //   (القياسات بوحدات كانفاس 1920×1080 مع مطابقة الارتفاع)

        // اليسار: زرا التوجيه جنباً إلى جنب
        CreateButton(canvasRT, "BtnLeft",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(250f, 240f), new Vector2(300f, 300f));

        CreateButton(canvasRT, "BtnRight",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(580f, 240f), new Vector2(300f, 300f));

        // اليمين: الفرامل (أكبر حجماً لسهولة الوصول)
        CreateButton(canvasRT, "BtnBrake",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(-250f, 240f), new Vector2(340f, 340f));
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
        text.font = UiKit.UIFont; // خط حقيقي مضمّن (v0.6.1) يُصيَّر بشكل مضمون على الأجهزة
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

    // ================= أدوات مساعدة =================

    Sprite MakeSolidSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Color white = Color.white;
        for (int i = 0; i < pixels.Length; i++) pixels[i] = white;
        tex.SetPixels(pixels);
        tex.Apply(false, false);

        // عدد البكسل كـ Pixels Per Unit => سبرايت 1x1 وحدة عالمية
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
