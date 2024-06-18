using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneMN : MonoBehaviour
{
    void OnEnable()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
