using UnityEngine;

namespace Objectives
{
    public interface IKeyTracker
    {
        bool PlayerHasKey { get; }
    }

    public interface IPlayerPosition
    {
        Vector3 Position { get; }
    }

    public interface IExtractionZone
    {
        Vector3 Position { get; }
    }

    public interface IPlayerAlive
    {
        bool IsAlive { get; }
    }

    public interface IExtractionBlocker
    {
        bool IsExtractionBlocked { get; }
    }

    public interface IExtractionChaserSpawner
    {
        void SpawnChaser();
    }
}
