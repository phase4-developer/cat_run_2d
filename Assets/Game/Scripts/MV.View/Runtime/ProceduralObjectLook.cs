using System;
using System.Collections.Generic;
using MV.Data;
using MV.Run;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 프리팹이 없는 종류(105.prefab·305 빈칸)의 코드 모양 그림(정본 `drawObj` L3099 분기, 모양 = `105_objectKind.viewShape`).
    /// 그림 조각은 모두 <see cref="Look"/> 묶음 아래에 두고, 그 묶음이 이 뷰의 그림 묶음(<see cref="RunObjectView.Pose"/> 대상)이다.
    /// 맞음 반응: 흔들림(장애물·우물, `106.objShake`), 롤 관문 번쩍(`106.flash`), 우물 벽돌 빠짐(WellFlush). 눌림·쓰러짐은 <see cref="ObjectMotion"/>.
    /// 판정 형상이 없으므로 투사체에 맞지 않는다(피격 가능 종류는 프리팹이 있어야 한다).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralObjectLook : RunObjectModule
    {
        public const string StairArrow = "World_Obj_Stair_Arrow";
        public const string FlashEllipse = "Fx_FlashEllipse", Stripe = "Fx_Stripe", GlowLin = "Fx_Glow_Lin", Square = "Fx_Square";

        /// <summary>롤 관문 배경 높이(가운데 y = 1.4, 세로 반지름 1.4), 롤 반지름 0.25(L3633).</summary>
        const double GateCy = 1.4, GateRy = 1.4, GateRoll = 0.25;
        /// <summary>`Fx_Stripe.png`(64×16) 안 불투명 영역 56×4 px 비율 [측정]. 줄 길이·굵기를 이 영역으로 맞춘다.</summary>
        const float StripeFillX = 56f / 64f, StripeFillY = 4f / 16f;
        /// <summary>츄르 더미 6개 (dx, dy, rot) — 정본 L3109, 크기 단위 c = 1 m(캔버스 y 아래 +).</summary>
        static readonly double[] s_churu = { -0.42, -0.12, -0.5, 0.4, -0.1, 0.45, 0, -0.22, 0.08, -0.2, -0.46, -0.25, 0.24, -0.44, 0.35, 0.02, -0.7, -0.08 };
        static readonly string[] s_stringNames = { "String0", "String1", "String2" };
        static readonly string[] s_stripeNames = { "Stripe0", "Stripe1", "Stripe2", "Stripe3", "Stripe4" };

        ObjectShape _shape;
        Transform _look;
        readonly List<SpriteRenderer> _parts = new List<SpriteRenderer>(8);
        readonly List<Vector3> _base = new List<Vector3>(8);
        readonly List<int> _brickOrder = new List<int>(64);
        double[] _depth;
        int[] _order, _scratch;
        double[] _gone, _grow;
        double _rx, _ry;
        bool _gold, _hasJunkFlash;
        double _shake, _flash;

        public ObjectShape Shape => _shape;
        /// <summary>그림 묶음(모든 조각의 부모).</summary>
        public Transform Look => _look;
        /// <summary>흔들림·번쩍 남은 초.</summary>
        public double Shake => _shake;
        public double Flash => _flash;

        /// <summary>지금 빠져 있는(숨김) 우물 벽돌 수.</summary>
        public int GoneBricks
        {
            get
            {
                int n = 0;
                if (_gone != null) for (int i = 0; i < _gone.Length; i++) if (_gone[i] > 0) n++;
                return n;
            }
        }

        /// <summary>관리자: 코드 모양 뷰를 만들 때 한 번.</summary>
        public void Setup(RunObjectView view, ObjectShape shape)
        {
            _shape = shape;
            var go = new GameObject("Look");
            _look = go.transform;
            _look.SetParent(view.transform, false);
            view.AddPoseTarget(_look);
        }

        public override void OnBind(RunObjectView view)
        {
            _shake = _flash = 0;
            _hasJunkFlash = false;
            for (int i = 0; i < _parts.Count; i++) _parts[i].gameObject.SetActive(false);
            _base.Clear();
            Build(view.Ctx, view.Obj);
        }

        public override void OnUnbind(RunObjectView view) => _shake = _flash = 0;

        public override void OnRunEvent(RunObjectView view, in RunEvent e)
        {
            if (e.Type == RunEventType.Hit)
            {
                var r = view.Obj?.Reaction;
                if (r == null) return;
                if (r.objShake > 0 && (_shape == ObjectShape.Junk || _shape == ObjectShape.Well)) _shake = ObjectReaction.ShakeSec;
                if (r.flash && _shape == ObjectShape.RollGate) _flash = ObjectReaction.FlashSec;
            }
            else if (e.Type == RunEventType.WellFlush && _shape == ObjectShape.Well) FlushBricks(view.Ctx, Math.Max(1, e.A));
        }

        /// <summary>정본 `flushBricks` L1861: 보이는 벽돌을 층 높은 순(잡동사니 먼저)으로 늘어놓고 앞 10개 안에서 무작위로 n개를 뺀다.</summary>
        void FlushBricks(ObjectViewContext ctx, int n)
        {
            if (_gone == null) return;
            var bricks = ctx.WellBricks;
            _brickOrder.Clear();
            for (int layer = 5; layer >= 0; layer--)
                for (int i = 0; i < bricks.Count && i < _gone.Length; i++)
                    if (bricks[i].Layer == layer) _brickOrder.Add(i);
            ctx.Flush(_gone, _grow, n, _brickOrder);
        }

        // ───────── 만들기 ─────────

        static string SpriteAt(RunObject o, int i)
        {
            var list = o.Kind?.sprite;
            if (list == null || list.Count == 0) return null;
            return list[((i % list.Count) + list.Count) % list.Count];
        }

        SpriteRenderer Part(ObjectViewContext ctx, int slot, string atlas, string sprite, Vector3 local, int order, bool flipX = false)
        {
            while (_parts.Count <= slot)
            {
                var go = new GameObject("P" + _parts.Count);
                go.transform.SetParent(_look, false);
                go.AddComponent<Billboard>();
                var r = go.AddComponent<SpriteRenderer>();
                r.sharedMaterial = ctx.SpriteMaterial;
                _parts.Add(r);
            }
            while (_base.Count <= slot) _base.Add(Vector3.zero);
            var sr = _parts[slot];
            sr.sprite = ctx.Sprites != null ? ctx.Sprites.Get(atlas, sprite) : null;
            sr.flipX = flipX;
            sr.sortingOrder = order;
            sr.color = Color.white;
            sr.transform.localPosition = local;
            sr.transform.localScale = Vector3.one;
            sr.transform.localRotation = Quaternion.identity;
            sr.gameObject.SetActive(sr.sprite != null);
            _base[slot] = local;
            return sr;
        }

        void Build(ObjectViewContext ctx, RunObject o)
        {
            string atlas = ctx.Atlas, fx = ctx.FxAtlas;
            switch (_shape)
            {
                case ObjectShape.Junk: BuildJunk(ctx, o); break;
                case ObjectShape.RollGate: BuildRollGate(ctx, o); break;
                case ObjectShape.WarpGate: BuildWarpGate(ctx, o); break;
                case ObjectShape.Well: BuildWell(ctx, o); break;
                case ObjectShape.Stair:
                    // 정본 drawBlackHole L3203: 먼 단부터 i = 2, 1, 0. 단 앵커 z = 0.3 + i·0.8 − d/2(d 0.8).
                    for (int i = 2; i >= 0; i--) Part(ctx, 2 - i, atlas, SpriteAt(o, i), new Vector3(0, 0, (float)(0.3 + i * 0.8 - 0.4)), 2 - i);
                    Part(ctx, 3, atlas, StairArrow, new Vector3(0, 2.4f, 0.6f), 3);
                    break;
                case ObjectShape.CoinPile:
                    for (int i = 0; i < s_churu.Length / 3; i++)
                    {
                        var p = Part(ctx, i, atlas, SpriteAt(o, i), new Vector3((float)s_churu[i * 3], (float)-s_churu[i * 3 + 1], 0), i);
                        p.transform.localRotation = Quaternion.Euler(0, 0, (float)(-s_churu[i * 3 + 2] * Mathf.Rad2Deg));
                    }
                    break;
                case ObjectShape.CatnipPot:
                    Part(ctx, 0, atlas, SpriteAt(o, 0), Vector3.zero, 0);
                    Part(ctx, 1, fx, ObjectViewFactory.Star, new Vector3(0.1f, 1.37f, 0), 1);
                    break;
                default:
                    Part(ctx, 0, atlas, SpriteAt(o, 0), Vector3.zero, 0);
                    break;
            }
        }

        void BuildJunk(ObjectViewContext ctx, RunObject o)
        {
            ObjectLayout.JunkJitter(o.Idx, o.Bar, out double jx, out double sc);
            bool soul = o.Rew == FieldReward.Soul && o.IsField;
            string sprite = soul ? o.Kind.spriteSoul : SpriteAt(o, ObjectLayout.JunkSpriteIndex(Math.Max(0, o.Lane), Math.Max(0, o.Row), Math.Max(0, o.Bar), Math.Max(0, o.Idx)));
            var body = Part(ctx, 0, ctx.Atlas, sprite, new Vector3((float)jx, (float)ObjectLayout.JunkLiftY, (float)ObjectLayout.JunkFrontZ), 0);
            body.transform.localScale = Vector3.one * (float)sc;
            if (soul) return;
            // 맞음 흰 섬광(L3629): 그림 발 위 0.6q, 반지름 1.1q × 0.8q, 알파 0.35. 흔들리는 동안만 켠다.
            var fl = Part(ctx, 2, ctx.FxAtlas, FlashEllipse,
                new Vector3((float)jx, (float)(ObjectLayout.JunkLiftY + ObjectReaction.JunkFlashY * sc), (float)ObjectLayout.JunkFrontZ), 2);
            if (fl.sprite != null)
            {
                var b = fl.sprite.bounds.size;
                fl.transform.localScale = new Vector3((float)(2 * ObjectReaction.JunkFlashRx * sc) / Mathf.Max(0.0001f, b.x),
                    (float)(2 * ObjectReaction.JunkFlashRy * sc) / Mathf.Max(0.0001f, b.y), 1f);
                fl.color = new Color(1f, 1f, 1f, (float)ObjectReaction.JunkFlashAlpha);
                _hasJunkFlash = true;
            }
            fl.gameObject.SetActive(false);
            if (!o.IsField) return;
            Part(ctx, 1, ctx.Atlas, ObjectLayout.BadgeSprite(o.Rew),
                new Vector3((float)(jx + ObjectLayout.BadgeX * sc), (float)(ObjectLayout.JunkLiftY + ObjectLayout.BadgeY * sc), (float)ObjectLayout.JunkFrontZ), 1);
        }

        void BuildRollGate(ObjectViewContext ctx, RunObject o)
        {
            // 정본 drawRollGate L3632: 가운데 (0, 1.4), rx = hw·1.02, ry = 1.4, 롤 반지름 0.25. 배경 둥근 모서리는 사각형 근사.
            bool stairGate = o.Kind.key == "sgate";
            string tint = o.Kind.tint != null && o.Kind.tint.Count > 0 ? o.Kind.tint[0] : (stairGate ? "#6fd27a" : "#f08ab0");
            double rx = Math.Max(0.1, o.Hw) * 1.02, ry = GateRy, r = GateRoll, cy = GateCy;
            _rx = rx;
            _ry = ry;
            float x0 = (float)-rx, x1 = (float)rx, y0 = (float)-ry, y1 = (float)ry;
            var mb = ctx.Mesh;
            // 배경 그라데이션: 평소(아래 `55`)와 번쩍(아래 `aa`) 두 장을 만들어 켜고 끈다(런 중 메쉬 다시 쓰기 없음).
            for (int f = 0; f < 2; f++)
            {
                var bg = MeshChild(ctx, f == 0 ? "Bg" : "BgFlash");
                bg.localPosition = new Vector3(0, (float)cy, 0);
                mb.Clear();
                Color32 top = MvColor.WithAlpha(tint, ObjectReaction.GateBgTop),
                    bottom = MvColor.WithAlpha(tint, f == 0 ? ObjectReaction.GateBgBottom : ObjectReaction.GateBgBottomFlash);
                mb.AddQuad(new Vector3(x0, y0), new Vector3(x1, y0), new Vector3(x1, y1), new Vector3(x0, y1), bottom, bottom, top, top);
                bg.GetComponent<VertexColorFog>().Bind(mb, f == 0 ? "RollGateBg" : "RollGateBgFlash");
                bg.GetComponent<MeshRenderer>().sortingOrder = 0;
                bg.gameObject.SetActive(f == 0);
            }
            // 위로 흐르는 흰 줄 5개(L3635): 배경 위·롤 아래, 관문 면(XY 평면)에 둔다.
            for (int i = 0; i < ObjectReaction.GateStripes; i++)
            {
                var sr = Flat(ctx, s_stripeNames[i], ctx.FxAtlas, Stripe, 1);
                if (sr.sprite == null) continue;
                var b = sr.sprite.bounds.size;
                sr.transform.localScale = new Vector3((float)(rx * ObjectReaction.StripeHalfW * 2) / Mathf.Max(0.0001f, b.x * StripeFillX),
                    (float)ObjectReaction.StripeThick / Mathf.Max(0.0001f, b.y * StripeFillY), 1f);
                sr.color = new Color(1f, 1f, 1f, (float)ObjectReaction.StripeAlpha);
            }
            int slot = 0, ny = Math.Max(2, (int)Math.Round(ry * 2 / (r * 1.9))), nx = Math.Max(2, (int)Math.Round(rx * 2 / (r * 1.9)));
            for (int k = 0; k <= ny; k++)
            {
                float y = (float)(cy - ry + k * (ry * 2 / ny));   // 정본 c.y + ry − k·… (캔버스 아래 +) → 아래부터 위로
                Part(ctx, slot, ctx.Atlas, SpriteAt(o, slot), new Vector3(x0, y, 0), 2); slot++;
                Part(ctx, slot, ctx.Atlas, SpriteAt(o, slot), new Vector3(x1, y, 0), 2); slot++;
            }
            for (int k = 1; k < nx; k++) { Part(ctx, slot, ctx.Atlas, SpriteAt(o, slot), new Vector3((float)(-rx + k * (rx * 2 / nx)), (float)(cy + ry), 0), 2); slot++; }
        }

        /// <summary>빌보드가 아닌 평면 스프라이트 자식(이름으로 찾고 없으면 만든다). 관문 면(XY 평면)에 붙는 줄·흰 줄용. 그림이 없으면 끈다.</summary>
        SpriteRenderer Flat(ObjectViewContext ctx, string name, string atlas, string sprite, int order)
        {
            var t = _look.Find(name);
            SpriteRenderer sr;
            if (t == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(_look, false);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = ctx.SpriteMaterial;
            }
            else sr = t.GetComponent<SpriteRenderer>();
            sr.sprite = ctx.Sprites != null ? ctx.Sprites.Get(atlas, sprite) : null;
            sr.sortingOrder = order;
            sr.color = Color.white;
            sr.flipX = false;
            sr.transform.localPosition = Vector3.zero;
            sr.transform.localRotation = Quaternion.identity;
            sr.transform.localScale = Vector3.one;
            sr.gameObject.SetActive(sr.sprite != null);
            return sr;
        }

        Transform MeshChild(ObjectViewContext ctx, string name)
        {
            var t = _look.Find(name);
            if (t == null)
            {
                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(VertexColorFog));
                t = go.transform;
                t.SetParent(_look, false);
                go.GetComponent<MeshRenderer>().sharedMaterial = ctx.VertexColorMaterial;
            }
            t.gameObject.SetActive(true);
            return t;
        }

        void BuildWarpGate(ObjectViewContext ctx, RunObject o)
        {
            bool gold = o.Kind.key == "archGoal";
            string p1 = gold ? WarpGateLayout.P1Gold : WarpGateLayout.P1, p2 = gold ? WarpGateLayout.P2Gold : WarpGateLayout.P2;
            var boxes = WarpGateLayout.Boxes;
            var mb = ctx.Mesh;
            for (int i = 0; i < boxes.Length; i++)
            {
                var b = boxes[i];
                var t = MeshChild(ctx, "Box" + i);
                if (t.GetComponent<Billboard>() == null) t.gameObject.AddComponent<Billboard>();
                t.localPosition = new Vector3((float)b.X, (float)b.Y, (float)(-b.D / 2));   // 앵커 = 앞면 아래 가운데
                mb.Clear();
                BoxBillboardBuilder.Build(mb, b.W, b.H, b.D, p1, p2, double.PositiveInfinity, ctx.Cfg.OutlineColor);
                t.GetComponent<VertexColorFog>().Bind(mb, "GateBox");
                t.GetComponent<MeshRenderer>().sortingOrder = i == 4 ? 3 : 1;
            }
            // 기둥(원근 기울기 포함 그림, 왼쪽 flipX)은 받침 위·중간 발판 아래(정본 순서: 받침 → 기둥 → 중간 발판).
            Part(ctx, 0, ctx.Atlas, SpriteAt(o, 0), new Vector3((float)-WarpGateLayout.PostX, 0, 0), 0, true);
            Part(ctx, 1, ctx.Atlas, SpriteAt(o, 0), new Vector3((float)WarpGateLayout.PostX, 0, 0), 0);
            // 매달린 장난감 줄(L3221, 관문 면 XY 평면) → 공. 줄은 발판 위, 공은 줄 위.
            for (int i = 0; i < 3; i++)
            {
                var str = Flat(ctx, s_stringNames[i], ctx.FxAtlas, Square, 4);
                if (str.sprite != null) str.color = MvColor.ToColor32(ctx.Cfg.OutlineColor);
                Part(ctx, 2 + i, ctx.Atlas, "World_Obj_Arch_Toy_" + i, new Vector3((float)WarpGateLayout.ToyDx[i], (float)ObjectReaction.ToyBallY, 0), 5);
            }
            // 금빛 관문: 광륜(L3215, 맨 먼저)·기둥 위 별(L3223, 맨 나중).
            _gold = gold;
            if (!gold) return;
            var glow = Part(ctx, 5, ctx.FxAtlas, GlowLin, new Vector3(0, (float)ObjectReaction.GlowY, 0), -1);
            if (glow.sprite != null)
            {
                glow.transform.localScale = Vector3.one * ((float)(ObjectReaction.GlowR * 2) / Mathf.Max(0.0001f, glow.sprite.bounds.size.x));
                var c = (Color)MvColor.ToColor32(ObjectReaction.GlowColor);
                c.a = 1f;   // Fx_Glow_Lin 중심 알파 ≈ 0.45(정본 0.55) [근사]
                glow.color = c;
            }
            for (int s = 0; s < 2; s++)
            {
                var star = Part(ctx, 6 + s, ctx.FxAtlas, ObjectViewFactory.Star, new Vector3((float)(s == 0 ? -WarpGateLayout.PostX : WarpGateLayout.PostX), (float)ObjectReaction.StarY, 0), 6);
                star.color = MvColor.ToColor32(ObjectReaction.StarColor);
            }
        }

        void BuildWell(ObjectViewContext ctx, RunObject o)
        {
            var bricks = ctx.WellBricks;
            Part(ctx, 0, ctx.Atlas, SpriteAt(o, 0), new Vector3(0, 1.25f, 0), 0);
            int n = bricks.Count;
            if (_depth == null || _depth.Length != n) { _depth = new double[n]; _order = new int[n]; _scratch = new int[n]; }
            if (_gone == null || _gone.Length != n) { _gone = new double[n]; _grow = new double[n]; }
            for (int i = 0; i < n; i++) { _gone[i] = 0; _grow[i] = 1; }
            for (int i = 0; i < n; i++)
            {
                var b = bricks[i];
                Part(ctx, 1 + i, ctx.Atlas, (b.Junk ? "World_Obj_Well_Junk_" : "World_Obj_Well_Brick_") + b.Variant, new Vector3((float)b.X, (float)b.Y, (float)b.Z), 1 + i);
            }
        }

        // ───────── 매 프레임 ─────────

        public override void OnTick(RunObjectView view, in ObjectFrame f)
        {
            var o = view.Obj;
            double t = f.T, nowMs = t * 1000.0;
            if (_shake > 0) _shake = Math.Max(0, _shake - f.Dt);
            if (_flash > 0) _flash = Math.Max(0, _flash - f.Dt);
            switch (_shape)
            {
                case ObjectShape.WarpGate:
                    bool shown = WarpGateLayout.Alpha(f.PlayerZ - o.Z, out double a);
                    view.Pose.Alpha *= shown ? (float)a : 0f;
                    for (int i = 0; i < 3; i++)
                    {
                        var b = _base[2 + i];
                        double sw = WarpGateLayout.ToySway(i, t);
                        _parts[2 + i].transform.localPosition = new Vector3(b.x + (float)sw, b.y, b.z);
                        PlaceString(_look.Find(s_stringNames[i]), WarpGateLayout.ToyDx[i], sw);
                    }
                    if (_gold)
                        for (int s = 0; s < 2 && 6 + s < _parts.Count; s++)
                        {
                            var star = _parts[6 + s];
                            if (star.sprite == null) continue;
                            star.transform.localScale = Vector3.one * (float)(2 * ObjectReaction.StarRadius(t) / Mathf.Max(0.0001f, star.sprite.bounds.size.x));
                        }
                    break;
                case ObjectShape.Stair:
                    var ab = _base[3];
                    _parts[3].transform.localPosition = new Vector3(ab.x, ab.y + Mathf.Sin((float)(t * 5)) * 0.25f, ab.z);
                    break;
                case ObjectShape.CoinPile:
                {
                    // 크기 k = 0.55 + 0.45·hp/max(L1525): 츄르 자리·크기 모두 k배(정본 c = s·body.scale.y).
                    float k = o.HasHp ? (float)ObjectReaction.PickupScale(o.Hp, o.Max, o.Dead) : 1f;
                    for (int i = 0; i < _parts.Count && i < 6; i++)
                    {
                        var b = _base[i];
                        _parts[i].transform.localPosition = new Vector3(b.x * k, (b.y - (float)(Math.Sin(nowMs / 500.0 + i) * 0.02)) * k, b.z);
                        _parts[i].transform.localScale = Vector3.one * k;
                    }
                    break;
                }
                case ObjectShape.CatnipPot:
                {
                    float k = o.HasHp ? (float)ObjectReaction.PickupScale(o.Hp, o.Max, o.Dead) : 1f;
                    if (_parts.Count > 0) _parts[0].transform.localScale = Vector3.one * k;
                    if (_parts.Count > 1 && _parts[1].sprite != null)
                    {
                        float r = (float)(0.14 * (0.8 + 0.2 * Math.Sin(nowMs / 200.0)));
                        var b = _base[1];
                        _parts[1].transform.localPosition = new Vector3(b.x * k, b.y * k, b.z);
                        _parts[1].transform.localScale = Vector3.one * (2 * r * k / Mathf.Max(0.0001f, _parts[1].sprite.bounds.size.x));
                    }
                    break;
                }
                case ObjectShape.Well:
                    AnimateBricks(f.Dt);
                    SortBricks(view.Ctx.Cam);
                    break;
                case ObjectShape.Junk:
                {
                    // 벽 흔들림(L1484): 그림 전체 x += sin(shake·90)·0.05, 흔들리는 동안 흰 섬광(L3629).
                    bool on = _shake > 0;
                    if (on) view.Pose.Offset += new Vector3((float)ObjectReaction.WallShakeX(_shake), 0, 0);
                    if (_hasJunkFlash && _parts.Count > 2 && _parts[2].gameObject.activeSelf != on) _parts[2].gameObject.SetActive(on);
                    break;
                }
                case ObjectShape.RollGate:
                    AnimateRollGate(view, o, t);
                    break;
            }
        }

        /// <summary>장난감 줄: 발판 (dx, 4.6) → 공 (dx + sw, 3.3) 선분을 관문 면 위 얇은 사각형으로.</summary>
        static void PlaceString(Transform str, double dx, double sw)
        {
            if (str == null || !str.gameObject.activeSelf) return;
            var sr = str.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;
            double ax = dx, ay = ObjectReaction.ToyTopY, bx = dx + sw, by = ObjectReaction.ToyBallY;
            double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            var size = sr.sprite.bounds.size;
            str.localPosition = new Vector3((float)((ax + bx) / 2), (float)((ay + by) / 2), 0);
            str.localRotation = Quaternion.Euler(0, 0, (float)(Math.Atan2(by - ay, bx - ax) * 180 / Math.PI) + 90f);
            str.localScale = new Vector3((float)ObjectReaction.ToyStringThick / Mathf.Max(0.0001f, size.x), (float)len / Mathf.Max(0.0001f, size.y), 1f);
        }

        /// <summary>우물: 벽돌 숨김·다시 자람(L1474~1475)과 더미 흔들림 x(L1477).</summary>
        void AnimateBricks(double dt)
        {
            if (_gone == null) return;
            float sx = (float)ObjectReaction.WellShakeX(_shake);
            int n = Math.Min(_gone.Length, _parts.Count - 1);
            for (int i = 0; i < n; i++)
            {
                double sc = ObjectReaction.BrickStep(ref _gone[i], ref _grow[i], dt);
                var tr = _parts[1 + i].transform;
                var b = _base[1 + i];
                tr.localPosition = new Vector3(b.x + sx, b.y, b.z);
                tr.localScale = Vector3.one * (float)sc;
            }
        }

        /// <summary>롤 관문: 번쩍 배경 바꾸기, 흰 줄 흐름, 통과 뒤 가라앉기(안쪽 y −sink·6, 0.5 넘으면 숨김, L1503).</summary>
        void AnimateRollGate(RunObjectView view, RunObject o, double t)
        {
            float dy = (float)ObjectReaction.GateSinkY(o.Sink);
            if (ObjectReaction.GateHidden(o.Sink)) view.Pose.Alpha = 0f;
            bool flash = _flash > 0;
            var bg = _look.Find("Bg");
            if (bg != null)
            {
                bg.localPosition = new Vector3(0, (float)GateCy + dy, 0);
                if (bg.gameObject.activeSelf == flash) bg.gameObject.SetActive(!flash);
            }
            var bgFlash = _look.Find("BgFlash");
            if (bgFlash != null)
            {
                bgFlash.localPosition = new Vector3(0, (float)GateCy + dy, 0);
                if (bgFlash.gameObject.activeSelf != flash) bgFlash.gameObject.SetActive(flash);
            }
            for (int i = 0; i < ObjectReaction.GateStripes; i++)
            {
                var s = _look.Find(s_stripeNames[i]);
                if (s == null || !s.gameObject.activeSelf) continue;
                s.localPosition = new Vector3(0, (float)(GateCy + ObjectReaction.StripeY(i, t, _ry)) + dy, 0);
            }
            if (dy != 0)
                for (int i = 0; i < _parts.Count && i < _base.Count; i++)
                {
                    var b = _base[i];
                    _parts[i].transform.localPosition = new Vector3(b.x, b.y + dy, b.z);
                }
        }

        /// <summary>우물 벽돌 그룹 안 정렬: 카메라 깊이 내림차순(먼 것 먼저, 정본 `list.sort((a,b)=>b.d−a.d)`).</summary>
        void SortBricks(Transform cam)
        {
            if (cam == null || _depth == null) return;
            Vector3 cp = cam.position, fwd = cam.forward;
            int n = _depth.Length;
            for (int i = 0; i < n; i++) _depth[i] = Vector3.Dot(_parts[1 + i].transform.position - cp, fwd);
            DepthMath.RankFarFirst(_depth, n, _order, _scratch);
            for (int i = 0; i < n; i++) _parts[1 + i].sortingOrder = 1 + _order[i];
        }
    }
}
