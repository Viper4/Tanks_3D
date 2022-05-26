using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TealBot : MonoBehaviour
{
    TargetSelector targetSelector;
    Quaternion rotToTarget;

    BaseTankLogic baseTankLogic;

    Transform body;
    Transform turret;
    Transform barrel;
    Quaternion turretAnchor;
    Vector3 lastEulerAngles;

    public float[] reactionTime = { 0.3f, 0.45f };
    public float[] fireDelay = { 0.3f, 0.6f };

    Rigidbody rb;

    [SerializeField] float turretRotSpeed = 25f;
    [SerializeField] float[] turretRangeX = { -20, 20 };
    
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float avoidSpeed = 2f;
    float speed = 4;

    [SerializeField] float gravity = 10;
    float velocityY = 0;
    
    FireControl fireControl;
    bool shooting = false;

    enum Mode
    {
        Normal,
        Shoot,
        Avoid
    }
    Mode mode = Mode.Normal;

    // Start is called before the first frame Update
    void Awake()
    {
        targetSelector = GetComponent<TargetSelector>();

        baseTankLogic = GetComponent<BaseTankLogic>();

        body = transform.Find("Body");
        turret = transform.Find("Turret");
        barrel = transform.Find("Barrel");
        turretAnchor = turret.rotation;
        lastEulerAngles = transform.eulerAngles;

        rb = GetComponent<Rigidbody>();

        fireControl = GetComponent<FireControl>();
    }

    // Update is called once per frame
    void Update()
    {
        if(!SceneLoader.frozen && Time.timeScale != 0)
        {
            if (fireControl.canFire && mode != Mode.Shoot && !shooting && Physics.Raycast(barrel.position, barrel.forward, out RaycastHit barrelHit, Mathf.Infinity, ~baseTankLogic.transparentLayers, QueryTriggerInteraction.Ignore))
            {
                // Ray hits the capsule collider which is on Tank Origin for player and the 2nd topmost transform for tank bots
                if (barrelHit.transform.root.name == "Player" && targetSelector.currentTarget.root.name == "Player")
                {
                    StartCoroutine(Shoot());
                }
                else if (barrelHit.transform == targetSelector.currentTarget.parent || barrelHit.transform == targetSelector.currentTarget) // target for tank bots is the turret, everything else is itself
                {
                    StartCoroutine(Shoot());
                }
            }

            if (rb != null)
            {
                // Movement
                Vector3 velocity;
                velocityY = baseTankLogic.IsGrounded() ? 0 : velocityY - Time.deltaTime * gravity;

                Vector3 targetDirection = transform.forward;
                if (Physics.Raycast(transform.position, -transform.up, out RaycastHit middleHit, 1) && Physics.Raycast(transform.position + transform.forward, -transform.up, out RaycastHit frontHit, 1))
                {
                    targetDirection = frontHit.point - middleHit.point;
                }

                // Checking Forward on the center, left, and right side
                RaycastHit forwardHit;
                if (Physics.Raycast(body.position, transform.forward, out forwardHit, 2, baseTankLogic.barrierLayers) || Physics.Raycast(body.position + transform.right, transform.forward, out forwardHit, 2, baseTankLogic.barrierLayers) || Physics.Raycast(body.position - transform.right, transform.forward, out forwardHit, 2, baseTankLogic.barrierLayers))
                {
                    mode = Mode.Avoid;
                    baseTankLogic.ObstacleAvoidance(forwardHit, 2, baseTankLogic.barrierLayers);
                }
                else if (mode == Mode.Avoid)
                {
                    mode = Mode.Normal;
                }

                switch (mode)
                {
                    case Mode.Normal:
                        speed = moveSpeed;

                        baseTankLogic.noisyRotation = true;
                        break;
                    case Mode.Avoid:
                        speed = avoidSpeed;

                        baseTankLogic.noisyRotation = false;
                        break;
                    case Mode.Shoot:
                        speed = 0;

                        baseTankLogic.noisyRotation = false;
                        break;
                }

                velocity = targetDirection * speed + Vector3.up * velocityY;

                rb.velocity = velocity;
            }

            // Correcting turret and barrel y rotation to not depend on the parent
            turret.eulerAngles = new Vector3(turret.eulerAngles.x, turret.eulerAngles.y + lastEulerAngles.y - transform.eulerAngles.y, turret.eulerAngles.z);
            barrel.eulerAngles = new Vector3(barrel.eulerAngles.x, barrel.eulerAngles.y + lastEulerAngles.y - transform.eulerAngles.y, barrel.eulerAngles.z);

            // Rotating turret and barrel towards player
            Vector3 targetDir = targetSelector.currentTarget.position - turret.position;
            rotToTarget = Quaternion.LookRotation(targetDir);
            turret.rotation = barrel.rotation = turretAnchor = Quaternion.RotateTowards(turretAnchor, rotToTarget, Time.deltaTime * turretRotSpeed);

            // Zeroing x and z eulers of turret and clamping barrel x euler
            turret.localEulerAngles = new Vector3(0, turret.localEulerAngles.y, 0);
            barrel.localEulerAngles = new Vector3(Clamping.ClampAngle(barrel.localEulerAngles.x, turretRangeX[0], turretRangeX[1]), barrel.localEulerAngles.y, 0);
            lastEulerAngles = transform.eulerAngles;
        }
        else
        {
            rb.velocity = Vector3.zero;
        }
    }

    void OnTriggerStay(Collider other)
    {
        Vector3 desiredDir;
        // Avoiding mines
        switch (other.tag)
        {
            case "Mine":
                if (SceneLoader.autoPlay)
                {
                    // Move in opposite direction of mine
                    desiredDir = transform.position - other.transform.position;

                    // Applying rotation
                    baseTankLogic.RotateToVector(desiredDir);
                }
                else if (other.GetComponent<MineBehaviour>().owner != targetSelector.currentTarget.root)
                {
                    // Move in opposite direction of mine
                    desiredDir = transform.position - other.transform.position;

                    // Applying rotation
                    baseTankLogic.RotateToVector(desiredDir);
                }
                break;
        }
    }
    
    IEnumerator Shoot()
    {
        shooting = true;
        // Keeps moving until reaction time from seeing player is reached
        yield return new WaitForSeconds(Random.Range(reactionTime[0], reactionTime[1]));
        // Stops moving and delay in firing
        mode = Mode.Shoot;
        yield return new WaitForSeconds(Random.Range(fireDelay[0], fireDelay[1]));
        StartCoroutine(GetComponent<FireControl>().Shoot());

        mode = Mode.Normal;
        shooting = false;
    }
}
