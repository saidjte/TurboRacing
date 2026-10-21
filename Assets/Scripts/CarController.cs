using System.Collections.Generic;
using MotionCore.Vehicle.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// TurboRacing - v1.6.0 (محرك الفيزياء MotionCore Vehicle — الأصل المطلوب من المستخدم)
/// واجهة قيادة (facade) تُبقي واجهتنا العامة المعتمدة كما هي، وداخلها الفيزياء الحقيقية
/// من MotionCore VehicleControllerBase (WheelColliders + تعليق + نقل ثقل + توجيه موزون بالسرعة):
/// - سرعات فيزياء حقيقية: الموديلات 195-235 كم/س (معامل تحويل المواصفات 2.6×)
///   مع منحنى عزم أركيد قوي من الوقوف وثابت عند القصوى، وتوجيه 36° وقوفاً ← 9° قصوى
/// - قيادة يدوية كاملة (v1.2.0 كما هي): زر بنزين GAS للتسارع — لم تعد السيارة تتحرك
///   من تلقاء نفسها؛ بدون ضغط تدحرج طبيعي بفرملة خمول MotionCore (idleBrakeTorque)
/// - زر الفرامل: فرملة فعلية أثناء الحركة ثم رجوع تلقائي عند التوقف (منطق MotionCore)
/// - تماسك مرجعي من مصنع MotionCore (fwd 1.5 / side 1.9) + قوة ضغط تزيد مع السرعة
///   + مضاد انقلاب (anti-roll) — علاج الانزلاق من تصميم المحرك نفسه
/// - الإدخال: خاصية CurrentCarInput يقرؤها المتحكم قبل كل خطوة فيزياء —
///   سيارة اللاعب = أزرار اللمس، سيارة المنافس = أوامر AiDriver (سقف سرعة خارجي)
/// - إصلاح انقلاب ذاتي + عقاب العشب عبر مزوّد السطح (تصنيف الأرض من اسم المصادم)
/// - v1.6.0: الكراج = 8 سيارات Racing Cars Pack 1 الحقيقية حصراً (أُزيلت الإجرائية
///   بطلب المستخدم)، وموضع النموذج نسبةً لجذر السيارة (إصلاح الغرق تحت الحلبة)
/// - أزرار اللمس + تتبع الأصابع (v0.6.8) + أيقونات مرسومة مستقلة عن الخطوط (v1.2.0)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    // --- إدخال اللاعب ---
    // (v2.3.0) holdingX = النتيجة المُركَّبة كل إطار من ثلاث قنوات مستقلة:
    //   1) evtX  : مسار EventTrigger عبر EventSystem (كما كان تاريخياً)
    //   2) touchX: استقصاء مباشر لـ Input.touches على مستطيلات الأزرار —
    //      يتجاوز EventSystem كلياً فيعالج صمتاً أي انقطاع في الواجهة
    //      (حجب raycast / عدم تطابق pointerId / خلل InputModule)
    //   3) لوحة المفاتيح (محرر فقط)
    private bool holdingLeft;
    private bool holdingRight;
    private bool holdingBrake;
    private bool holdingGas;   // v1.2.0: القيادة اليدوية
    private bool evtLeft, evtRight, evtBrake, evtGas;
    private bool touchLeft, touchRight, touchBrake, touchGas;
    private float bindRetryAt;          // (v2.3.0) موعد إعادة محاولة الربط
    private int boundButtons;           // (v2.3.0) عدد الأزرار المربوطة فعلاً
    private readonly Dictionary<string, RectTransform> btnRects = new Dictionary<string, RectTransform>();

    // --- تتبع اللمس لكل زر (v0.6.8 كما هو) ---
    private readonly Dictionary<string, HashSet<int>> holdPointers = new Dictionary<string, HashSet<int>>();
    private readonly Dictionary<string, bool> holdEngaged = new Dictionary<string, bool>();
    private readonly Dictionary<string, System.Action<bool>> holdCallbacks = new Dictionary<string, System.Action<bool>>();

    // --- التغذية البصرية للأزرار ---
    private static readonly Color SteerBaseColor    = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color SteerPressedColor = new Color(1f, 0.72f, 0.15f, 0.80f);
    private static readonly Color GasBaseColor      = new Color(0.05f, 0.38f, 0.14f, 0.60f); // أخضر داكن
    private static readonly Color GasPressedColor   = new Color(0.15f, 0.85f, 0.30f, 0.92f); // أخضر ساطع
    private static readonly Color BrakeBaseColor    = new Color(0.62f, 0.10f, 0.10f, 0.55f);
    private static readonly Color BrakePressedColor = new Color(1f, 0.25f, 0.18f, 0.90f);

    private readonly Dictionary<string, Image> driveButtons = new Dictionary<string, Image>();
    private readonly Dictionary<string, Color> baseColors = new Dictionary<string, Color>();
    private readonly Dictionary<string, Color> pressedColors = new Dictionary<string, Color>();

    private Rigidbody rb;
    private MotionCoreVehicleController mc;   // v1.5.0: محرك الفيزياء MotionCore
    private MotionCoreSurface mcSurface;      // v1.5.0: مزوّد السطح (عقاب العشب/الحصى)
    private readonly WheelCollider[] wheels = new WheelCollider[4]; // FL FR RL RR

    // --- v1.5.0: مدخلات اللحظة (اللاعب من اللمس، المنافس من AiDriver) ---
    private CarInput playerInput;
    private CarInput aiInput;

    // --- v1.4.0: تعدد الموديلات + سيارات المنافسين ---
    public bool isAi;                 // سيارة منافسة: بلا أزرار لمسية وبلا صوت تصادم اللاعب
    private int forcedModel = -1;     // موديل مفروض (سيارات الذكاء الاصطناعي)
    private int builtModel = -1;      // الموديل المبني فعلياً (لكشف الحاجة لإعادة البناء)
    private bool driftAttached;       // مؤثرات الانزلاق تُركّب مرة واحدة عبر إعادة البناء
    private CarModels.CarSpec aiSpec;
    private float aiSkill = 1f;
    private float aiLane;
    private ParticleSystem smokeRL, smokeRR;
    private TrailRenderer skidRL, skidRR;

    // --- الضبط ---
    private float maxSpeedKmh;       // الحد الأقصى من الضبطة (كم/س)
    private float baseSteerAngle = 36f; // أقصى زاوية توجيه من الضبطة (قبل تخفيض السرعة)
    private float baseSidewaysStiffness = 1f;
    private float gripOverride = 1f;

    // --- النموذج البصري ---
    private Transform modelRoot;
    private MeshRenderer paintRenderer;
    private readonly Transform[] wheelMeshes = new Transform[4];
    private bool usingArcadeAsset;

    // --- إصلاح الانقلاب ---
    private float flipTimer;

    /// <summary>السرعة الحالية كم/س (من فيزياء MotionCore).</summary>
    public float SpeedKmh => mc != null ? mc.SpeedKph
                                        : (rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f);
    public bool IsBraking => holdingBrake && ForwardSpeed() > 0.5f;

    /// <summary>السرعة الحالية نسبةً إلى الحد الأقصى (0..1) — للصوت والآثار.</summary>
    public float Speed01 => maxSpeedKmh > 1f ? Mathf.Clamp01(SpeedKmh / maxSpeedKmh) : 0f;

    /// <summary>هل اللاعب يضغط على البنزين الآن؟ (مفيد للصوت/HUD)</summary>
    public bool IsAccelerating => holdingGas;

    /// <summary>(v2.2.0) حالة المدخلات الحية للطبقة التشخيصية — كل لقطة شاشة
    /// تصير تقريراً كاملاً: هل وصل ضغط الزر للفيزياء أم لا؟</summary>
    public bool DebugGasHeld => holdingGas;
    public bool DebugBrakeHeld => holdingBrake;
    public float DebugSteer => (holdingLeft ? -1f : 0f) + (holdingRight ? 1f : 0f);

    /// <summary>(v2.3.0) تشخيص القنوات: أي مسار يستلم الضغط فعلاً؟</summary>
    public bool DebugEventGas => evtGas;                    // مسار EventSystem
    public bool DebugTouchGas => touchGas;                  // مسار الاستقصاء المباشر
    public int DebugBoundButtons => boundButtons;           // الأزرار المربوطة n/4
    public int DebugTouchCount => Input.touchCount;         // عدد الأصابع الفيزيائي
    public int DebugGroundedWheels { get; private set; }    // عجلات ملامسة للأرض x/4

    /// <summary>(v2.8.0) قنوات تشخيص ديناميكا طبقة VPP-style عبر المتحكم:
    /// L = عامل الإطلاق (رامب القابض)، T = أدنى معامل جر (تراكيشن كونترول)،
    /// r = رامب ضغط الفرملة، P = سرعة الميلان الأمامي (راد/ث).</summary>
    public float DebugLaunchFactor => mc != null ? mc.DebugLaunchFactor : 1f;
    public float DebugTcFactor     => mc != null ? mc.DebugTcFactor : 1f;
    public float DebugBrakeRamp    => mc != null ? mc.DebugBrakeRamp : 0f;
    public float DebugPitchRate    => mc != null ? mc.DebugPitchRate : 0f;

    /// <summary>(v2.9.0) الترس الحالي + RPM المتخيل + قوى عارضات الرف الحية.</summary>
    public int   DebugGear      => mc != null ? mc.DebugGear : 1;
    public float DebugRpm01     => mc != null ? mc.DebugRpm01 : 0f;
    public float DebugAntiRollN => mc != null ? mc.DebugAntiRollN : 0f;
    /// <summary>مُصدِّر مادة الطلاء (يستخدمه SurfaceChecker للتلطيخ على العشب).</summary>
    public MeshRenderer PaintRenderer => paintRenderer;

    /// <summary>
    /// مدخلات الفيزياء الحالية — يقرؤها MotionCore قبل كل خطوة فيزياء:
    /// سيارة اللاعب = أزرار اللمس، سيارة المنافس = أوامر AiDriver.
    /// التجميد (قائمة/عد/نهاية) يصفّرها تلقائياً.
    /// </summary>
    public CarInput CurrentCarInput
    {
        get
        {
            if (rb != null && (rb.constraints & RigidbodyConstraints.FreezePosition) != 0)
                return default; // مجمّدة: لا مدخلات إطلاقاً

            if (isAi) return aiInput;

            playerInput = default;
            playerInput.Steer = (holdingLeft ? -1f : 0f) + (holdingRight ? 1f : 0f);
            if (holdingBrake) playerInput.Brake = 1f;       // فرامل ثم رجوع (منطق MotionCore)
            else if (holdingGas) playerInput.Throttle = 1f; // القيادة اليدوية
            return playerInput;
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // فيزياء سيارة حقيقية: جاذبية + دوران حر + قيم Prometeo المرجعية
        rb.mass = 950f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.None;

        // (v1.4.0) سيارات المنافسين تؤجّل البناء إلى Start: SetAi (الموديل المفروض)
        // يُستدعى بعد AddComponent مباشرة — البناء هنا كان سيستعمل موديل اللاعب بالخطأ
        if (!isAi) BuildCar();
    }

    void Start()
    {
        // سيارات المنافسين (v1.4.0): الموديل المفروض + مواصفاته + لونه — لا واجهة لمسية
        if (isAi)
        {
            BuildCar(); // forcedModel جاهز الآن (SetAi استُدعيت بعد AddComponent)
            ApplySpec(aiSpec, aiSkill);
            SetBodyColor(aiSpec.paint);
            return;
        }

        // ربط الأزرار اللمسية تلقائياً بالاسم
        // (v2.3.0) ربط الأحداث يقترح evtX فقط — الحقل المركّب holdingX
        // يُحسب كل إطار في SynthesizeInputs من القنوات الثلاث
        BindHoldButton("BtnLeft",  down => evtLeft  = down);
        BindHoldButton("BtnRight", down => evtRight = down);
        BindHoldButton("BtnBrake", down => evtBrake = down);
        BindHoldButton("BtnGas",   down => evtGas   = down);

        ConfigureSteerIcon("BtnLeft",  pointRight: false);
        ConfigureSteerIcon("BtnRight", pointRight: true);

        // البنزين والفرامل (v1.2.0): أيقونتان مرسومتان (مثلث لأعلى = تسارع، مربع = توقف)
        // بنفس استراتيجية التحصين ضد أعطال الخطوط — ظهور مضمون على كل الأجهزة
        ApplyHoldIcon("BtnGas",   UiKit.TriangleSpriteUp(), GasBaseColor,   GasPressedColor);
        ApplyHoldIcon("BtnBrake", UiKit.SquareSprite(),     BrakeBaseColor, BrakePressedColor);

        // إعدادات اللاعب: الضبط + اللون + تخطيط الأزرار
        GameSettings.EnsureLoaded();
        ApplyTuning(GameSettings.CarPreset, GameSettings.SteerSens);
        ApplyModelPaint();
        DriveButtonsLayout.Apply(this);
    }

    /// <summary>دهان السيارة وفق الإعدادات (v1.5.0): الفهرس 0 = دهان المصنع —
    /// كل الموديلات تحتفظ بمظهرها الأصلي (نسيج سيارات الحزمة الحقيقية أو لون الإجرائية).
    /// أي لون آخر يعيد تلوين أي موديل كما في الإصدارات السابقة.</summary>
    public void ApplyModelPaint()
    {
        int model = builtModel >= 0 ? builtModel : 0;
        if (GameSettings.CarColorIndex == 0)
            SetBodyColor(CarModels.Get(model).paint);
        else
            SetBodyColor(GameSettings.CurrentColor);
    }

    // ================= بناء السيارة (نموذج الكراج المختار + فيزياء MotionCore) =================

    void BuildCar()
    {
        // v1.4.0: الموديل من الكراج (اللاعب) أو مفروض (المنافسون)
        int model = forcedModel >= 0 ? forcedModel : GameSettings.SelectedCarModel;
        builtModel = model;
        CarModels.CarSpec spec = CarModels.Get(model);

        if (spec.packIndex < 0 && spec.bodyType < 0)
        {
            // ARCADE GT: النموذج الأصلي أولاً ثم الكلاسيكية الإجرائية
            usingArcadeAsset = TryBuildArcadeModel(out modelRoot);
            if (!usingArcadeAsset)
            {
                CarModel3D.Handles h = CarModel3D.Build(transform);
                paintRenderer = h.Paint;
                modelRoot = h.BodyPivot != null ? h.BodyPivot.root.gameObject.transform : transform;
                if (h.Wheels != null)
                {
                    for (int i = 0; i < 4 && i < h.Wheels.Length; i++)
                        wheelMeshes[i] = h.Wheels[i];
                }
            }
        }
        else
        {
            // v1.5.0: سيارات Racing Cars Pack 1 الحقيقية + موديلات الكراج الإجرائية
            // (CarModels.Build يقرر: packIndex >= 0 = حقيقي، وإلا إجرائي)
            CarModels.Handles mh = CarModels.Build(transform, spec);
            paintRenderer = mh.Paint;
            modelRoot = mh.Root;
            for (int i = 0; i < 4; i++) wheelMeshes[i] = mh.Wheels[i];
        }

        FindWheelMeshes(usingArcadeAsset);

        // مصادم الهيكل (ارتجالات الجدران + أحداث التصادم) — من حدود النموذج الفعلية
        Vector3 min, max;
        ComputeModelBounds(out min, out max);
        BoxCollider body = gameObject.GetComponent<BoxCollider>();
        if (body == null) body = gameObject.AddComponent<BoxCollider>();
        body.center = new Vector3(0f, (min.y + max.y) * 0.5f + 0.05f, (min.z + max.z) * 0.5f);
        body.size = new Vector3(max.x - min.x + 0.05f, Mathf.Max(0.6f, max.y - min.y), max.z - min.z);

        PhysicsMaterial shell = new PhysicsMaterial("CarShell");
        shell.staticFriction = 0.05f;
        shell.dynamicFriction = 0.05f;
        shell.bounciness = 0.15f;
        shell.frictionCombine = PhysicsMaterialCombine.Minimum;
        body.sharedMaterial = shell;

        // مصادمات العجلات الأربعة عند مواضع عقد العجلات الفعلية
        BuildWheelColliders();

        // v1.5.0: مزوّد السطح قبل المتحكم (أساس MotionCore يبحث عنه في Awake)
        mcSurface = gameObject.AddComponent<MotionCoreSurface>();

        // v1.5.0: متحكم MotionCore — يقرأ CurrentCarInput من هذه الواجهة كل خطوة فيزياء
        // (v1.4.0: يُعاد استخدامه عبر إعادة بناء الموديل بدل إضافته من جديد)
        if (mc == null) mc = gameObject.AddComponent<MotionCoreVehicleController>();
        mc.Facade = this;
        ConfigureMotionCore();

        AttachDriftEffects();
    }

    /// <summary>تهيئة محرك MotionCore (v1.5.0): محاور من عقد النموذج + كتلة +
    /// مركز كتلة منخفض + تماسك مرجعي + توجيه موزون بالسرعة. الضبط النهائي
    /// (سرعة/عزم/زوايا) يأتي لاحقاً من ApplyTuning/ApplySpec حسب الموديل.</summary>
    void ConfigureMotionCore()
    {
        if (mc == null) return;

        GameObject com = new GameObject("Center Of Mass");
        com.transform.SetParent(transform, false);
        com.transform.localPosition = new Vector3(0f, -0.22f, 0.05f);

        const float stiffnessF = 1.5f, stiffnessS = 1.9f; // مرجع MotionCore + صلابة جانبية أعلى

        VehicleAxle front = new VehicleAxle
        {
            leftWheel = wheels[0], rightWheel = wheels[1],
            leftVisual = wheelMeshes[0], rightVisual = wheelMeshes[1],
            steering = true, handbrake = false, antiRollForce = 7000f,
            cachedLeftForwardStiffness = stiffnessF, cachedLeftSideStiffness = stiffnessS,
            cachedRightForwardStiffness = stiffnessF, cachedRightSideStiffness = stiffnessS
        };
        VehicleAxle rear = new VehicleAxle
        {
            leftWheel = wheels[2], rightWheel = wheels[3],
            leftVisual = wheelMeshes[2], rightVisual = wheelMeshes[3],
            steering = false, handbrake = true, antiRollForce = 7000f,
            cachedLeftForwardStiffness = stiffnessF, cachedLeftSideStiffness = stiffnessS,
            cachedRightForwardStiffness = stiffnessF, cachedRightSideStiffness = stiffnessS
        };

        MotionCoreSetup setup = new MotionCoreSetup
        {
            CenterOfMass = com.transform,
            BodyMass = 1100f,
            Axles = new[] { front, rear },
            MaxMotorTorque = 4200f,
            MaxBrakeTorque = 4200f,
            IdleBrakeTorque = 240f,
            MaxSpeedKph = 230f,
            MaxReverseSpeedKph = 38f,
            SteerAngleZero = 36f,
            SteerAngleTop = 9f,
            SteerInRate = 240f,
            SteerReturnRate = 330f,
            // (v2.5.0) كان 95 = 10g بالتسارع المباشر — عند ~70كم/س يبلع مسافة التعليق
            // كاملة (0.3م) فيلتحم جسم السيارة بالأرض ويصدر ارتطامات وهمية عند كل
            // مفصل بلاطة. الميزانية الفيزيائية: هامش الانضغاط المتاح بعد الثقل
            // الساكن ≈ 9900 نيوتن → سقف آمن ≈ 9 م/ث² — اعتمدنا 12 مع رفع صلابة النوابض.
            // v2.9.0: 22 بدل 12 — قوة رفع مزدوجة تقريباً تُغرس السيارة بالسرعة
            // العالية وتجعل نقل الثقل محسوساً، دون تجفيف استجابة التوجيه
            Downforce = 22f
        };

        mc.Configure(setup);

        // ما فوّته Awake المبكر للمتحكم (قبل ضبط الحقول): نطبّقه يدوياً
        rb.mass = setup.BodyMass;
        if (setup.CenterOfMass != null)
            rb.centerOfMass = transform.InverseTransformPoint(setup.CenterOfMass.position);
        if (wheels[0] != null)
            wheels[0].ConfigureVehicleSubsteps(5f, 12, 15);
    }

    /// <summary>تطبيق السيارة المختارة من الكراج (v1.4.0): إعادة بناء النموذج البصري إن تغيّر.
    /// يُستدعى قبل بدء السباق (الفيزياء مجمّدة) فلا خطر على المحاكاة.</summary>
    public void ApplySelectedModel()
    {
        if (isAi) return;

        int model = GameSettings.SelectedCarModel;
        if (model == builtModel && modelRoot != null) return;

        // فصل النموذج القديم كلياً (فوراً من الهرم — Destroy مؤجل — حتى لا تلوث
        // حدودها القديمة حساب مصادم الهيكل الجديد)
        if (modelRoot != null)
        {
            Transform old = modelRoot;
            old.SetParent(null, false);
            Destroy(old.gameObject);
        }
        modelRoot = null;
        paintRenderer = null;
        for (int i = 0; i < 4; i++) wheelMeshes[i] = null;

        // عجلات السيارة الكلاسيكية الإجرائية أبناء مباشرة لجذر السيارة (لا تتبع modelRoot)
        string[] procWheels = { "WheelFL", "WheelFR", "WheelRL", "WheelRR" };
        foreach (string wn in procWheels)
        {
            Transform wt = transform.Find(wn);
            if (wt != null)
            {
                wt.SetParent(null, false);
                Destroy(wt.gameObject);
            }
        }

        // مصادمات العجلات تُعاد من مواضع النموذج الجديد
        Transform wc = transform.Find("WheelColliders");
        if (wc != null)
        {
            wc.SetParent(null, false);
            Destroy(wc.gameObject);
        }

        // مركز الكتلة يُعاد بناؤه أيضاً (جديد مع MotionCore v1.5.0)
        Transform com = transform.Find("Center Of Mass");
        if (com != null)
        {
            com.SetParent(null, false);
            Destroy(com.gameObject);
        }

        BuildCar();
        ApplyTuning(GameSettings.CarPreset, GameSettings.SteerSens);
        ApplyModelPaint();
        Debug.Log($"[CarController] تم تطبيق موديل الكراج: {CarModels.Get(model).name}");
    }

    /// <summary>تحميل نموذج ARCADE من الموارد (الأصول الخاصة) وتركيبه تحت الجذر.</summary>
    bool TryBuildArcadeModel(out Transform root)
    {
        root = null;
        GameObject prefab = Resources.Load<GameObject>("GameAssets/ArcadeCar/ARCADE - FREE Racing Car");
        if (prefab == null)
        {
            Debug.Log("[CarController] نموذج ARCADE غير موجود في الموارد — استخدام السيارة الإجرائية.");
            return false;
        }

        GameObject go = Instantiate(prefab, transform);
        go.name = "ArcadeCarModel";
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        // إسناد الخامات قسراً (لا نعتمد materialSearch): الطلاء في الخانة 0
        // والإضاءة (مصابيح) في الخانة الثانية لمشيد Body — حسب بريفاب الناشر
        ApplyArcadeMaterials(go.transform, 3); // النسخة الحمراء أساساً حتى يختار اللاعب لونه

        root = go.transform;
        return true;
    }

    /// <summary>ربط خامات الطلاء والإضاءة على كل رندررات النموذج (منهج البريفاب الأصلي).</summary>
    void ApplyArcadeMaterials(Transform model, int variant)
    {
        if (model == null) return;

        Material paint = Resources.Load<Material>(
            $"GameAssets/ArcadeCar/Materials/Color Variations/AFRC_Mat_Col{Mathf.Clamp(variant, 1, 5)}");
        Material emission = Resources.Load<Material>("GameAssets/ArcadeCar/Materials/AFRC_Emission");
        if (paint == null) return;

        Transform bodyT = FindDeep(model, "Body");

        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int s = 0; s < mats.Length; s++)
            {
                if (s == 0) mats[s] = paint;
                else if (emission != null) mats[s] = emission;
            }
            r.sharedMaterials = mats;
            if (bodyT != null && r.transform == bodyT && mats.Length > 1)
                paintRenderer = r as MeshRenderer; // مشيد الهيكل = مصدر طلاء للتلطيخ
        }
    }

    void FindWheelMeshes(bool arcade)
    {
        if (arcade)
        {
            wheelMeshes[0] = FindDeep(transform, "FrontLeftWheel");
            wheelMeshes[1] = FindDeep(transform, "FrontRightWheel");
            wheelMeshes[2] = FindDeep(transform, "RearLeftWheel");
            wheelMeshes[3] = FindDeep(transform, "RearRightWheel");

            if (paintRenderer == null)
            {
                Transform bodyT = FindDeep(transform, "Body");
                if (bodyT != null) paintRenderer = bodyT.GetComponent<MeshRenderer>();
            }
        }

        for (int i = 0; i < 4; i++)
        {
            if (wheelMeshes[i] == null)
                Debug.LogWarning($"[CarController] wheel node #{i} missing - using estimated position.");
        }

        // مواضع تقديرية متماثلة عند غياب عقد العجلات (سيارة إجرائية بلا عقد مسماة)
        fallbackPositions = new Vector3[]
        {
            new Vector3(-0.62f, 0.30f,  0.95f),
            new Vector3( 0.62f, 0.30f,  0.95f),
            new Vector3(-0.62f, 0.30f, -0.95f),
            new Vector3( 0.62f, 0.30f, -0.95f),
        };
    }

    private Vector3[] fallbackPositions;

    static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform r = FindDeep(parent.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    void ComputeModelBounds(out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        bool any = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            // (v2.5.0) استبعاد مجسّمات العجلات: إدراجها كان يجعل صندوق الهيكل
            // يلتف حول العجلات حتى مستوى ملامستها للأرض — أي بروز أرضي صغير
            // يضرب حافة الصندوق مباشرة = ارتطامات وهمية وصوت اصطدام عند كل سرعة
            if (IsWheelRenderer(r)) continue;

            Bounds b = r.bounds;
            Vector3 lo = transform.InverseTransformPoint(b.min);
            Vector3 hi = transform.InverseTransformPoint(b.max);
            min = Vector3.Min(min, lo); max = Vector3.Max(max, hi);
            any = true;
        }
        if (!any) { min = new Vector3(-0.6f, 0f, -1.3f); max = new Vector3(0.6f, 1f, 1.3f); }
        // (v2.5.0) 0.18م فوق الجذر (كان 0.08 والواقع أسفلها عند إدراج العجلات):
        // خلاصة الهيكل فقط — حتى مع انضغاط التعليق الكامل يبقى وهماً ≈ 20سم
        min.y = Mathf.Max(0.18f, min.y);
    }

    /// <summary>(v2.5.0) هل هذا الرندرر ضمن مجسّم عجلة (أو من أبنائه)؟</summary>
    bool IsWheelRenderer(Renderer r)
    {
        for (int i = 0; i < wheelMeshes.Length; i++)
        {
            Transform w = wheelMeshes[i];
            if (w == null) continue;
            for (Transform t = r.transform; t != null; t = t.parent)
            {
                if (t == w) return true;
                if (t == transform) break;
            }
        }
        return false;
    }

    void BuildWheelColliders()
    {
        Transform colRoot = new GameObject("WheelColliders").transform;
        colRoot.SetParent(transform, false);

        Vector3[] pos = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            pos[i] = wheelMeshes[i] != null
                ? transform.InverseTransformPoint(wheelMeshes[i].position)
                : fallbackPositions[i];
        }

        float radius = EstimateWheelRadius();

        string[] names = { "WheelCol_FL", "WheelCol_FR", "WheelCol_RL", "WheelCol_RR" };
        for (int i = 0; i < 4; i++)
        {
            GameObject wg = new GameObject(names[i]);
            wg.transform.SetParent(colRoot, false);
            wg.transform.localPosition = pos[i];

            WheelCollider wc = wg.AddComponent<WheelCollider>();
            wc.radius = radius;

            // قيم التعليق: (v2.5.0) نوابض أصلب (50000 ن/م) ولمّاصة مضبوطة (ζ≈0.7)
            // ليتحمل التعليق قوة الضغط الهوائية دون الالتحام بالأرض عند السرعة
            JointSpring spring = wc.suspensionSpring;
            spring.spring = 50000f;
            spring.damper = 5200f;
            spring.targetPosition = 0.5f;
            wc.suspensionSpring = spring;
            wc.suspensionDistance = 0.3f;
            wc.forceAppPointDistance = 0.02f;

            wc.mass = 20f;
            wc.wheelDampingRate = 0.25f;
            // تماسك مرفوع (v1.2.0): علاج الانزلاق الشديد في المنعطفات —
            // ذروة التماسك أبكر (extremumSlip أضيق) وبقوة أعلى، والذيل (asymptote)
            // يحتفظ بقوة قريبة من الذروة فلا يفقد الإطار تماسكه فور تجاوز حد الانزلاق
            wc.forwardFriction  = MakeFriction(0.32f, 1.05f, 0.82f, 0.98f, 1f);
            wc.sidewaysFriction = MakeFriction(0.22f, 1.05f, 0.72f, 0.96f, 1f);

            wheels[i] = wc;
        }

        // صلابة جانبية أساسية مرفوعة (v1.2.0: 1.8 بدل 1.0) — العشب يضاعفها عبر SetGripOverride
        baseSidewaysStiffness = 1.8f;
    }


    float EstimateWheelRadius()
    {
        foreach (Transform wm in wheelMeshes)
        {
            if (wm == null) continue;
            Renderer r = wm.GetComponentInChildren<Renderer>();
            if (r == null) continue;
            Bounds b = r.bounds;
            // محور العجلة أفقي جانبي (X): الامتداد الرأسي/الطولي = القطر
            return Mathf.Max(0.18f, (b.extents.y + b.extents.z) * 0.5f);
        }
        return 0.34f; // مرجع Prometeo
    }

    static WheelFrictionCurve MakeFriction(float exSlip, float exVal, float asySlip, float asyVal, float stiff)
    {
        WheelFrictionCurve f = new WheelFrictionCurve();
        f.extremumSlip = exSlip; f.extremumValue = exVal;
        f.asymptoteSlip = asySlip; f.asymptoteValue = asyVal;
        f.stiffness = stiff;
        return f;
    }

    /// <summary>دخان الانزلاق + آثار الإطارات من حزمة Prometeo عند العجلتين الخلفيتين.
    /// (v1.4.0: تُبنى مرة واحدة فقط — إعادة بناء الموديل لا تكررها، والمراجع تُربط من جديد)</summary>
    void AttachDriftEffects()
    {
        if (driftAttached) return;
        driftAttached = true;

        if (wheels[2] == null || wheels[3] == null) return;

        GameObject smoke = Resources.Load<GameObject>("GameAssets/Prometeo/Effects/TireSmoke_PS");
        GameObject skid  = Resources.Load<GameObject>("GameAssets/Prometeo/Effects/TireSkid");

        for (int i = 2; i < 4; i++)
        {
            Vector3 at = wheels[i].transform.localPosition;
            if (smoke != null)
            {
                GameObject s = Instantiate(smoke, transform);
                s.transform.localPosition = new Vector3(at.x, 0.15f, at.z);
                if (i == 2) smokeRL = s.GetComponent<ParticleSystem>();
                else        smokeRR = s.GetComponent<ParticleSystem>();
            }
            if (skid != null)
            {
                GameObject t = Instantiate(skid, transform);
                t.transform.localPosition = new Vector3(at.x, 0.06f, at.z);
                if (i == 2) skidRL = t.GetComponent<TrailRenderer>();
                else        skidRR = t.GetComponent<TrailRenderer>();
            }
        }
    }

    // ================= حلقة القيادة (v1.5.0: الإدخال يجري عبر CurrentCarInput) =================

    void Update()
    {
        // (v2.3.0) حلقة الإدخال المركّبة: إعادة ربط ناقص + استقصاء اللمس + تركيب القنوات
        TryBindPendingButtons();
        PollTouchFallback();
        SynthesizeInputs();

        // قسر تحرير الأزرار العالقة (v0.6.8)
        PruneStalePointers();
    }

    /// <summary>(v2.3.0) إعادة محاولة ربط أي زر لم يُعثر عليه عند Start
    /// (حماية من أي فرق في ترتيب الإنشاء بين الأجهزة).</summary>
    void TryBindPendingButtons()
    {
        if (pendingBinds.Count == 0) return;
        if (Time.unscaledTime < bindRetryAt) return;
        bindRetryAt = Time.unscaledTime + 0.5f;

        for (int i = pendingBinds.Count - 1; i >= 0; i--)
        {
            if (TryBindHoldButton(pendingBinds[i], pendingCallbacks[i]))
            {
                pendingBinds.RemoveAt(i);
                pendingCallbacks.RemoveAt(i);
            }
        }
    }

    readonly List<string> pendingBinds = new List<string>();
    readonly List<System.Action<bool>> pendingCallbacks = new List<System.Action<bool>>();

    /// <summary>(v2.3.0) استقصاء اللمس المباشر: قناة إدخال مستقلة كلياً عن
    /// EventSystem — كل إصبع فيزيائي يُختبر على مستطيلات الأزرار الأربعة.
    /// لا تتأثر بحجب raycast ولا بنظام أحداث الواجهة إطلاقاً.</summary>
    void PollTouchFallback()
    {
        touchLeft = touchRight = touchBrake = touchGas = false;

        int n = Input.touchCount;
        for (int i = 0; i < n; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) continue;

            Vector2 sp = t.position;
            if (HitButton("BtnGas",   sp)) touchGas   = true;
            if (HitButton("BtnBrake", sp)) touchBrake = true;
            if (HitButton("BtnLeft",  sp)) touchLeft  = true;
            if (HitButton("BtnRight", sp)) touchRight = true;
        }
    }

    /// <summary>هل النقطة (بإحداثيات الشاشة) داخل مستطيل الزر؟
    /// كانفاس ScreenSpaceOverlay الكاميرا المرجعية = null.</summary>
    bool HitButton(string buttonName, Vector2 screenPos)
    {
        RectTransform rt = CachedButtonRect(buttonName);
        return rt != null &&
               RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null);
    }

    RectTransform CachedButtonRect(string buttonName)
    {
        RectTransform rt;
        if (btnRects.TryGetValue(buttonName, out rt) && rt != null) return rt;

        GameObject go = GameObject.Find(buttonName);
        rt = go != null ? go.GetComponent<RectTransform>() : null;
        btnRects[buttonName] = rt;
        return rt;
    }

    /// <summary>(v2.3.0) تركيب المدخلات النهائي من القنوات الثلاث + تغذية بصرية موحّدة.</summary>
    void SynthesizeInputs()
    {
        bool keyLeft = false, keyRight = false, keyBrake = false, keyGas = false;
#if UNITY_EDITOR || UNITY_STANDALONE
        keyLeft  = Input.GetKey(KeyCode.LeftArrow)  || Input.GetKey(KeyCode.A);
        keyRight = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
        keyBrake = Input.GetKey(KeyCode.Space)      || Input.GetKey(KeyCode.S);
        keyGas   = Input.GetKey(KeyCode.UpArrow)    || Input.GetKey(KeyCode.W);
#endif

        holdingLeft  = evtLeft  || touchLeft  || keyLeft;
        holdingRight = evtRight || touchRight || keyRight;
        holdingBrake = evtBrake || touchBrake || keyBrake;
        holdingGas   = evtGas   || touchGas   || keyGas;

        // التغذية البصرية تتبع الحالة المركّبة (أي قناة تضغط = إضاءة)
        SetPressedVisual("BtnLeft",  holdingLeft);
        SetPressedVisual("BtnRight", holdingRight);
        SetPressedVisual("BtnBrake", holdingBrake);
        SetPressedVisual("BtnGas",   holdingGas);
    }

    void FixedUpdate()
    {
        // الإدخال يصل للمتحكم عبر خاصية CurrentCarInput (يقرؤها MotionCore كل خطوة).
        // هذه الحلقة للمؤثرات والإنقاذ الذاتي فقط + (v2.3.0) تشخيص التماس.
        UpdateDriftEffects();
        CheckFlipRecovery();
        CheckFallRecovery();
        UpdateGroundedWheels();
    }

    float ForwardSpeed()
    {
        Vector3 vel = rb.linearVelocity; vel.y = 0f;
        return Vector3.Dot(vel, transform.forward);
    }

    // ================= المؤثرات (حالة الانزلاق من MotionCore) =================

    void UpdateDriftEffects()
    {
        if (mc == null) return;

        bool drift = mc.IsDrifting;
        if (drift == lastDrift) return;
        lastDrift = drift;

        if (drift)
        {
            if (smokeRL != null) smokeRL.Play();
            if (smokeRR != null) smokeRR.Play();
        }
        else
        {
            if (smokeRL != null) smokeRL.Stop();
            if (smokeRR != null) smokeRR.Stop();
        }

        if (skidRL != null) skidRL.emitting = drift;
        if (skidRR != null) skidRR.emitting = drift;
    }

    private bool lastDrift;

    // ================= إصلاح الانقلاب الذاتي =================

    void CheckFlipRecovery()
    {
        Vector3 up = transform.up;
        if (up.y < 0.35f)
        {
            flipTimer += Time.fixedDeltaTime;
            if (flipTimer > 1.5f)
            {
                float yaw = transform.eulerAngles.y;
                Vector3 p = transform.position;
                // v1.7.0: الارتفاع من سطح الحلبة الفعلي (البيضاوية المقعّرة المرتفعة
                // تحتاج أكثر من 1.2م الثابتة — كانت تُعاد داخل الميل)
                TrackBuilder tb = TrackBuilder.Instance;
                float ground = tb != null ? tb.SurfaceHeightAt(p.x, p.z) : float.MinValue;
                p.y = (ground > -50f ? ground : 1f) + 0.8f;
                rb.position = p;
                rb.rotation = Quaternion.Euler(0f, yaw, 0f);
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                flipTimer = 0f;
            }
        }
        else
        {
            flipTimer = 0f;
        }
    }

    // ================= إنقاذ السقوط (v1.7.0) =================

    private float fallTimer;
    private float sunkenTimer;   // (v2.1.0) استمرار الغوص الضحل
    private float offTrackTimer; // (v2.1.0) استمرار البعد عن خط السباق

    /// <summary>(v1.8.0) إزاحة جانبية ثابتة لكل سيارة عند الإنقاذ — سيارتان
    /// ساقطتان في نفس اللحظة لا تُعادان لنفس النقطة فتتكدسا وتنفجرا فيزيائياً.</summary>
    private float RescueLateral
    {
        get
        {
            if (rescueLateral == 0f)
                rescueLateral = ((GetInstanceID() & 7) - 3.5f) * 1.3f; // -4.55..+4.55م
            return rescueLateral;
        }
    }
    private float rescueLateral;

    /// <summary>
    /// (v1.7.0) علاج «يغوص الكل في الأرض»: سيارة تنتهي تحت سطح الحلبة
    /// (ثقبت شبكة رقيقة عند سرعة عالية أو طارت خارج حدود الأرض) تُعاد
    /// لأقرب نقطة على خط السباق بارتفاع سطحها الحقيقي واتجاهه —
    /// يمنع الغوص للأبد ويشمل اللاعب والمنافسين معاً (نفس المكوّن على الكل).
    /// (v2.1.0) فحص كل 0.25 ثانية + غوص ضحل (>2.5م) لـ 0.4 ثانية = إنقاذ
    /// فوري + إنقاذ «التائه»: بعدٌ عن خط السباق >35م لستّ ثوانٍ = إرجاع.
    /// </summary>
    void CheckFallRecovery()
    {
        fallTimer += Time.fixedDeltaTime;
        if (fallTimer < 0.25f) return; // فحص كل ربع ثانية (v2.1.0: كان 0.5)
        float dt = fallTimer;
        fallTimer = 0f;

        RaceFlow rf = RaceFlow.Instance;
        if (rf == null || (rf.State != RaceState.Racing && rf.State != RaceState.Finished)) return;

        TrackBuilder tb = TrackBuilder.Instance;
        if (tb == null) return;

        Vector3 p = transform.position;

        // 1) تحت العالم كلياً = إنقاذ فوري
        bool fell = p.y < -12f;
        if (!fell)
        {
            // 2) تحت السطح بأكثر من 2.5م لـ 0.4 ثانية متواصلة = غوص مؤكد
            float ground = tb.SurfaceHeightAt(p.x, p.z);
            if (ground > -50f && p.y < ground - 2.5f)
                sunkenTimer += dt;
            else
                sunkenTimer = 0f;
            fell = sunkenTimer >= 0.4f;
        }
        else sunkenTimer = 0f;

        // 3) التائه بعيداً عن المسار (v2.1.0): عشب بعيد لا يُنقذ قبل 6 ثوانٍ
        if (!fell && IsStranded(tb, dt)) fell = true;

        if (!fell) return;

        if (tb.GetRescuePose(p.x, p.z, RescueLateral, out Vector3 pos, out float yaw))
        {
            rb.position = pos;
            rb.rotation = Quaternion.Euler(0f, yaw, 0f);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            flipTimer = 0f;
            sunkenTimer = 0f;
            offTrackTimer = 0f;
            Debug.Log($"[CarController] إنقاذ: أُعيدت السيارة إلى خط السباق عند {pos}");

            if (!isAi && RaceHUD.Instance != null)
                RaceHUD.Instance.ShowToast("تم إرجاع السيارة إلى المسار");
        }
    }

    /// <summary>(v2.1.0) هل السيارة تائسة؟ بعد >35م عن أقرب نقطة خط سباق
    /// لمدة 6 ثوانٍ متواصلة = تائه (قرب العشب المباشر يبقى حراً).</summary>
    bool IsStranded(TrackBuilder tb, float dt)
    {
        Vector3[] line = tb.RacingLine;
        if (line == null || line.Length < 8) return false;

        Vector3 p = transform.position;
        float best = float.MaxValue;
        for (int i = 0; i < line.Length; i++)
        {
            float dx = line[i].x - p.x, dz = line[i].z - p.z;
            float d = dx * dx + dz * dz;
            if (d < best) best = d;
        }

        if (best <= 35f * 35f)
        {
            offTrackTimer = 0f;
            return false;
        }

        offTrackTimer += dt;
        return offTrackTimer >= 6f;
    }

    /// <summary>مؤثر التصادم: صوت ارتطام متدرّج القوة + اهتزاز كاميرا (كما في v0.7.0).
    /// (v1.4.0: سيارات المنافسين لا تُصدر صوت تصادم الواجهة)
    /// (v2.5.0) المقياس الآن = سرعة الاقتراب على عمود الاتصال لا طول السرعة:
    /// الاحتكاك الأمامي عند 100كم/س كان يعطي 27م/ث «ارتطام» وهمياً عند كل
    /// مفصل بلاطة/درجة صغيرة = صوت تحطم واهتزاز مستمر — بينما الاحتكاك
    /// موازٍ للسطح فمركبته على العمود ≈ صفر. الاصطدام الحقيقي بالجدران
    /// يبقى صاخباً كما هو.</summary>
    void OnCollisionEnter(Collision collision)
    {
        if (isAi) return;

        ContactPoint cp = collision.GetContact(0);
        float impact = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, -cp.normal));
        if (impact < 2f) return;

        float impact01 = Mathf.Clamp01((impact - 2f) / 8f);

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayCrash(impact01);

        if (CameraFollow.Instance != null)
            CameraFollow.Instance.Shake(Mathf.Lerp(0.08f, 0.3f, impact01));
    }

    // ================= ربط الأزرار تلقائي (كما هو v0.6.8) =================

    void BindHoldButton(string buttonName, System.Action<bool> onStateChanged)
    {
        // (v2.3.0) الفشل لم يعد نهائياً: يُسجَّل في قائمة الانتظار وتُعاد المحاولة كل 0.5ث
        if (!TryBindHoldButton(buttonName, onStateChanged))
        {
            if (!pendingBinds.Contains(buttonName))
            {
                pendingBinds.Add(buttonName);
                pendingCallbacks.Add(onStateChanged);
            }
            Debug.LogWarning($"[CarController] زر '{buttonName}' غير موجود الآن — أُجّل ربطه (إعادة محاولة كل 0.5ث).");
        }
    }

    /// <summary>(v2.3.0) فصل منطق الربط عن البحث ليصبح قابلاً لإعادة المحاولة.</summary>
    bool TryBindHoldButton(string buttonName, System.Action<bool> onStateChanged)
    {
        GameObject buttonGO = GameObject.Find(buttonName);
        if (buttonGO == null) return false;

        holdPointers[buttonName] = new HashSet<int>();
        holdEngaged[buttonName] = false;
        holdCallbacks[buttonName] = onStateChanged;

        EventTrigger trigger = buttonGO.GetComponent<EventTrigger>();
        if (trigger == null) trigger = buttonGO.AddComponent<EventTrigger>();

        AddTriggerEntry(trigger, EventTriggerType.PointerDown, ed => OnHoldPointer(buttonName, ed, true));
        AddTriggerEntry(trigger, EventTriggerType.PointerUp,   ed => OnHoldPointer(buttonName, ed, false));
        AddTriggerEntry(trigger, EventTriggerType.PointerExit, ed => OnHoldPointer(buttonName, ed, false));
        AddTriggerEntry(trigger, EventTriggerType.PointerEnter, ed => OnHoldPointer(buttonName, ed, true));

        boundButtons = holdCallbacks.Count;
        return true;
    }

    /// <summary>(v2.3.0) إحصاء العجلات الملامسة للأرض — يكشف في اللقطة إن كانت
    /// السيارة واقفة على مصادم صلب أم معلّقة/غارقة داخل الهندسة.</summary>
    void UpdateGroundedWheels()
    {
        int g = 0;
        for (int i = 0; i < 4; i++)
            if (wheels[i] != null && wheels[i].isGrounded) g++;
        DebugGroundedWheels = g;
    }

    void OnHoldPointer(string buttonName, BaseEventData eventData, bool pressed)
    {
        HashSet<int> ids;
        if (!holdPointers.TryGetValue(buttonName, out ids) || ids == null) return;

        PointerEventData ped = eventData as PointerEventData;
        int pointerId = ped != null ? ped.pointerId : -1;

        if (pressed) ids.Add(pointerId);
        else ids.Remove(pointerId);

        RefreshHold(buttonName);
    }

    void RefreshHold(string buttonName)
    {
        HashSet<int> ids;
        if (!holdPointers.TryGetValue(buttonName, out ids) || ids == null) return;

        bool pressed = false;
        foreach (int id in ids)
        {
            if (IsPointerPhysicallyDown(id))
            {
                pressed = true;
                break;
            }
        }

        bool current;
        if (!holdEngaged.TryGetValue(buttonName, out current)) current = false;
        if (current == pressed) return;

        holdEngaged[buttonName] = pressed;

        System.Action<bool> callback;
        if (holdCallbacks.TryGetValue(buttonName, out callback) && callback != null)
            callback(pressed);

        SetPressedVisual(buttonName, pressed);
    }

    bool IsPointerPhysicallyDown(int pointerId)
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (pointerId < 0) return Input.GetMouseButton(0);
#endif
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.fingerId == pointerId &&
                t.phase != TouchPhase.Ended &&
                t.phase != TouchPhase.Canceled)
                return true;
        }
        return false;
    }

    void PruneStalePointers()
    {
        foreach (KeyValuePair<string, HashSet<int>> kv in holdPointers)
        {
            if (kv.Value.Count == 0) continue;
            if (kv.Value.RemoveWhere(id => !IsPointerPhysicallyDown(id)) > 0)
                RefreshHold(kv.Key);
        }
    }

    void AddTriggerEntry(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(ed => action(ed));
        trigger.triggers.Add(entry);
    }

    /// <summary>(v0.6.6) أيقونة سهم مرسومة لأزرار التوجيه — تغليف على المُوحّد العام.</summary>
    void ConfigureSteerIcon(string buttonName, bool pointRight)
    {
        ApplyHoldIcon(buttonName, UiKit.TriangleSprite(pointRight), SteerBaseColor, SteerPressedColor);
    }

    /// <summary>
    /// (v1.2.0) توحيد عام لأيقونات أزرار القيادة: ألوان أساس/ضغط + تعطيل النص القديم
    /// + صورة أيقونة مركزية مولّدة برمجياً — مستقلة كلياً عن الخطوط فتظهر دائماً على كل الأجهزة.
    /// </summary>
    void ApplyHoldIcon(string buttonName, Sprite icon, Color baseColor, Color pressedColor)
    {
        GameObject buttonGO = GameObject.Find(buttonName);
        if (buttonGO == null) return;

        Image image = buttonGO.GetComponent<Image>();
        if (image != null)
        {
            image.color = baseColor;
            driveButtons[buttonName] = image;
            baseColors[buttonName] = baseColor;
            pressedColors[buttonName] = pressedColor;
        }

        // تعطيل نص الزر القديم (كان يعتمد على الخط)
        Text oldText = buttonGO.GetComponentInChildren<Text>(true);
        if (oldText != null) oldText.gameObject.SetActive(false);

        // الأيقونة (صورة مركزية لا تعترض اللمس)
        GameObject iconGO = new GameObject("Icon",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGO.transform.SetParent(buttonGO.transform, false);

        RectTransform irt = iconGO.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.5f, 0.5f);
        irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = Vector2.zero;

        RectTransform brt = buttonGO.GetComponent<RectTransform>();
        float side = 110f;
        if (brt != null && brt.rect.width > 1f)
            side = Mathf.Min(brt.rect.width, brt.rect.height) * 0.38f;
        irt.sizeDelta = new Vector2(side, side);

        Image img = iconGO.GetComponent<Image>();
        img.sprite = icon;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    void SetPressedVisual(string buttonName, bool pressed)
    {
        Image image;
        if (!driveButtons.TryGetValue(buttonName, out image) || image == null) return;

        Color target;
        if (pressed) pressedColors.TryGetValue(buttonName, out target);
        else baseColors.TryGetValue(buttonName, out target);

        image.color = target;
    }

    /// <summary>تصفير كل المدخلات والألوان (بدء/إعادة سباق أو عودة للقائمة).</summary>
    public void ResetInput()
    {
        holdingLeft = holdingRight = holdingBrake = holdingGas = false;
        evtLeft = evtRight = evtBrake = evtGas = false;         // (v2.3.0)
        touchLeft = touchRight = touchBrake = touchGas = false; // (v2.3.0)
        aiInput = default;
        mc.ExternalSpeedCapKph = 0f;

        foreach (KeyValuePair<string, HashSet<int>> kv in holdPointers)
            kv.Value.Clear();
        holdEngaged.Clear();

        foreach (KeyValuePair<string, Image> kv in driveButtons)
        {
            Color baseColor;
            if (kv.Value != null && baseColors.TryGetValue(kv.Key, out baseColor))
                kv.Value.color = baseColor;
        }
    }

    // ================= واجهة للسكربتات الأخرى =================

    /// <summary>تخفيض حد السرعة (العشب). مثال: 0.45 = 45% من السرعة القصوى.</summary>
    public void SetSpeedLimit(float multiplier)
    {
        if (mc != null && maxSpeedKmh > 1f)
            mc.ApplySpeedLimitKph(maxSpeedKmh * Mathf.Clamp01(multiplier));
    }

    /// <summary>إرجاع حد السرعة الطبيعي (عند العودة إلى المسار).</summary>
    public void RestoreSpeedLimit()
    {
        if (mc != null) mc.RestoreTunedSpeed();
    }

    // ================= الضبط القابل للتخصيص (v0.8.0 → v1.4.0) =================

    /// <summary>تهيئة سيارة منافس (v1.4.0): موديل + مهارة + حارة — تُستدعى قبل Start.</summary>
    public void SetAi(CarModels.CarSpec spec, float skillMul, float lane)
    {
        isAi = true;
        forcedModel = spec.id;
        aiSpec = spec;
        aiSkill = Mathf.Clamp(skillMul, 0.6f, 1f);
        aiLane = Mathf.Clamp(lane, -5f, 5f);
    }

    /// <summary>
    /// ضبط الأداء (v1.5.0 على MotionCore): سرعة الموديل تُرفع بمعامل تحويل
    /// المواصفات (سرعات فيزياء حقيقية 195-235 كم/س)، ومعامل الضبط
    /// (STOCK/SPORT/TURBO) يعمل كمُضاعِف (1.0 / 1.07 / 1.14).
    /// </summary>
    public void ApplyTuning(int preset, float steerSens)
    {
        CarModels.CarSpec spec = CarModels.Get(builtModel >= 0 ? builtModel : 0);

        float trim  = preset == 1 ? 1.07f : (preset == 2 ? 1.14f : 1f);
        float pMax  = spec.topSpeed * CarModels.MotionCoreSpeedScale * trim; // كم/س حقيقية
        float pTorque = 2400f + spec.accel * 260f;                           // نيوتن.متر
        float pAngle = spec.steerAngle;

        maxSpeedKmh = pMax;
        baseSteerAngle = pAngle;

        if (mc != null)
        {
            mc.ConfigurePartial(new MotionCoreSetup
            {
                MaxMotorTorque = pTorque,
                MaxSpeedKph = pMax
            });
            mc.ApplySteerSensitivity(steerSens);
            RestoreSpeedLimit();
        }
    }

    /// <summary>ضبط سيارة منافس من مواصفات الموديل + سقف مهارة (v1.5.0).</summary>
    public void ApplySpec(CarModels.CarSpec spec, float skillMul)
    {
        maxSpeedKmh = spec.topSpeed * CarModels.MotionCoreSpeedScale * Mathf.Clamp(skillMul, 0.6f, 1f);
        baseSteerAngle = spec.steerAngle;

        if (mc != null)
        {
            mc.ConfigurePartial(new MotionCoreSetup
            {
                MaxMotorTorque = 2400f + spec.accel * 260f,
                MaxSpeedKph = maxSpeedKmh
            });
            RestoreSpeedLimit();
        }
    }

    // ================= أوامر قيادة سيارات المنافسين (v1.5.0 على MotionCore) =================

    /// <summary>بنزين المنافس مع سقف سرعة للمهارة الحالية.</summary>
    public void AiThrottle(float speedCapKmh)
    {
        aiInput.Brake = 0f;
        aiInput.Throttle = 1f;
        if (mc != null)
            mc.ExternalSpeedCapKph = Mathf.Max(8f, speedCapKmh);
    }

    /// <summary>فرملة المنافس (استباقية قبل المنعطفات).</summary>
    public void AiBrake()
    {
        aiInput.Throttle = 0f;
        aiInput.Brake = 1f;
    }

    /// <summary>رجوع المنافس (إنقاذ العلوق) — الفرامل المستمرة عند التوقف = رجوع في MotionCore.</summary>
    public void AiReverse()
    {
        aiInput.Throttle = 0f;
        aiInput.Brake = 1f;
    }

    /// <summary>حياد المنافس (خارج السباق): تدحرج طبيعي حتى التوقف (فرملة الخمول).</summary>
    public void AiNeutral()
    {
        aiInput.Throttle = 0f;
        aiInput.Brake = 0f;
        if (mc != null) mc.ExternalSpeedCapKph = 0f;
    }

    /// <summary>توجيه المنافس: -1 يسار، +1 يمين، 0 استقامة (الأساس يوزّن الزاوية بالسرعة).</summary>
    public void AiSteer(int dir)
    {
        aiInput.Steer = Mathf.Clamp(dir, -1f, 1f);
    }

    public void SetGripOverride(float multiplier)
    {
        gripOverride = Mathf.Clamp(multiplier, 0.1f, 2f);
        if (mcSurface != null) mcSurface.GripOverride = gripOverride;
    }

    /// <summary>
    /// لون السيارة: مع نموذج ARCADE نختار أقرب دهان مصنعي من النسخ الخمسة (نسيج حقيقي)،
    /// ومع السيارة الإجرائية نلوّن مادة الهيكل مباشرة كما في v0.9.0.
    /// </summary>
    public void SetBodyColor(Color color)
    {
        if (usingArcadeAsset && modelRoot != null)
        {
            // تبديل كامل لخامة الطلاء إلى أقرب دهان مصنعي لاختيار اللاعب
            ApplyArcadeMaterials(modelRoot, PlayerColorToVariant(color));
        }
        else if (paintRenderer != null)
        {
            paintRenderer.material.color = color;
        }

        if (TryGetComponent(out SurfaceChecker surface))
            surface.RefreshBaseColor();
    }

    /// <summary>خريطة ألوان اللاعب الثمانية → دهانات ARCADE الخمسة (Col1..Col5).</summary>
    static int PlayerColorToVariant(Color playerColor)
    {
        float h, s, v;
        Color.RGBToHSV(playerColor, out h, out s, out v);

        if (s < 0.25f) return 4;                          // أبيض/رمادي → Col4 (أقل تشبعاً)

        float hue = h * 360f;
        float[] hueCols = { 43f, 227f, 2f, 36f, 277f };   // Col1..Col5 (قياس الأنسجة)
        float best = float.MaxValue; int variant = 1;
        for (int i = 0; i < hueCols.Length; i++)
        {
            float d = Mathf.Abs(Mathf.DeltaAngle(hue, hueCols[i]));
            if (d < best) { best = d; variant = i + 1; }
        }
        return variant;
    }

    /// <summary>شفافية أزرار القيادة من الإعدادات.</summary>
    public void ApplyButtonOpacity(float opacityMul)
    {
        float op = Mathf.Clamp01(opacityMul);
        UpdateBaseColor("BtnLeft",  new Color(0f, 0f, 0f, 0.35f * op));
        UpdateBaseColor("BtnRight", new Color(0f, 0f, 0f, 0.35f * op));
        UpdateBaseColor("BtnGas",   new Color(0.05f, 0.38f, 0.14f, 0.60f * op));
        UpdateBaseColor("BtnBrake", new Color(0.62f, 0.10f, 0.10f, 0.55f * op));
    }

    void UpdateBaseColor(string buttonName, Color c)
    {
        baseColors[buttonName] = c;

        bool engaged;
        if (!holdEngaged.TryGetValue(buttonName, out engaged)) engaged = false;
        if (engaged) return;

        Image image;
        if (driveButtons.TryGetValue(buttonName, out image) && image != null)
            image.color = c;
    }
}
