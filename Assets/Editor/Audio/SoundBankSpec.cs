using System;

namespace DreamLayerJam.EditorTools
{
    /// <summary>
    /// The shape of Assets/Audio/sound_bank.json (written by Tools/build_sound_bank.py, read by
    /// AgentScripts/BuildAudio.cs). Lives in the Editor assembly because JsonUtility only fills types from
    /// assemblies Unity loaded itself, not from a script compiled on the fly.
    /// </summary>
    [Serializable]
    public class SoundBankSpec
    {
        public Cue[] cues;

        [Serializable]
        public class Cue
        {
            public string id;
            public string category;
            public string[] clips;
            public string[] layer;
            public float volume, layerVolume, pitchMin, pitchMax, cooldown;
        }
    }
}
