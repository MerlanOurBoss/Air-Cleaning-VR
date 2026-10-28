using System.Globalization;
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
        Refresh();
    }

    private void OnDestroy()
    {
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
        string note = main >= 0 ? EmulsifierCalculation.MainGasFlowNotes[main] : "";
        mainFlowValue.text = Format(calculation.MainGasFlow, "#,0") + " м³/ч"
                             + (string.IsNullOrEmpty(note) ? "" : "\n<size=70%><color=#9AA>" + note + "</color></size>");
        mainFlowPrev.interactable = main > 0;
        mainFlowNext.interactable = main < EmulsifierCalculation.MainGasFlowOptions.Length - 1;

        Highlight(gasFlowButtons, EmulsifierCalculation.GasFlowOptions, calculation.GasFlow);
        Highlight(waterFlowButtons, EmulsifierCalculation.WaterFlowOptions, calculation.WaterFlow);
        Highlight(temperatureButtons, EmulsifierCalculation.TemperatureOptions, calculation.Temperature);

        resultLabels.text = string.Join("\n", ResultNames);
        resultValues.text = string.Join("\n", new[]
        {
            Format(calculation.GasSpeed, "0.000") + " м/с",
            Format(calculation.WaterSpeed, "0.000") + " м/с",
            Format(calculation.GasMassFlow, "0.000") + " м³/с",
            Format(calculation.WaterMassFlow, "0.000") + " м³/с",
            Format(calculation.MassTransfer, "0.0") + " м/с",
            Format(calculation.LiquidConsumption, "0.000") + " м³/с",
            Format(calculation.Diameter, "0.0") + " м",
            Format(calculation.Height, "0.0") + " м",
            Format(calculation.ConsumablesCost, "#,0.0") + " тг",
        });
    }

    // Подписи результатов — как в MathModuleForEmul.
    private static readonly string[] ResultNames =
    {
        "Скорость газа", "Скорость воды", "Массовый поток газа", "Массовый поток жидкости",
        "Коэф. массопереноса", "Расход жидкости", "Диаметр аппарата", "Высота аппарата", "Расходники",
    };

    private void Highlight(Button[] buttons, float[] options, float current)
    {
        for (int i = 0; i < buttons.Length; i++)
            EmulsifierTablet.Tint(buttons[i], Mathf.Approximately(options[i], current) ? activeColor : normalColor);
    }

    // Русский формат: пробел между разрядами, запятая в дробной части.
    private static readonly NumberFormatInfo RuNumbers = new NumberFormatInfo
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = " ",
    };

    private static string Format(double value, string format) => value.ToString(format, RuNumbers);

    private static int IndexOf(float[] options, float value)
    {
        for (int i = 0; i < options.Length; i++)
            if (Mathf.Approximately(options[i], value)) return i;
        return -1;
    }
}
