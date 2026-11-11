using System;
using Cysharp.Threading.Tasks;
using jjevol;
using MV.Data;
using MV.Meta;
using MV.Save;
using MV.Sound;
using UnityEngine;

namespace MV.UI
{
    /// <summary>가짜 광고 팝업 데이터(05 §5.12). <see cref="Finish"/>는 한 번만 콜백을 부른다.</summary>
    public sealed class FakeAdPopupData : IPopupData
    {
        /// <summary><c>08_adPlacement.key</c>.</summary>
        public string Key;
        public string Title = "";
        /// <summary>true = 끝까지 봄(보상 지급 대상), false = 닫음(건너뜀).</summary>
        public Action<bool> OnFinished;

        public bool Finished { get; private set; }

        /// <summary>처음 한 번만 <see cref="OnFinished"/>를 부른다. 불렀으면 true.</summary>
        public bool Finish(bool watched)
        {
            if (Finished) return false;
            Finished = true;
            OnFinished?.Invoke(watched);
            return true;
        }
    }

    /// <summary>가짜 광고 겉(D-92). 기본 겉은 <see cref="FakeAdView"/>.</summary>
    public interface IFakeAdView
    {
        /// <summary>「광고 보기(가짜)」.</summary>
        event Action WatchPressed;
        /// <summary>닫기(건너뜀).</summary>
        event Action ClosePressed;
        void SetTexts(string title, string sub);
    }

    /// <summary>가짜 광고 화면 코드. 「광고 보기」 → 보상 콜백(true) 1번, 닫기 → false 1번.</summary>
    public sealed class FakeAdPresenter
    {
        public IFakeAdView View { get; private set; }
        public FakeAdPopupData Data { get; private set; }
        public bool Done => Data == null || Data.Finished;

        public void SetView(IFakeAdView view)
        {
            View = view;
            Draw();
        }

        public void Open(FakeAdPopupData data)
        {
            Data = data;
            Draw();
        }

        public void Draw()
        {
            if (View == null || Data == null) return;
            View.SetTexts(Data.Title, "프로토 시뮬레이션 · 광고 대신 이 화면"); // ui.ad.sub
        }

        /// <summary>「광고 보기(가짜)」. 이번에 보상 콜백을 불렀으면 true.</summary>
        public bool PressWatch() => Data != null && Data.Finish(true);

        /// <summary>닫기·뒤로·강제 닫힘. 이번에 건너뜀 콜백을 불렀으면 true.</summary>
        public bool PressClose() => Data != null && Data.Finish(false);
    }

    /// <summary>
    /// <c>UIPopup_FakeAd</c> 컨트롤러(05 §5.12, 층 700). 광고 SDK가 없을 때만 쓴다.
    /// 어떻게 닫히든 콜백은 한 번 부른다(보기 전 닫힘 = 건너뜀).
    /// </summary>
    public class FakeAdPopup : UIPopup
    {
        [SerializeField] MonoBehaviour view;

        readonly FakeAdPresenter _presenter = new FakeAdPresenter();
        IFakeAdView _bound;

        public FakeAdPresenter Presenter
        {
            get
            {
                if (_bound == null) SetView(view as IFakeAdView ?? GetComponent<IFakeAdView>());
                return _presenter;
            }
        }

        public void SetView(IFakeAdView next)
        {
            if (_bound != null) { _bound.WatchPressed -= PressWatch; _bound.ClosePressed -= PressClose; }
            _bound = next;
            if (_bound != null) { _bound.WatchPressed += PressWatch; _bound.ClosePressed += PressClose; }
            _presenter.SetView(next);
        }

        protected override void Awake()
        {
            base.Awake();
            hudSettings.displayMode = UIHudDisplayMode.KeepPrevious;
            MvUi.EnsureLayer(gameObject, MvUiLayer.Ad, true);
        }

        protected override void _DrawContents()
        {
            var data = popupData as FakeAdPopupData;
            if (data != _presenter.Data) Presenter.Open(data);
            else Presenter.Draw();
        }

        public void PressWatch()
        {
            MvSound.PlayUi(MvUiSound.ButtonClick);
            if (Presenter.PressWatch()) ClosePopup(true);
        }

        public void PressClose()
        {
            MvSound.PlayUi(MvUiSound.ButtonClick);
            Presenter.PressClose();
            ClosePopup(false);
        }

        public override void HideUI()
        {
            base.HideUI();
            _presenter.PressClose(); // 뒤로 가기·강제 닫힘도 건너뜀으로 끝낸다
        }

        protected override void OnDestroy()
        {
            _presenter.PressClose();
            if (_bound != null) { _bound.WatchPressed -= PressWatch; _bound.ClosePressed -= PressClose; }
            base.OnDestroy();
        }
    }

    /// <summary>
    /// 보상 광고 1번(05 §5.12 흐름). 광고 공급자가 없으면(<see cref="MvFakeAdService"/>) 가짜 광고 팝업을 먼저 띄우고,
    /// 「광고 보기」를 누르면 <see cref="MvMeta.WatchAd"/>(한도 확인·사용 수)로 넘긴다.
    /// Framework 대체: platform.ads 고정본이 연결되면(G6 <c>AdManager.ShowRewardAd</c>를 감싼 IMvAdService) 팝업 없이 바로 <see cref="MvMeta.WatchAd"/>로 간다.
    /// </summary>
    public static class MvFakeAd
    {
        /// <summary><paramref name="done"/>(끝까지 봤는지)를 한 번 부른다. 메타가 없으면 false.</summary>
        public static void Watch(string key, Action<bool> done)
        {
            var meta = MvMeta.Current;
            if (meta == null || !meta.IsReady) { done?.Invoke(false); return; }
            if (!(meta.Ads is MvFakeAdService) || meta.SkipsAd(key) || !UIManager.HasInstance) { meta.WatchAd(key, done); return; }
            var row = meta.Tables.AdPlacement.ByKey(key);
            if (row == null || !row.active || meta.AdsLeft(key) == 0) { done?.Invoke(false); return; }
            var data = new FakeAdPopupData
            {
                Key = key,
                Title = MvUiText.Name(row.id, TitleOf(row, meta.Tables)),
                OnFinished = watched =>
                {
                    if (watched) meta.WatchAd(key, done);
                    else done?.Invoke(false);
                },
            };
            OpenAsync(data).Forget();
        }

        static async UniTaskVoid OpenAsync(FakeAdPopupData data)
        {
            FakeAdPopup popup = null;
            try
            {
                popup = await MvUiNav.OpenFakeAdAsync(data);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            if (popup == null) data.Finish(false);
        }

        /// <summary>
        /// 문구 키 없을 때 문구(<c>name:&lt;08_adPlacement id&gt;</c> 자리). 결과 광고는 배수 적용 재화(골드·블루 마법석, MV-SPEC-02 01 §10)
        /// 이름을 <c>01_wealth</c>에서, 배수를 그 행 <c>mul</c>에서 읽는다.
        /// </summary>
        public static string TitleOf(AdPlacementData row, MvTables tables)
        {
            if (row == null) return "보상 광고";
            string gold = MvUiText.WealthName(tables, MvWealthIds.TypeOf(MvWealthIds.Gold));
            string dia = MvUiText.WealthName(tables, MvWealthIds.TypeOf(MvWealthIds.Dia));
            switch (row.key)
            {
                case MvMetaResultAd.Key: return "보상 광고 · " + gold + "·" + dia + " ×" + row.mul;
                case "dia": return "보상 광고 · " + dia;
                case "skip": return "보상 광고 · 부활 쿨타임 줄이기";
                default: return "보상 광고";
            }
        }
    }
}
