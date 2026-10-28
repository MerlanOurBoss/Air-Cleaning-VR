using System;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Настройки эмульгатора для VR: мощность насоса и тип жидкости.
/// Повторяет визуальное поведение NasosScript и ChamgingEmul из AirCleaningMachine без их UI.
/// </summary>
public class EmulsifierSettings : MonoBehaviour
{
    public enum FluidType { Water = 1, CausticSoda = 2, Soda = 3 }

    public const int MinPumpLevel = 1;
    public const int MaxPumpLevel = 4;

    // Меши струй Alembic создаёт сам при загрузке потока, поэтому в префабе на них нет ссылок —
    // берём их из-под каждого директора, когда появятся.
    [SerializeField] private PlayableDirector[] fluidDirectors;
    [SerializeField] private Renderer[] tankRenderers;
    [SerializeField] private Material waterTankMaterial;
    [SerializeField] private Material causticSodaTankMaterial;
    [SerializeField] private Material sodaTankMaterial;

    // Уровни насоса 1–4: скорость Alembic-таймлайна и толщина струи (значения из NasosScript).
    private static readonly float[] PumpSpeeds = { 1f, 1f, 1.5f, 1.7f };
    private static readonly float[] PumpThickness = { 1f, 1f, 1.05f, 1.1f };

    public int PumpLevel { get; private set; } = MinPumpLevel;
    public FluidType Fluid { get; private set; } = FluidType.Water;

    public event Action Changed;

    private Renderer[] _streams;
    private Vector3[] _baseStreamScales;

    private void Awake()
    {
        _streams = new Renderer[fluidDirectors.Length];
        _baseStreamScales = new Vector3[fluidDirectors.Length];
        ApplyTank();
    }

    private void Update()
    {
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
        stream.material.color = FluidColor();
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
        foreach (var stream in _streams)
            if (stream != null) stream.material.color = FluidColor();
        ApplyTank();
        Changed?.Invoke();
    }

    public void SetFluid(int type) => SetFluid((FluidType)Mathf.Clamp(type, 1, 3));

    // Цвета жидкости и материалы корпуса — как в ChamgingEmul.
    private Color FluidColor()
    {
        switch (Fluid)
        {
            case FluidType.CausticSoda: return new Color(0.7f, 1f, 0f, 0.6f);
            case FluidType.Soda: return new Color(0f, 0f, 0f, 0.6f);
            default: return new Color(1f, 1f, 1f, 0.6f);
        }
    }

    private void ApplyTank()
    {
        Material tank = Fluid == FluidType.CausticSoda ? causticSodaTankMaterial
                      : Fluid == FluidType.Soda ? sodaTankMaterial
                      : waterTankMaterial;
        if (tank == null) return;

        foreach (var r in tankRenderers)
            if (r != null) r.sharedMaterial = tank;
    }
}
