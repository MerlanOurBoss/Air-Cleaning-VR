using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Страница «Расчёт» на планшете: выбор входов EmulsifierCalculation кнопками и вывод результатов.
/// </summary>
public class EmulsifierCalcPanel : MonoBehaviour
{
    [SerializeField] private EmulsifierCalculation calculation;

    [SerializeField] private Button mainFlowPrev;
    [SerializeField] private Button mainFlowNext;
    [SerializeField] private TMP_Text mainFlowValue;
    [SerializeField] private Button[] gasFlowButtons;
    [SerializeField] private Button[] waterFlowButtons;
    [SerializeField] private Button[] temperatureButtons;
    [SerializeField] private TMP_Text resultLabels;  // колонка подписей, выровнена влево
    [SerializeField] private TMP_Text resultValues;  // колонка значений, выровнена вправо

    [SerializeField] private Color normalColor = new Color(0.18f, 0.18f, 0.18f);
    [SerializeField] private Color activeColor = new Color(0.125f, 0.588f, 0.953f);

    private void Start()
    {
        if (calculation == null) calculation = FindAnyObjectByType<EmulsifierCalculation>();

        mainFlowPrev.onClick.AddListener(() => StepMainFlow(-1));
        mainFlowNext.onClick.AddListener(() => StepMainFlow(+1));
        Bind(gasFlowButtons, EmulsifierCalculation.GasFlowOptions, calculation.SetGasFlow);
        Bind(waterFlowButtons, EmulsifierCalculation.WaterFlowOptions, calculation.SetWaterFlow);
        Bind(temperatureButtons, EmulsifierCalculation.TemperatureOptions, calculation.SetTemperature);

        calculation.Changed += Refresh;
        L10n.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        L10n.Changed -= Refresh;
        if (calculation != null) calculation.Changed -= Refresh;
    }

    private static void Bind(Button[] buttons, float[] options, System.Action<float> set)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            float value = options[i];
            buttons[i].onClick.AddListener(() => set(value));
        }
    }

    private void StepMainFlow(int step)
    {
        var options = EmulsifierCalculation.MainGasFlowOptions;
        int index = Mathf.Clamp(IndexOf(options, calculation.MainGasFlow) + step, 0, options.Length - 1);
        calculation.SetMainGasFlow(options[index]);
    }

    private void Refresh()
    {
        int main = IndexOf(EmulsifierCalculation.MainGasFlowOptions, calculation.MainGasFlow);
        string note = main >= 0 ? EmulsifierCalculation.MainGasFlowNoteKeys[main] : "";
        mainFlowValue.text = L10n.Get("unit.m3_h", L10n.Number(calculation.MainGasFlow, "#,0"))
                             + (string.IsNullOrEmpty(note) ? "" : "\n<size=70%><color=#9AA>" + L10n.Get(note) + "</color></size>");
        mainFlowPrev.interactable = main > 0;
        mainFlowNext.interactable = main < EmulsifierCalculation.MainGasFlowOptions.Length - 1;

        Highlight(gasFlowButtons, EmulsifierCalculation.GasFlowOptions, calculation.GasFlow);
        Highlight(waterFlowButtons, EmulsifierCalculation.WaterFlowOptions, calculation.WaterFlow);
        Highlight(temperatureButtons, EmulsifierCalculation.TemperatureOptions, calculation.Temperature);

        // Дробные значения на кнопках пишутся по правилам языка.
        for (int i = 0; i < waterFlowButtons.Length; i++)
            waterFlowButtons[i].GetComponentInChildren<TMP_Text>().text = L10n.Number(EmulsifierCalculation.WaterFlowOptions[i], "0.0#");

        resultLabels.text = string.Join("\n", System.Array.ConvertAll(ResultNameKeys, key => L10n.Get(key)));
        resultValues.text = string.Join("\n", new[]
        {
            L10n.Get("unit.m_s", L10n.Number(calculation.GasSpeed, "0.000")),
            L10n.Get("unit.m_s", L10n.Number(calculation.WaterSpeed, "0.000")),
            L10n.Get("unit.m3_s", L10n.Number(calculation.GasMassFlow, "0.000")),
            L10n.Get("unit.m3_s", L10n.Number(calculation.WaterMassFlow, "0.000")),
            L10n.Get("unit.m_s", L10n.Number(calculation.MassTransfer, "0.0")),
            L10n.Get("unit.m3_s", L10n.Number(calculation.LiquidConsumption, "0.000")),
            L10n.Get("unit.m", L10n.Number(calculation.Diameter, "0.0")),
            L10n.Get("unit.m", L10n.Number(calculation.Height, "0.0")),
            L10n.Get("unit.tg", L10n.Number(calculation.ConsumablesCost, "#,0.0")),
        });
    }

    // Подписи результатов — как в MathModuleForEmul.
    private static readonly string[] ResultNameKeys =
    {
        "calc.r.gasSpeed", "calc.r.waterSpeed", "calc.r.gasMass", "calc.r.waterMass",
        "calc.r.transfer", "calc.r.consumption", "calc.r.diameter", "calc.r.height", "calc.r.cost",
    };

    private void Highlight(Button[] buttons, float[] options, float current)
    {
        for (int i = 0; i < buttons.Length; i++)
            EmulsifierTablet.Tint(buttons[i], Mathf.Approximately(options[i], current) ? activeColor : normalColor);
    }

    private static int IndexOf(float[] options, float value)
    {
        for (int i = 0; i < options.Length; i++)
            if (Mathf.Approximately(options[i], value)) return i;
        return -1;
    }
}
