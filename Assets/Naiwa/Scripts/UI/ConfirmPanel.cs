using System;
using UnityEngine;
using UnityEngine.UI;

namespace Naiwa.UI
{
    /// <summary>
    /// 确认面板（v2.0 重生确认）：白底圆角，标题 + 多行说明 + 「确认」「取消」两个按钮。
    /// 点「确认」回调 onConfirm 后关闭；点「取消」直接关闭。
    /// </summary>
    public sealed class ConfirmPanel : MonoBehaviour
    {
        public const int Width = 300;
        public const int Height = 164;
        const int Pad = 16;
        const int ButtonW = 104;
        const int ButtonH = 34;

        Text _title;
        Text _message;
        Action _onConfirm;

        public bool IsOpen => gameObject.activeSelf;

        public void Build(UiKit kit)
        {
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(Width, Height);

            var brown = UiKit.Hex("#6E4828", Color.black);
            var gold = UiKit.Hex("#E2C48F", brown);
            var bg = kit.Image("Bg", transform, kit.Rounded(14, 2, Color.white, gold), Color.white, raycast: true, sliced: true);
            UiKit.Stretch(bg.rectTransform);

            float top = Height / 2f - Pad;
            _title = kit.Text("Title", transform, "", 16, brown, bold: true);
            UiKit.Place(_title.rectTransform, 0, top - 11, Width - Pad * 2, 22);

            _message = kit.Text("Message", transform, "", 13, UiKit.Hex("#4A3220", brown));
            _message.horizontalOverflow = HorizontalWrapMode.Wrap;
            _message.lineSpacing = 1.15f;
            float msgTop = top - 26, msgBottom = -Height / 2f + Pad + ButtonH + 8;
            UiKit.Place(_message.rectTransform, 0, (msgTop + msgBottom) / 2f, Width - Pad * 2, msgTop - msgBottom);

            float by = -Height / 2f + Pad + ButtonH / 2f;
            Button(kit, "Confirm", "确认", -ButtonW / 2f - 8, by,
                UiKit.Hex("#F2B92B", Color.yellow), UiKit.Hex("#C98F12", brown), UiKit.Hex("#E89A1C", brown), Color.white,
                () =>
                {
                    var cb = _onConfirm;
                    Close();
                    cb?.Invoke();
                });
            Button(kit, "Cancel", "取消", ButtonW / 2f + 8, by,
                UiKit.Hex("#FFF4DA", Color.white), gold, UiKit.Hex("#F5E3B8", Color.white), brown,
                Close);

            gameObject.SetActive(false);
        }

        void Button(UiKit kit, string name, string label, float x, float y, Color fill, Color outline, Color hoverFill, Color textColor, Action clicked)
        {
            var normal = kit.Rounded(10, 1.5f, fill, outline);
            var hover = kit.Rounded(10, 1.5f, hoverFill, outline);
            var img = kit.Image(name, transform, normal, Color.white, raycast: true, sliced: true);
            UiKit.Place(img.rectTransform, x, y, ButtonW, ButtonH);
            var text = kit.Text("Label", img.transform, label, 15, textColor, bold: true);
            UiKit.Stretch(text.rectTransform);
            var click = img.gameObject.AddComponent<UiClickable>();
            click.Clicked = clicked;
            click.HoverChanged = h => img.sprite = h ? hover : normal;
        }

        public void Show(string title, string message, Action onConfirm)
        {
            _title.text = title;
            _message.text = message;
            _onConfirm = onConfirm;
            gameObject.SetActive(true);
        }

        public void Close()
        {
            _onConfirm = null;
            gameObject.SetActive(false);
        }
    }
}
