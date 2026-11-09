using MV.Data;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 화면 공간 층(02 §2.1.6·§2.5.5). 지금은 `Snow` 층만: `deco = snow`면 `GameCamera` 앞 1 m 평면에 고정 70개 스프라이트를
    /// C#이 정본 식(<see cref="SnowField"/>)으로 매 프레임 배치한다(파티클 시뮬 아님).
    /// 기준 px: 가로 view.refWidthPx 고정, 세로 = 가로 / 화면비(D-41 ② 확장). Vignette 층은 아직 없음(다음 계약).
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class ScreenRig : MonoBehaviour
    {
        public const string SnowLayer = "Snow";

        [SerializeField] Material _spriteMaterial;

        Camera _cam;
        ViewConfig _cfg;
        WorldSpriteCache _sprites;
        string _fxAtlas;
        SpriteRenderer[] _snow = new SpriteRenderer[0];
        bool _snowOn;

        public bool SnowOn => _snowOn;
        public int SnowCount => _snow.Length;

        public void Init(ViewConfig cfg, Camera cam, WorldSpriteCache sprites, string fxAtlas)
        {
            _cfg = cfg;
            _cam = cam;
            _sprites = sprites;
            _fxAtlas = fxAtlas;
            transform.SetParent(cam.transform, false);
            transform.localPosition = new Vector3(0f, 0f, 1f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        public void SetTheme(ThemeData theme)
        {
            _snowOn = theme != null && theme.deco == "snow" && _cfg != null;
            if (_snowOn && _snow.Length != _cfg.SnowCount) BuildSnow(_cfg.SnowCount);
            var sprite = _snowOn && _sprites != null ? _sprites.Get(_fxAtlas, SnowField.Sprite) : null;
            for (int i = 0; i < _snow.Length; i++)
            {
                _snow[i].sprite = sprite;
                _snow[i].gameObject.SetActive(_snowOn && sprite != null);
            }
        }

        void BuildSnow(int n)
        {
            for (int i = 0; i < _snow.Length; i++) if (_snow[i] != null) Destroy(_snow[i].gameObject);
            _snow = new SpriteRenderer[n];
            var white = new Color(1f, 1f, 1f, SnowField.Alpha);
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Snow" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                if (_spriteMaterial != null) sr.sharedMaterial = _spriteMaterial;
                sr.sortingLayerName = SnowLayer;
                sr.color = white;
                _snow[i] = sr;
            }
        }

        void LateUpdate()
        {
            if (!_snowOn || _cam == null || _cfg == null) return;
            double t = Time.realtimeSinceStartupAsDouble;   // 정본 performance.now()/1000 — 실시간
            float aspect = Mathf.Max(0.01f, _cam.aspect);
            double w = _cfg.RefWidthPx, h = w / aspect;
            float hh = Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad), hw = hh * aspect;
            for (int i = 0; i < _snow.Length; i++)
            {
                var sr = _snow[i];
                if (sr.sprite == null) continue;
                SnowField.Particle(i, t, w, h, 1, out double x, out double y, out double r);
                sr.transform.localPosition = new Vector3((float)((x / w - 0.5) * 2 * hw), (float)((0.5 - y / h) * 2 * hh), 0f);
                float size = Mathf.Max(0.0001f, sr.sprite.bounds.size.x);
                sr.transform.localScale = Vector3.one * (float)(2 * r / w * 2 * hw / size);
            }
        }
    }
}
