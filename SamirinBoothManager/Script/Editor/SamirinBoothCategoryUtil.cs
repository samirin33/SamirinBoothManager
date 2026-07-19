using UnityEngine;
using UnityEngine.UIElements;

namespace samirin33.SamirinBoothManager.UI.Parts
{
    /// <summary>
    /// カテゴリラベルの表示名・背景色・フォント自動調整の共通処理。
    /// </summary>
    static class SamirinBoothCategoryUtil
    {
        static readonly string[] CategoryNames =
        {
            "アバター",
            "アバターギミック",
            "衣装・アクセサリー",
            "ワールド",
            "ワールドギミック",
            "3Dモデル",
            "その他"
        };

        const float HueStart = 0f;         // 赤
        const float HueEnd = 270f / 360f;  // 紫
        const float Saturation = 0.75f;
        const float Value = 0.90f;
        const float Alpha = 0.4f;
        const float FontMax = 12f;
        const float FontMin = 6f;

        public static string GetCategoryName(Category category)
        {
            var index = Mathf.Clamp((int)category, 0, CategoryNames.Length - 1);
            return CategoryNames[index];
        }

        public static Color GetCategoryColor(Category category)
        {
            var index = Mathf.Clamp((int)category, 0, CategoryNames.Length - 1);
            int max = Mathf.Max(1, CategoryNames.Length - 1);
            float t = Mathf.Clamp01(index / (float)max);
            float hue = Mathf.Lerp(HueStart, HueEnd, t);
            var color = Color.HSVToRGB(hue, Saturation, Value);
            color.a = Alpha;
            return color;
        }

        /// <summary>
        /// ラベルの初期スタイル（中央揃え・白文字・折り返しなし）を設定する。
        /// </summary>
        public static void SetupLabel(Label label)
        {
            if (label == null)
                return;

            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.color = Color.white;
            label.style.overflow = Overflow.Hidden;
            label.style.whiteSpace = WhiteSpace.NoWrap;
        }

        /// <summary>
        /// テキスト・背景色を設定し、フォントサイズを枠に収まるよう調整する。
        /// </summary>
        public static void BindLabel(Label label, Category category)
        {
            if (label == null)
                return;

            label.text = GetCategoryName(category);
            label.style.backgroundColor = GetCategoryColor(category);
            FitFontSize(label);
        }

        public static void FitFontSize(Label label)
        {
            if (label == null)
                return;

            var text = label.text;
            if (string.IsNullOrEmpty(text))
                return;

            float width = label.layout.width;
            float height = label.layout.height;
            if (width <= 1f || height <= 1f)
            {
                width = label.resolvedStyle.width;
                height = label.resolvedStyle.height;
            }

            if (float.IsNaN(width) || float.IsNaN(height) || width <= 1f || height <= 1f)
            {
                label.schedule.Execute(() => FitFontSize(label)).StartingIn(1);
                return;
            }

            // 余白を少し確保
            float availW = Mathf.Max(1f, width - 4f);
            float availH = Mathf.Max(1f, height - 2f);
            float fontSize = Mathf.Min(FontMax, availH);

            while (fontSize > FontMin)
            {
                label.style.fontSize = fontSize;
                var size = label.MeasureTextSize(
                    text,
                    availW,
                    VisualElement.MeasureMode.Undefined,
                    availH,
                    VisualElement.MeasureMode.Undefined);

                if (size.x <= availW && size.y <= availH)
                    break;

                fontSize -= 0.5f;
            }

            label.style.fontSize = Mathf.Max(FontMin, fontSize);
        }
    }
}
