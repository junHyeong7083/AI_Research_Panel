using UnityEngine;
using UnityEngine.UI;

public class AgeRatioController : MonoBehaviour
{
    [SerializeField] private Slider[] ageSliders; 
    [SerializeField] private Text[] ageTexts;     

    private int[] ageRatios; // 내부 비율 저장용

    void Start()
    {
        ageRatios = new int[ageSliders.Length];

        for (int e = 0; e < ageSliders.Length; e++)
        {
            ageSliders[e].onValueChanged.AddListener(_ => UpdateRatios());
        }

        UpdateRatios(); // 초기 표시
    }

    void UpdateRatios()
    {
        int total = 0;

        // 슬라이더 값 계산 및 텍스트 표시
        for (int e = 0; e < ageSliders.Length; ++e)
        {
            int value = Mathf.RoundToInt(ageSliders[e].value);
            ageRatios[e] = value;
            ageTexts[e].text = $"{value}%";
            total += value;
        }

        // 총합 100 초과 시 자동 보정
        if (total > 100)
        {
            float ratio = 100f / total;
            for (int e = 0; e < ageSliders.Length; ++e)
            {
                ageSliders[e].value *= ratio;
            }
        }
    }

    // Getter: 나이대별 비율 반환
    public int[] GetAgeRatios()
    {
        return ageRatios;
    }

  
    public void SetAgeRatios(int[] newRatios)
    {
        if (newRatios.Length != ageSliders.Length)
        {
            return;
        }

        for (int e = 0; e < ageSliders.Length; ++e)
        {
            int clamped = Mathf.Clamp(newRatios[e], 0, 100);
            ageSliders[e].value = clamped;
            ageTexts[e].text = $"{clamped}%";
            ageRatios[e] = clamped;
        }

        UpdateRatios();
    }
}
