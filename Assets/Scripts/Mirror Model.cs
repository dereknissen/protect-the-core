using JetBrains.Annotations;
using UnityEngine;

public class MirrorModel : MonoBehaviour
{
    [Header("Animation Settings")]
    [Min(1)]
    public int framesPerFlip = 120;
    public bool startMirrored = false;
    private int frameCounter = 0;
    private bool isMirrored;
    private Vector3 originalScale;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        originalScale = transform.localScale;
        isMirrored = startMirrored;
        ApplyMirror();
    }

    // Update is called once per frame
    void Update()
    {
        frameCounter++;

        if(frameCounter >= framesPerFlip)
        {
            frameCounter = 0;
            isMirrored = !isMirrored;
            ApplyMirror();
        }
    }

    void ApplyMirror()
    {
        Vector3 scale = originalScale;
        if (isMirrored){
            scale.x *= -1f;
        }
        transform.localScale = scale;
    }
}
