using UnityEngine;
using System.Collections.Generic;

public class GeneratedWeaponManager : MonoBehaviour
{
    public List<GameObject> Frames = new List<GameObject>();
    public List<GameObject> Slides = new List<GameObject>();

    //The normal slide and the non-compact grip are the origin transform.
    //Different slide lengths require moving the frame in comparison
    // the compact grip requires moving the frame up
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        foreach (GameObject frame in Frames) {
            frame.SetActive(false);
        }
        foreach (GameObject slide in Slides) {
            slide.SetActive(false);
        }

        GameObject chosenSlide = Slides[Random.Range(0,3)];
        int gripChoice = Random.Range(0,2); //0 is normal, 1 is compact
        GameObject chosenFrame = Frames[0];
        if (chosenSlide == Slides[0])
        {
            //Long slide
            chosenFrame = (gripChoice == 0) ? Frames[0] : Frames[3];
        } else if (chosenSlide == Slides[1])
        {
            //Normal slide
            chosenFrame = (gripChoice == 0) ? Frames[1] : Frames[4];
        } else
        {
            //Short slide
            chosenFrame = (gripChoice == 0) ? Frames[2] : Frames[5];
        }
        chosenFrame.SetActive(true);
        chosenSlide.SetActive(true);
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
