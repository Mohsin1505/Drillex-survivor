using UnityEngine;
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var am = AudioManager.Instance;
        result.Log("playing {0} am {1} lib {2} menu {3} game {4} musicVol {5} on {6} master {7} listener {8}", EditorApplication.isPlaying, am != null, am && am.Library, am && am.Library ? am.Library.menuMusic : null, am && am.Library ? am.Library.gameplayMusic : null, am ? am.MusicVolume : -1, am ? am.MusicOn : false, AudioListener.volume, Object.FindAnyObjectByType<AudioListener>() != null);
        if (am) foreach (var s in am.GetComponents<AudioSource>()) if (s.loop) result.Log("src clip {0} playing {1} vol {2} state {3}", s.clip ? s.clip.name : "-", s.isPlaying, s.volume, s.clip ? s.clip.loadState.ToString() : "");
    }
}
