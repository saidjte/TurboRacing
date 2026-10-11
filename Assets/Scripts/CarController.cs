using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// TurboRacing - المرحلة 1
/// التحكم في السيارة بفيزياء Rigidbody2D (بدون جاذبية - Gravity Scale = 0):
/// - تسارع تلقائي دائم (أسلوب ألعاب الموبايل الشهيرة)
/// - زران للتوجيه (يسار/يمين) + زر فرامل (يرجع للخلف عند التوقف الكامل)
/// - السكربت يضبط نفسه ذاتياً: يبحث عن الأزرار بالاسم ويربطها في Start()
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
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

    [Tooltip("تماسك الإطارات الجانبي (0 = انزلاق كامل، 10 = تماسك قوي)")]
    public float lateralGrip = 6f;

    // --- حالة الإدخال اللمسي ---
    private bool holdingLeft;
    private bool holdingRight;
    private bool holdingBrake;

    private Rigidbody2D rb;
    private float currentMaxSpeed;

    // --- خصائص للقراءة من سكربتات أخرى (HUD في المراحل القادمة) ---
    public float SpeedKmh => rb.linearVelocity.magnitude * 3.6f;
    public bool IsBraking => holdingBrake;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // ضبط ذاتي للفيزياء: لا جاذبية + إعدادات سلسة للموبايل
        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        currentMaxSpeed = maxSpeed;
    }

    void Start()
    {
        // ربط الأزرار اللمسية تلقائياً بالاسم (بدون أي سحب وإفلات في المحرر)
        BindHoldButton("BtnLeft",  down => holdingLeft  = down);
        BindHoldButton("BtnRight", down => holdingRight = down);
        BindHoldButton("BtnBrake", down => holdingBrake = down);

        // تنسيق الأزرار ونصوصها تلقائياً
        ConfigureButton("BtnLeft",  "<");
        ConfigureButton("BtnRight", ">");
        ConfigureButton("BtnBrake", "BRK");
    }

    void Update()
    {
        // احتياطي للتجربة داخل المحرر فقط (لا يُبنى داخل تطبيق الهاتف)
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetKeyDown(KeyCode.LeftArrow)  || Input.GetKeyDown(KeyCode.A)) holdingLeft  = true;
        if (Input.GetKeyUp(KeyCode.LeftArrow)    || Input.GetKeyUp(KeyCode.A))   holdingLeft  = false;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) holdingRight = true;
        if (Input.GetKeyUp(KeyCode.RightArrow)   || Input.GetKeyUp(KeyCode.D))   holdingRight = false;
        if (Input.GetKeyDown(KeyCode.Space)      || Input.GetKeyDown(KeyCode.S)) holdingBrake = true;
        if (Input.GetKeyUp(KeyCode.Space)        || Input.GetKeyUp(KeyCode.S))   holdingBrake = false;
#endif
    }

    void FixedUpdate()
    {
        float steerInput = (holdingRight ? 1f : 0f) - (holdingLeft ? 1f : 0f);

        ApplySteering(steerInput);
        ApplyThrottleAndBrake();
        ApplyLateralGripAndSpeedClamp();
    }

    // ================= الفيزياء =================

    void ApplySteering(float steerInput)
    {
        if (Mathf.Abs(steerInput) < 0.01f) return;

        float forwardSpeed = Vector2.Dot(rb.linearVelocity, transform.up);

        // قوة التوجيه تتناسب مع السرعة (لا دوران في الثبات)
        float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / fullTurnAtSpeed);

        // عند الرجوع للخلف ينعكس اتجاه الدوران مثل سيارة حقيقية
        float reverse = forwardSpeed < -0.1f ? -1f : 1f;

        float turnDelta = steerInput * maxTurnSpeed * speedFactor * reverse * Time.fixedDeltaTime;

        // MoveRotation = دوران فيزيائي سلس مع Interpolate
        rb.MoveRotation(rb.rotation - turnDelta);
    }

    void ApplyThrottleAndBrake()
    {
        Vector2 forward = transform.up;
        Vector2 right = transform.right;
        Vector2 vel = rb.linearVelocity;

        float forwardSpeed = Vector2.Dot(vel, forward);
        float lateralSpeed = Vector2.Dot(vel, right);

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

        rb.linearVelocity = forward * newForwardSpeed + right * lateralSpeed;
    }

    void ApplyLateralGripAndSpeedClamp()
    {
        Vector2 forward = transform.up;
        Vector2 right = transform.right;
        Vector2 vel = rb.linearVelocity;

        float forwardSpeed = Vector2.Dot(vel, forward);
        float lateralSpeed = Vector2.Dot(vel, right);

        // إخماد الانزلاق الجانبي تدريجياً (إحساس تماسك الإطارات)
        lateralSpeed *= Mathf.Clamp01(1f - lateralGrip * Time.fixedDeltaTime);

        Vector2 newVel = forward * forwardSpeed + right * lateralSpeed;

        // حد أقصى صارم للسرعة
        if (newVel.magnitude > currentMaxSpeed)
            newVel = newVel.normalized * currentMaxSpeed;

        rb.linearVelocity = newVel;
    }

    // ================= ربط الأزرار تلقائياً =================

    void BindHoldButton(string buttonName, System.Action<bool> onStateChanged)
    {
        GameObject buttonGO = GameObject.Find(buttonName);

        if (buttonGO == null)
        {
            Debug.LogWarning($"[CarController] لم أجد زراً باسم '{buttonName}' في المشهد - تخطيت ربطه. تأكد من الاسم بدقة.");
            return;
        }

        EventTrigger trigger = buttonGO.GetComponent<EventTrigger>();
        if (trigger == null) trigger = buttonGO.AddComponent<EventTrigger>();

        // PointerDown = بدء الضغط ، PointerUp = ترك الضغط (يدعم اللمس المتعدد)
        AddTriggerEntry(trigger, EventTriggerType.PointerDown, () => onStateChanged(true));
        AddTriggerEntry(trigger, EventTriggerType.PointerUp,   () => onStateChanged(false));
    }

    void AddTriggerEntry(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());   // الربط بالكود (AddListener) كما هو مطلوب
        trigger.triggers.Add(entry);
    }

    void ConfigureButton(string buttonName, string label)
    {
        GameObject buttonGO = GameObject.Find(buttonName);
        if (buttonGO == null) return;

        Image image = buttonGO.GetComponent<Image>();
        if (image != null) image.color = new Color(0f, 0f, 0f, 0.35f); // أسود شبه شفاف

        Text text = buttonGO.GetComponentInChildren<Text>(true);
        if (text == null) return;

        text.text = label;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.fontStyle = FontStyle.Bold;

        RectTransform rt = buttonGO.GetComponent<RectTransform>();
        if (rt != null) text.fontSize = Mathf.Max(24, Mathf.RoundToInt(rt.rect.height * 0.4f));
    }

    // ================= واجهة للمراحل القادمة =================

    /// <summary>تخفيض حد السرعة (سيستخدمه العشب في المرحلة 2). مثال: 0.4 = 40% من السرعة القصوى.</summary>
    public void SetSpeedLimit(float multiplier)
    {
        currentMaxSpeed = maxSpeed * Mathf.Clamp01(multiplier);
    }

    /// <summary>إرجاع حد السرعة الطبيعي (عند العودة إلى المسار).</summary>
    public void RestoreSpeedLimit()
    {
        currentMaxSpeed = maxSpeed;
    }
}
