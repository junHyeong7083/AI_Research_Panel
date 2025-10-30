using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ButtonEvent : MonoBehaviour
{
    [SerializeField] private Dropdown methodDropdown;
    [SerializeField] private Dropdown sampleSizeDropdown;
    [SerializeField] private Dropdown ruleDropdown;

    [SerializeField] private Button nextButton;

    private void Start() => nextButton.onClick.AddListener(OnNextButtonClicked);

    void OnNextButtonClicked()
    {
        string method = methodDropdown.options[methodDropdown.value].text;
        string sampleSize = sampleSizeDropdown.options[sampleSizeDropdown.value].text;
        string rule = ruleDropdown.options[ruleDropdown.value].text;


        if(SelectionManager.instance != null)
        {
            SelectionManager.instance.method = method;
            SelectionManager.instance.sampleSize = sampleSize;
            SelectionManager.instance.screeningRule = rule;
        }

        SceneManager.LoadScene("PersonaCreatorScene");
    }
}
