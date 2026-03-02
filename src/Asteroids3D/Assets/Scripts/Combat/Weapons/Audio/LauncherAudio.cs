using UnityEngine;

namespace Combat.Weapons.Audio
{
    [RequireComponent(typeof(AudioSource), typeof(WeaponComponent))]
    public class LauncherAudio : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioClip fireSound;
        [SerializeField, Range(0f, 1f)] private float fireVolume = 0.5f;
        
        private AudioSource audioSource;
        private WeaponComponent weapon;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            weapon = GetComponent<WeaponComponent>();
        }

        private void OnEnable()
        {
            weapon.OnFire += PlayFireSound;
        }

        private void OnDisable()
        {
            weapon.OnFire -= PlayFireSound;
        }

        private void PlayFireSound()
        {
            if (fireSound)
            {
                audioSource.PlayOneShot(fireSound, fireVolume);
            }
        }
    }
}
