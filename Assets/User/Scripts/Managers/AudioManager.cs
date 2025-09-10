
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [SerializeField] private AudioClip[] blastAudioClips;
    [SerializeField] private AudioClip pickupAudioClip;
    public static AudioManager Instance {  get; private set; }
    private void Awake()
    {
        Instance = this;
    }

    public void PlaySoundAtPoint(Vector3 point, AudioClip clip,float volume = 0.5f)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, point, volume * GameProperties.VolumeScale);
        else Debug.LogWarning("No clip was found");
    }
    public void PlayBlastAudioClipAt(Vector3 point,float volume = .8f)
    {
        int rand = Random.Range(0,blastAudioClips.Length);
        var clip = blastAudioClips[rand];
        AudioSource.PlayClipAtPoint(clip,point, volume * GameProperties.VolumeScale);
    }
}