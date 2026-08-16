using UnityEngine;
using UnityEngine.Audio;

namespace MicroJam.Game
{
    [CreateAssetMenu(fileName = "GameAudioCatalog", menuName = "MicroJam/Audio/Game Audio Catalog")]
    public sealed class GameAudioCatalog : ScriptableObject
    {
        [Header("Optional Unity Audio Mixer routing")]
        [SerializeField] private AudioMixerGroup soundEffectsOutput;
        [SerializeField] private AudioMixerGroup musicOutput;

        [Header("Buildings")]
        [SerializeField] private AudioClip buildStructure;
        [SerializeField] private AudioClip buildTower;
        [SerializeField] private AudioClip destroyStructure;
        [SerializeField] private AudioClip destroyTower;

        [Header("Dinosaur")]
        [SerializeField] private AudioClip dinosaurDeath;
        [SerializeField] private AudioClip dinosaurHitBuilding;
        [SerializeField] private AudioClip dinosaurHitCampfire;
        [SerializeField] private AudioClip dinosaurHitPlayer;
        [SerializeField] private AudioClip dinosaurMiss;
        [SerializeField] private AudioClip dinosaurWalk;

        [Header("Human")]
        [SerializeField] private AudioClip humanHitBush;
        [SerializeField] private AudioClip humanHitDinosaur;
        [SerializeField] private AudioClip humanHitRock;
        [SerializeField] private AudioClip humanHitTree;
        [SerializeField] private AudioClip humanMiss;
        [SerializeField] private AudioClip humanDeath;
        [SerializeField] private AudioClip humanSteps;

        public AudioMixerGroup SoundEffectsOutput => soundEffectsOutput;
        public AudioMixerGroup MusicOutput => musicOutput;

        public AudioClip GetClip(GameSound sound)
        {
            return sound switch
            {
                GameSound.BuildStructure => buildStructure,
                GameSound.BuildTower => buildTower != null ? buildTower : buildStructure,
                GameSound.DestroyStructure => destroyStructure,
                GameSound.DestroyTower => destroyTower != null ? destroyTower : destroyStructure,
                GameSound.DinosaurDeath => dinosaurDeath,
                GameSound.DinosaurHitBuilding => dinosaurHitBuilding,
                GameSound.DinosaurHitCampfire => dinosaurHitCampfire != null ? dinosaurHitCampfire : dinosaurHitBuilding,
                GameSound.DinosaurHitPlayer => dinosaurHitPlayer,
                GameSound.DinosaurMiss => dinosaurMiss,
                GameSound.DinosaurWalk => dinosaurWalk,
                GameSound.HumanHitBush => humanHitBush,
                GameSound.HumanHitDinosaur => humanHitDinosaur,
                GameSound.HumanHitRock => humanHitRock,
                GameSound.HumanHitTree => humanHitTree != null ? humanHitTree : humanHitRock,
                GameSound.HumanMiss => humanMiss,
                GameSound.HumanDeath => humanDeath,
                GameSound.HumanSteps => humanSteps,
                _ => null
            };
        }
    }
}
