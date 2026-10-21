using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TurboRacing - المرحلة 4 (تطوير واجهة المرحلة 3)
/// HUD السباق: عدد اللفة الحالية من الإجمالي + زمن اللفة + الأفضل/الأخير
/// + عد تنازلي كبير وسط الشاشة + زر إيقاف مؤقت + شارة الإصدار.
/// يظهر فقط أثناء (Countdown/Racing/Paused) ويُدار من RaceFlow عبر فحص الحالة.
/// </summary>
public class RaceHUD : MonoBehaviour
{
    public static RaceHUD Instance { get; private set; }

    GameObject canvasGO;
    Text lapText;
    Text timeText;
    Text bestText;
    Text lastText;
    Text posText;
    Text countdownText;
    bool hudShown;

    // (v2.1.0) طبقة تشخيصية + تنبيه الإنقاذ
    Text dbgText;
    Text toastText;
    float dbgNext;
    float toastUntil;
    Transform carTr;
    CarController carCtrl;

    // (v2.6.0) خريطة الحلبة الحية — ميزة بأسلوب Racing Starter Kit
    UnityEngine.UI.RawImage mapImg;
    Texture2D mapTex;
    Color[] mapBg;
    bool mapReady;
    float mapNext;
    const int MapRes = 256;
    float mapOriginX, mapOriginZ, mapScale;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        BuildCanvas();
        BuildMapTexture();
        canvasGO.SetActive(false); // RaceFlow سيظهره عند بدء السباق
    }

    // ================= خريطة الحلبة الحية (v2.6.0) =================

    /// <summary>بناء خريطة الحلبة مرة واحدة من خط السباق المخبوز: خلفية داكنة
    /// نصف شفافة + شريط طريق بعرضه الحقيقي بحدود داكنة + مؤشر بداية كهرماني.
    /// التحديث afterwards = نقطة اللاعب فقط (نسخ + ختم + Apply بمعدل 8Hz).</summary>
    void BuildMapTexture()
    {
        TrackBuilder tb = TrackBuilder.Instance;
        if (tb == null || tb.RacingLine == null || tb.RacingLine.Length < 12)
        {
            if (mapImg != null) mapImg.gameObject.SetActive(false);
            return;
        }

        Vector3[] dense = TrackStyle.ResampleClosed(tb.RacingLine, 3f, out _);

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (Vector3 p in dense)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.z < minZ) minZ = p.z;
            if (p.z > maxZ) maxZ = p.z;
        }
        const float Margin = 12f;
        float w = Mathf.Max(1f, maxX - minX + Margin * 2f);
        float h = Mathf.Max(1f, maxZ - minZ + Margin * 2f);
        mapScale = Mathf.Min((MapRes - 10f) / w, (MapRes - 10f) / h);
        mapOriginX = (minX + maxX) * 0.5f;
        mapOriginZ = (minZ + maxZ) * 0.5f;

        var px = new Color[MapRes * MapRes];
        Color bg = new Color(0.05f, 0.06f, 0.09f, 0.62f);
        for (int i = 0; i < px.Length; i++) px[i] = bg;

        // عرض الطريق الحقيقي على الخريطة (نصف العرض المقيس ≈ 11م)
        float roadR = 11f * mapScale;
        Color outline = new Color(0f, 0f, 0f, 0.78f);
        Color asphalt = new Color(0.55f, 0.58f, 0.62f, 0.95f);
        foreach (Vector3 p in dense)
            StampDisc(px, WorldToMap(p), roadR + 2.2f, outline);
        foreach (Vector3 p in dense)
            StampDisc(px, WorldToMap(p), roadR, asphalt);

        // مؤشر خط البداية/النهاية
        StampDisc(px, WorldToMap(tb.RacingLine[0]), 4.5f, UiKit.Amber);

        mapBg = px;
        mapTex = new Texture2D(MapRes, MapRes, TextureFormat.RGBA32, false);
        mapTex.filterMode = FilterMode.Bilinear;
        mapTex.SetPixels(px);
        mapTex.Apply(false, false);
        if (mapImg != null) mapImg.texture = mapTex;
        mapReady = true;
    }

    Vector2 WorldToMap(Vector3 p)
    {
        return new Vector2((p.x - mapOriginX) * mapScale + MapRes * 0.5f,
                           (p.z - mapOriginZ) * mapScale + MapRes * 0.5f);
    }

    static void StampDisc(Color[] px, Vector2 c, float r, Color col)
    {
        int cx = Mathf.RoundToInt(c.x), cy = Mathf.RoundToInt(c.y);
        int ri = Mathf.CeilToInt(r);
        float r2 = r * r;
        for (int dy = -ri; dy <= ri; dy++)
        {
            for (int dx = -ri; dx <= ri; dx++)
            {
                if (dx * dx + dy * dy > r2) continue;
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= MapRes || y >= MapRes) continue;
                px[y * MapRes + x] = col;
            }
        }
    }

    /// <summary>تحديث نقطة اللاعب على الخريطة (8Hz كافٍ ويوفر البطارية).</summary>
    void UpdateMap()
    {
        if (!mapReady || Time.unscaledTime < mapNext) return;
        mapNext = Time.unscaledTime + 0.12f;
        if (carTr == null)
        {
            GameObject c = GameObject.Find("Car");
            if (c != null) carTr = c.transform;
            if (carTr == null) return;
        }

        var px = (Color[])mapBg.Clone();
        Vector2 m = WorldToMap(carTr.position);
        StampDisc(px, m, 5.2f, UiKit.Amber);
        StampDisc(px, m, 2.4f, Color.white);
        mapTex.SetPixels(px);
        mapTex.Apply(false, false);
    }

    void BuildCanvas()
    {
        // ترتيب 10: فوق الحلبة وتحت لوحات القوائم
        canvasGO = UiKit.NewCanvas("HUDCanvas", 10);

        lapText = UiKit.MakeText(canvasGO.transform, "LapText",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -30f),
            new Vector2(500f, 90f), 64, TextAnchor.UpperLeft, Color.white, FontStyle.Bold);

        // (v1.4.0) مركز اللاعب بين المنافسين P1/P5
        posText = UiKit.MakeText(canvasGO.transform, "PosText",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -128f),
            new Vector2(400f, 64f), 46, TextAnchor.UpperLeft, UiKit.Amber, FontStyle.Bold);

        timeText = UiKit.MakeText(canvasGO.transform, "TimeText",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f),
            new Vector2(700f, 110f), 84, TextAnchor.UpperCenter, Color.white, FontStyle.Bold);

        // إزاحة لليسار لتفادي زر الإيقاف أعلى اليمين
        bestText = UiKit.MakeText(canvasGO.transform, "BestText",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150f, -30f),
            new Vector2(700f, 60f), 44, TextAnchor.UpperRight, Color.white);

        lastText = UiKit.MakeText(canvasGO.transform, "LastText",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150f, -96f),
            new Vector2(700f, 60f), 44, TextAnchor.UpperRight, Color.white);

        countdownText = UiKit.MakeText(canvasGO.transform, "CountdownText",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f),
            new Vector2(800f, 280f), 170, TextAnchor.MiddleCenter, UiKit.Amber, FontStyle.Bold);
        countdownText.text = "";

        // (v2.1.0) طبقة تشخيصية صغيرة أعلى اليسار — إحداثيات السيارة وسطحها
        // ومسافتها عن خط السباق: كل لقطة شاشة من المستخدم تصبح تقريراً دقيقاً
        dbgText = UiKit.MakeText(canvasGO.transform, "DbgText",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -200f),
            new Vector2(640f, 130f), 30, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.85f));
        dbgText.text = "";

        // (v2.1.0) تنبيه الإنقاذ (توست) أسفل المؤقت
        toastText = UiKit.MakeText(canvasGO.transform, "ToastText",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f),
            new Vector2(1000f, 70f), 44, TextAnchor.UpperCenter, UiKit.Amber, FontStyle.Bold);
        toastText.text = "";

        UiKit.MakeText(canvasGO.transform, "VersionText",
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, -12f),
            new Vector2(300f, 40f), 26, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.55f))
            .text = "v" + GameVersion.Number;

        // (v2.6.0) خريطة الحلبة الحية أسفل اليسار (خارج نطاق أزرار القيادة)
        GameObject mapGo = new GameObject("MiniMap",
            typeof(UnityEngine.UI.RawImage));
        mapGo.transform.SetParent(canvasGO.transform, false);
        UnityEngine.UI.RawImage raw = mapGo.GetComponent<UnityEngine.UI.RawImage>();
        mapImg = raw;
        RectTransform mrt = mapGo.GetComponent<RectTransform>();
        mrt.anchorMin = new Vector2(0f, 0f);
        mrt.anchorMax = new Vector2(0f, 0f);
        mrt.pivot = new Vector2(0f, 0f);
        mrt.anchoredPosition = new Vector2(28f, 34f);
        mrt.sizeDelta = new Vector2(230f, 230f);
        raw.color = new Color(1f, 1f, 1f, 0.9f);
        raw.raycastTarget = false;

        // زر الإيقاف المؤقت (v0.6.6) - أيقونة حقيقية (شريطان أبيضان) مرسومة بالصور
        // أعلى يمين الشاشة — مستقلة تماماً عن الخطوط فتظهر دائماً على كل الأجهزة
        UiKit.MakePauseButton(canvasGO.transform, "BtnPause",
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(100f, 100f),
            () => { if (RaceFlow.Instance != null) RaceFlow.Instance.PauseRace(); });
    }

    void Update()
    {
        RaceFlow rf = RaceFlow.Instance;
        if (rf == null) return;

        // إدارة الظهور حسب حالة السباق (فحص رخيص مع كاش)
        bool show = rf.State == RaceState.Racing ||
                    rf.State == RaceState.Paused ||
                    rf.State == RaceState.Countdown;
        if (show != hudShown)
        {
            hudShown = show;
            if (canvasGO != null) canvasGO.SetActive(show);
        }
        if (!show) return;

        // العد التنازلي ووميض GO!
        if (rf.State == RaceState.Countdown)
            countdownText.text = Mathf.CeilToInt(Mathf.Max(0f, rf.CountdownRemaining)).ToString();
        else if (rf.State == RaceState.Racing && Time.time < rf.GoFlashUntil)
            countdownText.text = "GO!";
        else
            countdownText.text = "";

        LapManager lm = LapManager.Instance;
        if (lm == null) return;

        lapText.text = "LAP " + Mathf.Min(lm.LapsCompleted + 1, RaceFlow.LapsPerRace) +
                       "/" + RaceFlow.LapsPerRace;

        // (v1.4.0) المركز الحي بين الشبكة (لاعب + منافسون)
        if (AiManager.Instance != null && AiManager.Instance.FieldSize > 1)
            posText.text = "P" + AiManager.Instance.PlayerPosition + "/" + AiManager.Instance.FieldSize;
        else
            posText.text = "";

        timeText.text = UiKit.FormatTime(lm.CurrentLapTime);
        bestText.text = "BEST " + UiKit.FormatTime(lm.BestLapTime);
        lastText.text = lm.LastLapTime > 0f ? "LAST " + UiKit.FormatTime(lm.LastLapTime) : "";

        UpdateMap(); // (v2.6.0) نقطة اللاعب على الخريطة الحية
        UpdateDebug();
        if (toastText != null && Time.time > toastUntil && toastText.text.Length > 0)
            toastText.text = "";
    }

    /// <summary>(v2.1.0) توست رسالة قصيرة (نتائج الإنقاذ وأخبار المسار).</summary>
    public void ShowToast(string msg)
    {
        if (toastText == null) return;
        toastText.text = msg;
        toastUntil = Time.time + 2f;
    }

    /// <summary>(v2.1.0) الطبقة التشخيصية: موضع السيارة وسطحها ومسافتها عن
    /// خط السباق — تحويل أي شكوى مستخدم إلى بيانات قابلة للتصحيح فوراً.</summary>
    void UpdateDebug()
    {
        if (dbgText == null || Time.unscaledTime < dbgNext) return;
        dbgNext = Time.unscaledTime + 0.2f;

        if (carTr == null)
        {
            GameObject c = GameObject.Find("Car");
            if (c != null) { carTr = c.transform; carCtrl = c.GetComponent<CarController>(); }
        }
        if (carTr == null) { dbgText.text = ""; return; }

        Vector3 p = carTr.position;
        TrackBuilder tb = TrackBuilder.Instance;
        string surf = tb != null ? tb.SurfaceNameAt(p.x, p.z) : "?";

        float lineD = -1f;
        Vector3[] line = tb != null ? tb.RacingLine : null;
        if (line != null && line.Length > 0)
        {
            float best = float.MaxValue;
            for (int i = 0; i < line.Length; i++)
            {
                float dx = line[i].x - p.x, dz = line[i].z - p.z;
                float d = dx * dx + dz * dz;
                if (d < best) best = d;
            }
            lineD = Mathf.Sqrt(best);
        }

        // (v2.3.0) السطر الثالث الموسّع: مصدر خط السباق + السرعة + المدخلات المركّبة
        // + قناتا الإدخال (E=مسار EventSystem / T=الأصابع الفيزيائية) + عدد الأزرار
        // المربوطة + العجلات الملامسة — من أي لقطة نعرف فوراً أين انقطعت السلسلة:
        //   b2/4  → ربط ناقص (زر لم يُعثر عليه)
        //   E+ T0 → حدث وصل بلا لمس فيزيائي (خلل InputModule)
        //   T2 G- → لمس موجود والزر لا يستقبله (حجب raycast) ← الاستقصاء المباشر يعالجه
        //   w0/4  → السيارة معلّقة في الهواء/غارقة في الهندسة (مشكلة فيزياء لا إدخال)
        float spd = carCtrl != null ? carCtrl.SpeedKmh : 0f;
        string inp;
        if (carCtrl != null)
        {
            string g = carCtrl.DebugGasHeld ? "+" : "-";
            string s = carCtrl.DebugSteer > 0f ? "+" : carCtrl.DebugSteer < 0f ? "-" : "0";
            string b = carCtrl.DebugBrakeHeld ? "+" : "-";
            string e = carCtrl.DebugEventGas ? "+" : "-";
            string t = carCtrl.DebugTouchGas ? "+" : "-";
            inp = $"G{g} S{s} B{b}  E{e} t{t} T{carCtrl.DebugTouchCount} " +
                  $"b{carCtrl.DebugBoundButtons}/4 w{carCtrl.DebugGroundedWheels}/4";
        }
        else inp = "?";
        string src = TrackBuilder.LineSourceTag;

        // (v2.8.0) السطر الرابع — ديناميكا طبقة VPP-style حية:
        //   L1.00  عامل الإطلاق (0.38..1.00 أثناء رامب القابض من الوقوف)
        //   T1.0   أدنى معامل جر للعجلات المدفوعة (تراكيشن كونترول؛ <1 = قص عزم جارٍ)
        //   P-0.32 سرعة الميلان الأمامي بالراد/ث (اهتزاز قبوة كبير = مشكلة تعليق)
        //   r0.6   رامب ضغط الفرملة (يتصاعد 0→1 أثناء الفرملة)
        string dyn = carCtrl != null
            ? $"L{carCtrl.DebugLaunchFactor:0.00} T{carCtrl.DebugTcFactor:0.0} " +
              $"P{carCtrl.DebugPitchRate:+0.00;-0.00} r{carCtrl.DebugBrakeRamp:0.0}"
            : "?";

        dbgText.text = $"X {p.x:0.#}  Y {p.y:0.#}  Z {p.z:0.#}\n" +
                       $"{surf}   line {(lineD >= 0f ? lineD.ToString("0") + "m" : "-")}\n" +
                       $"{src}   {spd:0}km/h  {inp}\n" +
                       $"{dyn}";
    }
}
