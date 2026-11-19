using System.Collections.Generic;
using Levels.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Levels.Audio
{
    internal sealed class GameSfx
    {
        private sealed class Voice
        {
            public AudioSource source;
            public SoundCue cue;
            public Transform owner;
            public double endsAt;
            public int handle;
            public bool paused;
        }

        private readonly AudioLibrary library;
        private readonly Voice[] voices;
        private readonly Dictionary<Cue, SoundCue> cues = new();
        private readonly Dictionary<Cue, double> lastPlayed = new();
        private readonly Dictionary<Cue, AudioClip> lastClip = new();
        private readonly System.Random random = new();
        private int nextHandle;

        public GameSfx(GameObject root, AudioLibrary library, AudioMixerGroup group)
        {
            this.library = library;
            foreach (SoundCue cue in library.cues) cues[cue.cue] = cue;
            voices = new Voice[library.voiceCount];
            for (int i = 0; i < voices.Length; i++)
                voices[i] = new Voice { source = GameAudio.CreateSource(root, $"SFX {i + 1}", group, 120) };
        }

        public int Play(Cue id, float distance, bool loop, Transform owner, bool loading, out double duration)
        {
            duration = 0d;
            if (!cues.TryGetValue(id, out SoundCue cue) || cue.clips.Length == 0) return 0;
            bool lethalEnemyHit = GameFlow.State == RunState.GameOver &&
                (id == Cue.GoblinClaw || id == Cue.CyclopsClub);
            if (cue.bus == SoundBus.World && (loading || (!GameFlow.IsGameplayActive && !lethalEnemyHit))) return 0;

            float gain = cue.gain * library.sfxGain;
            if (cue.audibleDistance > 0f)
            {
                float attenuation = 1f - Mathf.Clamp01(distance / cue.audibleDistance);
                gain *= attenuation * attenuation;
            }
            if (gain < .01f) return 0;

            double now = AudioSettings.dspTime;
            if (lastPlayed.TryGetValue(id, out double previous) && now - previous < cue.cooldown) return 0;
            Voice voice = FindVoice(cue, now);
            if (voice == null) return 0;

            int clipIndex = random.Next(cue.clips.Length);
            if (cue.clips.Length > 1 && lastClip.TryGetValue(id, out AudioClip recent) && cue.clips[clipIndex] == recent)
                clipIndex = (clipIndex + 1) % cue.clips.Length;
            var clip = cue.clips[clipIndex];
            if (clip == null) return 0;

            Stop(voice);
            lastPlayed[id] = now;
            lastClip[id] = clip;
            voice.cue = cue;
            voice.owner = owner;
            voice.handle = ++nextHandle;
            voice.source.priority = 180 - cue.priority * 15;
            voice.source.pitch = cue.pitch + (float)(random.NextDouble() * 2d - 1d) * cue.pitchVariation;
            voice.source.volume = gain;
            voice.source.clip = clip;
            voice.source.loop = loop;
            duration = (double)clip.samples / clip.frequency / voice.source.pitch;
            voice.endsAt = loop ? double.PositiveInfinity : now + duration;
            voice.source.Play();
            return voice.handle;
        }

        private Voice FindVoice(SoundCue cue, double now)
        {
            Voice free = null;
            Voice replaceable = null;
            Voice same = null;
            int matching = 0;
            int enemySteps = 0;
            bool isEnemyStep = IsEnemyStep(cue.cue);
            foreach (Voice voice in voices)
            {
                if (voice.cue == null || (!voice.paused && now >= voice.endsAt))
                {
                    free ??= voice;
                    continue;
                }
                if (voice.cue == cue)
                {
                    matching++;
                    same ??= voice;
                }
                if (isEnemyStep && IsEnemyStep(voice.cue.cue)) enemySteps++;
                if (voice.cue.priority < cue.priority &&
                    (replaceable == null || voice.cue.priority < replaceable.cue.priority))
                    replaceable = voice;
            }
            if (cue.maxVoices > 0 && matching >= cue.maxVoices) return cue.bus == SoundBus.UI ? same : null;
            if (isEnemyStep && enemySteps >= library.enemyStepVoices) return null;
            return free ?? replaceable;
        }

        private static bool IsEnemyStep(Cue cue) => cue == Cue.GoblinStep || cue == Cue.CyclopsStep;

        public void StopWorld()
        {
            foreach (Voice voice in voices)
                if (voice.cue?.bus == SoundBus.World) Stop(voice);
        }

        public void StopCue(Cue cue)
        {
            foreach (Voice voice in voices)
                if (voice.cue?.cue == cue) Stop(voice);
        }

        public void StopForLoading()
        {
            foreach (Voice voice in voices)
                if (voice.cue != null && voice.cue.bus != SoundBus.Door) Stop(voice);
        }

        public void StopLoop(int handle)
        {
            if (handle == 0) return;
            foreach (Voice voice in voices)
                if (voice.handle == handle && voice.source.loop) Stop(voice);
        }

        public void Update()
        {
            foreach (Voice voice in voices)
                if (voice.cue != null && voice.source.loop && (voice.owner == null || !voice.owner.gameObject.activeInHierarchy))
                    Stop(voice);
        }

        public void SetPaused(bool paused)
        {
            double now = AudioSettings.dspTime;
            foreach (Voice voice in voices)
            {
                if (voice.cue?.bus != SoundBus.World || voice.paused == paused) continue;
                if (paused)
                {
                    if (now >= voice.endsAt) { Stop(voice); continue; }
                    voice.endsAt -= now;
                    voice.source.Pause();
                }
                else
                {
                    voice.endsAt += now;
                    voice.source.UnPause();
                }
                voice.paused = paused;
            }
        }

        private static void Stop(Voice voice)
        {
            voice.source.Stop();
            voice.source.clip = null;
            voice.cue = null;
            voice.owner = null;
            voice.handle = 0;
            voice.paused = false;
        }
    }
}
