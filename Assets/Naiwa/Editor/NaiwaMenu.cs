using System.IO;
using Naiwa.Core;
using Naiwa.Fx;
using Naiwa.Hud;
using Naiwa.Pet;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
            int size = cfg.window.WindowSizePx;

            PlayerSettings.companyName = "Naiwa";
            PlayerSettings.productName = "NaiwaPet";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = size;
            PlayerSettings.defaultScreenHeight = size;
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
            Debug.Log($"[Naiwa] Player Settings 已设置：Windowed {size}×{size}、D3D11、关闭 DXGI flip model、Run In Background、MSAA 关闭");
        }

        [MenuItem("Naiwa/搭建主场景", priority = 31)]
        public static void BuildMainScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            NaiwaEditorConfig.Invalidate();
            var cfg = NaiwaEditorConfig.Config;
            var smokeMaterial = EnsureMaterial(NaiwaEditorConfig.SmokeMaterialPath, "Naiwa/ParticlePremultiplied");
            var textMaterial = EnsureMaterial(NaiwaEditorConfig.TextMaterialPath, "Naiwa/TextPremultiplied");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 正交相机（V.3.8）：1 世界单位 = sizePx/6 像素；相机中心对准画布中心
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
            camGo.transform.position = new Vector3(0f, PetGeometry.CanvasCenterY, -10f);

            var root = new GameObject("NaiwaPet");
            var bootstrap = root.AddComponent<GameBootstrap>();

            var petGo = new GameObject("Pet");
            petGo.transform.SetParent(root.transform, false);
            var animator = petGo.AddComponent<PetAnimatorLite>();
            animator.layerA = CreateLayer(petGo.transform, "LayerA", 0);
            animator.layerB = CreateLayer(petGo.transform, "LayerB", 1);
            var squash = petGo.AddComponent<SquashStretch>();

            // 计数框：脚下方，不参与点击判定
            var counterGo = new GameObject("CounterHud");
            counterGo.transform.SetParent(root.transform, false);
            var counter = counterGo.AddComponent<GrowthCounterView>();
            var boxGo = new GameObject("Box");
            boxGo.transform.SetParent(counterGo.transform, false);
            counter.box = boxGo.AddComponent<SpriteRenderer>();
            counter.box.sortingOrder = cfg.counter.sortingOrder;
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(counterGo.transform, false);
            counter.textFilter = textGo.AddComponent<MeshFilter>();
            counter.textRenderer = textGo.AddComponent<MeshRenderer>();
            counter.textRenderer.sharedMaterial = textMaterial;
            counter.textRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            counter.textRenderer.receiveShadows = false;
            counter.textRenderer.sortingOrder = cfg.counter.sortingOrder + 1;
            counter.textMaterial = textMaterial;

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
            bootstrap.counterView = counter;

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
