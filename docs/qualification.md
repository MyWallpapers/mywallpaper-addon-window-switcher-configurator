# Qualification locale A09 — 13 octobre 2026

Autorité : ledger courant MyWallpaper, commit `299548ebcc20770fd73b10bdffd5f05586fbe126`. Au début de l’étude : SHA-256 `ed477485a495b1e6ff9e931dfae5378461378c4c57977a4e436e753ca9ddba6c`. À la production après son évolution concurrente : SHA-256 `640be5b1751a9c2308c45e460d8d10f9f2612751ce86d4bfbdd29c36cdc44e65`. Les DEC concernées ont été relues et les anciennes mentions de frontière Windhawk ouverte supprimées. Ce rapport est consommable et limité au prototype local. Les empreintes et sources épinglées sont dans [source-evidence.json](source-evidence.json).

| Constat | Rattachement | Classification / preuve |
| --- | --- | --- |
| Companion externe et settings device ; effets, filtres et profils dans l’add-on | DEC-0012/0013 | Conforme au mandat local ; manifeste, projets .NET, protocole v5 et sources Windows |
| 92 définitions, dont neuf sections et 83 valeurs ; sept presets | DEC-0012 | Conforme ; validation publique SDK MyWallpaper, pas de champ technique obligatoire |
| Une famille cohérente avec cinq modes, aucun conteneur de sous-add-ons | DEC-0099 | Conforme à ce périmètre ; l’add-on exécute sa propre surface |
| Réalisation locale sans modification de la portée de lancement | DEC-0157 | Conforme ; aucune modification du ledger ou du core |
| Sources concurrentes étudiées, aucun code de mod ou moteur repris ; licence MIT de ce companion original | DEC-0098/0099/0160 | Conforme au retrait de Windhawk et à la séparation du moteur/SDK interne propriétaire ; aucun de ces composants n’est incorporé |
| Personnalisation de Task View natif | DEC-0012/0013, absence de décision dédiée A09 | Extension non qualifiée ; probe réel indisponible et contrat minimal préparé, pas de claim d’effet |

## Réalisation vérifiée

Le build Release Windows compile avec zéro avertissement/erreur. **734 checks** portent sur presets, snapshots complets/retour aux defaults, couleurs alpha-last, validation de types/plages, filtres, recherche, navigation, cinq dispositions (y compris limites d’espace et origine d’écran négative simulée) et framing mono/multi-chunks v5. Les valeurs hors contrat sont refusées. Ces tests ne constituent pas une qualification d’un écran à origine négative ou de DPI mixtes réels.

Le harness lance le vrai companion Windows par stdin/stdout et valide **neuf scénarios** : init/progress/ready, capacité Task View indisponible, snapshot invalide conservant le précédent, hook du raccourci dédié installé, désactivation libérant hook/poll/session/miniatures, réactivation avec autre profil, shutdown, EOF et premier record invalide. [Résultat du protocole](evidence/protocol-result.json). Les frappes globales ne sont pas simulées.

Le bundle autonome contient **468 fichiers, 160,2 MiB**. L’entrée exacte du CLI, les notices et les enveloppes de taille/nombre de fichiers du SDK public sont vérifiées dans [local-delivery.json](evidence/local-delivery.json). La commande CLI complète `check` échoue uniquement sur `provenance.git` dans ce dépôt neuf, sans origin ni HEAD ; aucun dépôt distant, tag ou commit de release n’a été fabriqué pour lui donner une provenance. Son lancement Windows rencontre aussi une dépendance `sharp` disponible seulement dans l’environnement WSL ; le même check sous WSL atteint le diagnostic Git. La validation publique du manifeste, du bundle et les tests locaux passent indépendamment. Ce point concerne la préparation de publication, pas l’exécution du companion local.

Les captures montrent les **vrais HWND** de trois fenêtres de test d’un processus enfant : Atlas, Journal et Studio. DWM compose réellement leurs aperçus ; la capture du rectangle du panneau est faite après stabilisation. Host observé : Windows 11 `10.0.26200.0`, x64, **un écran 2560 × 1440, 96 DPI**. Le matériau Windows demandé par Verre boréal est accepté sur ce host. Grille/liste montrent trois miniatures ; Classique n’en demande aucune ; cascade/carte du bureau donnent un aperçu à la sélection. La réduction affiche un remplacement et la fermeture d’une fixture passe par `WM_CLOSE`. Après chaque suspension et à la fin : **zéro miniature enregistrée restante**.

La qualification de `WM_SYSCOMMAND/SC_CLOSE` — message utilisé par le chemin Alt+F4 — vérifie fermeture de la session, panneau masqué, zéro miniature, puis réouverture du même panneau avec ses trois aperçus. Elle ne prétend pas avoir reçu une frappe physique Alt+F4. [Résultats Windows](evidence/windows-result.json).

Les premières captures ont révélé un timer de qualification réénumérant la session utilisateur. Il a été stoppé dès construction et le mode diagnostic possède maintenant uniquement ses fixtures. Ces premières images restent dans un répertoire privé ignoré, ne sont pas distribuées et ne servent pas de preuve de finition. Une capture de carte du bureau a aussi croisé un toast d’une autre application ; elle est remplacée par une capture inspectée sans cet overlay. Il n’y a eu ni injection ni redémarrage d’Explorer, ni modification de fenêtre native de Task View, ni automatisation UAC.

## Revue et impact

La revue de finition indépendante a demandé deux corrections : fin de session contrôlée pour Alt+F4/SC_CLOSE, et présentation Classique sans rangée vide, avec place pour identifier les fenêtres. Les références documentaires manquantes ont été complétées. Le verdict ciblé et les limites de la revue sont conservés dans `evidence/finish-review.md`. [Impact et revue de code mort](impact-and-dead-code.md), [83 consommateurs de paramètres](setting-consumers.json).

L’index Codebase Memory a permis de cibler les relations, mais certains snippets étaient périmés ou mal attribués. Les sources présentes et les registrations WPF/Win32 ont été vérifiées avant toute suppression. Les faibles fan-in ne sont pas assimilés à de l’obsolescence pour les callbacks ou ressources embarquées.

## Points précis restant à qualifier

- Gestes physiques : Alt maintenu, Alt+Maj+Tab, tap plus bref que le délai, clavier AZERTY/AltGr, clic/molette, fermeture confirmée et annulation.
- Focus : applications Win32/Chromium/UWP réelles, dialogue modal de l’application cible, refus Windows d’activation et changement d’application pendant l’affichage.
- Host : parcours d’activation/réglages/presets/arrêt dans le Desktop MyWallpaper actuel, autres builds Windows 11 et Windows 10.
- Affichage : DPI 125–200 %, plusieurs écrans, écran secondaire à origine négative, retrait/reconnexion d’un écran, contenu protégé et accéléré.
- Accessibilité/performance : lecteurs d’écran, contraste élevé Windows, mesure CPU/GPU/latence sur beaucoup de fenêtres et longue durée. Les limites d’aperçus bornent les ressources ; elles ne sont pas un benchmark.
- Vraie projection Flip 3D, mini-fenêtres persistantes du bureau, onglets Edge et personnalisation native de Task View : **pas réalisés** par ce prototype. La cascade et la carte temporaire portent des noms qui décrivent leur comportement réel.

La réduction de mouvements, les profils, les coins/ombres et la recherche sont implémentés. Les effets dont les tests n’observent que le contrat restent marqués comme tels. Le runtime intégré n’a pas été publié ou signé pour diffusion ; la présence d’un bundle local ne vaut pas une release approuvée.

