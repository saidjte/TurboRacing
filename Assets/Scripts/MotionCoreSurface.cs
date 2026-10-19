using MotionCore.Vehicle.Core;
using UnityEngine;

/// <summary>
/// TurboRacing v1.3.0 — مزوّد السطح لمحرك MotionCore.
/// يطبق واجهة IVehicleSurfaceProvider الخاصة بالحزمة:
/// - يصنف الأرض تحت كل عجلة من اسم المصادم المصيب (أشعة الفيزياء الداخلية للعجلة):
///     TARMAC/PIT/kerb = إسفلت (تماسك كامل)
///     GRASS/sand/GRAVEL/rock = عشب/حصى (تماسك مخفَّض — عقاب خارج المسار)
/// - مضاعف خارجي (gripOverride) من واجهة CarController يبقى صالحاً فوق التصنيف
///   (يستخدمه SurfaceChecker للتوافق مع السلوك القديم).
/// </summary>
public class MotionCoreSurface : MonoBehaviour, IVehicleSurfaceProvider
{
    [Tooltip("مضاعف تماسك أقصى على العشب (أمامي/جانبي) — يضبطه عقاب العشب")]
    public float grassForwardGrip = 0.55f;
    public float grassSidewaysGrip = 0.45f;

    /// <summary>مضاعف عام (1 = طبيعي). تضبطه CarController.SetGripOverride.</summary>
    public float GripOverride { get; set; } = 1f;

    public SurfaceProperties GetSurface(WheelCollider wheel, in WheelHit hit)
    {
        bool asphalt = IsAsphaltCollider(hit.collider);
        if (asphalt)
        {
            return new SurfaceProperties
            {
                forwardGripMultiplier = GripOverride,
                sidewaysGripMultiplier = GripOverride
            };
        }

        return new SurfaceProperties
        {
            forwardGripMultiplier = grassForwardGrip * GripOverride,
            sidewaysGripMultiplier = grassSidewaysGrip * GripOverride
        };
    }

    /// <summary>هل هذا المصادم سطحه إسفلت صالح للقيادة؟ (يستعمله أيضاً TrackBuilder.IsGrass).</summary>
    public static bool IsAsphaltCollider(Collider c)
    {
        if (c == null) return false;
        string n = c.name.ToUpperInvariant();
        return n.Contains("TARMAC") || n.Contains("PITLANE") || n.Contains("PIT_")
             || n.Contains("KERB") || n.Contains("ROAD");
    }
}
