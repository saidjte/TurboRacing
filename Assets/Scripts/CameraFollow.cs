using UnityEngine;

/// <summary>
/// TurboRacing - v1.0.0 (لعبة ثلاثية الأبعاد حقيقية)
/// كاميرا مطاردة مجسّمة حقيقية:
/// - وضع السباق: كاميرا منظورية منخفضة خلف السيارة (ميلان ~20°) — تظهر السماء
///   والأفق والأشجار والمدرّجات = إحساس عمق ثلاثي الأبعاد كامل
///   (نقطة نظرة أمام السيارة + تنعيم مزدوج: الموضع والدوران + مسافة قابلة للضبط)
/// - وضع القائمة: منظور علوي مائل يؤطر الحلبة كاملة (يُحسب من أبعاد الحلبة
///   ومجال الرؤية ونسبة الشاشة) — يبقى مجسّماً (ليس أورثوغرافياً)
/// - ضبط الضباب حسب الوضع (ضباب قريب سينمائي في السباق، شبه معدوم في القائمة)
/// - (v1.3.0) توسيع مجال الرؤية مع السرعة (+9° عند السرعة القصوى) = إحساس فيزيائي بالتسارع
/// - اهتزاز التصادم + إعدادات اللاعب (مسافة/تعطيل) كما في v0.8.0
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public static CameraFollow Instance { get; private set; }

    [Header("وضع المطاردة (منظور الشخص الثالث)")]
    [Tooltip("زاوية الميلان بالدرجات (0 = أفقية تماماً، 90 = علوية كاملة)")]
    public float chasePitchDeg = 20f;

    [Tooltip("بعد الكاميرا عن نقطة النظرة (وحدات عالمية)")]
    public float chaseDistance = 10.5f;

    [Tooltip("مجال الرؤية للكاميرا المنظورية")]
    public float chaseFov = 58f;

    [Tooltip("مسافة نقطة النظرة أمام السيارة")]
    public float chaseLookAhead = 5f;

    [Tooltip("ارتفاع نقطة النظرة فوق الأرض")]
    public float chasePivotHeight = 1.2f;

    [Tooltip("سرعة تتبع دوران الكاميرا لاتجاه السيارة (أكبر = أسرع)")]
    public float yawLerp = 7f;

    [Header("عام")]
    [Tooltip("نعومة متابعة نقطة النظرة")]
    public float smoothTime = 0.09f;

    private Transform target;
    private Camera cam;
    private CarController targetCar;   // (v1.3.0) لتوسيع مجال الرؤية مع السرعة
    private bool inOverview;
    private bool inGarage;             // (v1.4.0) وضع الكراج

    // حالة وضع المطاردة
    private Vector3 smoothPivot;
    private Vector3 pivotVel;
    private float smoothYaw;
    private bool yawInit;

    // (v0.8.0) إعدادات اللاعب
    private float zoomMul = 1f;
    private bool shakeEnabled = true;

    // (v0.7.0) اهتزاز التصادم
    private float shakeAmp;

    // ألوان السماء/الضباب (يضبطها GameBootstrap؛ نعدّل مدى الضباب فقط عند التبديل)
    const float ChaseFogStart = 90f,  ChaseFogEnd = 340f;   // v1.5.0: مدى الحلبة البيضاوية الكبيرة
    const float MenuFogStart  = 380f, MenuFogEnd  = 850f;   // v1.5.0: تأطير الحلبة كاملة

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam != null) cam.fieldOfView = chaseFov;

        GameObject car = GameObject.Find("Car");
        if (car != null)
        {
            target = car.transform;
            targetCar = car.GetComponent<CarController>();
        }
        else
        {
            Debug.LogWarning("[CameraFollow] لم أجد كائناً باسم 'Car' - تأكد من تسمية السيارة بالضبط.");
        }

        GameSettings.EnsureLoaded();
        ApplyZoom(GameSettings.CamZoom);
        ApplyShake(GameSettings.CamShake);
    }

    /// <summary>(v0.8.0) مسافة الكاميرا من الإعدادات: 0.8 قريبة ← 1.3 بعيدة.</summary>
    public void ApplyZoom(float zoom)
    {
        zoomMul = Mathf.Clamp(zoom, 0.6f, 1.6f);
    }

    /// <summary>(v0.8.0) تفعيل/تعطيل اهتزاز الكاميرا عند التصادم.</summary>
    public void ApplyShake(bool enabled)
    {
        shakeEnabled = enabled;
    }

    /// <summary>
    /// تبديل بين وضعي الكاميرا:
    /// true  = القائمة (منظور علوي مائل يأطر الحلبة)
    /// false = السباق (مطاردة منخفضة خلف السيارة)
    /// </summary>
    public void SetOverviewMode(bool overview)
    {
        inOverview = overview;
        inGarage = false; // أي تبديل آخر يخرج من الكراج
        if (cam == null) cam = GetComponent<Camera>();

        // مدى الضباب حسب الوضع (القائمة تحتاج شفافية كاملة للحلبة)
        RenderSettings.fogStartDistance = overview ? MenuFogStart : ChaseFogStart;
        RenderSettings.fogEndDistance   = overview ? MenuFogEnd   : ChaseFogEnd;

        if (!overview)
        {
            // التقاط فوري لنقطة النظرة والاتجاه (بدون انزلاق من وضع القائمة)
            yawInit = false;
            pivotVel = Vector3.zero;
            if (target != null)
            {
                Vector3 fwd = target.forward; fwd.y = 0f; fwd.Normalize();
                smoothPivot = target.position + Vector3.up * chasePivotHeight + fwd * chaseLookAhead;
                smoothYaw = target.eulerAngles.y;
                yawInit = true;
            }
        }
    }

    /// <summary>
    /// (v1.4.0) وضع الكراج: كاميرا ثابتة تُطر المنصة الدوّارة في GarageStage.
    /// الخروج منه يتم عبر SetOverviewMode (يُنادى عند العودة للقائمة).
    /// </summary>
    public void SetGarageMode(bool on)
    {
        inGarage = on;
        if (cam == null) cam = GetComponent<Camera>();

        if (on)
        {
            // ضباب قريب لإخفاء العالم خلف جدار الاستوديو = خلفية نظيفة
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 110f;
        }
    }

    void LateUpdate()
    {
        // ================= وضع الكراج (v1.4.0): تأطير منصة العرض =================
        if (inGarage)
        {
            GarageStage gs = GarageStage.Instance;
            if (gs != null && cam != null)
            {
                transform.SetPositionAndRotation(gs.CameraPos,
                    Quaternion.LookRotation((gs.CameraLook - gs.CameraPos).normalized, Vector3.up));
                cam.fieldOfView = 42f;
            }
            return;
        }

        // ================= وضع القائمة: منظور علوي مائل =================
        // v1.8.0: التأطير من TrackBuilder — على الحلبة البيضاوية الضخمة (870×420م)
        // كان تأطير الحلبة كاملة يجعل السيارات المتجاورة «تتكدس» كنقاط واحدة.
        // الآن: البيضاوية تُؤطَّر عند منطقة الانطلاق (130م) فتظهر الشبكة والسيارات
        // بوضوح، والإجرائية تبقى مؤطرة كاملة كما كانت.
        if (inOverview)
        {
            TrackBuilder tb = TrackBuilder.Instance;
            if (tb == null) return;

            if (cam != null)
            {
                cam.fieldOfView = chaseFov; // التأطير المحسوب يعتمد المجال المرجعي

                float halfV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float aspect = Mathf.Max(0.6f, cam.aspect);

                float span = Mathf.Max(60f, tb.OverviewSpan);

                // العمق المائل (عامل 0.72) والعرض بالكامل
                float needV = (span * 0.55f) / halfV;
                float needH = span / (halfV * aspect);
                float h = Mathf.Max(needV, needH) * 1.06f;

                Vector3 c = tb.OverviewCenter;
                transform.position = new Vector3(c.x, h, c.z - h * 0.48f);
                transform.rotation = Quaternion.LookRotation((c + Vector3.up * 1.5f) - transform.position, Vector3.up);
            }
            return;
        }

        if (target == null) return;

        // ================= مطاردة منخفضة خلف السيارة =================
        float dt = Time.deltaTime;

        // نقطة النظرة أمام السيارة مع تنعيم
        Vector3 carFwd = target.forward; carFwd.y = 0f; carFwd.Normalize();
        Vector3 pivot = target.position + Vector3.up * chasePivotHeight + carFwd * chaseLookAhead;

        if (!yawInit)
        {
            smoothPivot = pivot;
            smoothYaw = target.eulerAngles.y;
            yawInit = true;
        }

        smoothPivot = Vector3.SmoothDamp(smoothPivot, pivot, ref pivotVel, smoothTime);

        // دوران الكاميرا مع اتجاه السيارة (تنعيم أُسّي مستقر مع أي معدل إطارات)
        float targetYaw = target.eulerAngles.y;
        smoothYaw = Mathf.LerpAngle(smoothYaw, targetYaw, 1f - Mathf.Exp(-yawLerp * dt));

        Quaternion rot = Quaternion.Euler(chasePitchDeg, smoothYaw, 0f);
        Vector3 pos = smoothPivot - rot * Vector3.forward * (chaseDistance * zoomMul);

        // (v0.7.0) اهتزاز التصادم: إزاحة عشوائية صغيرة تتلاشى أُسّياً
        if (shakeAmp > 0.001f)
        {
            pos += new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f) * 0.6f, Random.Range(-1f, 1f)) * shakeAmp;
            shakeAmp *= Mathf.Exp(-6f * dt);
        }
        else
        {
            shakeAmp = 0f;
        }

        // (v1.3.0) إحساس السرعة: مجال الرؤية يتسع حتى +9° تدريجياً مع اقتراب السرعة القصوى
        if (cam != null)
            cam.fieldOfView = chaseFov + (targetCar != null ? targetCar.Speed01 * 9f : 0f);

        transform.SetPositionAndRotation(pos, rot);
    }

    /// <summary>(v0.7.0) دفعة اهتزاز (تُستخدم أكبر قيمة إذا تزامنت دفعات).</summary>
    public void Shake(float amp)
    {
        if (!shakeEnabled) return;
        shakeAmp = Mathf.Max(shakeAmp, amp);
    }
}
