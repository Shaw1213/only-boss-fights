using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DreamLayerJam;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play-mode checks for the Animation Gym (open AnimGym.unity first).
///   All:  walks every character and clip, sampling 5 moments, and reports any frame without a sprite.
///   Shot: shows one clip at a time and pauses, for capture_game_view. Args: [character, clip index, seconds].
/// </summary>
public static class GymCheck
{
    const BindingFlags NonPublic = BindingFlags.NonPublic | BindingFlags.Instance;

    public static async Task<string> All()
    {
        var gym = Object.FindAnyObjectByType<AnimationGym>();
        if (gym == null) return "no AnimationGym in the scene";
        var t = typeof(AnimationGym);
        var characters = (System.Collections.IList)t.GetField("characters", NonPublic).GetValue(gym);
        var sb = new StringBuilder();
        int problems = 0;
        for (int ci = 0; ci < characters.Count; ci++)
        {
            gym.SelectCharacter(ci);
            var clips = (System.Collections.IList)characters[ci].GetType().GetField("clips").GetValue(characters[ci]);
            sb.Append($"{characters[ci].GetType().GetField("name").GetValue(characters[ci])}: ");
            for (int k = 0; k < clips.Count; k++)
            {
                gym.SelectClip(k);
                var clip = clips[k];
                float duration = (float)clip.GetType().GetProperty("Duration").GetValue(clip);
                for (int i = 0; i < 5; i++)
                {
                    t.GetField("time", NonPublic).SetValue(gym, duration * i / 4f);
                    t.GetField("playing", NonPublic).SetValue(gym, false);
                    await Awaitable.NextFrameAsync();
                    if (gym.subject.sprite == null) { problems++; sb.Append($"[no sprite: {clip.GetType().GetField("name").GetValue(clip)} @{duration * i / 4f:0.00}s] "); }
                }
                sb.Append($"{clip.GetType().GetField("name").GetValue(clip)} ({duration:0.00}s), ");
            }
            sb.AppendLine();
        }
        sb.AppendLine(problems == 0 ? "every sampled frame has a sprite" : $"{problems} problems");
        return sb.ToString();
    }

    public static async Task<string> Shot(float character, float clipIndex, float seconds)
    {
        EditorApplication.isPaused = false;
        var gym = Object.FindAnyObjectByType<AnimationGym>();
        var t = typeof(AnimationGym);
        gym.SelectCharacter((int)character);
        gym.SelectClip((int)clipIndex);
        t.GetField("time", NonPublic).SetValue(gym, seconds);
        t.GetField("playing", NonPublic).SetValue(gym, false);
        await Awaitable.NextFrameAsync();
        await Awaitable.NextFrameAsync();
        EditorApplication.isPaused = true;
        return $"paused on clip {clipIndex} @ {seconds:0.00}s";
    }
}
