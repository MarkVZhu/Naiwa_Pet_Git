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

        public void SetShapeFrom(Sprite sprite)
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
                    new Vector2(b.min.x, b.min.y), new Vector2(b.max.x, b.min.y),
                    new Vector2(b.max.x, b.max.y), new Vector2(b.min.x, b.max.y),
                });
                return;
            }

            col.pathCount = count;
            for (int i = 0; i < count; i++)
            {
                _points.Clear();
                sprite.GetPhysicsShape(i, _points);
                col.SetPath(i, _points);
            }
        }

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
