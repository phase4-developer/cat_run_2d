using System;
using System.Collections.Generic;

namespace MV.UI
{
    /// <summary>본문 칸 종류(05 §5.16). 1차 더미 겉은 모양을 크게 나누지 않고 글자만 다르게 그린다.</summary>
    public enum MetaRowKind
    {
        /// <summary><c>MetaRowCell</c>(.row2): 아이콘·제목·값·설명·행동 버튼.</summary>
        Row,
        /// <summary><c>MetaHeaderCell</c>(.gp-h): 묶음 제목.</summary>
        Header,
        /// <summary><c>MetaGridCell</c>(.gpc): 격자 칸(카드 등).</summary>
        Grid,
        /// <summary>안내 글(.sub).</summary>
        Text,
        /// <summary>지도 구역 머리·Area 배너.</summary>
        Banner,
    }

    /// <summary>행동 버튼 1개. <see cref="Id"/>는 패널 안에서 유일하다.</summary>
    public sealed class MetaAction
    {
        public string Id;
        public string Text;
        public bool Enabled;

        public MetaAction(string id, string text, bool enabled) { Id = id; Text = text; Enabled = enabled; }
    }

    /// <summary>본문 한 칸. 표 행 1개가 보통 1칸이 된다(행 추가 → 칸 추가).</summary>
    public sealed class MetaRow
    {
        public string Id;
        public MetaRowKind Kind;
        /// <summary>아이콘 스프라이트 키(표 icon 열). 1차 더미 겉은 그리지 않는다.</summary>
        public string Icon;
        public string Title;
        public string Value;
        public string Desc;
        /// <summary>현재 위치·사용 중 같은 강조.</summary>
        public bool Highlight;
        public bool Locked;
        public readonly List<MetaAction> Actions = new List<MetaAction>();

        public MetaRow(string id, MetaRowKind kind, string title) { Id = id; Kind = kind; Title = title; }

        public MetaRow Act(string id, string text, bool enabled)
        {
            Actions.Add(new MetaAction(id, text, enabled));
            return this;
        }
    }

    /// <summary>탭 1칸(야옹신 탭 모양, 05 §5.7·§5.16).</summary>
    public sealed class MetaTab
    {
        public string Id;
        public string Text;
        public string Sub;
        public bool Selected;
        public bool Locked;
    }

    /// <summary>
    /// 메타 공통 패널 틀(<c>MetaFrame</c>, 05 §5.16)이 그리는 값 전부. 화면 코드(Presenter)가 만들고 겉(<see cref="IMetaPanelView"/>)은 이것만 그린다.
    /// </summary>
    public sealed class MetaPanelModel
    {
        public string Title = "";
        public string Sub = "";
        /// <summary>재화 줄(그 화면이 쓰는 재화).</summary>
        public string Currencies = "";
        public readonly List<MetaTab> Tabs = new List<MetaTab>();
        public readonly List<MetaRow> Rows = new List<MetaRow>();
        /// <summary>아래 큰 버튼(<c>GoldButton</c>). 없으면 null.</summary>
        public MetaAction Confirm;
        /// <summary>본문이 비었을 때 글(<c>ui.meta.empty</c>).</summary>
        public string Empty = "";
        /// <summary>본문을 아래에서 위로 쌓는다(지도, 05 §5.10 column-reverse). <see cref="Rows"/>는 아래부터 순서.</summary>
        public bool BottomUp;
        /// <summary>열 때 화면 가운데로 올 칸(지도 현재 Area).</summary>
        public string FocusRowId;

        public void Clear()
        {
            Title = Sub = Currencies = Empty = "";
            Tabs.Clear();
            Rows.Clear();
            Confirm = null;
            BottomUp = false;
            FocusRowId = null;
        }

        public MetaRow Add(MetaRow row)
        {
            Rows.Add(row);
            return row;
        }

        public MetaRow FindRow(string id)
        {
            foreach (var r in Rows) if (r.Id == id) return r;
            return null;
        }

        /// <summary>본문 행동 또는 아래 큰 버튼. 없으면 null.</summary>
        public MetaAction FindAction(string id)
        {
            if (id == null) return null;
            if (Confirm != null && Confirm.Id == id) return Confirm;
            foreach (var r in Rows)
                foreach (var a in r.Actions)
                    if (a.Id == id) return a;
            return null;
        }

        public IEnumerable<MetaAction> AllActions()
        {
            foreach (var r in Rows)
                foreach (var a in r.Actions)
                    yield return a;
            if (Confirm != null) yield return Confirm;
        }

        /// <summary>표 행 칸 수(종류 지정 시 그 종류만).</summary>
        public int Count(MetaRowKind? kind = null)
        {
            if (kind == null) return Rows.Count;
            int n = 0;
            foreach (var r in Rows) if (r.Kind == kind.Value) n++;
            return n;
        }
    }

    /// <summary>메타 패널 겉(D-92). 컨트롤러·화면 코드는 이 인터페이스로만 겉을 만진다. 기본 겉은 <see cref="MetaPanelView"/>.</summary>
    public interface IMetaPanelView
    {
        /// <summary>닫기(✕).</summary>
        event Action ClosePressed;
        /// <summary>막을 눌렀다(닫힘 여부는 화면 코드가 정한다).</summary>
        event Action DimPressed;
        /// <summary>본문 행동 버튼 또는 아래 큰 버튼(<see cref="MetaAction.Id"/>).</summary>
        event Action<string> ActionPressed;
        event Action<string> TabPressed;
        void Render(MetaPanelModel model);
    }
}
