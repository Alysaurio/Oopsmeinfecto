using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Manages game audio: looping background music, movement SFX, win/loss SFX, and ducking.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _sfxSource;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip _musicLevelGame;
        [SerializeField] private AudioClip _movePlayer;
        [SerializeField] private AudioClip _moveEnemy;
        [SerializeField] private AudioClip _lose;
        [SerializeField] private AudioClip _lose2;
        [SerializeField] private AudioClip _win;

        private Coroutine _duckingCoroutine;
        private bool _defeatAudioTriggered = false;

        public AudioSource MusicSource => _musicSource;
        public AudioSource SfxSource => _sfxSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            EnsureSources();
            LoadClips();
        }

        private void Start()
        {
            PlayMusic();
        }

        private void EnsureSources()
        {
            if (_musicSource == null)
            {
                var musicChild = new GameObject("MusicSource");
                musicChild.transform.SetParent(transform, false);
                _musicSource = musicChild.AddComponent<AudioSource>();
            }
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _musicSource.volume = 1f;

            if (_sfxSource == null)
            {
                var sfxChild = new GameObject("SfxSource");
                sfxChild.transform.SetParent(transform, false);
                _sfxSource = sfxChild.AddComponent<AudioSource>();
            }
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;
            _sfxSource.volume = 1f;
        }

        private void LoadClips()
        {
            if (_musicLevelGame == null) _musicLevelGame = LoadClip("MusicLevelGame");
            if (_movePlayer == null) _movePlayer = LoadClip("MovePlayer");
            if (_moveEnemy == null) _moveEnemy = LoadClip("MoveEnemy");
            if (_lose == null) _lose = LoadClip("Lose");
            if (_lose2 == null) _lose2 = LoadClip("Lose2");
            if (_win == null) _win = LoadClip("Win");
        }

        private static AudioClip LoadClip(string name)
        {
            AudioClip clip = Resources.Load<AudioClip>("Sounds/" + name);
#if UNITY_EDITOR
            if (clip == null)
            {
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/" + name + ".mp3");
            }
#endif
            return clip;
        }

        public void PlayMusic()
        {
            if (_musicSource == null) return;
            if (_musicLevelGame != null)
            {
                if (_musicSource.clip != _musicLevelGame)
                {
                    _musicSource.clip = _musicLevelGame;
                }
                _musicSource.loop = true;
                _musicSource.volume = 1f;
                if (!_musicSource.isPlaying)
                {
                    _musicSource.Play();
                }
            }
        }

        public void PlayMovePlayer()
        {
            if (_sfxSource != null && _movePlayer != null)
            {
                _sfxSource.PlayOneShot(_movePlayer, 1.50f);
            }
        }

        public void PlayMoveEnemy()
        {
            if (_sfxSource != null && _moveEnemy != null)
            {
                _sfxSource.PlayOneShot(_moveEnemy, 1.50f);
            }
        }

        public void PlayVictory()
        {
            StartDuckingAndPlay(_win);
        }

        public void PlayDefeat(DefeatReason reason)
        {
            if (_defeatAudioTriggered) return;
            _defeatAudioTriggered = true;

            AudioClip clip = (reason == DefeatReason.DamageTile) ? _lose2 : _lose;
            StartDuckingAndPlay(clip);
        }

        private void StartDuckingAndPlay(AudioClip endClip)
        {
            if (_sfxSource != null && endClip != null)
            {
                _sfxSource.PlayOneShot(endClip);
            }

            if (_duckingCoroutine != null)
            {
                StopCoroutine(_duckingCoroutine);
            }
            _duckingCoroutine = StartCoroutine(DuckMusicRoutine());
        }

        private System.Collections.IEnumerator DuckMusicRoutine()
        {
            if (_musicSource == null) yield break;

            float startVolume = _musicSource.volume;
            float targetVolume = 0.1f;
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _musicSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
                yield return null;
            }

            _musicSource.volume = targetVolume;
            _duckingCoroutine = null;
        }

        public void RestoreMusicVolume()
        {
            _defeatAudioTriggered = false;
            if (_duckingCoroutine != null)
            {
                StopCoroutine(_duckingCoroutine);
                _duckingCoroutine = null;
            }
            if (_musicSource != null)
            {
                _musicSource.volume = 1f;
                if (!_musicSource.isPlaying && _musicLevelGame != null)
                {
                    _musicSource.Play();
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
