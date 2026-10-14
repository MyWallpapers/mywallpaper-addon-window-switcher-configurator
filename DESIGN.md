---
name: Sélecteur de fenêtres MyWallpaper
description: Native Windows WPF companion tokens captured from the local A09 implementation.
colors:
  # Primary / accents
  accent-boreal: "#71F5DBFF"
  accent-midnight: "#9EC5FFFF"
  accent-rose: "#FFB8DDFF"
  accent-paper: "#735529FF"
  accent-cinema: "#F4CF7BFF"
  accent-classic: "#A5D6FFFF"
  accent-flip: "#C3B2FFFF"
  # Selection and hover
  selected-boreal: "#215B66FF"
  selected-midnight: "#284569FF"
  selected-rose: "#7C335BFF"
  selected-paper: "#DFD3BAFF"
  selected-cinema: "#453820FF"
  selected-classic: "#1F4E7CFF"
  selected-flip: "#3B4B79FF"
  hover-boreal: "#24505CFF"
  hover-midnight: "#273954FF"
  hover-rose: "#69304FFF"
  hover-paper: "#E7DCC7FF"
  hover-cinema: "#393022FF"
  hover-classic: "#424242FF"
  hover-flip: "#3A3F65FF"
  # Surfaces
  surface-boreal: "#102A36FF"
  surface-midnight: "#101826FF"
  surface-rose: "#36172DFF"
  surface-paper: "#F7F3E9FF"
  surface-cinema: "#101011FF"
  surface-classic: "#232323FF"
  surface-flip: "#141C33FF"
  gradient-end-boreal: "#153E48FF"
  gradient-end-midnight: "#152439FF"
  gradient-end-rose: "#591F41FF"
  gradient-end-paper: "#EFE8D9FF"
  gradient-end-cinema: "#272017FF"
  gradient-end-classic: "#232323FF"
  gradient-end-flip: "#2B254AFF"
  card-boreal: "#173846FF"
  card-midnight: "#1B293FFF"
  card-rose: "#54243FFF"
  card-paper: "#EEE6D6FF"
  card-cinema: "#25221CFF"
  card-classic: "#303030FF"
  card-flip: "#263252FF"
  # Foregrounds
  text-boreal: "#F0FFFFFF"
  text-midnight: "#F4F7FFFF"
  text-rose: "#FFF4FAFF"
  text-paper: "#27251FFF"
  text-cinema: "#FFF9EAFF"
  text-classic: "#FFFFFFFF"
  text-flip: "#F5F0FFFF"
  muted-boreal: "#BCDAE0FF"
  muted-midnight: "#BBC9E2FF"
  muted-rose: "#F3BCD5FF"
  muted-paper: "#615847FF"
  muted-cinema: "#D0BE96FF"
  muted-classic: "#D2D2D2FF"
  muted-flip: "#CCC5E9FF"
  # Borders
  border-boreal: "#66D9DB80"
  border-midnight: "#456088FF"
  border-rose: "#AD547FFF"
  border-paper: "#C1AD89FF"
  border-cinema: "#806E49FF"
  border-classic: "#696969FF"
  border-flip: "#7C73ADFF"
typography:
  heading:
    fontFamily: "Segoe UI Variable"
    fontSize: "24px"
    fontWeight: 600
  window-title:
    fontFamily: "Segoe UI Variable"
    fontSize: "15px"
    fontWeight: 600
  supporting:
    fontFamily: "Segoe UI Variable"
    fontSize: "12px"
    fontWeight: 400
  fallback-label:
    fontFamily: "Segoe UI Variable"
    fontSize: "11px"
    fontWeight: 400
rounded:
  preview: "6px"
  card: "14px"
  panel: "22px"
spacing:
  card-inset: "8px"
  grid-gap: "16px"
  panel-padding: "28px"
  search-padding: "8px 12px"
components:
  companion-panel:
    backgroundColor: "{colors.surface-boreal}"
    textColor: "{colors.text-boreal}"
    rounded: "{rounded.panel}"
    padding: "{spacing.panel-padding}"
  window-card:
    backgroundColor: "{colors.card-boreal}"
    textColor: "{colors.text-boreal}"
    typography: "{typography.window-title}"
    rounded: "{rounded.card}"
    padding: "{spacing.card-inset}"
  selected-window-card:
    backgroundColor: "{colors.selected-boreal}"
    textColor: "{colors.text-boreal}"
    rounded: "{rounded.card}"
  hovered-window-card:
    backgroundColor: "{colors.hover-boreal}"
    textColor: "{colors.text-boreal}"
    rounded: "{rounded.card}"
  preview-frame:
    backgroundColor: "{colors.surface-boreal}"
    rounded: "{rounded.preview}"
  search-field:
    backgroundColor: "{colors.card-boreal}"
    textColor: "{colors.text-boreal}"
    typography: "{typography.window-title}"
    padding: "{spacing.search-padding}"
---

# Design System: Sélecteur de fenêtres MyWallpaper

## Overview

**Creative North Star: "Choix immédiat, surface lisible"**

Cette fiche consigne le rendu réellement présent dans le prototype local A09. Elle décrit le companion Windows x64 natif WPF et les valeurs visuelles du manifeste et des profils ; elle ne crée aucune décision produit, architecture, lancement ou compatibilité. Le manifeste sélectionne Verre boréal comme défaut de cette réalisation locale, conformément à la recommandation déléguée.

Les réglages restent dans l'expérience habituelle de hostMyWallpaper, avec les sections, sélecteurs, plages et interrupteurs définis par le manifeste. Le companion séparé présente un titre et le nombre de fenêtres, une recherche facultative, des cartes lisibles, des aperçus DWM et des indications clavier. Son interface est WPF native, sans HTML/CSS. Les tokens décrivent le rendu du companion, pas une nouvelle marque durable.

Aucune capture de qualification ne sert de verdict de lancement ni de preuve de compatibilité multi-écran.

**Key Characteristics:**

- Le manifeste choisit Verre boréal, grille et matière vitrée comme valeurs initiales.
- Sept profils coordonnent surfaces, cartes, sélection, survol, texte, accent et contour.
- Le companion utilise les primitives WPF et DWM sur Windows x64.
- La cascade Flip expose une profondeur 2D avec un aperçu DWM vivant sur la carte avant.
Les sources de valeurs sont [manifest.json](manifest.json) et [profiles/](profiles/) ; le rendu courant est dans [SwitcherWindow.cs](src/Switcher.Windows/SwitcherWindow.cs) et [Layout.cs](src/Switcher.Core/Layout.cs).

**The Readable Selection Rule.** Keep the first view centered on the window title and count, optional search, readable cards, and keyboard hints; the settings remain in the hostMyWallpaper settings experience.

## Colors

Les clés de couleur du frontmatter conservent les valeurs exactes des neuf rôles fournis par chaque profil. La palette active s'applique en ensemble : surface, fin de dégradé, carte, sélection, survol, texte, texte secondaire, accent et contour. Les noms et rôles de profil restent ceux de la réalisation locale.

Verre boréal est l'état initial, avec base encre et turquoise lumineux sur matière vitrée Windows. Minuit garde une surface bleu nuit opaque et une sélection bleue claire. Papier demande le thème clair de DWM ; les autres profils demandent le thème sombre. Rose électrique et Cinéma utilisent des dégradés prune-rose et charbon-ambre. Papier inverse la luminance avec une base crème et un accent brun. Classique utilise une surface charbon unie sans rangée d'aperçu. Cascade Flip associe indigo et violet à l'empilement de cartes.

**The Profile Palette Rule.** Use one active profile as a coordinated surface, card, selected, hover, text, muted, accent, and border set. Read the canonical manifest and profile values as #RRGGBBAA, with alpha last.

## Typography

Segoe UI Variable est la valeur système native du companion. La hiérarchie par défaut est 24 DIP pour l'en-tête, 15 DIP pour le titre de fenêtre et la recherche, puis 12 DIP pour les informations secondaires. Les libellés de secours d'aperçu sont à 11 DIP. Le titre de fenêtre reste réglable de 12 à 24 DIP et les informations secondaires de 10 à 18 DIP ; le manifeste autorise aussi Segoe UI, Arial et Consolas.

**The Native Type Ramp Rule.** Use the 24/15/12 WPF DIP hierarchy by default with Segoe UI Variable; the smaller 11 DIP fallback label is for preview status only. Keep the manifest's font alternatives and size ranges user-controlled.

## Layout

Le panneau est centré sur l'écran choisi. Les pourcentages de largeur et hauteur bornent la place disponible dans la zone de travail ; ils ne forcent pas le contenu à occuper toute cette surface. Le profil initial conseille une grille, une largeur de carte de 280 DIP, un rapport de prévisualisation paysage 16:10, un espacement de 16 DIP, un remplissage intérieur de 28 DIP et une densité équilibrée. Un nombre de colonnes à zéro laisse le calcul s'adapter à l'écran ; la pagination affiche jusqu'à 24 fenêtres.

La liste réserve une colonne d'aperçu facultative. Classique privilégie les icônes et les titres enveloppés sur deux lignes, sans rangée ni rectangle d'aperçu vide. La cascade décale les cartes en 2D ; la carte spatiale reporte les positions observées des fenêtres dans une carte temporaire.

**The Work-Area Rule.** Fit the grid and page count to the selected monitor's available work area, using the configured width and height as layout bounds.

## Elevation & Depth

La profondeur combine les matériaux Windows et deux fenêtres compagnons séparées. Le verre demande le backdrop DWM ; en cas de refus, le fond de surface sert de backing opaque. Uni et dégradé restent disponibles comme matières réglables. La surface vitrée commence à 86 % d'opacité dans Verre boréal.

L'ombre noire est rendue par une fenêtre WPF non interactive distincte : opacité 35 %, flou 36 DIP, distance 12 DIP, direction 270°. Un voile noir indépendant assombrit les limites de l'écran choisi à 30 % par défaut et disparaît lorsque sa valeur est zéro.

**The Glass Fallback Rule.** Request the Windows DWM glass backdrop for the glass material and switch to an opaque surface-color backing when DWM declines it.

## Shapes

Le panneau est arrondi à 22 DIP et les cartes à 14 DIP ; les cadres d'aperçu utilisent 6 DIP. Le coin du panneau est appliqué au backdrop DWM et à la région native de la fenêtre. La bordure du panneau et celle des cartes sont nulles par défaut ; l'accent apparaît sur la bordure de sélection.

L'entrée de recherche reste un TextBox WPF natif avec remplissage vertical/horizontal de 8/12 DIP. Le code ne lui impose pas de rayon personnalisé.

**The Shared Radius Rule.** Keep the implemented silhouette at 22 DIP for the panel, 14 DIP for cards, and 6 DIP for preview frames; round the native window region with the panel.

## Components

### Companion panel

Le compagnon utilise une fenêtre WPF sans cadre, centrée et au premier plan. L'en-tête fixe affiche « Vos fenêtres » et le compteur ; le champ de recherche et la ligne d'aide au clavier peuvent être masqués selon le mode ou les réglages.

### Window cards

Chaque carte est un bouton WPF de 280 DIP par défaut avec un retrait de contenu de 8 DIP. À l'état normal, elle utilise la couleur de carte et l'opacité inactive de 90 %. La sélection passe à la couleur sélectionnée, reçoit une bordure accentuée de 3 DIP par défaut et revient à 100 % d'opacité. Le survol change le fond et, si le mouvement est autorisé, agrandit la carte à 1,03 et la remonte de 3 DIP. Le survol ne choisit pas la fenêtre par défaut.

### Preview

Les aperçus sont des miniatures DWM réelles, limitées par le budget et par les cartes qui ont un cadre d'aperçu. Une fenêtre réduite, occupée ou sans miniature disponible reçoit un libellé de remplacement. Certaines fenêtres protégées peuvent encore retourner un aperçu vide selon Windows. Dans la cascade, seule la carte avant porte l'aperçu DWM vivant.

### Search field and close action

La recherche est un TextBox WPF avec fond de carte, texte de premier plan, caret et bordure d'accent. Le bouton de fermeture de carte est compact (26 × 26 DIP) et utilise une croix simple. Une fermeture de fenêtre de sélection déclenche l'annulation de la sélection courante via SC_CLOSE/Alt+F4 ; le HWND compagnon reste réutilisable.

### Host settings

Conserver les profils et leurs options dans l'UI réglages habituelle de hostMyWallpaper, selon son expérience taskbar. Le manifeste exprime les choix sous forme de sections groupées, select, range et boolean ; le companion ne devient pas une page de configuration.

### Motion

L’ouverture propose none, fade, slide et spring. Le mode par défaut fait un fondu de 0,35 à 1 et une translation verticale de 16 à 0 DIP en 190 ms, avec ExponentialEase exponent 5. Fade retire la translation ; spring utilise BackEase amplitude 0,3. none, une durée d’ouverture nulle ou la réduction de mouvement coupe l’entrée. Quand le réglage de suivi Windows est actif, les animations système désactivées coupent aussi les effets. Le changement de sélection est immédiat ; seul l’en-tête pulse de 0,75 à 1 sur 120 ms. Le survol anime l’échelle et le déplacement indiqués ci-dessus.
**The Selection Contrast Rule.** Make selection legible with selectedColor, an accentColor border at the configured 3 DIP default, and full opacity against the 90% default opacity of inactive cards.

**The DWM Preview Truth Rule.** Show live DWM thumbnails only where the layout grants a preview; retain fallback labels for unavailable or minimized previews, and keep the cascade's live thumbnail on the front card.

## Do's and Don'ts

### Do:

- Do derive companion colors from the active manifest profile and keep the alpha byte last in #RRGGBBAA values.
- Do keep the MyWallpaper settings flow in hostMyWallpaper and use its existing native grouped controls.
- Do retain opaque material fallback, preview fallbacks, and reduced-motion handling where the current implementation provides them.
- Do treat the selected profile and tokens as a snapshot of this local realization, with the ledger remaining the only decision authority.

### Don't:

- Don't create an HTML/CSS version of the WPF companion or move host settings into a second settings surface.
- Don't describe the Flip cascade as a real 3D projection or as native Windows Task View.
- Don't treat qualification captures as a launch decision or proof of multi-monitor compatibility.
