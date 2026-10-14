# Extension native à qualifier

Autorité : DEC-0012/0013 (frontière générique), DEC-0157 (périmètre de lancement inchangé) et DEC-0160 (retrait de Windhawk ; moteur et SDK interne propriétaires). Cette fiche décrit un besoin du prototype ; elle ne ratifie ni une API partagée ni une personnalisation native de Task View. Aucun SDK interne du moteur n’est lié ou redistribué par ce dépôt.

L’ABI v1 courante de `mywallpaper-native-engine/include/mw_native_sdk.h` fournit `resolve_export`, la création et le cycle de vie des hooks. Les fonctions privées Alt+Tab du catalogue ne sont pas des exports. Un hook à partir d’une adresse ne démontre pas comment cette adresse a été obtenue.

Le contrat technique minimal proposé est une **résolution asynchrone de symboles d’un module exactement identifié**. Le schéma de requête est [private-shell-symbols.v1.schema.json](../contracts/private-shell-symbols.v1.schema.json). La requête associe SHA-256 du DLL, timestamp PE, architecture, GUID/âge du PDB, nom de PDB et noms de symboles exacts. Elle est bornée à seize symboles et cinq secondes. La préparation s’effectue hors callback injecté ; aucun téléchargement ou RPC synchrone n’entre dans un détournement.

Une réponse utilisable devra donner, pour chaque nom exact, un RVA et l’identité vérifiée du module/PDB, ou une erreur explicite : module différent, PDB absent/différent, symbole absent/ambigu, RVA hors section exécutable, délai ou annulation. Les RVA sont relatifs au module et doivent être revalidés dans le processus cible. Pas d’adresse absolue inter-processus, de recherche approximative silencieuse ni de supposition qu’une même signature vaut pour toutes les builds. Le fournisseur doit préciser provenance, cache et limites de licence du PDB utilisé.

Le nom du module et les symboles sont des données de l’appelant, pas des choix métier du moteur. L’exemple A09 utilise `twinui.pcshell.dll` ; une primitive commune conserve ce mécanisme techniquement générique et ne possède aucun profil ou comportement Alt+Tab/Task View.

La preuve d’usage est concrète mais limitée : `tools/probe-shell-module.mjs` lit le PE **installé sur le host Windows**, extrait son CodeView RSDS et produit une requête correspondant au symbole Alt+Tab étudié. Il ne résout aucun symbole et ne modifie pas le DLL. `tools/test-companion.mjs` vérifie que la commande process-v2 `probeTaskView` répond `available:false`, avec `private-shell-symbols-unavailable`. Le sélecteur indépendant poursuit son fonctionnement.

Task View demande ensuite sa propre preuve : hosts réellement concernés, méthodes/objets de présentation, version Windows, coexistence avec les consommateurs XAML, drainage et restauration. Le symbole Alt+Tab du contrat d’exemple ne certifie pas un contrôle de Task View. Aucun adaptateur dormant n’appelle une adresse supposée et aucun réglage ne présente Task View comme disponible.

