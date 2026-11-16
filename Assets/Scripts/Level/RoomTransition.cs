using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Fades to black, loads the next room scene, fades back in.
// Created on demand and kept alive across scene loads.
public class RoomTransition : MonoBehaviour
{
    public static string PendingSpawn { get; private set; }
    public static bool IsBusy => instance != null && instance.busy;

    const float FadeTime = 0.2f;

    static RoomTransition instance;

    float alpha;
    bool busy;

    // Clears statics when entering Play Mode with domain reload turned off.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        PendingSpawn = null;
        instance = null;
    }

    public static void Go(string sceneName, string spawnId)
    {
        if (instance == null)
        {
            var go = new GameObject("[RoomTransition]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RoomTransition>();
        }

        if (instance.busy) return;
        instance.StartCoroutine(instance.Run(sceneName, spawnId));
    }

    IEnumerator Run(string sceneName, string spawnId)
    {
        busy = true;
        Time.timeScale = 0f; // freeze player and enemies during the fade

        yield return Fade(0f, 1f);

        PendingSpawn = spawnId;
        yield return SceneManager.LoadSceneAsync(sceneName);

        yield return Fade(1f, 0f);

        Time.timeScale = 1f;
        busy = false;
    }

    IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < FadeTime; t += Time.unscaledDeltaTime)
        {
            alpha = Mathf.Lerp(from, to, t / FadeTime);
            yield return null;
        }
        alpha = to;
    }

    void OnGUI()
    {
        if (alpha <= 0f) return;
        GUI.depth = -1000;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            Time.timeScale = 1f;
            instance = null;
        }
    }
}
