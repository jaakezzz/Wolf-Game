using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class MainMenuUI : MonoBehaviour
{
    [Header("Scene to Load")]
    [Tooltip("Name of your gameplay scene (the one you've been working in). Make sure it's in Build Settings!")]
    public string gameSceneName = "SampleScene";

    [Header("Cursor")]
    public bool showCursor = true;

    [Header("Audio")]
    public AudioSource musicSource;     // assign in inspector
    public AudioClip menuMusic;         // assign in inspector
    [Range(0f, 1f)] public float musicVolume = 0.7f;
    public float musicFadeTime = 1f;    // fade-out duration

    [Header("Fade UI")]
    public Image fadeImage;             // full-screen UI Image (black, alpha=0)
    public float fadeTime = 1f;

    void OnEnable()
    {
        Time.timeScale = 1f;

        if (showCursor)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        // start menu music
        if (musicSource && menuMusic)
        {
            musicSource.clip = menuMusic;
            musicSource.volume = musicVolume;
            musicSource.loop = true;
            musicSource.Play();
        }

        if (fadeImage) fadeImage.gameObject.SetActive(true);
    }

    // Hook this to the Start button
    public void OnStartGame()
    {
        StartCoroutine(FadeAndLoad());
    }

    IEnumerator FadeAndLoad()
    {
        float t = 0f;
        float startVol = musicSource ? musicSource.volume : 0f;

        Color c = fadeImage ? fadeImage.color : Color.black;

        while (t < fadeTime)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / fadeTime);

            // fade visual
            if (fadeImage)
            {
                c.a = a;
                fadeImage.color = c;
            }

            // fade audio
            if (musicSource)
                musicSource.volume = Mathf.Lerp(startVol, 0f, t / musicFadeTime);

            yield return null;
        }

        SceneManager.LoadScene(gameSceneName);
    }

    // Hook this to a Quit button
    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
