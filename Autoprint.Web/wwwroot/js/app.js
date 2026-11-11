// Fonctions JS appelées par Blazor (sorties de index.html pour permettre une CSP sans 'unsafe-inline')

// Thème clair / sombre de la charte (B77) : attribut lu par autoprint-theme.css, posé avec ThemeService.SetTheme
window.autoprintSetTheme = (dark) => {
    if (dark) document.documentElement.setAttribute('data-theme', 'dark');
    else document.documentElement.removeAttribute('data-theme');
};

// Menu principal réduit d'office sur les écrans étroits (charte B77)
window.autoprintIsNarrow = () => window.innerWidth < 1024;

window.setupChart = (id, labels, data) => {
    const ctx = document.getElementById(id);
    if (window.myChart instanceof Chart) {
        window.myChart.destroy();
    }
    window.myChart = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: labels,
            datasets: [{
                data: data,
                backgroundColor: ['#4e73df', '#1cc88a', '#36b9cc', '#f6c23e', '#e74a3b', '#858796'],
                hoverOffset: 4
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: 'right' } }
        }
    });
};

window.downloadFileFromStream = async (fileName, contentStreamReference) => {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer]);
    const url = URL.createObjectURL(blob);
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? '';
    anchorElement.click();
    anchorElement.remove();
    URL.revokeObjectURL(url);
};

// Insère un texte à l'emplacement du curseur d'un champ et renvoie la nouvelle valeur (règle de nommage, Paramètres)
window.apInsertAtCursor = function (id, text) {
    var el = document.getElementById(id);
    if (!el) return text;
    var s = el.selectionStart ?? el.value.length, e = el.selectionEnd ?? s;
    el.value = el.value.slice(0, s) + text + el.value.slice(e);
    el.focus();
    el.setSelectionRange(s + text.length, s + text.length);
    return el.value;
};