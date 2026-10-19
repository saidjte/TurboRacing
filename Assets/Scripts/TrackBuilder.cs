using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TurboRacing - v1.0.0 (لعبة ثلاثية الأبعاد حقيقية) — مطوّرة v1.3.0 (مسار أعرض وبلا حواجز)
/// يبني الحلبة كعالم مجسّم كامل بالكود (بلا أي أصول خارجية):
/// - أرضية عشب واسعة بنسيج مكرر (خطوط جز + حبيبات)
/// - طريق إسفلت كشبكة واحدة + خط بداية/نهاية مربعات كُرْد
/// - كيرب أحمر/أبيض مرفوع 7سم على كل حواف الطريق
/// - v1.3.0: لا حواجز إطلاقاً — أزيلت الجدران الخرسانية والشدّدات كلها
///   (سيارة حرة على كل عرض الطريق، وحاجز شفاف عند طرف العالم فقط كصمام أمان)
/// - دعائم كرتونية Low-Poly بأسلوب أصول المتجر المختارة (Environment Track LowPoly
///   Cartoon Props + Cartoon Race Track Oval): مخاريط مرورية ومكدّسات إطارات
/// - أشجار (جذع + تُيج) ومدرّجات جمهور ولوحات إعلانات وبوابة انطلاق
/// - v1.3.0: طريق عرضه 8 بلاطات (16 متراً بدل 12) على خريطة 72×44 بلاطة
///   مع إعادة تموضع كل الديكور (لوحات/بوابة الانطلاق) وبوابات اللفة والانطلاق
/// أداءً: كل النوع الواحد مدموج في شبكة واحدة = دورة رسم قليلة جداً.
/// </summary>
public class TrackBuilder : MonoBehaviour
{
    public static TrackBuilder Instance { get; private set; }

    [Header("أبعاد البلاطة")]
    [Tooltip("حجم البلاطة بالوحدات العالمية (2 = طريق عرضه 16 متراً بالخريطة الجديدة)")]
    public float tileSize = 2f;

    // --- نقطة الانطلاق (تُحسب تلقائياً من الخريطة) ---
    public Vector3 StartPosition { get; private set; }
    public float StartYaw { get; private set; }

    // --- أبعاد عالمية للكاميرا (وضع القائمة) ---
    public Vector3 MapCenter { get; private set; }
    public float MapWidthUnits { get; private set; }
    public float MapHeightUnits { get; private set; }

    /// <summary>
    /// بوابات اللفة: محور + مستوى عبور + نطاق على المحور الآخر + اتجاه.
    /// axis: 0 = X ، 1 = Z. dir: +1/-1 اتجاه العبور الصحيح.
    /// [0..2] نقاط تفتيش إلزامية، [3] خط النهاية. (نفس قيم v0.6.x حرفياً)
    /// </summary>
    public struct LapGate
    {
        public int axis;
        public float plane;
        public float spanMin;
        public float spanMax;
        public int dir;
    }

    public LapGate[] LapGates { get; private set; }

    /// <summary>
    /// خط السباق (v1.4.0): نقاط مركز الطريق على حلقة مستطيل مقوّس بالاتجاه الرسمي
    /// (شرقاً على العلوي ← جنوباً على الأيمن ← غرباً على السفلي ← شمالاً على الأيسر).
    /// يُستخدم لقيادة سيارات المنافسين وحساب الترتيب.
    /// </summary>
    public Vector3[] RacingLine { get; private set; }

    // --- أبعاد خريطة الحلبة بالبلاطات (v1.3.0: طريق أعرض 16م بلا حواجز) ---
    const int W = 72;    // 144 متراً عالمياً
    const int H = 44;    // 88 متراً عالمياً
    const int RoadW = 8; // عرض الطريق بالبلاطات (16 متراً)

    // رموز الخريطة: '.' عشب   'R' طريق   'S' خط البداية (v1.3.0: لا جدران)
    const char GrassChar = '.';
    const char RoadChar = 'R';
    const char StartChar = 'S';

    char[,] map;
    Material matGround, matRoad, matStart, matCurb, matPaint;

    void Awake()
    {
        Instance = this;

        map = BuildMap();
        EnsureMaterials();

        BuildGround();
        BuildRoadAndStart();
        BuildCurbs();
        BuildScenery();
        BuildWorldBounds();
        ComputeStartPose();
        BuildLapGates();
        BuildRacingLine();   // v1.4.0: مسار سيارات المنافسين
        SpawnParkedCars();   // v1.4.0: سيارات متوقفة كديكور خارج المسار

        MapWidthUnits = W * tileSize;
        MapHeightUnits = H * tileSize;
        MapCenter = new Vector3(MapWidthUnits * 0.5f, 0f, MapHeightUnits * 0.5f);

        Debug.Log($"[TrackBuilder] الحلبة ثلاثية الأبعاد جاهزة: {W}×{H} بلاطة، انطلاق من {StartPosition}");
    }

    // ================= تصميم الحلبة (v1.3.0: أعرض وبلا حواجز) =================

    static char[,] BuildMap()
    {
        char[,] m = new char[W, H];

        Fill(m, GrassChar);

        // المستقيمات الأربعة (عرض 8 بلاطات = 16 متراً) — فراغ عشبي 2 بلاطة للحواف
        Rect(m, RoadChar, 3, 2, 66, RoadW);    // السفلي (z منخفض)
        Rect(m, RoadChar, 3, 34, 66, RoadW);   // العلوي (خط البداية)
        Rect(m, RoadChar, 3, 2, RoadW, 40);    // الأيسر
        Rect(m, RoadChar, 63, 2, RoadW, 40);   // الأيمن

        // (v1.3.0) أزيلت كل الجدران: لا إطار خارجي، لا إطار الجزيرة،
        // ولا شدّدات ضيّق الطريق — السباق حر على كامل عرض الإسفلت.

        // خط البداية/النهاية: عمودان مقلّمان على المستقيم العلوي (وسطه تقريباً)
        for (int z = 34; z <= 41; z++)
        {
            m[36, z] = StartChar;
            m[37, z] = StartChar;
        }

        return m;
    }

    static void Fill(char[,] m, char c)
    {
        for (int x = 0; x < W; x++)
            for (int y = 0; y < H; y++)
                m[x, y] = c;
    }

    static void Rect(char[,] m, char c, int x, int y, int w, int h)
    {
        for (int i = x; i < x + w; i++)
            for (int j = y; j < y + h; j++)
                m[i, j] = c;
    }

    /// <summary>رسم إطار مجوّف (سماكة بلاطة واحدة) داخل المستطيل — محفوظ للاستخدام المستقبلي.
    /// (v1.3.0: بلا جدران، فلا يُستدعى حالياً)</summary>
    static void Ring(char[,] m, char c, int x, int y, int w, int h)
    {
        Rect(m, c, x, y, w, 1);          // الصف العلوي
        Rect(m, c, x, y + h - 1, w, 1);  // الصف السفلي
        Rect(m, c, x, y, 1, h);          // العمود الأيسر
        Rect(m, c, x + w - 1, y, 1, h);  // العمود الأيمن
    }

    // ================= نقطة الانطلاق (v1.3.0) =================

    void ComputeStartPose()
    {
        // أمام السيارة خط البداية مباشرة (العمودان 36-37 على المستقيم العلوي)
        float startX = 34f * tileSize + tileSize * 0.5f; // مركز العمود 34
        float startZ = 38f * tileSize;                   // وسط طريق الـ 8 صفوف (z 68..84)

        StartPosition = new Vector3(startX, 0f, startZ);
        StartYaw = 90f; // transform.forward يشير نحو +X => انطلاق شرقاً (السباق مع عقارب الساعة)
    }

    /// <summary>
    /// بوابات اللفة بإحداثيات عالمية (v1.3.0، بلاطة 2 متر):
    ///   المستقيم العلوي z 68..84 ، السفلي z 4..20 ، الأيمن x 126..142 ، الأيسر x 6..22
    /// اتجاه السباق: شرقاً على العلوي ← جنوباً على الأيمن ← غرباً على السفلي ← شمالاً على الأيسر.
    /// نطاق كل بوابة = مدى مركز السيارة على الطريق بالضبط (الطريق مزاحاً نصف بلاطة من كل جهة)
    /// فلا يُحتسب أي موقع خارج الإسفلت ولا تُفوّت أي لفة صحيحة.
    /// [0] يمين: عبور Z=44 هبوطاً (X من 126.5 إلى 141.5)
    /// [1] أسفل: عبور X=74 يساراً وسط المستقيم السفلي (Z من 4.5 إلى 19.5)
    /// [2] يسار: عبور Z=44 صعوداً (X من 6.5 إلى 21.5)
    /// [3] نهاية: عبور X=74 يميناً وسط خط البداية المقلّم (Z من 68.5 إلى 83.5)
    /// </summary>
    void BuildLapGates()
    {
        LapGates = new[]
        {
            new LapGate { axis = 1, plane = 44f, spanMin = 126.5f, spanMax = 141.5f, dir = -1 },
            new LapGate { axis = 0, plane = 74f, spanMin = 4.5f,   spanMax = 19.5f,  dir = -1 },
            new LapGate { axis = 1, plane = 44f, spanMin = 6.5f,   spanMax = 21.5f,  dir = +1 },
            new LapGate { axis = 0, plane = 74f, spanMin = 68.5f,  spanMax = 83.5f,  dir = +1 }
        };
    }

    // ================= خط السباق (v1.4.0) =================

    void BuildRacingLine()
    {
        var pts = new List<Vector3>(192);

        // حلقة مستطيل مقوّس على مراكز المستقيمات الأربعة + أقواس Bezier في الزوايا
        const float xL = 14f, xR = 134f, zT = 76f, zB = 12f, R = 13f;

        AddLine(pts, new Vector3(xL + R, 0f, zT), new Vector3(xR - R, 0f, zT), 2f);        // العلوي شرقاً (يبدأ من نهاية قوس الشمال الغربي)
        AddArc(pts, new Vector3(xR - R, 0f, zT), new Vector3(xR, 0f, zT), new Vector3(xR, 0f, zT - R), 6);
        AddLine(pts, new Vector3(xR, 0f, zT - R), new Vector3(xR, 0f, zB + R), 2f);       // الأيمن جنوباً
        AddArc(pts, new Vector3(xR, 0f, zB + R), new Vector3(xR, 0f, zB), new Vector3(xR - R, 0f, zB), 6);
        AddLine(pts, new Vector3(xR - R, 0f, zB), new Vector3(xL + R, 0f, zB), 2f);       // السفلي غرباً
        AddArc(pts, new Vector3(xL + R, 0f, zB), new Vector3(xL, 0f, zB), new Vector3(xL, 0f, zB + R), 6);
        AddLine(pts, new Vector3(xL, 0f, zB + R), new Vector3(xL, 0f, zT - R), 2f);       // الأيسر شمالاً
        AddArc(pts, new Vector3(xL, 0f, zT - R), new Vector3(xL, 0f, zT), new Vector3(xL + R, 0f, zT), 6);
        // (v1.4.0 fix) الحلقة تنغلق ذاتياً: نهاية قوس الشمال الغربي (xL+R, zT) =
        // أول نقطة في المستقيم العلوي بعد الالتفاف — بلا جسر ولا انعكاس 180°.

        // تدوير القائمة لتبدأ قرب خط البداية (69,76) — فهرس صفر = منطقة الانطلاق
        int startIdx = 0; float best = float.MaxValue;
        for (int i = 0; i < pts.Count; i++)
        {
            float d = (pts[i].x - 69f) * (pts[i].x - 69f) + (pts[i].z - 76f) * (pts[i].z - 76f);
            if (d < best) { best = d; startIdx = i; }
        }

        var rotated = new List<Vector3>(pts.Count);
        for (int i = 0; i < pts.Count; i++) rotated.Add(pts[(startIdx + i) % pts.Count]);
        RacingLine = rotated.ToArray();

        Debug.Log($"[TrackBuilder] خط السباق جاهز: {RacingLine.Length} نقطة");
    }

    static void AddLine(List<Vector3> pts, Vector3 from, Vector3 to, float step)
    {
        Vector3 seg = to - from;
        float len = seg.magnitude;
        int count = Mathf.Max(1, Mathf.RoundToInt(len / step));
        for (int i = 0; i < count; i++)
            pts.Add(from + seg * ((float)i / count));
    }

    /// <summary>قوس Bezier تربيعي (يستثني نقطة البداية المكررة مع المستقيم السابق).</summary>
    static void AddArc(List<Vector3> pts, Vector3 a, Vector3 c, Vector3 b, int samples)
    {
        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            float it = 1f - t;
            pts.Add(it * it * a + 2f * t * it * c + t * t * b);
        }
    }

    /// <summary>أقرب نقطة على خط السباق حول فهرس مرجعي (نافذة ±14 — بلا مسح كامل).</summary>
    public int NearestWaypoint(Vector3 pos, int hint)
    {
        if (RacingLine == null || RacingLine.Length == 0) return 0;
        int n = RacingLine.Length;
        int best = 0; float bestD = float.MaxValue;
        int window = Mathf.Min(14, n);
        for (int off = -window; off <= window; off++)
        {
            int i = ((hint + off) % n + n) % n;
            float dx = RacingLine[i].x - pos.x, dz = RacingLine[i].z - pos.z;
            float d = dx * dx + dz * dz;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// <summary>نقطة على الخط أمام فهرس بمسافة معينة مع إزاحة جانبية ثابتة (حارة المنافس).
    /// lateral موجب = يمين اتجاه السير، سالب = يساره.</summary>
    public Vector3 PointAlongLine(int startIdx, float dist, float lateral)
    {
        Vector3[] line = RacingLine;
        int n = line.Length;
        Vector3 pos = line[startIdx];
        int i = startIdx;
        float remaining = dist;
        Vector3 dir = Vector3.forward;
        int guard = 0;

        while (remaining > 0.0001f && guard++ < 1024)
        {
            Vector3 next = line[(i + 1) % n];
            Vector3 seg = next - pos; seg.y = 0f;
            float len = seg.magnitude;
            if (len <= 0.0001f) { i = (i + 1) % n; pos = next; continue; }
            dir = seg / len;
            if (remaining <= len)
            {
                pos = pos + dir * remaining;
                remaining = 0f;
                break;
            }
            remaining -= len;
            pos = next;
            i = (i + 1) % n;
        }

        Vector3 right = Vector3.Cross(Vector3.up, dir);
        return pos + right * lateral;
    }

    /// <summary>مجموع تغيّر الاتجاه (درجات) على مسافة أمام فهرس — مؤشر انحناء المنعطفات.
    /// المستقيمات ≈ 0، الأقواس ≈ 40°..90° بحسب المسافة المقطوعة.</summary>
    public float LineHeadingChange(int startIdx, float dist)
    {
        Vector3[] line = RacingLine;
        int n = line.Length;
        float travelled = 0f, acc = 0f;
        float prevAng = float.NaN;
        int i = startIdx;
        int guard = 0;

        while (travelled < dist && guard++ < 512)
        {
            Vector3 seg = line[(i + 1) % n] - line[i % n]; seg.y = 0f;
            float len = seg.magnitude;
            if (len > 0.0001f)
            {
                float ang = Mathf.Atan2(seg.z, seg.x) * Mathf.Rad2Deg;
                if (!float.IsNaN(prevAng)) acc += Mathf.Abs(Mathf.DeltaAngle(prevAng, ang));
                prevAng = ang;
                travelled += len;
            }
            i++;
        }
        return acc;
    }

    // ================= سيارات متوقفة كديكور (v1.4.0) =================

    /// <summary>
    /// زيادة عدد السيارات في المشهد: موديلات كاملة متوقفة خارج المسار
    /// (موقف خلف المدرّج الشمالي + زاوية جنوبية شرقية) بمصادمات صندوقية ثابتة.
    /// </summary>
    void SpawnParkedCars()
    {
        CarModels.CarSpec[] pool =
        {
            CarModels.Get(1), CarModels.Get(3), CarModels.Get(4),
            CarModels.Get(2), CarModels.Get(5),
        };

        Vector3[] spots =
        {
            new Vector3(62f, 0f, 93.0f),      // الموقف الشمالي خلف المدرّج
            new Vector3(67.5f, 0f, 93.3f),
            new Vector3(73f, 0f, 92.8f),
            new Vector3(98f, 0f, -8.0f),      // الزاوية الجنوبية الشرقية
            new Vector3(104f, 0f, -8.3f),
        };
        float[] yaws = { 270f, 270f, 270f, 90f, 90f };

        for (int i = 0; i < spots.Length && i < pool.Length; i++)
        {
            CarModels.Handles h = CarModels.Build(transform, pool[i]);
            h.Root.position = spots[i];
            h.Root.rotation = Quaternion.Euler(0f, yaws[i], 0f);

            // مصادم ثابت بسيط (تتصادم معه سيارات اللاعب/المنافسين)
            BoxCollider col = h.Root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.45f, 0f);
            col.size = new Vector3(1.15f, 0.9f, 2.6f);
        }
    }

    // ================= استعلام السطح =================

    /// <summary>هل الموضع العالمي على العشب (خارج الطريق)؟ يُستخدم لعقاب العشب.</summary>
    public bool IsGrass(Vector3 worldPos)
    {
        if (map == null) return false;

        int tx = Mathf.FloorToInt(worldPos.x / tileSize);
        int tz = Mathf.FloorToInt(worldPos.z / tileSize);

        if (tx < 0 || tz < 0 || tx >= W || tz >= H) return true; // خارج الخريطة = عشب
        char c = map[tx, tz];
        return c != RoadChar && c != StartChar;
    }

    // ================= المواد والنسيج الإجرائي =================

    void EnsureMaterials()
    {
        matGround = MakeMat(GrassTexture(), 0.06f);
        matRoad   = MakeMat(AsphaltTexture(), 0.12f);
        matStart  = MakeMat(CheckerTexture(), 0.08f);
        matCurb   = MakeMat(CurbTexture(), 0.10f);
        matPaint  = MakeMat(WhiteTexture(), 0.25f);
    }

    static Material MakeMat(Texture2D tex, float smooth)
    {
        // TR/Paint المخصص (يُولَّد وقت البناء داخل Resources) = دعم مضمون
        // لألوان الرؤوس + ظلال؛ البديل Standard للمحرر قبل أول بناء.
        Shader sh = Shader.Find("TR/Paint");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Legacy Shaders/Diffuse");

        Material m = new Material(sh);
        m.name = "MAT_" + tex.name;
        m.mainTexture = tex;

        if (m.HasProperty("_SpecColor"))
            m.SetColor("_SpecColor", new Color(0.10f, 0.10f, 0.10f, 1f)); // لمعان منخفض للمسار
        if (m.HasProperty("_Shininess"))
            m.SetFloat("_Shininess", Mathf.Clamp(smooth, 0.01f, 1f));
        if (m.HasProperty("_Glossiness"))
        {
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", 0f);
        }
        if (m.HasProperty("_GlossyReflections"))
            m.SetFloat("_GlossyReflections", 0f); // لا انعكاسات بيئية (موبايل)

        return m;
    }

    /// <summary>نسيج أبيض مصمت (4×4): يظهر لون الرأس كما هو للعناصر الملوّنة رأسياً.</summary>
    static Texture2D WhiteTexture()
    {
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply(false, false);
        tex.name = "White";
        return tex;
    }

    /// <summary>عشب: خضرة متدرجة + خطوط جز أفقية باهتة + حبيبات وشفرات أفتح.</summary>
    static Texture2D GrassTexture()
    {
        const int N = 256;
        Texture2D tex = NewTex(N);
        var rng = new System.Random(77);
        Color[] px = new Color[N * N];

        for (int y = 0; y < N; y++)
        {
            // خطوط جز كل 42 بكسل (≈ 0.5م عالمياً عند تكرار 3م)
            bool mow = ((y / 42) & 1) == 0;
            for (int x = 0; x < N; x++)
            {
                float n = (float)rng.NextDouble() * 0.10f - 0.05f;
                float mowMul = mow ? 1.06f : 0.94f;
                float blade = rng.NextDouble() < 0.015 ? 0.18f : 0f;
                float r = (0.235f + n + blade) * mowMul;
                float g = (0.470f + n + blade * 1.4f) * mowMul;
                float b = (0.160f + n * 0.6f + blade * 0.5f) * mowMul;
                px[y * N + x] = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, false);
        tex.name = "Grass";
        return tex;
    }

    /// <summary>إسفلت: رمادي داكن بحبيبات متفاوتة وبقع قيمة خفيفة.</summary>
    static Texture2D AsphaltTexture()
    {
        const int N = 128;
        Texture2D tex = NewTex(N);
        var rng = new System.Random(41);
        Color[] px = new Color[N * N];

        for (int i = 0; i < N * N; i++)
        {
            float n = (float)rng.NextDouble();
            float v = 0.30f + n * 0.09f;               // أساس داكن
            if (rng.NextDouble() < 0.02) v += 0.08f;    // حصاة أفتح
            if (rng.NextDouble() < 0.015) v -= 0.06f;   // بقعة أغمق
            px[i] = new Color(v, v, v * 1.02f, 1f);
        }

        tex.SetPixels(px);
        tex.Apply(false, false);
        tex.name = "Asphalt";
        return tex;
    }

    /// <summary>مربعات كُرْد أبيض/أسود 8×8 لخط البداية واللافتة.</summary>
    static Texture2D CheckerTexture()
    {
        const int N = 64;
        const int Cell = 8;
        Texture2D tex = NewTex(N);
        Color[] px = new Color[N * N];

        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                bool c = ((x / Cell) + (y / Cell)) % 2 == 0;
                px[y * N + x] = c ? Color.white : new Color(0.08f, 0.08f, 0.08f, 1f);
            }

        tex.SetPixels(px);
        tex.Apply(false, false);
        tex.name = "Checker";
        return tex;
    }

    /// <summary>كيرب: شريطان (أحمر ثم أبيض) على المحور U — كل شريط 0.5 متر.</summary>
    static Texture2D CurbTexture()
    {
        const int Nw = 64, Nh = 32;
        Texture2D tex = NewTex(Nw, Nh);
        Color[] px = new Color[Nw * Nh];
        var rng = new System.Random(9);

        Color red = new Color(0.78f, 0.10f, 0.10f);
        Color white = new Color(0.92f, 0.92f, 0.90f);

        for (int y = 0; y < Nh; y++)
            for (int x = 0; x < Nw; x++)
            {
                Color c = x < Nw / 2 ? red : white;
                float n = (float)rng.NextDouble() * 0.05f - 0.025f;
                px[y * Nw + x] = new Color(c.r + n, c.g + n, c.b + n, 1f);
            }

        tex.SetPixels(px);
        tex.Apply(false, false);
        tex.name = "Curb";
        return tex;
    }

    static Texture2D NewTex(int size) => NewTex(size, size);

    static Texture2D NewTex(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;
        return tex;
    }

    // ================= بناء مجسّم: أرض/طريق/كيرب/جدران =================

    GameObject EmitMesh(MeshAccumulator acc, Material mat, string name,
                        bool castShadows, bool makeStatic)
    {
        if (acc.VertexCount == 0) return null;

        Mesh mesh = acc.Build(name + "_Mesh");
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = castShadows
            ? UnityEngine.Rendering.ShadowCastingMode.On
            : UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;

        if (makeStatic) go.isStatic = true;
        return go;
    }

    void BuildGround()
    {
        // أرضية عشب واسعة تمتد خارج حدود الحلبة (أفق المشهد المجسّم)
        var acc = new MeshAccumulator();
        float mx = -40f, Mx = W * tileSize + 40f;
        float mz = -40f, Mz = H * tileSize + 40f;
        acc.AddTop(mx, Mx, mz, Mz, 0f, 1f / 3f, Color.white); // تكرار النسيج كل 3 أمتار
        EmitMesh(acc, matGround, "Ground", castShadows: false, makeStatic: true);

        // (v1.1.0) مصادم الأرض للفيزياء الحقيقية (WheelColliders): صندوق واحد هائل
        // سطحه العلوي عند مستوى الطريق (0.02) — العشب أسفله بـ 2سم فقط (فرق غير محسوس)
        BoxCollider ground = gameObject.AddComponent<BoxCollider>();
        ground.center = new Vector3((mx + Mx) * 0.5f, -0.5f + 0.02f, (mz + Mz) * 0.5f);
        ground.size = new Vector3(Mx - mx, 1f, Mz - mz);
    }

    void BuildRoadAndStart()
    {
        var road = new MeshAccumulator();
        var start = new MeshAccumulator();

        for (int x = 0; x < W; x++)
        {
            for (int z = 0; z < H; z++)
            {
                char c = map[x, z];
                if (c != RoadChar && c != StartChar) continue;

                float x0 = x * tileSize, x1 = (x + 1) * tileSize;
                float z0 = z * tileSize, z1 = (z + 1) * tileSize;

                if (c == StartChar)
                {
                    // uv متصل عبر البلاطات حتى يستمر نمط الكُرْد عبر الشريط كله (v1.2.0: مرجع العمود 36/الصف 36)
                    float u = (x - 36f), v = (z - 36f);
                    start.AddQuad(
                        new Vector3(x0, 0.02f, z0), new Vector3(x0, 0.02f, z1),
                        new Vector3(x1, 0.02f, z1), new Vector3(x1, 0.02f, z0),
                        new Vector2(u, v), new Vector2(u, v + 1f),
                        new Vector2(u + 1f, v + 1f), new Vector2(u + 1f, v), Color.white);
                }
                else
                {
                    // تنويع سطوع خفيف بين البلاطات (يكسر الرتابة دون نسيج إضافي)
                    float tint = ((x * 7 + z * 13) & 1) == 0 ? 1f : 0.955f;
                    road.AddTop(x0, x1, z0, z1, 0.02f, 0.5f,
                        new Color(tint, tint, tint, 1f)); // تكرار كل بلاطة
                }
            }
        }

        EmitMesh(road, matRoad, "Road", castShadows: false, makeStatic: true);
        EmitMesh(start, matStart, "StartLine", castShadows: false, makeStatic: true);
    }

    /// <summary>كيرب مرفوع (0.07م) على كل حافة طريق مجاورة لغير الطريق.</summary>
    void BuildCurbs()
    {
        var acc = new MeshAccumulator();
        const float h = 0.07f;  // الارتفاع
        const float wdt = 0.55f; // العرض داخل الطريق
        Color topCol = Color.white;

        for (int x = 0; x < W; x++)
        {
            for (int z = 0; z < H; z++)
            {
                if (!IsRoadLike(x, z)) continue;

                float x0 = x * tileSize, x1 = (x + 1) * tileSize;
                float z0 = z * tileSize, z1 = (z + 1) * tileSize;

                // الحافة الشمالية (+Z): شريط بمحاذاة X
                if (!IsRoadLike(x, z + 1))
                {
                    float a = z1 - wdt, b = z1;
                    acc.AddQuad(new Vector3(x0, h, a), new Vector3(x0, h, b),
                                new Vector3(x1, h, b), new Vector3(x1, h, a),
                                new Vector2(x0, a), new Vector2(x0, b),
                                new Vector2(x1, b), new Vector2(x1, a), topCol);
                    AddCurbOuterFace(acc, x0, x1, z1, z1, h, true);
                }
                // الحافة الجنوبية (-Z)
                if (!IsRoadLike(x, z - 1))
                {
                    float a = z0, b = z0 + wdt;
                    acc.AddQuad(new Vector3(x0, h, a), new Vector3(x0, h, b),
                                new Vector3(x1, h, b), new Vector3(x1, h, a),
                                new Vector2(x0, a), new Vector2(x0, b),
                                new Vector2(x1, b), new Vector2(x1, a), topCol);
                    AddCurbOuterFace(acc, x0, x1, z0, z0, h, false);
                }
                // الحافة الشرقية (+X): شريط بمحاذاة Z
                if (!IsRoadLike(x + 1, z))
                {
                    float a = x1 - wdt, b = x1;
                    acc.AddQuad(new Vector3(a, h, z0), new Vector3(a, h, z1),
                                new Vector3(b, h, z1), new Vector3(b, h, z0),
                                new Vector2(z0, a), new Vector2(z1, a),
                                new Vector2(z1, b), new Vector2(z0, b), topCol);
                    AddCurbOuterFace(acc, x1, x1, z0, z1, h, true);
                }
                // الحافة الغربية (-X)
                if (!IsRoadLike(x - 1, z))
                {
                    float a = x0, b = x0 + wdt;
                    acc.AddQuad(new Vector3(a, h, z0), new Vector3(a, h, z1),
                                new Vector3(b, h, z1), new Vector3(b, h, z0),
                                new Vector2(z0, a), new Vector2(z1, a),
                                new Vector2(z1, b), new Vector2(z0, b), topCol);
                    AddCurbOuterFace(acc, x0, x0, z0, z1, h, false);
                }
            }
        }

        EmitMesh(acc, matCurb, "Curbs", castShadows: false, makeStatic: true);
    }

    bool IsRoadLike(int x, int z)
    {
        if (x < 0 || z < 0 || x >= W || z >= H) return false;
        char c = map[x, z];
        return c == RoadChar || c == StartChar;
    }

    /// <summary>وجه رأسي خارجي للكيرب (شفة صغيرة تحاكي ارتفاع الحافة).
    /// الاستدعاءان: حافة على X ⟹ a=مدى X و c=ثابت Z | حافة على Z ⟹ a=ثابت X و c=مدى Z.</summary>
    static void AddCurbOuterFace(MeshAccumulator acc,
        float a0, float a1, float c0, float c1, float h, bool positiveDir)
    {
        if (Mathf.Approximately(c0, c1))
        {
            // وجه بمحاذاة X عند z ثابت
            float x0 = a0, x1 = a1, zc = c0;
            if (positiveDir)
                acc.AddQuad(new Vector3(x0, 0f, zc), new Vector3(x1, 0f, zc),
                            new Vector3(x1, h, zc), new Vector3(x0, h, zc),
                            new Vector2(x0, 0f), new Vector2(x1, 0f),
                            new Vector2(x1, h), new Vector2(x0, h), Color.white);
            else
                acc.AddQuad(new Vector3(x1, 0f, zc), new Vector3(x0, 0f, zc),
                            new Vector3(x0, h, zc), new Vector3(x1, h, zc),
                            new Vector2(x1, 0f), new Vector2(x0, 0f),
                            new Vector2(x0, h), new Vector2(x1, h), Color.white);
        }
        else
        {
            // وجه بمحاذاة Z عند x ثابت
            float xc = a0, z0 = c0, z1 = c1;
            if (positiveDir)
                acc.AddQuad(new Vector3(xc, 0f, z0), new Vector3(xc, h, z0),
                            new Vector3(xc, h, z1), new Vector3(xc, 0f, z1),
                            new Vector2(z0, 0f), new Vector2(z0, h),
                            new Vector2(z1, h), new Vector2(z1, 0f), Color.white);
            else
                acc.AddQuad(new Vector3(xc, 0f, z0), new Vector3(xc, 0f, z1),
                            new Vector3(xc, h, z1), new Vector3(xc, h, z0),
                            new Vector2(z0, 0f), new Vector2(z1, 0f),
                            new Vector2(z1, h), new Vector2(z0, h), Color.white);
        }
    }

    /// <summary>
    /// (v1.3.0) بلا حواجز: أزيلت الجدران الخرسانية ومصادماتها كلياً.
    /// بقي فقط حاجز شفاف عند أطراف العالم (خارج هامش العشب بـ 40م) كصمام أمان
    /// يمنع سقوط السيارة خارج المشهد — غير مرئي ولا يلمس السباق أصلاً.
    /// </summary>
    void BuildWorldBounds()
    {
        GameObject boundsGO = new GameObject("WorldBounds");
        boundsGO.transform.SetParent(transform, false);

        float mx = -40f, Mx = W * tileSize + 40f; // مدى أرضية العشب نفسها
        float mz = -40f, Mz = H * tileSize + 40f;
        float h = 12f;  // ارتفاع كافٍ لمنع القفز فوقه
        float t = 2f;   // سماكة الجدار الشفاف

        AddBoundBox(boundsGO, new Vector3(mx - t * 0.5f, h * 0.5f, (mz + Mz) * 0.5f), new Vector3(t, h, Mz - mz + t * 2f));
        AddBoundBox(boundsGO, new Vector3(Mx + t * 0.5f, h * 0.5f, (mz + Mz) * 0.5f), new Vector3(t, h, Mz - mz + t * 2f));
        AddBoundBox(boundsGO, new Vector3((mx + Mx) * 0.5f, h * 0.5f, mz - t * 0.5f), new Vector3(Mx - mx + t * 2f, h, t));
        AddBoundBox(boundsGO, new Vector3((mx + Mx) * 0.5f, h * 0.5f, Mz + t * 0.5f), new Vector3(Mx - mx + t * 2f, h, t));
    }

    static void AddBoundBox(GameObject parent, Vector3 center, Vector3 size)
    {
        GameObject go = new GameObject("Bound");
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = center;
        BoxCollider col = go.AddComponent<BoxCollider>();
        col.size = size;
    }

    // ================= ديكور مجسّم (مادة ملوّنة رأسياً واحدة) — مواضع v1.3.0 =================

    void BuildScenery()
    {
        var acc = new MeshAccumulator();
        var rng = new System.Random(20260101);

        Color trunk = new Color(0.42f, 0.29f, 0.17f);
        Color leafA = new Color(0.24f, 0.50f, 0.18f);
        Color leafB = new Color(0.18f, 0.43f, 0.15f);
        Color concrete = new Color(0.80f, 0.80f, 0.78f);
        Color roofRed = new Color(0.80f, 0.22f, 0.18f);
        Color postCol = new Color(0.35f, 0.36f, 0.38f);

        // --- أشجار الجزيرة الداخلية (الجزيرة الآن x 22..126 ، z 20..68) ---
        for (int i = 0; i < 16; i++)
        {
            float tx = rng.Next(26, 118) + (float)rng.NextDouble();
            float tz = rng.Next(24, 64) + (float)rng.NextDouble();
            AddTree(acc, new Vector3(tx, 0f, tz), 0.85f + (float)rng.NextDouble() * 0.5f,
                    rng.NextDouble() < 0.5 ? leafA : leafB, trunk);
        }

        // --- أشجار خارج حدود الحلبة (أفق المشهد) ---
        for (int i = 0; i < 8; i++)
        {
            float x = 6f + rng.Next(0, 132);
            if (x > 40f && x < 92f) x += 50f; // إبعاد عن المدرّج الجنوبي
            if (x > 95f && x < 107f) x += 25f; // إبعاد عن الموقف الجنوبي الشرقي (v1.4.0)
            AddTree(acc, new Vector3(x, 0f, -4f - rng.Next(0, 7)),
                    1.0f + (float)rng.NextDouble() * 0.5f,
                    rng.NextDouble() < 0.5 ? leafA : leafB, trunk);
        }

        for (int i = 0; i < 8; i++)
        {
            float x = 6f + rng.Next(0, 132);
            if (x > 30f && x < 60f) x += 45f; // إبعاد عن المدرّج عند خط البداية
            if (x > 58f && x < 78f) x += 40f; // إبعاد عن موقف السيارات المتوقفة (v1.4.0)
            AddTree(acc, new Vector3(x, 0f, 92f + rng.Next(0, 7)),
                    1.0f + (float)rng.NextDouble() * 0.5f,
                    rng.NextDouble() < 0.5 ? leafA : leafB, trunk);
        }

        for (int i = 0; i < 4; i++)
        {
            AddTree(acc, new Vector3(-4f - rng.Next(0, 6), 0f, 6f + rng.Next(0, 76)),
                    1.0f + (float)rng.NextDouble() * 0.4f, leafB, trunk);
            AddTree(acc, new Vector3(W * tileSize + 4f + rng.Next(0, 6), 0f, 6f + rng.Next(0, 76)),
                    1.0f + (float)rng.NextDouble() * 0.4f, leafA, trunk);
        }

        // --- مدرّج جمهور عند خط البداية (يطل على المستقيم العلوي — يواجه -Z) ---
        AddGrandstand(acc, new Vector3(45f, 0f, 92.5f), 22f, concrete, roofRed, postCol, rng, facePositiveZ: false);
        // --- مدرّج جنوبي (يطل على المستقيم السفلي — يواجه +Z) ---
        AddGrandstand(acc, new Vector3(66f, 0f, -6.5f), 22f, concrete, roofRed, postCol, rng, facePositiveZ: true);

        // --- لوحات إعلانات على حواف الجزيرة (تواجه المستقيمين — v1.3.0: 1.5م عن حافة الطريق) ---
        Color[] boardCols =
        {
            new Color(0.90f, 0.30f, 0.25f),
            new Color(0.25f, 0.55f, 0.90f),
            new Color(0.95f, 0.78f, 0.20f),
            new Color(0.20f, 0.70f, 0.45f),
            new Color(0.90f, 0.45f, 0.15f)
        };
        for (int i = 0; i < 4; i++)
            AddBillboard(acc, new Vector3(62f + i * 20f, 0f, 66.5f), 0f, boardCols[i % boardCols.Length], postCol);
        for (int i = 0; i < 3; i++)
            AddBillboard(acc, new Vector3(30f + i * 20f, 0f, 21.5f), 0f, boardCols[(i + 2) % boardCols.Length], postCol);

        // --- دعائم كرتونية Low-Poly (v1.3.0، بأسلوب Environment Track LowPoly Cartoon Props):
        // مخاريط مرورية برتقالية على حافّتي الجزيرة + مكدّسات إطارات في زواياها الأربع ---
        Color coneOrange = new Color(0.95f, 0.42f, 0.10f);
        Color coneWhite  = new Color(0.92f, 0.92f, 0.90f);
        for (int i = 0; i < 8; i++)
        {
            AddTrafficCone(acc, new Vector3(28f + i * 12f, 0f, 65.5f), coneOrange, coneWhite);
            AddTrafficCone(acc, new Vector3(28f + i * 12f, 0f, 22.5f), coneOrange, coneWhite);
        }
        Color tireDark  = new Color(0.13f, 0.13f, 0.14f);
        Color tireMid   = new Color(0.20f, 0.20f, 0.22f);
        AddTireStack(acc, new Vector3(26f, 0f, 24f),  tireDark, tireMid);
        AddTireStack(acc, new Vector3(26f, 0f, 62f),  tireMid,  tireDark);
        AddTireStack(acc, new Vector3(120f, 0f, 24f), tireMid,  tireDark);
        AddTireStack(acc, new Vector3(120f, 0f, 62f), tireDark, tireMid);

        // --- بوابة الانطلاق: عمودان + لافتة مقلّمة تعلو السيارة (خط البداية x 72..76) ---
        float gx = 74f;
        acc.AddBox(new Vector3(gx, 2.6f, 66.5f), new Vector3(0.55f, 5.2f, 0.55f), 0.5f, Color.white, concrete);
        acc.AddBox(new Vector3(gx, 2.6f, 85.5f), new Vector3(0.55f, 5.2f, 0.55f), 0.5f, Color.white, concrete);
        for (int i = 0; i < 6; i++)
        {
            float z0 = 66.5f + i * (19.0f / 6f);
            Color c = (i & 1) == 0 ? roofRed : Color.white;
            acc.AddBox(new Vector3(gx, 4.65f, z0 + (19.0f / 12f)),
                       new Vector3(0.4f, 1.0f, 19.0f / 6f), 0.5f, c, c * 0.85f);
        }

        EmitMesh(acc, matPaint, "Scenery", castShadows: true, makeStatic: true);
    }

    /// <summary>شجرة: جذع أسطواني + تُيج كروي مضغوط.</summary>
    static void AddTree(MeshAccumulator acc, Vector3 pos, float s, Color leaf, Color trunk)
    {
        float trunkH = 1.15f * s;
        acc.AddCylinder(new Vector3(pos.x, trunkH * 0.5f, pos.z), 0.14f * s, trunkH, 7, 0.5f, trunk);

        float cy = trunkH + 0.72f * s;
        acc.AddSphere(new Vector3(pos.x, cy, pos.z), 0.92f * s, 8, 5, leaf, 0.85f);
        // طبقة تُيج ثانية أصغر أعلى الشجرة (عمق أكبر)
        acc.AddSphere(new Vector3(pos.x + 0.1f * s, cy + 0.55f * s, pos.z - 0.08f * s),
                      0.55f * s, 7, 4, leaf * 0.92f, 0.9f);
    }

    /// <summary>مدرّج: قاعدة درجات + جمهور ملوّن + سقف على أعمدة. الدرجات تصعد بعيداً عن الحلبة.</summary>
    static void AddGrandstand(MeshAccumulator acc, Vector3 origin, float width,
                              Color concrete, Color roof, Color post, System.Random rng, bool facePositiveZ)
    {
        // origin = مركز القاعدة على الأرض. اتجاه الصعود d = بعيداً عن جهة المواجهة.
        float d = facePositiveZ ? -1f : 1f;
        acc.AddBox(new Vector3(origin.x, 0.35f, origin.z), new Vector3(width, 0.7f, 5.6f), 0.5f, concrete, concrete * 0.85f);

        // درجات متصاعدة بعيداً عن الحلبة (نحو +Z) بجبانة ملوّنة
        Color[] crowd =
        {
            new Color(0.85f, 0.30f, 0.28f), new Color(0.30f, 0.48f, 0.85f),
            new Color(0.93f, 0.80f, 0.30f), new Color(0.35f, 0.70f, 0.45f),
            Color.white, new Color(0.60f, 0.40f, 0.75f)
        };

        for (int step = 0; step < 4; step++)
        {
            float y = 0.7f + step * 0.5f;
            float z = origin.z + d * (1.4f + step * 1.05f);
            acc.AddBox(new Vector3(origin.x, y, z), new Vector3(width, 0.5f, 1.15f), 0.5f, concrete, concrete * 0.85f);

            // جمهور: مكعبات صغيرة ملوّنة فوق كل درجة
            for (int i = 0; i < width / 1.2f; i++)
            {
                float px = origin.x - width * 0.5f + 0.8f + i * 1.2f + (float)rng.NextDouble() * 0.4f;
                Color c = crowd[rng.Next(crowd.Length)];
                acc.AddBox(new Vector3(px, y + 0.42f, z - d * 0.15f + (float)rng.NextDouble() * 0.3f),
                           new Vector3(0.34f, 0.42f, 0.3f), 0.5f, c, c * 0.8f);
            }
        }

        // أعمدة السقف
        float topY = 0.7f + 4 * 0.5f;
        acc.AddBox(new Vector3(origin.x - width * 0.42f, topY + 1.1f, origin.z + d * 2.4f), new Vector3(0.22f, 2.2f, 0.22f), 0.5f, post, post);
        acc.AddBox(new Vector3(origin.x + width * 0.42f, topY + 1.1f, origin.z + d * 2.4f), new Vector3(0.22f, 2.2f, 0.22f), 0.5f, post, post);
        // السقف
        acc.AddBox(new Vector3(origin.x, topY + 2.3f, origin.z + d * 0.4f), new Vector3(width + 0.8f, 0.18f, 5.2f), 0.5f, roof, roof * 0.8f);
    }

    /// <summary>لوحة إعلانية: لوح ملوّن على عمودين (يواجه -Z بعد الدوران 0).</summary>
    static void AddBillboard(MeshAccumulator acc, Vector3 origin, float yawDeg, Color panel, Color post)
    {
        float px = origin.x, pz = origin.z;
        acc.AddBox(new Vector3(px - 2.2f, 0.7f, pz), new Vector3(0.16f, 1.4f, 0.16f), 0.5f, post, post);
        acc.AddBox(new Vector3(px + 2.2f, 0.7f, pz), new Vector3(0.16f, 1.4f, 0.16f), 0.5f, post, post);
        acc.AddBox(new Vector3(px, 1.75f, pz), new Vector3(5.6f, 1.5f, 0.22f), 0.5f, panel, panel * 0.75f);
        // إطار أبيض رقيق أعلى وأسفل اللوح
        acc.AddBox(new Vector3(px, 2.56f, pz), new Vector3(5.6f, 0.12f, 0.24f), 0.5f, Color.white, Color.white * 0.8f);
        acc.AddBox(new Vector3(px, 0.96f, pz), new Vector3(5.6f, 0.12f, 0.24f), 0.5f, Color.white, Color.white * 0.8f);
    }

    /// <summary>
    /// مخروط مروري كرتوني (v1.3.0): قاعدة مربّعة + جسم مخروطي + شريط أبيض عاكس.
    /// ثلاث أسطوانات فقط لكل مخروط (Low-Poly صديق للموبايل) بارتفاع 60سم.
    /// </summary>
    static void AddTrafficCone(MeshAccumulator acc, Vector3 pos, Color orange, Color white)
    {
        // القاعدة العريضة
        acc.AddCylinder(new Vector3(pos.x, 0.06f, pos.z), 0.30f, 0.12f, 8, 0.5f, orange);
        // الجسم المخروطي (أسطوانة ضيقة تامة بالسطح العلوي)
        acc.AddCylinder(new Vector3(pos.x, 0.36f, pos.z), 0.15f, 0.48f, 8, 0.5f, orange);
        // الشريط الأبيض العاكس حول الجسم
        acc.AddCylinder(new Vector3(pos.x, 0.40f, pos.z), 0.175f, 0.12f, 8, 0.5f, white);
    }

    /// <summary>
    /// مكدّس إطارات (v1.3.0): ثلاث إطارات أسطوانية داكنة مكدّسة بارتفاع 78سم
    /// بأسلوب الدعائم الكرتونية لمضامير الكارتينغ.
    /// </summary>
    static void AddTireStack(MeshAccumulator acc, Vector3 pos, Color c0, Color c1)
    {
        acc.AddCylinder(new Vector3(pos.x, 0.13f, pos.z), 0.52f, 0.26f, 10, 0.5f, c0);
        acc.AddCylinder(new Vector3(pos.x, 0.39f, pos.z), 0.52f, 0.26f, 10, 0.5f, c1);
        acc.AddCylinder(new Vector3(pos.x + 0.03f, 0.65f, pos.z - 0.02f), 0.52f, 0.26f, 10, 0.5f, c0);
    }
}
