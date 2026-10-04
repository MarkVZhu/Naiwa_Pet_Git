using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Naiwa.Content;
using Naiwa.Economy;
using Naiwa.Fx;
using Naiwa.Growth;
using Naiwa.Input;
using Naiwa.Pet;
using Naiwa.Platform;
using Naiwa.Save;
using Naiwa.UI;
using UnityEngine;

namespace Naiwa.Core
{
    /// <summary>
    /// 唯一的入口 MonoBehaviour：按 Config → Save → Platform → Content → Growth/Economy → Input → Pet → UI 的顺序构造服务，
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
        public HudController hud;
        public HookUIInputModule uiInput;

        // 菜单命令
        const int CmdTogglePause = 1;
        const int CmdToggleClicks = 2;
        const int CmdToggleGrowth = 3;
        const int CmdToggleCollection = 4;
        const int CmdSwitchFormBase = 20; // + FormId
        const int CmdDebugClicks2000 = 40;
        const int CmdDebugClearCooldown = 41;
        const int CmdDebugDrawFree = 42;
        const int CmdDebugResetCollection = 43;
        const int CmdDebugUnlockAll = 44;
        const int CmdDebugSetFormBase = 50; // + FormId
        const int CmdDebugGrowth1000 = 60;
        const int CmdDebugPlayAll = 61;
        const int CmdExit = 99;
        const float HeldKeyPruneIntervalSec = 10f;
        const float LotteryCheckIntervalSec = 1f;

        GameConfig _config;
        ITimeProvider _time;
        SaveServiceLite _saveService;
        SaveDataLite _save;
        TransparentWindow _window;
        WindowCanvasLayout _layout;
        WindowMover _mover;
        ClickThroughController _clickThrough;
        NativeContextMenu _menu;
        EmoteCatalog _catalog;
        UnlockService _unlocks;
        FormLibrary _library;
        IconLibrary _icons;
        GrowthService _growth;
        FormSwitchService _forms;
        ClickWallet _wallet;
        LotteryService _lottery;
        InputFilterLite _filter;
        readonly List<IInputSource> _sources = new List<IInputSource>();
        readonly List<RawInputEvent> _events = new List<RawInputEvent>(64);
        PetClickRouter _router;
        PetStateMachineLite _stateMachine;

        bool _initialized;
        bool _isDuplicateInstance;
        bool _menuRequested;
        bool _usesNativeKeyIds;
        bool _lotteryDirty = true;
        double _nextSaveAt, _nextTopmostAt, _nextPruneAt, _nextLotteryCheckAt;

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
            _saveService = new SaveServiceLite(Application.persistentDataPath, null, new SaveDefaults
            {
                hudShowClicks = _config.hud.showClicksDefault,
                hudShowGrowth = _config.hud.showGrowthDefault,
                windowShiftX = _config.window.sideWidthPx,
            });
            _save = _saveService.Load();
            if (_saveService.LastLoadMigrated)
                Debug.Log($"[Naiwa] 存档已从 v1 升级到 v2：点击量 = {_save.clicks}（旧存档备份为 save.v1.bak.json）");
            _save.ApplyHeadroom(_config.window.headroomPx);

            // ---- Platform ----
            _layout = new WindowCanvasLayout(_config.window);
            _window = new TransparentWindow();
            _mover = new WindowMover(_window, _layout, _config.window.minVisibleFraction);
            _clickThrough = new ClickThroughController(_window, hitTester, () => uiInput != null && uiInput.IsOverUi);
            _menu = new NativeContextMenu(_window);

            // ---- Content ----
            var content = LoadContent();
            _catalog = new EmoteCatalog(content);
            _unlocks = new UnlockService(_catalog, _save.unlockedEmotes);
            _unlocks.ApplyDefaults();
            _unlocks.Changed += () => _lotteryDirty = true;
            _library = new FormLibrary(content, _config.content.clipsResourcePath, _unlocks.IsUnlocked, _config.pet.ScaleOf);
            _icons = new IconLibrary(Path.Combine(Application.streamingAssetsPath, "content"), _config.content.clipsResourcePath, LoadClipIndex());
            var picker = new EmotePicker(_config.emote.avoidImmediateRepeat);

            // ---- Growth / Economy ----
            var highest = Enum.IsDefined(typeof(FormId), _save.highestForm) ? (FormId)_save.highestForm : FormId.Egg;
            var display = Enum.IsDefined(typeof(FormId), _save.form) ? (FormId)_save.form : FormId.Egg;
            if (display > highest) highest = display;
            _growth = new GrowthService(_config.growth.eggToSmall, _config.growth.smallToBig, _save.growth, highest);
            _forms = new FormSwitchService(_growth, display, _config.form.autoEvolveOnUnlock);
            _wallet = new ClickWallet(_save.clicks, _save.clicksLifetimeEarned, _save.clicksLifetimeSpent);
            _wallet.Changed += (_, __) => _lotteryDirty = true;
            _lottery = new LotteryService(_wallet, _catalog, _unlocks, new SystemRandom(), () => _forms.DisplayForm, SaveNow,
                _config.lottery.cost, _config.lottery.Cooldown, _config.lottery.Scope,
                _save.lotteryNextAvailableUnixMs, _save.lotteryDrawCount);

            // ---- Input ----
            _filter = new InputFilterLite(_config.input.maxCountPerSecond, _config.input.ignoreInjected)
            {
                Paused = _save.countingPaused,
            };

            // ---- Pet ----
            evolutionFx.Configure(_config.evolutionFx, _config.evolution);
            if (squash != null) squash.Configure(_config.pet);
            else Debug.LogWarning("[Naiwa] 场景缺少 SquashStretch（请重新执行 Naiwa/搭建主场景）");

            _stateMachine = new PetStateMachineLite(this, _config, _library, petAnimator, hitTester, picker,
                _catalog, _unlocks, _forms, evolutionFx, _time);
            _stateMachine.EmptyPoke += () => { if (squash != null) squash.Poke(); };
            _stateMachine.RevealStarted += _ => { if (hud != null) hud.Badge.Show(_config.pet.newBadgeSec); };
            _stateMachine.TransitionCompleted += _ =>
            {
                SaveNow();
                if (hud != null) hud.Collection.Refresh();
                _lotteryDirty = true;
            };
            _growth.HighestFormChanged += _ => RefreshGrowthBar(true);
            _forms.UnlockNotice += OnFormUnlockNotice;
            _forms.DisplayFormChanged += _ =>
            {
                if (hud != null) { hud.Collection.Refresh(); hud.SetHeadTop(hitTester.TopY * _config.window.PixelsPerUnit); }
            };

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

            // ---- UI ----
            if (hud != null)
            {
                hud.Initialize(_config, _catalog, _unlocks, _icons, () => _forms.DisplayForm);
                hud.SetToggleImmediate(_save.hudShowClicks, _save.hudShowGrowth);
                hud.SetClicks(_wallet.Balance, _filter.Paused);
                RefreshGrowthBar(true);
                hud.SetHeadTop(hitTester.TopY * _config.window.PixelsPerUnit);
                hud.Bubble.Clicked += OnBubbleClicked;
                hud.Collection.PlayRequested += id => _stateMachine.RequestPlay(id);
                hud.Collection.CloseRequested += () => hud.Collection.Close();
            }
            else Debug.LogWarning("[Naiwa] 场景缺少 HudController（请重新执行 Naiwa/搭建主场景）");

            if (uiInput != null)
                uiInput.SetPositionProvider(() => _window.DesktopToUnityScreen(_window.GetCursorDesktop()));
            else Debug.LogWarning("[Naiwa] 场景缺少 HookUIInputModule（请重新执行 Naiwa/搭建主场景）");
        }

        IEnumerator Start()
        {
            if (_isDuplicateInstance) yield break;

            // 等 Unity 完成窗口创建后再改窗口样式
            yield return null;
            _window.Initialize(_layout.Width, _layout.Height, _config.window.useLayeredAlpha);
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

            // 1. 出队所有输入事件 → UI 优先 → 宠物判定 → 过滤计数（成长值与点击量同涨）
            _events.Clear();
            foreach (var src in _sources) src.Drain(_events);
            bool petClickOnly = _config.InputSource == InputSourceMode.PetClickOnly;
            bool anyPhysicalPress = false;
            foreach (var e in _events)
            {
                bool hitPet = HandlePointer(e);
                if (petClickOnly && !(e.Kind == RawKind.MouseDown && hitPet)) continue;
                if (_filter.Process(e, out bool isPhysicalPress))
                {
                    _growth.Add(1);
                    _wallet.Earn(1);
                }
                anyPhysicalPress |= isPhysicalPress;
            }

            // 1.5 按下反馈：挤压回弹（拖动中不生效）
            if (anyPhysicalPress && squash != null && !_router.IsDragging) squash.Trigger();

            // 2. 悬停、拖动、穿透
            if (uiInput != null) uiInput.Tick();
            _router.Tick();
            _clickThrough.Tick(_router.IsDragging || _router.IsPressPending || _menu.IsOpen);

            // 3. 调试快捷键（Editor 下方便切阶段）
            if (DebugAvailable && Application.isFocused)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1)) DebugSetForm(FormId.Egg);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2)) DebugSetForm(FormId.Small);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3)) DebugSetForm(FormId.Big);
            }

            // 4. 表现
            _stateMachine.Tick(dt);
            petAnimator.Tick(dt);
            if (squash != null) squash.Tick(dt);

            // 5. HUD
            double now = _time.RealtimeSeconds;
            if (hud != null)
            {
                hud.SetClicks(_wallet.Balance, _filter.Paused);
                RefreshGrowthBar(false);
                if (_lotteryDirty || now >= _nextLotteryCheckAt) RefreshLotteryUi();
            }

            // 6. 定时任务
            if (now >= _nextSaveAt) { _nextSaveAt = now + _config.app.saveIntervalSec; SaveNow(); }
            if (now >= _nextTopmostAt) { _nextTopmostAt = now + _config.window.topmostReassertSec; _window.ReassertTopmost(); }
            if (_usesNativeKeyIds && now >= _nextPruneAt)
            {
                _nextPruneAt = now + HeldKeyPruneIntervalSec;
                _filter.PruneHeldKeys(GlobalInputHook.IsKeyStillDown);
            }

            // 7. 右键菜单（原生菜单会阻塞主线程直到关闭，放在最后）
            if (_menuRequested)
            {
                _menuRequested = false;
                _menu.Show(BuildMenu(), OnMenuCommand);
            }
        }

        /// <summary>鼠标事件分发：UI 优先（§7.2），其余交给宠物。返回 MouseDown 是否落在宠物或 UI 上。</summary>
        bool HandlePointer(in RawInputEvent e)
        {
            if (_menu.IsOpen) return false;
            switch (e.Kind)
            {
                case RawKind.MouseDown:
                    if (uiInput != null && e.Button == MouseBtn.Left && !_router.IsDragging && uiInput.PointerDown()) return true;
                    if (uiInput != null && e.Button == MouseBtn.Right && uiInput.IsOverUi) { _menuRequested = true; return true; }
                    return _router.HandleEvent(e);
                case RawKind.MouseUp:
                    if (uiInput != null && e.Button == MouseBtn.Left) uiInput.PointerUp();
                    _router.HandleEvent(e);
                    return false;
                case RawKind.MouseWheel:
                    if (uiInput != null) uiInput.Scroll(e.WheelDelta);
                    return false;
                default:
                    return false;
            }
        }

        void OnGUI()
        {
            if (_menu != null && _menu.IsOpen) _menu.DrawEditorFallback();
        }

        // ---------- 成长 / 抽奖 UI ----------

        void RefreshGrowthBar(bool snap)
        {
            if (hud == null) return;
            hud.SetGrowth(GrowthProgress.Ratio(_growth), GrowthProgress.IsMax(_growth.HighestForm), snap);
        }

        void RefreshLotteryUi()
        {
            _lotteryDirty = false;
            _nextLotteryCheckAt = _time.RealtimeSeconds + LotteryCheckIntervalSec;
            var status = _lottery.GetStatus(_time.UtcNow);
            hud.SetBubbleWanted(status.IsReady && !hud.Bubble.IsBusy);
            if (hud.Collection.IsOpen)
                hud.Collection.SetHint(UiTextFormat.CollectionLine1(status), UiTextFormat.CollectionLine2(status));
        }

        void OnBubbleClicked()
        {
            var result = _lottery.TryDraw(_time.UtcNow); // C14：扣费、冷却、解锁、存档在这里一次完成
            if (!result.Ok)
            {
                Debug.LogWarning("[Naiwa] 抽奖条件已不满足（可能刚好在冷却边界），本次不扣费");
                hud.Bubble.HideImmediate();
                _lotteryDirty = true;
                return;
            }

            var def = _catalog.Get(result.EmoteId);
            if (def != null && def.Form == _forms.DisplayForm) _library.GetOrLoadEmote(def); // 转盘转动期间预加载
            hud.SetClicks(_wallet.Balance, _filter.Paused);
            hud.Bubble.PlaySpin(() => Reveal(result.EmoteId));
        }

        /// <summary>揭晓（v1.0 §5.6）：当前阶段 → 桌宠播放 +「新！」；图鉴开着 → 格子揭晓动画，否则弹卡片。</summary>
        void Reveal(string id)
        {
            var def = _catalog.Get(id);
            if (def == null) return;
            _lotteryDirty = true;

            if (def.Form == _forms.DisplayForm) _stateMachine.QueueReveal(id);

            if (hud == null) return;
            if (hud.Collection.IsOpen)
            {
                hud.Collection.RevealUnlocked(id);
                return;
            }

            string formName = _catalog.FormDisplayName(def.Form);
            string line3 = def.Form == _forms.DisplayForm ? formName
                : def.Form <= _growth.HighestForm ? $"切换到{formName}后可以播放"
                : $"进化到{formName}后可以播放";
            hud.PlaceSidePanels(_mover.ChooseSide());
            hud.Toast.Show(_icons.Get(def), "新表情！", def.DisplayName, line3, _config.lottery.revealToastSec);
        }

        void OnFormUnlockNotice(FormId form)
        {
            SaveNow();
            if (hud == null) return;
            string name = _catalog.FormDisplayName(form);
            hud.PlaceSidePanels(_mover.ChooseSide());
            hud.Toast.Show(null, $"{name}解锁了！", "可以在右键菜单切换", "", _config.lottery.revealToastSec);
        }

        // ---------- 菜单 ----------

        List<ContextMenuItem> BuildMenu()
        {
            var status = _lottery.GetStatus(_time.UtcNow); // 弹出那一刻的快照
            bool busy = _forms.IsTransitioning || _stateMachine.State == PetState.Evolving;

            var formItems = new List<ContextMenuItem>();
            foreach (FormId f in Enum.GetValues(typeof(FormId)))
            {
                string name = _catalog.FormDisplayName(f);
                if (_forms.CanSelect(f))
                    formItems.Add(ContextMenuItem.Command(CmdSwitchFormBase + (int)f, name, f == _forms.DisplayForm, !busy));
                else
                    formItems.Add(ContextMenuItem.Command(CmdSwitchFormBase + (int)f,
                        $"{name}（成长值 {UiTextFormat.Thousands(_growth.ThresholdOf(f))} 解锁）", false, false));
            }

            bool collectionOpen = hud != null && hud.Collection.IsOpen;
            var items = new List<ContextMenuItem>
            {
                ContextMenuItem.Label($"点击量：{UiTextFormat.Thousands(_wallet.Balance)}"),
                ContextMenuItem.Label(BuildGrowthLine()),
                ContextMenuItem.Label($"抽奖：{UiTextFormat.MenuLine(status)}"),
                ContextMenuItem.Separator(),
                ContextMenuItem.Command(CmdToggleCollection, "图鉴", collectionOpen, hud != null),
                ContextMenuItem.SubMenu("切换形态", formItems),
                ContextMenuItem.Command(CmdToggleClicks, "显示点击量", _save.hudShowClicks),
                ContextMenuItem.Command(CmdToggleGrowth, "显示成长进度条", _save.hudShowGrowth),
                ContextMenuItem.Command(CmdTogglePause, _filter.Paused ? "继续计数" : "暂停计数", _filter.Paused),
                ContextMenuItem.Separator(),
            };

            if (DebugAvailable)
            {
                var debugItems = new List<ContextMenuItem>
                {
                    ContextMenuItem.Command(CmdDebugClicks2000, "点击量 +2000"),
                    ContextMenuItem.Command(CmdDebugClearCooldown, "清除抽奖冷却"),
                    ContextMenuItem.Command(CmdDebugDrawFree, "立即抽一次（无视条件、不扣点击量）", false, _lottery.PoolCount > 0),
                    ContextMenuItem.Separator(),
                    ContextMenuItem.Command(CmdDebugResetCollection, "重置图鉴为默认解锁"),
                    ContextMenuItem.Command(CmdDebugUnlockAll, "解锁全部表情"),
                    ContextMenuItem.Command(CmdDebugPlayAll, "播放全部表情（依次）"),
                    ContextMenuItem.Separator(),
                };
                foreach (FormId f in Enum.GetValues(typeof(FormId)))
                    debugItems.Add(ContextMenuItem.Command(CmdDebugSetFormBase + (int)f,
                        $"设为{_catalog.FormDisplayName(f)}（成长值 {UiTextFormat.Thousands(_growth.ThresholdOf(f))}）", false, !busy));
                debugItems.Add(ContextMenuItem.Command(CmdDebugGrowth1000, "成长 +1000"));

                items.Add(ContextMenuItem.SubMenu("调试", debugItems));
                items.Add(ContextMenuItem.Separator());
            }

            items.Add(ContextMenuItem.Command(CmdExit, "退出"));
            return items;
        }

        string BuildGrowthLine()
        {
            string growth = UiTextFormat.Thousands(_growth.Growth);
            var highest = _growth.HighestForm;
            if (!highest.HasNext()) return $"成长值：{growth}（已满级）";
            var next = highest.Next();
            return $"成长值：{growth} / {UiTextFormat.Thousands(_growth.ThresholdOf(next))}（→ {_catalog.FormDisplayName(next)}）";
        }

        /// <summary>
        /// 调试功能是否可用：配置 debug.enabled，且打包时没有用「Naiwa/打包选项」关掉（关掉后会定义 NAIWA_NO_DEBUG，
        /// 改 json 也打不开）。
        /// </summary>
        bool DebugAvailable
        {
            get
            {
#if NAIWA_NO_DEBUG
                return false;
#else
                return _config.debug.enabled;
#endif
            }
        }

        static bool IsDebugCommand(int id) => id >= CmdDebugClicks2000 && id < CmdExit;

        void OnMenuCommand(int id)
        {
            if (IsDebugCommand(id) && !DebugAvailable) return;

            if (id >= CmdSwitchFormBase && id < CmdSwitchFormBase + 3)
            {
                _forms.RequestSwitch((FormId)(id - CmdSwitchFormBase));
                return;
            }
            if (id >= CmdDebugSetFormBase && id < CmdDebugSetFormBase + 3)
            {
                DebugSetForm((FormId)(id - CmdDebugSetFormBase));
                return;
            }

            switch (id)
            {
                case CmdTogglePause:
                    _filter.Paused = !_filter.Paused;
                    SaveNow();
                    break;
                case CmdToggleClicks:
                    _save.hudShowClicks = !_save.hudShowClicks;
                    if (hud != null) hud.SetToggles(_save.hudShowClicks, _save.hudShowGrowth);
                    SaveNow();
                    break;
                case CmdToggleGrowth:
                    _save.hudShowGrowth = !_save.hudShowGrowth;
                    if (hud != null) hud.SetToggles(_save.hudShowClicks, _save.hudShowGrowth);
                    SaveNow();
                    break;
                case CmdToggleCollection:
                    if (hud == null) break;
                    if (hud.Collection.IsOpen) hud.Collection.Close();
                    else
                    {
                        hud.PlaceSidePanels(_mover.ChooseSide());
                        hud.Toast.Hide();
                        hud.Collection.Open();
                        _lotteryDirty = true;
                    }
                    break;
                case CmdDebugClicks2000:
                    _wallet.Earn(2000);
                    SaveNow();
                    break;
                case CmdDebugClearCooldown:
                    _lottery.ClearCooldown();
                    _lotteryDirty = true;
                    SaveNow();
                    break;
                case CmdDebugDrawFree:
                {
                    var r = _lottery.DebugDrawFree();
                    if (r.Ok)
                    {
                        var def = _catalog.Get(r.EmoteId);
                        if (def != null && def.Form == _forms.DisplayForm) _library.GetOrLoadEmote(def);
                        Reveal(r.EmoteId);
                    }
                    break;
                }
                case CmdDebugResetCollection:
                    _unlocks.ResetToDefaults();
                    if (hud != null) hud.Collection.Refresh();
                    SaveNow();
                    break;
                case CmdDebugUnlockAll:
                    _unlocks.UnlockAll();
                    if (hud != null) hud.Collection.Refresh();
                    SaveNow();
                    break;
                case CmdDebugGrowth1000:
                    _growth.Add(1000);
                    SaveNow();
                    break;
                case CmdDebugPlayAll:
                    _stateMachine.DebugPlayAllEmotes();
                    break;
                case CmdExit:
                    Quit();
                    break;
            }
        }

        void DebugSetForm(FormId form)
        {
            if (!_forms.DebugSetForm(form)) return;
            RefreshGrowthBar(true);
            SaveNow();
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
            if (_saveService == null || _isDuplicateInstance || _growth == null) return;
            _save.growth = _growth.Growth;
            _save.form = (int)_forms.DisplayForm;
            _save.highestForm = (int)_growth.HighestForm;
            _save.clicks = _wallet.Balance;
            _save.clicksLifetimeEarned = _wallet.LifetimeEarned;
            _save.clicksLifetimeSpent = _wallet.LifetimeSpent;
            _save.unlockedEmotes = _unlocks.ToSaveList();
            _save.lotteryNextAvailableUnixMs = _lottery.NextAvailableUnixMs;
            _save.lotteryDrawCount = _lottery.DrawCount;
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

        ContentConfigResult LoadContent()
        {
            string path = Path.Combine(Application.streamingAssetsPath, _config.content.contentJsonPath);
            ContentConfigResult result;
            if (File.Exists(path))
            {
                string text;
                try { text = File.ReadAllText(path); }
                catch (Exception e) { text = null; Debug.LogWarning($"[Naiwa] 读取 {path} 失败：{e.Message}"); }
                result = ContentConfig.Parse(text);
            }
            else
            {
                Debug.LogWarning($"[Naiwa] 找不到 {path}，回退读取 Resources/{_config.content.manifestResourcePath}.json（所有表情视为 Default）");
                var asset = Resources.Load<TextAsset>(_config.content.manifestResourcePath);
                result = ContentConfig.Parse(asset != null ? asset.text : null);
            }

            foreach (var w in result.Warnings) Debug.LogWarning("[Naiwa] " + w);
            foreach (var err in result.Errors) Debug.LogError("[Naiwa] " + err);
            return result;
        }

        ClipIndex LoadClipIndex()
        {
            var asset = Resources.Load<TextAsset>(_config.content.clipIndexResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[Naiwa] 找不到序列帧索引 Resources/{_config.content.clipIndexResourcePath}.json，图鉴只能显示 icon 文件");
                return null;
            }
            try
            {
                return JsonUtility.FromJson<ClipIndex>(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Naiwa] 序列帧索引解析失败：{e.Message}");
                return null;
            }
        }

        void ApplyCameraSettings()
        {
            if (targetCamera == null) return;
            var p = targetCamera.transform.position;
            targetCamera.transform.position = new Vector3(0f, _config.window.CameraY, p.z < 0f ? p.z : -10f);
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = _config.window.OrthographicSize;
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            targetCamera.allowHDR = false;
            targetCamera.allowMSAA = false;
        }
    }
}
