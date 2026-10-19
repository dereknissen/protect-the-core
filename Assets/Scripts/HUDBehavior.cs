using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

public class HUDBehavior : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public Text livesLeftText;
    public Text pointsText;
    public GameObject gameManager;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        livesLeftText.text = "Lives: " + gameManager.GetComponent<GameManagerScript>().health;
        pointsText.text = "Points: " + gameManager.GetComponent<GameManagerScript>().enemiesStopped + " / 10";
    }
}
