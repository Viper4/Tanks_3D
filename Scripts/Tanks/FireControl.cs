using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Pun.UtilityScripts;

public class FireControl : MonoBehaviour
{
    [SerializeField] PhotonView PV;
    [SerializeField] PlayerControl playerControl;

    [SerializeField] Transform barrel;

    [SerializeField] Transform bullet;
    [SerializeField] Transform spawnPoint;
    [SerializeField] Transform shootEffect;
    public Transform bulletParent;

    public float speed = 32;
    [SerializeField] int pierceLevel = 0;
    [SerializeField] int pierceLimit = 0;
    [SerializeField] int ricochetLevel = 1;

    public int bulletLimit = 5;
    public List<Transform> firedBullets { get; set; } = new List<Transform>();
    [SerializeField] float[] fireCooldown = { 2, 4 };
    public bool canFire = true;
    
    [SerializeField] LayerMask solidLayerMask;

    private void Start()
    {
        if (GameManager.autoPlay)
        {
            bulletParent = GameObject.Find("ToClear").transform;
        }
    }

    public bool BulletSpawnClear()
    {
        return !Physics.CheckBox(spawnPoint.position, bullet.GetComponent<Collider>().bounds.size, spawnPoint.rotation, solidLayerMask);
    }

    public IEnumerator Shoot()
    {
        if (canFire && firedBullets.Count < bulletLimit && Time.timeScale != 0 && BulletSpawnClear())
        {
            canFire = false;

            if (transform.CompareTag("Player"))
            {
                DataManager.playerData.shots++;
            }
            
            InstantiateBullet(spawnPoint.position, spawnPoint.rotation);

            if (!PhotonNetwork.OfflineMode && !GameManager.autoPlay)
            {
                PV.RPC("InstantiateBullet", RpcTarget.Others, new object[] { spawnPoint.position, spawnPoint.rotation });
            }

            yield return new WaitForSeconds(Random.Range(fireCooldown[0], fireCooldown[1]));

            canFire = true;
        }
        else
        {
            canFire = true;
            yield return null;
        }
    }

    [PunRPC]
    Transform InstantiateBullet(Vector3 position, Quaternion rotation)
    {
        Transform bulletClone = Instantiate(bullet, position, rotation, bulletParent);
        firedBullets.Add(bulletClone);
        Instantiate(shootEffect, position, rotation, bulletParent);
        StartCoroutine(InitializeBullet(bulletClone));

        return bulletClone;
    }

    IEnumerator InitializeBullet(Transform bullet)
    {
        bullet.gameObject.SetActive(false);
        yield return new WaitUntil(() => bullet.GetComponent<BulletBehaviour>() != null);
        if (bullet != null)
        {
            bullet.gameObject.SetActive(true);

            BulletBehaviour bulletBehaviour = bullet.GetComponent<BulletBehaviour>();
            bulletBehaviour.owner = transform;
            bulletBehaviour.ownerPV = PV;
            bulletBehaviour.speed = speed;
            bulletBehaviour.pierceLevel = pierceLevel;
            bulletBehaviour.pierceLimit = pierceLimit;
            bulletBehaviour.ricochetLevel = ricochetLevel;
            bulletBehaviour.ResetVelocity();
        }
        else
        {
            firedBullets.Remove(bullet);
        }
    }
}
