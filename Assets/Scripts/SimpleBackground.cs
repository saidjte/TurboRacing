using UnityEngine;

/// <summary>
/// TurboRacing - المرحلة 1
/// خلفية بسيطة: أرضية شبكية مولّدة بالكود (بدون أي صور خارجية)
/// + ضبط الكاميرا (حجم العرض، لون الخلفية) + تثبيت هدف 60 إطاراً/ثانية.
/// سيتم استبدالها بالمسار الحقيقي (Tilemap) في المرحلة 2.
/// </summary>
public class SimpleBackground : MonoBehaviour
{
    [Header("شكل الأرضية")]
    [Tooltip("حجم الأرضية المربعة بالوحدات")]
    public float worldSize = 200f;

    [Tooltip("عدد خانات الشبكة على كل ضلع")]
    public int gridCells = 50;

    [Tooltip("سماكة خطوط الشبكة بالبكسل")]
    public int lineThickness = 2;

    [Header("الألوان")]
    public Color groundColor = new Color(0.16f, 0.18f, 0.20f);
    public Color lineColor   = new Color(0.27f, 0.30f, 0.33f);

    [Header("الكاميرا")]
    [Tooltip("حجم عرض الكاميرا العمودي (Orthographic Size)")]
    public float cameraViewSize = 7f;

    void Start()
    {
        // هدف 60 إطاراً في الثانية على الموبايل
        Application.targetFrameRate = 60;

        SetupCamera();
        CreateGround();
    }

    void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        if (cam.orthographic)
            cam.orthographicSize = cameraViewSize;

        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = groundColor;
    }

    void CreateGround()
    {
        const int TexSize = 1024;
        int cells = Mathf.Max(1, gridCells);
        int cellPx = Mathf.Max(2, TexSize / cells);
        int thickness = Mathf.Clamp(lineThickness, 1, cellPx / 2);

        // توليد نسيج الشبكة إجرائياً
        Texture2D tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color[] pixels = new Color[TexSize * TexSize];

        for (int y = 0; y < TexSize; y++)
        {
            bool rowLine = (y % cellPx) < thickness;
            for (int x = 0; x < TexSize; x++)
            {
                bool colLine = (x % cellPx) < thickness;
                pixels[y * TexSize + x] = (rowLine || colLine) ? lineColor : groundColor;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);

        // بكسل لكل وحدة بحيث تغطي الأرضية worldSize × worldSize
        float ppu = TexSize / Mathf.Max(1f, worldSize);

        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, TexSize, TexSize),
            new Vector2(0.5f, 0.5f),
            ppu);
        sprite.name = "GridBackground";

        GameObject groundGO = new GameObject("GroundSprite");
        groundGO.transform.SetParent(transform, false);
        groundGO.transform.localPosition = Vector3.zero;

        SpriteRenderer sr = groundGO.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = -10;
    }
}
