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
///
/// v2.8.0 — طبقة ديناميكا بمعايير VPP (تقنيات موثّقة علناً في توثيق
/// Vehicle Physics Pro، مطبّقة على محركنا المرخّص — VPP CE نفسه ممنوع
/// في بناءات أندرويد بترخيصه فطبقنا هندسته على أساسنا):
/// 1) Launch Control (معادل تدنّي القابض): رامب عزم ناعم من الوقوف بدل
///    الضربة اللحظية التي ترفع المقدمة وتصدم التعليق (علاج «الانطلاق»).
/// 2) Traction Control: قص العزم عند انزلاق أمامي مرتفع على العجلات
///    المدفوعة (الجر = Fz × μ محدود، والعزم الزائد يُهدر على تدوير العجلات).
/// 3) ABS مبسّط + رامب ضغط فرامل: لا انغلاق عجلات (يفقد التوجيه والثبات)
///    وبداية فرملة ناعمة (علاج «الغوص» القاسي).
/// 4) مثبط ميلان أمامي (معادل VPP AdvancedDamper): مقاومة سرعة الميلان
///    حول المحور الجانبي فقط — يقتل اهتزاز القبوة دون تجفيف التعليق.
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

    // ================= v2.8.0 — ثوابت طبقة الديناميكا VPP-style =================

    // --- Launch Control ---
    /// <summary>مدة رامب الإطلاق من الوقوف (ث) — معادل تدنّي القابض في VPP.</summary>
    const float LaunchRampTime = 0.45f;
    /// <summary>عزم بداية الرامب كنسبة من العزم الكامل.</summary>
    const float LaunchStartFraction = 0.38f;
    /// <summary>خروج نافذة الإطلاق عند هذه النسبة من السرعة القصوى.</summary>
    const float LaunchExitSpeed01 = 0.12f;

    // --- Traction Control ---
    /// <summary>انزلاق أمامي يبدأ عنده قص العزم (ذروة منحنى الإطارات ≈ 0.32).</summary>
    const float TcSlipOnset = 0.35f;
    /// <summary>أدنى معامل عزم يسمح به القص (نحافظ على دفع قوي دائماً).</summary>
    const float TcMinFactor = 0.35f;

    // --- ABS مبسّط ---
    /// <summary>انزلاق فرملة سالب تحت هذا الحد = عجلة منغلقة.</summary>
    const float AbsSlipOnset = -0.40f;
    /// <summary>أدنى معامل فرملة بعد القص.</summary>
    const float AbsMinFactor = 0.45f;

    // --- رامب ضغط الفرامل ---
    const float BrakeRampTime = 0.18f;      // صعود الضغط (بداية ناعمة)
    const float BrakeReleaseTime = 0.10f;   // تفريغ أسرع عند التخلي
    const float BrakeStartFraction = 0.40f; // ضغط اللحظة الأولى
    /// <summary>أدنى مستوى يُعد فرملة خدمة (فوقه = فرملة رئيسية لا خمول 240).</summary>
    const float ServiceBrakeMinTorque = 300f;

    // --- مثبط الميلان ---
    /// <summary>تسارع زاوي مقاوم لكل وحدة سرعة ميلان (ForceMode.Acceleration).</summary>
    const float PitchDampAccel = 4.5f;
    /// <summary>سقف التسارع المقاوم — لا نتصلع مع الفيزياء الطبيعية للهبوط.</summary>
    const float PitchDampMax = 12f;

    // --- حالة الطبقة ---
    float launchTimer;                    // زمن داخل نافذة الإطلاق (ث)
    float brakeRamp;                      // رامب ضغط الفرامل 0..1 (تشخيص)
    float lastLaunchFactor = 1f;          // آخر عامل إطلاق مطبق (تشخيص)
    float lastTcFactor = 1f;              // أدنى معامل جر حالي (تشخيص)
    float lastPitchRate;                  // آخر سرعة ميلان (تشخيص راد/ث)
    /// <summary>معاملات لكل عجلة: [i*2]=يسرى، [i*2+1]=يمنى (تهيّأ على 1).</summary>
    readonly float[] tcFactor  = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
    readonly float[] absFactor = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };

    // --- قنوات تشخيص حية (تعروض في سطر الشاشة عبر CarController) ---
    public float DebugLaunchFactor => lastLaunchFactor;
    public float DebugTcFactor      => lastTcFactor;
    public float DebugBrakeRamp     => brakeRamp;
    public float DebugPitchRate     => lastPitchRate;

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

        // v2.8.0 — طبقة الديناميكا تعمل بعد الأساس مباشرة وقبل خطوة المحاكاة:
        // تعديلات العزم/الفرملة هنا تسبق حساب الفيزياء فتصير حيّة في نفس الخطوة
        ApplyTractionControl();
        ApplyAbsBrakes();
        ApplyPitchDamping();
    }

    // ================= v2.8.0 — طبقة الديناميكا VPP-style =================

    /// <summary>هل المحور i مستقبِل عزم في الضبطة الحالية (RWD لدينا دائماً
    /// لكن نعمّم كما يفعل الأساس).</summary>
    bool IsDrivenAxle(int i) =>
        Drivetrain == DrivetrainLayout.AllWheelDrive
        || (Drivetrain == DrivetrainLayout.FrontWheelDrive
            ? i == 0
            : i == Axles.Length - 1);

    /// <summary>
    /// Traction Control (VPP-grade): العجلة المدفوعة المنزلقة أمامياً فوق ذروة
    /// منحنى الإطارات تفقد قوة الدفع ويُهدر العزم الزائد على تدوير العجلات
    /// (الجر = Fz × μ محدود فيزيائياً). نقص معامل العزم تدريجياً حتى تعود
    /// العجلة لحافة الجر ثم استرجاع أبطأ — حلقة تغذية راجعة ذاتية التكيّف
    /// مع أي سطح: العشب يخفض الجر فيقص العزم تلقائياً دون أي قوائم مخصصة.
    /// </summary>
    void ApplyTractionControl()
    {
        bool accelerating = InputState.Throttle > 0.05f;
        float dt = Time.fixedDeltaTime;
        float minSeen = 1f;

        for (int i = 0; i < Axles.Length; i++)
        {
            if (!IsDrivenAxle(i)) continue;

            minSeen = Mathf.Min(minSeen,
                TickGripFactor(Axles[i].leftWheel,  ref tcFactor[i * 2],     accelerating, TcSlipOnset, TcMinFactor, dt));
            minSeen = Mathf.Min(minSeen,
                TickGripFactor(Axles[i].rightWheel, ref tcFactor[i * 2 + 1], accelerating, TcSlipOnset, TcMinFactor, dt));
        }

        lastTcFactor = minSeen;
    }

    /// <summary>حلقة معامل جر واحدة: تنخفض 1→min خلال 0.12 ثانية عند تجاوز
    /// الانزلاق وترجع min→1 خلال 0.5 ثانية عند الهدوء، وتضرب عزم المحرك.</summary>
    float TickGripFactor(WheelCollider wheel, ref float factor, bool active,
        float slipOnset, float minFactor, float dt)
    {
        if (factor <= 0f) factor = 1f;
        if (wheel == null) return 1f;

        bool overloaded = false;
        if (active && wheel.GetGroundHit(out WheelHit hit))
            overloaded = hit.forwardSlip > slipOnset;

        if (overloaded) factor = Mathf.Max(factor - dt / 0.12f, minFactor);
        else            factor = Mathf.Min(factor + dt / 0.50f, 1f);

        wheel.motorTorque *= factor;
        return factor;
    }

    /// <summary>
    /// ABS مبسّط + رامب ضغط فرامل (VPP-grade): العجلة المنغلقة تفقد التوجيه
    /// والثبات — نخفف فرملتها تدريجياً حتى تعود للحافة (تكيف ذاتي مع السطح)،
    /// والرامب يجعل ضغط الفرملة يتراكم خلال 0.18 ثانية بدل الضربة اللحظية
    /// التي تقلب الثقل إلى المقدمة بعنف (النصف الأول من علاج «الغوص»).
    /// فرملة الخمول (≤ 300 نيوتن.متر) ومسار الرجوع لا تُلمسان — الخدمية فقط.
    /// </summary>
    void ApplyAbsBrakes()
    {
        float brake = InputState.Brake;
        bool brakingForward = brake > 0.05f && ForwardSpeed > 0.5f;
        float dt = Time.fixedDeltaTime;

        // رامب الضغط: صعود خلال BrakeRampTime أثناء الفرملة وتفريغ أسرع عند التخلي
        brakeRamp = Mathf.MoveTowards(brakeRamp, brakingForward ? 1f : 0f,
            dt / (brakingForward ? BrakeRampTime : BrakeReleaseTime));
        float rampScale = Mathf.Lerp(BrakeStartFraction, 1f, brakeRamp);

        for (int i = 0; i < Axles.Length; i++)
        {
            TickBrake(Axles[i].leftWheel,  ref absFactor[i * 2],     brakingForward, rampScale, dt);
            TickBrake(Axles[i].rightWheel, ref absFactor[i * 2 + 1], brakingForward, rampScale, dt);
        }
    }

    void TickBrake(WheelCollider wheel, ref float factor, bool brakingForward, float rampScale, float dt)
    {
        if (factor <= 0f) factor = 1f;
        if (wheel == null || wheel.brakeTorque <= 0f) return;

        float bt = wheel.brakeTorque;

        // (1) رامب الضغط على الفرملة الخدمية فقط (فرملة الخمول تحت 300 لا تُرامب)
        if (brakingForward && bt > ServiceBrakeMinTorque)
            bt *= rampScale;

        // (2) ABS: انزلاق فرملة سالب عميق = انغلاق → تخفيف تدريجي واسترجاع أبطأ
        if (brakingForward && wheel.GetGroundHit(out WheelHit hit) && hit.forwardSlip < AbsSlipOnset)
            factor = Mathf.Max(factor - dt / 0.15f, AbsMinFactor);
        else
            factor = Mathf.Min(factor + dt / 0.40f, 1f);

        wheel.brakeTorque = bt * factor;
    }

    /// <summary>
    /// مثبط الميلان (معادل VPP AdvancedDamper): مقاومة تتناسب مع سرعة الدوران
    /// حول المحور الجانبي فقط (القبوة لأعلى/أسفل) — يخمد اهتزاز الميلان عند
    /// الفرملة القوية والإطلاق والهبوط دون تجفيف الحركة الرأسية للتعليق،
    /// وسقف المقاومة يضمن عدم التدخل في الديناميكا الطبيعية الكبيرة.
    /// </summary>
    void ApplyPitchDamping()
    {
        if (!IsGrounded || Body == null)
        {
            lastPitchRate = 0f;
            return;
        }

        Vector3 axis = Body.transform.right;   // الدوران حوله = ميلان القبوة
        lastPitchRate = Vector3.Dot(Body.angularVelocity, axis);
        float accel = Mathf.Clamp(-lastPitchRate * PitchDampAccel, -PitchDampMax, PitchDampMax);
        Body.AddTorque(axis * accel, ForceMode.Acceleration);
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
    /// منحنى عزم أركيد (v2.4.0 إصلاح جذري «زر البنزين لا يعمل»):
    /// عقد الأساس (VehicleControllerBase.ApplyDrive) يتعامل مع قيمة هذه الدالة
    /// كعزم كلي بالنيوتن.متر (فقط) — يقسمه على العجلات المدفوعة دون أي ضرب
    /// في MaxMotorTorque. الإرجاع السابق كان معاملاً بلا وحدة (0..1) فيصبح
    /// العزم الفعلي ≤ 1 نيوتن.متر ≈ صفر — بينما فرملة الرجوع تتجاوز هذه الدالة
    /// وتستخدم maxMotorTorque مباشرة، ولهذا كانت السيارة «تتحرك للخلف فقط»
    /// منذ v1.5.0 وبدت أزرار القيادة ميتة في كل الإصدارات التالية.
    /// الإصلاح: ضرب المعامل في MaxMotorTorque (نيوتن.متر الحقيقية).
    /// المنحنى: عزم كامل حتى 45% من السرعة القصوى ثم ينخفض خطياً حتى 35% —
    /// تسارع قوي من الوقوف وثبات عالٍ عند السرعة، والسقف الخارجي (المنافسون)
    /// يقطع العزم فوقه.
    ///
    /// v2.8.0 Launch Control: داخل نافذة السرعة المنخفضة يبني العزم عبر
    /// منحنى SmoothStep من 38% إلى 100% خلال 0.45 ثانية — نفس وظيفة تدنّي
    /// القابض في VPP (عزم متصاعد لا ضربة لحظية). العداد يتقدم فقط أثناء
    /// البنزين الفعلي (الأساس يستدعي هذه الدالة عند throttle > 0.05 حصراً)
    /// ويُصفَّر عند الخروج من النافذة. معامل Traction Control يضرب العزم
    /// لاحقاً لكل عجلة في ApplyTractionControl (بعد خطوة الأساس).
    /// </summary>
    protected override float EvaluateMotorTorque(float throttle, float normalizedSpeed)
    {
        if (ExternalSpeedCapKph > 1f && SpeedKph >= ExternalSpeedCapKph)
            return 0f;

        float falloff;
        if (normalizedSpeed < 0.45f) falloff = 1f;
        else falloff = Mathf.Lerp(1f, 0.35f, (normalizedSpeed - 0.45f) / 0.55f);

        float launchFactor = 1f;
        if (normalizedSpeed < LaunchExitSpeed01)
        {
            launchTimer += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(launchTimer / LaunchRampTime);
            launchFactor = Mathf.Lerp(LaunchStartFraction, 1f, t * t * (3f - 2f * t));
        }
        else
        {
            launchTimer = 0f;
        }

        lastLaunchFactor = launchFactor;
        return throttle * falloff * launchFactor * MaxMotorTorque;
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
