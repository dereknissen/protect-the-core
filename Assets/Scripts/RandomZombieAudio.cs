
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class RandomZombieAudio : MonoBehaviour
{
    [Header("Zombie Sounds")]
    public AudioClip[] zombieSounds;

    [Header("Random Delay (Seconds)")]
    public float minDelay = 5f;
    public float maxDelay = 15f;

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;

        StartCoroutine(PlayRandomSounds());
    }

    IEnumerator PlayRandomSounds()
    {
        if (zombieSounds == null || zombieSounds.Length == 0)
        {
            Debug.LogWarning("No zombie sounds assigned!");
            yield break;
        }

        while (true)
        {
            float delay = Random.Range(minDelay, maxDelay);
            yield return new WaitForSeconds(delay);

            int randomIndex = Random.Range(0, zombieSounds.Length);
            AudioClip selectedSound = zombieSounds[randomIndex];

            if (selectedSound == null)
                continue;

            audioSource.clip = selectedSound;
            audioSource.Play();

            while (audioSource.isPlaying)
            {
                yield return null;
            }
        }
    }
}