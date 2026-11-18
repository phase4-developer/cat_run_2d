using System;
using System.Collections.Generic;
using DG.Tweening;
using jjevol;
using MV.Data;
using MV.Run;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 스킬 롤링 섹션(<c>ClimbStair/Skill Section</c>, MV-UI3-HUD7, HUD_구조_설계안 3.3 SkillRollSection).
    /// 계단 중에는 Current 등급 「?」 카드, 룰렛이 시작되면 그 등급 카드 그림이 넘어가다(넘김 = 시뮬 Flips) 시뮬이 정한 카드에서 멈춘다.
    /// 결과는 시뮬이 이미 정했고(<see cref="RouletteState"/>), 이 섹션은 보이는 것만 한다. 확정 카드가 날기 시작하면 <see cref="FlyStarted"/>로 알린다.
    /// </summary>
    public sealed class SkillRollSection : UIBase
    {
        [Tooltip("롤링 카드(Card_Skill). 디자이너가 Skill Section 안에 둔 카드 1장")]
        [SerializeField] HudSkillCard card;
        [Tooltip("그림 한 장 넘김 초(1배속 기준, 배속만큼 빨라진다)")]
        [SerializeField] float rollStepSec = 0.06f;

        RouletteState _rl;
        int _focus = -1, _focusFlips;
        Tween _roll;
        readonly HashSet<int> _flown = new HashSet<int>();
        readonly List<string> _faces = new List<string>();

        /// <summary>확정 카드가 Card Pos로 날기 시작했다(룰렛 카드, 출발 카드).</summary>
        public event Action<RouletteCard, HudSkillCard> FlyStarted;

        public HudSkillCard Card => card;
        /// <summary>지금 롤링·확정 표시 중인 룰렛 카드 번호(없으면 −1).</summary>
        public int Focus => _focus;
        /// <summary>날기 시작한 카드가 하나라도 있다(디자이너 시퀀스: 이때 Top·Mid를 다시 켠다).</summary>
        public bool AnyFlown => _flown.Count > 0;

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _roll?.Kill();
        }

        public void ResetRun()
        {
            _rl = null;
            _focus = -1;
            _roll?.Kill();
            _flown.Clear();
        }

        /// <summary>계단 Current 등급 「?」.</summary>
        public void ShowQuestion(MvTables t, int tier)
        {
            if (card) card.ShowQuestion(t, tier);
        }

        public void Tick(RunState s, float speed)
        {
            var rl = s?.Roulette;
            if (rl != _rl)
            {
                _rl = rl;
                _focus = -1;
                _flown.Clear();
                _roll?.Kill();
            }
            if (rl == null || !card) return;
            var t = s.Tables;

            int f = -1;
            for (int i = 0; i < rl.Cards.Count; i++) if (!rl.Cards[i].Flying && !rl.Cards[i].Applied) { f = i; break; }
            if (f != _focus)
            {
                _focus = f;
                _roll?.Kill();
                if (f >= 0)
                {
                    var k = rl.Cards[f];
                    _focusFlips = k.Flips;
                    if (k.Chosen) card.ShowCard(t, k.Pick);
                    else card.BeginRoll(t, k.Tier, Faces(s, k.Tier));
                }
            }
            if (f >= 0)
            {
                var k = rl.Cards[f];
                if (!k.Chosen && k.Flips != _focusFlips) { _focusFlips = k.Flips; RollStep(s, k.Tier, speed); }
                else if (k.Chosen && card.CardId != (k.Pick?.id ?? 0)) { _roll?.Kill(); card.ShowCard(t, k.Pick); }
            }
            for (int i = 0; i < rl.Cards.Count; i++)
            {
                var k = rl.Cards[i];
                if (!k.Flying || _flown.Contains(i)) continue;
                _flown.Add(i);
                FlyStarted?.Invoke(k, card);
            }
        }

        /// <summary>롤링 후보 그림: 그 등급 카드 풀의 그림(UI 쪽 무작위, 시뮬 결과와 무관). 없으면 풀 전체.</summary>
        List<string> Faces(RunState s, int tier)
        {
            _faces.Clear();
            foreach (var c in s.CardPool) if (c.tier == tier && !string.IsNullOrEmpty(c.icon)) _faces.Add(c.icon);
            if (_faces.Count == 0) foreach (var c in s.CardPool) if (!string.IsNullOrEmpty(c.icon)) _faces.Add(c.icon);
            for (int i = _faces.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); (_faces[i], _faces[j]) = (_faces[j], _faces[i]); }
            return _faces;
        }

        void RollStep(RunState s, int tier, float speed)
        {
            if (_roll != null && _roll.IsActive()) _roll.Complete(true);
            var faces = Faces(s, tier);
            string next = faces.Count > 0 ? faces[0] : null;
            float sec = rollStepSec / Math.Max(1, speed);
            if (!HudRectMotion.Animate) { card.Advance(next); return; }
            float k = 0;
            _roll = DOTween.To(() => k, v => { k = v; card.SetStripOffset(v); }, 1f, sec).SetUpdate(true).SetTarget(this)
                .OnComplete(() => card.Advance(next));
        }
    }
}
