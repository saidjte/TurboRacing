using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

/// <summary>
/// TurboRacing - v0.9.0 (المظهر شبه ثلاثي الأبعاد للحلبة)
/// يبني حلبة سباق كاملة بالكود عبر Tilemap حقيقي (بدون أي أصول خارجية) — نفس
/// الخريطة والبوابات والتصادم القديمة حرفياً (اللعب لم يتغير إطلاقاً)، لكن:
/// - كل البلاطات 128×128 بفلتر Bilinear (مظهر ناعم بدل البكسلي المسطح)
/// - الجدران: وجوه علوية إسمنتية بحواف مشطوفة (Bevel) = صناديق حواجز مجسمة
/// - ظلال ملقاة: كل جدار يرمي ظلاً متدرجاً جنوباً وشرقاً (شمس من الشمال الغربي)
///   على الطريق والعشب — أقوى إشارة ارتفاع في المنظور العلوي
/// - العشب: خطوط جز بمائلة مستمرة عبر الخريطة + بقع ورُبُع وزهور نادرة
/// - الإسفلت: بقع قيمة كبيرة + حصى + شقوق + إطار AO عند الكيرب
/// - أشجار مظللة (تاج طبقي + ظل أرضي) على الهوامش وبالجزيرة الداخلية
/// - بوابة بداية: قاعدتا أعمدة + لافتة مقلّمة تعلو السيارة عند العبور
/// - حلقة طلاء بيضاء في الجزيرة (لمسة مدرسة سباق)
/// يعرّض نقطة انطلاق السيارة + استعلام IsGrass لعقاب العشب.
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

    // --- أبعاد عالمية للكاميرا (وضع القائمة) ---
    public Vector3 MapCenter { get; private set; }
    public float MapWidthUnits { get; private set; }
    public float MapHeightUnits { get; private set; }

    /// <summary>
    /// بوابات اللفة: محور + مستوى عبور + نطاق على المحور الآخر + اتجاه.
    /// axis: 0 = X ، 1 = Y. dir: +1/-1 اتجاه العبور الصحيح.
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
    const int Tex = 128; // دقة نسيج البلاطة (v0.9.0: 128 بدل 64)

    // رموز الخريطة: '#' جدار   '.' عشب   'R' طريق   'S' خط البداية
    const char WallChar = '#';
    const char GrassChar = '.';
    const char RoadChar = 'R';
    const char StartChar = 'S';

    Tilemap grassMap;
    Tilemap roadMap;
    Tilemap wallMap;
    Tilemap shadowMap;

    void Awake()
    {
        Instance = this;

        char[,] map = BuildMap();

        // شبكة الحلبة
        GameObject gridGO = new GameObject("TrackGrid");
        gridGO.transform.SetParent(transform, false);

        Grid grid = gridGO.AddComponent<Grid>();
        grid.cellSize = new Vector3(tileSize, tileSize, 0f);

        grassMap  = CreateLayer(gridGO, "GrassTiles",  -5, withColliders: false);
        roadMap   = CreateLayer(gridGO, "RoadTiles",    0, withColliders: false);
        shadowMap = CreateLayer(gridGO, "ShadowTiles",  2, withColliders: false); // (v0.9.0)
        wallMap   = CreateLayer(gridGO, "WallTiles",    5, withColliders: true);

        Paint(map);
        PaintWallShadows(map);   // (v0.9.0) ظلال الجدران على الأرض
        AddInfieldDeco();        // (v0.9.0) حلقة طلاء بيضاء
        AddTrees(map);           // (v0.9.0) أشجار مظللة
        AddStartGantry();        // (v0.9.0) بوابة خط البداية
        ComputeStartPose();
        BuildLapGates();

        // أبعاد عالمية (كاميرا القائمة)
        MapWidthUnits = W * tileSize;
        MapHeightUnits = H * tileSize;
        MapCenter = new Vector3(MapWidthUnits * 0.5f, MapHeightUnits * 0.5f, 0f);

        Debug.Log($"[TrackBuilder] الحلبة جاهزة: {W}×{H} بلاطة، انطلاق من {StartPosition}");
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

    // ================= الرسم على الطبقات =================

    void Paint(char[,] map)
    {
        Tile grassA = MakeTile(GrassTexture(0), "GrassTileA");
        Tile grassB = MakeTile(GrassTexture(1), "GrassTileB");
        Tile roadA  = MakeTile(RoadTexture(0),  "RoadTileA");
        Tile roadB  = MakeTile(RoadTexture(1),  "RoadTileB");
        Tile curbA  = MakeTile(CurbTexture(0),  "CurbTileA");
        Tile curbB  = MakeTile(CurbTexture(1),  "CurbTileB");
        Tile startTile = MakeTile(CheckerTexture(), "StartTile");
        Tile wallA  = MakeTile(WallTexture(0),  "WallTileA");
        Tile wallB  = MakeTile(WallTexture(1),  "WallTileB");

        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                bool alt = ((x + y) & 1) == 0;

                // العشب يغطي كل الأرض كطبقة أساس (نسختان لكسر الرتابة)
                grassMap.SetTile(cell, alt ? grassA : grassB);

                char c = map[x, y];
                // تنويع زائف للنسخ الثاني (بذرة مختلفة لكل بلاطة)
                bool v2 = ((x * 7 + y * 13) & 1) == 0;

                if (c == RoadChar)
                {
                    bool edge = IsRoadEdge(map, x, y);
                    roadMap.SetTile(cell, edge ? (v2 ? curbA : curbB) : (v2 ? roadA : roadB));
                }
                else if (c == StartChar)
                    roadMap.SetTile(cell, startTile);
                else if (c == WallChar)
                    wallMap.SetTile(cell, alt ? wallA : wallB);
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

    // ================= ظلال الجدران (v0.9.0) =================

    /// <summary>
    /// شمس من الشمال الغربي: كل بلاطة جدار مفتوحة من الجنوب ترمي ظلاً على الجار
    /// الجنوبي (يتدلى من الحافة العلوية للبلاطة المظلولة)، ومفتوحة من الشرق ترمي
    /// ظلاً على الجار الشرقي (من حافته اليسرى). الزاويتان تجمعان الظلين.
    /// </summary>
    void PaintWallShadows(char[,] map)
    {
        Tile shS  = MakeTile(ShadowTileTexture(false, true),  "WallShadowS");
        Tile shE  = MakeTile(ShadowTileTexture(true,  false), "WallShadowE");
        Tile shSE = MakeTile(ShadowTileTexture(true,  true),  "WallShadowSE");

        for (int x = 0; x < W; x++)
        {
            for (int y = 0; y < H; y++)
            {
                if (map[x, y] != WallChar) continue;

                bool south = Open(map, x, y - 1);
                bool east  = Open(map, x + 1, y);

                // ظل جنوبي: يلتصق بأعلى البلاطة الواقعة جنوب الجدار
                if (south) shadowMap.SetTile(new Vector3Int(x, y - 1, 0), shS);
                // ظل شرقي: يلتصق بيسار البلاطة الواقعة شرق الجدار
                if (east) shadowMap.SetTile(new Vector3Int(x + 1, y, 0), shE);

                // زاوية محدبة: البلاطة القطرية تستقبل الظل المزدوج (انحناء الظل حول الصندوق)
                if (south && east && Open(map, x + 1, y - 1))
                    shadowMap.SetTile(new Vector3Int(x + 1, y - 1, 0), shSE);
            }
        }
    }

    /// <summary>هل البلاطة أرض مفتوحة (عشب/طريق/بداية) يمكن أن يقع عليها ظل؟</summary>
    static bool Open(char[,] map, int x, int y)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return false;
        char c = map[x, y];
        return c == GrassChar || c == RoadChar || c == StartChar;
    }

    // ================= الأشجار والديكور (v0.9.0) =================

    void AddTrees(char[,] map)
    {
        Sprite blob = BlobSprite();
        var rng = new System.Random(20240);
        var taken = new List<Vector2Int>();

        // 1) أشجار الجزيرة الداخلية (كبيرة)
        int placed = 0;
        for (int tries = 0; tries < 120 && placed < 9; tries++)
        {
            int x = rng.Next(10, 36);
            int y = rng.Next(10, 18);
            if (map[x, y] != GrassChar) continue;
            if (!Open(map, x - 1, y) || !Open(map, x + 1, y) ||
                !Open(map, x, y - 1) || !Open(map, x, y + 1)) continue;

            bool far = true;
            foreach (var t in taken)
                if (Mathf.Abs(t.x - x) < 5 && Mathf.Abs(t.y - y) < 4) { far = false; break; }
            if (!far) continue;

            taken.Add(new Vector2Int(x, y));
            float s = 1.0f + (float)rng.NextDouble() * 0.25f;
            MakeTree(new Vector3((x + 0.5f) * tileSize, (y + 0.5f) * tileSize, 0f), s, blob, rng);
            placed++;
        }

        // 2) هوامش خارجية: صف علوي (y=1) وصف سفلي (y=26) وأعمدة يسرى/يمنى (x=1,44)
        var margin = new List<Vector2Int>();
        for (int x = 4; x <= 42; x += 4) { margin.Add(new Vector2Int(x, 1)); margin.Add(new Vector2Int(x, 26)); }
        for (int y = 4; y <= 24; y += 4) { margin.Add(new Vector2Int(1, y));  margin.Add(new Vector2Int(44, y)); }

        foreach (var c in margin)
        {
            if (c.x < 0 || c.y < 0 || c.x >= W || c.y >= H) continue;
            if (map[c.x, c.y] != GrassChar) continue;

            int jx = c.x + rng.Next(-1, 2);
            int jy = c.y + rng.Next(0, 2);
            if (jx < 1 || jy < 1 || jx >= W - 1 || jy >= H - 1) continue;
            if (map[jx, jy] != GrassChar) continue;

            float s = 0.62f + (float)rng.NextDouble() * 0.22f;
            MakeTree(new Vector3((jx + 0.5f) * tileSize, (jy + 0.5f) * tileSize, 0f), s, blob, rng);
        }
    }

    /// <summary>شجرة: ظل أرضي (جنوب-شرق باتجاه الشمس) + تاج طبقي يعلو كل شيء.</summary>
    void MakeTree(Vector3 pos, float scale, Sprite blob, System.Random rng)
    {
        GameObject tree = new GameObject("Tree");
        tree.transform.SetParent(transform, false);
        tree.transform.position = pos;

        // الظل الأرضي
        GameObject shGO = new GameObject("TreeShadow");
        shGO.transform.SetParent(tree.transform, false);
        SpriteRenderer shSR = shGO.AddComponent<SpriteRenderer>();
        shSR.sprite = blob;
        shSR.sortingOrder = 2;
        shSR.color = new Color(0f, 0f, 0f, 0.36f);
        shGO.transform.localPosition = new Vector3(0.50f, -0.62f, 0f);
        shGO.transform.localScale = new Vector3(1.55f * scale, 1.15f * scale, 1f);

        // التاج (فوق السيارة والجدران واللافتة: الشجرة أطول من الجميع)
        GameObject topGO = new GameObject("TreeTop");
        topGO.transform.SetParent(tree.transform, false);
        SpriteRenderer sr = topGO.AddComponent<SpriteRenderer>();
        sr.sprite = TreeSprite();
        sr.sortingOrder = 12;
        float g = 0.88f + (float)rng.NextDouble() * 0.12f;
        sr.color = new Color(g, g, g, 1f); // تنويع خفيف بين الأشجار
        topGO.transform.localScale = new Vector3(scale, scale, 1f);
    }

    void AddInfieldDeco()
    {
        // حلقة طلاء بيضاء باهتة وسط الجزيرة (لمسة مدارس السباق)
        GameObject ringGO = new GameObject("PaintRing");
        ringGO.transform.SetParent(transform, false);
        ringGO.transform.position = new Vector3(46f, 28f, 0f);
        SpriteRenderer sr = ringGO.AddComponent<SpriteRenderer>();
        sr.sprite = PaintRingSprite();
        sr.sortingOrder = 1;
        sr.color = new Color(1f, 1f, 1f, 0.9f);
        ringGO.transform.localScale = new Vector3(1.7f, 1.7f, 1f);
    }

    void AddStartGantry()
    {
        // خط البداية عند X≈62 (السيارة تعبر شرقاً) — اللافتة تعلو السيارة عند المرور
        float gx = 62f;

        Sprite blob = BlobSprite();
        Sprite basePlate = PostBaseSprite();

        foreach (float py in new float[] { 42.4f, 53.6f })
        {
            // ظل القاعدة
            GameObject shGO = new GameObject("PostShadow");
            shGO.transform.SetParent(transform, false);
            shGO.transform.position = new Vector3(gx + 0.55f, py - 0.45f, 0f);
            SpriteRenderer shSR = shGO.AddComponent<SpriteRenderer>();
            shSR.sprite = blob;
            shSR.sortingOrder = 2;
            shSR.color = new Color(0f, 0f, 0f, 0.32f);
            shGO.transform.localScale = new Vector3(0.95f, 0.75f, 1f);

            // قاعدة العمود
            GameObject pGO = new GameObject("PostBase");
            pGO.transform.SetParent(transform, false);
            pGO.transform.position = new Vector3(gx, py, 0f);
            SpriteRenderer pSR = pGO.AddComponent<SpriteRenderer>();
            pSR.sprite = basePlate;
            pSR.sortingOrder = 4;
        }

        // ظل اللافتة على الطريق (شرقها باتجاه الشمس)
        GameObject bShGO = new GameObject("BannerShadow");
        bShGO.transform.SetParent(transform, false);
        bShGO.transform.position = new Vector3(gx + 0.85f, 48f, 0f);
        SpriteRenderer bShSR = bShGO.AddComponent<SpriteRenderer>();
        bShSR.sprite = BlobSprite();
        bShSR.sortingOrder = 2;
        bShSR.color = new Color(0f, 0f, 0f, 0.30f);
        bShGO.transform.localScale = new Vector3(0.9f, 11.5f, 1f);

        // اللافتة المقلّمة نفسها (فوق السيارة = تعبر تحتها)
        GameObject bGO = new GameObject("StartBanner");
        bGO.transform.SetParent(transform, false);
        bGO.transform.position = new Vector3(gx, 48f, 0f);
        SpriteRenderer bSR = bGO.AddComponent<SpriteRenderer>();
        bSR.sprite = BannerSprite();
        bSR.sortingOrder = 12;
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

    // ================= نقطة الانطلاق (كما هي) =================

    void ComputeStartPose()
    {
        // أمام السيارة خط البداية مباشرة (العمودان 30-31 على المستقيم السفلي)
        float startX = 28f * tileSize + tileSize * 0.5f; // مركز العمود 28
        float startY = 23f * tileSize + tileSize * 0.5f; // مركز الصف 23

        StartPosition = new Vector3(startX, startY, 0f);
        StartRotationZ = -90f; // transform.up يشير نحو +X => انطلاق شرقاً
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

    public bool IsGrass(Vector3 worldPos)
    {
        if (roadMap == null) return false;
        Vector3Int cell = roadMap.WorldToCell(worldPos);
        return roadMap.GetTile(cell) == null;
    }

    // ================= توليد البلاطات إجرائياً (128px, Bilinear) =================

    Tile MakeTile(Texture2D tex, string tileName)
    {
        tex.name = tileName + "_Tex";
        tex.Apply(false, false);

        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, Tex, Tex),
            new Vector2(0.5f, 0.5f),
            (float)Tex / tileSize);
        sprite.name = tileName + "_Sprite";

        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.name = tileName;
        return tile;
    }

    Texture2D NewTex()
    {
        Texture2D tex = new Texture2D(Tex, Tex, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear; // (v0.9.0) نعومة تدعم إحساس العمق
        return tex;
    }

    /// <summary>عشب: خطوط جز مائلة مستمرة + حبيبات + شفرات + زهور نادرة.</summary>
    Texture2D GrassTexture(int variant)
    {
        Texture2D tex = NewTex();
        Color baseA = variant == 0
            ? new Color(0.235f, 0.470f, 0.160f)
            : new Color(0.220f, 0.445f, 0.150f);
        Color blade = new Color(0.155f, 0.350f, 0.105f);
        Color flowerW = new Color(0.92f, 0.92f, 0.85f);
        Color flowerY = new Color(0.92f, 0.80f, 0.25f);

        var rng = new System.Random(77 + variant);
        Color[] px = new Color[Tex * Tex];

        for (int y = 0; y < Tex; y++)
        {
            for (int x = 0; x < Tex; x++)
            {
                Color c = baseA;

                // خطوط جز مائلة (الفترة 32px تقسم 128 => تستمر عبر البلاطات)
                float s = Mathf.Sin((x + y) * Mathf.PI / 32f) * 0.035f;
                c.r += s * 0.5f; c.g += s; c.b += s * 0.5f;

                float n = (float)rng.NextDouble() * 0.05f - 0.025f;
                c.r += n; c.g += n; c.b += n;

                int roll = rng.Next(1000);
                if (roll < 40) c = blade;                      // شفرات داكنة
                else if (roll < 43) c = flowerW;               // زهرة بيضاء نادرة
                else if (roll < 45) c = flowerY;               // زهرة صفراء نادرة

                px[y * Tex + x] = c;
            }
        }

        tex.SetPixels(px);
        return tex;
    }

    /// <summary>إسفلت: بقع قيمة كبيرة + حبيبات + حصى + شقوق.</summary>
    Texture2D RoadTexture(int variant)
    {
        Texture2D tex = NewTex();
        Color asphalt = new Color(0.255f, 0.255f, 0.285f);
        Color pebbleLight = new Color(0.335f, 0.335f, 0.365f);
        Color pebbleDark  = new Color(0.180f, 0.180f, 0.205f);
        Color crack = new Color(0.150f, 0.150f, 0.170f);

        var rng = new System.Random(123 + variant * 7);
        Color[] px = new Color[Tex * Tex];

        // بقع قيمة كبيرة (4 دوائر ناعمة) لكسر الرتابة
        var blobs = new List<Vector3>();
        for (int i = 0; i < 4; i++)
            blobs.Add(new Vector3(rng.Next(Tex), rng.Next(Tex), 30f + rng.Next(30)));

        for (int y = 0; y < Tex; y++)
        {
            for (int x = 0; x < Tex; x++)
            {
                Color c = asphalt;

                foreach (var b in blobs)
                {
                    float dx = Mathf.Abs(x - b.x); dx = Mathf.Min(dx, Tex - dx);
                    float dy = Mathf.Abs(y - b.y); dy = Mathf.Min(dy, Tex - dy);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < b.z)
                    {
                        float k = (1f - d / b.z) * 0.05f;
                        c.r += k; c.g += k; c.b += k;
                    }
                }

                float n = (float)rng.NextDouble() * 0.04f - 0.02f;
                c.r += n; c.g += n; c.b += n;

                int roll = rng.Next(100);
                if (roll < 5) c = pebbleLight;
                else if (roll < 9) c = pebbleDark;

                px[y * Tex + x] = c;
            }
        }

        // شقوق قصيرة
        for (int i = 0; i < 2; i++)
        {
            int cxp = rng.Next(Tex), cyp = rng.Next(Tex);
            int len = 10 + rng.Next(14);
            float dir = (float)rng.NextDouble() * Mathf.PI * 2f;
            for (int s = 0; s < len; s++)
            {
                int xx = Mathf.Clamp(Mathf.RoundToInt(cxp + Mathf.Cos(dir) * s), 0, Tex - 1);
                int yy = Mathf.Clamp(Mathf.RoundToInt(cyp + Mathf.Sin(dir) * s), 0, Tex - 1);
                px[yy * Tex + xx] = crack;
            }
        }

        tex.SetPixels(px);
        return tex;
    }

    /// <summary>كيرب: أحمر/أبيض بتدرج لكل شريحة + خط خارجي داكن + AO نحو الإسفلت.</summary>
    Texture2D CurbTexture(int variant)
    {
        Texture2D tex = RoadTexture(variant);
        Color red   = new Color(0.78f, 0.16f, 0.14f);
        Color white = new Color(0.93f, 0.93f, 0.90f);

        Color[] px = tex.GetPixels();
        const int Border = 14;

        for (int y = 0; y < Tex; y++)
        {
            for (int x = 0; x < Tex; x++)
            {
                bool edge = x < Border || x > Tex - 1 - Border || y < Border || y > Tex - 1 - Border;
                if (!edge) continue;

                // شريحة بتدرج إضاءة (مصدر ضوء ثابت أعلى-يسار)
                float stripePos = Mathf.Repeat(x + y, 28f);
                float shade = Mathf.Lerp(1.12f, 0.88f, stripePos / 28f);
                Color c = ((x + y) / 14) % 2 == 0 ? red : white;
                c.r = Mathf.Clamp01(c.r * shade);
                c.g = Mathf.Clamp01(c.g * shade);
                c.b = Mathf.Clamp01(c.b * shade);

                // خط خارجي داكن (AO مع ما حول الطريق)
                bool outer = x < 3 || x > Tex - 4 || y < 3 || y > Tex - 4;
                if (outer) c *= 0.45f;

                px[y * Tex + x] = c;
            }
        }

        // ظل ناعم داخل الإسفلت عند حدود الكيرب (اتصال واقعي)
        for (int y = Border; y < Tex - Border; y++)
        {
            for (int x = Border; x < Tex - Border; x++)
            {
                float dEdge = Mathf.Min(Mathf.Min(x - Border, Tex - 1 - Border - x),
                              Mathf.Min(y - Border, Tex - 1 - Border - y));
                if (dEdge < 6f)
                {
                    Color c = px[y * Tex + x];
                    float k = 0.22f * (1f - dEdge / 6f);
                    px[y * Tex + x] = new Color(c.r * (1f - k), c.g * (1f - k), c.b * (1f - k), 1f);
                }
            }
        }

        tex.SetPixels(px);
        return tex;
    }

    /// <summary>خط البداية: رقعة شطرنج بتدرج خفيف + شبكة داكنة رقيقة.</summary>
    Texture2D CheckerTexture()
    {
        Texture2D tex = NewTex();
        Color black = new Color(0.09f, 0.09f, 0.09f);
        Color white = new Color(0.94f, 0.94f, 0.94f);

        var rng = new System.Random(321);
        Color[] px = new Color[Tex * Tex];

        for (int y = 0; y < Tex; y++)
        {
            for (int x = 0; x < Tex; x++)
            {
                Color c = ((x / 16 + y / 16) % 2 == 0) ? white : black;
                float n = (float)rng.NextDouble() * 0.05f - 0.025f;
                c.r += n; c.g += n; c.b += n;
                if (x % 16 == 0 || y % 16 == 0) c *= 0.55f; // فواصل رقيقة
                px[y * Tex + x] = c;
            }
        }

        tex.SetPixels(px);
        return tex;
    }

    /// <summary>
    /// الجدار v0.9.0: وجه علوي لإسمنت على شكل صندوق حاجز:
    /// حواف مشطوفة (أعلى/يسار مضيئة، أسفل/يمين داكنة) + خطوط تحذير مائلة
    /// + خشونة + خط داكن حول كل كتلة (فواصل الصناديق المكدسة).
    /// </summary>
    Texture2D WallTexture(int variant)
    {
        Texture2D tex = NewTex();
        Color yellow = new Color(0.85f, 0.66f, 0.12f);
        Color darkStripe = new Color(0.15f, 0.15f, 0.17f);
        Color concrete = new Color(0.52f, 0.53f, 0.57f);

        var rng = new System.Random(555 + variant * 11);
        Color[] px = new Color[Tex * Tex];
        int phase = variant * 8; // إزاحة نصف فترة => كل كتلة مختلفة عن جارتها

        for (int y = 0; y < Tex; y++)
        {
            for (int x = 0; x < Tex; x++)
            {
                int sx = x + phase;
                Color c = (((sx + y) / 16) % 2 == 0) ? yellow : darkStripe;

                // خشونة
                float n = (float)rng.NextDouble() * 0.06f - 0.03f;
                c.r += n; c.g += n; c.b += n;

                // شرائط الإسمنت بين الخطوط (تخفيف التباين)
                if (((sx + y) / 16) % 2 == 0)
                    c = Color.Lerp(c, concrete, 0.18f);

                px[y * Tex + x] = c;
            }
        }

        // Bevel: أعلى ويسار مضيء، أسفل ويمين داكن — يوهم بالسماكة
        for (int y = 0; y < Tex; y++)
        {
            for (int x = 0; x < Tex; x++)
            {
                Color c = px[y * Tex + x];
                float k = 1f;

                if (y >= Tex - 5) k = Mathf.Lerp(1.28f, 1f, (Tex - 1 - y) / 5f);      // الحافة العلوية
                else if (y < 5)   k = Mathf.Lerp(0.55f, 1f, y / 5f);                  // السفلية
                if (x < 5)        k *= Mathf.Lerp(1.22f, 1f, x / 5f);                 // اليسرى
                else if (x >= Tex - 5) k *= Mathf.Lerp(0.60f, 1f, (Tex - 1 - x) / 5f);// اليمنى

                if (k != 1f)
                {
                    c.r = Mathf.Clamp01(c.r * k);
                    c.g = Mathf.Clamp01(c.g * k);
                    c.b = Mathf.Clamp01(c.b * k);
                }

                // خط داكن حول الكتلة (فاصل الصناديق)
                if (x == 0 || y == 0 || x == Tex - 1 || y == Tex - 1) c *= 0.38f;

                px[y * Tex + x] = c;
            }
        }

        tex.SetPixels(px);
        return tex;
    }

    /// <summary>بلاطة ظل متدرجة: من الحافة المواجهة للجدار تنسدل وتتلاشى.</summary>
    Texture2D ShadowTileTexture(bool fromEast, bool fromSouth)
    {
        Texture2D tex = NewTex();
        Color[] px = new Color[Tex * Tex];

        for (int y = 0; y < Tex; y++)
        {
            for (int x = 0; x < Tex; x++)
            {
                float a = 0f;

                if (fromSouth) // الجدار شمال البلاطة: الظل يلتصق بأعلى بلاطتها
                {
                    float dTop = (Tex - 1 - y);
                    if (dTop < 58f) a = Mathf.Max(a, 0.34f * Mathf.Pow(1f - dTop / 58f, 1.25f));
                }
                if (fromEast)  // الجدار غرب البلاطة: الظل يلتصق بيسارها
                {
                    if (x < 46f) a = Mathf.Max(a, 0.28f * Mathf.Pow(1f - x / 46f, 1.25f));
                }

                px[y * Tex + x] = new Color(0f, 0f, 0f, a);
            }
        }

        tex.SetPixels(px);
        return tex;
    }

    // ================= سبرايتات الديكور =================

    /// <summary>تاج شجرة: دوائر ناعمة متراكبة (قاعدة داكنة + وسط + لمعة شمال-غرب).</summary>
    Sprite TreeSprite()
    {
        const int S = 160;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color[] px = new Color[S * S];
        var rng = new System.Random(606);

        SoftCircle(px, S, 84f, 76f, 56f, new Color(0.10f, 0.27f, 0.10f, 1f));
        SoftCircle(px, S, 72f, 90f, 45f, new Color(0.16f, 0.40f, 0.14f, 1f));
        SoftCircle(px, S, 63f, 99f, 27f, new Color(0.25f, 0.51f, 0.18f, 0.85f));
        SoftCircle(px, S, 97f, 63f, 20f, new Color(0.07f, 0.20f, 0.08f, 0.9f)); // قعر داكن جنوب-شرق
        SoftCircle(px, S, 96f, 62f, 7f,  new Color(0.33f, 0.22f, 0.13f, 0.95f));// ثمرة جذع بارزة

        // حبيبات أوراق
        for (int i = 0; i < 260; i++)
        {
            int x = rng.Next(S), y = rng.Next(S);
            if (px[y * S + x].a > 0.5f)
            {
                Color c = px[y * S + x];
                float n = (float)rng.NextDouble() * 0.10f - 0.05f;
                px[y * S + x] = new Color(Mathf.Clamp01(c.r + n), Mathf.Clamp01(c.g + n), Mathf.Clamp01(c.b + n), c.a);
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, false);

        Sprite sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 64f);
        sp.name = "TreeSprite";
        return sp;
    }

    /// <summary>قرص ظل ناعم (أشجار/أعمدة/لافتة).</summary>
    Sprite BlobSprite()
    {
        const int S = 96;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color[] px = new Color[S * S];
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float nx = (x + 0.5f) / S * 2f - 1f;
                float ny = (y + 0.5f) / S * 2f - 1f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float a = Mathf.Pow(1f - Mathf.SmoothStep(0.05f, 1f, d), 1.3f);
                px[y * S + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, false);

        Sprite sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 64f);
        sp.name = "BlobShadowSprite";
        return sp;
    }

    /// <summary>حلقة طلاء بيضاء باهتة (بليت قديم على العشب).</summary>
    Sprite PaintRingSprite()
    {
        const int S = 256;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        var rng = new System.Random(808);
        Color[] px = new Color[S * S];
        float c = S * 0.5f;

        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                float band = Mathf.SmoothStep(96f, 101f, d) * (1f - Mathf.SmoothStep(117f, 122f, d));
                float erode = (float)rng.NextDouble() < 0.28f ? 0.35f : 1f; // طلاء متآكل
                float a = band * 0.52f * erode;
                px[y * S + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, false);

        Sprite sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 64f);
        sp.name = "PaintRingSprite";
        return sp;
    }

    /// <summary>قاعدة عمود البوابة: صفيحة داكنة + صرة معدنية.</summary>
    Sprite PostBaseSprite()
    {
        const int S = 40;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color[] px = new Color[S * S];
        float c = S * 0.5f;

        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                Color col = new Color(0, 0, 0, 0);
                if (d < 17f) col = new Color(0.13f, 0.13f, 0.15f, 1f);   // الصفيحة
                if (d < 16f && (x + y) > S) col *= 0.65f;                 // ظل نصفها
                if (d < 8f) col = new Color(0.34f, 0.35f, 0.38f, 1f);     // الصرة
                if (d < 6.2f && d > 4.6f) col = new Color(0.55f, 0.56f, 0.60f, 1f); // حلقة
                if (d < 3f) col = new Color(0.12f, 0.12f, 0.14f, 1f);     // المركز
                if (d > 16.2f && d < 17.4f) col = new Color(0.05f, 0.05f, 0.06f, 0.9f); // حافة
                px[y * S + x] = col;
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, false);

        Sprite sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 32f);
        sp.name = "PostBaseSprite";
        return sp;
    }

    /// <summary>لافتة البوابة: رقعة شطرنج 2×12 مع إطار ولمعة علوية (1.25×11.75 وحدة).</summary>
    Sprite BannerSprite()
    {
        const int SW = 40, SH = 376;
        Texture2D tex = new Texture2D(SW, SH, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color black = new Color(0.10f, 0.10f, 0.11f);
        Color white = new Color(0.93f, 0.93f, 0.93f);
        Color frame = new Color(0.16f, 0.16f, 0.18f);

        Color[] px = new Color[SW * SH];

        for (int y = 0; y < SH; y++)
        {
            for (int x = 0; x < SW; x++)
            {
                Color c;
                if (x < 3 || x >= SW - 3 || y < 3 || y >= SH - 3) c = frame;
                else
                {
                    int col = (x - 3) / 17;          // خليتان عرضياً
                    int row = (y - 3) / 31;          // 12 خلية طولياً
                    c = (col + row) % 2 == 0 ? white : black;
                    float n = ((x * 31 + y * 17) % 9) * 0.006f;
                    c.r = Mathf.Clamp01(c.r + n); c.g = Mathf.Clamp01(c.g + n); c.b = Mathf.Clamp01(c.b + n);
                }
                px[y * SW + x] = c;
            }
        }

        // لمعة الحافة الغربية (جهة الشمس)
        for (int y = 0; y < SH; y++)
        {
            Color c = px[y * SW + 3];
            px[y * SW + 3] = new Color(Mathf.Clamp01(c.r + 0.22f), Mathf.Clamp01(c.g + 0.22f), Mathf.Clamp01(c.b + 0.22f), 1f);
        }

        tex.SetPixels(px);
        tex.Apply(false, false);

        Sprite sp = Sprite.Create(tex, new Rect(0, 0, SW, SH), new Vector2(0.5f, 0.5f), 32f);
        sp.name = "BannerSprite";
        return sp;
    }

    // ================= أداة دوائر ناعمة مشتركة =================

    static void SoftCircle(Color[] px, int S, float cx, float cy, float r, Color c, float soft = 2.5f)
    {
        int xa = Mathf.Max(0, Mathf.FloorToInt(cx - r - soft));
        int xb = Mathf.Min(S - 1, Mathf.CeilToInt(cx + r + soft));
        int ya = Mathf.Max(0, Mathf.FloorToInt(cy - r - soft));
        int yb = Mathf.Min(S - 1, Mathf.CeilToInt(cy + r + soft));

        for (int y = ya; y <= yb; y++)
        {
            for (int x = xa; x <= xb; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) +
                                     (y + 0.5f - cy) * (y + 0.5f - cy));
                float a = 1f - Mathf.SmoothStep(r - soft, r, d);
                if (a <= 0f) continue;
                Color src = c; src.a *= a;
                Color dst = px[y * S + x];
                if (src.a >= dst.a) px[y * S + x] = src;
            }
        }
    }
}
