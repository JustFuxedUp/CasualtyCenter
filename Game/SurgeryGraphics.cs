using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace CasualtiesSurgery.Game
{
    public sealed class ToolWheelGraphic : MaskableGraphic
    {
        public int Selected = -1;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            for (int tool = 0; tool < 2; tool++)
                for (int segment = 0; segment < 30; segment++)
                {
                    float a = (tool * Mathf.PI - Mathf.PI / 2 + 0.04f) + segment * (Mathf.PI - 0.08f) / 30;
                    float b = a + (Mathf.PI - 0.08f) / 30;
                    Color c = tool == Selected ? new Color(0.12f,0.25f,0.48f,0.96f) : new Color(0.025f,0.06f,0.14f,0.96f);
                    int start = mesh.currentVertCount;
                    foreach (Vector2 point in new[] { Polar(a, 55), Polar(a, 190), Polar(b, 190), Polar(b, 55) })
                        mesh.AddVert(point, c, Vector2.zero);
                    mesh.AddTriangle(start, start+1, start+2); mesh.AddTriangle(start, start+2, start+3);
                }
        }
        static Vector2 Polar(float a, float radius) => new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
    }

    // Fade the lower arm without editing or redistributing the game's hand sprites.
    public sealed class WristFade : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            var triangles = new List<UIVertex>(); mesh.GetUIVertexStream(triangles);
            if (triangles.Count == 0) return;
            float low = float.MaxValue, high = float.MinValue;
            foreach (var v in triangles) { low = Mathf.Min(low, v.position.y); high = Mathf.Max(high, v.position.y); }
            float height = high - low; if (height < 0.001f) return;
            mesh.Clear();
            for (int triangle = 0; triangle < triangles.Count; triangle += 3)
                for (int band = 0; band < 12; band++)
                {
                    var polygon = new List<UIVertex> { triangles[triangle], triangles[triangle+1], triangles[triangle+2] };
                    polygon = Clip(polygon, low + height * band / 12, true);
                    polygon = Clip(polygon, low + height * (band+1) / 12, false);
                    if (polygon.Count < 3) continue;
                    int first = mesh.currentVertCount;
                    foreach (var original in polygon)
                    {
                        var vertex = original; var c = (Color)vertex.color;
                        c.a *= Mathf.InverseLerp(0.35f, 0.72f, (vertex.position.y-low)/height);
                        vertex.color = c; mesh.AddVert(vertex);
                    }
                    for (int i = 1; i < polygon.Count-1; i++) mesh.AddTriangle(first, first+i, first+i+1);
                }
        }
        static List<UIVertex> Clip(List<UIVertex> input, float y, bool above)
        {
            var result = new List<UIVertex>(); if (input.Count == 0) return result;
            UIVertex previous = input[input.Count-1]; bool wasInside = above ? previous.position.y >= y : previous.position.y <= y;
            foreach (var current in input)
            {
                bool inside = above ? current.position.y >= y : current.position.y <= y;
                if (inside != wasInside)
                {
                    float t = (y-previous.position.y)/(current.position.y-previous.position.y);
                    var v = previous; v.position = Vector3.Lerp(previous.position, current.position, t);
                    v.uv0 = Vector4.Lerp(previous.uv0, current.uv0, t); v.color = Color.Lerp(previous.color, current.color, t);
                    result.Add(v);
                }
                if (inside) result.Add(current);
                previous = current; wasInside = inside;
            }
            return result;
        }
    }
}
