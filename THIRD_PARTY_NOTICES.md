# Provenance et licences

Le code de ce configurateur est une implémentation originale sous MIT. La référence taskbar a fourni la direction d’intégration et de réglages ; ses sources C++ ne sont pas incorporées. Aucun code de mod Windhawk, aucune DLL Windhawk et aucun moteur injecté ne sont incorporés.

La lecture de mods sert de preuve technique, pas de portage textuel. `desktop-window-cards` déclare GPL-3.0-only : sa stratégie est étudiée, son code n’est pas réutilisé. Les licences observées et empreintes figurent dans `docs/source-evidence.json`. Le ledger courant DEC-0160 impose le retrait de Windhawk et réserve le moteur propre et son SDK interne sous licence propriétaire. Ce bundle ne reprend ni code, ni SDK interne, ni binaire de ces moteurs ; le SDK/CLI public MyWallpaper reste distinct sous MIT selon DEC-0098.

Le bundle autonome redistribue .NET 8, WPF et les composants du runtime Microsoft. Il conserve `DOTNET-LICENSE.txt` et `DOTNET-ThirdPartyNotices.txt` provenant du SDK utilisé. Les notices de composants restent applicables ; la licence de cet add-on ne les remplace pas.

Les API Win32 et DWM sont fournies par Windows. Aucun binaire Windows n’est copié dans le dépôt. Les captures de qualification montrent des fenêtres de test réellement exécutées ; elles ne constituent pas des ressources du produit et sont exclues du bundle.

