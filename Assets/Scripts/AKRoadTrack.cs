/// <summary>
/// v2.7.0 — تكامل حقيقي مع AK Road Editor (Unity Asset Store 312766).
///
/// الفكرة: النقاط المخدَّدة من TrackStyle (Catmull-Rom ملتحمة بالسطح المخبوز)
/// تُغذى كمراسي Bezier داخل مكوّنات AK الفعلية:
///   RoadEditor + RoadGenerator  → شبكة طريق AK الحقيقية (رؤوس ثلاثية/UV تراكمي)
///   RailingGenerator (حزمة AK)  → حواجز جدارية مرئية عند حدود الإسفلت بمصادمات
///
/// الفيزياء تبقى على Deck المخبوز المجرَّب (طبقة 10) — شبكة AK المرئية مرتفعة
/// 4.5سم فوقها ومصادمها يُقصف فور البناء، فلا خطر «جدران غير مرئية» جديد.
/// الحواجز الجدارية تُرفع 0.6م خارج حافة الإسفلت المقيسة (منطقة فرك على العشب).
/// </summary>
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AK.MapEditorTools;

public class AKRoadTrack : MonoBehaviour
{
    TrackStyle style;
    GameObject akRoad, railL, railR;

    IEnumerator Start()
    {
        style = GetComponent<TrackStyle>();
        if (style == null) yield break;
        int guard = 0;
        while (!style.Ready && guard++ < 240) yield return null;
        if (!style.Ready) yield break;
        yield return null; // إفساح مجال لبناء TrackProps أولاً (أولوية العناصر)
        Build();
    }

    void Build()
    {
        Vector3[] pts = style.Points;
        Vector3[] dirs = style.Dirs;
        float[] extL = style.ExtL, extR = style.ExtR;
        int n = pts.Length;

        // ---------- 1) عرض الطريق: مئين 5 لـ(min(extL,extR)) ناقص هامش ----------
        // يبقى داخل منطقة الطلاء المرسوم في كل العينات تقريباً (مقاوم للفحوصات الخاطئة)
        var mins = new List<float>(n);
        for (int i = 0; i < n; i++) mins.Add(Mathf.Min(extL[i], extR[i]));
        mins.Sort((a, b) => a.CompareTo(b));
        float p5 = mins[Mathf.Clamp(Mathf.RoundToInt(mins.Count * 0.05f), 0, mins.Count - 1)];
        float halfW = Mathf.Clamp(p5 - 0.3f, 5.5f, 9.0f);

        // ---------- 2) مراسي Bezier كل ~30م بمقابض ملساء تلقائية ----------
        int step = Mathf.Clamp(n / 70, 6, 24);
        int anchorCount = Mathf.Max(6, n / step);
        float handleLen = step * 2.5f * 0.33f;

        var anchors = new List<RoadEditor.BezierPoint>(anchorCount);
        for (int a = 0; a < anchorCount; a++)
        {
            int idx = (a * step) % n;
            Vector3 prev = pts[(idx - step + n) % n];
            Vector3 next = pts[(idx + step) % n];
            Vector3 tan = (next - prev);
            tan.y = 0f;
            tan = tan.sqrMagnitude > 0.0001f ? tan.normalized * handleLen : Vector3.forward * handleLen;
            anchors.Add(new RoadEditor.BezierPoint(pts[idx], -tan, tan));
        }

        // ---------- 3) شبكة الطريق عبر مكوّنات AK الحقيقية ----------
        Material asphalt = MakeAsphaltMat();
        Material shoulder = MakeGrassMat();

        akRoad = new GameObject("AK_Road");
        akRoad.transform.SetParent(transform, false);
        akRoad.isStatic = true;

        RoadEditor re = akRoad.AddComponent<RoadEditor>();
        re.curveResolution = 10;
        RoadGenerator rg = akRoad.AddComponent<RoadGenerator>();
        rg.roadEditor = re;
        re.roadGenerator = rg;
        rg.roadWidth = halfW * 2f;
        rg.roadMaterial = asphalt;
        rg.terrainMaterial = shoulder;
        rg.terrainSize = 2.2f;
        rg.terrainHeightOffset = -0.5f;
        rg.heightOffset = 0.045f;   // فوق الطلاء المخبوز — بلا Z-fighting
        rg.uvTilingDensity = 0.35f; // تكرار الخامة كل ~2.9م على طول الطريق
        rg.uvTilingWidth = 1.0f;

        var live = re.GetBezierPoints();
        live.Clear();
        foreach (var bp in anchors) live.Add(bp);

        rg.SetClosedPath(true); // حلقة مغلقة → المنحنى يلتف ويعود للبداية

        // تنظيف ما لا نريد: كتفا AK (الأرض المخبوزة موجودة) + مصادم شبكة AK
        // (الفيزياء تبقى على Deck طبقة 10 المجرَّب منذ v2.2)
        Transform lt = akRoad.transform.Find("Left Terrain");
        Transform rt = akRoad.transform.Find("Right Terrain");
        if (lt != null) Destroy(lt.gameObject);
        if (rt != null) Destroy(rt.gameObject);
        MeshCollider akCol = akRoad.GetComponent<MeshCollider>();
        if (akCol != null) Destroy(akCol);

        // إسكات حلقة Update (تعاير الشبكة كل إطار في اللعب مضيعة للذاكرة)
        rg.roadEditor = null;

        // ---------- 4) حواجز AK عند حدّي الإسفلت (+0.6م عشب) ----------
        Material guard = MakeGuardMat();
        railL = BuildRailing("AK_RailL", RailingGenerator.RailingSide.Left, guard, pts, dirs, extL, +1f, step);
        railR = BuildRailing("AK_RailR", RailingGenerator.RailingSide.Right, guard, pts, dirs, extR, -1f, step);

        Debug.Log($"[AKRoadTrack] v2.7.0: طريق AK حقيقي جاهز — {anchorCount} مرساة بيزييه، " +
                  $"عرض {(halfW * 2f):0.0}م، حاجزان جداريان بطول المسار كاملاً (Asset Store 312766)");
    }

    GameObject BuildRailing(string name, RailingGenerator.RailingSide side, Material mat,
                            Vector3[] pts, Vector3[] dirs, float[] ext, float sideSign, int step)
    {
        int n = pts.Length;
        int railStep = Mathf.Max(2, step / 3); // ~10م لكل قطعة حاجز
        var edge = new List<Vector3>(n / railStep + 1);
        for (int i = 0; i < n; i += railStep)
        {
            Vector3 right = Vector3.Cross(Vector3.up, dirs[i]);
            edge.Add(pts[i] + right * sideSign * (ext[i] + 0.6f));
        }
        if (edge.Count < 2) return null;

        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.isStatic = true;

        RailingGenerator rgen = go.AddComponent<RailingGenerator>();
        rgen.railingType = RailingGenerator.RailingType.Wall;
        rgen.railingSide = side;                 // يحدد اتجاه الانزياح فقط — الإزاحة صفر
        rgen.offset = 0f;                        // المسار نفسه على الحافة
        rgen.wallHeight = 0.9f;
        rgen.addColliders = true;                // حواجز مرئية بمصادمات حقيقية
        rgen.material = mat;
        rgen.GenerateRailing(edge);

        StaticBatchingUtility.Combine(go); // دمج مئات المكعبات في نداء رسم واحد
        return go;
    }

    // ================= خامات إجرائية (Legacy Shaders/Diffuse موجود حتماً بالبناء —
    // خامات حزمة Cartoon Props نفسها تشير إليه) =================

    static Material FindDiffuse()
    {
        Shader sh = Shader.Find("Legacy Shaders/Diffuse");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) return TrackStyle.MakePaintMat();
        return new Material(sh);
    }

    static Texture2D NoiseTex(int size, Color baseCol, float amp, int seed)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.anisoLevel = 4;
        var rnd = new System.Random(seed);
        var px = new Color[size * size];
        for (int i = 0; i < px.Length; i++)
        {
            float nse = (float)(rnd.NextDouble() * 2.0 - 1.0);
            float v = amp * (0.65f * nse + 0.35f * (float)(rnd.NextDouble() * 2.0 - 1.0));
            px[i] = new Color(
                Mathf.Clamp01(baseCol.r + v),
                Mathf.Clamp01(baseCol.g + v),
                Mathf.Clamp01(baseCol.b + v));
        }
        tex.SetPixels(px);
        tex.Apply(false, false);
        return tex;
    }

    static Material MakeAsphaltMat()
    {
        Material m = FindDiffuse();
        m.name = "AK_Asphalt";
        m.mainTexture = NoiseTex(128, new Color(0.335f, 0.335f, 0.35f), 0.045f, 20270101);
        return m;
    }

    static Material MakeGrassMat()
    {
        Material m = FindDiffuse();
        m.name = "AK_Shoulder";
        m.mainTexture = NoiseTex(32, new Color(0.30f, 0.46f, 0.20f), 0.06f, 20270102);
        return m;
    }

    static Material MakeGuardMat()
    {
        Material m = FindDiffuse();
        m.name = "AK_GuardRail";
        // ألواح حمراء/بيضاء بالتناوب على طول الحاجز
        Texture2D tex = new Texture2D(64, 8, TextureFormat.RGB24, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[64 * 8];
        for (int x = 0; x < 64; x++)
            for (int y = 0; y < 8; y++)
                px[y * 64 + x] = (x / 16) % 2 == 0
                    ? new Color(0.82f, 0.12f, 0.10f)
                    : new Color(0.94f, 0.94f, 0.91f);
        tex.SetPixels(px);
        tex.Apply(false, false);
        m.mainTexture = tex;
        if (m.HasProperty("_MainTex")) m.mainTextureScale = new Vector2(3f, 1f);
        return m;
    }
}
