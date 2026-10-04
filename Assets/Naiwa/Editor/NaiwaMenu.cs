using System.IO;
using Naiwa.Core;
using Naiwa.Fx;
using Naiwa.Pet;
using Naiwa.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace Naiwa.EditorTools
{
    /// <summary>Naiwa/一键设置 Player Settings、Naiwa/搭建主场景。</summary>
    public static class NaiwaMenu
    {
        [MenuItem("Naiwa/一键设置 Player Settings", priority = 30)]
        public static void ApplyPlayerSettings()
        {
            NaiwaEditorConfig.Invalidate();
            var cfg = NaiwaEditorConfig.Config;
            int w = cfg.window.WindowWidthPx, h = cfg.window.WindowHeightPx;

            PlayerSettings.companyName = "Naiwa";
            PlayerSettings.productName = "NaiwaPet";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = w;
            PlayerSettings.defaultScreenHeight = h;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.allowFullscreenSwitch = false;
            PlayerSettings.forceSingleInstance = false;      // 用互斥体自己实现（§3.6）
            PlayerSettings.useFlipModelSwapchain = false;    // C2：开启时 DWM 透明失效
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 }); // C1

            // C8：关闭 MSAA（所有质量等级）
            string[] names = QualitySettings.names;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.antiAliasing = 0;
            }
            QualitySettings.SetQualityLevel(current, false);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Naiwa] Player Settings 已设置：Windowed {w}×{h}、D3D11、关闭 DXGI flip model、Run In Background、MSAA 关闭");
        }

        [MenuItem("Naiwa/搭建主场景", priority = 31)]
        public static void BuildMainScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            NaiwaEditorConfig.Invalidate();
            var cfg = NaiwaEditorConfig.Config;
            var smokeMaterial = EnsureMaterial(NaiwaEditorConfig.SmokeMaterialPath, "Naiwa/ParticlePremultiplied");
            var uiMaterial = EnsureMaterial(NaiwaEditorConfig.UiMaterialPath, "Naiwa/UIPremultiplied");
            var silhouetteMaterial = EnsureMaterial(NaiwaEditorConfig.SilhouetteMaterialPath, "Naiwa/UISilhouettePremultiplied");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 正交相机：1 世界单位 = sizePx/6 像素；窗口为固定大画布（v1.0 §7.3），宠物在屏幕上的位置与 v0.1 一致
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = cfg.window.OrthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            camGo.transform.position = new Vector3(0f, cfg.window.CameraY, -10f);

            var root = new GameObject("NaiwaPet");
            var bootstrap = root.AddComponent<GameBootstrap>();

            var petGo = new GameObject("Pet");
            petGo.transform.SetParent(root.transform, false);
            var animator = petGo.AddComponent<PetAnimatorLite>();
            animator.layerA = CreateLayer(petGo.transform, "LayerA", 0);
            animator.layerB = CreateLayer(petGo.transform, "LayerB", 1);
            var squash = petGo.AddComponent<SquashStretch>();

            // UI：Canvas 由 HudController 在运行时搭建；EventSystem 用钩子驱动的输入模块（§7.2）
            var hudGo = new GameObject("Hud");
            hudGo.layer = 5;
            hudGo.AddComponent<RectTransform>();
            var hud = hudGo.AddComponent<HudController>();
            hud.uiMaterial = uiMaterial;
            hud.silhouetteMaterial = silhouetteMaterial;

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            var uiInput = esGo.AddComponent<HookUIInputModule>();

            var hitGo = new GameObject("HitShape");
            hitGo.transform.SetParent(root.transform, false);
            hitGo.AddComponent<PolygonCollider2D>();
            var hit = hitGo.AddComponent<PetHitTester>();
            hit.targetCamera = cam;

            var smokeGo = new GameObject("EvolutionSmoke");
            smokeGo.transform.SetParent(root.transform, false);
            var ps = smokeGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var psr = smokeGo.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = smokeMaterial;
            psr.sortingOrder = cfg.evolutionFx.sortingOrder;
            var fx = smokeGo.AddComponent<EvolutionFx>();

            bootstrap.targetCamera = cam;
            bootstrap.petAnimator = animator;
            bootstrap.hitTester = hit;
            bootstrap.evolutionFx = fx;
            bootstrap.squash = squash;
            bootstrap.hud = hud;
            bootstrap.uiInput = uiInput;

            Directory.CreateDirectory(NaiwaEditorConfig.ToFullPath(Path.GetDirectoryName(NaiwaEditorConfig.MainScenePath)));
            EditorSceneManager.SaveScene(scene, NaiwaEditorConfig.MainScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(NaiwaEditorConfig.MainScenePath, true) };
            Debug.Log($"[Naiwa] 主场景已生成：{NaiwaEditorConfig.MainScenePath}（并设为 Build Settings 唯一场景）");
        }

        static SpriteRenderer CreateLayer(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            sr.color = new Color(1f, 1f, 1f, 0f);
            return sr;
        }

        static Material EnsureMaterial(string path, string shaderName)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[Naiwa] 找不到 shader {shaderName}");
                return mat;
            }

            if (mat == null)
            {
                Directory.CreateDirectory(NaiwaEditorConfig.ToFullPath(Path.GetDirectoryName(path)));
                mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
