using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrownBot : MonoBehaviour
{
    BaseTankLogic baseTankLogic;

    float dstToTarget;

    Transform turret;
    Transform barrel;

    public float[] fireDelay = { 1, 3 };

    [SerializeField] float turretRotSpeed = 20;

    [SerializeField] Vector2 turretScanRange = new Vector2(8, 45);
    [SerializeField] float[] scanChangeDelay = { 3, 6 };
    float scanOffset = 0;
    float currentScanOffset = 0;

    bool shooting = false;

    TargetSelector targetSelector;

    // Start is called before the first frame Update
    void Awake()
    {
        barrel = transform.Find("Barrel");
        turret = transform.Find("Turret");

        if (GetComponent<TargetSelector>() != null)
        {
            targetSelector = GetComponent<TargetSelector>();
        }

        baseTankLogic = GetComponent<BaseTankLogic>();

        StartCoroutine(ChangeScan());
    }

    // Update is called once per frame
    void Update()
    {
        if (!SceneLoader.frozen && Time.timeScale != 0 && targetSelector.target != null)
        {
            dstToTarget = Vector3.Distance(transform.position, targetSelector.target.position);

            float angleX = Mathf.PingPong(Time.time * turretRotSpeed, turretScanRange.x * 2) - turretScanRange.x;
            float angleY = Mathf.PingPong(Time.time * turretRotSpeed, turretScanRange.y * 2) - turretScanRange.y;

            currentScanOffset += (scanOffset + angleY - currentScanOffset) * (Time.deltaTime * turretRotSpeed / 30);

            turret.localEulerAngles = new Vector3(0, currentScanOffset, 0);
            barrel.localEulerAngles = new Vector3(angleX, currentScanOffset, 0);

            // If target is in front of barrel then fire
            if (!shooting && Physics.Raycast(barrel.position + barrel.forward, barrel.forward, out RaycastHit barrelHit, dstToTarget, ~baseTankLogic.transparentLayers, QueryTriggerInteraction.Ignore))
            {
                // Ray hits the capsule collider which is on Tank Origin for player and the 2nd topmost transform for tank bots
                if (barrelHit.transform.root.name == "Player" && targetSelector.target.root.name == "Player")
                {
                    StartCoroutine(Shoot());
                }
                else if (barrelHit.transform == targetSelector.target.parent) // target for tank bots is the turret
                {
                    StartCoroutine(Shoot());
                }
            }
        }
    }

    IEnumerator Shoot()
    {
        shooting = true;

        yield return new WaitForSeconds(Random.Range(fireDelay[0], fireDelay[1]));
        StartCoroutine(GetComponent<FireControl>().Shoot());

        shooting = false;
    }
    
    IEnumerator ChangeScan()
    {
        yield return new WaitForSeconds(Random.Range(scanChangeDelay[0], scanChangeDelay[1]));
        scanOffset = Random.Range(-180.0f, 180.0f);
        
        StartCoroutine(ChangeScan());
    }
}
