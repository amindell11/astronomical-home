namespace FMOD.Studio
{
    public struct System
    {
        public RESULT getCoreSystem(out FMOD.System coresystem)
        {
            coresystem = default;
            return RESULT.OK;
        }
    }

    public struct EventInstance
    {
        public RESULT set3DAttributes(ATTRIBUTES_3D attributes) => RESULT.OK;

        public RESULT setParameterByName(string name, float value, bool ignoreseekspeed = false) => RESULT.OK;

        public RESULT start() => RESULT.OK;

        public RESULT release() => RESULT.OK;
    }
}
