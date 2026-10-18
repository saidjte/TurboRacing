using UnityEngine;

/// <summary>
/// TurboRacing - المرحلة 1 (مطوّرة في المرحلة 4)
/// كاميرا تتبع السيارة بسلاسة مع توقع بسيط لاتجاه الحركة (Look-Ahead).
/// وضع جديد في المرحلة 4: وضع القائمة (Overview) يؤطر الحلبة كاملة
/// بدل تتبع السيارة - يستخدمه RaceFlow في القائمة الرئيسية.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Tooltip("بُعد الكاميرا في المحور Z (لا تغيّره)")]
    public float cameraZ = -10f;

    [Tooltip("نعومة المتابعة (قيمة أصغر = متابعة أسرع)")]
    public float smoothTime = 0.12f;

    [Tooltip("ثواني التقدم للأمام بناء على سرعة السيارة (0 لإيقافه)")]
    public float lookAheadSeconds = 0.3f;

    [Tooltip("أقصى مسافة تقدم للأمام")]
    public float maxLookAhead = 5f;

    private Transform target;
    private Rigidbody2D targetRb;
    private Vector3 currentVelocity;
    private Camera cam;
    private float followSize = 7f;
    private bool inOverview;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam != null) followSize = cam.orthographicSize;

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

        // محاذاة فورية فوق السيارة عند بداية المشهد
        if (target != null)
            transform.position = new Vector3(target.position.x, target.position.y, cameraZ);
    }

    /// <summary>
    /// تبديل بين تتبع السيارة (false) وتأطير الحلبة كاملة (true - وضع القائمة).
    /// عند العودة للتتبع: تقريب فوري فوق السيارة دون انزلاق عبر الحلبة.
    /// </summary>
    public void SetOverviewMode(bool overview)
    {
        inOverview = overview;

        if (!overview && target != null)
        {
            transform.position = new Vector3(target.position.x, target.position.y, cameraZ);
            currentVelocity = Vector3.zero;
        }
    }

    void LateUpdate()
    {
        // وضع القائمة: ثبات على مركز الحلبة مع ملاءمة الإطار لنسبة الشاشة
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

        if (cam != null) cam.orthographicSize = followSize;
        if (target == null) return;

        Vector3 goal = target.position;

        // تقدم بسيط نحو اتجاه الحركة يجعل المتابعة أنعم وأكثر احترافية
        if (targetRb != null && lookAheadSeconds > 0f)
        {
            Vector3 ahead = targetRb.linearVelocity * lookAheadSeconds;
            if (ahead.magnitude > maxLookAhead)
                ahead = ahead.normalized * maxLookAhead;
            goal += ahead;
        }

        goal.z = cameraZ;
        transform.position = Vector3.SmoothDamp(transform.position, goal, ref currentVelocity, smoothTime);
    }
}
