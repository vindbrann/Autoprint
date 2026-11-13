# Mode opératoire : récupérer la doc, préparer et déposer le template

## Où tout se trouve

| Quoi | Où |
|---|---|
| Dépôt GitHub | `vindbrann/Autoprint` |
| Branche de travail | **`feature/charte-graphique`** (créée depuis `master` le 09/11/2026) |
| Documentation | `Docs/projet/Charte-graphique/` : ce dossier |
| Dépôt du template | `Docs/projet/Charte-graphique/livrables/` (arborescence dans [livrables/README.md](livrables/README.md)) |

Cette branche ne sert qu'au design : **on n'y modifie pas le code de l'application** tant que l'intégration n'a pas commencé.

## 1. Récupérer la documentation

**Sans Git** (personne extérieure, outil de maquettage) :
1. Sur GitHub, ouvrir le dépôt, puis choisir la branche **`feature/charte-graphique`** dans la liste des branches.
2. Ouvrir le dossier `Docs/projet/Charte-graphique` pour lire en ligne.
3. Pour tout récupérer : **Code → Download ZIP**.

**Avec Git** :

```bash
git clone https://github.com/vindbrann/Autoprint.git
```

```bash
git switch feature/charte-graphique
```

Ou, dans un dépôt déjà cloné :

```bash
git fetch origin
```

```bash
git switch feature/charte-graphique
```

À lire dans l'ordre : [README.md](README.md), puis les fiches 01 à 03 (une par interface), puis [04-livrables-et-integration.md](04-livrables-et-integration.md) pour les formats attendus.

## 2. Préparer le template

- Le travail se fait **en dehors de l'application** : maquettes HTML ou images, jetons de design, fichiers de thème.
- Respecter les contraintes communes du [README](README.md) : clair et sombre, contraste AA, aucune ressource externe, polices sous licence libre.
- Garder les noms de fichiers et l'arborescence de [livrables/README.md](livrables/README.md) : l'intégration en dépend.
- Noter les choix et les questions dans `livrables/NOTES.md` : thème de base Radzen, polices, icônes, licences, points à arbitrer.

## 3. Déposer le travail

On peut déposer au fur et à mesure, il n'est pas nécessaire d'attendre que tout soit fini.

**Sans Git (interface GitHub)** :
1. Sur la branche `feature/charte-graphique`, ouvrir `Docs/projet/Charte-graphique/livrables`.
2. **Add file → Upload files**, puis glisser les fichiers en respectant les sous-dossiers.
3. En bas de la page : un message court (ex. « Maquettes console v1 »), cocher **Commit directly to the `feature/charte-graphique` branch**, puis **Commit changes**.

**Avec Git** :

```bash
git switch feature/charte-graphique
```

```bash
git pull
```

Copier les fichiers dans `Docs/projet/Charte-graphique/livrables/`, puis :

```bash
git add Docs/projet/Charte-graphique/livrables
```

```bash
git commit -m "Charte graphique : maquettes console v1"
```

```bash
git push
```

**Ou** transmettre les fichiers à l'équipe de développement, qui les dépose.

Bonnes pratiques :
- **un commit par lot cohérent** (« jetons v1 », « maquettes client »…), avec un message clair ;
- **pas de fichiers lourds inutiles** : pas de fichiers sources de l'outil de maquettage de plusieurs centaines de Mo ni de vidéos ; des exports PNG / SVG / HTML suffisent ;
- pour une nouvelle version, **remplacer** le fichier plutôt qu'ajouter `-v2`, `-final` : Git garde l'historique.

## 4. Et ensuite ?

1. **Radzen 12** : la console passe à Radzen 12 sur une autre branche (`chore/radzen-12`), qui est ensuite fusionnée dans `master`.
2. **Mise à jour de cette branche** : on récupère `master` (qui contient alors Radzen 12) dans `feature/charte-graphique`.
3. **Intégration** sur cette branche, à partir des livrables :
   - **console** : thème Radzen, classes CSS, nettoyage des styles écrits en dur ;
   - **client** et **installeur** : dictionnaires XAML, icône, images MSI.

   Le client et l'installeur peuvent être intégrés sans attendre Radzen 12.
4. **Recette en labo**, puis **pull request** de `feature/charte-graphique` vers `master`, puis livraison.

## Garder la branche à jour

Si `master` évolue pendant le travail (correctifs, Radzen 12), l'équipe de développement y récupère régulièrement `master` :

```bash
git switch feature/charte-graphique
```

```bash
git merge origin/master
```

Cela ne touche pas aux fichiers de `livrables/`.
