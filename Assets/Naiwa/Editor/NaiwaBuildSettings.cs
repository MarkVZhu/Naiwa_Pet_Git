using UnityEditor;
using UnityEngine;

namespace Naiwa.EditorTools
{
    /// <summary>
    /// 打包选项，存在 ProjectSettings/NaiwaBuildSettings.asset（随工程走，不是本机偏好）。
    /// 只影响「Naiwa/打包 Windows」的产物；Editor 里运行始终按 game_config.json 的 debug.enabled。
    /// </summary>
    [FilePath("ProjectSettings/NaiwaBuildSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class NaiwaBuildSettings : ScriptableSingleton<NaiwaBuildSettings>
    {
        /// <summary>
        /// true：包里保留右键「调试」子菜单（仍可在包内 game_config.json 用 debug.enabled 关掉）。
        /// false：打包时定义 NAIWA_NO_DEBUG 并把包内 debug.enabled 写成 false，调试功能彻底不可用。
        /// </summary>
        public bool includeDebugInBuild = true;

        public void SaveNow() => Save(true);

        const string MenuPath = "Naiwa/打包选项/包含调试菜单";

        [MenuItem(MenuPath, priority = 49)]
        static void Toggle()
        {
            instance.includeDebugInBuild = !instance.includeDebugInBuild;
            instance.SaveNow();
            Debug.Log($"[Naiwa] 打包时{(instance.includeDebugInBuild ? "包含" : "不包含")}调试菜单（下次「Naiwa/打包 Windows」生效）");
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, instance.includeDebugInBuild);
            return true;
        }
    }
}
