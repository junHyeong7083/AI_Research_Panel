using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager instance;


    [HideInInspector]
    public string method;
    [HideInInspector]
    public string sampleSize;
    [HideInInspector] 
    public string screeningRule;
    
    
    // gender
    [HideInInspector]
    public int maleRatio;
    [HideInInspector]
    public int femaleRatio;

    // age
    [HideInInspector]
    public int age10Ratio;
    [HideInInspector]
    public int age20Ratio;
    [HideInInspector]
    public int age30Ratio;
    [HideInInspector]
    public int age40Ratio;
    [HideInInspector]
    public int age50Ratio;



    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(instance);
        }
        else Destroy(instance);
    }
}
