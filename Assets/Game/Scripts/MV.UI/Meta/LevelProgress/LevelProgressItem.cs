using System.Collections;
using jjevol;
using TMPro;
using UnityEngine;

namespace MV.UI
{
    /// <summary>LevelProgress 관문 한 칸 표시 데이터.</summary>
    public readonly struct LevelProgressItemData
    {
        public readonly long Milestone;
        /// <summary>Area 첫 관문이면 Area 이름표(<c>Slot_AreaLabel</c>)를 켠다(노션: 「Area 첫번째 단계에서 동적으로 Active」).</summary>
        public readonly bool ShowLabel;
        public readonly AreaLabelData Label;
        /// <summary>Milestone 재화 아이콘(07_consumeType). 비면 숨긴다.</summary>
        public readonly string IconAtlas, Icon;
        /// <summary>Area가 풀리는 영혼 수(<see cref="LevelProgressAreaInfo.UnlockSouls"/>). 이름표가 켜진 칸만 Lock을 쓴다.</summary>
        public readonly long UnlockSouls;

        public LevelProgressItemData(long milestone, bool showLabel, in AreaLabelData label, string iconAtlas = null, string icon = null, long unlockSouls = 0)
        {
            UnlockSouls = unlockSouls;
            Milestone = milestone;
            ShowLabel = showLabel;
            Label = label;
            IconAtlas = iconAtlas;
            Icon = icon;
        }
    }

    /// <summary>
    /// 디자이너 프리팹 <c>Element_Progress_Item</c>의 루트 부품(MV-UI3-LVPROG). Milestone 숫자와 Area 이름표만 맡는다.
    /// 막대·인디케이터 위치는 팝업(<see cref="LevelProgressView"/>)이 <see cref="MilestoneAnchor"/> 위치를 읽어 정한다.
    /// </summary>
    public sealed class LevelProgressItem : UIBase
    {
        /// <summary><c>Milestone</c>(영혼 아이콘 + 숫자 묶음). 막대 눈금 위치 기준.</summary>
        [SerializeField] RectTransform milestone;
        /// <summary><c>Milestone/Value Text (TMP)</c>.</summary>
        [SerializeField] TMP_Text milestoneText;
        /// <summary><c>Milestone/Icon (2)</c> 재화 아이콘(AddressableImage, 프리팹에 그림 없음).</summary>
        [SerializeField] AddressableImage milestoneIcon;
        /// <summary>중첩 <c>Slot_AreaLabel</c>의 <see cref="HudAreaLabel"/>.</summary>
        [SerializeField] HudAreaLabel areaLabel;
        /// <summary>
        /// <c>Area/Lock</c>: 아직 풀리지 않은 Area 이름표 덮개(디자이너, 노션 「Popup_LevelProgress : Area Lock 표기」).
        /// 영혼이 Area 값 이상이 되면 <see cref="areaLockAnimator"/>에 <see cref="unlockTrigger"/>를 주고, 연출이 끝나면 끈다.
        /// </summary>
        [SerializeField] GameObject areaLock;
        /// <summary><c>Area/Lock</c>의 Animator(<c>ProgressLevel_Area_UnLock.controller</c>). 없으면 연출 없이 바로 끈다.</summary>
        [SerializeField] Animator areaLockAnimator;
        /// <summary>풀림 연출 트리거 이름(디자이너 Animator Controller의 매개변수).</summary>
        [SerializeField] string unlockTrigger = "Play";

        LevelProgressItemData _data;
        Coroutine _unlock;

        public RectTransform MilestoneAnchor => milestone;
        public string MilestoneText => milestoneText ? milestoneText.text : null;
        public HudAreaLabel AreaLabel => areaLabel;
        public string MilestoneIconShown => milestoneIcon && milestoneIcon.enabled ? milestoneIcon.spriteName : null;
        public bool HasRefs => milestone && milestoneText && areaLabel && milestoneIcon && areaLock;
        /// <summary>Lock 덮개가 켜져 있는지(풀림 연출 중이면 아직 켜짐).</summary>
        public bool LockShown => areaLock && areaLock.activeSelf;
        public bool Unlocking => _unlock != null;

        public void ShowUI(in LevelProgressItemData data)
        {
            _data = data;
            ShowUI();
        }

        protected override void _DrawContents()
        {
            if (milestoneText) milestoneText.text = MvUiFormat.Fmt(_data.Milestone);
            HudImage.SetOrHide(milestoneIcon, _data.IconAtlas, _data.Icon);
            if (areaLabel)
            {
                if (_data.ShowLabel) areaLabel.ShowUI(_data.Label);
                else areaLabel.HideUI();
            }
            if (!_data.ShowLabel) HideLock();
        }

        /// <summary>
        /// 보이는 영혼 수로 Lock을 정한다. 영혼 &lt; Area 값이면 켠다. 켜져 있다가 풀리면 <paramref name="animate"/>일 때 연출 뒤 끄고,
        /// 아니면 바로 끈다. 막대 오름과 따로 돈다(노션: Progress Fill과 순차적인 비동기 연출).
        /// </summary>
        public void SetSouls(long souls, bool animate)
        {
            if (!areaLock || !_data.ShowLabel) return;
            if (souls < _data.UnlockSouls)
            {
                if (_unlock != null) StopUnlock();
                areaLock.SetActive(true);
                return;
            }
            if (!areaLock.activeSelf || _unlock != null) return;
            if (animate && areaLockAnimator && isActiveAndEnabled && areaLockAnimator.runtimeAnimatorController)
                _unlock = StartCoroutine(PlayUnlock());
            else HideLock();
        }

        IEnumerator PlayUnlock()
        {
            var anim = areaLockAnimator;
            int idle = anim.GetCurrentAnimatorStateInfo(0).shortNameHash;
            float limit = 1f;
            foreach (var clip in anim.runtimeAnimatorController.animationClips) if (clip) limit = Mathf.Max(limit, clip.length + 1f);
            anim.SetTrigger(unlockTrigger);
            float t = 0f;
            yield return null;
            while (t < limit)
            {
                var st = anim.GetCurrentAnimatorStateInfo(0);
                if (!anim.IsInTransition(0) && st.shortNameHash != idle && st.normalizedTime >= 1f) break;
                t += Time.deltaTime;
                yield return null;
            }
            _unlock = null;
            HideLock();
        }

        void StopUnlock()
        {
            if (_unlock != null) { StopCoroutine(_unlock); _unlock = null; }
            ResetLockPose();
        }

        void HideLock()
        {
            StopUnlock();
            if (areaLock && areaLock.activeSelf) areaLock.SetActive(false);
        }

        /// <summary>연출이 바꾼 값(투명도·크기·회전)을 처음 값으로 되돌려, 다시 잠길 때(환생 등) 덮개가 제 모습으로 보이게 한다.</summary>
        void ResetLockPose()
        {
            if (!areaLockAnimator || !areaLockAnimator.isActiveAndEnabled) return;
            areaLockAnimator.Rebind();
            areaLockAnimator.Update(0f);
        }

        public override void HideUI()
        {
            StopUnlock();
            base.HideUI();
        }
    }
}
