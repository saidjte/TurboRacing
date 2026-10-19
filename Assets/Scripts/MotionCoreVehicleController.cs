using System.Reflection;
using MotionCore.Vehicle.Core;
using UnityEngine;

/// <summary>
/// TurboRacing v1.3.0 — متحكم السيارة على محرك MotionCore Vehicle Physics.
/// فئة مشتقة من VehicleControllerBase (الأساس المرخّص من حزمة MotionCore):
/// - يجرّ مدخلات اللمس من واجهة CarController المعتمدة ويمررها للفيزياء
///   داخل FixedUpdate قبل تنفيذ الأساس (ضمان ترتيب صارم للإدخال)
/// - يضبط حقول الأساس الخاصة وقت التشغيل عبر انعكاس (الأساس يعرّفها
///   [SerializeField] private — ولا نعدّل مصدر الأصل إطلاقاً)
/// - منحنى عزم أركيد (قوة تتناقص قرب السرعة القصوى) + سرعة قصوى عالية
///   بطلب المستخدم (220 كم/س) + توجيه موزون بالسرعة من الأساس (علاج الانزلاق)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MotionCoreVehicleController : VehicleControllerBase
{
    // --- حقول الأساس الخاصة (تُضبط عبر انعكاس مرة واحدة عند التهيئة) ---
    static readonly FieldInfo F_centerOfMass   = Field("centerOfMass");
    static readonly FieldInfo F_bodyMass       = Field("bodyMass");
    static readonly FieldInfo F_axles          = Field("axles");
    static readonly FieldInfo F_drivetrain     = Field("drivetrain");
    static readonly FieldInfo F_maxMotorTorque = Field("maxMotorTorque");
    static readonly FieldInfo F_maxBrakeTorque = Field("maxBrakeTorque");
    static readonly FieldInfo F_reverseScale   = Field("reverseTorqueScale");
    static readonly FieldInfo F_handbrakeTq    = Field("handbrakeTorque");
    static readonly FieldInfo F_idleBrake      = Field("idleBrakeTorque");
    static readonly FieldInfo F_maxSpeed       = Field("maxSpeedKph");
    static readonly FieldInfo F_maxRevSpeed    = Field("maxReverseSpeedKph");
    static readonly FieldInfo F_steerZero      = Field("steerAngleAtZeroSpeed");
    static readonly FieldInfo F_steerTop       = Field("steerAngleAtTopSpeed");
    static readonly FieldInfo F_steerInRate    = Field("steerInputRateDegPerSec");
    static readonly FieldInfo F_steerRetRate   = Field("steerReturnRateDegPerSec");
    static readonly FieldInfo F_handbrakeGrip  = Field("handbrakeGripFactor");
    static readonly FieldInfo F_downforce      = Field("downforce");

    static FieldInfo Field(string name) =>
        typeof(VehicleControllerBase).GetField(name,
            BindingFlags.NonPublic | BindingFlags.Instance);

    /// <summary>الحد الأقصى المضبوط (كم/س) قبل أي عقاب عشب — يقرأه Speed01 من الواجهة.</summary>
    public float TunedMaxSpeedKph { get; private set; }

    /// <summary>المدخلات الحالية (تقرأها الفيزياء من الواجهة كل خطوة).</summary>
    CarInput ReadFromFacade()
    {
        CarController f = Facade;
        return f != null ? f.CurrentCarInput : default;
    }

    internal CarController Facade { get; set; }

    protected override void Awake()
    {
        // تهيئة الأساس أولاً (rb + interpolation + تعريف مزوّد السطح)
        base.Awake();

        // الواجهة (CarController) تبني النموذج والعجلات ثم تستدعي Configure(...)
        // قبل أول خطوة فيزياء — انظر CarController.BuildMotionCore().
        if (Facade == null) Facade = GetComponent<CarController>();
    }

    void FixedUpdate()
    {
        // الإدخال يُقرأ من الواجهة ويُثبَّت قبل فيزياء الأساس مباشرة
        SetInput(ReadFromFacade());
        base.FixedUpdate();
    }

    // ================= الضبط عبر الانعكاس =================

    public void Configure(MotionCoreSetup s)
    {
        if (F_centerOfMass != null && s.CenterOfMass != null)
            F_centerOfMass.SetValue(this, s.CenterOfMass);
        SetF(F_bodyMass, s.BodyMass);
        SetF(F_drivetrain, (int)DrivetrainLayout.RearWheelDrive);
        SetF(F_maxMotorTorque, s.MaxMotorTorque);
        SetF(F_maxBrakeTorque, s.MaxBrakeTorque);
        SetF(F_reverseScale, 0.45f);
        SetF(F_handbrakeTq, 4000f);
        SetF(F_idleBrake, s.IdleBrakeTorque);
        SetF(F_maxSpeed, s.MaxSpeedKph);
        SetF(F_maxRevSpeed, s.MaxReverseSpeedKph);
        SetF(F_steerZero, s.SteerAngleZero);
        SetF(F_steerTop, s.SteerAngleTop);
        SetF(F_steerInRate, s.SteerInRate);
        SetF(F_steerRetRate, s.SteerReturnRate);
        SetF(F_handbrakeGrip, 0.6f);
        SetF(F_downforce, s.Downforce);

        if (F_axles != null && s.Axles != null)
            F_axles.SetValue(this, s.Axles);

        TunedMaxSpeedKph = s.MaxSpeedKph;
    }

    void SetF(FieldInfo f, object v)
    {
        if (f != null) f.SetValue(this, v);
    }

    /// <summary>
    /// تحديث جزئي (ضبط قوالب القيادة): يغيّر العزم والسرعة القصوى فقط
    /// ولا يمس المحاور ولا مركز الكتلة ولا زوايا التوجيه.
    /// </summary>
    public void ConfigurePartial(MotionCoreSetup s)
    {
        if (s == null) return;
        if (s.MaxMotorTorque > 0f) SetF(F_maxMotorTorque, s.MaxMotorTorque);
        if (s.MaxSpeedKph > 1f)
        {
            SetF(F_maxSpeed, s.MaxSpeedKph);
            TunedMaxSpeedKph = s.MaxSpeedKph;
        }
    }

    /// <summary>حد السرعة الحالي (عقاب العشب يخفضه مؤقتاً عبر الواجهة).</summary>
    public void ApplySpeedLimitKph(float kph)
    {
        SetF(F_maxSpeed, Mathf.Max(8f, kph));
    }

    public void RestoreTunedSpeed()
    {
        SetF(F_maxSpeed, TunedMaxSpeedKph);
    }

    /// <summary>ضبط زاوية التوجيه من حساسية اللاعب (0.7..1.4).</summary>
    public void ApplySteerSensitivity(float sens)
    {
        sens = Mathf.Clamp(sens, 0.5f, 2f);
        SetF(F_steerZero, 34f * sens);
        SetF(F_steerTop, 9f * sens);
    }

    // ================= نقاط امتداد الأركيد =================

    /// <summary>
    /// سقف سرعة خارجي مؤقت (كم/س) — يستخدمه ذكاء المنافسين (AiThrottle).
    /// 0 = بلا سقف. يُفحص قبل تطبيق العزم فقط (لا يمس حد السرعة المضبوط).
    /// </summary>
    public float ExternalSpeedCapKph { get; set; }

    /// <summary>
    /// منحنى عزم أركيد: عزم كامل حتى 45% من السرعة القصوى ثم ينخفض
    /// خطياً حتى 35% عند القصوى — تسارع قوي من الوقوف وثبات عالٍ عند السرعة.
    /// السقف الخارجي (المنافسون) يقطع العزم فوقه.
    /// </summary>
    protected override float EvaluateMotorTorque(float throttle, float normalizedSpeed)
    {
        if (ExternalSpeedCapKph > 1f && SpeedKph >= ExternalSpeedCapKph)
            return 0f;

        float falloff;
        if (normalizedSpeed < 0.45f) falloff = 1f;
        else falloff = Mathf.Lerp(1f, 0.35f, (normalizedSpeed - 0.45f) / 0.55f);
        return throttle * falloff;
    }

    /// <summary>عتبة انزلاق مرتفعة قليلاً: دخان/آثار عند انزلاق حقيقي فقط.</summary>
    protected override float DriftSlipThreshold => 0.55f;
}

/// <summary>حزمة تهيئة متحكم MotionCore (تُملأ من CarController).</summary>
public sealed class MotionCoreSetup
{
    public Transform CenterOfMass;
    public float BodyMass = 1150f;
    public VehicleAxle[] Axles;
    public float MaxMotorTorque = 3600f;
    public float MaxBrakeTorque = 4200f;
    public float IdleBrakeTorque = 240f;
    public float MaxSpeedKph = 220f;
    public float MaxReverseSpeedKph = 40f;
    public float SteerAngleZero = 34f;
    public float SteerAngleTop = 9f;
    public float SteerInRate = 240f;
    public float SteerReturnRate = 330f;
    public float Downforce = 95f;
}
