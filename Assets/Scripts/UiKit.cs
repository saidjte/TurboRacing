using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TurboRacing - المرحلة 4
/// أدوات واجهة مشتركة: كل الواجهات تُبنى بالكود عبر هذه المساعدة
/// (كانفاس بمقاس مرجعي موحّد + نصوص + أزرار + لوحات + تنسيق وقت موحّد).
/// </summary>
public static class UiKit
{
    // ---- الهوية البصرية الموحّدة ----
    public static readonly Color Amber = new Color(1f, 0.72f, 0.15f);      // لون السباق الأساسي
    public static readonly Color PanelDark = new Color(0.06f, 0.07f, 0.09f, 0.96f);
    public static readonly Color OverlayDark = new Color(0f, 0f, 0f, 0.60f);
    public static readonly Color OverlaySoft = new Color(0f, 0f, 0f, 0.45f);
    public static readonly Color BtnDark = new Color(0f, 0f, 0f, 0.55f);
    public static readonly Color BtnLightText = Color.white;
    public static readonly Color BtnAmberText = new Color(0.12f, 0.08f, 0.02f);

    static Font cachedUiFont;

    /// <summary>
    /// خط الواجهة (v0.6.1): خط حقيقي مضمّن في الحزمة (Assets/Resources/Fonts/UiFont.ttf)
    /// بدل الخط المدمج LegacyRuntime الذي قد لا يُصيَّر نصوصه على بعض أجهزة أندرويد
    /// (صورة الأزرار تظهر والنص مخفي). مع احتياطي تلقائي للخط المدمج إن فشل التحميل.
    /// </summary>
    public static Font UIFont
    {
        get
        {
            if (cachedUiFont == null)
            {
                // v0.6.6: fallback chain with per-candidate health check
                // 1) bundled real font, 2) Android OS fonts (Roboto), 3) builtin legacy
                Font f = Resources.Load<Font>("Fonts/UiFont");
                if (!IsHealthyFont(f))
                {
                    foreach (string name in OsFontCandidates)
                    {
                        Font os;
                        try { os = Font.CreateDynamicFontFromOSFont(name, 48); }
                        catch { os = null; }
                        if (IsHealthyFont(os)) { f = os; break; }
                    }
                    if (!IsHealthyFont(f))
                    {
                        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                        catch { f = null; }
                    }
                }
                cachedUiFont = f;
                PrewarmFont(cachedUiFont);
            }
            return cachedUiFont;
        }
    }

    /// <summary>توافق مع الاستدعاءات القديمة — يعيد UIFont.</summary>
    public static Font BuiltinFont => UIFont;

    // مرشحو خطوط النظام (Roboto هو خط أندرويد المضمون على كل الأجهزة تقريباً)
    static readonly string[] OsFontCandidates = { "Roboto", "Noto Sans", "Open Sans", "Helvetica", "Arial" };

    /// <summary>فحص صحة الخط: ماديته موجودة ويستطيع رندر حرف فعلي — يحمي من خط محمّل بلا بيانات.</summary>
    static bool IsHealthyFont(Font f)
    {
        if (f == null) return false;
        try { return f.material != null && f.HasCharacter('A'); }
        catch { return false; }
    }

    /// <summary>
    /// تهيئة ذاكرة الحروف مسبقاً (كل ASCII) — يمنع أي فراغ في أول إطارات الرندر
    /// ويجبر محرك الخطوط على بناء الأطلس فوراً عند الإقلاع.
    /// </summary>
    static void PrewarmFont(Font f)
    {
        if (f == null) return;
        var sb = new System.Text.StringBuilder();
        for (int i = 32; i <= 126; i++) sb.Append((char)i);
        try { f.RequestCharactersInTexture(sb.ToString(), 48, FontStyle.Bold); } catch { }
        try { f.RequestCharactersInTexture(sb.ToString(), 48, FontStyle.Normal); } catch { }
    }

    // سبرايتات الأسهم المرسومة (مستقلة عن الخطوط)
    static Sprite cachedArrowLeft;
    static Sprite cachedArrowRight;
    static Sprite cachedArrowUp;   // v1.2.0: زر البنزين
    static Sprite cachedSquare;    // v1.2.0: زر الفرامل

    /// <summary>سهم أبيض مرسوم بالكود (مثلث) — بديل مضمون عن نصوص أزرار التوجيه على كل الأجهزة (v0.6.6).</summary>
    public static Sprite TriangleSprite(bool pointRight)
    {
        if (pointRight && cachedArrowRight != null) return cachedArrowRight;
        if (!pointRight && cachedArrowLeft != null) return cachedArrowLeft;

        const int N = 128;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[N * N];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        // مثلث أفقي: القاعدة عند الوجه المقابل لاتجاه السهم
        for (int x = 0; x < N; x++)
        {
            float t = pointRight ? 1f - x / (float)(N - 1) : x / (float)(N - 1);
            int halfH = Mathf.RoundToInt(t * (N / 2 - 2));
            for (int y = N / 2 - halfH; y <= N / 2 + halfH; y++)
                pixels[y * N + x] = Color.white;
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        sp.name = pointRight ? "ArrowRight" : "ArrowLeft";

        if (pointRight) cachedArrowRight = sp; else cachedArrowLeft = sp;
        return sp;
    }

    /// <summary>مثلث أبيض متجه لأعلى (v1.2.0) — أيقونة زر البنزين، مستقلة عن الخطوط.</summary>
    public static Sprite TriangleSpriteUp()
    {
        if (cachedArrowUp != null) return cachedArrowUp;

        const int N = 128;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[N * N];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        // مثلث عمودي: القاعدة أسفل والرأس أعلى (اتجاه التسارع)
        for (int y = 0; y < N; y++)
        {
            float t = 1f - y / (float)(N - 1);
            int halfW = Mathf.RoundToInt(t * (N / 2 - 2));
            for (int x = N / 2 - halfW; x <= N / 2 + halfW; x++)
                pixels[y * N + x] = Color.white;
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        sp.name = "ArrowUp";
        cachedArrowUp = sp;
        return sp;
    }

    /// <summary>مربع أبيض صلب بحواف مشطوفة (v1.2.0) — أيقونة زر الفرامل (رمز التوقف).</summary>
    public static Sprite SquareSprite()
    {
        if (cachedSquare != null) return cachedSquare;

        const int N = 128;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[N * N];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        int min = 16, max = N - 17;
        int chamfer = 10; // قطع زوايا بسيط يمنح مظهر زر توقف ناعم
        for (int y = min; y <= max; y++)
        {
            int inset = 0;
            if (y < min + chamfer) inset = (min + chamfer) - y;
            else if (y > max - chamfer) inset = y - (max - chamfer);
            for (int x = min + inset; x <= max - inset; x++)
                pixels[y * N + x] = Color.white;
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        sp.name = "StopSquare";
        cachedSquare = sp;
        return sp;
    }

    /// <summary>شريط أبيض صلب (عنصر بناء الأيقونات — مستقل عن الخطوط).</summary>
    static void MakeSolidBar(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = go.GetComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = false;
    }

    /// <summary>
    /// زر أيقونة إيقاف مؤقت حقيقي: شريطان أبيضان داخل مربع داكن (v0.6.6).
    /// مرسوم بالصور بالكامل — مستقل عن أي خط، يظهر دائماً حتى لو فشل رندر كل النصوص.
    /// </summary>
    public static Button MakePauseButton(Transform parent, string name,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.50f);

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(onClick);

        // (v0.7.0) نقرة موحّدة لكل أزرار الواجهة
        btn.onClick.AddListener(() =>
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
        });

        float barW = size.x * 0.15f;
        float barH = size.y * 0.44f;
        float gap  = size.x * 0.11f;

        MakeSolidBar(go.transform, "BarL", new Vector2(-gap / 2f - barW / 2f, 0f), new Vector2(barW, barH));
        MakeSolidBar(go.transform, "BarR", new Vector2( gap / 2f + barW / 2f, 0f), new Vector2(barW, barH));

        return btn;
    }

    /// <summary>كانفاس Overlay بمقاس مرجعي 1920×1080 (مطابق لعرض الهواتف الأفقية).</summary>
    public static GameObject NewCanvas(string name, int sortingOrder)
    {
        GameObject go = new GameObject(name,
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        return go;
    }

    /// <summary>لوحة تغطي الشاشة كاملة (خلفية معتمة تحجب اللمس عن ما تحتها).</summary>
    public static RectTransform FullRect(Transform parent, string name, Color bg, bool raycast)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.color = bg;
        img.raycastTarget = raycast;
        return rt;
    }

    /// <summary>لوحة بحجم وموضع محدد (نقطة ارتكاز واحدة).</summary>
    public static RectTransform MakePanel(Transform parent, string name,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color, bool raycast)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return rt;
    }

    /// <summary>نص بحدود سوداء للقراءة فوق أي خلفية (raycast معطل دائماً).</summary>
    public static Text MakeText(Transform parent, string name,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
        int fontSize, TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Text text = go.GetComponent<Text>();
        text.font = BuiltinFont;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.fontStyle = style;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        return text;
    }

    /// <summary>زر كامل (صورة + نص + حدث ضغط) بنمط موحّد.</summary>
    public static Button MakeButton(Transform parent, string name, string label,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
        Color bg, Color fg, int fontSize, UnityEngine.Events.UnityAction onClick,
        FontStyle style = FontStyle.Bold)
    {
        GameObject go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = go.GetComponent<Image>();
        img.color = bg;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(onClick);

        // (v0.7.0) نقرة موحّدة لكل أزرار الواجهة
        btn.onClick.AddListener(() =>
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
        });

        // نص ممتد يملأ الزر
        // (v0.6.7) إصلاح جوهري: معامل label كان يُستقبل ولا يُسند أبداً —
        // كل أزرار القوائم كانت فارغة منذ المرحلة 4 (START RACE / RESUME / ...)
        Text text = MakeText(go.transform, "Label",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size,
            fontSize, TextAnchor.MiddleCenter, fg, style);
        text.text = label;
        RectTransform trt = text.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        trt.anchoredPosition = Vector2.zero;

        return btn;
    }

    static Sprite cachedCircle;
    static Sprite cachedClose;

    /// <summary>دائرة بيضاء مرسومة (مقبض السلايدر وأيقونة التحديد) — v0.8.0.</summary>
    public static Sprite CircleSprite()
    {
        if (cachedCircle != null) return cachedCircle;

        const int N = 96;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[N * N];
        float c = (N - 1) * 0.5f;
        float r = N * 0.5f - 2f;

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float dx = x - c, dy = y - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((r - d) / 1.5f); // حافة ناعمة
                pixels[y * N + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        cachedCircle = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        cachedCircle.name = "Circle";
        return cachedCircle;
    }

    /// <summary>علامة X بيضاء مرسومة (زر إغلاق الإعدادات) — v0.8.0.</summary>
    public static Sprite CloseSprite()
    {
        if (cachedClose != null) return cachedClose;

        const int N = 128;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[N * N];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        float t = N * 0.075f; // سماكة الخط
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float d1 = Mathf.Abs(x - y);                    // قطر \
                float d2 = Mathf.Abs(x - (N - 1 - y));          // قطر /
                if (d1 < t || d2 < t)
                    pixels[y * N + x] = Color.white;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        cachedClose = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        cachedClose.name = "Close";
        return cachedClose;
    }

    /// <summary>
    /// سلايدر أفقي كامل البناء بالكود (v0.8.0): مسار شبه شفاف + تعبئة عنبرية + مقبض دائري أبيض.
    /// المنطقة الملموسة = مستطيل السلايدر كاملاً (السحب من أي موضع يقفز بالمقبض إلى هناك).
    /// </summary>
    public static Slider MakeSlider(Transform parent, string name,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
        float min, float max, float value, UnityEngine.Events.UnityAction<float> onChange)
    {
        GameObject go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Slider));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Color trackCol = new Color(1f, 1f, 1f, 0.16f);

        // المسار (شريط رفيع بمنتصف السلايدر)
        RectTransform track = MakePanel(go.transform, "Track",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(size.x, 22f), trackCol, raycast: true);

        // منطقة التعبئة (تمتد بعرض السلايدر بارتفاع المسار)
        float barHalf = 11f;
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        RectTransform fart = fillArea.GetComponent<RectTransform>();
        fart.anchorMin = Vector2.zero;
        fart.anchorMax = Vector2.one;
        fart.offsetMin = new Vector2(0f, size.y * 0.5f - barHalf);
        fart.offsetMax = new Vector2(0f, -(size.y * 0.5f - barHalf));

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fill.transform.SetParent(fart.transform, false);
        RectTransform frt = fill.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = new Vector2(0f, 1f); // السلايدر يحرك anchorMax.x
        frt.offsetMin = Vector2.zero;
        frt.offsetMax = Vector2.zero;
        Image fillImg = fill.GetComponent<Image>();
        fillImg.color = Amber;
        fillImg.raycastTarget = true;

        // منطقة المقبض
        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        RectTransform hart = handleArea.GetComponent<RectTransform>();
        hart.anchorMin = Vector2.zero;
        hart.anchorMax = Vector2.one;
        hart.offsetMin = Vector2.zero;
        hart.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handle.transform.SetParent(hart.transform, false);
        RectTransform hrt = handle.GetComponent<RectTransform>();
        hrt.anchorMin = new Vector2(0f, 0.5f); // السلايدر يحرك anchorMin/Max.x
        hrt.anchorMax = new Vector2(0f, 0.5f);
        hrt.pivot = new Vector2(0.5f, 0.5f);
        hrt.sizeDelta = new Vector2(40f, 40f);
        Image handleImg = handle.GetComponent<Image>();
        handleImg.sprite = CircleSprite();
        handleImg.color = Color.white;
        handleImg.raycastTarget = true;

        // تركيب السلايدر
        Slider slider = go.GetComponent<Slider>();
        slider.fillRect = frt;
        slider.handleRect = hrt;
        slider.targetGraphic = handleImg;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = Mathf.Clamp(value, min, max);
        if (onChange != null) slider.onValueChanged.AddListener(onChange);

        return slider;
    }

    /// <summary>تنسيق موحّد للوقت: mm:ss.mmm (أو شرطات عندما لا يوجد زمن).</summary>
    public static string FormatTime(float t)
    {
        if (t <= 0f) return "--:--.---";

        int ms = Mathf.RoundToInt(t * 1000f);
        int minutes = ms / 60000; ms -= minutes * 60000;
        int seconds = ms / 1000;  ms -= seconds * 1000;
        return $"{minutes:00}:{seconds:00}.{ms:000}";
    }
}
