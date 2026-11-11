using UI.Projection;
using UnityEngine;

namespace UI.Hangar.Displays
{
    [CreateAssetMenu(fileName = "HangarDisplayProfile", menuName = "Astronomical/UI/Hangar display")]
    public sealed class HangarDisplayProfile : ScriptableObject
    {
        [SerializeField] private CanopySettings mainDisplay = CanopySettings.Combat;
        [SerializeField] private CanopySettings detailDisplay = CanopySettings.Combat;

        public CanopySettings MainDisplay => mainDisplay;
        public CanopySettings DetailDisplay => detailDisplay;
    }
}
