using UnityEngine;

namespace MV.Data
{
    /// <summary>
    /// Inspector에 보일 한글 이름. 저장되는 필드 이름은 그대로라 에셋 값에는 영향이 없다. 그리는 쪽은 MV.Editor `MvLabelDrawer`.
    /// </summary>
    public sealed class MvLabelAttribute : PropertyAttribute
    {
        public readonly string Text;

        public MvLabelAttribute(string text) => Text = text;
    }
}
