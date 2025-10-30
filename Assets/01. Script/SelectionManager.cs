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
