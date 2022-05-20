using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class TealBot : MonoBehaviour
{
    public Transform target;
    float dstToTarget;
    Quaternion rotToTarget;

    [SerializeField] LayerMask targetLayerMasks;

    BaseTankLogic baseTankLogic;

    Transform body;
    Transform turret;
    Transform barrel;
    Quaternion turretAnchor;

    public float shootRadius = 99f;

    public float[] reactionTime = { 0.3f, 0.45f };

    public float[] fireDelay = { 0.3f, 0.6f };
    float cooldown = 0;

    Rigidbody rb;

    [SerializeField] bool randomizeSeed = true;

    [SerializeField] float turretRotSpeed = 25f;

    [SerializeField] float[] turretRangeX = { -20, 20 };

    [SerializeField] float tankRotSpeed = 250f;
    [SerializeField] float tankRotNoiseScale = 5;
    [SerializeField] float tankRotNoiseSpeed = 0.5f;
    [SerializeField] float tankRotSeed = 0;

    Vector3 lastEulerAngles;

    public float speed = 3;
    public float gravity = 10;
    float velocityY = 0;

    float triggerRadius = 3.5f;

    Coroutine shootRoutine;

    enum Mode
    {
        Move,
        Shoot,
        Avoid
    }
    Mode mode = Mode.Move;

    // Start is called before the first frame Update
    void Awake()
    {
        if (target == null)
        {
            Debug.Log("The variable target of GreyBot has been defaulted to player's Camera Target");
            target = GameObject.Find("Player").transform.Find("Camera Target");
        }

        baseTankLogic = GetComponent<BaseTankLogic>();

        body = transform.Find("Body");
        turret = transform.Find("Turret");
        barrel = transform.Find("Barrel");

        rb = GetComponent<Rigidbody>();

        lastEulerAngles = transform.eulerAngles;

        if (randomizeSeed)
        {
            tankRotSeed = Random.Range(0f, 99.0f);
        }

        triggerRadius = GetComponent<SphereCollider>().radius;
    }

    // Update is called once per frame
    void Update()
    {
        cooldown = cooldown > 0 ? cooldown - Time.deltaTime : 0;
        dstToTarget = Vector3.Distance(transform.position, target.position);

        if (dstToTarget < shootRadius && mode != Mode.Shoot && cooldown == 0)
        {
            // If nothing blocking player
            if (!Physics.Raycast(turret.position, target.position - turret.position, dstToTarget, ~targetLayerMasks))
            {
                shootRoutine = StartCoroutine(Shoot());
            }
        }
        else if (shootRoutine != null && Physics.Raycast(turret.position, target.position - turret.position, dstToTarget, ~targetLayerMasks))
        {
            StopCoroutine(shootRoutine);
            shootRoutine = null;
            mode = Mode.Move;
        }

        if (rb != null)
        {
            // Movement
            Vector3 velocity;
            velocityY = baseTankLogic.IsGrounded() ? 0 : velocityY - Time.deltaTime * gravity;
            Vector3 targetDirection = transform.forward;

            if (mode == Mode.Move || mode == Mode.Avoid)
            {
                if (Physics.Raycast(transform.position, -transform.up, out RaycastHit middleHit, 1) && Physics.Raycast(transform.position + transform.forward, -transform.up, out RaycastHit frontHit, 1))
                {
                    targetDirection = frontHit.point - middleHit.point;
                }
            }
            else
            {
                targetDirection = Vector3.zero;
            }

            velocity = targetDirection * speed + Vector3.up * velocityY;

            rb.velocity = velocity;

            if (mode == Mode.Move)
            {
                // Adding noise to rotation
                float noise = tankRotNoiseScale * (Mathf.PerlinNoise(tankRotSeed + Time.time * tankRotNoiseSpeed, (tankRotSeed + 1) + Time.time * tankRotNoiseSpeed) - 0.5f);
                Quaternion desiredTankRot = Quaternion.LookRotation(Quaternion.AngleAxis(noise, Vector3.up) * transform.forward);
                rb.rotation = Quaternion.RotateTowards(transform.rotation, desiredTankRot, Time.deltaTime * tankRotSpeed);
            }
        }

        // Correcting turret and barrel y rotation to not depend on the parent
        turret.eulerAngles = new Vector3(turret.eulerAngles.x, turret.eulerAngles.y + lastEulerAngles.y - transform.eulerAngles.y, turret.eulerAngles.z);
        barrel.eulerAngles = new Vector3(barrel.eulerAngles.x, barrel.eulerAngles.y + lastEulerAngles.y - transform.eulerAngles.y, barrel.eulerAngles.z);

        // Rotating turret and barrel towards player
        Vector3 targetDir = target.position - turret.position;
        rotToTarget = Quaternion.LookRotation(targetDir);
        turret.rotation = barrel.rotation = turretAnchor = Quaternion.RotateTowards(turretAnchor, rotToTarget, Time.deltaTime * turretRotSpeed);

        // Zeroing x and z eulers of turret and clamping barrel x euler
        turret.localEulerAngles = new Vector3(0, turret.localEulerAngles.y, 0);
        barrel.localEulerAngles = new Vector3(Clamping.ClampAngle(barrel.localEulerAngles.x, turretRangeX[0], turretRangeX[1]), barrel.localEulerAngles.y, 0);
        lastEulerAngles = transform.eulerAngles;
    }

    private void OnTriggerStay(Collider other)
    {
        if (mode != Mode.Shoot)
        {
            Vector3 desiredDir;
            // Avoiding bullets, mines, and obstacles
            switch (other.tag)
            {
                case "Bullet":
                    // If bullet is going to hit then dodge by Move perpendicular to bullet path
                    RaycastHit bulletHit;
                    if (Physics.Raycast(other.transform.position, other.transform.forward, out bulletHit))
                    {
                        if (bulletHit.transform == transform)
                        {
                            desiredDir = Random.Range(0, 2) == 0 ? Quaternion.AngleAxis(90, Vector3.up) * (other.transform.position - turret.position) : Quaternion.AngleAxis(-90, Vector3.up) * (other.transform.position - turret.position);

                            // Applying rotation
                            rb.MoveRotation(Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(desiredDir), Time.deltaTime * tankRotSpeed * 1.4f));
                        }
                    }
                    break;
                case "Mine":
                    // Move in opposite direction of mine
                    desiredDir = transform.position - other.transform.position;

                    // Applying rotation
                    rb.MoveRotation(Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(desiredDir), Time.deltaTime * tankRotSpeed * 1.2f));
                    break;
                default:
                    // Obstacle Avoidance
                    bool[] pathClear = { true, true, true };
                    float dotProductY = 0;

                    // Checking Forward
                    RaycastHit forwardHit;
                    if (Physics.Raycast(body.position, transform.forward, out forwardHit, triggerRadius)) // center
                    {
                        // Cross product doesn't give absolute value of angle
                        dotProductY = Vector3.Dot(Vector3.Cross(transform.forward, forwardHit.normal), transform.up);
                        pathClear[1] = false;
                    }
                    else if (Physics.Raycast(body.position + transform.right, transform.forward, out forwardHit, triggerRadius)) // left side
                    {
                        dotProductY = Vector3.Dot(Vector3.Cross(transform.forward, forwardHit.normal), transform.up);
                        pathClear[1] = false;
                    }
                    else if (Physics.Raycast(body.position - transform.right, transform.forward, out forwardHit, triggerRadius)) // right side
                    {
                        dotProductY = Vector3.Dot(Vector3.Cross(transform.forward, forwardHit.normal), transform.up);
                        pathClear[1] = false;
                    }
                    Debug.DrawLine(body.position, body.position + transform.forward * triggerRadius, Color.blue, 0.1f);

                    if (!pathClear[1])
                    {
                        mode = Mode.Avoid;

                        // Checking Left
                        if (Physics.Raycast(body.position, -transform.right, triggerRadius))
                        {
                            pathClear[0] = false;
                        }
                        Debug.DrawLine(body.position, body.position - transform.right * 3, Color.red, 0.1f);
                        // Checking Right
                        if (Physics.Raycast(body.position, transform.right, triggerRadius))
                        {
                            pathClear[2] = false;
                        }
                        Debug.DrawLine(body.position, body.position + transform.right * 3, Color.red, 0.1f);

                        // Over rotating on left and right to prevent jittering
                        if (pathClear[0])
                        {
                            if (pathClear[2])
                            {
                                // If the obstacle is directly in front of tank, turn left or right randomly
                                if (dotProductY == 0)
                                {
                                    // Rotate left or right
                                    desiredDir = Random.Range(0, 2) == 0 ? Quaternion.AngleAxis(-10, Vector3.up) * -transform.right : Quaternion.AngleAxis(10, Vector3.up) * transform.right;
                                }
                                else
                                {
                                    // Rotate left if obstacle is facing left
                                    desiredDir = dotProductY < 0 ? Quaternion.AngleAxis(-10, Vector3.up) * -transform.right : Quaternion.AngleAxis(10, Vector3.up) * transform.right;
                                }
                            }
                            else
                            {
                                // Rotate left
                                desiredDir = Quaternion.AngleAxis(-10, Vector3.up) * -transform.right;
                            }
                        }
                        else if (pathClear[2])
                        {
                            // Rotate right
                            desiredDir = Quaternion.AngleAxis(10, Vector3.up) * transform.right;
                        }
                        else
                        {
                            // Rotate backward
                            desiredDir = -transform.forward;
                        }

                        // Applying rotation
                        rb.MoveRotation(Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(desiredDir), Time.deltaTime * tankRotSpeed));
                    }
                    else
                    {
                        mode = Mode.Move;
                    }
                    break;
            }
        }
    }

    IEnumerator Shoot()
    {
        mode = Mode.Shoot;

        // Waiting for tank to move forward a bit more
        yield return new WaitForSeconds(0.5f);

        // Reaction time from seeing player
        yield return new WaitForSeconds(Random.Range(reactionTime[0], reactionTime[1]));
        cooldown = GetComponent<FireControl>().fireCooldown;
        StartCoroutine(GetComponent<FireControl>().Shoot());
        yield return new WaitForSeconds(Random.Range(fireDelay[0], fireDelay[1]));

        shootRoutine = null;
        mode = Mode.Move;
    }
}
