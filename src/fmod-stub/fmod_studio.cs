namespace FMOD.Studio
{
    public enum STOP_MODE : int
    {
        ALLOWFADEOUT,
        IMMEDIATE,
    }

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

        public RESULT stop(STOP_MODE mode) => RESULT.OK;

        public RESULT keyOff() => RESULT.OK;

        public bool isValid() => false;
    }
}
