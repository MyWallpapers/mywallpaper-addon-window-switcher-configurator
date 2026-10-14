# Première livraison A09 — qualification du 14 octobre 2026

Rapport consommable, pas une décision supplémentaire. Autorité examinée : MyWallpaper HEAD `299548ebcc20770fd73b10bdffd5f05586fbe126`, ledger SHA-256 `5c198b56bd404e3e035f31687e2ed23b7c0d87655e998bd0940fa2649ddae993`. Le dépôt A09 était sans commit initial lors des tests ; les empreintes exactes de ses sources et de sa livraison sont produites par `tools/verify-local-delivery.mjs`.

| Contrôle | Preuve et portée | DEC / classification |
| --- | --- | --- |
| Manifeste et réglages | SDK public actuel : 92 définitions, 83 valeurs avec consommateurs | DEC-0012, conforme |
| Protocole | 734 checks de modèle et framing ; neuf scénarios du vrai companion Windows, init, refus des réglages incorrects, shutdown et EOF | DEC-0013/0016, conforme |
| MyWallpaper | Activation réelle du bundle initial ; grille ouverte au raccourci physique Ctrl+Maj+Espace, confirmée par l’utilisateur ; cinq arrêts/reprises ; processus arrêté au retrait du calque | DEC-0043, preuve locale |
| Mémoire au repos | Bundle initial, 13 échantillons sur 60 secondes : 223 072 256 octets privés constants, 1 537 handles constants, 0,015625 seconde CPU supplémentaire | DEC-0152, preuve limitée |
| Rendu corrigé | Vrais HWND dédiés : sept profils, cinq présentations, aperçus DWM ; fond et ombre à chaud, retrait, SC_CLOSE et réouverture | DEC-0013, conforme au comportement réalisé |
| Répétitions | 1 500 ouvertures/fermetures sur cinq présentations, sans GC forcé ; zéro miniature et icône conservée après chaque fermeture ; handles 1 479 au premier et dernier relevé | DEC-0152, preuve locale |
| Mémoire sous répétitions | Le runtime WPF reste coûteux : pic privé 421 228 544 octets ; derniers relevés entre 367 869 952 et 419 852 288 octets. Les fluctuations ne démontrent pas une fuite, ni son absence sur une journée | DEC-0152, limites explicites |
| Reconstruction locale | .NET SDK exact 8.0.425, runtime autonome 8.0.31 ; 468 fichiers, 160,2 MiB ; deux chemins de construction distincts donnent des octets identiques après normalisation des chemins | DEC-0152, conforme ; reconstruction centrale encore distincte |
| Licence et moteur | Sources propres MIT, notices .NET conservées ; pas de binaire, SDK interne ou code du moteur propriétaire incorporé | DEC-0099/0160, conforme |

Une première tentative de bundle de test omettait les assemblies .NET ; le harness a été corrigé pour déclarer le répertoire complet, comme le vrai projet. Les changements d’état de deux add-ons lancés simultanément par les harness dépassaient leur délai d’observation ; les cinq cycles A09 et les cycles A08 ont ensuite passé séparément. Ce constat ne qualifie pas toutes les modifications concurrentes possibles dans MyWallpaper.

Les empreintes du dernier paquet normalisé sont dans les reçus de livraison et de reconstruction. Le test prolongé précédait la normalisation des chemins et de la version d’assembly ; la qualification Windows de 150 cycles et le protocole ont été relancés après cette normalisation.

Le paquet corrigé et normalisé a été activé dans MyWallpaper, digest `b589f38d958c26d5c33878ac710de304b8a4faf9442ef2a33c30fdec323509f9`. Le 14 octobre, l’utilisateur a confirmé les trois contrôles physiques : Alt maintenu avec Tab/Maj+Tab et sélection au relâchement, Alt+Tab bref, raccourci Ctrl+Maj+Espace puis annulation avec Échap. Aucun outil n’a simulé ces frappes. L’enregistrement du dépôt créateur a réussi ; la révision courante des conditions était déjà acceptée par le compte.

Le commit initial de sources est `df1cbc0` ; les assemblies .NET produites depuis un checkout Git portent aussi cette révision dans leurs métadonnées. Leur hash diffère donc légitimement des copies sans Git. La première version doit être reconstruite depuis son tag exact et comparée entre deux checkouts Git, puis contrôlée par le serveur ; les empreintes d’un autre contexte ne sont pas utilisées comme preuve du paquet publié.

La publication centrale et l’activation du paquet publié restent à constater. Restent hors de cette qualification : journée complète, autres builds et DPI, multi-écran, lecteurs d’écran et focus entre toutes les applications. Task View natif, Edge et vraie projection 3D ne sont pas proposés. Aucun redémarrage d’Explorer et aucun changement du moteur ne sont nécessaires à A09.

Le build central de 0.1.1 a échoué avant la compilation : `native.builds[0].inputs is not supported`, run `38625020424`. La candidate 0.1.2 utilise le sous-ensemble de configuration accepté des deux côtés, sans changement du code d’interaction. Elle récupère le SDK exact lorsqu’il manque, depuis l’archive Microsoft et son SHA-512 officiel. Les tags existants restent immuables. Les résultats finaux de publication sont conservés à part du tag pour ne pas changer sa provenance après qualification.
