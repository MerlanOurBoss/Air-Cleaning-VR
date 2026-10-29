using UnityEngine;

/// <summary>
/// Масштабирует модель эмульгатора по уровню «Газа установки» (EmulsifierCalculation.MainGasFlow):
/// каждый следующий уровень +10% к предыдущему, переход на максимальный уровень — +30%.
/// Ширина и высота растут одинаково, низ модели остаётся на полу.
/// </summary>
public class EmulsifierSizer : MonoBehaviour
{
    [SerializeField] private EmulsifierCalculation calculation;
    [Tooltip("Прирост размера за каждый шаг «Газа установки».")]
    [SerializeField] private float stepGrowth = 0.10f;
    [Tooltip("Прирост размера при переходе на максимальный уровень.")]
    [SerializeField] private float maxStepGrowth = 0.30f;
    [Tooltip("Время плавного изменения размера, с.")]
    [SerializeField] private float resizeTime = 0.8f;

    private Vector3 _baseScale;
    private Vector3 _basePosition;
    // Высота низа модели над опорной точкой объекта: точка лежит ниже пола,
    // и без поправки при росте установка поднималась бы над полом.
    private float _bottomOffset;
    private DropSpawner[] _spawners;
    private float[] _baseGravity;
    private float _factor = 1f;
    private float _targetFactor = 1f;
    private float _velocity;

    private void Awake()
    {
        _baseScale = transform.localScale;
        _basePosition = transform.localPosition;

        float bottom = float.PositiveInfinity;
        foreach (var r in GetComponentsInChildren<MeshRenderer>())
            if (r.enabled) bottom = Mathf.Min(bottom, r.bounds.min.y);
        _bottomOffset = float.IsInfinity(bottom) ? 0f : bottom - transform.position.y;

        // Гравитация капель привязана к масштабу, чтобы темп падения не менялся.
        _spawners = GetComponentsInChildren<DropSpawner>(true);
        _baseGravity = new float[_spawners.Length];
        for (int i = 0; i < _spawners.Length; i++) _baseGravity[i] = _spawners[i].dropGravityScale;
    }

    private void OnEnable()
    {
        calculation.Changed += UpdateTarget;
        UpdateTarget();
        _factor = _targetFactor;
        Apply();
    }

    private void OnDisable()
    {
        calculation.Changed -= UpdateTarget;
    }

    private void UpdateTarget()
    {
        var options = EmulsifierCalculation.MainGasFlowOptions;
        int level = System.Array.FindIndex(options, v => Mathf.Approximately(v, calculation.MainGasFlow));
        if (level < 0) return;

        float factor = 1f;
        for (int i = 1; i <= level; i++)
            factor *= 1f + (i == options.Length - 1 ? maxStepGrowth : stepGrowth);
        _targetFactor = factor;
    }

    private void Update()
    {
        if (Mathf.Approximately(_factor, _targetFactor)) return;

        _factor = Mathf.SmoothDamp(_factor, _targetFactor, ref _velocity, resizeTime);
        if (Mathf.Abs(_factor - _targetFactor) < 0.0005f) _factor = _targetFactor;
        Apply();
    }

    private void Apply()
    {
        transform.localScale = _baseScale * _factor;
        transform.localPosition = _basePosition - Vector3.up * (_bottomOffset * (_factor - 1f));
        for (int i = 0; i < _spawners.Length; i++) _spawners[i].dropGravityScale = _baseGravity[i] * _factor;
    }
}
