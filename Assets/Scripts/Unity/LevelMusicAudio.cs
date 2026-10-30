using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Manages level background music playback.
    /// Loads MusicLevel1..MusicLevel4 from Resources/Sounds (or inspector assignments)
    /// and plays one randomly in loop when the game starts.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class LevelMusicAudio : MonoBehaviour
    {
        private const string MusicFolder = "Sounds";
        private const string MusicPrefix = "MusicLevel";

        [Header("Scene references")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip[] _musicClips = new AudioClip[0];

        [Header("Playback")]
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.4f;
        [SerializeField] private bool _playOnStart = true;
        [SerializeField] private bool _avoidImmediateRepeat = true;

        private int _currentIndex = -1;

        public AudioSource AudioSource => _audioSource;
        public AudioClip CurrentClip => _audioSource != null ? _audioSource.clip : null;
        public bool IsPlaying => _audioSource != null && _audioSource.isPlaying;
        public int CurrentIndex => _currentIndex;
        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Mathf.Clamp01(value);
                if (_audioSource != null) _audioSource.volume = _volume;
            }
        }

        private void Awake()
        {
            EnsureAudioSource();
            EnsureClipsLoaded();
        }

        private void Start()
        {
            if (_playOnStart && !IsPlaying)
            {
                PlayRandomTrack();
            }
        }

        public void EnsureAudioSource()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.playOnAwake = false;
            _audioSource.loop = true;
            _audioSource.spatialBlend = 0f; // 2D Stereo
            _audioSource.volume = _volume;
        }

        public void EnsureClipsLoaded()
        {
            if (_musicClips != null && _musicClips.Length > 0) return;

            var list = new List<AudioClip>(4);
            for (int i = 1; i <= 4; i++)
            {
                var clip = Resources.Load<AudioClip>($"{MusicFolder}/{MusicPrefix}{i}");
                if (clip != null) list.Add(clip);
            }

            if (list.Count == 0)
            {
                var loaded = Resources.LoadAll<AudioClip>(MusicFolder);
                Array.Sort(loaded, (left, right) =>
                    string.CompareOrdinal(left != null ? left.name : string.Empty,
                                          right != null ? right.name : string.Empty));

                foreach (var clip in loaded)
                {
                    if (clip != null && clip.name.StartsWith(MusicPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(clip);
                    }
                }
            }

            _musicClips = list.ToArray();
        }

        /// <summary>
        /// Plays a random track from MusicLevel1..MusicLevel4 in a loop.
        /// </summary>
        public void PlayRandomTrack(bool forceChange = false)
        {
            EnsureAudioSource();
            EnsureClipsLoaded();

            if (_musicClips == null || _musicClips.Length == 0)
            {
                Debug.LogWarning("[LevelMusicAudio] No music clips found in Resources/Sounds/MusicLevel*.");
                return;
            }

            if (!forceChange && IsPlaying)
            {
                return;
            }

            int index = PickRandomIndex();
            PlayTrack(index);
        }

        /// <summary>
        /// Plays a specific track index in loop.
        /// </summary>
        public void PlayTrack(int index)
        {
            EnsureAudioSource();
            EnsureClipsLoaded();

            if (_musicClips == null || _musicClips.Length == 0) return;

            if (index < 0 || index >= _musicClips.Length)
            {
                index = 0;
            }

            var clip = _musicClips[index];
            if (clip == null) return;

            _currentIndex = index;
            _audioSource.clip = clip;
            _audioSource.loop = true;
            _audioSource.volume = _volume;
            _audioSource.spatialBlend = 0f;
            _audioSource.Play();
        }

        public void Stop()
        {
            if (_audioSource != null) _audioSource.Stop();
        }

        public void Pause()
        {
            if (_audioSource != null) _audioSource.Pause();
        }

        public void Resume()
        {
            if (_audioSource != null) _audioSource.UnPause();
        }

        private int PickRandomIndex()
        {
            if (_musicClips.Length <= 1) return 0;

            int index = UnityEngine.Random.Range(0, _musicClips.Length);
            if (_avoidImmediateRepeat && index == _currentIndex)
            {
                index = (index + UnityEngine.Random.Range(1, _musicClips.Length)) % _musicClips.Length;
            }

            return index;
        }
    }
}
