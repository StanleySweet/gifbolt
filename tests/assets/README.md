# Test Assets

Ce dossier contient des fichiers GIF de test pour valider le décodeur.

## Génération d'un GIF de test

Vous pouvez créer un simple GIF avec ImageMagick:

```bash
# macOS
brew install imagemagick

# Créer un GIF animé simple (2 frames)
convert -size 100x100 xc:red frame1.png
convert -size 100x100 xc:blue frame2.png
convert -delay 50 -loop 0 frame1.png frame2.png test.gif
rm frame1.png frame2.png
```

Ou téléchargez n'importe quel GIF animé de test depuis Internet.

## Utilisation dans les tests

```csharp
var player = new GifPlayer();
bool loaded = player.Load("tests/assets/test.gif");
Console.WriteLine($"Frames: {player.FrameCount}, Size: {player.Width}x{player.Height}");
```
