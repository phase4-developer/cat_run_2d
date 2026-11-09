using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 정점색 메쉬(상자 빌보드·담장)의 안개 알파: 기본 정점색 알파 × a를 정점색에 다시 넣는다.
    /// 공유 머티리얼을 그대로 두기 위해 MPB·머티리얼 복제를 쓰지 않는다(03 §3.8.9). 바뀔 때만 다시 쓴다(<see cref="DepthSortable"/>가 1/255 미만 변화를 거른다).
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class VertexColorFog : MonoBehaviour, IFogAlphaTarget
    {
        Mesh _mesh;
        Color32[] _base;
        Color32[] _work;

        /// <summary>메쉬를 만든 뒤 한 번 부른다(이 컴포넌트가 메쉬를 소유한다).</summary>
        public void Bind(MeshBuffer mb, string meshName)
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = meshName };
                GetComponent<MeshFilter>().sharedMesh = _mesh;
            }
            mb.ApplyTo(_mesh);
            _base = mb.Colors.ToArray();
            _work = new Color32[_base.Length];
        }

        public void SetFogAlpha(float alpha)
        {
            if (_mesh == null || _base == null) return;
            for (int i = 0; i < _base.Length; i++)
            {
                var c = _base[i];
                c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * alpha), 0, 255);
                _work[i] = c;
            }
            _mesh.colors32 = _work;
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
