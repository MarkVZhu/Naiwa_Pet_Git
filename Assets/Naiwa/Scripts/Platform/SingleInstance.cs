using System;
using System.Threading;
using UnityEngine;

namespace Naiwa.Platform
{
    /// <summary>§3.6：命名互斥体实现单实例。第二个实例发现互斥体已存在时直接退出。仅打包后生效。</summary>
    public static class SingleInstance
    {
        public const string MutexName = @"Global\NaiwaPet_SingleInstance";
        static Mutex s_mutex;

        /// <summary>返回 false 表示已有实例在运行，调用方应退出。</summary>
        public static bool TryAcquire()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                s_mutex = new Mutex(true, MutexName, out bool createdNew);
                if (!createdNew)
                {
                    s_mutex.Dispose();
                    s_mutex = null;
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Naiwa] 创建单实例互斥体失败，忽略单实例检查：{e.Message}");
            }
#endif
            return true;
        }

        public static void Release()
        {
            if (s_mutex == null) return;
            try { s_mutex.ReleaseMutex(); } catch (Exception) { }
            s_mutex.Dispose();
            s_mutex = null;
        }
    }
}
