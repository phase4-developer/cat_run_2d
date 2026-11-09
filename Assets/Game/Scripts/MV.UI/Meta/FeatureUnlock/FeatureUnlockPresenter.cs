using System.Collections.Generic;
using MV.Data;

namespace MV.UI
{
    /// <summary>
    /// 신기능 해금 안내 화면 코드(05 §5.11, 1차 정적 팝업 — 열림·빛 애니메이션은 MV-UI-FX2).
    /// 열 때 받은 <c>05_contentOpen</c> key 목록을 차례로 보여 주고, 「확인」마다 <see cref="IMetaUiPort.MarkUnlockSeen"/> 1번 → 다음, 끝이면 닫힘.
    /// </summary>
    public sealed class FeatureUnlockPresenter : MetaPanelPresenter
    {
        public const string OkId = "ok";

        readonly List<string> _keys = new List<string>();
        IReadOnlyList<string> _given;
        int _index;

        public string CurrentKey => _index < _keys.Count ? _keys[_index] : null;
        public int Remaining => _keys.Count - _index;

        /// <summary>보여 줄 key. null이면 열 때 <see cref="IMetaUiPort.PendingUnlockShows"/>.</summary>
        public void SetKeys(IReadOnlyList<string> keys) => _given = keys;

        protected override void OnOpen()
        {
            _keys.Clear();
            _keys.AddRange(_given ?? Port.PendingUnlockShows());
            _index = 0;
            if (CurrentKey != null) Play(MetaSound.Unlock); // 소리 lvup(L2613)
        }

        protected override void Build(MetaPanelModel m, MvTables tables)
        {
            m.Title = "NEW FEATURE"; // ui.unlock.tag
            string key = CurrentKey;
            var row = key != null ? tables.ContentOpen.ByKey(key) : null;
            if (row != null)
                m.Add(new MetaRow(row.key, MetaRowKind.Row, NameOf(row.id, row.key)) { Icon = row.icon, Desc = DescOf(row.id) });
            m.Confirm = new MetaAction(OkId, "확인", true); // ui.common.ok
        }

        protected override bool OnAction(string actionId)
        {
            if (actionId != OkId) return false;
            string key = CurrentKey;
            if (key != null)
            {
                var r = Port.MarkUnlockSeen(key); // 「본 해금」 저장. 로비 버튼 sbHint 강조는 MV-UI-FX2.
                if (!r.Ok) Say(r.Code);
                _index++;
            }
            if (CurrentKey == null) RequestClose();
            else Play(MetaSound.Unlock);
            return true;
        }
    }
}
