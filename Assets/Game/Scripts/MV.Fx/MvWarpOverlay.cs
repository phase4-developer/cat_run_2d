using System;
using MV.View;
using UnityEngine;

namespace MV.Fx
{
    /// <summary>
    /// 우다다 화면(06 §6.11). `GameCamera` 앞 1 m(02 §2.1.6 ScreenRig 규칙: 로컬 단위 = 기준 px, 스케일 2·tan(vFOV/2)/H)에
    /// 동적 메쉬 3개: `Warp`(하늘·별·해달·지붕·계단·빛), `WarpOut`(종료 흰 섬광·링), `SpeedLine`(집중선).
    /// 고양이 다시 그리기(`WarpCat`)는 MV.View 몫, 단계 이름 글자는 <see cref="StageNameChanged"/>로 UI가 그린다(TMP).
    /// 시간: 단계 진행·흐름은 simDt, 별·이름·종료·집중선은 실시간(06 §6.10).
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class MvWarpOverlay : MonoBehaviour
    {
        static readonly string[] Walls = { "#f0e4d0", "#e8d0c0", "#d8e0e8", "#f4e8c8" };
        static readonly string[] Roofs = { "#d0704a", "#5a7ab8", "#6a9a7a", "#a87a6a" };
        const string Olc = "#4a3446";

        [SerializeField] Material _normal;
        [SerializeField] Material _add;
        Camera _cam;
        MvWarpModel _model;
        readonly MvFxMesh _bWarp = new MvFxMesh(), _bOut = new MvFxMesh(), _bLine = new MvFxMesh();
        Mesh _mWarp, _mOut, _mLine;
        bool _on;
        double _t, _stageAt = -1e9, _outAt = -1e9, _now;
        int _stage = -1;
        string _tierColor = "#ffffff";

        /// <summary>단계 이름 표시 시작(단계 번호). UI가 330 행의 이름 문구로 1.8 s 그린다(알파·크기는 <see cref="MvWarpModel"/>).</summary>
        public event Action<int> StageNameChanged;

        public bool Active => _on;
        public int Stage => _stage;

        public void Init(Camera cam, MvWarpModel model, Material normal, Material add)
        {
            _cam = cam;
            _model = model;
            if (normal != null) _normal = normal;
            if (add != null) _add = add;
            transform.SetParent(cam.transform, false);
            transform.localPosition = new Vector3(0, 0, 1);
            transform.localRotation = Quaternion.identity;
            _mWarp = Layer("Warp", out _);
            _mOut = Layer("WarpOut", out _);
            _mLine = Layer("SpeedLine", out _);
        }

        Mesh Layer(string sortingLayer, out MeshRenderer mr)
        {
            var go = new GameObject(sortingLayer);
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { _normal, _add };
            mr.sortingLayerName = sortingLayer;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var m = new Mesh { name = "Warp_" + sortingLayer };
            m.MarkDynamic();
            mf.sharedMesh = m;
            return m;
        }

        public void Begin()
        {
            _on = true;
            _t = 0;
            _stage = -1;
            _tierColor = "#ffffff";
        }

        public void SetStage(int st)
        {
            if (st == _stage) return;
            _stage = st;
            _stageAt = _now;
            StageNameChanged?.Invoke(st);
        }

        public void SetTierColor(string hex) => _tierColor = string.IsNullOrEmpty(hex) ? "#ffffff" : hex;

        public void End()
        {
            _on = false;
            _outAt = _now;
        }

        void LateUpdate()
        {
            if (_cam == null || _model == null) return;
            _now += Time.unscaledDeltaTime;
            if (_on) _t += MvFxClock.SimDt;
            float H = Mathf.Max(1, _cam.pixelHeight), W = H * _cam.aspect;
            float scale = 2f * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / H;
            transform.localScale = new Vector3(scale, scale, 1);
            _bWarp.Clear(); _bOut.Clear(); _bLine.Clear();
            if (_on) { DrawWarp(W, H); DrawLines(W, H); }
            double wo = (_now - _outAt) / _model.OutSec;
            if (wo >= 0 && wo < 1) DrawOut(wo, W, H);
            _bWarp.Apply(_mWarp); _bOut.Apply(_mOut); _bLine.Apply(_mLine);
        }

        // 캔버스 좌표(왼위 원점, y 아래) → 로컬(가운데 원점, y 위)
        static Vector3 P(double x, double y, float W, float H) => new Vector3((float)(x - W * 0.5), (float)(H * 0.5 - y), 0);

        static Color32 Col(string hex, double a = 1) => MvColor.WithAlpha(hex, Math.Max(0, Math.Min(1, a)));

        void Rect(MvFxMesh b, double x, double y, double w, double h, Color32 c, float W, float H, bool add = false, FxShape shape = FxShape.Solid, float param = 0)
        {
            var center = P(x + w * 0.5, y + h * 0.5, W, H);
            b.Quad(center, new Vector3((float)(w * 0.5), 0, 0), new Vector3(0, (float)(h * 0.5), 0), c, shape, param, add);
        }

        void DrawWarp(float W, float H)
        {
            int st = Math.Max(0, _stage);
            var S = _model.Get(st);
            double a = _model.Alpha(_t), T = _t, sp = MvWarpModel.Speed(st, T), VX = W / 2.0, VY = H * _model.VanishY, night = S.Night;
            // 하늘: 0 → 0.6 → 1 (높이 H·0.6), 그 아래 sky3
            var c1 = Col(S.Sky1, a); var c2 = Col(S.Sky2, a); var c3 = Col(S.Sky3, a);
            _bWarp.Quad4(P(0, H * 0.36, W, H), P(W, H * 0.36, W, H), P(W, 0, W, H), P(0, 0, W, H), c2, c2, c1, c1);
            _bWarp.Quad4(P(0, H * 0.6, W, H), P(W, H * 0.6, W, H), P(W, H * 0.36, W, H), P(0, H * 0.36, W, H), c3, c3, c2, c2);
            _bWarp.Quad4(P(0, H, W, H), P(W, H, W, H), P(W, H * 0.6, W, H), P(0, H * 0.6, W, H), c3, c3, c3, c3);
            // 별
            if (night > 0.5)
                for (int i = 0; i < 50; i++)
                {
                    double tw = 0.5 + 0.5 * Math.Sin(_now * 3 + i);
                    Rect(_bWarp, MvWarpModel.Hash(i * 1.7) * W, MvWarpModel.Hash(i * 2.3) * H * 0.45, 2, 2, Col("#ffffff", a * (0.3 + 0.6 * tw)), W, H);
                }
            // 해·달
            if (st == 0) Disc(W * 0.8, H * 0.09, W * 0.07, Col("#fff3b0", a), W, H);
            else if (st == 1)
            {
                Glow(VX, VY, W * 0.45, Col("#ffc878", a * 0.8), W, H);
                Disc(VX, VY + H * 0.01, W * 0.12, Col("#ffb070", a), W, H);
            }
            else
            {
                double mr = st == 3 ? W * 0.16 + Math.Min(W * 0.08, T * 1.5) : W * 0.08, mx = st == 3 ? VX : W * 0.78, my = st == 3 ? VY - H * 0.03 : H * 0.1;
                Glow(mx, my, mr * 3, Col("#fff6d0", a * 0.55), W, H);
                Disc(mx, my, mr, Col("#fff6d0", a), W, H);
                if (st == 2) Disc(mx + mr * 0.45, my - mr * 0.22, mr * 0.9, Col(S.Sky1, a), W, H);
            }
            // 지붕
            for (int sdI = 0; sdI < 2; sdI++)
            {
                int sd = sdI == 0 ? -1 : 1;
                for (int i = 0; i < _model.RoofCount; i++)
                {
                    _model.Roof(i, sd, T, sp, W, H, out var x0, out var y, out var bw, out var bh, out _, out var hh);
                    string wc = MvColor.Mix(Walls[(int)Math.Floor(hh * 4) % 4], "#2a2a5a", night);
                    string rc = st == 3 ? "#f4f8ff" : MvColor.Mix(Roofs[(int)Math.Floor(hh * 13) % 4], "#1a1a3a", night * 0.8);
                    Rect(_bWarp, x0, y - bh, bw, bh, Col(wc, a), W, H);
                    _bWarp.Quad4(P(x0 - bw * 0.08, y - bh, W, H), P(x0 + bw * 1.08, y - bh, W, H), P(x0 + bw * 0.92, y - bh * 1.35, W, H), P(x0 + bw * 0.08, y - bh * 1.35, W, H),
                        Col(rc, a), Col(rc, a), Col(rc, a), Col(rc, a));
                    if (night > 0.5 && hh > 0.3) Rect(_bWarp, x0 + bw * 0.3, y - bh * 0.7, bw * 0.2, bh * 0.25, Col("#ffd878", a), W, H);
                }
            }
            // 계단
            int N = _model.StepCount;
            double ph0 = (T * sp * 1.5) % 1;
            int bas = (int)Math.Floor(T * sp * 1.5);
            string stepC = MvColor.Mix("#f2ece0", "#3a3a6a", night * 0.6), rollC = MvColor.Mix("#fffdf8", "#5a5a8a", night * 0.5), holeC = MvColor.Mix("#c8bca8", "#2a2a4a", night * 0.5);
            for (int j = 0; j < N; j++)
            {
                double k0 = (j + ph0) / N, k1 = (j + 1 + ph0) / N;
                if (k0 > 1.05) continue;
                _model.StepAt(k0, W, H, out var y0, out var w0);
                _model.StepAt(Math.Min(1.1, k1), W, H, out var y1, out var w1);
                double bh = y1 - y0;
                if (bh < 1.2) continue;
                var sc = Col(stepC, a);
                _bWarp.Quad4(P(VX - w1, y1, W, H), P(VX + w1, y1, W, H), P(VX + w0, y0, W, H), P(VX - w0, y0, W, H), sc, sc, sc, sc);
                int n = 7;
                double rr = Math.Min(bh * 0.36, w0 * 2 / n * 0.46), yy = y0 + bh * 0.42;
                if (rr > 1.5)
                    for (int i = 0; i < n; i++)
                    {
                        double x = VX - w0 + (i + 0.5) * (w0 * 2 / n);
                        Disc(x, yy, rr, Col(rollC, a), W, H, (float)(Math.Max(0.8, rr * 0.12) / rr));
                        Disc(x, yy, rr * 0.35, Col(holeC, a), W, H);
                    }
                double band = Math.Max(1.5, bh * 0.12);
                string bc = (N - j + bas) % 4 == 0 ? _tierColor : MvColor.Shade(stepC, -0.1);
                Rect(_bWarp, VX - w1, y1 - band, w1 * 2, band, Col(bc, a), W, H);
            }
            // 소실점 빛(가산)
            Glow(VX, VY, W * 0.25 * 2, Col(S.Accent, a * 0.7), W, H, true);
        }

        void DrawLines(float W, float H)
        {
            long t = (long)Math.Floor(_now * 1000 / 40);
            var c = Col("#ffffff", 0.6);
            for (int i = 0; i < _model.SpeedLines; i++)
            {
                MvWarpModel.SpeedLine(i, t, W, H, out var ang, out var r0, out var r1, out var w);
                var a = P(W / 2.0 + Math.Cos(ang) * r0, H * 0.5 + Math.Sin(ang) * r0, W, H);
                var b = P(W / 2.0 + Math.Cos(ang) * r1, H * 0.5 + Math.Sin(ang) * r1, W, H);
                _bLine.Line(a, b, (float)w, Vector3.forward, c, false);
            }
        }

        void DrawOut(double wo, float W, float H)
        {
            MvWarpModel.Out(wo, W, H, out var whiteA, out var ringW, out var ringR);
            Rect(_bOut, 0, 0, W, H, Col("#ffffff", whiteA), W, H);
            var S = _model.Get(Math.Max(0, _stage));
            double r = ringR + ringW * 0.5;
            var center = P(W / 2.0, H * 0.62, W, H);
            _bOut.Quad(center, new Vector3((float)r, 0, 0), new Vector3(0, (float)r, 0), Col(S.Accent ?? "#ffffff", 1 - wo), FxShape.Ellipse, (float)(ringW / Math.Max(1, r)), false);
        }

        void Disc(double x, double y, double r, Color32 c, float W, float H, float outline = 0)
        {
            _bWarp.Quad(P(x, y, W, H), new Vector3((float)r, 0, 0), new Vector3(0, (float)r, 0), c, FxShape.Disc, outline, false);
        }

        void Glow(double x, double y, double r, Color32 c, float W, float H, bool add = false)
        {
            _bWarp.Quad(P(x, y, W, H), new Vector3((float)r, 0, 0), new Vector3(0, (float)r, 0), c, FxShape.Glow, 0, add);
        }

        void OnDestroy()
        {
            if (_mWarp != null) Destroy(_mWarp);
            if (_mOut != null) Destroy(_mOut);
            if (_mLine != null) Destroy(_mLine);
        }
    }
}
