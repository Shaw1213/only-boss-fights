using System.Text;
using System.Threading.Tasks;
using DreamLayerJam;
using UnityEngine;

/// <summary>Measures how game time advances against real time and awaited frames (Play mode diagnostics).</summary>
public static class ProbeClock
{
    public static async Task<string> Run()
    {
        var sb = new StringBuilder();
        var hero = Object.FindAnyObjectByType<HeroController>();
        float rt0 = Time.realtimeSinceStartup, t0 = Time.time;
        int f0 = Time.frameCount;
        sb.AppendLine($"start: timeScale={Time.timeScale} frame={f0} time={t0:0.000} hitStopFrozen={HitStop.IsFrozen} hero={hero?.CurrentState}");
        int awaits = 0;
        while (Time.realtimeSinceStartup - rt0 < 0.5f)
        {
            await Awaitable.NextFrameAsync();
            awaits++;
        }
        sb.AppendLine($"after 0.5s real: awaits={awaits} frames={Time.frameCount - f0} gameTime={Time.time - t0:0.000} timeScale={Time.timeScale} dt={Time.deltaTime:0.0000} unscaledDt={Time.unscaledDeltaTime:0.0000} maxDt={Time.maximumDeltaTime}");
        sb.AppendLine($"captureFramerate={Time.captureFramerate} targetFrameRate={Application.targetFrameRate} runInBackground={Application.runInBackground} isFocused={Application.isFocused}");
        return sb.ToString();
    }
}
