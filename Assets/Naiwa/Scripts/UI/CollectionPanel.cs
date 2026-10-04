using System;
using System.Collections.Generic;
using Naiwa.Content;
using Naiwa.Core;
using Naiwa.Growth;
using UnityEngine;
using UnityEngine.UI;

namespace Naiwa.UI
{
    /// <summary>
    /// 图鉴面板（v1.0 §8.2）：380×470 白底圆角；标题「图鉴 已收集 x / y」+ 关闭 ×；
    /// 三个页签（当前阶段加「·当前」，每次打开默认当前阶段）；每行 3 格，超过 2 行可滚动；底部两行抽奖提示。
    /// 点面板外面不关闭。
    /// </summary>
    public sealed class CollectionPanel : MonoBehaviour
    {
        const int Pad = 16;
        const int TitleH = 34;
        const int TabH = 32;
        const int BottomH = 46;

        UiKit _kit;
        CollectionConfig _cfg;
        EmoteCatalog _catalog;
        IUnlockService _unlocks;
        IconLibrary _icons;
        Func<FormId> _displayForm;

        Text _count;
        Text _bottom1, _bottom2;
        RectTransform _content;
        ScrollRect _scroll;
        readonly List<CollectionSlotView> _slots = new List<CollectionSlotView>();
        readonly Dictionary<FormId, (Image bg, Text label)> _tabs = new Dictionary<FormId, (Image, Text)>();
        Sprite _tabOn, _tabOff;
        Color _silhouetteColor;
        FormId _page;
        int _viewportH;

        public bool IsOpen => gameObject.activeSelf;
        public FormId Page => _page;

        public event Action<string> PlayRequested;
        public event Action CloseRequested;

        public void Build(UiKit kit, CollectionConfig cfg, int width, int height, EmoteCatalog catalog, IUnlockService unlocks,
            IconLibrary icons, Func<FormId> displayForm)
        {
            _kit = kit;
            _cfg = cfg;
            _catalog = catalog;
            _unlocks = unlocks;
            _icons = icons;
            _displayForm = displayForm;
            _silhouetteColor = UiKit.Hex(cfg.silhouetteColor, Color.black);

            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(width, height);

            var brown = UiKit.Hex("#6E4828", Color.black);
            var bg = kit.Image("Bg", transform, kit.Rounded(16, 2, Color.white, brown), Color.white, raycast: true, sliced: true);
            UiKit.Stretch(bg.rectTransform);

            float top = height / 2f - Pad;
            var title = kit.Text("Title", transform, "图鉴", 20, brown, TextAnchor.MiddleLeft, bold: true);
            PlaceLeft(title.rectTransform, -width / 2f + Pad + 4, top - TitleH / 2f, 80, TitleH);
            _count = kit.Text("Count", transform, "", 13, UiKit.Hex("#9A7B5C", brown), TextAnchor.MiddleLeft);
            PlaceLeft(_count.rectTransform, -width / 2f + Pad + 4 + 50, top - TitleH / 2f - 1, 160, TitleH);

            var closeBg = kit.Image("Close", transform, kit.Icons.Circle, UiKit.Hex("#E2C48F", brown), raycast: true);
            UiKit.Place(closeBg.rectTransform, width / 2f - Pad - 14, top - TitleH / 2f, 28, 28);
            var closeX = kit.Image("X", closeBg.transform, kit.Icons.Close, Color.white);
            UiKit.Place(closeX.rectTransform, 0, 0, 22, 22);
            var closeClick = closeBg.gameObject.AddComponent<UiClickable>();
            closeClick.Clicked = () => CloseRequested?.Invoke();
            closeClick.HoverChanged = h => closeBg.color = h ? brown : UiKit.Hex("#E2C48F", brown);

            // 页签
            _tabOn = kit.Rounded(10, 1.5f, UiKit.Hex("#F2B92B", Color.yellow), UiKit.Hex("#C98F12", brown));
            _tabOff = kit.Rounded(10, 1.5f, UiKit.Hex("#FFF4DA", Color.white), UiKit.Hex("#E2C48F", brown));
            float tabY = top - TitleH - 6 - TabH / 2f;
            float tabW = (width - Pad * 2 - 12) / 3f;
            int idx = 0;
            foreach (FormId f in Enum.GetValues(typeof(FormId)))
            {
                var tabBg = kit.Image("Tab_" + f, transform, _tabOff, Color.white, raycast: true, sliced: true);
                UiKit.Place(tabBg.rectTransform, -width / 2f + Pad + tabW / 2f + idx * (tabW + 6), tabY, tabW, TabH);
                var label = kit.Text("Label", tabBg.transform, f.DisplayName(), 14, brown, bold: true);
                UiKit.Stretch(label.rectTransform);
                var form = f;
                tabBg.gameObject.AddComponent<UiClickable>().Clicked = () => ShowPage(form);
                _tabs[f] = (tabBg, label);
                idx++;
            }

            // 格子区（ScrollRect + RectMask2D）
            float gridTop = tabY - TabH / 2f - 10;
            float gridBottom = -height / 2f + Pad + BottomH + 6;
            _viewportH = Mathf.RoundToInt(gridTop - gridBottom);
            var viewport = UiKit.Rect("Viewport", transform);
            UiKit.Place(viewport, 0, (gridTop + gridBottom) / 2f, width - Pad * 2, _viewportH);
            viewport.gameObject.AddComponent<RectMask2D>();
            // 透明的射线目标，让滚轮落在格子之间的空隙时也能滚动
            var hit = kit.Image("Hit", viewport, null, new Color(1, 1, 1, 0), raycast: true);
            UiKit.Stretch(hit.rectTransform);

            _content = UiKit.Rect("Content", viewport);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0, _viewportH);

            _scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;
            _scroll.inertia = false;

            // 底部抽奖提示
            var sep = kit.Image("Sep", transform, null, UiKit.Hex("#F0E3C6", Color.gray));
            UiKit.Place(sep.rectTransform, 0, -height / 2f + Pad + BottomH + 2, width - Pad * 2, 1.5f);
            _bottom1 = kit.Text("Hint1", transform, "", 14, brown, bold: true);
            UiKit.Place(_bottom1.rectTransform, 0, -height / 2f + Pad + BottomH - 14, width - Pad * 2, 20);
            _bottom2 = kit.Text("Hint2", transform, "", 11, UiKit.Hex("#9A7B5C", brown));
            UiKit.Place(_bottom2.rectTransform, 0, -height / 2f + Pad + 10, width - Pad * 2, 18);

            gameObject.SetActive(false);
        }

        static void PlaceLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            ShowPage(_displayForm());
        }

        public void Close() => gameObject.SetActive(false);

        public void ShowPage(FormId form)
        {
            _page = form;
            _content.anchoredPosition = Vector2.zero;
            Refresh();
        }

        /// <summary>按当前解锁状态和显示形态重算（形态变化、解锁时调用）；保持当前页签。</summary>
        public void Refresh()
        {
            if (!IsOpen) return;
            var display = _displayForm();
            foreach (var kv in _tabs)
            {
                bool selected = kv.Key == _page;
                kv.Value.bg.sprite = selected ? _tabOn : _tabOff;
                kv.Value.label.text = CollectionViewModel.TabTitle(_catalog, kv.Key, display);
                kv.Value.label.color = selected ? Color.white : UiKit.Hex("#6E4828", Color.black);
            }

            var (collected, total) = CollectionViewModel.Count(_catalog, _unlocks);
            _count.text = $"已收集 {collected} / {total}";

            var page = CollectionViewModel.BuildPage(_catalog, _unlocks, display, _page);
            EnsureSlots(page.Slots.Count);

            int cols = Mathf.Max(1, _cfg.columns);
            int slotPx = _cfg.slotPx, gap = _cfg.slotSpacingPx;
            float cellH = slotPx + 22;
            float rowW = cols * slotPx + (cols - 1) * gap;
            for (int i = 0; i < _slots.Count; i++)
            {
                var view = _slots[i];
                bool used = i < page.Slots.Count;
                view.gameObject.SetActive(used);
                if (!used) continue;
                int r = i / cols, c = i % cols;
                var rt = (RectTransform)view.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(-rowW / 2f + slotPx / 2f + c * (slotPx + gap), -r * (cellH + gap));
                var model = page.Slots[i];
                view.Bind(model, _icons.Get(model.Def));
            }

            int rows = (page.Slots.Count + cols - 1) / cols;
            float contentH = Mathf.Max(_viewportH, rows * cellH + Mathf.Max(0, rows - 1) * gap);
            _content.sizeDelta = new Vector2(0, contentH);
        }

        public void SetHint(string line1, string line2)
        {
            if (_bottom1.text != line1) _bottom1.text = line1;
            if (_bottom2.text != line2) _bottom2.text = line2;
        }

        /// <summary>打开期间抽中：切到该表情所在阶段页，对应格子播放揭晓动画。</summary>
        public void RevealUnlocked(string id)
        {
            var def = _catalog.Get(id);
            if (def == null || !IsOpen) return;
            if (_page != def.Form) ShowPage(def.Form);
            else Refresh();
            foreach (var s in _slots)
                if (s.gameObject.activeSelf && s.Id == id) s.PlayUnlockReveal();
        }

        void EnsureSlots(int count)
        {
            while (_slots.Count < count)
            {
                var rt = UiKit.Rect("Slot" + _slots.Count, _content);
                var view = rt.gameObject.AddComponent<CollectionSlotView>();
                view.Build(_kit, _cfg.slotPx, _silhouetteColor);
                view.PlayRequested += id => PlayRequested?.Invoke(id);
                _slots.Add(view);
            }
        }
    }
}
