# Sélecteur de fenêtres MyWallpaper

Un add-on natif A09 : **sept profils, cinq présentations et 83 valeurs de réglage** dans l’interface habituelle MyWallpaper. Verre boréal est recommandé par défaut. Le manifeste compte **92 définitions**, sections comprises, sous la limite de 100 de DEC-0012. Les contrôles de la première publication sont suivis dans [la qualification du 14 octobre](docs/release-qualification-2026-10-14.md).

Le companion crée sa propre surface Win32/WPF et ses aperçus DWM. Les styles, animations, filtres et interactions appartiennent à l’add-on. Il ne charge aucun composant dans Explorer. L’ABI actuelle du moteur ne permet pas de qualifier les adaptations privées d’Alt+Tab ou Task View ; [le contrat minimal et sa preuve d’usage](docs/shared-capability-contract.md) décrivent ce point sans modifier le moteur partagé.

## Utiliser

Le raccourci initial est **Ctrl + Alt + Espace**. Il laisse le sélecteur ouvert : saisir recherche par titre/application, Tab ou flèches parcourent les fenêtres, Entrée choisit, Échap annule. Le clic choisit aussi une carte ; la molette parcourt les fenêtres. Le bouton Fermer et Suppr demandent une fermeture normale avec confirmation par défaut. L’application conserve ses éventuelles demandes d’enregistrement.

Dans les réglages usuels de l’add-on, « Ouvrir le sélecteur » peut activer le remplacement **Alt + Tab** : maintenir Alt, parcourir, puis relâcher Alt pour choisir. Un appui plus bref que le délai ne montre pas le panneau. Les touches Windows, Ctrl + Alt + Tab, les touches injectées et Win + Tab suivent Windows. Le plein écran utilise Windows par défaut. Le remplacement se limite à la session interactive de cet utilisateur et cesse avec le companion.

L’activation « Raccourci dédié » est proposée par défaut. Une seule instance possède le raccourci dans une session Windows ; une seconde instance est refusée. La portée device des réglages ne crée pas de principal partagé supplémentaire.

## Profils et réglages

| Profil | Présentation conseillée | Palette / matière |
| --- | --- | --- |
| Verre boréal | Grille | Encre, turquoise et matière translucide Windows |
| Minuit | Grille | Bleu nuit opaque, sélection claire |
| Rose électrique | Grille | Dégradé prune/rose |
| Papier | Liste | Crème, encre sombre, accent brun |
| Cinéma | Grille | Dégradé charbon/ambre |
| Classique | Compacte | Icônes et titres, sans miniatures |
| Cascade Flip | Cascade | Dégradé indigo/violet et carte avant agrandie |

Le menu « Ambiance visuelle » change seulement palette/matière et la présentation conseillée. Les contrôles de géométrie, filtres et interactions restent réglables. « Personnalisée » expose les couleurs et préserve les choix personnels pendant un aller-retour vers un profil. Les sept **presets MyWallpaper** du manifeste remettent les contrôles aux valeurs par défaut puis sélectionnent leur profil et remplissent ses couleurs : passez ensuite à « Personnalisée » pour ajuster cette palette sans la recréer. C’est une remise à zéro explicite et cohérente. Les paramètres ne demandent aucun JSON ni CSS.

Les neuf sections couvrent : le sélecteur, disposition/densité, fenêtres proposées, fond/lumière, cartes/sélection, lisibilité, navigation/actions, mouvements et performance facultative. Les conditions `showIf`, les groupes `parent` et la section performance repliée utilisent le contrat de réglages existant. Les couleurs sont en `#RRGGBBAA`, alpha en dernier. Les distances visuelles sont en pixels indépendants du DPI ; les aperçus DWM utilisent des rectangles physiques.

Les filtres proposent tous les écrans, celui du pointeur ou celui de la fenêtre active ; inclusion/exclusion d’exécutables, fenêtres réduites, fantômes, fenêtres minuscules, sans titre ou occupées. Les listes d’applications se saisissent avec un nom par ligne, comme `notepad.exe`. Le sélecteur conserve l’ordre récent Windows, ou trie par titre/application, avec regroupement facultatif et pagination automatique.

## Présentations

La **grille** ajuste colonnes et taille aux dimensions disponibles. La **liste** montre des aperçus facultatifs. Le mode **classique** privilégie icônes et titres. La **cascade** empile des cartes avec titres accessibles et aperçu DWM sur la carte avant : c’est une interprétation propre inspirée de Flip 3D, **sans projection 3D réelle et sans restauration du composant Vista**. La **carte du bureau** place les cartes selon les positions observées des fenêtres ; la carte sélectionnée porte l’aperçu live. Les recouvrements correspondent à la géométrie réelle. Il s’agit de la carte temporaire du sélecteur, pas de mini-fenêtres persistantes ancrées au bureau.

Le budget de miniatures favorise la sélection. Les aperçus gardent le rapport d’aspect ou remplissent la carte par recadrage. Les fenêtres réduites, occupées et les échecs DWM ont une représentation de remplacement. Les sources protégées peuvent rendre une image vide selon Windows ; l’add-on n’enregistre aucun contenu de fenêtre sur disque en usage normal. Les captures sont réservées au mode explicite de qualification.

Le panneau dispose d’un fond uni, dégradé ou verre Windows avec secours opaque, d’un masque natif de coins arrondis, de contours et d’une ombre séparée sans interaction. Le fond du bureau peut être assombri. L’entrée, le survol et la sélection sont animés, avec réglage de durée et réduction de mouvements explicite ou provenant de Windows. Les titres et actions utilisent des boutons WPF exposés à UI Automation. La validation avec lecteur d’écran reste à faire.

## Construire et essayer

Prérequis : Windows x64, **SDK .NET 8.0.425** (`global.json`) et Node. Le build n’exige pas de privilèges administrateur. Le script utilise un SDK déjà installé ou le cache local `MyWallpaperAddonBuild/dotnet-8.0.425` ; il ne télécharge rien implicitement. [Téléchargement officiel .NET](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File native/build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File native/preview.ps1
```

Le premier script génère le manifeste/profils, exécute les tests et publie un bundle Windows autonome dans `native/out/windows-x86_64/`. Il inclut l’exécutable, les assemblies, le runtime et les notices. `-DotnetPath` choisit un SDK ; `-FrameworkDependent` produit une variante qui exige .NET Desktop Runtime 8 sur la machine cible. En mode standalone, l’icône de zone de notification permet d’ouvrir ou d’arrêter le sélecteur. `native/preview.ps1 -PreviewOnly` montre le sélecteur sans installer de raccourci global.

`mywallpaper.config.json` déclare le build et tous ses outputs, avec l’entrée exacte requise par le CLI. L’intégration respecte `native-v1`, `process-v2` **v5**, init/ready, snapshots complets de réglages, messages routés, shutdown et EOF. L’I/O est hors thread UI, la file sortante est bornée. Le companion accepte des records de quatre MiB au maximum comme politique propre à cet add-on ; chaque chunk physique reste limité à un MiB. Les réglages incorrects conservent le dernier snapshot accepté. Le host MyWallpaper conserve ses garanties génériques de supervision/intégrité ; aucune nouvelle garantie de sandbox OS n’est revendiquée.

```powershell
node tools/validate-manifest.mjs
node tools/check-setting-usage.mjs
node tools/test-companion.mjs
node tools/probe-shell-module.mjs
```

La validation publique utilise le SDK du dépôt MyWallpaper frère en lecture seule ; `MW_SDK_ROOT` peut désigner sa copie construite. Les tests du companion activent brièvement le raccourci dédié, le désactivent, vérifient le refus de réglages incorrects et quittent. Ils ne simulent pas de frappes dans les applications de l’utilisateur.

Pour refaire les captures de test réelles : `companion.exe --qualify <dossier-absolu>`. Ce mode ouvre brièvement trois fenêtres dédiées, sans activation, qualifie les variantes et les ferme. Il peut croiser d’autres overlays de la session ; les captures doivent être inspectées avant réemploi.

## État vérifié et limites

Les essais locaux ont observé des fenêtres/miniatures réelles sur Windows 11 **build 26200**, x64, un écran **2560 × 1440 à 96 DPI** : sept profils, cinq présentations, matériau Windows accepté, réduction avec remplacement, fermeture normale d’une fixture et zéro miniature retenue après nettoyage. Les builds et la validation du SDK passent ; les détails et empreintes sont dans [le rapport de qualification](docs/qualification.md).

Restent à qualifier : gestes physiques Alt + Tab et tap bref, conservation/restitution du focus entre applications réelles, navigation souris et confirmations dans une session utilisateur, DPI mixtes/multi-écran, Windows 10 et autres builds Windows 11, contenus protégés/Chromium/UWP, lecteurs d’écran et l’activation depuis le Desktop MyWallpaper. Le protocole testé par un harness réel ne certifie pas le parcours UI du Desktop. Task View natif, onglets Edge et bureaux virtuels supplémentaires restent hors capacités réalisées. Win + Tab n’est pas intercepté.

Les limites ci-dessus décrivent les essais du 13 octobre. L’activation depuis MyWallpaper, les cycles d’arrêt et les nouveaux contrôles de reconstruction sont détaillés dans le rapport du 14 octobre ; leur portée reste locale à Windows 11 x64, 96 DPI. Le ledger courant reste l’unique autorité.

