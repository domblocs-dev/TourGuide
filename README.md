# TourGuide

API Web ASP.NET Core (.NET 7) d'un service de voyage : elle localise les utilisateurs,
leur indique les attractions les plus proches, leur attribue des points de récompense et
leur propose des offres de séjour.

Ce dépôt contient la version corrigée et optimisée de l'application : tests réparés,
nouvelle fonctionnalité des attractions proches, traitement en parallèle de 100 000
utilisateurs, et chaîne d'intégration continue.

## Sommaire

1. [Structure de la solution](#structure-de-la-solution)
2. [Lancer l'API](#lancer-lapi)
3. [Lancer les tests](#lancer-les-tests)
4. [Travaux réalisés](#travaux-réalisés)
5. [Performances mesurées](#performances-mesurées)
6. [Intégration continue](#intégration-continue)
7. [Animations](#animations)

## Structure de la solution

| Projet          | Rôle                                                                                                                                                                          |
| --------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Api`           | l'API Web : contrôleur, services `TourGuideService` et `RewardsService`, et `Tracker`, qui suit tous les utilisateurs, puis fait une pause de 5 minutes avant le tour suivant |
| `GpsUtil`       | librairie de géolocalisation (simulée, 30 à 99 ms par appel)                                                                                                                  |
| `RewardCentral` | librairie de calcul des points de récompense (simulée, 1 à 999 ms par appel)                                                                                                  |
| `TripPricer`    | librairie des offres de séjour                                                                                                                                                |
| `TourGuideTest` | tests xUnit : fonctionnels et de performance                                                                                                                                  |

Il n'y a pas de base de données : au démarrage, l'application crée 100 utilisateurs
fictifs, nommés `internalUser0` à `internalUser99`.

## Lancer l'API

Prérequis : le SDK .NET 7.

```bash
dotnet run --project Api
```

L'interface Swagger est alors disponible à l'adresse `http://localhost:5200/swagger`.

| Requête                                            | Réponse                                                     |
| -------------------------------------------------- | ----------------------------------------------------------- |
| `GET /TourGuide/getLocation?userName=...`          | la dernière position de l'utilisateur                       |
| `GET /TourGuide/getNearbyAttractions?userName=...` | les 5 attractions les plus proches, avec distance et points |
| `GET /TourGuide/getRewards?userName=...`           | les récompenses obtenues                                    |
| `GET /TourGuide/getTripDeals?userName=...`         | les 10 offres de séjour                                     |

## Lancer les tests

```bash
dotnet test
```

La suite compte 11 tests. Les deux tests de performance traitent chacun 100 000
utilisateurs et durent environ 13 minutes à eux deux. Pour lancer uniquement les tests
rapides :

```bash
dotnet test --filter "FullyQualifiedName!~PerformanceTest"
```

## Travaux réalisés

| Travail                                                 | Fichiers principaux                             | Correction                                                                                                                |
| ------------------------------------------------------- | ----------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| Tests en échec intermittent (accès concurrents)         | `Api/Users/User.cs`                             | listes privées, propriétés qui renvoient une copie, écritures sous verrou                                                 |
| Test des offres de séjour en échec permanent            | `TripPricer/TripPricer.cs`                      | la boucle produit 10 offres au lieu de 5, et le tirage atteint les 10 noms de fournisseurs                                |
| Les 5 attractions les plus proches                      | `TourGuideService.cs`, `NearbyAttractionDto.cs` | tri par distance puis 5 premières, quelle que soit la distance ; nouvel objet JSON ; les 5 calculs de points en parallèle |
| Suivre 100 000 utilisateurs en moins de 15 minutes      | `TourGuideService.TrackAllUsersLocation`        | `Parallel.ForEach`, 100 utilisateurs à la fois                                                                            |
| Récompenser 100 000 utilisateurs en moins de 20 minutes | `RewardsService.CalculateAllUsersRewards`       | `Parallel.ForEach`, 100 utilisateurs à la fois                                                                            |
| Réserve de threads trop lente à grandir                 | `Program.cs`, `PerformanceTest.cs`              | `ThreadPool.SetMinThreads(100, 100)` au démarrage                                                                         |

Les librairies simulent leur lenteur par une attente bloquante (`Thread.Sleep`) et ne
proposent pas de méthode asynchrone. Le gain vient donc du parallélisme : plusieurs
utilisateurs sont traités en même temps, chacun sur son thread.

## Performances mesurées

Les temps « Avant » sont ceux du tableur de performance fourni avec le projet. Les temps
« Après » sont mesurés sur une machine à 4 processeurs, avec le test de performance
correspondant.

**Suivi des utilisateurs** (`HighVolumeTrackLocation`, objectif : moins de 15 minutes pour 100 000)

| Utilisateurs | Avant   | Après    |
| ------------ | ------- | -------- |
| 100          | 7 s     | 0,36 s   |
| 1 000        | 75 s    | 2,39 s   |
| 5 000        | 376 s   | 9,14 s   |
| 10 000       | 762 s   | 16,79 s  |
| 50 000       | 3 791 s | 90,34 s  |
| 100 000      | 7 579 s | 157,80 s |

**Calcul des récompenses** (`HighVolumeGetRewards`, objectif : moins de 20 minutes pour 100 000)

| Utilisateurs | Avant    | Après    |
| ------------ | -------- | -------- |
| 100          | 44 s     | 1,70 s   |
| 1 000        | 472 s    | 11,51 s  |
| 10 000       | 5 820 s  | 94,20 s  |
| 100 000      | 64 020 s | 597,32 s |

Pour 100 000 utilisateurs, le suivi passe d'environ 2 heures à moins de 3 minutes, et le
calcul des récompenses d'environ 18 heures à 10 minutes.

## Intégration continue

La chaîne GitHub Actions, décrite dans `.github/workflows/ci.yml`, se déclenche à chaque
envoi ou demande de fusion sur la branche `main`. Elle comporte trois jobs :

| Job           | Ce qu'il fait                                          | Durée indicative   |
| ------------- | ------------------------------------------------------ | ------------------ |
| `tests`       | compile en Release et lance les 9 tests rapides        | 1 à 2 minutes      |
| `performance` | compile en Release et lance les 2 tests de performance | environ 13 minutes |
| `artefact`    | publie l'API et archive ses DLL au format zip          | 1 minute           |

Les deux premiers jobs s'exécutent en parallèle : un échec fonctionnel est signalé en
1 à 2 minutes, sans attendre la fin des tests de performance. Le job `artefact` ne démarre
que si les deux ont réussi : une archive produite par la chaîne a donc toujours passé
l'ensemble des tests.

L'archive `tourguide-api` se télécharge depuis l'onglet Actions du dépôt, sur la page de
l'exécution concernée.

## Animations

Cinq animations montrent, étape par étape, le défaut corrigé et le fonctionnement de la
correction. Ce sont des pages HTML autonomes, sans bibliothèque : il suffit de les
télécharger puis de les ouvrir dans un navigateur.

| Fichier                                                                      | Sujet                                                            |
| ---------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| [1conflit_concurrence.html](docs/animations/1conflit_concurrence.html)       | le conflit d'accès concurrent sur les listes de l'utilisateur    |
| [2offres_sejour.html](docs/animations/2offres_sejour.html)                   | les offres de séjour, de 5 à 10                                  |
| [3attractions_proches.html](docs/animations/3attractions_proches.html)       | les 5 attractions les plus proches et leurs calculs en parallèle |
| [4suivi_parallele.html](docs/animations/4suivi_parallele.html)               | le suivi de 100 000 utilisateurs en parallèle                    |
| [5recompenses_paralleles.html](docs/animations/5recompenses_paralleles.html) | le calcul des récompenses de 100 000 utilisateurs en parallèle   |
