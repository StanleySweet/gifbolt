# GifBolt Sample Application

Démonstration simple de l'utilisation du contrôle WPF `GifBoltControl`.

## Build

Disponible uniquement sur Windows (nécessite .NET Framework 4.7.2 et DirectX 11).

```powershell
dotnet build samples/GifBolt.SampleApp/GifBolt.SampleApp.csproj -c Release
```

## Utilisation

1. Lancez l'application.
2. Utilisez "Load GIF..." pour charger un fichier GIF.
3. Les boutons Play/Pause/Stop contrôleront la lecture (à implémenter dans GifBoltControl).

## Notes

- Le contrôle `GifBoltControl` appelle la bibliothèque native `GifBolt.Native.dll` via P/Invoke.
- Le backend D3D11 gère le rendu GPU sur Windows.
- Pour l'instant, la lib native doit être copiée manuellement dans le dossier de sortie ou ajoutée au PATH.
