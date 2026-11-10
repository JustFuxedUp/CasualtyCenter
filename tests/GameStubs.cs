// Minimal test doubles for adapter checks; never compiled into the plugin.
// These do not simulate Unity object destruction, game updates, input or networking.
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public static float Distance(Vector3 a, Vector3 b) => (float)System.Math.Sqrt(
            (a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));
    }
    public class Transform { public Vector3 position; }
    public struct Scene { public int handle; }
    public class GameObject { public Scene scene; }
    public static class Mathf
    {
        public static float Min(float a, float b) => System.Math.Min(a,b);
        public static float Max(float a, float b) => System.Math.Max(a,b);
    }
}
namespace CUCoreLib.Networking { public static class MultiplayerApi { public static bool IsRunning; } }
public class Body
{
    public bool alive = true, conscious = true;
    public Limb[] limbs;
    public UnityEngine.Transform transform = new UnityEngine.Transform();
    public UnityEngine.GameObject gameObject = new UnityEngine.GameObject();
}
public struct ProstheticInfo { public bool hasProsthetic; }
public class Limb
{
    public Body body;
    public int shrapnel;
    public float skinHealth, bleedAmount;
    public bool dismembered;
    public ProstheticInfo prostheticInfo;
}
public class PlayerCamera { public static PlayerCamera main; public Body body; }
public class Minigame { }
public class MinigameBase { public static MinigameBase main; public Minigame currentMinigame; }
