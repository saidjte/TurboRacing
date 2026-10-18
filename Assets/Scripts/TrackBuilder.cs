using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// TurboRacing - المرحلة 2
/// يبني حلبة سباق كاملة بالكود عبر Tilemap حقيقي (بدون أي أصول خارجية):
/// - طبقة عشب أساس + طبقة طريق (حواف كيرب تلقائية + خط بداية مقلّم) + طبقة جدران
/// - الجدران: TilemapCollider2D + CompositeCollider2D لتصادم سلس مع السيارة
/// - كل الرسوم مولّدة إجرائياً (نسيج 64x64 لكل بلاطة، حجم البلاطة 2×2 وحدة)
/// - يعرّض نقطة انطلاق السيارة + استعلام IsGrass لعقاب العشب
/// </summary>
public class TrackBuilder : MonoBehaviour
{
    public static TrackBuilder Instance { get; private set; }

    [Header("أبعاد البلاطة")]
    [Tooltip("حجم البلاطة بالوحدات العالمية (2 = طريق عرضه 4 بلاطات يسمح بمناورة مريحة)")]
    public float tileSize = 2f;

    // --- نقطة الانطلاق (تُحسب تلقائياً من الخريطة) ---
    public Vector3 StartPosition { get; private set; }
    public float StartRotationZ { get; private set; }

    /// <summary>
    /// بوابات اللفة (المرحلة 3): محور + مستوى عبور + نطاق على المحور الآخر + اتجاه.
    /// axis: 0 = X ، 1 = Y. dir: +1/-1 اتجاه العبور الصحيح (عكس عقارب الساعة).
    /// [0..2] نقاط تفتيش إلزامية، [3] خط النهاية.
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

    Tilemap grassMap;
    Tilemap roadMap;
    Tilemap wallMap;

    void Awake()
    {
        Instance = this;

        char[,] map = BuildMap();

        // شبكة الحلبة
        GameObject gridGO = new GameObject("TrackGrid");
        gridGO.transform.SetParent(transform, false);

        Grid grid = gridGO.AddComponent<Grid>();
        grid.cellSize = new Vector3(tileSize, tileSize, 0f);

        grassMap = CreateLayer(gridGO, "GrassTiles", -5, withColliders: false);
        roadMap  = CreateLayer(gridGO, "RoadTiles",   0, withColliders: false);
        wallMap  = CreateLayer(gridGO, "WallTiles",   5, withColliders: true);

        Paint(map);
        ComputeStartPose();
        BuildLapGates();

        Debug.Log($"[TrackBuilder] الحلبة جاهزة: {W}×{H} بلاطة، انطلاق من {StartPosition}");
    }

    // ================= تصميم الحلبة =================

    /// <summary>
    /// حلبة مستطيلة مغلقة مع جزيرة عشبية داخلية ومشدّدين (شكانات):
    /// أعلى المستقيم العلوي وسط المستقيم السفلي - اتجاه السباق عكس عقارب الساعة.
    /// بناء الخريطة بمستطيلات مساعدة بدل نص مرسوم يدوياً = صفر أخطاء إحداثيات.
    /// </summary>
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

        // مشدّد علوي: يضيّق الطريق من 4 بلاطات إلى 2 (يجبر على التخطي من الأعلى)
        Rect(m, WallChar, 12, 4, 6, 2);

        // مشدّد سفلي: يضيّق الطريق إلى 3 بلاطات (يجبر على التخطي من الأسفل)
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

    // ================= الرسم على الطبقات =================

    void Paint(char[,] map)
    {
        Tile grassTile = MakeTile(GrassTexture(), "GrassTile");
        Tile roadTile  = MakeTile(RoadTexture(),  "RoadTile");
        Tile curbTile  = MakeTile(CurbTexture(),  "CurbTile");
        Tile startTile = MakeTile(CheckerTexture(), "StartTile");
        Tile wallTile  = MakeTile(WallTexture(),  "WallTile");

        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);

                // العشب يغطي كل الأرض كطبقة أساس
                grassMap.SetTile(cell, grassTile);

                char c = map[x, y];

                if (c == RoadChar)
                    roadMap.SetTile(cell, IsRoadEdge(map, x, y) ? curbTile : roadTile);
                else if (c == StartChar)
                    roadMap.SetTile(cell, startTile);
                else if (c == WallChar)
                    wallMap.SetTile(cell, wallTile);
            }
        }
    }

    /// <summary>بلاطة طريق مجاورة لأي شيء ليس طريقاً = حافة => كيرب أحمر/أبيض.</summary>
    static bool IsRoadEdge(char[,] map, int x, int y)
    {
        return !IsRoadLike(map, x + 1, y) ||
               !IsRoadLike(map, x - 1, y) ||
               !IsRoadLike(map, x, y + 1) ||
               !IsRoadLike(map, x, y - 1);
    }

    static bool IsRoadLike(char[,] map, int x, int y)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return false;
        char c = map[x, y];
        return c == RoadChar || c == StartChar;
    }

    // ================= الطبقات والتصادم =================

    Tilemap CreateLayer(GameObject gridGO, string layerName, int sortingOrder, bool withColliders)
    {
        GameObject layerGO = new GameObject(layerName);
        layerGO.transform.SetParent(gridGO.transform, false);

        Tilemap tm = layerGO.AddComponent<Tilemap>();

        TilemapRenderer tr = layerGO.AddComponent<TilemapRenderer>();
        tr.sortingOrder = sortingOrder;
        tr.material = new Material(Shader.Find("Sprites/Default"));

        if (withColliders)
        {
            // جسم ثابت + مصهر تصادم: كل بلاطات الجدار تصبح شكلاً موحداً ناعماً
            Rigidbody2D rb = layerGO.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            TilemapCollider2D tileCol = layerGO.AddComponent<TilemapCollider2D>();
            CompositeCollider2D comp = layerGO.AddComponent<CompositeCollider2D>();
            comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
            tileCol.usedByComposite = true;
        }

        return tm;
    }

    // ================= نقطة الانطلاق =================

    void ComputeStartPose()
    {
        // أمام السيارة خط البداية مباشرة (العمودان 30-31 على المستقيم السفلي)
        float startX = 28f * tileSize + tileSize * 0.5f; // مركز العمود 28
        float startY = 23f * tileSize + tileSize * 0.5f; // مركز الصف 23 (السيارة بطول صفّين)

        StartPosition = new Vector3(startX, startY, 0f);
        StartRotationZ = -90f; // transform.up يشير نحو +X => انطلاق نحو خط البداية (عكس عقارب الساعة)
    }

    /// <summary>
    /// بوابات اللفة بإحداثيات عالمية (كل البوابات منتصف المستقيم بعيداً عن الأركان):
    /// [0] يمين: عبور Y=24 صعوداً على أيمن المستقيم الأيمن (X من 77 إلى 87)
    /// [1] أعلى: عبور X=48 يساراً وسط المستقيم العلوي (Y من 3 إلى 13)
    /// [2] يسار: عبور Y=24 هبوطاً على أيمن المستقيم الأيسر (X من 5 إلى 15)
    /// [3] نهاية: عبور X=62 يميناً وسط خط البداية المقلّم (Y من 43.5 إلى 52.5)
    /// </summary>
    void BuildLapGates()
    {
        LapGates = new[]
        {
            new LapGate { axis = 1, plane = 24f, spanMin = 77f,   spanMax = 87f,   dir = +1 },
            new LapGate { axis = 0, plane = 48f, spanMin = 3f,    spanMax = 13f,   dir = -1 },
            new LapGate { axis = 1, plane = 24f, spanMin = 5f,    spanMax = 15f,   dir = -1 },
            new LapGate { axis = 0, plane = 62f, spanMin = 43.5f, spanMax = 52.5f, dir = +1 }
        };
    }

    // ================= استعلام السطح (لعقاب العشب) =================

    /// <summary>
    /// هل هذا الموضع العالمي على العشب؟
    /// أي بلاطة ليس عليها طريق = عشب (الجدار يمنع الوصول إليه أصلاً).
    /// </summary>
    public bool IsGrass(Vector3 worldPos)
    {
        if (roadMap == null) return false;
        Vector3Int cell = roadMap.WorldToCell(worldPos);
        return roadMap.GetTile(cell) == null;
    }

    // ================= توليد البلاطات إجرائياً =================

    Tile MakeTile(Texture2D tex, string tileName)
    {
        tex.name = tileName + "_Tex";
        tex.Apply(false, false);

        // PPU = 64 / tileSize => البلاطة تشغل tileSize × tileSize وحدة عالمية
        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, 64, 64),
            new Vector2(0.5f, 0.5f),
            64f / tileSize);
        sprite.name = tileName + "_Sprite";

        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        // البلاطة تتموضع تلقائياً بمحاذاة مركز الخلية (pivot المركز للسبرايت)
        tile.name = tileName;
        return tile;
    }

    Texture2D NewTex()
    {
        Texture2D tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point; // مظهر بكسلي حاد
        return tex;
    }

    /// <summary>عشب: درجتان متناوبتان + حبيبات عشوائية + شفرات داكنة (بذرة ثابتة).</summary>
    Texture2D GrassTexture()
    {
        Texture2D tex = NewTex();
        Color patchA = new Color(0.23f, 0.46f, 0.16f);
        Color patchB = new Color(0.21f, 0.43f, 0.15f);
        Color blade  = new Color(0.16f, 0.36f, 0.11f);

        var rng = new System.Random(77);
        Color[] px = new Color[64 * 64];

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                Color c = ((x / 8 + y / 8) % 2 == 0) ? patchA : patchB;

                float n = (float)rng.NextDouble() * 0.06f - 0.03f;
                c.r += n; c.g += n; c.b += n;

                if (rng.Next(100) < 5) c = blade;

                px[y * 64 + x] = c;
            }
        }

        tex.SetPixels(px);
        return tex;
    }

    /// <summary>إسفلت داكن مع حصى فاتح وداكن.</summary>
    Texture2D RoadTexture()
    {
        Texture2D tex = NewTex();
        Color asphalt = new Color(0.25f, 0.25f, 0.28f);
        Color pebbleLight = new Color(0.32f, 0.32f, 0.35f);
        Color pebbleDark  = new Color(0.18f, 0.18f, 0.21f);

        var rng = new System.Random(123);
        Color[] px = new Color[64 * 64];

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                Color c = asphalt;

                float n = (float)rng.NextDouble() * 0.04f - 0.02f;
                c.r += n; c.g += n; c.b += n;

                int roll = rng.Next(100);
                if (roll < 4) c = pebbleLight;
                else if (roll < 8) c = pebbleDark;

                px[y * 64 + x] = c;
            }
        }

        tex.SetPixels(px);
        return tex;
    }

    /// <summary>كيرب: نفس الإسفلت مع إطار أحمر/أبيض مائل على الأطراف.</summary>
    Texture2D CurbTexture()
    {
        Texture2D tex = RoadTexture();
        Color red   = new Color(0.78f, 0.16f, 0.14f);
        Color white = new Color(0.92f, 0.92f, 0.90f);

        Color[] px = tex.GetPixels();
        const int Border = 10;

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                bool edge = x < Border || x > 63 - Border || y < Border || y > 63 - Border;
                if (!edge) continue;

                px[y * 64 + x] = (((x + y) / 12) % 2 == 0) ? red : white;
            }
        }

        tex.SetPixels(px);
        return tex;
    }

    /// <summary>خط البداية: رقعة شطرنج أبيض/أسود.</summary>
    Texture2D CheckerTexture()
    {
        Texture2D tex = NewTex();
        Color black = new Color(0.08f, 0.08f, 0.08f);
        Color white = new Color(0.95f, 0.95f, 0.95f);

        Color[] px = new Color[64 * 64];

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                px[y * 64 + x] = ((x / 8 + y / 8) % 2 == 0) ? white : black;
            }
        }

        tex.SetPixels(px);
        return tex;
    }

    /// <summary>جدار حماية: خطوط تحذير قطرية صفراء/سوداء.</summary>
    Texture2D WallTexture()
    {
        Texture2D tex = NewTex();
        Color dark   = new Color(0.12f, 0.12f, 0.14f);
        Color yellow = new Color(0.85f, 0.68f, 0.10f);

        var rng = new System.Random(555);
        Color[] px = new Color[64 * 64];

        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                Color c = (((x + y) / 8) % 2 == 0) ? yellow : dark;

                float n = (float)rng.NextDouble() * 0.05f - 0.025f;
                c.r += n; c.g += n; c.b += n;

                px[y * 64 + x] = c;
            }
        }

        tex.SetPixels(px);
        return tex;
    }
}
