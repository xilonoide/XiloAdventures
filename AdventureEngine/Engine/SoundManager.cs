using System;
using System.IO;
using System.Security.Cryptography;
using NAudio.Wave;

namespace XiloAdventures.Engine;

public class SoundManager : IDisposable
{
    private readonly string _soundFolder;

    private IWavePlayer? _worldMusicPlayer;
    private AudioFileReader? _worldMusicReader;
    private string? _worldCurrentPath;

    private IWavePlayer? _roomMusicPlayer;
    private AudioFileReader? _roomMusicReader;
    private string? _roomCurrentPath;

    public bool SoundEnabled { get; set; } = true;

    public SoundManager(string soundFolder)
    {
        _soundFolder = soundFolder;
    }

    /// <summary>
    /// Reproduce la música global del mundo. Mantiene el reproductor activo
    /// (para que el tiempo avance) aunque en ocasiones podamos silenciarlo.
    /// </summary>
    public void PlayWorldMusic(string? worldMusicId, string? worldMusicBase64)
    {
        if (!SoundEnabled)
        {
            StopWorldMusic();
            return;
        }

        if (string.IsNullOrWhiteSpace(worldMusicId) && string.IsNullOrWhiteSpace(worldMusicBase64))
        {
            StopWorldMusic();
            return;
        }

        var path = EnsureAudioFile(worldMusicId, worldMusicBase64);
        if (path == null || !File.Exists(path))
        {
            StopWorldMusic();
            return;
        }

        // Si ya estamos reproduciendo este mismo archivo, no reiniciamos la música.
        if (_worldMusicPlayer != null && string.Equals(_worldCurrentPath, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        StopWorldMusic();

        try
        {
            _worldMusicReader = new AudioFileReader(path);
            _worldMusicPlayer = new WaveOutEvent();
            _worldMusicPlayer.Init(_worldMusicReader);
            _worldCurrentPath = path;
            _worldMusicPlayer.Play();
        }
        catch
        {
            StopWorldMusic();
        }
    }

        /// <summary>
    /// Reproduce la música especial de una sala.
    /// - Si la sala tiene música propia: se silencia la música del mundo y se reproduce la de la sala.
    /// - Si la sala no tiene música propia: se para la de sala (si sonaba) y se restablece el volumen de la del mundo.
    /// La música global del mundo se arranca desde GameEngine al iniciar la partida.
    /// </summary>
    public void PlayRoomMusic(string? musicId, string? musicBase64)
    {
        if (!SoundEnabled)
        {
            StopRoomMusic();
            return;
        }

        var hasRoomMusic = !string.IsNullOrWhiteSpace(musicId) || !string.IsNullOrWhiteSpace(musicBase64);

        if (!hasRoomMusic)
        {
            // No hay música especial: parar música de sala y restaurar volumen del mundo.
            StopRoomMusic();
            SetWorldMusicVolume(1.0f);
            return;
        }

        // Hay música especial de sala: silenciamos la música global del mundo (pero sigue avanzando)
        SetWorldMusicVolume(0.0f);

        var path = EnsureAudioFile(musicId, musicBase64);
        if (path == null || !File.Exists(path))
        {
            // Si no podemos reproducir la música especial, restauramos música del mundo audible.
            StopRoomMusic();
            SetWorldMusicVolume(1.0f);
            return;
        }

        // Si ya estamos reproduciendo esta misma pista de sala, no hacemos nada.
        if (_roomMusicPlayer != null && string.Equals(_roomCurrentPath, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        StopRoomMusic();

        try
        {
            _roomMusicReader = new AudioFileReader(path);
            _roomMusicPlayer = new WaveOutEvent();
            _roomMusicPlayer.Init(_roomMusicReader);
            _roomCurrentPath = path;
            _roomMusicPlayer.Play();
        }
        catch
        {
            StopRoomMusic();
        }
    }


    private string? EnsureAudioFile(string? fileName, string? base64)
    {
        if (string.IsNullOrWhiteSpace(fileName) && string.IsNullOrWhiteSpace(base64))
            return null;

        Directory.CreateDirectory(_soundFolder);

        // Si no tenemos nombre pero sí contenido Base64, generamos un nombre determinista
        // a partir de un hash del audio. Así, la misma pista siempre usa el mismo archivo
        // y no se reinicia la música al volver a llamarla.
        if (string.IsNullOrWhiteSpace(fileName) && !string.IsNullOrWhiteSpace(base64))
        {
            try
            {
                var bytes = Convert.FromBase64String(base64);
                using var sha1 = SHA1.Create();
                var hash = sha1.ComputeHash(bytes);
                var hashHex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                fileName = $"audio_{hashHex}.bin";
            }
            catch
            {
                return null;
            }
        }

        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var path = Path.Combine(_soundFolder, fileName);

        if (!string.IsNullOrWhiteSpace(base64))
        {
            try
            {
                var bytes = Convert.FromBase64String(base64);
                File.WriteAllBytes(path, bytes);
            }
            catch
            {
                return null;
            }
        }

        return path;
    }

    private void SetWorldMusicVolume(float volume)
    {
        if (_worldMusicReader != null)
        {
            _worldMusicReader.Volume = volume;
        }
    }

    private void StopWorldMusic()
    {
        try
        {
            _worldMusicPlayer?.Stop();
            _worldMusicPlayer?.Dispose();
            _worldMusicReader?.Dispose();
        }
        catch
        {
            // Ignorar
        }
        finally
        {
            _worldMusicPlayer = null;
            _worldMusicReader = null;
            _worldCurrentPath = null;
        }
    }

    private void StopRoomMusic()
    {
        try
        {
            _roomMusicPlayer?.Stop();
            _roomMusicPlayer?.Dispose();
            _roomMusicReader?.Dispose();
        }
        catch
        {
            // Ignorar
        }
        finally
        {
            _roomMusicPlayer = null;
            _roomMusicReader = null;
            _roomCurrentPath = null;
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
        StopRoomMusic();
        StopWorldMusic();
    }

    public void Dispose()
    {
        StopMusic();
    }
}