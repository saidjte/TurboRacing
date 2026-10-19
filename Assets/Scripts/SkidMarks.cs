using UnityEngine;

/// <summary>
/// TurboRacing - v1.0.0 (لعبة ثلاثية الأبعاد حقيقية)
/// آثار إطارات متلاشية على الطريق: شبكة ديناميكية واحدة (دورة رسم واحدة)
/// بدل مئات الكائنات — مخزون دائري من أرباعاً ملقاة فوق الإسفلت بقليل
/// (0.045م) ممدودة باتجاه قيادة السيارة، تتلاشى ألفاها في LateUpdate
/// ثم يُعاد تدوير مواقعها (بلا تخصيص ذاكرة أثناء السباق).
/// النسيج رقعي ناعم الحواف مولّد بالكود، والتلوين عبر ألوان الرؤوس.
/// </summary>
public class SkidMarks : MonoBehaviour
{
    public static SkidMarks Instance { get; private set; }

    [Header("ضبط الآثار")]
    [Tooltip("عدد العلامات في المخزون الدائري")]
    public int poolSize = 160;

    [Tooltip("عرض العلامة (عرض الإطار) بالمتر")]
    public float markWidth = 0.28f;

    [Tooltip("طول العلامة (بصمة تلامس) بالمتر")]
    public float markLength = 0.55f;

    [Tooltip("ثواني بقاء الأثر قبل التلاشي الكامل")]
    public float lifetime = 3.5f;

    [Tooltip("شفافية الأثر الابتدائية")]
    public float startAlpha = 0.55f;

    const float HoverY = 0.045f; // فوق الإسفلت (0.02) وتحت كل شيء مهم

    Mesh mesh;
    Vector3[] verts;
    Vector2[] uvs;
    Color[] colors;
    float[] born;
    int next;

    readonly Color markTint = new Color(0.07f, 0.07f, 0.08f, 0f);

    void Awake()
    {
        Instance = this;

        int n = Mathf.Max(8, poolSize);
        verts = new Vector3[n * 4];
        uvs = new Vector2[n * 4];
        colors = new Color[n * 4];
        born = new float[n];

        int[] tris = new int[n * 6];
        for (int i = 0; i < n; i++)
        {
            born[i] = -999f;

            int v = i * 4, t = i * 6;
            tris[t] = v;     tris[t + 1] = v + 1; tris[t + 2] = v + 2;
            tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;

            // uv واحدة لكل ربع (رقعة ناعمة الحواف)
            uvs[v]     = new Vector2(0f, 0f);
            uvs[v + 1] = new Vector2(0f, 1f);
            uvs[v + 2] = new Vector2(1f, 1f);
            uvs[v + 3] = new Vector2(1f, 0f);
        }

        mesh = new Mesh();
        mesh.name = "SkidMarks_Mesh";
        mesh.MarkDynamic();
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.colors = colors;
        mesh.triangles = tris;
        // مجال ثابت كبير: العلامات تتنقل كل أنحاء الحلبة (لا قصّ خاطئ)
        mesh.bounds = new Bounds(new Vector3(46f, 0f, 28f), new Vector3(400f, 10f, 400f));

        GameObject go = new GameObject("SkidMesh");
        go.transform.SetParent(transform, false);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = MakeMarkMaterial();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    /// <summary>
    /// وضع أثر إطار عند موضع عالمي مع دوران Y (يستدعيه CarController في FixedUpdate).
    /// </summary>
    public void Drop(Vector3 pos, float yawDeg)
    {
        int i = next;
        born[i] = Time.time;
        next = (next + 1) % (verts.Length / 4);

        float w = markWidth * 0.5f, l = markLength * 0.5f;
        Quaternion rot = Quaternion.Euler(0f, yawDeg, 0f);
        Vector3 up = Vector3.up * HoverY;

        int v = i * 4;
        // ترتيب (x0,z0)→(x0,z1)→(x1,z1)→(x1,z0) = وجه علوي صحيح (مُثبت بالاختبار)
        verts[v]     = pos + rot * new Vector3(-w, 0f, -l) + up;
        verts[v + 1] = pos + rot * new Vector3(-w, 0f,  l) + up;
        verts[v + 2] = pos + rot * new Vector3( w, 0f,  l) + up;
        verts[v + 3] = pos + rot * new Vector3( w, 0f, -l) + up;

        mesh.vertices = verts; // رفع كامل رخيص (640 رأساً)

        Color c = markTint; c.a = startAlpha;
        for (int k = 0; k < 4; k++) colors[v + k] = c;
        mesh.colors = colors;
    }

    void LateUpdate()
    {
        bool dirty = false;
        float t = Time.time;
        int n = verts.Length / 4;

        for (int i = 0; i < n; i++)
        {
            float age = t - born[i];
            float a = (age < 0f || age >= lifetime) ? 0f : startAlpha * (1f - age / lifetime);

            int v = i * 4;
            if (!Mathf.Approximately(colors[v].a, a))
            {
                Color c = markTint; c.a = a;
                colors[v] = c; colors[v + 1] = c; colors[v + 2] = c; colors[v + 3] = c;
                dirty = true;
            }
        }

        if (dirty) mesh.colors = colors;
    }

    /// <summary>مادة Sprites/Default (ملوّنة بالرؤوس + نسيج رقعي) — رخيصة ومضمونة الإدراج.</summary>
    static Material MakeMarkMaterial()
    {
        Shader sh = Shader.Find("Sprites/Default");
        Material m = new Material(sh != null ? sh : Shader.Find("UI/Default"));
        m.name = "SkidMarkMat";
        m.mainTexture = MakeMarkTexture();
        return m;
    }

    /// <summary>رقعة بيضاء بحواف ناعمة (تُلوَّن داكنة عبر ألوان الرؤوس).</summary>
    static Texture2D MakeMarkTexture()
    {
        const int N = 32;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[N * N];

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float dx = Mathf.Abs(x - (N - 1) * 0.5f) / (N * 0.5f);
                float dy = Mathf.Abs(y - (N - 1) * 0.5f) / (N * 0.5f);
                float a = Mathf.Clamp01(1f - dx * dx) * Mathf.Clamp01(1f - dy * dy);
                px[y * N + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, false);
        tex.name = "SkidMark_Tex";
        return tex;
    }
}
