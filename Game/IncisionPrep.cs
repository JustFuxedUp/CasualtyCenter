using System;
using CasualtiesSurgery.Procedures;
using UnityEngine;
using UnityEngine.UI;

namespace CasualtiesSurgery.Game
{
    internal sealed class IncisionPrep
    {
        readonly Image[] bands = new Image[GelRules.IncisionSections];
        float[] coverage;

        public IncisionPrep(RectTransform field, float[] coverage)
        {
            this.coverage = coverage;
            for (int i = 0; i < bands.Length; i++)
            {
                var go = new GameObject("DisinfectedSection", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(field, false);
                rect.anchoredPosition = Section(i);
                rect.sizeDelta = new Vector2(10, 26);
                bands[i] = go.GetComponent<Image>();
                bands[i].raycastTarget = false;
            }
        }

        public static Vector2 Section(int index) => new Vector2(0, 120 - index * 30);
        public void Bind(float[] value) {coverage=value;}

        public void Apply(Vector2 point, float ml, Func<int, float, bool> disinfect)
        {
            int closest = -1;
            float best = 48 * 48;
            for (int i = 0; i < coverage.Length; i++)
            {
                float distance = (point - Section(i)).sqrMagnitude;
                if (coverage[i] >= GelRules.SectionMl || distance >= best) continue;
                closest = i;
                best = distance;
            }
            if (closest >= 0) disinfect(closest, ml);
        }

        public void Refresh(bool visible)
        {
            for (int i = 0; i < bands.Length; i++)
            {
                bands[i].gameObject.SetActive(visible);
                bands[i].color = new Color(.25f, .85f, .75f, .55f * coverage[i] / GelRules.SectionMl);
            }
        }
    }
}
