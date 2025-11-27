using System;
using System.IO;
using NAudio.Wave;

namespace XiloAdventures.Engine;

public class SoundManager : IDisposable
{
    private readonly string _soundFolder;
    private IWavePlayer? _musicPlayer;
    private AudioFileReader? _musicReader;

    public bool SoundEnabled { get; set; } = true;

    public SoundManager(string soundFolder)
    {
        _soundFolder = soundFolder;
    }

    public void PlayWorldMusic(string? worldMusicId)
    {
        if (string.IsNullOrWhiteSpace(worldMusicId))
        {
            StopMusic();
            return;
        }

        PlayMusic(worldMusicId);
    }

    public void PlayRoomMusic(string? musicId, string? worldMusicIdFallback)
    {
        if (!SoundEnabled)
            return;

        if (!string.IsNullOrWhiteSpace(musicId))
        {
            PlayMusic(musicId);
        }
        else
        {
            PlayWorldMusic(worldMusicIdFallback);
        }
    }

    private void PlayMusic(string fileName)
    {
        if (!SoundEnabled)
            return;

        try
        {
            var path = Path.Combine(_soundFolder, fileName);
            if (!File.Exists(path))
                return;

            StopMusic();

            _musicReader = new AudioFileReader(path);
            _musicPlayer = new WaveOutEvent();
            _musicPlayer.Init(_musicReader);
            _musicPlayer.Play();
        }
        catch
        {
            // No romper la aventura si hay problemas de sonido.
        }
    }

    public void PlayEffect(string effectFileName)
    {
        if (!SoundEnabled)
            return;

        try
        {
            var path = Path.Combine(_soundFolder, effectFileName);
            if (!File.Exists(path))
                return;

            var reader = new AudioFileReader(path);
            var player = new WaveOutEvent();
            player.Init(reader);
            player.Play();

            player.PlaybackStopped += (_, _) =>
            {
                player.Dispose();
                reader.Dispose();
            };
        }
        catch
        {
            // Ignorar errores de sonido
        }
    }

    public void StopMusic()
    {
        try
        {
            _musicPlayer?.Stop();
            _musicPlayer?.Dispose();
            _musicReader?.Dispose();
        }
        catch
        {
            // Ignorar
        }
        finally
        {
            _musicPlayer = null;
            _musicReader = null;
        }
    }

    public void Dispose()
    {
        StopMusic();
    }
}
