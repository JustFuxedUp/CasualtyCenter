using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace CasualtiesSurgery
{
    [BepInPlugin("local.casualties.surgery", "Casualties Surgery", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<KeyCode> toggleKey;
        private bool panelVisible;
        private Rect panel = new Rect(40, 80, 420, 240);

        private void Awake()
        {
            toggleKey = Config.Bind("Interface", "ToggleKey", KeyCode.F8,
                "Show or hide the surgery development panel.");
            Logger.LogInfo("Casualties Surgery 0.1.0 loaded. Press " + toggleKey.Value + " for the development panel.");
            Logger.LogInfo("Detected game version: " + Application.version);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey.Value))
                panelVisible = !panelVisible;
        }

        private void OnGUI()
        {
            if (panelVisible)
                panel = GUI.Window(GetInstanceID(), panel, DrawPanel, "Casualties Surgery - development");
        }

        private void DrawPanel(int windowId)
        {
            GUILayout.Label("Starter plugin loaded successfully.");
            GUILayout.Space(8);
            GUILayout.Label("Game version: " + Application.version);
            GUILayout.Label("This panel does not treat injuries or consume items.");
            GUILayout.Label("Next milestone: one playable operation on yourself.");
            GUILayout.Label("Multiplayer surgery is not connected yet.");
            GUILayout.Space(12);
            if (GUILayout.Button("Close (" + toggleKey.Value + ")"))
                panelVisible = false;
            GUI.DragWindow(new Rect(0, 0, 420, 24));
        }
    }
}
