using UnityEngine;

/// <summary>
/// TurboRacing - v1.4.0 (الكراج)
/// مسرح عرض ثلاثي الأبعاد خارج حدود الحلبة (x=214): أرضية مظلمة + منصة دوّارة
/// بحلقة مضيئة + جدار استوديو خلفي + إضاءة نقطتان + سيارة العرض تتقافز الدوران.
/// الكاميرا تُدار من CameraFollow بوضع Garage (موضع ثابت يأطر السيارة).
/// سيارة العرض بلا فيزياء إطلاقاً (عرض بصري فقط عبر CarModels.Build).
/// </summary>
public class GarageStage : MonoBehaviour
{
    public static GarageStage Instance { get; private set; }

    public Vector3 Center { get; private set; }

    public Vector3 CameraPos => Center + new Vector3(0f, 2.7f, -7.6f);
    public Vector3 CameraLook => Center + new Vector3(0f, 0.85f, 0f);

    Transform showcaseRoot;
    int current = -1;
    const float PlatformTopY = 0.18f;

    void Awake()
    {
        Instance = this;
        Center = new Vector3(214f, 0f, 40f); // خارج حدود الحلبة والأرضية العشبية (تنتهي عند x=184)
        BuildStage();
    }

    void BuildStage()
    {
        GameObject stage = new GameObject("GarageRoom");
        stage.transform.SetParent(transform, false);
        stage.isStatic = true;

        var acc = new MeshAccumulator();
        Color floorCol = new Color(0.15f, 0.155f, 0.175f);
        Color platCol  = new Color(0.22f, 0.23f, 0.26f);
        Color wallCol  = new Color(0.12f, 0.13f, 0.16f);
        Color beamCol  = new Color(0.17f, 0.18f, 0.20f);
        Color amber    = new Color(1f, 0.72f, 0.15f);
        Color lampCol  = new Color(0.98f, 0.96f, 0.88f);

        // الأرضية المظلمة (سطحها العلوي عند y=0)
        acc.AddBox(new Vector3(Center.x, -0.10f, Center.z), new Vector3(36f, 0.2f, 36f), 0.5f, floorCol, floorCol * 0.8f);

        // منصة العرض الدوّارة (قرص + حلقة عنبرية مضيئة)
        acc.AddCylinder(new Vector3(Center.x, 0.09f, Center.z), 5.4f, 0.18f, 28, 0.5f, platCol);
        acc.AddCylinder(new Vector3(Center.x, 0.19f, Center.z), 5.42f, 0.06f, 28, 0.5f, amber);
        acc.AddCylinder(new Vector3(Center.x, 0.19f, Center.z), 4.9f, 0.07f, 28, 0.5f, new Color(0.08f, 0.08f, 0.09f));

        // جدار الاستوديو الخلفي (خلف السيارة من جهة الكاميرا) + شريطا هوية
        acc.AddBox(new Vector3(Center.x, 3.4f, Center.z + 16f), new Vector3(36f, 6.8f, 0.7f), 0.5f, wallCol, wallCol * 0.85f);
        acc.AddBox(new Vector3(Center.x, 1.05f, Center.z + 15.6f), new Vector3(36f, 0.28f, 0.1f), 0.5f, amber, amber);
        acc.AddBox(new Vector3(Center.x, 5.6f, Center.z + 15.6f), new Vector3(36f, 0.28f, 0.1f), 0.5f, amber, amber);

        // عمودان جانبيان + جسر علوي + مصباحا تعليق
        acc.AddBox(new Vector3(Center.x - 13.5f, 2.2f, Center.z), new Vector3(0.5f, 4.4f, 0.5f), 0.5f, beamCol, beamCol * 0.85f);
        acc.AddBox(new Vector3(Center.x + 13.5f, 2.2f, Center.z), new Vector3(0.5f, 4.4f, 0.5f), 0.5f, beamCol, beamCol * 0.85f);
        acc.AddBox(new Vector3(Center.x, 4.55f, Center.z), new Vector3(27.5f, 0.5f, 2.2f), 0.5f, beamCol, beamCol * 0.85f);
        acc.AddBox(new Vector3(Center.x - 3f, 4.1f, Center.z), new Vector3(1.6f, 0.22f, 1.6f), 0.5f, lampCol, lampCol * 0.9f);
        acc.AddBox(new Vector3(Center.x + 3f, 4.1f, Center.z), new Vector3(1.6f, 0.22f, 1.6f), 0.5f, lampCol, lampCol * 0.9f);

        // شبكة واحدة بمادة ألوان رأسية (نفس منهجية TrackBuilder)
        Shader sh = Shader.Find("TR/Paint");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Legacy Shaders/Diffuse");
        Material mat = new Material(sh);
        mat.name = "GarageStage_Mat";

        MeshFilter mf = stage.AddComponent<MeshFilter>();
        mf.sharedMesh = acc.Build("GarageStage_Mesh");
        MeshRenderer mr = stage.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        mr.receiveShadows = true;

        // إضاءة نقطتان (بدون ظلال — أداء الموبايل)
        AddPointLight(new Vector3(Center.x - 3f, 3.9f, Center.z - 1f), 1.15f, 22f, new Color(1f, 0.97f, 0.90f));
        AddPointLight(new Vector3(Center.x + 3f, 3.9f, Center.z + 1f), 0.85f, 18f, new Color(0.92f, 0.95f, 1f));
    }

    void AddPointLight(Vector3 pos, float intensity, float range, Color color)
    {
        GameObject go = new GameObject("GarageLight");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;

        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
    }

    /// <summary>عرض موديل في منصة الكراج (يعاد البناء عند تغيير الاختيار فقط).</summary>
    public void Show(int modelIndex)
    {
        int idx = Mathf.Clamp(modelIndex, 0, CarModels.Count - 1);
        if (idx == current && showcaseRoot != null) return;

        if (showcaseRoot != null)
            DestroyImmediate(showcaseRoot.gameObject);

        CarModels.Handles h = CarModels.Build(transform, CarModels.Get(idx));
        showcaseRoot = h.Root;
        showcaseRoot.position = Center + new Vector3(0f, PlatformTopY, 0f);
        showcaseRoot.rotation = Quaternion.Euler(0f, 25f, 0f);
        current = idx;
    }

    void Update()
    {
        // دوران عرضي بطيء يبرز الهيكل من كل الجهات
        if (showcaseRoot != null)
            showcaseRoot.Rotate(0f, 14f * Time.deltaTime, 0f, Space.Self);
    }
}
