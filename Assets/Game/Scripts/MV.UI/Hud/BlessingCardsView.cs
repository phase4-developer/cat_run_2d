using System;
using System.Collections.Generic;
using jjevol;
using MV.Data;
using MV.Run;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 가호 카드 룰렛 `#hc`(05 §5.2.9, 층 220)와 가호 줄 `#buffs`(§5.2.7). 런 상태 <see cref="RunState.Roulette"/>·<see cref="RunState.Cards"/>를 읽어 그린다.
    /// 연출 정본 = 새 HTML `startRoulette`·`updRoulette`·`flyCard`·가호 줄 그리기. 시각 = `Atlas_UI_Card`(110 icon·115 frameSprite).
    /// 시간은 시뮬 룰렛 시계(rl.T·FlyLeft)를 따른다. 가호 칸 `arrive`만 UI 실시간.
    /// 만들지 않은 것(리소스·연출 교체 예정, 2026-11-14 사용자): 카드 별 14개, 확정 뒤 빛 그림자, 둥근 모서리(칸·그림 바탕), 글자 테두리 두께 맞춤.
    /// </summary>
    public sealed class BlessingCardsView : MonoBehaviour
    {
        const float U = MvUi.U;

        // ---- 05 §5.2.9·§5.2.7 배치 상수(CSS 값, 표 키 없음) ----
        const float CardW = 25 * U, CardH = 33 * U, CardGap = 3 * U, CardBorder = .6f * U;
        /// <summary>줄 중심 = 화면 높이 46%(새 HTML `H*0.46`).</summary>
        const float RowY = .46f;
        const float TierTop = .8f * U, TierSize = 3.4f * U;
        const float ArtSize = 17 * U, ArtTop = 3 * U, NameTop = 1 * U, NameSize = 3 * U, NameLine = 1.1f;
        const float ArtInset = 6f; // inset 2px × 3(D-31)
        const float SlotSize = 8.4f * U, SlotGap = 1 * U, SlotBorder = 4.5f, BuffLeft = 3 * U, BuffBottom = 3.4f * U, BuffMaxW = 62 * U;
        const float BadgeSize = 2.6f * U, BadgeOut = .8f * U, BadgePad = 1 * U;
        /// <summary>`chosen` .8s, `arrive` .5s(CSS).</summary>
        const float ChosenSec = .8f, ArriveSec = .5f, ArriveScale = 1.6f, ArriveBright = 3f, FlyAlpha = .85f;
        static readonly float[] ChosenScaleT = { 0, .25f, 1 }, ChosenScaleV = { 1.35f, 1.08f, 1.04f };
        static readonly float[] ChosenBrightT = { 0, .25f, .45f, .65f, 1 }, ChosenBrightV = { 3, 1.6f, 2.2f, 1.3f, 1 };
        static readonly Color ArtBg = new Color(10 / 255f, 8 / 255f, 24 / 255f, .55f), ArtLine = new Color(1, 1, 1, .5f);
        static readonly Color Stroke = new Color(0, 0, 0, .5f), BadgeText = MvUi.Hex("#0b0b0e");

        sealed class CardEl
        {
            public RectTransform Rt;
            public AddressableImage Skin, Art;
            public Text Tier, Name;
            public Image Flash;
            public CanvasGroup Group;
        }

        sealed class SlotEl
        {
            public RectTransform Rt;
            public Image[] Border;
            public AddressableImage Icon;
            public RectTransform Badge;
            public Image BadgeBg;
            public Text BadgeText;
            public Image Flash;
        }

        Func<RunState> _state;
        RunState _run;
        MvTables _tables;
        readonly List<CardData> _order = new List<CardData>();
        readonly List<CardData> _pool = new List<CardData>();
        double _flyTime;
        float _bx1, _by1, _bx2, _by2;

        RectTransform _row, _fly, _buffs;
        readonly List<CardEl> _cards = new List<CardEl>();
        readonly List<CardEl> _flyCards = new List<CardEl>();
        readonly List<SlotEl> _slots = new List<SlotEl>();
        readonly List<int> _slotIds = new List<int>();
        readonly Dictionary<int, int> _counts = new Dictionary<int, int>();
        readonly Dictionary<int, float> _arrive = new Dictionary<int, float>();

        /// <summary>런 상태 출처. null이면 숨긴다.</summary>
        public void Bind(Func<RunState> state)
        {
            _state = state;
            _run = null;
        }

        // ---- 시험·겉 교체용 읽기 ----
        public int CardCount { get { int n = 0; foreach (var c in _cards) if (c.Rt.gameObject.activeSelf) n++; return n; } }
        public RectTransform CardRect(int i) => _cards[i].Rt;
        public string CardSkin(int i) => _cards[i].Skin.spriteName;
        public string CardArt(int i) => _cards[i].Art.spriteName;
        public string CardName(int i) => _cards[i].Name.text;
        public string CardTier(int i) => _cards[i].Tier.text;
        public float CardAlpha(int i) => _cards[i].Group.alpha;
        public float CardFlash(int i) => _cards[i].Flash.color.a;
        public RectTransform FlyRect(int i) => i < _flyCards.Count && _flyCards[i].Rt.gameObject.activeSelf ? _flyCards[i].Rt : null;
        public float FlyGroupAlpha(int i) => _flyCards[i].Group.alpha;
        public int SlotCount => _slotIds.Count;
        public int SlotCard(int j) => _slotIds[j];
        public RectTransform SlotRect(int j) => _slots[j].Rt;
        public string SlotIcon(int j) => _slots[j].Icon.spriteName;
        public string SlotBadge(int j) => _slots[j].Badge.gameObject.activeSelf ? _slots[j].BadgeText.text : "";
        public Color SlotColor(int j) => _slots[j].Border[0].color;
        /// <summary>한 줄 칸 수 = ⌊(62u + 사이) / (칸 + 사이)⌋.</summary>
        public static int SlotsPerRow => Mathf.Max(1, Mathf.FloorToInt((BuffMaxW + SlotGap) / (SlotSize + SlotGap) + 1e-4f));

        void EnsureBuilt()
        {
            if (_buffs) return;
            var root = (RectTransform)transform;
            _buffs = MvUi.Place(MvUi.Rect(root, "Buffs"), Vector2.zero, Vector2.zero, new Vector2(BuffLeft, BuffBottom), new Vector2(BuffMaxW, SlotSize));
            var center = MvUi.Stretch(MvUi.Rect(root, "Cards"));
            MvUi.EnsureLayer(center.gameObject, MvUiLayer.HudCenter, false);
            _row = MvUi.Place(MvUi.Rect(center, "Row"), new Vector2(.5f, 1 - RowY), new Vector2(.5f, .5f), Vector2.zero, new Vector2(CardW, CardH));
            _fly = MvUi.Stretch(MvUi.Rect(center, "Fly"));
        }

        void SetTables(MvTables t)
        {
            if (_tables == t) return;
            _tables = t;
            _order.Clear();
            if (t == null) return;
            _order.AddRange(t.Card.Datas);
            _order.Sort((a, b) => a.id.CompareTo(b.id)); // 정본 CARDS 순서: 공용 → 신 고유
            _flyTime = t.RunConfig.D(RunConfigKeys.FlyCard_Time);
            ParseBezier(t.RunConfig.Raw(RunConfigKeys.FlyCard_Bezier));
        }

        void ParseBezier(string raw)
        {
            var p = (raw ?? "").Split('/', ',');
            if (p.Length != 4) { Debug.LogError("[BlessingCardsView] 300_runConfig flyCard.bezier 형식이 틀립니다: " + raw); return; }
            float F(string s) => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture);
            _bx1 = F(p[0]); _by1 = F(p[1]); _bx2 = F(p[2]); _by2 = F(p[3]);
        }

        public void Tick(float dt)
        {
            EnsureBuilt();
            var s = _state?.Invoke();
            if (s != _run)
            {
                _run = s;
                _counts.Clear();
                _arrive.Clear();
                if (s != null) foreach (var kv in s.Cards) _counts[kv.Key] = kv.Value;
            }
            SetTables(s?.Tables);
            if (s == null || _tables == null)
            {
                _row.gameObject.SetActive(false);
                _fly.gameObject.SetActive(false);
                _buffs.gameObject.SetActive(false);
                return;
            }
            _buffs.gameObject.SetActive(true);
            UpdateBuffs(s, dt);
            UpdateCards(s);
        }

        // ---------------- #hc 카드 줄 ----------------

        void UpdateCards(RunState s)
        {
            var rl = s.Roulette;
            bool show = rl != null && s.Phase != RunPhase.Result;
            _row.gameObject.SetActive(show);
            _fly.gameObject.SetActive(show);
            if (!show) return;

            var climb = _tables.Climb.Config();
            int n = rl.Cards.Count;
            while (_cards.Count < n) { _cards.Add(BuildCard(_row, "Card" + _cards.Count)); _flyCards.Add(BuildCard(_fly, "FlyCard" + _flyCards.Count)); }
            float total = n * CardW + (n - 1) * CardGap;
            for (int i = 0; i < _cards.Count; i++)
            {
                var el = _cards[i];
                var fl = _flyCards[i];
                bool on = i < n;
                el.Rt.gameObject.SetActive(on);
                if (!on) { fl.Rt.gameObject.SetActive(false); continue; }
                var k = rl.Cards[i];
                el.Rt.anchoredPosition = new Vector2(-total / 2 + CardW / 2 + i * (CardW + CardGap), 0);

                // 넘김 중: 그 등급 풀 카드(새 HTML pool[step % len]), 확정: 뽑힌 카드
                CardData face = k.Chosen ? k.Pick : FlipFace(s, k, i);
                int tier = k.Chosen && k.Pick != null ? k.Pick.tier : k.Tier;
                SetFace(el, tier, face);

                float scale = 1, bright = 1;
                if (k.Chosen)
                {
                    float u = Mathf.Clamp01((float)(rl.T - k.EndAt) / ChosenSec);
                    scale = Keys(ChosenScaleT, ChosenScaleV, u);
                    bright = Keys(ChosenBrightT, ChosenBrightV, u);
                }
                el.Rt.localScale = Vector3.one * scale;
                SetFlash(el.Flash, bright);
                // 날기 시작하면 원본은 숨기고 복제가 난다(새 HTML flyCard)
                el.Group.alpha = k.Flying || k.Applied ? 0 : 1;

                bool flying = k.Flying && !k.Applied;
                fl.Rt.gameObject.SetActive(flying);
                if (!flying) continue;
                SetFace(fl, tier, k.Pick);
                SetFlash(fl.Flash, 1);
                double elapsed = climb["rlFlyMs"] / 1000.0 - k.FlyLeft;
                float p = _flyTime > 0 ? Mathf.Clamp01((float)(elapsed / _flyTime)) : 1;
                float e = Bezier(p);
                Vector3 from = el.Rt.position;
                Vector3 to = _buffs.TransformPoint(SlotCenter(FlyTargetIndex(k.Pick)));
                fl.Rt.position = Vector3.LerpUnclamped(from, to, e);
                fl.Rt.localScale = Vector3.one * Mathf.LerpUnclamped(1, SlotSize / CardW, e);
                fl.Group.alpha = Mathf.Lerp(1, FlyAlpha, p);
                fl.Rt.SetAsLastSibling();
            }
        }

        CardData FlipFace(RunState s, RouletteCard k, int i)
        {
            int tier = k.Pick != null ? k.Pick.tier : k.Tier;
            _pool.Clear();
            foreach (var c in s.CardPool) if (c.tier == tier) _pool.Add(c);
            if (_pool.Count == 0) return k.Pick;
            return _pool[(i + k.Flips) % _pool.Count];
        }

        void SetFace(CardEl el, int tier, CardData face)
        {
            var tr = TierRow(tier);
            Sprite(el.Skin, MvAtlas.UiCard, tr?.frameSprite);
            el.Tier.text = tr != null ? TierName(tr) : "";
            Sprite(el.Art, MvAtlas.UiCard, face?.icon);
            el.Name.text = face != null ? CardDisplayName(face) : "";
        }

        CardTierData TierRow(int tier)
        {
            foreach (var r in _tables.CardTier.Datas) if (r.tierIndex == tier) return r;
            return null;
        }

        /// <summary>등급 이름 `name:&lt;115 id&gt;`. 번역이 없으면 표 이름 끝 낱말 대문자(새 HTML TIERS.n 「COMMON」 등).</summary>
        string TierName(CardTierData r)
        {
            string raw = MvUiText.RawName(_tables.CardTier.RawRows, r.id) ?? "";
            int sp = raw.LastIndexOf(' ');
            return MvUiText.Name(r.id, (sp >= 0 ? raw.Substring(sp + 1) : raw).ToUpperInvariant());
        }

        string CardDisplayName(CardData c) => MvUiText.Name(c.id, MvUiText.RawName(_tables.Card.RawRows, c.id) ?? c.key);

        static void Sprite(AddressableImage img, string atlas, string sprite)
        {
            bool has = !string.IsNullOrEmpty(sprite);
            img.enabled = has;
            if (has && (img.spriteName != sprite || img.atlasKey != atlas)) img.SetAtlasSprite(atlas, sprite);
        }

        CardEl BuildCard(RectTransform parent, string name)
        {
            var el = new CardEl();
            el.Skin = Img(parent, name, true);
            el.Rt = MvUi.Place(el.Skin.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(CardW, CardH));
            el.Group = el.Rt.gameObject.AddComponent<CanvasGroup>();
            el.Group.blocksRaycasts = false;
            el.Group.interactable = false;
            el.Rt.gameObject.AddComponent<Mask>().showMaskGraphic = true; // 번쩍임 덮개를 카드 모양으로 자른다

            // 세로 가운데 묶음(새 HTML flex column center): 그림 위 여백 + 그림 + 이름 위 여백 + 이름 한 줄
            float inner = CardH - 2 * CardBorder;
            float content = ArtTop + ArtSize + NameTop + NameSize * NameLine;
            float top = CardBorder + (inner - content) / 2;

            el.Tier = MvUi.Label(el.Rt, "Tier", TierSize, Color.white, TextAnchor.MiddleCenter);
            MvUi.Place(el.Tier.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -TierTop), new Vector2(CardW, TierSize * 1.2f));
            AddStroke(el.Tier);

            var artBg = MvUi.Box(el.Rt, "ArtBg", ArtBg);
            MvUi.Place(artBg.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -(top + ArtTop)), new Vector2(ArtSize, ArtSize));
            Frame(artBg.rectTransform, ArtLine, ArtInset);
            el.Art = Img(artBg.rectTransform, "Art", false);
            el.Art.preserveAspect = true;
            MvUi.Stretch(el.Art.rectTransform);

            el.Name = MvUi.Label(el.Rt, "Name", NameSize, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
            el.Name.horizontalOverflow = HorizontalWrapMode.Wrap;
            el.Name.lineSpacing = NameLine;
            MvUi.Place(el.Name.rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -(top + ArtTop + ArtSize + NameTop)), new Vector2(CardW - 2 * .6f * U, NameSize * NameLine * 2));
            AddStroke(el.Name);

            el.Flash = MvUi.Box(el.Rt, "Flash", new Color(1, 1, 1, 0));
            MvUi.Stretch(el.Flash.rectTransform);
            return el;
        }

        // ---------------- #buffs 가호 줄 ----------------

        void UpdateBuffs(RunState s, float dt)
        {
            _slotIds.Clear();
            foreach (var c in _order)
            {
                s.Cards.TryGetValue(c.id, out int n);
                if (n <= 0) continue;
                _counts.TryGetValue(c.id, out int before);
                if (n > before) _arrive[c.id] = 0; // 날아온 카드의 칸만 arrive(새 HTML ui.arrive)
                _counts[c.id] = n;
                _slotIds.Add(c.id);
            }
            while (_slots.Count < _slotIds.Count) _slots.Add(BuildSlot(_buffs, "Buff" + _slots.Count));
            for (int j = 0; j < _slots.Count; j++)
            {
                var el = _slots[j];
                bool on = j < _slotIds.Count;
                el.Rt.gameObject.SetActive(on);
                if (!on) continue;
                var card = _tables.Card.Get(_slotIds[j]);
                s.Cards.TryGetValue(card.id, out int n);
                el.Rt.anchoredPosition = SlotCenter(j);
                var tr = TierRow(card.tier);
                Color col = tr != null ? MvUi.Hex(tr.color) : Color.white;
                foreach (var b in el.Border) b.color = col;
                el.BadgeBg.color = col;
                Sprite(el.Icon, MvAtlas.UiCard, card.icon);
                el.Badge.gameObject.SetActive(n > 1);
                if (n > 1)
                {
                    el.BadgeText.text = "×" + n;
                    el.Badge.sizeDelta = new Vector2(el.BadgeText.preferredWidth + 2 * BadgePad, BadgeSize * 1.15f);
                }

                float scale = 1, bright = 1;
                if (_arrive.TryGetValue(card.id, out float at))
                {
                    float u = Mathf.Clamp01(at / ArriveSec), e = EaseOut(u);
                    scale = Mathf.Lerp(ArriveScale, 1, e);
                    bright = Mathf.Lerp(ArriveBright, 1, e);
                    if (u >= 1) _arrive.Remove(card.id); else _arrive[card.id] = at + dt;
                }
                el.Rt.localScale = Vector3.one * scale;
                SetFlash(el.Flash, bright);
            }
        }

        /// <summary>칸 j 중심(가호 줄 왼쪽 아래 기준). 줄바꿈은 아래에서 위로(wrap-reverse).</summary>
        static Vector2 SlotCenter(int j)
        {
            int per = SlotsPerRow, r = j / per, c = j % per;
            return new Vector2(c * (SlotSize + SlotGap) + SlotSize / 2, r * (SlotSize + SlotGap) + SlotSize / 2);
        }

        /// <summary>날아갈 칸: 같은 카드 칸, 없으면 마지막 칸 다음 자리(칸이 없으면 첫 자리).</summary>
        int FlyTargetIndex(CardData pick)
        {
            if (pick != null)
                for (int j = 0; j < _slotIds.Count; j++) if (_slotIds[j] == pick.id) return j;
            return _slotIds.Count;
        }

        SlotEl BuildSlot(RectTransform parent, string name)
        {
            var el = new SlotEl();
            var bg = MvUi.Box(parent, name, MvUi.Panel);
            el.Rt = MvUi.Place(bg.rectTransform, Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, new Vector2(SlotSize, SlotSize));
            el.Icon = Img(el.Rt, "Icon", false);
            el.Icon.preserveAspect = true;
            MvUi.Stretch(el.Icon.rectTransform);
            el.Border = Frame(el.Rt, Color.white, SlotBorder);
            el.Flash = MvUi.Box(el.Rt, "Flash", new Color(1, 1, 1, 0));
            MvUi.Stretch(el.Flash.rectTransform);
            el.BadgeBg = MvUi.Box(el.Rt, "Badge", Color.white);
            el.Badge = MvUi.Place(el.BadgeBg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(BadgeOut, -BadgeOut), new Vector2(BadgeSize, BadgeSize));
            el.BadgeText = MvUi.Label(el.Badge, "Text", BadgeSize, BadgeText, TextAnchor.MiddleCenter, FontStyle.Normal);
            MvUi.Stretch(el.BadgeText.rectTransform);
            return el;
        }

        // ---------------- 도구 ----------------

        static Image[] Frame(RectTransform rt, Color c, float w)
        {
            var e = new Image[4];
            e[0] = MvUi.Box(rt, "Top", c); MvUi.Place(e[0].rectTransform, new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, Vector2.zero);
            e[1] = MvUi.Box(rt, "Bottom", c); MvUi.Place(e[1].rectTransform, new Vector2(.5f, 0), new Vector2(.5f, 0), Vector2.zero, Vector2.zero);
            e[2] = MvUi.Box(rt, "Left", c); MvUi.Place(e[2].rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, Vector2.zero);
            e[3] = MvUi.Box(rt, "Right", c); MvUi.Place(e[3].rectTransform, new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, Vector2.zero);
            for (int i = 0; i < 4; i++)
            {
                var r = e[i].rectTransform;
                bool horiz = i < 2;
                r.anchorMin = horiz ? new Vector2(0, r.anchorMin.y) : new Vector2(r.anchorMin.x, 0);
                r.anchorMax = horiz ? new Vector2(1, r.anchorMax.y) : new Vector2(r.anchorMax.x, 1);
                r.sizeDelta = horiz ? new Vector2(0, w) : new Vector2(w, -2 * w);
            }
            return e;
        }

        /// <summary>그림 이름 없이 만든 아틀라스 그림(값은 <see cref="Sprite"/>가 넣는다).</summary>
        static AddressableImage Img(Transform parent, string name, bool sliced)
        {
            var img = MvUi.Rect(parent, name).gameObject.AddComponent<AddressableImage>();
            img.raycastTarget = false;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.enabled = false;
            return img;
        }

        static void AddStroke(Text t)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = Stroke;
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }

        /// <summary>CSS 밝기 b를 흰 덮개 알파 1 − 1/b로 흉내 낸다(UI 기본 셰이더는 곱하기만 됨).</summary>
        static void SetFlash(Image flash, float bright)
        {
            var c = flash.color;
            c.a = bright > 1 ? 1 - 1 / bright : 0;
            flash.color = c;
        }

        /// <summary>CSS 키프레임: 구간마다 ease-out.</summary>
        static float Keys(float[] t, float[] v, float u)
        {
            for (int i = 1; i < t.Length; i++)
                if (u <= t[i]) return Mathf.LerpUnclamped(v[i - 1], v[i], EaseOut((u - t[i - 1]) / (t[i] - t[i - 1])));
            return v[v.Length - 1];
        }

        static float EaseOut(float x) => CubicBezier(0, 0, .58f, 1, x);

        float Bezier(float x) => CubicBezier(_bx1, _by1, _bx2, _by2, x);

        /// <summary>CSS cubic-bezier: 시간 x → 진행 y(이분법).</summary>
        public static float CubicBezier(float x1, float y1, float x2, float y2, float x)
        {
            if (x <= 0) return 0;
            if (x >= 1) return 1;
            float lo = 0, hi = 1, t = x;
            for (int i = 0; i < 24; i++)
            {
                t = (lo + hi) / 2;
                if (B(x1, x2, t) < x) lo = t; else hi = t;
            }
            return B(y1, y2, t);
        }

        static float B(float a, float b, float t)
        {
            float u = 1 - t;
            return 3 * u * u * t * a + 3 * u * t * t * b + t * t * t;
        }
    }
}
