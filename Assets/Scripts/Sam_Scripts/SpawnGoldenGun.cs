using UnityEngine;

public class SpawnGoldenGun : MonoBehaviour
{
    public bool hasSpawned = false;
    public GameObject gunPrefab;
    public Transform where;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    // void Start()
    // {
        
    // }

    // // Update is called once per frame
    // void Update()
    // {
        
    // }
    public void SpawnGun()
    {
        if (hasSpawned) return;
        Instantiate(gunPrefab, transform.position, transform.rotation);
        hasSpawned = true;
    }
}
