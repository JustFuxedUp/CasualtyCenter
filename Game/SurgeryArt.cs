using System;
using System.Collections.Generic;
using System.IO;
using CasualtiesSurgery.Procedures;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesSurgery.Game
{
    internal static class SurgeryArt
    {
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly List<Texture2D> textures = new List<Texture2D>();

        public const float BruiseSize = 78f;

        public static readonly Color OpenField = new Color32(139, 65, 0, 255);
        public static readonly Color Blood = new Color32(255, 201, 0, 255);
        public static readonly Color Bruise = new Color32(200, 145, 16, 255);
        public static readonly Color Gel = new Color32(77, 215, 120, 255);

        public static Sprite Get(string name)
        {
            if (sprites.TryGetValue(name, out var sprite)) return sprite;
            using (var stream = typeof(SurgeryArt).Assembly.GetManifestResourceStream("SurgeryArt." + name + ".png"))
            {
                if (stream == null) throw new InvalidOperationException("Missing surgery artwork: " + name);
                using (var buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) {
                        name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
                    };
                    bool softenBruise = name == "bruise";
                    if (!ImageConversion.LoadImage(texture, buffer.ToArray(), !softenBruise))
                    {
                        UnityEngine.Object.Destroy(texture);
                        throw new InvalidOperationException("Invalid surgery artwork: " + name);
                    }
                    if (softenBruise) SoftenBruise(texture);
                    var bounds = new Rect(0, 0, texture.width, texture.height);
                    // Trim wound and shard padding to keep the visible art near the existing contact geometry.
                    if (name == "smallwound") bounds = new Rect(23, 4, 17, 149);
                    if (name == "fragment1") bounds = new Rect(20, 14, 88, 131);
                    // Stretch the shaft to the viewport while keeping the bone ends at source scale.
                    var border = name == "fallbackbone" ? new Vector4(0, 40, 0, 40) : Vector4.zero;
                    sprite = Sprite.Create(texture, bounds, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
                    sprites.Add(name, sprite);
                    textures.Add(texture);
                    return sprite;
                }
            }
        }

        static void SoftenBruise(Texture2D texture)
        {
            // Feather the supplied silhouette in alpha and preserve the grayscale source art.
            var pixels = texture.GetPixels32();
            var center = new Vector2(texture.width * .44f, texture.height * .50f);
            var radius = new Vector2(texture.width * .39f, texture.height * .37f);
            for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                {
                    int i = y * texture.width + x;
                    if (pixels[i].a == 0) continue;
                    var delta = new Vector2((x - center.x) / radius.x, (y - center.y) / radius.y);
                    float feather = Mathf.SmoothStep(1f, .20f, Mathf.InverseLerp(.5f, 1.1f, delta.magnitude));
                    pixels[i].a = (byte)Mathf.RoundToInt(pixels[i].a * feather);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
        }

        public static Image Bone(RectTransform contact, bool upper)
        {
            // Mirror the artwork end-for-end without moving the forceps coordinate system.
            contact.GetComponent<Image>().enabled = false;
            var artwork = new GameObject("BoneArtwork", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = artwork.rectTransform;
            rect.SetParent(contact, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (upper) rect.localEulerAngles = new Vector3(0, 0, 180);
            artwork.sprite = Get("fallbackbone");
            artwork.type = Image.Type.Sliced;
            artwork.raycastTarget = false;
            return artwork;
        }

        public static Color FragmentWound(float gelMl) =>
            Color.Lerp(Color.white, Gel, gelMl / GelRules.FragmentMl);

        public static void Clear()
        {
            foreach (var sprite in sprites.Values) UnityEngine.Object.Destroy(sprite);
            foreach (var texture in textures) UnityEngine.Object.Destroy(texture);
            sprites.Clear();
            textures.Clear();
        }
    }

    internal sealed class BruiseArt
    {
        const float FadeSeconds = .3f;
        readonly Image image;
        float? fadeStarted;

        public BruiseArt(Image image, InternalWoundPhase initial)
        {
            this.image = image;
            image.sprite = SurgeryArt.Get("bruise");
            if (initial == InternalWoundPhase.Repaired) fadeStarted = Time.unscaledTime - FadeSeconds;
        }

        public bool Refresh(InternalWoundPhase phase, float gelMl)
        {
            var tint = Color.Lerp(SurgeryArt.Bruise, SurgeryArt.Gel, gelMl / GelRules.FragmentMl);
            if (phase == InternalWoundPhase.Repaired)
            {
                if (!fadeStarted.HasValue) fadeStarted = Time.unscaledTime;
                tint.a = 1 - Mathf.Clamp01((Time.unscaledTime - fadeStarted.Value) / FadeSeconds);
            }
            image.color = tint;
            return phase == InternalWoundPhase.Repaired && tint.a > 0;
        }
    }

    internal sealed class LacerationArt : MonoBehaviour
    {
        const float FadeSeconds = .3f;
        Image image;
        float? fadeStarted;

        public void Setup(Image wound, InternalWoundPhase initial)
        {
            image = wound;
            image.sprite = SurgeryArt.Get("laceration");
            image.color = initial == InternalWoundPhase.Repaired ? Color.clear : Color.white;
            if (initial == InternalWoundPhase.Repaired) fadeStarted = Time.unscaledTime - FadeSeconds;
            SetWidth(initial == InternalWoundPhase.Open ? LacerationDrag.OpenWidth : LacerationDrag.ClosedWidth);
        }

        // Only the artwork eases open. Wound state and forceps contact stay authoritative.
        public bool Refresh(InternalWoundPhase phase, float? dragWidth)
        {
            if (phase == InternalWoundPhase.Repaired)
            {
                if (!fadeStarted.HasValue) fadeStarted = Time.unscaledTime;
                SetWidth(LacerationDrag.ClosedWidth);
                float alpha = 1 - Mathf.Clamp01((Time.unscaledTime - fadeStarted.Value) / FadeSeconds);
                image.color = new Color(1, 1, 1, alpha);
                return alpha > 0;
            }
            image.color = Color.white;
            float width = phase == InternalWoundPhase.Pinched ? LacerationDrag.ClosedWidth :
                dragWidth ?? Mathf.MoveTowards(image.rectTransform.sizeDelta.x, LacerationDrag.OpenWidth, 260 * Time.unscaledDeltaTime);
            SetWidth(width);
            return false;
        }

        void SetWidth(float width)
        {
            var size = image.rectTransform.sizeDelta;
            image.rectTransform.sizeDelta = new Vector2(width, size.y);
        }
    }
}
