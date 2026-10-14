using UnityEngine;

// All Drillex music and sound effects in one place. Lives at Resources/DrillexAudioLibrary so the
// persistent AudioManager can load it from any scene without scene references.
[CreateAssetMenu(menuName = "Drillex/Audio Library", fileName = "DrillexAudioLibrary")]
public class AudioLibrary : ScriptableObject
{
    [Header("Music")]
    public AudioClip menuMusic;
    public AudioClip gameplayMusic;
    [Range(0f, 1f)] public float musicBaseVolume = 0.55f;

    [Header("UI")]
    public AudioClip uiClick;
    public AudioClip uiOpen;
    public AudioClip uiClose;
    public AudioClip purchase;
    public AudioClip denied;
    public AudioClip unlock;
    public AudioClip levelUp;
    public AudioClip orbPickup;
    public AudioClip orbDeposit;
    public AudioClip warning;

    [Header("Weapons")]
    public AudioClip basicShot;
    public AudioClip doubleShot;
    public AudioClip machineGunShot;
    public AudioClip rocketLaunch;
    public AudioClip explosion;
    public AudioClip electricShot;
    public AudioClip electricImpact;
    public AudioClip bulletImpact;
    public AudioClip flameLoop;
    public AudioClip trapDeploy;
}
