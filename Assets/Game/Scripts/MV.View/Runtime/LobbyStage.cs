using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using jjevol;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 로비 아지트 직교 장면(02 §2.9, 03 §3.5.6). `LobbyCamera`(직교, 로비일 때만 켬) 앞에 기준 프레임 9×16 단위로
    /// 방 텍스처 `Tex_Lobby_Room` 1장 + 시설 스프라이트(`Atlas_World_Lobby`, `_2`)를 <see cref="LobbyIso"/> 자리에 놓고,
    /// sortingOrder를 화면 y로 매 프레임 계산한다(위 = 뒤 먼저). 안개·틴트·가산광 없음.
    /// 화면이 9:16보다 길면 프레임을 세로 가운데에 두고 위는 벽 색, 아래는 바닥 바깥 색(카메라 배경)으로 채운다.
    /// 시설 단계는 메타(MV-META·MV-UI-LOBBY)가 <see cref="SetFacility"/>로 넣는다. 이름표·말풍선·고양이는 다음 단계.
    /// </summary>
    [DefaultExecutionOrder(70)]
    public sealed class LobbyStage : MonoBehaviour
    {
        public const string RoomTexture = "Tex_Lobby_Room";
        public static readonly string[] Atlases = { "Atlas_World_Lobby", "Atlas_World_Lobby_2" };

        [SerializeField] Camera _camera;
        [SerializeField] Material _spriteMaterial;

        readonly Dictionary<string, (SpriteRenderer sr, int st, int extra, double bump)> _fac = new Dictionary<string, (SpriteRenderer, int, int, double)>();
        WorldSpriteCache _sprites;
        AddressableLease<Texture2D> _roomLease;
        Sprite _roomSprite;
        SpriteRenderer _room, _wallTop;
        double _t;

        public Camera Cam => _camera;
        public SpriteRenderer Room => _room;

        /// <summary>카메라·배경 틀을 만든다. 텍스처·아틀라스는 <see cref="LoadAsync"/>.</summary>
        public void Init(WorldSpriteCache sprites)
        {
            _sprites = sprites;
            if (_camera == null)
            {
                var camGo = new GameObject("LobbyCamera");
                camGo.transform.SetParent(transform, false);
                _camera = camGo.AddComponent<Camera>();
            }
            _camera.orthographic = true;
            _camera.transform.localPosition = new Vector3(0, 0, -10);
            _camera.transform.localRotation = Quaternion.identity;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            MvColor.TryColor32(LobbyIso.FloorOut, out var floor);
            _camera.backgroundColor = floor;
            _room = Child("Room", -32000);
            _wallTop = Child("WallTop", -32001);
            MvColor.TryColor32(LobbyIso.WallTop, out var wall);
            _wallTop.color = wall;
            foreach (var s in LobbyIso.Slots) _fac[s.Key] = (Child("Fac_" + s.Key, 0), 0, 0, 0);
            Fit();
        }

        SpriteRenderer Child(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (_spriteMaterial != null) sr.sharedMaterial = _spriteMaterial;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>방 텍스처와 로비 아틀라스를 로드한다(03 §3.8.5). 실패해도 틀은 남는다.</summary>
        public async UniTask<bool> LoadAsync(CancellationToken ct = default)
        {
            bool ok = true;
            foreach (var a in Atlases) ok &= await _sprites.LoadAtlasAsync(a, ct);
            _roomLease?.Dispose();
            _roomLease = await AddressableScope.Get<Texture2D>(RoomTexture, ct);
            var tex = _roomLease?.Asset;
            if (tex != null)
            {
                // 가로 = 프레임 9 단위.
                _roomSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width / (float)LobbyIso.FrameW);
                _room.sprite = _roomSprite;
            }
            else ok = false;
            WhiteWall();
            return ok;
        }

        void WhiteWall()
        {
            // 위 채움: 흰 점 스프라이트를 늘려 쓴다(Fx 아틀라스가 없으면 방 텍스처 맨 윗줄 대신 카메라 배경만).
            var dot = _sprites != null ? _sprites.GetAny(new[] { WorldView.FxAtlas }, "Fx_Square") : null;
            _wallTop.sprite = dot;
            Fit();
        }

        /// <summary>시설 단계(0 = 잠김). <paramref name="extra"/> = 건조대 생선 수.</summary>
        public void SetFacility(string key, int stage, int extra = 0)
        {
            if (!_fac.TryGetValue(key, out var f)) return;
            _fac[key] = (f.sr, stage, extra, f.bump);
        }

        /// <summary>터치 반동 시작(정본 `AZ.bump[k] = 1`).</summary>
        public void Bump(string key)
        {
            if (_fac.TryGetValue(key, out var f)) _fac[key] = (f.sr, f.st, f.extra, 1);
        }

        void Fit()
        {
            if (_camera == null) return;
            float aspect = Mathf.Max(0.01f, _camera.aspect);
            // 가로 9 단위를 늘 보이게: 세로 반 = max(8, 4.5/aspect).
            _camera.orthographicSize = Mathf.Max(LobbyIso.OrthoSize, (float)(LobbyIso.FrameW * 0.5 / aspect));
            if (_wallTop != null && _wallTop.sprite != null)
            {
                float h = _camera.orthographicSize - (float)(LobbyIso.FrameH * 0.5);
                var size = _wallTop.sprite.bounds.size;
                _wallTop.transform.localPosition = new Vector3(0, (float)(LobbyIso.FrameH * 0.5) + h * 0.5f, 0);
                _wallTop.transform.localScale = new Vector3((float)LobbyIso.FrameW / Mathf.Max(0.0001f, size.x), Mathf.Max(0f, h) / Mathf.Max(0.0001f, size.y), 1);
                _wallTop.enabled = h > 0;
            }
        }

        void LateUpdate()
        {
            if (_camera == null || !_camera.enabled) return;
            _t += Time.unscaledDeltaTime;   // 정본 lobbyT(실시간 누적)
            Fit();
            foreach (var s in LobbyIso.Slots)
            {
                var f = _fac[s.Key];
                string name = LobbyIso.FacSprite(s.Key, f.st, _t, f.extra);
                var sprite = _sprites != null ? _sprites.GetAny(Atlases, name) : null;
                f.sr.sprite = sprite;
                f.sr.enabled = sprite != null;
                LobbyIso.IsoUnits(s.U, s.V, out double x, out double y);
                f.sr.transform.localPosition = new Vector3((float)x, (float)y, 0);
                f.sr.sortingOrder = LobbyIso.SortingOrder(y);
                if (sprite != null)
                {
                    // 그림은 s = 160·sc px/단위로 구움 → 프레임 단위 크기 = 9·0.115·sc. sc는 그림에 들어 있다.
                    float k = (float)(LobbyIso.UnitSize * sprite.pixelsPerUnit / LobbyIso.BakedUnitPx * LobbyIso.Bump(f.bump));
                    f.sr.transform.localScale = new Vector3(k, k, 1);
                }
                if (f.bump > 0) _fac[s.Key] = (f.sr, f.st, f.extra, System.Math.Max(0, f.bump - LobbyIso.BumpDecayPerFrame));
            }
        }

        /// <summary>로비 ↔ 런 전환(02 §2.9): 로비면 이 카메라만 켠다. `GameCamera`는 호출자가 반대로 둔다.</summary>
        public void SetActive(bool lobby)
        {
            if (_camera != null) _camera.enabled = lobby;
            gameObject.SetActive(lobby);
        }

        void OnDestroy()
        {
            if (_roomSprite != null) Destroy(_roomSprite);
            _roomLease?.Dispose();
        }
    }
}
