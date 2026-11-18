using UnityEngine;

namespace ZhuZiShou
{
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Object.FindObjectOfType<GameController>() != null) return;
            Debug.Log("[ZhuZiShou] Boot");
            GameObject go = new GameObject("ZhuZiShou");
            go.AddComponent<GameController>();
        }
    }
}
