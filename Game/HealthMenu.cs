using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CasualtiesSurgery.Game
{
    // Lives only alongside the local player's native health dummy. No prefab edits.
    internal sealed class HealthMenu : IDisposable
    {
        readonly Action<WoundView, Limb> begin;
        readonly Action dismiss;
        readonly List<HealthLimbClick> hooks = new List<HealthLimbClick>();
        WoundView view;
        GameObject overlay;
        RectTransform panel;
        TextMeshProUGUI title, reason;
        Button procedure;
        Body patient;
        Limb limb;
        public bool IsOpen => overlay != null && overlay.activeSelf;

        public HealthMenu(Action<WoundView, Limb> begin, Action dismiss)
        { this.begin = begin; this.dismiss = dismiss; }

        public void Tick()
        {
            var camera = PlayerCamera.main;
            var current = camera != null && camera.woundView != null
                ? camera.woundView.GetComponent<WoundView>() : null;
            if (current != view)
            {
                Dispose(); view = current;
                if (view != null)
                    foreach (var native in view.GetComponentsInChildren<WoundViewLimb>(true))
                    {
                        var hook = native.gameObject.AddComponent<HealthLimbClick>();
                        hook.Menu = this; hook.Native = native; hooks.Add(hook);
                    }
            }
            if (!IsOpen) return;
            if (view == null || !view.gameObject.activeInHierarchy || view.body != patient)
            { Close(); return; }
            Refresh();
        }

        public void Open(WoundViewLimb target, PointerEventData pointer)
        {
            if (Plugin.OperationVisible || view == null || !view.gameObject.activeInHierarchy || target.woundview != view) return;
            Body body = view.body;
            int index = target.limb;
            if (body == null || body.limbs == null || index < 0 || index >= body.limbs.Length) return;
            Limb selected = body.limbs[index];
            if (selected == null) return;
            // Capture the clicked limb, not the last hover index (which can be stale).
            patient = body; limb = selected;
            if (overlay == null) Build();
            overlay.SetActive(true); overlay.transform.SetAsLastSibling();
            var parent = (RectTransform)overlay.transform;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, pointer.position, pointer.pressEventCamera, out local);
            Rect bounds = parent.rect;
            float width = Mathf.Min(280, bounds.width), height = Mathf.Min(174, bounds.height);
            panel.sizeDelta = new Vector2(width, height);
            panel.localPosition = new Vector3(
                Mathf.Clamp(local.x + 12, bounds.xMin, bounds.xMax - width),
                Mathf.Clamp(local.y, bounds.yMin + height, bounds.yMax), 0);
            Refresh(); pointer.Use();
        }

        void Build()
        {
            overlay = new GameObject("CasualtyCenterHealthMenu", typeof(RectTransform), typeof(Image), typeof(Button));
            overlay.transform.SetParent(view.transform, false);
            Stretch((RectTransform)overlay.transform);
            overlay.GetComponent<Image>().color = Color.clear;
            overlay.GetComponent<Button>().onClick.AddListener(Close);
            var box = new GameObject("Procedures", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(overlay.transform, false);
            panel = (RectTransform)box.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f); panel.pivot = new Vector2(0, 1);
            box.GetComponent<Image>().color = new Color(0.025f,0.05f,0.11f,0.98f);
            title = Text("PatientLimb", panel, new Vector2(12, -10), new Vector2(256, 40), 16);
            procedure = MakeButton("Extract fragment + suture", new Vector2(12, -56), new Vector2(256, 32), Start);
            reason = Text("Eligibility", panel, new Vector2(12, -94), new Vector2(256, 42), 13);
            MakeButton("Back", new Vector2(12, -140), new Vector2(256, 26), Close);
            overlay.SetActive(false);
        }

        Button MakeButton(string caption, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(panel, false); Place((RectTransform)go.transform, position, size);
            go.GetComponent<Image>().color = new Color(0.10f,0.19f,0.36f);
            var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
            button.onClick.AddListener(action);
            var text = Text("Label", go.transform, Vector2.zero, size, 14);
            text.text = caption;
            Stretch(text.rectTransform); text.alignment = TextAlignmentOptions.Center;
            return button;
        }

        TextMeshProUGUI Text(string name, Transform parent, Vector2 position, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false); Place((RectTransform)go.transform, position, size);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (view.limbNameText != null) text.font = view.limbNameText.font;
            text.fontSize = fontSize; text.color = Color.white; text.raycastTarget = false;
            text.enableWordWrapping = true; text.text = name; return text;
        }

        void Refresh()
        {
            string unavailable = OperationSession.Ineligible(patient, limb);
            title.text = "SURGERY / " + (limb != null ? limb.fullName : "No limb");
            procedure.interactable = unavailable == null;
            reason.text = unavailable ?? (limb.shrapnel + " fragment(s). Free test kit; world stays live.");
        }
        void Start()
        {
            // Revalidate at click time, even if the button was enabled last frame.
            if (view == null || !view.gameObject.activeInHierarchy || view.body != patient ||
                OperationSession.Ineligible(patient, limb) != null) { Refresh(); return; }
            WoundView source = view; Limb selected = limb;
            Close(); begin(source, selected);
        }
        public void Close()
        {
            bool wasOpen = IsOpen;
            if (overlay != null) overlay.SetActive(false);
            patient = null; limb = null;
            if (wasOpen) dismiss();
        }
        public void Dispose()
        {
            Close();
            foreach (var hook in hooks)
                if (hook != null) { hook.Menu = null; UnityEngine.Object.Destroy(hook); }
            hooks.Clear();
            if (overlay != null) UnityEngine.Object.Destroy(overlay);
            overlay = null; view = null;
        }
        static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }

    public sealed class HealthLimbClick : MonoBehaviour, IPointerClickHandler
    {
        internal HealthMenu Menu;
        internal WoundViewLimb Native;
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Right && Menu != null && Native != null)
                Menu.Open(Native, data);
        }
    }
}
