using UnityEngine;

/// <summary>
/// TurboRacing - v0.8.0
/// مولّد سبرايتات السيارة إجرائياً (بلا أي أصول خارجية):
/// - Body(): هيكل سيارة سباق أبيض (يُلوَّن عبر SpriteRenderer.color) بمقدمة
///   مدببة ومؤخرة عريضة وزوايا مستديرة — 128×256 بكسل = 1×2 وحدة عالمية
///   (نفس أبعاد السيارة القديمة تماماً: فيزياء ومصادم بلا أي تغيير)
/// - Trim(): طبقة التفاصيل الثابتة فوق الهيكل: عجلات + قمرة زجاجية +
///   جناح خلفي + مصابيح أمامية وخلفية — ألوانها لا تتأثر بلون الطلاء
/// الاتجاه: الأمام = أعلى السبرايت (+Y المحلي).
/// </summary>
public static class CarSkin
{
    const int W = 128;
    const int H = 256;

    static Sprite cachedBody;
    static Sprite cachedTrim;

    public static Sprite Body()
    {
        if (cachedBody != null) return cachedBody;

        Color[] px = new Color[W * H];
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int i = 0; i < px.Length; i++) px[i] = clear;

        for (int y = 0; y < H; y++)
        {
            float hw = HalfWidth(y);
            for (int x = 0; x < W; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - W * 0.5f);
                if (dx <= hw)
                {
                    // حافة داكنة قليلاً لتحديد شكل الهيكل بعد التلوين
                    float v = (hw - dx) < 2.5f ? 0.78f : 1f;
                    px[y * W + x] = new Color(v, v, v, 1f);
                }
            }
        }

        cachedBody = MakeSprite(px, "CarBodySprite");
        return cachedBody;
    }

    public static Sprite Trim()
    {
        if (cachedTrim != null) return cachedTrim;

        Color[] px = new Color[W * H];
        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int i = 0; i < px.Length; i++) px[i] = clear;

        Color wheelDark = new Color(0.07f, 0.07f, 0.08f, 1f);
        Color wheelHub  = new Color(0.34f, 0.35f, 0.38f, 1f);
        Color glass     = new Color(0.11f, 0.13f, 0.17f, 1f);
        Color glassHi   = new Color(0.24f, 0.29f, 0.36f, 1f);
        Color wing      = new Color(0.10f, 0.10f, 0.12f, 1f);
        Color headlight = new Color(1f, 0.95f, 0.72f, 1f);
        Color taillight = new Color(0.90f, 0.12f, 0.12f, 1f);

        // ===== الجناح الخلفي (أعرض قليلاً من المؤخرة) =====
        FillRect(px, 2, 16, W - 2, 20, wing);

        // ===== العجلات (أربع، تبرز قليلاً خارج الهيكل) =====
        // الخلفية
        FillRect(px, 12, 34, 34, 88, wheelDark);
        FillRect(px, W - 34, 34, W - 12, 88, wheelDark);
        FillRect(px, 17, 40, 29, 82, wheelHub);
        FillRect(px, W - 29, 40, W - 17, 82, wheelHub);
        // الأمامية
        FillRect(px, 14, 168, 34, 222, wheelDark);
        FillRect(px, W - 34, 168, W - 14, 222, wheelDark);
        FillRect(px, 19, 174, 29, 216, wheelHub);
        FillRect(px, W - 29, 174, W - 19, 216, wheelHub);

        // ===== القمرة الزجاجية (زجاج أمامي + سقف + زجاج خلفي) =====
        // الزجاج الأمامي: شبه منحرف أعرض عند الأعلى
        FillTrapezoid(px, 148, 190, 54f, 68f, glass);
        // شريط انعكاس أعلى الزجاج
        FillTrapezoid(px, 178, 188, 62f, 66f, glassHi);
        // السقف
        FillRect(px, 36, 96, W - 36, 148, glass);
        // الزجاج الخلفي: شبه منحرف أضيق عند الأسفل
        FillTrapezoid(px, 62, 96, 48f, 58f, glass);

        // ===== المصابيح =====
        // أمامية (على الأنف المدبب)
        FillRect(px, 42, 240, 57, 249, headlight);
        FillRect(px, W - 57, 240, W - 42, 249, headlight);
        // خلفية
        FillRect(px, 30, 4, 50, 12, taillight);
        FillRect(px, W - 50, 4, W - 30, 12, taillight);

        cachedTrim = MakeSprite(px, "CarTrimSprite");
        return cachedTrim;
    }

    // ================= هندسة الهيكل =================

    /// <summary>نصف عرض الهيكل عند الصف y (0=المؤخرة، H-1=الأنف).</summary>
    static float HalfWidth(float y)
    {
        const float rearHalf = 55f;
        const float noseHalf = 30f;

        float hw = rearHalf;

        // تضييق الأنف (من y=196 حتى النهاية)
        if (y > 196f)
        {
            float t = (y - 196f) / 59f;
            hw = Mathf.Lerp(rearHalf, noseHalf, t);
        }

        // استدارة زوايا المؤخرة (نصف قطر 16)
        if (y < 16f)
        {
            float dy = 16f - y;
            float dx = Mathf.Sqrt(Mathf.Max(0f, 16f * 16f - dy * dy));
            hw = Mathf.Min(hw, rearHalf - (16f - dx));
        }

        // استدارة طرف الأنف (نصف قطر 11)
        if (y > 244f)
        {
            float dy = y - 244f;
            float dx = Mathf.Sqrt(Mathf.Max(0f, 11f * 11f - dy * dy));
            hw = Mathf.Min(hw, noseHalf - (11f - dx));
        }

        return hw;
    }

    // ================= أدوات الرسم =================

    static void FillRect(Color[] px, int x0, int y0, int x1, int y1, Color c)
    {
        x0 = Mathf.Clamp(x0, 0, W - 1);
        x1 = Mathf.Clamp(x1, 0, W - 1);
        y0 = Mathf.Clamp(y0, 0, H - 1);
        y1 = Mathf.Clamp(y1, 0, H - 1);

        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                px[y * W + x] = c;
    }

    /// <summary>شبه منحرف أفقي: عرض wBottom عند y0 و wTop عند y1 (متمركز حول منتصف X).</summary>
    static void FillTrapezoid(Color[] px, int y0, int y1, float wBottom, float wTop, Color c)
    {
        y0 = Mathf.Clamp(y0, 0, H - 1);
        y1 = Mathf.Clamp(y1, 0, H - 1);
        float cx = W * 0.5f;

        for (int y = y0; y <= y1; y++)
        {
            float t = (y1 == y0) ? 0f : (y - y0) / (float)(y1 - y0);
            float halfW = Mathf.Lerp(wBottom, wTop, t) * 0.5f;
            int xa = Mathf.RoundToInt(cx - halfW);
            int xb = Mathf.RoundToInt(cx + halfW);
            for (int x = xa; x <= xb; x++)
            {
                if (x < 0 || x >= W) continue;
                px[y * W + x] = c;
            }
        }
    }

    static Sprite MakeSprite(Color[] px, string name)
    {
        Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply(false, false);

        // 128 بكسل لكل وحدة => الهيكل 1×2 وحدة عالمية (مطابق للفيزياء القديمة)
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), W);
        sp.name = name;
        return sp;
    }
}
