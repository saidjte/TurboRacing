using UnityEngine;

/// <summary>
/// TurboRacing - v1.0.0 (نفس سلوك v0.8.0 على الفضاء المجسّم)
/// يراقب السطح تحت السيارة كل إطار فيزياء:
/// - على العشب: خفض حد السرعة + تقليل تماسك الإطارات + تعتيم طلاء الهيكل المجسّم
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

    /// <summary>هل السيارة على العشب الآن؟ (يستخدمه الصوت/HUD)</summary>
    public bool OnGrass { get; private set; }

    CarController car;
    MeshRenderer paint;
    Color baseColor;
    bool hasBase;

    void Awake()
    {
        car = GetComponent<CarController>();
    }

    /// <summary>تحديث اللون الأساس بعد تغيير طلاء السيارة من الإعدادات
    /// (يُتجاهل أثناء التواجد على العشب حتى لا يُلتقط اللون المتلطّخ).</summary>
    public void RefreshBaseColor()
    {
        if (paint == null) paint = car.PaintRenderer;
        if (paint != null && !OnGrass)
        {
            baseColor = paint.material.color;
            hasBase = true;
        }
    }

    void FixedUpdate()
    {
        if (TrackBuilder.Instance == null) return;

        bool grass = TrackBuilder.Instance.IsGrass(transform.position);
        if (grass == OnGrass) return; // لا تغيير في السطح

        OnGrass = grass;

        if (paint == null) paint = car.PaintRenderer;

        if (grass)
        {
            if (paint != null && !hasBase) { baseColor = paint.material.color; hasBase = true; }

            car.SetSpeedLimit(grassSpeedMultiplier);
            car.SetGripOverride(grassGripMultiplier);

            // يبدو "متسخاً" (تعتيم الطلاء بلا شفافية)
            if (paint != null)
                paint.material.color = new Color(baseColor.r * 0.55f, baseColor.g * 0.55f, baseColor.b * 0.55f, 1f);
        }
        else
        {
            car.RestoreSpeedLimit();
            car.SetGripOverride(1f);

            if (paint != null && hasBase) paint.material.color = baseColor;
        }
    }
}
