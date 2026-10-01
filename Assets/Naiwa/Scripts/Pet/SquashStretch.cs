using Naiwa.Core;
using UnityEngine;

namespace Naiwa.Pet
{
    /// <summary>
    /// 按下时的挤压回弹（§6.6 简化版）。作用在 Pet 根节点 localScale 上，叠加在所有动画之上。
    /// Sprite 的 pivot 在脚底、Pet 根节点原点在脚底，所以缩放时底部位置不变。
    /// 按下当帧立即压到 squashScaleY，再在 squashRecoverSec 内 ease-out 回到 1；连续按下不累加，每次重新开始。
    /// </summary>
    public sealed class SquashStretch : MonoBehaviour
    {
        float _scaleX = 1f, _scaleY = 0.93f, _recoverSec = 0.12f;
        float _elapsed = -1f;
        bool _fresh;

        public bool IsActive => _elapsed >= 0f;

        public void Configure(PetConfig config)
        {
            _scaleX = config.squashScaleX;
            _scaleY = config.squashScaleY;
            _recoverSec = Mathf.Max(0.01f, config.squashRecoverSec);
            ResetScale();
        }

        public void Trigger()
        {
            _elapsed = 0f;
            _fresh = true;
        }

        public void ResetScale()
        {
            _elapsed = -1f;
            _fresh = false;
            transform.localScale = Vector3.one;
        }

        public void Tick(float dt)
        {
            if (_elapsed < 0f) return;

            // 触发当帧不推进时间，保证按下同一帧画面就压到最低（支柱 1）
            if (_fresh) _fresh = false;
            else _elapsed += dt;

            float k = _elapsed / _recoverSec;
            if (k >= 1f)
            {
                ResetScale();
                return;
            }

            float inv = 1f - k;
            float ease = 1f - inv * inv * inv;
            transform.localScale = new Vector3(Mathf.Lerp(_scaleX, 1f, ease), Mathf.Lerp(_scaleY, 1f, ease), 1f);
        }
    }
}
