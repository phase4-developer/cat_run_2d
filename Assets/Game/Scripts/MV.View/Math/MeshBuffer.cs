using System.Collections.Generic;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 절차 메쉬 정점·색·삼각형 모음(지면 띠·상자 빌보드·담장). 목록을 재사용해 매 프레임 할당하지 않는다.
    /// 다각형은 볼록이라고 보고 부채꼴로 나눈다(정본 `poly`가 쓰는 모양은 모두 볼록).
    /// </summary>
    public sealed class MeshBuffer
    {
        public readonly List<Vector3> Positions = new List<Vector3>(1024);
        public readonly List<Color32> Colors = new List<Color32>(1024);
        public readonly List<int> Triangles = new List<int>(2048);

        public int VertexCount => Positions.Count;

        public void Clear()
        {
            Positions.Clear();
            Colors.Clear();
            Triangles.Clear();
        }

        /// <summary>a→b→c→d 순서의 사각형 하나(같은 색).</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 col) => AddQuad(a, b, c, d, col, col, col, col);

        /// <summary>정점마다 색이 다른 사각형(세로 그라데이션 등).</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 ca, Color32 cb, Color32 cc, Color32 cd)
        {
            int i = Positions.Count;
            Positions.Add(a); Positions.Add(b); Positions.Add(c); Positions.Add(d);
            Colors.Add(ca); Colors.Add(cb); Colors.Add(cc); Colors.Add(cd);
            // Cull Off 셰이더라 감는 방향은 상관없다.
            Triangles.Add(i); Triangles.Add(i + 1); Triangles.Add(i + 2);
            Triangles.Add(i); Triangles.Add(i + 2); Triangles.Add(i + 3);
        }

        /// <summary>
        /// 선분 a→b를 굵기 <paramref name="width"/>의 띠로(캔버스 stroke처럼 중심선 기준 안팎 반씩).
        /// <paramref name="normal"/>은 선 평면 안에서 선에 수직인 단위 벡터. 모서리를 덮도록 양 끝을 반 굵기만큼 늘린다(사각 끝).
        /// </summary>
        public void AddLine(Vector3 a, Vector3 b, Vector3 normal, float width, Color32 col)
        {
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len <= 0f || width <= 0f) return;
            dir /= len;
            float h = width * 0.5f;
            Vector3 a2 = a - dir * h, b2 = b + dir * h, n = normal * h;
            AddQuad(a2 - n, b2 - n, b2 + n, a2 + n, col);
        }

        /// <summary>평면(xy) 다각형 외곽선: 변마다 <see cref="AddLine"/>. z는 0.</summary>
        public void AddOutline2D(IReadOnlyList<Vector2> pts, float width, Color32 col)
        {
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
                Vector2 d = b - a;
                if (d.sqrMagnitude <= 0f) continue;
                d.Normalize();
                AddLine(a, b, new Vector3(-d.y, d.x, 0f), width, col);
            }
        }

        /// <summary>평면(xy) 볼록 다각형 채움. z는 0.</summary>
        public void AddPolygon2D(IReadOnlyList<Vector2> pts, Color32 col)
        {
            int i0 = Positions.Count;
            for (int i = 0; i < pts.Count; i++)
            {
                Positions.Add(pts[i]);
                Colors.Add(col);
            }
            for (int i = 1; i + 1 < pts.Count; i++)
            {
                Triangles.Add(i0); Triangles.Add(i0 + i); Triangles.Add(i0 + i + 1);
            }
        }

        /// <summary>메쉬에 옮긴다(정점 65 535개 넘으면 32비트 인덱스).</summary>
        public void ApplyTo(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = Positions.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(Positions);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Triangles, 0, true);
        }
    }
}
