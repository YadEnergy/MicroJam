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
        [SerializeField, Range(0f, 1f)] private float buildStructureVolume = 1f;
        [SerializeField] private AudioClip buildTower;
        [SerializeField, Range(0f, 1f)] private float buildTowerVolume = 1f;
        [SerializeField] private AudioClip destroyStructure;
        [SerializeField, Range(0f, 1f)] private float destroyStructureVolume = 1f;
        [SerializeField] private AudioClip destroyTower;
        [SerializeField, Range(0f, 1f)] private float destroyTowerVolume = 1f;

        [Header("Dinosaur")]
        [SerializeField] private AudioClip dinosaurDeath;
        [SerializeField, Range(0f, 1f)] private float dinosaurDeathVolume = 1f;
        [SerializeField] private AudioClip dinosaurHitBuilding;
        [SerializeField, Range(0f, 1f)] private float dinosaurHitBuildingVolume = 1f;
        [SerializeField] private AudioClip dinosaurHitCampfire;
        [SerializeField, Range(0f, 1f)] private float dinosaurHitCampfireVolume = 1f;
        [SerializeField] private AudioClip dinosaurHitPlayer;
        [SerializeField, Range(0f, 1f)] private float dinosaurHitPlayerVolume = 1f;
        [SerializeField] private AudioClip dinosaurMiss;
        [SerializeField, Range(0f, 1f)] private float dinosaurMissVolume = 1f;
        [SerializeField] private AudioClip dinosaurWalk;
        [SerializeField, Range(0f, 1f)] private float dinosaurWalkVolume = 1f;
        [SerializeField] private AudioClip dinosaurGetHit;
        [SerializeField, Range(0f, 1f)] private float dinosaurGetHitVolume = 1f;

        [Header("Human")]
        [SerializeField] private AudioClip humanHitBush;
        [SerializeField, Range(0f, 1f)] private float humanHitBushVolume = 1f;
        [SerializeField] private AudioClip humanHitRock;
        [SerializeField, Range(0f, 1f)] private float humanHitRockVolume = 1f;
        [SerializeField] private AudioClip humanHitTree;
        [SerializeField, Range(0f, 1f)] private float humanHitTreeVolume = 1f;
        [SerializeField] private AudioClip humanMiss;
        [SerializeField, Range(0f, 1f)] private float humanMissVolume = 1f;
        [SerializeField] private AudioClip humanDeath;
        [SerializeField, Range(0f, 1f)] private float humanDeathVolume = 1f;
        [SerializeField] private AudioClip humanSteps;
        [SerializeField, Range(0f, 1f)] private float humanStepsVolume = 1f;

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
                GameSound.DinosaurGetHit => dinosaurGetHit,
                GameSound.HumanHitBush => humanHitBush,
                GameSound.HumanHitRock => humanHitRock,
                GameSound.HumanHitTree => humanHitTree != null ? humanHitTree : humanHitRock,
                GameSound.HumanMiss => humanMiss,
                GameSound.HumanDeath => humanDeath,
                GameSound.HumanSteps => humanSteps,
                _ => null
            };
        }

        public float GetVolume(GameSound sound)
        {
            return sound switch
            {
                GameSound.BuildStructure => buildStructureVolume,
                GameSound.BuildTower => buildTower != null ? buildTowerVolume : buildStructureVolume,
                GameSound.DestroyStructure => destroyStructureVolume,
                GameSound.DestroyTower => destroyTower != null ? destroyTowerVolume : destroyStructureVolume,
                GameSound.DinosaurDeath => dinosaurDeathVolume,
                GameSound.DinosaurHitBuilding => dinosaurHitBuildingVolume,
                GameSound.DinosaurHitCampfire => dinosaurHitCampfire != null ? dinosaurHitCampfireVolume : dinosaurHitBuildingVolume,
                GameSound.DinosaurHitPlayer => dinosaurHitPlayerVolume,
                GameSound.DinosaurMiss => dinosaurMissVolume,
                GameSound.DinosaurWalk => dinosaurWalkVolume,
                GameSound.DinosaurGetHit => dinosaurGetHitVolume,
                GameSound.HumanHitBush => humanHitBushVolume,
                GameSound.HumanHitRock => humanHitRockVolume,
                GameSound.HumanHitTree => humanHitTree != null ? humanHitTreeVolume : humanHitRockVolume,
                GameSound.HumanMiss => humanMissVolume,
                GameSound.HumanDeath => humanDeathVolume,
                GameSound.HumanSteps => humanStepsVolume,
                _ => 1f
            };
        }
    }
}
