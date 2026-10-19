using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// TurboRacing - v1.0.0 (لعبة ثلاثية الأبعاد حقيقية)
/// نفس نموذج القيادة المعتمد (v0.6.8) منقول إلى فيزياء Rigidbody ثلاثية الأبعاد:
/// - تسارع تلقائي دائم + فرامل تتحول لرجوع بطيء + توجيه بسرعة زاوية صريحة
/// - القيد FreezePositionY + FreezeRotationX/Z = السيارة تنزلق على مستوى أفقي
///   ثابت تماماً كالنسخة ثنائية الأبعاد، لكن داخل عالم مجسّم كامل
/// - سيطرة كاملة على الدوران: rb.angularVelocity يُضبط كل خطوة فيزيائية
///   (تصادمات الجدران لا تستطيع تدوير السيارة من تلقاء نفسها — نفس جذر إصلاح v0.6.8)
/// - مادة هيكل بلا احتكاك: انزلاق ناعم على الجدران
/// - إشارة التوجيه في 3D: دوران يمين (+1) = دوران موجب حول Y (تأكيد هندسي في التعليقات)
/// - حركة بصرية: عجلات تدور وتتوجه + ميلان هيكل في المنعطفات + انحناء عند الفرملة
/// - تتبع معرفات الأصابع لكل زر (v0.6.8) كما هو دون تغيير
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("السرعة (متر/ثانية)")]
    [Tooltip("السرعة القصوى")]
    public float maxSpeed = 14f;

    [Tooltip("التسارع التلقائي (م/ث لكل ثانية)")]
    public float acceleration = 10f;

    [Tooltip("تباطؤ الفرامل (م/ث لكل ثانية)")]
    public float brakeDeceleration = 22f;

    [Tooltip("أقصى سرعة رجوع للخلف عند الاستمرار بالضغط على الفرامل")]
    public float maxReverseSpeed = 4f;

    [Header("التوجيه")]
    [Tooltip("أقصى سرعة دوران (درجة/ثانية)")]
    public float maxTurnSpeed = 220f;

    [Tooltip("السرعة التي تصل عندها قوة التوجيه إلى قصاها")]
    public float fullTurnAtSpeed = 5f;

    [Tooltip("نسبة التوجيه عند السرعة القصوى (0.75 = توجيه أهدأ 25% في المستقيمات)")]
    [Range(0.5f, 1f)]
    public float highSpeedTurnFactor = 0.75f;

    [Tooltip("تماسك الإطارات الجانبي (0 = انزلاق كامل، 10 = تماسك قوي)")]
    public float lateralGrip = 6f;

    // --- حالة الإدخال اللمسي ---
    private bool holdingLeft;
    private bool holdingRight;
    private bool holdingBrake;

    // --- تتبع اللمس لكل زر (v0.6.8 كما هو) ---
    private readonly Dictionary<string, HashSet<int>> holdPointers = new Dictionary<string, HashSet<int>>();
    private readonly Dictionary<string, bool> holdEngaged = new Dictionary<string, bool>();
    private readonly Dictionary<string, System.Action<bool>> holdCallbacks = new Dictionary<string, System.Action<bool>>();

    // --- التغذية البصرية للأزرار ---
    private static readonly Color SteerBaseColor    = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color SteerPressedColor = new Color(1f, 0.72f, 0.15f, 0.80f);
    private static readonly Color BrakeBaseColor    = new Color(0.62f, 0.10f, 0.10f, 0.55f);
    private static readonly Color BrakePressedColor = new Color(1f, 0.25f, 0.18f, 0.90f);

    private readonly Dictionary<string, Image> driveButtons = new Dictionary<string, Image>();
    private readonly Dictionary<string, Color> baseColors = new Dictionary<string, Color>();
    private readonly Dictionary<string, Color> pressedColors = new Dictionary<string, Color>();

    private Rigidbody rb;
    private float currentMaxSpeed;

    // --- الضبط القابل للتخصيص (v0.8.0) ---
    private float baseGrip = 6f;
    private float gripOverride = 1f;
    private MeshRenderer paintRenderer;
    private SurfaceChecker surface;

    // --- الحركة البصرية المجسّمة (v1.0.0) ---
    private Transform[] wheelPivots = new Transform[4];
    private Transform bodyPivot;
    private float wheelSpin;
    private float steerVisual;
    private float rollCur, pitchCur;

    // خصائص للقراءة من سكربتات أخرى
    public float SpeedKmh => rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;
    public bool IsBraking => holdingBrake;

    /// <summary>السرعة الحالية نسبةً إلى الحد الأقصى (0..1) — للصوت والآثار.</summary>
    public float Speed01 => rb != null
        ? Mathf.Clamp01(rb.linearVelocity.magnitude / Mathf.Max(1f, currentMaxSpeed))
        : 0f;

    /// <summary>مُصدِّر مادة الطلاء (يستخدمه SurfaceChecker للتلطيخ على العشب).</summary>
    public MeshRenderer PaintRenderer => paintRenderer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // ضبط ذاتي للفيزياء المجسّمة: حركة مستوية ثابتة (لا طيران ولا انقلاب)
        rb.useGravity = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.constraints = RigidbodyConstraints.FreezePositionY |
                         RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;

        // مادة هيكل بلا احتكاك + ارتداد خفيف (كما في v0.6.8)
        Collider bodyCol = GetComponent<Collider>();
        if (bodyCol != null && bodyCol.sharedMaterial == null)
        {
            PhysicsMaterial shell = new PhysicsMaterial("CarShell");
            shell.staticFriction = 0f;
            shell.dynamicFriction = 0f;
            shell.bounciness = 0.15f;
            shell.frictionCombine = PhysicsMaterialCombine.Minimum;
            bodyCol.sharedMaterial = shell;
        }

        currentMaxSpeed = maxSpeed;

        // بناء النموذج المجسّم (هيكل/تفاصيل/عجلات)
        CarModel3D.Handles h = CarModel3D.Build(transform);
        bodyPivot = h.BodyPivot;
        paintRenderer = h.Paint;
        wheelPivots = h.Wheels;
    }

    void Start()
    {
        // ربط الأزرار اللمسية تلقائياً بالاسم
        BindHoldButton("BtnLeft",  down => holdingLeft  = down);
        BindHoldButton("BtnRight", down => holdingRight = down);
        BindHoldButton("BtnBrake", down => holdingBrake = down);

        ConfigureSteerIcon("BtnLeft",  pointRight: false);
        ConfigureSteerIcon("BtnRight", pointRight: true);
        ConfigureButton("BtnBrake", "BRK", BrakeBaseColor, BrakePressedColor);

        // إعدادات اللاعب: الضبط + اللون + تخطيط الأزرار
        GameSettings.EnsureLoaded();
        ApplyTuning(GameSettings.CarPreset, GameSettings.SteerSens);
        SetBodyColor(GameSettings.CurrentColor);
        DriveButtonsLayout.Apply(this);
    }

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
#endif

        // قسر تحرير الأزرار العالقة (v0.6.8)
        PruneStalePointers();
    }

    void FixedUpdate()
    {
        float steerInput = (holdingRight ? 1f : 0f) - (holdingLeft ? 1f : 0f);

        ApplySteering(steerInput);
        ApplyThrottleAndBrake();
        ApplyLateralGripAndSpeedClamp();
        UpdateVisuals(steerInput);

        // آثار الإطارات: انعطاف حاد بسرعة أو فرملة قوية بسرعة
        if (SkidMarks.Instance != null)
        {
            Vector3 vel = rb.linearVelocity; vel.y = 0f;
            float forwardSpeedNow = Vector3.Dot(vel, transform.forward);
            bool hardSteer = Mathf.Abs(steerInput) > 0.5f && forwardSpeedNow > 7f;
            bool hardBrake = holdingBrake && forwardSpeedNow > 5f;
            if (hardSteer || hardBrake)
                SkidMarks.Instance.Drop(rb.position - transform.forward * 0.95f, transform.eulerAngles.y);
        }
    }

    /// <summary>
    /// مؤثر التصادم: صوت ارتطام متدرّج القوة + اهتزاز كاميرا (كما في v0.7.0).
    /// </summary>
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

    // ================= الفيزياء =================

    /// <summary>
    /// إشارة الدوران في الفضاء المجسّم (تأكيد هندسي):
    /// forward(yaw) = (sinθ, 0, cosθ) — زيادة θ (+Y) تقود من +Z نحو +X
    /// أي من الشمال نحو الشرق = دوران يمين (مع عقارب الساعة من الأعلى).
    /// إذن steerInput = +1 (يمين) ⟹ سرعة زاوية +Y. (في 2D كانت السالبة لعكس
    /// اتجاه عقارب السكان هناك — نفس الإحساس بالضبط بعد التحويل).
    /// </summary>
    void ApplySteering(float steerInput)
    {
        Vector3 vel = rb.linearVelocity; vel.y = 0f;
        float forwardSpeed = Vector3.Dot(vel, transform.forward);

        // قوة التوجيه تتناسب مع السرعة (لا دوران في الثبات)
        float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / fullTurnAtSpeed);

        // عند الرجوع للخلف ينعكس اتجاه الدوران مثل سيارة حقيقية
        float reverse = forwardSpeed < -0.1f ? -1f : 1f;

        // تخفيف طفيف للتوجيه قرب السرعة القصوى
        float speed01 = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / Mathf.Max(1f, currentMaxSpeed));
        float turnScale = Mathf.Lerp(1f, highSpeedTurnFactor, speed01);

        // الإصلاح الجذري (v0.6.8) بنسخة مجسّمة: الضبط الصريح للسرعة الزاوية
        // كل خطوة فيزيائية — أي دوران ضخّه تصادم يموت فوراً في الخطوة التالية
        rb.angularVelocity = Vector3.up * (steerInput * maxTurnSpeed * speedFactor * reverse * turnScale);
    }

    void ApplyThrottleAndBrake()
    {
        Vector3 forward = transform.forward; forward.y = 0f; forward.Normalize();
        Vector3 right = transform.right;     right.y = 0f;  right.Normalize();
        Vector3 vel = rb.linearVelocity;     vel.y = 0f;

        float forwardSpeed = Vector3.Dot(vel, forward);
        float lateralSpeed = Vector3.Dot(vel, right);

        float targetSpeed;
        float rate;

        if (holdingBrake)
        {
            // الفرامل توقف السيارة ثم ترجعها للخلف ببطء (أسلوب الموبايل)
            targetSpeed = -maxReverseSpeed;
            rate = brakeDeceleration;
        }
        else
        {
            // تسارع تلقائي دائم حتى السرعة القصوى
            targetSpeed = currentMaxSpeed;
            rate = acceleration;
        }

        float newForwardSpeed = Mathf.MoveTowards(forwardSpeed, targetSpeed, rate * Time.fixedDeltaTime);

        rb.linearVelocity = forward * newForwardSpeed + right * lateralSpeed; // y يبقى صفراً
    }

    void ApplyLateralGripAndSpeedClamp()
    {
        Vector3 forward = transform.forward; forward.y = 0f; forward.Normalize();
        Vector3 right = transform.right;     right.y = 0f;  right.Normalize();
        Vector3 vel = rb.linearVelocity;     vel.y = 0f;

        float forwardSpeed = Vector3.Dot(vel, forward);
        float lateralSpeed = Vector3.Dot(vel, right);

        // إخماد الانزلاق الجانبي تدريجياً (إحساس تماسك الإطارات)
        lateralSpeed *= Mathf.Clamp01(1f - lateralGrip * Time.fixedDeltaTime);

        Vector3 newVel = forward * forwardSpeed + right * lateralSpeed;

        // حد أقصى صارم للسرعة
        if (newVel.magnitude > currentMaxSpeed)
            newVel = newVel.normalized * currentMaxSpeed;

        rb.linearVelocity = newVel;
    }

    // ================= الحركة البصرية المجسّمة (v1.0.0) =================

    void UpdateVisuals(float steerInput)
    {
        float dt = Time.fixedDeltaTime;

        Vector3 vel = rb.linearVelocity; vel.y = 0f;
        float fwdSpeed = Vector3.Dot(vel, transform.forward);

        // 1) دوران العجلات مع السرعة (نصف قطر 0.30)
        wheelSpin = Mathf.Repeat(wheelSpin + (fwdSpeed / 0.30f) * Mathf.Rad2Deg * dt, 360f);

        // 2) توجيه العجلات الأماميتين مع التوجيه
        float steerTarget = steerInput * 30f;
        steerVisual = Mathf.Lerp(steerVisual, steerTarget, 1f - Mathf.Exp(-14f * dt));

        for (int i = 0; i < 4; i++)
        {
            if (wheelPivots[i] == null) continue;
            bool front = i < 2;
            // Euler(spin حول X = محور العجلة، steer حول Y) — التوجيه خارج الدوران
            wheelPivots[i].localRotation = Quaternion.Euler(wheelSpin, front ? steerVisual : 0f, 0f);
        }

        // 3) ميلان الهيكل: للخارج في المنعطفات + انحناء أمامي عند الفرملة
        float rollTarget  = steerVisual * 0.10f;  // حتى ~3° للخارج
        float pitchTarget = holdingBrake && fwdSpeed > 1f ? 1.8f : -0.6f;

        rollCur  = Mathf.Lerp(rollCur,  rollTarget,  1f - Mathf.Exp(-8f * dt));
        pitchCur = Mathf.Lerp(pitchCur, pitchTarget, 1f - Mathf.Exp(-8f * dt));

        if (bodyPivot != null)
            bodyPivot.localRotation = Quaternion.Euler(pitchCur, 0f, rollCur);
    }

    // ================= ربط الأزرار تلقائي =================

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

    void ConfigureButton(string buttonName, string label, Color baseColor, Color pressedColor)
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

        Text text = buttonGO.GetComponentInChildren<Text>(true);
        if (text == null) return;

        text.text = label;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontStyle = FontStyle.Bold;

        RectTransform rt = buttonGO.GetComponent<RectTransform>();
        if (rt != null) text.fontSize = Mathf.Max(24, Mathf.RoundToInt(rt.rect.height * 0.4f));
    }

    void ConfigureSteerIcon(string buttonName, bool pointRight)
    {
        GameObject buttonGO = GameObject.Find(buttonName);
        if (buttonGO == null) return;

        Image image = buttonGO.GetComponent<Image>();
        if (image != null)
        {
            image.color = SteerBaseColor;
            driveButtons[buttonName] = image;
            baseColors[buttonName] = SteerBaseColor;
            pressedColors[buttonName] = SteerPressedColor;
        }

        Text oldText = buttonGO.GetComponentInChildren<Text>(true);
        if (oldText != null) oldText.gameObject.SetActive(false);

        GameObject icon = new GameObject("ArrowIcon",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.transform.SetParent(buttonGO.transform, false);

        RectTransform irt = icon.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.5f, 0.5f);
        irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = Vector2.zero;

        RectTransform brt = buttonGO.GetComponent<RectTransform>();
        float side = 110f;
        if (brt != null && brt.rect.width > 1f)
            side = Mathf.Min(brt.rect.width, brt.rect.height) * 0.38f;
        irt.sizeDelta = new Vector2(side, side);

        Image img = icon.GetComponent<Image>();
        img.sprite = UiKit.TriangleSprite(pointRight);
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
        holdingLeft = holdingRight = holdingBrake = false;

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

    /// <summary>تخفيض حد السرعة (العشب). مثال: 0.4 = 40% من السرعة القصوى.</summary>
    public void SetSpeedLimit(float multiplier)
    {
        currentMaxSpeed = maxSpeed * Mathf.Clamp01(multiplier);
    }

    /// <summary>إرجاع حد السرعة الطبيعي (عند العودة إلى المسار).</summary>
    public void RestoreSpeedLimit()
    {
        currentMaxSpeed = maxSpeed;
    }

    // ================= الضبط القابل للتخصيص (v0.8.0) =================

    public void ApplyTuning(int preset, float steerSens)
    {
        float pMax, pAcc, pTurn, pGrip;

        switch (Mathf.Clamp(preset, 0, 2))
        {
            case 1: // SPORT
                pMax = 15.5f; pAcc = 12f; pTurn = 235f; pGrip = 6.6f;
                break;
            case 2: // TURBO
                pMax = 17f;  pAcc = 14f; pTurn = 245f; pGrip = 5.6f;
                break;
            default: // STOCK
                pMax = 14f;  pAcc = 10f; pTurn = 220f; pGrip = 6f;
                break;
        }

        maxSpeed = pMax;
        acceleration = pAcc;
        maxTurnSpeed = pTurn * Mathf.Clamp(steerSens, 0.5f, 2f);
        baseGrip = pGrip;
        lateralGrip = pGrip * gripOverride;

        currentMaxSpeed = maxSpeed;
    }

    public void SetGripOverride(float multiplier)
    {
        gripOverride = Mathf.Clamp(multiplier, 0.1f, 2f);
        lateralGrip = baseGrip * gripOverride;
    }

    /// <summary>
    /// لون طلاء السيارة: يغيّر لون مادة الهيكل المجسّم ويحدّث اللون الأساس
    /// لدى SurfaceChecker (تلطيخ العشب) عند عدم التواجد عليه.
    /// </summary>
    public void SetBodyColor(Color color)
    {
        if (paintRenderer == null)
        {
            Transform t = transform.Find("BodyPivot/Paint");
            if (t != null) paintRenderer = t.GetComponent<MeshRenderer>();
        }
        if (paintRenderer != null) paintRenderer.material.color = color;

        if (surface == null) surface = GetComponent<SurfaceChecker>();
        if (surface != null) surface.RefreshBaseColor();
    }

    /// <summary>شفافية أزرار القيادة من الإعدادات.</summary>
    public void ApplyButtonOpacity(float opacityMul)
    {
        float op = Mathf.Clamp01(opacityMul);
        UpdateBaseColor("BtnLeft",  new Color(0f, 0f, 0f, 0.35f * op));
        UpdateBaseColor("BtnRight", new Color(0f, 0f, 0f, 0.35f * op));
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
