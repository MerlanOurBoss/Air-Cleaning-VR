using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Достраивает трубу от неподвижного конца обвязки цеха до патрубка эмульгатора.
/// Эмульгатор меняет размер (EmulsifierSizer), его патрубки едут вместе с ним, а этот участок
/// перестраивается: прямые отрезки вдоль осей с коленами, как у настоящей обвязки.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class PipeConnector : MonoBehaviour
{
    [Tooltip("Патрубок на эмульгаторе; ось Z (forward) смотрит от аппарата.")]
    [SerializeField] private Transform emulsifierPort;
    [Tooltip("Конец неподвижной трубы цеха; ось Z смотрит в сторону эмульгатора.")]
    [SerializeField] private Transform fixedPort;
    [SerializeField] private float radius = 0.15f;
    [Tooltip("Прямой участок у каждого конца перед первым коленом, м.")]
    [SerializeField] private float stub = 0.15f;
    [Tooltip("Порядок поворотов от эмульгатора к цеху: X, Y, Z — выйти на координату конца цеха; " +
             "H — подняться на высоту выше обоих концов.")]
    [SerializeField] private string route = "XZY";
    [Tooltip("Радиус фланца у патрубка эмульгатора; 0 — без фланца.")]
    [SerializeField] private float flangeRadius;
    [SerializeField] private int sides = 16;

    private const float FlangeThickness = 0.03f;
    private const float MoveThreshold = 0.0005f;

    private Mesh _mesh;
    private Vector3 _lastA = Vector3.positiveInfinity;
    private Vector3 _lastB = Vector3.positiveInfinity;

    private readonly List<Vector3> _vertices = new List<Vector3>();
    private readonly List<Vector3> _normals = new List<Vector3>();
    private readonly List<Vector2> _uvs = new List<Vector2>();
    private readonly List<int> _triangles = new List<int>();

    private void OnEnable()
    {
        _lastA = _lastB = Vector3.positiveInfinity;
        Rebuild();
    }

    private void OnDisable()
    {
        if (_mesh == null) return;
        if (Application.isPlaying) Destroy(_mesh);
        else DestroyImmediate(_mesh);
        _mesh = null;
    }

    private void LateUpdate()
    {
        if (emulsifierPort == null || fixedPort == null) return;
        if ((emulsifierPort.position - _lastA).sqrMagnitude > MoveThreshold * MoveThreshold ||
            (fixedPort.position - _lastB).sqrMagnitude > MoveThreshold * MoveThreshold)
            Rebuild();
    }

    private void OnValidate()
    {
        _lastA = _lastB = Vector3.positiveInfinity;
    }

    public void Rebuild()
    {
        if (emulsifierPort == null || fixedPort == null) return;

        if (_mesh == null)
        {
            _mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        _lastA = emulsifierPort.position;
        _lastB = fixedPort.position;

        var path = BuildPath(_lastA, emulsifierPort.forward, _lastB, fixedPort.forward);

        _vertices.Clear(); _normals.Clear(); _uvs.Clear(); _triangles.Clear();
        float v = 0f;
        for (int i = 0; i < path.Count - 1; i++)
        {
            AddTube(path[i], path[i + 1], radius, v, false);
            v += Vector3.Distance(path[i], path[i + 1]);
        }
        for (int i = 1; i < path.Count - 1; i++)
            AddSphere(path[i], radius);
        if (flangeRadius > 0f)
            AddTube(path[0], path[0] + (path[1] - path[0]).normalized * FlangeThickness, flangeRadius, 0f, true);

        // Вершины считаются в мировых координатах, а меш живёт в локальных координатах объекта.
        for (int i = 0; i < _vertices.Count; i++)
        {
            _vertices[i] = transform.InverseTransformPoint(_vertices[i]);
            _normals[i] = transform.InverseTransformDirection(_normals[i]).normalized;
        }

        _mesh.Clear();
        _mesh.SetVertices(_vertices);
        _mesh.SetNormals(_normals);
        _mesh.SetUVs(0, _uvs);
        _mesh.SetTriangles(_triangles, 0);
        _mesh.RecalculateBounds();
        _mesh.RecalculateTangents();

        // MeshCollider не замечает изменений меша сам — переназначаем.
        var meshCollider = GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = _mesh;
        }
    }

    private List<Vector3> BuildPath(Vector3 a, Vector3 dirA, Vector3 b, Vector3 dirB)
    {
        var a1 = a + dirA.normalized * stub;
        var b1 = b + dirB.normalized * stub;
        var raw = new List<Vector3> { a, a1 };

        var p = a1;
        foreach (char c in route)
        {
            switch (char.ToUpperInvariant(c))
            {
                case 'X': p.x = b1.x; break;
                case 'Y': p.y = b1.y; break;
                case 'Z': p.z = b1.z; break;
                case 'H': p.y = Mathf.Max(a1.y, b1.y); break;
                default: continue;
            }
            raw.Add(p);
        }
        raw.Add(b1);
        raw.Add(b);

        // Убираем нулевые отрезки и точки посреди прямой, чтобы не было лишних колен.
        var path = new List<Vector3> { raw[0] };
        for (int i = 1; i < raw.Count; i++)
        {
            if ((raw[i] - path[path.Count - 1]).sqrMagnitude < 1e-6f) continue;
            if (path.Count >= 2)
            {
                var d0 = (path[path.Count - 1] - path[path.Count - 2]).normalized;
                var d1 = (raw[i] - path[path.Count - 1]).normalized;
                if (Vector3.Dot(d0, d1) > 0.9999f) path.RemoveAt(path.Count - 1);
            }
            path.Add(raw[i]);
        }
        return path;
    }

    private void AddTube(Vector3 p0, Vector3 p1, float r, float v0, bool caps)
    {
        var axis = p1 - p0;
        float length = axis.magnitude;
        if (length < 1e-5f) return;
        axis /= length;
        var side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
        var up = Vector3.Cross(axis, side);
        float circumference = 2f * Mathf.PI * r;

        int start = _vertices.Count;
        for (int k = 0; k <= sides; k++)
        {
            float a = k * 2f * Mathf.PI / sides;
            var n = side * Mathf.Cos(a) + up * Mathf.Sin(a);
            _vertices.Add(p0 + n * r); _normals.Add(n); _uvs.Add(new Vector2(circumference * k / sides, v0));
            _vertices.Add(p1 + n * r); _normals.Add(n); _uvs.Add(new Vector2(circumference * k / sides, v0 + length));
        }
        for (int k = 0; k < sides; k++)
        {
            int i = start + k * 2;
            _triangles.Add(i); _triangles.Add(i + 3); _triangles.Add(i + 1);
            _triangles.Add(i); _triangles.Add(i + 2); _triangles.Add(i + 3);
        }
        if (!caps) return;

        foreach (var (center, normal) in new[] { (p0, -axis), (p1, axis) })
        {
            int c = _vertices.Count;
            _vertices.Add(center); _normals.Add(normal); _uvs.Add(Vector2.zero);
            for (int k = 0; k <= sides; k++)
            {
                float a = k * 2f * Mathf.PI / sides;
                var n = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                _vertices.Add(center + n * r); _normals.Add(normal); _uvs.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            bool front = normal == axis;
            for (int k = 0; k < sides; k++)
            {
                _triangles.Add(c);
                _triangles.Add(c + 1 + (front ? k : k + 1));
                _triangles.Add(c + 1 + (front ? k + 1 : k));
            }
        }
    }

    private void AddSphere(Vector3 center, float r)
    {
        int rings = Mathf.Max(4, sides / 2);
        int start = _vertices.Count;
        for (int j = 0; j <= rings; j++)
        {
            float phi = Mathf.PI * j / rings;
            for (int k = 0; k <= sides; k++)
            {
                float theta = 2f * Mathf.PI * k / sides;
                var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                _vertices.Add(center + n * r); _normals.Add(n);
                _uvs.Add(new Vector2(2f * Mathf.PI * r * k / sides, Mathf.PI * r * j / rings));
            }
        }
        for (int j = 0; j < rings; j++)
        {
            for (int k = 0; k < sides; k++)
            {
                int i = start + j * (sides + 1) + k;
                int below = i + sides + 1;
                _triangles.Add(i); _triangles.Add(below); _triangles.Add(i + 1);
                _triangles.Add(i + 1); _triangles.Add(below); _triangles.Add(below + 1);
            }
        }
    }
}
