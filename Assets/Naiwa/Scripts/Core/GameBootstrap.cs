using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Naiwa.Content;
using Naiwa.Fx;
using Naiwa.Growth;
using Naiwa.Hud;
using Naiwa.Input;
using Naiwa.Pet;
using Naiwa.Platform;
using Naiwa.Save;
using UnityEngine;

namespace Naiwa.Core
{
    /// <summary>
    /// 唯一的入口 MonoBehaviour：按 Config → Save → Platform → Content → Growth → Input → Pet 的顺序构造服务，
    /// 并在 Update 里按固定顺序驱动它们。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public Camera targetCamera;
        public PetAnimatorLite petAnimator;
        public PetHitTester hitTester;
        public EvolutionFx evolutionFx;
        public SquashStretch squash;
        public GrowthCounterView counterView;

        const int CmdTogglePause = 1;
        const int CmdToggleCounter = 2;
        const int CmdDebugAdd1000 = 10;
        const int CmdDebugEvolve = 11;
        const int CmdDebugPlayAll = 12;
        const int CmdDebugResetEgg = 13;
        const int CmdExit = 99;
        const float HeldKeyPruneIntervalSec = 10f;

        GameConfig _config;
        ITimeProvider _time;
        SaveServiceLite _saveService;
        SaveDataLite _save;
        TransparentWindow _window;
        WindowMover _mover;
        ClickThroughController _clickThrough;
        NativeContextMenu _menu;
        FormLibrary _library;
        GrowthService _growth;
        InputFilterLite _filter;
        readonly List<IInputSource> _sources = new List<IInputSource>();
        readonly List<RawInputEvent> _events = new List<RawInputEvent>(64);
        PetClickRouter _router;
        PetStateMachineLite _stateMachine;

        bool _initialized;
        bool _isDuplicateInstance;
        bool _menuRequested;
        bool _usesNativeKeyIds;
        double _nextSaveAt, _nextTopmostAt, _nextPruneAt;

        void Awake()
        {
            // ---- Config ----
            _config = ConfigLoader.Load();
            _time = new SystemTimeProvider();

            if (!SingleInstance.TryAcquire())
            {
                Debug.LogWarning("[Naiwa] 已有实例在运行，本实例退出");
                _isDuplicateInstance = true;
                enabled = false;
                Application.Quit();
                return;
            }

            Application.runInBackground = true;
            Application.targetFrameRate = _config.app.targetFps;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 0;
            Physics2D.simulationMode = SimulationMode2D.Script;
            ApplyCameraSettings();

            // ---- Save ----
            _saveService = new SaveServiceLite(Application.persistentDataPath);
            _save = _saveService.Load();

            // ---- Platform ----
            _window = new TransparentWindow();
            _mover = new WindowMover(_window, _config.window.minVisibleFraction);
            _clickThrough = new ClickThroughController(_window, hitTester);
            _menu = new NativeContextMenu(_window);

            // ---- Content ----
            var manifest = LoadManifest();
            _library = new FormLibrary(manifest, _config.content.clipsResourcePath);
            var picker = new EmotePicker(_config.emote.avoidImmediateRepeat);

            // ---- Growth ----
            var savedForm = System.Enum.IsDefined(typeof(FormId), _save.form) ? (FormId)_save.form : FormId.Egg;
            _growth = new GrowthService(_config.growth.eggToSmall, _config.growth.smallToBig, _save.growth, savedForm);

            // ---- Input ----
            _filter = new InputFilterLite(_config.input.maxCountPerSecond, _config.input.ignoreInjected)
            {
                Paused = _save.countingPaused,
            };

            // ---- Pet ----
            evolutionFx.Configure(_config.evolutionFx, _config.evolution);
            if (squash != null) squash.Configure(_config.pet);
            else Debug.LogWarning("[Naiwa] 场景缺少 SquashStretch（请重新执行 Naiwa/搭建主场景）");
            if (counterView != null)
            {
                counterView.Configure(_config.counter, _config.window.PixelsPerUnit);
                counterView.SetVisible(_save.showCounter);
                counterView.SetValue(_growth.Growth, _filter.Paused);
            }
            else Debug.LogWarning("[Naiwa] 场景缺少计数框（请重新执行 Naiwa/搭建主场景）");
            _stateMachine = new PetStateMachineLite(this, _config, _library, petAnimator, hitTester, picker, _growth, evolutionFx, _time);
            _stateMachine.EvolutionCompleted += _ => SaveNow();

            _router = new PetClickRouter(_window, _mover, hitTester, _config.pet.dragThresholdPx);
            _router.Clicked += () => _stateMachine.OnPetClicked();
            _router.DragStarted += () => _stateMachine.IsDragging = true;
            _router.DragEnded += pos =>
            {
                _stateMachine.IsDragging = false;
                if (_window.IsNative) { _save.windowX = pos.x; _save.windowY = pos.y; }
                SaveNow();
            };
            _router.ContextMenuRequested += () => _menuRequested = true;

            _stateMachine.Begin();
        }

        IEnumerator Start()
        {
            if (_isDuplicateInstance) yield break;

            // 等 Unity 完成窗口创建后再改窗口样式
            yield return null;
            _window.Initialize(_config.window.WindowSizePx, _config.window.useLayeredAlpha);
            if (_window.IsNative)
            {
                var pos = _mover.RestoreOrDefault(_save.HasWindowPosition, _save.windowX, _save.windowY);
                _save.windowX = pos.x;
                _save.windowY = pos.y;
            }

            StartInputSources();

            double now = _time.RealtimeSeconds;
            _nextSaveAt = now + _config.app.saveIntervalSec;
            _nextTopmostAt = now + _config.window.topmostReassertSec;
            _nextPruneAt = now + HeldKeyPruneIntervalSec;
            _initialized = true;
            SaveNow();
        }

        void StartInputSources()
        {
            bool installKeyboard = _config.InputSource == InputSourceMode.GlobalHook;
#if UNITY_EDITOR
            if (!_config.debug.editorUseGlobalHook)
            {
                _sources.Add(new EditorInputSimulator(true, true, () => _time.MonotonicMs));
                return;
            }
            const bool requireFocus = true;
#else
            const bool requireFocus = false;
#endif
            var hook = new GlobalInputHook(installKeyboard);
            hook.Start();
            _sources.Add(hook);
            _usesNativeKeyIds = hook.KeyboardHooked;
            if (!hook.MouseHooked)
            {
                Debug.LogWarning("[Naiwa] 鼠标钩子不可用，改用 Unity Input 处理角色上的点击（失焦时可能收不到）");
                _sources.Add(new EditorInputSimulator(false, requireFocus, () => _time.MonotonicMs));
            }
        }

        void Update()
        {
            if (!_initialized) return;
            float dt = Time.deltaTime;

            // 1. 出队所有输入事件 → 判定 → 过滤计数
            _events.Clear();
            foreach (var src in _sources) src.Drain(_events);
            bool petClickOnly = _config.InputSource == InputSourceMode.PetClickOnly;
            bool anyPhysicalPress = false;
            foreach (var e in _events)
            {
                bool hitPet = false;
                bool isMouse = e.Kind == RawKind.MouseDown || e.Kind == RawKind.MouseUp;
                if (isMouse && !_menu.IsOpen) hitPet = _router.HandleEvent(e);

                if (petClickOnly && !(e.Kind == RawKind.MouseDown && hitPet)) continue;
                if (_filter.Process(e, out bool isPhysicalPress)) _growth.Add(1);
                anyPhysicalPress |= isPhysicalPress;
            }

            // 1.5 按下反馈：挤压回弹（拖动中不生效，§6.6）+ 计数框
            if (anyPhysicalPress && squash != null && !_router.IsDragging) squash.Trigger();
            if (counterView != null) counterView.SetValue(_growth.Growth, _filter.Paused);

            // 2. 拖动、穿透
            _router.Tick();
            _clickThrough.Tick(_router.IsDragging || _router.IsPressPending || _menu.IsOpen);

            // 3. 调试快捷键（无特效切换阶段）
            if (_config.debug.enabled && Application.isFocused)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1)) _stateMachine.DebugSwitchFormImmediate(FormId.Egg);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2)) _stateMachine.DebugSwitchFormImmediate(FormId.Small);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3)) _stateMachine.DebugSwitchFormImmediate(FormId.Big);
            }

            // 4. 表现
            _stateMachine.Tick(dt);
            petAnimator.Tick(dt);
            if (squash != null) squash.Tick(dt);

            // 5. 定时任务
            double now = _time.RealtimeSeconds;
            if (now >= _nextSaveAt) { _nextSaveAt = now + _config.app.saveIntervalSec; SaveNow(); }
            if (now >= _nextTopmostAt) { _nextTopmostAt = now + _config.window.topmostReassertSec; _window.ReassertTopmost(); }
            if (_usesNativeKeyIds && now >= _nextPruneAt)
            {
                _nextPruneAt = now + HeldKeyPruneIntervalSec;
                _filter.PruneHeldKeys(GlobalInputHook.IsKeyStillDown);
            }

            // 6. 右键菜单（原生菜单会阻塞主线程直到关闭，放在最后）
            if (_menuRequested)
            {
                _menuRequested = false;
                _menu.Show(BuildMenu(), OnMenuCommand);
            }
        }

        void OnGUI()
        {
            if (_menu != null && _menu.IsOpen) _menu.DrawEditorFallback();
        }

        // ---------- 菜单 ----------

        List<ContextMenuItem> BuildMenu()
        {
            var items = new List<ContextMenuItem>
            {
                ContextMenuItem.Label(BuildGrowthLine()),
                ContextMenuItem.Separator(),
                ContextMenuItem.Command(CmdTogglePause, _filter.Paused ? "继续计数" : "暂停计数", _filter.Paused),
                ContextMenuItem.Command(CmdToggleCounter, "显示计数框", _save.showCounter),
                ContextMenuItem.Separator(),
            };

            if (_config.debug.enabled)
            {
                items.Add(ContextMenuItem.Command(CmdDebugAdd1000, "[调试] 成长 +1000"));
                items.Add(ContextMenuItem.Command(CmdDebugEvolve, "[调试] 立即进化到下一阶段"));
                items.Add(ContextMenuItem.Command(CmdDebugPlayAll, "[调试] 播放全部表情（依次）"));
                items.Add(ContextMenuItem.Command(CmdDebugResetEgg, "[调试] 重置为奶蛋"));
                items.Add(ContextMenuItem.Separator());
            }

            items.Add(ContextMenuItem.Command(CmdExit, "退出"));
            return items;
        }

        string BuildGrowthLine()
        {
            var inv = CultureInfo.InvariantCulture;
            string growth = _growth.Growth.ToString("N0", inv);
            var form = _growth.Form;
            if (!form.HasNext())
                return $"成长值：{growth}（{form.DisplayName()}）";
            int remain = Mathf.Max(0, _growth.ThresholdOf(form.Next()) - _growth.Growth);
            return $"成长值：{growth}（{form.DisplayName()} → {form.Next().DisplayName()} 还差 {remain.ToString("N0", inv)}）";
        }

        void OnMenuCommand(int id)
        {
            switch (id)
            {
                case CmdTogglePause:
                    _filter.Paused = !_filter.Paused;
                    SaveNow();
                    break;
                case CmdToggleCounter:
                    _save.showCounter = !_save.showCounter;
                    if (counterView != null) counterView.SetVisible(_save.showCounter);
                    SaveNow();
                    break;
                case CmdDebugAdd1000:
                    _growth.Add(1000);
                    SaveNow();
                    break;
                case CmdDebugEvolve:
                    if (_growth.NextThreshold is int t && _growth.Growth < t) _growth.SetGrowth(t);
                    break;
                case CmdDebugPlayAll:
                    _stateMachine.DebugPlayAllEmotes();
                    break;
                case CmdDebugResetEgg:
                    _stateMachine.DebugResetToEgg();
                    SaveNow();
                    break;
                case CmdExit:
                    Quit();
                    break;
            }
        }

        void Quit()
        {
            SaveNow();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------- 存档 / 生命周期 ----------

        void SaveNow()
        {
            if (_saveService == null || _isDuplicateInstance) return;
            _save.growth = _growth.Growth;
            _save.form = (int)_growth.Form;
            _save.countingPaused = _filter.Paused;
            if (_window != null && _window.IsNative && !_mover.IsDragging)
            {
                var pos = _window.GetPosition();
                _save.windowX = pos.x;
                _save.windowY = pos.y;
            }
            _saveService.Save(_save);
        }

        void OnApplicationQuit()
        {
            if (_isDuplicateInstance) return;
            SaveNow();
            DisposeInputs();
            SingleInstance.Release();
        }

        void OnDestroy()
        {
            DisposeInputs();
        }

        void DisposeInputs()
        {
            foreach (var src in _sources) src.Dispose();
            _sources.Clear();
        }

        ContentManifestResult LoadManifest()
        {
            var asset = Resources.Load<TextAsset>(_config.content.manifestResourcePath);
            if (asset == null)
            {
                Debug.LogError($"[Naiwa] 找不到映射表 Resources/{_config.content.manifestResourcePath}.json");
                return ContentManifest.Parse(null);
            }

            var result = ContentManifest.Parse(asset.text);
            foreach (var w in result.Warnings) Debug.LogWarning("[Naiwa] " + w);
            foreach (var err in result.Errors) Debug.LogError("[Naiwa] " + err);
            return result;
        }

        void ApplyCameraSettings()
        {
            if (targetCamera == null) return;
            var p = targetCamera.transform.position;
            targetCamera.transform.position = new Vector3(0f, PetGeometry.CanvasCenterY, p.z < 0f ? p.z : -10f);
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = _config.window.OrthographicSize;
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            targetCamera.allowHDR = false;
            targetCamera.allowMSAA = false;
        }
    }
}
