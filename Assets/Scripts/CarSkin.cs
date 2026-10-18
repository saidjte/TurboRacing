using UnityEngine;

/// <summary>
/// TurboRacing - v0.9.0 (الطلاء شبه ثلاثي الأبعاد)
/// مولّد سبرايتات السيارة إجرائياً (بلا أي أصول خارجية) — نفس الهندسة القديمة تماماً
/// (128×256 بكسل، PPU=128 = 1×2 وحدة عالمية، نفس HalfWidth) فالفيزياء والمصادم
/// لا يتغيران، لكن الطلاء الآن يحاكي الحجم:
/// - Body(): هيكل أبيض بقبة مضيئة وسطاً تنسدل نحو الحواف (Ambient Occlusion) +
///   أحزمة طولية + لمعان العمود الفقري + شريطا سباق مزدوجان — يُلوَّن بطلاء اللاعب
/// - Trim(): تفاصيل ثابتة فوق الهيكل: عجلات بجنوط معدنية، قمرة زجاجية بانعكاس
///   سماء مائل، جناح خلفي "طائر" بظل على الصندوق، مصابيح عدسات، فتحات تهوية،
///   مرايا، شفة أمامية، عوادم — مع خط ظل داكن حول كامل الصورة الظلية
/// - Shadow(): ظل إسفلي ناعم (new v0.9.0) يوضع تحت السيارة = أهم إشارة عمق
/// الاتجاه: الأمام = أعلى السبرايت (+Y المحلي).
/// </summary>
public static class CarSkin
{
    const int W = 128;
    const int H = 256;

    static Sprite cachedBody;
    static Sprite cachedTrim;
    static Sprite cachedShadow;

    static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

    // ================= الهيكل الملوَّن =================

    public static Sprite Body()
    {
        if (cachedBody != null) return cachedBody;

        Color[] px = new Color[W * H];
        var rng = new System.Random(9001);

        for (int y = 0; y < H; y++)
        {
            float hw = HalfWidth(y);
            for (int x = 0; x < W; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - W * 0.5f);
                if (dx > hw) { px[y * W + x] = Clear; continue; }

                float t = dx / hw; // 0 = المنتصف، 1 = الحافة

                // قبة جانبية: مضيئة بالمنتصف تنسدل نحو الحواف (يحاكي سقف مجسم)
                float dome = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.26f) / 0.74f));
                float v = Mathf.Lerp(1.00f, 0.58f, dome);

                // تظليل طولي: أنف أفتح (يلتقط الضوء) ومؤخرة أغمق قليلاً
                if (y > 190f) v *= Mathf.Lerp(1.03f, 1.09f, Mathf.Clamp01((y - 190f) / 65f));
                else if (y < 46f) v *= 0.93f;

                // لمعان العمود الفقري (سبتاكولار ضيق على غطاء المحرك والسقف)
                if (dx < 5.5f && y > 52f && y < 214f) v *= 1.055f;

                // شريطا سباق مزدوجان (أغمق من الطلاء بحافة ناعمة)
                float cx = Mathf.Abs(x + 0.5f - W * 0.5f);
                float stripe = Mathf.Abs(cx - 9.5f);
                if (stripe < 5.0f && y > 28f)
                    v *= Mathf.Lerp(0.85f, 1f, Mathf.SmoothStep(0f, 1f, stripe / 5.0f));

                // حواف: ظل حاد 1.4px + نطاق AO ناعم
                float edge = hw - dx;
                if (edge < 1.4f) v *= 0.50f;
                else if (edge < 3.6f) v *= Mathf.Lerp(0.72f, 1f, (edge - 1.4f) / 2.2f);

                // حبيبات دقيقة (خشونة طلاء خفيفة)
                float n = (float)rng.NextDouble() * 0.035f - 0.0175f;
                v = Mathf.Clamp(v + n, 0.30f, 1.12f);

                px[y * W + x] = new Color(v, v, v, 1f);
            }
        }

        cachedBody = MakeSprite(px, "CarBodySprite", W);
        return cachedBody;
    }

    // ================= طبقة التفاصيل الثابتة =================

    public static Sprite Trim()
    {
        if (cachedTrim != null) return cachedTrim;

        Color[] px = new Color[W * H];

        Color tire      = new Color(0.055f, 0.055f, 0.065f, 1f);
        Color tireEdge  = new Color(0.02f, 0.02f, 0.025f, 1f);
        Color rim       = new Color(0.40f, 0.41f, 0.44f, 1f);
        Color rimHi     = new Color(0.56f, 0.58f, 0.62f, 1f);
        Color hub       = new Color(0.14f, 0.14f, 0.16f, 1f);
        Color glass     = new Color(0.10f, 0.12f, 0.17f, 1f);
        Color glassHi   = new Color(0.55f, 0.62f, 0.72f, 1f);
        Color frame     = new Color(0.05f, 0.05f, 0.07f, 1f);
        Color wing      = new Color(0.10f, 0.10f, 0.12f, 1f);
        Color wingEdge  = new Color(0.34f, 0.34f, 0.38f, 1f);
        Color lens      = new Color(0.95f, 0.92f, 0.70f, 1f);
        Color lensCore  = new Color(1.00f, 0.98f, 0.85f, 1f);
        Color chrome    = new Color(0.85f, 0.86f, 0.88f, 1f);
        Color tail      = new Color(0.85f, 0.10f, 0.12f, 1f);
        Color tailCore  = new Color(1.00f, 0.30f, 0.28f, 1f);
        Color dark      = new Color(0.09f, 0.09f, 0.11f, 1f);

        // ===== 1) ظل الجناح على صندوق المؤخرة (الجناح "طائر" فوقه) =====
        FillRoundRect(px, 24f, 17f, W - 20f, 27f, 5f, new Color(0f, 0f, 0f, 0.34f));

        // ===== 2) العجلات (أربع، تبرز خارج الهيكل) =====
        Wheel(px, 6f, 28f, 40f, 94f, tire, tireEdge, rim, rimHi, hub);
        Wheel(px, W - 40f, 28f, W - 6f, 94f, tire, tireEdge, rim, rimHi, hub);
        Wheel(px, 8f, 164f, 40f, 226f, tire, tireEdge, rim, rimHi, hub);
        Wheel(px, W - 40f, 164f, W - 8f, 226f, tire, tireEdge, rim, rimHi, hub);

        // ===== 3) الجناح الخلفي: دعامتان + ريشة بعرض أكبر + حواف مضيئة =====
        FillRect(px, 46, 16, 52, 26, dark);
        FillRect(px, W - 52, 16, W - 46, 26, dark);
        FillRoundRect(px, 1f, 1f, 6f, 19f, 1.5f, new Color(0.18f, 0.18f, 0.21f, 1f));
        FillRoundRect(px, W - 6f, 1f, W - 1f, 19f, 1.5f, new Color(0.18f, 0.18f, 0.21f, 1f));
        FillRect(px, 2, 18, 5, 19, wingEdge);             // لمعة أعلى الدعامات الطرفية
        FillRect(px, W - 5, 18, W - 2, 19, wingEdge);
        FillRoundRect(px, 3f, 3f, W - 3f, 15f, 3f, wing); // الريشة
        FillRect(px, 4, 12, W - 4, 14, wingEdge);          // لمعة الحافة العلوية

        // ===== 4) القمرة الزجاجية: إطار + زجاج + انعكاس سماء مائل =====
        // الإطار (أعمدة سوداء حول الزجاج)
        FillTrapezoid(px, 174, 198, 48f, 62f, frame);
        FillRoundRect(px, 32f, 118f, W - 32f, 178f, 10f, frame);
        FillTrapezoid(px, 96, 120, 42f, 54f, frame);
        // الزجاج (أصغر بإطار 2.5px)
        FillTrapezoid(px, 177, 195, 43f, 57f, glass);
        FillRoundRect(px, 34.5f, 121f, W - 34.5f, 175f, 8f, glass);
        FillTrapezoid(px, 99, 117, 37f, 49f, glass);
        // انعكاسات مائلة (أشرطة سماء): تُرسم فقط فوق بكسلات الزجاج الحالية
        ReflectStreaks(px, 177, 195, 43f, 57f, glassHi, 18f, 5f);
        ReflectStreaksRound(px, 34.5f, 121f, W - 34.5f, 175f, glassHi, 22f, 6f);
        ReflectStreaks(px, 99, 117, 37f, 49f, glassHi, 14f, 4f);
        // التقاط ضوء أعلى الزجاج الأمامي (حافة مضيئة)
        FillTrapezoid(px, 192, 195, 55.5f, 56.5f, new Color(0.75f, 0.82f, 0.90f, 0.65f));

        // ===== 5) فتحات تهوية غطاء المحرك =====
        for (int s = 0; s < 2; s++)
        {
            int x0 = s == 0 ? 38 : W - 50;
            FillRect(px, x0, 212, x0 + 12, 222, dark);
            FillRect(px, x0, 210, x0 + 12, 211, new Color(0.45f, 0.45f, 0.50f, 0.8f));
        }

        // ===== 6) المرايا الجانبية =====
        FillRoundRect(px, 2f, 168f, 10f, 176f, 2f, dark);
        FillRect(px, 2, 174, 10, 175, wingEdge);
        FillRoundRect(px, W - 10f, 168f, W - 2f, 176f, 2f, dark);
        FillRect(px, W - 10, 174, W - 2, 175, wingEdge);

        // ===== 7) المصابيح =====
        // أمامية: عدسة بتدرج + حلقة كروم
        Headlight(px, 48f, 246f, lens, lensCore, chrome);
        Headlight(px, W - 48f, 246f, lens, lensCore, chrome);
        // خلفية: housing + أحمر قلب ساطع
        FillRoundRect(px, 22f, 4f, 46f, 13f, 2.5f, dark);
        FillRoundRect(px, 24f, 5.5f, 44f, 11.5f, 2f, tail);
        FillEllipse(px, 35f, 8.5f, 7f, 2.2f, tailCore);
        FillRoundRect(px, W - 46f, 4f, W - 22f, 13f, 2.5f, dark);
        FillRoundRect(px, W - 44f, 5.5f, W - 24f, 11.5f, 2f, tail);
        FillEllipse(px, W - 35f, 8.5f, 7f, 2.2f, tailCore);

        // ===== 8) العوادم =====
        Exhaust(px, 54f, 6f);
        Exhaust(px, 74f, 6f);

        // ===== 9) الشفة الأمامية (شفة سوداء عند الأنف) =====
        for (int y = 249; y <= 255; y++)
        {
            float hw = HalfWidth(y);
            int xa = Mathf.RoundToInt(W * 0.5f - hw + 1f);
            int xb = Mathf.RoundToInt(W * 0.5f + hw - 1f);
            for (int x = xa; x <= xb; x++)
                if (x >= 0 && x < W) px[y * W + x] = dark;
        }

        // ===== 10) خط الظل المحيط (AO خارجي حول الصورة الظلية) =====
        for (int y = 0; y < H; y++)
        {
            float hw = HalfWidth(y);
            for (int x = 0; x < W; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - W * 0.5f);
                if (dx > hw && dx <= hw + 1.3f && px[y * W + x].a == 0f)
                    px[y * W + x] = new Color(0f, 0f, 0f, 0.55f);
            }
        }

        cachedTrim = MakeSprite(px, "CarTrimSprite", W);
        return cachedTrim;
    }

    // ================= ظل الأرض (new v0.9.0) =================

    /// <summary>ظل ناعم كبير يوضع تحت السيارة (أقوى إشارة عمق في الألعاب العلوية).</summary>
    public static Sprite Shadow()
    {
        if (cachedShadow != null) return cachedShadow;

        const int SW = 192, SH = 320;
        Color[] px = new Color[SW * SH];
        float p = 3.2f; // أُس superellipse: يقترب شكل الظل من مستطيل مستدير يلتف حول السيارة

        for (int y = 0; y < SH; y++)
        {
            for (int x = 0; x < SW; x++)
            {
                float nx = (x + 0.5f) / SW * 2f - 1f;
                float ny = (y + 0.5f) / SH * 2f - 1f;
                float d = Mathf.Pow(
                    Mathf.Pow(Mathf.Abs(nx), p) + Mathf.Pow(Mathf.Abs(ny), p), 1f / p);
                float a = 0.62f * (1f - Mathf.SmoothStep(0.55f, 1.0f, d));
                px[y * SW + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        }

        cachedShadow = MakeSprite(px, "CarShadowSprite", SW);
        return cachedShadow;
    }

    // ================= أدوات العناصر =================

    /// <summary>عجلة: إطار داكن بحافة + جنط معدني بلمعة علوية + صرة.</summary>
    static void Wheel(Color[] px, float x0, float y0, float x1, float y1,
        Color tire, Color tireEdge, Color rim, Color rimHi, Color hub)
    {
        FillRoundRect(px, x0, y0, x1, y1, 7f, tireEdge);
        FillRoundRect(px, x0 + 1.5f, y0 + 1.5f, x1 - 1.5f, y1 - 1.5f, 6f, tire);
        float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
        FillRoundRect(px, x0 + 6f, y0 + 7f, x1 - 6f, y1 - 7f, 5f, rim);
        FillRoundRect(px, x0 + 6f, cy + 2f, x1 - 6f, y1 - 8f, 5f, rimHi); // لمعة النصف العلوي
        FillRoundRect(px, x0 + 8f, cy - 3f, x1 - 8f, y1 - 10f, 4f, rim);  // إعادة الوسط (تدرج حاد)
        FillEllipse(px, cx, cy, 4.5f, 4.5f, hub);                          // الصرة
    }

    static void Headlight(Color[] px, float cx, float cy, Color lens, Color core, Color chrome)
    {
        FillEllipse(px, cx, cy, 10f, 6.5f, chrome);
        FillEllipse(px, cx, cy, 8.5f, 5.2f, lens);
        FillEllipse(px, cx - 1f, cy + 0.5f, 5.5f, 3.2f, core);
    }

    static void Exhaust(Color[] px, float cx, float cy)
    {
        FillEllipse(px, cx, cy, 5f, 5f, new Color(0.42f, 0.43f, 0.46f, 1f));
        FillEllipse(px, cx, cy, 3.2f, 3.2f, new Color(0.05f, 0.05f, 0.06f, 1f));
    }

    /// <summary>أشرطة انعكاس مائلة داخل شبه منحرف الزجاج (فوق بكسلات الزجاج فقط).</summary>
    static void ReflectStreaks(Color[] px, int y0, int y1, float wBottom, float wTop,
        Color hi, float period, float width)
    {
        float cx = W * 0.5f;
        for (int y = y0; y <= y1; y++)
        {
            float t = (y1 == y0) ? 0f : (y - y0) / (float)(y1 - y0);
            float halfW = Mathf.Lerp(wBottom, wTop, t) * 0.5f;
            float xStart = -halfW + (y - y0) * 0.35f;
            for (int x = Mathf.RoundToInt(cx - halfW); x <= Mathf.RoundToInt(cx + halfW); x++)
            {
                if (x < 0 || x >= W || px[y * W + x].a == 0f) continue;
                float local = (x + 0.5f - cx) - xStart;
                float m = Mathf.Repeat(local, period);
                if (m < width)
                {
                    Color c = px[y * W + x];
                    px[y * W + x] = Color.Lerp(c, hi, 0.5f * (1f - m / width));
                }
            }
        }
    }

    /// <summary>نفس الأشرطة داخل مستطيل مستدير (سقف القمرة).</summary>
    static void ReflectStreaksRound(Color[] px, float x0, float y0, float x1, float y1,
        Color hi, float period, float width)
    {
        int ya = Mathf.CeilToInt(y0), yb = Mathf.FloorToInt(y1);
        for (int y = ya; y <= yb; y++)
        {
            float xStart = x0 + (y - y0) * 0.30f;
            for (int x = Mathf.CeilToInt(x0); x <= Mathf.FloorToInt(x1); x++)
            {
                if (px[y * W + x].a == 0f) continue;
                float m = Mathf.Repeat((x + 0.5f) - xStart, period);
                if (m < width)
                {
                    Color c = px[y * W + x];
                    px[y * W + x] = Color.Lerp(c, hi, 0.45f * (1f - m / width));
                }
            }
        }
    }

    // ================= هندسة الهيكل (كما هي — الفيزياء ثابتة) =================

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

    /// <summary>مستطيل مستدير الزوايا بحواف ناعمة بكسل واحد.</summary>
    static void FillRoundRect(Color[] px, float x0, float y0, float x1, float y1, float r, Color c)
    {
        int xa = Mathf.Max(0, Mathf.FloorToInt(x0));
        int xb = Mathf.Min(W - 1, Mathf.CeilToInt(x1));
        int ya = Mathf.Max(0, Mathf.FloorToInt(y0));
        int yb = Mathf.Min(H - 1, Mathf.CeilToInt(y1));

        for (int y = ya; y <= yb; y++)
        {
            for (int x = xa; x <= xb; x++)
            {
                float fx = Mathf.Clamp(x + 0.5f, x0 + r, x1 - r);
                float fy = Mathf.Clamp(y + 0.5f, y0 + r, y1 - r);
                float d = Mathf.Sqrt((x + 0.5f - fx) * (x + 0.5f - fx) +
                                     (y + 0.5f - fy) * (y + 0.5f - fy));
                float a = Mathf.Clamp01(r - d + 0.5f); // حافة ناعمة 1px
                if (a <= 0f) continue;
                Color dst = px[y * W + x];
                Color src = c; src.a *= a;
                px[y * W + x] = dst.a == 0f ? src : Blend(dst, src);
            }
        }
    }

    /// <summary>قطع ناقص بحواف ناعمة.</summary>
    static void FillEllipse(Color[] px, float cx, float cy, float rx, float ry, Color c)
    {
        int xa = Mathf.Max(0, Mathf.FloorToInt(cx - rx));
        int xb = Mathf.Min(W - 1, Mathf.CeilToInt(cx + rx));
        int ya = Mathf.Max(0, Mathf.FloorToInt(cy - ry));
        int yb = Mathf.Min(H - 1, Mathf.CeilToInt(cy + ry));

        for (int y = ya; y <= yb; y++)
        {
            for (int x = xa; x <= xb; x++)
            {
                float nx = (x + 0.5f - cx) / rx;
                float ny = (y + 0.5f - cy) / ry;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float a = Mathf.Clamp01((1f - d) * Mathf.Min(rx, ry) + 0.5f);
                if (a <= 0f) continue;
                Color dst = px[y * W + x];
                Color src = c; src.a *= Mathf.Clamp01(a);
                px[y * W + x] = dst.a == 0f ? src : Blend(dst, src);
            }
        }
    }

    /// <summary>دمج src فوق dst بخوارزمية over القياسية.</summary>
    static Color Blend(Color dst, Color src)
    {
        float a = src.a + dst.a * (1f - src.a);
        if (a <= 0f) return Clear;
        return new Color(
            (src.r * src.a + dst.r * dst.a * (1f - src.a)) / a,
            (src.g * src.a + dst.g * dst.a * (1f - src.a)) / a,
            (src.b * src.a + dst.b * dst.a * (1f - src.a)) / a,
            a);
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
                px[y * W + x] = px[y * W + x].a == 0f ? c : Blend(px[y * W + x], c);
            }
        }
    }

    static Sprite MakeSprite(Color[] px, string name, int texW)
    {
        Texture2D tex = new Texture2D(texW, px.Length / texW, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear; // مظهر ناعم يدعم إحساس الحجم (v0.9.0)
        tex.SetPixels(px);
        tex.Apply(false, false);

        // 128 بكسل لكل وحدة => الهيكل 1×2 وحدة عالمية (مطابق للفيزياء)
        Sprite sp = Sprite.Create(tex, new Rect(0, 0, texW, px.Length / texW),
            new Vector2(0.5f, 0.5f), 128f);
        sp.name = name;
        return sp;
    }
}
