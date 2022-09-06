using System.Collections;
using UnityEngine;
using Photon.Pun;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;
using ExitGames.Client.Photon;
using Photon.Realtime;
using Photon.Pun.UtilityScripts;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class PlayerControl : MonoBehaviourPunCallbacks
{
    public ClientManager clientManager;

    [SerializeField] Rigidbody RB;
    [SerializeField] BaseTankLogic baseTankLogic;
    [SerializeField] Transform mainCamera;

    [SerializeField] Transform tankOrigin;

    [SerializeField] bool cheats = false;
    public bool godMode = false;
    public bool Dead { get; set; } = false;
    public bool Paused { get; set; } = false;
    public bool showHUD = true;

    bool respawning = false;

    float currentSpeed;
    public float speedSmoothTime = 0.1f;
    float speedSmoothVelocity;

    public float turnSmoothTime = 0.2f;
    float turnSmoothVelocity;
    float velocityY = 0;

    [SerializeField] LayerMask ignoreLayerMasks;

    // Update is called once per frame
    void LateUpdate()
    {
        if (PhotonNetwork.OfflineMode || clientManager.photonView.IsMine)
        {
            if (!Dead && !GameManager.frozen)
            {
                if (Time.timeScale != 0)
                {
                    Vector2 inputDir = Vector2.zero;

                    if (!Paused)
                    {
                        if (Input.GetKeyDown(DataManager.playerSettings.keyBinds["Shoot"]))
                        {
                            StartCoroutine(GetComponent<FireControl>().Shoot());
                        }
                        else if (Input.GetKeyDown(DataManager.playerSettings.keyBinds["Lay Mine"]) && baseTankLogic.IsGrounded())
                        {
                            StartCoroutine(GetComponent<MineControl>().LayMine());
                        }
                        else if (Input.GetKeyDown(DataManager.playerSettings.keyBinds["Toggle HUD"]))
                        {
                            showHUD = !showHUD;
                        }
                        inputDir = new Vector2(GetInputAxis("Horizontal"), GetInputAxis("Vertical")).normalized;
                    }

                    // Moving the tank with player input

                    float targetSpeed = baseTankLogic.normalSpeed * 0.5f * inputDir.magnitude;

                    currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, GetModifiedSmoothTime(speedSmoothTime));

                    Vector3 velocityDir = tankOrigin.forward;

                    if (Physics.Raycast(tankOrigin.position, -tankOrigin.up, out RaycastHit middleHit, 1, ~ignoreLayerMasks) && Physics.Raycast(tankOrigin.position + tankOrigin.forward, -tankOrigin.up, out RaycastHit frontHit, 1, ~ignoreLayerMasks))
                    {
                        velocityDir = frontHit.point - middleHit.point;
                    }

                    Vector3 velocity = currentSpeed * velocityDir;

                    velocityY = !baseTankLogic.IsGrounded() && baseTankLogic.useGravity ? velocityY - Time.deltaTime * baseTankLogic.gravity : 0;
                    velocityY = Mathf.Clamp(velocityY, -baseTankLogic.velocityLimit, baseTankLogic.velocityLimit);

                    RB.velocity = velocity + Vector3.up * velocityY;

                    // Rotating tank with movement
                    if (inputDir != Vector2.zero)
                    {
                        float targetRotation = Mathf.Atan2(inputDir.x, inputDir.y) * Mathf.Rad2Deg + mainCamera.eulerAngles.y;
                        float angle = tankOrigin.eulerAngles.y - targetRotation;
                        angle = angle < 0 ? angle + 360 : angle;
                        if (angle > 180 - baseTankLogic.flipAngleThreshold && angle < 180 + baseTankLogic.flipAngleThreshold)
                        {
                            baseTankLogic.FlipTank();
                        }
                        else
                        {
                            tankOrigin.eulerAngles = new Vector3(tankOrigin.eulerAngles.x, Mathf.SmoothDampAngle(tankOrigin.eulerAngles.y, targetRotation, ref turnSmoothVelocity, GetModifiedSmoothTime(turnSmoothTime)), tankOrigin.eulerAngles.z);
                        }
                    }
                }
            }
            else
            {
                RB.velocity = Vector3.zero;
            }

            if (cheats)
            {
                if (Input.GetKey(KeyCode.LeftAlt))
                {
                    if (Input.GetKeyDown(KeyCode.N))
                    {
                        Debug.Log("Cheat Next Level");
                        GameManager.gameManager.LoadNextScene();
                    }
                    else if (Input.GetKeyDown(KeyCode.R))
                    {
                        Debug.Log("Cheat Reload");
                        GameManager.gameManager.LoadScene(-1);
                    }
                    else if (Input.GetKeyDown(KeyCode.B))
                    {
                        Debug.Log("Cheat Reset");
                        Dead = false;

                        tankOrigin.Find("Body").GetComponent<MeshRenderer>().enabled = true;
                        tankOrigin.Find("Turret").GetComponent<MeshRenderer>().enabled = true;
                        tankOrigin.Find("Barrel").GetComponent<MeshRenderer>().enabled = true;
                    }
                    else if (Input.GetKeyDown(KeyCode.G))
                    {
                        Debug.Log("God Mode Toggled");

                        godMode = !godMode;
                    }
                }
            }
        }
    }

    float GetModifiedSmoothTime(float smoothTime)
    {
        if (baseTankLogic.IsGrounded())
        {
            return smoothTime;
        }

        return smoothTime / 0.1f;
    }

    private float GetInputAxis(string axis)
    {
        switch (axis)
        {
            case "Horizontal":
                float horizontal = 0;
                if (Input.GetKey(DataManager.playerSettings.keyBinds["Right"]))
                {
                    horizontal += 1;
                }
                if (Input.GetKey(DataManager.playerSettings.keyBinds["Left"]))
                {
                    horizontal -= 1;
                }
                return horizontal;
            case "Vertical":
                float vertical = 0;
                if (Input.GetKey(DataManager.playerSettings.keyBinds["Forward"]))
                {
                    vertical += 1;
                }
                if (Input.GetKey(DataManager.playerSettings.keyBinds["Backward"]))
                {
                    vertical -= 1;
                }
                return vertical;
        }
        
        return 0;
    }

    public void OnDeath()
    {
        if (!PhotonNetwork.OfflineMode)
        {
            transform.SetParent(null);

            if (clientManager.photonView.IsMine)
            {
                DataManager.playerData.deaths++;

                PhotonHashtable playerProperties = new PhotonHashtable();
                PhotonHashtable roomProperties = new PhotonHashtable();

                RoomSettings roomSettings = (RoomSettings)PhotonNetwork.CurrentRoom.CustomProperties["RoomSettings"];

                switch (roomSettings.primaryMode)
                {
                    case "Co-Op":
                        int totalLives = (int)PhotonNetwork.CurrentRoom.CustomProperties["Total Lives"];
                        totalLives--;
                        roomProperties.Add("Total Lives", totalLives);

                        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProperties);

                        if (FindObjectOfType<PlayerManager>().transform.childCount < 1)
                        {
                            GameManager.frozen = true;
                            if (totalLives > 0)
                            {
                                GameManager.gameManager.PhotonLoadScene(-1, 3, true, false);
                                Hashtable parameters = new Hashtable
                                {
                                    { "sceneIndex", -1 },
                                    { "delay", 3 },
                                    { "save", true },
                                    { "waitWhilePaused", false }
                                };
                                PhotonNetwork.RaiseEvent(GameManager.gameManager.LoadSceneEventCode, parameters, RaiseEventOptions.Default, SendOptions.SendUnreliable);
                            }
                            else
                            {
                                GameManager.gameManager.PhotonLoadScene("End Scene", 3, true, false);
                                Hashtable parameters = new Hashtable
                                {
                                    { "sceneName", "End Scene" },
                                    { "delay", 3 },
                                    { "save", true },
                                    { "waitWhilePaused", false }
                                };
                                PhotonNetwork.RaiseEvent(GameManager.gameManager.LoadSceneEventCode, parameters, RaiseEventOptions.Default, SendOptions.SendUnreliable);
                            }
                        }
                        break;
                    default:
                        if (!respawning)
                        {
                            playerProperties.Add("Deaths", DataManager.playerData.deaths);
                            PhotonNetwork.LocalPlayer.SetCustomProperties(playerProperties);

                            StartCoroutine(MultiplayerRespawn());
                        }
                        break;
                }
            }
        }
        else
        {
            DataManager.playerData.deaths++;
            DataManager.playerData.lives--;

            if (DataManager.playerData.lives > 0)
            {
                GameManager.gameManager.LoadScene(-1, 3, true);
            }
            else
            {
                GameManager.gameManager.LoadScene("End Scene", 3, true);
            }
        }
    }
     
    IEnumerator MultiplayerRespawn()
    {
        respawning = true;
        yield return new WaitForSeconds(3);

        Dead = false;

        tankOrigin.localPosition = Vector3.zero;
        tankOrigin.localRotation = Quaternion.identity;
        FindObjectOfType<PlayerManager>().RespawnPlayer(tankOrigin);

        clientManager.photonView.RPC("ReactivatePlayer", RpcTarget.All);

        respawning = false;
    }
    
    [PunRPC]
    void ReactivatePlayer()
    {
        tankOrigin.GetComponent<CapsuleCollider>().enabled = true;

        tankOrigin.Find("Body").gameObject.SetActive(true);
        tankOrigin.Find("Turret").gameObject.SetActive(true);
        tankOrigin.Find("Barrel").gameObject.SetActive(true);

        tankOrigin.Find("TrackMarks").GetComponent<PhotonView>().RPC("ResetTrails", RpcTarget.All);
    }
}
