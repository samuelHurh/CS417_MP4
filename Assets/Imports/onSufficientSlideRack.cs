using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class onSufficientSlideRack : MonoBehaviour
{
    // Start is called before the first frame update

    public float threshold;
    public Transform target;
    public UnityEvent successfulSlideRack;

    public fireControl fc_ref;

    private bool wasReachedPreviously = false;
    private void FixedUpdate() {
        if (!fc_ref.roundChambered) {
            threshold = 0.025f;
        } else {
            threshold = 0.005f;
        }
        float distance = Vector3.Distance(transform.position, target.position);
        //Debug.Log(distance);
        if (distance < threshold && wasReachedPreviously == false) {
            //Reached the target
            //Debug.Log("Successful slide rack");
            successfulSlideRack.Invoke();
            wasReachedPreviously = true;
        } else if (distance >= threshold) {
            wasReachedPreviously = false;
        }
    }
}
