using System.Collections.Generic;
using DG.Tweening;
using Ease = DG.Tweening.Ease; // ui.contracts pre.5의 jjevol.Ease(위젯 모델)와 이름이 겹친다
using jjevol;
using MV.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 디자이너 <c>Card_Skill</c>(스킬 카드)의 값 연결(MV-UI2-HUD, [HUD] 인게임 Skill Section·Card Pos).
    /// 롤링 전에는 Question만 보이고(Mask 아래는 끔), 롤링은 <c>Slot_Skill</c> 띠를 한 칸씩 올려 보이고, 확정되면 첫 칸에 고른 카드 그림·제목에 카드 이름.
    /// 고르는 결과는 시뮬(룰렛)이 정하고, 이 카드는 그 결과를 보인다.
    /// 섹션은 <see cref="ShowUI(in SkillCardData)"/>로 켜고 정지 상태(「?」 또는 확정 카드)를 그린다. 롤링은 <see cref="BeginRoll"/>·<see cref="Advance"/>(공용 부품 MV-UI3-HUD2).
    /// </summary>
    public sealed class HudSkillCard : UIBase
    {
        [SerializeField] AddressableImage grade;
        [SerializeField] RectTransform strip;
        [SerializeField] AddressableImage[] cells;
        /// <summary>카드 그림 띠(<c>Slot_Skill</c>)를 가리는 <c>Mask</c> 오브젝트. 프리팹 원본에서는 꺼져 있어(롤링 전 모습), 롤링·확정 때 켠다.</summary>
        [SerializeField] GameObject mask;
        /// <summary>롤링 전 고정 글(<c>Question Text</c>).</summary>
        [SerializeField] GameObject question;
        /// <summary>Mask 아래 카드 바탕 그림(<c>Card Texture (1)</c>). 롤링 전에는 끈다.</summary>
        [SerializeField] GameObject maskBack;
        [SerializeField] TMP_Text title;

        [Header("롤링 완료 연출(디자이너 리소스)")]
        [Tooltip("완료 때 하얗게 번쩍이는 덮개(Flash). 흰 칠 재질 Image, 연출 때만 켠다.")]
        [SerializeField] Graphic flash;
        [Tooltip("완료 때 터지는 반짝임(Effect_Sparkle). 꺼 둔 채로 둔다.")]
        [SerializeField] GameObject sparkle;

        Vector2 _stripHome;
        bool _homeSet;

        public int Tier { get; private set; } = -1;
        public int CardId { get; private set; }
        public bool Rolling { get; private set; }
        public string Title => title ? title.text : null;
        public string GradeSprite => grade ? grade.spriteName : null;
        public string FaceSprite => cells != null && cells.Length > 0 && cells[0] ? cells[0].spriteName : null;
        public bool QuestionShown => question && question.activeSelf;
        /// <summary>카드 그림이 실제로 보이는가(Mask·띠가 둘 다 켜지고 첫 칸이 Mask 창 안, 시험·QA).</summary>
        public bool FaceVisible => strip && strip.gameObject.activeSelf && (!mask || mask.activeSelf) && FaceInsideMask;

        /// <summary>첫 칸 가운데가 Mask 창 안인가. 띠 위치가 어긋나면(Slot_Skill y = 0) 첫 칸이 창 밖으로 나가 뒷판만 보인다.</summary>
        bool FaceInsideMask
        {
            get
            {
                if (!mask || cells == null || cells.Length == 0 || !cells[0]) return true;
                var m = mask.transform as RectTransform;
                if (!m) return true;
                var c = cells[0].rectTransform;
                Vector3 local = m.InverseTransformPoint(c.TransformPoint(c.rect.center));
                return m.rect.Contains(local);
            }
        }
        public RectTransform Rect => (RectTransform)transform;
        public int CellCount => cells?.Length ?? 0;

        SkillCardData _data;

        /// <summary>데이터를 넣고 켜서 그린다. Card가 없으면 「?」(롤링 전), 있으면 확정 카드.</summary>
        public void ShowUI(in SkillCardData data)
        {
            _data = data;
            ShowUI();
        }

        protected override void _DrawContents()
        {
            if (_data.Card != null) ShowCard(_data.Tables, _data.Card);
            else ShowQuestion(_data.Tables, _data.TierIndex);
        }

        void Home()
        {
            if (_homeSet || !strip) return;
            _stripHome = strip.anchoredPosition;
            _homeSet = true;
        }

        void SetGrade(MvTables t, int tierIndex)
        {
            Tier = tierIndex;
            var row = HudGrade.CardTier(t, tierIndex);
            if (row != null) HudImage.Set(grade, MvAtlas.UiCard, row.frameSprite);
        }

        /// <summary>롤링 전: Question만(디자이너 시퀀스 1). 제목 = 등급 이름.</summary>
        public void ShowQuestion(MvTables t, int tierIndex)
        {
            Home();
            CardId = 0;
            Rolling = false;
            SetGrade(t, tierIndex);
            if (title) title.text = HudGrade.CardTierName(t, HudGrade.CardTier(t, tierIndex));
            if (question) question.SetActive(true);
            if (strip) { strip.gameObject.SetActive(false); strip.anchoredPosition = _stripHome; }
            if (maskBack) maskBack.SetActive(false);
        }

        /// <summary>롤링 시작: Question을 끄고 칸에 후보 그림을 채운다(시퀀스 4).</summary>
        public void BeginRoll(MvTables t, int tierIndex, IReadOnlyList<string> faces)
        {
            Home();
            CardId = 0;
            Rolling = true;
            SetGrade(t, tierIndex);
            if (title) title.text = HudGrade.CardTierName(t, HudGrade.CardTier(t, tierIndex));
            if (question) question.SetActive(false);
            if (maskBack) maskBack.SetActive(false);
            if (mask) mask.SetActive(true);
            if (strip) { strip.gameObject.SetActive(true); strip.anchoredPosition = _stripHome; }
            if (cells == null || faces == null || faces.Count == 0) return;
            for (int i = 0; i < cells.Length; i++) HudImage.Set(cells[i], MvAtlas.UiCard, faces[i % faces.Count]);
        }

        /// <summary>한 칸 거리(Slot_Skill 첫 두 칸 사이, 레이아웃이 정한 값). 띠를 이만큼 옮기면 둘째 칸이 첫 칸 자리에 온다.</summary>
        public Vector2 CellStep()
        {
            if (cells == null || cells.Length < 2 || !cells[0] || !cells[1]) return Vector2.zero;
            return (Vector2)(cells[0].rectTransform.localPosition - cells[1].rectTransform.localPosition);
        }

        /// <summary>띠를 제자리에서 k(0~1)칸 옮긴 자리로.</summary>
        public void SetStripOffset(float k)
        {
            if (!strip) return;
            Home();
            strip.anchoredPosition = _stripHome + CellStep() * k;
        }

        /// <summary>칸 그림을 한 칸 앞으로 당기고(첫 칸 ← 둘째 칸) 마지막 칸에 next를 넣고 띠를 제자리로.</summary>
        public void Advance(string next)
        {
            if (cells == null || cells.Length == 0) return;
            for (int i = 0; i < cells.Length - 1; i++)
                if (cells[i] && cells[i + 1]) HudImage.Set(cells[i], cells[i + 1].atlasKey, cells[i + 1].spriteName);
            HudImage.Set(cells[cells.Length - 1], MvAtlas.UiCard, next);
            if (strip) strip.anchoredPosition = _stripHome;
        }

        /// <summary>확정(시퀀스 5): 첫 칸에 카드 그림, 제목에 카드 이름.</summary>
        public void ShowCard(MvTables t, CardData card)
        {
            Home();
            Rolling = false;
            CardId = card?.id ?? 0;
            if (card != null) SetGrade(t, card.tier);
            if (question) question.SetActive(false);
            if (maskBack) maskBack.SetActive(false);
            if (mask) mask.SetActive(true);   // 원본은 Mask가 꺼져 있어, 켜지 않으면 Card Pos 자리 카드에 그림이 안 보인다
            if (strip) { strip.gameObject.SetActive(true); strip.anchoredPosition = _stripHome; }
            if (card != null && cells != null && cells.Length > 0) HudImage.Set(cells[0], MvAtlas.UiCard, card.icon);
            if (title) title.text = HudGrade.CardName(t, card);
        }

        // ---------------- 롤링 완료 연출(스킬 섹션이 시간·배율을 정해 부른다, MV-UI3-CLIMBFX) ----------------

        static bool s_missingLogged;

        public bool HasFlash => flash;
        public bool HasSparkle => sparkle;
        /// <summary>완료 연출 중(시험·QA).</summary>
        public bool CompletePlaying => DOTween.IsTweening(this);

        /// <summary>풀 반환·재사용 전: 연출을 멈추고 제모습으로.</summary>
        public void ResetLook()
        {
            DOTween.Kill(this);
            transform.localScale = Vector3.one;
            if (flash) flash.gameObject.SetActive(false);
            if (sparkle) sparkle.SetActive(false);
        }

        /// <summary>
        /// 롤링 완료(노션 ClimbStair 연출 보강 2): 크기 1 → peakScale → 1(앞 upRatio 동안 커지고 나머지 동안 돌아옴),
        /// 플래시 flashAlpha → 0, 반짝임 1회.
        /// </summary>
        public Tween PlayComplete(float sec, float peakScale, float upRatio, float flashAlpha)
        {
            ResetLook();
            if (!s_missingLogged && (!flash || !sparkle))
            {
                s_missingLogged = true;
                Debug.LogWarning("[HudSkillCard] 롤링 완료 연출 리소스 없음: Assets/Game/Art/UI/Prefab/HUD/Card_Skill.prefab에 "
                    + (flash ? "" : "Flash ") + (sparkle ? "" : "Effect_Sparkle ") + "연결이 없습니다. 있는 것만 연출합니다.");
            }
            if (sec <= 0 || !Application.isPlaying) return null;
            if (sparkle) sparkle.SetActive(true);
            float up = sec * Mathf.Clamp01(upRatio);
            var seq = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            seq.Append(transform.DOScale(peakScale, up).SetEase(Ease.OutQuad));
            seq.Append(transform.DOScale(1f, sec - up).SetEase(Ease.OutBack));
            if (flash)
            {
                var c = flash.color;
                flash.color = new Color(c.r, c.g, c.b, 1f);
                flash.gameObject.SetActive(true);
                float a = flashAlpha;
                flash.canvasRenderer.SetAlpha(a);
                seq.Insert(0, DOTween.To(() => a, v => { a = v; flash.canvasRenderer.SetAlpha(v); }, 0f, sec).SetEase(Ease.OutQuad));
            }
            seq.OnComplete(() => { if (flash) flash.gameObject.SetActive(false); });
            return seq;
        }
    }

    /// <summary>스킬 카드 정지 상태 데이터: 등급 번호(115_cardTier)와 확정 카드(없으면 「?」).</summary>
    public readonly struct SkillCardData
    {
        public readonly MvTables Tables;
        public readonly int TierIndex;
        public readonly CardData Card;

        public SkillCardData(MvTables tables, int tierIndex, CardData card)
        {
            Tables = tables;
            TierIndex = tierIndex;
            Card = card;
        }
    }
}
