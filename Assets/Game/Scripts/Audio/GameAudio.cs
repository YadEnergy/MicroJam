using System.Collections.Generic;
using UnityEngine;

namespace MicroJam.Game
{
    public sealed class GameAudio : MonoBehaviour
    {
        private const string SfxVolumeKey = "Audio.SfxVolume";
        private const string MusicVolumeKey = "Audio.MusicVolume";
        private static GameAudio instance;
        [SerializeField] private GameAudioCatalog catalog;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSources;
        private float sfxVolume = 1f;
        private float musicVolume = 1f;
        private float nextHumanStepTime;
        private float nextDinosaurStepTime;

        public static float SfxVolume => Instance != null ? Instance.sfxVolume : 1f;
        public static float MusicVolume => Instance != null ? Instance.musicVolume : 1f;
        private static GameAudio Instance
        {
            get
            {
                if (instance == null) instance = FindFirstObjectByType<GameAudio>(FindObjectsInactive.Include);
                if (instance == null) Debug.LogError("GameAudio is missing from the startup scene.");
                return instance;
            }
        }

        public void Configure(GameAudioCatalog configuredCatalog, AudioSource configuredMusicSource, AudioSource[] configuredOneShotSources)
        {
            catalog = configuredCatalog;
            musicSource = configuredMusicSource;
            sfxSources = configuredOneShotSources != null && configuredOneShotSources.Length > 0 ? configuredOneShotSources[0] : null;
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
            musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
            if (musicSource != null) musicSource.volume = musicVolume;
        }

        public static void Play(GameSound sound)
        {
            GameAudio audio = Instance;
            AudioClip clip = audio != null && audio.catalog != null ? audio.catalog.GetClip(sound) : null;
            if (clip == null) return;
            AudioSource source = audio.GetAvailableSource();
            if (source == null) return;
            source.PlayOneShot(clip, audio.sfxVolume * audio.catalog.GetVolume(sound));
        }

        public static void PlayHumanAttack(IEnumerable<Health> targets)
        {
            GameSound selected = GameSound.HumanMiss;
            int priority = 0;
            bool hitDinosaur = false;
            if (targets != null)
            {
                foreach (Health target in targets)
                {
                    if (target == null) continue;
                    ResourceNode resource = target.GetComponentInParent<ResourceNode>();
                    if (resource != null && priority < 1)
                    {
                        selected = resource.NodeType switch
                        {
                            ResourceNodeType.Bush => GameSound.HumanHitBush,
                            ResourceNodeType.Rock => GameSound.HumanHitRock,
                            ResourceNodeType.Tree => GameSound.HumanHitTree,
                            _ => GameSound.HumanMiss
                        };
                        priority = 1;
                    }
                    else if (target.GetComponentInParent<DinosaurAgent>() != null)
                    {
                        hitDinosaur = true;
                    }
                }
            }
            // Dinosaur hit audio is emitted by DinosaurAgent for every successful
            // damage event, including tower projectiles. Avoid playing it twice here.
            if (hitDinosaur && priority == 0) return;
            Play(selected);
        }

        public static void PlayDinosaurAttack(Health target, bool hit)
        {
            if (!hit || target == null) { Play(GameSound.DinosaurMiss); return; }
            Transform targetTransform = target.transform;
            if (targetTransform.GetComponentInParent<PlayerMovement>() != null) Play(GameSound.DinosaurHitPlayer);
            // CampfireInteraction emits its own sound from Health.DamageReceived so it is
            // tied to applied damage rather than specifically to a dinosaur attack.
            else if (targetTransform.GetComponentInParent<CampfireInteraction>() != null) return;
            else Play(GameSound.DinosaurHitBuilding);
        }

        public static void ReportHumanWalking(bool walking)
        {
            GameAudio audio = Instance;
            if (audio == null || !walking || Time.time < audio.nextHumanStepTime) return;
            audio.nextHumanStepTime = Time.time + audio.GetClipDuration(GameSound.HumanSteps, 0.35f);
            Play(GameSound.HumanSteps);
        }

        public static void ReportDinosaurWalking()
        {
            GameAudio audio = Instance;
            if (audio == null || Time.time < audio.nextDinosaurStepTime) return;
            audio.nextDinosaurStepTime = Time.time + audio.GetClipDuration(GameSound.DinosaurWalk, 0.45f);
            Play(GameSound.DinosaurWalk);
        }

        public static void PlayMusic(AudioClip clip, bool loop = true)
        {
            GameAudio audio = Instance;
            if (audio == null || audio.musicSource == null) return;
            audio.musicSource.clip = clip;
            audio.musicSource.loop = loop;
            if (clip != null) audio.musicSource.Play();
        }

        public static void StopMusic()
        {
            GameAudio audio = Instance;
            if (audio != null && audio.musicSource != null) audio.musicSource.Stop();
        }

        public static void SetSfxVolume(float value)
        {
            GameAudio audio = Instance;
            if (audio == null) return;
            audio.sfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, audio.sfxVolume);
        }

        public static void SetMusicVolume(float value)
        {
            GameAudio audio = Instance;
            if (audio == null) return;
            audio.musicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, audio.musicVolume);
            if (audio.musicSource != null) audio.musicSource.volume = audio.musicVolume;
        }

        private AudioSource GetAvailableSource()
        {
            if (sfxSources == null) Debug.LogError("GameAudio has no preconfigured SFX AudioSource.", this);
            return sfxSources;
        }

        private float GetClipDuration(GameSound sound, float fallback)
        {
            AudioClip clip = catalog != null ? catalog.GetClip(sound) : null;
            return clip != null ? Mathf.Max(fallback, clip.length) : fallback;
        }

    }
}
