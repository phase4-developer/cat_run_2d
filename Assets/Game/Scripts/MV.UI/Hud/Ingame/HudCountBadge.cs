using TMPro;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 얻은 카드 중복 배지(디자이너 <c>Mid/Card Pos/Holder_Card/Badge_Override</c>, 노션 「IngameScreen : 카드 중복획득 시 표기 및 연출」).
    /// 같은 카드를 2장 이상 얻으면 켜고 <c>Value Text (TMP)</c>에 「x{개수}」(언어팩 <c>ui.common.countX</c>)를 쓴다.
    /// 연출은 디자이너가 같은 오브젝트에 둔 Tween(켜질 때 재시작)이 맡는다. 개수가 늘면 껐다 켜서 다시 재생한다(노션: Climb 연출 뒤 중복으로 들어올 때 연출).
    /// </summary>
    public sealed class HudCountBadge : MonoBehaviour
    {
        /// <summary><c>Badge_Override/Value Text (TMP)</c>.</summary>
        [SerializeField] TMP_Text valueText;

        int _shown;

        public int Shown => _shown;
        public bool Visible => gameObject.activeSelf;
        public string Text => valueText ? valueText.text : null;
        public bool HasRefs => valueText;

        /// <summary>카드 개수. 2 미만이면 끈다. 2 이상이면 켜고, 보이던 개수보다 늘었으면 연출을 다시 재생한다.</summary>
        public void Show(int count)
        {
            if (count < 2)
            {
                _shown = 0;
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }
            if (valueText) valueText.text = string.Format(MvUiText.Get("ui", ItemSlot.CountKey, ItemSlot.CountFallback), count);
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            else if (count > _shown) { gameObject.SetActive(false); gameObject.SetActive(true); }
            _shown = count;
        }
    }
}
