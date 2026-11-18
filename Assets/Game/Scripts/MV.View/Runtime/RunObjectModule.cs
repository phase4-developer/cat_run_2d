using System;
using System.Collections.Generic;
using MV.Data;
using MV.Run;
using UnityEngine;

namespace MV.View
{
    /// <summary>한 프레임의 오브젝트 연출 시각. <see cref="T"/> = 실시간 초, <see cref="Dt"/> = 지난 프레임부터 초(최대 0.1), <see cref="PlayerZ"/> = 플레이어 z.</summary>
    public readonly struct ObjectFrame
    {
        public readonly double T, Dt, PlayerZ;

        public ObjectFrame(double t, double dt, double playerZ)
        {
            T = t;
            Dt = dt;
            PlayerZ = playerZ;
        }
    }

    /// <summary>
    /// 오브젝트 프리팹에 붙이는 기능 모듈(흔들림·눌림·쓰러짐·빛·반짝이·프레임 넘김 등). 프리팹에 붙은 모듈 목록이 그 오브젝트의 동작이다.
    /// 생성·연결·회수는 관리자(<see cref="ObjectViewFactory"/>)가 하고, 모듈은 자기 그림 자식만 다룬다. 모듈마다 Update는 없고
    /// <see cref="RunObjectView"/>가 같은 순서로 부른다: <see cref="OnBind"/>(연결) → 매 프레임 <see cref="OnTick"/> → <see cref="OnUnbind"/>(회수, 프리팹 기본으로 되돌림).
    /// 게임 규칙(피해·보상·관통)은 시뮬(MV.Run)이 정본이고 모듈은 그 결과(<see cref="RunObject"/> 값·<see cref="RunEvent"/>)를 그림으로만 보인다.
    /// </summary>
    public abstract class RunObjectModule : MonoBehaviour
    {
        /// <summary>쓰러지는 중(paperDie)에도 <see cref="OnTick"/>을 받을지. 기본은 받지 않는다(쓰러지는 그림이 다른 연출에 흔들리지 않게).</summary>
        public virtual bool TicksWhileDying => false;

        /// <summary>이 모듈이 지금 회수를 막는지(예: 쓰러지는 그림이 끝나기 전). 관리자는 모든 모듈이 false일 때만 회수한다.</summary>
        public virtual bool HoldsRelease => false;

        /// <summary>오브젝트에 연결됨(풀에서 꺼낼 때마다). 상태를 처음으로 둔다.</summary>
        public virtual void OnBind(RunObjectView view) { }

        /// <summary>매 프레임. 그림 묶음 자세는 <see cref="RunObjectView.Pose"/>에 더하고 곱한다(뷰가 끝에 한 번 적용).</summary>
        public virtual void OnTick(RunObjectView view, in ObjectFrame f) { }

        /// <summary>이 오브젝트의 런 이벤트(Hit·Break·WellFlush 등, 06 §6.13).</summary>
        public virtual void OnRunEvent(RunObjectView view, in RunEvent e) { }

        /// <summary>회수됨(풀로 돌아감). 바꾼 그림 자식을 프리팹 기본으로 되돌린다.</summary>
        public virtual void OnUnbind(RunObjectView view) { }
    }

    /// <summary>
    /// 그림 묶음 자세(한 프레임 합). 모듈이 더하고 곱한 값을 <see cref="RunObjectView"/>가 그림 묶음(프리팹 빌보드, 코드 모양은 Look)에 한 번 적용한다.
    /// 판정 Hitbox는 묶음 밖(프리팹 뿌리 아래)이라 이 값으로 움직이지 않는다.
    /// </summary>
    public sealed class ObjectPose
    {
        public Vector3 Offset;
        public Vector3 Scale = Vector3.one;
        /// <summary>화면 평면 기울기(도, 반시계 +).</summary>
        public float Roll;
        /// <summary>그룹 알파 배율(<see cref="DepthSortable.SetAlphaScale"/>).</summary>
        public float Alpha = 1f;

        public void Reset()
        {
            Offset = Vector3.zero;
            Scale = Vector3.one;
            Roll = 0f;
            Alpha = 1f;
        }
    }

    /// <summary>
    /// 오브젝트 뷰들이 함께 쓰는 공통 자원(관리자가 하나 만들어 넘긴다). 오브젝트별 동작은 여기 없다.
    /// </summary>
    public sealed class ObjectViewContext
    {
        public ViewConfig Cfg;
        public Transform Cam;
        public WorldSpriteCache Sprites;
        /// <summary>지금 테마 아틀라스(`304_theme.worldAtlas`)와 fx 아틀라스.</summary>
        public string Atlas, FxAtlas;
        public Material SpriteMaterial, VertexColorMaterial, LabelMaterial;
        /// <summary>투사체 판정 등록처. 없으면(시험·로비) 등록하지 않는다.</summary>
        public RunHitWorld Hits;
        /// <summary>결계 블록 글자판 「+골드」 배율(정본 `goldMul()` L1492).</summary>
        public double GoldMul = 1;
        /// <summary>글자판 문구 덮어쓰기(시험·Localization). 있으면 이 값이 한 줄 글자, null이면 글자판을 숨긴다.</summary>
        public Func<RunObject, string> LabelText;
        public readonly WallRowIndex WallRows = new WallRowIndex();
        /// <summary>표시 전용 난수(정본 Math.random). 규칙 난수와 무관.</summary>
        public readonly System.Random FxRng = new System.Random(0x5EED);
        public readonly MeshBuffer Mesh = new MeshBuffer();
        public readonly List<WellBrick> WellBricks = new List<WellBrick>(64);
        readonly List<int> _pick = new List<int>(64);

        public ObjectViewContext() => ObjectLayout.WellBricks(WellBricks);

        /// <summary>
        /// 정본 `flushBricks` L1861: <paramref name="order"/> 순서로 늘어놓은 보이는 칸 중 앞 <see cref="ObjectReaction.BrickPickTop"/>개 안에서 무작위로 n개를 뺀다
        /// (숨김 <see cref="ObjectReaction.BrickGoneSec"/>초, 다시 자람 0부터). <paramref name="order"/>가 null이면 0..len−1 순서.
        /// </summary>
        public void Flush(double[] gone, double[] grow, int n, IReadOnlyList<int> order = null)
        {
            if (gone == null) return;
            _pick.Clear();
            if (order == null) { for (int i = 0; i < gone.Length; i++) if (gone[i] <= 0) _pick.Add(i); }
            else for (int k = 0; k < order.Count; k++) if (gone[order[k]] <= 0) _pick.Add(order[k]);
            for (int k = 0; k < n && _pick.Count > 0; k++)
            {
                int at = Math.Min(_pick.Count - 1, (int)Math.Floor(FxRng.NextDouble() * Math.Min(_pick.Count, ObjectReaction.BrickPickTop)));
                int b = _pick[at];
                _pick.RemoveAt(at);
                gone[b] = ObjectReaction.BrickGoneSec;
                grow[b] = 0;
            }
        }

        /// <summary>fx 아틀라스 별(`Fx_Star4`) 반짝이 <paramref name="n"/>개를 <paramref name="parent"/> 아래에 만든다. 정렬은 그룹 안 그림 위(50).</summary>
        public SpriteRenderer[] MakeSparks(Transform parent, int n, Color color, bool billboard)
        {
            var sprite = Sprites != null ? Sprites.Get(FxAtlas, ObjectViewFactory.Star) : null;
            var arr = new SpriteRenderer[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Spark" + i);
                go.transform.SetParent(parent, false);
                if (billboard) go.AddComponent<Billboard>();
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                if (SpriteMaterial != null) sr.sharedMaterial = SpriteMaterial;
                sr.color = color;
                sr.sortingOrder = 50;
                arr[i] = sr;
            }
            return arr;
        }

        /// <summary>반짝이 한 개를 자리 <paramref name="local"/>, 반지름 <paramref name="radius"/> m로.</summary>
        public static void SetSpark(SpriteRenderer sr, Vector3 local, double radius)
        {
            if (sr == null) return;
            var tr = sr.transform;
            tr.localPosition = local;
            float w = sr.sprite != null ? Mathf.Max(0.0001f, sr.sprite.bounds.size.x) : 1f;
            tr.localScale = Vector3.one * (float)(2 * Math.Max(0, radius) / w);
        }

        /// <summary>색 알파에 비율을 곱한다(0~1로 자름).</summary>
        public static Color Alpha(Color c, double ratio)
        {
            c.a = Mathf.Clamp01(c.a * (float)ratio);
            return c;
        }
    }

    /// <summary>
    /// 결계 블록 글자판 「앞쪽 2칸만」(정본 L1488)에 쓰는 줄 색인: 같은 골목·같은 줄(bar·lane)마다 살아 있는 블록 행 번호. 프레임마다 다시 모은다.
    /// </summary>
    public sealed class WallRowIndex
    {
        readonly Dictionary<int, List<int>> _rows = new Dictionary<int, List<int>>(16);

        public void Collect(IReadOnlyList<RunObject> objs)
        {
            foreach (var l in _rows.Values) l.Clear();
            if (objs == null) return;
            for (int i = 0; i < objs.Count; i++)
            {
                var o = objs[i];
                if (o == null || o.Dead || o.Kind == null || o.Kind.labelKind != LabelKind.WallCell) continue;
                int key = Key(o);
                if (!_rows.TryGetValue(key, out var list)) _rows[key] = list = new List<int>(16);
                list.Add(o.Row);
            }
            foreach (var l in _rows.Values) if (l.Count > 1) l.Sort();
        }

        /// <summary>이 블록 앞(행 번호가 작은 쪽)에 살아 있는 같은 줄 블록 수(정본 `w.row &lt; o.row &amp;&amp; !w.dead`).</summary>
        public int Ahead(RunObject o)
        {
            if (o?.Kind == null || o.Kind.labelKind != LabelKind.WallCell || !_rows.TryGetValue(Key(o), out var list)) return 0;
            int n = 0;
            for (int i = 0; i < list.Count && list[i] < o.Row; i++) n++;
            return n;
        }

        static int Key(RunObject o) => o.Bar * 1024 + o.Lane;
    }
}
