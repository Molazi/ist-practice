using UnityEngine;
using UnityEngine.SceneManagement;

public class PersistentAudio : MonoBehaviour
{
    public static PersistentAudio instance;

    [Header("Music Clips")]
    public AudioClip menuMusic;      // музыка в меню
    public AudioClip gameMusic;      // музыка в основной сцене

    private AudioSource audioSource;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.loop = true;
        audioSource.playOnAwake = false;   // будем управлять вручную

        // Подписываемся на событие загрузки сцены
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        // При старте сразу включаем музыку для текущей сцены
        UpdateMusic(SceneManager.GetActiveScene().name);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateMusic(scene.name);
    }

    void UpdateMusic(string sceneName)
    {
        AudioClip clipToPlay = null;

        // Сравниваем имя сцены и выбираем нужный клип
        if (sceneName == "MainMenu")
            clipToPlay = menuMusic;
        else if (sceneName == "TerrainScene")   // <-- замени на точное имя своей основной сцены
            clipToPlay = gameMusic;

        // Если выбрали другой клип, переключаем
        if (clipToPlay != null && audioSource.clip != clipToPlay)
        {
            audioSource.clip = clipToPlay;
            audioSource.Play();
        }
    }

    void OnDestroy()
    {
        // Отписываемся, чтобы не было утечек
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}