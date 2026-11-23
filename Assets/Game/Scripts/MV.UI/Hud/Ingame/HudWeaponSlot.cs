using DG.Tweening;
using Ease = DG.Tweening.Ease; // ui.contracts pre.5의 jjevol.Ease(위젯 모델)와 이름이 겹친다
using jjevol;
using MV.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>무기 칸 표시 데이터: 103_item 단계와 공격력 배수(공격력 카드).</summary>
    public readonly struct WeaponSlotData
    {
        public readonly MvTables Tables;
        public readonly int Tier;
        public readonly double DmgMul;

        public WeaponSlotData(MvTables tables, int tier, double dmgMul)
        {
            Tables = tables;
            Tier = tier;
            DmgMul = dmgMul;
        }
    }

    /// <summary>
    /// 디자이너 <c>Slot_Weapon</c>(·변형 <c>Slot_Weapon_Stack</c>)의 값 연결(MV-UI2-HUD, 공용 부품 MV-UI3-HUD2). 겉(배치·그림)은 프리팹이 정하고, 등급 틀·무기 그림·공격력만 넣는다.
    /// 등급 틀 = 09_uiResource 등급 틀(103_item.tier 번호), 무기 그림 = 103_item.icon(Atlas_UI_Icon).
    /// 섹션은 <see cref="ShowUI(in WeaponSlotData)"/>로 켜고 그린다. 연출(등장·모임·튐)은 무기 섹션 단계에서 이 부품에 붙인다.
    /// </summary>
    public sealed class HudWeaponSlot : UIBase
    {
        [SerializeField] AddressableImage grade;
        [SerializeField] AddressableImage icon;
        /// <summary>공격력 글자(<c>Slot_Attack/Value Text</c>). 스택 변형에서 꺼져 있으면 넣기만 한다.</summary>
        [SerializeField] TMP_Text attack;

        [Header("머지 연출(디자이너 리소스, 없으면 크기 연출만)")]
        [Tooltip("머지 때 하얗게 밝아지는 덮개(05 §5.3.3 wmg·wmerged 밝기). 꺼 둔 Image + 흰 칠 재질(UIWhiteFill), 연출 때 등급 틀 그림 모양을 따라 켠다. 쉴 때 색은 프리팹 그대로 둔다(디자이너 Slot_Weapon은 알파 0).")]
        [SerializeField] Graphic flash;
        [Tooltip("덮개 최고 알파(임시, HudGradeUpFx.flashPeak와 같음). 연출 동안 덮개 색 알파를 이 값으로 두고 밝기를 곱한다. MV-UI3-FXFIX: 예전엔 Image 색 알파가 최고 밝기라 디자이너 알파 0에서 덮개가 보이지 않았다.")]
        [SerializeField] float flashPeak = 1f;
        [Tooltip("머지 결과 칸 위에서 터지는 반짝임(영상 7.3). 꺼 둔 채로 둔다.")]
        [SerializeField] GameObject sparkle;

        public int Tier { get; private set; }
        public bool HasFlash => flash;
        public bool HasSparkle => sparkle;
        public string GradeSprite => grade ? grade.spriteName : null;
        public string IconSprite => icon ? icon.spriteName : null;
        public string AttackText => attack ? attack.text : null;

        WeaponSlotData _data;

        /// <summary>데이터를 넣고 켜서 그린다(UIBase 수명: 켜기 → Refresh → 그리기).</summary>
        public void ShowUI(in WeaponSlotData data)
        {
            _data = data;
            ShowUI();
        }

        protected override void _DrawContents() => Draw(_data.Tables, _data.Tier, _data.DmgMul);

        /// <summary>켜고 끄지 않고 지금 그린다(화면 조합 전 기존 호출용). 단계 tier의 103 행으로 칸을 채운다. dmgMul = 공격력 카드 배수.</summary>
        public void Set(MvTables t, int tier, double dmgMul)
        {
            _data = new WeaponSlotData(t, tier, dmgMul);
            Draw(t, tier, dmgMul);
        }

        // ---------------- 연출(무기 섹션이 시간을 정해 부른다, MV-UI3-HUD4) ----------------

        Graphic[] _graphics;
        static bool s_missingLogged;
        Color _flashRest;
        bool _flashCaptured;

        /// <summary>시험·QA: 덮개가 지금 보이는 밝기(켜짐 × 색 알파 × 밝기).</summary>
        public float FlashVisibleAlpha => flash && flash.gameObject.activeInHierarchy ? flash.color.a * flash.canvasRenderer.GetAlpha() : 0f;
        /// <summary>시험·QA: 반짝임이 켜져 있는가.</summary>
        public bool SparkleOn => sparkle && sparkle.activeInHierarchy;

        void CaptureFlash()
        {
            if (_flashCaptured || !flash) return;
            _flashCaptured = true;
            _flashRest = flash.color;
        }

        Graphic[] Graphics => _graphics ??= GetComponentsInChildren<Graphic>(true);

        void LogMissingOnce()
        {
            if (s_missingLogged || (flash && sparkle)) return;
            s_missingLogged = true;
            Debug.LogWarning("[HudWeaponSlot] 머지 연출 리소스 없음: Assets/Game/Art/UI/Prefab/HUD/Slot_Weapon.prefab에 "
                + (flash ? "" : "흰 덮개(flash) ") + (sparkle ? "" : "반짝임(sparkle) ") + "자식이 없습니다. 크기 연출만 합니다.");
        }

        /// <summary>그림 전체 투명도(CanvasRenderer, 부품을 더 붙이지 않는다).</summary>
        public void SetAlpha(float a)
        {
            foreach (var g in Graphics) if (g && g != flash) g.canvasRenderer.SetAlpha(a);
        }

        /// <summary>풀 반환·재사용 전: 연출을 멈추고 제모습으로.</summary>
        public void ResetLook()
        {
            DOTween.Kill(this);
            DOTween.Kill(transform);
            transform.localScale = Vector3.one;
            SetAlpha(1);
            if (flash)
            {
                flash.canvasRenderer.SetAlpha(0);
                if (_flashCaptured) flash.color = _flashRest;
                flash.gameObject.SetActive(false);
            }
            if (sparkle) sparkle.SetActive(false);
        }

        /// <summary>플래시를 켜고 지금 등급 틀 그림 모양을 따른다(틀 그림은 등급마다 바뀐다).</summary>
        void ArmFlash(float alpha)
        {
            if (!flash) return;
            CaptureFlash();
            var c = _flashRest;
            c.a = flashPeak;
            flash.color = c;
            if (flash is Image f && grade)
            {
                f.sprite = grade.sprite;
                f.type = grade.type;
                f.pixelsPerUnitMultiplier = grade.pixelsPerUnitMultiplier;
                f.preserveAspect = grade.preserveAspect;
            }
            flash.gameObject.SetActive(true);
            flash.canvasRenderer.SetAlpha(alpha);
        }

        /// <summary>전달: 투명 → 불투명(05 §5.3.3 wdeal, ease-out).</summary>
        public Tween PlayDeal(float sec)
        {
            SetAlpha(0);
            float a = 0;
            return DOTween.To(() => a, v => { a = v; SetAlpha(v); }, 1f, sec).SetEase(Ease.OutQuad).SetUpdate(true).SetTarget(this);
        }

        /// <summary>모으기: md 동안 밝아지며 scale 1.06(05 §5.3.3 wmg, ease-in, 끝 유지).</summary>
        public Tween PlayGather(float md, float scale)
        {
            LogMissingOnce();
            var seq = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            seq.Join(transform.DOScale(scale, md).SetEase(Ease.InQuad));
            if (flash)
            {
                float a = 0;
                ArmFlash(0);
                seq.Join(DOTween.To(() => a, v => { a = v; flash.canvasRenderer.SetAlpha(v); }, 1f, md).SetEase(Ease.InQuad));
            }
            return seq;
        }

        /// <summary>결과: scale 1.3·밝음 → 제모습(05 §5.3.3 wmerged, ease-out) + 반짝임(영상).</summary>
        public Tween PlayMerged(float sec, float fromScale)
        {
            LogMissingOnce();
            transform.localScale = Vector3.one * fromScale;
            if (sparkle) { sparkle.SetActive(false); sparkle.SetActive(true); }
            var seq = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            seq.Join(transform.DOScale(1f, sec).SetEase(Ease.OutQuad));
            if (flash)
            {
                float a = 1;
                ArmFlash(1);
                seq.Join(DOTween.To(() => a, v => { a = v; flash.canvasRenderer.SetAlpha(v); }, 0f, sec).SetEase(Ease.OutQuad));
                seq.OnComplete(() => { if (flash) flash.gameObject.SetActive(false); });
            }
            return seq;
        }

        void Draw(MvTables t, int tier, double dmgMul)
        {
            Tier = tier;
            ItemData item = null;
            if (t != null) foreach (var it in t.Item.Datas) if (it.tier == tier) { item = it; break; }
            HudImage.Set(grade, HudGrade.Frame(t, tier));
            if (item != null) HudImage.Set(icon, MvAtlas.UiIcon, item.icon);
            if (attack) attack.text = item != null ? MvUiFormat.Fmt(item.dmg * dmgMul) : "";
        }
    }

    /// <summary>AddressableImage에 표 그림을 넣는 공통 도우미(같은 그림이면 다시 읽지 않는다).</summary>
    public static class HudImage
    {
        public static void Set(AddressableImage img, UiResourceData row)
        {
            if (row != null) Set(img, row.atlas, row.sprite);
        }

        public static void Set(AddressableImage img, string atlas, string sprite)
        {
            if (!img || string.IsNullOrEmpty(atlas) || string.IsNullOrEmpty(sprite)) return;
            if (img.spriteName != sprite || img.atlasKey != atlas) img.SetAtlasSprite(atlas, sprite);
        }

        /// <summary>단독 텍스처(표 주소 열, <c>Local_Texture</c>)를 넣는다. 키가 비면 프리팹 값을 그대로 둔다.</summary>
        public static void Set(AddressableTexture img, string address)
        {
            if (!img || string.IsNullOrEmpty(address)) return;
            if (img.spriteKey != address) img.spriteKey = address;
        }
    }
}
