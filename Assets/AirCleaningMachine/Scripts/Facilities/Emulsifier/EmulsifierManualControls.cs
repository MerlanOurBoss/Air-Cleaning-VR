using System.Globalization;
using TMPro;
using Unity.VRTemplate;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Пост ручного управления: рубильник пускает и останавливает установку, вентиль задаёт расход жидкости.
/// Работает вместе с планшетом: рубильник и вентиль повторяют то, что выбрано на нём, и наоборот.
/// Выполняется после XRKnob и EmulsifierEffects: начальное состояние не должно считаться действием игрока.
/// </summary>
[DefaultExecutionOrder(100)]
public class EmulsifierManualControls : MonoBehaviour
{
    [SerializeField] private EmulsifierEffects effects;
    [SerializeField] private EmulsifierCalculation calculation;
    [SerializeField] private EmulsifierFault fault;
    [SerializeField] private XRKnob breaker;
    [SerializeField] private XRKnob valve;
    [SerializeField] private TMP_Text valveValue;

    private static readonly NumberFormatInfo RuNumbers = new NumberFormatInfo { NumberDecimalSeparator = "," };

    private bool _syncing;

    private void Start()
    {
        Sync();

        breaker.onValueChange.AddListener(OnBreakerMoved);
        valve.onValueChange.AddListener(OnValveMoved);
        breaker.selectExited.AddListener(OnReleased);
        valve.selectExited.AddListener(OnReleased);

        effects.StateChanged += Sync;
        calculation.Changed += Sync;
        fault.Changed += Sync;
    }

    private void OnDestroy()
    {
        effects.StateChanged -= Sync;
        calculation.Changed -= Sync;
        fault.Changed -= Sync;
    }

    private void OnBreakerMoved(float value)
    {
        if (_syncing || fault.HasExploded) return;

        bool on = value >= 0.5f;
        if (on == effects.IsPlaying) return;
        if (on) effects.Play();
        else effects.Stop();
    }

    private void OnValveMoved(float value)
    {
        if (_syncing) return;

        var options = EmulsifierCalculation.WaterFlowOptions;
        float flow = options[Mathf.RoundToInt(value * (options.Length - 1))];
        if (!Mathf.Approximately(flow, calculation.WaterFlow)) calculation.SetWaterFlow(flow);
    }

    // Отпущенный рукоятка встаёт в положение, соответствующее настройке, а не остаётся на полпути.
    private void OnReleased(SelectExitEventArgs args) => Sync();

    private void Sync()
    {
        _syncing = true;

        // После взрыва рубильник заблокирован, как и кнопка «Пуск» на планшете.
        breaker.enabled = !fault.HasExploded;
        if (!breaker.isSelected) breaker.value = effects.IsPlaying ? 1f : 0f;

        var options = EmulsifierCalculation.WaterFlowOptions;
        int index = System.Array.FindIndex(options, v => Mathf.Approximately(v, calculation.WaterFlow));
        if (index >= 0 && !valve.isSelected) valve.value = index / (options.Length - 1f);
        valveValue.text = calculation.WaterFlow.ToString("0.00", RuNumbers) + " м³/с";

        _syncing = false;
    }
}
