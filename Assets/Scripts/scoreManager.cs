using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static int startingLives = 3;
    public static ScoreManager Instance;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI livesText;
    private int score = 0;
    private int lives = startingLives;
    private bool isHandlingPlayerDeath;

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
        livesText.text = "Lives:" + lives.ToString();
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
        scoreText = GameObject.Find("Text (TMP)").GetComponent<TextMeshProUGUI>();
        scoreText.text = "Score:" + score.ToString();
        livesText = GameObject.Find("Lives").GetComponent<TextMeshProUGUI>();
        livesText.text = "Lives:" + lives.ToString();
        isHandlingPlayerDeath = false;
    }

    public void ResetScoreAndReturnToFirstMap()
    {
        score = 0;
        lives = startingLives;
        Room.ResetRoomProgress();
        SceneManager.LoadScene("Map1");
    }

    public void HandlePlayerDeathAfterDelay(float delay)
    {
        if (isHandlingPlayerDeath)
        {
            return;
        }
        isHandlingPlayerDeath = true;
        lives--;
        livesText.text = "Lives:" + lives.ToString();
        StartCoroutine(HandlePlayerDeathAfterDelayCoroutine(delay, SceneManager.GetActiveScene().name));
    }

    IEnumerator HandlePlayerDeathAfterDelayCoroutine(float delay, string sceneName)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (lives <= 0)
        {
            ResetScoreAndReturnToFirstMap();
            yield break;
        }
        SceneManager.LoadScene(sceneName);
    }
}