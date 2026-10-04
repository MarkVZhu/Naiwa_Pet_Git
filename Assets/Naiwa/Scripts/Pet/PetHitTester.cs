using System.Collections.Generic;
using UnityEngine;

namespace Naiwa.Pet
{
    /// <summary>
    /// 点击判定（§V.3.5）：PolygonCollider2D，形状取自当前阶段 idle 第 0 帧的 Sprite Physics Shape。
    /// 表情期间轮廓会变，但仍用 idle 形状 [A]。
    /// </summary>
    [RequireComponent(typeof(PolygonCollider2D))]
    public sealed class PetHitTester : MonoBehaviour
    {
        public Camera targetCamera;

        PolygonCollider2D _collider;
        readonly List<Vector2> _points = new List<Vector2>();

        PolygonCollider2D Collider => _collider != null ? _collider : (_collider = GetComponent<PolygonCollider2D>());

        /// <summary>当前判定形状的最高点（本地坐标，世界单位，脚底为 0）。用于「新！」定位。</summary>
        public float TopY { get; private set; }

        /// <param name="scale">阶段显示缩放（以脚底 = 原点为锚点）。</param>
        public void SetShapeFrom(Sprite sprite, float scale = 1f)
        {
            _scale = scale > 0f ? scale : 1f;
            ApplyShape(sprite);
            var col = Collider;
            float top = 0f;
            for (int i = 0; i < col.pathCount; i++)
                foreach (var p in col.GetPath(i)) top = Mathf.Max(top, p.y);
            TopY = top;
        }

        void ApplyShape(Sprite sprite)
        {
            var col = Collider;
            if (sprite == null)
            {
                col.pathCount = 0;
                return;
            }

            int count = sprite.GetPhysicsShapeCount();
            if (count == 0)
            {
                // 没有 Physics Shape（导入设置未生效）：退化为 Sprite 包围盒
                Debug.LogWarning($"[Naiwa] Sprite '{sprite.name}' 没有 Physics Shape，点击判定退化为包围盒");
                var b = sprite.bounds;
                col.pathCount = 1;
                col.SetPath(0, new[]
                {
                    new Vector2(b.min.x, b.min.y) * _scale, new Vector2(b.max.x, b.min.y) * _scale,
                    new Vector2(b.max.x, b.max.y) * _scale, new Vector2(b.min.x, b.max.y) * _scale,
                });
                return;
            }

            col.pathCount = count;
            for (int i = 0; i < count; i++)
            {
                _points.Clear();
                sprite.GetPhysicsShape(i, _points);
                for (int p = 0; p < _points.Count; p++) _points[p] *= _scale;
                col.SetPath(i, _points);
            }
        }

        float _scale = 1f;

        /// <summary>Unity 屏幕坐标（原点左下）是否命中角色。</summary>
        public bool HitUnityScreen(Vector2 screenPoint)
        {
            if (targetCamera == null) return false;
            if (screenPoint.x < 0 || screenPoint.y < 0 || screenPoint.x > Screen.width || screenPoint.y > Screen.height)
                return false;
            var col = Collider;
            if (col.pathCount == 0) return false;
            Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, -targetCamera.transform.position.z));
            return col.OverlapPoint(world);
        }
    }
}
