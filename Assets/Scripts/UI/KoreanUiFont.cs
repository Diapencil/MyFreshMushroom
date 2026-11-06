using System;
using UnityEngine;
using UnityEngine.UI;

namespace MyFreshMushroom
{
    // Each panel owns its runtime font; OS font files are not copied into the project.
    public sealed class KoreanUiFont : IDisposable
    {
        private Font font;

        public void Apply(Text text)
        {
            if (text == null) return;
            if (font == null)
                font = Font.CreateDynamicFontFromOSFont(new[] {
                    "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR"
                }, 24);
            if (font != null && font.HasCharacter('가')) text.font = font;
        }

        public void Dispose()
        {
            if (font != null) UnityEngine.Object.Destroy(font);
            font = null;
        }
    }
}
