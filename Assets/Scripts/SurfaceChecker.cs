using UnityEngine;

/// <summary>
/// TurboRacing - المرحلة 2
/// يراقب البلاطة تحت السيارة كل إطار فيزياء:
/// - على العشب: خفض حد السرعة + تقليل تماسك الإطارات + تلويث لون السيارة
/// - العودة للطريق: استعادة كل شيء تلقائياً
/// ملاحظة: الجدران تمنع الخروج من الحلبة أصلاً، والعقاب مخصص للعشب بين الكيرب والجدران.
/// </summary>
[RequireComponent(typeof(CarController))]
public class SurfaceChecker : MonoBehaviour
{
    [Header("عقاب العشب")]
    [Tooltip("مضاعف حد السرعة على العشب (0.45 = 45% من السرعة القصوى)")]
    public float grassSpeedMultiplier = 0.45f;

    [Tooltip("مضاعف تماسك الإطارات على العشب (انزلاق أكثر = فرملة أضعف على التوجيه)")]
    public float grassGripMultiplier = 0.6f;

    /// <summary>هل السيارة على العشب الآن؟ (مفيد للـ HUD في المراحل القادمة)</summary>
    public bool OnGrass { get; private set; }

    CarController car;
    SpriteRenderer sr;
    Color baseColor;

    void Awake()
    {
        car = GetComponent<CarController>();
        // (v0.8.0) الرسم انتقل إلى الأبناء: CarBody (الهيكل الملوّن) + CarTrim (التفاصيل)
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
    }

    /// <summary>(v0.8.0) تحديث اللون الأساس بعد تغيير طلاء السيارة من الإعدادات
    /// (يُتجاهل أثناء التواجد على العشب حتى لا يُلتقط اللون المتلطّخ).</summary>
    public void RefreshBaseColor()
    {
        if (sr != null && !OnGrass) baseColor = sr.color;
    }

    void FixedUpdate()
    {
        if (TrackBuilder.Instance == null) return;

        bool grass = TrackBuilder.Instance.IsGrass(transform.position);
        if (grass == OnGrass) return; // لا تغيير في السطح

        OnGrass = grass;

        if (grass)
        {
            car.SetSpeedLimit(grassSpeedMultiplier);
            car.SetGripOverride(grassGripMultiplier); // (v0.8.0) يُركَّب فوق تماسك مستوى الضبط
            if (sr != null) sr.color = new Color(baseColor.r * 0.6f, baseColor.g * 0.6f, baseColor.b * 0.6f, 1f); // يبدو "متسخاً" (بلا شفافية)
        }
        else
        {
            car.RestoreSpeedLimit();
            car.SetGripOverride(1f);
            if (sr != null) sr.color = baseColor;
        }
    }
}
