using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// TurboRacing - v1.3.0 (محرك PROMETEO: Car Controller)
/// واجهة قيادة (facade) تُبقي واجهتنا العامة المعتمدة كما هي، وداخلها الفيزياء الحقيقية
/// من PrometeoCarController (WheelColliders + تعليق + نقل ثقل + انزلاق):
/// - سرعات أعلى (v1.3.0): STOCK 72 / SPORT 80 / TURBO 88 كم/س بدل 50/56/61
///   مع فرملة أقوى ورجوع أسرع، وتوجيه يتقلّص زاويته مع السرعة لثبات أعلى
///   (أسلوب المضامير الأركيد في مراجعات مثل Multiversal Vehicle Controller)
/// - قيادة يدوية كاملة (v1.2.0): زر بنزين GAS للتسارع — لم تعد السيارة تتحرك من تلقاء نفسها،
///   وبدون ضغط تتدحرج وتتباطأ بأسلوب Prometeo الأصلي (DecelerateCar بإيقاعه المرجعي 0.1ث)
/// - زر الفرامل: فرملة فعلية أثناء الحركة ثم رجوع بطيء عند التوقف تقريباً (أسلوب الموبايل)
/// - تماسك إطارات مرفوع (v1.2.0): صلابة جانبية 1.8 ومنحنى أضيق = توجيه مثبت بلا انزلاق شديد
/// - التوجيه: TurnLeft/TurnRight/ResetSteeringAngle من Prometeo مباشرة
/// - وضع externalControl في نسخة Prometeo (تعديل مصرّح به ملاحظة المؤلف) يعطّل مدخلات
///   الكيبورد المدمجة ويُبقي حساب السرعة/الانزلاق وتحريك عجلات النموذج
/// - إصلاح انقلاب ذاتي: لو انقلبت السيارة (فيزياء حقيقية) تعود واقفة بعد لحظة
/// - عقاب العشب عبر SetSpeedLimit (حد السرعة) + SetGripOverride (تماسك WheelColliders)
/// - لون السيارة: اختيار أقرب دهان من دهانات ARCADE الخمسة (نسيج حقيقي كامل)
/// - أزرار اللمس + تتبع الأصابع (v0.6.8) + أيقونات مرسومة مستقلة عن الخطوط (v1.2.0)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    // --- إدخال اللاعب ---
    private bool holdingLeft;
    private bool holdingRight;
    private bool holdingBrake;
    private bool holdingGas;   // v1.2.0: القيادة اليدوية

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
    private PrometeoCarController pcc;
    private readonly WheelCollider[] wheels = new WheelCollider[4]; // FL FR RL RR

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

    // --- التدحرج الطبيعي (v1.2.0): أسلوب Prometeo الأصلي لتباطؤ السيارة بلا مدخلات ---
    private bool coasting;
    const float CoastInvokeInterval = 0.1f; // الإيقاع المرجعي في Prometeo نفسه

    // خصائص للقراءة من سكربتات أخرى (الواجهة المعتمدة كما هي)
    public float SpeedKmh => pcc != null ? Mathf.Abs(pcc.carSpeed)
                                         : (rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f);
    public bool IsBraking => holdingBrake && ForwardSpeed() > 0.5f;

    /// <summary>السرعة الحالية نسبةً إلى الحد الأقصى (0..1) — للصوت والآثار.</summary>
    public float Speed01 => maxSpeedKmh > 1f ? Mathf.Clamp01(SpeedKmh / maxSpeedKmh) : 0f;

    /// <summary>هل اللاعب يضغط على البنزين الآن؟ (مفيد للصوت/HUD)</summary>
    public bool IsAccelerating => holdingGas;
    /// <summary>مُصدِّر مادة الطلاء (يستخدمه SurfaceChecker للتلطيخ على العشب).</summary>
    public MeshRenderer PaintRenderer => paintRenderer;

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

        BuildCar();
    }

    void Start()
    {
        // ربط الأزرار اللمسية تلقائياً بالاسم
        BindHoldButton("BtnLeft",  down => holdingLeft  = down);
        BindHoldButton("BtnRight", down => holdingRight = down);
        BindHoldButton("BtnBrake", down => holdingBrake = down);
        BindHoldButton("BtnGas",   down => holdingGas   = down);

        ConfigureSteerIcon("BtnLeft",  pointRight: false);
        ConfigureSteerIcon("BtnRight", pointRight: true);

        // البنزين والفرامل (v1.2.0): أيقونتان مرسومتان (مثلث لأعلى = تسارع، مربع = توقف)
        // بنفس استراتيجية التحصين ضد أعطال الخطوط — ظهور مضمون على كل الأجهزة
        ApplyHoldIcon("BtnGas",   UiKit.TriangleSpriteUp(), GasBaseColor,   GasPressedColor);
        ApplyHoldIcon("BtnBrake", UiKit.SquareSprite(),     BrakeBaseColor, BrakePressedColor);

        // إعدادات اللاعب: الضبط + اللون + تخطيط الأزرار
        GameSettings.EnsureLoaded();
        ApplyTuning(GameSettings.CarPreset, GameSettings.SteerSens);
        SetBodyColor(GameSettings.CurrentColor);
        DriveButtonsLayout.Apply(this);
    }

    // ================= بناء السيارة (نموذج ARCADE + فيزياء Prometeo) =================

    void BuildCar()
    {
        usingArcadeAsset = TryBuildArcadeModel(out modelRoot);
        if (!usingArcadeAsset)
        {
            // الاحتياطي: السيارة الإجرائية (CarModel3D) — نفس جودة اللعب بلا الأصل الخارجي
            CarModel3D.Handles h = CarModel3D.Build(transform);
            paintRenderer = h.Paint;
            modelRoot = h.BodyPivot != null ? h.BodyPivot.root.gameObject.transform : transform;
            if (h.Wheels != null)
            {
                for (int i = 0; i < 4 && i < h.Wheels.Length; i++)
                    wheelMeshes[i] = h.Wheels[i]; // محاور العجلات الإجرائية = مواضع العجلات
            }
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

        // متحكم Prometeo: وضع التحكم الخارجي (ندرس المدخلات ونمرر الأوامر)
        pcc = gameObject.AddComponent<PrometeoCarController>();
        pcc.externalControl = true;
        pcc.useTouchControls = false;
        pcc.useUI = false;
        pcc.useSounds = false; // نظام الصوت المعتمد (SoundManager) يبقى مصدر الصوت
        pcc.useEffects = true;

        pcc.frontLeftMesh  = wheelMeshes[0] != null ? wheelMeshes[0].gameObject : null;
        pcc.frontRightMesh = wheelMeshes[1] != null ? wheelMeshes[1].gameObject : null;
        pcc.rearLeftMesh   = wheelMeshes[2] != null ? wheelMeshes[2].gameObject : null;
        pcc.rearRightMesh  = wheelMeshes[3] != null ? wheelMeshes[3].gameObject : null;
        pcc.frontLeftCollider  = wheels[0];
        pcc.frontRightCollider = wheels[1];
        pcc.rearLeftCollider   = wheels[2];
        pcc.rearRightCollider  = wheels[3];
        pcc.bodyMassCenter = new Vector3(0f, 0.15f, 0f);

        AttachDriftEffects();
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
            Bounds b = r.bounds;
            Vector3 lo = transform.InverseTransformPoint(b.min);
            Vector3 hi = transform.InverseTransformPoint(b.max);
            min = Vector3.Min(min, lo); max = Vector3.Max(max, hi);
            any = true;
        }
        if (!any) { min = new Vector3(-0.6f, 0f, -1.3f); max = new Vector3(0.6f, 1f, 1.3f); }
        min.y = Mathf.Max(0.08f, min.y); // المصادم يبدأ فوق سطح العجلات
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

            // قيم التعليق المرجعية من بريفاب Prometeo (950 كغم)
            JointSpring spring = wc.suspensionSpring;
            spring.spring = 35000f;
            spring.damper = 4500f;
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

    /// <summary>دخان الانزلاق + آثار الإطارات من حزمة Prometeo عند العجلتين الخلفيتين.</summary>
    void AttachDriftEffects()
    {
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
                if (i == 2) pcc.RLWParticleSystem = s.GetComponent<ParticleSystem>();
                else        pcc.RRWParticleSystem = s.GetComponent<ParticleSystem>();
            }
            if (skid != null)
            {
                GameObject t = Instantiate(skid, transform);
                t.transform.localPosition = new Vector3(at.x, 0.06f, at.z);
                if (i == 2) pcc.RLWTireSkid = t.GetComponent<TrailRenderer>();
                else        pcc.RRWTireSkid = t.GetComponent<TrailRenderer>();
            }
        }
    }

    // ================= حلقة القيادة (توزيع الأوامر على Prometeo) =================

    void Update()
    {
        // احتياطي للتجربة داخل المحرر فقط
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetKeyDown(KeyCode.LeftArrow)  || Input.GetKeyDown(KeyCode.A)) holdingLeft  = true;
        if (Input.GetKeyUp(KeyCode.LeftArrow)    || Input.GetKeyUp(KeyCode.A))   holdingLeft  = false;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) holdingRight = true;
        if (Input.GetKeyUp(KeyCode.RightArrow)   || Input.GetKeyUp(KeyCode.D))   holdingRight = false;
        if (Input.GetKeyDown(KeyCode.Space)      || Input.GetKeyDown(KeyCode.S)) holdingBrake = true;
        if (Input.GetKeyUp(KeyCode.Space)        || Input.GetKeyUp(KeyCode.S))   holdingBrake = false;
        if (Input.GetKeyDown(KeyCode.UpArrow)    || Input.GetKeyDown(KeyCode.W)) holdingGas   = true;
        if (Input.GetKeyUp(KeyCode.UpArrow)      || Input.GetKeyUp(KeyCode.W))   holdingGas   = false;
#endif

        // قسر تحرير الأزرار العالقة (v0.6.8)
        PruneStalePointers();
    }

    void FixedUpdate()
    {
        if (pcc == null) return;

        bool frozen = (rb.constraints & RigidbodyConstraints.FreezePosition) != 0;

        if (frozen)
        {
            // أثناء العد التنازلي/القائمة: لا عزم ولا تدحرج (الـ Rigidbody مجمّد أصلاً)
            SetCoasting(false);
            pcc.ThrottleOff();
            return;
        }

        // --- التوجيه (v1.3.0: زاوية توجيه تتقلص مع السرعة — ثبات أعلى بسرعات جديدة) ---
        // عند التوقف كامل الزاوية للمناورة؛ وعند السرعة القصوى 60% منها فقط
        // فلا تنقلب السيارة ولا تنزلق من حدة التوجيه، مع بقاء التماسك الجانبي المرفوع.
        pcc.maxSteeringAngle = Mathf.RoundToInt(
            Mathf.Lerp(baseSteerAngle, baseSteerAngle * 0.60f, Speed01));

        if (holdingLeft && !holdingRight) pcc.TurnLeft();
        else if (holdingRight && !holdingLeft) pcc.TurnRight();
        else pcc.ResetSteeringAngle();

        // --- القيادة اليدوية (v1.2.0): بنزين/فرامل/تدحرج طبيعي ---
        float fwd = ForwardSpeed();
        if (holdingBrake)
        {
            // الفرامل تتفوق على البنزين دائماً (أمان)
            SetCoasting(false);
            if (fwd > 1.2f)
            {
                pcc.ThrottleOff();
                pcc.Brakes();
            }
            else
            {
                pcc.GoReverse();
            }
        }
        else if (holdingGas)
        {
            // القيادة اليدوية: التسارع فقط أثناء الضغط على زر البنزين
            SetCoasting(false);
            pcc.GoForward();
        }
        else
        {
            // بلا مدخلات: تدحرج طبيعي — تباطؤ Prometeo المرجعي (DecelerateCar كل 0.1ث)
            // يوقف السيارة كلياً تحت 0.25 م/ث ويلغي نفسه ذاتياً
            SetCoasting(true);
        }

        UpdateDriftEffects();
        CheckFlipRecovery();
    }

    /// <summary>
    /// تبديل وضع التدحرج الطبيعي (v1.2.0): تكرار DecelerateCar بإيقاع Prometeo المرجعي
    /// (كل 0.1ث — لا أسرع) + تصفير الفرامل العالقة وعدم العزم فوراً عند الدخول في الوضع.
    /// </summary>
    void SetCoasting(bool on)
    {
        if (coasting == on) return;
        coasting = on;
        if (pcc == null) return;

        if (on)
        {
            pcc.ThrottleOff();
            for (int i = 0; i < 4; i++)
                if (wheels[i] != null) wheels[i].brakeTorque = 0f; // لا فرامل عالقة أثناء التدحرج
            pcc.InvokeRepeating("DecelerateCar", 0f, CoastInvokeInterval);
        }
        else
        {
            pcc.CancelInvoke("DecelerateCar");
        }
    }

    float ForwardSpeed()
    {
        Vector3 vel = rb.linearVelocity; vel.y = 0f;
        return Vector3.Dot(vel, transform.forward);
    }

    // ================= المؤثرات (مزامنة نبضات Prometeo) =================

    void UpdateDriftEffects()
    {
        // pcc.isDrifting يُحسب داخل GoForward/GoReverse عبر localVelocityX
        // (يعمل لأن externalControl يُبقي حساب بيانات السيارة شغالاً في Update)
        pcc.DriftCarPS();
    }

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
                p.y = 1.2f;
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

    /// <summary>مؤثر التصادم: صوت ارتطام متدرّج القوة + اهتزاز كاميرا (كما في v0.7.0).</summary>
    void OnCollisionEnter(Collision collision)
    {
        float impact = collision.relativeVelocity.magnitude;
        if (impact < 2.5f) return;

        float impact01 = Mathf.Clamp01((impact - 2.5f) / 10f);

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayCrash(impact01);

        if (CameraFollow.Instance != null)
            CameraFollow.Instance.Shake(Mathf.Lerp(0.08f, 0.3f, impact01));
    }

    // ================= ربط الأزرار تلقائي (كما هو v0.6.8) =================

    void BindHoldButton(string buttonName, System.Action<bool> onStateChanged)
    {
        GameObject buttonGO = GameObject.Find(buttonName);

        if (buttonGO == null)
        {
            Debug.LogWarning($"[CarController] لم أجد زراً باسم '{buttonName}' في المشهد - تخطيت ربطه.");
            return;
        }

        holdPointers[buttonName] = new HashSet<int>();
        holdEngaged[buttonName] = false;
        holdCallbacks[buttonName] = onStateChanged;

        EventTrigger trigger = buttonGO.GetComponent<EventTrigger>();
        if (trigger == null) trigger = buttonGO.AddComponent<EventTrigger>();

        AddTriggerEntry(trigger, EventTriggerType.PointerDown, ed => OnHoldPointer(buttonName, ed, true));
        AddTriggerEntry(trigger, EventTriggerType.PointerUp,   ed => OnHoldPointer(buttonName, ed, false));
        AddTriggerEntry(trigger, EventTriggerType.PointerExit, ed => OnHoldPointer(buttonName, ed, false));
        AddTriggerEntry(trigger, EventTriggerType.PointerEnter, ed => OnHoldPointer(buttonName, ed, true));
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
        SetCoasting(false);

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
        if (pcc != null && maxSpeedKmh > 1f)
            pcc.maxSpeed = Mathf.Max(8, Mathf.RoundToInt(maxSpeedKmh * Mathf.Clamp01(multiplier)));
    }

    /// <summary>إرجاع حد السرعة الطبيعي (عند العودة إلى المسار).</summary>
    public void RestoreSpeedLimit()
    {
        if (pcc != null && maxSpeedKmh > 1f)
            pcc.maxSpeed = Mathf.RoundToInt(maxSpeedKmh);
    }

    // ================= الضبط القابل للتخصيص (v0.8.0 → v1.1.0) =================

    public void ApplyTuning(int preset, float steerSens)
    {
        float pMax;   // كم/س
        int   pAcc;   // معامل عزم Prometeo (1..10)
        float pSteer; // سرعة عجلة القيادة (0.1..1)
        float pAngle; // أقصى زاوية توجيه

        switch (Mathf.Clamp(preset, 0, 2))
        {
            case 1: // SPORT
                pMax = 80f; pAcc = 10; pSteer = 0.65f; pAngle = 38f;
                break;
            case 2: // TURBO
                pMax = 88f; pAcc = 10; pSteer = 0.72f; pAngle = 40f;
                break;
            default: // STOCK
                pMax = 72f; pAcc = 9;  pSteer = 0.58f; pAngle = 36f;
                break;
        }

        maxSpeedKmh = pMax;
        baseSteerAngle = pAngle;

        if (pcc != null)
        {
            pcc.maxSpeed = Mathf.RoundToInt(pMax);
            pcc.maxReverseSpeed = 30;
            pcc.accelerationMultiplier = Mathf.Clamp(pAcc, 1, 10);
            pcc.steeringSpeed = Mathf.Clamp(pSteer * Mathf.Clamp(steerSens, 0.5f, 2f), 0.1f, 1f);
            pcc.maxSteeringAngle = Mathf.RoundToInt(pAngle);
            pcc.brakeForce = 540;
            pcc.decelerationMultiplier = 2;
            pcc.handbrakeDriftMultiplier = 5;
            RestoreSpeedLimit();
        }

        ApplyGrip();
    }

    public void SetGripOverride(float multiplier)
    {
        gripOverride = Mathf.Clamp(multiplier, 0.1f, 2f);
        ApplyGrip();
    }

    void ApplyGrip()
    {
        // تماسك WheelColliders الجانبي: نقص العشب يُطبَّق على عجلة القيادة الفيزيائية نفسها
        for (int i = 0; i < 4; i++)
        {
            if (wheels[i] == null) continue;
            WheelFrictionCurve f = wheels[i].sidewaysFriction;
            f.stiffness = baseSidewaysStiffness * gripOverride;
            wheels[i].sidewaysFriction = f;
        }
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
