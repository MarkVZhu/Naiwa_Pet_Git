using UnityEngine;

namespace Naiwa.Pet
{
    public sealed class SpriteSequence
    {
        public readonly string Id;
        public readonly Sprite[] Frames;
        public readonly float Fps;
        public readonly bool Loop;
        /// <summary>显示缩放（所属阶段的 pet.xxxScale），以脚底为锚点。由 FormLibrary 加载时设置。</summary>
        public float DisplayScale = 1f;

        public SpriteSequence(string id, Sprite[] frames, float fps, bool loop)
        {
            Id = id;
            Frames = frames ?? new Sprite[0];
            Fps = fps > 0f ? fps : 12f;
            Loop = loop;
        }

        public int FrameCount => Frames.Length;
        public float Duration => FrameCount / Fps;

        /// <summary>累计时间换算帧号：frameIndex = floor(elapsed × fps)。</summary>
        public int FrameIndexAt(float elapsed, bool loop)
        {
            if (FrameCount == 0) return -1;
            int idx = Mathf.FloorToInt(Mathf.Max(0f, elapsed) * Fps);
            return loop ? idx % FrameCount : Mathf.Min(idx, FrameCount - 1);
        }

        public override string ToString() => $"{Id}[{FrameCount}f@{Fps}]";
    }
}
