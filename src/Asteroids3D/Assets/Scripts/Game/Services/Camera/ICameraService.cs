using Cameras;
using Player;
using UnityEngine;

namespace Game.Services
{
    public interface ICameraService
    {
        /// <summary>The active camera rig, if initialized.</summary>
        CameraRig CameraRig { get; }

        /// <summary>Main gameplay camera.</summary>
        Camera MainCamera { get; }

        /// <summary>UI overlay camera.</summary>
        Camera UICamera { get; }

        /// <summary>Instantiate the camera rig from a prefab.</summary>
        void Initialize(CameraRig prefab);

        /// <summary>Set the primary follow subject.</summary>
        void SetSubject(Transform subject);

        /// <summary>Add a secondary subject for the camera to track.</summary>
        void AddSecondarySubject(Transform subject);

        /// <summary>Remove a secondary subject.</summary>
        void RemoveSecondarySubject(Transform subject);

        /// <summary>Configure player input screen-to-plane projection.</summary>
        void ConfigurePlayerInputProjection(PlayerCommander commander);

        /// <summary>Destroy the camera rig and reset state.</summary>
        void Clear();
    }
}
