using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class MultiplayerCameraControl : MonoBehaviour
{
    Camera thisCamera;

    public float sensitivity = 15;
    [SerializeField] Transform target;
    public Transform reticle;

    [SerializeField] PlayerControl playerControl;

    Transform tankOrigin;
    Transform body;
    Transform turret;
    Transform barrel;

    Vector3 lastEulerAngles;

    [SerializeField] float dstFromTarget = 4;
    [SerializeField] Vector2 targetDstMinMax = new Vector2(0, 30);

    [SerializeField] Vector2 pitchMinMaxN = new Vector2(-40, 80);
    [SerializeField] Vector2 pitchMinMaxL = new Vector2(-20, 20);

    Vector2 pitchMinMax = new Vector2(-40, 80);

    [SerializeField] float rotationSmoothTime = 0.1f;
    Vector3 rotationSmoothVelocity;
    Vector3 currentRotation;

    [SerializeField] LayerMask mouseIgnoreLayers;
    [SerializeField] LayerMask cameraIgnoreLayers;

    float yaw;
    float pitch;
    bool lockTurret = false;
    bool lockCamera = false;
    bool switchCamera = false;

    // Start is called before the first frame Update
    void Start()
    {
        if (playerControl.ClientManager.ViewIsMine())
        {
            thisCamera = GetComponent<Camera>();

            // If target is not set, automatically set it to the parent
            if (target == null)
            {
                target = transform.parent;
            }

            tankOrigin = transform.parent.Find("Tank Origin");
            body = tankOrigin.Find("Body");
            turret = tankOrigin.Find("Turret");
            barrel = tankOrigin.Find("Barrel");

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            lastEulerAngles = tankOrigin.eulerAngles;

            // Disabling all other cameras except for this client's
            GameObject[] allCameras = GameObject.FindGameObjectsWithTag("MainCamera");
            foreach (GameObject camera in allCameras)
            {
                if (camera != gameObject)
                {
                    camera.SetActive(false);
                }
            }

            // Disabling all other audio listeners except for this client's
            AudioListener[] audioListeners = FindObjectsOfType<AudioListener>();
            foreach (AudioListener audioListener in audioListeners)
            {
                if (audioListener != transform.GetComponent<AudioListener>())
                {
                    audioListener.enabled = false;
                }
            }
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (playerControl.ClientManager.ViewIsMine() && !playerControl.Paused)
        {
            // Updating every username to rotate to this camera for this client
            UsernameSystem[] allUsernames = FindObjectsOfType<UsernameSystem>();
            foreach (UsernameSystem username in allUsernames)
            {
                username.UpdateTextMeshTo(transform);
            }

            float zoomRate = Input.GetKey(KeyCode.LeftShift) ? 0.5f : 5f;
            // Zoom with scroll
            if (Input.GetAxisRaw("Mouse ScrollWheel") > 0)
            {
                dstFromTarget = Mathf.Clamp(dstFromTarget - zoomRate, targetDstMinMax.x, targetDstMinMax.y);
            }
            else if (Input.GetAxisRaw("Mouse ScrollWheel") < 0)
            {
                dstFromTarget = Mathf.Clamp(dstFromTarget + zoomRate, targetDstMinMax.x, targetDstMinMax.y);
            }

            // Lock turret toggle
            if (Input.GetKeyDown(playerControl.dataSystem.currentSettings.keyBinds["Lock Turret"]))
            {
                lockTurret = !lockTurret;
            }
            else if (!switchCamera && Input.GetKeyDown(playerControl.dataSystem.currentSettings.keyBinds["Lock Camera"]))
            {
                lockCamera = !lockCamera;
            }
            else if (Input.GetKeyDown(playerControl.dataSystem.currentSettings.keyBinds["Switch Camera"]))
            {
                switchCamera = !switchCamera;
                lockCamera = false;
            }

            if (!playerControl.Dead && Time.timeScale != 0)
            {
                // Unlinking y eulers of turret, barrel, and target from parent
                turret.eulerAngles = new Vector3(turret.eulerAngles.x, turret.eulerAngles.y + lastEulerAngles.y - tankOrigin.eulerAngles.y, turret.eulerAngles.z);
                barrel.eulerAngles = target.eulerAngles = new Vector3(barrel.eulerAngles.x, barrel.eulerAngles.y + lastEulerAngles.y - tankOrigin.eulerAngles.y, barrel.eulerAngles.z);

                reticle.gameObject.SetActive(true);
                Cursor.visible = false;
                if (switchCamera)
                {
                    transform.eulerAngles = new Vector3(90, 0, 0);
                    transform.position = new Vector3(0, 30 + dstFromTarget, 0);
                }

                if (dstFromTarget == 0 && !switchCamera)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    pitchMinMax = pitchMinMaxL;

                    turret.rotation = barrel.rotation = transform.rotation;
                    reticle.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0);

                    barrel.GetComponent<MeshRenderer>().enabled = false;
                }
                else
                {
                    barrel.GetComponent<MeshRenderer>().enabled = true;

                    Cursor.lockState = CursorLockMode.Confined;
                    pitchMinMax = pitchMinMaxN;

                    if (!lockTurret)
                    {
                        RotateToMousePoint();
                    }
                    else
                    {
                        if (Physics.Raycast(barrel.position + barrel.forward, barrel.forward, out RaycastHit barrelHit, Mathf.Infinity, ~mouseIgnoreLayers))
                        {
                            reticle.position = transform.GetComponent<Camera>().WorldToScreenPoint(barrelHit.point);
                        }
                    }
                }

                // Zeroing x and z eulers of turret and clamping barrel x euler
                turret.localEulerAngles = new Vector3(0, turret.localEulerAngles.y, 0);
                barrel.localEulerAngles = new Vector3(Clamping.ClampAngle(barrel.localEulerAngles.x, pitchMinMaxL.x, pitchMinMaxL.y), barrel.localEulerAngles.y, 0);

                lastEulerAngles = tankOrigin.eulerAngles;
            }
            else
            {
                reticle.position = Input.mousePosition;
            }

            if (!switchCamera)
            {
                if (!lockCamera)
                {
                    // Translating inputs from mouse into smoothed rotation of camera
                    yaw += Input.GetAxis("Mouse X") * sensitivity / 4;
                    pitch -= Input.GetAxis("Mouse Y") * sensitivity / 4;
                    pitch = Mathf.Clamp(pitch, pitchMinMax.x, pitchMinMax.y);

                    currentRotation = Vector3.SmoothDamp(currentRotation, new Vector3(pitch, yaw), ref rotationSmoothVelocity, rotationSmoothTime);
                    // Setting rotation and position of camera on previous params and target and dstFromTarget
                    transform.eulerAngles = currentRotation;
                }
                transform.position = target.position - transform.forward * dstFromTarget;

                // Prevent clipping of camera
                if (Physics.Raycast(target.position, (transform.position - target.position).normalized, out RaycastHit clippingHit, dstFromTarget, ~cameraIgnoreLayers))
                {
                    transform.position = clippingHit.point - (transform.position - target.position).normalized;
                }
            }
        }
    }

    void RotateToMousePoint()
    {
        Ray mouseRay = thisCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(mouseRay, out RaycastHit mouseHit, Mathf.Infinity, ~mouseIgnoreLayers))
        {
            Debug.DrawLine(transform.position, mouseHit.point, Color.red, 0.1f);
            // Rotating turret and barrel towards the mouseHit point
            //Vector3 dir = Vector3.RotateTowards(target.forward, mouseHit.point - target.position, Time.deltaTime * 3, 0);
            Quaternion lookRotation = Quaternion.LookRotation(mouseHit.point - target.position, tankOrigin.up);

            turret.rotation = Quaternion.AngleAxis(lookRotation.eulerAngles.y, Vector3.up);
            barrel.rotation = target.rotation = lookRotation;
        }
        reticle.position = Input.mousePosition;
    }
}