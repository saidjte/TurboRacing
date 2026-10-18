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
    float baseGrip;
    Color baseColor;

    void Awake()
    {
        car = GetComponent<CarController>();
        sr = GetComponent<SpriteRenderer>();
        baseGrip = car.lateralGrip;
        if (sr != null) baseColor = sr.color;
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
            car.lateralGrip = baseGrip * grassGripMultiplier;
            if (sr != null) sr.color = baseColor * 0.6f; // السيارة تبدو "متسخة"
        }
        else
        {
            car.RestoreSpeedLimit();
            car.lateralGrip = baseGrip;
            if (sr != null) sr.color = baseColor;
        }
    }
}
