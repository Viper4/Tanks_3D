using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Photon.Pun;
using MyUnityAddons.CustomPhoton;
using MyUnityAddons.Calculations;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using ExitGames.Client.Photon;
using Photon.Realtime;
using System.Text.RegularExpressions;
using Photon.Pun.UtilityScripts;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager gameManager;

    public bool offlineMode = true;
    public static bool frozen;
    public static bool autoPlay;
    public bool reachedLastLevel = false;

    bool loadingScene = false;

    readonly byte StartEventCode = 3;
    public readonly byte LoadSceneEventCode = 4;
    readonly byte AddReadyPlayerCode = 5;
    readonly byte RemoveReadyPlayerCode = 6;

    public Transform loadingScreen;
    [SerializeField] Transform progressBar;
    [SerializeField] Transform label;
    [SerializeField] Transform extraLifePopup;
    [SerializeField] Transform startButton;
    [SerializeField] Transform readyButton;

    PhotonView playerPV;
    BaseUIHandler baseUIHandler;

    public readonly int multiplayerSceneIndexEnd = 4;
    int lastSceneIndex = -1;

    int readyPlayers = 0;

    void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        
        SaveSystem.Init();
        PhotonNetwork.SendRate = 30;
        PhotonNetwork.SerializationRate = 15;
        if (gameManager == null)
        {
            PhotonNetwork.OfflineMode = offlineMode;
            gameManager = this;
            DontDestroyOnLoad(transform);

            OnSceneLoad();
        }
        else if (gameManager != this)
        {
            gameManager.OnSceneLoad();

            Destroy(gameObject);
        }
    }

    public void UpdatePlayerVariables(PhotonView PV)
    {
        playerPV = PV;
        baseUIHandler = PV.transform.Find("Player UI").GetComponent<BaseUIHandler>();
    }

    public void OnSceneLoad()
    {
        StopAllCoroutines();
        loadingScene = false;
        string currentSceneName = SceneManager.GetActiveScene().name;
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        Time.timeScale = 1;

        if (PhotonNetwork.OfflineMode)
        {
            baseUIHandler = FindObjectOfType<BaseUIHandler>();
        }

        switch (currentSceneName)
        {
            case "Main Menu":
                lastSceneIndex = -1;
                PhotonNetwork.OfflineMode = true;
                offlineMode = true;
                SaveSystem.ResetPlayerData("PlayerData");                 // Resetting lives, kills, deaths, etc... but keeping bestTime

                loadingScreen.gameObject.SetActive(false);
                autoPlay = true;
                StartCoroutine(ResetAutoPlay(2.5f));
                break;
            case "Waiting Room":
                PhotonNetwork.OfflineMode = false;
                offlineMode = false;

                loadingScreen.gameObject.SetActive(false);
                autoPlay = true;
                StartCoroutine(ResetAutoPlay(2.5f));
                break;
            case "End Scene":
                if (PhotonNetwork.OfflineMode || playerPV.IsMine)
                {
                    PlayerData playerData = SaveSystem.LoadPlayerData("PlayerData");

                    Text labelText = baseUIHandler.UIElements["EndMenu"].Find("LabelBackground").GetChild(0).GetComponent<Text>();
                    Transform stats = baseUIHandler.UIElements["StatsMenu"].Find("Stats");
                    gameManager.loadingScreen.gameObject.SetActive(false);

                    labelText.text = "Game over";
                    if (gameManager.reachedLastLevel)
                    {
                        if (PhotonNetwork.OfflineMode)
                        {
                            if (playerData.lives > 0)
                            {
                                labelText.text = "Campaign complete!";
                            }
                        }
                        else if ((int)PhotonNetwork.CurrentRoom.CustomProperties["Total Lives"] > 0)
                        {
                            labelText.text = "Campaign complete!";
                        }
                    }

                    stats.Find("Time").GetComponent<Text>().text = "Time: " + playerData.time.FormattedTime();
                    stats.Find("Best Time").GetComponent<Text>().text = "Best Time: " + playerData.bestTime.FormattedTime();

                    if (playerData.kills > 0)
                    {
                        float accuracy = 1;
                        if (playerData.shots != 0)
                        {
                            accuracy = Mathf.Clamp((float)playerData.kills / playerData.shots, 0, 1);
                        }
                        stats.Find("Accuracy").GetComponent<Text>().text = "Accuracy: " + (Mathf.Round(accuracy * 10000) / 100).ToString() + "%";
                        stats.Find("Kills").GetComponent<Text>().text = "Kills: " + playerData.kills;
                        if (playerData.deaths == 0)
                        {
                            stats.Find("KD Ratio").GetComponent<Text>().text = "KD Ratio: " + playerData.kills.ToString();
                        }
                        else
                        {
                            stats.Find("KD Ratio").GetComponent<Text>().text = "KD Ratio: " + ((float)playerData.kills / playerData.deaths).ToString();
                        }
                    }

                    stats.Find("Deaths").GetComponent<Text>().text = "Deaths: " + playerData.deaths;
                }
                break;
            default:
                autoPlay = false;

                if (currentSceneIndex <= multiplayerSceneIndexEnd)
                {
                    PhotonNetwork.OfflineMode = false;
                    offlineMode = false;

                    gameManager.loadingScreen.gameObject.SetActive(false);
                    frozen = false;
                }
                else
                {
                    int.TryParse(Regex.Match(currentSceneName, @"\d+").Value, out int levelIndex);
                    levelIndex--;
                    Time.timeScale = 0;
                    frozen = true;

                    gameManager.loadingScreen.gameObject.SetActive(true);
                    gameManager.progressBar.gameObject.SetActive(false);
                    if (PhotonNetwork.OfflineMode)
                    {
                        DataManager.playerData = SaveSystem.LoadPlayerData("PlayerData");

                        gameManager.startButton.gameObject.SetActive(true);
                        readyButton.gameObject.SetActive(false);
                        if (lastSceneIndex != currentSceneIndex && levelIndex != 0 && levelIndex % 5 == 0)
                        {
                            DataManager.playerData.lives++;
                            StartCoroutine(PopupExtraLife(2.25f));
                        }

                        gameManager.label.Find("Lives").GetComponent<Text>().text = "Lives: " + DataManager.playerData.lives;
                    }
                    else
                    {
                        gameManager.readyButton.gameObject.SetActive(true);
                        startButton.gameObject.SetActive(false);
                        if (lastSceneIndex != currentSceneIndex && levelIndex != 0 && levelIndex % 5 == 0)
                        {
                            PhotonHashtable roomProperties = new PhotonHashtable
                            {
                                { "Total Lives", ((int)PhotonNetwork.CurrentRoom.CustomProperties["Total Lives"]) + 1 }
                            };
                            PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);
                            StartCoroutine(PopupExtraLife(2.25f));

                            gameManager.label.Find("Lives").GetComponent<Text>().text = "Lives: " + (int)PhotonNetwork.CurrentRoom.CustomProperties["Total Lives"] + 1;
                        }
                        else
                        {
                            gameManager.label.Find("Lives").GetComponent<Text>().text = "Lives: " + (int)PhotonNetwork.CurrentRoom.CustomProperties["Total Lives"];
                        }
                    }

                    if (!FindObjectOfType<TankManager>().lastCampaignScene)
                    {
                        gameManager.label.Find("Level").GetComponent<Text>().text = currentSceneName;
                    }
                    else
                    {
                        gameManager.reachedLastLevel = true;
                        gameManager.label.Find("Level").GetComponent<Text>().text = "Final " + Regex.Match(currentSceneName, @"(.*?)[ ][0-9]+$").Groups[1] + " Mission";
                    }
                    gameManager.label.Find("EnemyTanks").GetComponent<Text>().text = "Enemy tanks: " + GameObject.Find("Tanks").transform.childCount;
                }
                lastSceneIndex = currentSceneIndex;
                break;
        }
    }

    // Outside classes can't start coroutines here
    public void LoadNextScene(float delay = 0, bool save = false)
    {
        int activeSceneIndex = SceneManager.GetActiveScene().buildIndex;
        StartCoroutine(LoadSceneRoutine(activeSceneIndex + 1, delay, false, save, true));
    }
    public void PhotonLoadNextScene(float delay = 0, bool save = false)
    {
        int activeSceneIndex = SceneManager.GetActiveScene().buildIndex;
        StartCoroutine(LoadSceneRoutine(activeSceneIndex + 1, delay, true, save, false));
    }

    public void LoadScene(int sceneIndex = -1, float delay = 0, bool save = false, bool waitWhilePaused = true)
    {
        StartCoroutine(LoadSceneRoutine(sceneIndex, delay, false, save, waitWhilePaused));
    }

    public void LoadScene(string sceneName = null, float delay = 0, bool save = false, bool waitWhilePaused = true)
    {
        StartCoroutine(LoadSceneRoutine(sceneName, delay, false, save, waitWhilePaused));
    }

    public void PhotonLoadScene(int sceneIndex = -1, float delay = 0, bool save = false, bool waitWhilePaused = true)
    {
        StartCoroutine(LoadSceneRoutine(sceneIndex, delay, true, save, waitWhilePaused));
    }

    public void PhotonLoadScene(string sceneName = null, float delay = 0, bool save = false, bool waitWhilePaused = true)
    {
        StartCoroutine(LoadSceneRoutine(sceneName, delay, true, save, waitWhilePaused));
    }

    private IEnumerator LoadSceneRoutine(int sceneIndex, float delay, bool photon, bool save, bool waitWhilePaused)
    {
        if (!loadingScene)
        {
            loadingScene = true;
            
            if (sceneIndex < 0)
            {
                sceneIndex = SceneManager.GetActiveScene().buildIndex;
            }

            if (save)
            {
                DataManager.playerData.SavePlayerData("PlayerData", sceneIndex == SceneManager.sceneCountInBuildSettings - 1);
            }

            yield return new WaitForSecondsRealtime(delay);
            if (baseUIHandler != null && waitWhilePaused)
            {
                yield return new WaitWhile(() => baseUIHandler.PauseUIActive());
            }

            if (photon)
            {
                PhotonNetwork.LoadLevel(sceneIndex);

                if (!autoPlay)
                {
                    loadingScreen.gameObject.SetActive(true);
                    startButton.gameObject.SetActive(false);
                    readyButton.gameObject.SetActive(false);
                    progressBar.gameObject.SetActive(true);

                    float progress = Mathf.Clamp01(PhotonNetwork.LevelLoadingProgress / .9f);
                    while (progress < 1)
                    {
                        progress = Mathf.Clamp01(PhotonNetwork.LevelLoadingProgress / .9f);

                        progressBar.GetComponent<Slider>().value = progress;
                        progressBar.Find("Text").GetComponent<Text>().text = progress * 100 + "%";
                        yield return null;
                    }
                }
            }
            else
            {
                AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneIndex);

                if (!autoPlay)
                {
                    loadingScreen.gameObject.SetActive(true);
                    startButton.gameObject.SetActive(false);
                    readyButton.gameObject.SetActive(false);
                    progressBar.gameObject.SetActive(true);

                    while (!asyncLoad.isDone)
                    {
                        float progress = Mathf.Clamp01(asyncLoad.progress / .9f);

                        progressBar.GetComponent<Slider>().value = progress;
                        progressBar.Find("Text").GetComponent<Text>().text = progress * 100 + "%";
                        yield return null;
                    }
                }
            }
        }
    }

    private IEnumerator LoadSceneRoutine(string sceneName, float delay, bool photon, bool save, bool waitWhilePaused)
    {
        if (!loadingScene)
        {
            loadingScene = true;

            if (sceneName == null)
            {
                sceneName = SceneManager.GetActiveScene().name;
            }

            if (save)
            {
                DataManager.playerData.SavePlayerData("PlayerData", sceneName == "End Scene");
            }

            yield return new WaitForSecondsRealtime(delay);
            if (baseUIHandler != null && waitWhilePaused)
            {
                yield return new WaitWhile(() => baseUIHandler.PauseUIActive());
            }

            if (photon)
            {
                PhotonNetwork.LoadLevel(sceneName);

                if (!autoPlay)
                {
                    loadingScreen.gameObject.SetActive(true);
                    startButton.gameObject.SetActive(false);
                    readyButton.gameObject.SetActive(false);
                    progressBar.gameObject.SetActive(true);

                    float progress = Mathf.Clamp01(PhotonNetwork.LevelLoadingProgress / .9f);
                    while (progress < 1)
                    {
                        progress = Mathf.Clamp01(PhotonNetwork.LevelLoadingProgress / .9f);

                        progressBar.GetComponent<Slider>().value = progress;
                        progressBar.Find("Text").GetComponent<Text>().text = progress * 100 + "%";
                        yield return null;
                    }
                }
            }
            else
            {
                AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

                if (!autoPlay)
                {
                    loadingScreen.gameObject.SetActive(true);
                    startButton.gameObject.SetActive(false);
                    readyButton.gameObject.SetActive(false);
                    progressBar.gameObject.SetActive(true);

                    while (!asyncLoad.isDone)
                    {
                        float progress = Mathf.Clamp01(asyncLoad.progress / .9f);

                        progressBar.GetComponent<Slider>().value = progress;
                        progressBar.Find("Text").GetComponent<Text>().text = progress * 100 + "%";
                        yield return null;
                    }
                }
            }
        }
    }

    IEnumerator PopupExtraLife(float delay)
    {
        extraLifePopup.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(delay);
        extraLifePopup.gameObject.SetActive(false);
    }

    public IEnumerator ResetAutoPlay(float startDelay = 0)
    {
        yield return new WaitForEndOfFrame(); // Waiting for scripts and scene to fully load

        Time.timeScale = 1;
        frozen = true;
        FindObjectOfType<LevelGenerator>().GenerateLevel();
        yield return new WaitForSecondsRealtime(startDelay);
        frozen = false;
    }

    public void StartGame()
    {
        Time.timeScale = 1;
        loadingScreen.gameObject.SetActive(false);
        StartCoroutine(DelayedStart());
    }

    public void ToggleReady()
    {
        if (playerPV.IsMine)
        {
            Image readyImage = readyButton.GetComponent<Image>();

            if (readyImage.color == Color.green)
            {
                readyPlayers--;
                PhotonNetwork.RaiseEvent(RemoveReadyPlayerCode, null, RaiseEventOptions.Default, SendOptions.SendUnreliable);
                readyImage.color = Color.red;
            }
            else
            {
                readyPlayers++;
                PhotonNetwork.RaiseEvent(AddReadyPlayerCode, null, RaiseEventOptions.Default, SendOptions.SendUnreliable);
                readyImage.color = Color.green;
            }

            if (readyPlayers >= CustomNetworkHandling.NonSpectatorList.Length)
            {
                readyImage.color = Color.red;
                readyPlayers = 0;
                PhotonNetwork.RaiseEvent(StartEventCode, null, RaiseEventOptions.Default, SendOptions.SendUnreliable);
                StartGame();
            }
        }
    }

    private void OnEnable()
    {
        base.OnEnable();
        Debug.Log("Enabled");
        PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate = 0;
        PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
    }

    private void OnDisable()
    {
        base.OnDisable();
        Debug.Log("Disabled");
        PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;
    }

    void OnEvent(EventData eventData)
    {
        if (eventData.Code == StartEventCode)
        {
            readyButton.GetComponent<Image>().color = Color.red;
            readyPlayers = 0;
            StartGame();
        }
        else if (eventData.Code == LoadSceneEventCode)
        {
            PhotonHashtable parameters = (PhotonHashtable)eventData.Parameters[ParameterCode.Data];
            if (parameters.ContainsKey("sceneName"))
            {
                PhotonLoadScene((string)parameters["sceneName"], (int)parameters["delay"], (bool)parameters["save"], (bool)parameters["waitWhilePaused"]);
            }
            else if (parameters.ContainsKey("sceneIndex"))
            {
                PhotonLoadScene((int)parameters["sceneIndex"], (int)parameters["delay"], (bool)parameters["save"], (bool)parameters["waitWhilePaused"]);
            }
        }
        else if (eventData.Code == AddReadyPlayerCode)
        {
            readyPlayers++;
            Debug.Log(readyPlayers);
            readyPlayers = Mathf.Clamp(readyPlayers, 0, CustomNetworkHandling.NonSpectatorList.Length);
        }
        else if (eventData.Code == RemoveReadyPlayerCode)
        {
            readyPlayers--;
            Debug.Log(readyPlayers);
            readyPlayers = Mathf.Clamp(readyPlayers, 0, CustomNetworkHandling.NonSpectatorList.Length);
        }
    }

    IEnumerator DelayedStart()
    {
        yield return new WaitForEndOfFrame();

        if (PhotonNetwork.OfflineMode)
        {
            baseUIHandler.GetComponent<PlayerUIHandler>().Resume();
        }
        else if (playerPV.IsMine)
        {
            DataManager.playerData = SaveSystem.LoadPlayerData("PlayerData");
            baseUIHandler.GetComponent<PlayerUIHandler>().Resume();
        }

        yield return new WaitForSecondsRealtime(3);
        if (PhotonNetwork.OfflineMode)
        {
            yield return new WaitWhile(() => baseUIHandler.PauseUIActive());
        }
        frozen = false;
    }

    public void MainMenu()
    {
        if (PhotonNetwork.OfflineMode)
        {
            LoadScene("Main Menu", 0, false, false);
        }
        else
        {
            PhotonNetwork.Disconnect();
        }
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene("Lobby");
    }


    public override void OnDisconnected(DisconnectCause cause)
    {
        SceneManager.LoadScene("Main Menu");
        Debug.Log("Disconnected: " + cause.ToString());
    }
}
