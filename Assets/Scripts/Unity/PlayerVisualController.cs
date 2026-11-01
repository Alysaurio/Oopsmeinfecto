using System;
using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Controls the visual state and animations for the player character.
    /// Handles Idle, Jump (movement step), Infected playback, and Defeat (squash with PlayerLimitTime).
    /// Handles horizontal flipping (FlipX) according to movement direction (Right = original, Left = flipX,
    /// Vertical = retain last horizontal facing, Level start = facing right).
    /// Operates purely on presentation and never modifies GameState or logical coordinates.
    /// </summary>
    public sealed class PlayerVisualController : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private CharacterSpritesConfig _config;
        private Animator _animator;

        private Vector3 _startPosition;
        private Vector3 _targetPosition;
        private Vector3 _defeatBasePosition;
        private bool _isMoving;
        private bool _isInfected;
        private bool _isDefeated;
        private bool _isAgony;
        private bool _facingRight = true;

        private float _animTimer;
        private float _defeatTimer;
        private float _defeatDuration = 0.6f;
        private Vector3 _baseScale = Vector3.one;

        public bool IsMoving => _isMoving;
        public bool IsInfected => _isInfected;
        public bool IsDefeated => _isDefeated;
        public bool FacingRight => _facingRight;

        public void Initialize(SpriteRenderer renderer, CharacterSpritesConfig config)
        {
            _renderer = renderer;
            _config = config;
            _animator = GetComponent<Animator>();
            _baseScale = transform.localScale;
            _startPosition = transform.position;
            _targetPosition = transform.position;
            _defeatBasePosition = transform.position;
            _isMoving = false;
            _isInfected = false;
            _isDefeated = false;
            _isAgony = false;
            _facingRight = true;
            _animTimer = 0f;
            _defeatTimer = 0f;

            if (_renderer != null)
            {
                _renderer.flipX = false;
            }

            ApplyInitialSprite();
        }

        private void ApplyInitialSprite()
        {
            if (_renderer == null) return;
            if (_config != null && _config.playerIdle != null && _config.playerIdle.Length > 0 && _config.playerIdle[0] != null)
            {
                _renderer.sprite = _config.playerIdle[0];
            }
            else
            {
                _renderer.sprite = PixelArtFactory.PlayerSprite;
            }
        }

        public void OnMoveStarted(Vector3 from, Vector3 to)
        {
            if (_isDefeated) return;
            _startPosition = from;
            _targetPosition = to;
            _isMoving = true;
            _animTimer = 0f;

            // Horizontal orientation based on movement delta
            float dx = to.x - from.x;
            if (dx > 0.01f)
            {
                _facingRight = true;
                if (_renderer != null) _renderer.flipX = false;
            }
            else if (dx < -0.01f)
            {
                _facingRight = false;
                if (_renderer != null) _renderer.flipX = true;
            }
            // Vertical movement (|dx| <= 0.01f) retains the last horizontal orientation

            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.SetBool("IsMoving", true);
            }
        }

        public void OnMoveFinished()
        {
            if (_isDefeated) return;
            _isMoving = false;
            _animTimer = 0f;

            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.SetBool("IsMoving", false);
            }
        }

        public void SetInfected(bool infected)
        {
            if (_isInfected == infected) return;
            _isInfected = infected;
            _animTimer = 0f;

            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.SetBool("IsInfected", infected);
            }
        }

        public void PlayDefeat(DefeatReason reason, float duration = 0.6f)
        {
            _isAgony = false;
            _isDefeated = true;
            _isMoving = false;
            _defeatDuration = Mathf.Max(0.5f, duration);
            _defeatTimer = 0f;
            transform.localPosition = new Vector3(0f, 0.25f, 0f);
            transform.localScale = _baseScale;

            // Stop normal animations completely so Animator never overwrites the defeat sprite
            if (_animator != null)
            {
                _animator.SetBool("IsDefeated", true);
                _animator.enabled = false;
            }

            if (_renderer != null)
            {
                _renderer.flipX = false;
                if (_config != null && _config.playerLimitTime != null)
                {
                    _renderer.sprite = _config.playerLimitTime;
                }
            }
        }

        public void Purify()
        {
            _isAgony = false;
            _isInfected = false;
            _isDefeated = false;
            _isMoving = false;
            _animTimer = 0f;
            transform.localScale = _baseScale;
            transform.localPosition = new Vector3(0f, 0.25f, 0f);

            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.SetBool("IsInfected", false);
                _animator.SetBool("IsMoving", false);
                _animator.SetBool("IsDefeated", false);
                _animator.Play("PlayerIdle", 0, 0f);
            }

            if (_renderer != null)
            {
                _renderer.color = Color.white;
                ApplyInitialSprite();
            }
        }

        public void KeepHumanSprite()
        {
            _isAgony = true;
            _isDefeated = false;
            _isMoving = false;
            _isInfected = false;
            transform.localScale = _baseScale;
            transform.localPosition = new Vector3(0f, 0.25f, 0f);

            if (_animator != null)
            {
                _animator.enabled = false;
            }

            if (_renderer != null)
            {
                _renderer.flipX = false;
                _renderer.color = Color.white;
                ApplyInitialSprite();
            }
        }

        public void PlayAgonyLimitTime()
        {
            _isAgony = true;
            _isDefeated = false;
            _isMoving = false;
            _isInfected = false;
            transform.localScale = _baseScale;
            transform.localPosition = new Vector3(0f, 0.25f, 0f);

            if (_animator != null)
            {
                _animator.enabled = false;
            }

            if (_renderer != null)
            {
                _renderer.flipX = false;
                if (_config != null && _config.playerLimitTime != null)
                {
                    _renderer.sprite = _config.playerLimitTime;
                }
            }
        }

        public void SwitchToInfectedSprite()
        {
            _isAgony = true;
            _isDefeated = false;
            _isMoving = false;
            _isInfected = true;
            transform.localScale = _baseScale;
            transform.localPosition = new Vector3(0f, 0.25f, 0f);

            if (_animator != null)
            {
                _animator.enabled = false;
            }

            if (_renderer != null)
            {
                _renderer.flipX = false;
                if (_config != null && _config.playerInfected != null && _config.playerInfected.Length > 0 && _config.playerInfected[0] != null)
                {
                    _renderer.sprite = _config.playerInfected[0];
                }
            }
        }

        public void EndAgonyAndSetInfected(bool infected)
        {
            _isAgony = false;
            _isDefeated = false;
            _isMoving = false;
            transform.localScale = _baseScale;
            transform.localPosition = new Vector3(0f, 0.25f, 0f);

            if (_animator != null)
            {
                _animator.enabled = true;
            }

            SetInfected(infected);
        }

        public void ResetState()
        {
            _isAgony = false;
            _isDefeated = false;
            _isMoving = false;
            _isInfected = false;
            _facingRight = true;
            _animTimer = 0f;
            _defeatTimer = 0f;
            transform.localScale = _baseScale;
            transform.localPosition = new Vector3(0f, 0.25f, 0f);

            if (_renderer != null)
            {
                _renderer.flipX = false;
            }

            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.SetBool("IsMoving", false);
                _animator.SetBool("IsInfected", false);
                _animator.SetBool("IsDefeated", false);
            }

            ApplyInitialSprite();
        }

        private void Update()
        {
            if (_renderer == null || _config == null) return;
            if (_isAgony) return;

            if (_isDefeated)
            {
                // Defeat animation: smooth vertical squash (~22%) and subtle downward sink (~0.12 units)
                _defeatTimer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_defeatTimer / _defeatDuration);
                float ease = Mathf.SmoothStep(0f, 1f, t);

                float scaleY = Mathf.Lerp(1f, 0.78f, ease);
                float scaleX = Mathf.Lerp(1f, 1.10f, ease);
                transform.localScale = new Vector3(_baseScale.x * scaleX, _baseScale.y * scaleY, _baseScale.z);

                float dropY = Mathf.Lerp(0f, -0.12f, ease);
                transform.localPosition = new Vector3(0f, 0.25f + dropY, 0f);

                if (_renderer != null)
                {
                    _renderer.flipX = false;
                    if (_config.playerLimitTime != null)
                    {
                        _renderer.sprite = _config.playerLimitTime;
                    }
                }
                return;
            }

            bool hasActiveAnimator = _animator != null && _animator.runtimeAnimatorController != null && _animator.isActiveAndEnabled;
            if (hasActiveAnimator)
            {
                // Animator drives the sprite frames via PlayerAnimatorController
                return;
            }

            // Fallback frame-stepping when Animator is not driving
            if (_isMoving)
            {
                var jumpSprites = _config.playerJump;
                if (jumpSprites != null && jumpSprites.Length > 0)
                {
                    float totalDist = Vector3.Distance(_startPosition, _targetPosition);
                    float curDist = Vector3.Distance(transform.position, _targetPosition);
                    float progress = totalDist > 0.001f ? Mathf.Clamp01(1f - (curDist / totalDist)) : 0f;

                    int frame = Mathf.Clamp(Mathf.FloorToInt(progress * jumpSprites.Length), 0, jumpSprites.Length - 1);
                    if (jumpSprites[frame] != null)
                    {
                        _renderer.sprite = jumpSprites[frame];
                    }
                }
            }
            else if (_isInfected)
            {
                var infSprites = _config.playerInfected;
                if (infSprites != null && infSprites.Length > 0)
                {
                    _animTimer += Time.unscaledDeltaTime;
                    int frame = Mathf.FloorToInt(_animTimer * 8f) % infSprites.Length;
                    if (infSprites[frame] != null)
                    {
                        _renderer.sprite = infSprites[frame];
                    }
                }
            }
            else
            {
                var idleSprites = _config.playerIdle;
                if (idleSprites != null && idleSprites.Length > 0)
                {
                    _animTimer += Time.unscaledDeltaTime;
                    int frame = Mathf.FloorToInt(_animTimer * 8f) % idleSprites.Length;
                    if (idleSprites[frame] != null)
                    {
                        _renderer.sprite = idleSprites[frame];
                    }
                }
            }
        }

        private void LateUpdate()
        {
            if (_renderer == null) return;

            if (_isDefeated)
            {
                _renderer.flipX = false;
                if (_config != null && _config.playerLimitTime != null)
                {
                    _renderer.sprite = _config.playerLimitTime;
                }
            }
            else if (_isAgony)
            {
                _renderer.flipX = false;
            }
            else
            {
                // Enforce horizontal orientation even if an Animator state changed
                _renderer.flipX = !_facingRight;
            }
        }
    }
}
