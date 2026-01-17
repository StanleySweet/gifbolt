# GifBolt .NET Core Tests

Tests d'intégration P/Invoke pour valider que `GifBolt.Core` peut charger et utiliser la librairie native via l'ABI C.

## Build et exécution

```bash
dotnet build tests/GifBolt.Core.Tests/GifBolt.Core.Tests.csproj
dotnet run --project tests/GifBolt.Core.Tests/GifBolt.Core.Tests.csproj
```

## Prérequis

- La bibliothèque native `GifBolt.Native.dll` (Windows) ou `libGifBolt.Native.dylib` (macOS) doit être accessible dans le PATH ou copiée à côté de l'exécutable de test.

## Tests inclus

- Création et destruction du `GifPlayer`
- Gestion des fichiers invalides
- Chargement basique (nécessite un fichier GIF de test)
