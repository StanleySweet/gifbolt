// SPDX-License-Identifier: MIT
using System;
using GifBolt.Internal;

namespace GifBolt;

/// <summary>
/// Surface core de lecture (netstandard2.0), sans UI.
/// Les wrappers (WPF, etc.) s’appuient dessus.
/// </summary>
/// <summary>
/// Lecteur GIF de base. Fournit le chargement, lecture/pause et accès aux pixels RGBA.
/// </summary>
public sealed class GifPlayer : IDisposable
{
    private DecoderHandle? _decoder;

    /// <summary>Indique si la lecture est en cours.</summary>
    public bool IsPlaying { get; private set; }
    /// <summary>Indique si le GIF boucle indéfiniment.</summary>
    public bool IsLooping { get; private set; }

    /// <summary>Nombre total d’images.</summary>
    public int FrameCount { get; private set; }
    /// <summary>Index de l’image courante.</summary>
    public int CurrentFrame { get; private set; }
    /// <summary>Largeur de l’image en pixels.</summary>
    public int Width { get; private set; }
    /// <summary>Hauteur de l’image en pixels.</summary>
    public int Height { get; private set; }

    /// <summary>Charge un GIF depuis un chemin de fichier.</summary>
    /// <param name="path">Chemin du fichier GIF.</param>
    /// <returns>true si chargé avec succès ; sinon false.</returns>
    public bool Load(string path)
    {
        this.DisposeDecoder();
        var h = Native.gb_decoder_create();
        if (h == IntPtr.Zero)
            return false;

        var tmp = new DecoderHandle(h);
        int ok = Native.gb_decoder_load_from_path(tmp.DangerousGetHandle(), path);
        if (ok == 0)
        {
            tmp.Dispose();
            return false;
        }

        this._decoder = tmp;
        this.Width = Native.gb_decoder_get_width(this._decoder.DangerousGetHandle());
        this.Height = Native.gb_decoder_get_height(this._decoder.DangerousGetHandle());
        this.FrameCount = Native.gb_decoder_get_frame_count(this._decoder.DangerousGetHandle());
        this.IsLooping = Native.gb_decoder_get_loop_count(this._decoder.DangerousGetHandle()) < 0;
        this.CurrentFrame = 0;
        return true;
    }

    /// <summary>Démarre la lecture.</summary>
    public void Play() => this.IsPlaying = true;
    /// <summary>Met la lecture en pause.</summary>
    public void Pause() => this.IsPlaying = false;
    /// <summary>Arrête la lecture et revient à la première image.</summary>
    public void Stop()
    {
        this.IsPlaying = false;
        this.CurrentFrame = 0;
    }

    /// <summary>Récupère les pixels RGBA32 d’une image donnée.</summary>
    /// <param name="frameIndex">Index de l’image.</param>
    /// <param name="pixels">Buffer de pixels RGBA32 de sortie.</param>
    /// <returns>true si disponible ; sinon false.</returns>
    public bool TryGetFramePixelsRgba32(int frameIndex, out byte[] pixels)
    {
        pixels = Array.Empty<byte>();
        if (this._decoder == null || frameIndex < 0 || frameIndex >= this.FrameCount)
            return false;
        int byteCount;
        var ptr = Native.gb_decoder_get_frame_pixels_rgba32(this._decoder.DangerousGetHandle(), frameIndex, out byteCount);
        if (ptr == IntPtr.Zero || byteCount <= 0)
            return false;
        pixels = new byte[byteCount];
        System.Runtime.InteropServices.Marshal.Copy(ptr, pixels, 0, byteCount);
        return true;
    }

    /// <summary>Retourne le délai de l’image (ms).</summary>
    /// <param name="frameIndex">Index de l’image.</param>
    /// <returns>Délai en millisecondes.</returns>
    public int GetFrameDelayMs(int frameIndex)
    {
        if (this._decoder == null || frameIndex < 0 || frameIndex >= this.FrameCount)
            return 0;
        return Native.gb_decoder_get_frame_delay_ms(this._decoder.DangerousGetHandle(), frameIndex);
    }

    private void DisposeDecoder()
    {
        if (this._decoder != null)
        {
            this._decoder.Dispose();
            this._decoder = null;
        }
    }

    /// <summary>Libère les resources natives associées.</summary>
    public void Dispose()
    {
        this.DisposeDecoder();
        GC.SuppressFinalize(this);
    }
}
