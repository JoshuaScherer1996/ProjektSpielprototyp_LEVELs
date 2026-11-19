using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Levels.Audio
{
    public enum Cue
    {
        Navigate, Confirm, Pause, Continue, ShopOpen, Purchase, Rejected, Well,
        PlayerLevel, EnemyLevel, Gravel, Grass, Stone, GoblinStep, CyclopsStep,
        Sword, PlayerPain, PlayerDeath, Potion, Shield, ShieldBreak, Ring,
        GoblinPain, GoblinClaw, GoblinDeath, CyclopsPain, CyclopsClub, CyclopsDeath, TowerDoor,
        SpiderLand, SpiderIntroRumble, SpiderRumble, SpiderIntro, SpiderScuttle, SpiderPain, SpiderMelee,
        WebSpit, WebVolley, WebFlight, WebImpact, WebStep, SpiderPhase, WebWave, SpiderRise,
        SpiderCeiling, SpiderFall, SpiderSpin, SpiderQuickLeap, SpiderQuickLand, SpiderDeath
    }

    public enum SoundBus { World, UI, Ending, Door }

    [Serializable]
    public sealed class SoundCue
    {
        public Cue cue;
        public AudioClip[] clips;
        public SoundBus bus;
        [Range(0f, 1f)] public float gain = .5f;
        [Range(.5f, 2f)] public float pitch = 1f;
        [Range(0f, .2f)] public float pitchVariation;
        [Min(0f)] public float cooldown;
        [Min(0f)] public float audibleDistance;
        [Range(0, 10)] public int priority = 4;
        [Min(0)] public int maxVoices = 1;
    }

    public sealed class AudioLibrary : ScriptableObject
    {
        public AudioMixer mixer;
        [Range(0f, 1f)] public float musicGain = .55f;
        [Range(0f, 1f)] public float sfxGain = .8f;
        [Range(0f, 1f)] public float pausedMusicGain = .25f;
        [Range(.05f, 1f)] public float musicFade = .22f;
        [Range(6, 24)] public int voiceCount = 14;
        [Range(1, 6)] public int enemyStepVoices = 2;
        [Min(.1f)] public float playerStride = 1.28f;
        [Min(.1f)] public float goblinStride = .62f;
        [Min(.1f)] public float cyclopsStride = 1.22f;
        [Min(.1f)] public float spiderStride = 1.1f;
        public SoundCue[] cues = Array.Empty<SoundCue>();
    }
}
