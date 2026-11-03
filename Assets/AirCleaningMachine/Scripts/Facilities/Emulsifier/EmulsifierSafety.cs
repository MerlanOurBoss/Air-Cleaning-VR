using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Порядок безопасности: СИЗ со стойки, опасная зона за жёлтой линией на полу и блокировка рубильника на посту.
/// Состав СИЗ и шаги учебные, их нужно сверять с регламентом предприятия.
/// </summary>
public class EmulsifierSafety : MonoBehaviour
{
    [SerializeField] private EmulsifierEffects effects;
    [SerializeField] private PpeItem[] items;

    [Header("Опасная зона: прямоугольник внутри жёлтой линии, м")]
    [SerializeField] private Vector2 zoneMin = new Vector2(-1.4f, -1.6f);
    [SerializeField] private Vector2 zoneMax = new Vector2(9f, 2.2f);

    [Header("Блокировка")]
    [SerializeField] private InspectableNode lockNode;
    [SerializeField] private GameObject lockShackle;
    [SerializeField] private TMP_Text lockLabel;

    public int WornCount
    {
        get
        {
            int count = 0;
            foreach (var item in items)
                if (item.Worn) count++;
            return count;
        }
    }

    public int ItemCount => items.Length;
    public bool AllWorn => WornCount == items.Length;

    /// <summary>Игрок вошёл в опасную зону, не надев СИЗ.</summary>
    public event Action ZoneEnteredUnprotected;
    public event Action Changed;

    private Transform _head;
    private bool _inZone;

    private void Awake()
    {
        _head = Camera.main.transform;
        foreach (var item in items) item.Changed += OnItemChanged;
        lockNode.Pressed += ToggleLock;
    }

    private void Start()
    {
        _inZone = IsInZone();
        effects.StateChanged += RefreshLock;
        L10n.Changed += RefreshLock;
        RefreshLock();
    }

    private void OnDestroy()
    {
        foreach (var item in items) item.Changed -= OnItemChanged;
        lockNode.Pressed -= ToggleLock;
        effects.StateChanged -= RefreshLock;
        L10n.Changed -= RefreshLock;
    }

    private void Update()
    {
        bool inZone = IsInZone();
        if (inZone && !_inZone && !AllWorn) ZoneEnteredUnprotected?.Invoke();
        _inZone = inZone;
    }

    private bool IsInZone()
    {
        var position = _head.position;
        return position.x >= zoneMin.x && position.x <= zoneMax.x && position.z >= zoneMin.y && position.z <= zoneMax.y;
    }

    private void OnItemChanged() => Changed?.Invoke();

    // Работающую установку не блокируем: сначала её нужно остановить. Об этом говорит подсказка на замке.
    private void ToggleLock()
    {
        if (effects.IsPlaying) return;
        effects.SetPowerLocked(!effects.PowerLocked);
    }

    private void RefreshLock()
    {
        bool locked = effects.PowerLocked;
        lockShackle.SetActive(locked);
        lockLabel.text = L10n.Get(locked ? "lock.label.closed" : "lock.label.open");
        lockNode.SetHint(locked ? "card.hint.unlock" : effects.IsPlaying ? "card.hint.lockRefused" : "card.hint.lock");
    }

    /// <summary>Сценарий начинается заново: СИЗ снова на стойке, блокировка снята.</summary>
    public void ResetAll()
    {
        foreach (var item in items) item.SetWorn(false);
        effects.SetPowerLocked(false);
        _inZone = IsInZone();
    }
}
