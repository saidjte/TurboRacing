using UnityEngine;

/// <summary>
/// TurboRacing - v1.4.0 (زيادة عدد السيارات: منافسون أحياء على الحلبة)
/// سائق ذكاء اصطناعي يقود سيارة Prometeo على خط السباق (RacingLine):
/// - نقطة هدف أمام السيارة بمسافة تتوسع مع السرعة (أسلوب المضامير الأركيد)
/// - التوجيه: زاوية الهدف عن مقدمة السيارة تقرر TurnLeft/TurnRight/ResetSteeringAngle
/// - السرعة: بنزين مستمر بسقف مهارة + فرملة استباقية عند انحناء الطريق القادم
/// - حارة جانبية متمايزة لكل منافس (لا يتكدسون على الخط نفسه)
/// - إنقاذ ذاتي عند العلوق: رجوع قصير ثم استئناف
/// يعمل فقط خلال Racing/Finished (يتجمد تلقائياً في العد/الإيقاف/الكراج).
/// </summary>
[RequireComponent(typeof(CarController))]
public class AiDriver : MonoBehaviour
{
    CarController car;
    TrackBuilder track;

    CarModels.CarSpec spec;
    float skill = 0.9f;
    float laneOffset;

    int wpIdx;
    float stuckTimer;
    float reverseTimer;
    bool recovering;

    public void Init(CarModels.CarSpec carSpec, float skillMul, float lane)
    {
        spec = carSpec;
        skill = Mathf.Clamp(skillMul, 0.6f, 1f);
        laneOffset = Mathf.Clamp(lane, -5f, 5f);
    }

    void Start()
    {
        car = GetComponent<CarController>();
        track = TrackBuilder.Instance;
        if (track != null && track.RacingLine != null)
            wpIdx = track.NearestWaypoint(transform.position, 0);
    }

    void FixedUpdate()
    {
        if (car == null || track == null || track.RacingLine == null || track.RacingLine.Length < 10)
            return;

        RaceFlow rf = RaceFlow.Instance;
        if (rf == null) return;

        // خارج السباق (قائمة/كراج/عد تنازلي/إيقاف): لا أوامر — تبقى السيارة مكانها
        if (rf.State != RaceState.Racing && rf.State != RaceState.Finished)
        {
            car.AiNeutral();
            return;
        }

        // --- إنقاذ العلوق: رجوع قصير ---
        if (recovering)
        {
            reverseTimer -= Time.fixedDeltaTime;
            car.AiReverse();
            if (reverseTimer <= 0f || car.SpeedKmh > 6f)
            {
                recovering = false;
                stuckTimer = 0f;
                car.AiNeutral();
            }
            return;
        }

        if (car.SpeedKmh < 1.6f) stuckTimer += Time.fixedDeltaTime;
        else stuckTimer = 0f;

        if (stuckTimer > 2.4f)
        {
            recovering = true;
            reverseTimer = 1.3f;
            stuckTimer = 0f;
            return;
        }

        DriveLine();
    }

    void DriveLine()
    {
        Vector3 pos = transform.position; pos.y = 0f;

        // تقدّم البحث حول آخر فهرس معروف (بدون مسح كامل الخط كل إطار)
        wpIdx = track.NearestWaypoint(pos, wpIdx);

        // مسافة الاستباق تتوسع مع السرعة = توجيه مبكر بسرعات عالية
        float speed01 = Mathf.Clamp01(car.SpeedKmh / Mathf.Max(1f, spec.topSpeed));
        float look = 6.5f + speed01 * 8.5f;

        // نقطة الهدف على خط السباق مع إزاحة الحارة الجانبية
        Vector3 target = track.PointAlongLine(wpIdx, look, laneOffset);

        // زاوية الهدف بالنسبة لمقدمة السيارة
        Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 dir = target - pos; dir.y = 0f;
        float dist = Mathf.Max(0.001f, dir.magnitude);
        dir /= dist;
        float angle = Mathf.Atan2(Vector3.Dot(right, dir), Vector3.Dot(fwd, dir)) * Mathf.Rad2Deg;

        // فرملة استباقية: مجموع تغير الاتجاه أمامنا يتجاوز حد المنعطف
        float curveAhead = track.LineHeadingChange(wpIdx, look * 2.2f);
        float cap = spec.topSpeed * skill;

        if (curveAhead > 30f && car.SpeedKmh > cap * 0.55f)
            car.AiBrake();
        else
            car.AiThrottle(cap);

        if (angle > 4.5f) car.AiSteer(+1);
        else if (angle < -4.5f) car.AiSteer(-1);
        else car.AiSteer(0);
    }
}
