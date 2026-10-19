using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TurboRacing - v1.0.0 (التحويل إلى ثلاثية أبعاد حقيقية)
/// مُراكم شبكات (Mesh Accumulator): يجمع صناديق/أرباعاً كثيرة في شبكة واحدة
/// ليُرسَل كل النوع الواحد بدوران رسم واحد — أساس أداء الموبايل في المشهد المجسّم.
/// - رؤوس بفضاء العالم مباشرة (الشبكة النهائية عند نقطة الأصل دون إزاحة)
/// - ألوان رأسية لكل وجه (تُضرب بلون المادة في ستاندر شيدر) لتلوين مجاني
/// - winding مضبوط يدوياً لكل اتجاه وجه (Unity: عقارب الساعة = الوجه الأمامي)
/// </summary>
public class MeshAccumulator
{
    readonly List<Vector3> verts = new List<Vector3>(4096);
    readonly List<Vector2> uvs = new List<Vector2>(4096);
    readonly List<Color> cols = new List<Color>(4096);
    readonly List<int> tris = new List<int>(8192);

    public int VertexCount => verts.Count;

    /// <summary>
    /// إضافة ربعاً بأربع رؤوس بترتيب يحدد اتجاه الوجه (Unity: الوجه الأمامي
    /// يظهر عند رؤية الرؤوس بترتيب عقارب الساعة). المثلثان (0,1,2)(0,2,3).
    /// </summary>
    public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                        Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD, Color col)
    {
        int i = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
        uvs.Add(uvA); uvs.Add(uvB); uvs.Add(uvC); uvs.Add(uvD);
        cols.Add(col); cols.Add(col); cols.Add(col); cols.Add(col);
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
    }

    /// <summary>ربع علوي (الوجه نحو +Y): (x0,z0)→(x0,z1)→(x1,z1)→(x1,z0).</summary>
    public void AddTop(float x0, float x1, float z0, float z1, float y, float uvScale, Color col)
    {
        float k = uvScale;
        AddQuad(new Vector3(x0, y, z0), new Vector3(x0, y, z1),
                new Vector3(x1, y, z1), new Vector3(x1, y, z0),
                new Vector2(x0 * k, z0 * k), new Vector2(x0 * k, z1 * k),
                new Vector2(x1 * k, z1 * k), new Vector2(x1 * k, z0 * k), col);
    }

    /// <summary>
    /// صندوق محاذٍ للمحاور بستة أوجه كاملة. الأشعة المتناسبة مع uvScale تتحكم
    /// في تكرار النسيج (بالعالم: المتر). topCol/sideCol يضيئان السطح عن الجوانب.
    /// </summary>
    public void AddBox(Vector3 center, Vector3 size, float uvScale, Color topCol, Color sideCol)
    {
        float x0 = center.x - size.x * 0.5f, x1 = center.x + size.x * 0.5f;
        float y0 = center.y - size.y * 0.5f, y1 = center.y + size.y * 0.5f;
        float z0 = center.z - size.z * 0.5f, z1 = center.z + size.z * 0.5f;
        float k = uvScale;

        // الوجه العلوي (+Y) والسفلي (-Y)
        AddTop(x0, x1, z0, z1, y1, k, topCol);
        AddQuad(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0),
                new Vector3(x1, y0, z1), new Vector3(x0, y0, z1),
                new Vector2(x0 * k, z0 * k), new Vector2(x1 * k, z0 * k),
                new Vector2(x1 * k, z1 * k), new Vector2(x0 * k, z1 * k),
                sideCol * 0.7f); // السفلي داكناً دائماً

        // الوجوه الجانبية: كل وجه برؤوسه بترتيب winding الصحيح لاتجاه نورماله
        AddQuad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0),   // +X
                new Vector3(x1, y1, z1), new Vector3(x1, y0, z1),
                new Vector2(z0 * k, y0 * k), new Vector2(z0 * k, y1 * k),
                new Vector2(z1 * k, y1 * k), new Vector2(z1 * k, y0 * k), sideCol);

        AddQuad(new Vector3(x0, y0, z0), new Vector3(x0, y0, z1),   // -X
                new Vector3(x0, y1, z1), new Vector3(x0, y1, z0),
                new Vector2(z0 * k, y0 * k), new Vector2(z1 * k, y0 * k),
                new Vector2(z1 * k, y1 * k), new Vector2(z0 * k, y1 * k), sideCol);

        AddQuad(new Vector3(x0, y0, z1), new Vector3(x1, y0, z1),   // +Z
                new Vector3(x1, y1, z1), new Vector3(x0, y1, z1),
                new Vector2(x0 * k, y0 * k), new Vector2(x1 * k, y0 * k),
                new Vector2(x1 * k, y1 * k), new Vector2(x0 * k, y1 * k), sideCol);

        AddQuad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0),   // -Z
                new Vector3(x1, y1, z0), new Vector3(x1, y0, z0),
                new Vector2(x0 * k, y0 * k), new Vector2(x0 * k, y1 * k),
                new Vector2(x1 * k, y1 * k), new Vector2(x1 * k, y0 * k), sideCol);
    }

    /// <summary>أسطوانة (لعجلات/أشجار): محور Y، نصف قطر r، ارتفاع h، تقسيمات قليلة.</summary>
    public void AddCylinder(Vector3 center, float r, float h, int segments, float uvScale, Color col)
    {
        float y0 = center.y - h * 0.5f, y1 = center.y + h * 0.5f;
        float k = uvScale;

        for (int i = 0; i < segments; i++)
        {
            float a0 = (i / (float)segments) * Mathf.PI * 2f;
            float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;
            Vector3 bl = center + new Vector3(Mathf.Cos(a0) * r, y0 - center.y, Mathf.Sin(a0) * r);
            Vector3 br = center + new Vector3(Mathf.Cos(a1) * r, y0 - center.y, Mathf.Sin(a1) * r);
            Vector3 tl = center + new Vector3(Mathf.Cos(a0) * r, y1 - center.y, Mathf.Sin(a0) * r);
            Vector3 tr = center + new Vector3(Mathf.Cos(a1) * r, y1 - center.y, Mathf.Sin(a1) * r);

            AddQuad(bl, tl, tr, br,  // (bl,tl,tr,br) = winding خارجي (مُثبت بالاختبار)
                    new Vector2(a0 * r * k, y0 * k), new Vector2(a0 * r * k, y1 * k),
                    new Vector2(a1 * r * k, y1 * k), new Vector2(a1 * r * k, y0 * k), col);

            // قرص علوي (مروحة مثلثات نحو +Y): (cTop, br, bl) يعطي نورمال +Y
            Vector3 cTop = new Vector3(center.x, y1, center.z);
            int s = verts.Count;
            verts.Add(cTop);
            uvs.Add(new Vector2(center.x * k, center.z * k));
            cols.Add(col);
            int b0 = verts.Count; verts.Add(bl); uvs.Add(new Vector2(bl.x * k, bl.z * k)); cols.Add(col);
            int b1 = verts.Count; verts.Add(br); uvs.Add(new Vector2(br.x * k, br.z * k)); cols.Add(col);
            tris.Add(s); tris.Add(b1); tris.Add(b0);

            // قرص سفلي نحو -Y: (cBot, bl, br) يعطي نورمال -Y
            Vector3 cBot = new Vector3(center.x, y0, center.z);
            s = verts.Count; verts.Add(cBot);
            uvs.Add(new Vector2(center.x * k, center.z * k)); cols.Add(col);
            b0 = verts.Count; verts.Add(bl); uvs.Add(new Vector2(bl.x * k, bl.z * k)); cols.Add(col);
            b1 = verts.Count; verts.Add(br); uvs.Add(new Vector2(br.x * k, br.z * k)); cols.Add(col);
            tris.Add(s); tris.Add(b0); tris.Add(b1);
        }
    }

    /// <summary>كرة منخفضة المضلعات (تُيجات الأشجار): squishY لهرسها عمودياً.</summary>
    public void AddSphere(Vector3 center, float r, int lonSeg, int latSeg, Color col, float squishY = 1f)
    {
        for (int iy = 0; iy < latSeg; iy++)
        {
            float phi0 = (iy / (float)latSeg) * Mathf.PI;
            float phi1 = ((iy + 1) / (float)latSeg) * Mathf.PI;

            for (int ix = 0; ix < lonSeg; ix++)
            {
                float t0 = (ix / (float)lonSeg) * Mathf.PI * 2f;
                float t1 = ((ix + 1) / (float)lonSeg) * Mathf.PI * 2f;

                Vector3 P(float t, float phi)
                {
                    return center + new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(t) * r,
                        Mathf.Cos(phi) * r * squishY,
                        Mathf.Sin(phi) * Mathf.Sin(t) * r);
                }

                Vector3 bl = P(t0, phi1), tl = P(t0, phi0), tr = P(t1, phi0), br = P(t1, phi1);
                AddQuad(bl, tl, tr, br,
                        new Vector2(ix / (float)lonSeg, iy / (float)latSeg),
                        new Vector2(ix / (float)lonSeg, (iy + 1) / (float)latSeg),
                        new Vector2((ix + 1) / (float)lonSeg, (iy + 1) / (float)latSeg),
                        new Vector2((ix + 1) / (float)lonSeg, iy / (float)latSeg), col);
            }
        }
    }

    /// <summary>تجميع الشبكة النهائية (normal تلقائية + مجال مرن للإسقاط).</summary>
    public Mesh Build(string name)
    {
        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.indexFormat = verts.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
