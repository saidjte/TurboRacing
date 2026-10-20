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
    /// ظاهرة بحجم واضح)، ومركز الخريطة على الإجرائية (حلبة صغيرة تُرى كاملة).</summary>
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

    void Awake()
    {
        Instance = this;

        // v1.5.0: الأصل المطلوب أولاً — الحلبة البيضاوية الكرتونية الكاملة
        if (TryBuildOvalAsset())
        {
            usingOvalAsset = true;
            BuildOvalRacingLine();
            AlignOvalStartAndGates(); // v1.7.0: اتجاه الانطلاق والبوابات من خط السباق نفسه
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
        if (!usingOvalAsset || ovalRoadCells.Count == 0) return;

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

            Vector3 slicePos = new Vector3(at.x, hit.point.y + 0.06f, at.z);

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

        // 1) الأرضية (سطح القيادة): مصادمات شبكية مقعّرة على كل شبكة
        GameObject terrainGo = InstantiateOvalModule("oval_mod_terrain", OvalColliderMode.Concave);
        if (terrainGo == null) return false;

        Physics.SyncTransforms();

        // 2) مسح الطريق أولاً (قبل الديكور) — نحتاج خلايا الطريق
        //    لاستبعاد أي مجسّم ديكور يقطع مسار السباق (v1.8.0).
        if (!ScanOvalRoad(terrainGo))
        {
            Renderer tarmac = FindRendererBySubstring(terrainGo.transform, "TARMAC");
            ovalRoadBounds = tarmac != null ? tarmac.bounds : new Bounds(MapCenter, new Vector3(228f, 1f, 110f));
            StartPosition = new Vector3(ovalRoadBounds.center.x, 1.0f, ovalRoadBounds.min.z + 5f);
            StartYaw = 90f; // احتياطي: شرقاً على أدنى مستقيم — يُضبط نهائياً من الخط إن وجد
        }
        else
        {
            // v1.9.0 — علاج جذري لـ «الخروج من الحلبة = الغوص في الأرض»:
            // داخل الحلبة كامل (حول الجزيرة) توجد مناطق بلا أي سطح (فجوات شبكة
            // التضاريس الأصل) تنتهي بقاع صخري على عمق -200..-360م — أي خروج
            // من الإسفلت نحوها = سقوط حرفي داخل الأرض. الملء بالأشعة (أدناه)
            // يبني أرضية عشب صلبة فوق كل فجوة يكتشفها بنفسه فتستقيم الدائرة.
            BuildAbyssFiller();
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
        InstantiateOvalModule("oval_mod_startlights", OvalColliderMode.None);

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

    // ================= ملء فجوات التضاريس (v1.9.0) =================

    /// <summary>
    /// v1.9.0 — علاج «الخروج من الحلبة = الغوص في الأرض» من جذوره:
    /// شبكة تضاريس الأصل الأصلي بها فجوات حقيقية داخل الحلبة (حول الجزيرة
    /// وجنوبها وشرقها) — أشعة نازلة من فوقها لا تصيب أي سطح حتى قاع صخري
    /// مدفون على عمق -200..-360م (مجسّم underground)، فأي سيارة تغادر
    /// الإسفلت نحوها تسقط حرفياً داخل الأرض.
    /// العلاج: مسح شبكي ذاتي (8م) فوق كامل نطاق الحلبة — كل خلية يكون
    /// أعلى سطح فيها «أرض الأمان العميقة» أو لا سطح إطلاقاً = خلية فجوة،
    /// تُبنى لها بلاطة أرضية (عشب + مصادم شبكي مقعّر) في شبكة واحدة
    /// مدمجة بمجسّم واحد. الخلايا السليمة (إسفلت/عشب/حصى/رمل) تُتخطى
    /// تلقائياً فلا تلمس الملء أسطحاً قانونية أبداً.
    /// </summary>
    void BuildAbyssFiller()
    {
        EnsureOvalMaterials();

        const float Cell = 8f;
        const float Y = 0f;          // ارتفاع الملء = سطح القاعدة (الطرق القانونية 0..7م فوقه لا تحته)
        const float RayTop = 150f;   // بداية الأشعة فوق أعلى تلّ (77م)
        const float RayDepth = 400f; // تغطي القاع الصخري حتى -250م
        const float AbyssBelow = -15f; // كل سطح أدنى من هذا = قاع الفجوة (لا سطح حقيقي)

        float minX = ovalRoadBounds.min.x - 40f;
        float maxX = ovalRoadBounds.max.x + 40f;
        float minZ = ovalRoadBounds.min.z - 40f;
        float maxZ = ovalRoadBounds.max.z + 40f;

        var verts = new List<Vector3>(4096);
        var tris = new List<int>(8192);
        var uvs = new List<Vector2>(4096);
        int cells = 0;
        int mask = 1 << TrackLayer;

        for (float x = minX; x < maxX; x += Cell)
        {
            for (float z = minZ; z < maxZ; z += Cell)
            {
                float cx = x + Cell * 0.5f;
                float cz = z + Cell * 0.5f;

                if (Physics.Raycast(new Vector3(cx, RayTop, cz), Vector3.down,
                                   out RaycastHit hit, RayDepth, mask) &&
                    hit.point.y > AbyssBelow)
                    continue; // سطح قانوني (إسفلت/عشب/حصى/رمل) — لا ملء

                // بلاطة 8×8 عند سطح القاعدة (رؤوس مع اتجاه مزدوج عبر مثلثين)
                int b = verts.Count;
                verts.Add(new Vector3(x, Y, z));
                verts.Add(new Vector3(x + Cell, Y, z));
                verts.Add(new Vector3(x + Cell, Y, z + Cell));
                verts.Add(new Vector3(x, Y, z + Cell));
                // وجه علوي
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
                // وجه سفلي (لأشعة من الأسفل إن حدثت)
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
                float u0 = x / 15f, v0 = z / 15f, u1 = (x + Cell) / 15f, v1 = (z + Cell) / 15f;
                uvs.Add(new Vector2(u0, v0));
                uvs.Add(new Vector2(u1, v0));
                uvs.Add(new Vector2(u1, v1));
                uvs.Add(new Vector2(u0, v1));
                cells++;
            }
        }

        if (cells == 0)
        {
            Debug.Log("[TrackBuilder] v1.9.0: لا فجوات تضاريس — لا حاجة لملء");
            return;
        }

        Mesh mesh = new Mesh
        {
            name = "AbyssFiller",
            indexFormat = cells > 16384
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16
        };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject go = new GameObject("AbyssFiller");
        go.transform.SetParent(transform, false);
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = ovalMatGrass != null ? ovalMatGrass : ovalMatRock;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MeshCollider col = go.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;
        col.convex = false; // مقعّر: أسطح مسطحة فقط وأدق دقة
        go.layer = TrackLayer;

        Physics.SyncTransforms(); // الخبز قبل أي أشعة لاحقة (خط البداية/الإنقاذ)

        Debug.Log($"[TrackBuilder] v1.9.0: مُلئت {cells} فجوة تضاريس (شبكة 8م) — لا غوص بعد اليوم");
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
