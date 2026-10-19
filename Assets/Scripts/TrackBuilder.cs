using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TurboRacing - v1.0.0 (لعبة ثلاثية الأبعاد حقيقية)
/// يبني الحلبة كعالم مجسّم كامل بالكود (بلا أي أصول خارجية):
/// - أرضية عشب واسعة بنسيج مكرر (خطوط جز + حبيبات)
/// - طريق إسفلت كشبكة واحدة + خط بداية/نهاية مربعات كُرْد
/// - كيرب أحمر/أبيض مرفوع 7سم على كل حواف الطريق
/// - حواجز خرسانية مجسّمة (صناديق بظلال حقيقية) بنفس تخطيط الخريطة القديم حرفياً
/// - أشجار (جذع + تُيج) ومدرّجات جمهور ولوحات إعلانات وبوابة انطلاق
/// - نفس البوابات الأربع ونقطة الانطلاق واستعلام IsGrass — اللعب لم يتغير
/// أداءً: كل النوع الواحد مدموج في شبكة واحدة = دورة رسم قليلة جداً.
/// </summary>
public class TrackBuilder : MonoBehaviour
{
    public static TrackBuilder Instance { get; private set; }

    [Header("أبعاد البلاطة")]
    [Tooltip("حجم البلاطة بالوحدات العالمية (2 = طريق عرضه 8 أمتار)")]
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

    // --- أبعاد خريطة الحلبة بالبلاطات ---
    const int W = 46;
    const int H = 28;

    // رموز الخريطة: '#' جدار   '.' عشب   'R' طريق   'S' خط البداية
    const char WallChar = '#';
    const char GrassChar = '.';
    const char RoadChar = 'R';
    const char StartChar = 'S';

    char[,] map;
    Material matGround, matRoad, matStart, matCurb, matWall, matPaint;

    void Awake()
    {
        Instance = this;

        map = BuildMap();
        EnsureMaterials();

        BuildGround();
        BuildRoadAndStart();
        BuildCurbs();
        BuildWalls();
        BuildScenery();
        BuildWallColliders();
        ComputeStartPose();
        BuildLapGates();

        MapWidthUnits = W * tileSize;
        MapHeightUnits = H * tileSize;
        MapCenter = new Vector3(MapWidthUnits * 0.5f, 0f, MapHeightUnits * 0.5f);

        Debug.Log($"[TrackBuilder] الحلبة ثلاثية الأبعاد جاهزة: {W}×{H} بلاطة، انطلاق من {StartPosition}");
    }

    // ================= تصميم الحلبة (كما هو — اللعب ثابت) =================

    static char[,] BuildMap()
    {
        char[,] m = new char[W, H];

        Fill(m, GrassChar);

        // المستقيمات الأربعة (عرض 4 بلاطات)
        Rect(m, RoadChar, 3, 2, 40, 4);    // العلوي
        Rect(m, RoadChar, 3, 22, 40, 4);   // السفلي
        Rect(m, RoadChar, 3, 2, 4, 24);    // الأيسر
        Rect(m, RoadChar, 39, 2, 4, 24);   // الأيمن

        // الجدار الخارجي المحيط بكل شيء
        Ring(m, WallChar, 0, 0, W, H);

        // جدار الجزيرة العشبية الداخلية
        Ring(m, WallChar, 8, 8, 30, 12);

        // مشدّد علوي: يضيّق الطريق من 4 بلاطات إلى 2
        Rect(m, WallChar, 12, 4, 6, 2);

        // مشدّد سفلي: يضيّق الطريق إلى 3 بلاطات
        Rect(m, WallChar, 20, 21, 8, 2);

        // خط البداية/النهاية: عمودان مقلّمان على المستقيم السفلي
        for (int y = 22; y <= 25; y++)
        {
            m[30, y] = StartChar;
            m[31, y] = StartChar;
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

    /// <summary>رسم إطار مجوّف (سماكة بلاطة واحدة) داخل المستطيل.</summary>
    static void Ring(char[,] m, char c, int x, int y, int w, int h)
    {
        Rect(m, c, x, y, w, 1);          // الصف العلوي
        Rect(m, c, x, y + h - 1, w, 1);  // الصف السفلي
        Rect(m, c, x, y, 1, h);          // العمود الأيسر
        Rect(m, c, x + w - 1, y, 1, h);  // العمود الأيمن
    }

    // ================= نقطة الانطلاق (كما هي) =================

    void ComputeStartPose()
    {
        // أمام السيارة خط البداية مباشرة (العمودان 30-31 على المستقيم السفلي)
        float startX = 28f * tileSize + tileSize * 0.5f; // مركز العمود 28
        float startZ = 23f * tileSize + tileSize * 0.5f; // مركز الصف 23

        StartPosition = new Vector3(startX, 0f, startZ);
        StartYaw = 90f; // transform.forward يشير نحو +X => انطلاق شرقاً (في الفضاء المجسّم)
    }

    void BuildLapGates()
    {
        LapGates = new[]
        {
            new LapGate { axis = 1, plane = 24f, spanMin = 77f,   spanMax = 87f,   dir = -1 },
            new LapGate { axis = 0, plane = 48f, spanMin = 3f,    spanMax = 13f,   dir = -1 },
            new LapGate { axis = 1, plane = 24f, spanMin = 5f,    spanMax = 15f,   dir = +1 },
            new LapGate { axis = 0, plane = 62f, spanMin = 43.5f, spanMax = 52.5f, dir = +1 }
        };
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
        matWall   = MakeMat(ConcreteTexture(), 0.05f);
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

    /// <summary>خرسانة الحواجز: رمادي فاتح مع حبيبات وعروق باهتة.</summary>
    static Texture2D ConcreteTexture()
    {
        const int N = 128;
        Texture2D tex = NewTex(N);
        var rng = new System.Random(515);
        Color[] px = new Color[N * N];

        for (int y = 0; y < N; y++)
        {
            float seam = (y % 64) < 2 ? -0.10f : 0f; // مفاصل أفقية كل مترين
            for (int x = 0; x < N; x++)
            {
                float n = (float)rng.NextDouble() * 0.08f - 0.04f;
                float v = 0.70f + n + seam;
                px[y * N + x] = new Color(v, v * 0.99f, v * 0.96f, 1f);
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, false);
        tex.name = "Concrete";
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
        // أرضية عشب واسعة تمتد خارج الحوائط (أفق المشهد المجسّم)
        var acc = new MeshAccumulator();
        float mx = -40f, Mx = W * tileSize + 40f;
        float mz = -40f, Mz = H * tileSize + 40f;
        acc.AddTop(mx, Mx, mz, Mz, 0f, 1f / 3f, Color.white); // تكرار النسيج كل 3 أمتار
        EmitMesh(acc, matGround, "Ground", castShadows: false, makeStatic: true);
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
                    // uv متصل عبر البلاطات حتى يستمر نمط الكُرْد عبر الشريط كله
                    float u = (x - 30f), v = (z - 22f);
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

    void BuildWalls()
    {
        var acc = new MeshAccumulator();
        Color side = new Color(0.86f, 0.86f, 0.84f);
        Color top = new Color(0.97f, 0.97f, 0.95f);

        for (int x = 0; x < W; x++)
        {
            for (int z = 0; z < H; z++)
            {
                if (map[x, z] != WallChar) continue;

                float cx = (x + 0.5f) * tileSize;
                float cz = (z + 0.5f) * tileSize;
                acc.AddBox(new Vector3(cx, 0.55f, cz), new Vector3(tileSize, 1.1f, tileSize),
                           0.5f, top, side); // تكرار الخرسانة كل 2م
            }
        }

        EmitMesh(acc, matWall, "Walls", castShadows: true, makeStatic: true);
    }

    /// <summary>
    /// مصادمات الجدران: دمج الصفوف الأفقية المتتالية في صناديق أقل عدداً —
    /// نفس تغطية بلاطات '#' القديمة حرفياً (اللعب والتصادم كما كانا).
    /// </summary>
    void BuildWallColliders()
    {
        GameObject wallsGO = GameObject.Find("Walls");
        if (wallsGO == null) wallsGO = gameObject;

        for (int z = 0; z < H; z++)
        {
            int x = 0;
            while (x < W)
            {
                if (map[x, z] != WallChar) { x++; continue; }

                int x0 = x;
                while (x < W && map[x, z] == WallChar) x++;
                int x1 = x - 1;

                BoxCollider col = wallsGO.AddComponent<BoxCollider>();
                col.center = new Vector3((x0 + (x1 - x0 + 1) * 0.5f) * tileSize, 0.55f, (z + 0.5f) * tileSize);
                col.size = new Vector3((x1 - x0 + 1) * tileSize, 1.1f, tileSize);
            }
        }
    }

    // ================= ديكور مجسّم (مادة ملوّنة رأسياً واحدة) =================

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

        // --- أشجار الجزيرة الداخلية ---
        for (int i = 0; i < 11; i++)
        {
            float tx = rng.Next(22, 70) + (float)rng.NextDouble();
            float tz = rng.Next(20, 36) + (float)rng.NextDouble();
            AddTree(acc, new Vector3(tx, 0f, tz), 0.85f + (float)rng.NextDouble() * 0.5f,
                    rng.NextDouble() < 0.5 ? leafA : leafB, trunk);
        }

        // --- أشجار خارج الجدار (أفق المشهد) ---
        for (int i = 0; i < 7; i++)
        {
            float x = 6f + rng.Next(0, 84);
            if (x > 28f && x < 54f) x += 40f; // إبعاد عن المدرّج الشمالي
            AddTree(acc, new Vector3(x, 0f, -4f - rng.Next(0, 7)),
                    1.0f + (float)rng.NextDouble() * 0.5f,
                    rng.NextDouble() < 0.5 ? leafA : leafB, trunk);
        }

        for (int i = 0; i < 6; i++)
        {
            float x = 6f + rng.Next(0, 84);
            if (x > 18f && x < 48f) x += 40f; // إبعاد عن المدرّج الجنوبي
            AddTree(acc, new Vector3(x, 0f, 60f + rng.Next(0, 7)),
                    1.0f + (float)rng.NextDouble() * 0.5f,
                    rng.NextDouble() < 0.5 ? leafA : leafB, trunk);
        }

        for (int i = 0; i < 4; i++)
        {
            AddTree(acc, new Vector3(-4f - rng.Next(0, 6), 0f, 6f + rng.Next(0, 44)),
                    1.0f + (float)rng.NextDouble() * 0.4f, leafB, trunk);
            AddTree(acc, new Vector3(W * tileSize + 4f + rng.Next(0, 6), 0f, 6f + rng.Next(0, 44)),
                    1.0f + (float)rng.NextDouble() * 0.4f, leafA, trunk);
        }

        // --- مدرّج جمهور جنوبي (يطل على المستقيم السفلي — يواجه -Z) ---
        AddGrandstand(acc, new Vector3(33f, 0f, 60.5f), 20f, concrete, roofRed, postCol, rng, facePositiveZ: false);
        // --- مدرّج شمالي (يطل على المستقيم العلوي — يواجه +Z) ---
        AddGrandstand(acc, new Vector3(41f, 0f, -5.5f), 18f, concrete, roofRed, postCol, rng, facePositiveZ: true);

        // --- لوحات إعلانات على جدار الجزيرة (تواجه المستقيمين) ---
        Color[] boardCols =
        {
            new Color(0.90f, 0.30f, 0.25f),
            new Color(0.25f, 0.55f, 0.90f),
            new Color(0.95f, 0.78f, 0.20f),
            new Color(0.20f, 0.70f, 0.45f),
            new Color(0.90f, 0.45f, 0.15f)
        };
        for (int i = 0; i < 4; i++)
            AddBillboard(acc, new Vector3(24f + i * 15f, 0f, 41.1f), 0f, boardCols[i % boardCols.Length], postCol);
        for (int i = 0; i < 3; i++)
            AddBillboard(acc, new Vector3(27f + i * 15f, 0f, 15.2f), 0f, boardCols[(i + 2) % boardCols.Length], postCol);

        // --- بوابة الانطلاق: عمودان + لافتة مقلّمة تعلو السيارة ---
        float gx = 62f;
        acc.AddBox(new Vector3(gx, 2.6f, 42.6f), new Vector3(0.55f, 5.2f, 0.55f), 0.5f, Color.white, concrete);
        acc.AddBox(new Vector3(gx, 2.6f, 53.4f), new Vector3(0.55f, 5.2f, 0.55f), 0.5f, Color.white, concrete);
        for (int i = 0; i < 6; i++)
        {
            float z0 = 42.6f + i * (10.8f / 6f);
            Color c = (i & 1) == 0 ? roofRed : Color.white;
            acc.AddBox(new Vector3(gx, 4.65f, z0 + (10.8f / 12f)),
                       new Vector3(0.4f, 1.0f, 10.8f / 6f), 0.5f, c, c * 0.85f);
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
}
