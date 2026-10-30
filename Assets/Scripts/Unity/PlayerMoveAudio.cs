using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Plays a random move sound whenever the player changes tile.
    /// The clips live in Assets/Resources/Sounds/Move (MovePlayer1..MovePlayer8) and can also
    /// be assigned explicitly in the Inspector.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class PlayerMoveAudio : MonoBehaviour
    {
        private const string MoveClipFolder = "Sounds/Move";
        private const string MoveClipPrefix = "MovePlayer";

        [Header("Scene references")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip[] _moveClips = new AudioClip[0];

        [Header("Playback")]
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.8f;
        [SerializeField] private bool _avoidImmediateRepeat = true;

        private int _lastIndex = -1;

        private void Awake()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.playOnAwake = false;
            _audioSource.loop = false;
            _audioSource.spatialBlend = 0f;

            if (_moveClips == null || _moveClips.Length == 0)
            {
                var loaded = Resources.LoadAll<AudioClip>(MoveClipFolder);
                System.Array.Sort(loaded, (left, right) =>
                    string.CompareOrdinal(left != null ? left.name : string.Empty,
                                          right != null ? right.name : string.Empty));

                // Only the MovePlayer* clips are valid move sounds.
                var valid = new System.Collections.Generic.List<AudioClip>(loaded.Length);
                foreach (var clip in loaded)
                {
                    if (clip != null && clip.name.StartsWith(MoveClipPrefix)) valid.Add(clip);
                }

                _moveClips = valid.ToArray();
            }
        }

        public void PlayRandomMove()
        {
            if (_moveClips == null || _moveClips.Length == 0) return;

            int index = PickIndex();
            var clip = _moveClips[index];
            if (clip == null) return;

            _lastIndex = index;
            _audioSource.PlayOneShot(clip, _volume);
        }

        private int PickIndex()
        {
            if (_moveClips.Length <= 1) return 0;

            int index = Random.Range(0, _moveClips.Length);
            if (_avoidImmediateRepeat && index == _lastIndex)
            {
                index = (index + Random.Range(1, _moveClips.Length)) % _moveClips.Length;
            }

            return index;
        }
    }
}
