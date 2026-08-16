using UnityEngine;
using UnityEngine.UI;

namespace MicroJam.Game
{
    public sealed class AudioVolumeControls : MonoBehaviour
    {
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider soundEffectsSlider;

        public float SoundEffectsVolume => GameAudio.SfxVolume;
        public float MusicVolume => GameAudio.MusicVolume;

        public void Configure(Slider music, Slider soundEffects)
        {
            musicSlider = music;
            soundEffectsSlider = soundEffects;
        }

        private void OnEnable()
        {
            if (musicSlider != null)
            {
                musicSlider.SetValueWithoutNotify(GameAudio.MusicVolume);
                musicSlider.onValueChanged.AddListener(SetMusicVolume);
            }

            if (soundEffectsSlider != null)
            {
                soundEffectsSlider.SetValueWithoutNotify(GameAudio.SfxVolume);
                soundEffectsSlider.onValueChanged.AddListener(SetSoundEffectsVolume);
            }
        }

        private void OnDisable()
        {
            musicSlider?.onValueChanged.RemoveListener(SetMusicVolume);
            soundEffectsSlider?.onValueChanged.RemoveListener(SetSoundEffectsVolume);
        }

        public void SetSoundEffectsVolume(float value) => GameAudio.SetSfxVolume(value);
        public void SetMusicVolume(float value) => GameAudio.SetMusicVolume(value);
    }
}
