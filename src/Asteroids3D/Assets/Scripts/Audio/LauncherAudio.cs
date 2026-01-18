using Combat.Weapons;
using UnityEngine;

namespace Weapons
{
    [RequireComponent(typeof(AudioSource), typeof(WeaponComponent))]
    public class LauncherAudio : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioClip fireSound;
        [SerializeField, Range(0f, 1f)] private float fireVolume = 0.5f;
        
        private AudioSource _audioSource;
        private WeaponComponent _weapon;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _weapon = GetComponent<WeaponComponent>();
        }

        private void OnEnable()
        {
            _weapon.OnFire += PlayFireSound;
        }

        private void OnDisable()
        {
            _weapon.OnFire -= PlayFireSound;
        }

        private void PlayFireSound()
        {
            if (fireSound)
            {
                _audioSource.PlayOneShot(fireSound, fireVolume);
            }
        }
    }
}
