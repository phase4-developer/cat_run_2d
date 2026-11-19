using System;
using System.Collections.Generic;
using MV.Fx;
using MV.UI;
using MV.View;
using UnityEngine;

namespace MV.Boot
{
    /// <summary>
    /// 비행 층(<see cref="MvFlyLayer"/>, MV.Fx)과 HUD 비행 입구(<see cref="MvHudFly"/>, MV.UI)를 잇는다(MV-UI3-HUD4).
    /// 그림·도착 자리는 대상별 HUD 섹션이 맡고, 맡은 섹션이 없는 대상은 그림 없이 도착 시점만 지킨다(기존과 같다).
    /// </summary>
    sealed class MvHudFlyBridge : IMvFlyView, IMvHudAnchors, IDisposable
    {
        readonly MvFxPlayer _player;

        MvHudFlyBridge(MvFxPlayer player) { _player = player; }

        /// <summary>비행 층에 HUD를 꽂는다. 대상 이름이 어긋나면 오류로 드러내고 꽂지 않는다.</summary>
        public static MvHudFlyBridge Attach(MvFxPlayer player)
        {
            if (player?.Flies == null) return null;
            if (!SameTargets())
            {
                Debug.LogError("[MvHudFlyBridge] MV.Fx.FlyTarget과 MV.UI.HudFlyTarget 이름·순서가 다릅니다. HUD 비행을 연결하지 않습니다.");
                return null;
            }
            var b = new MvHudFlyBridge(player);
            player.Flies.View = b;
            player.Flies.Anchors = b;
            player.FlyArrived += b.OnArrived;
            MvHudFly.InFlight = b.CountOf;
            GateRewardDisplay.Released += OnGateReleased;
            CharacterPresenter.LookTier = HudWeaponTier;   // 캐릭터 무기 외형 = HUD 맨 위 무기(머지 연출 시점)
            return b;
        }

        static bool SameTargets()
        {
            var a = Enum.GetNames(typeof(FlyTarget));
            var h = Enum.GetNames(typeof(HudFlyTarget));
            if (a.Length != h.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != h[i]) return false;
            return true;
        }

        static readonly Func<int> HudWeaponTier = () => WeaponSection.ShownTier;

        static readonly List<Rect> s_rects = new List<Rect>(16);

        /// <summary>문 위 보상 카드 통과(MV.View) → HUD 카드 출발(MV.UI). 종류별 받을 곳: 무기 = 손패 스택.</summary>
        static void OnGateReleased(GateRewardKind kind, IReadOnlyList<GateCardRelease> cards)
        {
            HudFlyTarget target;
            switch (kind)
            {
                case GateRewardKind.Weapon: target = HudFlyTarget.Hand; break;
                default: return;
            }
            s_rects.Clear();
            foreach (var c in cards) s_rects.Add(new Rect(c.Screen - c.Size * 0.5f, c.Size));
            MvHudFly.RaiseLaunched(target, s_rects);
            s_rects.Clear();
        }

        int CountOf(HudFlyTarget t) => _player.Flies != null ? _player.Flies.CountOf((FlyTarget)(int)t) : 0;

        void OnArrived(FlyTarget t, int amount) => MvHudFly.RaiseArrived((HudFlyTarget)(int)t, amount);

        static IHudFlyHandler H(FlyTarget t) => MvHudFly.Handler((HudFlyTarget)(int)t);

        public bool TryGetRect(FlyTarget target, out Vector2 center, out Vector2 size)
        {
            var h = H(target);
            if (h != null) return h.TryGetRect(out center, out size);
            center = size = default;
            return false;
        }

        public void Show(int id, FlyTarget icon, int amount) => H(icon)?.Show(id, amount);

        // id만 오므로 맡은 섹션 모두에 넘기고, 자기 id가 아니면 섹션이 무시한다.
        public void Place(int id, Vector2 screen, float scale, float alpha)
        {
            foreach (HudFlyTarget t in Enum.GetValues(typeof(HudFlyTarget))) MvHudFly.Handler(t)?.Place(id, screen, scale, alpha);
        }

        public void Hide(int id)
        {
            foreach (HudFlyTarget t in Enum.GetValues(typeof(HudFlyTarget))) MvHudFly.Handler(t)?.Hide(id);
        }

        public void Dispose()
        {
            GateRewardDisplay.Released -= OnGateReleased;
            if (CharacterPresenter.LookTier == HudWeaponTier) CharacterPresenter.LookTier = null;
            if (_player != null)
            {
                _player.FlyArrived -= OnArrived;
                if (_player.Flies != null)
                {
                    if (_player.Flies.View == this) _player.Flies.View = null;
                    if (_player.Flies.Anchors == this) _player.Flies.Anchors = null;
                }
            }
            MvHudFly.InFlight = null;
        }
    }
}
