using UnityEngine;

/// <summary>
/// TurboRacing - المرحلة 5 (v0.7.0)
/// مولّد أصوات إجرائي بالكود: كل المؤثرات تُبنى رياضياً عند الإقلاع
/// (بلا ملفات صوتية خارجية) — متوافق تماماً مع خط الإنتاج السحابي.
/// القاعدة الذهبية للحلقات المستمرة: كل الترددات داخل المقطع يجب أن تكون
/// مضاعفات صحيحة لـ 1/المدة حتى تُحكم الحلقة بلا نقرة عند الالتفاف.
/// </summary>
public static class AudioSynth
{
    public const int Rate = 22050; // معدل عينات كافٍ للنغمات المركّبة وخفيف على الموبايل

    /// <summary>بناء مقطع أحادي من مولّد عينات (i = رقم العينة، الناتج -1..1).</summary>
    static AudioClip Build(string name, int samples, System.Func<int, float> gen)
    {
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
            data[i] = Mathf.Clamp(gen(i), -1f, 1f);

        AudioClip clip = AudioClip.Create(name, samples, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>موجة نشّارة (Saw) متمركزة حول الصفر — أساس صوت المحرك.</summary>
    static float Saw(float t, float freq)
    {
        float phase = t * freq;
        return 2f * (phase - Mathf.Floor(phase + 0.5f));
    }

    // ================= الحلقات المستمرة =================

    /// <summary>
    /// حلقة محرك (0.6 ث): نشّارة 85Hz + 170 + 255 (كلها دورية في المقطع)
    /// مع تذبذب سعة 10Hz يعطي إحساس الاحتراق — الطبق يتغير لاحقاً عبر AudioSource.pitch.
    /// </summary>
    public static AudioClip EngineLoop()
    {
        const float dur = 0.6f;
        int n = Mathf.RoundToInt(dur * Rate);
        return Build("EngineLoop", n, i =>
        {
            float t = i / (float)Rate;
            float wob = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 10f * t);
            return (0.50f * Saw(t, 85f) + 0.30f * Saw(t, 170f) + 0.15f * Saw(t, 255f)) * wob * 0.55f;
        });
    }

    /// <summary>حلقة صرير فرامل (0.5 ث): ثلاث نغمات عالية خشنة + تذبذب سعة 8Hz.</summary>
    public static AudioClip SkidLoop()
    {
        const float dur = 0.5f;
        int n = Mathf.RoundToInt(dur * Rate);
        return Build("SkidLoop", n, i =>
        {
            float t = i / (float)Rate;
            float wob = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 8f * t);
            return (0.40f * Mathf.Sin(2f * Mathf.PI * 1990f * t)
                  + 0.30f * Mathf.Sin(2f * Mathf.PI * 2270f * t)
                  + 0.20f * Mathf.Sin(2f * Mathf.PI * 1710f * t)) * wob * 0.5f;
        });
    }

    /// <summary>حلقة حفيف عشب (0.5 ث): نغمات منخفضة متداخلة تموّج "خرخراً" ناعماً.</summary>
    public static AudioClip GrassLoop()
    {
        const float dur = 0.5f;
        int n = Mathf.RoundToInt(dur * Rate);
        return Build("GrassLoop", n, i =>
        {
            float t = i / (float)Rate;
            float wob = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 4f * t);
            return (0.45f * Mathf.Sin(2f * Mathf.PI * 24f * t)
                  + 0.30f * Mathf.Sin(2f * Mathf.PI * 32f * t)
                  + 0.25f * Mathf.Sin(2f * Mathf.PI * 48f * t)
                  + 0.20f * Mathf.Sin(2f * Mathf.PI * 64f * t)) * wob * 0.5f;
        });
    }

    // ================= المؤثرات اللحظية =================

    /// <summary>نغمة قصيرة بنيّة حادة (تنبيهات العد، GO، نقرات الواجهة).</summary>
    public static AudioClip Beep(float freq, float dur, string name, float vol = 0.9f)
    {
        int n = Mathf.RoundToInt(dur * Rate);
        return Build(name, n, i =>
        {
            float t = i / (float)Rate;
            float attack = Mathf.Min(1f, i / (0.008f * Rate));        // هجوم 8ms
            float decay = Mathf.Exp(-3.5f * t / dur);                 // تلاشٍ أُسّي
            return Mathf.Sin(2f * Mathf.PI * freq * t) * attack * decay * vol;
        });
    }

    /// <summary>رنة لفة مكتملة: نغمتان صاعدتان 660 ← 990 هرتز (~0.4 ث).</summary>
    public static AudioClip LapChime()
    {
        const float dur = 0.42f;
        int n = Mathf.RoundToInt(dur * Rate);
        return Build("LapChime", n, i =>
        {
            float t = i / (float)Rate;
            if (t < 0.17f)
            {
                float a = Mathf.Min(1f, i / (0.006f * Rate)) * Mathf.Exp(-2f * t / 0.17f);
                return Mathf.Sin(2f * Mathf.PI * 660f * t) * a * 0.85f;
            }
            float t2 = t - 0.20f;
            if (t2 < 0f) return 0f;
            float a2 = Mathf.Min(1f, t2 / 0.006f) * Mathf.Exp(-2.5f * t2 / 0.22f);
            return Mathf.Sin(2f * Mathf.PI * 990f * t2) * a2 * 0.9f;
        });
    }

    /// <summary>رنة رقم قياسي جديد: أربع نغمات صاعدة سريعة تنتهي بنغمة عالية ممتدة (~0.9 ث).</summary>
    public static AudioClip RecordJingle()
    {
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        const float step = 0.13f;
        const float dur = 0.95f;
        int n = Mathf.RoundToInt(dur * Rate);
        return Build("RecordJingle", n, i =>
        {
            float t = i / (float)Rate;
            int idx = Mathf.Min((int)(t / step), notes.Length - 1);
            float lt = t - idx * step;
            float a = Mathf.Min(1f, lt / 0.006f);
            float env;
            if (idx < notes.Length - 1)
                env = a * Mathf.Exp(-2f * lt / step);
            else
                env = a * Mathf.Exp(-1.8f * lt / (dur - (notes.Length - 1) * step));
            return Mathf.Sin(2f * Mathf.PI * notes[idx] * t) * env * 0.85f;
        });
    }

    /// <summary>تصفيق نهاية السباق: فانفار أربع نغمات + وتر ختامي (~1.2 ث).</summary>
    public static AudioClip FinishFanfare()
    {
        float[] notes = { 392f, 523.25f, 659.25f, 783.99f };
        const float step = 0.15f;
        const float dur = 1.25f;
        int n = Mathf.RoundToInt(dur * Rate);
        return Build("FinishFanfare", n, i =>
        {
            float t = i / (float)Rate;
            // الوتر الختامي بعد آخر نغمة
            if (t >= notes.Length * step)
            {
                float ct = t - notes.Length * step;
                float a = Mathf.Min(1f, ct / 0.01f) * Mathf.Exp(-1.6f * ct / 0.5f);
                return (Mathf.Sin(2f * Mathf.PI * 523.25f * t)
                      + Mathf.Sin(2f * Mathf.PI * 659.25f * t)
                      + Mathf.Sin(2f * Mathf.PI * 783.99f * t)) * a * 0.28f;
            }

            int idx = Mathf.Min((int)(t / step), notes.Length - 1);
            float lt = t - idx * step;
            float env = Mathf.Min(1f, lt / 0.006f) * Mathf.Exp(-1.5f * lt / step);
            return Mathf.Sin(2f * Mathf.PI * notes[idx] * t) * env * 0.8f;
        });
    }

    /// <summary>ارتطام: ضجيج مُمرَّر منخفضاً يخفت بسرعة + دقة 55Hz عميقة (0.4 ث).</summary>
    public static AudioClip Crash()
    {
        const float dur = 0.4f;
        int n = Mathf.RoundToInt(dur * Rate);
        float lp = 0f; // مرشح تمرير منخفض بسيط (حالة مُنغلقة)
        return Build("Crash", n, i =>
        {
            float t = i / (float)Rate;
            float w = Mathf.Sin(i * 12.9898f) * 43758.5453f;
            w -= Mathf.Floor(w);          // 0..1
            w = w * 2f - 1f;              // -1..1
            lp += 0.28f * (w - lp);       // تنعيم
            float env = Mathf.Exp(-11f * t);
            float thump = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-9f * t) * 0.8f;
            return (lp * 0.9f + thump) * env;
        });
    }
}
