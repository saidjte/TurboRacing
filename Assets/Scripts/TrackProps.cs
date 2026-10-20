using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TurboRacing - v2.6.0 — حزمة عناصر الحلبة الكرتونية (المكافئ الإجرائي للأصل
/// «Environment Track Lowpoly: Cartoon Props Mobile Free» الذي لا يمكن تنزيله
/// خارج حساب Unity - EULA). الأصل يضم 14 مجسماً منخفض المضلعات: أقواس، حواجز،
/// مخاريط، سياج، أسهم طرق، لافتات، إطارات، إشارات مرور، مدرّج، أعلام، أعمدة
/// إنارة — بنينا المكافئ بنفس الأسلوب (ألوان رأسية مشبعة، مضلعات قليلة):
/// - مخاريط مرورية عند رؤوس المنعطفات الداخلية (بلا مصادم = قابل للعبور
///   الكرتوني، ولا يصنع جداراً حتى لو أخطأ قياس)
/// - حواجز بلاستيكية / سياج خرساني / مكدّسات إطارات / أعمدة إنارة / لافتات
///   منعطفات / أعلام / قوس لافتة / مدرّجان — بمصادمات صناديق حقيقية لكن
///   كلها خارج حافة الإسفلت بهامش مُتحقَّق منه (قاعدة v1.8.0: لا جدران غير
///   مرئية أبداً — كل مصادم مرئي بوضوح وبعيد عن الطريق مسافة إلزامية)
/// - التموضع من بيانات TrackStyle المقيسة (عينات + امتداد إسفلت لكل جهة +
///   انحناء) مع فحص مزدوج لكل مصادم: المسافة الأدنى إلى كل عينات خط السباق
///   ≥ (امتداد الإسفلت لنفس الجهة + هامش) + السطح تحته ليس TARMAC
/// أداءً: شبكة واحدة مدمجة بدورة رسم واحدة + مصادمات صناديق رخيصة.
/// </summary>
public class TrackProps : MonoBehaviour
{
    TrackStyle style;
    MeshAccumulator acc;
    GameObject colliderRoot;
    int colliders, skipped;

    struct Run { public int start; public int len; }

    // ألوان الحزمة الكرتونية
    static readonly Color ConeOrange = new Color(0.95f, 0.45f, 0.08f);
    static readonly Color ConeWhite = new Color(0.94f, 0.94f, 0.92f);
    static readonly Color BarrierWhite = new Color(0.92f, 0.92f, 0.90f);
    static readonly Color SignRed = new Color(0.82f, 0.12f, 0.10f);
    static readonly Color Concrete = new Color(0.80f, 0.80f, 0.78f);
    static readonly Color PoleGray = new Color(0.42f, 0.44f, 0.47f);
    static readonly Color LampAmber = new Color(1f, 0.85f, 0.35f);
    static readonly Color FlagOrange = new Color(0.98f, 0.55f, 0.10f);
    static readonly Color BannerAmber = new Color(0.95f, 0.62f, 0.10f);
    static readonly Color SignYellow = new Color(0.95f, 0.80f, 0.10f);
    static readonly Color TireDark = new Color(0.12f, 0.12f, 0.13f);
    static readonly Color TireMid = new Color(0.20f, 0.20f, 0.22f);
    static readonly Color SteelBlue = new Color(0.20f, 0.36f, 0.60f);
    static readonly Color InkDark = new Color(0.08f, 0.08f, 0.09f);
    static readonly Color BaseDark = new Color(0.25f, 0.25f, 0.26f);

    void Start()
    {
        // TrackStyle قد يبدأ بعدنا (ترتيب Start داخل GO واحد غير مضمون) — ننتظره
        style = GetComponent<TrackStyle>();
        if (style == null) return;
        StartCoroutine(BuildWhenReady());
    }

    System.Collections.IEnumerator BuildWhenReady()
    {
        int guard = 0;
        while (!style.Ready && guard++ < 120) yield return null;
        if (!style.Ready) yield break;
        Build();
    }

    void Build()
    {
        acc = new MeshAccumulator();
        Vector3[] P = style.Points;
        int n = P.Length;
        Vector3 C = Centroid(P);

        colliderRoot = new GameObject("TrackPropsColliders");
        colliderRoot.transform.SetParent(transform, false);
        colliderRoot.isStatic = true; // طبقة افتراضية: أشعة السطح (طبقة 10) لا تراه

        var cornerFlag = new bool[n];
        var straightFlag = new bool[n];
        for (int i = 0; i < n; i++)
        {
            cornerFlag[i] = style.Curve[i] >= 24f;
            straightFlag[i] = style.Curve[i] < 10f;
        }

        var corners = CollectRuns(cornerFlag, true, 4);
        var straights = CollectRuns(straightFlag, false, 15);
        straights.Sort((a, b) => b.len.CompareTo(a.len));

        foreach (var run in corners) BuildCorner(run.start, run.len, C);
        BuildStraights(straights, C, n);
        BuildStartFurnish(n);
        BuildBannerArch(straights, n);

        // الشبكة الواحدة (طبقة افتراضية — أشعة السطح لا تمسها والمصادمات تعمل)
        Mesh mesh = acc.Build("TrackProps_Mesh");
        GameObject go = new GameObject("TrackProps");
        go.transform.SetParent(transform, false);
        go.isStatic = true;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = TrackStyle.MakePaintMat();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;

        Debug.Log($"[TrackProps] v2.6.0: حزمة العناصر جاهزة — {corners.Count} منعطف، " +
                  $"{straights.Count} مستقيم، {colliders} مصادم مرئي، استُبعد {skipped}");
    }

    // ================= التموضع =================

    void BuildCorner(int start, int len, Vector3 C)
    {
        Vector3[] P = style.Points;
        int n = P.Length;

        // رأس المنعطف = أعلى انحناء
        int apexI = start;
        for (int k = 0; k < len; k++)
        {
            int i = (start + k) % n;
            if (style.Curve[i] > style.Curve[apexI]) apexI = i;
        }

        // أي جهة داخلية؟ (الجهة التي تقرّب من مركز الحلبة)
        Vector3 rApex = RightAt(apexI);
        float inside = (P[apexI] + rApex * 5f - C).sqrMagnitude <
                       (P[apexI] - C).sqrMagnitude ? 1f : -1f;
        float outside = -inside;

        // مخاريط داخلية عند الرأس (بلا مصادم — كرتوني قابل للعبور)
        for (int k = -3; k <= 3; k += 2)
        {
            int i = (apexI + k + n) % n;
            AddCone(At(i, inside * Mathf.Max(1.2f, ExtAt(i, inside) - 1.1f)),
                    YawOf(style.Dirs[i]));
        }

        // حواجز خارجية على امتداد المنعطف (مصادم مرئي بهامش إلزامي)
        int placed = 0;
        for (int k = 0; k < len && placed < 8; k += 2)
        {
            int i = (start + k) % n;
            if (TryAddCollidable(i, outside * (ExtAt(i, outside) + 2.4f), 1.1f,
                                 (p, y) => AddBarrier(p, y)))
                placed++;
        }

        // لافتة منعطف قبل الدخول بـ ~20م (جهة خارجية، تواجه القادمين)
        int si = (start - 8 + n) % n;
        TryAddCollidable(si, outside * (ExtAt(si, outside) + 2.0f), 1.1f,
                         (p, y) => AddCornerSign(si, p, y));

        // مكدّسا إطارات عند المخرج
        for (int k = 3; k <= 6; k += 3)
        {
            int i = (start + len + k) % n;
            TryAddCollidable(i, outside * (ExtAt(i, outside) + 3.0f), 1.2f,
                             (p, y) => AddTireStack(p, y));
        }
    }

    void BuildStraights(List<Run> straights, Vector3 C, int n)
    {
        int poles = 0, fences = 0, tribunes = 0;

        for (int s = 0; s < straights.Count; s++)
        {
            var run = straights[s];
            int mid = (run.start + run.len / 2) % n;

            // الجهة الخارجية (الأبعد عن مركز الحلبة)
            Vector3 r = RightAt(mid);
            float away = (style.Points[mid] + r * 8f - C).sqrMagnitude >
                         (style.Points[mid] - C).sqrMagnitude ? 1f : -1f;

            // أعمدة إنارة متناوبة الجانب كل ~35م على كل مستقيم (مستقيمات العملاقة طويلة)
            for (int k = 4; k < run.len - 2 && poles < 18; k += 14)
            {
                int i = (run.start + k) % n;
                float side = (poles & 1) == 0 ? away : -away;
                int poleIdx = i;
                TryAddCollidable(i, side * (ExtAt(i, side) + 3.8f), 1.1f,
                                 (p, y) => AddLightPole(poleIdx, p, y));
                poles++;
            }

            // مدرّجان على أطول مستقيمين (خارج كل شيء، المقاعد تستقيل عن الطريق)
            if (s < 2 && tribunes < 2 && run.len >= 18)
            {
                int triIdx = mid;
                if (TryAddCollidable(mid, away * (ExtAt(mid, away) + 7.2f), 5.5f,
                                     (p, y) => AddTribune(triIdx, p, y)))
                    tribunes++;
            }

            // سياج خرساني متقطع على المستقيمات الأدنى
            if (s >= 1)
            {
                for (int k = 2; k < run.len - 2 && fences < 24; k += 3)
                {
                    int i = (run.start + k) % n;
                    TryAddCollidable(i, away * (ExtAt(i, away) + 3.2f), 1.1f,
                                     (p, y) => AddFence(p, y));
                    fences++;
                }
            }
        }
    }

    void BuildStartFurnish(int n)
    {
        // علما جانبي مستقيم الانطلاق قرب بوابة الأضواء (العينة 0 = البداية)
        for (int k = 0; k < 2; k++)
        {
            int i = k == 0 ? 6 : n - 6;
            float side = k == 0 ? -1f : 1f;
            int flagIdx = i;
            TryAddCollidable(i, side * (ExtAt(i, side) + 2.6f), 1.0f,
                             (p, y) => AddFlag(flagIdx, p, y));
        }
    }

    void BuildBannerArch(List<Run> straights, int n)
    {
        // قوس اللافتة فوق أطول مستقيم لا يحوي خط البداية (العينة 0)
        foreach (var run in straights)
        {
            if (run.len < 16 || ContainsIndex(run, 0, n)) continue;

            int mid = (run.start + run.len / 2) % n;
            float yaw = YawOf(style.Dirs[mid]);
            Vector3 c = SnapY(style.Points[mid]);
            Vector3 rgt = RightAt(mid);

            // ساقان على حافتي الطريق (كل ساق بامتداد جهتها + هامش 1.8م)
            float legR = style.ExtR[mid] + 1.8f;
            float legL = style.ExtL[mid] + 1.8f;
            bool both = true;
            Vector3 legRPos = SnapY(c + rgt * legR);
            Vector3 legLPos = SnapY(c - rgt * legL);
            if (!OffRoad(legRPos, style.ExtR[mid] + 1.0f) || SurfaceIsRoad(legRPos)) both = false;
            if (!OffRoad(legLPos, style.ExtL[mid] + 1.0f) || SurfaceIsRoad(legLPos)) both = false;
            if (!both) continue;

            AddRotBox(acc, legRPos, yaw + 90f, 0.7f, 0.7f, 5.2f, Color.white, PoleGray);
            AddRotBox(acc, legLPos, yaw + 90f, 0.7f, 0.7f, 5.2f, Color.white, PoleGray);
            AddCollider(legRPos, yaw, new Vector3(0.75f, 5.2f, 0.75f));
            AddCollider(legLPos, yaw, new Vector3(0.75f, 5.2f, 0.75f));

            // اللافتة العلوية (بلا مصادم — علوية فوق ممر القيادة بـ 4.5م)
            float span = legR + legL;
            Vector3 bannerC = c + rgt * ((legR - legL) * 0.5f) + Vector3.up * 4.55f;
            AddRotBox(acc, bannerC, yaw + 90f, span, 0.55f, 1.45f,
                      BannerAmber, BannerAmber * 0.9f);
            for (int k = 0; k < 5; k++)
            {
                float t = (k + 0.5f) / 5f - 0.5f;
                AddRotBox(acc, bannerC + rgt * (t * span * 0.92f) + Vector3.up * 0.2f,
                          yaw + 90f, span * 0.14f, 0.6f, 0.95f, Color.white, Color.white);
            }
            break; // قوس واحد فقط
        }
    }

    // ================= أدوات التموضع الآمن =================

    /// <summary>إضافة عنصر ذي مصادم: يُقبل فقط إن كان بُعده الأدنى عن كل عينات
    /// خط السباق ≥ (امتداد الإسفلت لنفس الجهة + هامش) وسفله ليس إسفلتاً.
    /// هذه هي الضمانة المضادة لـ«الجدران غير المرئية» المطبقة على كل مصادم.</summary>
    bool TryAddCollidable(int i, float lateralOffset, float margin,
                          System.Action<Vector3, float> builder)
    {
        Vector3 pos = style.Points[i] + RightAt(i) * lateralOffset;
        float ext = ExtAt(i, lateralOffset >= 0f ? 1f : -1f);
        if (!OffRoad(pos, ext + margin)) { skipped++; return false; }
        if (SurfaceIsRoad(pos)) { skipped++; return false; }
        builder(SnapY(pos), YawOf(style.Dirs[i]));
        return true;
    }

    /// <summary>هل الموضع بعيد بما يكفي عن كل عينات خط السباق؟</summary>
    bool OffRoad(Vector3 pos, float minClear)
    {
        float best = float.MaxValue;
        Vector3[] P = style.Points;
        for (int i = 0; i < P.Length; i++)
        {
            float dx = P[i].x - pos.x, dz = P[i].z - pos.z;
            float d = dx * dx + dz * dz;
            if (d < best) best = d;
        }
        return Mathf.Sqrt(best) >= minClear;
    }

    bool SurfaceIsRoad(Vector3 pos)
    {
        if (Physics.Raycast(pos + Vector3.up * 30f, Vector3.down, out RaycastHit h,
                            90f, 1 << 10))
            return h.collider.name.ToUpperInvariant().Contains("TARMAC");
        return false;
    }

    Vector3 SnapY(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out RaycastHit h,
                            90f, 1 << 10))
            p.y = h.point.y;
        return p;
    }

    Vector3 At(int i, float lateral) => SnapY(style.Points[i] + RightAt(i) * lateral);
    Vector3 RightAt(int i) => Vector3.Cross(Vector3.up, style.Dirs[i]);
    float ExtAt(int i, float sideSign) => sideSign > 0 ? style.ExtR[i] : style.ExtL[i];

    static float YawOf(Vector3 d) => Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;

    static Vector3 Centroid(Vector3[] P)
    {
        Vector3 c = Vector3.zero;
        foreach (var p in P) c += p;
        return c / P.Length;
    }

    static bool ContainsIndex(Run run, int idx, int n)
    {
        for (int k = 0; k < run.len; k++)
            if ((run.start + k) % n == idx) return true;
        return false;
    }

    static List<Run> CollectRuns(bool[] flags, bool val, int minLen)
    {
        var runs = new List<Run>();
        int n = flags.Length;
        // بذرة آمنة خارج قيمة val لمنع دمج الحلقة الملفوفة مرتين
        int seed = 0;
        if (flags[0] == val)
        {
            int s2 = 0;
            while (s2 < n && flags[s2] == val) s2++;
            seed = s2 % n;
        }
        int i = seed, count = 0;
        while (count < n)
        {
            if (flags[i % n] == val)
            {
                int start = i % n, len = 0;
                while (count < n && flags[i % n] == val) { len++; i++; count++; }
                if (len >= minLen) runs.Add(new Run { start = start, len = len });
            }
            else { i++; count++; }
        }
        return runs;
    }

    void AddCollider(Vector3 pos, float yaw, Vector3 size)
    {
        var go = new GameObject("PropCollider");
        go.transform.SetParent(colliderRoot.transform, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        var bc = go.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, size.y * 0.5f, 0f);
        bc.size = size;
        colliders++;
    }

    // ================= مجسّمات أساسية مُدوّرة =================

    /// <summary>صندوق مُدوّر حول Y: الطول على محور fwd(yaw) والعرض على right.
    /// الجوانب بحلقة زوايا تصاعدية (نمط AddCylinder المُثبَّت) والوجه العلوي
    /// بالحلقة التنازلية (نمط AddTop) — كلتاهما متحقَّق منهما في المشهد منذ v1.0.</summary>
    static void AddRotBox(MeshAccumulator acc, Vector3 baseCenter, float yaw,
                          float len, float wid, float hgt, Color top, Color side)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 fwd = rot * Vector3.forward;
        Vector3 rgt = rot * Vector3.right;
        float hl = len * 0.5f, hw = wid * 0.5f;

        var corners = new Vector3[4];
        int k = 0;
        for (int f = -1; f <= 1; f += 2)
            for (int r = -1; r <= 1; r += 2)
                corners[k++] = baseCenter + fwd * (hl * f) + rgt * (hw * r);

        System.Array.Sort(corners, (a, b) =>
            Mathf.Atan2(a.z - baseCenter.z, a.x - baseCenter.x)
            .CompareTo(Mathf.Atan2(b.z - baseCenter.z, b.x - baseCenter.x)));

        var btm = new Vector3[4];
        var top4 = new Vector3[4];
        var uv0 = Vector2.zero;
        for (int i = 0; i < 4; i++)
        {
            btm[i] = corners[i];
            top4[i] = corners[i] + Vector3.up * hgt;
        }
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) & 3;
            acc.AddQuad(btm[i], top4[i], top4[j], btm[j], uv0, uv0, uv0, uv0, side);
        }
        acc.AddQuad(top4[3], top4[2], top4[1], top4[0], uv0, uv0, uv0, uv0, top);
    }

    /// <summary>موشور رباعي متقلّص (مخاريط): قاعدة half0 وقمة half1 على ارتفاع hgt.</summary>
    static void AddFrustum4(MeshAccumulator acc, Vector3 baseCenter, float yaw,
                            float half0, float half1, float hgt, Color col, bool capTop)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 fwd = rot * Vector3.forward;
        Vector3 rgt = rot * Vector3.right;

        var b = new Vector3[4];
        var t = new Vector3[4];
        int k = 0;
        for (int f = -1; f <= 1; f += 2)
            for (int r = -1; r <= 1; r += 2)
            {
                b[k] = baseCenter + fwd * (half0 * f) + rgt * (half0 * r);
                t[k] = baseCenter + fwd * (half1 * f) + rgt * (half1 * r) + Vector3.up * hgt;
                k++;
            }

        int[] idx = { 0, 1, 2, 3 };
        System.Array.Sort(idx, (a, c) =>
            Mathf.Atan2(b[idx[a]].z - baseCenter.z, b[idx[a]].x - baseCenter.x)
            .CompareTo(Mathf.Atan2(b[idx[c]].z - baseCenter.z, b[idx[c]].x - baseCenter.x)));

        var ob = new Vector3[4];
        var ot = new Vector3[4];
        for (int i = 0; i < 4; i++) { ob[i] = b[idx[i]]; ot[i] = t[idx[i]]; }

        var uv0 = Vector2.zero;
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) & 3;
            acc.AddQuad(ob[i], ot[i], ot[j], ob[j], uv0, uv0, uv0, uv0, col);
        }
        if (capTop)
            acc.AddQuad(ot[3], ot[2], ot[1], ot[0], uv0, uv0, uv0, uv0, col);
    }

    // ================= عناصر الحزمة =================

    /// <summary>مخروط مروري برتقالي بحزام أبيض — بلا مصادم (زينة قابلة للعبور).</summary>
    void AddCone(Vector3 pos, float yaw)
    {
        AddRotBox(acc, pos, yaw, 0.5f, 0.5f, 0.05f, BaseDark, BaseDark);
        AddFrustum4(acc, pos + Vector3.up * 0.05f, yaw, 0.185f, 0.125f, 0.24f, ConeOrange, false);
        AddFrustum4(acc, pos + Vector3.up * 0.29f, yaw, 0.125f, 0.10f, 0.10f, ConeWhite, false);
        AddFrustum4(acc, pos + Vector3.up * 0.39f, yaw, 0.10f, 0.025f, 0.18f, ConeOrange, true);
    }

    /// <summary>حواجز بلاستيكية بيضاء بحزام أحمر وقدمان — مصادم 2م.</summary>
    void AddBarrier(Vector3 pos, float yaw)
    {
        AddRotBox(acc, pos, yaw, 2.0f, 0.42f, 0.72f, BarrierWhite, BarrierWhite * 0.92f);
        AddRotBox(acc, pos + Vector3.up * 0.30f, yaw, 2.0f, 0.47f, 0.14f, SignRed, SignRed);
        Vector3 fwd = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
        AddRotBox(acc, pos + fwd * 0.65f, yaw, 0.5f, 0.8f, 0.07f, PoleGray, PoleGray);
        AddRotBox(acc, pos - fwd * 0.65f, yaw, 0.5f, 0.8f, 0.07f, PoleGray, PoleGray);
        AddCollider(pos, yaw, new Vector3(0.5f, 0.85f, 2.05f));
    }

    /// <summary>مكدّس 3 إطارات بحزام أبيض — مصادم متر واحد.</summary>
    void AddTireStack(Vector3 pos, float yaw)
    {
        acc.AddCylinder(pos + Vector3.up * 0.12f, 0.36f, 0.24f, 8, 0.5f, TireDark);
        acc.AddCylinder(pos + Vector3.up * 0.36f, 0.36f, 0.24f, 8, 0.5f, TireMid);
        acc.AddCylinder(pos + Vector3.up * 0.36f, 0.372f, 0.07f, 8, 0.5f, ConeWhite);
        acc.AddCylinder(pos + Vector3.up * 0.60f, 0.36f, 0.24f, 8, 0.5f, TireDark);
        AddCollider(pos, yaw, new Vector3(0.8f, 0.75f, 0.8f));
    }

    /// <summary>سياج خرساني بخط أحمر علوي — مصادم 3.8م.</summary>
    void AddFence(Vector3 pos, float yaw)
    {
        AddRotBox(acc, pos, yaw, 3.8f, 0.35f, 0.95f, Concrete, Concrete * 0.9f);
        AddRotBox(acc, pos + Vector3.up * 0.85f, yaw, 3.8f, 0.38f, 0.10f, SignRed, SignRed);
        AddCollider(pos, yaw, new Vector3(0.4f, 1.0f, 3.85f));
    }

    /// <summary>عمود إنارة 6م بذراع ومصباح نحو الطريق — مصادم عمودي رفيع.</summary>
    void AddLightPole(int i, Vector3 pos, float yaw)
    {
        acc.AddCylinder(pos + Vector3.up * 3.1f, 0.09f, 6.2f, 6, 0.5f, PoleGray);

        Vector3 toward = style.Points[i] - pos; toward.y = 0f;
        toward = toward.sqrMagnitude > 0.001f ? toward.normalized : -RightAt(i);
        float armYaw = YawOf(toward);
        AddRotBox(acc, pos + toward * 0.7f + Vector3.up * 5.95f, armYaw,
                  1.4f, 0.13f, 0.12f, PoleGray, PoleGray);
        AddRotBox(acc, pos + toward * 1.3f + Vector3.up * 5.85f, armYaw,
                  0.55f, 0.30f, 0.15f, LampAmber, LampAmber);
        AddCollider(pos, yaw, new Vector3(0.3f, 6.2f, 0.3f));
    }

    /// <summary>علم برتقالي على سارية 3.8م (القماش يستقيل بعيداً عن الطريق).</summary>
    void AddFlag(int i, Vector3 pos, float yaw)
    {
        acc.AddCylinder(pos + Vector3.up * 1.9f, 0.05f, 3.8f, 6, 0.5f, PoleGray);
        Vector3 away = pos - style.Points[i]; away.y = 0f;
        away = away.sqrMagnitude > 0.001f ? away.normalized : -RightAt(i);
        float fy = YawOf(away);
        AddRotBox(acc, pos + away * 0.55f + Vector3.up * 3.35f, fy,
                  1.1f, 0.05f, 0.7f, FlagOrange, FlagOrange);
        AddRotBox(acc, pos + away * 0.18f + Vector3.up * 3.35f, fy,
                  0.35f, 0.07f, 0.7f, Color.white, Color.white);
        AddCollider(pos, yaw, new Vector3(0.18f, 3.8f, 0.18f));
    }

    /// <summary>لافتة منعطف صفراء بثلاث شيفرونات داكنة تواجه القادمين.</summary>
    void AddCornerSign(int i, Vector3 pos, float yaw)
    {
        Vector3 travel = style.Dirs[i];
        Vector3 rgt = RightAt(i);

        AddRotBox(acc, pos + Vector3.up * 0.95f, yaw + 90f, 2.2f, 0.10f, 1.0f,
                  SignYellow, SignYellow * 0.9f);
        acc.AddCylinder(pos + rgt * 0.8f + Vector3.up * 0.5f, 0.05f, 1.0f, 6, 0.5f, PoleGray);
        acc.AddCylinder(pos - rgt * 0.8f + Vector3.up * 0.5f, 0.05f, 1.0f, 6, 0.5f, PoleGray);
        for (int k = -1; k <= 1; k++)
        {
            AddRotBox(acc, pos - travel * 0.08f + rgt * (k * 0.6f) + Vector3.up * 0.95f,
                      yaw + 90f + k * 25f, 0.5f, 0.05f, 0.6f, InkDark, InkDark);
        }
        AddCollider(pos, yaw, new Vector3(0.5f, 1.6f, 0.5f));
    }

    /// <summary>مدرّج جمهور 20م: قاعدة + 4 صفوف ملوّنة + سقف على 4 أعمدة.</summary>
    void AddTribune(int i, Vector3 pos, float yaw)
    {
        Vector3 away = pos - style.Points[i]; away.y = 0f;
        away = away.sqrMagnitude > 0.001f ? away.normalized : -RightAt(i);
        Vector3 along = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));

        AddRotBox(acc, pos, yaw, 20f, 4.6f, 0.5f, Concrete, Concrete * 0.9f);
        Color[] rows = { SignRed, ConeWhite, SteelBlue, BannerAmber };
        for (int r = 0; r < 4; r++)
        {
            Vector3 rc = pos + away * (1.0f + r * 0.95f) + Vector3.up * (0.5f + r * 0.55f);
            AddRotBox(acc, rc, yaw, 20f, 0.95f, 0.55f, rows[r], rows[r] * 0.9f);
        }
        AddRotBox(acc, pos + away * 2.3f + Vector3.up * 3.5f, yaw, 21f, 5.6f, 0.15f,
                  Color.white, Concrete);
        for (int sgn = -1; sgn <= 1; sgn += 2)
            for (int a = 0; a < 2; a++)
            {
                Vector3 post = pos + away * (a == 0 ? 0.2f : 4.4f) + along * (sgn * 9.5f);
                acc.AddCylinder(post + Vector3.up * 1.75f, 0.08f, 3.5f, 6, 0.5f, PoleGray);
            }
        AddCollider(pos + away * 2.3f, yaw, new Vector3(5.0f, 3.0f, 20.5f));
    }
}
