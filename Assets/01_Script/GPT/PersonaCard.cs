using UnityEngine;
using UnityEngine.UI;
public class PersonaCard : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text Name;
    [SerializeField] private Text Gender;
    [SerializeField] private Text Age;
    [SerializeField] private Text Occupation;
    [SerializeField] private Text Description;

    private PersonaData data;

    public void SetData(PersonaData persona)
    {
        data = persona;

        Name.text = persona.name;
        Gender.text = persona.gender;
        int parsedAge = 0;
        if (int.TryParse(persona.age.ToString(), out int result))
            parsedAge = result;
        Age.text = parsedAge.ToString();
        Occupation.text = persona.occupation;
        Description.text = persona.description;

    }
}
