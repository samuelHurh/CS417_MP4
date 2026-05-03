using UnityEngine;

public class RGBMaterial : MonoBehaviour
{
    public Renderer targetRenderer;
    public float speed = 1.0f;
    public float intensity = 0.1f;

    private Material mat;

    private bool isActive;

    void Start()
    {
    }

    public void ReceiveSetup(GameObject slide)
    {
        mat = slide.GetComponentInParent<Renderer>().material;
        mat.EnableKeyword("_EMISSION");
        isActive = true;
    }

    void Update()
    {
        if (!isActive) return;
        float t = Time.time * speed;

        // Smooth rainbow using HSV
        Color rgb = Color.HSVToRGB(Mathf.Repeat(t, 1f), 1f, 1f);

        // Apply emission (this is the key)
        mat.SetColor("_EmissionColor", rgb * intensity);
    }
}