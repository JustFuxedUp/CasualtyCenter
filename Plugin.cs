using BepInEx;
using BepInEx.Configuration;
using CUCoreLib.Helpers;
using UnityEngine;

namespace CasualtiesSurgery
{
    [BepInPlugin("local.casualties.surgery", "Casualties Surgery", "0.2.0")]
    // Match the BepInEx plugin version (1.1), not the DLL assembly version (1.1.0.0).
    [BepInDependency("net.cucorelib", "1.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<KeyCode> toggleKey;
        private bool panelVisible;
        private Rect panel = new Rect(40, 80, 540, 430);
        private Vector2 scroll;

        private void Awake()
        {
            toggleKey = Config.Bind("Interface", "ToggleKey", KeyCode.F8,
                "Show or hide the surgery development panel.");
            Logger.LogInfo("Casualties Surgery 0.2.0 loaded. Press " + toggleKey.Value + " for the development panel.");
            Logger.LogInfo("Unity application version: " + Application.version);
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
            GUILayout.Label("CUCoreLib loaded | " + (GameBuildInfo.IsFullGame ? "Full game" : "Demo / other build"));
            GUILayout.Label("Read-only injury inspector. No treatment or item consumption yet.");
            GUILayout.Label("Planned: extraction + suture reduces bleeding and restores skin.");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(300));
            Body body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
            if (body == null || body.limbs == null)
                GUILayout.Label("Enter a run to inspect your character's limbs.");
            else
            {
                foreach (Limb limb in body.limbs)
                {
                    if (limb == null) continue;
                    string name = string.IsNullOrEmpty(limb.fullName) ? limb.name : limb.fullName;
                    GUILayout.Label(string.Format("{0}: fragments {1} | skin {2:0.0}/100 | bleeding {3:0.00}{4}",
                        name, limb.shrapnel, limb.skinHealth, limb.bleedAmount,
                        limb.dismembered ? " | detached" : limb.prostheticInfo.hasProsthetic ? " | prosthetic" : ""));
                }
            }
            GUILayout.EndScrollView();
            if (GUILayout.Button("Close (" + toggleKey.Value + ")")) panelVisible = false;
            GUI.DragWindow(new Rect(0, 0, 540, 24));
        }
    }
}
