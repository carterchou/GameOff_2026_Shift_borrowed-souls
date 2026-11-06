using BorrowedSouls.Bootstrap;
using BorrowedSouls.Data;
using UnityEngine;

namespace BorrowedSouls.Rest
{
    /// <summary>One of the menu tracks, at the player's volume. The licensed tracks stay out of the public repo, so none may be there.</summary>
    public sealed class RestRoomMusic : MonoBehaviour
    {
        [SerializeField] private AudioSource _source;
        [SerializeField] private AudioBank _bank;

        private void Start()
        {
            AudioBank.MusicTrack[] tracks = _bank.MenuTracks;
            if (tracks.Length == 0) return;
            AudioBank.MusicTrack track = tracks[Random.Range(0, tracks.Length)];
            _source.clip = track.Clip;
            _source.volume = track.Volume * GameOptions.Load().MasterVolume;
            _source.Play();
        }
    }
}
