namespace Ships
{
    /// <summary>
    /// Names not yet migrated to the anatomy, which the anatomy test and the hull import validator
    /// skip. Each migration removes a name; the arc closes when the list is empty.
    /// </summary>
    public static class ShipLegacyList
    {
        public static readonly string[] Chassis = { "Nightshade", "Junker_1" };

        public static readonly string[] HullModels = { "Vanguard", "Valis" };

        public static readonly string[] SavedColliderMeshes = { "Crimson", "Vanguard", "Valis" };
    }
}
