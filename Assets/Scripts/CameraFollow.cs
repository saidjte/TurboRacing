using UnityEngine;

/// <summary>
/// TurboRacing - المرحلة 1
/// كاميرا تتبع السيارة بسلاسة مع توقع بسيط لاتجاه الحركة (Look-Ahead).
/// تربط نفسها تلقائياً بالسيارة عبر البحث عن الاسم "Car".
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

    void Start()
    {
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

    void LateUpdate()
    {
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
