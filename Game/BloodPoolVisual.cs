using CasualtiesSurgery.Procedures;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesSurgery.Game
{
    internal sealed class BloodPoolVisual
    {
        BloodPools state;
        readonly RectTransform field;
        readonly MinigameBase host;
        readonly SurgeryActions actions;
        readonly Image[] squares = new Image[2];

        public BloodPoolVisual(BloodPools state, RectTransform field, Vector2 center, MinigameBase host, SurgeryActions actions)
        {
            this.state = state;
            this.field = field;
            this.host = host;
            this.actions = actions;
            for (int i = 0; i < squares.Length; i++)
            {
                var square = new GameObject("BloodPool", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)square.transform;
                rect.SetParent(field, false);
                rect.anchoredPosition = center + (i == 0 ? Vector2.zero : new Vector2(37, -25));
                rect.sizeDelta = Vector2.one * (i == 0 ? SurgeryEffects.GelSquareMax * 2 : SurgeryEffects.GelSquareMin + SurgeryEffects.GelSquareMax);
                squares[i] = square.GetComponent<Image>();
                squares[i].raycastTarget = false;
                square.SetActive(false);
            }
        }

        public void Refresh(bool visible = true)
        {
            for (int i = 0; i < squares.Length; i++)
            {
                float remaining = state.Remaining(i);
                bool active = visible && remaining > 0;
                squares[i].gameObject.SetActive(active);
                if (active)
                    squares[i].rectTransform.localEulerAngles = new Vector3(0, 0,
                        (i == 0 ? state.FirstAngle : state.SecondAngle) ?? 0);
                squares[i].color = new Color(.78f, .055f, .12f, Mathf.Lerp(.18f, .65f, remaining / BloodPools.ClearSeconds));
            }
        }
        public void Bind(BloodPools value) { state=value; }

        public float Distance(Vector3 world, out int index)
        {
            index = -1;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < squares.Length; i++)
            {
                var rect = squares[i].rectTransform;
                Vector2 delta = rect.InverseTransformPoint(world);
                float distance = delta.sqrMagnitude;
                if (squares[i].gameObject.activeInHierarchy && distance < nearest &&
                    Mathf.Abs(delta.x) <= rect.rect.width / 2 && Mathf.Abs(delta.y) <= rect.rect.height / 2)
                {
                    index = i;
                    nearest = distance;
                }
            }
            return nearest;
        }

        public void Suction(int index, float seconds)
        {
            if (!actions.Suction(state, index, seconds)) return;
            Plugin.Feedback?.Good(squares[index].rectTransform.position, host.guideText.font,
                field.GetComponentInParent<Canvas>());
        }
    }
}
