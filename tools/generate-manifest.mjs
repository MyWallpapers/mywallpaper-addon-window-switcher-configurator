import { mkdir, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const settings = [];
let currentSection;
const custom = { setting: 'profile', equals: 'custom' };
const section = (id, label, description) => { currentSection = id; settings.push({ id, type: 'section', label, defaultCollapsed: id === 'advancedSection', ...(description ? { description } : {}) }); };
const value = (id, type, label, def, extra = {}) => settings.push({ id, type, label, parent: currentSection, default: def, scope: 'device', ...extra });
const bool = (id, label, def, description) => value(id, 'boolean', label, def, { description });
const range = (id, label, def, min, max, step = 1, extra = {}) => value(id, 'range', label, def, { min, max, step, ...extra });
const select = (id, label, def, pairs, extra = {}) => value(id, 'select', label, def, { options: pairs.map(([value, label]) => ({ value, label })), ...extra });
const color = (id, label, def) => value(id, 'color', label, def, { alpha: true, showIf: custom });

section('startSection', 'Votre sélecteur', 'Choisissez un profil, puis une présentation. Les réglages s’appliquent à cet appareil.');
bool('enabled', 'Activer le sélecteur', true, 'À l’arrêt, le raccourci et tous les aperçus sont libérés.');
select('profile', 'Ambiance visuelle', 'boreal', [['boreal', 'Verre boréal'], ['midnight', 'Minuit'], ['rose', 'Rose électrique'], ['paper', 'Papier'], ['cinema', 'Cinéma'], ['classic', 'Classique'], ['flip', 'Cascade Flip'], ['custom', 'Personnalisée']], { description: 'Les profils fixent la palette et la matière. Personnalisée dévoile les couleurs ; les choix personnels sont conservés quand vous changez de profil.' });
select('layout', 'Présentation', 'profile', [['profile', 'Conseillée par le profil'], ['grid', 'Grille d’aperçus'], ['list', 'Liste détaillée'], ['classic', 'Classique compact'], ['flip', 'Cascade inspirée de Flip 3D'], ['desktop', 'Carte des fenêtres du bureau']], { description: 'La carte reprend la position des fenêtres sur l’écran choisi. La cascade propose un effet de profondeur sans restaurer le composant Vista.' });
select('activationMode', 'Ouvrir le sélecteur', 'shortcut', [['shortcut', 'Raccourci dédié'], ['altTab', 'Alt + Tab et raccourci dédié']], { description: 'Le raccourci dédié laisse le sélecteur ouvert. Avec Alt + Tab, relâchez Alt pour choisir. Win + Tab conserve Task View de Windows.' });
select('shortcut', 'Raccourci dédié', 'ctrlAltSpace', [['ctrlAltSpace', 'Ctrl + Alt + Espace'], ['ctrlShiftSpace', 'Ctrl + Maj + Espace'], ['altBacktick', 'Alt + touche `']], { description: 'Appuyez de nouveau pour annuler. Le raccourci est actif uniquement pendant l’exécution de cet add-on.' });
range('openDelayMs', 'Délai d’apparition (ms)', 120, 0, 600, 10, { description: 'Un Alt + Tab très bref change de fenêtre sans faire apparaître le panneau.' });
bool('fullscreenBypass', 'Garder Alt + Tab Windows en plein écran', true, 'Évite d’intercepter Alt + Tab quand la fenêtre active couvre tout son écran.');

section('layoutSection', 'Disposition et densité');
select('placement', 'Écran d’affichage', 'cursor', [['cursor', 'Sous le pointeur'], ['focused', 'Fenêtre active'], ['primary', 'Écran principal']]);
range('panelWidthPct', 'Largeur maximale (% de l’écran)', 80, 40, 96);
range('panelHeightPct', 'Hauteur maximale (% de l’écran)', 80, 35, 94);
range('columns', 'Colonnes (0 = automatique)', 0, 0, 8);
range('cardWidth', 'Largeur souhaitée des cartes (px)', 280, 120, 480, 10);
select('previewAspect', 'Format des cartes', 'wide', [['wide', 'Paysage 16:10'], ['square', 'Carré'], ['natural', 'Format de la fenêtre sélectionnée']]);
select('density', 'Densité', 'balanced', [['airy', 'Aérée'], ['balanced', 'Équilibrée'], ['compact', 'Compacte']]);
range('gap', 'Espacement des cartes (px)', 16, 4, 40);
range('padding', 'Marge intérieure (px)', 28, 12, 52);
range('cardRadius', 'Coins des cartes (px)', 14, 0, 32);
range('pageSize', 'Cartes par page au maximum', 24, 4, 48);
select('sort', 'Ordre des fenêtres', 'recent', [['recent', 'Dernières utilisées'], ['title', 'Titre'], ['application', 'Application']]);
bool('listPreview', 'Aperçus dans la liste', true, 'Sans aperçu, davantage de fenêtres tiennent dans la liste.');
bool('groupApplications', 'Rassembler les fenêtres de la même application', false, 'Les fenêtres restent sélectionnables séparément.');

section('filterSection', 'Fenêtres proposées');
select('windowScreen', 'Fenêtres de quels écrans ?', 'all', [['all', 'Tous les écrans'], ['cursor', 'Écran du pointeur'], ['focused', 'Écran de la fenêtre active']]);
select('minimized', 'Fenêtres réduites', 'include', [['include', 'Inclure'], ['exclude', 'Masquer'], ['only', 'Seulement les fenêtres réduites']]);
value('includeApplications', 'textarea', 'Montrer uniquement ces applications', '', { description: 'Facultatif : noms des exécutables, un par ligne, par exemple notepad.exe. Laissez vide pour toutes les applications.', placeholder: 'notepad.exe\nexplorer.exe' });
value('excludeApplications', 'textarea', 'Applications à masquer', '', { description: 'Facultatif : noms des exécutables, un par ligne. Aucun code ni sélecteur nécessaire.', placeholder: 'exemple.exe' });
bool('hideGhosts', 'Masquer les fenêtres fantômes', true, 'Écarte les fenêtres nommées Ghost et les fenêtres minuscules qui ne peuvent pas servir d’aperçu.');
range('minWindowSize', 'Seuil des fenêtres minuscules (px)', 48, 0, 200, 4, { description: 'Masque les fenêtres dont les deux dimensions sont inférieures à ce seuil.', showIf: { setting: 'hideGhosts', equals: true } });
bool('hideUntitled', 'Masquer les fenêtres sans titre', true, 'Les fenêtres sans titre sont souvent des utilitaires invisibles.');
bool('hideHung', 'Masquer les applications qui ne répondent pas', false, 'Ce filtre ne ferme pas et ne force pas l’arrêt de l’application.');

section('materialSection', 'Fond et lumière', 'Choisissez l’ambiance Personnalisée pour éditer la palette. Les fonds verre utilisent la matière Windows quand elle est disponible, sinon un fond uni lisible.');
select('material', 'Fond', 'glass', [['solid', 'Uni'], ['gradient', 'Dégradé'], ['glass', 'Verre Windows']], { showIf: custom });
color('surfaceColor', 'Couleur de départ', '#102A36FF');
color('gradientEnd', 'Couleur d’arrivée', '#153E48FF');
range('gradientAngle', 'Direction du dégradé (degrés)', 30, 0, 360, 5, { showIf: custom });
range('surfaceOpacity', 'Opacité du fond (%)', 86, 20, 100, 1, { showIf: custom });
range('desktopDim', 'Assombrir le bureau (%)', 30, 0, 75);
range('panelRadius', 'Coins du panneau (px)', 22, 0, 48);
range('panelBorderWidth', 'Contour du panneau (px)', 0, 0, 4, 0.5);
color('panelBorderColor', 'Couleur du contour', '#66D9DB80');
range('shadowOpacity', 'Intensité de l’ombre (%)', 35, 0, 75);
range('shadowBlur', 'Douceur de l’ombre (px)', 36, 0, 80, 2);
range('shadowDistance', 'Décalage de l’ombre (px)', 12, 0, 28);

section('cardSection', 'Cartes et sélection');
color('cardColor', 'Fond des cartes', '#173846FF');
color('selectedColor', 'Fond de la carte sélectionnée', '#215B66FF');
color('hoverColor', 'Fond au survol', '#24505CFF');
color('textColor', 'Texte principal', '#F0FFFFFF');
color('mutedColor', 'Texte secondaire', '#BCDAE0FF');
color('accentColor', 'Accent de sélection', '#71F5DBFF');
range('previewOpacity', 'Opacité des aperçus (%)', 100, 40, 100);
select('previewFit', 'Cadrage de l’aperçu', 'contain', [['contain', 'Fenêtre entière'], ['cover', 'Remplir la carte']]);
bool('previewClientOnly', 'Aperçu sans la barre de titre', false, 'Demande à DWM de ne montrer que le contenu de la fenêtre.');
bool('showIcon', 'Icône de l’application', true, 'L’icône réelle de la fenêtre apparaît si Windows la fournit.');
bool('showTitle', 'Titre de la fenêtre', true, 'Un titre abrégé reste disponible pour l’accessibilité.');
bool('showApplication', 'Nom de l’application', true, 'Affiche le nom de l’exécutable sans son extension.');
bool('showScreen', 'Numéro d’écran', false, 'Utile quand vous recherchez parmi plusieurs écrans.');
bool('showMinimizedBadge', 'Indiquer les fenêtres réduites', true, 'Les fenêtres réduites affichent une représentation de remplacement quand l’aperçu n’est pas disponible.');
range('cardBorderWidth', 'Contour des cartes (px)', 0, 0, 3, 0.5);
range('selectionBorderWidth', 'Contour de sélection (px)', 3, 1, 6, 0.5);
range('inactiveOpacity', 'Opacité des cartes non sélectionnées (%)', 90, 35, 100);

section('typeSection', 'Lisibilité');
select('fontFamily', 'Police', 'system', [['system', 'Segoe UI Variable'], ['segoe', 'Segoe UI'], ['arial', 'Arial'], ['consolas', 'Consolas']]);
range('titleFontSize', 'Taille des titres (px)', 15, 12, 24);
range('appFontSize', 'Taille du texte secondaire (px)', 12, 10, 18);
select('titleWeight', 'Épaisseur des titres', 'semibold', [['normal', 'Normale'], ['medium', 'Moyenne'], ['semibold', 'Semi-grasse']]);

section('navigationSection', 'Navigation et actions');
bool('wrapNavigation', 'Revenir au début après la dernière fenêtre', true, 'S’applique à Tab, aux flèches et à la molette.');
bool('hoverSelect', 'Sélectionner au survol', false, 'Le clic choisit toujours la fenêtre.');
bool('mouseWheel', 'Parcourir avec la molette', true, 'Une encoche sélectionne la carte suivante ou précédente.');
bool('showClose', 'Bouton Fermer sur les cartes', true, 'Demande une fermeture normale ; l’application peut demander d’enregistrer.');
bool('closeConfirm', 'Confirmer avant de fermer', true, 'La confirmation fonctionne aussi avec Suppr.');
bool('searchEnabled', 'Rechercher par titre ou application', true, 'Dans le sélecteur ouvert par raccourci, commencez à saisir pour filtrer.');
bool('dismissOutside', 'Annuler en cliquant hors du panneau', true, 'L’application précédemment active retrouve le focus.');
bool('dismissOnFocusLoss', 'Annuler quand une autre application prend le focus', true, 'Ne garde pas le sélecteur au-dessus d’une autre action.');
bool('showHints', 'Afficher les commandes clavier', true, 'Tab et flèches : parcourir ; Entrée : choisir ; Échap : annuler ; Suppr : fermer.');

section('motionSection', 'Mouvements');
select('animationMode', 'Ouverture du panneau', 'slide', [['none', 'Immédiate'], ['fade', 'Fondu'], ['slide', 'Glissement doux'], ['spring', 'Ressort léger']]);
range('animationDurationMs', 'Durée d’ouverture (ms)', 190, 0, 700, 10);
range('selectionDurationMs', 'Transition de sélection (ms)', 120, 0, 400, 10);
range('hoverScale', 'Agrandissement au survol', 1.03, 1, 1.10, 0.01);
range('hoverLift', 'Soulèvement au survol (px)', 3, 0, 12);
range('flipDepth', 'Écart de profondeur en cascade (px)', 44, 20, 90, 2, { showIf: { setting: 'layout', equals: 'flip' } });
range('flipVisible', 'Cartes visibles dans la cascade', 5, 3, 9, 1, { showIf: { setting: 'layout', equals: 'flip' } });
bool('reduceMotion', 'Réduire les animations', false, 'Coupe l’entrée, le survol animé et les transitions.');
bool('followWindowsMotion', 'Respecter la réduction d’animation de Windows', true, 'Suit le réglage d’accessibilité Windows.');

section('advancedSection', 'Performance — facultatif');
range('previewBudget', 'Aperçus simultanés au maximum', 24, 4, 48, 1, { description: 'Les autres cartes gardent titre et icône. Aucun contenu de fenêtre n’est enregistré sur disque.' });
range('refreshMs', 'Actualiser la liste toutes les (ms)', 1000, 500, 5000, 100);
range('maxCandidates', 'Fenêtres examinées au maximum', 160, 16, 240, 8);

if (settings.length > 100) throw new Error(`Manifest settings: ${settings.length} exceeds DEC-0012`);
const manifest = { runtime: 'native-v1', name: 'Sélecteur de fenêtres', description: 'Un sélecteur personnalisable : grille, liste, classique, cascade et carte du bureau, avec aperçus DWM, profils et navigation clavier.', version: '0.1.2', deviceSettingsVersion: '1', settings, native: { companion: { runtime: 'process-v2', entries: { 'windows-x86_64': 'native/out/windows-x86_64/companion.exe' } } } };

// Profiles own visual intent only. Interaction and the user's geometry remain independent.
const profiles = {
  boreal: { name: 'Verre boréal', layout: 'grid', material: 'glass', surfaceColor: '#102A36FF', gradientEnd: '#153E48FF', surfaceOpacity: 86, cardColor: '#173846FF', selectedColor: '#215B66FF', hoverColor: '#24505CFF', textColor: '#F0FFFFFF', mutedColor: '#BCDAE0FF', accentColor: '#71F5DBFF', panelBorderColor: '#66D9DB80' },
  midnight: { name: 'Minuit', layout: 'grid', material: 'solid', surfaceColor: '#101826FF', gradientEnd: '#152439FF', surfaceOpacity: 100, cardColor: '#1B293FFF', selectedColor: '#284569FF', hoverColor: '#273954FF', textColor: '#F4F7FFFF', mutedColor: '#BBC9E2FF', accentColor: '#9EC5FFFF', panelBorderColor: '#456088FF' },
  rose: { name: 'Rose électrique', layout: 'grid', material: 'gradient', surfaceColor: '#36172DFF', gradientEnd: '#591F41FF', gradientAngle: 35, surfaceOpacity: 100, cardColor: '#54243FFF', selectedColor: '#7C335BFF', hoverColor: '#69304FFF', textColor: '#FFF4FAFF', mutedColor: '#F3BCD5FF', accentColor: '#FFB8DDFF', panelBorderColor: '#AD547FFF' },
  paper: { name: 'Papier', layout: 'list', material: 'solid', surfaceColor: '#F7F3E9FF', gradientEnd: '#EFE8D9FF', surfaceOpacity: 100, cardColor: '#EEE6D6FF', selectedColor: '#DFD3BAFF', hoverColor: '#E7DCC7FF', textColor: '#27251FFF', mutedColor: '#615847FF', accentColor: '#735529FF', panelBorderColor: '#C1AD89FF' },
  cinema: { name: 'Cinéma', layout: 'grid', material: 'gradient', surfaceColor: '#101011FF', gradientEnd: '#272017FF', gradientAngle: 90, surfaceOpacity: 100, cardColor: '#25221CFF', selectedColor: '#453820FF', hoverColor: '#393022FF', textColor: '#FFF9EAFF', mutedColor: '#D0BE96FF', accentColor: '#F4CF7BFF', panelBorderColor: '#806E49FF' },
  classic: { name: 'Classique', layout: 'classic', material: 'solid', surfaceColor: '#232323FF', gradientEnd: '#232323FF', surfaceOpacity: 100, cardColor: '#303030FF', selectedColor: '#1F4E7CFF', hoverColor: '#424242FF', textColor: '#FFFFFFFF', mutedColor: '#D2D2D2FF', accentColor: '#A5D6FFFF', panelBorderColor: '#696969FF' },
  flip: { name: 'Cascade Flip', layout: 'flip', material: 'gradient', surfaceColor: '#141C33FF', gradientEnd: '#2B254AFF', gradientAngle: 145, surfaceOpacity: 100, cardColor: '#263252FF', selectedColor: '#3B4B79FF', hoverColor: '#3A3F65FF', textColor: '#F5F0FFFF', mutedColor: '#CCC5E9FF', accentColor: '#C3B2FFFF', panelBorderColor: '#7C73ADFF' }
};
await mkdir(resolve(root, 'profiles'), { recursive: true });
for (const [id, profile] of Object.entries(profiles)) await writeFile(resolve(root, 'profiles', `${id}.json`), JSON.stringify({ schemaVersion: 1, id, ...profile }, null, 2) + '\n');
const defaults = Object.fromEntries(settings.filter(s => s.type !== 'section').map(s => [s.id, s.default]));
manifest.presets = Object.entries(profiles).map(([id, profile]) => ({ id, name: profile.name, settings: {
  ...defaults,
  ...Object.fromEntries(Object.entries(profile).filter(([key]) => key !== 'layout' && Object.hasOwn(defaults, key))),
  profile: id
} }));
await writeFile(resolve(root, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
console.log(`${settings.length} definitions, ${settings.filter(s => s.type !== 'section').length} values, ${Object.keys(profiles).length} profiles`);

