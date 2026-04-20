using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class fireControl : MonoBehaviour
{
    // Start is called before the first frame update
    
    //The projectile
    public GameObject projectilePrefab = null;
    //The slide/bolt of the weapon
    public GameObject slidePrefab = null;
    //The origin of the projectile movement
    public Transform startPoint = null;
    //The point at which to apply the blowback force on the slide
    public Transform blowBackPoint = null;
    //The multiplier for the launchspeed of the projectile
    public float launchSpeed = 1.0f;
    //The force at which to apply to the slide upon blowback
    public float blowBackForce = 0;

    public float recoilForce = 0;

    public GameObject grabCenter;
    public XRGrabInteractable frameGrip;

    //Defines whether a round is chambered or not
    public bool roundChambered;

    //Reference to the inserted magazine
    //Note that the "Magazine" type refers to the magazine class found in HandleMagCapacity.cs
    public Magazine magazine;
    private XRBaseInteractable magAsInteractable;
    public XRBaseInteractor socketInteractor;

    //Mag release variables:
    public InputActionReference magReleaseButton = null;
    public InteractionLayerMask socketInteractionLayerReference;
    private bool didRelease = false;

    public AudioClip fireSFX;
    public AudioClip emptySFX;
    public AudioClip magSFX;
    public AudioSource audioSource;

    //Full auto variables:
    private bool triggerIsReset;
    public bool isFullAuto = false;
    public InputActionReference fireModeToggleButton = null;
    private bool didReleaseToggleButton = true;
    public Material semiSlideMaterial;
    public Material autoSlideMaterial;
    //Variable that makes it so that release mag and change fire mode
    //only works when the item is actually in the grab interactable.
    private bool extraButtonsActive;

    
    public GameObject leftHandGrabInteractor;
    public GameObject rightHandGrabInteractor;

    public GameObject grabCenter_l;
    public GameObject grabCenter_r;

    public InputActionReference primaryButtonRight;
    public InputActionReference secondaryButtonRight;
    public InputActionReference primaryButtonLeft;
    public InputActionReference secondaryButtonLeft;

    public Transform gripAttach_l;
    public Transform gripAttach_r;


    //As of 3/20/24, I am scrapping the realistic handgun implementation getting rid of the magazine. This vastly simplifies this gun object.
    private int currAmmo;

    void Start()
    {
        // socketInteractor.onSelectEntered.AddListener(insertMag);
        // socketInteractor.onSelectExited.AddListener(removeMag);
        triggerIsReset = true;
        currAmmo = 17;
    }

    // Update is called once per frame
    void Update()
    {   
        if (extraButtonsActive) {
            float magReleaseVal = magReleaseButton.action.ReadValue<float>();
            float toggleFire = fireModeToggleButton.action.ReadValue<float>();
            if (magReleaseVal > 0 && didRelease ==  false) {
                StartCoroutine(releaseMag());
                didRelease = true;
            } else if (magReleaseVal == 0) {
                didRelease = false;
            }
            if (didReleaseToggleButton == true && toggleFire > 0) {
                didReleaseToggleButton = false;
                isFullAuto = !isFullAuto;
                if (isFullAuto) {
                    slidePrefab.GetComponent<MeshRenderer>().material = autoSlideMaterial;
                } else {
                    slidePrefab.GetComponent<MeshRenderer>().material = semiSlideMaterial;
                }
                
            } else {
                if (toggleFire == 0) {
                    didReleaseToggleButton = true;
                }
            }
        }
        
    }

    public void activateExtraButtons() {
        extraButtonsActive = true;
    }

    public void deactivateExtraButtons() {
        extraButtonsActive = false;
    }

    //Administrative function for pulling the trigger.
    public void Fire()
    {
        // if (roundChambered && triggerIsReset) {
        //     triggerIsReset = false;
        //     if (isFullAuto) {
        //         StartCoroutine(fullAutoCoroutine());
        //     } else {
        //         GameObject newObject = Instantiate(projectilePrefab, startPoint.position, startPoint.rotation);
        //         Destroy(newObject, 5);
        //         if (newObject.TryGetComponent(out Rigidbody rigidBody)) {
        //             ApplyForce(rigidBody);
        //             audioSource.PlayOneShot(fireSFX, 1);
        //             Rigidbody slide = slidePrefab.GetComponent<Rigidbody>();
        //             blowBack(slide);
        //             recoil(grabCenter.GetComponent<Rigidbody>());
        //             roundChambered = false; //This'll ping back and forth between fire() and attemptChamber() until the magazine is empty
        //             attemptChamber();
                    
        //         } else {
        //             //Debug.Log("Didn't get component");
        //         }
        //     }
            
        // } else {
        //     // if (magazine == null) {
        //     //     Debug.Log("Null Magazine");
        //     // } else{
        //     //     Debug.Log("Magazine has a capacity of " + magazine.getCurrCap());

        //     // }
        //     audioSource.PlayOneShot(emptySFX,1);
        //     if (roundChambered == false) {
        //         //Debug.Log("Round was never chambered");
        //     }
        // }   
        if (currAmmo > 0) {
            GameObject newObject = Instantiate(projectilePrefab, startPoint.position, startPoint.rotation);
            Destroy(newObject, 5);
            if (newObject.TryGetComponent(out Rigidbody rigidBody)) {
                ApplyForce(rigidBody);
                audioSource.PlayOneShot(fireSFX, 1);
                Rigidbody slide = slidePrefab.GetComponent<Rigidbody>();
                blowBack(slide);
                recoil(grabCenter.GetComponent<Rigidbody>());
                currAmmo--;
            } else {
                Debug.Log("Didn't get component");
            }
        } else {
            audioSource.PlayOneShot(emptySFX,1);
        }
    }

    public void Reload() {
        currAmmo = 17;
    }

    IEnumerator fullAutoCoroutine() {
        while (!triggerIsReset) {
            if (!roundChambered) {
                break;
            }
            yield return new WaitForSeconds(0.05f);
            GameObject newObject = Instantiate(projectilePrefab, startPoint.position, startPoint.rotation);
            Destroy(newObject, 5);
            if (newObject.TryGetComponent(out Rigidbody rigidBody)) {
                ApplyForce(rigidBody);
                audioSource.PlayOneShot(fireSFX, 1);
                Rigidbody slide = slidePrefab.GetComponent<Rigidbody>();
                blowBack(slide);
                recoil(grabCenter.GetComponent<Rigidbody>());
                roundChambered = false; //This'll ping back and forth between fire() and attemptChamber() until the magazine is empty
                attemptChamber();
                
            } else {
                //Debug.Log("Didn't get component");
            }
            yield return null;
        }
        
        yield return null;
    }

    //Called on the Deactivate event of the XR grab interactable
    //More useful for full as the Activate event implicitly
    //only triggers once before the potentiometer in the controller
    //needs to be reset to a certain "deactivated" value.
    public void triggerReset() {
        triggerIsReset = true;
        //Debug.Log("RESET");
    }
    //Applies force to the spawned projectile upon firing
    private void ApplyForce(Rigidbody rigidBody)
    {
        Vector3 force = startPoint.forward * launchSpeed;
        rigidBody.AddForce(force);
        // Debug.Log(force);
        

    }

    //Blows the slide backwards upon firing
    private void blowBack(Rigidbody rigidBody) {
        Vector3 force = blowBackPoint.forward * -blowBackForce;
        rigidBody.AddForce(force);
        // Debug.Log(force);
    }

    private void recoil(Rigidbody rigidBody) {
        Vector3 force = blowBackPoint.forward * -recoilForce;
        force += blowBackPoint.up * recoilForce * 0.25f;
        Vector3 torque = blowBackPoint.right * -10000000000000f;
        rigidBody.AddTorque(torque);
        rigidBody.AddForce(force);
    }

    public void insertMag(XRBaseInteractable interactable) {
        // magazine = interactable.GetComponent<Magazine>();
        // if (magazine == null) {
        //     Debug.Log("magazine appears to be null");
        // }
        // Debug.Log("Inserted magazine of capacity " + magazine.getCurrCap());
        //Debug.Log("Got to insertMag()");
        IXRSelectInteractable obj = socketInteractor != null ? socketInteractor.firstInteractableSelected : null;
        magazine = obj != null ? obj.transform.GetComponent<Magazine>() : null;
        //Debug.Log(obj.transform.name + " in socket of " + transform.name);
        magAsInteractable = interactable;
        audioSource.PlayOneShot(magSFX, 1);
        if(magAsInteractable == null) {
            //Debug.Log("mag is null");
        }
        //Debug.Log("Inserted magazine of capacity " + magazine.getCurrCap());
    }

    //Resets the magazine that the frame has a reference to
    public void removeMag(XRBaseInteractable interactable) {
        magAsInteractable = null;
        magazine = null;
    }

    IEnumerator releaseMag() {
        //Something here
        if (magAsInteractable == null) {
            //Debug.Log("mag is null");
        } else {
            //Debug.Log("Successful eject");
            //Debug.Log("Changing interaction layerMask of the mag socket");
            socketInteractor.interactionLayers = 0;
            audioSource.PlayOneShot(magSFX,1);
            //Why I made this a coroutine. This line is the same as waiting for 1
            //second without making the entire program pause.
            yield return new WaitForSeconds(0.5f);
            //Debug.Log("Resetting the interaction layerMask of the mag socket");
            socketInteractor.interactionLayers = socketInteractionLayerReference;
        }
        
        magazine = null;

        //Coroutines terminate when they reach the end of the function.
        //If I want it to end before that, I can use:
        //yield break;
    }


    public void attemptChamber() {
        if (magazine != null && magazine.getCurrCap() != 0) {
            roundChambered = true;
            magazine.decrement();
            if (magazine.getCurrCap() == 0) {
                //Debug.Log("The last round was just chambered");
            }
        } else if (magazine == null) {
           // Debug.Log("Tried to chamber a round with no magazine inserted");
        } else if (magazine.getCurrCap() == 0) {
           // Debug.Log("Tried to chamber from an empty magazine");
        }
    }

    public void determineWhichHandRecoils() {
         Transform interactorTransform = GetCurrentInteractorTransform();
         if (interactorTransform == null) {
            return;
         }

         if (MatchesInteractor(interactorTransform, leftHandGrabInteractor)) {
            grabCenter = grabCenter_l;
            magReleaseButton = secondaryButtonLeft;
            fireModeToggleButton = primaryButtonLeft;
            frameGrip.attachTransform = gripAttach_l;
        }
        if (MatchesInteractor(interactorTransform, rightHandGrabInteractor)) {
            grabCenter = grabCenter_r;
            magReleaseButton = secondaryButtonRight;
            fireModeToggleButton = primaryButtonRight;
            frameGrip.attachTransform = gripAttach_r;
        }
    }

    private Transform GetCurrentInteractorTransform() {
        if (frameGrip == null) {
            return null;
        }

        if (frameGrip.firstInteractorSelecting != null) {
            return frameGrip.firstInteractorSelecting.transform;
        }

        if (frameGrip.interactorsHovering.Count > 0) {
            return frameGrip.interactorsHovering[0].transform;
        }

        return null;
    }

    private bool MatchesInteractor(Transform interactorTransform, GameObject handInteractor) {
        if (interactorTransform == null || handInteractor == null) {
            return false;
        }

        return interactorTransform.gameObject == handInteractor ||
               interactorTransform.root.gameObject == handInteractor ||
               interactorTransform.name == handInteractor.name;
    }



}
