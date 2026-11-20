using UnityEngine;

public class ModelSwitcher : MonoBehaviour
{
    public GameObject modelA;
    public GameObject modelB;
    public int switchInterval = 180;

    private int frameCounter = 0;
    private bool showingA = true;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        modelA.SetActive(true);
        modelB.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        frameCounter++;

        if (frameCounter >= switchInterval)
        {
            frameCounter = 0;
            showingA = !showingA;

            modelA.SetActive(showingA);
            modelB.SetActive(!showingA);
        }
    }
}
