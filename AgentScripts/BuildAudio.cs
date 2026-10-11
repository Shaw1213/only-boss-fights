using System;
using System.IO;
using System.Linq;
using System.Text;
using DreamLayerJam;
using DreamLayerJam.EditorTools;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds Assets/Resources/SoundBank.asset from Assets/Audio/sound_bank.json (Tools/build_sound_bank.py: which clips
/// each cue uses and the loudness-measured volumes), and sets the clips' import settings. Music comes from
/// Assets/Audio/Music (Tools/prepare_music.py); empty music slots get the default picks, filled ones are kept, so a
/// track swapped on the asset by hand survives a rebuild. Re-runnable.
///   python -I Tools/build_sound_bank.py --packs <unzipped packs>
///   python -I Tools/prepare_music.py --src <downloaded tracks>
///   unity command run_script --file AgentScripts/BuildAudio.cs --entry BuildAudio.All
/// </summary>
public static class BuildAudio
{
    const string SpecPath = "Assets/Audio/sound_bank.json";
    const string BankPath = "Assets/Resources/SoundBank.asset";
    const string MusicDir = "Assets/Audio/Music";

    public static string All()
    {
        var log = new StringBuilder();
        AssetDatabase.Refresh();
        var spec = JsonUtility.FromJson<SoundBankSpec>(File.ReadAllText(SpecPath));
        if (spec?.cues == null || spec.cues.Length == 0) return $"no cues read from {SpecPath}";

        // Short effects: decompressed on load (no decode cost when they fire), mono.
        foreach (var path in spec.cues.SelectMany(c => c.clips.Concat(c.layer ?? new string[0])).Distinct())
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter imp)) { log.AppendLine($"missing {path}"); continue; }
            var s = imp.defaultSampleSettings;
            if (imp.forceToMono && s.loadType == AudioClipLoadType.DecompressOnLoad && s.compressionFormat == AudioCompressionFormat.Vorbis) continue;
            imp.forceToMono = true;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }

        // Music: stereo, streamed from disk instead of decoded into memory (minutes of audio, played one at a time).
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicDir }))
        {
            if (!(AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) is AudioImporter imp)) continue;
            var s = imp.defaultSampleSettings;
            if (!imp.forceToMono && s.loadType == AudioClipLoadType.Streaming && s.compressionFormat == AudioCompressionFormat.Vorbis) continue;
            imp.forceToMono = false;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.6f;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<SoundBank>();
            AssetDatabase.CreateAsset(bank, BankPath);
            log.AppendLine($"created {BankPath}");
        }

        AudioClip[] Load(string[] paths) => (paths ?? new string[0]).Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c => c != null).ToArray();
        bank.cues = spec.cues.Select(c =>
        {
            if (!Enum.TryParse(c.id, out Sfx id)) { log.AppendLine($"unknown cue id {c.id} (add it to the Sfx enum)"); return null; }
            return new SoundBank.Cue
            {
                id = id, clips = Load(c.clips), layer = Load(c.layer), volume = c.volume, layerVolume = c.layerVolume,
                pitch = new Vector2(c.pitchMin, c.pitchMax), cooldown = c.cooldown,
            };
        }).Where(c => c != null).ToArray();

        AudioClip Music(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{MusicDir}/{name}.ogg");
        if (bank.lobbyMusic == null) bank.lobbyMusic = Music("lobby_artifact_room");
        if (bank.fightMusic == null) bank.fightMusic = Music("fight_epic_boss_battle");
        if (bank.fightPhase2Music == null) bank.fightPhase2Music = Music("fight_phase2_determined_pursuit");
        if (bank.endingMusic == null) bank.endingMusic = Music("lobby_alt_cathedral_in_the_forest");

        var missing = ((Sfx[])Enum.GetValues(typeof(Sfx))).Where(s => s != Sfx.None && bank.cues.All(c => c.id != s)).ToArray();
        if (missing.Length > 0) log.AppendLine("no clips yet for: " + string.Join(", ", missing));
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        log.AppendLine($"SoundBank: {bank.cues.Length} cues, {bank.cues.Sum(c => c.clips.Length + c.layer.Length)} clips; " +
                       $"music: lobby {Name(bank.lobbyMusic)}, fight {Name(bank.fightMusic)}, phase 2 {Name(bank.fightPhase2Music)}, ending {Name(bank.endingMusic)}");
        return log.ToString();
    }

    static string Name(AudioClip c) => c != null ? c.name : "-";
}
