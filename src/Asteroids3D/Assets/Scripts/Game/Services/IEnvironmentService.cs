using System.Collections;

namespace Game.Services
{
    public interface IEnvironmentService
    {
        void Clear();
        IEnumerator LoadSceneAsync(string sceneName);
        IEnumerator UnloadSceneAsync(string sceneName);
    }
}
