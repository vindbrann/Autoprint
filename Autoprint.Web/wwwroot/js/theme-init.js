// Thème sombre mémorisé (clé « user_theme », valeur JSON) appliqué avant le rendu, pour éviter un flash clair (B77).
// Fichier séparé : la politique de sécurité du contenu interdit les scripts en ligne.
(function () {
    try {
        var saved = localStorage.getItem('user_theme');
        if (saved && saved.indexOf('dark') !== -1) document.documentElement.setAttribute('data-theme', 'dark');
    } catch (e) { /* stockage indisponible : thème clair */ }
})();
