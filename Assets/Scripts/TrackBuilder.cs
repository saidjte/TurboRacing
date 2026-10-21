using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TurboRacing - v1.0.0 (لعبة ثلاثية الأبعاد حقيقية) — مطوّرة v1.7.0 (حلبة أعرض وانطلاق صحيح)
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
/// - v1.7.0 على البيضاوية: الطريق الرئيسي حصراً في المسح (كانت الطرق الداخلية
///   للجزيرة وحارة pitlane والجراجات تلوّث خط السباق فتحوّ الانطلاق نحو الجزيرة)،
///   انطلاق على مستقيم الأصل الملاصق لحارة الـ pitlane (خط بداية الأصل الحقيقي)،
///   بوابات اللفة مشتقة من أرباع خط السباق، وتكبير الحلبة ×1.5 (طريق أعرض)،
///   والديكور القريب من الطريق بلا مصادمات (كانت القباب المحدبة للجسور/اللافتات
///   تعترض الطريق فترمي السيارات).
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

    // --- v1.8.0: تأطير كاميرا القائمة ---
    /// <summary>مركز التأطير في وضع القائمة: الانطلاق على البيضاوية (السيارات
    /// ظاهرة بحجم واضح)، ومركز الخريطة على الإجرائية (حلبة صغيرة تُرى كاملة).
    /// v2.9.0: UsingOvalAsset يقرأها GameBootstrap ليقرر إضافة طبقة AK الإجرائية
    /// أو الاستغناء عنها — فوق الحلبة الأصلية صارت طبقة مزدوجة ضارة.</summary>
    public bool UsingOvalAsset => usingOvalAsset;

    public Vector3 OverviewCenter => usingOvalAsset ? StartPosition : MapCenter;
    /// <summary>العرض الأفقي (متر) الذي تغطيه كاميرا القائمة — 100م على البيضاوية
    /// يُظهر خط البداية والشبكة بحجم واضح دون أن تبدو السيارات نقاطاً متكدسة.</summary>
    public float OverviewSpan => usingOvalAsset ? 100f : Mathf.Max(60f, MapWidthUnits);

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

    // --- v1.5.0: وضع الحلبة البيضاوية (الأصل المطلوب Cartoon Race Track - Oval) ---
    bool usingOvalAsset;
    const string TrackRes = "GameAssets/CartoonOvalTrack/";
    const int TrackLayer = 10; // طبقة مصادمات الحلبة البيضاوية (أشعة العشب/المسح)

    /// <summary>(v1.7.0) تكبير موحّد لوحدات الحلبة البيضاوية — علاج «المسار ضيق جدا».
    /// كل الأنظمة (بوابات/خط سباق/انطلاق/عشب) مشتقة بالأشعة فتتكيف تلقائياً.</summary>
    const float OvalScale = 1.5f;

    /// <summary>خلايا الطريق الرئيسي حصراً (TARMAC_OVAL) — أساس خط السباق والبوابات
    /// والانطلاق. v1.7.0: استُبعدت الطرق الداخلية للجزيرة وحارة pitlane والجراجات
    /// التي كانت تلوّث الخط وتسحبه نحو الجزيرة.</summary>
    readonly HashSet<long> ovalRoadCells = new HashSet<long>();

    /// <summary>(v1.7.0) خلايا حارة الـ pitlane — تُحدّد مستقيم البداية الحقيقي
    /// (حارة الـ pitlane تلاصق مستقيم الأصل الرئيسي دائماً في هذا الأصل).</summary>
    readonly HashSet<long> ovalPitCells = new HashSet<long>();
    float ovalScanStep = 2f;
    Bounds ovalRoadBounds;

    /// <summary>(v2.0.0) وحدة أضواء الانطلاق — جسم البوابة startlights_body هو
    /// علامة خط البداية/النهاية الحقيقية للأصل. الانطلاق يُحاذى إليه بدل
    /// «وسط المستقيم» الذي كان يقع ~24م شرق البوابة الحقيقية.</summary>
    Transform ovalStartLights;

    void Awake()
    {
        Instance = this;

        // v1.5.0: الأصل المطلوب أولاً — الحلبة البيضاوية الكرتونية الكاملة
        if (TryBuildOvalAsset())
        {
            usingOvalAsset = true;
            // v2.2.0: الخط المخبوز أولاً (مولّد من هندسة APK الحقيقية) — المسح
            // الاحتياطي فقط إن فشل فك الخط، لأن المسح ظل يفشل صامتاً على الأجهزة
            if (!TryBuildBakedLine())
                BuildOvalRacingLine();
            AlignOvalStartAndGates(); // v1.7.0: اتجاه الانطلاق والبوابات من خط السباق نفسه
            VerifySpawnOnTarmac();    // v2.1.0: ضمان أن الانطلاق فوق الإسفلت فعلاً
            BuildOvalStartLine();     // v1.8.0: خط بداية مربعات مرئي عند الانطلاق
            Debug.Log($"[TrackBuilder] الحلبة البيضاوية (Cartoon Oval ×{OvalScale:0.0}) جاهزة: انطلاق من {StartPosition} بyaw {StartYaw:0}°");
            return;
        }

        BuildProceduralTrack();
    }

    /// <summary>المسار الإجرائي الكامل كما في v1.4.0 (احتياطي عند غياب الأصل).</summary>
    void BuildProceduralTrack()
    {
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
        if (!AiManager.SoloMode)
            SpawnParkedCars();   // v1.9.1: skipped in solo mode (parked cars carry BoxColliders)

        MapWidthUnits = W * tileSize;
        MapHeightUnits = H * tileSize;
        MapCenter = new Vector3(MapWidthUnits * 0.5f, 0f, MapHeightUnits * 0.5f);

        Debug.Log($"[TrackBuilder] الحلبة الإجرائية جاهزة: {W}×{H} بلاطة، انطلاق من {StartPosition}");
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
    /// زيادة عدد السيارات في المشهد: موديلات كاملة متوقفة خارج المسار بجانب
    /// مستقيم الانطلاق. v1.6.0: المواضع مشتقة من StartPosition/StartYaw بدل
    /// إحداثيات الحلبة القديمة الثابتة (كانت تقع داخل تضاريس/مباني البيضاوية)
    /// + فحص عشب (خارج الطريق) + ارتفاع الأرض بالأشعة فتجلس على السطح لا على صفر العالم.
    /// </summary>
    void SpawnParkedCars()
    {
        CarModels.CarSpec[] pool =
        {
            CarModels.Get(1), CarModels.Get(3), CarModels.Get(4),
            CarModels.Get(2), CarModels.Get(5),
        };

        Quaternion rot = Quaternion.Euler(0f, StartYaw, 0f);
        Vector3 fwd = rot * Vector3.forward;
        Vector3 right = rot * Vector3.right;

        // صف متجاور بجانب المستقيم: 5 سيارات بتقدم تدريجي على جانب العشب
        float[] along = { -8f, -2f, 4f, 10f, 16f };

        int mask = 1 << TrackLayer;
        int placed = 0;
        for (int side = 1; side >= -1 && placed < pool.Length; side -= 2)
        {
            for (int i = 0; i < pool.Length && placed < pool.Length; i++)
            {
                Vector3 spot = StartPosition
                             + fwd * (along[i] + 6f)
                             + right * side * (13f + placed * 0.4f);

                // يجب أن تكون خارج الطريق (عشب) — وإلا جرّب عمقاً أكبر
                Vector3 probe = spot;
                bool ok = false;
                for (int step = 0; step < 4 && !ok; step++)
                {
                    if (IsGrass(probe)) { ok = true; spot = probe; break; }
                    probe += right * side * 3f;
                }
                if (!ok) continue;

                // ارتفاع الأرض الفعلي تحت الموضع (سطح الحلبة/الأرضية الإجرائية)
                // + على البيضاوية نشترط أن يكون المصادم من شبكة العشب نفسها (GRASS)
                // حتى لا تنغرس السيارة داخل خيام/مدرجات/ديكور المصادمات المحدبة
                float groundY = StartPosition.y;
                bool groundOk = false;
                if (Physics.Raycast(spot + Vector3.up * 15f, Vector3.down, out RaycastHit hit, 60f, mask))
                {
                    groundY = hit.point.y;
                    groundOk = !usingOvalAsset ||
                               hit.collider.name.ToUpperInvariant().Contains("GRASS");
                }
                if (!groundOk) continue;

                CarModels.Handles h = CarModels.Build(transform, pool[placed]);
                h.Root.position = new Vector3(spot.x, groundY, spot.z);
                h.Root.rotation = Quaternion.Euler(0f, StartYaw + 90f, 0f);

                // مصادم ثابت بسيط (تتصادم معه سيارات اللاعب/المنافسين)
                BoxCollider col = h.Root.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.45f, 0f);
                col.size = new Vector3(1.15f, 0.9f, 2.6f);

                placed++;
            }
        }

        Debug.Log($"[TrackBuilder] سيارات متوقفة ديكورياً: {placed}/{pool.Length}");
    }

    // ================= استعلام السطح =================

    /// <summary>هل الموضع العالمي على العشب (خارج الطريق)؟ يُستخدم لعقاب العشب.</summary>
    public bool IsGrass(Vector3 worldPos)
    {
        // v1.5.0: وضع الحلبة البيضاوية — أشعة لأسفل على طبقة الحلبة
        if (usingOvalAsset)
        {
            int mask = 1 << TrackLayer;
            Vector3 origin = worldPos + Vector3.up * 12f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 40f, mask))
                return !MotionCoreSurface.IsAsphaltCollider(hit.collider);
            return true; // خارج العالم = عقاب
        }

        if (map == null) return false;

        int tx = Mathf.FloorToInt(worldPos.x / tileSize);
        int tz = Mathf.FloorToInt(worldPos.z / tileSize);

        if (tx < 0 || tz < 0 || tx >= W || tz >= H) return true; // خارج الخريطة = عشب
        char c = map[tx, tz];
        return c != RoadChar && c != StartChar;
    }

    // ================= مساعدات السطح والإنقاذ (v1.7.0) =================

    /// <summary>ارتفاع سطح الحلبة عند (x,z) بالأشعة على طبقة الحلبة —
    /// float.MinValue إن لم يوجد سطح (خارج العالم).</summary>
    public float SurfaceHeightAt(float x, float z)
    {
        int mask = 1 << TrackLayer;
        if (Physics.Raycast(new Vector3(x, 30f, z), Vector3.down, out RaycastHit hit, 80f, mask))
            return hit.point.y;
        return float.MinValue;
    }

    /// <summary>وضع إنقاذ لسيارة ساقطة: أقرب نقطة على خط السباق + ارتفاع سطحها
    /// الحقيقي + اتجاه المقطع التالي + إزاحة جانبية خاصة بكل سيارة (v1.8.0)
    /// حتى لا تُعاد سيارتان لنفس النقطة فتتكدسا. false إن لم يوجد خط سباق صالح.</summary>
    public bool GetRescuePose(float x, float z, float lateralOffset, out Vector3 pos, out float yaw)
    {
        pos = Vector3.zero;
        yaw = 0f;

        Vector3[] line = RacingLine;
        if (line == null || line.Length < 8) return false;

        int n = line.Length;
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            float dx = line[i].x - x;
            float dz = line[i].z - z;
            float d = dx * dx + dz * dz;
            if (d < bestD) { bestD = d; best = i; }
        }

        Vector3 at = line[best];
        float gy = SurfaceHeightAt(at.x, at.z);
        if (gy == float.MinValue) gy = 1.0f;

        Vector3 nxt = line[(best + 1) % n];
        Vector3 dir = nxt - at;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
        yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        // v1.8.0: إزاحة جانبية على يمين اتجاه المقطع + رفع إضافي — لا تكدس عند الإعادة
        Vector3 right = new Vector3(dir.z, 0f, -dir.x).normalized;
        pos = new Vector3(at.x, gy + 1.0f, at.z) + right * lateralOffset;
        return true;
    }

    /// <summary>توافق مع الاستدعاءات القديمة (بلا إزاحة جانبية).</summary>
    public bool GetRescuePose(float x, float z, out Vector3 pos, out float yaw)
    {
        return GetRescuePose(x, z, 0f, out pos, out yaw);
    }

    // ================= خط بداية مرئي (v1.8.0) =================

    /// <summary>
    /// v1.8.0 — خط بداية مربعات (أبيض/داكن) ممتد على كامل عرض الطريق عند
    /// نقطة الانطلاق عمودياً على اتجاه الانطلاق. الأصل كان بلا أي علامة مرئية
    /// (خطوط الأصل وضوؤه مدفونة تحت الأرض بإزاحات GeometricTranslation) —
    /// فكان المستخدم يرى الانطلاق «في المكان الخطأ». الشرائح تتبع ارتفاع
    /// السطح الفعلي عبر العرض فلا تطفو ولا تنغرس.
    /// </summary>
    void BuildOvalStartLine()
    {
        // v2.2.0: كان يتطلب ovalRoadCells.Count > 0 — والمسح لا ينجح على الأجهزة
        // فكان خط البداية المرئي لا يظهر أبداً (جزء من شكوى «نقطة الإنطلاق الخاطئة»)
        if (!usingOvalAsset) return;

        Quaternion rot = Quaternion.Euler(0f, StartYaw, 0f);
        Vector3 fwd = rot * Vector3.forward;
        Vector3 right = rot * Vector3.right;
        Vector3 center = StartPosition;

        Texture2D checker = CheckerTexture();
        if (checker == null) return;

        Material mat = new Material(Shader.Find("Standard"));
        mat.name = "StartLineChecker";
        mat.mainTexture = checker;
        mat.mainTextureScale = new Vector2(10f, 1f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.1f);

        const float HalfSpan = 14f;  // ±14م من المركز = يغطي عرض الطريق (22.5م)
        const float HalfThick = 1.1f; // نصف سماكة الخط على طول المسار
        const int Slices = 8;        // شرائح عرضية تتبع سطح الطريق

        GameObject root = new GameObject("OvalStartLine");
        root.transform.SetParent(transform, false);

        Vector3 prevPos = Vector3.zero;
        bool hasPrev = false;

        for (int i = 0; i <= Slices; i++)
        {
            float t = i / (float)Slices;               // -1..1 على العرض
            float lateral = Mathf.Lerp(-HalfSpan, HalfSpan, t);
            Vector3 at = center + right * lateral;

            int mask = 1 << TrackLayer;
            if (!Physics.Raycast(at + Vector3.up * 25f, Vector3.down, out RaycastHit hit, 70f, mask) ||
                !MotionCoreSurface.IsAsphaltCollider(hit.collider))
            {
                hasPrev = false; // قطع عند حافة الأسفلت (لا يمتد على العشب)
                continue;
            }

            // v2.2.0: +0.09 بدل +0.06 — يبقى الخط مرئياً حتى فوق سطح الطريق الصلب
            // المخبوز (المنخفض 5-10سم تحت شبكة الأسفلت البصرية)
            Vector3 slicePos = new Vector3(at.x, hit.point.y + 0.09f, at.z);

            if (hasPrev)
            {
                Vector3 mid = (slicePos + prevPos) * 0.5f;
                float span = Vector3.Distance(slicePos, prevPos);
                if (span > 0.01f)
                {
                    GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = "StartLineSlice";
                    quad.transform.SetParent(root.transform, false);
                    quad.transform.position = mid;
                    // مستوى أفقي: النورمال للأعلى، محور الارتفاع المحلي على اتجاه المسار
                    quad.transform.rotation = Quaternion.LookRotation(Vector3.up, fwd);
                    quad.transform.localScale = new Vector3(span, HalfThick * 2f, 1f);
                    quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
                    Destroy(quad.GetComponent<Collider>()); // مرئي فقط — بلا مصادم
                }
            }

            prevPos = slicePos;
            hasPrev = true;
        }

        Debug.Log("[TrackBuilder] v1.8.0: خط بداية مربعات مرئي عند " + center);
    }

    // ==================================================================
    // ============ v1.5.0: الحلبة البيضاوية (الأصل المطلوب) ============
    // ==================================================================

    bool TryBuildOvalAsset()
    {
        GameObject terrain = Resources.Load<GameObject>(TrackRes + "oval_mod_terrain");
        if (terrain == null) return false;

        // v2.9.0 — العلاج الجذري لـ«حلبتين إحداهما فوق الأخرى» (شكوى ميدانية بعد v2.8.0):
        // شبكة الأصل كانت تأخذ مصادمات مقعّرة تحت الديك المخبوز بـ 9سم = سطحان فيزيائيان
        // متوازيان، فتقع السيارة أحياناً في «الحلبة السفلى» وتُحشر بين صناديق الديك
        // (سمك 30م) عند الحدود. القرار: الأصل المرئي فقط بلا مصادمات إطلاقاً —
        // والفيزياء كلها على الديك المخبوز الوحيد (المجرَّب منذ v2.2، وطبخ
        // MeshCollider المقعّر كان يفشل أصلاً على الأجهزة فكان مصادماً وهمياً).
        GameObject terrainGo = InstantiateOvalModule("oval_mod_terrain", OvalColliderMode.None);
        if (terrainGo == null) return false;

        Physics.SyncTransforms();

        // v3.0.0 — العلاج الجذري لـ«السيارة تصطدم بأشياء غير مرئية في المنعطفات
        // وتنزلق بشدة»: الحلبة أصل مصرفي (يرتفع الطريق إلى ~16م على منحدر حتى
        // 34 درجة)، والديك الأفقي 2×2م كان يترجم المنحدر إلى درج غير مرئي بخطوات
        // 25..150سم تصدم السيارة عند كل خلية. الأسطح الجديدة: حزام مائل متصل C0
        // حول خط السباق (صناديق بدوران مطابقة للميلان) + العالم الخارجي مستطيلات.
        // false فقط عند فك تالف → المسار القديم الكامل احتياطاً.
        if (!TryBuildSmoothBandSurfaces())
        {
            // v2.2.0: الخط المخبوز أولاً (مولّد من هندسة APK الحقيقية) — المسح
            // الاحتياطي فقط إن فشل فك الخط، لأن المسح ظل يفشل صامتاً على الأجهزة
            BuildBakedGroundNet();
            BuildRoadDeck();
        }

        // 2) مسح الطريق (تشخيصي + خلايا استبعاد الديكور) — بعد وجود الديك
        // v2.2.0: الخط والانطلاق والبوابات من البيانات المخبوزة لا من نتيجة المسح
        if (!ScanOvalRoad(terrainGo))
        {
            if (LineSourceTag == "-") LineSourceTag = "SCAN-FAIL";
            Renderer tarmac = FindRendererBySubstring(terrainGo.transform, "TARMAC");
            ovalRoadBounds = tarmac != null ? tarmac.bounds : new Bounds(MapCenter, new Vector3(228f, 1f, 110f));
            StartPosition = new Vector3(ovalRoadBounds.center.x, 1.0f, ovalRoadBounds.min.z + 5f);
            StartYaw = 90f; // احتياطي: شرقاً على أدنى مستقيم — يُضبط نهائياً من الخط إن وجد
        }
        else if (LineSourceTag == "-")
        {
            LineSourceTag = "SCAN:" + ovalRoadCells.Count;
        }

        // v1.9.0 — أرضية أمان عالمية (آخر خط دفاع): توصف أي سقوط خارج حدود
        // العالم أو من ثغرة غير متوقعة على عمق -25م، وفحص الإنقاذ في
        // CarController (أدنى من -12م) يعيد السيارة لخط السباق فوراً.
        GameObject floor = new GameObject("WorldSafetyFloor");
        floor.transform.SetParent(transform, false);
        BoxCollider floorCol = floor.AddComponent<BoxCollider>();
        floorCol.center = new Vector3(0f, -25.5f, 125f);
        floorCol.size = new Vector3(1900f, 1f, 1200f);
        floorCol.isTrigger = false;
        floor.layer = TrackLayer;

        // 3) خطوط الطلاء فوق الإسفلت (بلا مصادم) + أضواء الانطلاق (بلا مصادم)
        InstantiateOvalModule("oval_mod_lines", OvalColliderMode.None);
        GameObject startLightsGo = InstantiateOvalModule("oval_mod_startlights", OvalColliderMode.None); // v2.0.0: مرجع بوابة الانطلاق الحقيقية
        ovalStartLights = startLightsGo != null ? startLightsGo.transform : null;

        // 4) الديكور — v1.8.0: بلا مصادمات إطلاقاً + إسقاط ما يقطع الطريق.
        //    الجذر الحقيقي لـ «تطير/تغوص/تتكدس» منذ v1.5.0: مدرّجان في وحدة المقاعد
        //    يقفان فوق أسفلت السباق (grandstands_small_014 على المستقيم الشرقي،
        //    grandstands_large_011 على المنحنى الشرقي) بمصادمات CONVEX = جدران
        //    شفافة تتفتت الحقل كل لفة وتدفع السيارات تحت شبكة الطريق. حتى الأصلي
        //    البعيد صار بلا مصادم: لا جدران غير مرئية أبداً (طلب المستخدم الدائم).
        InstantiateOvalModuleCullRoad("oval_mod_buildings");
        InstantiateOvalModuleCullRoad("oval_mod_seats");
        InstantiateOvalModuleCullRoad("oval_mod_tents");
        InstantiateOvalModule("oval_mod_pitwall",     OvalColliderMode.None);
        InstantiateOvalModule("oval_mod_lamps",       OvalColliderMode.None);
        InstantiateOvalModule("oval_mod_tyres",       OvalColliderMode.None);
        InstantiateOvalModule("oval_mod_banners",     OvalColliderMode.None);
        InstantiateOvalModule("oval_mod_turnsigns",   OvalColliderMode.None);
        InstantiateOvalModule("oval_mod_bridges",     OvalColliderMode.None);
        InstantiateOvalModule("oval_mod_blocks",      OvalColliderMode.None);

        // بلا حواجز بطلب المستخدم: barriers/walls/trees لا تُستنسخ إطلاقاً

        // 5) أبعاد الكاميرا من حدود الأرضية الكاملة
        Bounds tb = RenderersBounds(terrainGo.transform);
        MapWidthUnits = Mathf.Max(60f, tb.size.x);
        MapHeightUnits = Mathf.Max(60f, tb.size.z);
        MapCenter = new Vector3(tb.center.x, 0f, tb.center.z);

        return true;
    }

    /// <summary>
    /// v1.8.0 — استنساخ وحدة ديكور بلا مصادمات مع إسقاط أي مجسّم فرعي
    /// تداخل بصمته الأفقية مع خلايا الطريق الممسوحة (ovalRoadCells).
    /// يحل جذرياً «مدرّج على الأسفلت» دون مسّ بقيّة الديكور.
    /// </summary>
    void InstantiateOvalModuleCullRoad(string name)
    {
        GameObject go = InstantiateOvalModule(name, OvalColliderMode.None);
        if (go == null || ovalRoadCells.Count == 0) return;

        int culled = 0;
        foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
        {
            Renderer r = mf.GetComponent<Renderer>();
            if (r == null) continue;

            Bounds b = r.bounds; // حدود عالمية (بعد التكبير ×1.5)
            if (FootprintHitsRoad(b)) { Destroy(mf.gameObject); culled++; }
        }

        if (culled > 0)
            Debug.Log($"[TrackBuilder] v1.8.0: أُسقط {culled} مجسّم ديكور يقطع الطريق من '{name}'");
    }

    /// <summary>هل البصمة الأفقية لصندوق المجسّم تلمس خلايا الطريق (بهامش 3م)؟</summary>
    bool FootprintHitsRoad(Bounds b)
    {
        const float Margin = 3f;
        float minX = b.min.x - Margin, maxX = b.max.x + Margin;
        float minZ = b.min.z - Margin, maxZ = b.max.z + Margin;

        // عيّنة كل ~2م (مطابقة لخطوة المسح) — مجسّم 50م = ~676 عينة رخيصة
        for (float x = minX; x <= maxX; x += ovalScanStep)
        {
            for (float z = minZ; z <= maxZ; z += ovalScanStep)
            {
                long key = OvalCellKey(
                    Mathf.FloorToInt(x / ovalScanStep),
                    Mathf.FloorToInt(z / ovalScanStep));
                if (ovalRoadCells.Contains(key)) return true;
            }
        }
        return false;
    }

    // ================= شبكة الأرض المخبوزة (v2.1.0) =================

    /// <summary>(v2.1.0) شبكة الأرض المخبوزة — مولدة من هندسة الـ APK الحقيقية (2م/خلية).
    /// سجلات int16: (cx, cz, w2, h2, y10) — أصل الشبكة (-620,-620)، w2/h2 بوحدات 2م،
    /// y10 = ارتفاع السطح ×10. كل مستطيل = عشب صلب (BoxCollider بتنورة سفلية)
    /// يغلق فجوات تضاريس الأصل داخل نطاق الحلبة — بلا أي أشعة وقت التشغيل.</summary>
    const string GroundNetB64 = @"DwAYADQAAQAAAEMAGAABAAEAsP9EABgAAQABACgARQAYAAUAAQB4AEoAGAABAAEAfv9LABgAAQABAOz/TAAYAAEAAQBkAE0AGAA6AAEAeAC/ABgADAABABQAywAYAAEAAgAKAMwAGAABAAEAAADNABgAAQABAAoAzgAYABkAAQAAACQBGAACAAEAFAAmARgAAQABAAoAJwEYAAMAAQAUACoBGAABAAEACgArARgADAABABQANwEYAAEAAgAeADgBGAACAAEAKAA6ARgAAQABAB4AOwEYAAMAAQAoAGgBGAAPAAEAPAB3ARgABAABADIAewEYAAMACAAoAH4BGAAEAAEAMgCCARgAAwABADwAhQEYAAEAAQAoAIYBGAACAAQAPACIARgABgABACgAjgEYAAIAAQAyAJABGAAHAAEAKACXARgABAABADwAmwEYAAUAAQAoAKABGAADAAEARgCjARgAAQABAFAApAEYAAMAAQA8AKcBGAABAAEA2P+oARgAAwABADIAqwEYAAQAAQBQAK8BGAADAAEAWgCyARgAAwACAGQAtQEYAAIAAQBGALcBGAABAAEAUAC4ARgAAQABADwAuQEYAAEAAQAeALoBGAABAAEAAAC7ARgAAgABAGQAvQEYAAIAAQBuAL8BGAAOAAEAeADNARgAAQABAIj/zgEYAHMAAQAAAA8AGQAyAAEAAABBABkAAQABAHT/QgAZAAEAAQDs/0MAGQABAAEAWgBEABkABQABAHgASQAZAAEAAQCw/0oAGQABAAEAKABLABkAOgABAHgAwgAZAAYAAQAUAMgAGQADAAEACgDMABkAAQABAAoAzQAZABcAAQAAACgBGQADAAEAFAArARkAAQABAAoALAEZAAoAAQAUADYBGQABAAEAHgA4ARkAAQABAB4AbQEZAAkAAQA8AHYBGQAEAAEAMgB6ARkAAQAGACgAfgEZAAEACQAoAH8BGQAEAAEAMgCDARkAAwABADwAiAEZAAMAAgA8AIsBGQAGAAEAKACRARkAAgABADIAkwEZAAIAAQA8AJUBGQABAAMAKACWARkAAQABAAoAlwEZAAQAAQAoAJsBGQAEAAEAPACfARkAAgACACgAoQEZAAMAAQAyAKQBGQAFAAEAUACpARkABAABADwArQEZAAEAAQAoAK4BGQABAAEAAACvARkAAQABAFAAsAEZAAIAAQBaALUBGQAEAAIAZAC5ARkABAABAFAAvQEZAAEAAQAoAL4BGQACAAEAbgDAARkAEAABAHgA0AEZAAEAAQCm/9EBGQABAAEAfv/SARkAbwABAAAADwAaADEAAQAAAEAAGgABAAEApv9BABoAAQABACgAQgAaAAUAAQB4AEcAGgABAAEAdP9IABoAAQABAOz/SQAaAAEAAQBaAEoAGgANAAEAeABXABoAAQABADwAWAAaACsAAQB4AMUAGgAFAAEACgDKABoAGAABAAAAcAEaAAUAAQA8AHUBGgAEAAEAMgB5ARoAAQAFACgAfwEaAAEACAAoAIABGgACAAEAMgCCARoAAQABACgAgwEaAAEABAAyAIQBGgACAAEAPACLARoABAACADwAjwEaAAEAAQAyAJABGgAFAAEAKACWARoABAABADwAmgEaAAMAAQAoAJ0BGgABAAEAAACeARoAAQABAIj/oQEaAAEAAQAoAKIBGgABAAEAPACjARoAAwABADIApgEaAAEAAQA8AKcBGgAGAAEAUACtARoAAwABADwAsAEaAAEAAQBGALEBGgABAAEAMgCyARoAAQABABQAswEaAAIAAQBkALkBGgACAAIAZAC7ARoAAQABAG4AvAEaAAEAAQBaAL0BGgACAAEAUAC/ARoAAgABAG4AwQEaABEAGgB4ANIBGgABAAEA7P/TARoAAQABAMT/1AEaAAEAAQCc/9UBGgBsAAEAAAAPABsAMAABAAAAPwAbAAEAAQCw/0AAGwABAAEAPABBABsABQABAHgARgAbAAEAAQCw/0cAGwABAAEAKABIABsABQABAHgATQAbAAEAAQAyAE4AGwAHAAEAeABVABsAAQABAMT/VgAbAAEAAQBGAFcAGwApAAEAeADKABsAFQABAAAAcwEbAAEAAQA8AHQBGwAEAAEAMgB4ARsAAQADACgAgAEbAAEACAAoAIEBGwACAAEAMgCEARsAAQADADIAhQEbAAEAAQA8AIgBGwABAAEAKACJARsAAgACADwAjwEbAAQAAQA8AJMBGwABAAQAMgCUARsAAQABACgAlgEbAAQAAQAoAJoBGwAEAAEAPACeARsAAwABADIAoQEbAAEAAQAUAKIBGwABAAEA7P+jARsAAQACADwApAEbAAEAAQDY/6UBGwADAAEAMgCoARsABgABAFAArgEbAAMAAQBaALEBGwADAAEARgC0ARsAAQABAFAAtQEbAAEAAQA8ALYBGwABAAEAHgC3ARsAAgABAGQAuwEbAAEAAgBkALwBGwADAAEAbgC/ARsAAQACAHgAwAEbAAEAAQBuANIBGwADACAAeADVARsAAQABABQA1gEbAGsAAQAAAA8AHAAvAAEAAAA+ABwAAQABAMT/PwAcAAEAAQBGAEAAHAAEAAEAeABEABwAAQABAHT/RQAcAAEAAQDs/0YAHAABAAEAWgBHABwABQABAHgATAAcAAEAAQA8AE0AHAAHAAEAeABUABwAAQABAOz/VQAcAAEAAQBkAFYAHAAoAAEAeADPABwADQABAAAAdQEcAAIAAQAyAHcBHAABAAIAKACBARwAAQAIACgAggEcAAEAAQAyAIUBHAABAAEAMgCGARwAAQABADwAhwEcAAIAAgAyAIsBHAACAAIAMgCNARwAAQABADwAjgEcAAIAAQAyAJABHAADAAEAKACUARwAAwABADIAlwEcAAEAAQA8AJgBHAAGAAEAKACeARwAAgABADwAoAEcAAEAAQBGAKEBHAACAAEAPACkARwAAQABADwApQEcAAEAAQAeAKYBHAABAAEAAACnARwAAwABADwAqgEcAAEAAQAoAKsBHAAEAAEAUACvARwAAwABAFoAsgEcAAMAAgBkALUBHAAFAAEAUAC6ARwAAQABACgAvAEcAAEAAQBkAL0BHAACAAEAbgDAARwAAQACAHgA1QEcAAIAIAB4ANcBHABqAAEAAAAPAB0ALgABAAAAPQAdAAEAAQDO/z4AHQABAAEAUAA/AB0ABAABAHgAQwAdAAEAAQCm/0QAHQABAAEAKABFAB0ABQABAHgASgAdAAEAAQB0/0sAHQABAAEAUABMAB0ABwABAHgAUwAdAAEAAQAoAFQAHQAoAAEAeACCAR0AAQAHACgAhQEdAAIACQAoAIkBHQABAAEAMgCKAR0AAQAKACgAjQEdAAEAAQAyAI4BHQABAAMAKACPAR0ABAABADIAlAEdAAIAAQAyAJYBHQADAAEAKACZAR0ABAABADwAnQEdAAMAAQAoAKABHQACAAEAMgCiAR0AAgABAEYApAEdAAEAAQBQAKUBHQAEAAEAPACpAR0AAQABACgAqgEdAAMAAQA8AK0BHQABAAEARgCuAR0AAQABADIArwEdAAEAAQBQALABHQACAAEAWgC1AR0ABAACAGQAuQEdAAUAAQBQAL4BHQACAAEAbgDXAR0AAwAgAHgA2gEdAGcAAQAAAA8AHgAtAAEAAAA8AB4AAQABANj/PQAeAAEAAQBkAD4AHgAEAAEAeABCAB4AAQABALD/QwAeAAEAAQA8AEQAHgAFAAEAeABJAB4AAQABALD/SgAeAAEAAQAoAEsAHgAGAAEAeABRAB4AAQABAOz/UgAeAAEAAQBkAFMAHgAOAAEAeABhAB4AAQABABQAYgAeABgAAQB4AIMBHgACAAcAKACHAR4AAwAJACgAiwEeAAMACgAoAI8BHgABAAIAKACQAR4AAwABADIAlAEeAAMAAQA8AJcBHgADAAEAKACaAR4AAgABADIAnAEeAAEAAQAoAJ0BHgABAAIAMgCeAR4AAwABADwAoQEeAAEAAQDY/6IBHgADAAEAMgClAR4AAQABADwApgEeAAMAAQBQAKkBHgACAAEAPACrAR4AAgACAFAArQEeAAEAAQA8AK4BHgADAAEARgCxAR4AAQABAFAAsgEeAAEAAQA8ALMBHgACAAEAZAC5AR4AAgACAGQAuwEeAAEAAQBuALwBHgABAAEAWgC9AR4AAgABAFAAvwEeAAIAAQBuANoBHgACACAAeADcAR4AZQABAAAADwAfACsAAQAAADoAHwABAAEAav87AB8AAQABAOz/PAAfAAEAAQBuAD0AHwAEAAEAeABBAB8AAQABAMT/QgAfAAEAAQBGAEMAHwAEAAEAeABHAB8AAQABAHT/SAAfAAEAAQDs/0kAHwABAAEAWgBKAB8ABgABAHgAUAAfAAEAAQAoAFEAHwAFAAEAeABWAB8AAQABAEYAVwAfACEAAQB4AJABHwAHAAEAKACXAR8ABAABADwAmwEfAAIAAQAyAJ4BHwABAAEAMgCfAR8AAQACADwAoAEfAAEAAQAyAKEBHwAEAAIAPAClAR8AAQABABQApgEfAAEAAQDs/6cBHwABAAEAKACoAR8AAQABAAAAqQEfAAEAAQDi/6oBHwABAAEAUACtAR8AAQACAFAArgEfAAMAAQBaALEBHwABAAEARgCyAR8ABQABAFAAtwEfAAIAAQBkALsBHwABAAIAZAC8AR8AAwABAG4AvwEfAAEAAgB4AMABHwABAAEAbgDcAR8AAwAgAHgA3wEfAGIAAQAAAA8AIAAqAAEAAAA5ACAAAQABAHT/OgAgAAEAAQAAADsAIAAFAAEAeABAACAAAQABAM7/QQAgAAEAAQBQAEIAIAAEAAEAeABGACAAAQABAKb/RwAgAAEAAQAoAEgAIAAHAAUAeABPACAAAQABAFoAUAAgAAUAAQB4AFUAIAABAAEAUABWACAACAACAHgAXgAgAAEAAQBkAF8AIAAXAAEAeAB9ASAAAQABACgAjgEgAAIAAQAyAJABIAADAAEAKACTASAABQABADIAmAEgAAEAAQDO/5kBIAADAAEAKACcASAAAwABADwAoAEgAAEAAgA8AKUBIAAEAAIAPACpASAAAQABABQAqgEgAAEAAQDs/6sBIAABAAEAMgCsASAAAQABABQArgEgAAEAAgBQAK8BIAADAAEAWgCyASAAAwACAGQAtQEgAAYAAQBQALwBIAABAAEAZAC9ASAAAgABAG4AwAEgAAEAAgB4AN8BIAADACAAeADiASAAXwABAAAADwAhACkAAQAAADgAIQABAAEAiP85ACEAAQABAAoAOgAhAAUAAQB4AD8AIQABAAEA2P9AACEAAQABAGQAQQAhAAQAAQB4AEUAIQABAAEAsP9GACEAAQABADwARwAhAAEAEAB4AE8AIQAEAAEAeABTACEAAQABANj/VAAhAAEAAQBkAFUAIQABAAUAeABeACEAFgABAHgAjgEhAAEAAwAoAI8BIQAEAAEAMgCTASEAAwABADwAlgEhAAEAAgAoAJcBIQABAAEAMgCYASEAAgABADwAmgEhAAEAAQAyAJsBIQADAAIAPACeASEAAQABANj/nwEhAAEAAQAyAKEBIQACAAEARgCjASEAAgABADwAqQEhAAIAAQA8AKsBIQABAAEARgCsASEAAQABADwArQEhAAEAAQAUAK8BIQABAAMAUACwASEAAgABAFoAtQEhAAQAAgBkALkBIQADAAEAUAC8ASEAAgACAFoAvgEhAAIAAQBuAOIBIQACACEAeADkASEAXQABAAAADwAiACgAAgAAADcAIgABAAEAkv84ACIAAQABABQAOQAiAAQAAQB4AD0AIgABAAEAav8+ACIAAQABAOz/PwAiAAEAAQBuAEAAIgAEAAEAeABEACIAAQABAMT/RQAiAAEAAQBGAEYAIgABAAoAeABPACIAAwABAHgAUgAiAAEAAQDs/1MAIgABAAEAbgBUACIAAQAHAHgAVgAiAAUAAgB4AFsAIgABAAEAZABcACIAFgABAHgAjwEiAAEAAgAoAJABIgAGAAEAMgCXASIAAQABACgAmAEiAAIAAQAyAJoBIgABAAMAPACeASIAAQABACgAnwEiAAEAAQAeAKABIgABAAEA7P+hASIAAQABAMT/ogEiAAIAAQA8AKQBIgADAAEAUACnASIAAQABADwAqAEiAAEAAQAUAKkBIgABAAEA7P+qASIAAgABADwArAEiAAMAAQBGALABIgADAAEAUACzASIAAgABAGQAuQEiAAIAAgBkALsBIgABAAEAbgC+ASIAAQABAFoAvwEiAAIAAQBuAOQBIgADACEAeADnASIAWgABAAAANwAjAAEAAQAoADgAIwAEAAEAeAA8ACMAAQABAHT/PQAjAAEAAQAAAD4AIwAFAAEAeABDACMAAQABAM7/RAAjAAEAAQBQAEUAIwABAAUAeABPACMAAQABAHgAUAAjAAEAAQB0/1EAIwABAAEAAABSACMAAgADAHgAWwAjABUAAQB4AJABIwACAAIAKACSASMAAwABADIAlQEjAAMAAQA8AJgBIwABAAIAMgCZASMAAQABADwAmwEjAAEABwA8AJwBIwABAAEAKACdASMAAgAHADwAnwEjAAEAAQBGAKABIwADAAEAPACjASMAAQABABQApAEjAAEAAQD2/6UBIwABAAEA2P+mASMAAQABALD/pwEjAAEAAQBGAKgBIwADAAEAUACrASMAAQABADwArAEjAAEAAQAUAK0BIwABAAEA7P+uASMAAQABAMT/sAEjAAEAAQBaALEBIwAGAAEAUAC3ASMAAgABAGQAuwEjAAEAAgBkALwBIwADAAEAbgC/ASMAAQACAHgAwAEjAAEAAQBuAOcBIwACACEAeADpASMAWAABAAAADwAkACcAAQAAADYAJAABAAEAMgA3ACQABAABAHgAOwAkAAEAAQCI/zwAJAABAAEACgA9ACQABQABAHgAQgAkAAEAAQDY/0MAJAABAAEAZABEACQAAQAKAHgATwAkAAEAAQCw/1AAJAABAAEAKABRACQAAQACAHgAVgAkAAIAAwB4AFgAJAABAAEAZABZACQAFgABAHgAjgEkAAIAAQAyAJIBJAABAAEAKACTASQABQABADIAmQEkAAEAAQAyAJwBJAABAAIAPACfASQAAwACADwAogEkAAEAAQAoAKMBJAABAAEAUACkASQAAQADADwApQEkAAEAAQAeAKYBJAABAAEAAACnASQAAQABANj/qAEkAAEAAQD2/6kBJAABAAEAzv+qASQAAQABALD/qwEkAAQAAQBQAK8BJAABAAEAPACwASQAAQABABQAsQEkAAEAAgBaALIBJAADAAIAZAC1ASQABAABAFAAuQEkAAIAAQBaALwBJAABAAEAZAC9ASQAAgABAG4AwAEkAAEAAgB4AOkBJAADACEAeADsASQAVQABAAAADwAlACYAAQAAADUAJQABAAEAPAA2ACUABAACAHgAOgAlAAEAAQCS/zsAJQABAAEAFAA8ACUABAABAHgAQAAlAAEAAQBq/0EAJQABAAEA7P9CACUAAQABAG4AQwAlAAEABAB4AEgAJQAGAAEAeABOACUAAQABAOz/TwAlAAEAAQBaAFAAJQABAAMAeABYACUACwABAHgAYwAlAAEAAQBkAGQAJQAJAAEAeACOASUAAQADACgAjwElAAQAAQAyAJMBJQADAAEAPACWASUAAQABACgAlwElAAEAAQAyAJgBJQACAAUAPACaASUAAQABADIAogElAAIAAgA8AKUBJQABAAEARgCmASUAAQABADIApwElAAIAAQBQAKkBJQABAAEAKACqASUAAQABAAoAqwElAAEAAQDs/6wBJQACAAEAxP+uASUAAQABAKb/rwElAAEAAQBQALABJQABAAEAWgC1ASUABAACAGQAuQElAAEAAQBQALoBJQADAAEAWgC9ASUAAQACAGQAvgElAAIAAQBuAOwBJQACACIAeADuASUAUwABAAAADwAmACUAAgAAADQAJgABAAEAUAA1ACYAAQAzAHgAOgAmAAEAAQAoADsAJgAEAAEAeAA/ACYAAQABAHT/QAAmAAEAAQAAAEEAJgACAAQAeABIACYABQABAHgATQAmAAEAAQAoAE4AJgACAAMAeABRACYAAQABABQAUgAmAAEAAgB4AFMAJgABAAEAbgBVACYAAQABAFoAWAAmAAEAAwB4AFkAJgABAAEAFABaACYAAwABAHgAXQAmAAEAAQBaAF4AJgADAAEAeABhACYAAQABABQAYgAmAAcAAQB4AI8BJgABAAIAKACQASYABAABADIAlAEmAAQAAQA8AJoBJgABAAQAPACcASYAAQABADIAnwEmAAMAAQAoAKUBJgABAAEAPACmASYAAwABAEYAqQEmAAEAAQBQAKoBJgABAAEAPACrASYAAgADAFAArQEmAAEAAQA8AK4BJgABAAEAFACvASYAAQABAPb/sAEmAAEAAQDY/7EBJgABAAEAxP+yASYAAQABAJz/swEmAAIAAQBkALkBJgACAAIAZAC7ASYAAQABAG4AvAEmAAEAAQBaAL4BJgABAAEAZAC/ASYAAgABAG4A7gEmAAIAIgB4APABJgBRAAEAAAA0ACcAAQA0AHgANgAnAAMAAQB4ADkAJwABAAEAMgA6ACcABAABAHgAPgAnAAEAAQCI/z8AJwABAAEACgBAACcAAQAEAHgASAAnAAMAAQB4AEsAJwABAAEA9v9MACcAAgAEAHgAUQAnAAEAAQB4AFMAJwABAAEAKABVACcAAgABAHgAVwAnAAEAAQD2/1kAJwACAAEAeABbACcAAQABACgAXAAnAAMAAQB4AF8AJwABAAEA9v9gACcAAQABAGQAYQAnAAYAAQB4AJABJwACAAEAKACSAScAAwACADIAlQEnAAMAAQA8AJwBJwABAAMAPACfAScAAQABAEYAoAEnAAEAAQAyAKEBJwADAAEAKACkAScAAQABAAoApQEnAAIAAQAoAKcBJwADAAEARgCqAScAAQABAFAArQEnAAIAAgBQAK8BJwACAAEAWgCxAScAAQABADwAsgEnAAEAAQAUALMBJwABAAEA9v+0AScAAQABAM7/tQEnAAEAAQBQALYBJwABAAEAWgC3AScAAgABAGQAuwEnAAEAAgBkALwBJwADAAEAbgC/AScAAQACAHgAwAEnAAEAAQBuAPABJwACACIAeADyAScATwABAAAADwAoACQAAQAAADMAKAABADQAeAA2ACgAAgABAHgAOAAoAAEAAQA8ADkAKAAEAAIAeAA9ACgAAQABAJL/PgAoAAEAAQAUAD8AKAABACQAeABFACgAAQABAG4ASAAoAAIAAgB4AEoAKAABAAEAZABLACgAAQABAHgAUAAoAAEAAQAKAFEAKAABAAEA7P9SACgAAQABAGQAUwAoAAEAAQB4AFUAKAABAAEA2P9WACgAAQABAGQAVwAoAAEAAQB4AFkAKAABAAEA7P9aACgAAQABAGQAWwAoAAIAAQB4AF0AKAABAAEA2P9eACgAAQABACgAXwAoAAYAAQB4AJEBKAABAAEAKACVASgAAgABADIAlwEoAAEAAgA8AJ8BKAADAAIAPACiASgAAQABAEYAowEoAAEAAQAyAKQBKAABAAEAPAClASgAAQABACgApgEoAAIAAQAyAKgBKAABAAEAuv+pASgAAQABAPb/qgEoAAEAAQAyAK8BKAAEAAIAUACzASgAAgABAGQAtQEoAAEAAQA8ALYBKAABAAEAFAC3ASgAAQABAOz/uAEoAAEAAQDE/7kBKAABAAEAWgC6ASgAAQABAGQAvAEoAAEAAQBkAL0BKAACAAEAbgDAASgAAQACAHgA8gEoAAMAIgB4APUBKABMAAEAAAAPACkAIwACAAAAMgApAAEANAB4ADYAKQABAC8AeAA3ACkAAQABAFAAOAApAAEALAB4AD0AKQABAAEAKAA+ACkAAQAkAHgAQwApAAEAAQAAAEUAKQABAAQAeABKACkAAQAJAHgASwApAAEAAQBQAE4AKQABAAEAeABPACkAAQABALr/UAApAAEAAQAoAFEAKQACAAEAeABTACkAAQABANj/VAApAAEAAQAoAFUAKQACAAEAeABXACkAAQABALr/WAApAAEAAQAoAFkAKQACAAEAeABbACkAAQABALr/XAApAAEAAQAoAF0AKQABAAEAZABeACkABQABAHgAogEpAAEAAQA8AKMBKQADAAEARgCmASkAAQABAFAApwEpAAEAAQA8AKgBKQABAAMAUACpASkAAQABADwAqgEpAAEAAQAKAKsBKQABAAEAPACsASkAAQABABQArQEpAAEAAQD2/64BKQABAAEA2P+zASkAAQACAFAAtAEpAAIAAQBaALYBKQADAAEAZAC5ASkAAQABADwAugEpAAEAAQAUALsBKQABAAEA7P+8ASkAAQABAMT/vQEpAAEAAQBkAL4BKQACAAEAbgD1ASkAAgAjAHgA9wEpAEoAAQAAADcAKgABACwAeAA5ACoAAwABAHgAPAAqAAEAAQAyAD0AKgABACQAeABBACoAAQABAIj/QgAqAAEAAQAKAEMAKgABAAQAeABIACoAAQABAHgASQAqAAEAAQBkAEsAKgABAAMAeABOACoAAQABAOz/TwAqAAEAAQBkAFAAKgABAAEAeABRACoAAQABAH7/UgAqAAEAAQDs/1MAKgABAAEAZABUACoAAgABAHgAVgAqAAEAAQDs/1cAKgABAAEAZABYACoAAgABAHgAWgAqAAEAAQDs/1sAKgABAAEAKABcACoAAQAEAHgAXQAqAAEAAQBaAF4AKgADAAEAeACiASoAAgABAEYApAEqAAIAAQBQAKYBKgABAAEARgCnASoAAQACAFAAqQEqAAQAAgBQAK0BKgABAAEAPACuASoAAQABABQArwEqAAEAAQD2/7ABKgABAAEA2P+xASoAAQABALD/sgEqAAEAAQDi/7QBKgABAAEAUAC1ASoAAgABAFoAtwEqAAQAAQBkALsBKgABAAEAbgC8ASoAAQABAFoAvQEqAAEAAQA8AL4BKgABAAEAFAC/ASoAAgABAG4A9wEqAAIAIwB4APkBKgBIAAEAAAAPACsAIgABAAAAMQArAAEANAB4ADkAKwACAAEAeAA7ACsAAQABADwAPAArAAEAJAB4AEAAKwABAAEAsP9BACsAAQABACgAQgArAAEABQB4AEgAKwABAAEAbgBJACsAAQADAHgATAArAAEAAQB4AE0AKwABAAEAKABOACsAAgABAHgAUAArAAEAAQCw/1EAKwABAAEAAABSACsAAwABAHgAVQArAAEAAQAoAFYAKwACAAEAeABYACsAAQABALD/WQArAAEAAQAoAFoAKwABAAIAWgBbACsAAQADAHgAXQArAAMAAQB4AK0BKwADAAIAUACwASsAAQABAFoAsQErAAEAAQA8ALIBKwABAAEAFACzASsAAQABAPb/tAErAAEAAQDO/7UBKwABAAEAsP+2ASsAAQABAMT/twErAAEAAQBaALgBKwAEAAEAZAC8ASsAAwABAG4AvwErAAEAAgB4AMABKwABAAEAbgD5ASsAAgAkAHgA+wErAEYAAQAAAA8ALAAhAAEAAAAwACwAAQA1AHgAOQAsAAEAJwB4ADoALAABAAEAUAA7ACwAAQAlAHgAQAAsAAEAAQBkAEEALAABAB4AeABGACwAAQABAAAASAAsAAEABAB4AEwALAABAAEAPABNACwAAgABAHgATwAsAAEAAQDs/1AALAABAAEACgBRACwAAwACAHgAVAAsAAEAAQBkAFUALAACAAEAeABXACwAAQABAOz/WAAsAAEAAQAoAFkALAABAAQAeABdACwAAQABAHgAqwEsAAIAAQBQALABLAABAAEAUACxASwAAwACAFoAtAEsAAEAAQBkALUBLAABAAEAPAC2ASwAAQABABQAtwEsAAEAAQDs/7gBLAABAAEAxP+5ASwAAQABAKb/ugEsAAMAAQBkAL0BLAACAAEAbgDAASwAAQACAHgA+wEsAAIAJQB4AP0BLABEAAEAAAAPAC0AIAABAAAALwAtAAEANQB4ADoALQABACUAeABAAC0AAQAeAHgARQAtAAEAAQAKAEYALQABAAUAeABLAC0AAQABAFAATAAtAAIAAgB4AE4ALQABAAIAKABPAC0AAQABABQAUAAtAAEAAwB4AFQALQABAAEAeABVAC0AAQABALD/VgAtAAEAAQAoAFcALQABAAEAWgBYAC0AAQADAHgAWgAtAAEAAgB4AK8BLQABAAEAUACwAS0AAQABAFoAtAEtAAEAAQBaALUBLQAEAAIAZAC5AS0AAQABADwAugEtAAEAAQAUALsBLQABAAEA7P+8AS0AAQABAMT/vQEtAAEAAQCc/74BLQACAAEAbgD9AS0AAgAlAHgA/wEtAEIAAQAAAA8ALgAfAAEAAAAuAC4AAQA2AHgAQwAuAAEAAQCS/0QALgABAAEAFABFAC4AAQAFAHgASQAuAAEAAQBaAEsALgABAAEAbgBPAC4AAQABAFAAUQAuAAEAAQBaAFIALgABAAIAeABTAC4AAQABAG4AVAAuAAEAAQDs/1UALgABAAEAWgBWAC4AAQABAHgAVwAuAAEAAQA8ALMBLgACAAEAZAC5AS4AAgACAGQAuwEuAAEAAQBuALwBLgABAAEAWgC9AS4AAQABADwAvgEuAAEAAQAUAL8BLgACAAEAbgD/AS4AAgAmAHgAAQIuAEAAAQAAAA8ALwAeAAEAAAAtAC8AAQA3AHgAQwAvAAEAAQAoAEQALwABAAUAeABJAC8AAQAEAHgASwAvAAIAAQB4AE0ALwABAAEAMgBOAC8AAQABAGQATwAvAAEAAgB4AFEALwABAAMAeABTAC8AAQABACgAVAAvAAIAAQB4AFYALwABAAEARgBXAC8AAQACAHgAtwEvAAIAAQBkALsBLwABAAIAZAC8AS8AAwABAG4AvwEvAAEAAgB4AMABLwABAAEAbgABAi8AAgAnAHgAAwIvAD4AAQAAAA8AMAAdAAEAAAAsADAAAQA4AHgAQgAwAAEAAQBaAEMAMAABAAUAeABIADAAAQABAAoASwAwAAEAAQB4AEwAMAABAAEA7P9NADAAAQABAG4ATgAwAAEAAgB4AFAAMAABAAEACgBSADAAAQABAFoAUwAwAAEAAQB4AFQAMAABAAEAzv9VADAAAQABAFAAVgAwAAEAAgB4ALwBMAABAAEAZAC9ATAAAgABAG4AwAEwAAEAAgB4AAMCMAACACgAeAAFAjAAPAABAAAADwAxABwAAgAAACsAMQABADkAeABCADEAAQAXAHgARwAxAAEAAQAUAEgAMQABAAMAeABLADEAAQABAAAATAAxAAIAAgB4AE8AMQABAAEAFABQADEAAQACAHgAUgAxAAEAAQB4AFMAMQABAAEA2P9UADEAAQABAGQAVQAxAAEAAgB4AL4BMQACAAEAbgAFAjEAAgAoAHgABwIxADoAAQAAAEYAMgABAAEAKABHADIAAQADAHgASgAyAAEAAQAKAEsAMgABAAMAeABOADIAAQABACgATwAyAAEAAwB4AFEAMgABAAEAZABSADIAAQABAOz/UwAyAAEAAQBuAFQAMgABAAIAeAC/ATIAAgABAG4ABwIyAAIAKQB4AAkCMgA4AAEAAAAPADMAGwABAAAAKgAzAAEAOAB4AEUAMwABAAEAMgBGADMAAQADAHgASQAzAAEAAQAUAEoAMwABAAwAeABMADMAAQABAHgATQAzAAEAAQAyAE4AMwABAAEARgBQADMAAQABAG4AUQAzAAEAAQAAAFIAMwACAAIAeADAATMAAQABAG4ACQIzAAIAKgB4AAsCMwA2AAEAAAAPADQAGgABAAAAKQA0AAEAOQB4AEQANAABAAEAPABFADQAAQADAHgASAA0AAEAAQAoAEkANAABAAwAeABMADQAAQABADwATQA0AAEAAQBQAE4ANAABAAIAeABQADQAAQABAAoAUQA0AAEAAgB4AMMBNAAPAAEAeAALAjQAAQArAHgADAI0ADUAAQAAAA8ANQAZAAEAAAAoADUAAQA6AHgAQwA1AAEAAQBQAEQANQABABEAeABHADUAAQABADIASAA1AAEADQB4AEsANQABAAEAUABMADUAAQABAGQATQA1AAEAAgB4AE8ANQABAAEAFABQADUAAQACAHgAUgA1AAEAAQB4AMUBNQANAAEAeAAMAjUAAQArAHgA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";


    const float GroundNetOriginX = -620f;
    const float GroundNetOriginZ = -620f;
    const float GroundNetCell = 2f;

    /// <summary>
    /// v2.1.0 — بناء شبكة الأرض من البيانات المخبوزة: مجسّم عشب واحد (ربع
    /// لكل مستطيل، تكرار نسيج 15م) + BoxCollider لكل مستطيل بتنورة سفلية
    /// (القمة عند ارتفاع السطح بالضبط) — فيزياء صناديق رخيصة تماماً بلا
    /// طبخ Concave ولا مسح أشعة عند الإقلاع.
    /// </summary>
    void BuildBakedGroundNet()
    {
        EnsureOvalMaterials();

        byte[] data;
        try { data = System.Convert.FromBase64String(GroundNetB64); }
        catch (System.FormatException)
        {
            Debug.LogWarning("[TrackBuilder] v2.1.0: GroundNetB64 تالف — تخطي الشبكة");
            return;
        }

        int recCount = data.Length / 10;
        if (recCount == 0) return;

        var verts = new List<Vector3>(recCount * 4);
        var tris = new List<int>(recCount * 6);
        var uvs = new List<Vector2>(recCount * 4);

        GameObject go = new GameObject("GroundNet");
        go.transform.SetParent(transform, false);
        go.layer = TrackLayer;

        for (int r = 0; r < recCount; r++)
        {
            int cx = System.BitConverter.ToInt16(data, r * 10);
            int cz = System.BitConverter.ToInt16(data, r * 10 + 2);
            int w2 = System.BitConverter.ToInt16(data, r * 10 + 4);
            int h2 = System.BitConverter.ToInt16(data, r * 10 + 6);
            float y = System.BitConverter.ToInt16(data, r * 10 + 8) * 0.1f;

            float x0 = GroundNetOriginX + cx * GroundNetCell;
            float z0 = GroundNetOriginZ + cz * GroundNetCell;
            float w = w2 * GroundNetCell;
            float h = h2 * GroundNetCell;

            int b = verts.Count;
            verts.Add(new Vector3(x0, y, z0));
            verts.Add(new Vector3(x0 + w, y, z0));
            verts.Add(new Vector3(x0 + w, y, z0 + h));
            verts.Add(new Vector3(x0, y, z0 + h));
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            float u0 = x0 / 15f, v0 = z0 / 15f;
            uvs.Add(new Vector2(u0, v0));
            uvs.Add(new Vector2((x0 + w) / 15f, v0));
            uvs.Add(new Vector2((x0 + w) / 15f, (z0 + h) / 15f));
            uvs.Add(new Vector2(u0, (z0 + h) / 15f));

            // مصادم الصندوق: قمته عند y بالضبط + تنورة حتى 40م تحت — لا تسلل
            float skirt = Mathf.Min(40f, Mathf.Max(10f, y + 32f));
            BoxCollider col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(x0 + w * 0.5f, y - skirt * 0.5f, z0 + h * 0.5f);
            col.size = new Vector3(w, skirt, h);

            // (v2.5.0) تسجيله للتنسيق اللاحق مع سطح الطريق (إزالة/تخفيض قرب الممر)
            bakedSoftRects.Add(new BakedRect { x0 = x0, z0 = z0, w = w, h = h, top = y, col = col });
        }

        Mesh mesh = new Mesh
        {
            name = "GroundNet",
            indexFormat = verts.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16
        };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = ovalMatGrass != null ? ovalMatGrass : ovalMatRock;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Physics.SyncTransforms();

        Debug.Log($"[TrackBuilder] v2.1.0: شبكة الأرض المخبوزة جاهزة — {recCount} مستطيل عشب صلب (بلا أشعة تشغيل)");
    }

    // ================= v2.2.0: البيانات المخبوزة من هندسة APK الحقيقية =================

    /// <summary>خط السباق المخبوز: 96 نقطة (زوج int16 ×0.1م بإحداثيات عالمية) —
    /// مولّد أوفلاين (gen_v220_baked.py) من الشبكة المثلثية المستخرجة من APK المبني.
    /// يبدأ عند إسقاط بوابة الأضواء على منتصف المستقيم الرئيسي باتجاه الشرق.</summary>
    const string BakedLineB64 = @"O/9f/rv/Xf49AF3+wwBf/kkBXv7RAV7+XgJe/vACXv6JA17+LQRe/twEXv6aBV7+awZe/lQHXv5XCFv+bAlJ/oUKGv6XC8b9mAxN/YINsfxSDvj7BQ8k+5gPOfoIED35VRAz+HwQIfd8EA32VRD99AkQ9fOYD/nyBQ8N8lIOOPGBDX7wlgzk75cLa++HChjvbgno7lkI0+5WB87ubAbO7psFzu7dBM7uLwTO7o0Dzu7yAs7uYALO7tMBzu5KAc7uwwDO7j0Az+67/8/uOv/O7rb+zu4t/s7uoP3O7g79zu5z/M7u0fvO7iP7zu5l+s7ulPnO7qr4zu6n99Pukvbo7nn1GO9p9GvvavPk73/yfvCu8Tjx+/AN8mjw+fL37/Xzq+/99ITvDfaE7yH3q+8z+PjvPflo8Dn6+/Ak+67x+Pt+8rH8aPNN/Wn0xv179Rr+lPZJ/qn3W/6s+F7+lfle/mb6Xv4k+17+0/te/nf8Xv4Q/V7+ov1e/i/+Xv63/l7+";

    /// <summary>سطح الطريق الصلب: مستطيلات (cx,cz,w2,h2,y20) على شبكة 2م أصلها
    /// (-600,-600) قمتها y×0.05م — فوق كل خلية أسفلت قابلة للقيادة (TARMAC/PITLANE/
    /// KERB/CONCRETE). بديل مضمون عن MeshCollider المقعّر الذي يفشل طبخه على
    /// الأجهزة للمساحات الضخمة (سبب الغوص الحقيقي) — BoxCollider بلا طبخ إطلاقاً.</summary>
    const string DeckB64 = @"eQBBAAIAAwBqAHgAQgABAAYAbQB2AEMAAgAEAG4AewBDAAEABABqAHUARAABAAQAbwB5AEQAAgACAGwAcwBFAAIAAQBwAHwARQABAAEAagByAEYAAwADAHIAeQBGAAIAAQBtAHAARwACAAIAdAB2AEcAAgACAG8AeQBHAAEAAQBtAG8ASAABAAcAdwB1AEgAAQACAHIAeABIAAEAAQBvALUASAABAAIACgC2AEgAAgABAAgAuABIAAIAAgAGALoASAAFAAEAAwC/AEgAEgABAP//0QBIAAMAFQD+/20ASQACAAYAeABwAEkAAgADAHYAcgBJAAMAAQBzAHYASQABAAEAcQCsAEkAAgABABwArgBJAAIAAQAXALAASQACAAEAEwCyAEkAAQABABAAswBJAAIAAQANALYASQABAAIACAC3AEkAAQAFAAYAugBJAAQAAgADAL4ASQASAAEAAADQAEkAAQATAP7/1ABJAAUAFgD+/2wASgABAAUAeQByAEoAAwABAHQApgBKAAEAAQArAKcASgABAAEAKACoAEoAAQABACUAqQBKAAEAAQAjAKoASgABAAEAIACrAEoAAQABAB4ArABKAAEAAgAbAK0ASgABAAEAGQCuAEoAAgABABYAsABKAAIAAQARALIASgACAAIADQC0AEoAAQACAAoAtQBKAAEAAwAJALgASgACAAQABAC+AEoAEAABAP//zgBKAAIAEQD+/9kASgAFABoA/v9xAUoAIAAMAP7/kQFKAAsAAQACAJwBSgAEAAEABQCgAUoAAgACAAYAawBLAAEAAwB6AHIASwACAAEAdACiAEsAAQABADIAowBLAAEAAQAvAKQASwABAAEALQClAEsAAQABACoApgBLAAEAAQAoAKcASwABAAEAJQCoAEsAAQABACMAqQBLAAEAAQAhAKoASwABAAEAHgCrAEsAAQACABsArQBLAAIAAQAWAK8ASwACAAEAEgCxAEsAAQADAA4AtgBLAAEABAAGALoASwAFAAIAAgC/AEsADwAMAP//3gBLAJMACwD+/5EBSwALAAsA//+cAUsABAACAAMAogFLAAEABAAGAKMBSwACAAIACQClAUsAAgABAA0ApwFLAAIAAQARAKkBSwACAAEAFgCrAUsAAQABABoAagBMAAEAAwB7AHAATAACAAEAdwCeAEwAAQABADoAnwBMAAEAAQA3AKAATAABAAEANAChAEwAAQABADEAogBMAAEAAQAvAKMATAABAAEALACkAEwAAQABACoApQBMAAIAAQAmAKcATAABAAEAIwCoAEwAAQABACAAqQBMAAEAAQAeAKoATAABAAIAGwCsAEwAAgABABcArgBMAAIAAQATALAATAABAAEAEACyAEwAAgABAAwAtABMAAEAAgAIAKABTAACAAQABAClAUwAAgABAAwApwFMAAEAAgAOAKgBTAACAAEAEQCqAUwAAQACABMAqwFMAAIAAQAXAK0BTAABAAEAGgCuAUwAAQABAB0ArwFMAAEAAQAfALABTAABAAEAIgCxAUwAAQABACUAaABNAAIABAB8AHAATQABAAEAdwCbAE0AAQABAD8AnABNAAEAAQA8AJ0ATQABAAEAOQCeAE0AAQABADYAnwBNAAIAAQAyAKEATQACAAEALQCjAE0AAgABACgApQBNAAEAAQAlAKYATQACAAEAIQCoAE0AAQABAB4AqQBNAAEAAQAcAKsATQACAAEAFwCtAE0AAgABABMArwBNAAEAAQAQALAATQABAAIADgCyAE0AAQACAAoAswBNAAEAAgAIALUATQABAAMABgC6AE0ABQAFAAEAnAFNAAQAAwACAKMBTQACAAEACAClAU0AAQACAAgApgFNAAEAAgAKAKgBTQABAAIADgCpAU0AAQABABAAqwFNAAEAAgATAKwBTQACAAEAFwCuAU0AAgABABsAsAFNAAEAAQAeALEBTQACAAEAIQCzAU0AAQABACUAZwBOAAEABgB+AGsATgABAAQAfACYAE4AAQABAEQAmQBOAAEAAQBBAJoATgABAAEAPgCbAE4AAQABADsAnABOAAEAAQA4AJ0ATgABAAEANQCeAE4AAQABADIAnwBOAAEAAQAwAKAATgABAAEALQChAE4AAQABACsAogBOAAEAAQAoAKMATgACAAEAJQClAE4AAgABACEApwBOAAEAAQAeAKgATgABAAEAGwCpAE4AAQABABkAqgBOAAIAAQAWAKwATgACAAEAEwCuAE4AAQABABAArwBOAAEAAgAOALEATgABAAIACgC0AE4AAQADAAYAtwBOAAMAAwADAKMBTgACAAIABgCnAU4AAQACAAoAqQFOAAEAAgAOAKoBTgABAAEAEACsAU4AAgABABQArgFOAAEAAgAWAK8BTgABAAEAGQCwAU4AAQABABsAsQFOAAEAAQAeALIBTgACAAEAIQC0AU4AAgABACUAtgFOAAEAAQAoAGYATwABAAQAfgBqAE8AAQAEAHwAbABPAAIAAQB6AG4ATwABAAEAeQCVAE8AAQABAEoAlgBPAAEAAQBHAJcATwABAAEAQwCYAE8AAQABAEAAmQBPAAEAAQA9AJoATwABAAEAOgCbAE8AAQABADcAnABPAAEAAQA0AJ0ATwABAAEAMQCeAE8AAQABAC8AnwBPAAEAAQAsAKAATwABAAEAKgChAE8AAgABACYAowBPAAEAAQAjAKQATwACAAEAIACmAE8AAgABABwAqABPAAEAAQAZAKkATwACAAEAFgCrAE8AAgABABIArQBPAAEAAQAQAK4ATwABAAIADQCwAE8AAQACAAoAsgBPAAEAAQAJALMATwABAAMABgC2AE8AAQAEAAMAogFPAAEABAADAKUBTwABAAMABgCmAU8AAQABAAkAqAFPAAEAAgAKAKoBTwABAAIADQCrAU8AAgABABEArQFPAAEAAgASAK8BTwABAAEAFwCwAU8AAQABABkAsQFPAAIAAQAcALMBTwACAAEAIAC1AU8AAQABACMAtgFPAAEAAQAlALcBTwABAAEAKAC4AU8AAQABACoAuQFPAAEAAQAsALoBTwABAAEALwBlAFAAAQAEAIAAbABQAAEAAQB7AJIAUAABAAEAUQCTAFAAAQABAE0AlABQAAEAAQBKAJUAUAABAAEARgCWAFAAAQABAEIAlwBQAAEAAQA/AJgAUAABAAEAPACZAFAAAQABADkAmgBQAAEAAQA2AJsAUAABAAEAMwCcAFAAAQABADAAnQBQAAEAAQAuAJ4AUAABAAEAKwCfAFAAAQABACkAoABQAAIAAQAlAKIAUAACAAEAIQCkAFAAAQABAB4ApQBQAAIAAQAbAKcAUAACAAEAFwCpAFAAAgABABQAqwBQAAIAAQAQAK0AUAABAAIADQCvAFAAAQABAAsAsQBQAAIAAQAIALUAUAABAAQAAwCcAVAABQADAAEAoQFQAAEABAACAKMBUAACAAMABACmAVAAAgABAAgAqQFQAAEAAQALAKsBUAACAAEADgCuAVAAAgABABQAsAFQAAIAAQAXALIBUAACAAEAGwC0AVAAAQABAB4AtQFQAAIAAQAhALcBUAACAAEAJQC5AVAAAQABACkAugFQAAEAAQArALsBUAABAAEALgC8AVAAAQABADAAvQFQAAEAAQAzAGMAUQACAAYAgQBoAFEAAgACAH4AkABRAAEAAQBVAJEAUQABAAEAUQCSAFEAAQABAE0AkwBRAAEAAQBJAJQAUQABAAEARQCVAFEAAQABAEIAlgBRAAEAAQA+AJcAUQABAAEAOwCYAFEAAQABADgAmQBRAAEAAQA1AJoAUQABAAEAMgCbAFEAAQABAC8AnABRAAEAAQAsAJ0AUQABAAEAKgCeAFEAAgABACYAoABRAAEAAQAjAKEAUQABAAEAIACiAFEAAQABAB4AowBRAAIAAQAbAKUAUQABAAEAGQCmAFEAAgABABYAqABRAAIAAQASAKoAUQACAAEAEACsAFEAAQACAAwArgBRAAEAAQALAK8AUQACAAEACACxAFEAAgACAAYAtABRAAEAAwADALcAUQADAAQAAQCmAVEAAgACAAYAqAFRAAIAAQAIAKoBUQADAAEADACtAVEAAgABABAArwFRAAIAAQASALEBUQACAAEAFgCzAVEAAgABABoAtQFRAAEAAQAcALYBUQABAAEAHgC3AVEAAQABACAAuAFRAAEAAQAjALkBUQACAAEAJgC7AVEAAQABACoAvAFRAAEAAQAsAL0BUQABAAEALwC+AVEAAQABADIAvwFRAAEAAQA1AMABUQABAAEAOABiAFIAAQAGAIIAjgBSAAEAAQBZAI8AUgABAAEAVQCQAFIAAQABAFAAkQBSAAEAAQBMAJIAUgABAAEASACTAFIAAQABAEQAlABSAAEAAQBAAJUAUgABAAEAPQCWAFIAAQABADoAlwBSAAEAAQA2AJgAUgABAAEAMwCZAFIAAQABADAAmgBSAAEAAQAtAJsAUgABAAEAKwCcAFIAAQABACgAnQBSAAEAAQAmAJ4AUgABAAEAIwCfAFIAAgABACAAoQBSAAIAAQAcAKMAUgABAAEAGQCkAFIAAgABABYApgBSAAIAAQATAKgAUgACAAEAEACqAFIAAgABAA0ArQBSAAMAAQAJALAAUgABAAIABgCzAFIAAQACAAQAugBSAAUACAD//6UBUgABAAIABACoAVIAAgABAAgAqgFSAAIAAQAKAKwBUgACAAEADACuAVIAAgABAA4AsAFSAAIAAQASALIBUgACAAEAFAC0AVIAAQABABcAtQFSAAEAAQAZALYBUgACAAEAHAC4AVIAAgABACAAugFSAAEAAQAjALsBUgABAAEAJgC8AVIAAQABACgAvQFSAAEAAQArAL4BUgABAAEALQC/AVIAAQABADAAwAFSAAEAAQAzAMEBUgABAAEANgDCAVIAAQABADoAwwFSAAEAAQA9AGEAUwABAAYAgwBmAFMAAQADAIAAaABTAAEAAQB/AIwAUwABAAEAXQCNAFMAAQABAFkAjgBTAAEAAQBUAI8AUwABAAEAUACQAFMAAQABAEsAkQBTAAEAAQBHAJIAUwABAAEAQwCTAFMAAQABAEAAlABTAAEAAQA8AJUAUwABAAEAOACWAFMAAQABADUAlwBTAAEAAQAyAJgAUwABAAEALwCZAFMAAQABACwAmgBTAAIAAQAoAJwAUwABAAEAJACdAFMAAQABACIAngBTAAEAAQAfAJ8AUwACAAEAHAChAFMAAQABABkAogBTAAIAAQAWAKQAUwACAAEAEwCmAFMAAgABABAAqABTAAIAAQANAKoAUwACAAEACgCsAFMAAgABAAgArgBTAAIAAQAGALEAUwACAAIAAwC2AFMAAQAEAAAAnAFTAAUABAD//6IBUwADAAMAAQCmAVMAAwABAAQAqQFTAAIAAQAGAKsBUwADAAEACQCuAVMAAgABAAwAsAFTAAIAAQAOALIBUwACAAEAEgC0AVMAAQABABQAtQFTAAIAAQAWALcBUwABAAEAGQC4AVMAAgABABwAugFTAAEAAQAfALsBUwABAAEAIgC8AVMAAQABACQAvQFTAAIAAQAoAL8BUwABAAEALADAAVMAAQABAC8AwQFTAAEAAQAyAMIBUwABAAEANQDDAVMAAQABADgAxAFTAAEAAQA8AMUBUwABAAEAQABgAFQAAQAEAIQAZQBUAAEAAwCBAGcAVAABAAEAgACKAFQAAQABAGIAiwBUAAEAAQBdAIwAVAABAAEAWACNAFQAAQABAFMAjgBUAAEAAQBPAI8AVAABAAEASwCQAFQAAQABAEYAkQBUAAEAAQBCAJIAVAABAAEAPwCTAFQAAQABADsAlABUAAEAAQA3AJUAVAABAAEANACWAFQAAQABADAAlwBUAAEAAQAtAJgAVAABAAEAKgCZAFQAAQABACgAmgBUAAEAAQAlAJsAVAACAAEAIQCdAFQAAQABAB4AngBUAAEAAQAbAJ8AVAABAAEAGQCgAFQAAgABABYAogBUAAIAAQATAKQAVAACAAEAEACmAFQAAgABAA0AqABUAAIAAQAKAKoAVAACAAEACACsAFQABAABAAUAsABUAAEAAgACALMAVAADAAMAAAChAVQAAQAEAAAApQFUAAQAAQACAKkBVAAEAAEABQCtAVQAAwABAAkAsAFUAAIAAQAMALIBVAACAAEADgC0AVQAAgABABEAtgFUAAIAAQAUALgBVAABAAEAFwC5AVQAAQABABkAugFUAAEAAQAbALsBVAABAAEAHgC8AVQAAgABACEAvgFUAAEAAQAlAL8BVAABAAEAKADAAVQAAQABACoAwQFUAAEAAQAtAMIBVAABAAEAMADDAVQAAQABADQAxAFUAAEAAQA3AMUBVAABAAEAOwDGAVQAAQABAD8AxwFUAAEAAQBCAF8AVQABAAQAhACIAFUAAQABAGYAiQBVAAEAAQBhAIoAVQABAAEAXACLAFUAAQABAFcAjABVAAEAAQBTAI0AVQABAAEATgCOAFUAAQABAEoAjwBVAAEAAQBGAJAAVQABAAEAQQCRAFUAAQABAD0AkgBVAAEAAQA6AJMAVQABAAEANgCUAFUAAQABADIAlQBVAAEAAQAvAJYAVQABAAEALACXAFUAAQABACkAmABVAAEAAQAmAJkAVQABAAEAIwCaAFUAAQABACAAmwBVAAEAAQAeAJwAVQABAAEAHACdAFUAAQABABkAngBVAAIAAQAWAKAAVQACAAEAEgCiAFUAAQABABAAowBVAAMAAQANAKYAVQADAAEACQCpAFUAAwABAAYArABVAAQAAQADALEAVQACAAIAAAC3AFUAAwAFAP7/pQFVAAUAAQABAKoBVQAEAAEABACuAVUAAgABAAYAsAFVAAMAAQAJALMBVQADAAEADQC2AVUAAgABABEAuAFVAAEAAQATALkBVQACAAEAFgC7AVUAAQABABkAvAFVAAEAAQAcAL0BVQABAAEAHgC+AVUAAQABACAAvwFVAAEAAQAjAMABVQABAAEAJgDBAVUAAQABACkAwgFVAAEAAQAsAMMBVQABAAEALwDEAVUAAQABADIAxQFVAAEAAQA2AMYBVQABAAEAOgDHAVUAAQABAD0A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iQBfAAEAAQAlAIoAXwABAAEAIACLAF8AAQABABwAjABfAAEAAQAYAI0AXwABAAEAFACOAF8AAQABABAAjwBfAAEAAQANAJAAXwABAAEACQCRAF8AAQABAAYAkgBfAAEAAQADAJMAXwACAAQA/v/VAF8ABAABAP7/5QBfAAIADwD+/+cAXwABAAEABQDFAV8AAQABAAAAxgFfAAEAAQADAMcBXwABAAEABgDIAV8AAQABAAkAyQFfAAEAAQANAMoBXwABAAEAEADLAV8AAQABABQAzAFfAAEAAQAYAM0BXwABAAEAHADOAV8AAQABACAAzwFfAAEAAQAlANABXwABAAEAKQDRAV8AAQABAC4A0gFfAAEAAQAzANMBXwABAAEANwDUAV8AAQABADwA1QFfAAEAAQBBANYBXwABAAEARgDXAV8AAQABAEwA2AFfAAEAAQBRANkBXwABAAEAVgDaAV8AAQABAFwAeABgAAEAAQB4AHkAYAABAAEAcgB6AGAAAQABAGwAewBgAAEAAQBmAHwAYAABAAEAYQB9AGAAAQABAFsAfgBgAAEAAQBVAH8AYAABAAEAUACAAGAAAQABAEoAgQBgAAEAAQBFAIIAYAABAAEAQACDAGAAAQABADsAhABgAAEAAQA2AIUAYAABAAEAMQCGAGAAAQABACwAhwBgAAEAAQAnAIgAYAABAAEAIwCJAGAAAQABAB4AigBgAAEAAQAaAIsAYAABAAEAFgCMAGAAAQABABIAjQBgAAEAAQAOAI4AYAABAAEACgCPAGAAAQABAAcAkABgAAEAAQADAJEAYAACAAQA/v+YAGAAAgABAP7/1gBgAAMAAQD+/+cAYAABAA8A/v/oAGAAAQABAAQAxwFgAAEAAQAAAMgBYAABAAEAAwDJAWAAAQABAAcAygFgAAEAAQAKAMsBYAABAAEADgDMAWAAAQABABIAzQFgAAEAAQAWAM4BYAABAAEAGgDPAWAAAQABAB4A0AFgAAEAAQAjANEBYAABAAEAJwDSAWAAAQABACwA0wFgAAEAAQAxANQBYAABAAEANgDVAWAAAQABADsA1gFgAAEAAQBAANcBYAABAAEARQDYAWAAAQABAEoA2QFgAAEAAQBQANoBYAABAAEAVQDbAWAAAQABAFsAVQBhAAEABACOAHcAYQABAAEAeAB4AGEAAQABAHIAeQBhAAEAAQBsAHoAYQABAAEAZgB7AGEAAQABAGAAfABhAAEAAQBaAH0AYQABAAEAVAB+AGEAAQABAE8AfwBhAAEAAQBJAIAAYQABAAEARACBAGEAAQABAD8AggBhAAEAAQA5AIMAYQABAAEANACEAGEAAQABAC8AhQBhAAEAAQAqAIYAYQABAAEAJgCHAGEAAQABACEAiABhAAEAAQAcAIkAYQABAAEAGACKAGEAAQABABQAiwBhAAEAAQAPAIwAYQABAAEACwCNAGEAAQABAAgAjgBhAAEAAQAEAI8AYQACAAQA/v/XAGEAAgABAP7/6ABhAAEAEAD+/+kAYQABAAEAAwCcAWEAAQADAAAAyQFhAAEAAQAAAMoBYQABAAEABADLAWEAAQABAAgAzAFhAAEAAQALAM0BYQABAAEADwDOAWEAAQABABQAzwFhAAEAAQAYANABYQABAAEAHADRAWEAAQABACEA0gFhAAEAAQAmANMBYQABAAEAKgDUAWEAAQABAC8A1QFhAAEAAQA0ANYBYQABAAEAOQDXAWEAAQABAD8A2AFhAAEAAQBEANkBYQABAAEASQDaAWEAAQABAE8A2wFhAAEAAQBUANwBYQABAAEAWgDdAWEAAQABAGAAVABiAAEABwCPAFoAYgABAAEAjAB2AGIAAQABAHgAdwBiAAEAAQByAHgAYgABAAEAbAB5AGIAAQABAGYAegBiAAEAAQBgAHsAYgABAAEAWgB8AGIAAQABAFQAfQBiAAEAAQBOAH4AYgABAAEASAB/AGIAAQABAEMAgABiAAEAAQA9AIEAYgABAAEAOACCAGIAAQABADMAgwBiAAEAAQAuAIQAYgABAAEAKQCFAGIAAQABACQAhgBiAAEAAQAfAIcAYgABAAEAGgCIAGIAAQABABYAiQBiAAEAAQARAIoAYgABAAEADQCLAGIAAQABAAkAjABiAAEAAQAFAI0AYgABAAEAAQCOAGIAAQAEAP7/lQBiAAEAAQD+/9gAYgABAAEA/v/pAGIAAQAPAP7/6gBiAAEAAQACAMsBYgABAAEAAQDMAWIAAQABAAUAzQFiAAEAAQAJAM4BYgABAAEADQDPAWIAAQABABEA0AFiAAEAAQAWANEBYgABAAEAGgDSAWIAAQABAB8A0wFiAAEAAQAkANQBYgABAAEAKQDVAWIAAQABAC4A1gFiAAEAAQAzANcBYgABAAEAOADYAWIAAQABAD0A2QFiAAEAAQBDANoBYgABAAEASADbAWIAAQABAE4A3AFiAAEAAQBUAN0BYgABAAEAWgDeAWIAAQABAGAAWABjAAEAAwCNAHQAYwABAAEAfwB1AGMAAQABAHkAdgBjAAEAAQByAHcAYwABAAEAbAB4AGMAAQABAGUAeQBjAAEAAQBfAHoAYwABAAEAWQB7AGMAAQABAFMAfABjAAEAAQBNAH0AYwABAAEARwB+AGMAAQABAEIAfwBjAAEAAQA8AIAAYwABAAEANwCBAGMAAQABADEAggBjAAEAAQAsAIMAYwABAAEAJwCEAGMAAQABACIAhQBjAAEAAQAdAIYAYwABAAEAGACHAGMAAQABABQAiABjAAEAAQAPAIkAYwABAAEACwCKAGMAAQABAAYAiwBjAAEAAQACAIwAYwACAAQA/v/qAGMAAQAPAP7/6wBjAAEAAQABAM0BYwABAAEAAgDOAWMAAQABAAYAzwFjAAEAAQALANABYwABAAEADwDRAWMAAQABABQA0gFjAAEAAQAYANMBYwABAAEAHQDUAWMAAQABACIA1QFjAAEAAQAnANYBYwABAAEALADXAWMAAQABADEA2AFjAAEAAQA3ANkBYwABAAEAPADaAWMAAQABAEIA2wFjAAEAAQBHANwBYwABAAEATQDdAWMAAQABAFMA3gFjAAEAAQBZAN8BYwABAAEAXwBTAGQAAQAJAJAAVwBkAAEAAwCOAHMAZAABAAEAgAB0AGQAAQABAHkAdQBkAAEAAQByAHYAZAABAAEAbAB3AGQAAQABAGUAeABkAAEAAQBfAHkAZAABAAEAWQB6AGQAAQABAFMAewBkAAEAAQBNAHwAZAABAAEARwB9AGQAAQABAEEAfgBkAAEAAQA7AH8AZAABAAEANgCAAGQAAQABADAAgQBkAAEAAQArAIIAZAABAAEAJQCDAGQAAQABACAAhABkAAEAAQAbAIUAZAABAAEAFgCGAGQAAQABABIAhwBkAAEAAQANAIgAZAABAAEACACJAGQAAQABAAQAigBkAAIABAD+/5EAZAABAAEA/v/bAGQAAwABAP7/6wBkAAIADwD+/5wBZAABABcAAwDOAWQAAQABAAAAzwFkAAEAAQAEANABZAABAAEACADRAWQAAQABAA0A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2QFqAAEAAQANANoBagABAAEAEwDbAWoAAQABABkA3AFqAAEAAQAfAN0BagABAAEAJQDeAWoAAQABACsA3wFqAAEAAQAyAOABagABAAEAOADhAWoAAQABAD8A4gFqAAEAAQBGAOMBagABAAEATADkAWoAAQABAFMA5QFqAAEAAQBaAOYBagABAAEAYQDnAWoAAQABAGkATgBrAAEACwCRAGwAawABAAEAiABtAGsAAQABAIAAbgBrAAEAAQB5AG8AawABAAEAcQBwAGsAAQABAGoAcQBrAAEAAQBjAHIAawABAAEAWwBzAGsAAQABAFQAdABrAAEAAQBNAHUAawABAAEARgB2AGsAAQABAD8AdwBrAAEAAQA4AHgAawABAAEAMgB5AGsAAQABACsAegBrAAEAAQAlAHsAawABAAEAHwB8AGsAAQABABgAfQBrAAEAAQASAH4AawABAAEADAB/AGsAAQABAAcAgABrAAEAAQABAIEAawABAAQA/v/1AGsAmQALAP7/2AFrAAEAAQABANkBawABAAEABwDaAWsAAQABAAwA2wFrAAEAAQASANwBawABAAEAGADdAWsAAQABAB8A3gFrAAEAAQAlAN8BawABAAEAKwDgAWsAAQABADIA4QFrAAEAAQA4AOIBawABAAEAPwDjAWsAAQABAEYA5AFrAAEAAQBNAOUBawABAAEAVADmAWsAAQABAFsA5wFrAAEAAQBjAOgBawABAAEAagBNAGwAAQAMAJEAawBsAAEAAQCKAGwAbAABAAEAggBtAGwAAQABAHoAbgBsAAEAAQBzAG8AbAABAAEAawBwAGwAAQABAGQAcQBsAAEAAQBcAHIAbAABAAEAVQBzAGwAAQABAE4AdABsAAEAAQBHAHUAbAABAAEAQAB2AGwAAQABADkAdwBsAAEAAQAyAHgAbAABAAEAKwB5AGwAAQABACUAegBsAAEAAQAeAHsAbAABAAEAGAB8AGwAAQABABIAfQBsAAEAAQAMAH4AbAABAAEABgB/AGwAAgAEAP7/hABsAAEAAQD+/48BbAABAAwA/v/ZAWwAAQABAAAA2gFsAAEAAQAGANsBbAABAAEADADcAWwAAQABABIA3QFsAAEAAQAYAN4BbAABAAEAHgDfAWwAAQABACUA4AFsAAEAAQArAOEBbAABAAEAMgDiAWwAAQABADkA4wFsAAEAAQBAAOQBbAABAAEARwDlAWwAAQABAE4A5gFsAAEAAQBVAOcBbAABAAEAXADoAWwAAQABAGQA6QFsAAEAAQBrAGsAbQABAAEAhABsAG0AAQABAHwAbQBtAAEAAQB0AG4AbQABAAEAbQBvAG0AAQABAGUAcABtAAEAAQBdAHEAbQABAAEAVgByAG0AAQABAE8AcwBtAAEAAQBHAHQAbQABAAEAQAB1AG0AAQABADkAdgBtAAEAAQAyAHcAbQABAAEAKwB4AG0AAQABACUAeQBtAAEAAQAeAHoAbQABAAEAGAB7AG0AAQABABEAfABtAAEAAQALAH0AbQABAAEABQB+AG0AAQAFAP7/2gFtAAEAAQD//9sBbQABAAEABQDcAW0AAQABAAsA3QFtAAEAAQARAN4BbQABAAEAGADfAW0AAQABAB4A4AFtAAEAAQAlAOEBbQABAAEAKwDiAW0AAQABADIA4wFtAAEAAQA5AOQBbQABAAEAQADlAW0AAQABAEcA5gFtAAEAAQBPAOcBbQABAAEAVgDoAW0AAQABAF0A6QFtAAEAAQBlAEwAbgABAAsAkQBqAG4AAQABAIYAawBuAAEAAQB+AGwAbgABAAEAdgBtAG4AAQABAG4AbgBuAAEAAQBmAG8AbgABAAEAXwBwAG4AAQABAFcAcQBuAAEAAQBQAHIAbgABAAEASABzAG4AAQABAEEAdABuAAEAAQA6AHUAbgABAAEAMwB2AG4AAQABACwAdwBuAAEAAQAlAHgAbgABAAEAHgB5AG4AAQABABcAegBuAAEAAQARAHsAbgABAAEACwB8AG4AAQABAAQAfQBuAAEABQD+/9wBbgABAAEABADdAW4AAQABAAsA3gFuAAEAAQARAN8BbgABAAEAFwDgAW4AAQABAB4A4QFuAAEAAQAlAOIBbgABAAEALADjAW4AAQABADMA5AFuAAEAAQA6AOUBbgABAAEAQQDmAW4AAQABAEgA5wFuAAEAAQBQAOgBbgABAAEAVwDpAW4AAQABAF8A6gFuAAEAAQBmAGkAbwABAAEAiABqAG8AAQABAIAAawBvAAEAAQB4AGwAbwABAAEAcABtAG8AAQABAGgAbgBvAAEAAQBgAG8AbwABAAEAWQBwAG8AAQABAFEAcQBvAAEAAQBJAHIAbwABAAEAQgBzAG8AAQABADsAdABvAAEAAQAzAHUAbwABAAEALAB2AG8AAQABACUAdwBvAAEAAQAeAHgAbwABAAEAGAB5AG8AAQABABEAegBvAAEAAQAKAHsAbwABAAEABAB8AG8AAQAFAP7/vABvAAMACQD//90BbwABAAEABADeAW8AAQABAAoA3wFvAAEAAQARAOABbwABAAEAGADhAW8AAQABAB4A4gFvAAEAAQAlAOMBbwABAAEALADkAW8AAQABADMA5QFvAAEAAQA7AOYBbwABAAEAQgDnAW8AAQABAEkA6AFvAAEAAQBRAOkBbwABAAEAWQDqAW8AAQABAGAA6wFvAAEAAQBoAEsAcAABAAsAkQBoAHAAAQABAIoAaQBwAAEAAQCCAGoAcAABAAEAegBrAHAAAQABAHIAbABwAAEAAQBqAG0AcAABAAEAYgBuAHAAAQABAFoAbwBwAAEAAQBSAHAAcAABAAEASwBxAHAAAQABAEMAcgBwAAEAAQA8AHMAcAABAAEANAB0AHAAAQABAC0AdQBwAAEAAQAmAHYAcAABAAEAHwB3AHAAAQABABgAeABwAAEAAQARAHkAcAABAAEACgB6AHAAAQABAAQAewBwAAEABQD+/38AcAABAAEA/v+1AHAABwAIAP//vwBwAAcACAD//94BcAABAAEABADfAXAAAQABAAoA4AFwAAEAAQARAOEBcAABAAEAGADiAXAAAQABAB8A4wFwAAEAAQAmAOQBcAABAAEALQDlAXAAAQABADQA5gFwAAEAAQA8AOcBcAABAAEAQwDoAXAAAQABAEsA6QFwAAEAAQBSAOoBcAABAAEAWgDrAXAAAQABAGIA7AFwAAEAAQBqAGcAcQABAAEAjABoAHEAAQABAIQAaQBxAAEAAQB8AGoAcQABAAEAdABrAHEAAQABAGwAbABxAAEAAQBkAG0AcQABAAEAXABuAHEAAQABAFQAbwBxAAEAAQBMAHAAcQABAAEARQBxAHEAAQABAD0AcgBxAAEAAQA1AHMAcQABAAEALgB0AHEAAQABACcAdQBxAAEAAQAfAHYAcQABAAEAGAB3AHEAAQABABEAeABxAAEAAQAKAHkAcQABAAEAAwB6AHEAAQAFAP7/sgBxAAMACQD//8YAcQADAAgA///pAHEAAQABAAEA3wFxAAEAAQADAOABcQABAAEACgDhAXEAAQABABEA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7QF4AAEAAQBCAO4BeAABAAEASgDvAXgAAQABAFMA8AF4AAEAAQBbAPEBeAABAAEAZADyAXgAAQABAGwARgB5AAEADACSAGIAeQABAAEAiQBjAHkAAQABAIAAZAB5AAEAAQB4AGUAeQABAAEAbwBmAHkAAQABAGYAZwB5AAEAAQBeAGgAeQABAAEAVQBpAHkAAQABAE0AagB5AAEAAQBEAGsAeQABAAEAPABsAHkAAQABADMAbQB5AAEAAQArAG4AeQABAAEAIwBvAHkAAQABABsAcAB5AAEAAQATAHEAeQABAAEACwByAHkAAQABAAMAcwB5AAEABQD+/6YAeQABAA4A///HAHkAAgABAP//1QB5AAEADQD//+YBeQABAAEAAwDnAXkAAQABAAsA6AF5AAEAAQATAOkBeQABAAEAGwDqAXkAAQABACMA6wF5AAEAAQArAOwBeQABAAEAMwDtAXkAAQABADwA7gF5AAEAAQBEAO8BeQABAAEATQDwAXkAAQABAFUA8QF5AAEAAQBeAPIBeQABAAEAZgDzAXkAAQABAG8AYQB6AAEAAQCMAGIAegABAAEAhABjAHoAAQABAHsAZAB6AAEAAQByAGUAegABAAEAaQBmAHoAAQABAGEAZwB6AAEAAQBYAGgAegABAAEATwBpAHoAAQABAEcAagB6AAEAAQA+AGsAegABAAEANgBsAHoAAQABAC4AbQB6AAEAAQAlAG4AegABAAEAHQBvAHoAAQABABUAcAB6AAEAAQANAHEAegABAAEABAByAHoAAQAGAP7/pQB6AAEADwD//9YAegABAA0A///XAHoAAQABAAIA5wF6AAEAAQAEAOgBegABAAEADQDpAXoAAQABABUA6gF6AAEAAQAdAOsBegABAAEAJQDsAXoAAQABAC4A7QF6AAEAAQA2AO4BegABAAEAPgDvAXoAAQABAEcA8AF6AAEAAQBPAPEBegABAAEAWADyAXoAAQABAGEA8wF6AAEAAQBpAPQBegABAAEAcgBFAHsAAQAMAJIAYAB7AAEAAQCQAGEAewABAAEAhwBiAHsAAQABAH4AYwB7AAEAAQB1AGQAewABAAEAbQBlAHsAAQABAGQAZgB7AAEAAQBbAGcAewABAAEAUgBoAHsAAQABAEoAaQB7AAEAAQBBAGoAewABAAEAOQBrAHsAAQABADAA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7QCAAAEAFQD//+sBgAABAAEAAgDsAYAAAQABAAsA7QGAAAEAAQAUAO4BgAABAAEAHADvAYAAAQABACUA8AGAAAEAAQAuAPEBgAABAAEANwDyAYAAAQABAEAA8wGAAAEAAQBIAPQBgAABAAEAUQD1AYAAAQABAFoA9gGAAAEAAQBjAPcBgAABAAEAbABCAIEAAQAQAJIAXQCBAAEAAQCMAF4AgQABAAEAgwBfAIEAAQABAHoAYACBAAEAAQBwAGEAgQABAAEAZwBiAIEAAQABAF4AYwCBAAEAAQBVAGQAgQABAAEATABlAIEAAQABAEMAZgCBAAEAAQA6AGcAgQABAAEAMQBoAIEAAQABACgAaQCBAAEAAQAgAGoAgQABAAEAFwBrAIEAAQABAA4AbACBAAEAAQAFAG0AgQABAAcA/v/cAIEAAQABAAIA7ACBAAEAAQACAPUAgQABAAIA///2AIEAAQABAAIA7AGBAAEAAQAFAO0BgQABAAEADgDuAYEAAQABABcA7wGBAAEAAQAgAPABgQABAAEAKADxAYEAAQABADEA8gGBAAEAAQA6APMBgQABAAEAQwD0AYEAAQABAEwA9QGBAAEAAQBVAPYBgQABAAEAXgD3AYEAAQABAGcA+AGBAAEAAQBwAEEAggABABMAkgBcAIIAAQABAJAAXQCCAAEAAQCHAF4AggABAAEAfgBfAIIAAQABAHUAYACCAAEAAQBrAGEAggABAAEAYgBiAIIAAQABAFkAYwCCAAEAAQBQAGQAggABAAEARwBlAIIAAQABAD4AZgCCAAEAAQA1AGcAggABAAEALABoAIIAAQABACMAaQCCAAEAAQAaAGoAggABAAEAEQBrAIIAAQABAAkAbACCAAEACAD+/58AggABACEA///cAIIAAQALAP//3QCCAAEAAQACAOsAggABAAEAAgDsAIIAAQASAP//7AGCAAEAAQAAAO0BggABAAEACQDuAYIAAQABABEA7wGCAAEAAQAaAPABggABAAEAIwDxAYIAAQABACwA8gGCAAEAAQA1APMBggABAAEAPgD0AYIAAQABAEcA9QGCAAEAAQBQAPYBggABAAEAWQD3AYIAAQABAGIA+AGCAAEAAQBrAFwAgwABAAEAiwBdAIMAAQABAIIAXgCDAAEAAQB5AF8AgwABAAEAcABgAIMAAQABAGYA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AgKfAAEAAgBlAAMCnwABAAIAbwBSAKAAAQACAIwAUwCgAAEAAgCCAFkAoAABAAEARQBdAKAAAQACABwAXgCgAAEAAQATAF8AoAABAAEACACnAKAAAQAWAP//UAGgAAEAAQBJAFEBoAACAAEARgBTAaAABQABAEQAkAGgAAIAAQD+/5IBoAABAAIAAACUAaAABAABAAMAmAGgAAMABAAFAPkBoAABAAEACAD6AaAAAQABABMA+wGgAAEAAgAcAP8BoAABAAEARQA5AKEAAQAwAJIAVAChAAEAAQB3AFUAoQABAAEAbABWAKEAAQABAGIAVwChAAEAAQBYAFgAoQABAAEATgBZAKEAAQACAEIAWgChAAEAAQA5AFsAoQABAAEALwBcAKEAAQABACUAXgChAAEAAgAQAF8AoQABAAIABQBQAaEAAQACAEYAUQGhAAIAAQBBAFMBoQAEAAEAPgBXAaEAAgABADwAkAGhAAIAAQD8/5QBoQADAAEAAgCXAaEAAQADAAMAmwGhAAEABgAEAPkBoQABAAIABQD6AaEAAQACABAA/AGhAAEAAQAlAP0BoQABAAEALwD+AaEAAQABADkA/wGhAAEAAgBCAAACoQABAAEATgABAqEAAQABAFgAAgKhAAEAAQBiAAMCoQABAAEAbABSAKIAAQACAIgAUwCiAAEAAgB+AFQAogABAAIAdABVAKIAAQACAGoAVgCiAAEAAgBgAFcAogABAAIAVQBYAKIAAQACAEsAWgCiAAEAAgA2AFsAogABAAEALQBcAKIAAQABACMAXQCiAAEAAQAZAKgAogABABYA//9RAaIAAQABAD4AUgGiAAIAAQA6AFQBogAEAAEAOABYAaIAAQABADYAkAGiAAIAAQD6/5IBogACAAEA/f+UAaIAAwABAAEA+wGiAAEAAQAZAPwBogABAAEAIwD9AaIAAQABAC0A/gGiAAEAAgA2AAACogABAAIASwABAqIAAQACAFUAAgKiAAEAAgBgAAMCogABAAIAagBZAKMAAQABAEAAWwCjAAEAAgAqAFwAowABAAIAIABdAKMAAQACABYAXgCjAAEAAgAMAF8AowABAAIAAgBRAaMAAQABADkAUgGjAAEAAQA2AFMBowAEAAEAMwBXAaMAAQABADIAWAGjAAEAAQAwAJABowABAAEA9/+RAaMAAgABAPr/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kAG0AAEAAQC1/5EBtAABAAEAuv+SAbQAAQABAL//kwG0AAEAAQDD/1MBtQABAAEA8P9UAbUAAQABAOz/VQG1AAEAAQDo/1YBtQABAAEA5P9XAbUAAQABAOD/WAG1AAEAAQDc/1kBtQABAAEA2P9aAbUAAQABANT/hAG1AAEAAQCB/4UBtQABAAEAhf+GAbUAAQABAIj/hwG1AAEAAQCM/4gBtQABAAEAj/+JAbUAAQABAJP/igG1AAEAAQCX/4sBtQABAAEAm/+MAbUAAQABAJ7/jQG1AAEAAQCj/44BtQABAAEAp/+PAbUAAQABAKz/kAG1AAEAAQCw/5EBtQABAAEAtf9TAbYAAQABAO3/VAG2AAEAAQDp/1UBtgABAAEA5f9WAbYAAQABAOD/VwG2AAEAAQDc/1gBtgABAAEA2P9ZAbYAAQABANP/WgG2AAIAAQDP/4MBtgABAAEAef+EAbYAAQABAH3/hQG2AAEAAQCB/4YBtgABAAEAhf+HAbYAAQABAIj/iAG2AAEAAQCM/4kBtgABAAEAkP+KAbYAAQABAJP/iwG2AAEAAQCX/4wBtgABAAEAm/+NAbYAAQABAJ//jgG2AAEAAQCj/48BtgABAAEAqP+QAbYAAQABAK3/sQC3AAEAGAD//1MBtwABAAEA6v9UAbcAAQABAOb/VQG3AAEAAQDh/1YBtwABAAEA3f9XAbcAAQABANj/WAG3AAEAAQDU/1kBtwABAAEAz/9aAbcAAQABAMv/WwG3AAEAAQDI/4IBtwABAAEAcv+DAbcAAQABAHX/hAG3AAEAAQB5/4UBtwABAAEAff+GAbcAAQABAIH/hwG3AAEAAQCF/4gBtwABAAEAif+JAbcAAQABAI3/igG3AAEAAQCQ/4sBtwABAAEAlP+MAbcAAQABAJj/jQG3AAEAAQCc/44BtwABAAEAoP+PAbcAAQABAKT/VAG4AAEAAQDj/1UBuAABAAEA3v9WAbgAAQABANn/VwG4AAEAAQDV/1gBuAABAAEA0P9ZAbgAAQABAMv/WgG4AAEAAQDG/1sBuAABAAEAwv+BAbgAAQABAGr/ggG4AAEAAQBu/4MBuAABAAEAcv+EAbgAAQABAHb/hQG4AAEAAQB6/4YBuAABAAEAff+HAbgAAQABAIH/iAG4AAEAAQCG/4kBuAABAAEAif+KAbgAAQABAI3/iwG4AAEAAQCR/4wBuAABAAEAlf+NAbgAAQABAJn/jgG4AAEAAQCc/1EAuQABAAQAhwBUALkAAQAEAGgAVwC5AAEAAwBIALIAuQABABkA//9UAbkAAQACAN//VQG5AAEAAQDb/1YBuQABAAEA1v9XAbkAAQABANH/WAG5AAEAAQDM/1kBuQABAAEAx/9aAbkAAQABAML/WwG5AAEAAQC9/1wBuQABAAEAu/+AAbkAAQABAGP/gQG5AAEAAQBn/4IBuQABAAEAav+DAbkAAQABAG7/hAG5AAEAAQBy/4UBuQABAAEAdv+GAbkAAQABAHr/hwG5AAEAAQB+/4gBuQABAAEAgv+JAbkAAQABAIb/igG5AAEAAQCK/4sBuQABAAEAjv+MAbkAAQABAJL/+gG5AAEABAAAAAECuQABAAMASAAEArkAAQAEAGgAUgC6AAEABAB9AFMAugABAAQAcwBVALoAAQAEAF4AVgC6AAEABABUAFgAugABAAMAPwBZALoAAQADADQAWwC6AAEABAAgAFUBugABAAEA2P9WAboAAQABANP/VwG6AAEAAQDO/1gBugABAAEAyP9ZAboAAQABAMP/WgG6AAEAAQC+/1sBugABAAEAuf9cAboAAQABALT/fwG6AAEAAQBd/4ABugABAAEAYP+BAboAAQABAGP/ggG6AAEAAQBn/4MBugABAAEAa/+EAboAAQABAG7/hQG6AAEAAQBy/4YBugABAAEAd/+HAboAAQABAHv/iAG6AAEAAQB//4kBugABAAEAg/+KAboAAQABAIf/iwG6AAEAAQCL//0BugABAAQAIAD/AboAAQADADQAAAK6AAEAAwA/AAICugABAAQAVAADAroAAQAEAF4ABQK6AAEAAwByAFoAuwABAAIAKgBcALsAAQADABYAXQC7AAEAAwAMALMAuwABABoA//9VAbsAAQABANb/VgG7AAEAAQDQ/1cBuwABAAEAy/9YAbsAAQABAMX/WQG7AAEAAQC//1oBuwABAAEAuv9bAbsAAQABALX/XAG7AAEAAQCv/10BuwABAAEArf9+AbsAAQABAFf/fwG7AAEAAQBa/4ABuwABAAEAXf+BAbsAAQABAGD/ggG7AAEAAQBk/4MBuwABAAEAZ/+EAbsAAQABAGv/hQG7AAEAAQBv/4YBuwABAAEAc/+HAbsAAQABAHf/iAG7AAEAAQB8/4kBuwABAAEAgP+KAbsAAQABAIT/+wG7AAEAAwAMAPwBuwABAAMAFgD+AbsAAQACACoAVwC8AAEAAwBLAF4AvAABAAMAAgBVAbwAAQABANP/VgG8AAEAAQDO/1cBvAABAAEAyP9YAbwAAQABAML/WQG8AAEAAQC8/1oBvAABAAEAt/9bAbwAAQABALH/XAG8AAEAAQCr/10BvAACAAEApf99AbwAAgABAFP/fwG8AAEAAQBX/4ABvAABAAEAWv+BAbwAAQACAFz/ggG8AAEAAgBf/4MBvAABAAEAZP+EAbwAAQABAGj/hQG8AAEAAQBs/4YBvAABAAEAcP+HAbwAAQABAHT/iAG8AAEAAQB4/wECvAABAAMASwA9AL0AAQAjAJIAUQC9AAEAAgCKAFQAvQABAAIAagBYAL0AAQADAEEAWQC9AAEAAwA3AFoAvQABAAMALQC0AL0AAQAaAP//VQG9AAEAAwDQ/1YBvQABAAEAy/9XAb0AAQACAMT/WAG9AAEAAQC//1kBvQABAAEAuf9aAb0AAQABALT/WwG9AAEAAQCu/1wBvQABAAEAqP9dAb0AAQABAKL/XgG9AAIAAQCc/3oBvQADAAEATv99Ab0AAQACAE//fgG9AAEAAQBS/38BvQABAAIAVP+AAb0AAQABAFf/gwG9AAEAAQBh/4QBvQABAAEAZf+FAb0AAQABAGn/hgG9AAEAAQBt/4cBvQABAAEAcf/6Ab0AAQACAAIA/gG9AAEAAwAtAP8BvQABAAMANwAAAr0AAQADAEEABAK9AAEAAgBqAFIAvgABAAIAgABTAL4AAQACAHYAVQC+AAEAAgBgAFYAvgABAAIAVgBbAL4AAQABACIAXAC+AAEAAgAYAF0AvgABAAIADgBWAb4AAQABAMn/WAG+AAEAAQC9/1kBvgABAAEAt/9aAb4AAQACALD/WwG+AAEAAgCq/1wBvgABAAEApf9dAb4AAQABAJ//XgG+AAEAAgCY/18BvgABAAEAlf95Ab4AAQABAE3/egG+AAMAAQBL/34BvgABAAIAT/+AAb4AAQACAFT/gQG+AAEAAgBX/4IBvgABAAIAWv+DAb4AAQABAF//hAG+AAEAAQBi/4UBvgABAAEAZv+GAb4AAQABAGn/+wG+AAEAAgAOAPwBvgABAAIAGAD9Ab4AAQABACIAAgK+AAEAAgBWAAMCvgABAAIAYABRAL8AAQADAIwAVAC/AAEAAwBtAFcAvwABAAIATgBbAL8AAQADACUAXgC/AAEAAwAGALUAvwABABsA//9WAb8AAQACAMb/VwG/AAEAAgC//1gBvwABAAEAuv9ZAb8AAQABALX/XAG/AAEAAgCh/10BvwABAAIAm/9fAb8AAQABAJH/YAG/AAIAAQCM/3gBvwABAAEAS/95Ab8AAwABAEn/fAG/AAIAAgBL/38BvwABAAIAUP+DAb8AAQABAFz/hAG/AAEAAQBf/4UBvwABAAEAYv/6Ab8AAQADAAYA/QG/AAEAAwAlAAECvwABAAIATgAEAr8AAQADAG0AUgDAAAEAAwCDAFMAwAABAAIAeABVAMAAAQADAGQAVgDAAAEAAgBZAFgAwAABAAIARABZAMAAAQABADkAWgDAAAEAAgAwAFwAwAABAAIAGwBdAMAAAQACABEAVQHAAAEAAQDN/1gBwAABAAEAuP9ZAcAAAQABALL/WgHAAAEAAgCr/1sBwAABAAIApf9eAcAAAQACAJP/XwHAAAEAAgCO/2ABwAABAAIAiP9hAcAAAgABAIT/dwHAAAEAAQBK/3gBwAAEAAcAR/9+AcAAAQACAEz/gAHAAAEAAgBQ/4EBwAACAAEAVf+DAcAAAQABAFn/+wHAAAEAAgARAPwBwAABAAIAGwD+AcAAAQACADAA/wHAAAEAAQA5AAACwAABAAIARAACAsAAAQACAFkAAwLAAAEAAwBkAFcAwQABAAIAUABZAMEAAQACADwAYgDBAAEADwD//1UBwQABAAIAyf9WAcEAAQABAMX/VwHBAAEAAQC9/1gBwQABAAEAtv9ZAcEAAQABALD/XAHBAAEAAgCd/10BwQABAAIAl/9hAcEAAQADAIL/YgHBAAIAAQB+/2QBwQABAAEAfP91AcEAAQABAEz/dgHBAAIABABI/3wBwQACAAIASf9/AcEAAQACAEz/gQHBAAEAAgBQ/4IBwQABAAEAVP//AcEAAQACADwAAQLBAAEAAgBQAFEAwgABAAEAjgBTAMIAAQACAHoAVADCAAEAAgBwAFYAwgABAAEAWwBYAMIAAQABAEYAWgDCAAEAAgAyAFsAwgABAAIAKABcAMIAAQABAB0AXQDCAAEAAQATAF4AwgABAAIACgC2AMIAAQAaAP//VgHCAAEAAQDB/1cBwgABAAEAuv9YAcIAAQABALP/WQHCAAEAAgCs/1oBwgABAAEAqP9bAcIAAQACAKH/XgHCAAEAAQCR/18BwgABAAIAiv9gAcIAAQACAIb/YgHCAAEABAB8/2MBwgABAAgAef9kAcIAAwABAHb/cwHCAAEAAQBP/3QBwgABAAMAS/91AcIAAQADAEn/fgHCAAEAAwBJ/4ABwgABAAEATv/5AcIAAQACAAAA+gHCAAEAAgAKAPsBwgABAAEAEwD8AcIAAQABAB0A/QHCAAEAAgAoAP4BwgABAAIAMgAAAsIAAQABAEYAAgLCAAEAAQBbAAQCwgABAAIAcABRAMMAAQACAJAAUgDDAAEAAgCGAFUAwwABAAIAaABWAMMAAQACAF4AVwDDAAEAAQBSAFgAwwABAAEASABZAMMAAQACAD4AXADDAAEAAgAgAF0AwwABAAIAFgBUAcMAAQABAMr/VQHDAAEAAQDD/1YBwwABAAEAvP9XAcMAAQABALf/WAHDAAEAAQCx/1oBwwABAAEApf9cAcMAAQABAJr/XQHDAAEAAgCT/14BwwABAAIAjv9kAcMAAgACAHX/ZgHDAAEABQBz/2cBwwACAAEAcP9pAcMAAQABAG3/agHDAAEAAQBr/2sBwwABAAEAaP9uAcMAAQABAF//bwHDAAEAAQBb/3ABwwABAAEAV/9xAcMAAQADAFT/cgHDAAEAAgBQ/3MBwwABAAIATf98AcMAAgADAEf/fwHDAAEAAQBK//sBwwABAAIAFgD8AcMAAQACACAA/wHDAAEAAgA+AAACwwABAAEASAABAsMAAQABAFIAAgLDAAEAAgBeAAMCwwABAAIAaABTAMQAAQACAH4AVADEAAEAAgB0AFcAxAABAAIAVQBYAMQAAQACAEoAWgDEAAEAAQA1AFsAxAABAAEAKwBeAMQAAQACAA0AXwDEAAEAAgADALcAxAABABsA//9UAcQAAQABAMb/VQHEAAEAAgC//1YBxAABAAEAuv9XAcQAAQABALT/WAHEAAEAAQCu/1kBxAABAAEAqf9aAcQAAQACAKL/WwHEAAEAAgCd/1wBxAABAAIAl/9fAcQAAQACAIj/YAHEAAEAAwCD/2EBxAABAAYAf/9nAcQAAQADAHD/aAHEAAEAAwBu/2kBxAABAAMAa/9qAcQAAQACAGj/awHEAAEAAgBk/2wBxAABAAEAYf9tAcQAAQACAF//bgHEAAEAAgBc/28BxAABAAIAWf9wAcQAAQABAFX/+QHEAAEAAgADAPoBxAABAAIADQD9AcQAAQABACsA/gHEAAEAAQA1AAACxAABAAIASgABAsQAAQACAFUABALEAAEAAQBzAFIAxQABAAIAigBVAMUAAQABAGoAVgDFAAEAAQBgAFkAxQABAAEAQQBaAMUAAQABADcAWwDFAAEAAQAtAFwAxQABAAEAIgBdAMUAAQABABgAUgHFAAEAAgDQ/1MBxQABAAIAyf9UAcUAAQABAMT/VgHFAAEAAQC4/1cBxQABAAEAsv9YAcUAAQACAKv/WQHFAAEAAgCl/10BxQABAAEAkf9eAcUAAQACAIz/ZAHFAAEABgB4/2UBxQABAAQAdv9sAcUAAQACAGT/cAHFAAEAAgBY/3IBxQABAAIAU/9zAcUAAgABAE//dQHFAAIAAQBL/3cBxQABAAMASv/7AcUAAQABABgA/AHFAAEAAQAiAP0BxQABAAEALQD+AcUAAQABADcA/wHFAAEAAQBBAAICxQABAAEAYAADAsUAAQABAGoAUwDGAAEAAgCBAFQAxgABAAIAdwBVAMYAAQACAG0AVgDGAAEAAgBjAFcAxgABAAEAVwBYAMYAAQABAE0AWQDGAAEAAQBDAFoAxgABAAEAOQBbAMYAAQABAC8AXADGAAEAAgAlAF0AxgABAAIAGwBeAMYAAQACABEAXwDGAAEAAQAGALgAxgABABsA//9RAcYAAQACANT/VAHGAAEAAgDC/1UBxgABAAIAvP9WAcYAAQACALX/VwHGAAEAAgCv/1oBxgABAAEAn/9bAcYAAQABAJr/XAHGAAEABACT/10BxgABAAQAjv9fAcYAAQACAIb/YgHGAAEABAB7/2oBxgABAAIAav9rAcYAAQACAGj/bQHGAAEAAQBh/24BxgACAAEAXf9xAcYAAQACAFf/cwHGAAIAAQBQ/3UBxgABAAIATv92AcYAAQABAEv/fAHGAAEAAQBH//kBxgABAAEABgD6AcYAAQACABEA+wHGAAEAAgAbAPwBxgABAAIAJQD9AcYAAQABAC8A/gHGAAEAAQA5AP8BxgABAAEAQwAAAsYAAQABAE0AAQLGAAEAAQBXAAICxgABAAIAYwADAsYAAQACAG0AUgDHAAEAAgCNAFcAxwABAAIAWgBYAMcAAQACAFAAWQDHAAEAAgBGAFoAxwABAAIAPABbAMcAAQACADIAXwDHAAEAAgAJAGMAxwABAAwA//9QAccAAQAHANr/UgHHAAEAAgDO/1MBxwABAAYAxv9YAccAAQAEAKf/WQHHAAEAAwCi/1oBxwABAAMAnP9bAccAAQADAJf/XgHHAAEAAwCJ/2ABxwABAAMAgf9nAccAAQAEAHP/aAHHAAEAAgBw/2kBxwABAAIAbv9sAccAAQACAGb/bQHHAAEAAgBk/24BxwABAAIAYf9vAccAAgABAFz/cgHHAAIAAQBU/3QBxwABAAIAUv92AccAAQACAE7/eAHHAAMAAQBJ//kBxwABAAIACQD9AccAAQACADIA/gHHAAEAAgA8AP8BxwABAAIARgAAAscAAQACAFAAAQLHAAEAAgBaAFMAyAABAAEAhABUAMgAAQABAHoAVQDIAAEAAQBwAFYAyAABAAEAZQBcAMgAAQABACkAXQDIAAEAAQAeAF4AyAABAAEAFAC5AMgAAQAcAP//TwHIAAEABgDf/1EByAABAAUA0/9UAcgAAQAFAMD/VQHIAAEABQC6/1YByAABAAQAs/9XAcgAAQADAK3/XwHIAAEAAQCE/2YByAABAAMAdf9qAcgAAgABAGv/bwHIAAEAAgBg/3AByAABAAIAXf9xAcgAAQACAFr/cgHIAAEAAgBY/3MByAABAAEAVf91AcgAAQABAFD/dwHIAAEAAgBO/3gByAACAAEASv/4AcgAAQACAAEA+gHIAAEAAQAUAPsByAABAAEAHgD8AcgAAQABACkAAgLIAAEAAQBlAAMCyAABAAEAcABSAMkAAQABAJAAUwDJAAEAAgCHAFQAyQABAAIAfQBVAMkAAQACAHMAVgDJAAEAAgBpAFcAyQABAAEAXQBYAMkAAQACAFQAWQDJAAEAAQBJAFoAyQABAAEAPwBbAMkAAQABADUAXADJAAEAAQArAF0AyQABAAEAIQBeAMkAAQABABYAXwDJAAEAAQAMAGAAyQABAAEAAgBOAckAAQAFAOX/UgHJAAEAAwDM/18ByQABAAEAhv9lAckAAQACAHj/aAHJAAEAAwBy/2kByQABAAMAcP9qAckAAQADAG7/awHJAAEAAgBs/2wByQABAAIAaP9tAckAAQACAGb/bgHJAAEAAgBk/3MByQABAAIAWP90AckAAgABAFP/dgHJAAEAAQBQ//kByQABAAEADAD6AckAAQABABYA+wHJAAEAAQAhAPwByQABAAEAKwD9AckAAQABADUA/gHJAAEAAQA/AP8ByQABAAEASQAAAskAAQACAFQAAQLJAAEAAQBdAAICyQABAAIAaQADAskAAQABAHIAVwDKAAEAAQBgAFkAygABAAEASwBaAMoAAQABAEEAWwDKAAEAAQA3AFwAygABAAEALQBdAMoAAQABACMAXgDKAAEAAQAZAF8AygABAAIAEABgAMoAAQACAAYATQHKAAEABADq/1kBygABAAEApP9aAcoAAQABAKH/YQHKAAEAAQCE/2IBygABAAEAgf9jAcoAAQABAH3/bwHKAAIAAQBh/3EBygACAAEAXP90AcoAAQABAFb/+AHKAAEAAgAGAPkBygABAAIAEAD6AcoAAQABABkA+wHKAAEAAQAjAPwBygABAAEALQD9AcoAAQABADcA/gHKAAEAAQBBAP8BygABAAEASwABAsoAAQABAGAAUwDLAAEAAQCKAFQAywABAAEAgABVAMsAAQABAHYAVgDLAAEAAgBtAFcAywABAAIAYwBYAMsAAQACAFkAWQDLAAEAAQBOAFoAywABAAEARABbAMsAAQABADoAXADLAAEAAQAvAF0AywABAAEAJQBeAMsAAQABABsAZADLAAEACwD//7oAywABABsA//9XAcsAAQABALD/ZQHLAAEAAQB9/2YBywABAAEAd/9rAcsAAQABAG3/bAHLAAEAAQBr/20BywABAAEAaP9uAcsAAgABAGX/cAHLAAEAAQBh//oBywABAAEAGwD7AcsAAQABACUA/AHLAAEAAQAvAP0BywABAAEAOgD+AcsAAQABAEQA/wHLAAEAAQBOAAACywABAAIAWQABAssAAQACAGMAAgLLAAEAAgBtAD4AzAABABcAkgBTAMwAAQABAI0AVADMAAEAAQCDAFUAzAABAAEAeQBZAMwAAQABAFAAWgDMAAEAAQBGAFsAzAABAAEAPABcAMwAAQABADIAXQDMAAEAAQAoAF4AzAABAAEAHgBfAMwAAQABABQAYADMAAEAAgALAEwBzAABAAQA8P9SAcwAAQACAM7/VgHMAAEAAQC5//cBzAABAAIAAQD4AcwAAQACAAsA+QHMAAEAAQAUAPoBzAABAAEAHgD7AcwAAQABACgA/AHMAAEAAQAyAP0BzAABAAEAPAD+AcwAAQABAEYA/wHMAAEAAQBQAFMAzQABAAEAjwBUAM0AAQABAIUAVQDNAAEAAQB7AFYAzQABAAEAcQBXAM0AAQACAGgAWADNAAEAAgBeAFkAzQABAAIAVABaAM0AAQABAEkAWwDNAAEAAQA/AFwAzQABAAEANQBdAM0AAQABACsAXgDNAAEAAQAhAF8AzQABAAEAFgBhAM0AAQABAAIAuwDNAAEAHAD//1EBzQABAAIA1v9TAc0AAQACAMr/VAHNAAEAAQDD/1UBzQABAAEAwf/5Ac0AAQABABYA+gHNAAEAAQAhAPsBzQABAAEAKwD8Ac0AAQABADUA/QHNAAEAAQA/AP4BzQABAAEASQD/Ac0AAQACAFQAAALNAAEAAgBeAAECzQABAAIAaAACAs0AAQABAHEAVADOAAEAAQCIAFUAzgABAAEAfgBWAM4AAQABAHQAWgDOAAEAAQBLAFsAzgABAAEAQQBcAM4AAQABADcAXQDOAAEAAQAtAF4AzgABAAEAIwBfAM4AAQABABkAYADOAAEAAQAPAGEAzgABAAEABQBlAM4AAQALAP//SwHOAAEAAwD1/00BzgABAAMA7f9OAc4AAQACAOj/TwHOAAEAAgDi/1ABzgABAAEA3P9SAc4AAQABAND/VAHOAAEAAQDJ//cBzgABAAEABQD4Ac4AAQABAA8A+QHOAAEAAQAZAPoBzgABAAEAIwD7Ac4AAQABAC0A/AHOAAEAAQA3AP0BzgABAAEAQQD+Ac4AAQABAEsAVADPAAEAAQCKAFUAzwABAAEAgABWAM8AAQABAHYAVwDPAAEAAQBsAFgAzwABAAEAYgBZAM8AAQABAFgAWgDPAAEAAQBOAFsAzwABAAEARABcAM8AAQABADoAXQDPAAEAAQAwAF4AzwABAAEAJgBfAM8AAQABABwAYADPAAEAAQASAGEAzwABAAEACAC8AM8AAQAdAP//UAHPAAEAAgDe/1EBzwABAAEA2P9SAc8AAQACANP/UwHPAAEAAQDP//cBzwABAAEACAD4Ac8AAQABABIA+QHPAAEAAQAcAPoBzwABAAEAJgD7Ac8AAQABADAA/AHPAAEAAQA6AP0BzwABAAEARAD+Ac8AAQABAE4A/wHPAAEAAQBYAAACzwABAAEAYgABAs8AAQABAGwAVADQAAEAAQCNAFUA0AABAAEAgwBWANAAAQABAHkAVwDQAAEAAQBvAFgA0AABAAEAZQBZANAAAQABAFsAWgDQAAEAAQBRAFsA0AABAAEARwBcANAAAQABAD0AXQDQAAEAAQAzAF4A0AABAAEAKQBfANAAAQABAB8AYADQAAEAAQAVAGEA0AABAAEACwBiANAAAQABAAEATAHQAAEAAgDy/04B0AABAAIA6v9PAdAAAQACAOX/UQHQAAEAAgDb//YB0AABAAEAAQD3AdAAAQABAAsA+AHQAAEAAQAVAPkB0AABAAEAHwD6AdAAAQABACkA+wHQAAEAAQAzAPwB0AABAAEAPQD9AdAAAQABAEcA/gHQAAEAAQBRAP8B0AABAAEAWwAAAtAAAQABAGUAAQLQAAEAAQBvAFQA0QABAAEAkABVANEAAQABAIYAVgDRAAEAAQB8AFcA0QABAAEAcgBYANEAAQABAGgAWQDRAAEAAQBeAFoA0QABAAEAVABbANEAAQABAEoAXADRAAEAAQBAAF0A0QABAAEANgBeANEAAQABACwAXwDRAAEAAQAiAGAA0QABAAEAGABhANEAAQABAA8AYgDRAAEAAQAFAGYA0QABAAoA//+9ANEAAQAdAP//SgHRAAEABAD7/0sB0QABAAIA+P9NAdEAAQACAPD/UAHRAAEAAQDh/1IB0QABAAEA2f/2AdEAAQABAAUA9wHRAAEAAQAPAPgB0QABAAEAGAD5AdEAAQABACIA+gHRAAEAAQAsAPsB0QABAAEANgD8AdEAAQABAEAA/QHRAAEAAQBKAP4B0QABAAEAVAD/AdEAAQABAF4AAALRAAEAAQBoAAEC0QABAAEAcgA/ANIAAQATAJIAVQDSAAEAAQCJAFYA0gABAAEAfwBXANIAAQABAHUAWADSAAEAAQBrAFkA0gABAAEAYQBaANIAAQABAFcAWwDSAAEAAQBNAFwA0gABAAEAQwBdANIAAQABADkAXgDSAAEAAQAvAF8A0gABAAEAJQBgANIAAQABABwAYQDSAAEAAQASAGIA0gABAAEACABMAdIAAQACAPb/TgHSAAEAAgDu/08B0gABAAEA6P9QAdIAAQACAOX/UQHSAAIAAQDe//YB0gABAAEACAD3AdIAAQABABIA+AHSAAEAAQAcAPkB0gABAAEAJQD6AdIAAQABAC8A+wHSAAEAAQA5APwB0gABAAEAQwD9AdIAAQABAE0A/gHSAAEAAQBXAP8B0gABAAEAYQAAAtIAAQABAGsAVQDTAAEAAQCMAFYA0wABAAEAggBXANMAAQABAHgAWADTAAEAAQBuAFkA0wABAAEAZABaANMAAQABAFoAWwDTAAEAAQBQAFwA0wABAAEARgBdANMAAQABADwAXgDTAAEAAQAzAF8A0wABAAEAKQBgANMAAQABAB8AYQDTAAEAAQAVAGIA0wABAAEACwBjANMAAQABAAIASwHTAAEAAwD6/00B0wABAAIA9P9PAdMAAQABAOv/UQHTAAEAAQDh//UB0wABAAEAAgD2AdMAAQABAAsA9wHTAAEAAQAVAPgB0wABAAEAHwD5AdMAAQABACkA+gHTAAEAAQAzAPsB0wABAAEAPAD8AdMAAQABAEYA/QHTAAEAAQBQAP4B0wABAAEAWgD/AdMAAQABAGQAAALTAAEAAQBuAFUA1AABAAEAjwBWANQAAQABAIUAVwDUAAEAAQB7AFgA1AABAAEAcQBZANQAAQABAGcAWgDUAAEAAQBdAFsA1AABAAEAUwBcANQAAQABAEoAXQDUAAEAAQBAAF4A1AABAAEANgBfANQAAQABACwAYADUAAEAAQAiAGEA1AABAAEAGQBiANQAAQABAA8AYwDUAAEAAQAFAGcA1AABAAkA//++ANQAAQAdAP//TAHUAAEAAQD3/04B1AABAAIA8v9PAdQAAQACAO7/UAHUAAEAAQDo/1EB1AABAAEA5f/1AdQAAQABAAUA9gHUAAEAAQAPAPcB1AABAAEAGQD4AdQAAQABACIA+QHUAAEAAQAsAPoB1AABAAEANgD7AdQAAQABAEAA/AHUAAEAAQBKAP0B1AABAAEAUwD+AdQAAQABAF0A/wHUAAEAAQBnAAAC1AABAAEAcQBWANUAAQABAIgAVwDVAAEAAQB+AFgA1QABAAEAdABZANUAAQABAGoAWgDVAAEAAQBhAFsA1QABAAEAVwBcANUAAQABAE0AXQDVAAEAAQBDAF4A1QABAAEAOQBfANUAAQABADAAYADVAAEAAQAmAGEA1QABAAEAHABiANUAAQABABIAYwDVAAEAAQAJAEoB1QABAAYA/v9MAdUAAQADAPr/TQHVAAEAAgD3/1AB1QABAAEA6/9RAdUAAQABAOj/9AHVAAEAAQD///UB1QABAAEACQD2AdUAAQABABIA9wHVAAEAAQAcAPgB1QABAAEAJgD5AdUAAQABADAA+gHVAAEAAQA5APsB1QABAAEAQwD8AdUAAQABAE0A/QHVAAEAAQBXAP4B1QABAAEAYQD/AdUAAQABAGoAVgDWAAEAAQCLAFcA1gABAAEAggBYANYAAQABAHgAWQDWAAEAAQBuAFoA1gABAAEAZABbANYAAQABAFoAXADWAAEAAQBQAF0A1gABAAEARwBeANYAAQABAD0AXwDWAAEAAQAzAGAA1gABAAEAKgBhANYAAQABACAAYgDWAAEAAQAWAGMA1gABAAEADABkANYAAQABAAMAvwDWAAEAHgD//0sB1gABAAYA/v9OAdYAAQACAPb/TwHWAAEAAgDy/1AB1gABAAIA7/9RAdYAAQABAOv/9AHWAAEAAQADAPUB1gABAAEADAD2AdYAAQABABYA9wHWAAEAAQAgAPgB1gABAAEAKgD5AdYAAQABADMA+gHWAAEAAQA9APsB1gABAAEARwD8AdYAAQABAFAA/QHWAAEAAQBaAP4B1gABAAEAZAD/AdYAAQABAG4AQADXAAEAEQCSAFYA1wABAAEAjwBXANcAAQABAIUAWADXAAEAAQB7AFkA1wABAAEAcQBaANcAAQABAGgAWwDXAAEAAQBeAFwA1wABAAEAVABdANcAAQABAEoAXgDXAAEAAQBBAF8A1wABAAEANwBgANcAAQABAC0AYQDXAAEAAQAkAGIA1wABAAEAGgBjANcAAQABABAAZADXAAEAAQAHAGgA1wABAAgA//9NAdcAAQACAPr/UQHXAAEAAgDu//QB1wABAAEABwD1AdcAAQABABAA9gHXAAEAAQAaAPcB1wABAAEAJAD4AdcAAQABAC0A+QHXAAEAAQA3APoB1wABAAEAQQD7AdcAAQABAEoA/AHXAAEAAQBUAP0B1wABAAEAXgD+AdcAAQABAGgA/wHXAAEAAQBxAFcA2AABAAEAiABYANgAAQABAH8AWQDYAAEAAQB1AFoA2AABAAEAawBbANgAAQABAGEAXADYAAEAAQBYAF0A2AABAAEATgBeANgAAQABAEQAXwDYAAEAAQA7AGAA2AABAAEAMQBhANgAAQABACcA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YgDjAAEAAQBOAGMA4wABAAEARQBkAOMAAQABADwAZQDjAAEAAQAzAGYA4wABAAEAKgBnAOMAAQABACAAaADjAAEAAQAXAGkA4wABAAEADgBqAOMAAQABAAUAbgDjAAEABgD//8YA4wABACUA//8mAeMAAwALAP//VwHjAAIADAD//1sB4wABAAMA/f/uAeMAAQABAAUA7wHjAAEAAQAOAPAB4wABAAEAFwDxAeMAAQABACAA8gHjAAEAAQAqAPMB4wABAAEAMwD0AeMAAQABADwA9QHjAAEAAQBFAPYB4wABAAEATgD3AeMAAQABAFcA+AHjAAEAAQBgAPkB4wABAAEAagD6AeMAAQABAHMAXADkAAEAAQCKAF0A5AABAAEAgQBeAOQAAQABAHgAXwDkAAEAAQBuAGAA5AABAAEAZQBhAOQAAQABAFwAYgDkAAEAAQBTAGMA5AABAAEASgBkAOQAAQABAEEAZQDkAAEAAQA4AGYA5AABAAEALwBnAOQAAQABACYAaADkAAEAAQAdAGkA5AABAAEAFABqAOQAAQABAAsAawDkAAEAAQACAG8A5AABAAcA//8jAeQAAwALAP//WQHkAAIACwD//1wB5AACAA0A///tAeQAAQABAAIA7gHkAAEAAQALAO8B5AABAAEAFADwAeQAAQABAB0A8QHkAAEAAQAmAPIB5AABAAEALwDzAeQAAQABADgA9AHkAAEAAQBBAPUB5AABAAEASgD2AeQAAQABAFMA9wHkAAEAAQBcAPgB5AABAAEAZQD5AeQAAQABAG4AXADlAAEAAQCPAF0A5QABAAEAhgBeAOUAAQABAHwAXwDlAAEAAQBzAGAA5QABAAEAagBhAOUAAQABAGEAYgDlAAEAAQBYAGMA5QABAAEATwBkAOUAAQABAEYAZQDlAAEAAQA9AGYA5QABAAEANABnAOUAAQABACsAaADlAAEAAQAiAGkA5QABAAEAGQBqAOUAAQABABAAawDlAAEAAQAHAMcA5QABADwA//8gAeUAAwALAP//XgHlAAEADQD//+0B5QABAAEABwDuAeUAAQABABAA7wHlAAEAAQAZAPAB5QABAAEAIgDxAeUAAQABACsA8gHlAAEAAQA0APMB5QABAAEAPQD0AeUAAQABAEYA9QHlAAEAAQBPAPYB5QABAAEAWAD3AeUAAQABAGEA+AHlAAEAAQBqAEUA5gABAAsAkgBdAOYAAQABAIoAXgDmAAEAAQCBAF8A5gABAAEAeABgAOYAAQABAG8AYQDmAAEAAQBmAGIA5gABAAEAXQBjAOYAAQABAFQAZADmAAEAAQBLAGUA5gABAAEAQgBmAOYAAQABADkAZwDmAAEAAQAwAGgA5gABAAEAJwBpAOYAAQABAB4AagDmAAEAAQAVAGsA5gABAAEADQBsAOYAAQABAAQAcADmAAEABgD//8gA5gABAAEAAgAdAeYAAwALAP//WwHmAAEACgD//18B5gACAA0A///sAeYAAQABAAQA7QHmAAEAAQANAO4B5gABAAEAFQDvAeYAAQABAB4A8AHmAAEAAQAnAPEB5gABAAEAMADyAeYAAQABADkA8wHmAAEAAQBCAPQB5gABAAEASwD1AeYAAQABAFQA9gHmAAEAAQBdAPcB5gABAAEAZgD4AeYAAQABAG8AXQDnAAEAAQCPAF4A5wABAAEAhgBfAOcAAQABAH0AYADnAAEAAQB0AGEA5wABAAEAawBiAOcAAQABAGIAYwDnAAEAAQBZAGQA5wABAAEAUABlAOcAAQABAEcAZgDnAAEAAQA+AGcA5wABAAEANQBoAOcAAQABAC0AaQDnAAEAAQAkAGoA5wABAAEAGwBrAOcAAQABABIAbADnAAEAAQAJAHEA5wABAAYA///IAOcAAQA7AP//GgHnAAMACwD//2EB5wACAA0A///rAecAAQABAAEA7AHnAAEAAQAJAO0B5wABAAEAEgDuAecAAQABABsA7wHnAAEAAQAkAPAB5wABAAEALQDxAecAAQABADUA8gHnAAEAAQA+APMB5wABAAEARwD0AecAAQABAFAA9QHnAAEAAQBZAPYB5wABAAEAYgD3AecAAQABAGsARgDoAAEACwCSAF4A6AABAAEAiwBfAOgAAQABAIIAYADoAAEAAQB5AGEA6AABAAEAcABiAOgAAQABAGcAYwDoAAEAAQBeAGQA6AABAAEAVQBlAOgAAQABAE0AZgDoAAEAAQBEAGcA6AABAAEAOwBoAOgAAQABADIAaQDoAAEAAQApAGoA6AABAAEAIABrAOgAAQABABgAbADoAAEAAQAPAG0A6AABAAEABgDJAOgAAQA6AP//FwHoAAMACwD//zwB6AACAAEAAQBIAegABAABAAAAYwHoAAEADQD//+sB6AABAAEABgDsAegAAQABAA8A7QHoAAEAAQAYAO4B6AABAAEAIADvAegAAQABACkA8AHoAAEAAQAyAPEB6AABAAEAOwDyAegAAQABAEQA8wHoAAEAAQBNAPQB6AABAAEAVQD1AegAAQABAF4A9gHoAAEAAQBnAPcB6AABAAEAcABeAOkAAQABAJAAXwDpAAEAAQCHAGAA6QABAAEAfwBhAOkAAQABAHYAYgDpAAEAAQBtAGMA6QABAAEAZABkAOkAAQABAFsAZQDpAAEAAQBSAGYA6QABAAEASQBnAOkAAQABAEAAaADpAAEAAQA3AGkA6QABAAEALwBqAOkAAQABACYAawDpAAEAAQAdAGwA6QABAAEAFQBtAOkAAQABAAwAbgDpAAEAAQAEAHIA6QABAAYA///KAOkAAQA5AP//FAHpAAMADAD//zgB6QACAAEAAABNAekAAgABAAAAZAHpAAIADQD//+oB6QABAAEABADrAekAAQABAAwA7AHpAAEAAQAVAO0B6QABAAEAHQDuAekAAQABACYA7wHpAAEAAQAvAPAB6QABAAEANwDxAekAAQABAEAA8gHpAAEAAQBJAPMB6QABAAEAUgD0AekAAQABAFsA9QHpAAEAAQBkAPYB6QABAAEAbQBHAOoAAQAKAJIAXwDqAAEAAQCNAGAA6gABAAEAhABhAOoAAQABAHsAYgDqAAEAAQByAGMA6gABAAEAaQBkAOoAAQABAGAAZQDqAAEAAQBXAGYA6gABAAEATwBnAOoAAQABAEYAaADqAAEAAQA9AGkA6gABAAEANABqAOoAAQABACwAawDqAAEAAQAjAGwA6gABAAEAGwBtAOoAAQABABIAbgDqAAEAAQAJAHMA6gABAAYA///LAOoAAQA4AP//EQHqAAMADAD//zQB6gADAAEAAABPAeoAAgABAAAAZgHqAAIADQD//+kB6gABAAEAAQDqAeoAAQABAAkA6wHqAAEAAQASAOwB6gABAAEAGwDtAeoAAQABACMA7gHqAAEAAQAsAO8B6gABAAEANADwAeoAAQABAD0A8QHqAAEAAQBGAPIB6gABAAEATwDzAeoAAQABAFcA9AHqAAEAAQBgAPUB6gABAAEAaQD2AeoAAQABAHIASADrAAEACwCSAGAA6wABAAEAiQBhAOsAAQABAIAAYgDrAAEAAQB3AGMA6wABAAEAbgBkAOsAAQABAGYAZQDrAAEAAQBdAGYA6wABAAEAVABnAOsAAQABAEsAaADrAAEAAQBDAGkA6wABAAEAOgBqAOsAAQABADEAawDrAAEAAQApAGwA6wABAAEAIABtAOsAAQABABgAbgDrAAEAAQAPAG8A6wABAAEABwB0AOsAAQAGAP//zADrAAEAOAD//w4B6wADAAwA//9SAesAAQABAAAAaAHrAAEADQD//+gB6wABAAEA///pAesAAQABAAcA6gHrAAEAAQAPAOsB6wABAAEAGADsAesAAQABACAA7QHrAAEAAQApAO4B6wABAAEAMQDvAesAAQABADoA8AHrAAEAAQBDAPEB6wABAAEASwDyAesAAQABAFQA8wHrAAEAAQBdAPQB6wABAAEAZgD1AesAAQABAG4AYADsAAEAAQCOAGEA7AABAAEAhgBiAOwAAQABAH0AYwDsAAEAAQB0AGQA7AABAAEAawBlAOwAAQABAGIAZgDsAAEAAQBaAGcA7AABAAEAUQBoAOwAAQABAEgAaQDsAAEAAQBAAGoA7AABAAEANwBrAOwAAQABAC8AbADsAAEAAQAmAG0A7AABAAEAHgBuAOwAAQABABUAbwDsAAEAAQANAHAA7AABAAEABQDNAOwAAQA3AP//CwHsAAMADAD//1MB7AADAAEAAABpAewAAgANAP//6AHsAAEAAQAFAOkB7AABAAEADQDqAewAAQABABUA6wHsAAEAAQAeAOwB7AABAAEAJgDtAewAAQABAC8A7gHsAAEAAQA3AO8B7AABAAEAQADwAewAAQABAEgA8QHsAAEAAQBRAPIB7AABAAEAWgDzAewAAQABAGIA9AHsAAEAAQBrAEkA7QABAAoAkgBhAO0AAQABAIsAYgDtAAEAAQCCAGMA7QABAAEAeQBkAO0AAQABAHEAZQDtAAEAAQBoAGYA7QABAAEAXwBnAO0AAQABAFcAaADtAAEAAQBOAGkA7QABAAEARQBqAO0AAQABAD0AawDtAAEAAQA0AGwA7QABAAEALABtAO0AAQABACQAbgDtAAEAAQAbAG8A7QABAAEAEwBwAO0AAQABAAsAcQDtAAEAAQADAHUA7QABAAUA///OAO0AAQA2AP//CAHtAAMADAD//1UB7QABAAEAAQBrAe0AAQANAP//5wHtAAEAAQADAOgB7QABAAEACwDpAe0AAQABABMA6gHtAAEAAQAbAOsB7QABAAEAJADsAe0AAQABACwA7QHtAAEAAQA0AO4B7QABAAEAPQDvAe0AAQABAEUA8AHtAAEAAQBOAPEB7QABAAEAVwDyAe0AAQABAF8A8wHtAAEAAQBoAPQB7QABAAEAcQBhAO4AAQABAJAAYgDuAAEAAQCIAGMA7gABAAEAfwBkAO4AAQABAHYAZQDuAAEAAQBuAGYA7gABAAEAZQBnAO4AAQABAFwAaADuAAEAAQBUAGkA7gABAAEASwBqAO4AAQABAEMAawDuAAEAAQA6AGwA7gABAAEAMgBtAO4AAQABACoAbgDuAAEAAQAhAG8A7gABAAEAGQBwAO4AAQABABEAcQDuAAEAAQAJAHYA7gABAAUA///PAO4AAQA1AP//BQHuAAMADAD//yYB7gACAAEA//9sAe4AAgANAP//5gHuAAEAAQABAOcB7gABAAEACQDoAe4AAQABABEA6QHuAAEAAQAZAOoB7gABAAEAIQDrAe4AAQABACoA7AHuAAEAAQAyAO0B7gABAAEAOgDuAe4AAQABAEMA7wHuAAEAAQBLAPAB7gABAAEAVADxAe4AAQABAFwA8gHuAAEAAQBlAPMB7gABAAEAbgBKAO8AAQAJAJIAYgDvAAEAAQCNAGMA7wABAAEAhQBkAO8AAQABAHwAZQDvAAEAAQBzAGYA7wABAAEAawBnAO8AAQABAGIAaADvAAEAAQBaAGkA7wABAAEAUQBqAO8AAQABAEkAawDvAAEAAQBAAGwA7wABAAEAOABtAO8AAQABADAAbgDvAAEAAQAoAG8A7wABAAEAHwBwAO8AAQABABcAcQDvAAEAAQAPAHIA7wABAAEABwB3AO8AAQAFAP//0ADvAAEANQD//9EA7wABAAEAAgACAe8AAwAMAP//IwHvAAIAAQD//1oB7wABAAEA//9uAe8AAgANAP//5QHvAAEAAQAAAOYB7wABAAEABwDnAe8AAQABAA8A6AHvAAEAAQAXAOkB7wABAAEAHwDqAe8AAQABACgA6wHvAAEAAQAwAOwB7wABAAEAOADtAe8AAQABAEAA7gHvAAEAAQBJAO8B7wABAAEAUQDwAe8AAQABAFoA8QHvAAEAAQBiAPIB7wABAAEAawBLAPAAAQAJAJEAYwDwAAEAAQCKAGQA8AABAAEAggBlAPAAAQABAHkAZgDwAAEAAQBwAGcA8AABAAEAaABoAPAAAQABAF8AaQDwAAEAAQBXAGoA8AABAAEATwBrAPAAAQABAEYAbADwAAEAAQA+AG0A8AABAAEANgBuAPAAAQABAC4AbwDwAAEAAQAmAHAA8AABAAEAHgBxAPAAAQABABYAcgDwAAEAAQAOAHMA8AABAAEABgB4APAAAQAFAP//0QDwAAIANAD//wAB8AACAAwA//8gAfAAAgABAP//cAHwAAEADQD//+UB8AABAAEABgDmAfAAAQABAA4A5wHwAAEAAQAWAOgB8AABAAEAHgDpAfAAAQABACYA6gHwAAEAAQAuAOsB8AABAAEANgDsAfAAAQABAD4A7QHwAAEAAQBGAO4B8AABAAEATwDvAfAAAQABAFcA8AHwAAEAAQBfAPEB8AABAAEAaADyAfAAAQABAHAAYwDxAAEAAQCQAGQA8QABAAEAhwBlAPEAAQABAH8AZgDxAAEAAQB2AGcA8QABAAEAbgBoAPEAAQABAGUAaQDxAAEAAQBdAGoA8QABAAEAVQBrAPEAAQABAEwAbADxAAEAAQBEAG0A8QABAAEAPABuAPEAAQABADQAbwDxAAEAAQAsAHAA8QABAAEAJABxAPEAAQABABwAcgDxAAEAAQAUAHMA8QABAAEADAB0APEAAQABAAUAeQDxAAEABQD//9MA8QACADMA///9APEAAwALAP//HQHxAAIAAQD//10B8QABAAEA//9xAfEAAgANAP//5AHxAAEAAQAFAOUB8QABAAEADADmAfEAAQABABQA5wHxAAEAAQAcAOgB8QABAAEAJADpAfEAAQABACwA6gHxAAEAAQA0AOsB8QABAAEAPADsAfEAAQABAEQA7QHxAAEAAQBMAO4B8QABAAEAVQDvAfEAAQABAF0A8AHxAAEAAQBlAPEB8QABAAEAbgBMAPIAAQAJAJEAZADyAAEAAQCNAGUA8gABAAEAhABmAPIAAQABAHwAZwDyAAEAAQBzAGgA8gABAAEAawBpAPIAAQABAGMAagDyAAEAAQBaAGsA8gABAAEAUgBsAPIAAQABAEoAbQDyAAEAAQBCAG4A8gABAAEAOgBvAPIAAQABADIAcADyAAEAAQAqAHEA8gABAAEAIgByAPIAAQABABoAcwDyAAEAAQATAHQA8gABAAEACwB1APIAAQABAAQAegDyAAEABQD//9UA8gACABEA///6APIAAwALAP//GgHyAAIAAQD//3MB8gACAA0A///jAfIAAQABAAQA5AHyAAEAAQALAOUB8gABAAEAEwDmAfIAAQABABoA5wHyAAEAAQAiAOgB8gABAAEAKgDpAfIAAQABADIA6gHyAAEAAQA6AOsB8gABAAEAQgDsAfIAAQABAEoA7QHyAAEAAQBSAO4B8gABAAEAWgDvAfIAAQABAGMA8AHyAAEAAQBrAE0A8wABAAkAkQBlAPMAAQABAIoAZgDzAAEAAQCCAGcA8wABAAEAeQBoAPMAAQABAHEAaQDzAAEAAQBpAGoA8wABAAEAYABrAPMAAQABAFgAbADzAAEAAQBQAG0A8wABAAEASABuAPMAAQABAEAAbwDzAAEAAQA4AHAA8wABAAEAMABxAPMAAQABACgAcgDzAAEAAQAhAHMA8wABAAEAGQB0APMAAQABABIAdQDzAAEAAQAKAHYA8wABAAEAAwB7APMAAQAFAP//1wDzAAIADgD///QA8wAGAAsA//8XAfMAAgABAP//dQHzAAEADQD//+IB8wABAAEAAwDjAfMAAQABAAoA5AHzAAEAAQASAOUB8wABAAEAGQDmAfMAAQABACEA5wHzAAEAAQAoAOgB8wABAAEAMADpAfMAAQABADgA6gHzAAEAAQBAAOsB8wABAAEASADsAfMAAQABAFAA7QHzAAEAAQBYAO4B8wABAAEAYADvAfMAAQABAGkA8AHzAAEAAQBxAE4A9AABAAkAkQBmAPQAAQABAIgAZwD0AAEAAQB/AGgA9AABAAEAdwBpAPQAAQABAG8AagD0AAEAAQBmAGsA9AABAAEAXgBsAPQAAQABAFYAbQD0AAEAAQBOAG4A9AABAAEARgBvAPQAAQABAD4AcAD0AAEAAQA2AHEA9AABAAEALwByAPQAAQABACcAcwD0AAEAAQAgAHQA9AABAAEAGAB1APQAAQABABEAdgD0AAEAAQAKAHcA9AABAAEAAgB8APQAAQAFAP//2QD0AAMADQD///EA9AADAA0A//9iAfQAAQABAP//dgH0AAIADQD//+EB9AABAAEAAgDiAfQAAQABAAoA4wH0AAEAAQARAOQB9AABAAEAGADlAfQAAQABACAA5gH0AAEAAQAnAOcB9AABAAEALwDoAfQAAQABADYA6QH0AAEAAQA+AOoB9AABAAEARgDrAfQAAQABAE4A7AH0AAEAAQBWAO0B9AABAAEAXgDuAfQAAQABAGYA7wH0AAEAAQBvAGYA9QABAAEAjQBnAPUAAQABAIUAaAD1AAEAAQB9AGkA9QABAAEAdQBqAPUAAQABAGwAawD1AAEAAQBkAGwA9QABAAEAXABtAPUAAQABAFQAbgD1AAEAAQBMAG8A9QABAAEARABwAPUAAQABAD0AcQD1AAEAAQA1AHIA9QABAAEALQBzAPUAAQABACYAdAD1AAEAAQAfAHUA9QABAAEAFwB2APUAAQABABAAdwD1AAEAAQAJAHgA9QABAAEAAgB9APUAAQAFAP//3AD1AAMADQD//98A9QABAAEAAgDgAPUAAQANAP//7QD1AAMAAQABAPAA9QABAA0A//94AfUAAQANAP//4AH1AAEAAQACAOEB9QABAAEACQDiAfUAAQABABAA4wH1AAEAAQAXAOQB9QABAAEAHwDlAfUAAQABACYA5gH1AAEAAQAtAOcB9QABAAEANQDoAfUAAQABAD0A6QH1AAEAAQBEAOoB9QABAAEATADrAfUAAQABAFQA7AH1AAEAAQBcAO0B9QABAAEAZADuAfUAAQABAGwATwD2AAEACQCRAGcA9gABAAEAiwBoAPYAAQABAIMAaQD2AAEAAQB7AGoA9gABAAEAcgBrAPYAAQABAGoAbAD2AAEAAQBiAG0A9gABAAEAWgBuAPYAAQABAFIAbwD2AAEAAQBLAHAA9gABAAEAQwBxAPYAAQABADsAcgD2AAEAAQA0AHMA9gABAAEALAB0APYAAQABACUAdQD2AAEAAQAeAHYA9gABAAEAFwB3APYAAQABABAAeAD2AAEAAQAJAHkA9gABAAEAAgB+APYAAQAFAP//3wD2AAEADAD//+EA9gAPAAwA//95AfYAAgANAP//3wH2AAEAAQACAOAB9gABAAEACQDhAfYAAQABABAA4gH2AAEAAQAXAOMB9gABAAEAHgDkAfYAAQABACUA5QH2AAEAAQAsAOYB9gABAAEANADnAfYAAQABADsA6AH2AAEAAQBDAOkB9gABAAEASwDqAfYAAQABAFIA6wH2AAEAAQBaAOwB9gABAAEAYgDtAfYAAQABAGoAUAD3AAEACQCRAGgA9wABAAEAiQBpAPcAAQABAIAAagD3AAEAAQB4AGsA9wABAAEAcABsAPcAAQABAGgAbQD3AAEAAQBgAG4A9wABAAEAWQBvAPcAAQABAFEAcAD3AAEAAQBJAHEA9wABAAEAQgByAPcAAQABADoAcwD3AAEAAQAzAHQA9wABAAEAKwB1APcAAQABACQAdgD3AAEAAQAdAHcA9wABAAEAFgB4APcAAQABAA8AeQD3AAEAAQAJAHoA9wABAAEAAgB/APcAAQAFAP//ZwH3AAEAAQD//3sB9wACAA4A///eAfcAAQABAAIA3wH3AAEAAQAJAOAB9wABAAEADwDhAfcAAQABABYA4gH3AAEAAQAdAOMB9wABAAEAJADkAfcAAQABACsA5QH3AAEAAQAzAOYB9wABAAEAOgDnAfcAAQABAEIA6AH3AAEAAQBJAOkB9wABAAEAUQDqAfcAAQABAFkA6wH3AAEAAQBgAOwB9wABAAEAaABRAPgAAQAJAJAAaQD4AAEAAQCGAGoA+AABAAEAfgBrAPgAAQABAHYAbAD4AAEAAQBuAG0A+AABAAEAZwBuAPgAAQABAF8AbwD4AAEAAQBXAHAA+AABAAEATwBxAPgAAQABAEgAcgD4AAEAAQBAAHMA+AABAAEAOQB0APgAAQABADIAdQD4AAEAAQArAHYA+AABAAEAJAB3APgAAQABAB0AeAD4AAEAAQAWAHkA+AABAAEADwB6APgAAQABAAkAewD4AAEAAQACAIAA+AABAAUA//99AfgAAQAOAP//3QH4AAEAAQACAN4B+AABAAEACQDfAfgAAQABAA8A4AH4AAEAAQAWAOEB+AABAAEAHQDiAfgAAQABACQA4wH4AAEAAQArAOQB+AABAAEAMgDlAfgAAQABADkA5gH4AAEAAQBAAOcB+AABAAEASADoAfgAAQABAE8A6QH4AAEAAQBXAOoB+AABAAEAXwDrAfgAAQABAGcAUgD5AAEACQCQAGoA+QABAAEAhABrAPkAAQABAHwAbAD5AAEAAQB1AG0A+QABAAEAbQBuAPkAAQABAGUAbwD5AAEAAQBdAHAA+QABAAEAVgBxAPkAAQABAE4AcgD5AAEAAQBHAHMA+QABAAEAPwB0APkAAQABADgAdQD5AAEAAQAxAHYA+QABAAEAKgB3APkAAQABACMAeAD5AAEAAQAdAHkA+QABAAEAFgB6APkAAQABAA8AewD5AAEAAQAJAHwA+QABAAEAAwCBAPkAAgAFAP//agH5AAEAAQD//34B+QACAA0A///cAfkAAQABAAMA3QH5AAEAAQAJAN4B+QABAAEADwDfAfkAAQABABYA4AH5AAEAAQAdAOEB+QABAAEAIwDiAfkAAQABACoA4wH5AAEAAQAxAOQB+QABAAEAOADlAfkAAQABAD8A5gH5AAEAAQBHAOcB+QABAAEATgDoAfkAAQABAFYA6QH5AAEAAQBdAOoB+QABAAEAZQDrAfkAAQABAG0AagD6AAEAAQCKAGsA+gABAAEAggBsAPoAAQABAHsAbQD6AAEAAQBzAG4A+gABAAEAawBvAPoAAQABAGMAcAD6AAEAAQBcAHEA+gABAAEAVAByAPoAAQABAE0AcwD6AAEAAQBGAHQA+gABAAEAPwB1APoAAQABADgAdgD6AAEAAQAxAHcA+gABAAEAKgB4APoAAQABACMAeQD6AAEAAQAcAHoA+gABAAEAFgB7APoAAQABABAAfAD6AAEAAQAJAH0A+gABAAEAAwCDAPoAAQAFAP//gAH6AAIADgD//9sB+gABAAEAAwDcAfoAAQABAAkA3QH6AAEAAQAQAN4B+gABAAEAFgDfAfoAAQABABwA4AH6AAEAAQAjAOEB+gABAAEAKgDiAfoAAQABADEA4wH6AAEAAQA4AOQB+gABAAEAPwDlAfoAAQABAEYA5gH6AAEAAQBNAOcB+gABAAEAVADoAfoAAQABAFwA6QH6AAEAAQBjAOoB+gABAAEAawBTAPsAAQAIAJAAawD7AAEAAQCIAGwA+wABAAEAgQBtAPsAAQABAHkAbgD7AAEAAQBxAG8A+wABAAEAagBwAPsAAQABAGIAcQD7AAEAAQBbAHIA+wABAAEAUwBzAPsAAQABAEwAdAD7AAEAAQBFAHUA+wABAAEAPgB2APsAAQABADcAdwD7AAEAAQAwAHgA+wABAAEAKgB5APsAAQABACMAegD7AAEAAQAdAHsA+wABAAEAFgB8APsAAQABABAAfQD7AAEAAQAKAH4A+wABAAEABACEAPsAAQAFAP//ggH7AAEADgD//9oB+wABAAEABADbAfsAAQABAAoA3AH7AAEAAQAQAN0B+wABAAEAFgDeAfsAAQABAB0A3wH7AAEAAQAjAOAB+wABAAEAKgDhAfsAAQABADAA4gH7AAEAAQA3AOMB+wABAAEAPgDkAfsAAQABAEUA5QH7AAEAAQBMAOYB+wABAAEAUwDnAfsAAQABAFsA6AH7AAEAAQBiAOkB+wABAAEAagBUAPwAAQAGAJAAbAD8AAEAAQCHAG0A/AABAAEAfwBuAPwAAQABAHcAbwD8AAEAAQBwAHAA/AABAAEAaABxAPwAAQABAGEAcgD8AAEAAQBaAHMA/AABAAEAUgB0APwAAQABAEsAdQD8AAEAAQBEAHYA/AABAAEAPgB3APwAAQABADcAeAD8AAEAAQAwAHkA/AABAAEAKgB6APwAAQABACMAewD8AAEAAQAdAHwA/AABAAEAFwB9APwAAQABABEAfgD8AAEAAQALAH8A/AABAAEABQCFAPwAAQAFAP///QD8AAIAAQD//28B/AABAAEA//+DAfwAAgANAP//2AH8AAEAAQD//9kB/AABAAEABQDaAfwAAQABAAsA2wH8AAEAAQARANwB/AABAAEAFwDdAfwAAQABAB0A3gH8AAEAAQAjAN8B/AABAAEAKgDgAfwAAQABADAA4QH8AAEAAQA3AOIB/AABAAEAPgDjAfwAAQABAEQA5AH8AAEAAQBLAOUB/AABAAEAUgDmAfwAAQABAFoA5wH8AAEAAQBhAOgB/AABAAEAaABVAP0AAQAFAI8AbQD9AAEAAQCFAG4A/QABAAEAfQBvAP0AAQABAHYAcAD9AAEAAQBvAHEA/QABAAEAZwByAP0AAQABAGAAcwD9AAEAAQBZAHQA/QABAAEAUgB1AP0AAQABAEsAdgD9AAEAAQBEAHcA/QABAAEAPQB4AP0AAQABADcAeQD9AAEAAQAwAHoA/QABAAEAKgB7AP0AAQABACQAfAD9AAEAAQAdAH0A/QABAAEAFwB+AP0AAQABABIAfwD9AAEAAQAMAIAA/QABAAEABgCGAP0AAgAEAP//+gD9AAIAAQD//4UB/QACAA4A///XAf0AAQABAAEA2AH9AAEAAQAGANkB/QABAAEADADaAf0AAQABABIA2wH9AAEAAQAXANwB/QABAAEAHQDdAf0AAQABACQA3gH9AAEAAQAqAN8B/QABAAEAMADgAf0AAQABADcA4QH9AAEAAQA9AOIB/QABAAEARADjAf0AAQABAEsA5AH9AAEAAQBSAOUB/QABAAEAWQDmAf0AAQABAGAA5wH9AAEAAQBnAFYA/gABAAYAjgBuAP4AAQABAIQAbwD+AAEAAQB8AHAA/gABAAEAdQBxAP4AAQABAG0AcgD+AAEAAQBmAHMA/gABAAEAXwB0AP4AAQABAFgAdQD+AAEAAQBRAHYA/gABAAEASgB3AP4AAQABAEQAeAD+AAEAAQA9AHkA/gABAAEANwB6AP4AAQABADAAewD+AAEAAQAqAHwA/gABAAEAJAB9AP4AAQABAB4AfgD+AAEAAQAYAH8A/gABAAEAEwCAAP4AAQABAA0AgQD+AAEAAQAHAIIA/gABAAEAAgCIAP4AAQAFAP//9AD+AAUAAQD//4cB/gABAA4A///WAf4AAQABAAIA1wH+AAEAAQAHANgB/gABAAEADQDZAf4AAQABABMA2gH+AAEAAQAYANsB/gABAAEAHgDcAf4AAQABACQA3QH+AAEAAQAqAN4B/gABAAEAMADfAf4AAQABADcA4AH+AAEAAQA9AOEB/gABAAEARADiAf4AAQABAEoA4wH+AAEAAQBRAOQB/gABAAEAWADlAf4AAQABAF8A5gH+AAEAAQBmAG8A/wABAAEAggBwAP8AAQABAHsAcQD/AAEAAQB0AHIA/wABAAEAbQBzAP8AAQABAGUAdAD/AAEAAQBeAHUA/wABAAEAWAB2AP8AAQABAFEAdwD/AAEAAQBKAHgA/wABAAEARAB5AP8AAQABAD0AegD/AAEAAQA3AHsA/wABAAEAMQB8AP8AAQABACsAfQD/AAEAAQAlAH4A/wABAAEAHwB/AP8AAQABABkAgAD/AAEAAQAUAIEA/wABAAEADgCCAP8AAQABAAkAgwD/AAEAAQAEAIkA/wACAAQA///0AP8ABAABAAAAdAH/AAEAAQD//4gB/wACAA4A///VAf8AAQABAAQA1gH/AAEAAQAJANcB/wABAAEADgDYAf8AAQABABQA2QH/AAEAAQAZANoB/wABAAEAHwDbAf8AAQABACUA3AH/AAEAAQArAN0B/wABAAEAMQDeAf8AAQABADcA3wH/AAEAAQA9AOAB/wABAAEARADhAf8AAQABAEoA4gH/AAEAAQBRAOMB/wABAAEAWADkAf8AAQABAF4A5QH/AAEAAQBlAFcAAAEBAAgAjQBwAAABAQABAIEAcQAAAQEAAQB6AHIAAAEBAAEAcwBzAAABAQABAGwAdAAAAQEAAQBlAHUAAAEBAAEAXgB2AAABAQABAFcAdwAAAQEAAQBRAHgAAAEBAAEASgB5AAABAQABAEQAegAAAQEAAQA+AHsAAAEBAAEANwB8AAABAQABADEAfQAAAQEAAQArAH4AAAEBAAEAJgB/AAABAQABACAAgAAAAQEAAQAaAIEAAAEBAAEAFQCCAAABAQABABAAgwAAAQEAAQAKAIQAAAEBAAEABQCLAAABAQAFAP//9AAAAQIAAQACAIoBAAEBAA4A///TAQABAQABAAAA1AEAAQEAAQAFANUBAAEBAAEACgDWAQABAQABABAA1wEAAQEAAQAVANgBAAEBAAEAGgDZAQABAQABACAA2gEAAQEAAQAmANsBAAEBAAEAKwDcAQABAQABADEA3QEAAQEAAQA3AN4BAAEBAAEAPgDfAQABAQABAEQA4AEAAQEAAQBKAOEBAAEBAAEAUQDiAQABAQABAFcA4wEAAQEAAQBeAOQBAAEBAAEAZQBYAAEBAQAHAI0AcQABAQEAAQCAAHIAAQEBAAEAeQBzAAEBAQABAHIAdAABAQEAAQBrAHUAAQEBAAEAZAB2AAEBAQABAF4AdwABAQEAAQBXAHgAAQEBAAEAUQB5AAEBAQABAEoAegABAQEAAQBEAHsAAQEBAAEAPgB8AAEBAQABADgAfQABAQEAAQAyAH4AAQEBAAEALAB/AAEBAQABACcAgAABAQEAAQAhAIEAAQEBAAEAHACCAAEBAQABABYAgwABAQEAAQARAIQAAQEBAAEADACFAAEBAQABAAcAhgABAQEAAQACAIcAAQEBAAEA//+MAAEBAgAEAP//1wABAQIAAQACANsAAQEBAAEAAwDxAAEBAgABAAQAiwEBAQMADgD//44BAQEBAAEAAwDSAQEBAQABAAIA0wEBAQEAAQAHANQBAQEBAAEADADVAQEBAQABABEA1gEBAQEAAQAWANcBAQEBAAEAHADYAQEBAQABACEA2QEBAQEAAQAnANoBAQEBAAEALADbAQEBAQABADIA3AEBAQEAAQA4AN0BAQEBAAEAPgDeAQEBAQABAEQA3wEBAQEAAQBKAOABAQEBAAEAUQDhAQEBAQABAFcA4gEBAQEAAQBeAOMBAQEBAAEAZABUAAIBAgADAI4AWQACAQEACACMAHIAAgEBAAEAfwBzAAIBAQABAHgAdAACAQEAAQBxAHUAAgEBAAEAawB2AAIBAQABAGQAdwACAQEAAQBdAHgAAgEBAAEAVwB5AAIBAQABAFEAegACAQEAAQBLAHsAAgEBAAEARAB8AAIBAQABAD8AfQACAQEAAQA5AH4AAgEBAAEAMwB/AAIBAQABAC0AgAACAQEAAQAoAIEAAgEBAAEAIgCCAAIBAQABAB0AgwACAQEAAQAYAIQAAgEBAAEAEwCFAAIBAQABAA4AhgACAQEAAQAJAIcAAgEBAAEABACOAAIBAgAEAP//3wACAQUAAQADAOQAAgEJAAEAAgDtAAIBAQABAAQAjgECAQEAGwD//48BAgEBAAEAAgDQAQIBAQABAAAA0QECAQEAAQAEANIBAgEBAAEACQDTAQIBAQABAA4A1AECAQEAAQATANUBAgEBAAEAGADWAQIBAQABAB0A1wECAQEAAQAiANgBAgEBAAEAKADZAQIBAQABAC0A2gECAQEAAQAzANsBAgEBAAEAOQDcAQIBAQABAD8A3QECAQEAAQBEAN4BAgEBAAEASwDfAQIBAQABAFEA4AECAQEAAQBXAOEBAgEBAAEAXQDiAQIBAQABAGQAWgADAQEACQCLAHMAAwEBAAEAfgB0AAMBAQABAHgAdQADAQEAAQBxAHYAAwEBAAEAagB3AAMBAQABAGQAeAADAQEAAQBdAHkAAwEBAAEAVwB6AAMBAQABAFEAewADAQEAAQBLAHwAAwEBAAEARQB9AAMBAQABAD8AfgADAQEAAQA6AH8AAwEBAAEANACAAAMBAQABAC4AgQADAQEAAQApAIIAAwEBAAEAJACDAAMBAQABAB8AhAADAQEAAQAaAIUAAwEBAAEAFQCGAAMBAQABABAAhwADAQEAAQALAIgAAwEBAAEABwCJAAMBAQABAAIAigADAQEAAQD//5AAAwEBAAQA///VAAMBAQAiAP//egEDAQEAAQD//48BAwECABoA///PAQMBAQABAAIA0AEDAQEAAQAHANEBAwEBAAEACwDSAQMBAQABABAA0wEDAQEAAQAVANQBAwEBAAEAGgDVAQMBAQABAB8A1gEDAQEAAQAkANcBAwEBAAEAKQDYAQMBAQABAC4A2QEDAQEAAQA0ANoBAwEBAAEAOgDbAQMBAQABAD8A3AEDAQEAAQBFAN0BAwEBAAEASwDeAQMBAQABAFEA3wEDAQEAAQBXAOABAwEBAAEAXQBWAAQBAQADAI0AWwAEAQEABwCLAHQABAEBAAEAfgB1AAQBAQABAHcAdgAEAQEAAQBxAHcABAEBAAEAagB4AAQBAQABAGQAeQAEAQEAAQBeAHoABAEBAAEAVwB7AAQBAQABAFEAfAAEAQEAAQBMAH0ABAEBAAEARgB+AAQBAQABAEAAfwAEAQEAAQA7AIAABAEBAAEANQCBAAQBAQABADAAggAEAQEAAQAqAIMABAEBAAEAJQCEAAQBAQABACAAhQAEAQEAAQAbAIYABAEBAAEAFwCHAAQBAQABABIAiAAEAQEAAQANAIkABAEBAAEACQCKAAQBAQABAAUAkQAEAQIABAD//5EBBAEBABkA//+SAQQBAQABAAMAzQEEAQEAAQABAM4BBAEBAAEABQDPAQQBAQABAAkA0AEEAQEAAQANANEBBAEBAAEAEgDSAQQBAQABABcA0wEEAQEAAQAbANQBBAEBAAEAIADVAQQBAQABACUA1gEEAQEAAQAqANcBBAEBAAEAMADYAQQBAQABADUA2QEEAQEAAQA7ANoBBAEBAAEAQADbAQQBAQABAEYA3AEEAQEAAQBMAN0BBAEBAAEAUQDeAQQBAQABAFcA3wEEAQEAAQBeAFUABQEBAAEAjQB1AAUBAQABAH0AdgAFAQEAAQB3AHcABQEBAAEAcQB4AAUBAQABAGoAeQAFAQEAAQBkAHoABQEBAAEAXgB7AAUBAQABAFgAfAAFAQEAAQBSAH0ABQEBAAEATAB+AAUBAQABAEcAfwAFAQEAAQBBAIAABQEBAAEAPACBAAUBAQABADYAggAFAQEAAQAxAIMABQEBAAEALACEAAUBAQABACcAhQAFAQEAAQAiAIYABQEBAAEAHQCHAAUBAQABABkAiAAFAQEAAQAUAIkABQEBAAEAEACKAAUBAQABAAsAiwAFAQEAAQAHAIwABQEBAAEAAwCNAAUBAQABAP//kwAFAQIABAD//3wBBQEBAAEAAwCSAQUBAQAYAP//kwEFAQEAAQADAMsBBQEBAAEA///MAQUBAQABAAMAzQEFAQEAAQAHAM4BBQEBAAEACwDPAQUBAQABABAA0AEFAQEAAQAUANEBBQEBAAEAGQDSAQUBAQABAB0A0wEFAQEAAQAiANQBBQEBAAEAJwDVAQUBAQABACwA1gEFAQEAAQAxANcBBQEBAAEANgDYAQUBAQABADwA2QEFAQEAAQBBANoBBQEBAAEARwDbAQUBAQABAEwA3AEFAQEAAQBSAN0BBQEBAAEAWADeAQUBAQABAF4AXAAGAQEABACKAHYABgEBAAEAfQB3AAYBAQABAHcAeAAGAQEAAQBxAHkABgEBAAEAagB6AAYBAQABAGQAewAGAQEAAQBeAHwABgEBAAEAWQB9AAYBAQABAFMAfgAGAQEAAQBNAH8ABgEBAAEASACAAAYBAQABAEIAgQAGAQEAAQA9AIIABgEBAAEAOACDAAYBAQABADMAhAAGAQEAAQAuAIUABgEBAAEAKQCGAAYBAQABACQAhwAGAQEAAQAfAIgABgEBAAEAGwCJAAYBAQABABYAigAGAQEAAQASAIsABgEBAAEADgCMAAYBAQABAAoAjQAGAQEAAQAGAI4ABgEBAAEAAgCPAAYBAQABAP//lQAGAQMABAD//34BBgEBAAEAAgB/AQYBAQABAP//kwEGAQEAFwD//5QBBgEBAAEAAwDJAQYBAQABAP//ygEGAQEAAQACAMsBBgEBAAEABgDMAQYBAQABAAoAzQEGAQEAAQAOAM4BBgEBAAEAEgDPAQYBAQABABYA0AEGAQEAAQAbANEBBgEBAAEAHwDSAQYBAQABACQA0wEGAQEAAQApANQBBgEBAAEALgDVAQYBAQABADMA1gEGAQEAAQA4ANcBBgEBAAEAPQDYAQYBAQABAEIA2QEGAQEAAQBIANoBBgEBAAEATQDbAQYBAQABAFMA3AEGAQEAAQBZAN0BBgEBAAEAXgBdAAcBAQAFAIoAeAAHAQEAAQB3AHkABwEBAAEAcQB6AAcBAQABAGsAewAHAQEAAQBlAHwABwEBAAEAXwB9AAcBAQABAFkAfgAHAQEAAQBUAH8ABwEBAAEATgCAAAcBAQABAEkAgQAHAQEAAQBDAIIABwEBAAEAPgCDAAcBAQABADkAhAAHAQEAAQA0AIUABwEBAAEALwCGAAcBAQABACoAhwAHAQEAAQAmAIgABwEBAAEAIQCJAAcBAQABAB0AigAHAQEAAQAYAIsABwEBAAEAFACMAAcBAQABABAAjQAHAQEAAQAMAI4ABwEBAAEACQCPAAcBAQABAAUAkAAHAQEAAQACAJgABwECAAQA///WAAcBAQACAAIAlAEHAQEAFgD//8gBBwEBAAEAAgDJAQcBAQABAAUAygEHAQEAAQAJAMsBBwEBAAEADADMAQcBAQABABAAzQEHAQEAAQAUAM4BBwEBAAEAGADPAQcBAQABAB0A0AEHAQEAAQAhANEBBwEBAAEAJgDSAQcBAQABACoA0wEHAQEAAQAvANQBBwEBAAEANADVAQcBAQABADkA1gEHAQEAAQA+ANcBBwEBAAEAQwDYAQcBAQABAEkA2QEHAQEAAQBOANoBBwEBAAEAVADbAQcBAQABAFkA3AEHAQEAAQBfAFgACAEBAAEAiwBeAAgBAQADAIkAeQAIAQEAAQB3AHoACAEBAAEAcQB7AAgBAQABAGsAfAAIAQEAAQBlAH0ACAEBAAEAYAB+AAgBAQABAFoAfwAIAQEAAQBVAIAACAEBAAEATwCBAAgBAQABAEoAggAIAQEAAQBFAIMACAEBAAEAQACEAAgBAQABADsAhQAIAQEAAQA2AIYACAEBAAEAMQCHAAgBAQABACwAiAAIAQEAAQAoAIkACAEBAAEAIwCKAAgBAQABAB8AiwAIAQEAAQAbAIwACAEBAAEAFwCNAAgBAQABABMAjgAIAQEAAQAPAI8ACAEBAAEACwCQAAgBAQABAAgAkQAIAQEAAQAEAJIACAEBAAEAAQCaAAgBAwAEAP//xgAIAQEAAQACAIEBCAEBAAEAAwCVAQgBAQAVAP//xgEIAQEAAQABAMcBCAEBAAEABADIAQgBAQABAAgAyQEIAQEAAQALAMoBCAEBAAEADwDLAQgBAQABABMAzAEIAQEAAQAXAM0BCAEBAAEAGwDOAQgBAQABAB8AzwEIAQEAAQAjANABCAEBAAEAKADRAQgBAQABACwA0gEIAQEAAQAxANMBCAEBAAEANgDUAQgBAQABADsA1QEIAQEAAQBAANYBCAEBAAEARQDXAQgBAQABAEoA2AEIAQEAAQBPANkBCAEBAAEAVQDaAQgBAQABAFoAXwAJAQEABQCIAHoACQEBAAEAeAB7AAkBAQABAHIAfAAJAQEAAQBsAH0ACQEBAAEAZgB+AAkBAQABAGEAfwAJAQEAAQBbAIAACQEBAAEAVgCBAAkBAQABAFAAggAJAQEAAQBLAIMACQEBAAEARgCEAAkBAQABAEEAhQAJAQEAAQA8AIYACQEBAAEANwCHAAkBAQABADMAiAAJAQEAAQAuAIkACQEBAAEAKgCKAAkBAQABACUAiwAJAQEAAQAhAIwACQEBAAEAHQCNAAkBAQABABkAjgAJAQEAAQAVAI8ACQEBAAEAEQCQAAkBAQABAA4AkQAJAQEAAQAKAJIACQEBAAEABwCTAAkBAgABAAMAnQAJAQIABAD//9YACQEBABwA//+DAQkBAQABAAIAhAEJAQEAAQD//5YBCQEBAAEAAwDDAQkBAQABAP//xAEJAQIAAQADAMYBCQEBAAEABwDHAQkBAQABAAoAyAEJAQEAAQAOAMkBCQEBAAEAEQDKAQkBAQABABUAywEJAQEAAQAZAMwBCQEBAAEAHQDNAQkBAQABACEAzgEJAQEAAQAlAM8BCQEBAAEAKgDQAQkBAQABAC4A0QEJAQEAAQAzANIBCQEBAAEANwDTAQkBAQABADwA1AEJAQEAAQBBANUBCQEBAAEARgDWAQkBAQABAEsA1wEJAQEAAQBQANgBCQEBAAEAVgDZAQkBAQABAFsAWQAKAQEAAQCKAFwACgEBAAQAiABgAAoBAQAEAIcAfAAKAQEAAQByAH0ACgEBAAEAbQB+AAoBAQABAGcAfwAKAQEAAQBiAIAACgEBAAEAXACBAAoBAQABAFcAggAKAQEAAQBSAIMACgEBAAEATQCEAAoBAQABAEcAhQAKAQEAAQBDAIYACgEBAAEAPgCHAAoBAQABADkAiAAKAQEAAQA0AIkACgEBAAEAMACKAAoBAQABACwAiwAKAQEAAQAnAIwACgEBAAEAIwCNAAoBAQABAB8AjgAKAQEAAQAbAI8ACgEBAAEAFwCQAAoBAQABABQAkQAKAQEAAQAQAJIACgEBAAEADQCTAAoBAQABAAoAlAAKAQEAAQAHAJUACgECAAEAAwCXAAoBAQABAAAAnwAKAQQABAD//9cACgEBAAEAAwCEAQoBAQABAAMAlgEKAQEAEwD//8EBCgECAAEAAQDDAQoBAQABAAQAxAEKAQEAAQAHAMUBCgEBAAEACgDGAQoBAQABAA0AxwEKAQEAAQAQAMgBCgEBAAEAFADJAQoBAQABABcAygEKAQEAAQAbAMsBCgEBAAEAHwDMAQoBAQABACMAzQEKAQEAAQAnAM4BCgEBAAEALADPAQoBAQABADAA0AEKAQEAAQA0ANEBCgEBAAEAOQDSAQoBAQABAD4A0wEKAQEAAQBDANQBCgEBAAEARwDVAQoBAQABAE0A1gEKAQEAAQBSANcBCgEBAAEAVwBbAAsBAQACAIkAXgALAQEABQCHAGEACwEBAAUAhgB9AAsBAQABAHMAfgALAQEAAQBtAH8ACwEBAAEAaACAAAsBAQABAGMAgQALAQEAAQBdAIIACwEBAAEAWACDAAsBAQABAFMAhAALAQEAAQBOAIUACwEBAAEASQCGAAsBAQABAEQAhwALAQEAAQA/AIgACwEBAAEAOwCJAAsBAQABADYAigALAQEAAQAyAIsACwEBAAEALQCMAAsBAQABACkAjQALAQEAAQAlAI4ACwEBAAEAIQCPAAsBAQABAB0AkAALAQEAAQAaAJEACwEBAAEAFgCSAAsBAQABABMAkwALAQEAAQAQAJQACwEBAAEADQCVAAsBAQABAAoAlgALAQIAAQAGAJgACwEBAAEAAwCZAAsBAQABAAAAowALAQMABAD//9cACwEBABoA//+GAQsBAQABAAIAvwELAQEAAQAAAMABCwECAAEABADCAQsBAQABAAcAwwELAQEAAQAKAMQBCwEBAAEADQDFAQsBAQABABAAxgELAQEAAQATAMcBCwEBAAEAFgDIAQsBAQABABoAyQELAQEAAQAdAMoBCwEBAAEAIQDLAQsBAQABACUAzAELAQEAAQApAM0BCwEBAAEALQDOAQsBAQABADIAzwELAQEAAQA2ANABCwEBAAEAOwDRAQsBAQABAD8A0gELAQEAAQBEANMBCwEBAAEASQDUAQsBAQABAE4A1QELAQEAAQBTANYBCwEBAAEAWABdAAwBAQADAIcAYgAMAQEAAwCFAH4ADAEBAAEAdAB/AAwBAQABAG4AgAAMAQEAAQBpAIEADAEBAAEAZACCAAwBAQABAF4AgwAMAQEAAQBZAIQADAEBAAEAVACFAAwBAQABAE8AhgAMAQEAAQBKAIcADAEBAAEARgCIAAwBAQABAEEAiQAMAQEAAQA8AIoADAEBAAEAOACLAAwBAQABADQAjAAMAQEAAQAvAI0ADAEBAAEAKwCOAAwBAQABACcAjwAMAQEAAQAjAJAADAEBAAEAHwCRAAwBAQABABwAkgAMAQEAAQAZAJMADAEBAAEAFQCUAAwBAQABABIAlQAMAQIAAQAOAJcADAECAAEACQCZAAwBAgABAAQAmwAMAQIAAQAAAKYADAEFAAQA///YAAwBAQABAAMAlwEMAQEAAQABALwBDAECAAEAAAC+AQwBAgABAAQAwAEMAQIAAQAJAMIBDAECAAEADgDEAQwBAQABABIAxQEMAQEAAQAVAMYBDAEBAAEAGQDHAQwBAQABABwAyAEMAQEAAQAfAMkBDAEBAAEAIwDKAQwBAQABACcAywEMAQEAAQArAMwBDAEBAAEALwDNAQwBAQABADQAzgEMAQEAAQA4AM8BDAEBAAEAPADQAQwBAQABAEEA0QEMAQEAAQBGANIBDAEBAAEASgDTAQwBAQABAE8A1AEMAQEAAQBUAGMADQEBAAMAhACAAA0BAQABAG8AgQANAQEAAQBqAIIADQEBAAEAZQCDAA0BAQABAF8AhAANAQEAAQBaAIUADQEBAAEAVQCGAA0BAQABAFEAhwANAQEAAQBMAIgADQEBAAEARwCJAA0BAQABAEIAigANAQEAAQA+AIsADQEBAAEAOQCMAA0BAQABADUAjQANAQEAAQAxAI4ADQEBAAEALQCPAA0BAQABACkAkAANAQEAAQAlAJEADQEBAAEAIgCSAA0BAQABAB4AkwANAQEAAQAbAJQADQEBAAEAGACVAA0BAQABABUAlgANAQEAAQASAJcADQECAAEADgCZAA0BAgABAAkAmwANAQIAAQAFAJ0ADQECAAEAAgCrAA0BBgAEAP//xgANAQEAAgABANgADQEBABgA///ZAA0BAQABAAQAiQENAQEAAQADAJcBDQEBABAA//+YAQ0BAQABAAIAuQENAQIAAQAAALsBDQECAAEABAC9AQ0BAQABAAYAvgENAQIAAQAJAMABDQECAAEADgDCAQ0BAQABABIAwwENAQEAAQAVAMQBDQEBAAEAGADFAQ0BAQABABsAxgENAQEAAQAeAMcBDQEBAAEAIgDIAQ0BAQABACUAyQENAQEAAQApAMoBDQEBAAEALQDLAQ0BAQABADEAzAENAQEAAQA1AM0BDQEBAAEAOQDOAQ0BAQABAD4AzwENAQEAAQBCANABDQEBAAEARwDRAQ0BAQABAEwA0gENAQEAAQBRANMBDQEBAAEAVQBfAA4BAgADAIUAZAAOAQEABQCDAIEADgEBAAEAcACCAA4BAQABAGsAgwAOAQEAAQBmAIQADgEBAAEAYQCFAA4BAQABAFwAhgAOAQEAAQBXAIcADgEBAAEAUgCIAA4BAQABAE0AiQAOAQEAAQBIAIoADgEBAAEARACLAA4BAQABAD8AjAAOAQEAAQA7AI0ADgEBAAEANwCOAA4BAQABADMAjwAOAQEAAQAvAJAADgEBAAEAKwCRAA4BAQABACcAkgAOAQEAAQAkAJMADgEBAAEAIACUAA4BAQABAB0AlQAOAQEAAQAaAJYADgEBAAEAFwCXAA4BAgABABMAmQAOAQIAAQAOAJsADgEBAAEACwCcAA4BAQABAAkAnQAOAQIAAQAGAJ8ADgECAAEAAwChAA4BAgABAAAAsQAOARUABQD//9kADgEBABcA///aAA4BAQABAAQAmAEOAQEADwD//5kBDgEBAAEAAQC1AQ4BAwABAAAAuAEOAQIAAQADALoBDgECAAEABgC8AQ4BAQABAAkAvQEOAQIAAQAMAL8BDgEBAAEADwDAAQ4BAgABABMAwgEOAQEAAQAXAMMBDgEBAAEAGgDEAQ4BAQABAB0AxQEOAQEAAQAgAMYBDgEBAAEAJADHAQ4BAQABACcAyAEOAQEAAQArAMkBDgEBAAEALwDKAQ4BAQABADMAywEOAQEAAQA3AMwBDgEBAAEAOwDNAQ4BAQABAD8AzgEOAQEAAQBEAM8BDgEBAAEASADQAQ4BAQABAE0A0QEOAQEAAQBSAGIADwEBAAUAgwBlAA8BAQAFAIIAgwAPAQEAAQBsAIQADwEBAAEAZwCFAA8BAQABAGIAhgAPAQEAAQBdAIcADwEBAAEAWACIAA8BAQABAFMAiQAPAQEAAQBOAIoADwEBAAEASgCLAA8BAQABAEUAjAAPAQEAAQBBAI0ADwEBAAEAPACOAA8BAQABADgAjwAPAQEAAQA0AJAADwEBAAEAMACRAA8BAQABACwAkgAPAQEAAQApAJMADwEBAAEAJQCUAA8BAQABACIAlQAPAQEAAQAfAJYADwEBAAEAHACXAA8BAQABABkAmAAPAQEAAQAXAJkADwECAAEAEwCbAA8BAgABAA4AnQAPAQIAAQAKAJ8ADwEBAAEACACgAA8BAwABAAUAowAPAQIAAQACAKUADwEBAAEAAADGAA8BAQASAP//2gAPAQEAFwD//9sADwEBAAEAAgDsAA8BgQAaAP//iwEPAQEAAgACAIwBDwECAA4A//+ZAQ8BAgANAP//mwEPAQEAAQADALEBDwEEAAEAAAC1AQ8BAwABAAQAuAEPAQEAAQAGALkBDwECAAEACQC7AQ8BAgABAAwAvQEPAQEAAQAPAL4BDwECAAEAEwDAAQ8BAQABABcAwQEPAQEAAQAZAMIBDwEBAAEAHADDAQ8BAQABAB8AxAEPAQEAAQAiAMUBDwEBAAEAJQDGAQ8BAQABACkAxwEPAQEAAQAsAMgBDwEBAAEAMADJAQ8BAQABADQAygEPAQEAAQA4AMsBDwEBAAEAPADMAQ8BAQABAEEAzQEPAQEAAQBFAM4BDwEBAAEASgDPAQ8BAQABAE4AYQAQAQEAAwCEAGMAEAEBAAUAggBmABABAQAFAIEAhAAQAQEAAQBtAIUAEAEBAAEAaACGABABAQABAGMAhwAQAQEAAQBeAIgAEAEBAAEAWQCJABABAQABAFQAigAQAQEAAQBQAIsAEAEBAAEASwCMABABAQABAEYAjQAQAQEAAQBCAI4AEAEBAAEAPgCPABABAQABADoAkAAQAQEAAQA2AJEAEAEBAAEAMgCSABABAQABAC4AkwAQAQEAAQArAJQAEAEBAAEAJwCVABABAQABACQAlgAQAQEAAQAhAJcAEAEBAAEAHgCYABABAQABABsAmQAQAQEAAQAZAJoAEAEBAAEAFgCbABABAgABABMAnQAQAQEAAQAQAJ4AEAECAAEADQCgABABAQABAAsAoQAQAQIAAQAIAKMAEAEDAAEABQCmABABAwABAAIAqQAQAQIAAQAAANsAEAECABYA///dABABAQABAAMA6gAQAQIAGAD//20BEAECACkA//+bARABAgAIAP//nQEQAQIAAQACAKUBEAECAAEAAgCsARABBQABAAAAsQEQAQMAAQADALQBEAECAAEABgC2ARABAgABAAgAuAEQAQIAAQAMALoBEAEBAAEADgC7ARABAgABABEAvQEQAQEAAQAUAL4BEAEBAAEAFgC/ARABAQABABkAwAEQAQEAAQAbAMEBEAEBAAEAHgDCARABAQABACEAwwEQAQEAAQAkAMQBEAEBAAEAJwDFARABAQABACsAxgEQAQEAAQAuAMcBEAEBAAEAMgDIARABAQABADYAyQEQAQEAAQA6AMoBEAEBAAEAPgDLARABAQABAEIAzAEQAQEAAQBHAM0BEAEBAAEASwDOARABAQABAFAAYAARAQEAAQCEAGcAEQEBAAMAgACGABEBAQABAGkAhwARAQEAAQBkAIgAEQEBAAEAXwCJABEBAQABAFoAigARAQEAAQBVAIsAEQEBAAEAUQCMABEBAQABAEwAjQARAQEAAQBIAI4AEQEBAAEAQwCPABEBAQABAD8AkAARAQEAAQA7AJEAEQEBAAEANwCSABEBAQABADMAkwARAQEAAQAwAJQAEQEBAAEALACVABEBAQABACkAlgARAQEAAQAmAJcAEQEBAAEAIwCYABEBAQABACAAmQARAQIAAQAcAJsAEQEBAAEAGQCcABEBAQABABYAnQARAQIAAQATAJ8AEQECAAEAEAChABEBAgABAAwAowARAQIAAQAKAKUAEQEDAAEABgCoABEBBAABAAMArAARAQUAAQAAAN0AEQENABUA//9vAREBAgAoAP//iAERAQQADAD//50BEQEOAAEA//+rAREBBQABAAIAsAERAQMAAQAFALMBEQEBAAEABwC0AREBAgABAAoAtgERAQIAAQAMALgBEQECAAEAEAC6AREBAgABABMAvAERAQEAAQAWAL0BEQEBAAEAGQC+AREBAgABABwAwAERAQEAAQAgAMEBEQEBAAEAIwDCAREBAQABACYAwwERAQEAAQApAMQBEQEBAAEALADFAREBAQABADAAxgERAQEAAQAzAMcBEQEBAAEANwDIAREBAQABADsAyQERAQEAAQA/AMoBEQEBAAEAQwDLAREBAQABAEgAzAERAQEAAQBMAGgAEgEBAAcAfgCIABIBAQABAGQAiQASAQEAAQBgAIoAEgEBAAEAWwCLABIBAQABAFYAjAASAQEAAQBRAI0AEgEBAAEATQCOABIBAQABAEkAjwASAQEAAQBEAJAAEgEBAAEAQACRABIBAQABADwAkgASAQEAAQA4AJMAEgEBAAEANQCUABIBAQABADEAlQASAQEAAQAuAJYAEgEBAAEAKgCXABIBAQABACgAmAASAQEAAQAlAJkAEgEBAAEAIgCaABIBAQABAB8AmwASAQIAAQAcAJ0AEgECAAEAFwCfABIBAgABABMAoQASAQIAAQAQAKMAEgECAAEADQClABIBAQABAAsApgASAQIAAQAIAKgAEgEDAAEABgCrABIBBQABAAMAsAASAQEAAgACAHEBEgEXAAwA//+dARIBDAABAAAAqQESAQUAAQADAK4BEgEDAAEABgCxARIBAgABAAgAswESAQIAAQAMALUBEgECAAEADgC3ARIBAgABABIAuQESAQEAAQAUALoBEgECAAEAFwC8ARIBAgABABwAvgESAQEAAQAfAL8BEgEBAAEAIgDAARIBAQABACUAwQESAQEAAQAoAMIBEgEBAAEAKgDDARIBAQABAC4AxAESAQEAAQAxAMUBEgEBAAEANQDGARIBAQABADgAxwESAQEAAQA8AMgBEgEBAAEAQADJARIBAQABAEQAygESAQEAAQBIAGQAEwEBAAMAgQBpABMBAQAHAH0AigATAQEAAQBgAIsAEwEBAAEAWwCMABMBAQABAFcAjQATAQEAAQBSAI4AEwEBAAEATgCPABMBAQABAEkAkAATAQEAAQBFAJEAEwEBAAEAQQCSABMBAQABAD0AkwATAQEAAQA6AJQAEwEBAAEANgCVABMBAQABADIAlgATAQEAAQAvAJcAEwEBAAEALACYABMBAQABACkAmQATAQIAAQAlAJsAEwECAAEAIACdABMBAQABAB0AngATAQEAAQAaAJ8AEwECAAEAFwChABMBAQABABUAogATAQIAAQASAKQAEwEBAAEAEAClABMBAgABAA4ApwATAQIAAQAKAKkAEwECAAEACACrABMBAwABAAYArgATAQIAAQAEALEAEwEIAAEAAQC5ABMBDQAFAP//nQETAQkAAQAAAKYBEwEFAAEAAwCrARMBAwABAAYArgETAQMAAQAJALEBEwECAAEADACzARMBAQABAA4AtAETAQIAAQAQALYBEwEBAAEAEwC3ARMBAgABABYAuQETAQEAAQAYALoBEwEBAAEAGgC7ARMBAQABAB0AvAETAQIAAQAgAL4BEwECAAEAJQDAARMBAQABACkAwQETAQEAAQAsAMIBEwEBAAEALwDDARMBAQABADIAxAETAQEAAQA2AMUBEwEBAAEAOgDGARMBAQABAD0AxwETAQEAAQBBAMgBEwEBAAEARQBlABQBAQADAIAAZwAUAQEABAB+AGoAFAECAAYAfACMABQBAQABAFwAjQAUAQEAAQBXAI4AFAEBAAEAUwCPABQBAQABAE8AkAAUAQEAAQBKAJEAFAEBAAEARgCSABQBAQABAEIAkwAUAQEAAQA+AJQAFAEBAAEAOwCVABQBAQABADcAlgAUAQEAAQA0AJcAFAEBAAEAMQCYABQBAQABAC4AmQAUAQEAAQArAJoAFAEBAAEAKACbABQBAQABACUAnAAUAQEAAQAjAJ0AFAEBAAEAIQCeABQBAQABAB4AnwAUAQIAAQAbAKEAFAECAAEAFwCjABQBAQABABUApAAUAQIAAQASAKYAFAEBAAEAEACnABQBAwABAA0AqgAUAQEAAgAMAKsAFAECAAEACACtABQBBAABAAYAsQAUAQQAAQADALUAFAEEAAIAAQCdARQBCQABAAEApgEUAQQAAQAEAKoBFAECAAEABgCsARQBAgABAAgArgEUAQMAAQAMALEBFAEBAAEADgCyARQBAgABABAAtAEUAQEAAQATALUBFAECAAEAFgC3ARQBAQABABgAuAEUAQIAAQAbALoBFAEBAAEAHgC7ARQBAQABACEAvAEUAQEAAQAjAL0BFAEBAAEAJQC+ARQBAQABACgAvwEUAQEAAQArAMABFAEBAAEALgDBARQBAQABADEAwgEUAQEAAQA0AMMBFAEBAAEANwDEARQBAQABADsAxQEUAQEAAQA+AMYBFAEBAAEAQgBmABUBAQACAH8AbAAVAQEAAwB7AI0AFQEBAAEAXACOABUBAQABAFgAjwAVAQEAAQBUAJAAFQEBAAEATwCRABUBAQABAEsAkgAVAQEAAQBHAJMAFQEBAAEAQwCUABUBAQABAD8AlQAVAQEAAQA8AJYAFQEBAAEAOACXABUBAQABADUAmAAVAQEAAQAyAJkAFQEBAAEALwCaABUBAQABACwAmwAVAQEAAQAqAJwAFQECAAEAJgCeABUBAgABACEAoAAVAQEAAQAeAKEAFQECAAEAGwCjABUBAgABABcApQAVAQEAAQAVAKYAFQECAAEAEgCoABUBAQABABAAqQAVAQEAAQAOAKsAFQECAAEACwCtABUBAgABAAgArwAVAQMAAQAGALIAFQEDAAEABACdARUBBwABAAEApAEVAQQAAQAEAKgBFQECAAEABgCqARUBAwABAAkArQEVAQMAAQANALABFQECAAEAEACyARUBAQABABMAswEVAQIAAQAWALUBFQEBAAEAGAC2ARUBAgABABsAuAEVAQEAAQAeALkBFQECAAEAIQC7ARUBAgABACYAvQEVAQEAAQAqAL4BFQEBAAEALAC/ARUBAQABAC8AwAEVAQEAAQAyAMEBFQEBAAEANQDCARUBAQABADgAwwEVAQEAAQA8AG0AFgEBAAMAegCQABYBAQABAFQAkQAWAQEAAQBQAJIAFgEBAAEATACTABYBAQABAEgAlAAWAQEAAQBEAJUAFgEBAAEAQACWABYBAQABAD0AlwAWAQEAAQA6AJgAFgEBAAEANwCZABYBAQABADQAmgAWAQEAAQAxAJsAFgEBAAEALgCcABYBAQABACsAnQAWAQEAAQApAJ4AFgECAAEAJQCgABYBAgABACEAogAWAQIAAQAcAKQAFgEBAAEAGgClABYBAgABABcApwAWAQIAAQAUAKkAFgECAAEAEACrABYBAgABAA4ArQAWAQIAAQAKAK8AFgECAAEACACxABYBAwABAAYAtAAWAQQAAQADALgAFgEBAAQAAgCdARYBBgABAAEAowEWAQMAAQAEAKYBFgECAAEABgCoARYBAwABAAkAqwEWAQIAAQAMAK0BFgEBAAEADgCuARYBAgABABAAsAEWAQIAAQAUALIBFgECAAEAFwC0ARYBAgABABsAtgEWAQEAAQAdALcBFgECAAEAIQC5ARYBAgABACUAuwEWAQEAAQApALwBFgEBAAEAKwC9ARYBAQABAC4AvgEWAQEAAQAxAL8BFgEBAAEANADAARYBAQABADYAwQEWAQEAAQA6AG4AFwEBAAUAeQCSABcBAQABAFAAkwAXAQEAAQBMAJQAFwEBAAEASQCVABcBAQABAEUAlgAXAQEAAQBBAJcAFwEBAAEAPgCYABcBAQABADsAmQAXAQEAAQA4AJoAFwEBAAEANQCbABcBAQABADIAnAAXAQEAAQAvAJ0AFwEBAAEALQCeABcBAQABACoAnwAXAQEAAQAoAKAAFwEBAAEAJQChABcBAQABACMAogAXAQIAAQAgAKQAFwECAAEAHACmABcBAQABABkApwAXAQIAAQAWAKkAFwECAAEAEwCrABcBAgABABAArQAXAQMAAQAMALAAFwECAAEACACyABcBAwABAAYAtQAXAQMAAgAEAJ0BFwEGAAEAAgCjARcBAwABAAUApgEXAQIAAQAIAKgBFwEBAAEACQCpARcBAwABAAwArAEXAQIAAQAQAK4BFwECAAEAEwCwARcBAgABABYAsgEXAQEAAQAZALMBFwECAAEAHAC1ARcBAgABACAAtwEXAQEAAQAjALgBFwEBAAEAJQC5ARcBAQABACgAugEXAQEAAQAqALsBFwEBAAEALQC8ARcBAQABAC8AvQEXAQEAAQAyAL4BFwEBAAEANQBsABgBAQAEAHoAbwAYAQIABAB4AJQAGAEBAAEATQCVABgBAQABAEkAlgAYAQEAAQBGAJcAGAEBAAEAQgCYABgBAQABAD8AmQAYAQEAAQA8AJoAGAEBAAEAOQCbABgBAQABADYAnAAYAQEAAQAzAJ0AGAEBAAEAMACeABgBAQABAC4AnwAYAQEAAQArAKAAGAECAAEAKACiABgBAQABACQAowAYAQIAAQAhAKUAGAEBAAEAHgCmABgBAgABABsAqAAYAQIAAQAXAKoAGAECAAEAFACsABgBAgABABAArgAYAQIAAQANALAAGAECAAIACwCyABgBAgABAAgAtAAYAQEAAgAGALkAGAEIAAIAAQDBABgBBQAIAP//mwEYAQUAAgABAKABGAEEAAEABACkARgBAgABAAYApgEYAQIAAQAJAKgBGAECAAEADACqARgBAgABAA4ArAEYAQIAAQASAK4BGAEBAAEAFACvARgBAgABABcAsQEYAQIAAQAbALMBGAEBAAEAHgC0ARgBAgABACEAtgEYAQEAAQAkALcBGAECAAEAKAC5ARgBAQABACsAugEYAQEAAQAuAG0AGQEBAAQAeQBxABkBAQAEAHcAlwAZAQEAAQBHAJgAGQEBAAEAQwCZABkBAQABAEAAmgAZAQEAAQA9AJsAGQEBAAEAOgCcABkBAQABADcAnQAZAQEAAQA0AJ4AGQEBAAEAMgCfABkBAQABAC8AoAAZAQEAAQAsAKEAGQEBAAEAKgCiABkBAQABACgAowAZAQEAAQAlAKQAGQEBAAEAIwClABkBAgABACAApwAZAQIAAQAcAKkAGQEBAAEAGQCqABkBAgABABYArAAZAQIAAQASAK4AGQEBAAIAEACvABkBAQABAA4AsgAZAQIAAQAJALUAGQEDAAEABQCgARkBAwACAAQAowEZAQIAAQAGAKUBGQECAAEACQCnARkBAgABAAwAqQEZAQEAAQAOAKoBGQECAAEAEACsARkBAQABABMArQEZAQIAAQAWAK8BGQEBAAEAGQCwARkBAgABABwAsgEZAQIAAQAgALQBGQEBAAEAIwC1ARkBAQABACUAtgEZAQEAAQAoALcBGQEBAAEAKgBqABoBAgABAHoAcgAaAQIAAgB1AJoAGgEBAAEAQQCbABoBAQABAD4AnAAaAQEAAQA7AJ0AGgEBAAEAOACeABoBAQABADUAnwAaAQEAAQAyAKAAGgEBAAEAMAChABoBAQABAC0AogAaAQEAAQArAKMAGgEBAAEAKQCkABoBAgABACUApgAaAQIAAQAhAKgAGgECAAEAHACqABoBAQABABkAqwAaAQIAAQAWAK0AGgEBAAEAEwCvABoBAQACABAAsAAaAQIAAQANALIAGgEBAAMADACzABoBAgACAAkAtQAaAQIAAQAGALcAGgEFAAEAAwC8ABoBBQAEAAEAmwEaAQUAAgACAKMBGgECAAEACAClARoBAQACAAoApgEaAQIAAQAMAKgBGgEBAAIADgCpARoBAgABABAAqwEaAQEAAQATAKwBGgECAAEAFgCuARoBAQABABkArwEaAQIAAQAcALEBGgECAAEAIQCzARoBAQABACQAawAbAQEAAQB6AHQAGwEBAAcAcwCdABsBAQABADwAngAbAQEAAQA5AJ8AGwEBAAEANgCgABsBAgABADIAogAbAQIAAQAtAKQAGwECAAEAKACmABsBAQACACYApwAbAQIAAQAhAKkAGwEBAAEAHgCqABsBAQABABsAqwAbAQEAAQAZAKwAGwECAAEAFgCuABsBAQABABMAsAAbAQIAAQAOALUAGwEBAAQACQC2ABsBAwABAAUAuQAbAQMAAwADAKABGwEDAAEABQCjARsBAQACAAgApAEbAQEAAgAKAKYBGwECAAEADQCpARsBAgABABIAqwEbAQIAAQAWAK0BGwEBAAEAGQBuABwBAwACAHcAcgAcAQIABAB0AHUAHAECAAUAcgChABwBAQABADQAogAcAQEAAQAxAKMAHAEBAAEALwCkABwBAQABACwApQAcAQEAAQAqAKcAHAEBAAIAJgCoABwBAgABACEAqgAcAQEAAQAeAKsAHAEBAAEAGwCsABwBAQABABkArQAcAQIAAQAWAK8AHAECAAEAEgCxABwBAQADABAAswAcAQIAAgALALYAHAEDAAEABgCZARwBBQABAAEAngEcAQMAAQAEAKEBHAECAAEABgClARwBAgABAAwAcQAdAQEAAwB1AHcAHQEBAAQAcAClAB0BAQABACwApgAdAQEAAQAqAKgAHQEBAAEAJQCpAB0BAgABACEAqwAdAQEAAgAeAKwAHQEBAAIAHACtAB0BAQABABkArgAdAQEAAgAXAK8AHQECAAEAEwCyAB0BAQABAA4AtgAdAQEAAwAIALcAHQECAAIABgCIAR0BAQABAP//cAAeAQEAAQB1AHgAHgECAAMAbwCqAB4BAQABACIArQAeAQEAAQAbAK8AHgEBAAEAFgCwAB4BAQABABQAsgAeAQEAAgAQALMAHgECAAIADQC5AB4BBAACAAQAvQAeAQQAAgABAHEBHgEWAAEA//96AB8BAgACAG0AtQAfAQEAAQALALcAHwECAAEACABxAR8BFAABAP//cwAgAQEAAQBzAHwAIAEBAAUAagDEACABAgABAAAAcQEgARIAAQD//3UAIQECAAEAcAB3ACEBAwACAG4AegAhAQIAAgBsAH0AIQECAAQAaABxASEBEAABAP//dgAiAQEAAQBwAH8AIgECAAEAZgBxASIBDQABAP//eAAjAQIAAQBtAHoAIwECAAIAagB/ACMBAwABAGUAggAjAQEAAwBhAHEBIwELAAEA//95ACQBAQABAGwAfwAkAQIAAgBlAIEAJAEBAAIAYwCDACQBAgACAF8AcQEkAQkAAQD//3sAJQEDAAEAaAB+ACUBAQADAGYAhQAlAQIAAgBbAIcAJQEBAAMAWQBxASUBBwABAP//fQAmAQEAAQBnAH8AJgECAAIAYwCBACYBAgADAGAAgwAmAQIAAQBeAIgAJgECAAIAVgDfACYBCwABAP//cQEmAQYAEwD//4MAJwEBAAMAXQCEACcBAgADAFsAhgAnAQEABABZAIoAJwEBAAIAVACLACcBAQAFAFEA5AAnAQYAAQD//4cAKAECAAMAVgCJACgBAQAEAFQAjAAoAQIAAQBQAI4AKAEBAAMATQDrACgBAQABAP//igApAQEABABRAIwAKQECAAEATgCPACkBAgABAEoAkQApAQEABABIAO8AKQF+ABAA//+FACoBAQABAFkAjAAqAQEAAwBOAI0AKgEBAAQATACPACoBAQADAEoAkAAqAQEABABIAJIAKgECAAEARgCUACoBAQAEAEIAiAArAQEAAQBUAI4AKwEBAAMASwCSACsBAQADAEUAkwArAQEABABEAJUAKwECAAEAQACXACsBAQAEAD4AiwAsAQEAAQBPAJUALAEBAAMAQACWACwBAQAEAD4AmAAsAQIAAQA8AJoALAECAAEAOACMAC0BAQABAE0AjwAtAQEAAgBIAJEALQEBAAIARgCYAC0BAQAEADsAmQAtAQIAAQA6AJsALQECAAEANgCdAC0BAQAEADQAngAtAQIAAQAyAJAALgEBAAEARwCSAC4BAQABAEQAlAAuAQEAAgBBAJkALgEBAAQAOQCaAC4BAgADADcAnAAuAQEABAA0AJ4ALgEBAAUAMQCfAC4BAgABADAAoQAuAQIAAQAsAKMALgEBAAUAKQCTAC8BAQABAEIAlQAvAQEAAQA/AJcALwEBAAIAPACfAC8BAQAEADAAoAAvAQIABAAtAKIALwEBAAUAKwCkAC8BAQAEACcApQAvAQIAAgAlAKcALwECAAMAIQCpAC8BAQAEAB4AqgAvAQEABgAcAJYAMAEBAAEAPQCrADABAgABABoArQAwAQIABAAWAK8AMAECAAMAEwCxADABAQAGABAAmgAxAQEAAQA3AJsAMQEBAAEANQCdADEBAQACADIApQAxAQEAAwAlAKYAMQEBAAMAIwCrADEBAQAEABoArAAxAQEABAAYALIAMQECAAIADgC0ADEBAgABAAoAtgAxAQMAAwAHALkAMQECAAYABQCnADIBAgADACAAtAAyAQEABAALALUAMgEBAAQACQC7ADIBBAAFAAMAvwAyAQkABQAAAKMAMwEBAAEAKACkADMBAQABACYAqQAzAQEAAgAdAK8AMwECAAMAEgCyADMBAgADAA0AyAAzARMABQD//60ANAEBAAEAFgCuADQBAQACABQAtgA0AQIAAwAIALgANAEBAAMABgDbADQBFAAFAP//wAA3AQgAAQAAANAAOAELAAEA///vADkBCwABAP//8AA6AQkAAQD///EAOwEHABYA///3AFEBAQABAP//";

    /// <summary>ردم صلب تحت العشب القريب من المسار (نفس ترميز Deck) — حتى لا يسقط
    /// السيارة عند الخروج من المسار إن فشل طبخ شبكات العشب الضخمة أيضاً.</summary>
    const string FillB64 = @"tQAgAB8AKAABAKwAIQAJACgADgDUACEABQAoAAAAgwAiAAEAMgBeAKYAIgAGACgAHwDZACIABQAoAAAAcQEiABMAAgBCAIQBIgAMAAIAZACQASIACAAGAHYAfgAjAAUADQBfAIQAIwABADUAWQCiACMABAAoACYA3gAjABYAKAABAPQAIwAcAAUAIwAQASMAIAAJAA4AMAEjAAgACQAmADgBIwA0AAEATQBsASMABQANADIApAEjAAEAAQAG/3wAJAACACEAXwCFACQAAQAzAFgAngAkAAQAKAAsADgBJAA0AAgAOwBxASQAEwAMADMAhAEkAAwABABaAJgBJAAIAAgAjAB4ACUABAAcAGYAhgAlAAEAMgBYAJsAJQADACgAMQCHACYAAQAwAFgAmAAmAAMAKAA2AHcAJwABABwAbACIACcAAQAtAE8AlQAnAAMAKAA+AHYAKAABABsAawCJACgAAQAsAE8AkgAoAAMAJABAAPQAKAAcAAgAFACEASgADAAEAFAAkAEoAAgACABkAKABKAAMAAQAqgCsASgABAAMAL4AdQApAAEAGwBrAIoAKQABACsATwCQACkAAgAoAEQAdAAqAAEAGwBrAIsAKgABACoAUACOACoAAgAoAEsAjAArAAIAKABMAG0ALAACAB0AdQBxACwAAwAZAHAAEAEsACQAHwABADQBLAA4AAQAJwCEASwADAAEAD8AmAEsAAgACAB6AKABLAAMAAQAoACwASwACAAIANAAuAEsAAEAEADcAG8ALQACABoAcgC5AS0AAQAPANsAbAAuAAEAHAB0AGsALwABABwAegC6AS8AAQANANoAagAwAAEAHAB5AH4AMAAFABwAXQD0ADAAHAAbAAEANAEwAFQABAAeAIgBMAAIAAgAMgCQATAACAAEAFoAoAEwAAgACACMAKgBMAAEAAwAoAC7ATAAAQAQANQAaQAxAAEAHAB5ALwBMQABAA8A3gBmADMAAwAaAHsAvQEzAAEADQDdAGQANAACABwAewA0ATQAVAAEABQAkAE0AAgABABQAJgBNAAIAAQAbgCsATQACAAIALIAtAE0AAQADAC+AL4BNAABAAwA3ABjADUAAQAcAH8AYQA2AAIAHAB+AL8BNgABAAoA2gBgADcAAQAdAH4AwAE3AAEACQDjAF4AOAACAB0AgwA0ATgAWAASAAIAjAE4AAgABAAjAJQBOAAIAAQASwCcATgACAAEAGkApAE4AAQABACCAFwAOQACAB4AgwDBATkAAQALANsAWwA6AAEAHwCGAMIBOgABAAoA2gBaADsAAQAfAIUAjAE8AAQADgAAAJQBPAAMAAQAPACgATwACAAEAF8AqAE8AAQACABuAKwBPAAIAAQAoAC4ATwAAwAIAL4AwwE8AAEADADSAFgAPQACAB4AhADEAT0AAQAHAOAAVwA+AAEAIACJAMUBPgAFAAYA3wBUAD8AAwAgAIkAUwBAAAEAJACOAJABQAAEAAoAAACkAUAABAAEAFoArAFAAAQABACCALABQAAIAAQApQC7AUAABQAEAMQAwAFAAAEACADNAMoBQAABAAgA0gBSAEEAAQAkAI4AeABBAAEAAQBkAHsAQQABAAIAZADLAUIAAQAGAM8AUQBDAAEAIwCNAE8ARAACACQAjgCUAUQAEAAGAAIAtAFEAAgABACbALwBRAAEAAQAtADBAUQAAgAEAMgAxAFEAAYACADAAMwBRAABAAgA4QBMAEUAAwAmAI8AcQBFAAIAAQBuAH0ARQABABcAawBLAEYAAQAqAJIAcQBGAAEAAQBuAHwARgABABcAbADNAUYAAQAGAN8ASQBHAAIAKwCRAG8ARwABAAEAeAB6AEcAAgAXAHEAzgFHAAEABQDeAEgASAABAC0AkAB5AEgAAQAXAHEApAFIABAAAwAWAMoBSAACAAQAvgDPAUgAAQAIANIAdwBJAAIAFwBzANABSQABAAcA2wB1AEoAAgAYAHYANAFKAD0AAQAAAKIBSgACAAEACgDRAUoAAQAGANkARgBLAAIAFQCbAHQASwABABgAdgCsAUsACAABAB4A0gFLAAEABQDWAEQATAACAC8AkwByAEwAAgAYAHoAfgBMAAUADQBqAJIATAADAAQATQCyAUwAEgABAC8AzAFMAAMABADIAGYATQACAAEAeABxAE0AAQAZAHoAtAFNABAAAQAwAGYATgABAAEAeABwAE4AAQAZAHsAtwFOAA0AAQAxANMBTgABAAIA0gBvAE8AAQAZAH0AuwFPAAkAAQA1ANQBTwABAAUA4ABDAFAAAQAYAJ0AZABQAAEAAQCCAG0AUAACABkAfQC+AVAACgABADoA1QFQAAEABADcAGwAUQABABoAfgDBAVEABwABADwA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6AHCAAEABgDfAPEBwgABABoAAACsAMMAAQBKAAAADgHDAAMAKAAAAGwBwwACAAEAVv+AAcMAAgAFAEz/rAHDAAEAAQAAAOkBwwABAAUA3gDfAMQAAQAxAAAACwHEAAMAKAAAACABxAAkABgACAB/AcQAAQAEAEL/iAHEAAQABADi/4wBxAAEAD0AAACYAcQABAARAAAA0gHEAAYABACFANgBxAAIAAQApQDjAcQAAQAIALQA5QHEAAIACADDAO4BxAACAAMA3ADwAcQAAQAaAAAAUQDFAAEAMwCUAK0AxQABAEgAAAAIAcUAAwAoAAAAUAHFAAIAAQDO/34BxQABAAMAQv/qAcUABAADANwABALFAAEABwB4ADgAxgABADsAlwC5AMYACgACAAAA4ADGAAEALwAAAAUBxgADACgAAABQAcYAAQABAM7/fQHGAAEAAgBC/6QBxgAGAAEAAAACAccAAwAoAAAAewHHAAIAAQBH/6QBxwAFAAEAAADuAccAAQABANwArgDIAAEARQAAALoAyAAJAAMAAADhAMgAAQAuAAAAAAHIAAIAKAAAAEQByAAIAAYA+/9MAcgAAwABAOL/egHIAAIABABM/4QByAAIADQAAQCkAcgABAABAAAA0AHIAAQACABaANQByAAIAAQAhwDcAcgABwAEAKQA6AHIAAQACADDAP0AyQADACgAAABMAckAAgABAOL/eAHJAAIAAwBM/6QByQADAAEAAABSAMoAAQAvAJMArwDKAAEAQwAAAPoAygADACgAAABMAcoAAQACAOL/WwHKAAUAAgCM/2ABygABAAIAfv91AcoAAwACAEz/pAHKAAIAAQAAAAMCygABAA4AeAC7AMsACAACAAAA4gDLAAEAKwAAAPQAywAGACgAAABYAcsAAwABAJz/YQHLAAQAAQB8/2cBywABAAEAdP9xAcsABAABAFT/pAHLAAEAAQAAAPEAzAADACgAAABsAcwACAAEAGD/gAHMAAQALgADAM8BzAABAAQAUADUAcwACAAEAHgA3AHMAAgACACMAOQBzAAEAAwAoAAIAswABAABANwAJgDNAAEABACgALAAzQABAEAAAAC8AM0ABwACAAAA4wDNAAEAKQAAAO0AzQAEACgAAAChAc0AAgABAAAACALNAAMAAgDcAFMAzgABAC0AkwDkAM4ACQAoAAAARAHOAAcAAwD8/6EBzgABAAEAAAACAs4AAQAKAHgAsQDPAAEAPwAAAL0AzwAGAAIAAAAIAs8AAgAEAOQAUwHQAAEACwDh/1gB0AAQAAgAEgB0AdAADAAIABwAzgHQAAYABAA/ANQB0AAIAAQAZADoAdAABAAEAKoAOQDRAAEAMQCXAL4A0QAFAAMAAABEAdEABgALAP//nAHRAAMAAQAAAFQA0gABACoAkgCyANIAAQA8AAAAnAHSAAIAAQAAAAEC0gABAAYAeABSAdMAAQAFAOD/nAHTAAEAAQAAAM0B0wABAAUAIgAIAtMAAQALAOsAJwDUAAEACgCqAL8A1AAEAAIAAABoAdQADAAEABQAzgHUAAYABAArANQB1AAIAAQAUADcAdQACAAEAH0A7AHUAAQADAAAADoA1QABADEAmABVANUAAQAoAJIAswDVAAEAOQAAAJgB1QADAAEAAADMAdUAAQAHABMAAALVAAEAAwB4AMAA1gADAAIAAACYAdYAAgABAAAAJgDXAAEABgCqALQA1wABADcAAACYAdcAAQABAAAAVgDYAAEAJgCRAMEA2AACAAIAAABYAdgAKAAEAAkAywHYAAEALQAAAM0B2AAHAAQAFQDUAdgACAAEADwA3AHYAAwABABxAP8B2AABABAAfQAEAtgABAAHANIAOwDZAAEAMACaAJQB2QADAAEAAAC1ANoAAQA0AAAAwgDaAAEAAwAAAJQB2gACAAEAAADKAdoAAQAsAAAAVwDbAAEAJQCRAEoB2wABAAEAAACUAdsAAQABAAAA/gHbAAEADQB+ALYA3AABADIAAAAgAdwAHAABAAAAVAHcACwAAQD//8kB3AABACoAAADMAdwACAAkAAEA1AHcAAgABAAoANwB3AAEAAgAPADgAdwABAAEAGQA6QHcAAMACwAAADwA3QABAC8AmQAgAd0AGAABAAAAVQHdACsAAQD//+gB3QABAA4AAAD9Ad0AAQALAH8AWADeAAEAIwCPAMQA3gADAAEAAAAgAd4AFAABAAAAVgHeACoAAQAAAJQB3gABACgAAADIAd4AAQApAAAAtwDfAAEALwAAAMUA3wACAAIAAAAgAd8AEQABAAAAVwHfACkAAQAAAJUB3wABACkAAAD8Ad8AAQAJAIEABALfAAMABgDaAD0A4AABACwAmQBZAOAAAQAiAI4AIAHgAA4AAQAAAFkB4AAnAAEA//+WAeAAAQApAAAAxgHgAAIAKAAAANQB4AAIAAQAFADgAeAABAAEAEYA7AHgAAMAAgAAALgA4QABAC0AAADGAOEAAQACAAAAIAHhAAsAAQAAAFoB4QAmAAEA//+XAeEAAQArAAAAwwHhAAMAKAAAAFoA4gABACEAjgAgAeIACQABAAAAWwHiACUAAQD//5gB4gABACsAAADBAeIAAgAoAAAA7AHiAAIAAgAAAPsB4gABAAoAdAA+AOMAAQApAJkAIAHjAAYAAQAAAFwB4wAkAAEA//+ZAeMAAQArAAAAvwHjAAIAKAAAAFsA5AABACAAjQC5AOQAAQAqAAAAIAHkAAMAAQAAAF4B5AAiAAEAAACaAeQAAQArAAAAvAHkAAMAKAAAANQB5AAEABkAAADkAeQABAAJAAAA7AHkAAEAAgAAAPoB5AABAAgAcwAAAuQABAAEALQAPwDlAAEAKACaAMgA5QABAAEAAABfAeUAIQABAAAAmwHlAAEAKgAAALkB5QADACgAAAD5AeUAAQAHAHQABALlAAIABQDcAFwA5gABACAAjQC6AOYAAQAoAAAAYQHmAB8AAQAAAJwB5gABACoAAAC1AeYABAAoAAAAYwHnAB0AAQAAAJ0B5wABACkAAACxAecABAAoAAAA6QHnAAIAAgAAAPgB5wABAAUAdgAGAucAAQABANwAQADoAAEAJgCVAF0A6AABAB8AjADKAOgAAQABAAAAPgHoAAoAJwAAAGQB6AAcAAEAAACeAegAAQAoAAAApQHoAAIAKAAAAKwB6AAFACgAAADYAegADAAKAAAA/AHoAAQACACbALsA6QABACUAAAA6AekABAAmAAAASAHpAAQAJgAAAGYB6QAaAAEAAACfAekABgAoAAAApwHpAAUAKAAAAOkB6QABAAEAAAD3AekAAQALAHwAXgDqAAEAHgCMADcB6gADACUAAABMAeoAAwAlAAAAaAHqABgAAQAAAAQC6gABAAMA4wBBAOsAAQAkAJYAXwDrAAEAHgCMAM0A6wABAAEAAAAxAesABgAkAAAATwHrAAIAJAAAAGkB6wAXAAEAAAD2AesAAQAJAHwAQgDsAAEAIwCWALwA7AABACIAAAAuAewAAwAjAAAAUQHsAAIAIwAAAGsB7AAVAAEAAAD1AewAAQAIAH0A+AHsAAQACACRAGAA7QABAB0AiwArAe0AAwAiAAAAUwHtAAIAIgAAAGwB7QAUAAEAAADkAe0AAwABAAAAQwDuAAEAIgCWAL0A7gABACAAAAAoAe4AAwAhAAAAVQHuAAIAIQAAAG4B7gASAAEAAADkAe4AAgABAAAA9AHuAAEABgB/AEQA7wABACMAkQBhAO8AAQAcAIsAJQHvAAMAIAAAAFcB7wADACAAAABwAe8AEAABAAAA5AHvAAEAAgAAAPMB7wABAAkAcgBiAPAAAQAcAIsAIgHwAAMAHwAAAFoB8AACAB8AAABxAfAADwABAAAAAALwAAMAAQDcAEUA8QABACIAkQC+APEAAQAdAAAAHwHxAAMAHgAAAFwB8QABAB4AAABzAfEADQABAAAA8gHxAAEABwB0AAAC8QACAAIA3ABjAPIAAQAbAIoAHAHyAAMAHQAAAF0B8gACAB0AAAB1AfIACwABAAAA2AHyAAsAAQAAAPEB8gABAAYAdQBGAPMAAQAhAJEAZADzAAEAGwCIABkB8wADABwAAABfAfMAAgAcAAAAdgHzAAoAAQAAANgB8wAKAAEAAAAAAvMAAQACAOEARwD0AAEAIQCRAGUA9AABABsAiAC/APQAAQAaAAAAFwH0AAIAGwAAAGEB9AABABsAAAB4AfQACAABAAAA2AH0AAkAAQAAAPAB9AABAAgAggD0AfQACAAEAKAA/AH0AAQAAwDIABQB9QADABoAAABiAfUAAgAaAAAAeQH1AAcAAQAAANgB9QAIAAEAAADvAfUAAQALAHUASAD2AAEAIACOAGYA9gABABoAhwDAAPYAAQAYAAAAEQH2AAMAGQAAAGQB9gACABkAAAB7AfYABQABAAAA2AH2AAcAAQAAAO4B9gABAAoAdgBJAPcAAQAgAI4AZwD3AAEAGgCHAA4B9wADABgAAABmAfcAAQAYAAAAfQH3AAMAAQAAANgB9wAGAAEAAADtAfcAAQAJAHcA/AH3AAMAAwDVAEoA+AABACAAjgBoAPgAAQAaAIQACwH4AAMAFwAAAGcB+AACABcAAAB+AfgAAgABAAAA2AH4AAUAAQAAAOwB+AABAAgAeADxAfgABwAEAJcA+AH4AAQABgDFAEsA+QABACEAkABpAPkAAQAaAIQAwQD5AAEAFQAAAAgB+QADABYAAABpAfkAAQAWAAAA2AH5AAQAAQAAAAUB+gADABUAAABqAfoAAgAVAAAAggH6AAIAAQAAANgB+gADAAEAAADrAfoAAQAKAHYA/AH6AAIAAQDcAEwA+wABACAAjgBqAPsAAQAZAIIAAgH7AAMAFAAAAGwB+wACABQAAACDAfsAAQABAAAA2AH7AAIAAQAAAOoB+wABAAkAdwD8AfsAAQABANwATQD8AAEAIACOAGsA/AABABgAggCGAPwAAQABAAAAwgD8AAEAEgAAAP8A/AADABMAAABuAfwAAQAUAAAAhQH8AAcAAQAAAOkB/AABAAgAeADwAfwACAAEAKoATgD9AAEAHwCPAGwA/QABABgAfgD8AP0AAwASAAAAbwH9AAIAFAAAAIcB/QAFAAEAAADUAf0AAwABAAAA6AH9AAEABwB5AG0A/gABABgAfgDDAP4AAQAQAAAA+QD+AAMAEQAAAHEB/gACABQAAACIAf4ABAABAAAA1AH+AAIAAQAAAOcB/gABAAoAdgD4Af4AAwABANIATwD/AAEAHgCPAG4A/wABABgAfQD4AP8AAQAQAAAAcwH/AAEAEwAAAIoB/wACAAEAAADUAf8AAQABAAAA5gH/AAEACQB3APgB/wACAAEA0gBQAAABAQAdAIwAbwAAAQEAGAB9APYAAAECAA8AAAB0AQABAgASAAAAiwEAAQEAAQAAAMwBAAEHAAEAAADlAQABAQAIAHgA7AEAAQgABACgAPQBAAEEAAMAyAD4AQABAQABAOYAUQABAQEAHQCMAHAAAQEBABcAewDZAAEBAgAMAAMA8wABAQMADgAAAHYBAQECABEAAACPAQEBAQABAAAAzAEBAQYAAQAAAOQBAQEBAAcAeQBSAAIBAQAeAIwAcQACAQEAFwB7ANcAAgECAAgAAgDbAAIBBAANAAUA7gACAQUADQAAAHgBAgEBABAAAADMAQIBBAABAAAA4wECAQEACgBsAFMAAwEBAB8AjAByAAMBAQAXAHoAkQADAQEAAQAAANYAAwEBAAQAAADfAAMBDwAMAAIAeQEDAQEADwAAAJEBAwEDAAEAAADMAQMBAwABAAAA4QEDAQIACQBtAPQBAwEDAAEAyABzAAQBAQAWAHoAxAAEAQEACgAAAHoBBAEBAA4AAACTAQQBAQABAAAAzAEEAQEAAQAAAOABBAEBAAgAbgDoAQQBCAAEAKAA8AEEAQYAAQDPAFQABQEBAB4AiAB0AAUBAQAWAHcAlQAFAQEAAQAAAHsBBQEBAA0AAADfAQUBAQALAGsA8AEFAQUAAgDMAFUABgEBAB0AiAB1AAYBAQAWAHYAxQAGAQEACAAAAHwBBgECAAwAAADeAQYBAQAKAGwAVgAHAQEAHQCIAHYABwECABUAdgB+AQcBAgALAAAA3QEHAQEACQBtAPABBwEDAAEAyABXAAgBAQAcAIgAeAAIAQEAFgByAIABCAEBAAoAAADbAQgBAgAEAF8A5AEIAQQABACMAPABCAECAAEA3ABYAAkBAQAcAIQAeQAJAQEAFQBxAMYACQEBAAQAAACBAQkBAgAJAAAA2gEJAQEABwBgAPABCQEBAAEA3AB6AAoBAgAVAHEA2AAKAQEAAgAAAIMBCgEBAAgAAADYAQoBAgAGAGEAWQALAQEAHACDAHwACwEBABUAbQCEAQsBAgAHAAAA1wELAQEACQBjAFoADAEBAB0AggB9AAwBAQAVAGwAhgEMAQIABgAAANUBDAECAAgAZADbAQwBAgAEAG4A4AEMAQQABACMAOgBDAEEAAIAvgDsAQwBAgABANwAWwANAQEAHACBAH4ADQECABQAbADaAA0BAQABAAAAiAENAQEABAAAANQBDQEBAAcAZQBcAA4BAQAcAH4AgAAOAQEAFABpAIkBDgECAAMAAADSAQ4BAgAGAFcA6AEOAQMAAQC+AF0ADwEBABsAfgCBAA8BAgAUAGgA3AAPARAAAQAAAG0BDwEBAAEAAADQAQ8BAgAFAFgA6AEPAQIAAQC+AF4AEAEBABsAfgCDABABAQAUAGgA3gAQAQwAAQAAAM8BEAEBAAgAVQDYARABBAAIAIIA5AEQAQQAAgC+AOgBEAEBAAEA3ABfABEBAQAbAH4AhAARAQIAEwBgAM0BEQECAAcAVgBgABIBAQAbAHoAhgASAQIAEwBfAMsBEgECAAYATgDkARIBAwABAL4AYQATAQEAGwB5AIgAEwECABMAVwDJARMBAgAJAE8A5AETAQIAAQC+AGIAFAEBABsAeACKABQBAgATAFYAxwEUAQIABABBANABFAEIAAQAcwDcARQBCAACALQA5AEUAQIAAQDcAGMAFQEBABsAeACMABUBAQATAFIAxAEVAQMABwBHAGQAFgEBABoAdgCNABYBAwASAFEAwgEWAQIADgBFANwBFgEHAAEAswBlABcBAgAZAHUAkAAXAQIAEgBJAL8BFwEDAAkAPQDcARcBBgABALEAZwAYAQEAHAByAJIAGAECABIASAC7ARgBBAAIADQAxwEYAQIACABaAMsBGAEFAAQAagDQARgBCAAEAIIA4AEYAQEAAQDmAGgAGQEBABsAcgCUABkBAwARAEIAuAEZAQMACwAzAGkAGgEBABoAcgCXABoBAwARAD4AtAEaAQQAEgAzAGoAGwEBABkAcgCaABsBAwARADkArgEbAQYADQAhAGsAHAECABQAcgB3ABwBAQABAG4AnQAcAQQAEQAxAKcBHAEHAAwAFADEARwBAwAIAF8AyQEcAQcABABvANABHAEIAAQAlgDYARwBBAAGAMEA3AEcAQEAAQDwAG0AHQEBABMAcAChAB0BBAARACsAiQEdAR4ADwADAG4AHgECABIAbwClAB4BBQARACMAhwEeAQIAMQABAHAAHwEBABEAbACqAB8BCAAQABQAhQEfAQIAMgD//3EAIAECABQAaACyACABEgARAAMAgwEgAQIAMwD+/7sBIAEHAAQARwDHASABBQAEAHQAzAEgAQgABACWANQBIAEEAAgAvgBzACEBAQATAGgAfwAhAQEAAQBkAMQAIQEEABEAAACBASEBAgAzAPz/dAAiAQIAEgBnAMgAIgEEABEAAAB+ASIBAwAuAP3/2AEiAQMAAwDPAHYAIwECABEAZwDMACMBBAAQAAAAfAEjAQIALQD8/3gAJAEBABQAXgCFACQBAQABAFoA0AAkAQUADwAAAHoBJAECACgA/f+4ASQBCAAEAEsAwAEkAQgABABuAMgBJAEIAAQAlgDQASQBBAAIAL4AeQAlAQIAEwBeANUAJQEFAA4AAAB4ASUBAgAnAP3/2AElAQIAAQDcAHsAJgECABIAXADaACYBBQANAAAAdwEmAQEAIgD//9gBJgEBAAIA3AB9ACcBAQARAFsA3wAnAQUADQAAAH4AKAEDABAAWQCPACgBAQABAFAA5AAoAQcADAAAAKcBKAEJAAQAIQCwASgBBAAMADwAuAEoAQgABABaAMABKAEIAAQAggDIASgBCAAEAKoA1AEoAQQAAQDcAIEAKQECAA8AVwDrACkBBAALAAAA1AEpAQMAAQDcAIMAKgECAA4AVACVACoBAgABADwA1AEqAQIAAQDcAIUAKwEDAA0AUQCYACsBAgABADwAiAAsAQIAEABIAJwALAEBAAEAMgCJASwBGwAQAAUApAEsAQwABAAoALQBLAEIAAQAUAC8ASwBCAAEAIIAxAEsAQgABACqAMwBLAEHAAEA0QCKAC0BAgAPAEgAoAAtAQEAAQAoAMwBLQEFAAEAzACMAC4BAwAOAEMApAAuAQEAAQAeAMwBLgEDAAIAyACPAC8BBAANAD4AqwAvAQcAAQATAGQAMAEDAAEAZABrADABBgAIAFcAkwAwAQMADAA8AKQBMAEIAAwAIwCsATABBAAQAEEAtAEwAQgABABaALwBMAEEAAwAjADAATABBAAJAKIAxAEwAQgAAQDIAGUAMQECAAEAZACWADEBAwALADkAuwAxAQkAAQAAAMQBMQEHAAMAxwBmADIBAQABAGQAmQAyAQQADgArAJ0AMwEFAA0AJQDbADMBBAABAAAAaAA0AQMAAQBaAHEANAEHAAgASwCiADQBBQAMACEAsAE0AQgACABYALgBNAEEAAwAeADEATQBBAADAL4AyAE0AQIAAQDcAGkANQECAAIAWgCnADUBBwALABUAyAE1AQEAAQDcAK4ANgEIAAoADABqADcBAQABAFoAtgA3AQoADQD+/8QBNwEDAAEAvgBrADgBBgABAEkAeAA4ARAABAA8AMAAOAEQABAA/f9sADkBBQADAEYA0AA5AR8AEwD9//oAOQF9AA8A/f/AATkBAwABALQA7wA6AQEAFwAAAPkAOgEBABkAAADAAToBAgACALQA8AA7AQEAFgAAAPgAOwEBABcAAABvADwBBAABAEYAeAA8AQQAAQA8AIAAPAEZAAQAKQCJATwBEwAEAAYAnAE8AQwABAAhAKgBPAEEAAgAQQCwATwBCAAEAGQAvAE8AQQAAgCgAMABPAEBAAEAvgC8AT4BAwACAKAAfwBAAQEAEADUAIgAQAEMAAQAFACUAEABIgAEAP//iQFAAQ8ABQAJAJgBQAEMAAQAKACkAUABBAAEADwArAFAAQgAAwBkALQBQAEHAAEAiwB+AEEBAQAPANQAtAFBAQQAAQCCAKwBQwECAAEAWgCMAEQBFAAEAPD/oABEASAABADi/5gBRAEMAAEANQCkAUQBAwABAEYAiQFFAQkAAQAKAIkBRgEIAAEACgCJAUcBBwABAAoAkABIAQQABADi/6AASAEkAAQAzf/EAEgBDAAEAOn/+gBIASoACADz/yQBSAFUAAgA3v+JAUgBBgABAA8AiQFJAQUAAQAOAIkBSgEEAAEADAB9AEsBAQAEANIAiQFLAQMAAQAKAJgATAEEAAwAbgCcAEwBBAAIAIwArABMAQQABACw/7QATAEUAAEAyv/KAEwBHgABAOv/6ABMAQcABQAAAHgBTAEEAAwAzv+JAUwBAgABAAoAtABNAQIAAQDE/7kATQENAAEAyv/PAE0BGQABAO3/iQFNAQEAAQAKANIATgEWAAEA7//UAE8BAQABAOL/2wBPAQ0AAQD1/4cBTwEBAAEA9v+BAFABAwABAJYAoABQAQgABACqALAAUAEBAAEA5gDcAFABBQABAOz/4wBQAQUAAQD0//oAUAEOAAIA+v8UAVABCAAEAOz/IAFQARQABADk/zgBUAFAAAEAzP98AVABBQAEAOT/gwBRAQEAAQCWAOcAUQEBAAEA9v84AVEBDQABANj/SwFRAS0AAQDI/4UBUQEBAAEA9v/6AFIBDAABAPv/OAFSAQwAAQDY/1EBUgEnAAEAx//6AFMBCAABAP7/WgFTAQYAAQDH/2IBUwEWAAEAxP+DAVMBAQABAOz/igBUAQIAAQBQAIwAVAEEAAIAKACUAFQBBAAEAFAAnABUAQQABgCgAKAAVAEMAAEAywD8AFQBBAABAAAANAFUAQIAAQDSAGUBVAELAAEAxP+gAFUBCwABAMsA/QBVAQIAAQAAADQBVQEBAAMA0gBvAVUBAQABAMT/jwBWAQEAAQAoAKAAVgEKAAIAygCWAFgBBgABAHsAoABYAQgAAwDIAKgAWAECAAEA5gAKAVgBAgABANIAcwFYAQUAAgDeAHwBWAEDAAEA0gCZAFkBAwABAIIAqABZAQEAAgDmAAsBWQEBAAMA0gB8AVkBAgABANIAnQBaAQMAAQCgAHQBWgEBAAEA3AB8AVoBAQABANIAogBbAQUAAQDKAAsBXAEBAAEA8AAUAVwBDAAEAIUALAFcAQcAAQDRACwBXQEGAAEAzwAsAV4BBAABAMgALAFfAQIAAQDIAAwBYAEEAAIAyAAQAWABEAABAJgAIAFgAQgAAQDDACgBYAEEAAEA3AANAWIBAgABAMgADQFjAQEAAQDIAOsAaQEBAAEA3v4=";

    const float DeckOrigin = -600f;
    const float DeckCell = 2f;

    /// <summary>وسم مصدر خط السباق للطبقة التشخيصية (BAKED / SCAN:n / SCAN-FAIL).</summary>
    public static string LineSourceTag = "-";

    // (v2.5.0) سجلات الأسطح المخبوزة لممرّض التنسيق — القضاء على «الجدران غير المرئية»:
    // ثلاث طبقات مصادمات متراكبة (شبكة الأرض + السطح + الردم) حيثما تتقاطع
    // بفروق سنتيمترات تخلق جدراناً رأسية غير مرئية ومصائد تماسك عشب فوق الطريق نفسه
    struct BakedRect { public float x0, z0, w, h, top; public Collider col; }
    readonly List<BakedRect> bakedDeckRects = new List<BakedRect>();
    readonly List<BakedRect> bakedSoftRects = new List<BakedRect>();   // الشبكة + الردم

    /// <summary>v2.2.0 — استبدال خط السباق بالخط المخبوز المثبت هندسياً.
    /// يلغي الاعتماد على ScanOvalRoad الذي فشل دائماً على الأجهزة فورثُ المسار
    /// الإجرائي الاحتياطي (مستطيل 14..134 × 12..76 فوق منطقة الجراجات!)
    /// ولهذا بدا الانطلاق خاطئاً وكانت بوابات اللفات ميتة والإنقاذ يجر السيارة
    /// إلى منطقة الجراجات. false عند فك تالف → المسح الاحتياطي كما كان.</summary>
    /// <summary>فك الخط المخبوز (v3.0.0: مشترك بين خط السباق وحزام الأسطح).</summary>
    static bool TryDecodeLinePoints(out Vector3[] pts)
    {
        pts = null;
        byte[] d;
        try { d = System.Convert.FromBase64String(BakedLineB64); }
        catch (System.FormatException) { return false; }

        int n = d.Length / 4;
        if (n < 12) return false;

        pts = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float x = System.BitConverter.ToInt16(d, i * 4) * 0.1f;
            float z = System.BitConverter.ToInt16(d, i * 4 + 2) * 0.1f;
            pts[i] = new Vector3(x, 0f, z);
        }
        return true;
    }

    bool TryBuildBakedLine()
    {
        if (!TryDecodeLinePoints(out Vector3[] pts)) return false;

        RacingLine = pts;
        // نحفظ نتيجة المسح في الوسم للتشخيص (BAKED+SCAN:n يعني الخط المخبوز والمسح نجح)
        LineSourceTag = LineSourceTag.StartsWith("SCAN") ? "BAKED+" + LineSourceTag : "BAKED";
        Debug.Log($"[TrackBuilder] v2.2.0: خط السباق المخبوز ({pts.Length} نقطة) — مستقل عن مسح وقت التشغيل");
        return true;
    }

    /// <summary>v2.2.0 — سطح الطريق الصلب + الردم: BoxColliders بلا أي طبخ
    /// (MechColliders المقعّرة كانت تفشل على الأجهزة). الاسم يحوي TARMAC للسطح
    /// (تماسك إسفلت كامل) وGRASS للردم (تماسك عشب) ليصنفهما MotionCoreSurface.</summary>
    void BuildRoadDeck()
    {
        // (v2.5.0) رفع سطح الطريق +0.09م: كان مخبوزاً على ارتفاع السطح الحقيقي −0.05..0.075
        // فيتصاعد المرور بين سطحين متوازيين (الشبكة الأصلية حيث تطبخ وبلاطات السطح حيث
        // لا تطبخ) = قفزات ارتطام عند كل حدود مناطق الطبخ. الرفع يجعل بلاطاتنا أعلى من
        // الشبكة الأصلية بـ 1.5..4سم = سطح قيادة وحيد متجانس فوق كل الحلبة
        int deck = BuildDeckFromBlob(DeckB64, "RoadDeck_TARMAC", 0.09f, true);
        int fill = BuildDeckFromBlob(FillB64, "FillNet_GRASS", 0f, false);
        Debug.Log($"[TrackBuilder] v2.2.0: سطح الطريق الصلب {deck} مستطيل + ردم عشب {fill} مستطيل");
        HarmonizeBakedSurfaces();
    }

    int BuildDeckFromBlob(string b64, string goName, float topOffset, bool isDeck)
    {
        byte[] data;
        try { data = System.Convert.FromBase64String(b64); }
        catch (System.FormatException)
        {
            Debug.LogWarning("[TrackBuilder] v2.2.0: blob تالف — تخطي " + goName);
            return 0;
        }

        int recCount = data.Length / 10;
        if (recCount == 0) return 0;

        GameObject go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        go.layer = TrackLayer;

        for (int r = 0; r < recCount; r++)
        {
            int cx = System.BitConverter.ToInt16(data, r * 10);
            int cz = System.BitConverter.ToInt16(data, r * 10 + 2);
            int w2 = System.BitConverter.ToInt16(data, r * 10 + 4);
            int h2 = System.BitConverter.ToInt16(data, r * 10 + 6);
            float y = System.BitConverter.ToInt16(data, r * 10 + 8) * 0.05f + topOffset;

            float x0 = DeckOrigin + cx * DeckCell;
            float z0 = DeckOrigin + cz * DeckCell;
            float w = w2 * DeckCell;
            float h = h2 * DeckCell;

            // صندوق سميك 30م (القمة عند y بالضبط) — لا نفاذ عند السرعة
            BoxCollider col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(x0 + w * 0.5f, y - 15f, z0 + h * 0.5f);
            col.size = new Vector3(w, 30f, h);

            // (v2.5.0) تسجيل المستطيل لممرّض التنسيق: السطح إلى قائمة الأرضية،
            // والردم إلى قائمة «اللينة» (المرشّحة للإزالة/التخفيض قرب الطريق)
            BakedRect br = new BakedRect { x0 = x0, z0 = z0, w = w, h = h, top = y, col = col };
            if (isDeck) bakedDeckRects.Add(br);
            else bakedSoftRects.Add(br);
        }
        return recCount;
    }

    /// <summary>
    /// (v2.5.0) ممرّض تنسيق الأسطح المخبوزة — إبقاء الطريق سطحاً واحداً بلا مفاجآت:
    /// 1) أي مصادم ردم/شبكة يتقاطع أفقياً مع بلاطة طريق يُستبدل (السطح صاحب القرار
    ///    في ممر القيادة — يمنع مصائد تماسك العشب فوق الإسفلت والجدران بين الطبقات).
    /// 2) أي مصادم قريب من الطريق (±4م) قمته أعلى من حافة الطريق يُخفَّض إلى
    ///    حافة الطريق −6سم حتى يصبح الدخول/الخروج من العشب تدحرجاً لا جداراً.
    /// </summary>
    void HarmonizeBakedSurfaces()
    {
        if (bakedDeckRects.Count == 0) return;

        // شبكة خلايا 2م من إحداثيات بلاطات الطريق (نفس محور شبكة blobs)
        var deckCells = new Dictionary<long, float>();
        foreach (BakedRect r in bakedDeckRects)
        {
            int ix0 = Mathf.FloorToInt(r.x0 / DeckCell);
            int ix1 = Mathf.FloorToInt((r.x0 + r.w - 0.01f) / DeckCell);
            int iz0 = Mathf.FloorToInt(r.z0 / DeckCell);
            int iz1 = Mathf.FloorToInt((r.z0 + r.h - 0.01f) / DeckCell);
            for (int ix = ix0; ix <= ix1; ix++)
            {
                for (int iz = iz0; iz <= iz1; iz++)
                {
                    long k = (long)ix * 1000003L + iz;
                    if (!deckCells.TryGetValue(k, out float t) || r.top < t)
                        deckCells[k] = r.top;
                }
            }
        }

        int removed = 0, lowered = 0;
        foreach (BakedRect rec in bakedSoftRects)
        {
            if (rec.col == null) continue;

            int rx0 = Mathf.FloorToInt(rec.x0 / DeckCell);
            int rx1 = Mathf.FloorToInt((rec.x0 + rec.w - 0.01f) / DeckCell);
            int rz0 = Mathf.FloorToInt(rec.z0 / DeckCell);
            int rz1 = Mathf.FloorToInt((rec.z0 + rec.h - 0.01f) / DeckCell);

            bool overlaps = false;
            float nearMinTop = float.MaxValue;
            for (int ix = rx0 - 2; ix <= rx1 + 2 && !overlaps; ix++)
            {
                for (int iz = rz0 - 2; iz <= rz1 + 2; iz++)
                {
                    if (!deckCells.TryGetValue((long)ix * 1000003L + iz, out float t)) continue;
                    bool inside = ix >= rx0 && ix <= rx1 && iz >= rz0 && iz <= rz1;
                    if (inside) { overlaps = true; break; }
                    if (t < nearMinTop) nearMinTop = t;
                }
            }

            if (overlaps)
            {
                Destroy(rec.col);
                removed++;
            }
            else if (nearMinTop < float.MaxValue && rec.top > nearMinTop - 0.06f)
            {
                float newTop = nearMinTop - 0.06f;
                BoxCollider b = rec.col as BoxCollider;
                if (b != null)
                {
                    b.center = new Vector3(b.center.x, newTop - b.size.y * 0.5f, b.center.z);
                    lowered++;
                }
            }
        }

        Physics.SyncTransforms();
        Debug.Log($"[TrackBuilder] v2.5.0: تنسيق الأسطح — أُزيل {removed} مصادماً فوق ممر الطريق، خُفّضت {lowered} حافة قريبة");
    }

    // ==================================================================
    // ==== v3.0.0: الأسطح المصقولة — حقل ارتفاع بصناديق مائلة ====
    // ==================================================================
    //
    // بلاغ ميداني بعد v2.9.0: «السيارة تنزلق بشدة وفي المنعطفات تصتضم بأشياء
    // غير مرئية». التشخيص الجنائي للبلوبات المخبوزة (محاكاة Python على 32 ألف
    // خلية) كشف الجذر: الحلبة البيضاوية أصل مصرفي (Banked Oval) — في المنعطفات
    // الأربعة يرتفع الطريق من ~0م إلى ~16م عبر منحدر جانبي يصل 34 درجة، وسطح
    // الطريق الصلب (v2.2.0) كان يترجم هذا المنحدر إلى صناديق أفقية مسطّحة 2×2م
    // = درج غير مرئي من 25سم حتى 150سم بين كل خليتين (20% من أزواج الخلايا
    // حول خط السباق تتجاوز 25سم). السيارة ترى إسفلتاً منحنياً ناعماً (شبكة
    // الأصل المرئية بلا مصادم منذ v2.9.0) وتصطدم جسدياً بسلّم غير مرئي عند
    // كل خلية — انطلاق في الهواء، هبوط مائل، انزلاق، والشعور بأن الفيزياء
    // «منعدمة». المستقيمات مسطحة (-0.01م) لذلك كانت سليمة تماماً.
    //
    // الحل (متحقق منه عددياً قبل الكتابة): داخل حزام حول خط السباق يُستبدل
    // الدرج بحقل ارتفاع متصل C0 — لكل خلية صندوق مائل (BoxCollider بدوران)
    // سطحُه العلوي يوازي المستوى الأنسب لارتفاعات عقد زواياها الأربعة،
    // والعقد نفسها ناتجة عن متوسطات الخلايا المجاورة + تمريرتا تنعيم
    // لابلاسي. النتيجة المقاسة: أقصى درجة بين خليتين متجاورتين 16.5سم
    // (كانت 150سم) وأقصى تغير ارتفاع لكل 2م على الخط 16.4سم — أرض مصرفية
    // حقيقية تُقود كمضمار، مع بقاء الدرج القديم احتياطاً عند فشل فك البلوبات.

    /// <summary>حجم خلية الشبكة الفيزيائية (نفس شبكة البلوبات المخبوزة).</summary>
    const float BandCell = 2f;
    /// <summary>نصف قطر حزام الطريق حول خط السباق بالخلايا (12 × 2م = 24م).</summary>
    const int BandRadiusCells = 12;
    /// <summary>فرق الارتفاع بين خليتين متجاورتين يوسّع الحزام تلقائياً فوق
    /// «الجدار المصرفي» أينما امتد — لا درج خارج الحزام أبداً.</summary>
    const float WallStepM = 0.06f;
    /// <summary>عمق مرجع العشب حول حزام الطريق بالخلايا (5 × 2م = 10م).</summary>
    const int ApronDepthCells = 5;
    /// <summary>رفع سطح الطريق فوق الارتفاع المخبوز (منذ v2.5.0).</summary>
    const float DeckTopLift = 0.09f;
    /// <summary>تسامح الخلية المسطحة — تُدمج في صناديق أفقية بدل المائلة.</summary>
    const float FlatTolerance = 0.025f;
    /// <summary>تمريرات تنعيم لابلاسي على عقد الشبكة.</summary>
    const int NodeSmoothPasses = 2;

    /// <summary>خلية في الحزام: نوع السطح + ارتفاعه النهائي.</summary>
    struct BandCell { public bool Tarmac; public float Top; }

    /// <summary>مستطيل مخبوز مفكوك (بوحدات عالمية، Y = قمة السطح).</summary>
    struct BakedRectY { public float X0, Z0, W, H, Y; }

    static long BandKey(int ix, int iz) => (long)ix * 1000003L + iz;

    /// <summary>فك بلوب مخبوز إلى قائمة مستطيلات عالمية + خريطة خلايا (أدنى قمة).</summary>
    static bool TryDecodeBaked(string b64, float originXZ, float yScale, float lift,
        List<BakedRectY> rects, Dictionary<long, float> cells)
    {
        byte[] data;
        try { data = System.Convert.FromBase64String(b64); }
        catch (System.FormatException) { return false; }
        int n = data.Length / 10;
        if (n == 0) return false;

        for (int r = 0; r < n; r++)
        {
            int cx = System.BitConverter.ToInt16(data, r * 10);
            int cz = System.BitConverter.ToInt16(data, r * 10 + 2);
            int w2 = System.BitConverter.ToInt16(data, r * 10 + 4);
            int h2 = System.BitConverter.ToInt16(data, r * 10 + 6);
            float y = System.BitConverter.ToInt16(data, r * 10 + 8) * yScale + lift;

            BakedRectY rc;
            rc.X0 = originXZ + cx * BandCell;
            rc.Z0 = originXZ + cz * BandCell;
            rc.W = w2 * BandCell;
            rc.H = h2 * BandCell;
            rc.Y = y;
            rects.Add(rc);

            int ix0 = Mathf.FloorToInt(rc.X0 / BandCell);
            int ix1 = Mathf.FloorToInt((rc.X0 + rc.W - 0.01f) / BandCell);
            int iz0 = Mathf.FloorToInt(rc.Z0 / BandCell);
            int iz1 = Mathf.FloorToInt((rc.Z0 + rc.H - 0.01f) / BandCell);
            for (int ix = ix0; ix <= ix1; ix++)
                for (int iz = iz0; iz <= iz1; iz++)
                {
                    long k = BandKey(ix, iz);
                    if (!cells.TryGetValue(k, out float t) || y < t) cells[k] = y;
                }
        }
        return true;
    }

    /// <summary>
    /// v3.0.0 — البناء الرئيسي: حزام مائل متصل حول خط السباق + العالم الخارجي
    /// بالمستطيلات المدمجة (بقواعد التنسيق القديمة). false عند فشل فك الخط أو
    /// البلوبات فيستدعي المسار القديم الكامل (شبكة + ديك + تنسيق).
    /// </summary>
    bool TryBuildSmoothBandSurfaces()
    {
        // 0) خط السباق المخبوز أولاً — الحزام نسبة إليه
        if (!TryDecodeLinePoints(out Vector3[] line) || line.Length < 12) return false;

        // 1) فك البلوبات الثلاثة
        var deckRects = new List<BakedRectY>(8085);
        var grndRects = new List<BakedRectY>(2572);
        var fillRects = new List<BakedRectY>(1709);
        var deckCells = new Dictionary<long, float>(34000);
        var grndCells = new Dictionary<long, float>(50000);
        var fillCells = new Dictionary<long, float>(90000);
        if (!TryDecodeBaked(DeckB64, -600f, 0.05f, DeckTopLift, deckRects, deckCells))
            return false;
        TryDecodeBaked(GroundNetB64, -620f, 0.1f, 0f, grndRects, grndCells);
        TryDecodeBaked(FillB64, -600f, 0.05f, 0f, fillRects, fillCells);

        // 2) كثافة الخط: عينة كل 2م (لدبابيس القرص)
        var dense = new List<Vector2>(1100);
        int ln = line.Length;
        for (int i = 0; i < ln; i++)
        {
            Vector2 a = new Vector2(line[i].x, line[i].z);
            Vector2 b = new Vector2(line[(i + 1) % ln].x, line[(i + 1) % ln].z);
            float L = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.FloorToInt(L / BandCell));
            for (int s = 0; s < steps; s++)
                dense.Add(Vector2.Lerp(a, b, (float)s / steps));
        }

        // 3) قرص حزام الطريق + توسيع تلقائي فوق الجدار المصرفي
        var bandDeck = new HashSet<long>();
        foreach (Vector2 p in dense)
        {
            int cix = Mathf.FloorToInt(p.x / BandCell);
            int ciz = Mathf.FloorToInt(p.y / BandCell);
            for (int dix = -BandRadiusCells; dix <= BandRadiusCells; dix++)
                for (int diz = -BandRadiusCells; diz <= BandRadiusCells; diz++)
                {
                    if (dix * dix + diz * diz > BandRadiusCells * BandRadiusCells) continue;
                    long k = BandKey(cix + dix, ciz + diz);
                    if (deckCells.ContainsKey(k)) bandDeck.Add(k);
                }
        }
        // جدران متصلة بالحزام (فرق ارتفاع > 6سم بين جارين) تدخل الحزام كله
        // حتى لا يقطع الحزامُ المنحدرَ بخطوة حدّية كبيرة.
        var wallQueue = new Queue<long>(bandDeck);
        while (wallQueue.Count > 0)
        {
            long k = wallQueue.Dequeue();
            int ix = (int)(k / 1000003L), iz = (int)(k - ix * 1000003L);
            float h0 = deckCells[k];
            TryExtendWall(ix + 1, iz, h0, deckCells, bandDeck, wallQueue);
            TryExtendWall(ix - 1, iz, h0, deckCells, bandDeck, wallQueue);
            TryExtendWall(ix, iz + 1, h0, deckCells, bandDeck, wallQueue);
            TryExtendWall(ix, iz - 1, h0, deckCells, bandDeck, wallQueue);
        }

        // 4) مرجع العشب: BFS بعمق 5 خلايا من حافة الحزام فوق خلايا العشب/الردم
        var apron = new HashSet<long>();
        var apronSeen = new HashSet<long>(bandDeck);
        var apronQueue = new Queue<System.Collections.Generic.KeyValuePair<long, int>>();
        foreach (long k in bandDeck)
        {
            int ix = (int)(k / 1000003L), iz = (int)(k - ix * 1000003L);
            EnqueueApron(ix + 1, iz, 1, grndCells, fillCells, apronSeen, apronQueue);
            EnqueueApron(ix - 1, iz, 1, grndCells, fillCells, apronSeen, apronQueue);
            EnqueueApron(ix, iz + 1, 1, grndCells, fillCells, apronSeen, apronQueue);
            EnqueueApron(ix, iz - 1, 1, grndCells, fillCells, apronSeen, apronQueue);
        }
        while (apronQueue.Count > 0)
        {
            var kv = apronQueue.Dequeue();
            if (kv.Value > ApronDepthCells) continue;
            long k = kv.Key;
            if (deckCells.ContainsKey(k)) continue;
            if (!grndCells.ContainsKey(k) && !fillCells.ContainsKey(k)) continue;
            apron.Add(k);
            int ix = (int)(k / 1000003L), iz = (int)(k - ix * 1000003L);
            EnqueueApron(ix + 1, iz, kv.Value + 1, grndCells, fillCells, apronSeen, apronQueue);
            EnqueueApron(ix - 1, iz, kv.Value + 1, grndCells, fillCells, apronSeen, apronQueue);
            EnqueueApron(ix, iz + 1, kv.Value + 1, grndCells, fillCells, apronSeen, apronQueue);
            EnqueueApron(ix, iz - 1, kv.Value + 1, grndCells, fillCells, apronSeen, apronQueue);
        }

        // 5) الارتفاعات النهائية: الطريق كما هو، المرجع يُمسك بحافة الطريق
        var tops = new Dictionary<long, BandCell>(bandDeck.Count + apron.Count);
        foreach (long k in bandDeck)
            tops[k] = new BandCell { Tarmac = true, Top = deckCells[k] };
        foreach (long k in apron)
        {
            // أدنى قمة طريق ضمن 4 خلايا (8م) — المرجع يلتصق بها من الأسفل
            int ix = (int)(k / 1000003L), iz = (int)(k - ix * 1000003L);
            float nearDeck = float.MaxValue;
            for (int r = 1; r <= 4 && nearDeck == float.MaxValue; r++)
                for (int dx = -r; dx <= r && nearDeck == float.MaxValue; dx++)
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                        if (deckCells.TryGetValue(BandKey(ix + dx, iz + dz), out float t) && t < nearDeck)
                            nearDeck = t;
                    }
            float src = grndCells.TryGetValue(k, out float g) ? g
                      : fillCells.TryGetValue(k, out float f2) ? f2 : 0f;
            if (nearDeck < float.MaxValue &&
                (src > nearDeck - 0.06f || Mathf.Abs(src - nearDeck) > 1f))
                src = nearDeck - 0.06f;
            tops[k] = new BandCell { Tarmac = false, Top = src };
        }

        // 6) عقد الشبكة (زوايا الخلايا) = متوسط الخلايا المجاورة + تنعيم لابلاسي
        var nodes = new Dictionary<long, float>();
        foreach (long k in tops.Keys)
        {
            int ix = (int)(k / 1000003L), iz = (int)(k - ix * 1000003L);
            nodes[BandKey(ix, iz)] = float.MinValue;
            nodes[BandKey(ix + 1, iz)] = float.MinValue;
            nodes[BandKey(ix, iz + 1)] = float.MinValue;
            nodes[BandKey(ix + 1, iz + 1)] = float.MinValue;
        }
        var nodeKeys = new List<long>(nodes.Keys);
        foreach (long k in nodeKeys)
        {
            int ix = (int)(k / 1000003L), iz = (int)(k - ix * 1000003L);
            float acc = 0f; int cnt = 0;
            AccumulateCell(tops, ix - 1, iz - 1, ref acc, ref cnt);
            AccumulateCell(tops, ix,     iz - 1, ref acc, ref cnt);
            AccumulateCell(tops, ix - 1, iz,     ref acc, ref cnt);
            AccumulateCell(tops, ix,     iz,     ref acc, ref cnt);
            nodes[k] = cnt > 0 ? acc / cnt : float.MinValue;
        }
        for (int pass = 0; pass < NodeSmoothPasses; pass++)
        {
            var snapshot = new Dictionary<long, float>(nodes);
            foreach (long k in nodeKeys)
            {
                if (!snapshot.TryGetValue(k, out float h) || h == float.MinValue) continue;
                int ix = (int)(k / 1000003L), iz = (int)(k - ix * 1000003L);
                float acc = 0f; int cnt = 0;
                AccumulateNode(snapshot, ix + 1, iz, ref acc, ref cnt);
                AccumulateNode(snapshot, ix - 1, iz, ref acc, ref cnt);
                AccumulateNode(snapshot, ix, iz + 1, ref acc, ref cnt);
                AccumulateNode(snapshot, ix, iz - 1, ref acc, ref cnt);
                if (cnt > 0) nodes[k] = 0.35f * h + 0.65f * (acc / cnt);
            }
        }

        // 7) إصدار الحزام: خلايا مسطحة تُدمج صفّياً، والمائلة صناديق مائلة
        var bandDeckGo = new GameObject("BandDeck_TARMAC");
        bandDeckGo.transform.SetParent(transform, false);
        bandDeckGo.layer = TrackLayer;
        bandDeckGo.isStatic = true;
        var bandGrassGo = new GameObject("BandApron_GRASS");
        bandGrassGo.transform.SetParent(transform, false);
        bandGrassGo.layer = TrackLayer;
        bandGrassGo.isStatic = true;

        int tiltedT = 0, tiltedG = 0, flatRuns = 0;
        var rowsT = new Dictionary<int, List<int>>();
        var rowsG = new Dictionary<int, List<int>>();
        foreach (var kv in tops)
        {
            int ix = (int)(kv.Key / 1000003L), iz = (int)(kv.Key - ix * 1000003L);
            AddRow(kv.Value.Tarmac ? rowsT : rowsG, iz, ix);
        }
        flatRuns += EmitBandRows(rowsT, tops, nodes, bandDeckGo.transform, "BandTarmac", true, ref tiltedT);
        flatRuns += EmitBandRows(rowsG, tops, nodes, bandGrassGo.transform, "BandGrass", false, ref tiltedG);

        // 8) خارج الحزام: طريق مدمج/صفّي + ردم مقصوص + عشب بقواعد v2.5.0
        GameObject outDeckGo = new GameObject("DeckFill_TARMAC");
        outDeckGo.transform.SetParent(transform, false);
        outDeckGo.layer = TrackLayer;
        outDeckGo.isStatic = true;

        int deckMerged = 0, deckRuns = 0;
        foreach (BakedRectY rc in deckRects)
        {
            bool intersects = RectTouchesBand(rc, tops);
            if (!intersects)
            {
                EmitFlatBox(outDeckGo.transform, "DeckTarmac", rc.X0, rc.Z0, rc.W, rc.H, rc.Y);
                deckMerged++;
            }
            else
            {
                deckRuns += EmitRowRuns(rc, tops, outDeckGo.transform, "DeckTarmac", 1);
            }
        }

        int fillMerged = 0, fillRuns = 0, fillClamped = 0;
        foreach (BakedRectY rc in fillRects)
        {
            // سقف القص: أدنى قمة طريق ضمن 24م من مركز المستطيل
            float clampTop = float.MaxValue;
            {
                int cix = Mathf.FloorToInt((rc.X0 + rc.W * 0.5f) / BandCell);
                int ciz = Mathf.FloorToInt((rc.Z0 + rc.H * 0.5f) / BandCell);
                for (int dx = -12; dx <= 12; dx++)
                    for (int dz = -12; dz <= 12; dz++)
                        if (deckCells.TryGetValue(BandKey(cix + dx, ciz + dz), out float t) && t < clampTop)
                            clampTop = t;
            }
            float y = rc.Y;
            if (clampTop < float.MaxValue && y > clampTop + 0.10f)
            {
                y = clampTop - 0.06f;   // جدار ردم غير مرئي يُسطّح إلى مرجع
                fillClamped++;
            }

            if (!RectTouchesBand(rc, tops))
            {
                EmitFlatBox(outDeckGo.transform, "FillGrass", rc.X0, rc.Z0, rc.W, rc.H, y);
                fillMerged++;
            }
            else
            {
                fillRuns += EmitRowRuns(rc, tops, outDeckGo.transform, "FillGrass", 4, y,
                    deckCells, clampToDeck: true);
            }
        }

        // 9) العشب البعيد: قواعد v2.5.0 (تخطي المتقاطع مع الطريق، تخفيض القريب)
        BuildFarGroundNet(grndRects, deckCells);

        Physics.SyncTransforms();
        Debug.Log($"[TrackBuilder] v3.0.0: الأسطح المصقولة جاهزة — حزام مائل " +
                  $"{tiltedT}+{tiltedG} خلية (مسطّح مدمج {flatRuns})، خارج الحزام " +
                  $"طريق {deckMerged} مدمج + {deckRuns} صفّي، ردم {fillMerged} مدمج + " +
                  $"{fillRuns} صفّي ({fillClamped} قُصّ)، عشب بعيد مدمج");
        return true;
    }

    void TryExtendWall(int ix, int iz, float h0,
        Dictionary<long, float> deckCells, HashSet<long> bandDeck, Queue<long> queue)
    {
        long k = BandKey(ix, iz);
        if (bandDeck.Contains(k) || !deckCells.TryGetValue(k, out float t)) return;
        if (Mathf.Abs(t - h0) <= WallStepM) return;
        bandDeck.Add(k);
        queue.Enqueue(k);
    }

    static void EnqueueApron(int ix, int iz, int depth,
        Dictionary<long, float> grndCells, Dictionary<long, float> fillCells,
        HashSet<long> seen, Queue<System.Collections.Generic.KeyValuePair<long, int>> queue)
    {
        long k = BandKey(ix, iz);
        if (seen.Contains(k)) return;
        seen.Add(k);
        queue.Enqueue(new System.Collections.Generic.KeyValuePair<long, int>(k, depth));
    }

    static void AccumulateCell(Dictionary<long, BandCell> tops, int ix, int iz, ref float acc, ref int cnt)
    {
        if (tops.TryGetValue(BandKey(ix, iz), out BandCell c)) { acc += c.Top; cnt++; }
    }

    static void AccumulateNode(Dictionary<long, float> nodes, int ix, int iz, ref float acc, ref int cnt)
    {
        if (nodes.TryGetValue(BandKey(ix, iz), out float h) && h != float.MinValue) { acc += h; cnt++; }
    }

    /// <summary>هل يلمس المستطيل أي خلية داخل الحزام؟</summary>
    bool RectTouchesBand(BakedRectY rc, Dictionary<long, BandCell> tops)
    {
        int ix0 = Mathf.FloorToInt(rc.X0 / BandCell);
        int ix1 = Mathf.FloorToInt((rc.X0 + rc.W - 0.01f) / BandCell);
        int iz0 = Mathf.FloorToInt(rc.Z0 / BandCell);
        int iz1 = Mathf.FloorToInt((rc.Z0 + rc.H - 0.01f) / BandCell);
        for (int ix = ix0; ix <= ix1; ix++)
            for (int iz = iz0; iz <= iz1; iz++)
                if (tops.ContainsKey(BandKey(ix, iz))) return true;
        return false;
    }

    /// <summary>مصفوفة صفوف خلايا (مفتاح = iz) مع دمج التكرارات.</summary>
    static void AddRow(Dictionary<int, List<int>> rows, int iz, int ix)
    {
        if (!rows.TryGetValue(iz, out List<int> l))
        {
            l = new List<int>();
            rows[iz] = l;
        }
        l.Add(ix);
    }

    /// <summary>
    /// إصدار خلايا حزام من نوع واحد: مسارات أفقية مسطّحة (تسامح 2.5سم) تُدمج،
    /// وخلايا مائلة تُصدر صناديق مائلة كل خلية على حدة. يعيد عدد المسارات المدمجة.
    /// </summary>
    int EmitBandRows(Dictionary<int, List<int>> rows, Dictionary<long, BandCell> tops,
        Dictionary<long, float> nodes, Transform parent, string namePrefix, bool tarmac, ref int tilted)
    {
        int runs = 0;
        foreach (var kv in rows)
        {
            List<int> ixs = kv.Value;
            if (ixs.Count == 0) continue;
            ixs.Sort();
            int i = 0;
            while (i < ixs.Count)
            {
                int iz = kv.Key;
                int ix = ixs[i];

                float runSum, runMean;
                if (IsFlatCell(ix, iz, tops, nodes, out runSum, out runMean))
                {
                    // مدّ المدى المسطّح
                    int j = i;
                    float sum = runSum;
                    int count = 1;
                    while (j + 1 < ixs.Count && ixs[j + 1] == ixs[j] + 1 &&
                           IsFlatCell(ixs[j + 1], iz, tops, nodes, out float s2, out float m2) &&
                           Mathf.Abs(m2 - runMean) <= 0.03f)
                    {
                        j++; sum += s2; count++;
                    }
                    float top = sum / count;
                    int len = count;
                    float x0 = ixs[i] * BandCell;
                    float z0 = iz * BandCell;
                    EmitFlatBox(parent, namePrefix, x0 - 0.02f, z0 - 0.02f,
                        len * BandCell + 0.04f, BandCell + 0.04f, top);
                    runs++;
                    i = j + 1;
                }
                else
                {
                    EmitTiltedCell(parent, $"{namePrefix}_{ix}_{iz}", ix, iz, tops, nodes, tarmac);
                    tilted++;
                    i++;
                }
            }
        }
        return runs;
    }

    /// <summary>هل الخلية مسطحة (عقدها الأربعة موجودة وضمن التسامح)؟ يعني متوسط ارتفاعها.</summary>
    bool IsFlatCell(int ix, int iz, Dictionary<long, BandCell> tops,
        Dictionary<long, float> nodes, out float sum, out float mean)
    {
        sum = 0f; mean = 0f;
        float h00 = NodeAt(nodes, ix, iz), h10 = NodeAt(nodes, ix + 1, iz);
        float h01 = NodeAt(nodes, ix, iz + 1), h11 = NodeAt(nodes, ix + 1, iz + 1);
        if (h00 == float.MinValue || h10 == float.MinValue ||
            h01 == float.MinValue || h11 == float.MinValue) return false;
        float mn = Mathf.Min(Mathf.Min(h00, h10), Mathf.Min(h01, h11));
        float mx = Mathf.Max(Mathf.Max(h00, h10), Mathf.Max(h01, h11));
        if (mx - mn > FlatTolerance) return false;
        sum = h00 + h10 + h01 + h11;
        mean = sum * 0.25f;
        return true;
    }

    static float NodeAt(Dictionary<long, float> nodes, int ix, int iz) =>
        nodes.TryGetValue(BandKey(ix, iz), out float h) ? h : float.MinValue;

    /// <summary>صندوق أفقي مسطّح (قمة عند top بالضبط).</summary>
    static void EmitFlatBox(Transform parent, string namePrefix,
        float x0, float z0, float w, float h, float top)
    {
        GameObject go = new GameObject($"{namePrefix}_{Mathf.RoundToInt(x0)}_{Mathf.RoundToInt(z0)}");
        go.layer = TrackLayer; // كل مصادم على طبقة الحلبة صراحةً (أشعة العشب/المسح)
        go.isStatic = true;
        go.transform.SetParent(parent, false);
        BoxCollider col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(w, 30f, h);
        col.center = new Vector3(w * 0.5f, -15f, h * 0.5f);
        go.transform.localPosition = new Vector3(x0, top, z0);
    }

    /// <summary>خلية مائلة: مستوى أنسب لعقدها الأربعة (C0 مع جيرانها).</summary>
    void EmitTiltedCell(Transform parent, string name, int ix, int iz,
        Dictionary<long, BandCell> tops, Dictionary<long, float> nodes, bool tarmac)
    {
        float h00 = NodeAt(nodes, ix, iz), h10 = NodeAt(nodes, ix + 1, iz);
        float h01 = NodeAt(nodes, ix, iz + 1), h11 = NodeAt(nodes, ix + 1, iz + 1);

        // عقدة ناقصة (خلية معزولة عند حافة الحزام) → صندوق مسطّح بارتفاع الخلية
        if (h00 == float.MinValue || h10 == float.MinValue ||
            h01 == float.MinValue || h11 == float.MinValue)
        {
            BandCell c = tops[BandKey(ix, iz)];
            EmitFlatBox(parent, tarmac ? "BandTarmac" : "BandGrass",
                ix * BandCell, iz * BandCell, BandCell, BandCell, c.Top);
            return;
        }

        // المستوى الأنسب: ناظم من الحدفين القطريين (أقل مربعات لشبكة 2×2)
        Vector3 d1 = new Vector3(BandCell, h10 - h01, -BandCell);   // p10 - p01
        Vector3 d2 = new Vector3(BandCell, h11 - h00, BandCell);    // p11 - p00
        Vector3 n = Vector3.Cross(d1, d2);
        if (n.sqrMagnitude < 1e-8f)
        {
            EmitFlatBox(parent, tarmac ? "BandTarmac" : "BandGrass",
                ix * BandCell, iz * BandCell, BandCell, BandCell, (h00 + h11) * 0.5f);
            return;
        }
        if (n.y < 0f) n = -n;
        n.Normalize();

        float ch = (h00 + h10 + h01 + h11) * 0.25f;
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, n);
        Vector3 pos = new Vector3(ix * BandCell + 1f, ch, iz * BandCell + 1f) - n * 15f;

        GameObject go = new GameObject(name);
        go.layer = TrackLayer;
        go.isStatic = true;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        BoxCollider col = go.AddComponent<BoxCollider>();
        // 2.9م = تغطية أي التفاف للخلية 2×2 حتى 45 درجة (تراكب مع الجيران
        // يضمن استمرارية السطح بلا فجوات عند زوايا الغلاف)
        col.size = new Vector3(2.9f, 30f, 2.9f);
        col.center = Vector3.zero;
    }

    /// <summary>
    /// صفوف خلايا مستطيل خارج الحزام → صناديق أفقية (أدنى طول اختياري).
    /// clampToDeck: كل مدى يُقص إلى أدنى قمة طريق ضمن 24م من مركزه −6سم
    /// إن كان أعلى منها — قتل جدران الردم غير المرئية عند حدود الحزام.
    /// </summary>
    static int EmitRowRuns(BakedRectY rc, Dictionary<long, BandCell> tops,
        Transform parent, string namePrefix, int minRun, float forcedTop = float.MaxValue,
        Dictionary<long, float> deckCells = null, bool clampToDeck = false)
    {
        int ix0 = Mathf.FloorToInt(rc.X0 / BandCell);
        int ix1 = Mathf.FloorToInt((rc.X0 + rc.W - 0.01f) / BandCell);
        int iz0 = Mathf.FloorToInt(rc.Z0 / BandCell);
        int iz1 = Mathf.FloorToInt((rc.Z0 + rc.H - 0.01f) / BandCell);
        float top = forcedTop != float.MaxValue ? forcedTop : rc.Y;

        int runs = 0;
        for (int iz = iz0; iz <= iz1; iz++)
        {
            int runStart = -1;
            for (int ix = ix0; ix <= ix1 + 1; ix++)
            {
                bool inBand = ix <= ix1 && tops.ContainsKey(BandKey(ix, iz));
                if (!inBand)
                {
                    if (runStart >= 0)
                    {
                        int len = ix - runStart;
                        if (len >= minRun)
                        {
                            float runTop = top;
                            if (clampToDeck && deckCells != null)
                            {
                                int cix = runStart + len / 2;
                                float nearDeck = float.MaxValue;
                                for (int dx = -12; dx <= 12; dx++)
                                    for (int dz = -12; dz <= 12; dz++)
                                        if (deckCells.TryGetValue(BandKey(cix + dx, iz + dz), out float t) && t < nearDeck)
                                            nearDeck = t;
                                if (nearDeck < float.MaxValue && runTop > nearDeck + 0.10f)
                                    runTop = nearDeck - 0.06f;
                            }
                            EmitFlatBox(parent, namePrefix,
                                runStart * BandCell, iz * BandCell,
                                len * BandCell, BandCell, runTop);
                            runs++;
                        }
                        runStart = -1;
                    }
                }
                else if (runStart < 0) runStart = ix;
            }
        }
        return runs;
    }

    /// <summary>
    /// العشب البعيد (خارج الحزام) بقواعد v2.5.0 حرفياً: مستطيل يقطع خلية طريق
    /// يُتخطى، وقريب من الطريق وقمته أعلى من حافته يُخفّض إلى الحافة −6سم.
    /// الشبكة المرئية تُبنى من قمم المستطيلات الأصلية (كما كانت).
    /// </summary>
    void BuildFarGroundNet(List<BakedRectY> grndRects, Dictionary<long, float> deckCells)
    {
        if (grndRects.Count == 0) return;
        EnsureOvalMaterials();

        var verts = new List<Vector3>(grndRects.Count * 4);
        var tris = new List<int>(grndRects.Count * 6);
        var uvs = new List<Vector2>(grndRects.Count * 4);

        GameObject go = new GameObject("GroundNet");
        go.transform.SetParent(transform, false);
        go.layer = TrackLayer;

        int emitted = 0, skipped = 0, lowered = 0;
        foreach (BakedRectY rc in grndRects)
        {
            int ix0 = Mathf.FloorToInt(rc.X0 / BandCell);
            int ix1 = Mathf.FloorToInt((rc.X0 + rc.W - 0.01f) / BandCell);
            int iz0 = Mathf.FloorToInt(rc.Z0 / BandCell);
            int iz1 = Mathf.FloorToInt((rc.Z0 + rc.H - 0.01f) / BandCell);

            // قاعدة v2.5.0 (1): تقاطع مع خلية طريق → بلا مصادم ولا شبكة
            bool overlaps = false;
            float nearMin = float.MaxValue;
            for (int ix = ix0 - 2; ix <= ix1 + 2 && !overlaps; ix++)
                for (int iz = iz0 - 2; iz <= iz1 + 2; iz++)
                {
                    if (!deckCells.TryGetValue(BandKey(ix, iz), out float t)) continue;
                    bool inside = ix >= ix0 && ix <= ix1 && iz >= iz0 && iz <= iz1;
                    if (inside) { overlaps = true; break; }
                    if (t < nearMin) nearMin = t;
                }
            if (overlaps) { skipped++; continue; }

            // قاعدة v2.5.0 (2): قريب من الطريق وأعلى من حافته → خفّض المصادم
            float colTop = rc.Y;
            if (nearMin < float.MaxValue && rc.Y > nearMin - 0.06f)
            {
                colTop = nearMin - 0.06f;
                lowered++;
            }

            // شبكة مرئية (قمم أصلية — مطابقة للسلوك السابق) + مصادم منفصل
            int b = verts.Count;
            verts.Add(new Vector3(rc.X0, rc.Y, rc.Z0));
            verts.Add(new Vector3(rc.X0 + rc.W, rc.Y, rc.Z0));
            verts.Add(new Vector3(rc.X0 + rc.W, rc.Y, rc.Z0 + rc.H));
            verts.Add(new Vector3(rc.X0, rc.Y, rc.Z0 + rc.H));
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
            float u0 = rc.X0 / 15f, v0 = rc.Z0 / 15f;
            uvs.Add(new Vector2(u0, v0));
            uvs.Add(new Vector2((rc.X0 + rc.W) / 15f, v0));
            uvs.Add(new Vector2((rc.X0 + rc.W) / 15f, (rc.Z0 + rc.H) / 15f));
            uvs.Add(new Vector2(u0, (rc.Z0 + rc.H) / 15f));

            GameObject cgo = new GameObject("GroundNetGrass");
            cgo.layer = TrackLayer;
            cgo.isStatic = true;
            cgo.transform.SetParent(go.transform, false);
            cgo.transform.localPosition = Vector3.zero;
            BoxCollider col = cgo.AddComponent<BoxCollider>();
            float skirt = Mathf.Min(40f, Mathf.Max(10f, colTop + 32f));
            col.center = new Vector3(rc.X0 + rc.W * 0.5f, colTop - skirt * 0.5f, rc.Z0 + rc.H * 0.5f);
            col.size = new Vector3(rc.W, skirt, rc.H);
            emitted++;
        }

        Mesh mesh = new Mesh
        {
            name = "GroundNet",
            indexFormat = verts.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16
        };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = ovalMatGrass != null ? ovalMatGrass : ovalMatRock;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Physics.SyncTransforms();
        Debug.Log($"[TrackBuilder] v3.0.0: العشب البعيد — {emitted} مستطيل " +
                  $"({skipped} متقاطع مع طريق، {lowered} مُخفّض)");
    }

    /// <summary>
    /// v2.1.0 — تحقق نهائي من نقطة الانطلاق: يجب أن تكون فوق الإسفلت فعلاً.
    /// إن كانت فوق عشب/حصى/فجوة → بحث حلزوني (حتى 60م) عن أقرب نقطة إسفلت
    /// واعتمادها بارتفاع سطحها الحقيقي، مع إعادة اشتقاق الاتجاه من خط السباق.
    /// </summary>
    void VerifySpawnOnTarmac()
    {
        int mask = 1 << TrackLayer;
        Vector3 p = StartPosition;

        bool onTarmac = false;
        string underName = "?";
        if (Physics.Raycast(new Vector3(p.x, p.y + 15f, p.z), Vector3.down, out RaycastHit hit, 60f, mask))
        {
            underName = hit.collider.name;
            string n = underName.ToUpperInvariant();
            onTarmac = n.Contains("TARMAC") || n.Contains("OVAL");
        }

        if (onTarmac)
        {
            Debug.Log($"[TrackBuilder] v2.1.0: الانطلاق فوق '{underName}' — سليم");
            return;
        }

        const float Step = 2f;
        const float MaxR = 60f;
        for (float r = Step; r <= MaxR; r += Step)
        {
            int steps = Mathf.Max(8, Mathf.CeilToInt(Mathf.PI * 2f * r / Step));
            for (int s = 0; s < steps; s++)
            {
                float a = (float)s / steps * Mathf.PI * 2f;
                float x = p.x + Mathf.Cos(a) * r;
                float z = p.z + Mathf.Sin(a) * r;
                if (Physics.Raycast(new Vector3(x, p.y + 15f, z), Vector3.down, out RaycastHit h2, 60f, mask))
                {
                    string n2 = h2.collider.name.ToUpperInvariant();
                    if (n2.Contains("TARMAC") || n2.Contains("OVAL"))
                    {
                        StartPosition = new Vector3(x, h2.point.y + 0.4f, z);
                        SnapYawToRacingLine();
                        Debug.Log($"[TrackBuilder] v2.1.0: الانطلاق كان فوق '{underName}' — مُسقط على أقرب إسفلت عند {StartPosition}");
                        return;
                    }
                }
            }
        }
        Debug.LogWarning("[TrackBuilder] v2.1.0: لا إسفلت قرب نقطة الانطلاق — أُبقيت كما هي");
    }

    /// <summary>إعادة اشتقاق StartYaw من خط السباق عند أقرب نقطة للانطلاق.</summary>
    void SnapYawToRacingLine()
    {
        Vector3[] line = RacingLine;
        if (line == null || line.Length < 4) return;

        int bi = 0;
        float bd = float.MaxValue;
        for (int i = 0; i < line.Length; i++)
        {
            float dx = line[i].x - StartPosition.x, dz = line[i].z - StartPosition.z;
            float d = dx * dx + dz * dz;
            if (d < bd) { bd = d; bi = i; }
        }

        Vector3 dir = line[(bi + 1) % line.Length] - line[bi];
        dir.y = 0f;
        if (dir.sqrMagnitude > 1e-6f)
            StartYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
    }

    /// <summary>اسم السطح تحت نقطة (للطبقة التشخيصية v2.1.0) — "VOID" إن لا شيء.</summary>
    public string SurfaceNameAt(float x, float z)
    {
        int mask = 1 << TrackLayer;
        if (Physics.Raycast(new Vector3(x, 40f, z), Vector3.down, out RaycastHit hit, 120f, mask))
            return hit.collider.name;
        return "VOID";
    }

    GameObject InstantiateOvalModule(string name, OvalColliderMode mode)
    {
        GameObject prefab = Resources.Load<GameObject>(TrackRes + name);
        if (prefab == null)
        {
            Debug.LogWarning($"[TrackBuilder] وحدة الحلبة '{name}' غير موجودة في الموارد");
            return null;
        }

        GameObject go = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
        go.name = name;

        // v1.7.0: تكبير موحّد للحلبة (طريق أعرض بطلب المستخدم) — كل الأنظمة
        // المشتقة بالأشعة/الخلايا تتكيف تلقائياً، والمقياس الموحّد آمن للمصادمات الشبكية
        go.transform.localScale = Vector3.one * OvalScale;

        ApplyOvalModuleMaterials(go, name);

        if (mode != OvalColliderMode.None)
        {
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;

                // v1.9.0: Plane001 (خلفية الجبال البعيدة) يخترق مستقيم الجنوب —
                // مجسّم مشهدٍ بعيد لا سطح قيادة: بلا مصادم حتى لا يصبح جداراً خفياً
                string mn = mf.name.ToUpperInvariant();
                if (mn.Contains("PLANE")) continue;

                MeshCollider col = mf.gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = mf.sharedMesh;
                col.convex = (mode == OvalColliderMode.Convex);
                mf.gameObject.layer = TrackLayer;
            }
        }

        return go;
    }

    void ApplyOvalModuleMaterials(GameObject go, string moduleName)
    {
        EnsureOvalMaterials();

        // الديكور: خامة واحدة لكل وحدة (من أسماء خامات FBX المرجعية)
        Material whole = null;
        switch (moduleName)
        {
            case "oval_mod_buildings":
            case "oval_mod_seats":
            case "oval_mod_lamps":
            case "oval_mod_pitwall":
            case "oval_mod_tents":
            case "oval_mod_turnsigns":
            case "oval_mod_bridges":
            case "oval_mod_blocks":
            case "oval_mod_startlights":
                whole = ovalMatGarage; break;
            case "oval_mod_banners": whole = ovalMatBanner; break;
            case "oval_mod_tyres":   whole = ovalMatTyre;   break;
            case "oval_mod_lines":   whole = ovalMatLine;   break;
        }

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (whole != null)
            {
                Material[] mats = new Material[r.sharedMaterials.Length];
                for (int s = 0; s < mats.Length; s++) mats[s] = whole;
                r.sharedMaterials = mats;
                continue;
            }

            // terrain: تصنيف كل رندر من اسمه
            string n = r.name.ToUpperInvariant();
            Material m;
            if (n.Contains("TARMAC") || n.Contains("PITLANE") || n.Contains("KERB")) m = ovalMatRoad;
            else if (n.Contains("GRASS2") || n.Contains("GRASS_2")) m = ovalMatGrass2;
            else if (n.Contains("GRASS")) m = ovalMatGrass;
            else if (n.Contains("GRAVEL") || n.Contains("SAND")) m = ovalMatGravel;
            else if (n.Contains("CONCRETE") || n.Contains("UNDERGROUND") || n.Contains("ROCK")) m = ovalMatRock;
            else m = ovalMatGrass;

            Material[] all = new Material[r.sharedMaterials.Length];
            for (int s = 0; s < all.Length; s++) all[s] = m;
            r.sharedMaterials = all;
        }
    }

    Material ovalMatRoad, ovalMatGrass, ovalMatGrass2, ovalMatGravel, ovalMatRock,
             ovalMatGarage, ovalMatBanner, ovalMatTyre, ovalMatLine;
    bool ovalMatsReady;

    void EnsureOvalMaterials()
    {
        if (ovalMatsReady) return;
        ovalMatsReady = true;

        ovalMatRoad   = OvalTexMat("TrackMat_Road",   LoadOvalTex("road"),     0.55f);
        ovalMatGrass  = OvalTexMat("TrackMat_Grass",  LoadOvalTex("grass_1"),  0f);
        ovalMatGrass2 = OvalTexMat("TrackMat_Grass2", LoadOvalTex("grass_2"),  0f);
        ovalMatGravel = OvalTexMat("TrackMat_Gravel", LoadOvalTex("gravel"),   0f);
        ovalMatRock   = OvalTexMat("TrackMat_Rock",   LoadOvalTex("rock"),     0f);
        ovalMatGarage = OvalTexMat("TrackMat_Garage", LoadOvalTex("garages"),  0.15f);
        ovalMatBanner = OvalTexMat("TrackMat_Banner", LoadOvalTex("banners"),  0.2f);
        ovalMatTyre   = OvalTexMat("TrackMat_Tyre",   LoadOvalTex("tyres"),    0f);

        ovalMatLine = new Material(Shader.Find("Standard"));
        ovalMatLine.name = "TrackMat_Line";
        ovalMatLine.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        if (ovalMatLine.HasProperty("_EmissionColor"))
        {
            ovalMatLine.EnableKeyword("_EMISSION");
            ovalMatLine.SetColor("_EmissionColor", new Color(0.55f, 0.55f, 0.55f, 1f));
        }
    }

    Texture2D LoadOvalTex(string name) => Resources.Load<Texture2D>(TrackRes + name);

    Material OvalTexMat(string name, Texture2D tex, float smooth)
    {
        Material m = new Material(Shader.Find("Standard"));
        m.name = name;
        if (tex != null) m.mainTexture = tex;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        return m;
    }

    static long OvalCellKey(int ix, int iz) => ((long)ix << 32) | (uint)iz;

    bool ScanOvalRoad(GameObject terrainGo)
    {
        // نطاق الطريق الرئيسي من رندر TARMAC_oval مباشرة
        Renderer tarmac = FindRendererBySubstring(terrainGo.transform, "TARMAC_OVAL");
        if (tarmac == null) return false;

        ovalRoadBounds = tarmac.bounds;
        ovalRoadBounds.Expand(3f);

        // v1.7.0: تصنيف الخلايا حسب اسم المصادم — الطريق الرئيسي فقط (TARMAC_OVAL)
        // هو أساس خط السباق/الانطلاق، وحارة PITLANE تُحفظ جانباً لتحديد مستقيم البداية.
        // (v1.5.0-v1.6.0 كانت تأخذ كل الأسفلت: طرق الجزيرة الداخلية 1TARMAC_inner
        // والجراجات 1TARMACgarages — فتشوّه الخط نحو الجزيرة وضع الانطلاق في الحارة!)
        ovalRoadCells.Clear();
        ovalPitCells.Clear();
        int mask = 1 << TrackLayer;
        Vector3 origin = new Vector3(0f, 30f, 0f);

        for (float x = ovalRoadBounds.min.x; x <= ovalRoadBounds.max.x; x += ovalScanStep)
        {
            for (float z = ovalRoadBounds.min.z; z <= ovalRoadBounds.max.z; z += ovalScanStep)
            {
                origin.x = x; origin.z = z;
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 70f, mask))
                {
                    string n = hit.collider.name.ToUpperInvariant();
                    long key = OvalCellKey(
                        Mathf.FloorToInt(x / ovalScanStep),
                        Mathf.FloorToInt(z / ovalScanStep));

                    if (n.Contains("OVAL")) ovalRoadCells.Add(key);
                    else if (n.Contains("PITLANE")) ovalPitCells.Add(key);
                }
            }
        }

        if (ovalRoadCells.Count < 50) return false;

        ComputeOvalStartPose();
        return true;
    }

    /// <summary>
    /// v1.7.0 — موضع الانطلاق من خلايا الطريق الرئيسي حصراً:
    /// 1) صفوف الطريق (ثابت Z) وأطول مدى أعمدة متصل في كل صف.
    /// 2) صفوف «المستقيمات» = المدى الطويل (المستقيم يمتد ~180م مقابل ~12م عرضاً).
    /// 3) شريحة البداية = شريحة المستقيم الأقرب إلى نطاق حارة الـ PITLANE
    ///    (حارة pitlane هذا الأصل تلاصق مستقيم بدايته الحقيقي — الضوءان والـ pitwall
    ///    مثبتان عند نقطة الأصل تحقيقاً لذلك)، أو الأقرب لنقطة الأصل إن غابت الحارة.
    /// 4) نقطة البداية = منتصف أطول مدى أعمدة في الشريحة (وسط الطريق تماماً —
    ///    لا pitlane الضيقة ولا حافة pitwall التي كانت تفجّر شبكة الانطلاق).
    /// الاتجاه النهائي (StartYaw) يُضبط لاحقاً من خط السباق في AlignOvalStartAndGates.
    /// </summary>
    void ComputeOvalStartPose()
    {
        // تجميع أعمدة كل صف
        Dictionary<int, List<int>> rows = new Dictionary<int, List<int>>();
        foreach (long key in ovalRoadCells)
        {
            int ix = (int)(key >> 32);
            int iz = (int)(key & 0xffffffff);
            if (!rows.TryGetValue(iz, out List<int> cols))
            {
                cols = new List<int>();
                rows[iz] = cols;
            }
            cols.Add(ix);
        }

        if (rows.Count == 0)
        {
            StartPosition = new Vector3(ovalRoadBounds.center.x, 1.0f, ovalRoadBounds.min.z + 5f);
            StartYaw = 90f;
            return;
        }

        // أطول مدى متصل لكل صف (المستقيم = مدى طويل، القوس = مدى قصير)
        Dictionary<int, int> rowRun = new Dictionary<int, int>();
        int maxRun = 0;
        foreach (KeyValuePair<int, List<int>> kv in rows)
        {
            kv.Value.Sort();
            int best = 0, cur = 0;
            for (int i = 0; i < kv.Value.Count; i++)
            {
                cur = (i > 0 && kv.Value[i] == kv.Value[i - 1] + 1) ? cur + 1 : 1;
                if (cur > best) best = cur;
            }
            rowRun[kv.Key] = best;
            if (best > maxRun) maxRun = best;
        }

        // صفوف المستقيمات (>= 60% من أطول مدى) مجمّعة في شرائح متصلة (علوية/سفلية)
        List<int> straightRows = new List<int>();
        foreach (KeyValuePair<int, int> kv in rowRun)
            if (kv.Value >= maxRun * 0.6f) straightRows.Add(kv.Key);
        straightRows.Sort();

        List<List<int>> groups = new List<List<int>>();
        for (int i = 0; i < straightRows.Count; i++)
        {
            if (i > 0 && straightRows[i] == straightRows[i - 1] + 1)
                groups[groups.Count - 1].Add(straightRows[i]);
            else
                groups.Add(new List<int> { straightRows[i] });
        }

        if (groups.Count == 0)
        {
            StartPosition = new Vector3(ovalRoadBounds.center.x, 1.0f, ovalRoadBounds.min.z + 5f);
            StartYaw = 90f;
            return;
        }

        // نطاق حارة الـ pitlane (يحدد مستقيم بداية الأصل)
        float pitLo = float.MaxValue, pitHi = float.MinValue;
        foreach (long key in ovalPitCells)
        {
            int iz = (int)(key & 0xffffffff);
            float z = iz * ovalScanStep;
            if (z < pitLo) pitLo = z;
            if (z > pitHi) pitHi = z;
        }
        bool hasPit = pitLo <= pitHi;

        // اختيار الشريحة: الأقرب لحارة pitlane، وإلا الأقرب لنقطة الأصل (0)
        List<int> chosen = groups[0];
        float bestScore = float.MaxValue;
        foreach (List<int> g in groups)
        {
            float zc = ((g[0] + g[g.Count - 1]) * 0.5f + 0.5f) * ovalScanStep;
            float score;
            if (hasPit)
                score = (zc < pitLo) ? pitLo - zc : (zc > pitHi ? zc - pitHi : 0f);
            else
                score = Mathf.Abs(zc);

            if (score < bestScore - 0.01f)
            {
                bestScore = score;
                chosen = g;
            }
        }

        // أطول مدى أعمدة داخل صفوف الشريحة = المستقيم نفسه
        HashSet<int> colSet = new HashSet<int>();
        foreach (int iz in chosen) colSet.UnionWith(rows[iz]);

        List<int> cols2 = new List<int>(colSet);
        cols2.Sort();

        int bestStart = 0, bestLen = 0, curStart = 0, curLen = 0;
        for (int i = 0; i < cols2.Count; i++)
        {
            if (i > 0 && cols2[i] == cols2[i - 1] + 1) curLen++;
            else { curStart = i; curLen = 1; }
            if (curLen > bestLen) { bestLen = curLen; bestStart = curStart; }
        }

        int c0 = cols2[bestStart], c1 = cols2[bestStart + bestLen - 1];
        float startX = ((c0 + c1) * 0.5f + 0.5f) * ovalScanStep;
        float startZ = ((chosen[0] + chosen[chosen.Count - 1]) * 0.5f + 0.5f) * ovalScanStep;

        // ارتفاع سطح الطريق الفعلي عند نقطة الانطلاق
        float surfaceY = 1.0f;
        int mask = 1 << TrackLayer;
        if (Physics.Raycast(new Vector3(startX, 30f, startZ), Vector3.down, out RaycastHit hit, 70f, mask))
            surfaceY = hit.point.y + 0.4f;

        StartPosition = new Vector3(startX, surfaceY, startZ);
        StartYaw = 90f; // مؤقت — المحاذاة النهائية من خط السباق (AlignOvalStartAndGates)
    }

    /// <summary>
    /// v1.7.0 — محاذاة الانطلاق والبوابات على خط السباق نفسه (ضمان التناغم الكامل):
    /// 1) تدوير خط السباق ليبدأ من أقرب نقطة لموضع الانطلاق (فهرس 0 = الانطلاق).
    /// 2) StartYaw = اتجاه الخط عند نقطة البداية — لا يمكن أبداً أن يواجه الجزيرة
    ///    لأن الخط نفسه على منتصف الطريق الرئيسي.
    /// 3) البوابات = أرباع الخط بالترتيب: [0..2] نقاط التفتيش (الربع 1..3)،
    ///    [3] خط النهاية عند نقطة البداية (الربع 0) — محاورها ونطاقاتها واتجاهاتها
    ///    مشتقة من هندسة الخط فتصح تلقائياً على أي اتجاه دوران.
    /// </summary>
    void AlignOvalStartAndGates()
    {
        Vector3[] line = RacingLine;
        if (line == null || line.Length < 12) return; // بلا خط: تبقى القيم الاحتياطية

        int n = line.Length;

        // ===== v2.0.0 — محاذاة الانطلاق إلى بوابة أضواء الانطلاق الحقيقية =====
        // جسم البوابة startlights_body هو علامة خط البداية/النهاية في الأصل
        // (تحليل APK: مركزها X≈-24 فوق المستقيم الشمالي). «وسط المستقيم»
        // الخوارزمي كان يقع عند X≈0 = ~24م شرق البوابة فبدت نقطة الانطلاق
        // خاطئة بوضوح أمام البوابة الظاهرة. الحل: إسقاط موضع البوابة على
        // خط السباق (منتصف الطريق) واعتماد نقطة الإسقاط انطلاقاً — فيقف
        // اللاعب تحت الأضواء تماماً ويبقى كل شيء (بوابات/خط مربعات/كاميرا) منسجماً.
        if (ovalStartLights != null)
        {
            Renderer body = FindRendererBySubstring(ovalStartLights, "STARTLIGHTS_BODY");
            if (body != null)
            {
                float gx = body.bounds.center.x;
                float gz = body.bounds.center.z;

                // أقرب نقطة خط للبوابة ثم إسقاط على المقطعين المجاورين
                int bi = 0; float bd = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    float ddx = line[i].x - gx, ddz = line[i].z - gz;
                    float d = ddx * ddx + ddz * ddz;
                    if (d < bd) { bd = d; bi = i; }
                }

                Vector3 bestPt = line[bi]; float bestD2 = float.MaxValue;
                for (int s = 0; s < 2; s++)
                {
                    Vector3 p0 = s == 0 ? line[bi] : line[(bi - 1 + n) % n];
                    Vector3 p1 = s == 0 ? line[(bi + 1) % n] : line[bi];
                    Vector3 seg = p1 - p0; seg.y = 0f;
                    float len2 = Mathf.Max(1e-6f, seg.sqrMagnitude);
                    float t = Mathf.Clamp01(Vector3.Dot(new Vector3(gx - p0.x, 0f, gz - p0.z), seg) / len2);
                    Vector3 pt = p0 + seg * t;
                    float d2 = (pt.x - gx) * (pt.x - gx) + (pt.z - gz) * (pt.z - gz);
                    if (d2 < bestD2) { bestD2 = d2; bestPt = pt; }
                }

                // ارتفاع سطح الطريق الفعلي تحت نقطة الإسقاط
                float sy = StartPosition.y;
                if (Physics.Raycast(new Vector3(bestPt.x, 30f, bestPt.z), Vector3.down,
                                    out RaycastHit ghit, 70f, 1 << TrackLayer))
                    sy = ghit.point.y + 0.4f;

                StartPosition = new Vector3(bestPt.x, sy, bestPt.z);
                Debug.Log($"[TrackBuilder] v2.0.0: الانطلاق محاذى لبوابة الأضواء الحقيقية عند {StartPosition}");
            }
        }

        // 1) تدوير الخط ليبدأ عند أقرب نقطة للانطلاق
        int i0 = 0;
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            float dx = line[i].x - StartPosition.x;
            float dz = line[i].z - StartPosition.z;
            float d = dx * dx + dz * dz;
            if (d < best) { best = d; i0 = i; }
        }

        Vector3[] rotated = new Vector3[n];
        for (int i = 0; i < n; i++) rotated[i] = line[(i0 + i) % n];
        RacingLine = rotated;
        line = rotated;

        // 2) اتجاه الانطلاق من أول مقاطع الخط
        Vector3 dir = line[1] - line[0];
        dir.y = 0f;
        if (dir.sqrMagnitude > 1e-6f)
            StartYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        // 3) بوابات الأرباع
        LapGate[] gates = new LapGate[4];
        for (int k = 0; k < 4; k++)
        {
            int iq = (k * n / 4) % n;
            gates[k] = GateFromLine(line, iq, n);
        }

        LapGates = new[]
        {
            gates[1], gates[2], gates[3], gates[0] // [0..2] تفتيش ثم [3] نهاية عند الانطلاق
        };

        Debug.Log($"[TrackBuilder] محاذاة v1.7.0: yaw={StartYaw:0.0}° عند {StartPosition} — بوابات أرباع خط {n} نقطة");
    }

    static LapGate GateFromLine(Vector3[] line, int iq, int n)
    {
        Vector3 a = line[iq];
        Vector3 b = line[(iq + 1) % n];
        Vector3 d = b - a;
        d.y = 0f;

        bool alongX = Mathf.Abs(d.x) >= Mathf.Abs(d.z);

        // نطاق العبور على المحور الآخر من نافذة نقاط حول الربع
        int w = Mathf.Max(2, n / 24);
        float lo = float.MaxValue, hi = float.MinValue;
        for (int j = -w; j <= w; j++)
        {
            Vector3 q = line[((iq + j) % n + n) % n];
            float v = alongX ? q.z : q.x;
            if (v < lo) lo = v;
            if (v > hi) hi = v;
        }

        LapGate g;
        if (alongX)
        {
            g.axis = 0; g.plane = a.x; g.dir = d.x >= 0f ? 1 : -1;
        }
        else
        {
            g.axis = 1; g.plane = a.z; g.dir = d.z >= 0f ? 1 : -1;
        }
        g.spanMin = lo - 1f;
        g.spanMax = hi + 1f;
        return g;
    }

    /// <summary>خط السباق البيضاوي (v1.5.0 لسيارات المنافسين): دلو زاوي حول مركز
    /// الطريق — متوسط خلايا كل دلو = نقطة خط منتصف الطريق، مرتبة عكس عقارب
    /// الساعة بدءاً من الانطلاق.</summary>
    void BuildOvalRacingLine()
    {
        if (ovalRoadCells.Count == 0) { BuildRacingLine(); return; } // احتياطي إجرائي

        float cx = ovalRoadBounds.center.x, cz = ovalRoadBounds.center.z;
        const int Buckets = 96;

        var sumX = new float[Buckets];
        var sumZ = new float[Buckets];
        var count = new int[Buckets];

        foreach (long key in ovalRoadCells)
        {
            int ix = (int)(key >> 32);
            int iz = (int)(key & 0xffffffff);
            float x = ix * ovalScanStep, z = iz * ovalScanStep;

            float ang = Mathf.Atan2(z - cz, x - cx);          // -π..π
            float t = (ang + Mathf.PI) / (2f * Mathf.PI);     // 0..1
            int b = Mathf.Clamp((int)(t * Buckets), 0, Buckets - 1);
            sumX[b] += x; sumZ[b] += z; count[b]++;
        }

        // الدلاء بالترتيب عكس عقارب الساعة: الزاوية تزداد من -π إلى +π
        // (شرقاً على الجنوب ← شمالاً على الشرق ← غرباً على الشمال ← جنوباً على الغرب)
        var pts = new List<Vector3>(Buckets);
        for (int b = 0; b < Buckets; b++)
        {
            if (count[b] == 0) continue;
            pts.Add(new Vector3(sumX[b] / count[b], 0f, sumZ[b] / count[b]));
        }

        if (pts.Count < 12) { BuildRacingLine(); return; }

        // تمهيد بسيط (متوسط جيران) لتنعيم الانتقالات بين الدلاء
        var smooth = new List<Vector3>(pts.Count);
        int n = pts.Count;
        for (int i = 0; i < n; i++)
        {
            Vector3 a = pts[(i - 1 + n) % n];
            Vector3 b = pts[i];
            Vector3 c = pts[(i + 1) % n];
            smooth.Add((a + b * 2f + c) * 0.25f);
        }

        // تدوير القائمة لتبدأ قرب نقطة الانطلاق
        int startIdx = 0; float best = float.MaxValue;
        for (int i = 0; i < smooth.Count; i++)
        {
            float dx = smooth[i].x - StartPosition.x, dz = smooth[i].z - StartPosition.z;
            float d = dx * dx + dz * dz;
            if (d < best) { best = d; startIdx = i; }
        }

        // v1.9.0 — عكس اتجاه الدوران إلى مع عقارب الساعة (مع إبقاء الفهرس 0
        // عند نقطة الانطلاق): ترتيب الدلاء بالزاوية المتزايدة يعطي عكس عقارب
        // الساعة (غرباً على المستقيم الشمالي) فكانت السيارة تنطلق «للخلف»
        // من جهة الـ pitlane ومخرج الجراجات (الفوة الخرسانية شرقه). السير
        // بالقائمة عكسياً انطلاقاً من أقرب نقطة للانطلاق يجعل الاتجاه شرقاً
        // (+90) — جهة الفوة/pit exit والاتجاه الذي اعتادته النسخ السابقة —
        // وكل الأنظمة (البوابات/الشبكة/الذكاء) تتبع الخط تلقائياً.
        var rotated = new List<Vector3>(smooth.Count);
        for (int i = 0; i < smooth.Count; i++)
            rotated.Add(smooth[(startIdx - i + smooth.Count) % smooth.Count]);

        RacingLine = rotated.ToArray();

        Debug.Log($"[TrackBuilder] خط السباق البيضاوي جاهز (مع عقارب الساعة v1.9.0): {RacingLine.Length} نقطة");
    }

    static Bounds RenderersBounds(Transform t)
    {
        Bounds b = new Bounds(t.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
        {
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    static Renderer FindRendererBySubstring(Transform root, string sub)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            if (r.name.ToUpperInvariant().Contains(sub)) return r;
        return null;
    }

    enum OvalColliderMode { None, Concave, Convex }

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
