using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// TurboRacing - v0.6.8
/// التحكم في السيارة بفيزياء Rigidbody2D (بدون جاذبية - Gravity Scale = 0):
/// - تسارع تلقائي دائم (أسلوب ألعاب الموبايل الشهيرة)
/// - التخطيط القياسي: زرا توجيه مجمّعان أسفل اليسار + زر فرامل كبير أسفل اليمين
/// - (v0.6.8) سيطرة كاملة على الدوران: rb.angularVelocity يُضبط كل خطوة فيزيائية
///   ← تصادمات الجدران لا يمكنها أبداً تدوير السيارة من تلقاء نفسها.
///   (السبب الجذري سابقاً: angularDamping = 0 مع MoveRotation التي تتجاهل السرعة
///    الزاوية الموروثة من التصادم ← السيارة ظلت تدور للأبد بعد أي احتكاك بجدار)
/// - (v0.6.8) تتبع معرفات الأصابع لكل زر: الانزلاق بين الزرين يفعّل الزر الآخر
///   فوراً + قسر تحرير أي زر عالق كل إطار بمطابقة الأصابع الملموسة فعلياً
/// - (v0.6.8) مادة هيكل بلا احتكاك: انزلاق ناعم على الجدران بدل الالتصاق
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

    [Tooltip("(v0.6.8) نسبة التوجيه عند السرعة القصوى (0.75 = توجيه أهدأ 25% في المستقيمات لثبات أعلى)")]
    [Range(0.5f, 1f)]
    public float highSpeedTurnFactor = 0.75f;

    [Tooltip("تماسك الإطارات الجانبي (0 = انزلاق كامل، 10 = تماسك قوي)")]
    public float lateralGrip = 6f;

    // --- حالة الإدخال اللمسي ---
    private bool holdingLeft;
    private bool holdingRight;
    private bool holdingBrake;

    // --- (v0.6.8) تتبع اللمس لكل زر ---
    // لكل زر: معرفات المؤشرات المسجلة عليه حالياً + حالة التنشيط الحالية
    // الزر منشط فقط ما دامت إحدى هذه المعرفات لا تزال ملموسة فعلياً على الشاشة
    private readonly Dictionary<string, HashSet<int>> holdPointers = new Dictionary<string, HashSet<int>>();
    private readonly Dictionary<string, bool> holdEngaged = new Dictionary<string, bool>();
    private readonly Dictionary<string, System.Action<bool>> holdCallbacks = new Dictionary<string, System.Action<bool>>();

    // --- التغذية البصرية للأزرار (v0.6.0) ---
    // لون ثابت عند السكون ولون مميز أثناء الضغط حتى يعرف اللاعب أن اللمس سُجّل
    private static readonly Color SteerBaseColor    = new Color(0f, 0f, 0f, 0.35f);        // أسود شبه شفاف
    private static readonly Color SteerPressedColor = new Color(1f, 0.72f, 0.15f, 0.80f);  // عنبري (هوية اللعبة)
    private static readonly Color BrakeBaseColor    = new Color(0.62f, 0.10f, 0.10f, 0.55f); // أحمر داكن
    private static readonly Color BrakePressedColor = new Color(1f, 0.25f, 0.18f, 0.90f);  // أحمر ساطع

    private readonly Dictionary<string, Image> driveButtons = new Dictionary<string, Image>();
    private readonly Dictionary<string, Color> baseColors = new Dictionary<string, Color>();
    private readonly Dictionary<string, Color> pressedColors = new Dictionary<string, Color>();

    private Rigidbody2D rb;
    private float currentMaxSpeed;

    // --- خصائص للقراءة من سكربتات أخرى (HUD في المراحل القادمة) ---
    public float SpeedKmh => rb.linearVelocity.magnitude * 3.6f;
    public bool IsBraking => holdingBrake;

    /// <summary>(v0.7.0) السرعة الحالية نسبةً إلى الحد الأقصى (0..1) — يستخدمها الصوت والآثار.</summary>
    public float Speed01 => rb != null
        ? Mathf.Clamp01(rb.linearVelocity.magnitude / Mathf.Max(1f, currentMaxSpeed))
        : 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // ضبط ذاتي للفيزياء: لا جاذبية + إعدادات سلسة للموبايل
        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // (v0.6.8) لا نعتمد بعد الآن على angularDamping لإيقاف الدوران -
        // ApplySteering يضبط السرعة الزاوية صريحاً كل خطوة فيزيائية

        // (v0.6.8) مادة هيكل بلا احتكاك + ارتداد خفيف: ملامسة الجدار تصبح
        // انزلاقاً ناعماً بدل الالتصاق والتباطؤ المفاجئ (مشكلة تحكم شائعة)
        Collider2D body = GetComponent<Collider2D>();
        if (body != null && body.sharedMaterial == null)
        {
            PhysicsMaterial2D shell = new PhysicsMaterial2D("CarShell");
            shell.friction = 0f;
            shell.bounciness = 0.15f;
            body.sharedMaterial = shell;
        }

        currentMaxSpeed = maxSpeed;
    }

    void Start()
    {
        // ربط الأزرار اللمسية تلقائياً بالاسم (بدون أي سحب وإفلات في المحرر)
        BindHoldButton("BtnLeft",  down => holdingLeft  = down);
        BindHoldButton("BtnRight", down => holdingRight = down);
        BindHoldButton("BtnBrake", down => holdingBrake = down);

        // أزرار التوجيه (v0.6.6): أيقونتا سهم مرسومتان بالصور — مستقلتان عن الخطوط
        ConfigureSteerIcon("BtnLeft",  pointRight: false);
        ConfigureSteerIcon("BtnRight", pointRight: true);

        // الفرامل: نص BRK (حروف أساسية متوفرة في كل الخطوط)
        ConfigureButton("BtnBrake", "BRK", BrakeBaseColor, BrakePressedColor);
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

        // (v0.6.8) قسر تحرير الأزرار العالقة: امسح كل معرف مؤشر لم يعد
        // ملموساً فعلياً على الشاشة (يغطي رفع الإصبع خارج حدود الزر أو
        // انتهاء اللمس لأي سبب دون وصول حدث PointerUp إلى الزر)
        PruneStalePointers();
    }

    void FixedUpdate()
    {
        float steerInput = (holdingRight ? 1f : 0f) - (holdingLeft ? 1f : 0f);

        ApplySteering(steerInput);
        ApplyThrottleAndBrake();
        ApplyLateralGripAndSpeedClamp();

        // (v0.7.0) آثار الإطارات: انعطاف حاد بسرعة أو فرملة قوية بسرعة
        if (SkidMarks.Instance != null)
        {
            float forwardSpeedNow = Vector2.Dot(rb.linearVelocity, transform.up);
            bool hardSteer = Mathf.Abs(steerInput) > 0.5f && forwardSpeedNow > 7f;
            bool hardBrake = holdingBrake && forwardSpeedNow > 5f;
            if (hardSteer || hardBrake)
                SkidMarks.Instance.Drop(rb.position - (Vector2)(transform.up * 0.55f), rb.rotation);
        }
    }

    /// <summary>
    /// (v0.7.0) مؤثر التصادم: صوت ارتطام متدرّج القوة + اهتزاز كاميرا
    /// حسب شدة الاصطدام (العتبة 2.5 م/ث لتجنب نقرات الملامسة العابرة).
    /// </summary>
    void OnCollisionEnter2D(Collision2D collision)
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

    void ApplySteering(float steerInput)
    {
        float forwardSpeed = Vector2.Dot(rb.linearVelocity, transform.up);

        // قوة التوجيه تتناسب مع السرعة (لا دوران في الثبات)
        float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / fullTurnAtSpeed);

        // عند الرجوع للخلف ينعكس اتجاه الدوران مثل سيارة حقيقية
        float reverse = forwardSpeed < -0.1f ? -1f : 1f;

        // (v0.6.8) تخفيف طفيف للتوجيه قرب السرعة القصوى: ثبات أعلى في المستقيمات
        float speed01 = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / Mathf.Max(1f, currentMaxSpeed));
        float turnScale = Mathf.Lerp(1f, highSpeedTurnFactor, speed01);

        // (v0.6.8) الإصلاح الجذري لدوران السيارة من تلقاء نفسها:
        // الضبط الصريح للسرعة الزاوية كل خطوة فيزيائية (Unity 2D بوحدة درجة/ثانية):
        // - بدون مدخل توجيه: سرعة زاوية = صفر ← أي دوران ضخّه تصادم جدار يموت فوراً
        //   بدل الاستمرار إلى الأبد (كان السبب: MoveRotation + angularDamping = 0
        //   ولا شيء يمس السرعة الزاوية الموروثة من التصادم)
        // - مع توجيه: الدوران يطابق مدخل اللاعب حصراً بغض النظر عن التصادمات
        // (الإشارة مطابقة للكود القديم: rotation - turnDelta مع steer موجب =
        // دوران مع عقارب الساعة = السرعة الزاوية سالبة)
        rb.angularVelocity = -steerInput * maxTurnSpeed * speedFactor * reverse * turnScale;
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

        // (v0.6.8) تسجيل حالة التتبع لهذا الزر
        holdPointers[buttonName] = new HashSet<int>();
        holdEngaged[buttonName] = false;
        holdCallbacks[buttonName] = onStateChanged;

        EventTrigger trigger = buttonGO.GetComponent<EventTrigger>();
        if (trigger == null) trigger = buttonGO.AddComponent<EventTrigger>();

        // (v0.6.8) ربط كامل لأحداث المؤشر مع تتبع المعرفات:
        // PointerDown/Up للضغط العادي، PointerExit لخروج الإصبع،
        // PointerEnter لتفعيل الزر عند الانزلاق عليه من زر آخر أو من الفراغ
        AddTriggerEntry(trigger, EventTriggerType.PointerDown, ed => OnHoldPointer(buttonName, ed, true));
        AddTriggerEntry(trigger, EventTriggerType.PointerUp,   ed => OnHoldPointer(buttonName, ed, false));
        AddTriggerEntry(trigger, EventTriggerType.PointerExit, ed => OnHoldPointer(buttonName, ed, false));
        AddTriggerEntry(trigger, EventTriggerType.PointerEnter, ed => OnHoldPointer(buttonName, ed, true));
    }

    /// <summary>(v0.6.8) معالجة موحدة لكل أحداث المؤشر على زر قيادة.</summary>
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

    /// <summary>
    /// (v0.6.8) إعادة حساب حالة الزر: منشط فقط إذا كان أحد معرفاته
    /// لا يزال ملموساً فعلياً على الشاشة، مع استدعاء الحد عند التغيير فقط.
    /// </summary>
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

    /// <summary>
    /// (v0.6.8) هل معرف المؤشر لا يزال ملموساً فعلياً؟
    /// اللمس: نطابق fingerId ضمن Input.touches (المعرفات غير السالبة).
    /// المحرر: الفأرة اليسرى بمعرف سالب (-1 في uGUI).
    /// </summary>
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

    /// <summary>(v0.6.8) تنظيف دوري: حذف معرفات المؤشرات التي انتهى لمسها.</summary>
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
        entry.callback.AddListener(ed => action(ed));   // الربط بالكود (AddListener) كما هو مطلوب
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

    /// <summary>
    /// (v0.6.6) أيقونة سهم مرسومة لأزرار التوجيه: تعطيل نص الزر ووضع Image بسبرايت
    /// مثلث مولّد برمجياً — بديل مضمون الظهور عن نصوص "&lt;" و"&gt;" المعتمدة على الخط.
    /// </summary>
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

        // تعطيل نص الزر القديم (كان يعتمد على الخط)
        Text oldText = buttonGO.GetComponentInChildren<Text>(true);
        if (oldText != null) oldText.gameObject.SetActive(false);

        // أيقونة السهم (صورة مركزية لا تعترض اللمس)
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

    /// <summary>تبديل لون الزر بين السكون والضغط (تغذية بصرية فورية للاعب).</summary>
    void SetPressedVisual(string buttonName, bool pressed)
    {
        Image image;
        if (!driveButtons.TryGetValue(buttonName, out image) || image == null) return;

        Color target;
        if (pressed)
        {
            pressedColors.TryGetValue(buttonName, out target);
        }
        else
        {
            baseColors.TryGetValue(buttonName, out target);
        }

        image.color = target;
    }

    /// <summary>تصفير كل المدخلات والألوان (يستدعى عند بدء/إعادة السباق أو العودة للقائمة
    /// حتى لا تبقى السيارة تتوجّه أو تفرمل بمعزل عن لمسات اللاعب القديمة).</summary>
    public void ResetInput()
    {
        holdingLeft = holdingRight = holdingBrake = false;

        // (v0.6.8) تصفير تتبع المؤشرات كلياً حتى لا تُعاد تنشيط أزرار
        // من بقايا لمسات قديمة بعد إعادة السباق أو العودة للقائمة
        foreach (KeyValuePair<string, HashSet<int>> kv in holdPointers)
            kv.Value.Clear();
        holdEngaged.Clear(); // RefreshHold يتعامل مع المفقود كغير منشّط افتراضياً

        foreach (KeyValuePair<string, Image> kv in driveButtons)
        {
            Color baseColor;
            if (kv.Value != null && baseColors.TryGetValue(kv.Key, out baseColor))
                kv.Value.color = baseColor;
        }
    }

    // ================= واجهة للمراحل القادمة =================

    /// <summary>تخفيض حد السرعة (سيستخدمه العشب). مثال: 0.4 = 40% من السرعة القصوى.</summary>
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
