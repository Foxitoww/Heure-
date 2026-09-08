# Publier une version (pour la mise à jour intégrée)

Le bouton **Réglages → Mise à jour → « Vérifier les mises à jour »** interroge
l'API GitHub :

```
GET https://api.github.com/repos/Foxitoww/Heure-/releases/latest
```

Pour qu'il détecte une nouvelle version et propose de l'installer, il faut donc :

### 1. Le dépôt doit être accessible sans authentification
Le dépôt GitHub doit être **public** (l'app fait un appel anonyme).
S'il reste privé, l'API renvoie 404 et l'app affiche
« Impossible de contacter GitHub, ou aucune version n'a encore été publiée ».

> Le nom du dépôt est fixé dans `src/HeurePlus/Services/UpdateService.cs`
> (`GitHubRepo = "Foxitoww/Heure-"`). Adaptez-le si besoin.

### 2. Incrémenter la version
Dans `src/HeurePlus/HeurePlus.csproj`, augmenter `<Version>` :

```xml
<Version>0.2.0</Version>
```

L'app compare cette version à `tag_name` de la release (le `v` initial est ignoré).

### 3. Construire l'installeur
```powershell
powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
```
→ `installer/Output/HeurePlus-Setup.exe`

### 4. Créer la release sur GitHub
- **Tag** : `v0.2.0` (doit correspondre à `<Version>`)
- **Titre** + notes de version (affichées dans l'app)
- **Joindre le fichier** `HeurePlus-Setup.exe` comme *asset*
  (l'app télécharge le premier asset dont le nom finit par `.exe`)

### Résultat côté utilisateur
Au prochain lancement (vérification silencieuse) ou via le bouton, l'app affiche
« Nouvelle version disponible : v0.2.0 » + les notes, puis
**« Télécharger et installer »** : elle télécharge `HeurePlus-Setup.exe`,
le lance et se ferme. Les données (`%LOCALAPPDATA%\HeurePlus`) sont conservées.
