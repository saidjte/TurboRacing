using UnityEngine;

/// <summary>
/// TurboRacing - المرحلة 5 (v0.7.0)
/// آثار إطارات متلاشية: مخزون دائري من علامات داكنة ناعمة الحواف تُوضع
/// خلف السيارة عند الانعطاف الحاد بسرعة أو الفرملة القوية، وتتلاشى خلال
/// ثوانٍ ثم يعاد تدويرها (بلا تخصيص ذاكرة أثناء السباق).
/// العلامة سبرايت 32×32 مولّد بالكود — ترتيب الرسم 9: فوق الطريق وتحت السيارة.
/// </summary>
public class SkidMarks : MonoBehaviour
{
    public static SkidMarks Instance { get; private set; }

    [Header("ضبط الآثار")]
    [Tooltip("عدد العلامات في المخزون الدائري")]
    public int poolSize = 160;

    [Tooltip("حجم العلامة بالوحدات العالمية")]
    public float markSize = 0.4f;

    [Tooltip("ثواني بقاء الأثر قبل التلاشي الكامل")]
    public float lifetime = 3.5f;

    [Tooltip("شفافية الأثر الابتدائية")]
    public float startAlpha = 0.55f;

    SpriteRenderer[] marks;
    float[] born;
    int next;
    Color markTint = new Color(0.07f, 0.07f, 0.08f, 1f);

    void Awake()
    {
        Instance = this;

        Sprite sprite = MakeMarkSprite();
        marks = new SpriteRenderer[poolSize];
        born = new float[poolSize];

        for (int i = 0; i < poolSize; i++)
        {
            GameObject go = new GameObject($"Mark{i}", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 9; // فوق الطريق (0) وتحت السيارة (10)
            sr.enabled = false;
            marks[i] = sr;
            born[i] = -999f;
        }
    }

    /// <summary>
    /// وضع أثر إطار عند موضع عالمي بزاوية معينة (يستدعيه CarController في FixedUpdate).
    /// </summary>
    public void Drop(Vector2 pos, float angleZ)
    {
        SpriteRenderer sr = marks[next];
        born[next] = Time.time;
        next = (next + 1) % poolSize;

        if (sr == null) return;

        sr.transform.position = pos;
        sr.transform.rotation = Quaternion.Euler(0f, 0f, angleZ);
        sr.transform.localScale = new Vector3(markSize, markSize, 1f);
        sr.enabled = true;
        sr.color = markTint;
    }

    void LateUpdate()
    {
        float t = Time.time;
        for (int i = 0; i < poolSize; i++)
        {
            SpriteRenderer sr = marks[i];
            if (sr == null || !sr.enabled) continue;

            float age = t - born[i];
            if (age >= lifetime)
            {
                sr.enabled = false;
                continue;
            }

            Color c = markTint;
            c.a = startAlpha * (1f - age / lifetime);
            sr.color = c;
        }
    }

    /// <summary>علامة بيضاء بحواف ناعمة (تُلوَّن داكنة عبر sr.color) مولّدة بالكود.</summary>
    static Sprite MakeMarkSprite()
    {
        const int N = 32;
        Texture2D tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        Color[] px = new Color[N * N];

        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                // شريط بعرض كامل ونعومة على الحواف الطولية (الأثر ممتد بالتدوير)
                float dx = Mathf.Abs(x - (N - 1) * 0.5f) / (N * 0.5f);
                float dy = Mathf.Abs(y - (N - 1) * 0.5f) / (N * 0.5f);
                float a = Mathf.Clamp01(1f - dx * dx) * Mathf.Clamp01(1f - dy * dy);
                px[y * N + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, false);

        Sprite sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        sp.name = "SkidMark";
        return sp;
    }
}
