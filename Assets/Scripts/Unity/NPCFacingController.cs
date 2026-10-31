using System;
using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>Eight visual directions used by the zombie sprite. Order matches the tuning fields.</summary>
    public enum NpcFacing
    {
        Up = 0,
        UpRight = 1,
        Right = 2,
        DownRight = 3,
        Down = 4,
        DownLeft = 5,
        Left = 6,
        UpLeft = 7
    }

    /// <summary>
    /// Controls the directional presentation and walk/idle animation cycles for the NPC zombie.
    /// When moving between cells: plays the 4-frame walk animation for the displacement direction.
    /// When idle / waiting: stops walk cycle, displays strictly frame 0 of the anticipatory facing direction,
    /// and performs a gentle ~4% vertical breathing contraction without touching the root position.
    /// Preserves the existing look-ahead prediction algorithm.
    /// </summary>
    public sealed class NPCFacingController : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private GameTuning _tuning;
        private CharacterSpritesConfig _characterSprites;
        private Animator _animator;

        private NpcFacing _facing = NpcFacing.Down;
        private NpcFacing _walkFacing = NpcFacing.Down;
        private bool _hasApplied;

        private bool _isMoving;
        private float _moveAnimTimer;
        private Vector3 _baseScale = Vector3.one;

        public NpcFacing Facing => _facing;
        public NpcFacing WalkFacing => _walkFacing;
        public bool IsMoving => _isMoving;

        public void Initialize(SpriteRenderer renderer, GameTuning tuning, NpcFacing initial = NpcFacing.Down)
        {
            Initialize(renderer, tuning, null, initial);
        }

        public void Initialize(SpriteRenderer renderer, GameTuning tuning, CharacterSpritesConfig spritesConfig, NpcFacing initial = NpcFacing.Down)
        {
            _renderer = renderer;
            _tuning = tuning;
            _characterSprites = spritesConfig != null
                ? spritesConfig
                : (tuning != null && tuning.characterSpritesConfig != null
                    ? tuning.characterSpritesConfig
                    : Resources.Load<CharacterSpritesConfig>("CharacterSpritesConfig"));

            _facing = initial;
            _walkFacing = initial;
            _baseScale = transform.localScale;
            _isMoving = false;
            _moveAnimTimer = 0f;
            _hasApplied = false;

            _animator = GetComponent<Animator>();
            if (_animator != null)
            {
                _animator.SetInteger("Facing", (int)initial);
                _animator.SetBool("IsMoving", false);
                _animator.enabled = false; // Keep disabled while idle so frame 0 is maintained
            }

            Apply(initial, true);
        }

        public void StartMovement(NpcFacing walkFacing)
        {
            _isMoving = true;
            _walkFacing = walkFacing;
            _moveAnimTimer = 0f;
            transform.localScale = _baseScale;

            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.SetInteger("Facing", (int)walkFacing);
                _animator.SetBool("IsMoving", true);
            }

            UpdateWalkFrame();
        }

        public void SetMoving(bool moving)
        {
            if (_isMoving == moving) return;
            _isMoving = moving;

            if (moving)
            {
                StartMovement(_facing);
            }
            else
            {
                _moveAnimTimer = 0f;
                transform.localScale = _baseScale;

                if (_animator != null)
                {
                    _animator.SetBool("IsMoving", false);
                    _animator.enabled = false;
                }

                ApplyIdleFrame();
            }
        }

        public void SetBasePosition(Vector3 position)
        {
            // Position is managed strictly by LevelRenderer grid interpolation
        }

        /// <summary>
        /// Recomputes the anticipatory facing after every step. Preserves the original 2-step look-ahead.
        /// </summary>
        public void UpdateFacing(GridPos currentPosition, GridPos twoStepsAhead, Direction? nextMove)
        {
            var delta = twoStepsAhead - currentPosition;
            if (delta.X == 0 && delta.Y == 0)
            {
                if (!nextMove.HasValue) return;
                delta = Directions.Delta(nextMove.Value);
            }

            Apply(FromDelta(delta), false);
        }

        public static NpcFacing FromDelta(GridPos delta)
        {
            int x = delta.X == 0 ? 0 : (delta.X > 0 ? 1 : -1);
            int y = delta.Y == 0 ? 0 : (delta.Y > 0 ? 1 : -1);

            if (x == 0 && y > 0) return NpcFacing.Up;
            if (x > 0 && y > 0) return NpcFacing.UpRight;
            if (x > 0 && y == 0) return NpcFacing.Right;
            if (x > 0 && y < 0) return NpcFacing.DownRight;
            if (x == 0 && y < 0) return NpcFacing.Down;
            if (x < 0 && y < 0) return NpcFacing.DownLeft;
            if (x < 0 && y == 0) return NpcFacing.Left;
            return NpcFacing.UpLeft;
        }

        public static NpcFacing FromDirection(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return NpcFacing.Up;
                case Direction.Right: return NpcFacing.Right;
                case Direction.Down: return NpcFacing.Down;
                default: return NpcFacing.Left;
            }
        }

        private void Apply(NpcFacing facing, bool force)
        {
            if (!force && _hasApplied && facing == _facing) return;
            _facing = facing;
            _hasApplied = true;

            if (_animator != null && _isMoving)
            {
                _animator.SetInteger("Facing", (int)_walkFacing);
            }

            if (!_isMoving)
            {
                ApplyIdleFrame();
            }
        }

        private void Update()
        {
            if (_renderer == null) return;

            if (_isMoving)
            {
                _moveAnimTimer += Time.unscaledDeltaTime;
                transform.localScale = _baseScale;

                bool hasActiveAnimator = _animator != null && _animator.runtimeAnimatorController != null && _animator.isActiveAndEnabled;
                if (!hasActiveAnimator)
                {
                    UpdateWalkFrame();
                }
            }
            else
            {
                // Idle: show strictly frame 0 and apply gentle ~4% breathing contraction
                ApplyIdleFrame();

                float breath = 1f - 0.04f * (0.5f + 0.5f * Mathf.Sin(Time.time * 2.8f));
                transform.localScale = new Vector3(_baseScale.x, _baseScale.y * breath, _baseScale.z);
            }
        }

        private void LateUpdate()
        {
            if (_renderer == null) return;

            // Ensure frame 0 of the anticipatory facing stays locked while idle
            if (!_isMoving)
            {
                ApplyIdleFrame();
            }
        }

        private void ApplyIdleFrame()
        {
            if (_renderer == null) return;

            Sprite[] directionSprites = _characterSprites != null ? _characterSprites.GetZombieDirectionSprites(_facing) : null;
            if (directionSprites != null && directionSprites.Length > 0 && directionSprites[0] != null)
            {
                _renderer.sprite = directionSprites[0];
                return;
            }

            var custom = CustomSprite(_facing);
            _renderer.sprite = custom != null ? custom : PixelArtFactory.ZombieFacingSprite(_facing);
        }

        private void UpdateWalkFrame()
        {
            if (_renderer == null) return;

            Sprite[] directionSprites = _characterSprites != null ? _characterSprites.GetZombieDirectionSprites(_walkFacing) : null;
            if (directionSprites != null && directionSprites.Length > 0 && directionSprites[0] != null)
            {
                int frame = Mathf.FloorToInt(_moveAnimTimer * 8f) % directionSprites.Length;
                if (directionSprites[frame] != null)
                {
                    _renderer.sprite = directionSprites[frame];
                }
                return;
            }

            var custom = CustomSprite(_walkFacing);
            _renderer.sprite = custom != null ? custom : PixelArtFactory.ZombieFacingSprite(_walkFacing);
        }

        private Sprite CustomSprite(NpcFacing facing)
        {
            if (_tuning == null) return null;
            switch (facing)
            {
                case NpcFacing.Up: return _tuning.zombieFacingUp;
                case NpcFacing.UpRight: return _tuning.zombieFacingUpRight;
                case NpcFacing.Right: return _tuning.zombieFacingRight;
                case NpcFacing.DownRight: return _tuning.zombieFacingDownRight;
                case NpcFacing.Down: return _tuning.zombieFacingDown;
                case NpcFacing.DownLeft: return _tuning.zombieFacingDownLeft;
                case NpcFacing.Left: return _tuning.zombieFacingLeft;
                default: return _tuning.zombieFacingUpLeft;
            }
        }
    }
}
