using UnityEngine;

/// <summary>
/// TurboRacing - v0.5.0 (منظور الشخص الثالث)
/// كاميرا مطاردة مائلة (Chase Camera) للسباق:
/// - كاميرا منظورية (Perspective) خلف السيارة بميلان ~55° حول محور العرض
/// - الدوران مقفول على اتجاه السيارة (Orientation Lock): السيارة تسير دائماً نحو أعلى الشاشة
///   والطريق أمامها يظهر بعمق متناقص (تأثير شبه ثلاثي الأبعاد)
/// - نقطة النظرة أمام السيارة (Look-Ahead) لرؤية الطريق القادم مع تنعيم مزدوج (نقطة + دوران)
/// - وضع القائمة (Overview): عودة للعلوية الأورثوغرافية لتأطير الحلبة كاملة
/// </summary>
public class CameraFollow : MonoBehaviour
{
    /// <summary>(v0.7.0) مرجع ساكن لاستدعاء الاهتزاز من CarController.</summary>
    public static CameraFollow Instance { get; private set; }

    [Header("وضع المطاردة (منظور الشخص الثالث)")]
    [Tooltip("تفعيل كاميرا المطاردة المائلة (false = متابعة علوية كلاسيكية)")]
    public bool thirdPerson = true;

    [Tooltip("زاوية الميلان بالدرجات (0 = مسطحة تماماً، 90 = علوية كاملة)")]
    public float chasePitchDeg = 55f;

    [Tooltip("بعد الكاميرا عن نقطة النظرة (وحدات عالمية)")]
    public float chaseDistance = 9f;

    [Tooltip("مجال الرؤية للكاميرا المنظورية")]
    public float chaseFov = 55f;

    [Tooltip("مسافة نقطة النظرة أمام السيارة (كلما زادت بدت السيارة أسفل الشاشة)")]
    public float chaseLookAhead = 4f;

    [Tooltip("سرعة تتبع دوران الكاميرا لاتجاه السيارة (أكبر = أسرع)")]
    public float yawLerp = 10f;

    [Header("وضع القائمة (Overview)")]
    [Tooltip("بُعد الكاميرا في المحور Z (لا تغيّره)")]
    public float cameraZ = -10f;

    [Tooltip("نعومة متابعة نقطة النظرة")]
    public float smoothTime = 0.1f;

    // --- احتياطي: متابعة علوية كلاسيكية عندما thirdPerson = false ---
    [Tooltip("ثواني التقدم للأمام بناء على سرعة السيارة (0 لإيقافه)")]
    public float lookAheadSeconds = 0.3f;

    [Tooltip("أقصى مسافة تقدم للأمام")]
    public float maxLookAhead = 5f;

    private Transform target;
    private Rigidbody2D targetRb;
    private Camera cam;
    private float followOrthoSize = 7f;
    private bool inOverview;

    // حالة وضع المطاردة
    private Vector3 smoothPivot;
    private Vector3 pivotVel;
    private float smoothYaw;
    private bool yawInit;
    private Vector3 legacyVel;

    // (v0.7.0) اهتزاز التصادم: سعة حالية تتلاشى أُسّياً
    private float shakeAmp;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam != null) followOrthoSize = cam.orthographicSize;

        GameObject car = GameObject.Find("Car");
        if (car != null)
        {
            target = car.transform;
            targetRb = car.GetComponent<Rigidbody2D>();
        }
        else
        {
            Debug.LogWarning("[CameraFollow] لم أجد كائناً باسم 'Car' - تأكد من تسمية السيارة بالضبط.");
        }

        // محاذاة فورية عند بداية المشهد
        if (target != null)
            transform.position = new Vector3(target.position.x, target.position.y, cameraZ);
    }

    /// <summary>
    /// تبديل بين وضعي الكاميرا:
    /// true  = القائمة (علوية أورثوغرافية تؤطر الحلبة)
    /// false = السباق (منظور شخص ثالث منظوري مائل)
    /// </summary>
    public void SetOverviewMode(bool overview)
    {
        inOverview = overview;
        if (cam == null) cam = GetComponent<Camera>();

        if (overview)
        {
            if (cam != null) cam.orthographic = true;
        }
        else
        {
            if (cam != null && thirdPerson)
            {
                cam.orthographic = false;
                cam.fieldOfView = chaseFov;
            }

            // التقاط فوري لنقطة النظرة والاتجاه (بدون انزلاق من وضع القائمة)
            yawInit = false;
            pivotVel = Vector3.zero;
            legacyVel = Vector3.zero;
            if (target != null)
                smoothPivot = target.position + target.up * chaseLookAhead;
        }
    }

    void LateUpdate()
    {
        // ================= وضع القائمة: تأطير الحلبة كاملة =================
        if (inOverview)
        {
            TrackBuilder tb = TrackBuilder.Instance;
            if (tb == null || cam == null) return;

            float needHeight = tb.MapHeightUnits * 0.55f;
            float needWidth = tb.MapWidthUnits / (2f * Mathf.Max(0.8f, cam.aspect)) * 1.02f;
            cam.orthographicSize = Mathf.Max(needHeight, needWidth);

            transform.position = new Vector3(tb.MapCenter.x, tb.MapCenter.y, cameraZ);
            return;
        }

        if (target == null) return;

        // ================= منظور الشخص الثالث: كاميرا مطاردة =================
        if (thirdPerson)
        {
            float dt = Time.deltaTime;

            // نقطة النظرة أمام السيارة مع تنعيم
            Vector3 pivot = target.position + target.up * chaseLookAhead;
            if (!yawInit)
            {
                smoothPivot = pivot;
                smoothYaw = target.rotation.eulerAngles.z;
                yawInit = true;
            }
            smoothPivot = Vector3.SmoothDamp(smoothPivot, pivot, ref pivotVel, smoothTime);

            // دوران الكاميرا مع اتجاه السيارة (تنعيم أُسّي مستقر مع أي معدل إطارات)
            float targetYaw = target.rotation.eulerAngles.z;
            smoothYaw = Mathf.LerpAngle(smoothYaw, targetYaw, 1f - Mathf.Exp(-yawLerp * dt));

            // ميلان حول محور العرض (النظر لأسفل نحو الطريق) ثم دوران مع السيارة:
            // forward = sin(pitch)·اتجاه السيارة + cos(pitch)·نحو المشهد
            // وبالتالي الكاميرا تقع خلف السيارة وعلى جهة المشاهد تماماً
            Quaternion rot = Quaternion.Euler(0f, 0f, smoothYaw) * Quaternion.Euler(-chasePitchDeg, 0f, 0f);
            Vector3 forward = rot * Vector3.forward;
            Vector3 pos = smoothPivot - forward * chaseDistance;

            // (v0.7.0) اهتزاز التصادم: إزاحة عشوائية صغيرة تتلاشى أُسّياً
            if (shakeAmp > 0.001f)
            {
                pos += new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * shakeAmp;
                shakeAmp *= Mathf.Exp(-6f * dt);
            }
            else
            {
                shakeAmp = 0f;
            }

            transform.SetPositionAndRotation(pos, rot);
            return;
        }

        // ================= احتياطي: متابعة علوية كلاسيكية =================
        if (cam != null) cam.orthographicSize = followOrthoSize;

        Vector3 goal = target.position;

        // تقدم بسيط نحو اتجاه الحركة يجعل المتابعة أنعم
        if (targetRb != null && lookAheadSeconds > 0f)
        {
            Vector3 ahead = targetRb.linearVelocity * lookAheadSeconds;
            if (ahead.magnitude > maxLookAhead)
                ahead = ahead.normalized * maxLookAhead;
            goal += ahead;
        }

        goal.z = cameraZ;
        transform.position = Vector3.SmoothDamp(transform.position, goal, ref legacyVel, smoothTime);
    }

    /// <summary>(v0.7.0) دفعة اهتزاز (تُستخدم أكبر قيمة إذا تزامنت دفعات).</summary>
    public void Shake(float amp)
    {
        shakeAmp = Mathf.Max(shakeAmp, amp);
    }
}
