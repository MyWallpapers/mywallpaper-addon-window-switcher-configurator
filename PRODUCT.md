# Sélecteur de fenêtres MyWallpaper

Ce fichier décrit le prototype local issu du mandat A09 du 13 octobre 2026. L'autorité produit et architecture reste `../MyWallpaper/docs/decisions/decision-ledger.json` ; aucun choix de lancement n'est ratifié ici.

## Plateforme et usage

Windows x64. Les utilisateurs personnalisent leur sélection de fenêtres depuis les réglages usuels MyWallpaper, sans programmation. Le brief demande des profils très distincts, puis des contrôles de disposition, couleur, matière, forme, densité, navigation et mouvement. Le propriétaire a délégué le choix du profil par défaut ; la recommandation retenue pour ce prototype est Verre boréal.

## Réalisation locale

Companion process-v2 avec interface WPF et API publiques Win32/DWM. Choix réversible d'implémentation locale sous le mandat. Les effets, filtres, styles et profils appartiennent à ce dépôt. L'add-on ne modifie pas le core, le moteur partagé ou le ledger. Les paramètres sont de portée device et restent sous les 100 définitions de DEC-0012.

## Limites à conserver

Task View natif dépend de vues privées non qualifiées. Le prototype utilise les fenêtres du bureau courant. Une cascade de cartes inspirée de Flip 3D est une présentation propre et non la restauration exacte de Vista. Les fenêtres réduites ou protégées peuvent n'offrir qu'une représentation de remplacement. Les preuves de build, fixtures et effet visuel réel sont distinguées.

