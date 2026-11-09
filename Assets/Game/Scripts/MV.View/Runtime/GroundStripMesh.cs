using MV.Data;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 지면 띠·옹벽 메쉬(02 §2.4.3). 매 프레임 카메라 뒤에 <see cref="GroundStripBuilder"/>로 사각형을 만들고 정점색을 다시 넣는다.
    /// 머티리얼은 공유 `MV_WorldVertexColor`, 정렬 레이어 `Ground`(항상 맨 뒤).
    /// </summary>
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GroundStripMesh : MonoBehaviour
    {
        public const string SortingLayer = "Ground";

        CameraRig _rig;
        IWorldViewSource _source;
        GroundStripBuilder _builder;
        GroundPalette _palette;
        readonly MeshBuffer _buffer = new MeshBuffer();
        Mesh _mesh;
        int _quant;

        public GroundStripBuilder Builder => _builder;

        public void Init(ViewConfig cfg, MvTables tables, CameraRig rig, IWorldViewSource source)
        {
            _rig = rig;
            _source = source;
            _quant = cfg.MixQuant;
            _builder = new GroundStripBuilder(cfg, tables.Camera.ByKey(CameraModel.KeyRun).fovV);
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "GroundStrip" };
                _mesh.MarkDynamic();
                GetComponent<MeshFilter>().sharedMesh = _mesh;
            }
            var mr = GetComponent<MeshRenderer>();
            mr.sortingLayerName = SortingLayer;
            mr.sortingOrder = 0;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
        }

        public void SetTheme(ThemeData theme)
        {
            _palette = theme != null ? new GroundPalette(theme, _quant) : null;
        }

        void LateUpdate()
        {
            if (_builder == null || _palette == null || _rig == null || !_rig.Ready) return;
            var m = _rig.Model;
            var input = _source.Camera;
            var f = new GroundFrame { Pz = m.Pos.z, CamPos = m.Pos, CamLook = m.Look, GroundY = input.Wy };
            f.HasCliff = _source.TryGetCliff(out f.Cliff);
            _builder.Build(f, _palette.SnowDeco, _quant);
            _builder.Fill(_buffer, _palette);
            _buffer.ApplyTo(_mesh);
            // 카메라를 따라 매 프레임 다시 만드는 메쉬라 경계 상자를 넉넉히 둔다(절두체 컬링에 빠지지 않게).
            _mesh.bounds = new Bounds(new Vector3(0f, (float)input.Wy, (float)m.Pos.z + 46f), new Vector3(160f, 80f, 200f));
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
