using System.Collections.Generic;
using Naiwa.Content;
using Naiwa.Growth;

namespace Naiwa.UI
{
    public enum SlotState
    {
        /// <summary>已解锁且页签 = 当前显示形态：悬停出现播放三角，点击播放。</summary>
        Playable,
        /// <summary>已解锁但页签 ≠ 当前显示形态：只看不能播。</summary>
        ViewOnly,
        /// <summary>未解锁：黑色剪影 + 锁，名字为 ？？？。</summary>
        Locked,
    }

    public struct SlotModel
    {
        public string Id;
        public string Name;
        public SlotState State;
        public EmoteDef Def;
    }

    public sealed class CollectionPage
    {
        public FormId Form;
        public string TabTitle;
        public bool IsCurrent;
        public readonly List<SlotModel> Slots = new List<SlotModel>();
    }

    /// <summary>图鉴视图模型（v1.0 §8.3），纯 C#。</summary>
    public static class CollectionViewModel
    {
        public const string LockedName = "？？？";
        public const string CurrentSuffix = "·当前";

        public static CollectionPage BuildPage(EmoteCatalog catalog, IUnlockService unlocks, FormId displayForm, FormId pageForm)
        {
            var page = new CollectionPage
            {
                Form = pageForm,
                IsCurrent = pageForm == displayForm,
            };
            page.TabTitle = TabTitle(catalog, pageForm, displayForm);

            foreach (var def in catalog.ForForm(pageForm))
            {
                bool unlocked = unlocks.IsUnlocked(def.Id);
                var state = !unlocked ? SlotState.Locked : page.IsCurrent ? SlotState.Playable : SlotState.ViewOnly;
                page.Slots.Add(new SlotModel
                {
                    Id = def.Id,
                    Def = def,
                    State = state,
                    Name = unlocked ? def.DisplayName : LockedName,
                });
            }
            return page;
        }

        public static string TabTitle(EmoteCatalog catalog, FormId form, FormId displayForm) =>
            catalog.FormDisplayName(form) + (form == displayForm ? CurrentSuffix : string.Empty);

        /// <summary>(已收集, 总数)。总数 = 所有已注册表情；配置里已删除的 id 不计入。</summary>
        public static (int collected, int total) Count(EmoteCatalog catalog, IUnlockService unlocks)
        {
            int c = 0;
            foreach (var e in catalog.All) if (unlocks.IsUnlocked(e.Id)) c++;
            return (c, catalog.Count);
        }
    }
}
