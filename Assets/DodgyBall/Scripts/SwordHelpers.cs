using System.Collections.Generic;
using UnityEngine;

namespace DodgyBall.Scripts
{
    public static class SwordHelpers
    {
        private static readonly Dictionary<int, GameObject> _startSwords = new();
        private static readonly Dictionary<int, GameObject> _endSwords = new();
        private static readonly Dictionary<int, GameObject> _planes = new();
        
        // Old debug funcs, Quaternion Directions are annoying haha
        public static void DebugStartEndSword(GameObject source, Transform owner, Quaternion start, Quaternion end)
        {
            int id = owner.GetInstanceID();
            
            // Clean Up
            if (_startSwords.TryGetValue(id, out var s) && s) UnityEngine.Object.Destroy(s);
            if (_endSwords.TryGetValue(id, out var e) && e) UnityEngine.Object.Destroy(e);
    
            var startSword = Object.Instantiate(source, owner.position, start);
            var endSword   = Object.Instantiate(source, owner.position, end);
            
            startSword.name = $"DebugStartSword";
            endSword.name = $"DebugEndSword";

            // DISABLE the script on the copy to prevent infinite loop
            var a = startSword.GetComponent<EfficientSword>();
            if (a) a.enabled = false;
            var b = endSword.GetComponent<EfficientSword>();
            if (b) b.enabled = false;

            SetTranslucentColor(startSword, new Color(0, 1, 0, 0.3f));
            SetTranslucentColor(endSword,   new Color(1, 0, 0, 0.3f));

            _startSwords[id] = startSword;
            _endSwords[id]   = endSword;
        }

        private static void SetTranslucentColor(GameObject obj, Color color)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
                SetTransparent(r, color);
        }
        
        private static readonly int _Color = Shader.PropertyToID("_Color");
        private static void SetTransparent(Renderer renderer, Color color)
        {
            var mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            mpb.SetColor(_Color, color);
            renderer.SetPropertyBlock(mpb);
            // If your shader needs proper transparency mode switching,
            // use a transparent material instead of mutating keywords here.
        }
        
        // Old translucent 
        // void SetTranslucentColor(GameObject obj, Color color)
        // {
        //     Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        //     foreach (Renderer r in renderers)
        //     {
        //         foreach (Material mat in r.materials)
        //         {
        //             mat.color = color;
        //             mat.SetFloat("_Mode", 3); // Transparent
        //             mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        //             mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        //             mat.SetInt("_ZWrite", 0);
        //             mat.DisableKeyword("_ALPHATEST_ON");
        //             mat.EnableKeyword("_ALPHABLEND_ON");
        //             mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        //             mat.renderQueue = 3000;
        //         }
        //     }
        // }
    
        public static void DrawSwingPlane(Transform owner, Vector3 swingAxis, Vector3 targetDir, float size = 2.5f)
        {
            int id = owner.GetInstanceID();
            if (_planes.TryGetValue(id, out var old) && old) UnityEngine.Object.Destroy(old);

            var plane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plane.name = "SwingPlane";
            plane.transform.localScale = new Vector3(size, size, 0.001f);
            plane.transform.position = owner.position;

            var planeUp = Vector3.Cross(swingAxis, targetDir).normalized;
            plane.transform.rotation = Quaternion.LookRotation(swingAxis, planeUp);

            var r = plane.GetComponent<Renderer>();
            if (r) SetTransparent(r, new Color(0, 1, 0, 0.15f));

            _planes[id] = plane;
        }
        
        public static void ClearDebug(Transform owner)
        {
            int id = owner.GetInstanceID();
            if (_startSwords.TryGetValue(id, out var s) && s) UnityEngine.Object.Destroy(s);
            if (_endSwords.TryGetValue(id, out var e) && e) UnityEngine.Object.Destroy(e);
            if (_planes.TryGetValue(id, out var p) && p) UnityEngine.Object.Destroy(p);
            _startSwords.Remove(id);
            _endSwords.Remove(id);
            _planes.Remove(id);
        }
        
        public static void DebugSwingKeyframe(SwingKeyframe kf)
        {
            Debug.Log(
                $"Random loaded keyframe {kf.angle} \n" +
                $"Axis: {kf.localSwingAxis} \n" +
                $"Relative Start: {kf.relativeStart} \n" +
                $"Relative End: {kf.relativeEnd} \n" +
                $"IsUpward: {kf.isUpwardSwing}"
            );
        }
    }
}