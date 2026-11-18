using System;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 적 몸 그림 프레임 넘김: `102_enemy.sprite` 접두 + 번호, `frameCount`장을 `102_enemy.fps`로 넘긴다.
    /// 몸 그림 위 <see cref="SpriteFrameList"/>에 그 접두 그림이 있으면 그것(스테이지 전용 리소스), 없으면 지금 테마 아틀라스에서 찾는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyFrameAnim : RunObjectModule
    {
        [Tooltip("프레임을 넘길 몸 그림")]
        [SerializeField] SpriteRenderer _body;

        Sprite[] _frames;
        Sprite _bodyBase;
        bool _captured;

        public override void OnBind(RunObjectView view)
        {
            if (!_captured) { _captured = true; if (_body != null) _bodyBase = _body.sprite; }
            _frames = null;   // 테마가 바뀌었을 수 있다(적 프레임은 지금 테마 아틀라스)
        }

        public override void OnUnbind(RunObjectView view)
        {
            _frames = null;
            if (_body != null) _body.sprite = _bodyBase;
        }

        public override void OnTick(RunObjectView view, in ObjectFrame f)
        {
            if (_body == null) return;
            var o = view.Obj;
            _frames ??= Frames(view);
            if (_frames == null || _frames.Length == 0) return;
            double fps = o.Enemy != null ? o.Enemy.fps : 0;
            int i = fps > 0 ? (int)(Math.Floor(f.T * fps) % _frames.Length) : 0;
            if (_frames[i] != null) _body.sprite = _frames[i];
        }

        Sprite[] Frames(RunObjectView view)
        {
            var o = view.Obj;
            string prefix = o.Enemy?.sprite;
            if (string.IsNullOrEmpty(prefix)) return null;
            if (!prefix.EndsWith("_")) prefix += "_";
            int count = Math.Max(1, o.Enemy.frameCount);
            var list = _body.GetComponentInParent<SpriteFrameList>(true);
            var own = list != null ? list.Pick(prefix, count) : null;
            if (own != null) return own;
            var sprites = view.Ctx.Sprites;
            return sprites != null ? sprites.GetFrames(view.Ctx.Atlas, prefix, count) : null;
        }
    }
}
