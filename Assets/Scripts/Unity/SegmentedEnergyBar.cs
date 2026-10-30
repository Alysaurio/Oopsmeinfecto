using System.Collections.Generic;
using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Segmented world-space energy bar that follows the player. The total width stays constant
    /// and is divided by maxEnergy. During lethal-tile defeat it drains to zero over a fixed time.
    /// </summary>
    public sealed class SegmentedEnergyBar : MonoBehaviour
    {
        private GameTuning _tuning;
        private Transform _follow;
        private int _maxEnergy = 1;
        private int _currentEnergy = 1;
        private float _width = 0.90f;
        private float _height = 0.12f;
        private float _gap = 0.022f;
        private float _verticalOffset = 0.72f;

        private readonly List<SpriteRenderer> _emptySegments = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _fillSegments = new List<SpriteRenderer>();
        private SpriteRenderer _border;
        private SpriteRenderer _background;

        private bool _infected;
        private bool _draining;
        private float _drainTimer;
        private float _drainDuration = 0.5f;
        private float _drainStartFraction;
        private float _lastFraction = 1f;

        public void Initialize(GameTuning tuning, int maxEnergy, Transform followTarget)
        {
            _tuning = tuning;
            _follow = followTarget;
            _maxEnergy = Mathf.Max(1, maxEnergy);
            _currentEnergy = _maxEnergy;
            _infected = false;
            _draining = false;
            _lastFraction = 1f;
            Rebuild();
        }

        public void SetEnergy(int currentEnergy, int maxEnergy)
        {
            maxEnergy = Mathf.Max(1, maxEnergy);
            if (maxEnergy != _maxEnergy)
            {
                _maxEnergy = maxEnergy;
                Rebuild();
            }
            _currentEnergy = Mathf.Clamp(currentEnergy, 0, _maxEnergy);
            if (!_draining)
            {
                _lastFraction = _currentEnergy / (float)_maxEnergy;
                ApplyFraction(_lastFraction);
            }
        }

        public void SetInfected(bool infected)
        {
            if (_infected == infected) return;
            _infected = infected;
            ApplyFraction(_lastFraction);
        }

        public void PlayDrain(float duration)
        {
            _draining = true;
            _drainTimer = 0f;
            _drainDuration = Mathf.Max(0.01f, duration);
            _drainStartFraction = _lastFraction;
        }

        private void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            _emptySegments.Clear();
            _fillSegments.Clear();

            _border = CreateQuad("EnergyBorder", _tuning != null ? _tuning.energyBorderColor : Color.white, 20);
            _border.transform.localScale = new Vector3(_width + 0.09f, _height + 0.09f, 1f);
            _background = CreateQuad("EnergyBackground", _tuning != null ? _tuning.energyBackgroundColor : Color.black, 21);
            _background.transform.localScale = new Vector3(_width + 0.05f, _height + 0.05f, 1f);

            float segmentWidth = (_width - _gap * (_maxEnergy - 1)) / _maxEnergy;
            float left = -_width * 0.5f;

            for (int i = 0; i < _maxEnergy; i++)
            {
                var empty = CreateQuad("EnergyEmpty" + i,
                    _tuning != null ? _tuning.energyEmptyColor : Color.gray, 22);
                empty.transform.localScale = new Vector3(segmentWidth, _height, 1f);
                empty.transform.localPosition = new Vector3(left + i * (segmentWidth + _gap) + segmentWidth * 0.5f, 0f, 0f);
                _emptySegments.Add(empty);

                var fill = CreateQuad("EnergyFill" + i,
                    _tuning != null ? _tuning.energyFilledColor : Color.green, 23);
                fill.transform.localScale = new Vector3(segmentWidth, _height, 1f);
                fill.transform.localPosition = new Vector3(left + i * (segmentWidth + _gap) + segmentWidth * 0.5f, 0f, 0f);
                _fillSegments.Add(fill);
            }

            ApplyFraction(_lastFraction);
        }

        private SpriteRenderer CreateQuad(string objectName, Color color, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PixelArtFactory.WhiteSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            if (PixelArtFactory.SpriteMaterial != null) renderer.sharedMaterial = PixelArtFactory.SpriteMaterial;
            return renderer;
        }

        private void ApplyFraction(float fraction)
        {
            _lastFraction = Mathf.Clamp01(fraction);
            if (_fillSegments.Count == 0) return;

            float segmentWidth = (_width - _gap * (_maxEnergy - 1)) / _maxEnergy;
            float left = -_width * 0.5f;
            Color filled = _tuning != null ? _tuning.energyFilledColor : Color.green;
            Color empty = _tuning != null ? _tuning.energyEmptyColor : Color.gray;
            if (_infected)
            {
                var target = _tuning != null ? _tuning.infectedDesaturation : Color.gray;
                filled = Color.Lerp(filled, target, 0.72f);
                empty = Color.Lerp(empty, target, 0.35f);
            }

            for (int i = 0; i < _maxEnergy; i++)
            {
                float fill = Mathf.Clamp01(_lastFraction * _maxEnergy - i);
                var renderer = _fillSegments[i];
                if (fill <= 0.001f)
                {
                    renderer.enabled = false;
                    continue;
                }

                renderer.enabled = true;
                renderer.color = filled;
                float fillWidth = segmentWidth * fill;
                renderer.transform.localScale = new Vector3(fillWidth, _height, 1f);
                renderer.transform.localPosition = new Vector3(
                    left + i * (segmentWidth + _gap) + fillWidth * 0.5f, 0f, 0f);

                if (_emptySegments.Count > i) _emptySegments[i].color = empty;
            }
        }

        private void LateUpdate()
        {
            if (_follow != null)
            {
                transform.position = _follow.position + Vector3.up * _verticalOffset;
            }

            if (_draining)
            {
                _drainTimer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_drainTimer / _drainDuration);
                ApplyFraction(Mathf.Lerp(_drainStartFraction, 0f, t));
                if (t >= 1f) _draining = false;
            }
        }
    }
}
