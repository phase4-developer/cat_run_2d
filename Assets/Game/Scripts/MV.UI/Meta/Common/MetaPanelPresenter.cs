using System;
using System.Collections.Generic;
using System.Globalization;
using MV.Data;

namespace MV.UI
{
    /// <summary>패널이 내는 소리 종류. 컨트롤러가 <c>MvUiSound</c>로 바꿔 낸다(화면 코드는 소리 모듈을 모른다).</summary>
    public enum MetaSound { None, Click, AreaBuy, PerkBuy, Rebirth, Unlock }

    /// <summary>
    /// 메타 패널 화면 코드 공통(D-92 ③⑤). Framework 화면 타입과 떨어진 순수 클래스다.
    /// 화면마다 <see cref="Build"/>로 표에서 <see cref="MetaPanelModel"/>을 만들고, <see cref="OnAction"/>으로 메타 명령을 부른다.
    /// 규칙: 메타가 없으면(<see cref="IMetaUiPort.Ready"/> false) 모든 버튼을 끄고 누름도 무시한다.
    /// </summary>
    public abstract class MetaPanelPresenter
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        IMetaUiPort _port;

        public IMetaUiPort Port => _port ?? MvMetaUiPort.Instance;
        public IMetaPanelView View { get; private set; }
        public MetaPanelModel Model { get; } = new MetaPanelModel();

        /// <summary>패널을 닫아 달라(컨트롤러가 ClosePopup).</summary>
        public event Action CloseRequested;
        /// <summary>토스트 문구(층 800, MV-UI-HUD 토스트로 연결).</summary>
        public event Action<string> Toast;
        public event Action<MetaSound> Sound;

        /// <summary>막을 누르면 닫히는지(야옹신만 true, 05 §5.7·§5.8).</summary>
        public virtual bool DimCloses => false;

        /// <summary>null이면 <see cref="MvMetaUiPort.Instance"/>.</summary>
        public void SetPort(IMetaUiPort port) => _port = port;

        public void SetView(IMetaPanelView view)
        {
            View = view;
            Draw();
        }

        /// <summary>새로 열림. 화면별 열림 상태를 비우고 그린다.</summary>
        public void Open()
        {
            OnOpen();
            Draw();
        }

        protected virtual void OnOpen() { }

        public void Draw()
        {
            Model.Clear();
            var tables = Port.Tables;
            if (tables != null) Build(Model, tables);
            if (Model.Rows.Count == 0 && string.IsNullOrEmpty(Model.Empty)) Model.Empty = "지금은 할 일이 없어요"; // ui.meta.empty
            if (!Port.Ready)
                foreach (var a in Model.AllActions()) a.Enabled = false;
            View?.Render(Model);
        }

        protected abstract void Build(MetaPanelModel m, MvTables tables);

        /// <summary>행동 버튼·아래 큰 버튼. 메타가 없거나 꺼진 버튼이면 아무것도 하지 않고 false.</summary>
        public bool Press(string actionId)
        {
            if (!Port.Ready) return false;
            var a = Model.FindAction(actionId);
            if (a == null || !a.Enabled) return false;
            bool done = OnAction(actionId);
            Draw();
            return done;
        }

        /// <summary>true면 이번 누름이 일을 했다.</summary>
        protected abstract bool OnAction(string actionId);

        /// <summary>탭은 보기만 바꾸므로 메타가 없어도 동작한다.</summary>
        public void SelectTab(string tabId)
        {
            if (OnTab(tabId)) Draw();
        }

        protected virtual bool OnTab(string tabId) => false;

        public void PressClose() => CloseRequested?.Invoke();

        public void PressDim()
        {
            if (DimCloses) CloseRequested?.Invoke();
        }

        /// <summary>열려 있는 동안 매 프레임(실시간 초). 쿨타임처럼 시간으로 바뀌는 화면만 쓴다.</summary>
        public virtual void Tick(float unscaledDt) { }

        protected void RequestClose() => CloseRequested?.Invoke();
        protected void Say(string text) { if (!string.IsNullOrEmpty(text)) Toast?.Invoke(text); }
        protected void Play(MetaSound s) => Sound?.Invoke(s);

        // ───────── 표기 도우미 ─────────

        /// <summary>표 행 이름 <c>name:&lt;id&gt;</c>, 없으면 key.</summary>
        protected static string NameOf(int id, string fallback) => MvUiText.Name(id, fallback ?? id.ToString(Inv));

        protected static string DescOf(int id, string fallback = "") => MvUiText.Get("description", id.ToString(Inv), fallback);

        /// <summary>성장 표 값 표기(07 <c>valueFormat</c>·<c>valueBase</c>·<c>valueStep</c>, 05 §5.8).</summary>
        public static string GrowthValue(string format, double valueBase, double valueStep, int level)
        {
            double v = valueBase + valueStep * level;
            string n = Math.Abs(v - Math.Round(v)) < 1e-9 ? Math.Round(v).ToString("0", Inv) : v.ToString("0.##", Inv);
            return string.IsNullOrEmpty(format) ? n : format.Replace("{0}", n);
        }

        /// <summary>가격 표기 「재화 {0}」(정본 <c>&lt;sprite herb&gt; {0}</c>, 1차 더미는 재화 이름 글자).</summary>
        public static string PriceText(IReadOnlyList<ConsumeData> price, MvTables tables)
        {
            if (price == null || price.Count == 0) return "MAX";
            var c = price[0];
            string name = c.Type == ConsumeType.Wealth ? MvUiText.Name(c.Id, WealthName(c.Id)) : c.Type.ToString();
            return name + " " + MvUiFormat.Fmt(c.Value);
        }

        static string WealthName(int id)
        {
            switch (id)
            {
                case MV.Save.MvWealthIds.Gold: return "츄르";
                case MV.Save.MvWealthIds.Dia: return "캣닢";
                case MV.Save.MvWealthIds.Souls: return "생선";
                case MV.Save.MvWealthIds.RbToken: return "조각";
                default: return id.ToString(Inv);
            }
        }

        /// <summary>정본 <c>hms</c>: 시:분:초(1시간 미만이면 분:초).</summary>
        public static string Hms(long ms)
        {
            long s = Math.Max(0, (ms + 999) / 1000);
            long h = s / 3600, m = s / 60 % 60, sec = s % 60;
            return h > 0 ? h + ":" + m.ToString("00", Inv) + ":" + sec.ToString("00", Inv) : m + ":" + sec.ToString("00", Inv);
        }
    }
}
