using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesSurgery.Game
{
    internal sealed class SutureThread
    {
        readonly RectTransform field;
        RectTransform layer, preview;
        Vector2 last;

        public SutureThread(RectTransform field) => this.field = field;

        public void Begin(Vector2 point)
        {
            Clear();
            layer = new GameObject("SutureThread", typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(field, false);
            layer.SetAsLastSibling();
            last = point;
        }

        public void Preview(Vector2 point)
        {
            if (preview == null) preview = Line("LiveThread", new Color(1, 1, 1, .5f));
            Place(preview, last, point);
        }

        public void Commit(Vector2 point)
        {
            Place(Line("Stitch", Color.white), last, point);
            last = point;
            Plugin.Feedback?.Stitch();
        }

        public void Clear()
        {
            if (layer != null)
            {
                layer.gameObject.SetActive(false);
                Object.Destroy(layer.gameObject);
            }
            layer = null;
            preview = null;
        }

        RectTransform Line(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(layer, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static void Place(RectTransform line, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            line.anchoredPosition = (from + to) * .5f;
            line.sizeDelta = new Vector2(delta.magnitude, 2);
            line.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }
}
