using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Страница «Авария» на планшете: запуск учебной аварии, показания приборов и действия по устранению.
/// О начале аварии панель не сообщает, пока не сработала сигнализация: её нужно заметить по приборам.
/// </summary>
public class EmulsifierFaultPanel : MonoBehaviour
{
    [SerializeField] private EmulsifierFault fault;
    [SerializeField] private EmulsifierEffects effects;
    [SerializeField] private EmulsifierFaultRules rules;
    [SerializeField] private EmulsifierScenario scenario;

    [SerializeField] private Button randomButton;
    [SerializeField] private Button clogButton;
    [SerializeField] private Button lowLevelButton;
    [SerializeField] private Button cleanButton;
    [SerializeField] private Button refillButton;
    [SerializeField] private Button restoreButton;
    [SerializeField] private Button rulesButton;
    [SerializeField] private TMP_Text rulesLabel;
    [Tooltip("Кнопка «Пуск/Стоп»: после взрыва запуск заблокирован.")]
    [SerializeField] private Button powerButton;
    [SerializeField] private TMP_Text readout;
    [SerializeField] private TMP_Text message;
    [SerializeField] private Color normalColor = new Color(0.18f, 0.18f, 0.18f);
    [SerializeField] private Color activeColor = new Color(0.125f, 0.588f, 0.953f);

    private static readonly System.Globalization.NumberFormatInfo RuNumbers = new System.Globalization.NumberFormatInfo
    {
        NumberDecimalSeparator = ",",
    };

    private const string HighPressureAlarm = "<color=#FF5A4A>ТРЕВОГА: высокое давление</color>";
    private const string LowLevelAlarm = "<color=#FF5A4A>ТРЕВОГА: низкий уровень</color>";

    private string _message = "";
    private int _shownPressure = -1;
    private int _shownLevel = -1;

    private void Start()
    {
        // Префаб планшета не может ссылаться на объекты сцены.
        if (fault == null) fault = FindAnyObjectByType<EmulsifierFault>();
        if (effects == null) effects = FindAnyObjectByType<EmulsifierEffects>();
        if (rules == null) rules = FindAnyObjectByType<EmulsifierFaultRules>();
        if (scenario == null) scenario = FindAnyObjectByType<EmulsifierScenario>();

        randomButton.onClick.AddListener(() =>
        {
            fault.StartRandom();
            Say("Авария случится в течение минуты. Следите за приборами.");
        });
        clogButton.onClick.AddListener(() =>
        {
            fault.StartClog();
            Say("Учебная авария: засор.");
        });
        lowLevelButton.onClick.AddListener(() =>
        {
            fault.StartLowLevel();
            Say("Учебная авария: падение уровня.");
        });
        cleanButton.onClick.AddListener(Clean);
        refillButton.onClick.AddListener(Refill);
        rulesButton.onClick.AddListener(() =>
        {
            rules.Toggle();
            Refresh();
        });
        restoreButton.onClick.AddListener(() =>
        {
            fault.Restore();
            Say("Установка восстановлена. Запустите её на странице «Управление».");
        });

        fault.Changed += Refresh;
        scenario.Changed += Refresh;
        scenario.Started += ClearMessage;
        Refresh();
    }

    private void OnDestroy()
    {
        if (fault != null) fault.Changed -= Refresh;
        if (scenario == null) return;
        scenario.Changed -= Refresh;
        scenario.Started -= ClearMessage;
    }

    private void Update()
    {
        // Текст меняем только при изменении: сборка строк каждый кадр создаёт мусор и паузы сборщика в шлеме.
        int pressure = Mathf.RoundToInt(fault.Pressure * 10f);
        int level = Mathf.RoundToInt(fault.Level * 100f);
        if (pressure != _shownPressure || level != _shownLevel)
        {
            _shownPressure = pressure;
            _shownLevel = level;
            readout.text = "Давление " + fault.Pressure.ToString("0.0", RuNumbers) + " кПа  ·  Уровень " + level + " %";
        }

        if (fault.HasExploded) return;
        string current = !fault.IsAlarm ? _message
                       : fault.Pressure >= fault.WarningPressure ? HighPressureAlarm : LowLevelAlarm;
        if (message.text != current) message.text = current;
    }

    private void Clean()
    {
        if (fault.Fault != EmulsifierFault.FaultType.Clog)
        {
            Say("Засора нет.");
            scenario.AddMistake("Прочистка без засора");
        }
        else if (effects.IsPlaying)
        {
            Say("Сначала остановите установку, затем прочистите.");
            scenario.AddMistake("Прочистка на работающей установке");
        }
        else
        {
            string cause = fault.Cause;
            if (fault.TryClean()) Say("Засор устранён." + CauseHint(cause, "Установку можно запускать."));
        }
    }

    private void Refill()
    {
        string cause = fault.Cause;
        if (fault.TryRefill())
        {
            Say("Идёт доливка." + CauseHint(cause, ""));
            return;
        }

        Say("Утечки нет.");
        scenario.AddMistake("Доливка без утечки");
    }

    // После устранения показываем, какую настройку поправить, иначе авария начнётся снова. На экзамене не подсказываем.
    private string CauseHint(string cause, string otherwise) =>
        string.IsNullOrEmpty(cause) || scenario.Mode == EmulsifierScenario.ScenarioMode.Exam
            ? " " + otherwise
            : " Исправьте настройку. " + cause + ".";

    private void Say(string text) => _message = text;

    private void ClearMessage() => Say("");

    private void Refresh()
    {
        powerButton.interactable = !fault.HasExploded;
        restoreButton.interactable = fault.HasExploded;
        cleanButton.interactable = !fault.HasExploded;
        refillButton.interactable = !fault.HasExploded;
        // Во время сценария аварии запускает он сам, а правила не отключаются.
        bool free = !fault.HasExploded && !scenario.IsRunning;
        randomButton.interactable = free;
        clogButton.interactable = free;
        lowLevelButton.interactable = free;
        rulesButton.interactable = !scenario.IsRunning;

        rulesLabel.text = rules.RulesEnabled ? "Правила: вкл" : "Правила: выкл";
        EmulsifierTablet.Tint(rulesButton, rules.RulesEnabled ? activeColor : normalColor);

        if (fault.HasExploded)
            message.text = "<color=#FF5A4A>ВЗРЫВ. Причина: "
                           + (!string.IsNullOrEmpty(fault.Cause) ? fault.Cause
                              : fault.ExplosionCause == EmulsifierFault.FaultType.Clog ? "засор" : "низкий уровень")
                           + ".</color>";
    }
}
