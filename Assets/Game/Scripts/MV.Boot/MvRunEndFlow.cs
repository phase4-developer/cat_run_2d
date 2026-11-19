using System;
using Cysharp.Threading.Tasks;
using MV.Meta;
using MV.UI;
using UnityEngine;

namespace MV.Boot
{
    /// <summary>
    /// 런 끝 흐름(MV-SPEC-02 01 §12-3 [원작], Q-24): ① 지도 → ② 결과 → ③ 받기(정산 1번) → 로비.
    /// 지도(<see cref="MapPopup"/>)를 닫으면 결과를 연다. 지도를 열지 못하면 결과로 바로 간다.
    /// </summary>
    sealed class MvRunEndFlow
    {
        ResultPopup _result;
        MapPopup _map;
        Func<bool> _active;

        /// <summary>결과 팝업을 열라고 한 횟수(지도 닫힘 → 결과, 시험이 읽는다).</summary>
        public int ResultOpens { get; private set; }
        /// <summary>정산 명령을 부른 횟수(받기 1번 = 1).</summary>
        public int SettleCount { get; private set; }
        public bool LastSettleOk { get; private set; }

        public bool MapOpen => IsActive && _map != null;
        public bool ResultOpen => IsActive && _result != null;
        bool IsActive => _active != null && _active();

        /// <summary>로비로 돌아가면 팝업 참조를 버린다.</summary>
        public void Clear()
        {
            _result = null;
            _map = null;
            _active = null;
        }

        /// <param name="active">결과 단계인지(아니면 늦게 온 콜백을 무시한다).</param>
        /// <param name="onCollected">받기가 끝나면(로비로).</param>
        public void Begin(ResultViewData data, Func<bool> active, Action<MvSettleResult> onCollected)
        {
            _active = active;
            var meta = MvMeta.Current;
            data.Settler = new CountingSettler(this, meta != null && meta.IsReady ? null : NoSaveSettler.Instance);
            data.OnCollected = r => { if (IsActive) onCollected(r); };
            OpenMapThenResultAsync(data).Forget(Debug.LogException);
        }

        async UniTask OpenMapThenResultAsync(ResultViewData data)
        {
            bool opened = false;
            void OpenResult()
            {
                if (opened || !IsActive) return;
                opened = true;
                ResultOpens++;
                _map = null;
                OpenResultAsync(data).Forget(Debug.LogException);
            }

            // 지도(LevelProgress)는 정산 전에 열리므로 이번 런 영혼 수를 넘겨 오름 연출을 보이고, 끝나면 스스로 닫힌다(MV-UI3-LVPROG).
            var mapData = new LevelProgressPopupData { OnClosed = OpenResult, RunEnd = true, RunSouls = data.Outcome?.Souls ?? 0 };
            _map = await MvUiNav.OpenMapAsync(mapData, playClick: false);
            if (_map == null)
            {
                Debug.LogWarning("[MvRunEndFlow] 지도를 열지 못했습니다. 결과로 바로 갑니다.");
                OpenResult();
            }
        }

        async UniTask OpenResultAsync(ResultViewData data)
        {
            _result = await MvUiNav.OpenResultAsync(data);
            if (_result == null) throw new InvalidOperationException("결과 팝업을 열지 못했습니다.");
        }

        /// <summary>런 끝 지도 닫기(시험용). 닫으면 결과가 열린다.</summary>
        public bool PressMapClose()
        {
            if (!MapOpen) return false;
            _map.PressClose();
            return true;
        }

        /// <summary>결과 「받기」(시험용). 팝업이 열려 있으면 정산 1번 → 로비.</summary>
        public bool PressCollect()
        {
            if (!ResultOpen) return false;
            _result.PressCollect();
            return true;
        }

        /// <summary>정산 1번을 세고 실제 정산기(기본 MvMeta.Current)에 넘긴다.</summary>
        sealed class CountingSettler : IResultSettler
        {
            readonly MvRunEndFlow _flow;
            readonly IResultSettler _inner;

            public CountingSettler(MvRunEndFlow flow, IResultSettler inner)
            {
                _flow = flow;
                _inner = inner ?? MvMetaResultSettler.Instance;
            }

            public MvSettleResult Settle(MvRunOutcome outcome, int mul)
            {
                _flow.SettleCount++;
                // 런 안 퀘스트 카운터(계단·벽)는 outcome에 실려 정산 저장에 함께 쓴다(따로 기록 요청을 보내지 않는다).
                var r = _inner.Settle(outcome, mul);
                _flow.LastSettleOk = r != null && r.Ok;
                return r;
            }
        }

        /// <summary>저장이 없을 때(복구 대기·데이터 없음): 정산하지 않고 실패 결과를 돌려준다(진행을 바꾸지 않는다).</summary>
        sealed class NoSaveSettler : IResultSettler
        {
            public static readonly NoSaveSettler Instance = new NoSaveSettler();
            public MvSettleResult Settle(MvRunOutcome outcome, int mul) => new MvSettleResult { Mul = mul };
        }
    }
}
