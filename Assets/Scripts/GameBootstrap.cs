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
        CreateCanvasAndButtons();
        CreateHUD();            // المرحلة 3: عداد اللفات والمؤقت
        CreateMenus();          // المرحلة 4: القائمة الرئيسية + النتائج + الإيقاف المؤقت
        CreateEventSystem();
        SetupCamera();
        CreateRaceFlow();       // المرحلة 4: آلة حالات السباق (أخيراً حتى يجد كل شيء)
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

        car.transform.localScale = new Vector3(1f, 2f, 1f);

        SpriteRenderer sr = car.AddComponent<SpriteRenderer>();
        sr.sprite = MakeSolidSprite(64);
        sr.color = new Color(0.85f, 0.2f, 0.2f, 1f); // أحمر
        sr.sortingOrder = 10; // فوق بلاطات الحلبة

        car.AddComponent<Rigidbody2D>(); // CarController يضبط خصائصه في Awake
        BoxCollider2D box = car.AddComponent<BoxCollider2D>();
        box.size = new Vector2(1f, 1f); // يطابق حجم السبرايت (1x1 وحدة عالمية)
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
        new GameObject("MenuUI").AddComponent<MenuUI>();       // القائمة الرئيسية (ترتيب 40)
        new GameObject("ResultsUI").AddComponent<ResultsUI>(); // شاشة النتائج (ترتيب 30)
        new GameObject("PauseUI").AddComponent<PauseUI>();     // الإيقاف المؤقت (ترتيب 20)
    }

    void CreateRaceFlow()
    {
        // يُنشأ أخيراً: Start الخاص به يعمل بعد جاهزية كل المكونات ويفتح القائمة
        new GameObject("RaceFlow").AddComponent<RaceFlow>();
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

        // القيم مطابقة تماماً لقيم المرحلة 1 في المحرر
        CreateButton(canvasRT, "BtnLeft",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(40f, 40f), new Vector2(280f, 280f));

        CreateButton(canvasRT, "BtnRight",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-40f, 40f), new Vector2(280f, 280f));

        CreateButton(canvasRT, "BtnBrake",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 40f), new Vector2(240f, 240f));
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
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
