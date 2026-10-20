using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TurboRacing - v1.4.0 (زيادة عدد السيارات: شبكة انطلاق وترتيب حي)
/// يولّد شبكة انطلاق خلف اللاعب: 4 منافسين بموديلات/mهارات/حارات مختلفة،
/// ويحسب تقدّم كل سيارة على خط السباق لإظهار المركز (P1/P5) في الـ HUD والنتائج.
/// التقدّم = لفات×عدد نقاط الخط + أقرب نقطة + كسر المقطع (دقة مترية كافية للترتيب).
/// </summary>
public class AiManager : MonoBehaviour
{
    public static AiManager Instance { get; private set; }

    class Racer
    {
        public Transform t;
        public bool isPlayer;
        public int lastWp;
        public int laps;
        public float progress;
    }

    readonly List<Racer> racers = new List<Racer>();
    readonly List<GameObject> spawnedCars = new List<GameObject>();

    // مهارات المنافسين (سقف السرعة نسبةً إلى مواصفات الموديل) — الأقوى يبدأ الأقرب للاعب
    static readonly float[] Skills = { 0.965f, 0.925f, 0.885f, 0.85f };
    static readonly float[] Lanes  = { 2.4f, -2.4f, 3.9f, -3.9f };

    public int FieldSize => racers.Count;

    void Awake()
    {
        Instance = this;
    }

    /// <summary>توليد شبكة الانطلاق عند بدء سباق (يستدعيها RaceFlow.StartRace).</summary>
    public void SpawnGrid()
    {
        DespawnAll();

        TrackBuilder track = TrackBuilder.Instance;
        if (track == null || track.RacingLine == null || track.RacingLine.Length < 10)
        {
            Debug.LogWarning("[AiManager] خط السباق غير جاهز — لن يوجد منافسون.");
            return;
        }

        // اللاعب أول المتسابقين دائماً
        GameObject player = GameObject.Find("Car");
        if (player == null) return;

        racers.Add(new Racer
        {
            t = player.transform,
            isPlayer = true,
            lastWp = track.NearestWaypoint(player.transform.position, 0),
            laps = 0
        });

        // شبكة الانطلاق خلف اللاعب (v1.6.0): مواضع مشتقة من StartPosition/StartYaw
        // بدل إحداثيات ثابتة لحلبة قديمة — تعمل على أي حلبة (البيضاوية والإجرائية):
        // صفاً خلف بعض بجانبين متبادلين على عرض الطريق.
        // v1.8.0: تباعد أكبر (10/18م) وحارات أوسع (±3.5/±5.5م) — الطريق 22.5م
        // يتسع لصفّ متباعد واضح، فلا تبدو السيارات «مكدسة» عند العد التنازلي.
        Vector3 startPos = track.StartPosition;
        Quaternion startRot = Quaternion.Euler(0f, track.StartYaw, 0f);
        Vector3 fwd = startRot * Vector3.forward;
        Vector3 right = startRot * Vector3.right;

        float[] back  = { 10f, 10f, 18f, 18f };
        float[] lanes = { 3.5f, -3.5f, 5.5f, -5.5f };

        // خلط مواصفات الموديلات 1..N واختيار أربعة
        List<int> pool = new List<int>();
        for (int i = 1; i < CarModels.Count; i++) pool.Add(i);

        var rng = new System.Random();
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            int tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
        }

        for (int i = 0; i < slotsCount && i < pool.Count; i++)
        {
            CarModels.CarSpec spec = CarModels.Get(pool[i]);

            Vector3 slot = startPos - fwd * back[i] + right * lanes[i];

            // v1.8.0: ضمان خلوّ الخانة من أي مصادم آخر (طبقة Default = السيارات فقط،
            // الحلبة على طبقة منفصلة) — إن احتلّت، نتأخر 4م خلفياً حتى نجد فراغاً.
            int clears = 0;
            while (clears++ < 6 &&
                   Physics.OverlapBox(slot + Vector3.up * 0.5f,
                       new Vector3(1.7f, 1.2f, 2.7f), startRot, 1 << 0).Length > 0)
            {
                slot -= fwd * 4f;
            }

            // v1.7.0: ارتفاع الأرض الفعلي تحت الخانة (سطح الحلبة + 0.4م مثل الانطلاق)
            // — المستقيمات المكبّرة قد تميل فلا يكفي y نقطة الانطلاق لكل الصفوف
            float slotY = startPos.y;
            float ground = track.SurfaceHeightAt(slot.x, slot.z);
            if (ground > -50f) slotY = ground + 0.4f;

            GameObject go = new GameObject("AiCar_" + (i + 1));
            go.transform.position = new Vector3(slot.x, slotY, slot.z);
            go.transform.rotation = startRot; // نفس اتجاه اللاعب

            go.AddComponent<Rigidbody>();
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.45f, 0.05f);
            box.size = new Vector3(CarModel3D.ColliderWidth, CarModel3D.ColliderHeight, CarModel3D.ColliderLength);

            CarController ctrl = go.AddComponent<CarController>();
            ctrl.SetAi(spec, Skills[i], Lanes[i]);

            AiDriver driver = go.AddComponent<AiDriver>();
            driver.Init(spec, Skills[i], Lanes[i]);

            spawnedCars.Add(go);

            racers.Add(new Racer
            {
                t = go.transform,
                isPlayer = false,
                lastWp = track.NearestWaypoint(slot, 0),
                laps = 0
            });

            Debug.Log($"[AiManager] منافس {i + 1}: {spec.name} (مهارة {Skills[i]:0.00})");
        }
    }

    const int slotsCount = 4;

    /// <summary>
    /// (v1.7.0) تجميد/تحرير سيارات المنافسين — علاج «تتصادم قبل بداية السباق»:
    /// كانت تُولد أثناء العد التنازلي Rigidbody حرة (التجميد كان لسيارة اللاعب فقط)
    /// فتهبط وتتنافر وتتدافع قبل GO. الآن تُجمّد كلياً أثناء العد وتُفك عند الانطلاق.
    /// </summary>
    public void SetFrozen(bool frozen)
    {
        foreach (GameObject go in spawnedCars)
        {
            if (go == null) continue;

            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null) continue;

            if (frozen)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }
            else
            {
                rb.constraints = RigidbodyConstraints.None;
            }
        }
    }

    /// <summary>إزالة كل المنافسين (العودة للقائمة/بدء سباق جديد).</summary>
    public void DespawnAll()
    {
        foreach (GameObject go in spawnedCars)
            if (go != null) Destroy(go);
        spawnedCars.Clear();
        racers.Clear();
    }

    void FixedUpdate()
    {
        TrackBuilder track = TrackBuilder.Instance;
        if (track == null || track.RacingLine == null || racers.Count == 0) return;

        RaceFlow rf = RaceFlow.Instance;
        if (rf == null) return;
        if (rf.State != RaceState.Racing && rf.State != RaceState.Finished) return;

        int n = track.RacingLine.Length;

        foreach (Racer r in racers)
        {
            if (r.t == null) continue;

            int idx = track.NearestWaypoint(r.t.position, r.lastWp);

            // لفة جديدة عند العبور الأمامي عبر بداية الخط (ارتداد = خصم)
            if (r.lastWp > n - 8 && idx < 8) r.laps++;
            else if (r.lastWp < 8 && idx > n - 8 && r.laps > 0) r.laps--;
            r.lastWp = idx;

            // كسر المقطع لدقة ترتيب أعلى داخل نفس النقطة
            Vector3 p0 = track.RacingLine[idx];
            Vector3 p1 = track.RacingLine[(idx + 1) % n];
            Vector3 seg = p1 - p0; seg.y = 0f;
            float lenSq = Mathf.Max(0.0001f, seg.sqrMagnitude);
            Vector3 d = r.t.position - p0; d.y = 0f;
            float t = Mathf.Clamp01(Vector3.Dot(d, seg) / lenSq);

            r.progress = r.laps * n + idx + t;
        }
    }

    /// <summary>مركز اللاعب الحالي (1 = الصدارة، FieldSize = الأخير).</summary>
    public int PlayerPosition
    {
        get
        {
            if (racers.Count == 0) return 1;

            float playerProgress = float.MinValue;
            foreach (Racer r in racers)
                if (r.isPlayer) { playerProgress = r.progress; break; }

            if (playerProgress <= float.MinValue + 1f) return 1;

            int better = 0;
            foreach (Racer r in racers)
                if (!r.isPlayer && r.progress > playerProgress) better++;

            return better + 1;
        }
    }
}
