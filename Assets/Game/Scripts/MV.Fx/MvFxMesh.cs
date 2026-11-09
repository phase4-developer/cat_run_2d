using System.Collections.Generic;
using UnityEngine;

namespace MV.Fx
{
    /// <summary>
    /// 셰이더 `MV/FxShape`가 그리는 모양(uv.z). 06 §6.2.1 해석표의 2D 그림을 셰이더에서 만든다(텍스처 없음).
    /// </summary>
    public enum FxShape
    {
        /// <summary>정점색 그대로(하늘·지붕·집중선).</summary>
        Solid = 0,
        /// <summary>빛 스프라이트: c → 알파 0.6@0.45 → 0 원형 그라데이션.</summary>
        Glow = 1,
        /// <summary>사각형 + 1px OLC 외곽선(텍스처 없는 메쉬, 파편).</summary>
        Rect = 2,
        /// <summary>타원 선(링·하악 파동). uv.w = 선 굵기(반지름 비율).</summary>
        Ellipse = 3,
        /// <summary>채운 원 + 외곽선(발도장 패드·해·달). uv.w = 외곽선 굵기(반지름 비율).</summary>
        Disc = 4,
    }

    /// <summary>
    /// 빌보드 사각형 묶음을 한 메쉬로 굽는다. 서브메쉬 0 = 일반 블렌딩, 1 = 가산(06 §6.14 재질 묶음). 재생 중 할당 없음.
    /// </summary>
    public sealed class MvFxMesh
    {
        readonly List<Vector3> _v = new List<Vector3>(256);
        readonly List<Color32> _c = new List<Color32>(256);
        readonly List<Vector4> _uv = new List<Vector4>(256);
        readonly List<int> _t0 = new List<int>(384);
        readonly List<int> _t1 = new List<int>(384);

        public int QuadCount => _v.Count / 4;

        public void Clear()
        {
            _v.Clear(); _c.Clear(); _uv.Clear(); _t0.Clear(); _t1.Clear();
        }

        /// <summary>중심 c, 화면 평면 축 ax(가로 반폭 포함)·ay(세로 반높이 포함) 사각형.</summary>
        public void Quad(Vector3 c, Vector3 ax, Vector3 ay, Color32 col, FxShape shape, float param, bool add)
        {
            if (col.a == 0) return;
            int b = _v.Count;
            _v.Add(c - ax - ay); _v.Add(c + ax - ay); _v.Add(c + ax + ay); _v.Add(c - ax + ay);
            _c.Add(col); _c.Add(col); _c.Add(col); _c.Add(col);
            float s = (float)shape;
            _uv.Add(new Vector4(0, 0, s, param)); _uv.Add(new Vector4(1, 0, s, param));
            _uv.Add(new Vector4(1, 1, s, param)); _uv.Add(new Vector4(0, 1, s, param));
            var t = add ? _t1 : _t0;
            t.Add(b); t.Add(b + 2); t.Add(b + 1);
            t.Add(b); t.Add(b + 3); t.Add(b + 2);
        }

        /// <summary>정점마다 색이 다른 사각형(하늘 그라데이션). 순서: 왼아래·오른아래·오른위·왼위.</summary>
        public void Quad4(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color32 c0, Color32 c1, Color32 c2, Color32 c3, bool add = false)
        {
            int b = _v.Count;
            _v.Add(p0); _v.Add(p1); _v.Add(p2); _v.Add(p3);
            _c.Add(c0); _c.Add(c1); _c.Add(c2); _c.Add(c3);
            _uv.Add(Vector4.zero); _uv.Add(Vector4.zero); _uv.Add(Vector4.zero); _uv.Add(Vector4.zero);
            var t = add ? _t1 : _t0;
            t.Add(b); t.Add(b + 2); t.Add(b + 1);
            t.Add(b); t.Add(b + 3); t.Add(b + 2);
        }

        /// <summary>선분 a→b, 굵기 w(화면 평면, n = 평면 법선).</summary>
        public void Line(Vector3 a, Vector3 b, float w, Vector3 n, Color32 col, bool add)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 1e-5f) return;
            var dir = d / len;
            var side = Vector3.Cross(n, dir).normalized * (w * 0.5f);
            Quad((a + b) * 0.5f, side, dir * (len * 0.5f), col, FxShape.Solid, 0, add);
        }

        public void Apply(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(_v);
            mesh.SetColors(_c);
            mesh.SetUVs(0, _uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(_t0, 0, false);
            mesh.SetTriangles(_t1, 1, false);
            mesh.RecalculateBounds();
        }
    }
}
