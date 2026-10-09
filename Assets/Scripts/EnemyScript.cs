using System;
using UnityEngine;

public class EnemyScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public GameObject Core;
    public GameObject GameManager;
    public int Speed = 10;

    void Start()
    {
   
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject == Core)
        {
            Console.WriteLine("Enemy reached columns!");
        }
    }
}
