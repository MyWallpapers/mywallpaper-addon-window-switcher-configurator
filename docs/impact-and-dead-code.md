# Impact et revue de code mort

Périmètre : le nouveau dépôt `mywallpaper-addon-window-switcher-configurator`, ses trois projets .NET, son manifeste généré, ses profils et ses outils de build/validation. Les autres repos, le core, le moteur et le ledger restent en lecture seule. L’index Codebase Memory du dépôt a été créé en mode fast et les chemins d’appels concernés ont été interrogés ; les enregistrements dynamiques ont ensuite été vérifiés dans les sources.

## Entrées et contrats retenus

`Program.Main` est désigné par `StartupObject` et le manifeste natif asInvoker/PerMonitorV2. Les modes process-v2, standalone, preview et qualification ont chacun leurs appelants explicites. `Settings`, `WindowFilter`, `SwitchSession`, `Layout` et `Wire` sont consommés par le projet Windows et les tests. Leurs types publics sont internes à cet ensemble d’assemblies d’add-on ; ils ne constituent pas une extension du SDK partagé.

Les profils sont des ressources embarquées déclarées dans `Switcher.Core.csproj`. Les paramètres se lisent par leurs IDs contractuels ; `tools/check-setting-usage.mjs` produit [les consommateurs des 83 valeurs](setting-consumers.json). C’est une preuve de références, pas une certification d’effet individuel. Les sections et presets sont consommés par le host de settings MyWallpaper : leur absence d’appel C# n’est pas une preuve de code mort.

Les presets générés remplissent également les valeurs de palette du profil choisi. Le passage à « Personnalisée » conserve ainsi cette palette dans le snapshot du host. Ces valeurs sont consommées par les mêmes IDs de réglages et les ressources de profil embarquées ; aucun symbole, réglage ou contrat n’est supprimé. La validation du manifeste compare chaque preset à son fichier de profil, en plus des checks existants.

`KeyboardRouter.OnKey` est retenu par le delegate `callback`, enregistré avec `SetWindowsHookEx`, puis libéré par `UnhookWindowsHookEx`. Son fan-in statique faible ne permet pas de le supprimer. `SwitcherWindow.OnNativeMessage` est enregistré dans `HwndSource`, `OnRendering` dans `CompositionTarget.Rendering` uniquement quand le panneau est visible. Les événements WPF, commandes de carte, timers, callbacks du tray et ressources du template sont des entrées dynamiques vérifiées. Les imports Win32 déclarent explicitement les variantes Unicode là où nécessaire.

La sortie process-v2 contient seulement les types acceptés par le host v5 : progress, ready, message avec target, error. Les commandes d’add-on open/cancel/status/probeTaskView ont leurs consommateurs dans le harness ou constituent le contrat facultatif documenté. Aucun adaptateur privé absent n’est gardé « pour plus tard ».

## Nettoyage et références supprimées

Deux helpers créés pendant cette implémentation ont été retirés après recherche exacte : `RectD.Contains` (la sélection souris est assurée par les boutons WPF) et le P/Invoke `GetAncestor` inutilisé. Un compteur de génération de rendu, sans consommation, a également été retiré. Ils n’avaient ni export public de SDK, ni référence de ressource, ni registration dynamique, ni client externe. Aucun code du dépôt taskbar ou du moteur n’est supprimé.

Les chemins de nettoyage restent actifs : suspend libère les miniatures, désabonne la boucle de composition et cache panneau/ombre/dimmer ; fin de session coupe le timer et le mode Alt maintenu ; dispose arrête poll/keyboard, ferme les fenêtres de l’add-on. Après chaque variante de qualification, `Thumbnail.ActiveCount` est vérifié à zéro. Les icônes Windows obtenues sont empruntées, jamais détruites par l’add-on. Les régions sont transférées à Windows après un `SetWindowRgn` réussi.

La revue a également corrigé un timer démarrant dès construction, la priorité d’aperçu de la sélection, un entrypoint Unicode, la prise en compte du mouvement/opacité dans les rectangles DWM et la protection du dialogue modal de fermeture. L’énumération/process metadata s’effectue hors callback clavier ; ce callback utilise un cache de fenêtres éligibles et met en file des commandes UI. Aucun RPC synchrone vers un contrôleur n’est utilisé.

## Complément du 14 octobre : cycle de vie et publication

`PollAsync` conserve le snapshot qui a lancé son travail et ignore un résultat après désactivation, remplacement du snapshot ou destruction. Ses appelants restent le timer, Configure et Close ; ces callbacks sont actifs. Aucun symbole n’est supprimé. `Suspend`, appelé par End et Dispose, vide aussi les icônes ; `LoadIcon` garde une génération de session pour ne pas les réinsérer après la fermeture. Les registrations clavier, WPF et Composition restent identiques.

`UpdateAuxiliaryWindows` regroupe les appels de Open et Render pour appliquer immédiatement le fond assombri, son écran et l’ombre. Position n’appelle plus SetArea séparément : ses deux consommateurs passent déjà par ce regroupement. Les effets désactivés ne sont plus reconstruits. Les fenêtres auxiliaires restent sous le panneau après leur affichage. `AuxiliaryState` est interne à l’assembly et consommé par la qualification ; aucun contrat SDK ni moteur ne change.

La qualification réelle des HWND ajoute l’application à chaud et le retrait du fond/de l’ombre, la désactivation, puis 150 cycles (1 500 avec `--soak`). Le mode fixture possède une durée maximale de 180 secondes, choisie seulement pour ce test ; le mode normal ne lit aucun de ces paramètres. Les captures ne portent que sur des fenêtres de test dédiées. Les empreintes de livraison doivent être régénérées après le build.

Le SDK .NET est désormais exigé à sa version exacte. Le PathMap normalise les chemins de compilation dans les trois projets et leur version devient 0.1.0. Deux répertoires de construction distincts donnent les mêmes 468 fichiers ; ce contrôle ne remplace pas la reconstruction centrale du serveur. Pas de dépendance ni de code partagé retiré ; les notices runtime et l’entrée exacte du CLI sont conservées.

Le contrôle depuis Git impose aussi des fins de ligne LF explicites et ignore le cache local `.mywallpaper/`. La version candidate finale est 0.1.1 ; le tag 0.1.0 est conservé et n’est pas déplacé. Le test de protocole lit la version du manifeste pour suivre les prochaines releases. Les métadonnées Git embarquées restent actives : la comparaison finale porte sur deux checkouts du même tag.

Après revue indépendante, `Closing` gère SC_CLOSE/Alt+F4 comme une annulation de sélection et `CloseOwned` réserve la destruction au dispose. Ces deux chemins restent nécessaires : le premier garde le HWND réutilisable, le second termine son cycle de vie. Le fallback d’icône utilise `LoadIconW`, avec un seul import retenu et ses deux appelants (cartes et tray). La désactivation arrête aussi le polling, et le harness vérifie sa réactivation. Le projet a été reconsidéré sur ces seuls chemins ; aucune suppression issue d’un fan-in nul n’a été faite.

Le nettoyage n’est pas déduit de l’absence d’un élément à l’écran : ses releases sont explicites et les observations sont dans les rapports de tests. Les compteurs ne démontrent pas l’absence de toute fuite .NET ou GPU ; cette portée n’est pas revendiquée.

