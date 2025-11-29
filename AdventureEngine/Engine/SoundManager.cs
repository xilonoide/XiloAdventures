using System;
using System.IO;
using NAudio.Wave;

namespace XiloAdventures.Engine;

public class SoundManager : IDisposable
{
    private readonly string _soundFolder;

    private IWavePlayer? _worldMusicPlayer;
    private AudioFileReader? _worldMusicReader;
    private string? _worldMusicPath;

    private IWavePlayer? _roomMusicPlayer;
    private AudioFileReader? _roomMusicReader;
    private string? _roomMusicPath;

    public bool SoundEnabled { get; set; } = true;

    public SoundManager(string soundFolder)
    {
        _soundFolder = soundFolder;
        try
        {
            if (!Directory.Exists(_soundFolder))
                Directory.CreateDirectory(_soundFolder);
        }
        catch
        {
            // Si falla la creación de la carpeta, seguimos pero la música puede no funcionar correctamente.
        }
    }

    /// <summary>
    /// Reproduce la música global del mundo a partir de un identificador de archivo
    /// y/o un contenido en Base64. Si ambos están vacíos se detiene la música global.
    /// </summary>
    public void PlayWorldMusic(string? musicId, string? musicBase64)
    {
        if (!SoundEnabled)
        {
            StopWorldMusic();
            return;
        }

        // Si ya tenemos un reproductor de música de mundo, no reiniciamos la pista.
        // Simplemente nos aseguramos de que esté sonando.
        if (_worldMusicPlayer != null)
        {
            if (_worldMusicPlayer.PlaybackState != PlaybackState.Playing)
                _worldMusicPlayer.Play();
            return;
        }

        if (string.IsNullOrWhiteSpace(musicId) && string.IsNullOrWhiteSpace(musicBase64))
        {
            StopWorldMusic();
            return;
        }

        var path = EnsureAudioFile(musicId, musicBase64);
        if (path == null || !File.Exists(path))
        {
            StopWorldMusic();
            return;
        }

        try
        {
            _worldMusicReader = new AudioFileReader(path)
            {
                Volume = 1.0f
            };
            _worldMusicPlayer = new WaveOutEvent();
            _worldMusicPlayer.Init(_worldMusicReader);
            _worldMusicPath = path;
            _worldMusicPlayer.Play();
        }
        catch
        {
            StopWorldMusic();
        }
    }


    /// <summary>
    /// Reproduce la música especial de una sala.
    /// - Si la sala tiene música propia: se reproduce la de la sala y, si hay música global, se deja en segundo plano con volumen 0.
    /// - Si la sala NO tiene música propia: se detiene la música de sala (si la hay) y se reestablece la música global (si existe).
    /// </summary>
    public void PlayRoomMusic(string? musicId, string? musicBase64, string? worldMusicIdFallback, string? worldMusicBase64Fallback)
    {
        if (!SoundEnabled)
        {
            StopRoomMusic();
            return;
        }

        var hasRoomMusic = !string.IsNullOrWhiteSpace(musicId) || !string.IsNullOrWhiteSpace(musicBase64);

        if (!hasRoomMusic)
        {
            // No hay música especial: paramos la de sala y restauramos el volumen de la música global si existe.
            StopRoomMusic();

            if (_worldMusicPlayer != null)
            {
                SetWorldMusicVolume(1.0f);
            }

            return;
        }

        // Hay música de sala: si la música global existe, la silenciamos.
        if (_worldMusicPlayer != null)
        {
            SetWorldMusicVolume(0.0f);
        }

        var path = EnsureAudioFile(musicId, musicBase64);
        if (path == null || !File.Exists(path))
        {
            StopRoomMusic();
            if (_worldMusicPlayer != null)
            {
                SetWorldMusicVolume(1.0f);
            }
            return;
        }

        if (_roomMusicPlayer != null &&
            string.Equals(_roomMusicPath, path, StringComparison.OrdinalIgnoreCase))
        {
            if (_roomMusicPlayer.PlaybackState != PlaybackState.Playing)
                _roomMusicPlayer.Play();
            return;
        }

        StopRoomMusic();

        try
        {
            _roomMusicReader = new AudioFileReader(path);
            _roomMusicPlayer = new WaveOutEvent();
            _roomMusicPlayer.Init(_roomMusicReader);
            _roomMusicPath = path;
            _roomMusicPlayer.Play();
        }
        catch
        {
            StopRoomMusic();
        }
    }

    /// <summary>
    /// Reproduce un efecto de sonido puntual desde un archivo en la carpeta de sonido.
    /// No utiliza Base64; se asume que el archivo ya existe físicamente.
    /// </summary>
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
                try
                {
                    player.Dispose();
                }
                catch
                {
                }

                try
                {
                    reader.Dispose();
                }
                catch
                {
                }
            };
        }
        catch
        {
            // Ignorar errores de sonido
        }
    }

    public void StopRoomMusic()
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
            _roomMusicPath = null;
        }
    }

    public void StopWorldMusic()
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
            _worldMusicPath = null;
        }
    }

    public void StopMusic()
    {
        StopRoomMusic();
        StopWorldMusic();
    }

    private void SetWorldMusicVolume(float volume)
    {
        if (_worldMusicReader != null)
        {
            try
            {
                _worldMusicReader.Volume = volume;
            }
            catch
            {
                // Ignorar problemas de volumen
            }
        }
    }

    /// <summary>
    /// Garantiza que tenemos un archivo de audio físico para reproducir.
    /// - Si musicBase64 viene informado, se decodifica y se guarda en la carpeta de sonido.
    /// - Si sólo viene musicId, se asume que es un nombre de archivo relativo a la carpeta de sonido.
    /// Para evitar reinicios constantes, cuando musicId viene vacío generamos un nombre
    /// estable basado en el propio contenido Base64.
    /// </summary>
    private string? EnsureAudioFile(string? musicId, string? musicBase64)
    {
        if (!string.IsNullOrWhiteSpace(musicBase64))
        {
            try
            {
                var bytes = Convert.FromBase64String(musicBase64);

                string fileName;
                if (!string.IsNullOrWhiteSpace(musicId))
                {
                    fileName = musicId;
                }
                else
                {
                    var basePart = musicBase64.Length > 16
                        ? musicBase64.Substring(0, 16)
                        : musicBase64;

                    foreach (var c in Path.GetInvalidFileNameChars())
                    {
                        basePart = basePart.Replace(c, '_');
                    }

                    fileName = $"music_{basePart}.mp3";
                }

                var path = Path.Combine(_soundFolder, fileName);

                if (!File.Exists(path))
                {
                    File.WriteAllBytes(path, bytes);
                }

                return path;
            }
            catch
            {
                return null;
            }
        }

        if (!string.IsNullOrWhiteSpace(musicId))
        {
            var path = Path.Combine(_soundFolder, musicId);
            return path;
        }

        return null;
    }

    public void Dispose()
    {
        StopMusic();
    }
}
