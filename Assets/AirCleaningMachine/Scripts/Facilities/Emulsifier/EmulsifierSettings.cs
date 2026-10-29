using System;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Настройки эмульгатора для VR: мощность насоса и тип жидкости.
/// Насос повторяет NasosScript из AirCleaningMachine; тип жидкости, в отличие от ChamgingEmul,
/// показывается цветом жидкости в смотровых стёклах-уровнемерах, а не перекраской корпуса.
/// </summary>
public class EmulsifierSettings : MonoBehaviour
{
    public enum FluidType { Water = 1, CausticSoda = 2, Soda = 3 }

    public const int MinPumpLevel = 1;
    public const int MaxPumpLevel = 4;

    // Меши струй Alembic создаёт сам при загрузке потока, поэтому в префабе на них нет ссылок —
    // берём их из-под каждого директора, когда появятся.
    [SerializeField] private PlayableDirector[] fluidDirectors;
    // Корпус не перекрашивается: тип жидкости виден по цвету столба в смотровых стёклах-уровнемерах.
    [SerializeField] private Renderer[] liquidRenderers;
    [SerializeField] private Color waterColor = new Color(0.55f, 0.8f, 1f);
    [SerializeField] private Color causticSodaColor = new Color(0.9f, 0.15f, 0.1f);
    [SerializeField] private Color sodaColor = new Color(0.2f, 0.85f, 0.25f);
    [Tooltip("Время смены цвета жидкости при смене реагента, с.")]
    [SerializeField] private float colorChangeTime = 1.5f;

    // Уровни насоса 1–4: скорость Alembic-таймлайна и толщина струи (значения из NasosScript).
    private static readonly float[] PumpSpeeds = { 1f, 1f, 1.5f, 1.7f };
    private static readonly float[] PumpThickness = { 1f, 1f, 1.05f, 1.1f };

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    public int PumpLevel { get; private set; } = MinPumpLevel;
    public FluidType Fluid { get; private set; } = FluidType.Water;

    public event Action Changed;

    private Renderer[] _streams;
    private Vector3[] _baseStreamScales;

    // Общая runtime-копия материала жидкости в уровнемерах: ассет материала не меняется.
    private Material _liquidMaterial;
    private Color _color;
    private float _colorBlend = 1f;
    private Color _colorFrom;

    private void Awake()
    {
        _streams = new Renderer[fluidDirectors.Length];
        _baseStreamScales = new Vector3[fluidDirectors.Length];
        _color = FluidColor();

        foreach (var r in liquidRenderers)
        {
            if (r == null) continue;
            if (_liquidMaterial == null) _liquidMaterial = new Material(r.sharedMaterial);
            r.sharedMaterial = _liquidMaterial;
        }

        ApplyColor();
    }

    private void OnDestroy()
    {
        Destroy(_liquidMaterial);
    }

    private void Update()
    {
        if (_colorBlend < 1f)
        {
            _colorBlend = Mathf.Min(1f, _colorBlend + Time.deltaTime / Mathf.Max(colorChangeTime, 0.01f));
            _color = Color.Lerp(_colorFrom, FluidColor(), Mathf.SmoothStep(0f, 1f, _colorBlend));
            ApplyColor();
        }

        // PlayableDirector.Play() пересоздаёт граф со скоростью 1, поэтому скорость поддерживаем постоянно.
        float speed = PumpSpeeds[PumpLevel - 1];
        foreach (var director in fluidDirectors)
        {
            if (director == null || !director.playableGraph.IsValid()) continue;

            var graph = director.playableGraph;
            for (int i = 0; i < graph.GetRootPlayableCount(); i++)
            {
                var root = graph.GetRootPlayable(i);
                if (root.IsValid() && !Mathf.Approximately((float)root.GetSpeed(), speed))
                    root.SetSpeed(speed);
            }
        }
    }

    private void LateUpdate()
    {
        // Alembic обновляет трансформы струй сам, поэтому толщину накладываем после него.
        float thickness = PumpThickness[PumpLevel - 1];
        for (int i = 0; i < fluidDirectors.Length; i++)
        {
            if (_streams[i] == null && !TryFindStream(i)) continue;
            _streams[i].transform.localScale = _baseStreamScales[i] * thickness;
        }
    }

    private bool TryFindStream(int i)
    {
        if (fluidDirectors[i] == null) return false;

        var stream = fluidDirectors[i].GetComponentInChildren<MeshRenderer>();
        if (stream == null) return false;

        _streams[i] = stream;
        _baseStreamScales[i] = stream.transform.localScale;
        return true;
    }

    public void SetPumpLevel(int level)
    {
        level = Mathf.Clamp(level, MinPumpLevel, MaxPumpLevel);
        if (level == PumpLevel) return;

        PumpLevel = level;
        Changed?.Invoke();
    }

    public void SetFluid(FluidType type)
    {
        if (type == Fluid) return;

        Fluid = type;
        // Плавный переход от текущего (возможно, промежуточного) цвета — реагент вытесняет прежнюю жидкость.
        _colorFrom = _color;
        _colorBlend = 0f;
        Changed?.Invoke();
    }

    public void SetFluid(int type) => SetFluid((FluidType)Mathf.Clamp(type, 1, 3));

    private Color FluidColor()
    {
        switch (Fluid)
        {
            case FluidType.CausticSoda: return causticSodaColor;
            case FluidType.Soda: return sodaColor;
            default: return waterColor;
        }
    }

    // Прозрачность берём из материала, меняем только RGB; подсветка — чтобы цвет читался и в тени.
    private void ApplyColor()
    {
        if (_liquidMaterial == null) return;
        var c = _color;
        c.a = _liquidMaterial.color.a;
        _liquidMaterial.color = c;
        _liquidMaterial.SetColor(EmissionColor, _color * 0.35f);
    }
}
