using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TurboRacing - v2.6.0 — طلاء الطريق بأسلوب AK Road Editor (المكافئ الإجرائي للأصل).
/// الأصل المطلوب (AK Road Editor) يعتمد منحنيات Bezier لرسم طريق سلس مع خطوط
/// وحواف تتبع الأرض تلقائياً — لا يمكن تنزيله خارج حساب Unity (EULA)، فبنينا
/// المكافئ الكامل بالكود فوق خط السباق المخبوز المجرَّب:
/// - إعادة تخديد خط السباق (96 نقطة) بمنحنى Catmull-Rom مغلق كل ~2.5م = مركز سلس
///   مستمر بلا كسور (جوهر "محرر الطرق" المنحني)
/// - قياس نصف عرض الإسفلت الفعلي عند كل عينة بأشعة عرضية (الطلاء يلاصق حافة
///   الطريق الحقيقية مهما اختلفت الهندسة — سلوك AK Road Editor التكيفي)
/// - خطا حافة أبيضان متصلان + كيرب أحمر/أبيض مخطط في كل المنعطفات + أسهم
///   شيفرون قبل دخول المنعطفات — كلها طلاء تجميلي بلا أي مصادم (درس v1.8.0:
///   لا جدران غير مرئية أبداً) ومرفوع سنتيمترات فوق السطح الموحّد (درس v2.5.0)
/// أداءً: شبكة واحدة مدمجة (MeshAccumulator) بدورة رسم واحدة + بلا مصادمات.
/// </summary>
public class TrackStyle : MonoBehaviour
{
    public static TrackStyle Instance { get; private set; }

    // --- البيانات المشتركة مع TrackProps (نفس الخط المقيس) ---
    Vector3[] pts;      // مواضع العينات (y = سطح الطريق الفعلي)
    Vector3[] dirs;     // اتجاه السير الموحّد عند كل عينة
    float[] extL, extR; // امتداد الإسفلت يساراً/يميناً من المركز (متر)
    float[] curve;      // الانحناء المحلي (درجات عبر نافذة ±50م)

    public Vector3[] Points => pts;
    public Vector3[] Dirs => dirs;
    public float[] ExtL => extL;
    public float[] ExtR => extR;
    public float[] Curve => curve;
    public bool Ready => pts != null && pts.Length > 16;

    const int Layer = 10;          // طبقة مصادمات الحلبة (نفس TrackBuilder.TrackLayer)
    const float Spacing = 2.5f;    // مسافة العينات بعد التخديد
    const float ExtCap = 11.25f;   // سقف امتداد الإسفلت (نصف عرض طريق الأصل 22.5م)
    const float ExtFloor = 6f;

    // ألوان الطلاء (كارتوني مشبع كأصول المتجر)
    static readonly Color PaintWhite = new Color(0.94f, 0.94f, 0.91f);
    static readonly Color PaintRed = new Color(0.82f, 0.12f, 0.10f);
    static readonly Color PaintBlue = new Color(0.16f, 0.30f, 0.62f);

    void Start()
    {
        Instance = this;
        TrackBuilder tb = TrackBuilder.Instance;
        if (tb == null || tb.RacingLine == null || tb.RacingLine.Length < 12) return;

        // 1) التخديد السلس (جوهر محرر الطرق المنحني)
        pts = ResampleClosed(tb.RacingLine, Spacing, out dirs);
        int n = pts.Length;

        // 2) الالتصاق بالسطح الفعلي + قياس امتداد الإسفلت عند كل عينة
        float prevY = 1f;
        extL = new float[n]; extR = new float[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 p = pts[i];
            if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out RaycastHit hit,
                                90f, 1 << Layer))
            {
                p.y = hit.point.y;
                prevY = p.y;
            }
            else p.y = prevY;
            pts[i] = p;

            Vector3 right = Vector3.Cross(Vector3.up, dirs[i]);
            extR[i] = Mathf.Clamp(ScanExtent(p, right), ExtFloor, ExtCap);
            extL[i] = Mathf.Clamp(ScanExtent(p, -right), ExtFloor, ExtCap);
        }

        // 3) الانحناء المحلي (نافذة ±20 عينة ≈ ±50م — الحلبة بيضاوية عملاقة
        //    ~2.1كم فالمنعطفات 180° ممتدة على ~270م، والنافذة القصيرة لا تراها)
        curve = new float[n];
        for (int i = 0; i < n; i++)
        {
            float a0 = Ang(dirs[(i - 20 + n) % n]);
            float a1 = Ang(dirs[(i + 20) % n]);
            curve[i] = Mathf.Abs(Mathf.DeltaAngle(a0, a1));
        }

        BuildPaint();
        Debug.Log($"[TrackStyle] v2.6.0: طلاء الطريق جاهز — {n} عينة متخّدة، " +
                  $"امتداد إسفلت {Min(extR):0.0}..{Max(extR):0.0}م يميناً");
    }

    // ================= التخديد (Catmull-Rom مغلق) =================

    /// <summary>إعادة تخديد حلقة مغلقة بمنحنى Catmull-Rom كل spacing متر.
    /// هذا نفس جوهر محررات الطرق المنحنية (Bezier/Catmull) بلا نقاط تحكم يدوية:
    /// نقاط الخط المخبوز تصبح عُقد المنحنى فيخرج مركز سلس مستمر.</summary>
    public static Vector3[] ResampleClosed(Vector3[] src, float spacing, out Vector3[] dirsOut)
    {
        int n = src.Length;
        var bag = new List<Vector3>(512);
        for (int i = 0; i < n; i++)
        {
            Vector3 p0 = src[(i - 1 + n) % n], p1 = src[i];
            Vector3 p2 = src[(i + 1) % n], p3 = src[(i + 2) % n];
            float segLen = Vector2.Distance(new Vector2(p1.x, p1.z), new Vector2(p2.x, p2.z));
            int steps = Mathf.Max(1, Mathf.CeilToInt(segLen / spacing));
            for (int s = 0; s < steps; s++)
                bag.Add(CatmullRom(p0, p1, p2, p3, s / (float)steps));
        }
        Vector3[] res = bag.ToArray();
        dirsOut = new Vector3[res.Length];
        for (int i = 0; i < res.Length; i++)
        {
            Vector3 d = res[(i + 1) % res.Length] - res[i]; d.y = 0f;
            dirsOut[i] = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.forward;
        }
        return res;
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    static float Ang(Vector3 d) => Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg;

    /// <summary>كم يمتد الإسفلت من النقطة باتجاه side قبل أن يتحول لغير TARMAC؟
    /// خطوات 0.6م حتى 14م — يعطي حافة الطريق الحقيقية عند كل عينة.</summary>
    float ScanExtent(Vector3 p, Vector3 side)
    {
        int mask = 1 << Layer;
        float ext = 0f;
        for (float d = 0.6f; d <= 14f; d += 0.6f)
        {
            Vector3 at = p + side * d;
            if (!Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out RaycastHit h,
                                 90f, mask))
                break;
            if (!h.collider.name.ToUpperInvariant().Contains("TARMAC")) break;
            ext = d;
        }
        return ext;
    }

    static float Min(float[] a) { float m = float.MaxValue; foreach (float v in a) if (v < m) m = v; return m; }
    static float Max(float[] a) { float m = float.MinValue; foreach (float v in a) if (v > m) m = v; return m; }

    // ================= بناء شبكة الطلاء =================

    void BuildPaint()
    {
        var acc = new MeshAccumulator();
        int n = pts.Length;
        const float Lift = 0.02f;    // رفع الطلاء فوق السطح الموحّد (بلا z-fighting)
        const float CurbLift = 0.05f;

        // 1) خطا الحافة الأبيضان المتصلان (عند ext − 0.55، عرض 0.3)
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            StripQuad(acc, i, j, extR[i] - 0.55f, extR[j] - 0.55f, 0.15f, Lift, PaintWhite);
            StripQuad(acc, i, j, -(extL[i] - 0.55f), -(extL[j] - 0.55f), 0.15f, Lift, PaintWhite);
        }

        // 2) الكيرب الأحمر/الأبيض في مناطق المنعطفات (كلا الحافتين، مخطط كل 5م)
        for (int i = 0; i < n; i++)
        {
            if (curve[i] < 24f) continue;
            int j = (i + 1) % n;
            bool red = ((i / 2) & 1) == 0;
            Color c = red ? PaintRed : PaintWhite;

            // يمين: من ext−0.2 إلى ext+0.6
            CurbQuad(acc, i, j, extR[i] - 0.2f, extR[j] - 0.2f,
                     extR[i] + 0.6f, extR[j] + 0.6f, CurbLift, c, +1);
            // يسار: معكوس الإشارة
            CurbQuad(acc, i, j, -(extL[i] - 0.2f), -(extL[j] - 0.2f),
                     -(extL[i] + 0.6f), -(extL[j] + 0.6f), CurbLift, c, -1);
        }

        // 3) أسهم الشيفرون قبل دخول كل منعطف (زوجان متتاليان على المحور)
        for (int i = 0; i < n; i++)
        {
            int prev = (i - 1 + n) % n;
            if (curve[i] < 24f || curve[prev] >= 24f) continue; // بداية منعطف فقط
            PlaceChevrons(acc, (i - 6 + n) % n, Lift);
            PlaceChevrons(acc, (i - 12 + n) % n, Lift);
        }

        // شبكة واحدة + مادة TR/Paint (ألوان رأسية) — بلا مصادم إطلاقاً
        Material mat = MakePaintMat();
        Mesh mesh = acc.Build("RoadPaint_Mesh");
        GameObject go = new GameObject("RoadPaint");
        go.transform.SetParent(transform, false);
        go.isStatic = true;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    /// <summary>شريط طلاء مسطّح بين عينتين: مقطع i يمتد [offI−w, offI+w] على
    /// right(i) ومقطع j يمتد [offJ−w, offJ+w] على right(j).
    /// winding عالمي صحيح للوجه +Y (نمط AddTop المُثبَّت بعد تدويره حول أي اتجاه):
    /// (أصغر إسقاط@i، أصغر@j، أكبر@j، أكبر@i) — لا فرز هنا لأن الإزاحات موصولة
    /// بعيناتها (فرز القيم يفصلها عن مواضعها فيفسد الشبكة على المنحنيات).</summary>
    void StripQuad(MeshAccumulator acc, int i, int j, float offI, float offJ,
                   float w, float lift, Color c)
    {
        Vector3 ri = Vector3.Cross(Vector3.up, dirs[i]);
        Vector3 rj = Vector3.Cross(Vector3.up, dirs[j]);
        Vector3 pi = pts[i] + Vector3.up * lift;
        Vector3 pj = pts[j] + Vector3.up * lift;
        acc.AddQuad(pi + ri * (offI - w), pj + rj * (offJ - w),
                    pj + rj * (offJ + w), pi + ri * (offI + w),
                    Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, c);
    }

    /// <summary>بلاطة كيرب: وجه علوي + وجه خارجي عمودي (winding لكل جهة، مشتق
    /// من قاعدة AddBox: N = U × D حيث D اتجاه الحافة السفلية).</summary>
    void CurbQuad(MeshAccumulator acc, int i, int j, float innerI, float innerJ,
                  float outerI, float outerJ, float lift, Color c, int side)
    {
        Vector3 ri = Vector3.Cross(Vector3.up, dirs[i]);
        Vector3 rj = Vector3.Cross(Vector3.up, dirs[j]);
        Vector3 pi = pts[i], pj = pts[j];

        // الوجه العلوي — رتّب حسب الإسقاط (نمط AddTop)
        float loI = innerI, loJ = innerJ, hiI = outerI, hiJ = outerJ;
        if (loI > hiI) { float t = loI; loI = hiI; hiI = t; }
        if (loJ > hiJ) { float t = loJ; loJ = hiJ; hiJ = t; }
        acc.AddQuad(pi + ri * loI + Vector3.up * lift, pj + rj * loJ + Vector3.up * lift,
                    pj + rj * hiJ + Vector3.up * lift, pi + ri * hiI + Vector3.up * lift,
                    Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, c);

        // الوجه الخارجي العمودي (عند الحد الخارجي) — اتجاه الحافة السفلية حسب side
        if (side > 0)
        {
            Vector3 bl = pi + ri * outerI, br = pj + rj * outerJ;
            acc.AddQuad(bl, bl + Vector3.up * lift, br + Vector3.up * lift, br,
                        Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, c * 0.85f);
        }
        else
        {
            Vector3 bl = pj + rj * outerJ, br = pi + ri * outerI;
            acc.AddQuad(bl, bl + Vector3.up * lift, br + Vector3.up * lift, br,
                        Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, c * 0.85f);
        }
    }

    /// <summary>زوج أسهم شيفرون «^» على محور السير عند عينة (تشير باتجاه السباق).</summary>
    void PlaceChevrons(MeshAccumulator acc, int idx, float lift)
    {
        for (int k = 0; k < 2; k++)
        {
            int i = (idx + k * 2 + pts.Length) % pts.Length;
            Vector3 dir = dirs[i];
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            Vector3 c = pts[i] + Vector3.up * lift;

            // شيفرون = ذراعان من الخلف (±0.85 عرضياً) إلى المقدمة (0 عرضياً)
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 tail = c - dir * 1.1f + right * (0.85f * s);
                Vector3 head = c + dir * 0.9f;
                Vector3 bd = (head - tail); bd.y = 0f;
                float len = bd.magnitude;
                if (len < 0.01f) continue;
                bd /= len;
                Vector3 br = Vector3.Cross(Vector3.up, bd);
                acc.AddQuad(tail - br * 0.18f, head - br * 0.18f, head + br * 0.18f,
                            tail + br * 0.18f,
                            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, PaintBlue);
            }
        }
    }

    // ================= مادة الطلاء =================

    /// <summary>مادة ألوان رأسية بنفس مسار TrackBuilder (TR/Paint المولَّد وقت
    /// البناء، والبدائل للمحرر) فوق نسيج أبيض مصمت — عامة ليتشاركها TrackProps.</summary>
    public static Material MakePaintMat()
    {
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[16];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply(false, false);
        tex.name = "PaintWhite";

        Shader sh = Shader.Find("TR/Paint");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Legacy Shaders/Diffuse");
        Material m = new Material(sh) { name = "MAT_RoadPaint", mainTexture = tex };
        if (m.HasProperty("_Glossiness")) { m.SetFloat("_Glossiness", 0.15f); m.SetFloat("_Metallic", 0f); }
        if (m.HasProperty("_GlossyReflections")) m.SetFloat("_GlossyReflections", 0f);
        return m;
    }
}
