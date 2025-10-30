using UnityEngine;
using UnityEngine.UI;

public class GenderRatioSlider : MonoBehaviour
{
    [SerializeField] private Slider genderSlider; // �����̴�
    [SerializeField] private Text ratioText;      // ���� �ؽ�Ʈ

    private int maleRatio;     // ���� �����
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
