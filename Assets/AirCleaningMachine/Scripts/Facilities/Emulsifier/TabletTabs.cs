using UnityEngine;
using UnityEngine.UI;

/// <summary>Вкладки планшета: кнопка i показывает страницу i, активная вкладка подсвечена.</summary>
public class TabletTabs : MonoBehaviour
{
    [SerializeField] private Button[] tabs;
    [SerializeField] private GameObject[] pages;
    [SerializeField] private Color normalColor = new Color(0.18f, 0.18f, 0.18f);
    [SerializeField] private Color activeColor = new Color(0.125f, 0.588f, 0.953f);

    private void Start()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i].onClick.AddListener(() => Show(index));
        }
        Show(0);
    }

    public void Show(int index)
    {
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == index);
            EmulsifierTablet.Tint(tabs[i], i == index ? activeColor : normalColor);
        }
    }
}
