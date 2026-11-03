using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Страница «Обучение» на планшете: выбор режима и языка, итог. Текущая задача показывается в отдельной полосе
/// над экраном: действия выполняются на других вкладках, и задача не должна при этом пропадать.
/// </summary>
public class EmulsifierScenarioPanel : MonoBehaviour
{
    [SerializeField] private EmulsifierScenario scenario;
    [SerializeField] private TabletTabs tabs;
    [Tooltip("Номер вкладки этой страницы: на неё планшет переходит, когда появился итог.")]
    [SerializeField] private int pageIndex = 3;

    [SerializeField] private Button trainingButton;
    [SerializeField] private Button examButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text info;
    [SerializeField] private GameObject taskStrip;
    [SerializeField] private TMP_Text taskText;

    [Tooltip("Кнопки языков, в том же порядке, что и коды.")]
    [SerializeField] private Button[] languageButtons;
    [SerializeField] private string[] languageCodes = { "ru", "kk", "en" };
    [SerializeField] private Color normalColor = new Color(0.18f, 0.18f, 0.18f);
    [SerializeField] private Color activeColor = new Color(0.125f, 0.588f, 0.953f);

    private void Start()
    {
        // Префаб планшета не может ссылаться на объекты сцены.
        if (scenario == null) scenario = FindAnyObjectByType<EmulsifierScenario>();

        trainingButton.onClick.AddListener(scenario.StartTraining);
        examButton.onClick.AddListener(scenario.StartExam);
        cancelButton.onClick.AddListener(scenario.Cancel);

        for (int i = 0; i < languageButtons.Length; i++)
        {
            string code = languageCodes[i];
            languageButtons[i].onClick.AddListener(() => L10n.SetLanguage(code));
        }

        scenario.Changed += Refresh;
        scenario.Finished += ShowPage;
        L10n.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        L10n.Changed -= Refresh;
        if (scenario == null) return;
        scenario.Changed -= Refresh;
        scenario.Finished -= ShowPage;
    }

    private void ShowPage() => tabs.Show(pageIndex);

    private void Refresh()
    {
        bool running = scenario.IsRunning;
        trainingButton.interactable = !running;
        examButton.interactable = !running;
        cancelButton.interactable = running;

        string report = scenario.Report;
        info.text = running ? L10n.Get("sc.running") : string.IsNullOrEmpty(report) ? L10n.Get("sc.intro") : report;
        taskStrip.SetActive(running);
        taskText.text = scenario.Task;

        string current = L10n.CurrentCode;
        for (int i = 0; i < languageButtons.Length; i++)
            EmulsifierTablet.Tint(languageButtons[i], languageCodes[i] == current ? activeColor : normalColor);
    }
}
