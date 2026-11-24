using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;
    public TextMeshProUGUI scoreText;
    private int score = 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        scoreText.text = "Score:" + score.ToString();
    }

    public void AddScore(int points)
    {
        score += points;
        scoreText.text = "Score:" + score.ToString();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        scoreText = FindFirstObjectByType<TMPro.TextMeshProUGUI>();
        scoreText.text = "Score:" + score.ToString();
    }

    public void ResetScoreAndReturnToFirstMap()
    {
        score = 0;
        Room.ResetRoomProgress();
        SceneManager.LoadScene("Map1");
    }
}