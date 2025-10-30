using UnityEngine;
using UnityEngine.UI;

public class GenderRatioSlider : MonoBehaviour
{
    [SerializeField] private Slider genderSlider; // 슬라이더
    [SerializeField] private Text ratioText;      // 비율 텍스트

    private int maleRatio;     // 내부 저장용
    private int femaleRatio;

    void Start()
    {
        UpdateRatio(genderSlider.value);
        genderSlider.onValueChanged.AddListener(UpdateRatio);
    }

    void UpdateRatio(float value)
    {
        maleRatio = Mathf.RoundToInt(value);
        femaleRatio = 100 - maleRatio;

        ratioText.text = $"{maleRatio}:{femaleRatio}";
    }
    public int GetMaleRatio() => maleRatio;
    public int GetFemaleRatio() => femaleRatio;

    public void SetMaleRatio(int value)
    {
        maleRatio = Mathf.Clamp(value, 0, 100);
        genderSlider.value = maleRatio;
        femaleRatio = 100 - maleRatio;
        ratioText.text = $"{maleRatio}:{femaleRatio}";
    }
}
