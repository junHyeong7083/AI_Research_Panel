using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ButtonEvent : MonoBehaviour
{
    [SerializeField] private Dropdown methodDropdown;
    [SerializeField] private Dropdown sampleSizeDropdown;
    [SerializeField] private Dropdown ruleDropdown;
    [SerializeField] private Slider genderSlider;
    [SerializeField] private Button nextButton;

    private GenderRatioSlider gender;
    private AgeRatioController age;

    private void Awake()
    {
        age = GetComponent<AgeRatioController>();
        gender = GetComponent<GenderRatioSlider>();
    }

    private void Start() => nextButton.onClick.AddListener(OnNextButtonClicked);

    void OnNextButtonClicked()
    {
        string method = methodDropdown.options[methodDropdown.value].text;
        string sampleSize = sampleSizeDropdown.options[sampleSizeDropdown.value].text;
        string rule = ruleDropdown.options[ruleDropdown.value].text;

        int maleRatio = gender.GetMaleRatio();
        int femaleRatio = gender.GetFemaleRatio();
        int[] ageRatios = age.GetAgeRatios(); // 🔹 나이대 비율 불러오기

        if (SelectionManager.instance != null)
        {
            SelectionManager.instance.method = method;
            SelectionManager.instance.sampleSize = sampleSize;
            SelectionManager.instance.screeningRule = rule;

            // Gender
            SelectionManager.instance.maleRatio = maleRatio;
            SelectionManager.instance.femaleRatio = femaleRatio;

            // 🔹 나이대별 저장
            SelectionManager.instance.age10Ratio = ageRatios[0];
            SelectionManager.instance.age20Ratio = ageRatios[1];
            SelectionManager.instance.age30Ratio = ageRatios[2];
            SelectionManager.instance.age40Ratio = ageRatios[3];
            SelectionManager.instance.age50Ratio = ageRatios[4];
        }

        SceneManager.LoadScene("PersonaCreatorScene");
    }

}
